using System.Text.Json;
using TaskStatus = AiTeam.Shared.Constants.TaskStatus;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Orchestration.Boss;
using AiTeam.Bot.Orchestration.Epic;
using AiTeam.Bot.Orchestration.Hitl;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Orchestration.Routing;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using AiTeam.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiTeam.Bot.Orchestration;

/// <summary>
/// Stage 36 / Stage 59：任務群組管理與主流程 dispatcher（兩次拆解後最終瘦身版）。
///
/// 職責（A + C + D 主 dispatch + 守門邏輯）：
///   - A. Group lifecycle：CreateGroupAsync / HandleAgentCompletedAsync 主 dispatcher / FireStepsAsync / FireOneStepAsync /
///        Cancel / Pause/Resume / Mock proposal trigger / BuildTaskDescription
///   - 守門：MarkGroupDoneOrInterventionAsync — 跨 B+E 邏輯（B「group 標 done/intervention 決策」+ E「sub-task 觸發 epic chain」），
///        留主檔避免 BossNotification → EpicChain 反向耦合（對齊 SOP 4 子 service 單向依賴 Commons 不可反向）
///   - C. Meeting service 薄 wrapper（保留 public 簽名供外部 caller — RecoverStuckOrchestrations / HandleKickoff/Design Confirmed）
///   - D. ProcessBossResponseAsync 主 dispatch（switch 入口語義 + caller 不變）— 5 case body 委派 BossResponseHandlerService / EpicChainService / PipelineRoutingService
///
/// 拆出的職責（Stage 36 既有 4 子 service + Stage 59 新增 4 子 service）：
///   Stage 36：
///     - Kickoff / Design / Crash Recovery → <see cref="MeetingOrchestrationService"/>
///     - Review Appeal + Dev_plan Appeal + Petra 審核 → <see cref="AppealOrchestrationService"/>
///     - QA 路由 → <see cref="QaCoordinationService"/>
///     - Dashboard 路徑 Proposal/Exec Confirm → <see cref="ProposalConfirmationService"/>
///   Stage 59（FF 五十四子項 1）：
///     - 5 NotifyBoss helpers → <see cref="BossNotificationService"/>
///     - D 區段 4 case body（dev_failed / qa_failed / sage_escalate / split_task_proposal）→ <see cref="BossResponseHandlerService"/>
///     - Epic 自動拆任務機制（5 method）→ <see cref="EpicChainService"/>
///     - 7 type-specific Pipeline routing TryRoute → <see cref="PipelineRoutingService"/>
/// </summary>
public class TaskGroupService(
    IServiceProvider serviceProvider,
    AgentQueueService agentQueueService,
    IHostApplicationLifetime appLifetime,
    MeetingOrchestrationService meetingOrchestration,
    BossResponseHandlerService bossResponseHandler,
    EpicChainService epicChain,
    PipelineRoutingService pipelineRouting,
    ILogger<TaskGroupService> logger)
{
    // ============================================================
    //  A 區段：任務群組建立
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
    //  A 區段：主 dispatcher（Stage 36：瘦身版）
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
            var workflowResolver = pipelineScope.ServiceProvider.GetRequiredService<WorkflowSettingsResolver>();
            if (await workflowResolver.GetUseFrameworkPipelineAsync(cancellationToken))
            {
                logger.LogInformation(
                    "[Stage53A] HandleAgentCompletedAsync framework path 接管（Group={Id}, completedAgent={Agent}）",
                    groupId, completedAgent);
                var router = pipelineScope.ServiceProvider.GetRequiredService<FrameworkPipelineRouter>();
                await router.ResumeAfterAgentAsync(group, completedAgent, result, cancellationToken);
                return;
            }
        }

        // ── Stage 55A：6+1 hooks 全部移除（議題 J1 解法 — Pipeline framework 已涵蓋全 NewFeature 主路徑 + 子流程）──
        //   feature flag UseFrameworkPipeline=true 為唯一 production path（Christ 拍板 2026-05-03，無 legacy 退路）
        logger.LogInformation(
            "[Stage55A] HandleAgentCompletedAsync fall through（legacy fallback 已移除）— Group={Id}, completedAgent={Agent}",
            groupId, completedAgent);
    }

    // ============================================================
    //  A 區段：Mock Mode 輔助
    // ============================================================

    public async Task FireMockProposalAndContinueAsync(TaskGroup group, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[MockMode] 模擬提案核准完成，觸發 Kickoff 流程");
        await FireStepsAsync(group, [new WorkflowStep(AgentNames.Kickoff)], cancellationToken);
    }

    // ============================================================
    //  A 區段：觸發 Agent 執行
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
    //  A 區段：Stage 45 — TaskGroup 流程暫停（FF 三十四）
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
        // Stage 55A：framework Pipeline 從 Kickoff 階段啟動 + sub-task 走 Pipeline framework path
        var isParentKickoffEntry = step.AgentName.Equals(AgentNames.Kickoff, StringComparison.OrdinalIgnoreCase)
            && group.ParentGroupId == null;
        var isSubTaskDevPlanEntry = step.AgentName.Equals("Dev_plan", StringComparison.OrdinalIgnoreCase)
            && group.ParentGroupId != null;

        if (group.WorkflowType == "new_feature"
            && group.PipelineFrameworkStateJson == null
            && (isParentKickoffEntry || isSubTaskDevPlanEntry))
        {
            await using var flagScope = serviceProvider.CreateAsyncScope();
            var workflowResolver = flagScope.ServiceProvider.GetRequiredService<WorkflowSettingsResolver>();
            if (await workflowResolver.GetUseFrameworkPipelineAsync(cancellationToken))
            {
                logger.LogInformation(
                    "[Stage55A] Pipeline framework path 從 {Entry} 啟動（Group={Id}, ParentGroupId={ParentId}）",
                    isParentKickoffEntry ? "Kickoff" : "Dev_plan(sub-task)", group.Id, group.ParentGroupId);
                var router = flagScope.ServiceProvider.GetRequiredService<FrameworkPipelineRouter>();
                // Aria 議題 11 修法：fire-and-forget 一行 ContinueWith pattern
                _ = router.HandlePipelineAsync(group, appLifetime.ApplicationStopping)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted) logger.LogError(t.Exception, "[Stage55A] HandlePipelineAsync 異常");
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
    //  A 區段：取消任務（Stage 14）
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
    //  守門邏輯：Stage 43-E group 標 done/intervention 決策（跨 B+E 邏輯留主檔）
    // ============================================================

    /// <summary>
    /// Stage 43-E：統一 mark done 守門。檢查 group 下所有 TaskItem，若有任何 failed/needs_intervention →
    /// group 不 mark done，改 mark needs_intervention（呼應 Trial_v4 Bug #11，避免分散判定漏壞 task）。
    ///
    /// Stage 59 設計：留主檔 — 跨 B（group 標 done/intervention 決策）+ E（sub-task 觸發 epic chain via EpicChainService）邏輯，
    /// 留主檔避免 BossNotification → EpicChain 反向耦合（對齊 SOP 4 子 service 單向依賴 Commons 不可反向）。
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
            // Stage 59：epic 機制委派 EpicChainService.PauseEpicAndNotifyAsync
            if (group.ParentGroupId is not null)
                await epicChain.PauseEpicAndNotifyAsync(group, ct);
        }
        else
        {
            taskRepo.UpdateGroupStatus(group, TaskStatus.Done);
            await taskRepo.SaveAsync(ct);

            // Stage 46-FF 三十五：sub-task done → 啟動下個 Phase or 標 epic 主 group done
            // Stage 59：epic 機制委派 EpicChainService.TriggerNextPhaseIfSubTaskAsync
            await epicChain.TriggerNextPhaseIfSubTaskAsync(group, ct);
        }
    }

    // ============================================================
    //  C 區段：Meeting service 薄 wrapper（保留 public 簽名供外部 caller）
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
    //  D 區段：Dashboard 回覆分派入口（Stage 28a）— 5 case body 委派子 service（Stage 59）
    // ============================================================

    public async Task ProcessBossResponseAsync(
        string interactionType, string action, string? contextJson,
        string? responseContent = null, CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var proposalConfirmation = scope.ServiceProvider.GetRequiredService<Proposal.ProposalConfirmationService>();
        var appealOrchestration  = scope.ServiceProvider.GetRequiredService<Appeal.AppealOrchestrationService>();

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
            {
                // Stage 55B Session B：Pipeline path 接管 routing（Stage 59 委派 PipelineRoutingService）
                if (contextJson is not null && await pipelineRouting.TryRoutePipelineDevPlanEscalateAsync(contextJson, action, ct))
                    break;
                // Legacy fallback（PipelineFrameworkStateJson IS NULL 殘留 group 走原路）
                if (contextJson is not null)
                    await appealOrchestration.HandleDevPlanEscalationAsync(contextJson, action, ct);
                break;
            }

            case "dev_plan_unable":
            {
                if (contextJson is not null && await pipelineRouting.TryRoutePipelineDevPlanUnableAsync(contextJson, action, ct))
                    break;
                // Legacy fallback — Stage 43-A 重用 devplan_escalate 路由（EndsWith match）
                if (contextJson is not null)
                    await appealOrchestration.HandleDevPlanEscalationAsync(contextJson, action, ct);
                break;
            }

            case "dev_failed_intervention":
            {
                if (contextJson is not null && await pipelineRouting.TryRoutePipelineDevInterventionAsync(contextJson, action, ct))
                    break;
                if (contextJson is not null)
                    await bossResponseHandler.HandleDevFailedInterventionAsync(contextJson, action, ct);
                break;
            }

            case "qa_failed_intervention":
            {
                if (contextJson is not null && await pipelineRouting.TryRoutePipelineQaInterventionAsync(contextJson, action, ct))
                    break;
                if (contextJson is not null)
                    await bossResponseHandler.HandleQaFailedInterventionAsync(contextJson, action, ct);
                break;
            }

            case "sage_escalate":
                if (contextJson is not null)
                    await bossResponseHandler.HandleSageEscalateAsync(contextJson, action, ct);
                break;

            // Stage 46-FF 三十五：拆 task 提案 + epic 部分暫停
            case "split_task_proposal":
            {
                // Stage 55B Session B：Pipeline path 接管 routing（含 BuildEpicSubTasks Pipeline 自接管）
                if (contextJson is not null && await pipelineRouting.TryRoutePipelineSplitTaskProposalAsync(contextJson, action, responseContent, ct))
                    break;
                if (contextJson is not null)
                    await bossResponseHandler.HandleSplitTaskProposalAsync(contextJson, action, responseContent, ct);
                break;
            }

            case "epic_partial_paused":
                if (contextJson is not null)
                    await epicChain.HandleEpicPartialPausedAsync(contextJson, action, ct);
                break;

            // Stage 57-FF 五十二：Reviewer fix loop ×3 達 limit HITL routing（第 6 routing 對齊 Stage 55B Session B）
            case "reviewer_fix_loop_limit":
            {
                if (contextJson is not null && await pipelineRouting.TryRoutePipelineReviewerFixLoopLimitAsync(contextJson, action, ct))
                    break;
                // 無 legacy fallback — 此 type 為 Pipeline 專屬
                logger.LogWarning("[Stage57] reviewer_fix_loop_limit 收到回應但 Pipeline path 未接管（PipelineFrameworkStateJson IS NULL?），略過");
                break;
            }

            // Stage 58-FF 五十三：Agent API 失敗 HITL routing（第 7 routing 對齊 Stage 55B Session B + Stage 57 第 6 routing）
            case "agent_api_failure_intervention":
            {
                if (contextJson is not null && await pipelineRouting.TryRoutePipelineAgentApiFailureAsync(contextJson, action, ct))
                    break;
                // 無 legacy fallback — 此 type 為 Pipeline 專屬
                logger.LogWarning("[Stage58] agent_api_failure_intervention 收到回應但 Pipeline path 未接管（PipelineFrameworkStateJson IS NULL?），略過");
                break;
            }

            // Stage 51：framework HITL 試點 — Christ 中途介入回應路由
            case "framework_kickoff_mid_interrupt":
            {
                if (contextJson is null) break;
                using var doc = JsonDocument.Parse(contextJson);
                if (!doc.RootElement.TryGetProperty("groupId", out var g)
                    || !Guid.TryParse(g.GetString(), out var groupId))
                    break;

                await using var midScope = serviceProvider.CreateAsyncScope();
                var taskRepo = midScope.ServiceProvider.GetRequiredService<TaskRepository>();
                var group    = await taskRepo.GetGroupByIdAsync(groupId, ct);
                if (group is null) break;

                var bridge = midScope.ServiceProvider.GetRequiredService<FrameworkHitlBridge>();
                await bridge.HandleMidInterruptResponseAsync(group, action, responseContent, ct);
                break;
            }

            default:
                logger.LogInformation("InteractionProcessor：無需處理的互動類型（{Type}）", interactionType);
                break;
        }
    }

    // ============================================================
    //  輔助方法（A 區段內部 helper）
    // ============================================================

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
}
