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
/// Stage 53A：Dev stage Executor（J1 yield-resume 機制 — Agent 型 stage 第 2 個）。
///
/// 職責（dual handler）：
///   - HandleEntryAsync(DevStageBridge)：state.CurrentStage = "Dev" → fetch fresh group → call FireStepsAsync(Dev) enqueue legacy AgentQueueService → SendMessageAsync(DevCompletionRequest) yield
///   - HandleResponseAsync(DevCompletionResponse)：收 result → 三分支：
///     ① result.Success=true → state.DevDone=true → SendMessageAsync(ReviewerStageBridge)
///     ② result.Success=false + [BLOCKED] + OutputContent → fallback dev_blocker（FinalizePipelineAsync 主動 call HandleDevBlockerAsync）
///     ③ result.Success=false 其他 → fallback dev_failed（FinalizePipelineAsync 主動 call NotifyBossDevFailedInterventionAsync）
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
                    _logger.LogInformation("[Stage53B] DevStage：[BLOCKED] Petra {Routing} → SetInterventionAndYieldAsync（Group={Id}）",
                        decision.Routing, state.GroupId);
                    await SetInterventionAndYieldAsync(context, state.GroupId,
                        $"Dev blocker {decision.Routing}：{decision.Instructions}", result);
                    return;
                }

                // Petra continue → DevRetryBridge 重試（HandleDevBlockerAsync 內 FireStepsAsync(Dev) 已 Pipeline path skip）
                _logger.LogInformation("[Stage53B] DevStage：[BLOCKED] Petra continue → DevRetryBridge 重試（Group={Id}）", state.GroupId);
                state.LastAgentResult = result;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new DevRetryBridge(state.GroupId));
                return;
            }

            // 其他失敗 → 53B：intervention（保留 dev_failed 邊界 reason 給 FinalizePipelineAsync notify）
            _logger.LogInformation("[Stage53B] DevStage：result.Success=false → SetInterventionAndYieldAsync（Group={Id}）", state.GroupId);
            await SetInterventionAndYieldAsync(context, state.GroupId, $"Dev 失敗：{result.Summary}", result);
            return;
        }

        state.DevDone = true;
        state.LastAgentResult = result;
        state.LastAgentName = "Dev";
        await PipelineStateHelpers.SaveAsync(context, state);
        _logger.LogInformation("[Stage53A] DevStage：passed → ReviewerStageBridge（Group={Id}）", state.GroupId);
        await context.SendMessageAsync(new ReviewerStageBridge(state.GroupId));
    }

    /// <summary>Stage 53B：intervention 統一 helper（dev_failed / blocker escalate）。</summary>
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
