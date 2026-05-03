using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Orchestration.Appeal;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
[SendsMessage(typeof(PipelineFallbackBridge))]
[SendsMessage(typeof(ReviewerCompletionRequest))]
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
            await ClearMarkerAndFallbackAsync(context, bridge.GroupId, "group_not_found", null);
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
            await ClearMarkerAndFallbackAsync(context, state.GroupId, "group_not_found", result);
            return;
        }

        var projectId = string.IsNullOrWhiteSpace(group.Project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(group.Project, default);

        var appealOrchestration = scope.ServiceProvider.GetRequiredService<AppealOrchestrationService>();
        var petraResult = await appealOrchestration.RunPetraGateAsync(group, result, taskRepo, projectId, default);

        // null = escalate（group 已標 failed + NotifyBossInterventionAsync 已 call）
        if (petraResult is null)
        {
            _logger.LogInformation("[Stage53A] ReviewerStage：Petra escalate → fallback reviewer_critical（Group={Id}）", state.GroupId);
            await ClearMarkerAndFallbackAsync(context, state.GroupId, "reviewer_critical", result);
            return;
        }

        // CriticalReviewCount=0 = approve / =1 = revise
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

        _logger.LogInformation("[Stage53A] ReviewerStage：Petra revise (CriticalReviewCount={Count}) → fallback reviewer_critical（Group={Id}）", petraResult.CriticalReviewCount, state.GroupId);
        await ClearMarkerAndFallbackAsync(context, state.GroupId, "reviewer_critical", petraResult);
    }

    private async ValueTask ClearMarkerAndFallbackAsync(
        IWorkflowContext context, Guid groupId, string reason, AgentExecutionResult? result)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.PipelineFrameworkStateJson, (string?)null)
                .SetProperty(g => g.ActiveOrchestration, (string?)null), default);
        await context.SendMessageAsync(new PipelineFallbackBridge(groupId, reason, result));
    }
}
