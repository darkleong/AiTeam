using AiTeam.Bot.Orchestration;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Pipeline.Executors;

/// <summary>
/// Stage 53A：NotifyMerge stage Executor — Pipeline 終結 Executor。
///
/// 職責：
///   - 同步 await TaskGroupService.NotifyBossMergeAsync（既有 method，發 Discord embed + 開 BossInteraction merge_notify）
///   - YieldOutputAsync(PipelineLoopResult Completed=true) → router 收到後 call FinalizePipelineAsync 收尾（清 marker）
///
/// 對齊 Stage 50 KickoffPlanExecutor / Stage 52 DesignPlanExecutor 終結 Executor pattern。
/// </summary>
[YieldsOutput(typeof(PipelineLoopResult))]
internal sealed partial class NotifyMergeStageExecutor : Executor<NotifyMergeStageBridge>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotifyMergeStageExecutor> _logger;

    public NotifyMergeStageExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<NotifyMergeStageExecutor> logger)
        : base("Pipeline-NotifyMergeStage")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask HandleAsync(
        NotifyMergeStageBridge bridge, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        state.CurrentStage = "NotifyMerge";
        await PipelineStateHelpers.SaveAsync(context, state);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(bridge.GroupId, cancellationToken);
        if (group is null)
        {
            _logger.LogError("[Stage53A] NotifyMergeStage：找不到 Group={Id}，YieldOutput Completed=false（無法通知）", bridge.GroupId);
            await context.YieldOutputAsync(new PipelineLoopResult
            {
                GroupId = bridge.GroupId,
                Completed = false,
                FallbackReason = "group_not_found",
                LastResult = state.LastAgentResult,
            });
            return;
        }

        var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
        await tgs.NotifyBossMergeAsync(group, cancellationToken);
        _logger.LogInformation("[Stage53A] NotifyMergeStage：NotifyBossMergeAsync 完成 → YieldOutput Completed=true（Group={Id}）", bridge.GroupId);

        await context.YieldOutputAsync(new PipelineLoopResult
        {
            GroupId = bridge.GroupId,
            Completed = true,
            FallbackReason = null,
            LastResult = state.LastAgentResult,
        });
    }
}
