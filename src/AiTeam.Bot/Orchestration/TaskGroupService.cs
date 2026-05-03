using System.Text.Json;
using TaskStatus = AiTeam.Shared.Constants.TaskStatus;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Orchestration.Appeal;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Orchestration.Proposal;
using AiTeam.Bot.Orchestration.Qa;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using AiTeam.Shared.ViewModels;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Orchestration;

/// <summary>
/// Stage 36：任務群組管理與主流程 dispatcher（瘦身版，從 2623 行拆至 ~500 行）。
///
/// 職責：
///   - 任務群組 CRUD（CreateGroupAsync）
///   - 主 dispatcher（HandleAgentCompletedAsync：DB 狀態更新 + 依 completedAgent 分派到子 service）
///   - 步驟派工（FireStepsAsync / FireOneStepAsync）
///   - 取消任務（CancelAsync）
///   - Dashboard 回覆分派入口（ProcessBossResponseAsync → Meeting/Appeal/Proposal service）
///   - Notify 系列輔助（Merge / Intervention）
///
/// 拆出的職責：
///   - Kickoff / Design / Crash Recovery → <see cref="MeetingOrchestrationService"/>
///   - Review Appeal + Dev_plan Appeal + Petra 審核 → <see cref="AppealOrchestrationService"/>
///   - QA 路由 → <see cref="QaCoordinationService"/>
///   - Dashboard 路徑 Proposal/Exec Confirm → <see cref="ProposalConfirmationService"/>
/// </summary>
public class TaskGroupService(
    IServiceProvider serviceProvider,
    DiscordSocketClient discordClient,
    IOptions<DiscordSettings> discordSettings,
    IOptions<GitHubSettings> gitHubSettings,
    WorkflowEngine workflowEngine,
    AgentQueueService agentQueueService,
    InteractionService interactionService,
    IHostApplicationLifetime appLifetime,
    MeetingOrchestrationService meetingOrchestration,
    AppealOrchestrationService appealOrchestration,
    QaCoordinationService qaCoordination,
    ProposalConfirmationService proposalConfirmation,
    ILogger<TaskGroupService> logger)
{
    private readonly DiscordSettings _discord = discordSettings.Value;
    private readonly GitHubSettings  _gitHub  = gitHubSettings.Value;

    // ============================================================
    //  任務群組建立
    // ============================================================

    public async Task<TaskGroup> CreateGroupAsync(
        string title,
        string project,
        WorkflowType workflowType,
        string? issueUrlsJson  = null,
        string? uiSpecContent  = null,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var group = new TaskGroup
        {
            Title          = title,
            Project        = project,
            Status         = "running",
            WorkflowType   = workflowType switch
            {
                WorkflowType.NewFeature      => "new_feature",
                WorkflowType.TechImprovement => "tech_improvement",
                _                            => "bug_fix"
            },
            IssueUrls      = issueUrlsJson,
            UiSpecContent  = uiSpecContent,
        };

        taskRepo.AddGroup(group);
        await taskRepo.SaveAsync(cancellationToken);

        logger.LogInformation("TaskGroup 建立：{Id}（{Title}，{Type}）",
            group.Id, group.Title, group.WorkflowType);

        return group;
    }

    // ============================================================
    //  主 dispatcher（Stage 36：瘦身版）
    // ============================================================

    /// <summary>
    /// Agent 完成後 dispatcher。依 completedAgent 分派到對應子 service，落底呼 WorkflowEngine。
    /// </summary>
    public async Task HandleAgentCompletedAsync(
        Guid groupId,
        string completedAgent,
        AgentExecutionResult result,
        string devPrUrl = "",
        CancellationToken cancellationToken = default)
    {
        if (groupId == Guid.Empty) return;

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var group = await taskRepo.GetGroupByIdAsync(groupId, cancellationToken);
        if (group is null)
        {
            logger.LogWarning("HandleAgentCompleted：找不到 TaskGroup（Id={Id}）", groupId);
            return;
        }

        if (group.Status is "done" or "failed")
        {
            logger.LogDebug("HandleAgentCompleted：TaskGroup {Id} 已結束（{Status}），略過", groupId, group.Status);
            return;
        }

        // ── 合併 DB 狀態更新（避免多次 SaveAsync）──
        var needsSave = false;

        if (!string.IsNullOrWhiteSpace(devPrUrl) && string.IsNullOrWhiteSpace(group.DevPrUrl))
        {
            group.DevPrUrl = devPrUrl;
            needsSave = true;
        }

        if (!string.IsNullOrWhiteSpace(result.ReviewBody))
        {
            group.LastReviewBody = result.ReviewBody;
            needsSave = true;
            logger.LogInformation("TaskGroup {Id} 已儲存 Vera 審查報告（{Len} 字）",
                groupId, result.ReviewBody.Length);
        }

        if ((completedAgent.Equals("Dev", StringComparison.OrdinalIgnoreCase)
             || completedAgent.Equals("Dev_fix", StringComparison.OrdinalIgnoreCase))
            && result.Success
            && !string.IsNullOrWhiteSpace(result.OutputContent))
        {
            group.ImplementationNote = result.OutputContent;
            needsSave = true;
            logger.LogInformation("TaskGroup {Id} 已儲存 Cody 實作說明（{Len} 字）",
                groupId, result.OutputContent.Length);
        }

        if (completedAgent.Equals(AgentNames.Qa, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(result.TestReport))
        {
            group.TestReport = result.TestReport;
            needsSave = true;
            logger.LogInformation("TaskGroup {Id} 已儲存 Quinn 測試報告（{Len} 字）",
                groupId, result.TestReport.Length);
        }

        if (needsSave)
            await taskRepo.SaveAsync(cancellationToken);

        // Stage 53A：framework Pipeline path 接管 callback resume（Aria 方案 C 拍板，2026-05-03）
        // 議題 10 修法：分流位置在 line 168 needsSave/SaveAsync 之後（既有 hooks 之前）— framework path 也需要 DevPrUrl/LastReviewBody/ImplementationNote/TestReport DB 欄位寫入
        // 議題 9 修法：fallback 路徑由 Pipeline Executor ClearMarkerAndFallbackAsync 已清 marker → callback resume 條件 (PipelineFrameworkStateJson != null) 自然失敗 → 走 legacy（避免遞迴）
        if (group.PipelineFrameworkStateJson != null)
        {
            await using var pipelineScope = serviceProvider.CreateAsyncScope();
            var workflowResolver = pipelineScope.ServiceProvider.GetRequiredService<Configuration.WorkflowSettingsResolver>();
            if (await workflowResolver.GetUseFrameworkPipelineAsync(cancellationToken))
            {
                logger.LogInformation(
                    "[Stage53A] HandleAgentCompletedAsync framework path 接管（Group={Id}, completedAgent={Agent}）",
                    groupId, completedAgent);
                var router = pipelineScope.ServiceProvider.GetRequiredService<Meeting.FrameworkPipelineRouter>();
                await router.ResumeAfterAgentAsync(group, completedAgent, result, cancellationToken);
                return;
            }
        }

        // ── Dev_plan 完成 → Petra 審核 + Appeal（Stage 37：搬至 AppealOrchestrationService.HandleDevPlanCompletedAsync）──
        if (completedAgent.Equals("Dev_plan", StringComparison.OrdinalIgnoreCase))
        {
            var groupProjectId = await GetGroupProjectIdAsync(group, taskRepo, cancellationToken);
            var shouldContinue = await appealOrchestration.HandleDevPlanCompletedAsync(
                group, result, taskRepo, groupProjectId, cancellationToken);
            if (!shouldContinue) return;
        }

        // ── Reviewer 完成 → AppealOrchestrationService ──
        if (completedAgent.Equals("Reviewer", StringComparison.OrdinalIgnoreCase))
        {
            // Stage 39：Skipped 結果（Vera 收到無可審檔案）也走「放行」路徑，跳過 Petra 評審
            if (!result.Success || result.ResultType == AgentResultType.Skipped)
            {
                if (result.ResultType == AgentResultType.Skipped)
                    logger.LogInformation("Vera 略過（{Summary}），跳過 Petra 審核，直接放行", result.Summary);
                else
                    logger.LogWarning("Vera 執行失敗（{Summary}），跳過 Petra 審核，直接放行", result.Summary);
                result = result with { CriticalReviewCount = 0 };
            }
            else
            {
                var groupProjectId = await GetGroupProjectIdAsync(group, taskRepo, cancellationToken);
                var reviewResult = await appealOrchestration.HandleReviewerCompletedAsync(
                    group, result, taskRepo, groupProjectId, cancellationToken);
                if (reviewResult is null) return;
                result = reviewResult;
            }
        }

        // ── Dev / Dev_fix 阻礙 → AppealOrchestrationService ──
        if ((completedAgent.Equals("Dev", StringComparison.OrdinalIgnoreCase)
             || completedAgent.Equals("Dev_fix", StringComparison.OrdinalIgnoreCase))
            && !result.Success
            && result.Summary.StartsWith("[BLOCKED]", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(result.OutputContent))
        {
            logger.LogWarning("Dev 回報阻礙，啟動 Petra 評估：Group={Id}", groupId);
            var groupProjectId = await GetGroupProjectIdAsync(group, taskRepo, cancellationToken);
            await appealOrchestration.HandleDevBlockerAsync(group, result, taskRepo, groupProjectId, cancellationToken);
            return;
        }

        // ── 仲裁後 Dev_fix 完成 → 跳過 Vera，直接 Petra 閘門 ──
        if (completedAgent.Equals("Dev_fix", StringComparison.OrdinalIgnoreCase)
            && result.Success
            && group.SkipReviewerAfterArbitration)
        {
            logger.LogInformation("仲裁後 Dev_fix 完成，跳過 Vera，直接交 Petra 閘門（Group={Id}）", group.Id);
            group.SkipReviewerAfterArbitration = false;
            await taskRepo.SaveAsync(cancellationToken);
            var groupProjectId = await GetGroupProjectIdAsync(group, taskRepo, cancellationToken);
            var petraResult = await appealOrchestration.RunPetraGateAsync(
                group, result, taskRepo, groupProjectId, cancellationToken);
            if (petraResult is null) return;
            result = petraResult;
        }

        // ── QA 修復模式 Dev_fix 完成 → 重新觸發 QA ──
        if (completedAgent.Equals("Dev_fix", StringComparison.OrdinalIgnoreCase)
            && result.Success
            && group.QaFixRound > 0)
        {
            logger.LogInformation("QA 修復後 Dev_fix 完成，重新觸發 QA（Group={Id}, Round={Round}）",
                group.Id, group.QaFixRound);
            await FireStepsAsync(group, [new WorkflowStep(AgentNames.Qa)], cancellationToken);
            return;
        }

        // ── Stage 43-B：Dev / Dev_fix 失敗 → 中止 fix loop + needs_intervention ──
        // 既有 line 239 只比對 "Dev"，漏 "Dev_fix"（AgentQueueProcessor.cs:256 傳 task.WorkflowAgentKey
        // 在 fix loop 時為 "Dev_fix"）。本 Stage 涵蓋兩者，但排除 "Dev_plan"（由 AppealOrchestrationService 處理）。
        if ((completedAgent.Equals("Dev",     StringComparison.OrdinalIgnoreCase) ||
             completedAgent.Equals("Dev_fix", StringComparison.OrdinalIgnoreCase))
            && !result.Success)
        {
            var isFixLoop = completedAgent.Equals("Dev_fix", StringComparison.OrdinalIgnoreCase);
            logger.LogError("Dev{Phase} 執行失敗，停止工作流程：Group={Id}，原因：{Summary}",
                isFixLoop ? "_fix" : " 初次", group.Id, result.Summary);
            taskRepo.UpdateGroupStatus(group, TaskStatus.NeedsIntervention);
            group.InterventionReason = $"Dev {(isFixLoop ? "fix" : "初次")} 失敗：{result.Summary}";
            await taskRepo.SaveAsync(cancellationToken);
            await NotifyBossDevFailedInterventionAsync(group, isFixLoop, result.Summary, cancellationToken);
            return;
        }

        // ── QA 完成 → QaCoordinationService ──
        if (completedAgent.Equals(AgentNames.Qa, StringComparison.OrdinalIgnoreCase) && result.Success)
        {
            await qaCoordination.HandleQaCompletedAsync(group, result, taskRepo, cancellationToken);
            return;
        }

        // ── 落底 WorkflowEngine.GetDecision ──
        var workflowType = group.WorkflowType switch
        {
            "new_feature"      => WorkflowType.NewFeature,
            "tech_improvement" => WorkflowType.TechImprovement,
            _                  => WorkflowType.BugFix
        };

        var decision = workflowEngine.GetDecision(
            workflowType, completedAgent, result, group.FixIteration);

        logger.LogInformation(
            "WorkflowEngine 決策：Group={Id}，completedAgent={Agent}，action={Action}",
            groupId, completedAgent, decision.Action);

        switch (decision.Action)
        {
            case NextAction.FireAgents:
                if (decision.NextSteps.Any(s => s.IsFixLoop))
                {
                    group.FixIteration++;
                    await taskRepo.SaveAsync(cancellationToken);
                }
                await FireStepsAsync(group, decision.NextSteps, cancellationToken);
                break;

            case NextAction.NotifyBossMerge:
                // Stage 43-E：透過守門 method 統一 mark done（檢查所有 task 無 failed/needs_intervention）
                await MarkGroupDoneOrInterventionAsync(group, taskRepo, cancellationToken);
                if (group.Status == TaskStatus.Done)
                    await NotifyBossMergeAsync(group, cancellationToken);
                else
                    await NotifyBossInterventionAsync(group, cancellationToken);
                break;

            case NextAction.NotifyBossIntervention:
                // Stage 43：fix loop 超限 = 介入後可恢復 → needs_intervention（與 failed 語意分離）
                taskRepo.UpdateGroupStatus(group, TaskStatus.NeedsIntervention);
                group.InterventionReason ??= $"Vera fix loop 超 {group.FixIteration} 次仍有問題";
                await taskRepo.SaveAsync(cancellationToken);
                await NotifyBossInterventionAsync(group, cancellationToken);
                break;

            case NextAction.Nothing:
                break;
        }
    }

    // ============================================================
    //  Mock Mode 輔助
    // ============================================================

    public async Task FireMockProposalAndContinueAsync(TaskGroup group, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[MockMode] 模擬提案核准完成，觸發 Kickoff 流程");
        await FireStepsAsync(group, [new WorkflowStep(AgentNames.Kickoff)], cancellationToken);
    }

    // ============================================================
    //  觸發 Agent 執行
    // ============================================================

    public async Task FireStepsAsync(
        TaskGroup group,
        IReadOnlyList<WorkflowStep> steps,
        CancellationToken cancellationToken = default)
    {
        if (steps.Count == 0) return;

        // Stage 45 Mock：偵測 PausePoint，模擬「外部按下暫停」的時序（驗收場景 B/C/D 用）。
        // 條件：PausePoint 已設且 groupId 匹配，且本次 fire 的 steps 含 beforeStep。
        if (MockClaudeCodeService.PausePoint is { } pp
            && pp.groupId == group.Id
            && steps.Any(s => s.AgentName.Equals(pp.beforeStep, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogInformation("[Stage45-MockPause] PausePoint 觸發：Group {Id} 即將 fire {Step} → 自動暫停",
                group.Id, pp.beforeStep);
            await PauseTaskGroupAsync(group.Id, "MockAutoPause", cancellationToken);
            MockClaudeCodeService.PausePoint = null; // 一次性
            // fall through 到 IsPaused 閘門
        }

        // Stage 45：暫停閘門 — fresh read DB 避免 stale cache
        if (await IsTaskGroupPausedAsync(group.Id, cancellationToken))
        {
            logger.LogInformation(
                "[Stage45-PauseGate] TaskGroup {Id} 暫停中，攔下 FireStepsAsync（steps={Steps}），記錄 PendingStepsJson 等待 Resume",
                group.Id, string.Join(",", steps.Select(s => s.AgentName)));

            await using var scope = serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fresh = await db.TaskGroups.FirstAsync(g => g.Id == group.Id, cancellationToken);
            fresh.PendingStepsJson = JsonSerializer.Serialize(steps);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        foreach (var step in steps)
            await FireOneStepAsync(group, step, cancellationToken);
    }

    // ============================================================
    //  Stage 45：TaskGroup 流程暫停（FF 三十四）
    // ============================================================

    /// <summary>
    /// Stage 45：fresh read TaskGroup.IsPaused（避免 stale cache）。
    /// 暫停可能在當前階段 subprocess 跑時被 Christ 從 Dashboard 按下（寫 DB），
    /// cached entity 不會反映 → 必須獨立 scope + 新 DbContext 讀最新（呼應 Stage 44 TokenLogService 風格）。
    /// </summary>
    internal async Task<bool> IsTaskGroupPausedAsync(Guid groupId, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TaskGroups
            .Where(g => g.Id == groupId)
            .Select(g => g.IsPaused)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Stage 45：標記 TaskGroup 為暫停。idempotent（已暫停則 no-op）。
    /// Dashboard 暫停按鈕 / Mock PausePoint 自動觸發共用此 method。
    /// </summary>
    public async Task PauseTaskGroupAsync(Guid groupId, string pausedBy, CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null) return;
        if (group.IsPaused) return;

        group.IsPaused = true;
        group.PausedAt = DateTime.UtcNow;
        group.PausedBy = pausedBy;
        await taskRepo.SaveAsync(ct);
        logger.LogInformation("[Stage45-Pause] TaskGroup {Id} 已標記暫停（by={By}）", groupId, pausedBy);
    }

    /// <summary>
    /// Stage 45：恢復暫停的 TaskGroup → 清 IsPaused/PausedAt/PausedBy/PendingStepsJson，
    /// 讀回先前被攔下的 steps 重新 FireStepsAsync。
    /// 若 PendingStepsJson 為 null（暫停期間沒有 next step 被攔），則只清旗標。
    /// </summary>
    public async Task ResumeTaskGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null)
        {
            logger.LogWarning("[Stage45-Resume] group {Id} not found", groupId);
            return;
        }
        if (!group.IsPaused)
        {
            logger.LogInformation("[Stage45-Resume] group {Id} 並未暫停，no-op", groupId);
            return;
        }

        var pendingJson = group.PendingStepsJson;
        group.IsPaused = false;
        group.PausedAt = null;
        group.PausedBy = null;
        group.PendingStepsJson = null;
        await taskRepo.SaveAsync(ct);

        if (string.IsNullOrEmpty(pendingJson))
        {
            logger.LogInformation("[Stage45-Resume] group {Id} 無 PendingSteps，僅清旗標", groupId);
            return;
        }

        WorkflowStep[]? steps;
        try
        {
            steps = JsonSerializer.Deserialize<WorkflowStep[]>(pendingJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Stage45-Resume] PendingStepsJson 反序列化失敗（group={Id}），僅清旗標", groupId);
            return;
        }
        if (steps is null || steps.Length == 0) return;

        // 路線 C：Resume race condition 觀察 log（驗收期掃同 Group 短時間內是否兩次 fire）
        logger.LogInformation("[Stage45-ResumeFire] Group {Id} resume → fire steps={Steps}",
            groupId, string.Join(",", steps.Select(s => s.AgentName)));
        await FireStepsAsync(group, steps, ct);
    }

    private async Task FireOneStepAsync(
        TaskGroup group,
        WorkflowStep step,
        CancellationToken cancellationToken)
    {
        // Stage 53A：framework Pipeline 從 Dev_plan 階段啟動（Aria 方案 C 拍板，2026-05-03）
        // single point of entry — 6 處 fire Dev_plan 散點全經過 FireOneStepAsync 統一節流
        // 排除條件：① WorkflowType=NewFeature 主路徑 ② sub-task 排除（ParentGroupId == null，Stage 46 機制 Stage 55 收尾整合）
        //          ③ PipelineFrameworkStateJson == null（entry guard，避免遞迴：Pipeline 啟動後 marker != null，下游 FireStepsAsync 自然走 legacy）
        //          ④ AgentName == Dev_plan ⑤ feature flag UseFrameworkPipeline=true
        if (group.WorkflowType == "new_feature"
            && group.ParentGroupId == null
            && group.PipelineFrameworkStateJson == null
            && step.AgentName.Equals("Dev_plan", StringComparison.OrdinalIgnoreCase))
        {
            await using var flagScope = serviceProvider.CreateAsyncScope();
            var workflowResolver = flagScope.ServiceProvider.GetRequiredService<Configuration.WorkflowSettingsResolver>();
            if (await workflowResolver.GetUseFrameworkPipelineAsync(cancellationToken))
            {
                logger.LogInformation("[Stage53A] Pipeline framework path 從 Dev_plan 啟動（Group={Id}）", group.Id);
                var router = flagScope.ServiceProvider.GetRequiredService<Meeting.FrameworkPipelineRouter>();
                // Aria 議題 11 修法：fire-and-forget 一行 ContinueWith pattern（避免 Task.Run + appLifetime 兩層包裝）
                _ = router.HandlePipelineAsync(group, appLifetime.ApplicationStopping)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted) logger.LogError(t.Exception, "[Stage53A] HandlePipelineAsync 異常");
                    }, TaskContinuationOptions.OnlyOnFaulted);
                return;
            }
        }

        // Kickoff / Design 步驟交由 MeetingOrchestrationService
        if (step.AgentName.Equals(AgentNames.Kickoff, StringComparison.OrdinalIgnoreCase))
        {
            _ = Task.Run(async () =>
            {
                try { await meetingOrchestration.RunKickoffMeetingAndWaitAsync(group, appLifetime.ApplicationStopping); }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TaskGroupService：Kickoff 會議執行失敗（Group={Id}）", group.Id);
                }
            }, appLifetime.ApplicationStopping);
            return;
        }

        if (step.AgentName.Equals(AgentNames.Design, StringComparison.OrdinalIgnoreCase))
        {
            _ = Task.Run(async () =>
            {
                try { await meetingOrchestration.RunDesignPhaseAsync(group, appLifetime.ApplicationStopping); }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TaskGroupService：Design 會議執行失敗（Group={Id}）", group.Id);
                }
            }, appLifetime.ApplicationStopping);
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var projectId = string.IsNullOrWhiteSpace(group.Project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(group.Project, cancellationToken);

        var workflowAgentKey = step.IsFixLoop && step.AgentName == AgentNames.Dev
            ? "Dev_fix"
            : step.AgentName;

        var taskItem = new TaskItem
        {
            Title            = $"{group.Title}（{step.AgentName}）",
            Description      = BuildTaskDescription(group, step),
            TriggeredBy      = "Orchestrator",
            AssignedAgent    = step.AgentName,
            Status           = "queued",
            GroupId          = group.Id,
            ProjectId        = projectId,
            WorkflowAgentKey = workflowAgentKey,
        };

        taskRepo.Add(taskItem);
        await taskRepo.SaveAsync(cancellationToken);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = taskItem.Id,
            GroupId   = group.Id,
            Title     = taskItem.Title,
            AgentName = taskItem.AssignedAgent,
            Status    = "queued"
        });

        await agentQueueService.EnqueueAsync(taskItem, cancellationToken);

        logger.LogInformation("TaskGroupService：{Agent} 任務已入佇列（Task={Id}，Group={GroupId}）",
            step.AgentName, taskItem.Id, group.Id);
    }

    // ============================================================
    //  取消任務（Stage 14）
    // ============================================================

    public async Task CancelAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var group = await taskRepo.GetGroupByIdAsync(groupId, cancellationToken);
        if (group is null)
        {
            logger.LogWarning("CancelAsync：找不到 TaskGroup（Id={Id}）", groupId);
            return;
        }

        await agentQueueService.CancelQueuedTasksForGroupAsync(groupId, cancellationToken);

        foreach (var task in group.Tasks)
            agentQueueService.TryCancel(task.Id);

        taskRepo.CancelGroupItems(group);
        taskRepo.UpdateGroupStatus(group, "cancelled");
        await taskRepo.SaveAsync(cancellationToken);

        logger.LogInformation("TaskGroup {Id}（{Title}）已取消", groupId, group.Title);
    }

    // ============================================================
    //  通知老闆（public 供子 service 呼叫）
    // ============================================================

    public async Task NotifyBossMergeAsync(TaskGroup group, CancellationToken cancellationToken)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null) return;

        var prLink = string.IsNullOrWhiteSpace(group.DevPrUrl)
            ? "（無 PR 連結）"
            : group.DevPrUrl;

        await ceoChannel.SendMessageAsync(
            $"✅ **{group.Title}** — 全流程完成！\n" +
            $"PR：{prLink}（含 code + tests + docs）\n" +
            $"請確認後即可合併 👆");

        logger.LogInformation("TaskGroup {Id} 通知老闆可以 merge PR", group.Id);

        _ = interactionService.CreateInteractionAsync(
            "merge_notify",
            title:                $"全流程完成：{group.Title}",
            description:          $"PR：{prLink}（含 code + tests + docs），請確認後合併。",
            project:              group.Project,
            agentName:            null,
            availableActionsJson: InteractionService.NotifyActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId = ceoChannel.Id.ToString(),
                groupId   = group.Id.ToString(),
                prUrl     = group.DevPrUrl ?? ""
            }),
            taskGroupId: group.Id);
    }

    /// <summary>
    /// Stage 43-E：統一 mark done 守門。檢查 group 下所有 TaskItem，若有任何 failed/needs_intervention →
    /// group 不 mark done，改 mark needs_intervention（呼應 Trial_v4 Bug #11，避免分散判定漏壞 task）。
    ///
    /// 取代分散在 4 處的 `taskRepo.UpdateGroupStatus(group, "done")`：
    ///   - TaskGroupService.HandleAgentCompletedAsync NotifyBossMerge
    ///   - QaCoordinationService passed → done
    ///   - QaCoordinationService no_applicable_tests + approve + Merge → done
    ///   - QaCoordinationService env_or_test_issue + Merge → done
    /// </summary>
    public async Task MarkGroupDoneOrInterventionAsync(
        TaskGroup group, TaskRepository taskRepo, CancellationToken ct)
    {
        await taskRepo.SaveAsync(ct);

        // 重抓 group 含 Tasks 確保 Tasks 集合最新（其他 scope 可能已寫入新 task）
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fresh = await db.TaskGroups
            .Include(g => g.Tasks)
            .FirstOrDefaultAsync(g => g.Id == group.Id, ct);

        var tasks = fresh?.Tasks ?? group.Tasks;

        // Stage 54 follow-up #1：忽略「同 AssignedAgent 後續有 newer success task」的舊 failed task。
        // 場景：① Reviewer fix loop Round N Dev_fix failed + Round N+1 success；② Dev [BLOCKED] retry Round N Dev failed + Round N+1 Dev success。
        // 既有判斷會誤判 needs_intervention（Round 1 failed task 殘留）→ Pipeline + legacy 路徑都受惠。
        // 註：判斷條件不限 IsFixLoop=true（dev_blocker retry 後 task 也是 IsFixLoop=false），改用「同 AssignedAgent newer success task」廣義判斷。
        var bad = tasks.Where(t => t.Status is "failed" or "needs_intervention").ToList();
        var unresolved = bad
            .Where(b => !tasks.Any(t => t.Status == "done"
                                     && t.AssignedAgent == b.AssignedAgent
                                     && t.CreatedAt > b.CreatedAt))
            .ToList();
        var anyBad = unresolved.Any();

        var supersededCount = bad.Count - unresolved.Count;
        if (supersededCount > 0)
        {
            logger.LogInformation(
                "[Stage54] MarkGroupDoneOrIntervention：group {Id} 跳過 {Count} 個被 newer success task 取代的舊 failed task（修法跳過 Round N 失敗 task）",
                group.Id, supersededCount);
        }

        if (anyBad)
        {
            taskRepo.UpdateGroupStatus(group, TaskStatus.NeedsIntervention);
            group.InterventionReason ??= "存在未處理的 failed / needs_intervention task";
            logger.LogWarning("MarkGroupDoneOrIntervention：group {Id} 有 failed/needs_intervention task → needs_intervention",
                group.Id);
            await taskRepo.SaveAsync(ct);

            // Stage 46-FF 三十五：sub-task needs_intervention → epic 標 EpicPaused + 建 BossInteraction
            if (group.ParentGroupId is not null)
                await PauseEpicAndNotifyAsync(group, ct);
        }
        else
        {
            taskRepo.UpdateGroupStatus(group, TaskStatus.Done);
            await taskRepo.SaveAsync(ct);

            // Stage 46-FF 三十五：sub-task done → 啟動下個 Phase or 標 epic 主 group done
            await TriggerNextPhaseIfSubTaskAsync(group, ct);
        }
    }

    /// <summary>
    /// Stage 43-B：Dev / Dev_fix 失敗 → 中止 fix loop，通知老闆介入。
    /// 與 NotifyBossInterventionAsync（fix loop 超限走 intervention type）區分用 dev_failed_intervention 細類。
    /// </summary>
    public async Task NotifyBossDevFailedInterventionAsync(
        TaskGroup group, bool isFixLoop, string failSummary, CancellationToken cancellationToken)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null) return;

        var phaseLabel = isFixLoop ? "Dev_fix" : "Dev";
        await ceoChannel.SendMessageAsync(
            $"⚠️ **{group.Title}** — {phaseLabel} 階段失敗，已中止流程。\n" +
            $"原因：{(failSummary.Length > 300 ? failSummary[..300] + "..." : failSummary)}\n" +
            $"PR：{group.DevPrUrl ?? "（無）"}");

        logger.LogWarning("TaskGroup {Id} {Phase} failed，中止 fix loop（Reason={R}）",
            group.Id, phaseLabel, failSummary);

        _ = interactionService.CreateInteractionAsync(
            "dev_failed_intervention",
            title:                $"{phaseLabel} 失敗：{group.Title}",
            description:          $"{phaseLabel} 階段失敗，已中止流程，需要您決定後續處理。原因：{(failSummary.Length > 500 ? failSummary[..500] + "..." : failSummary)}",
            project:              group.Project,
            agentName:            AgentNames.Dev,
            availableActionsJson: InteractionService.DevFailedInterventionActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId = ceoChannel.Id.ToString(),
                groupId   = group.Id.ToString(),
                phase     = phaseLabel,
                isFixLoop
            }),
            taskGroupId: group.Id);
    }

    public async Task NotifyBossInterventionAsync(TaskGroup group, CancellationToken cancellationToken)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null) return;

        await ceoChannel.SendMessageAsync(
            $"⚠️ **{group.Title}** — Vera 在 {group.FixIteration} 次修復後仍發現 🔴 問題，需要您介入處理。\n" +
            $"PR：{group.DevPrUrl ?? "（無）"}");

        logger.LogWarning("TaskGroup {Id} 修復次數超限（{Count} 次），升級給老闆", group.Id, group.FixIteration);

        _ = interactionService.CreateInteractionAsync(
            "intervention",
            title:                $"需要介入：{group.Title}",
            description:          $"Vera 在 {group.FixIteration} 次修復後仍發現問題，需要您介入處理。",
            project:              group.Project,
            agentName:            null,
            availableActionsJson: InteractionService.NotifyActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId    = ceoChannel.Id.ToString(),
                groupId      = group.Id.ToString(),
                prUrl        = group.DevPrUrl ?? "",
                fixIteration = group.FixIteration
            }),
            taskGroupId: group.Id);
    }

    // ============================================================
    //  Meeting service 薄 wrapper（保留 public 簽名供外部 caller）
    // ============================================================

    public Task RecoverStuckOrchestrationsAsync(CancellationToken ct)
        => meetingOrchestration.RecoverStuckOrchestrationsAsync(ct);

    public Task HandleKickoffConfirmedAsync(
        Guid groupId, string decision, string? modifyContent = null, CancellationToken ct = default)
        => meetingOrchestration.HandleKickoffConfirmedAsync(groupId, decision, modifyContent, ct);

    public Task HandleDesignConfirmedAsync(
        Guid groupId, string decision, string petraSessionId,
        string? modifyContent = null, CancellationToken ct = default)
        => meetingOrchestration.HandleDesignConfirmedAsync(groupId, decision, petraSessionId, modifyContent, ct);

    // ============================================================
    //  Dashboard 回覆分派入口（Stage 28a）
    // ============================================================

    public async Task ProcessBossResponseAsync(
        string interactionType, string action, string? contextJson,
        string? responseContent = null, CancellationToken ct = default)
    {
        switch (interactionType)
        {
            case "ceo_confirm":
                if (action == "confirm_yes" && contextJson is not null)
                    await proposalConfirmation.ProcessCeoConfirmAsync(contextJson, ct);
                else
                    logger.LogInformation("InteractionProcessor：CEO 確認取消（action={Action}）", action);
                break;

            case "exec_confirm":
                if (action == "exec_yes" && contextJson is not null)
                    await proposalConfirmation.ProcessExecConfirmAsync(contextJson, ct);
                else if (action == "exec_no" && contextJson is not null)
                    await proposalConfirmation.CancelTaskItemFromContextAsync(contextJson, ct);
                else
                    logger.LogInformation("InteractionProcessor：Agent 執行取消（action={Action}）", action);
                break;

            case "proposal":
                if (action == "propose_yes" && contextJson is not null)
                    await proposalConfirmation.ProcessProposalApprovedAsync(contextJson, ct);
                else if (action == "propose_adjust" && contextJson is not null)
                    await proposalConfirmation.ProcessProposalAdjustAsync(contextJson, responseContent, ct);
                else
                    logger.LogInformation("InteractionProcessor：提案取消（action={Action}）", action);
                break;

            case "kickoff":
            {
                if (contextJson is null) return;
                using var doc  = JsonDocument.Parse(contextJson);
                var groupIdStr = doc.RootElement.GetProperty("groupId").GetString() ?? "";
                if (!Guid.TryParse(groupIdStr, out var groupId)) return;
                var decision   = action.Replace("kickoff_", "");
                await HandleKickoffConfirmedAsync(groupId, decision, responseContent, ct);
                break;
            }

            case "design":
            {
                if (contextJson is null) return;
                using var doc        = JsonDocument.Parse(contextJson);
                var groupIdStr       = doc.RootElement.GetProperty("groupId").GetString() ?? "";
                var petraSessionId   = doc.RootElement.GetProperty("petraSessionId").GetString() ?? "";
                if (!Guid.TryParse(groupIdStr, out var groupId)) return;
                var decision         = action.Replace("design_", "");
                await HandleDesignConfirmedAsync(groupId, decision, petraSessionId, responseContent, ct);
                break;
            }

            case "devplan_escalate":
            case "dev_plan_unable":
                // Stage 43-A：dev_plan_unable 重用 devplan_escalate 路由（按鈕行為相同）
                if (contextJson is not null)
                    await appealOrchestration.HandleDevPlanEscalationAsync(contextJson, action, ct);
                break;

            case "dev_failed_intervention":
                if (contextJson is not null)
                    await HandleDevFailedInterventionAsync(contextJson, action, ct);
                break;

            case "qa_failed_intervention":
                if (contextJson is not null)
                    await HandleQaFailedInterventionAsync(contextJson, action, ct);
                break;

            case "sage_escalate":
                if (contextJson is not null)
                    await HandleSageEscalateAsync(contextJson, action, ct);
                break;

            // Stage 46-FF 三十五：拆 task 提案 + epic 部分暫停
            case "split_task_proposal":
                if (contextJson is not null)
                    await HandleSplitTaskProposalAsync(contextJson, action, responseContent, ct);
                break;

            case "epic_partial_paused":
                if (contextJson is not null)
                    await HandleEpicPartialPausedAsync(contextJson, action, ct);
                break;

            // Stage 51：framework HITL 試點 — Christ 中途介入回應路由
            case "framework_kickoff_mid_interrupt":
            {
                if (contextJson is null) break;
                using var doc = JsonDocument.Parse(contextJson);
                if (!doc.RootElement.TryGetProperty("groupId", out var g)
                    || !Guid.TryParse(g.GetString(), out var groupId))
                    break;

                await using var scope = serviceProvider.CreateAsyncScope();
                var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
                var group    = await taskRepo.GetGroupByIdAsync(groupId, ct);
                if (group is null) break;

                var bridge = scope.ServiceProvider
                    .GetRequiredService<AiTeam.Bot.Orchestration.Hitl.FrameworkHitlBridge>();
                await bridge.HandleMidInterruptResponseAsync(group, action, responseContent, ct);
                break;
            }

            default:
                logger.LogInformation("InteractionProcessor：無需處理的互動類型（{Type}）", interactionType);
                break;
        }
    }

    // ============================================================
    //  Stage 43：4 個新 BossInteraction type 的回覆分派
    // ============================================================

    /// <summary>Stage 43-B：Dev / Dev_fix failed intervention 路由（skip / retry / abort）。</summary>
    private async Task HandleDevFailedInterventionAsync(string contextJson, string action, CancellationToken ct)
    {
        using var doc  = JsonDocument.Parse(contextJson);
        if (!doc.RootElement.TryGetProperty("groupId", out var g)
            || !Guid.TryParse(g.GetString(), out var groupId))
            return;

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group    = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null) return;

        switch (action)
        {
            case "dev_intervention_skip":
                // 略過進下一階段 → fire Reviewer
                logger.LogInformation("Dev failed 介入：略過進 Reviewer（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "running");
                group.InterventionReason = null;
                await taskRepo.SaveAsync(ct);
                await FireStepsAsync(group, [new WorkflowStep(AgentNames.Reviewer)], ct);
                break;

            case "dev_intervention_retry":
                // 重啟 Dev
                logger.LogInformation("Dev failed 介入：重啟 Dev（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "running");
                group.InterventionReason = null;
                await taskRepo.SaveAsync(ct);
                await FireStepsAsync(group, [new WorkflowStep(AgentNames.Dev)], ct);
                break;

            case "dev_intervention_abort":
                // 放棄任務
                logger.LogInformation("Dev failed 介入：放棄任務（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "failed");
                await taskRepo.SaveAsync(ct);
                break;

            default:
                logger.LogWarning("Unknown dev_failed_intervention action: {A}", action);
                break;
        }
    }

    /// <summary>Stage 43-E：QA failed intervention 路由（continue / skip / abort）。</summary>
    private async Task HandleQaFailedInterventionAsync(string contextJson, string action, CancellationToken ct)
    {
        using var doc  = JsonDocument.Parse(contextJson);
        if (!doc.RootElement.TryGetProperty("groupId", out var g)
            || !Guid.TryParse(g.GetString(), out var groupId))
            return;

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group    = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null) return;

        switch (action)
        {
            case "qa_intervention_continue":
                // 再試一輪 QA
                logger.LogInformation("QA failed 介入：再試一輪 QA（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "running");
                group.InterventionReason = null;
                await taskRepo.SaveAsync(ct);
                await FireStepsAsync(group, [new WorkflowStep(AgentNames.Qa)], ct);
                break;

            case "qa_intervention_skip":
                // 略過進 Doc
                logger.LogInformation("QA failed 介入：略過進 Doc（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "running");
                group.InterventionReason = null;
                await taskRepo.SaveAsync(ct);
                await FireStepsAsync(group, [new WorkflowStep(AgentNames.Doc)], ct);
                break;

            case "qa_intervention_abort":
                logger.LogInformation("QA failed 介入：放棄任務（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "failed");
                await taskRepo.SaveAsync(ct);
                break;

            default:
                logger.LogWarning("Unknown qa_failed_intervention action: {A}", action);
                break;
        }
    }

    /// <summary>Stage 43-F：Sage escalate 路由（retry / skip / abort）。</summary>
    private async Task HandleSageEscalateAsync(string contextJson, string action, CancellationToken ct)
    {
        using var doc  = JsonDocument.Parse(contextJson);
        if (!doc.RootElement.TryGetProperty("groupId", out var g)
            || !Guid.TryParse(g.GetString(), out var groupId))
            return;

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group    = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null) return;

        switch (action)
        {
            case "sage_retry":
                // 重跑 Doc 階段
                logger.LogInformation("Sage escalate：重跑 Doc（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "running");
                group.InterventionReason = null;
                await taskRepo.SaveAsync(ct);
                await FireStepsAsync(group, [new WorkflowStep(AgentNames.Doc)], ct);
                break;

            case "sage_skip":
                // 略過歸檔，標完成（透過守門 method 確認）
                logger.LogInformation("Sage escalate：略過歸檔標完成（Group={Id}）", groupId);
                group.InterventionReason = null;
                await taskRepo.SaveAsync(ct);
                await MarkGroupDoneOrInterventionAsync(group, taskRepo, ct);
                break;

            case "sage_abort":
                logger.LogInformation("Sage escalate：保持 needs_intervention（Group={Id}）", groupId);
                // 保持 needs_intervention 狀態（不變）
                break;

            default:
                logger.LogWarning("Unknown sage_escalate action: {A}", action);
                break;
        }
    }

    // ============================================================
    //  輔助方法
    // ============================================================

    private async Task<Guid?> GetGroupProjectIdAsync(
        TaskGroup group, TaskRepository taskRepo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(group.Project)) return null;
        var projectId = await taskRepo.GetProjectIdByNameAsync(group.Project, cancellationToken);
        if (projectId is null)
            logger.LogWarning("HandleAgentCompleted：找不到專案名稱 '{Project}'，Petra TaskItem.ProjectId 將為 null", group.Project);
        return projectId;
    }

    /// <summary>
    /// 組建 TaskItem.Description，附帶 CEO 傳遞給 Dev 的上下文 metadata。
    /// </summary>
    private static string BuildTaskDescription(TaskGroup group, WorkflowStep step)
    {
        var desc = group.Title;

        if (step.AgentName.Equals("Dev_plan", StringComparison.OrdinalIgnoreCase))
        {
            var parts = new List<string> { desc };
            var meta  = new List<string> { "dev_plan_mode: true" };

            if (!string.IsNullOrWhiteSpace(group.IssueUrls))
                meta.Add($"issue_urls: {group.IssueUrls}");
            if (!string.IsNullOrWhiteSpace(group.UiSpecContent))
                meta.Add($"ui_spec_content:\n{group.UiSpecContent}");

            if (!string.IsNullOrWhiteSpace(group.DevPlan))
                meta.Add($"prev_dev_plan:\n{group.DevPlan}");

            if (!string.IsNullOrWhiteSpace(group.TaskPlan))
                meta.Add($"kickoff_task_plan:\n{group.TaskPlan}");

            if (!string.IsNullOrWhiteSpace(group.DesignPlan))
                meta.Add($"design_plan:\n{group.DesignPlan}");

            parts.Add("---");
            parts.AddRange(meta);
            parts.Add("---");

            return string.Join("\n", parts);
        }

        if (step.AgentName is AgentNames.Dev or AgentNames.Reviewer or AgentNames.Qa or AgentNames.Doc)
        {
            var parts = new List<string> { desc };

            if (!string.IsNullOrWhiteSpace(group.DevPrUrl))
                parts.Add($"PR 連結：{group.DevPrUrl}");

            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(group.IssueUrls))
                meta.Add($"issue_urls: {group.IssueUrls}");
            if (!string.IsNullOrWhiteSpace(group.UiSpecContent))
                meta.Add($"ui_spec_content:\n{group.UiSpecContent}");

            if (step.AgentName == AgentNames.Dev && !string.IsNullOrWhiteSpace(group.DevPlan))
                meta.Add($"dev_plan:\n{group.DevPlan}");

            if (!string.IsNullOrWhiteSpace(group.TaskPlan))
                meta.Add($"kickoff_task_plan:\n{group.TaskPlan}");

            if (!string.IsNullOrWhiteSpace(group.DesignPlan))
                meta.Add($"design_plan:\n{group.DesignPlan}");

            if ((step.AgentName == AgentNames.Reviewer || step.AgentName == AgentNames.Qa)
                && !string.IsNullOrWhiteSpace(group.ImplementationNote))
                meta.Add($"implementation_note:\n{group.ImplementationNote}");

            if (step.AgentName == AgentNames.Reviewer && !string.IsNullOrWhiteSpace(group.DevPlan))
                meta.Add($"dev_plan:\n{group.DevPlan}");

            if (step.AgentName == AgentNames.Qa)
            {
                if (!string.IsNullOrWhiteSpace(group.IssueUrls))
                    meta.Add($"issues_list: {group.IssueUrls}");
                if (!string.IsNullOrWhiteSpace(group.DevPlan))
                    meta.Add($"dev_plan:\n{group.DevPlan}");
            }

            if (step.AgentName == AgentNames.Doc && !string.IsNullOrWhiteSpace(group.TestReport))
                meta.Add($"test_report:\n{group.TestReport}");

            if (step.IsFixLoop)
            {
                meta.Add("fix_loop: true");
                if (!string.IsNullOrWhiteSpace(group.LastReviewBody))
                    meta.Add($"vera_review:\n{group.LastReviewBody}");
            }

            if (meta.Count > 0)
            {
                parts.Add("---");
                parts.AddRange(meta);
                parts.Add("---");
            }

            return string.Join("\n", parts);
        }

        return desc;
    }

    private IMessageChannel? FindChannel(string channelName)
    {
        if (!ulong.TryParse(_discord.GuildId, out var guildId)) return null;
        return discordClient.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }

    // ============================================================
    //  Stage 46-FF 三十五：自動拆任務（epic / sub-task / Sequential 鏈）
    // ============================================================

    /// <summary>
    /// Stage 46-FF 三十五：split_task_proposal BossInteraction 4 按鈕分派。
    /// - split_accept → BuildEpicSubTasksAsync
    /// - split_modify → 解析 responseContent 改寫的 phases JSON，失敗 fallback 到 split_reject
    /// - split_reject → 不拆，照舊 fire Dev_plan
    /// - split_abort  → mark cancelled
    /// </summary>
    private async Task HandleSplitTaskProposalAsync(
        string contextJson, string action, string? responseContent, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(contextJson);
        if (!doc.RootElement.TryGetProperty("groupId", out var g)
            || !Guid.TryParse(g.GetString(), out var groupId))
            return;
        var splitProposalJson = doc.RootElement.TryGetProperty("splitProposalJson", out var sp)
            ? sp.GetString() ?? ""
            : "";

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group    = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null)
        {
            logger.LogWarning("HandleSplitTaskProposal：找不到 TaskGroup ({Id})", groupId);
            return;
        }

        switch (action)
        {
            case "split_accept":
            {
                var proposal = DesignSplitProposalEvaluator.TryParseSplitProposal(splitProposalJson);
                if (proposal is null || !proposal.ShouldSplit || proposal.Phases is { Count: 0 })
                {
                    logger.LogWarning("split_accept：原始 splitProposalJson 解析失敗，fallback 到 split_reject");
                    await FireStepsAsync(group, [new WorkflowStep("Dev_plan")], ct);
                    return;
                }
                await BuildEpicSubTasksAsync(groupId, proposal, ct);
                break;
            }
            case "split_modify":
            {
                // v1.1 Aria 回饋 #2：Christ 從 TextInputDialog 改的 phases JSON 不一定合 schema，需防呆
                SplitProposal? modified = null;
                try { modified = DesignSplitProposalEvaluator.TryParseSplitProposal(responseContent ?? ""); }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "split_modify：Christ 改寫的 phases JSON 解析失敗，fallback 到 split_reject");
                }

                if (modified is null || !modified.ShouldSplit || modified.Phases is { Count: 0 })
                {
                    logger.LogInformation("split_modify fallback to split_reject（解析失敗或內容無效）");
                    await FireStepsAsync(group, [new WorkflowStep("Dev_plan")], ct);
                    return;
                }

                await BuildEpicSubTasksAsync(groupId, modified, ct);
                break;
            }
            case "split_reject":
                logger.LogInformation("split_reject：老闆選擇不拆，照舊 fire Dev_plan（Group={Id}）", groupId);
                await FireStepsAsync(group, [new WorkflowStep("Dev_plan")], ct);
                break;

            case "split_abort":
                logger.LogInformation("split_abort：老闆取消任務（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "cancelled");
                await taskRepo.SaveAsync(ct);
                break;

            default:
                logger.LogWarning("HandleSplitTaskProposal：未識別 action={Action}", action);
                break;
        }
    }

    /// <summary>
    /// Stage 46-FF 三十五：epic_partial_paused 卡片分派（恢復 epic / 放棄整個 epic）。
    /// </summary>
    private async Task HandleEpicPartialPausedAsync(
        string contextJson, string action, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(contextJson);
        if (!doc.RootElement.TryGetProperty("epicGroupId", out var g)
            || !Guid.TryParse(g.GetString(), out var epicId))
            return;

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var db       = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var epic = await taskRepo.GetGroupByIdAsync(epicId, ct);
        if (epic is null)
        {
            logger.LogWarning("HandleEpicPartialPaused：找不到 epic TaskGroup ({Id})", epicId);
            return;
        }

        switch (action)
        {
            case "epic_resume":
            {
                epic.EpicPaused = false;
                await taskRepo.SaveAsync(ct);

                // 找下個 pending sub-task fire Dev_plan
                var nextPending = await db.TaskGroups
                    .Where(t => t.ParentGroupId == epicId && t.Status == "pending")
                    .OrderBy(t => t.PhaseNumber)
                    .FirstOrDefaultAsync(ct);
                if (nextPending is not null)
                    await FireStepsAsync(nextPending, [new WorkflowStep("Dev_plan")], ct);
                break;
            }
            case "epic_abort":
            {
                // 標 epic + 所有 pending sub-task cancelled
                taskRepo.UpdateGroupStatus(epic, "cancelled");
                var subPending = await db.TaskGroups
                    .Where(t => t.ParentGroupId == epicId && t.Status == "pending")
                    .ToListAsync(ct);
                foreach (var s in subPending)
                    taskRepo.UpdateGroupStatus(s, "cancelled");
                await taskRepo.SaveAsync(ct);
                logger.LogInformation("epic_abort：epic + {Count} 個 pending sub-task 全標 cancelled（Epic={Id}）",
                    subPending.Count, epicId);
                break;
            }
            default:
                logger.LogWarning("HandleEpicPartialPaused：未識別 action={Action}", action);
                break;
        }
    }

    /// <summary>
    /// Stage 46-FF 三十五：依 SplitProposal phases 建 N 個 sub-task TaskGroup（共享 parent 4 大欄位）+ 啟動 Phase 1。
    /// v1.1 Aria 回饋 #1：idempotent 檢查防 double-click。
    /// v1.1 Aria 回饋 #3：簽名 Guid parentGroupId + 內部 fresh read parent，避免 stale 4 大欄位。
    /// </summary>
    public async Task BuildEpicSubTasksAsync(
        Guid parentGroupId, SplitProposal proposal, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ── v1.1 Aria 回饋 #1：idempotent 檢查（防雙 tab double-click 建重複 sub-task） ──
        if (await db.TaskGroups.AnyAsync(t => t.ParentGroupId == parentGroupId, ct))
        {
            logger.LogInformation(
                "BuildEpicSubTasksAsync：parent {Id} 已有 sub-task，視為重複呼叫，略過",
                parentGroupId);
            return;
        }

        // ── v1.1 Aria 回饋 #3：fresh read parent，避免 4 大欄位複製 stale 資料 ──
        var parent = await db.TaskGroups
            .FirstOrDefaultAsync(g => g.Id == parentGroupId, ct);
        if (parent is null)
        {
            logger.LogWarning("BuildEpicSubTasksAsync：找不到 parent {Id}", parentGroupId);
            return;
        }

        // 1. parent 標 epic 主 group：EpicPaused = false（議題 5）
        parent.EpicPaused = false;

        // 2. 依 phases 建 sub-task TaskGroup（共享 parent Kickoff/Design 4 大欄位）
        foreach (var phase in proposal.Phases)
        {
            var subGroup = new TaskGroup
            {
                Id               = Guid.NewGuid(),
                Title            = $"{parent.Title} - Phase {phase.Phase}: {phase.Description}", // 議題 7 命名
                Project          = parent.Project,
                ProjectId        = parent.ProjectId,
                Status           = "pending",
                WorkflowType     = parent.WorkflowType,
                ParentGroupId    = parent.Id,
                PhaseNumber      = phase.Phase,
                PhaseDescription = phase.Description,

                // sub-task 共享 parent Kickoff/Design 4 大欄位（FF 三十五 細節 2，fresh read 後複製）
                KickoffMeetingLog = parent.KickoffMeetingLog,
                TaskPlan          = parent.TaskPlan,
                DesignMeetingLog  = parent.DesignMeetingLog,
                DesignPlan        = parent.DesignPlan,

                // 共享 Issue 子集 + UI 規格（粗略策略：sub-task 都共享同一份，Cody Dev_plan 階段依 phase.Issues 自行對焦）
                IssueUrls     = FilterIssueUrls(parent.IssueUrls, phase.Issues),
                UiSpecContent = parent.UiSpecContent,
            };
            db.TaskGroups.Add(subGroup);
        }
        await db.SaveChangesAsync(ct);

        // 3. 啟動 Phase 1 sub-task（fire Dev_plan，跳過 Kickoff/Design）
        var phase1 = await db.TaskGroups
            .FirstOrDefaultAsync(g => g.ParentGroupId == parentGroupId && g.PhaseNumber == 1, ct);
        if (phase1 is not null)
        {
            logger.LogInformation(
                "BuildEpicSubTasksAsync：epic {Parent} 拆 {Count} 個 sub-task，啟動 Phase 1（{Phase1Id}）",
                parentGroupId, proposal.Phases.Count, phase1.Id);
            await FireStepsAsync(phase1, [new WorkflowStep("Dev_plan")], ct);
        }
        else
        {
            logger.LogWarning(
                "BuildEpicSubTasksAsync：找不到 Phase 1 sub-task（Parent={Id}）",
                parentGroupId);
        }
    }

    /// <summary>
    /// Stage 46-FF 三十五：sub-task done 後 → 啟動下個 Phase or 標 epic 主 group done。
    /// 在 MarkGroupDoneOrInterventionAsync done 路徑被呼叫；epic.EpicPaused=true 時攔下不啟動下個 Phase。
    /// </summary>
    private async Task TriggerNextPhaseIfSubTaskAsync(TaskGroup group, CancellationToken ct)
    {
        if (group.ParentGroupId is null) return; // 不是 sub-task

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var db       = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var parent = await taskRepo.GetGroupByIdAsync(group.ParentGroupId.Value, ct);
        if (parent is null) return;

        // epic 暫停中 → 不啟動下個 Phase（議題 8 兩機制獨立 + Stage 45 IsPaused 對齊）
        if (parent.EpicPaused == true)
        {
            logger.LogInformation(
                "TriggerNextPhaseIfSubTask：epic {Parent} 暫停中，不啟動下個 Phase（current={Phase}）",
                parent.Id, group.PhaseNumber);
            return;
        }

        // 找下個 PhaseNumber + 1
        var nextPhaseNum = (group.PhaseNumber ?? 0) + 1;
        var nextPhase = await db.TaskGroups
            .FirstOrDefaultAsync(g => g.ParentGroupId == parent.Id && g.PhaseNumber == nextPhaseNum, ct);

        if (nextPhase is not null)
        {
            logger.LogInformation(
                "TriggerNextPhaseIfSubTask：Phase {Done} done → 啟動 Phase {Next}（Epic={Parent}）",
                group.PhaseNumber, nextPhaseNum, parent.Id);
            await FireStepsAsync(nextPhase, [new WorkflowStep("Dev_plan")], ct);
        }
        else
        {
            // 最後一個 Phase done → epic 主 group 標 done
            taskRepo.UpdateGroupStatus(parent, TaskStatus.Done);
            await taskRepo.SaveAsync(ct);
            logger.LogInformation(
                "TriggerNextPhaseIfSubTask：最後 Phase {Done} done → epic {Parent} 標 done",
                group.PhaseNumber, parent.Id);
        }
    }

    /// <summary>
    /// Stage 46-FF 三十五：sub-task failed/needs_intervention → epic 標 EpicPaused + 建 BossInteraction。
    /// </summary>
    private async Task PauseEpicAndNotifyAsync(TaskGroup subTask, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var parent   = await taskRepo.GetGroupByIdAsync(subTask.ParentGroupId!.Value, ct);
        if (parent is null) return;

        parent.EpicPaused = true;
        await taskRepo.SaveAsync(ct);

        logger.LogWarning(
            "PauseEpicAndNotify：sub-task Phase {Phase} needs_intervention → epic {Parent} EpicPaused=true",
            subTask.PhaseNumber, parent.Id);

        _ = interactionService.CreateInteractionAsync(
            "epic_partial_paused",
            title:                $"Epic 部分暫停：{parent.Title}",
            description:          $"Phase {subTask.PhaseNumber}（{subTask.PhaseDescription}）失敗，後續 Phase 已暫停。" +
                                  $"原因：{subTask.InterventionReason ?? "（無）"}",
            project:              parent.Project,
            agentName:            null,
            availableActionsJson: InteractionService.EpicPartialPausedActionsJson,
            contextJson: JsonSerializer.Serialize(new
            {
                epicGroupId       = parent.Id.ToString(),
                failedPhaseId     = subTask.Id.ToString(),
                failedPhaseNumber = subTask.PhaseNumber
            }),
            taskGroupId: parent.Id);
    }

    /// <summary>
    /// Stage 46-FF 三十五：從 parent IssueUrls JSON array 過濾 phase.Issues 對應的 URL 子集。
    /// 失敗（解析錯 / index 越界）→ 回 parent 整份（後續 Cody 階段依 DesignPlan 自行對焦）。
    /// </summary>
    private static string? FilterIssueUrls(string? parentIssueUrls, List<int> phaseIssueIds)
    {
        if (string.IsNullOrWhiteSpace(parentIssueUrls) || phaseIssueIds is { Count: 0 })
            return parentIssueUrls;

        try
        {
            using var doc = JsonDocument.Parse(parentIssueUrls);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return parentIssueUrls;

            var allUrls = doc.RootElement.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            // phaseIssueIds 為 1-based（對應 Rosa 拆解的 Issue 編號）
            var filtered = phaseIssueIds
                .Where(id => id >= 1 && id <= allUrls.Count)
                .Select(id => allUrls[id - 1])
                .ToList();

            return filtered.Count > 0
                ? JsonSerializer.Serialize(filtered)
                : parentIssueUrls;
        }
        catch { return parentIssueUrls; }
    }
}
