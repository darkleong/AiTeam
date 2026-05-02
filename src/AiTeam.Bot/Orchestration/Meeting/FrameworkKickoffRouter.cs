using System.Text.Json;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord;
using AiTeam.Bot.GitHub;
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
    /// </summary>
    public async Task HandleKickoffMeetingAsync(TaskGroup group, CancellationToken ct)
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
            };

            // ── 跑 framework Workflow ──
            var workflow          = workflowFactory.CreateKickoffWorkflow();
            var checkpointManager = workflowFactory.CreateCheckpointManager();

            var loopResult = await RunWorkflowAsync(workflow, checkpointManager, sessionId, initialState, ct);

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
                return;
            }

            // ── 寫 DB（對齊 legacy line 119-123）──
            var freshGroup = await taskRepo.GetGroupByIdAsync(group.Id, ct);
            if (freshGroup is null)
            {
                logger.LogError("[Stage50] Kick-off 完成後找不到 Group={Id}", group.Id);
                return;
            }

            freshGroup.KickoffMeetingLog = loopResult.MeetingLog;
            freshGroup.TaskPlan          = loopResult.TaskPlan;
            freshGroup.KickoffRound      = loopResult.TotalRounds;
            await taskRepo.SaveAsync(ct);

            logger.LogInformation(
                "[Stage50] Kick-off 會議記錄已存入 DB（Group={Id}，decision={Decision}，rounds={Rounds}）",
                group.Id, loopResult.Decision, loopResult.TotalRounds);

            // ── Discord embed + 3 buttons + BossInteraction（對齊 legacy line 126-185 + escalate 路徑）──
            await CreateKickoffConfirmationAsync(freshGroup, loopResult, kickoffTask.Id, taskRepo, pushService, ct);
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

        var stuckGroupIds = await db.TaskGroups
            .Where(g => g.KickoffFrameworkStateJson != null && !g.IsPaused)
            .Select(g => g.Id)
            .ToListAsync(ct);

        if (stuckGroupIds.Count == 0)
        {
            logger.LogInformation("[FrameworkKickoffRouter] 啟動：無 stuck framework kickoff");
            return;
        }

        logger.LogWarning(
            "[FrameworkKickoffRouter] 啟動：發現 {Count} 個 stuck framework kickoff，採降級策略重啟",
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
                        "[FrameworkKickoffRouter] Recovery Group={Id}：KickoffFrameworkStateJson 有值但 latest checkpoint 不存在，跳過",
                        groupId);
                    continue;
                }

                logger.LogInformation(
                    "[FrameworkKickoffRouter] Recovery Group={Id}：framework Checkpointing 還原 superstep（latest={Ckpt}）",
                    groupId, latest.CheckpointId);

                // 對齊 Stage 49 case study 降級策略：清掉 KickoffFrameworkStateJson + ActiveOrchestration，
                // 讓既有 dispatcher 重新觸發 entry method（從 Round 1 重來）。
                // 此降級策略確保 Bot 重啟不卡死；Mock 場景 C 驗收後再升級為真實 ResumeAsync。
                await db.TaskGroups.Where(g => g.Id == groupId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(g => g.ActiveOrchestration, (string?)null)
                        .SetProperty(g => g.KickoffFrameworkStateJson, (string?)null),
                        ct);

                logger.LogWarning(
                    "[FrameworkKickoffRouter] Recovery Group={Id}：暫採降級策略（清 marker），Mock 場景 C 驗收後升級 ResumeAsync",
                    groupId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[FrameworkKickoffRouter] Recovery Group={Id} 失敗，跳過此 group", groupId);
            }
        }
    }

    // ============================================================
    //  Workflow run + Discord confirmation flow
    // ============================================================

    private async Task<KickoffLoopResult?> RunWorkflowAsync(
        Workflow workflow,
        Microsoft.Agents.AI.Workflows.CheckpointManager checkpointManager,
        string sessionId,
        KickoffState initialState,
        CancellationToken ct)
    {
        // initialState 直接作為 first input message：KickoffStartExecutor 的 [MessageHandler]
        // BroadcastInitialAsync 接 KickoffState 為 first message，內部 SaveAsync 寫進 framework state
        var run = await InProcessExecution.RunAsync(workflow, initialState, checkpointManager, sessionId, ct);

        // 找 WorkflowOutputEvent 取 KickoffLoopResult
        foreach (var ev in run.OutgoingEvents)
        {
            if (ev is WorkflowOutputEvent outputEvent && outputEvent.Is<KickoffLoopResult>(out var loopResult))
            {
                return loopResult;
            }
        }

        // 沒拿到 output event — fallback 視為失敗
        logger.LogWarning(
            "[Stage50] Workflow run 完成但無 WorkflowOutputEvent (sessionId={Id}, events={Count})",
            sessionId, run.OutgoingEvents.Count());
        return null;
    }

    /// <summary>
    /// 對齊 legacy MeetingOrchestrationService.RunKickoffMeetingAndWaitAsync line 126-192：
    /// Discord embed + 3 buttons + RegisterKickoffConfirmation + InteractionService.CreateInteractionAsync + task done log。
    /// </summary>
    private async Task CreateKickoffConfirmationAsync(
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
