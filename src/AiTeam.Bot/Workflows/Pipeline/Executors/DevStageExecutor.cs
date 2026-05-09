using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Orchestration.Appeal;
using AiTeam.Bot.Orchestration.Boss;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = AiTeam.Shared.Constants.TaskStatus;

namespace AiTeam.Bot.Workflows.Pipeline.Executors;

/// <summary>
/// Stage 53A：Dev stage Executor（J1 yield-resume 機制 — Agent 型 stage 第 2 個）。
///
/// 職責（dual handler）：
///   - HandleEntryAsync(DevStageBridge)：state.CurrentStage = "Dev" → fetch fresh group → call FireStepsAsync(Dev) enqueue legacy AgentQueueService → SendMessageAsync(DevCompletionRequest) yield
///   - HandleResponseAsync(DevCompletionResponse)：收 result → 三分支：
///     ① result.Success=true → state.DevDone=true → SendMessageAsync(ReviewerStageBridge)
///     ② result.Success=false + [BLOCKED] + OutputContent → Stage 55B Session B：dev_failed_intervention HITL yield 等 Christ
///     ③ result.Success=false 其他 → Stage 55B Session B：dev_failed_intervention HITL yield 等 Christ
///   - HandleDevInterventionResponseAsync(DevInterventionResponse)（Stage 55B Session B 新加）：Christ button 後 routing：
///     ① skip  → state.DevDone=true → SendMessageAsync(ReviewerStageBridge)
///     ② retry → SendMessageAsync(DevRetryBridge)（既有 self-loop）
///     ③ abort → SetInterventionAndYieldAsync（mark failed end Pipeline）
///
/// Stage 53A 範圍邊界（happy path 限定）：
///   - bypass Dev fix loop（屬 Stage 53B「fix loop 子流程」）
///   - Dev_fix Recovery 場景由 5 fallback 機制清 marker 後 callback 走 legacy
///
/// 紀律：fallback 時序（先清 marker → 再 SendMessage）+ type-explicit Bridge record（DevCompletionRequest/Response）+ Stage 50 三件套。
/// </summary>
[SendsMessage(typeof(ReviewerStageBridge))]
[SendsMessage(typeof(DevRetryBridge))]
[SendsMessage(typeof(PipelineFallbackBridge))]
[SendsMessage(typeof(DevCompletionRequest))]
[SendsMessage(typeof(DevInterventionRequest))]
// Stage 58-FF 五十三：Agent API 失敗 RequestPort（第 7 routing）
[SendsMessage(typeof(DevAgentApiFailureRequest))]
[YieldsOutput(typeof(PipelineLoopResult))]
internal sealed partial class DevStageExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DevStageExecutor> _logger;

    public DevStageExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DevStageExecutor> logger)
        : base("Pipeline-DevStage")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleEntryAsync(DevStageBridge bridge, IWorkflowContext context)
        => await EnqueueDevAndYieldAsync(bridge.GroupId, context);

    /// <summary>Stage 53B：Dev retry handler — [BLOCKED] Petra continue 後重試 Dev（self-loop via DevRetryBridge）。
    /// reuse EnqueueDevAndYieldAsync 共用邏輯。</summary>
    [MessageHandler]
    private async ValueTask HandleRetryAsync(DevRetryBridge bridge, IWorkflowContext context)
    {
        _logger.LogInformation("[Stage53B] DevStage：retry handler 觸發（DevRetryBridge, Group={Id}）", bridge.GroupId);
        await EnqueueDevAndYieldAsync(bridge.GroupId, context);
    }

    /// <summary>共用 enqueue + yield 邏輯。</summary>
    private async ValueTask EnqueueDevAndYieldAsync(Guid groupId, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        state.CurrentStage = "Dev";
        await PipelineStateHelpers.SaveAsync(context, state);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(groupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage53A] DevStage：找不到 Group={Id}，fallback", groupId);
            await context.SendMessageAsync(new PipelineFallbackBridge(groupId, "group_not_found", null));
            return;
        }

        var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
        await tgs.FireStepsAsync(group, [new WorkflowStep("Dev")], default);
        _logger.LogInformation("[Stage53A] DevStage：enqueue Dev + emit RequestPort yield（Group={Id}）", groupId);

        await context.SendMessageAsync(new DevCompletionRequest(groupId));
    }

    [MessageHandler]
    private async ValueTask HandleResponseAsync(DevCompletionResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        var result = response.Result;

        // Stage 58-FF 五十三：API 失敗 marker check（先於既有 [BLOCKED] / Success=false routing — 對齊 Stage 53B [BLOCKED] pattern）
        if (result.Summary.StartsWith("[API_FAILURE]", StringComparison.Ordinal))
        {
            _logger.LogWarning("[Stage58] DevStage：result [API_FAILURE] marker → fire agent_api_failure_intervention + yield 等 Christ（Group={Id}）",
                state.GroupId);
            state.LastAgentResult = result;
            state.LastAgentName = "Dev";
            await PipelineStateHelpers.SaveAsync(context, state);

            await using var apiFailScope = _scopeFactory.CreateAsyncScope();
            var apiFailRepo = apiFailScope.ServiceProvider.GetRequiredService<TaskRepository>();
            var apiFailGroup = await apiFailRepo.GetGroupByIdAsync(state.GroupId, default);
            if (apiFailGroup is null)
            {
                await context.SendMessageAsync(new PipelineFallbackBridge(state.GroupId, "group_not_found", result));
                return;
            }
            var apiFailBossNotification = apiFailScope.ServiceProvider.GetRequiredService<BossNotificationService>();
            await apiFailBossNotification.NotifyBossAgentApiFailureAsync(apiFailGroup, "Dev", result.Summary, default);
            await PipelineHitlHelper.YieldForChristResponseAsync(
                context, new DevAgentApiFailureRequest(state.GroupId), _logger,
                "agent_api_failure_intervention", state.GroupId);
            return;
        }

        if (!result.Success)
        {
            // [BLOCKED] 阻礙 → 53B：內 call HandleDevBlockerAsync（Pipeline path skip 內部 fire/UpdateStatus/Discord — 議題 F-1 修正 6-c）
            if (result.Summary.StartsWith("[BLOCKED]", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(result.OutputContent))
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
                var group = await taskRepo.GetGroupByIdAsync(state.GroupId, default);
                if (group is null)
                {
                    _logger.LogError("[Stage53B] DevStage：blocker 前找不到 Group={Id}，fallback", state.GroupId);
                    await context.SendMessageAsync(new PipelineFallbackBridge(state.GroupId, "group_not_found", result));
                    return;
                }

                var projectId = string.IsNullOrWhiteSpace(group.Project)
                    ? (Guid?)null
                    : await taskRepo.GetProjectIdByNameAsync(group.Project, default);
                var appealOrchestration = scope.ServiceProvider.GetRequiredService<AppealOrchestrationService>();

                // 子項 6-c 議題 F-1 修正：HandleDevBlockerAsync signature `Task` → `Task<BlockerDecision>` 回傳 Petra 評估
                // Pipeline path 下 fire + UpdateStatus + Discord 全 skip，Pipeline 用 decision.Routing 自接管 routing
                var decision = await appealOrchestration.HandleDevBlockerAsync(group, result, taskRepo, projectId, default);

                if (decision.Routing is "escalate_victoria" or "escalate_boss")
                {
                    // Stage 55B Session B：escalate → 開 dev_failed_intervention BossInteraction + yield 等 Christ button routing
                    _logger.LogInformation("[Stage55B] DevStage：[BLOCKED] Petra {Routing} → dev_failed_intervention HITL yield（Group={Id}）",
                        decision.Routing, state.GroupId);
                    state.LastAgentResult = result;
                    await PipelineStateHelpers.SaveAsync(context, state);

                    var bossNotification = scope.ServiceProvider.GetRequiredService<BossNotificationService>();
                    var failSummary = $"Dev blocker {decision.Routing}：{decision.Instructions}";
                    await bossNotification.NotifyBossDevFailedInterventionAsync(group, isFixLoop: false, failSummary, default);
                    await PipelineHitlHelper.YieldForChristResponseAsync(
                        context, new DevInterventionRequest(state.GroupId), _logger,
                        "dev_failed_intervention", state.GroupId);
                    return;
                }

                // Petra continue → DevRetryBridge 重試（HandleDevBlockerAsync 內 FireStepsAsync(Dev) 已 Pipeline path skip）
                _logger.LogInformation("[Stage53B] DevStage：[BLOCKED] Petra continue → DevRetryBridge 重試（Group={Id}）", state.GroupId);
                state.LastAgentResult = result;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new DevRetryBridge(state.GroupId));
                return;
            }

            // Stage 55B Session B：其他失敗 → 開 dev_failed_intervention BossInteraction + yield 等 Christ button routing
            _logger.LogInformation("[Stage55B] DevStage：result.Success=false → dev_failed_intervention HITL yield（Group={Id}）", state.GroupId);
            state.LastAgentResult = result;
            await PipelineStateHelpers.SaveAsync(context, state);

            await using (var scopeDevFail = _scopeFactory.CreateAsyncScope())
            {
                var taskRepoFail = scopeDevFail.ServiceProvider.GetRequiredService<TaskRepository>();
                var groupFail = await taskRepoFail.GetGroupByIdAsync(state.GroupId, default);
                if (groupFail is null)
                {
                    _logger.LogError("[Stage55B] DevStage：dev_failed yield 前找不到 Group={Id}，fallback", state.GroupId);
                    await context.SendMessageAsync(new PipelineFallbackBridge(state.GroupId, "group_not_found", result));
                    return;
                }
                var bossNotificationFail = scopeDevFail.ServiceProvider.GetRequiredService<BossNotificationService>();
                await bossNotificationFail.NotifyBossDevFailedInterventionAsync(groupFail, isFixLoop: false, $"Dev 失敗：{result.Summary}", default);
            }
            await PipelineHitlHelper.YieldForChristResponseAsync(
                context, new DevInterventionRequest(state.GroupId), _logger,
                "dev_failed_intervention", state.GroupId);
            return;
        }

        state.DevDone = true;
        state.LastAgentResult = result;
        state.LastAgentName = "Dev";
        await PipelineStateHelpers.SaveAsync(context, state);
        _logger.LogInformation("[Stage53A] DevStage：passed → ReviewerStageBridge（Group={Id}）", state.GroupId);
        await context.SendMessageAsync(new ReviewerStageBridge(state.GroupId));
    }

    /// <summary>Stage 55B Session B：dev_failed_intervention HITL 回應 handler — Christ button click 後 routing。
    /// skip → state.DevDone=true → ReviewerStageBridge / retry → DevRetryBridge（self-loop） / abort → SetInterventionAndYieldAsync end Pipeline。</summary>
    [MessageHandler]
    private async ValueTask HandleDevInterventionResponseAsync(DevInterventionResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);

        switch (response.Action.ToLower())
        {
            case "skip":
                _logger.LogInformation("[Stage55B] DevStage：dev_intervention skip → ReviewerStageBridge（Group={Id}）", state.GroupId);
                state.DevDone = true;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new ReviewerStageBridge(state.GroupId));
                return;

            case "retry":
                _logger.LogInformation("[Stage55B] DevStage：dev_intervention retry → DevRetryBridge（Group={Id}）", state.GroupId);
                await context.SendMessageAsync(new DevRetryBridge(state.GroupId));
                return;

            case "abort":
                _logger.LogInformation("[Stage55B] DevStage：dev_intervention abort → SetInterventionAndYieldAsync 結束 Pipeline（Group={Id}）", state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, "Dev intervention abort by Christ", state.LastAgentResult);
                return;

            default:
                _logger.LogWarning("[Stage55B] DevStage：未知 dev_intervention action={Action}（Group={Id}）— SetInterventionAndYieldAsync", response.Action, state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, $"未知 dev_intervention action: {response.Action}", state.LastAgentResult);
                return;
        }
    }

    /// <summary>Stage 58-FF 五十三：Agent API 失敗 HITL 回應 handler — Christ button click 後真三選 routing。
    /// continue → state.DevDone=true + SendMessage(ReviewerStageBridge) 跳下游 / retry → SendMessage(DevStageBridge) re-invoke 同 stage / abort → SetInterventionAndYieldAsync end Pipeline。</summary>
    [MessageHandler]
    private async ValueTask HandleAgentApiFailureResponseAsync(DevAgentApiFailureResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);

        switch (response.Action.ToLower())
        {
            case "continue":
                _logger.LogInformation("[Stage58] DevStage：agent_api_failure continue → ReviewerStageBridge（state.DevDone=true，跳過 Dev 進下階段）（Group={Id}）", state.GroupId);
                state.DevDone = true;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new ReviewerStageBridge(state.GroupId));
                return;

            case "retry":
                _logger.LogInformation("[Stage58] DevStage：agent_api_failure retry → DevStageBridge re-invoke 同 stage（儲值後）（Group={Id}）", state.GroupId);
                await context.SendMessageAsync(new DevStageBridge(state.GroupId));
                return;

            case "abort":
                _logger.LogInformation("[Stage58] DevStage：agent_api_failure abort → SetInterventionAndYieldAsync 結束 Pipeline（Group={Id}）", state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, "Dev API failure abort by Christ", state.LastAgentResult);
                return;

            default:
                _logger.LogWarning("[Stage58] DevStage：未知 agent_api_failure action={Action}（Group={Id}）— SetInterventionAndYieldAsync", response.Action, state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, $"未知 agent_api_failure action: {response.Action}", state.LastAgentResult);
                return;
        }
    }

    /// <summary>Stage 53B：intervention 統一 helper（dev_failed / blocker escalate / Stage 58 agent_api_failure abort）。</summary>
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
            var bossNotification = scope.ServiceProvider.GetRequiredService<BossNotificationService>();
            await bossNotification.NotifyBossInterventionAsync(freshGroup, default);
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
