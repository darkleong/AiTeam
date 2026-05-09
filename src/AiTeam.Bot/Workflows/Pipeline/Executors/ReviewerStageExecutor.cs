using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Orchestration.Appeal;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = AiTeam.Shared.Constants.TaskStatus;

namespace AiTeam.Bot.Workflows.Pipeline.Executors;

/// <summary>
/// Stage 53A：Reviewer stage Executor（J1 yield-resume 機制 — Agent 型 stage 第 3 個 + C2/I2 整合 RunPetraGateAsync）。
///
/// 職責（dual handler）：
///   - HandleEntryAsync(ReviewerStageBridge)：state.CurrentStage = "Reviewer" → fetch fresh group → call FireStepsAsync(Reviewer) enqueue → SendMessageAsync(ReviewerCompletionRequest) yield
///   - HandleResponseAsync(ReviewerCompletionResponse)：對齊 Stage 39 既有 line 179-199 三種 routing：
///     ① result.Success=false → 設 CriticalReviewCount=0 直接放行（log Skipped）→ SendMessageAsync(QaStageBridge)
///     ② result.ResultType == AgentResultType.Skipped → 放行直接 → SendMessageAsync(QaStageBridge)
///     ③ result.Success=true 且 CriticalReviewCount > 0 → call appealOrchestration.RunPetraGateAsync 同步 await（C2+I2 整合）
///         - Petra approve（return non-null with CriticalReviewCount=0）→ SendMessageAsync(QaStageBridge)
///         - Petra revise（return non-null with CriticalReviewCount=1）→ fallback reviewer_critical
///         - Petra escalate（return null）→ fallback reviewer_critical
///     ④ result.Success=true 且 CriticalReviewCount=0 → 直接放行 → SendMessageAsync(QaStageBridge)
///
/// Stage 53A 範圍邊界：
///   - bypass Cody-Vera Appeal loop（屬 Stage 53B「appeal 子流程」）
///   - reviewer_critical fallback 後 FinalizePipelineAsync 模擬 legacy WorkflowEngine Reviewer fail routing：FixIteration++ + FireStepsAsync(Dev, IsFixLoop:true)
///
/// 紀律：fallback 時序 + type-explicit Bridge record + Stage 50 三件套。
/// </summary>
[SendsMessage(typeof(QaStageBridge))]
[SendsMessage(typeof(DevFixStageBridge))]
[SendsMessage(typeof(PipelineFallbackBridge))]
[SendsMessage(typeof(ReviewerCompletionRequest))]
// Stage 57-FF 五十二：fix loop limit type-specific routing（第 6 routing）
[SendsMessage(typeof(DocStageBridge))]              // fix_loop_skip_qa case 跳 QA 直接送 Doc
[SendsMessage(typeof(ReviewerFixLoopLimitRequest))] // fix loop limit yield request
[YieldsOutput(typeof(PipelineLoopResult))]
internal sealed partial class ReviewerStageExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReviewerStageExecutor> _logger;

    public ReviewerStageExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<ReviewerStageExecutor> logger)
        : base("Pipeline-ReviewerStage")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleEntryAsync(ReviewerStageBridge bridge, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        state.CurrentStage = "Reviewer";
        await PipelineStateHelpers.SaveAsync(context, state);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(bridge.GroupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage53A] ReviewerStage：找不到 Group={Id}，fallback", bridge.GroupId);
            await context.SendMessageAsync(new PipelineFallbackBridge(bridge.GroupId, "group_not_found", null));
            return;
        }

        var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
        await tgs.FireStepsAsync(group, [new WorkflowStep("Reviewer")], default);
        _logger.LogInformation("[Stage53A] ReviewerStage：enqueue Reviewer + emit RequestPort yield（Group={Id}）", bridge.GroupId);

        await context.SendMessageAsync(new ReviewerCompletionRequest(bridge.GroupId));
    }

    [MessageHandler]
    private async ValueTask HandleResponseAsync(ReviewerCompletionResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        var result = response.Result;

        // 情境 ① / ②：Vera 失敗或略過 — 對齊 Stage 39 既有 line 179-199
        if (!result.Success || result.ResultType == AgentResultType.Skipped)
        {
            if (result.ResultType == AgentResultType.Skipped)
                _logger.LogInformation("[Stage53A] ReviewerStage：Vera 略過（{Summary}），跳過 Petra 審核直接放行（Group={Id}）", result.Summary, state.GroupId);
            else
                _logger.LogInformation("[Stage53A] ReviewerStage：Vera 失敗放行（{Summary}）（Group={Id}）", result.Summary, state.GroupId);

            state.ReviewerDone = true;
            state.LastAgentResult = result with { CriticalReviewCount = 0 };
            state.LastAgentName = "Reviewer";
            await PipelineStateHelpers.SaveAsync(context, state);
            await context.SendMessageAsync(new QaStageBridge(state.GroupId));
            return;
        }

        // 情境 ④：Vera 成功 + CriticalReviewCount=0 → 直接放行
        if (result.CriticalReviewCount == 0)
        {
            _logger.LogInformation("[Stage53A] ReviewerStage：Vera 通過 (CriticalReviewCount=0) → QaStageBridge（Group={Id}）", state.GroupId);
            state.ReviewerDone = true;
            state.LastAgentResult = result;
            state.LastAgentName = "Reviewer";
            await PipelineStateHelpers.SaveAsync(context, state);
            await context.SendMessageAsync(new QaStageBridge(state.GroupId));
            return;
        }

        // 情境 ③：Vera 成功 + CriticalReviewCount > 0 → Petra 閘門
        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(state.GroupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage53A] ReviewerStage：Petra 閘門前找不到 Group={Id}，fallback", state.GroupId);
            await context.SendMessageAsync(new PipelineFallbackBridge(state.GroupId, "group_not_found", result));
            return;
        }

        var projectId = string.IsNullOrWhiteSpace(group.Project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(group.Project, default);

        var appealOrchestration = scope.ServiceProvider.GetRequiredService<AppealOrchestrationService>();
        var petraResult = await appealOrchestration.RunPetraGateAsync(group, result, taskRepo, projectId, default);

        // null = escalate（Pipeline path 下 RunPetraGateAsync 已 skip side effects — 議題 F-1 修正 6-a）
        if (petraResult is null)
        {
            _logger.LogInformation("[Stage53B] ReviewerStage：Petra escalate → SetInterventionAndYieldAsync（Group={Id}）", state.GroupId);
            await SetInterventionAndYieldAsync(context, state.GroupId, "Petra escalate（Reviewer 仲裁失敗）", result);
            return;
        }

        // CriticalReviewCount=0 = approve / >0 = revise
        if (petraResult.CriticalReviewCount == 0)
        {
            _logger.LogInformation("[Stage53A] ReviewerStage：Petra approve → QaStageBridge（Group={Id}）", state.GroupId);
            state.ReviewerDone = true;
            state.LastAgentResult = petraResult;
            state.LastAgentName = "Reviewer";
            await PipelineStateHelpers.SaveAsync(context, state);
            await context.SendMessageAsync(new QaStageBridge(state.GroupId));
            return;
        }

        // 53B → 57：Petra revise → fix loop（max 3 輪）；Stage 57-FF 五十二：達 limit 改 yield-resume 第 6 routing
        if (group.FixIteration >= 3)
        {
            _logger.LogWarning("[Stage57] ReviewerStage：FixIteration={N} >= 3 達上限 → fire reviewer_fix_loop_limit interaction + yield 等 Christ（Group={Id}）",
                group.FixIteration, state.GroupId);

            // ① 紀錄 state 後 fire BossInteraction（type-specific）
            state.LastAgentResult = petraResult;
            state.LastAgentName = "Reviewer";
            await PipelineStateHelpers.SaveAsync(context, state);

            var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
            await tgs.NotifyBossReviewerFixLoopLimitAsync(group, petraResult, default);

            // ② SendsMessage(ReviewerFixLoopLimitRequest) → Workflow yield 等 Christ button click
            await PipelineHitlHelper.YieldForChristResponseAsync(
                context, new ReviewerFixLoopLimitRequest(state.GroupId), _logger,
                "reviewer_fix_loop_limit", state.GroupId);
            return;
        }

        // fix loop 觸發：FixIteration++ + SendMessage(DevFixStageBridge)
        group.FixIteration++;
        await taskRepo.SaveAsync(default);
        _logger.LogInformation("[Stage53B] ReviewerStage：Petra revise (CriticalReviewCount={Count}) → fix loop Round {N} → DevFixStageBridge（Group={Id}）",
            petraResult.CriticalReviewCount, group.FixIteration, state.GroupId);
        state.LastAgentResult = petraResult;
        state.LastAgentName = "Reviewer";
        await PipelineStateHelpers.SaveAsync(context, state);
        await context.SendMessageAsync(new DevFixStageBridge(state.GroupId));
    }

    /// <summary>Stage 57-FF 五十二：reviewer_fix_loop_limit HITL 回應 handler — Christ button click 後 routing（真三選獨立 path）。
    /// mark_done → QaStageBridge（state.ReviewerDone=true 走完整 QA → Doc）/
    /// skip_qa → DocStageBridge（state.ReviewerDone=true + state.QaDone=true 跳過 QA 直接 Doc）/
    /// abort → SetInterventionAndYieldAsync end Pipeline。</summary>
    [MessageHandler]
    private async ValueTask HandleReviewerFixLoopLimitResponseAsync(ReviewerFixLoopLimitResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);

        switch (response.Action.ToLower())
        {
            case "mark_done":
                _logger.LogInformation("[Stage57] ReviewerStage：reviewer_fix_loop_limit mark_done → QaStageBridge（認可審查走完整 QA → Doc）（Group={Id}）", state.GroupId);
                state.ReviewerDone = true;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new QaStageBridge(state.GroupId));
                return;

            case "skip_qa":
                _logger.LogInformation("[Stage57] ReviewerStage：reviewer_fix_loop_limit skip_qa → DocStageBridge（跳過 QA 直接 Doc）（Group={Id}）", state.GroupId);
                state.ReviewerDone = true;
                state.QaDone = true;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new DocStageBridge(state.GroupId));
                return;

            case "abort":
                _logger.LogInformation("[Stage57] ReviewerStage：reviewer_fix_loop_limit abort → SetInterventionAndYieldAsync 結束 Pipeline（Group={Id}）", state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, "Reviewer fix loop limit abort by Christ", state.LastAgentResult);
                return;

            default:
                _logger.LogWarning("[Stage57] ReviewerStage：未知 reviewer_fix_loop_limit action={Action}（Group={Id}）— SetIntervention", response.Action, state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, $"未知 reviewer_fix_loop_limit action: {response.Action}", state.LastAgentResult);
                return;
        }
    }

    /// <summary>Stage 53B：intervention 統一 helper（fix loop max_iter / Petra escalate）。
    /// DB set group.Status=NeedsIntervention + InterventionReason → call NotifyBossInterventionAsync → YieldOutput Completed=true 結束 Workflow。
    /// PipelineLoopResult.Completed=true 語義：Pipeline Workflow 完整跑完（含 intervention），FinalizePipelineAsync 只 ClearMarkersAsync。</summary>
    private async ValueTask SetInterventionAndYieldAsync(
        IWorkflowContext context, Guid groupId, string interventionReason, AgentExecutionResult? lastResult)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.Status, TaskStatus.NeedsIntervention)
                .SetProperty(g => g.InterventionReason, interventionReason), default);

        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var freshGroup = await taskRepo.GetGroupByIdAsync(groupId, default);
        if (freshGroup is not null)
        {
            var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
            await tgs.NotifyBossInterventionAsync(freshGroup, default);
        }

        await context.YieldOutputAsync(new PipelineLoopResult
        {
            GroupId = groupId,
            Completed = true,
            FallbackReason = null,
            LastResult = lastResult,
        });
    }
}
