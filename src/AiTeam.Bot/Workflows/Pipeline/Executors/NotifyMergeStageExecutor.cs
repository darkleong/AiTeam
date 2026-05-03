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
        // Stage 53A 驗收期 follow-up #2：對齊 legacy QaCoordinationService L99-103 寫法 — 先 MarkGroupDoneOrInterventionAsync 設 group.Status=Done，再決定 NotifyBossMerge / NotifyBossIntervention。
        // 原 Pipeline NotifyMergeStage 只 call NotifyBossMergeAsync 沒 mark Done，場景 B group.Status=done 是靠 Bug #1 第二次 legacy fall through side effect 完成（修 Bug #1 後此 mark Done 必須補上）。
        await tgs.MarkGroupDoneOrInterventionAsync(group, taskRepo, cancellationToken);
        if (group.Status == AiTeam.Shared.Constants.TaskStatus.Done)
            await tgs.NotifyBossMergeAsync(group, cancellationToken);
        else
            await tgs.NotifyBossInterventionAsync(group, cancellationToken);
        _logger.LogInformation("[Stage53A] NotifyMergeStage：MarkDone + NotifyBossMergeAsync 完成 (status={Status}) → YieldOutput Completed=true（Group={Id}）", group.Status, bridge.GroupId);

        await context.YieldOutputAsync(new PipelineLoopResult
        {
            GroupId = bridge.GroupId,
            Completed = true,
            FallbackReason = null,
            LastResult = state.LastAgentResult,
        });
    }
}
