using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
[SendsMessage(typeof(PipelineFallbackBridge))]
[SendsMessage(typeof(DevCompletionRequest))]
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
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        state.CurrentStage = "Dev";
        await PipelineStateHelpers.SaveAsync(context, state);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(bridge.GroupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage53A] DevStage：找不到 Group={Id}，fallback", bridge.GroupId);
            await ClearMarkerAndFallbackAsync(context, bridge.GroupId, "group_not_found", null);
            return;
        }

        var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
        await tgs.FireStepsAsync(group, [new WorkflowStep("Dev")], default);
        _logger.LogInformation("[Stage53A] DevStage：enqueue Dev + emit RequestPort yield（Group={Id}）", bridge.GroupId);

        await context.SendMessageAsync(new DevCompletionRequest(bridge.GroupId));
    }

    [MessageHandler]
    private async ValueTask HandleResponseAsync(DevCompletionResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        var result = response.Result;

        if (!result.Success)
        {
            // [BLOCKED] 阻礙 → dev_blocker fallback
            if (result.Summary.StartsWith("[BLOCKED]", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(result.OutputContent))
            {
                _logger.LogInformation("[Stage53A] DevStage：[BLOCKED] → fallback dev_blocker（Group={Id}）", state.GroupId);
                await ClearMarkerAndFallbackAsync(context, state.GroupId, "dev_blocker", result);
                return;
            }
            // 其他失敗 → dev_failed fallback
            _logger.LogInformation("[Stage53A] DevStage：result.Success=false → fallback dev_failed（Group={Id}）", state.GroupId);
            await ClearMarkerAndFallbackAsync(context, state.GroupId, "dev_failed", result);
            return;
        }

        state.DevDone = true;
        state.LastAgentResult = result;
        state.LastAgentName = "Dev";
        await PipelineStateHelpers.SaveAsync(context, state);
        _logger.LogInformation("[Stage53A] DevStage：passed → ReviewerStageBridge（Group={Id}）", state.GroupId);
        await context.SendMessageAsync(new ReviewerStageBridge(state.GroupId));
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
