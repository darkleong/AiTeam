using System.Text.Json;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Orchestration.Hitl;
using AiTeam.Bot.Services;
using AiTeam.Bot.Workflows.Kickoff;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using AiTeam.Shared.ViewModels;
using Discord;
using Discord.WebSocket;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Orchestration.Meeting;

/// <summary>
/// Stage 50：MS Agent Framework Kickoff Meeting Workflow 路由層（feature flag true 時接管 Kick-off 會議）。
///
/// 設計（路線 A2 fan-out/fan-in 拍板，對齊 Stage 49 FrameworkAppealRouter pattern 但單一 entry）：
///   - 從 group 設定 framework KickoffState 起始狀態
///   - 標 ActiveOrchestration = "FrameworkKickoff"（雙 marker：與 KickoffFrameworkStateJson 搭配，區隔 legacy/framework Crash Recovery）
///   - InProcessExecution.RunAsync(workflow, initialState, checkpointManager, sessionId, ct) 跑 framework Workflow
///   - 拿 WorkflowOutputEvent 取得 KickoffLoopResult → 把結果寫進既有 DB 欄位（task_groups.KickoffMeetingLog / TaskPlan / KickoffRound）
///   - Discord embed + 3 buttons + RegisterKickoffConfirmation + InteractionService.CreateInteractionAsync（對齊 legacy 行為）
///
/// Stage 50 不動的 legacy method：
///   - KickoffMeetingService.ModifyTaskPlanAsync 沿用既有（C2 拍板：Petra session_id = group.Id 仍可 resume，feature flag 開關不影響）
///   - HandleKickoffConfirmedAsync（Christ 按鈕路由）走 legacy MeetingOrchestrationService（C2 拍板）
///   - BossInteraction 沿用既有手刻 path（Stage 51 才動 framework Human-in-the-Loop）
///
/// fallback 拍板：framework Workflow 跑失敗 → 不 fallback to legacy（避免 Petra session 雙重佔用），改發 Discord error + 標 group failed。
/// </summary>
public class FrameworkKickoffRouter(
    IServiceProvider serviceProvider,
    KickoffWorkflowFactory workflowFactory,
    KickoffCheckpointStore checkpointStore,
    DiscordSocketClient discordClient,
    IOptions<DiscordSettings> discordSettings,
    IOptions<GitHubSettings> gitHubSettings,
    GitHubService gitHubService,
    InteractionService interactionService,
    WorkflowSettingsResolver workflowResolver,
    ILogger<FrameworkKickoffRouter> logger)
{
    private readonly DiscordSettings _discord = discordSettings.Value;
    private readonly GitHubSettings _gitHub  = gitHubSettings.Value;

    /// <summary>
    /// Stage 50：framework path 對應 MeetingOrchestrationService.RunKickoffMeetingAndWaitAsync。
    /// 由 MeetingOrchestrationService 入口分流（feature flag true 時呼叫此 method）。
    ///
    /// Stage 55A（議題 G3 解法）：加 skipFinalize 參數 + 改回傳 KickoffMeetingOutcome？
    ///   - skipFinalize=false（預設，legacy 路徑）：跑完 Workflow 後 call CreateKickoffConfirmationAsync 開 BossInteraction（行為不變）
    ///   - skipFinalize=true（Pipeline 路徑）：跑完 Workflow + 寫 DB 但 skip CreateKickoffConfirmationAsync — Pipeline KickoffStageExecutor 自己 call CreateKickoffConfirmationAsync 接管 finalize
    /// 回傳 KickoffMeetingOutcome：success path 含 LoopResult + KickoffTaskId（Pipeline 用）；yield/failure 回 null。
    /// </summary>
    public async Task<KickoffMeetingOutcome?> HandleKickoffMeetingAsync(
        TaskGroup group, CancellationToken ct, bool skipFinalize = false)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var owner = _gitHub.Owner;
        var repo  = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;

        // ── ActiveOrchestration 雙 marker（與 KickoffFrameworkStateJson 搭配，區隔 legacy/framework Crash Recovery）──
        await db.TaskGroups.Where(g => g.Id == group.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, "FrameworkKickoff"),
                CancellationToken.None);

        logger.LogInformation("[Stage50] HandleKickoffMeetingAsync framework path 接管（Group={Id}）", group.Id);

        // ── 建 kickoffTask + Dashboard status push（對齊 legacy line 73-101）──
        var projectId = string.IsNullOrWhiteSpace(group.Project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(group.Project, ct);

        var kickoffTask = new TaskItem
        {
            Title         = $"[Kickoff] {group.Title}",
            Description   = "Kick-off 多 Agent 會議（framework path）",
            TriggeredBy   = "Orchestrator",
            AssignedAgent = AgentNames.Kickoff,
            Status        = "running",
            GroupId       = group.Id,
            ProjectId     = projectId,
        };
        taskRepo.Add(kickoffTask);
        await taskRepo.SaveAsync(ct);
        taskRepo.AddLog(new TaskLog
        {
            TaskId = kickoffTask.Id,
            Agent  = AgentNames.Kickoff,
            Step   = "Kick-off 多 Agent 會議進行中（framework path）...",
            Status = "running"
        });
        await taskRepo.SaveAsync(ct);

        await pushService.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = AgentNames.Pm,
            Status           = "running",
            CurrentTaskTitle = $"Kick-off 會議：{group.Title}（framework path）"
        });
        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = kickoffTask.Id,
            GroupId   = group.Id,
            Title     = kickoffTask.Title,
            AgentName = AgentNames.Kickoff,
            Status    = "running"
        });

        // ── Clone repo（對齊 legacy line 67-77）──
        var workingDir = "";
        try
        {
            var cloneSuffix = "kickoff-" + group.Id.ToString("N")[..8];
            workingDir = gitHubService.CloneOrPull(owner, repo, cloneSuffix);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Stage50] clone repo 失敗，使用 workspace 路徑作為 fallback");
            workingDir = Path.Combine(_gitHub.WorkspacePath, repo);
        }

        // Stage 51：HITL yield 旗標 — true 時 finally 不清 marker / 不 cleanup workspace（保留給 Bridge resume）
        var yieldedForHitl = false;

        try
        {
            // ── 設 framework KickoffState 起始 ──
            var maxRounds       = await workflowResolver.GetKickoffMaxRoundsAsync(ct);
            var sessionId       = group.Id.ToString();
            var proposalContent = MeetingOrchestrationService.BuildKickoffProposalContent(group);

            await checkpointStore.LoadFromDbAsync(group.Id, ct);

            var initialState = new KickoffState
            {
                GroupId         = group.Id,
                Round           = 1,
                MaxRounds       = maxRounds,
                ProposalContent = proposalContent,
                PetraSessionId  = group.Id.ToString(),         // C2 拍板：固定 group.Id 給 Modify resume
                RosaSessionId   = Guid.NewGuid().ToString(),
                DemiSessionId   = Guid.NewGuid().ToString(),
                CodySessionId   = Guid.NewGuid().ToString(),
                QuinnSessionId  = Guid.NewGuid().ToString(),
                WorkingDir      = workingDir,
                Owner           = owner,
                Repo            = repo,
                MeetingLog      = $"# Kick-off 會議紀錄\n\n## 需求說明\n{proposalContent}\n\n",
                KickoffTaskId   = kickoffTask.Id,              // Stage 51：FinishKickoffAsync 從 state 取此 id mark done
            };

            // ── 跑 framework Workflow ──
            var workflow          = workflowFactory.CreateKickoffWorkflow();
            var checkpointManager = workflowFactory.CreateCheckpointManager();

            var (loopResult, yielded) =
                await RunWorkflowAsync(workflow, checkpointManager, sessionId, initialState, ct);

            // Stage 51：HITL yield path — workflow 暫停等 Christ 回應，由 FrameworkHitlBridge.HandleMidInterruptResponseAsync 接手
            // 此處不寫 DB / 不開 confirmation embed / 不 cleanup workspace / 不清 marker（finally 內也守此語意）
            if (yielded)
            {
                yieldedForHitl = true;
                logger.LogInformation(
                    "[Stage51] HandleKickoffMeetingAsync 提早 return — workflow yield for HITL（Group={Id}），等 Christ 回應",
                    group.Id);
                return null;
            }

            if (loopResult is null)
            {
                logger.LogError(
                    "[Stage50] Kickoff Workflow 未產生 KickoffLoopResult（Group={Id}），fallback 失敗處理",
                    group.Id);
                taskRepo.UpdateGroupStatus(group, "failed");
                taskRepo.AddLog(new TaskLog
                {
                    TaskId = kickoffTask.Id,
                    Agent  = AgentNames.Kickoff,
                    Step   = "Kick-off 失敗：framework Workflow 未產生結果",
                    Status = "failed"
                });
                taskRepo.UpdateStatus(kickoffTask, "failed");
                await taskRepo.SaveAsync(ct);
                await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
                {
                    TaskId    = kickoffTask.Id,
                    GroupId   = group.Id,
                    Title     = kickoffTask.Title,
                    AgentName = AgentNames.Kickoff,
                    Status    = "failed"
                });
                await pushService.PushAgentStatusAsync(new AgentStatusViewModel
                {
                    AgentName        = AgentNames.Pm,
                    Status           = "error",
                    CurrentTaskTitle = "Kick-off 失敗：framework Workflow 未產生結果"
                });
                await NotifyKickoffFailureAsync(group, "framework Workflow 未產生結果");
                return null;
            }

            // ── 寫 DB（對齊 legacy line 119-123）──
            var freshGroup = await taskRepo.GetGroupByIdAsync(group.Id, ct);
            if (freshGroup is null)
            {
                logger.LogError("[Stage50] Kick-off 完成後找不到 Group={Id}", group.Id);
                return null;
            }

            freshGroup.KickoffMeetingLog = loopResult.MeetingLog;
            freshGroup.TaskPlan          = loopResult.TaskPlan;
            freshGroup.KickoffRound      = loopResult.TotalRounds;
            await taskRepo.SaveAsync(ct);

            logger.LogInformation(
                "[Stage50] Kick-off 會議記錄已存入 DB（Group={Id}，decision={Decision}，rounds={Rounds}）",
                group.Id, loopResult.Decision, loopResult.TotalRounds);

            // ── Discord embed + 3 buttons + BossInteraction（對齊 legacy line 126-185 + escalate 路徑）──
            // Stage 55A：skipFinalize=true 時 Pipeline KickoffStageExecutor 自己接管 CreateKickoffConfirmationAsync
            if (!skipFinalize)
            {
                await CreateKickoffConfirmationAsync(freshGroup, loopResult, kickoffTask.Id, taskRepo, pushService, ct);
            }
            else
            {
                logger.LogInformation(
                    "[Stage55A] HandleKickoffMeetingAsync skipFinalize=true — Pipeline 接管 finalize（Group={Id}）",
                    group.Id);
            }

            return new KickoffMeetingOutcome(loopResult, kickoffTask.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Stage50] Kick-off Workflow 失敗（Group={Id}）", group.Id);

            taskRepo.AddLog(new TaskLog
            {
                TaskId = kickoffTask.Id,
                Agent  = AgentNames.Kickoff,
                Step   = $"Kick-off 失敗（framework path）：{ex.Message}",
                Status = "failed"
            });
            taskRepo.UpdateStatus(kickoffTask, "failed");
            await taskRepo.SaveAsync(CancellationToken.None);
            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = kickoffTask.Id,
                GroupId   = group.Id,
                Title     = kickoffTask.Title,
                AgentName = AgentNames.Kickoff,
                Status    = "failed"
            });
            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = AgentNames.Pm,
                Status           = "error",
                CurrentTaskTitle = $"Kick-off 失敗：{ex.Message}"
            });
            await NotifyKickoffFailureAsync(group, ex.Message);
        }
        finally
        {
            // Stage 51：HITL yield 路徑 — workspace 保留給 Bridge resume，marker 保留給 Recovery 識別
            if (yieldedForHitl)
            {
                logger.LogInformation(
                    "[Stage51] finally：yieldedForHitl=true，保留 workspace + marker 等 Bridge resume（Group={Id}）",
                    group.Id);
            }
            else
            {
                // workingDir cleanup（對齊 legacy line 197-202）
                if (!string.IsNullOrEmpty(workingDir))
                {
                    try { gitHubService.CleanupLocalRepo(workingDir); }
                    catch (Exception ex) { logger.LogWarning(ex, "[Stage50] cleanup workingDir 失敗"); }
                }
                // 清 marker（對齊 Stage 49 FrameworkAppealRouter pattern）
                await db.TaskGroups.Where(g => g.Id == group.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(g => g.ActiveOrchestration, (string?)null)
                        .SetProperty(g => g.KickoffFrameworkStateJson, (string?)null),
                        CancellationToken.None);
            }
        }

        // Stage 55A：catch path 走到此處（exception 後 finally 已跑），return null 表失敗
        return null;
    }

    /// <summary>
    /// Stage 50：Bot 啟動掃 task_groups.KickoffFrameworkStateJson != null 重啟 framework Kickoff。
    /// 對應 legacy MeetingOrchestrationService.RecoverStuckOrchestrationsAsync 的對等機制（雙系統各管自己）。
    /// 對齊 Stage 49 FrameworkAppealRouter.RecoverStuckFrameworkAppealsAsync 降級策略。
    /// Stage 45 紀律：paused TaskGroup 不參與 crash recovery（暫停意圖保留）。
    /// </summary>
    public async Task RecoverStuckFrameworkKickoffsAsync(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Stage 53A F-α 配套：避免 4 marker 共存的 Recovery 篩選優先級 collision
        // 當外層 PipelineFrameworkStateJson != null（framework-in-framework 場景），由 FrameworkPipelineRouter 接管 Recovery
        var stuckGroupIds = await db.TaskGroups
            .Where(g => g.KickoffFrameworkStateJson != null && !g.IsPaused
                     && g.PipelineFrameworkStateJson == null)
            .Select(g => g.Id)
            .ToListAsync(ct);

        if (stuckGroupIds.Count == 0)
        {
            logger.LogInformation("[FrameworkKickoffRouter] 啟動：無 stuck framework kickoff");
            return;
        }

        logger.LogWarning(
            "[FrameworkKickoffRouter] 啟動：發現 {Count} 個 stuck framework kickoff，採 ResumeStreamingAsync 升級策略 rehydrate（Stage 54 升級）",
            stuckGroupIds.Count);

        foreach (var groupId in stuckGroupIds)
        {
            try
            {
                await checkpointStore.LoadFromDbAsync(groupId, ct);
                var sessionId = groupId.ToString();
                var latest = checkpointStore.GetLatestCheckpoint(sessionId);
                if (latest is null)
                {
                    logger.LogWarning(
                        "[FrameworkKickoffRouter] Recovery Group={Id}：KickoffFrameworkStateJson 有值但 latest checkpoint 不存在，清 marker",
                        groupId);
                    await ClearKickoffMarkersAsync(db, groupId, ct);
                    continue;
                }

                // Stage 51 試點 know-how 必須保留（Aria 拿捏 #6 紀律）：
                // 先檢查是否「等待人類回應」狀態（MidInterruptRequestPending = true）
                // 是 → 不算 stuck、不清 marker / 不 ResumeStreamingAsync rehydrate；由 Christ 透過 BossInteraction 回應觸發 Bridge.HandleMidInterruptResponseAsync resume
                var ckptValue = await checkpointStore.RetrieveCheckpointAsync(sessionId, latest);
                if (ScanForBoolProperty(ckptValue, "midInterruptRequestPending"))
                {
                    logger.LogInformation(
                        "[Stage51] Recovery Group={Id}：等待人類回應（MidInterruptRequestPending=true），保留 marker 等 BossInteraction 觸發 resume",
                        groupId);
                    continue;
                }

                // ── Stage 54：升級 ResumeStreamingAsync（對齊 Pipeline 既有 pattern line 275-380）──
                var workflow = workflowFactory.CreateKickoffWorkflow();
                var manager = workflowFactory.CreateCheckpointManager();

                logger.LogInformation(
                    "[FrameworkKickoffRouter] Recovery Group={Id}：ResumeStreamingAsync rehydrate（latest={Ckpt}）",
                    groupId, latest.CheckpointId);

                await using var run = await InProcessExecution.ResumeStreamingAsync(workflow, latest, manager, ct);

                // 收第一個事件決定後續行為（對齊 Pipeline pattern + R6 保守處理）
                var seenOutput = false;
                var seenPendingRequest = false;
                await foreach (var ev in run.WatchStreamAsync().WithCancellation(ct))
                {
                    if (ev is RequestInfoEvent)
                    {
                        // Kickoff 有 MidInterrupt yield 點（Stage 51 試點），但已在前面 check 過
                        // 走到此分支 = 重啟期間剛好新出現 MidInterruptRequestPending，保留 marker
                        seenPendingRequest = true;
                        logger.LogInformation(
                            "[FrameworkKickoffRouter] Recovery Group={Id}：rehydrate 收 RequestInfoEvent，保留 marker 等下次 trigger",
                            groupId);
                        break;
                    }
                    if (ev is WorkflowOutputEvent)
                    {
                        // Kickoff 多數 Recovery 場景：rehydrate 直接走完剩餘 superstep → emit OutputEvent
                        // R6 保守處理：清 marker（Recovery rehydrate 完成），不主動 finalize（避免誤觸發 Discord 通知 / 重複建 BossInteraction —— idempotency check 已守 BossInteraction）
                        seenOutput = true;
                        logger.LogInformation(
                            "[FrameworkKickoffRouter] Recovery Group={Id}：rehydrate 直接 emit WorkflowOutputEvent → 清 marker（不主動 finalize）",
                            groupId);
                        break;
                    }
                }

                if (!seenPendingRequest && !seenOutput)
                {
                    logger.LogWarning(
                        "[FrameworkKickoffRouter] Recovery Group={Id}：rehydrate 未見預期 event，清 marker（避免無人推進）",
                        groupId);
                    await ClearKickoffMarkersAsync(db, groupId, ct);
                    continue;
                }

                if (seenOutput)
                {
                    await ClearKickoffMarkersAsync(db, groupId, ct);
                }
                // seenPendingRequest=true 不清 marker — 等 trigger 重觸發
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[FrameworkKickoffRouter] Recovery Group={Id} 失敗 — 清 marker", groupId);
                try { await ClearKickoffMarkersAsync(db, groupId, CancellationToken.None); } catch { /* swallow */ }
            }
        }
    }

    /// <summary>Stage 54：清 framework kickoff marker（Recovery 異常 / 完成後清）。</summary>
    private static Task ClearKickoffMarkersAsync(AppDbContext db, Guid groupId, CancellationToken ct)
        => db.TaskGroups.Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.ActiveOrchestration, (string?)null)
                .SetProperty(g => g.KickoffFrameworkStateJson, (string?)null),
                ct);

    private static bool ScanForBoolProperty(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.NameEquals(propertyName)
                        && (prop.Value.ValueKind == JsonValueKind.True
                            || prop.Value.ValueKind == JsonValueKind.False))
                    {
                        return prop.Value.GetBoolean();
                    }
                    if (ScanForBoolProperty(prop.Value, propertyName))
                        return true;
                }
                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (ScanForBoolProperty(item, propertyName))
                        return true;
                }
                return false;
            default:
                return false;
        }
    }

    // ============================================================
    //  Workflow run + Discord confirmation flow
    // ============================================================

    private async Task<(KickoffLoopResult? loopResult, bool yieldedForHitl)> RunWorkflowAsync(
        Workflow workflow,
        Microsoft.Agents.AI.Workflows.CheckpointManager checkpointManager,
        string sessionId,
        KickoffState initialState,
        CancellationToken ct)
    {
        // 驗收期 bug 修正（2026-05-02 Forge 自驗階段）：
        // 原 InProcessExecution.RunAsync 對 fan-out + fan-in barrier 拓撲無法完整 dispatch superstep（events=5 但 5 Agent 一個都沒 invoke）。
        // Stage 49 線性串聯（AddEdge/AddSwitch 單一推進路徑）用 RunAsync OK，Stage 50 fan-out/fan-in 必須改 streaming。
        // 對齊 MapReduce sample（dotnet/samples/03-workflows/Concurrent/MapReduce/Program.cs）+ Group Chat sample。
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, initialState, checkpointManager, sessionId, ct);

        KickoffLoopResult? loopResult = null;
        var yielded = false;

        await foreach (var ev in run.WatchStreamAsync().WithCancellation(ct))
        {
            // Stage 51：HITL RequestPort emit RequestInfoEvent → 開 BossInteraction 後 break loop（保留 checkpoint）
            // Bridge.HandleMidInterruptResponseAsync 在 Christ 回應後 ResumeStreamingAsync 接手
            if (ev is RequestInfoEvent requestEvt
                && requestEvt.Request.PortInfo.PortId == KickoffWorkflowFactory.MidInterruptPortId)
            {
                if (requestEvt.Request.TryGetDataAs<MidInterruptRequest>(out var midReq))
                {
                    var freshGroup = await GetFreshGroupAsync(midReq.GroupId, ct);
                    if (freshGroup is not null)
                    {
                        var bridge = serviceProvider.GetRequiredService<FrameworkHitlBridge>();
                        await bridge.RequestMidInterruptInteractionAsync(
                            freshGroup, midReq, requestEvt.Request, ct);
                    }
                    logger.LogInformation(
                        "[Stage51] RunWorkflowAsync: yield for HITL（sessionId={Id}，requestId={Rid}）",
                        sessionId, requestEvt.Request.RequestId);
                }
                else
                {
                    logger.LogWarning(
                        "[Stage51] RunWorkflowAsync: RequestInfoEvent.Data 不是 MidInterruptRequest（sessionId={Id}），仍 yield 等 Bridge 處理",
                        sessionId);
                }
                yielded = true;
                break;  // 結束 watch loop，run dispose 時 framework 保留 pending request 給下次 ResumeStreamingAsync re-emit
            }
            if (ev is WorkflowOutputEvent outputEvent && outputEvent.Is<KickoffLoopResult>(out var r))
            {
                loopResult = r;
                logger.LogInformation(
                    "[Stage50] WorkflowOutputEvent 取得 KickoffLoopResult（sessionId={Id}，decision={Decision}，rounds={Rounds}）",
                    sessionId, r.Decision, r.TotalRounds);
                // 不 break — 讓 framework 收完所有 superstep events（避免 cleanup 時 race condition）
            }
            else if (ev is WorkflowErrorEvent errorEvent)
            {
                logger.LogError(
                    "[Stage50] WorkflowErrorEvent: sessionId={Id}, exception={Exception}",
                    sessionId, errorEvent.Exception?.ToString() ?? "(null)");
            }
            else if (ev is ExecutorFailedEvent failedEvent)
            {
                logger.LogError(
                    "[Stage50] ExecutorFailedEvent: executorId={ExecutorId}, data={Data}",
                    failedEvent.ExecutorId, failedEvent.Data?.ToString() ?? "(null)");
            }
        }

        if (loopResult is null && !yielded)
        {
            logger.LogWarning(
                "[Stage50] Workflow streaming run 完成但無 WorkflowOutputEvent (sessionId={Id})",
                sessionId);
        }
        return (loopResult, yielded);
    }

    /// <summary>
    /// Stage 51：抽出 HandleKickoffMeetingAsync 尾段「寫 DB + Discord embed + cleanup workspace + clear marker + mark task done」
    /// 給 FrameworkHitlBridge.HandleMidInterruptResponseAsync resume 完成後調用（service locator 模式呼叫，避免 ctor 循環依賴）。
    ///
    /// 注意：sync path 也呼叫此 method 完成尾段；本 method 期待 KickoffState.WorkingDir 仍指向有效 workingDir
    /// （HITL yield 期間 router finally 條件式跳過 cleanup 保留之）。
    /// </summary>
    public async Task FinishKickoffAsync(Guid groupId, KickoffLoopResult loopResult, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var freshGroup = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (freshGroup is null)
        {
            logger.LogError("[Stage51] FinishKickoffAsync: 找不到 Group={Id}", groupId);
            return;
        }

        // 從 framework state 取 KickoffTaskId + WorkingDir（initialState 寫入時已序列化進 checkpoint）
        await checkpointStore.LoadFromDbAsync(groupId, ct);
        var sessionId = groupId.ToString();
        var latest = checkpointStore.GetLatestCheckpoint(sessionId);
        Guid kickoffTaskId = Guid.Empty;
        var workingDir = "";
        if (latest is not null)
        {
            try
            {
                var ckptValue = await checkpointStore.RetrieveCheckpointAsync(sessionId, latest);
                kickoffTaskId = ScanForGuidProperty(ckptValue, "kickoffTaskId");
                workingDir    = ScanForStringProperty(ckptValue, "workingDir") ?? "";
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Stage51] FinishKickoffAsync: 解 checkpoint 取 KickoffTaskId / WorkingDir 失敗（Group={Id}）", groupId);
            }
        }

        // 寫 DB（對齊 sync path）
        freshGroup.KickoffMeetingLog = loopResult.MeetingLog;
        freshGroup.TaskPlan          = loopResult.TaskPlan;
        freshGroup.KickoffRound      = loopResult.TotalRounds;
        await taskRepo.SaveAsync(ct);

        // Discord embed + 3 buttons + BossInteraction
        if (kickoffTaskId != Guid.Empty)
        {
            await CreateKickoffConfirmationAsync(freshGroup, loopResult, kickoffTaskId, taskRepo, pushService, ct);
        }
        else
        {
            logger.LogWarning(
                "[Stage51] FinishKickoffAsync: KickoffTaskId 未取得，跳過 task done log + Discord confirmation 開卡（資料完整性風險，請排查）");
        }

        // cleanup workspace + clear marker（對齊 sync path finally）
        if (!string.IsNullOrEmpty(workingDir))
        {
            try { gitHubService.CleanupLocalRepo(workingDir); }
            catch (Exception ex) { logger.LogWarning(ex, "[Stage51] FinishKickoffAsync: cleanup workingDir 失敗"); }
        }
        await db.TaskGroups.Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.ActiveOrchestration, (string?)null)
                .SetProperty(g => g.KickoffFrameworkStateJson, (string?)null),
                CancellationToken.None);

        logger.LogInformation(
            "[Stage51] FinishKickoffAsync 完成（Group={Id}，decision={Decision}，rounds={Rounds}）",
            groupId, loopResult.Decision, loopResult.TotalRounds);
    }

    private async Task<TaskGroup?> GetFreshGroupAsync(Guid groupId, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        return await taskRepo.GetGroupByIdAsync(groupId, ct);
    }

    private static Guid ScanForGuidProperty(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.NameEquals(propertyName)
                        && prop.Value.ValueKind == JsonValueKind.String
                        && Guid.TryParse(prop.Value.GetString(), out var g))
                    {
                        return g;
                    }
                    var nested = ScanForGuidProperty(prop.Value, propertyName);
                    if (nested != Guid.Empty) return nested;
                }
                return Guid.Empty;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = ScanForGuidProperty(item, propertyName);
                    if (nested != Guid.Empty) return nested;
                }
                return Guid.Empty;
            default:
                return Guid.Empty;
        }
    }

    private static string? ScanForStringProperty(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.NameEquals(propertyName)
                        && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        return prop.Value.GetString();
                    }
                    var nested = ScanForStringProperty(prop.Value, propertyName);
                    if (nested is not null) return nested;
                }
                return null;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = ScanForStringProperty(item, propertyName);
                    if (nested is not null) return nested;
                }
                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// 對齊 legacy MeetingOrchestrationService.RunKickoffMeetingAndWaitAsync line 126-192：
    /// Discord embed + 3 buttons + RegisterKickoffConfirmation + InteractionService.CreateInteractionAsync + task done log。
    ///
    /// Stage 55A：改 internal — Pipeline KickoffStageExecutor 同 assembly 直接 call 接管 finalize（議題 G3 解法）。
    /// </summary>
    internal async Task CreateKickoffConfirmationAsync(
        TaskGroup freshGroup,
        KickoffLoopResult loopResult,
        Guid kickoffTaskId,
        TaskRepository taskRepo,
        DashboardPushService pushService,
        CancellationToken ct)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null)
        {
            logger.LogError("[Stage50] 找不到 CEO 頻道，無法上呈 Kick-off 結果");
            return;
        }

        // ── escalate 路徑：發 escalate embed + buttons（沿用 kickoff_* button id，Christ 路由走 legacy HandleKickoffConfirmedAsync）──
        // ── consensus / max_iter：發一般 kick-off 完成 embed ──
        var isEscalate = loopResult.Decision == "escalate";

        var planPreview = string.IsNullOrWhiteSpace(freshGroup.TaskPlan)
            ? (isEscalate ? "（escalate — 無計劃書，請老闆裁決）" : "（無計劃書）")
            : freshGroup.TaskPlan.Length > 500
                ? freshGroup.TaskPlan[..500] + "\n...\n（完整內容請查看 Dashboard）"
                : freshGroup.TaskPlan;

        var embedBuilder = new EmbedBuilder()
            .WithTitle(isEscalate ? "⚠️ Kick-off 會議需老闆裁決（escalate）" : "🚀 Kick-off 會議完成")
            .WithColor(isEscalate ? Color.Orange : Color.Blue)
            .AddField("任務", freshGroup.Title)
            .AddField("會議輪次", loopResult.TotalRounds.ToString())
            .AddField(isEscalate ? "上呈原因" : "任務計劃書摘要",
                isEscalate
                    ? (loopResult.EscalateReason ?? "Petra 判斷需上呈老闆裁決")
                    : planPreview)
            .WithFooter("▶️ 繼續 = 進入設計規劃；⏹️ 停止 = 取消任務；✏️ 修改 = 調整計劃書")
            .WithTimestamp(DateTimeOffset.UtcNow);

        if (isEscalate && !string.IsNullOrWhiteSpace(freshGroup.TaskPlan))
        {
            // escalate 仍可能有部分計劃書內容（雖然當前設計沒產出，未來保留彈性）
            embedBuilder.AddField("會議紀錄摘要", planPreview);
        }

        var embed = embedBuilder.Build();
        var buttons = new ComponentBuilder()
            .WithButton("▶️ 繼續開發",   $"kickoff_continue_{freshGroup.Id}", ButtonStyle.Success)
            .WithButton("⏹️ 停止任務",   $"kickoff_stop_{freshGroup.Id}",     ButtonStyle.Danger)
            .WithButton("✏️ 修改計劃書", $"kickoff_modify_{freshGroup.Id}",   ButtonStyle.Secondary)
            .Build();

        var msg = await ceoChannel.SendMessageAsync(embed: embed, components: buttons);

        var commandHandler = serviceProvider.GetRequiredService<CommandHandler>();
        commandHandler.RegisterKickoffConfirmation(msg.Id, freshGroup.Id, planPreview);

        // Stage 54 idempotency check：Crash Recovery 重跑時若 (groupId, "kickoff") 已有 pending interaction → 跳過
        var interactionRepo = serviceProvider.GetRequiredService<BossInteractionRepository>();
        var existingInteraction = await interactionRepo.GetLatestForGroupByTypeAsync(freshGroup.Id, "kickoff", ct);
        if (existingInteraction is { Status: "pending" })
        {
            logger.LogInformation(
                "[Stage54] Recovery 重跑偵測 pending kickoff interaction（Id={Id}），跳過 CreateInteractionAsync（GroupId={GroupId}）",
                existingInteraction.Id, freshGroup.Id);
        }
        else
        {
            _ = interactionService.CreateInteractionAsync(
                "kickoff",
                title:                $"Kickoff 確認：{freshGroup.Title}",
                description:          isEscalate ? (loopResult.EscalateReason ?? planPreview) : planPreview,
                project:              freshGroup.Project,
                agentName:            AgentNames.Pm,
                availableActionsJson: InteractionService.KickoffActionsJson,
                contextJson:          JsonSerializer.Serialize(new
                {
                    channelId = ceoChannel.Id.ToString(),
                    groupId   = freshGroup.Id.ToString()
                }),
                discordMessageId: (decimal)msg.Id,
                taskGroupId:      freshGroup.Id);
        }

        // task done log
        taskRepo.AddLog(new TaskLog
        {
            TaskId = kickoffTaskId,
            Agent  = AgentNames.Kickoff,
            Step   = $"Kick-off 完成（framework path，共 {loopResult.TotalRounds} 輪，decision={loopResult.Decision}）",
            Status = "done"
        });
        // 取得 kickoffTask reference 以 UpdateStatus（沿用 legacy mode：透過 repo 取再 update）
        var kickoffTaskItem = await taskRepo.GetByIdAsync(kickoffTaskId, ct);
        if (kickoffTaskItem is not null)
        {
            taskRepo.UpdateStatus(kickoffTaskItem, "done");
        }
        await taskRepo.SaveAsync(ct);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = kickoffTaskId,
            GroupId   = freshGroup.Id,
            Title     = $"[Kickoff] {freshGroup.Title}",
            AgentName = AgentNames.Kickoff,
            Status    = "done"
        });
        await pushService.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = AgentNames.Pm,
            Status           = "idle",
            CurrentTaskTitle = null
        });
    }

    /// <summary>
    /// Stage 50：framework Workflow 跑失敗時的處理（fallback 拍板：不 fallback to legacy）。
    /// 理由：① Petra session_id = group.Id 已被 framework path 佔用，再走 legacy 會 double session creation
    ///       ② 對齊 Stage 49 R2 緩解原則「雙系統不互相 invoke」
    /// 改為發 Discord error embed + 標 group failed，由 Christ 線下決定 retry。
    /// </summary>
    private async Task NotifyKickoffFailureAsync(TaskGroup group, string reason)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null)
        {
            logger.LogError("[Stage50] 找不到 CEO 頻道，無法通知 Kick-off failure");
            return;
        }
        try
        {
            var embed = new EmbedBuilder()
                .WithTitle("⚠️ Kick-off Meeting 失敗（framework path）")
                .WithColor(Color.Red)
                .AddField("任務", group.Title)
                .AddField("失敗原因", reason.Length > 800 ? reason[..800] + "..." : reason)
                .WithFooter("framework path 失敗不自動 fallback 到 legacy（避免 Petra session 雙重佔用）。請 Christ 線下決定 retry。")
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();
            await ceoChannel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Stage50] NotifyKickoffFailureAsync 發送 Discord 失敗");
        }
    }

    private IMessageChannel? FindChannel(string channelName)
    {
        if (!ulong.TryParse(_discord.GuildId, out var guildId)) return null;
        return discordClient.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }
}

/// <summary>
/// Stage 55A：HandleKickoffMeetingAsync 回傳結構（議題 G3 解法）— Pipeline KickoffStageExecutor 收到後接管 finalize：
/// 自己 call CreateKickoffConfirmationAsync 開 BossInteraction + SendMessage(KickoffCompletionRequest) yield。
/// LoopResult / KickoffTaskId 為 success path，failure / yield path 整個 Outcome 為 null。
/// </summary>
public sealed record KickoffMeetingOutcome(KickoffLoopResult LoopResult, Guid KickoffTaskId);
