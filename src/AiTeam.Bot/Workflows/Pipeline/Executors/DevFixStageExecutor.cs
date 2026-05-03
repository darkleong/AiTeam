using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = AiTeam.Shared.Constants.TaskStatus;

namespace AiTeam.Bot.Workflows.Pipeline.Executors;

/// <summary>
/// Stage 53B：DevFix stage Executor（fix loop 子流程 framework 化）。
///
/// 職責（dual handler）：
///   - HandleEntryAsync(DevFixStageBridge)：state.CurrentStage = "Dev_fix" → fetch fresh group → call FireStepsAsync(Dev_fix, IsFixLoop:true) enqueue → SendMessageAsync(DevFixCompletionRequest) yield
///   - HandleResponseAsync(DevFixCompletionResponse)：
///     ① result.Success=true → state.LastAgentResult/Name → SendMessageAsync(ReviewerStageBridge) loop back（fix loop 主路徑）
///     ② result.Success=false → SetInterventionAndYieldAsync 結束 Workflow（Dev_fix [BLOCKED] / failed 一律 intervention，避免無限 appeal 循環）
///
/// 紀律（53B 議題 7 必修對齊子項 4 DevStage failure pattern）：
///   - 失敗場景一律 SetInterventionAndYieldAsync（不走 ClearMarkerAndFallbackAsync — 子項 9 移除）
///   - type-explicit Bridge record（DevFixStageBridge / DevFixCompletionRequest/Response 各自獨立型別）— Stage 52 fix#2 教訓
///   - Stage 50 三件套：[SendsMessage] + partial class + 註解
/// </summary>
[SendsMessage(typeof(ReviewerStageBridge))]
[SendsMessage(typeof(DevFixCompletionRequest))]
[YieldsOutput(typeof(PipelineLoopResult))]
internal sealed partial class DevFixStageExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DevFixStageExecutor> _logger;

    public DevFixStageExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DevFixStageExecutor> logger)
        : base("Pipeline-DevFixStage")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleEntryAsync(DevFixStageBridge bridge, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        state.CurrentStage = "Dev_fix";
        await PipelineStateHelpers.SaveAsync(context, state);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(bridge.GroupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage53B] DevFixStage：找不到 Group={Id}，intervention", bridge.GroupId);
            await SetInterventionAndYieldAsync(context, bridge.GroupId, "Group 不存在", null);
            return;
        }

        var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
        await tgs.FireStepsAsync(group, [new WorkflowStep("Dev_fix", IsFixLoop: true)], default);
        _logger.LogInformation("[Stage53B] DevFixStage：enqueue Dev_fix (IsFixLoop:true) + emit RequestPort yield（Group={Id}）", bridge.GroupId);

        await context.SendMessageAsync(new DevFixCompletionRequest(bridge.GroupId));
    }

    [MessageHandler]
    private async ValueTask HandleResponseAsync(DevFixCompletionResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        var result = response.Result;

        if (!result.Success)
        {
            // Dev_fix 階段 [BLOCKED] / failed 一律 intervention（避免無限 appeal 循環）
            var failReason = result.Summary.StartsWith("[BLOCKED]", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(result.OutputContent)
                ? $"Dev_fix [BLOCKED]：{result.Summary}"
                : $"Dev_fix 失敗：{result.Summary}";
            _logger.LogInformation("[Stage53B] DevFixStage：failure → SetInterventionAndYieldAsync（Group={Id}, reason={Reason}）",
                state.GroupId, failReason);
            await SetInterventionAndYieldAsync(context, state.GroupId, failReason, result);
            return;
        }

        // happy path：Dev_fix passed → ReviewerStage 重審（fix loop 主路徑）
        state.LastAgentResult = result;
        state.LastAgentName = "Dev_fix";
        await PipelineStateHelpers.SaveAsync(context, state);
        _logger.LogInformation("[Stage53B] DevFixStage：passed → ReviewerStageBridge loop back（Group={Id}）", state.GroupId);
        await context.SendMessageAsync(new ReviewerStageBridge(state.GroupId));
    }

    /// <summary>Stage 53B：intervention 統一 helper（對齊 ReviewerStage / DevPlanStage / DevStage / QaStage 共用 pattern）。
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
