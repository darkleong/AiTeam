using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Orchestration.Boss;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Bot.Services;
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
[SendsMessage(typeof(KickoffCompletionRequest))]
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
        _logger.LogInformation("[Stage55A] KickoffStage：sync await HandleKickoffMeetingAsync(skipFinalize=true)（Group={Id}）", bridge.GroupId);
        var outcome = await kickoffRouter.HandleKickoffMeetingAsync(group, default, skipFinalize: true);

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

            default:
                _logger.LogWarning("[Stage55A] KickoffStage：未知 decision={Decision}（Group={Id}）— intervention", response.Decision, state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, $"未知 Kickoff decision: {response.Decision}", null);
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
