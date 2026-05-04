using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = AiTeam.Shared.Constants.TaskStatus;

namespace AiTeam.Bot.Workflows.Pipeline.Executors;

/// <summary>
/// Stage 55A：Design stage Executor（議題 G3 解法 — Pipeline 接管 Design stage）。
///
/// 職責（dual handler）：
///   - HandleEntryAsync(DesignStageBridge)：
///     ① state.CurrentStage = "Design"
///     ② sync await designRouter.HandleDesignMeetingAsync(group, ct, skipFinalize: true) — 跑純 Workflow + 寫 DB（含 IssueUrls / UiSpecContent / DesignPlan）
///     ③ outcome null → SetInterventionAndYieldAsync
///     ④ Pipeline 自己 call designRouter.FinalizeDesignAsync(...,skipFireDevPlan: true) → 看回傳 DesignFinalizationDecision：
///        - SplitProposalOpened → CleanupWorkingDirAndMarkerAsync + SendMessage(PipelineFallbackBridge "design_split_proposal_opened") — sub-task chain 接手，parent Pipeline 結束
///        - ConsensusNoSplit → CleanupWorkingDirAndMarkerAsync + state.DesignDone=true + SendMessage(DevPlanStageBridge)
///        - EscalateConfirmationOpened → CleanupWorkingDirAndMarkerAsync + SendMessage(DesignCompletionRequest) yield 等 design_continue button
///   - HandleResponseAsync(DesignCompletionResponse)：
///     ① decision = "continue" → state.DesignDone=true → SendMessage(DevPlanStageBridge)
///     ② decision = "stop" → SetCancelledAndYieldAsync
///     注意：modify 走 legacy MeetingOrchestrationService.HandleDesignConfirmedAsync 既有邏輯（不餵 Pipeline response）
///     —— Pipeline 仍 yield 在 RequestPort 等下一輪 button
///
/// 紀律對齊 KickoffStageExecutor（同 stage Executor pattern）。
/// </summary>
[SendsMessage(typeof(DevPlanStageBridge))]
[SendsMessage(typeof(PipelineFallbackBridge))]
[SendsMessage(typeof(DesignCompletionRequest))]
[YieldsOutput(typeof(PipelineLoopResult))]
internal sealed partial class DesignStageExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DesignStageExecutor> _logger;

    public DesignStageExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DesignStageExecutor> logger)
        : base("Pipeline-DesignStage")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleEntryAsync(DesignStageBridge bridge, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        state.CurrentStage = "Design";
        await PipelineStateHelpers.SaveAsync(context, state);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo     = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var designRouter = scope.ServiceProvider.GetRequiredService<FrameworkDesignRouter>();

        var group = await taskRepo.GetGroupByIdAsync(bridge.GroupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage55A] DesignStage：找不到 Group={Id}，intervention", bridge.GroupId);
            await SetInterventionAndYieldAsync(context, bridge.GroupId, "Group 不存在", null);
            return;
        }

        // sync await inner Workflow（skipFinalize=true，inner finally 跳過 cleanup workingDir + marker 給 Pipeline 接管）
        _logger.LogInformation("[Stage55A] DesignStage：sync await HandleDesignMeetingAsync(skipFinalize=true)（Group={Id}）", bridge.GroupId);
        var outcome = await designRouter.HandleDesignMeetingAsync(group, default, skipFinalize: true);

        if (outcome is null)
        {
            _logger.LogWarning("[Stage55A] DesignStage：HandleDesignMeetingAsync 回傳 null → intervention（Group={Id}）", bridge.GroupId);
            await SetInterventionAndYieldAsync(context, bridge.GroupId, "Design Workflow 失敗", null);
            return;
        }

        // Pipeline 接管 finalize：自己 call FinalizeDesignAsync(skipFireDevPlan: true)
        var freshGroup = await taskRepo.GetGroupByIdAsync(bridge.GroupId, default);
        if (freshGroup is null)
        {
            _logger.LogError("[Stage55A] DesignStage：finalize 前找不到 Group={Id}，intervention", bridge.GroupId);
            await SetInterventionAndYieldAsync(context, bridge.GroupId, "Design finalize 前 Group 消失", null);
            return;
        }

        _logger.LogInformation("[Stage55A] DesignStage：Pipeline 接管 FinalizeDesignAsync(skipFireDevPlan=true)（Group={Id}）", bridge.GroupId);
        var decision = await designRouter.FinalizeDesignAsync(
            freshGroup, outcome.LoopResult, outcome.WorkingDir, default, skipFireDevPlan: true);

        // Pipeline 接管後的 cleanup（inner finally skipFinalize=true 跳過了）
        await designRouter.CleanupWorkingDirAndMarkerAsync(bridge.GroupId, outcome.WorkingDir, default);

        switch (decision)
        {
            case DesignFinalizationDecision.SplitProposalOpened:
                _logger.LogInformation("[Stage55A] DesignStage：SplitProposalOpened → sub-task chain 接手，parent Pipeline 結束（Group={Id}）", bridge.GroupId);
                await context.SendMessageAsync(new PipelineFallbackBridge(
                    bridge.GroupId, "design_split_proposal_opened", null));
                return;

            case DesignFinalizationDecision.ConsensusNoSplit:
                _logger.LogInformation("[Stage55A] DesignStage：ConsensusNoSplit → DevPlanStageBridge（Group={Id}）", bridge.GroupId);
                state.DesignDone = true;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new DevPlanStageBridge(bridge.GroupId));
                return;

            case DesignFinalizationDecision.EscalateConfirmationOpened:
                _logger.LogInformation("[Stage55A] DesignStage：EscalateConfirmationOpened → yield 等 design_continue button（Group={Id}）", bridge.GroupId);
                await context.SendMessageAsync(new DesignCompletionRequest(bridge.GroupId));
                return;
        }
    }

    [MessageHandler]
    private async ValueTask HandleResponseAsync(DesignCompletionResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);

        switch (response.Decision.ToLower())
        {
            case "continue":
                _logger.LogInformation("[Stage55A] DesignStage：continue → DevPlanStageBridge（Group={Id}）", state.GroupId);
                state.DesignDone = true;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new DevPlanStageBridge(state.GroupId));
                return;

            case "stop":
                _logger.LogInformation("[Stage55A] DesignStage：stop → SetCancelledAndYieldAsync（Group={Id}）", state.GroupId);
                await SetCancelledAndYieldAsync(context, state.GroupId, "Design 後老闆決定取消");
                return;

            default:
                _logger.LogWarning("[Stage55A] DesignStage：未知 decision={Decision}（Group={Id}）— intervention", response.Decision, state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, $"未知 Design decision: {response.Decision}", null);
                return;
        }
    }

    private async ValueTask SetCancelledAndYieldAsync(IWorkflowContext context, Guid groupId, string reason)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, "cancelled"), default);

        _logger.LogInformation("[Stage55A] DesignStage：cancelled（Group={Id}, reason={Reason}）", groupId, reason);

        await context.YieldOutputAsync(new PipelineLoopResult
        {
            GroupId = groupId,
            Completed = true,
            FallbackReason = null,
            LastResult = null,
        });
    }

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
