using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Workflows.Appeal;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Orchestration.Appeal;

/// <summary>
/// Stage 49：MS Agent Framework Appeal Workflow 路由層（feature flag true 時接管 2 個 entry method）。
///
/// 設計（路線 B 拍板）：
///   - 5 個 entry method 中只有 2 個（HandleReviewerCompletedAsync + HandleDevPlanCompletedAsync）真正建 framework Workflow
///   - 3 個 entry（RunPetraGateAsync / HandleDevBlockerAsync / HandleDevPlanEscalationAsync）即使 feature flag 開仍走 legacy
///   - → FrameworkAppealRouter 只定義 2 個 method，避免循環依賴 AppealOrchestrationService
///
/// 職責：
///   - 從 group 設定 framework AppealState 起始狀態
///   - 標 ActiveOrchestration = "FrameworkAppeal"（搭配 FrameworkAppealStateJson 雙 marker 區隔 legacy/framework Crash Recovery）
///   - InProcessExecution.RunAsync(workflow, "trigger", checkpointManager, sessionId, ct) 跑 framework Workflow
///   - 拿 WorkflowOutputEvent 取得 AppealLoopResult → 把結果寫進既有 DB 欄位（task_groups / tasks / Discord 通知）
///   - escalate 路徑直接呼叫 legacy method（NotifyBossDevPlanEscalationAsync / NotifyBossInterventionAsync）— 不動 BossInteraction（Stage 51 才動）
///
/// Stage 49 不動的 legacy method：
///   - RunPetraGateAsync / HandleDevBlockerAsync / HandleDevPlanEscalationAsync entry pass-through 走 legacy
///   - PmReviewService / ReviewAppealService / DevPlanAppealService 內部邏輯（service 包裝路線 B 直接 call 既有 method）
/// </summary>
public class FrameworkAppealRouter(
    IServiceProvider serviceProvider,
    AppealWorkflowFactory workflowFactory,
    AppealCheckpointStore checkpointStore,
    WorkflowSettingsResolver workflowResolver,
    ILogger<FrameworkAppealRouter> logger)
{

    /// <summary>
    /// Stage 49：framework path 對應 AppealOrchestrationService.HandleReviewerCompletedAsync。
    /// </summary>
    public async Task<AgentExecutionResult?> HandleReviewerCompletedAsync(
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        // ── Crash Recovery 雙 marker 區隔（風險點 R2 緩解）──
        await using var dbScope = serviceProvider.CreateAsyncScope();
        var db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == group.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, "FrameworkAppeal"),
                cancellationToken);

        try
        {
            // Vera Critical 全 0 → 對齊 legacy 短路：直接 Petra Gate（單輪，無 framework Workflow）
            if (result.CriticalReviewCount == 0)
            {
                logger.LogInformation(
                    "[FrameworkAppealRouter] Group={Id}：無 Critical，直接 Petra Gate（fallback to legacy single-shot）",
                    group.Id);
                // 借用 legacy 的 RunPetraGateAsync — 雙系統並存期 OK，Stage 54 才真正獨立
                var legacy = serviceProvider.GetRequiredService<AppealOrchestrationService>();
                return await legacy.RunPetraGateAsync(group, result, taskRepo, projectId, cancellationToken);
            }

            var maxRounds = await workflowResolver.GetReviewAppealMaxRoundsAsync(cancellationToken);
            var reviewBody = group.LastReviewBody ?? result.ReviewBody ?? "";

            var currentCriticalIds = AppealOrchestrationService
                .ExtractCriticalIdsFromReviewBody(reviewBody)
                .ToList();
            if (currentCriticalIds.Count == 0)
            {
                logger.LogWarning(
                    "[FrameworkAppealRouter] Group={Id}：有 Critical 但無法解析 ID，直接走 Petra Gate", group.Id);
                var legacy = serviceProvider.GetRequiredService<AppealOrchestrationService>();
                return await legacy.RunPetraGateAsync(group, result, taskRepo, projectId, cancellationToken);
            }

            // ── 設 framework AppealState 起始 ──
            var sessionId = group.Id.ToString();
            await checkpointStore.LoadFromDbAsync(group.Id, cancellationToken);

            var initialState = new AppealState
            {
                GroupId = group.Id,
                Kind = AppealLoopKind.ReviewAppeal,
                Round = 1,
                MaxRounds = maxRounds,
                LastReviewBody = reviewBody,
                RemainingCriticalIds = currentCriticalIds,
            };

            // ── 跑 framework Workflow ──
            var workflow = workflowFactory.CreateReviewAppealWorkflow();
            var checkpointManager = workflowFactory.CreateCheckpointManager();

            var appealResult = await RunWorkflowAsync(
                workflow, checkpointManager, sessionId, initialState, cancellationToken);

            if (appealResult is null)
            {
                logger.LogError(
                    "[FrameworkAppealRouter] ReviewAppeal Workflow 未產生 AppealLoopResult（Group={Id}），fallback escalate",
                    group.Id);
                taskRepo.UpdateGroupStatus(group, "failed");
                await taskRepo.SaveAsync(cancellationToken);
                var tgs = serviceProvider.GetRequiredService<TaskGroupService>();
                await tgs.NotifyBossInterventionAsync(group, cancellationToken);
                return null;
            }

            // ── 翻譯 AppealLoopResult 對應 legacy 行為 ──
            return await TranslateReviewAppealVerdictAsync(
                appealResult, group, result, taskRepo, projectId, cancellationToken);
        }
        finally
        {
            await db.TaskGroups.Where(g => g.Id == group.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(g => g.ActiveOrchestration, (string?)null)
                    .SetProperty(g => g.FrameworkAppealStateJson, (string?)null),
                    CancellationToken.None);
        }
    }

    /// <summary>
    /// Stage 49：framework path 對應 AppealOrchestrationService.HandleDevPlanCompletedAsync。
    /// 回傳 true → caller 應繼續 dispatcher（approve）；false → 已處理（escalate / fire Dev）。
    /// </summary>
    public async Task<bool> HandleDevPlanCompletedAsync(
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var dbScope = serviceProvider.CreateAsyncScope();
        var db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == group.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, "FrameworkAppeal"),
                cancellationToken);

        try
        {
            group.DevPlan = result.OutputContent ?? result.Summary;
            await taskRepo.SaveAsync(cancellationToken);

            // DevPlan 失敗判定（對齊 legacy Stage 43-A）— framework 不接管此判斷，直接 fallback legacy
            var (planFailed, _) = Agents.Pm.PmAgentCommons.IsDevPlanFailed(group.DevPlan);
            if (planFailed)
            {
                logger.LogInformation(
                    "[FrameworkAppealRouter] Group={Id}：DevPlan 失敗，fallback legacy HandleDevPlanCompletedAsync",
                    group.Id);
                var legacy = serviceProvider.GetRequiredService<AppealOrchestrationService>();
                // legacy entry 內含 ActiveOrchestration 寫 "DevPlanAppeal"，與本路徑寫 "FrameworkAppeal" 衝突
                // 解：先清掉 ActiveOrchestration 再 delegate（finally 會再清一次，無 leak）
                await db.TaskGroups.Where(g => g.Id == group.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, (string?)null),
                        CancellationToken.None);
                return await legacy.HandleDevPlanCompletedAsync(group, result, taskRepo, projectId, cancellationToken);
            }

            // ── Petra 初審（service 包裝路線 B：直接 call legacy method 取 PetraReview）──
            var legacyAppealSvc = serviceProvider.GetRequiredService<AppealOrchestrationService>();
            var (petraDevPlanReview, petraDevPlanTaskId) =
                await legacyAppealSvc.RunPetraDevPlanReviewAsync(group, result, projectId, cancellationToken);

            switch (petraDevPlanReview.Decision)
            {
                case "approve":
                    return true;  // 繼續 dispatcher → 觸發 Dev

                case "revise":
                    // ── framework Cody-Petra Appeal Workflow ──
                    var maxRounds = await workflowResolver.GetDevPlanAppealMaxRoundsAsync(cancellationToken);
                    var sessionId = group.Id.ToString();
                    await checkpointStore.LoadFromDbAsync(group.Id, cancellationToken);

                    var initialState = new AppealState
                    {
                        GroupId = group.Id,
                        Kind = AppealLoopKind.DevPlanAppeal,
                        Round = 1,
                        MaxRounds = maxRounds,
                        DevPlan = group.DevPlan,
                        InitialPetraReview = new Workflows.Appeal.PetraReviewSnapshot
                        {
                            Decision = petraDevPlanReview.Decision,
                            Summary = petraDevPlanReview.Summary,
                            RevisionInstructions = petraDevPlanReview.RevisionInstructions,
                        },
                    };

                    var workflow = workflowFactory.CreateDevPlanAppealWorkflow();
                    var checkpointManager = workflowFactory.CreateCheckpointManager();

                    var appealResult = await RunWorkflowAsync(
                        workflow, checkpointManager, sessionId, initialState, cancellationToken);

                    var appealApproved = appealResult is not null && appealResult.Verdict == "approve";

                    await legacyAppealSvc.FinalizePetraDevPlanTaskAsync(
                        petraDevPlanTaskId, appealApproved, group, cancellationToken);

                    if (appealApproved)
                    {
                        logger.LogInformation(
                            "[FrameworkAppealRouter] Group={Id}：Dev_plan Appeal 說服成功，觸發 Dev",
                            group.Id);
                        var tgs = serviceProvider.GetRequiredService<TaskGroupService>();
                        await tgs.FireStepsAsync(group, [new WorkflowStep(AgentNames.Dev)], cancellationToken);
                    }
                    else
                    {
                        logger.LogWarning(
                            "[FrameworkAppealRouter] Group={Id}：Dev_plan Appeal 耗盡，升級老闆", group.Id);
                        taskRepo.UpdateGroupStatus(group, "failed");
                        await taskRepo.SaveAsync(cancellationToken);
                        await legacyAppealSvc.NotifyBossDevPlanEscalationAsync(
                            group, petraDevPlanReview, cancellationToken);
                    }
                    return false;

                default:  // escalate
                    taskRepo.UpdateGroupStatus(group, "failed");
                    await taskRepo.SaveAsync(cancellationToken);
                    await legacyAppealSvc.NotifyBossDevPlanEscalationAsync(
                        group, petraDevPlanReview, cancellationToken);
                    return false;
            }
        }
        finally
        {
            await db.TaskGroups.Where(g => g.Id == group.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(g => g.ActiveOrchestration, (string?)null)
                    .SetProperty(g => g.FrameworkAppealStateJson, (string?)null),
                    CancellationToken.None);
        }
    }

    /// <summary>
    /// Stage 49：Bot 啟動掃 task_groups.FrameworkAppealStateJson != null 的 group 重啟 framework Workflow。
    /// 對應 legacy MeetingOrchestrationService.RecoverStuckOrchestrationsAsync 的對等機制（雙系統各管自己）。
    /// </summary>
    public async Task RecoverStuckFrameworkAppealsAsync(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Stage 53A F-α 配套：避免 4 marker 共存的 Recovery 篩選優先級 collision
        // 當外層 PipelineFrameworkStateJson != null（framework-in-framework 場景），由 FrameworkPipelineRouter 接管 Recovery
        var stuckGroupIds = await db.TaskGroups
            .Where(g => g.FrameworkAppealStateJson != null && !g.IsPaused
                     && g.PipelineFrameworkStateJson == null)
            .Select(g => g.Id)
            .ToListAsync(ct);

        if (stuckGroupIds.Count == 0)
        {
            logger.LogInformation("[FrameworkAppealRouter] 啟動：無 stuck framework appeal");
            return;
        }

        logger.LogWarning(
            "[FrameworkAppealRouter] 啟動：發現 {Count} 個 stuck framework appeal，重啟 Workflow",
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
                        "[FrameworkAppealRouter] Recovery Group={Id}：FrameworkAppealStateJson 有值但 latest checkpoint 不存在，跳過",
                        groupId);
                    continue;
                }

                // ── Recovery：依 AppealState.Kind 判 ReviewAppeal 或 DevPlanAppeal Workflow ──
                // 注意：framework 1.3.0 ResumeAsync 自己接 fromCheckpoint，內部會還原 state
                // 詳細 recovery flow 由 framework 處理；router 只負責建 Workflow + ResumeAsync
                logger.LogInformation(
                    "[FrameworkAppealRouter] Recovery Group={Id}：framework Checkpointing 還原 superstep（latest={Ckpt}）",
                    groupId, latest.CheckpointId);

                // TODO Stage 49 後續驗收：實際 ResumeAsync 路徑由 Mock 場景 C 線下驗收驅動完整實作
                // 暫時策略：清掉 FrameworkAppealStateJson + ActiveOrchestration，讓既有 dispatcher 重新觸發 entry method
                // 此降級策略確保 Bot 重啟不卡死；Mock 場景 C 驗收後升級為真實 ResumeAsync
                await db.TaskGroups.Where(g => g.Id == groupId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(g => g.ActiveOrchestration, (string?)null)
                        .SetProperty(g => g.FrameworkAppealStateJson, (string?)null),
                        ct);

                logger.LogWarning(
                    "[FrameworkAppealRouter] Recovery Group={Id}：暫採降級策略（清 marker），Mock 場景 C 驗收後升級 ResumeAsync",
                    groupId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[FrameworkAppealRouter] Recovery Group={Id} 失敗，跳過此 group", groupId);
            }
        }
    }

    // ============================================================
    //  Workflow run + verdict translate
    // ============================================================

    private async Task<AppealLoopResult?> RunWorkflowAsync(
        Workflow workflow,
        CheckpointManager checkpointManager,
        string sessionId,
        AppealState initialState,
        CancellationToken ct)
    {
        // initialState 直接作為 first input message：CodyReviewAppealExecutor / CodyDevPlanAppealExecutor
        // 的 [MessageHandler] HandleInitialAsync 接 AppealState 為 first message，內部 SaveAsync 寫進 framework state
        // → 後續 superstep 讀 state 已 OK，無需 router pre-seed dict
        var run = await InProcessExecution.RunAsync(workflow, initialState, checkpointManager, sessionId, ct);

        // 找 WorkflowOutputEvent 取 AppealLoopResult
        foreach (var ev in run.OutgoingEvents)
        {
            if (ev is WorkflowOutputEvent outputEvent && outputEvent.Is<AppealLoopResult>(out var appealResult))
            {
                return appealResult;
            }
        }

        // 沒拿到 output event — fallback 視為失敗
        logger.LogWarning(
            "[FrameworkAppealRouter] Workflow run 完成但無 WorkflowOutputEvent (sessionId={Id}, events={Count})",
            sessionId, run.OutgoingEvents.Count());
        return null;
    }

    private async Task<AgentExecutionResult?> TranslateReviewAppealVerdictAsync(
        AppealLoopResult appealResult,
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        // appealResult.Verdict 對應：
        //   - "approve" / "revise" / "escalate"（Petra Gate 路徑）
        //   - "max_iter_arbitration_approve" / "max_iter_arbitration_reject"（Petra Arbitration 路徑）

        switch (appealResult.Verdict)
        {
            case "approve":
            case "max_iter_arbitration_approve":
                logger.LogInformation(
                    "[FrameworkAppealRouter] Group={Id}：ReviewAppeal Workflow → approve（{Verdict}）",
                    group.Id, appealResult.Verdict);
                if (appealResult.ArbitrationTriggered)
                    group.SkipReviewerAfterArbitration = true;
                await taskRepo.SaveAsync(cancellationToken);
                return result with { CriticalReviewCount = 0 };

            case "revise":
                logger.LogInformation(
                    "[FrameworkAppealRouter] Group={Id}：ReviewAppeal Workflow → revise（{Count} criticals）",
                    group.Id, appealResult.FinalCriticalIds.Count);
                if (!string.IsNullOrWhiteSpace(appealResult.RevisionInstructions))
                {
                    group.LastReviewBody =
                        (group.LastReviewBody ?? "") +
                        "\n\n【Petra 修正指示】" + appealResult.RevisionInstructions;
                    await taskRepo.SaveAsync(cancellationToken);
                }
                return result with { CriticalReviewCount = appealResult.FinalCriticalIds.Count };

            case "max_iter_arbitration_reject":
            case "escalate":
            default:
                logger.LogWarning(
                    "[FrameworkAppealRouter] Group={Id}：ReviewAppeal Workflow → escalate（{Verdict}）",
                    group.Id, appealResult.Verdict);
                taskRepo.UpdateGroupStatus(group, "failed");
                await taskRepo.SaveAsync(cancellationToken);
                var tgs = serviceProvider.GetRequiredService<TaskGroupService>();
                await tgs.NotifyBossInterventionAsync(group, cancellationToken);
                return null;
        }
    }
}
