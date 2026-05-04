using System.Text.Json;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Bot.Workflows.Design;
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
/// Stage 52：MS Agent Framework Design Meeting Workflow 路由層（v4 漸進遷移第四步，feature flag true 時接管 Design 會議）。
///
/// 設計（路線 A2 fan-out/fan-in 沿用 Stage 50 FrameworkKickoffRouter pattern）：
///   - 從 group 設定 framework DesignState 起始狀態（含 PetraSessionId / RosaSessionId / CodySessionId / QuinnSessionId / DemiSessionId=null）
///   - 標 ActiveOrchestration = "FrameworkDesign"（雙 marker：與 DesignFrameworkStateJson 搭配，區隔 legacy/framework Crash Recovery）
///   - InProcessExecution.RunStreamingAsync(workflow, initialState, checkpointManager, sessionId, ct) 跑 framework Workflow
///   - WatchStreamAsync 收 WorkflowOutputEvent 取得 DesignLoopResult → 寫進既有 DB 欄位（DesignMeetingLog / DesignPlan / DesignRound / IssueUrls / UiSpecContent）
///   - finalize 段 call DesignSplitProposalEvaluator.EvaluateAndProposeSplitAsync（議題 C2：拆 task 提案後置）
///     - consensus + should_split=true → MeetingOrchestrationService.CreateSplitTaskProposalInteractionAsync（共用 SoT）
///     - consensus + should_split=false → fall through fire Dev_plan
///     - escalate → Discord embed + 3 buttons + RegisterDesignConfirmation + InteractionService.CreateInteractionAsync
///
/// 不動的 legacy 行為（沿用 A3 試點精神）：
///   - DesignMeetingService.ModifyDesignPlanAsync 沿用既有「Workflow 結束後修改」流程（議題 H1，Stage 55+ 真切 framework HITL）
///   - HandleDesignConfirmedAsync（Christ 按鈕路由）走 legacy MeetingOrchestrationService（PendingConfirmationStore 共用）
///   - BossInteraction 沿用既有手刻 path（Stage 51 framework HITL 試點僅 Kickoff，Design 維持 legacy）
///
/// fallback 拍板（議題 12）：framework Workflow 跑失敗 → 不 fallback to legacy（避免 Petra session 雙重佔用）+ 改發 Discord error embed +
/// 標 group failed，由 Christ 線下決定 retry。對齊 Stage 50 既有 fallback 拍板。
/// </summary>
public class FrameworkDesignRouter(
    IServiceProvider serviceProvider,
    DesignWorkflowFactory workflowFactory,
    DesignCheckpointStore checkpointStore,
    DiscordSocketClient discordClient,
    IOptions<DiscordSettings> discordSettings,
    IOptions<GitHubSettings> gitHubSettings,
    GitHubService gitHubService,
    InteractionService interactionService,
    WorkflowSettingsResolver workflowResolver,
    ILogger<FrameworkDesignRouter> logger)
{
    private readonly DiscordSettings _discord = discordSettings.Value;
    private readonly GitHubSettings _gitHub  = gitHubSettings.Value;

    /// <summary>
    /// Stage 52：framework path 對應 MeetingOrchestrationService.RunDesignPhaseAsync。
    /// 由 MeetingOrchestrationService 入口分流（feature flag UseFrameworkDesign=true 時呼叫此 method）。
    ///
    /// Stage 55A（議題 G3 解法）：加 skipFinalize 參數 + 改回傳 DesignMeetingOutcome？
    ///   - skipFinalize=false（預設，legacy 路徑）：跑完 Workflow 後 call FinalizeDesignAsync 含 fire Dev_plan / 開 BossInteraction（行為不變）
    ///   - skipFinalize=true（Pipeline 路徑）：跑完 Workflow + 寫 DB 但 skip FinalizeDesignAsync — Pipeline DesignStageExecutor 自己 call FinalizeDesignAsync(skipFireDevPlan=true) 接管
    /// 回傳 DesignMeetingOutcome：success path 含 LoopResult + DesignTaskId + WorkingDir；failure 回 null。
    /// </summary>
    public async Task<DesignMeetingOutcome?> HandleDesignMeetingAsync(
        TaskGroup group, CancellationToken ct, bool skipFinalize = false)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var owner = _gitHub.Owner;
        var repo  = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;

        // ── ActiveOrchestration 雙 marker（與 DesignFrameworkStateJson 搭配，區隔 legacy/framework Crash Recovery）──
        await db.TaskGroups.Where(g => g.Id == group.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, "FrameworkDesign"),
                CancellationToken.None);

        logger.LogInformation("[Stage52] HandleDesignMeetingAsync framework path 接管（Group={Id}）", group.Id);

        // ── 建 designTask + Dashboard status push（對齊 legacy MeetingOrchestrationService line 264-296）──
        var projectId = string.IsNullOrWhiteSpace(group.Project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(group.Project, ct);

        var designTask = new TaskItem
        {
            Title         = $"[Design] {group.Title}",
            Description   = "設計規劃多 Agent 會議（framework path）",
            TriggeredBy   = "Orchestrator",
            AssignedAgent = AgentNames.Design,
            Status        = "running",
            GroupId       = group.Id,
            ProjectId     = projectId,
        };
        taskRepo.Add(designTask);
        await taskRepo.SaveAsync(ct);
        taskRepo.AddLog(new TaskLog
        {
            TaskId = designTask.Id,
            Agent  = AgentNames.Design,
            Step   = "設計規劃多 Agent 會議進行中（framework path）...",
            Status = "running"
        });
        await taskRepo.SaveAsync(ct);

        await pushService.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = AgentNames.Pm,
            Status           = "running",
            CurrentTaskTitle = $"設計規劃會議：{group.Title}（framework path）"
        });
        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = designTask.Id,
            GroupId   = group.Id,
            Title     = designTask.Title,
            AgentName = AgentNames.Design,
            Status    = "running"
        });

        // ── Clone repo（對齊 legacy DesignMeetingService line 62-71）──
        var workingDir = "";
        try
        {
            var cloneSuffix = "design-" + group.Id.ToString("N")[..8];
            workingDir = gitHubService.CloneOrPull(owner, repo, cloneSuffix);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Stage52] clone repo 失敗，使用 workspace 路徑作為 fallback");
            workingDir = Path.Combine(_gitHub.WorkspacePath, repo);
        }

        try
        {
            // ── 設 framework DesignState 起始 ──
            var maxRounds = await workflowResolver.GetDesignMeetingMaxRoundsAsync(ct);
            var sessionId = group.Id.ToString();

            await checkpointStore.LoadFromDbAsync(group.Id, ct);

            var initialState = new DesignState
            {
                GroupId        = group.Id,
                DesignTaskId   = designTask.Id,
                Owner          = owner,
                Repo           = repo,
                WorkingDir     = workingDir,
                TaskPlan       = group.TaskPlan ?? "",
                MaxRounds      = maxRounds,
                Round          = 0,                                  // 前置作業階段，主迴圈進入時 mainStart 設 1
                PetraSessionId = Guid.NewGuid().ToString(),
                RosaSessionId  = Guid.NewGuid().ToString(),
                DemiSessionId  = null,                               // 條件式：DemiPreWork 依 NeedsDemi 動態建立
                CodySessionId  = Guid.NewGuid().ToString(),
                QuinnSessionId = Guid.NewGuid().ToString(),
                NeedsDemi      = false,
                IssuesJson     = "[]",
                IssueUrls      = null,
                UiSpecContent  = null,
                LastPetraOutput = null,
                MeetingLog     = "# 設計會議紀錄\n\n",
                FinalDecision  = "consensus",
                EscalateReason = null,
                TotalRounds    = 0,
            };

            // ── 跑 framework Workflow ──
            var workflow          = workflowFactory.CreateDesignWorkflow();
            var checkpointManager = workflowFactory.CreateCheckpointManager();

            var loopResult = await RunWorkflowAsync(workflow, checkpointManager, sessionId, initialState, ct);

            if (loopResult is null)
            {
                logger.LogError(
                    "[Stage52] Design Workflow 未產生 DesignLoopResult（Group={Id}），fallback 失敗處理",
                    group.Id);
                taskRepo.UpdateGroupStatus(group, "failed");
                taskRepo.AddLog(new TaskLog
                {
                    TaskId = designTask.Id,
                    Agent  = AgentNames.Design,
                    Step   = "設計規劃失敗：framework Workflow 未產生結果",
                    Status = "failed"
                });
                taskRepo.UpdateStatus(designTask, "failed");
                await taskRepo.SaveAsync(ct);
                await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
                {
                    TaskId    = designTask.Id,
                    GroupId   = group.Id,
                    Title     = designTask.Title,
                    AgentName = AgentNames.Design,
                    Status    = "failed"
                });
                await pushService.PushAgentStatusAsync(new AgentStatusViewModel
                {
                    AgentName        = AgentNames.Pm,
                    Status           = "error",
                    CurrentTaskTitle = "設計規劃失敗：framework Workflow 未產生結果"
                });
                await NotifyDesignFailureAsync(group, "framework Workflow 未產生結果");
                return null;
            }

            // ── 寫 DB（對齊 legacy MeetingOrchestrationService line 312-319）──
            var freshGroup = await taskRepo.GetGroupByIdAsync(group.Id, ct);
            if (freshGroup is null)
            {
                logger.LogError("[Stage52] Design 完成後找不到 Group={Id}", group.Id);
                return null;
            }

            freshGroup.DesignMeetingLog = loopResult.MeetingLog;
            freshGroup.DesignPlan       = loopResult.DesignPlan;
            freshGroup.DesignRound      = loopResult.TotalRounds;
            if (!string.IsNullOrWhiteSpace(loopResult.IssueUrls))
                freshGroup.IssueUrls = loopResult.IssueUrls;
            if (!string.IsNullOrWhiteSpace(loopResult.UiSpecContent))
                freshGroup.UiSpecContent = loopResult.UiSpecContent;
            await taskRepo.SaveAsync(ct);

            logger.LogInformation(
                "[Stage52] 設計規劃會議記錄已存入 DB（Group={Id}，decision={Decision}，rounds={Rounds}）",
                group.Id, loopResult.Decision, loopResult.TotalRounds);

            // ── 標 designTask done（對齊 legacy line 323-333）──
            taskRepo.AddLog(new TaskLog
            {
                TaskId = designTask.Id,
                Agent  = AgentNames.Design,
                Step   = $"設計規劃完成（framework path，共 {loopResult.TotalRounds} 輪，decision={loopResult.Decision}）",
                Status = "done"
            });
            taskRepo.UpdateStatus(designTask, "done");
            await taskRepo.SaveAsync(ct);
            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = designTask.Id,
                GroupId   = group.Id,
                Title     = designTask.Title,
                AgentName = AgentNames.Design,
                Status    = "done"
            });

            // ── finalize 段（議題 C2 拆 task 提案後置 / Discord 路由分支）──
            // Stage 55A：skipFinalize=true 時 Pipeline DesignStageExecutor 自己接管 FinalizeDesignAsync
            if (!skipFinalize)
            {
                await FinalizeDesignAsync(freshGroup, loopResult, workingDir, ct);
            }
            else
            {
                logger.LogInformation(
                    "[Stage55A] HandleDesignMeetingAsync skipFinalize=true — Pipeline 接管 FinalizeDesignAsync（Group={Id}）",
                    group.Id);
            }

            return new DesignMeetingOutcome(loopResult, designTask.Id, workingDir);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Stage52] Design Workflow 失敗（Group={Id}）", group.Id);

            taskRepo.AddLog(new TaskLog
            {
                TaskId = designTask.Id,
                Agent  = AgentNames.Design,
                Step   = $"設計規劃失敗（framework path）：{ex.Message}",
                Status = "failed"
            });
            taskRepo.UpdateStatus(designTask, "failed");
            await taskRepo.SaveAsync(CancellationToken.None);
            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = designTask.Id,
                GroupId   = group.Id,
                Title     = designTask.Title,
                AgentName = AgentNames.Design,
                Status    = "failed"
            });
            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = AgentNames.Pm,
                Status           = "error",
                CurrentTaskTitle = $"設計規劃失敗：{ex.Message}"
            });
            await NotifyDesignFailureAsync(group, ex.Message);
        }
        finally
        {
            // Stage 55A：skipFinalize=true 時保留 workingDir + marker 給 Pipeline DesignStageExecutor 接管 finalize（FinalizeDesignAsync 需 workingDir 給 splitEval）
            if (skipFinalize)
            {
                logger.LogInformation(
                    "[Stage55A] finally：skipFinalize=true，保留 workingDir + marker 等 Pipeline DesignStageExecutor 接管（Group={Id}）",
                    group.Id);
            }
            else
            {
                // workingDir cleanup（對齊 legacy DesignMeetingService line 346-350）
                if (!string.IsNullOrEmpty(workingDir))
                {
                    try { gitHubService.CleanupLocalRepo(workingDir); }
                    catch (Exception ex) { logger.LogWarning(ex, "[Stage52] cleanup workingDir 失敗"); }
                }
                // 清 marker（對齊 Stage 49/50 router pattern）
                await db.TaskGroups.Where(g => g.Id == group.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(g => g.ActiveOrchestration, (string?)null)
                        .SetProperty(g => g.DesignFrameworkStateJson, (string?)null),
                        CancellationToken.None);
            }

            // PM Agent status idle（對齊 legacy line 431-436）— 兩路徑都 push
            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = AgentNames.Pm,
                Status           = "idle",
                CurrentTaskTitle = null
            });
        }

        // Stage 55A：catch path 走到此處（exception 後 finally 已跑），return null 表失敗
        return null;
    }

    /// <summary>
    /// Stage 52：Bot 啟動掃 task_groups.DesignFrameworkStateJson != null 重啟 framework Design。
    /// 對應 legacy MeetingOrchestrationService.RecoverStuckOrchestrationsAsync 的對等機制（雙系統各管自己）。
    /// 對齊 Stage 49/50 既有降級策略：清 marker 重觸發 entry（從前置作業 Round 0 重來）。
    /// Stage 45 紀律：paused TaskGroup 不參與 crash recovery（暫停意圖保留）。
    /// </summary>
    public async Task RecoverStuckFrameworkDesignAsync(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Stage 55B：F-α 排除條件（PipelineFrameworkStateJson == null）移除 — sub-task TaskGroup 也納入篩選；
        //   sub-task 從 Dev_plan 啟動 skip Kickoff/Design 階段（Stage 55A 兩入口分流 + IsSubTask 路由）
        //   → sub-task 不會有 DesignFrameworkStateJson → race condition 風險 0
        // Stage 53A F-α 配套（4 marker 共存 Recovery 篩選優先級）：UseFrameworkPipeline=true 唯一 path 後 dead code
        var stuckGroupIds = await db.TaskGroups
            .Where(g => g.DesignFrameworkStateJson != null && !g.IsPaused)
            .Select(g => g.Id)
            .ToListAsync(ct);

        if (stuckGroupIds.Count == 0)
        {
            logger.LogInformation("[FrameworkDesignRouter] 啟動：無 stuck framework design");
            return;
        }

        logger.LogWarning(
            "[FrameworkDesignRouter] 啟動：發現 {Count} 個 stuck framework design，採 ResumeStreamingAsync 升級策略 rehydrate（Stage 54 升級）",
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
                        "[FrameworkDesignRouter] Recovery Group={Id}：DesignFrameworkStateJson 有值但 latest checkpoint 不存在，清 marker",
                        groupId);
                    await ClearDesignMarkersAsync(db, groupId, ct);
                    continue;
                }

                // ── Stage 54：升級 ResumeStreamingAsync（對齊 Pipeline 既有 pattern line 275-380）──
                var workflow = workflowFactory.CreateDesignWorkflow();
                var manager = workflowFactory.CreateCheckpointManager();

                logger.LogInformation(
                    "[FrameworkDesignRouter] Recovery Group={Id}：ResumeStreamingAsync rehydrate（latest={Ckpt}）",
                    groupId, latest.CheckpointId);

                await using var run = await InProcessExecution.ResumeStreamingAsync(workflow, latest, manager, ct);

                // 收第一個事件決定後續行為（對齊 Pipeline pattern + R6 保守處理）
                var seenOutput = false;
                var seenPendingRequest = false;
                await foreach (var ev in run.WatchStreamAsync().WithCancellation(ct))
                {
                    if (ev is RequestInfoEvent)
                    {
                        // Design 既有設計無 yield 點，極罕見走到此分支 — 保留 marker 等下次 trigger
                        seenPendingRequest = true;
                        logger.LogInformation(
                            "[FrameworkDesignRouter] Recovery Group={Id}：rehydrate 收 RequestInfoEvent（罕見），保留 marker 等下次 trigger",
                            groupId);
                        break;
                    }
                    if (ev is WorkflowOutputEvent)
                    {
                        // Design 多數 Recovery 場景：rehydrate 直接走完剩餘 superstep → emit OutputEvent
                        // R6 保守處理：清 marker（Recovery rehydrate 完成），不主動 finalize
                        // idempotency 已守：① RosaPreWork/Adjustment 用 LastIssueCreatedRound 防 GitHub Issue 重複；② FinalizeDesign CreateInteraction 用 BossInteraction lookup 防確認卡重複
                        seenOutput = true;
                        logger.LogInformation(
                            "[FrameworkDesignRouter] Recovery Group={Id}：rehydrate 直接 emit WorkflowOutputEvent → 清 marker（不主動 finalize）",
                            groupId);
                        break;
                    }
                }

                if (!seenPendingRequest && !seenOutput)
                {
                    logger.LogWarning(
                        "[FrameworkDesignRouter] Recovery Group={Id}：rehydrate 未見預期 event，清 marker（避免無人推進）",
                        groupId);
                    await ClearDesignMarkersAsync(db, groupId, ct);
                    continue;
                }

                if (seenOutput)
                {
                    await ClearDesignMarkersAsync(db, groupId, ct);
                }
                // seenPendingRequest=true 不清 marker — 等 trigger 重觸發
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[FrameworkDesignRouter] Recovery Group={Id} 失敗 — 清 marker", groupId);
                try { await ClearDesignMarkersAsync(db, groupId, CancellationToken.None); } catch { /* swallow */ }
            }
        }
    }

    /// <summary>Stage 54：清 framework design marker（Recovery 異常 / 完成後清）。</summary>
    private static Task ClearDesignMarkersAsync(AppDbContext db, Guid groupId, CancellationToken ct)
        => db.TaskGroups.Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.ActiveOrchestration, (string?)null)
                .SetProperty(g => g.DesignFrameworkStateJson, (string?)null),
                ct);

    // ============================================================
    //  Workflow run + finalize flow
    // ============================================================

    private async Task<DesignLoopResult?> RunWorkflowAsync(
        Workflow workflow,
        Microsoft.Agents.AI.Workflows.CheckpointManager checkpointManager,
        string sessionId,
        DesignState initialState,
        CancellationToken ct)
    {
        // 沿用 Stage 50 踩坑 #9：fan-out/fan-in 拓撲必須 RunStreamingAsync + WatchStreamAsync foreach
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, initialState, checkpointManager, sessionId, ct);

        DesignLoopResult? loopResult = null;

        await foreach (var ev in run.WatchStreamAsync().WithCancellation(ct))
        {
            if (ev is WorkflowOutputEvent outputEvent && outputEvent.Is<DesignLoopResult>(out var r))
            {
                loopResult = r;
                logger.LogInformation(
                    "[Stage52] WorkflowOutputEvent 取得 DesignLoopResult（sessionId={Id}，decision={Decision}，rounds={Rounds}）",
                    sessionId, r.Decision, r.TotalRounds);
                // 不 break — 讓 framework 收完所有 superstep events（避免 cleanup 時 race condition）
            }
            else if (ev is WorkflowErrorEvent errorEvent)
            {
                logger.LogError(
                    "[Stage52] WorkflowErrorEvent: sessionId={Id}, exception={Exception}",
                    sessionId, errorEvent.Exception?.ToString() ?? "(null)");
            }
            else if (ev is ExecutorFailedEvent failedEvent)
            {
                logger.LogError(
                    "[Stage52] ExecutorFailedEvent: executorId={ExecutorId}, data={Data}",
                    failedEvent.ExecutorId, failedEvent.Data?.ToString() ?? "(null)");
            }
        }

        if (loopResult is null)
        {
            logger.LogWarning(
                "[Stage52] Workflow streaming run 完成但無 WorkflowOutputEvent (sessionId={Id})",
                sessionId);
        }
        return loopResult;
    }

    /// <summary>
    /// Stage 52：finalize 段 — 議題 C2 拆 task 提案後置 + Discord 路由分支（consensus / max_iter / escalate）。
    ///
    /// consensus / max_iter 路徑：
    ///   - call DesignSplitProposalEvaluator.EvaluateAndProposeSplitAsync（規則層 + Petra 層 SoT）
    ///   - should_split=true + phases > 0 → MeetingOrchestrationService.CreateSplitTaskProposalInteractionAsync（共用 SoT，Stage 46-FF 三十五 機制不漂移）
    ///   - should_split=false / null → fall through fire Dev_plan
    /// escalate 路徑：
    ///   - Discord embed (purple 🎨) + 3 buttons + RegisterDesignConfirmation + InteractionService.CreateInteractionAsync
    ///   - Christ 按鈕路由走 legacy MeetingOrchestrationService.HandleDesignConfirmedAsync（議題 H1 沿用）
    ///
    /// Stage 55A（議題 G3 解法）：
    ///   - 改 internal — Pipeline DesignStageExecutor 同 assembly 直接 call 接管 finalize
    ///   - 加 skipFireDevPlan 參數：true 時 ConsensusNoSplit 路徑不 fire Dev_plan，由 Pipeline 接管 SendMessage(DevPlanStageBridge)
    ///   - 改回傳 DesignFinalizationDecision enum：Pipeline 看 decision 決定下一步
    /// </summary>
    internal async Task<DesignFinalizationDecision> FinalizeDesignAsync(
        TaskGroup freshGroup, DesignLoopResult loopResult, string workingDir, CancellationToken ct,
        bool skipFireDevPlan = false)
    {
        if (loopResult.Decision is "consensus" or "max_iter"
            && !string.IsNullOrWhiteSpace(loopResult.DesignPlan))
        {
            // ── 拆 task 提案評估（議題 C2）──
            await using var splitScope = serviceProvider.CreateAsyncScope();
            var splitEval    = splitScope.ServiceProvider.GetRequiredService<DesignSplitProposalEvaluator>();
            var tokenLog     = splitScope.ServiceProvider.GetRequiredService<TokenLogService>();
            var config       = splitScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var apiKey       = config["AITEAM_ANTHROPIC_KEY"] ?? config["Anthropic:ApiKey"] ?? "";

            var splitProposal = await splitEval.EvaluateAndProposeSplitAsync(
                loopResult.PetraSessionId,
                loopResult.DesignPlan!,
                loopResult.IssuesJson,
                workingDir,
                apiKey,
                loopResult.TotalRounds,
                tokenLog,
                ct);

            if (splitProposal is { ShouldSplit: true } sp && sp.Phases is { Count: > 0 })
            {
                logger.LogInformation(
                    "[Stage52] 拆 task 提案觸發（Group={Id}，phases={Count}）",
                    freshGroup.Id, sp.Phases.Count);

                // 共用 SoT（Stage 46-FF 三十五 機制不漂移）— call MeetingOrchestrationService.CreateSplitTaskProposalInteractionAsync
                var orchestrationService = serviceProvider.GetRequiredService<MeetingOrchestrationService>();
                await orchestrationService.CreateSplitTaskProposalInteractionAsync(freshGroup, sp, ct);
                return DesignFinalizationDecision.SplitProposalOpened;
            }

            // should_split=false / null → fall through fire Dev_plan（對齊 legacy line 349-352）
            logger.LogInformation("[Stage52] 設計規劃 consensus，直接進入 Dev_plan（Group={Id}）", freshGroup.Id);

            // Stage 55A：skipFireDevPlan=true 時 Pipeline DesignStageExecutor 接管 SendMessage(DevPlanStageBridge)
            if (!skipFireDevPlan)
            {
                var tgs = serviceProvider.GetRequiredService<TaskGroupService>();
                await tgs.FireStepsAsync(freshGroup, [new WorkflowStep("Dev_plan")], ct);
            }
            else
            {
                logger.LogInformation(
                    "[Stage55A] FinalizeDesignAsync skipFireDevPlan=true — Pipeline 接管 fire Dev_plan（Group={Id}）",
                    freshGroup.Id);
            }
            return DesignFinalizationDecision.ConsensusNoSplit;
        }

        // ── escalate 路徑：Discord embed + 3 buttons + BossInteraction（對齊 legacy line 356-405）──
        await CreateDesignEscalateConfirmationAsync(freshGroup, loopResult, ct);
        return DesignFinalizationDecision.EscalateConfirmationOpened;
    }

    /// <summary>
    /// Stage 52：escalate 路徑 Discord 確認流程（對齊 legacy MeetingOrchestrationService line 356-405）。
    /// Christ 按鈕路由走 legacy HandleDesignConfirmedAsync（PetraSessionId 透過 contextJson 傳遞 + RegisterDesignConfirmation）。
    /// </summary>
    private async Task CreateDesignEscalateConfirmationAsync(
        TaskGroup freshGroup, DesignLoopResult loopResult, CancellationToken ct)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null)
        {
            logger.LogError("[Stage52] 找不到 CEO 頻道，無法上呈設計規劃結果");
            return;
        }

        var planPreview = string.IsNullOrWhiteSpace(freshGroup.DesignPlan)
            ? "（escalate — 無設計規劃書，請老闆裁決）"
            : freshGroup.DesignPlan!.Length > 600
                ? freshGroup.DesignPlan[..600] + "\n...\n（完整內容請查看 Dashboard）"
                : freshGroup.DesignPlan;

        var embed = new EmbedBuilder()
            .WithTitle("🎨 設計規劃會議需上呈確認")
            .WithColor(Color.Purple)
            .AddField("任務", freshGroup.Title)
            .AddField("上呈原因", loopResult.EscalateReason ?? "設計存在分歧，需老闆裁決")
            .AddField("設計規劃書摘要", planPreview)
            .AddField("會議輪次", loopResult.TotalRounds.ToString())
            .WithFooter("▶️ 繼續 = 進入 Dev_plan；⏹️ 停止 = 取消任務；✏️ 修改 = 提供設計指引")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        var buttons = new ComponentBuilder()
            .WithButton("▶️ 繼續開發",  $"design_continue_{freshGroup.Id}", ButtonStyle.Success)
            .WithButton("⏹️ 停止任務",  $"design_stop_{freshGroup.Id}",     ButtonStyle.Danger)
            .WithButton("✏️ 修改設計",  $"design_modify_{freshGroup.Id}",   ButtonStyle.Secondary)
            .Build();

        var msg = await ceoChannel.SendMessageAsync(embed: embed, components: buttons);

        var commandHandler = serviceProvider.GetRequiredService<CommandHandler>();
        commandHandler.RegisterDesignConfirmation(msg.Id, freshGroup.Id, loopResult.PetraSessionId, loopResult.EscalateReason);

        // Stage 54 idempotency check：Crash Recovery 重跑時若 (groupId, "design") 已有 pending interaction → 跳過
        var interactionRepo = serviceProvider.GetRequiredService<BossInteractionRepository>();
        var existingInteraction = await interactionRepo.GetLatestForGroupByTypeAsync(freshGroup.Id, "design", ct);
        if (existingInteraction is { Status: "pending" })
        {
            logger.LogInformation(
                "[Stage54] Recovery 重跑偵測 pending design interaction（Id={Id}），跳過 CreateInteractionAsync（GroupId={GroupId}）",
                existingInteraction.Id, freshGroup.Id);
        }
        else
        {
            _ = interactionService.CreateInteractionAsync(
                "design",
                title:                $"設計確認：{freshGroup.Title}",
                description:          planPreview,
                project:              freshGroup.Project,
                agentName:            AgentNames.Pm,
                availableActionsJson: InteractionService.DesignActionsJson,
                contextJson:          JsonSerializer.Serialize(new
                {
                    channelId      = ceoChannel.Id.ToString(),
                    groupId        = freshGroup.Id.ToString(),
                    petraSessionId = loopResult.PetraSessionId
                }),
                discordMessageId: (decimal)msg.Id,
                taskGroupId:      freshGroup.Id);
        }

        logger.LogInformation(
            "[Stage52] escalate confirmation 已建立（Group={Id}，PetraSessionId={Sid}）",
            freshGroup.Id, loopResult.PetraSessionId);
    }

    /// <summary>
    /// Stage 52：framework Workflow 跑失敗時的處理（fallback 拍板：不 fallback to legacy）。
    /// 對齊 Stage 50 NotifyKickoffFailureAsync 既有設計（避免 Petra session 雙重佔用）。
    /// </summary>
    private async Task NotifyDesignFailureAsync(TaskGroup group, string reason)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null)
        {
            logger.LogError("[Stage52] 找不到 CEO 頻道，無法通知 Design failure");
            return;
        }
        try
        {
            var embed = new EmbedBuilder()
                .WithTitle("⚠️ Design Meeting 失敗（framework path）")
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
            logger.LogError(ex, "[Stage52] NotifyDesignFailureAsync 發送 Discord 失敗");
        }
    }

    private IMessageChannel? FindChannel(string channelName)
    {
        if (!ulong.TryParse(_discord.GuildId, out var guildId)) return null;
        return discordClient.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }

    /// <summary>
    /// Stage 55A：cleanup workspace + clear DesignFrameworkStateJson marker（Pipeline DesignStageExecutor 接管 finalize 後自行 cleanup 用）。
    /// 對齊 inner finally cleanup 段（line 297-307）。
    /// </summary>
    internal async Task CleanupWorkingDirAndMarkerAsync(Guid groupId, string workingDir, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!string.IsNullOrEmpty(workingDir))
        {
            try { gitHubService.CleanupLocalRepo(workingDir); }
            catch (Exception ex) { logger.LogWarning(ex, "[Stage55A] CleanupWorkingDirAndMarkerAsync cleanup workingDir 失敗"); }
        }

        await db.TaskGroups.Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.ActiveOrchestration, (string?)null)
                .SetProperty(g => g.DesignFrameworkStateJson, (string?)null),
                CancellationToken.None);

        logger.LogInformation(
            "[Stage55A] CleanupWorkingDirAndMarkerAsync 完成（Group={Id}）", groupId);
    }
}

/// <summary>
/// Stage 55A：HandleDesignMeetingAsync 回傳結構（議題 G3 解法）— Pipeline DesignStageExecutor 收到後接管 finalize：
/// 自己 call FinalizeDesignAsync(skipFireDevPlan=true) → 看回傳 decision 決定下一步。
/// LoopResult / DesignTaskId / WorkingDir 為 success path，failure 整個 Outcome 為 null。
/// </summary>
public sealed record DesignMeetingOutcome(DesignLoopResult LoopResult, Guid DesignTaskId, string WorkingDir);

/// <summary>
/// Stage 55A：FinalizeDesignAsync 回傳 — Pipeline DesignStageExecutor 看 decision 決定下一步：
///   - SplitProposalOpened：sub-task chain 接手，Pipeline 結束此 group DesignStage（SendMessage(PipelineFallbackBridge "design_split_proposal_opened")）
///   - ConsensusNoSplit：Pipeline SendMessage(DevPlanStageBridge) 進 DevPlanStage
///   - EscalateConfirmationOpened：Pipeline yield 等 design_continue button → ResumeAfterDesignAsync
/// </summary>
public enum DesignFinalizationDecision
{
    SplitProposalOpened,
    ConsensusNoSplit,
    EscalateConfirmationOpened
}
