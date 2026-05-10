using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Orchestration.Boss;
using AiTeam.Bot.Orchestration.Epic;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Bot.Services;
using AiTeam.Shared.Constants;
using AiTeam.Shared.ViewModels;
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
[SendsMessage(typeof(DesignStageBridge))]
[SendsMessage(typeof(PipelineFallbackBridge))]
[SendsMessage(typeof(DesignCompletionRequest))]
[SendsMessage(typeof(SplitTaskProposalRequest))]
[SendsMessage(typeof(DesignAgentApiFailureRequest))]
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
        // Stage 60：sync await 外圍 catch MeetingSubprocessFailureException / LlmApiFailureException → fire 第 7 routing
        _logger.LogInformation("[Stage55A] DesignStage：sync await HandleDesignMeetingAsync(skipFinalize=true)（Group={Id}）", bridge.GroupId);
        DesignMeetingOutcome? outcome;
        try
        {
            outcome = await designRouter.HandleDesignMeetingAsync(group, default, skipFinalize: true);
        }
        catch (Exception bizEx) when (bizEx is MeetingSubprocessFailureException or LlmApiFailureException)
        {
            await FireDesignAgentApiFailureRoutingAsync(context, bridge.GroupId, bizEx);
            return;
        }

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
                // Stage 55B Session B：split_task_proposal HITL — BossInteraction 已由 FinalizeDesignAsync 內部 BuildSplitTaskProposalAsync 開
                // yield 等 Christ button → ResumeAfterSplitTaskProposalAsync → HandleSplitTaskProposalResponseAsync routing
                _logger.LogInformation("[Stage55B] DesignStage：SplitProposalOpened → split_task_proposal HITL yield 等 Christ（Group={Id}）", bridge.GroupId);
                await PipelineHitlHelper.YieldForChristResponseAsync(
                    context, new SplitTaskProposalRequest(bridge.GroupId), _logger,
                    "split_task_proposal", bridge.GroupId);
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

            // Stage 60-FF 五十五：modify path 真切遷 framework Pipeline（議題 H1 收口）
            case "modify":
                await HandleDesignModifyAsync(context, state.GroupId, response.ModifyContent ?? "");
                return;

            default:
                _logger.LogWarning("[Stage55A] DesignStage：未知 decision={Decision}（Group={Id}）— intervention", response.Decision, state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, $"未知 Design decision: {response.Decision}", null);
                return;
        }
    }

    /// <summary>
    /// Stage 60：modify path 跑 modify 一輪 → 寫 DB → re-open BossInteraction → SendMessage(DesignCompletionRequest) yield。
    /// petraSessionId 從最新 design BossInteraction.contextJson 取（FinalizeDesignAsync 寫入）。
    /// </summary>
    private async ValueTask HandleDesignModifyAsync(IWorkflowContext context, Guid groupId, string modifyContent)
    {
        if (string.IsNullOrWhiteSpace(modifyContent))
        {
            _logger.LogWarning("[Stage60] DesignStage：modify 但 ModifyContent 空 → 重發 DesignCompletionRequest 等下一輪 button（Group={Id}）", groupId);
            await context.SendMessageAsync(new DesignCompletionRequest(groupId));
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var interactionRepo = scope.ServiceProvider.GetRequiredService<BossInteractionRepository>();
        var designRouter = scope.ServiceProvider.GetRequiredService<FrameworkDesignRouter>();

        var group = await taskRepo.GetGroupByIdAsync(groupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage60] DesignStage modify：找不到 Group={Id}，intervention", groupId);
            await SetInterventionAndYieldAsync(context, groupId, "Group 不存在", null);
            return;
        }

        // 從最新 design BossInteraction.contextJson 取 petraSessionId（FinalizeDesignAsync / CreateDesignConfirmationAfterModifyAsync 寫入）
        var latestDesignInteraction = await interactionRepo.GetLatestForGroupByTypeAsync(groupId, "design", default);
        string petraSessionId = "";
        if (latestDesignInteraction is not null && !string.IsNullOrWhiteSpace(latestDesignInteraction.ContextJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(latestDesignInteraction.ContextJson);
                if (doc.RootElement.TryGetProperty("petraSessionId", out var pid))
                    petraSessionId = pid.GetString() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Stage60] DesignStage modify：parse petraSessionId 失敗（Group={Id}）", groupId);
            }
        }

        if (string.IsNullOrEmpty(petraSessionId))
        {
            _logger.LogWarning("[Stage60] DesignStage modify：取不到 petraSessionId → fallback to new GUID（Petra context 將遺失）（Group={Id}）", groupId);
            petraSessionId = Guid.NewGuid().ToString();
        }

        await pushService.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = AgentNames.Pm,
            Status           = "running",
            CurrentTaskTitle = $"設計規劃修改：{group.Title}"
        });

        try
        {
            _logger.LogInformation("[Stage60] DesignStage modify：sync await RunDesignModifyAsync（Group={Id}, petraSessionId={Sid}）", groupId, petraSessionId);
            var modifyResult = await designRouter.RunDesignModifyAsync(group, modifyContent, petraSessionId, default);

            // 寫 DB（對齊 legacy MeetingOrchestrationService line 887-895）
            var modifyLogEntry =
                $"\n## Christ 設計修改 {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n" +
                $"### Christ 修改指引\n{modifyContent}\n\n" +
                $"### Petra 修改後設計規劃書\n{modifyResult.RevisedPlan}\n";
            group.DesignMeetingLog = (group.DesignMeetingLog ?? "") + modifyLogEntry;

            if (!string.IsNullOrWhiteSpace(modifyResult.RevisedPlan))
                group.DesignPlan = modifyResult.RevisedPlan;
            await taskRepo.SaveAsync(default);

            // re-open BossInteraction
            await designRouter.CreateDesignConfirmationAfterModifyAsync(group, modifyResult, petraSessionId, default);

            // yield 等下一輪 button via ResumeAfterDesignAsync 餵 DesignCompletionResponse
            await context.SendMessageAsync(new DesignCompletionRequest(groupId));
        }
        catch (Exception bizEx) when (bizEx is MeetingSubprocessFailureException or LlmApiFailureException)
        {
            await FireDesignAgentApiFailureRoutingAsync(context, groupId, bizEx);
        }
        finally
        {
            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = AgentNames.Pm,
                Status           = "idle",
                CurrentTaskTitle = null
            });
        }
    }

    /// <summary>
    /// Stage 60-FF 五十五：subprocess / API 失敗 fire 第 7 routing 共用 helper（HandleEntryAsync + HandleDesignModifyAsync 共用）。
    /// </summary>
    private async ValueTask FireDesignAgentApiFailureRoutingAsync(
        IWorkflowContext context, Guid groupId, Exception bizEx)
    {
        var failSummary = bizEx is MeetingSubprocessFailureException sub
            ? $"[SUBPROCESS_FAILURE] {sub.AgentDisplayName}: {sub.RawError}"
            : bizEx is LlmApiFailureException api
                ? $"[API_FAILURE] {api.ProviderType}: {api.RawError}"
                : $"[UNKNOWN] {bizEx.Message}";

        _logger.LogWarning(bizEx,
            "[Stage60] DesignStage catch → fire agent_api_failure_intervention (agent=Petra-Design)（Group={Id}）",
            groupId);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var bossNotification = scope.ServiceProvider.GetRequiredService<BossNotificationService>();

        var freshGroup = await taskRepo.GetGroupByIdAsync(groupId, default);
        if (freshGroup is not null)
        {
            await bossNotification.NotifyBossAgentApiFailureAsync(freshGroup, "Petra-Design", failSummary, default);
        }

        await context.SendMessageAsync(new DesignAgentApiFailureRequest(groupId));
    }

    /// <summary>
    /// Stage 60-FF 五十五：Design Petra subprocess 失敗 routing 回應 handler — Christ 真三選 continue / retry / abort。
    /// </summary>
    [MessageHandler]
    private async ValueTask HandleAgentApiFailureResponseAsync(DesignAgentApiFailureResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);

        switch (response.Action.ToLower())
        {
            case "continue":
                _logger.LogInformation("[Stage60] DesignStage agent_api_failure continue → DevPlanStageBridge（略過 Design）（Group={Id}）", state.GroupId);
                state.DesignDone = true;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new DevPlanStageBridge(state.GroupId));
                return;

            case "retry":
                _logger.LogInformation("[Stage60] DesignStage agent_api_failure retry → DesignStageBridge re-entry（Group={Id}）", state.GroupId);
                await ResetDesignStateForRetryAsync(state.GroupId);
                state.DesignDone = false;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new DesignStageBridge(state.GroupId));
                return;

            case "abort":
                _logger.LogInformation("[Stage60] DesignStage agent_api_failure abort → SetInterventionAndYieldAsync（Group={Id}）", state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, "Design Agent API 失敗 — Christ abort", null);
                return;

            default:
                _logger.LogWarning("[Stage60] DesignStage agent_api_failure 未知 action={Action}（Group={Id}）— intervention", response.Action, state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, $"未知 DesignAgentApiFailure action: {response.Action}", null);
                return;
        }
    }

    /// <summary>Stage 60：retry 用 — 清 DesignFrameworkStateJson marker（讓 inner Workflow 可重跑）。</summary>
    private async ValueTask ResetDesignStateForRetryAsync(Guid groupId)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.DesignFrameworkStateJson, (string?)null)
                .SetProperty(g => g.ActiveOrchestration, (string?)null), default);
    }

    /// <summary>Stage 55B Session B：split_task_proposal HITL 回應 handler — Christ button click 後 routing。
    /// accept/modify → BuildEpicSubTasksAsync 創 sub-task chain → SendMessage(PipelineFallbackBridge) parent ends（sub-task chain 接手）
    /// reject → state.DesignDone=true → SendMessage(DevPlanStageBridge)（不拆繼續原 Pipeline）
    /// abort  → SetCancelledAndYieldAsync。</summary>
    [MessageHandler]
    private async ValueTask HandleSplitTaskProposalResponseAsync(SplitTaskProposalResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);

        switch (response.Action.ToLower())
        {
            case "accept":
            {
                _logger.LogInformation("[Stage55B] DesignStage：split_task_proposal accept → BuildEpicSubTasks + sub-task chain 接手（Group={Id}）", state.GroupId);
                if (string.IsNullOrWhiteSpace(response.SplitProposalJson))
                {
                    _logger.LogWarning("[Stage55B] DesignStage：split_accept 但 SplitProposalJson 缺，fallback to PipelineFallbackBridge（Group={Id}）", state.GroupId);
                    await context.SendMessageAsync(new PipelineFallbackBridge(state.GroupId, "split_proposal_json_missing", null));
                    return;
                }
                var proposal = DesignSplitProposalEvaluator.TryParseSplitProposal(response.SplitProposalJson);
                if (proposal is null || !proposal.ShouldSplit || proposal.Phases is { Count: 0 })
                {
                    _logger.LogWarning("[Stage55B] DesignStage：split_accept SplitProposalJson 解析失敗，視同 reject → DevPlanStage（Group={Id}）", state.GroupId);
                    state.DesignDone = true;
                    await PipelineStateHelpers.SaveAsync(context, state);
                    await context.SendMessageAsync(new DevPlanStageBridge(state.GroupId));
                    return;
                }
                await using (var scopeAccept = _scopeFactory.CreateAsyncScope())
                {
                    var epicChain = scopeAccept.ServiceProvider.GetRequiredService<EpicChainService>();
                    await epicChain.BuildEpicSubTasksAsync(state.GroupId, proposal, default);
                }
                await context.SendMessageAsync(new PipelineFallbackBridge(state.GroupId, "split_accepted", null));
                return;
            }

            case "modify":
            {
                _logger.LogInformation("[Stage55B] DesignStage：split_task_proposal modify → BuildEpicSubTasks(modified) + sub-task chain 接手（Group={Id}）", state.GroupId);
                SplitProposal? modified = null;
                try { modified = DesignSplitProposalEvaluator.TryParseSplitProposal(response.ModifyContent ?? ""); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Stage55B] DesignStage：split_modify Christ 改寫的 phases JSON 解析失敗，fallback 視同 reject（Group={Id}）", state.GroupId);
                }
                if (modified is null || !modified.ShouldSplit || modified.Phases is { Count: 0 })
                {
                    _logger.LogInformation("[Stage55B] DesignStage：split_modify fallback to reject → DevPlanStage（Group={Id}）", state.GroupId);
                    state.DesignDone = true;
                    await PipelineStateHelpers.SaveAsync(context, state);
                    await context.SendMessageAsync(new DevPlanStageBridge(state.GroupId));
                    return;
                }
                await using (var scopeModify = _scopeFactory.CreateAsyncScope())
                {
                    var epicChain = scopeModify.ServiceProvider.GetRequiredService<EpicChainService>();
                    await epicChain.BuildEpicSubTasksAsync(state.GroupId, modified, default);
                }
                await context.SendMessageAsync(new PipelineFallbackBridge(state.GroupId, "split_modified", null));
                return;
            }

            case "reject":
                _logger.LogInformation("[Stage55B] DesignStage：split_task_proposal reject → DevPlanStageBridge（不拆繼續原 Pipeline）（Group={Id}）", state.GroupId);
                state.DesignDone = true;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new DevPlanStageBridge(state.GroupId));
                return;

            case "abort":
                _logger.LogInformation("[Stage55B] DesignStage：split_task_proposal abort → SetCancelledAndYieldAsync 結束 Pipeline（Group={Id}）", state.GroupId);
                await SetCancelledAndYieldAsync(context, state.GroupId, "Split task proposal abort by Christ");
                return;

            default:
                _logger.LogWarning("[Stage55B] DesignStage：未知 split_task_proposal action={Action}（Group={Id}）— SetIntervention", response.Action, state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, $"未知 split_task_proposal action: {response.Action}", null);
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
