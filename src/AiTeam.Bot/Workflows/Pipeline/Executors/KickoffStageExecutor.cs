using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Orchestration.Boss;
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
/// Stage 55A：Kickoff stage Executor（議題 G3 解法 — Pipeline 從 Kickoff 階段啟動）。
///
/// 職責（dual handler）：
///   - HandleEntryAsync(KickoffStageBridge)：
///     ① state.CurrentStage = "Kickoff"
///     ② sync await kickoffRouter.HandleKickoffMeetingAsync(group, ct, skipFinalize: true) — 跑純 Workflow + 寫 DB
///     ③ outcome null → SetInterventionAndYieldAsync（failure / Stage 51 mid-HITL yield 罕見）
///     ④ Pipeline 自己 call kickoffRouter.CreateKickoffConfirmationAsync(...) 開 BossInteraction（Stage 54 idempotency check 仍生效）
///     ⑤ SendMessageAsync(KickoffCompletionRequest) yield 等 Christ button via ResumeAfterKickoffAsync
///   - HandleResponseAsync(KickoffCompletionResponse)：
///     ① decision = "continue" → state.KickoffDone=true → SendMessage(DesignStageBridge)
///     ② decision = "stop" → SetCancelledAndYieldAsync 結束 Workflow
///     注意：modify / restart 走 legacy MeetingOrchestrationService.HandleKickoffConfirmedAsync 既有邏輯（不餵 Pipeline response）
///     —— Pipeline 仍 yield 在 RequestPort 等下一輪 button，無需 re-entry
///
/// 紀律：
///   - 三件套（Stage 50 踩坑 #10）：[SendsMessage] + partial class + 註解
///   - type-explicit Bridge record（Stage 52 fix#2）
///   - sync await inner router 模式對齊 Aria 拿捏 #2（Pipeline 完全控制 finalize）
/// </summary>
[SendsMessage(typeof(DesignStageBridge))]
[SendsMessage(typeof(KickoffStageBridge))]
[SendsMessage(typeof(KickoffCompletionRequest))]
[SendsMessage(typeof(KickoffAgentApiFailureRequest))]
[YieldsOutput(typeof(PipelineLoopResult))]
internal sealed partial class KickoffStageExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KickoffStageExecutor> _logger;

    public KickoffStageExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<KickoffStageExecutor> logger)
        : base("Pipeline-KickoffStage")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleEntryAsync(KickoffStageBridge bridge, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        state.CurrentStage = "Kickoff";
        await PipelineStateHelpers.SaveAsync(context, state);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var kickoffRouter = scope.ServiceProvider.GetRequiredService<FrameworkKickoffRouter>();

        var group = await taskRepo.GetGroupByIdAsync(bridge.GroupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage55A] KickoffStage：找不到 Group={Id}，intervention", bridge.GroupId);
            await SetInterventionAndYieldAsync(context, bridge.GroupId, "Group 不存在", null);
            return;
        }

        // sync await inner Workflow（skipFinalize=true）— Aria 拿捏 #2 同步 await 模式
        // Stage 60：sync await 外圍 catch MeetingSubprocessFailureException / LlmApiFailureException → fire 第 7 routing
        _logger.LogInformation("[Stage55A] KickoffStage：sync await HandleKickoffMeetingAsync(skipFinalize=true)（Group={Id}）", bridge.GroupId);
        KickoffMeetingOutcome? outcome;
        try
        {
            outcome = await kickoffRouter.HandleKickoffMeetingAsync(group, default, skipFinalize: true);
        }
        catch (Exception bizEx) when (bizEx is MeetingSubprocessFailureException or LlmApiFailureException)
        {
            await FireKickoffAgentApiFailureRoutingAsync(context, bridge.GroupId, bizEx);
            return;
        }

        if (outcome is null)
        {
            _logger.LogWarning("[Stage55A] KickoffStage：HandleKickoffMeetingAsync 回傳 null（failure / mid-HITL yield 罕見）→ intervention（Group={Id}）", bridge.GroupId);
            await SetInterventionAndYieldAsync(context, bridge.GroupId, "Kickoff Workflow 失敗或 yield", null);
            return;
        }

        // Pipeline 接管 finalize：自己 call CreateKickoffConfirmationAsync 開 BossInteraction
        var freshGroup = await taskRepo.GetGroupByIdAsync(bridge.GroupId, default);
        if (freshGroup is null)
        {
            _logger.LogError("[Stage55A] KickoffStage：finalize 前找不到 Group={Id}，intervention", bridge.GroupId);
            await SetInterventionAndYieldAsync(context, bridge.GroupId, "Kickoff finalize 前 Group 消失", null);
            return;
        }

        _logger.LogInformation("[Stage55A] KickoffStage：Pipeline 接管 CreateKickoffConfirmationAsync（Group={Id}）", bridge.GroupId);
        await kickoffRouter.CreateKickoffConfirmationAsync(
            freshGroup, outcome.LoopResult, outcome.KickoffTaskId, taskRepo, pushService, default);

        // yield 等 ResumeAfterKickoffAsync 餵 KickoffCompletionResponse
        await context.SendMessageAsync(new KickoffCompletionRequest(bridge.GroupId));
    }

    [MessageHandler]
    private async ValueTask HandleResponseAsync(KickoffCompletionResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);

        switch (response.Decision.ToLower())
        {
            case "continue":
                _logger.LogInformation("[Stage55A] KickoffStage：continue → DesignStageBridge（Group={Id}）", state.GroupId);
                state.KickoffDone = true;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new DesignStageBridge(state.GroupId));
                return;

            case "stop":
                _logger.LogInformation("[Stage55A] KickoffStage：stop → SetCancelledAndYieldAsync（Group={Id}）", state.GroupId);
                await SetCancelledAndYieldAsync(context, state.GroupId, "Kickoff 後老闆決定取消");
                return;

            // Stage 60-FF 五十五：modify path 真切遷 framework Pipeline（議題 C2 收口）
            case "modify":
                await HandleKickoffModifyAsync(context, state.GroupId, response.ModifyContent ?? "");
                return;

            // Stage 60：restart path 對齊「重新召開 Kickoff」 — 重設 KickoffRound + clear framework state + re-entry KickoffStageBridge
            case "restart":
                _logger.LogInformation("[Stage60] KickoffStage：restart → 重設 state + KickoffStageBridge re-entry（Group={Id}）", state.GroupId);
                await ResetKickoffStateForRestartAsync(state.GroupId);
                state.KickoffDone = false;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new KickoffStageBridge(state.GroupId));
                return;

            default:
                _logger.LogWarning("[Stage55A] KickoffStage：未知 decision={Decision}（Group={Id}）— intervention", response.Decision, state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, $"未知 Kickoff decision: {response.Decision}", null);
                return;
        }
    }

    /// <summary>
    /// Stage 60：modify path 跑 modify 一輪 → 寫 DB → re-open BossInteraction → SendMessage(KickoffCompletionRequest) yield。
    /// subprocess 失敗 → catch MeetingSubprocessFailureException / LlmApiFailureException → fire 第 7 routing。
    /// </summary>
    private async ValueTask HandleKickoffModifyAsync(IWorkflowContext context, Guid groupId, string modifyContent)
    {
        if (string.IsNullOrWhiteSpace(modifyContent))
        {
            _logger.LogWarning("[Stage60] KickoffStage：modify 但 ModifyContent 空 → 重發 KickoffCompletionRequest 等下一輪 button（Group={Id}）", groupId);
            await context.SendMessageAsync(new KickoffCompletionRequest(groupId));
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var kickoffRouter = scope.ServiceProvider.GetRequiredService<FrameworkKickoffRouter>();

        var group = await taskRepo.GetGroupByIdAsync(groupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage60] KickoffStage modify：找不到 Group={Id}，intervention", groupId);
            await SetInterventionAndYieldAsync(context, groupId, "Group 不存在", null);
            return;
        }

        await pushService.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = AgentNames.Pm,
            Status           = "running",
            CurrentTaskTitle = $"Kickoff 計劃書修改：{group.Title}"
        });

        try
        {
            _logger.LogInformation("[Stage60] KickoffStage modify：sync await RunKickoffModifyAsync（Group={Id}）", groupId);
            var modifyResult = await kickoffRouter.RunKickoffModifyAsync(group, modifyContent, default);

            // 寫 DB（對齊 legacy MeetingOrchestrationService line 698-708）
            var modifyLogEntry =
                $"\n## Christ 修改 Round {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n" +
                $"### Christ 修改意見\n{modifyContent}\n\n" +
                $"### Petra 回應（完整）\n{modifyResult.PetraFullOutput}\n";

            group.KickoffMeetingLog = (group.KickoffMeetingLog ?? "") + modifyLogEntry;

            if (modifyResult.Impact == "small" && !string.IsNullOrWhiteSpace(modifyResult.RevisedPlan))
                group.TaskPlan = modifyResult.RevisedPlan;

            await taskRepo.SaveAsync(default);

            // re-open BossInteraction（small → kickoff embed / large → restart embed）
            await kickoffRouter.CreateKickoffConfirmationAfterModifyAsync(group, modifyResult, default);

            // yield 等下一輪 button via ResumeAfterKickoffAsync 餵 KickoffCompletionResponse
            await context.SendMessageAsync(new KickoffCompletionRequest(groupId));
        }
        catch (Exception bizEx) when (bizEx is MeetingSubprocessFailureException or LlmApiFailureException)
        {
            await FireKickoffAgentApiFailureRoutingAsync(context, groupId, bizEx);
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

    /// <summary>Stage 60：restart 用 — 重設 group.KickoffRound + 清 KickoffFrameworkStateJson marker（讓 inner Workflow 可重跑）。</summary>
    private async ValueTask ResetKickoffStateForRestartAsync(Guid groupId)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.KickoffRound, 0)
                .SetProperty(g => g.KickoffFrameworkStateJson, (string?)null)
                .SetProperty(g => g.ActiveOrchestration, (string?)null), default);
    }

    /// <summary>
    /// Stage 60-FF 五十五：subprocess / API 失敗 fire 第 7 routing 共用 helper（HandleEntryAsync + HandleKickoffModifyAsync 共用）。
    /// 對齊 Stage 58 既有 4 stage executor pattern：fire BossInteraction agent_api_failure_intervention（agent="Petra-Kickoff"）+ SendMessage KickoffAgentApiFailureRequest yield。
    /// </summary>
    private async ValueTask FireKickoffAgentApiFailureRoutingAsync(
        IWorkflowContext context, Guid groupId, Exception bizEx)
    {
        var failSummary = bizEx is MeetingSubprocessFailureException sub
            ? $"[SUBPROCESS_FAILURE] {sub.AgentDisplayName}: {sub.RawError}"
            : bizEx is LlmApiFailureException api
                ? $"[API_FAILURE] {api.ProviderType}: {api.RawError}"
                : $"[UNKNOWN] {bizEx.Message}";

        _logger.LogWarning(bizEx,
            "[Stage60] KickoffStage catch → fire agent_api_failure_intervention (agent=Petra-Kickoff)（Group={Id}）",
            groupId);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var bossNotification = scope.ServiceProvider.GetRequiredService<BossNotificationService>();

        var freshGroup = await taskRepo.GetGroupByIdAsync(groupId, default);
        if (freshGroup is not null)
        {
            await bossNotification.NotifyBossAgentApiFailureAsync(freshGroup, "Petra-Kickoff", failSummary, default);
        }

        // SendMessage 進 KickoffAgentApiFailurePort yield 等 Christ 真三選 via 第 7 routing → ResumeAfterKickoffAgentApiFailureAsync
        await context.SendMessageAsync(new KickoffAgentApiFailureRequest(groupId));
    }

    /// <summary>
    /// Stage 60-FF 五十五：Kickoff Petra subprocess 失敗 routing 回應 handler — Christ 真三選 continue / retry / abort。
    /// 對齊 Stage 58 既有 4 stage executor HandleAgentApiFailureResponseAsync pattern。
    /// </summary>
    [MessageHandler]
    private async ValueTask HandleAgentApiFailureResponseAsync(KickoffAgentApiFailureResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);

        switch (response.Action.ToLower())
        {
            case "continue":
                _logger.LogInformation("[Stage60] KickoffStage agent_api_failure continue → DesignStageBridge（略過 Kickoff）（Group={Id}）", state.GroupId);
                state.KickoffDone = true;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new DesignStageBridge(state.GroupId));
                return;

            case "retry":
                _logger.LogInformation("[Stage60] KickoffStage agent_api_failure retry → KickoffStageBridge re-entry（Group={Id}）", state.GroupId);
                await ResetKickoffStateForRestartAsync(state.GroupId);
                state.KickoffDone = false;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new KickoffStageBridge(state.GroupId));
                return;

            case "abort":
                _logger.LogInformation("[Stage60] KickoffStage agent_api_failure abort → SetInterventionAndYieldAsync（Group={Id}）", state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, "Kickoff Agent API 失敗 — Christ abort", null);
                return;

            default:
                _logger.LogWarning("[Stage60] KickoffStage agent_api_failure 未知 action={Action}（Group={Id}）— intervention", response.Action, state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, $"未知 KickoffAgentApiFailure action: {response.Action}", null);
                return;
        }
    }

    /// <summary>Stage 55A：cancel 統一 helper（"stop" decision 用）— DB set group.Status=Cancelled + YieldOutput Completed=true。</summary>
    private async ValueTask SetCancelledAndYieldAsync(IWorkflowContext context, Guid groupId, string reason)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, "cancelled"), default);

        _logger.LogInformation("[Stage55A] KickoffStage：cancelled（Group={Id}, reason={Reason}）", groupId, reason);

        await context.YieldOutputAsync(new PipelineLoopResult
        {
            GroupId = groupId,
            Completed = true,
            FallbackReason = null,
            LastResult = null,
        });
    }

    /// <summary>Stage 55A：intervention 統一 helper — DB set group.Status=NeedsIntervention + InterventionReason → call NotifyBossInterventionAsync → YieldOutput Completed=true。</summary>
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
