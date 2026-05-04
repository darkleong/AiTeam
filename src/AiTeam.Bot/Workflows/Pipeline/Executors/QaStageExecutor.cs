using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Orchestration.Qa;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = AiTeam.Shared.Constants.TaskStatus;

namespace AiTeam.Bot.Workflows.Pipeline.Executors;

/// <summary>
/// Stage 53A：QA stage Executor（J1 yield-resume 機制 — Agent 型 stage 第 4 個 + C2 整合 HandleQaCompletedAsync）。
///
/// 職責（dual + intervention handler）：
///   - HandleEntryAsync(QaStageBridge)：state.CurrentStage = "QA" → fetch fresh group → call FireStepsAsync(QA) enqueue → SendMessageAsync(QaCompletionRequest) yield
///   - HandleResponseAsync(QaCompletionResponse)：
///     ① result.Success=false → Stage 55B Session B：qa_failed_intervention HITL yield
///     ② result.Success=true → call qaCoordination.HandleQaCompletedAsync 同步 await
///         - QaFixRound > 0 → DevFixStageBridge（53B fix loop）
///         - Status NeedsIntervention/Failed → Stage 55B Session B：qa_failed_intervention HITL yield
///         - happy path → DocStageBridge
///   - HandleQaInterventionResponseAsync(QaInterventionResponse)（Stage 55B Session B 新加）：Christ button routing：
///     ① continue → SendMessageAsync(QaStageBridge)（QA 再試一輪 self-loop）
///     ② skip     → state.QaDone=true → SendMessageAsync(DocStageBridge)
///     ③ abort    → SetInterventionAndYieldAsync（mark failed end Pipeline）
///
/// 紀律：fallback 時序（先清 marker → 再 SendMessage）+ type-explicit Bridge record + Stage 50 三件套。
/// </summary>
[SendsMessage(typeof(DocStageBridge))]
[SendsMessage(typeof(DevFixStageBridge))]
[SendsMessage(typeof(PipelineFallbackBridge))]
[SendsMessage(typeof(QaCompletionRequest))]
[SendsMessage(typeof(QaStageBridge))]
[SendsMessage(typeof(QaInterventionRequest))]
[YieldsOutput(typeof(PipelineLoopResult))]
internal sealed partial class QaStageExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QaStageExecutor> _logger;

    public QaStageExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<QaStageExecutor> logger)
        : base("Pipeline-QaStage")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleEntryAsync(QaStageBridge bridge, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        state.CurrentStage = "QA";
        await PipelineStateHelpers.SaveAsync(context, state);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(bridge.GroupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage53A] QaStage：找不到 Group={Id}，fallback", bridge.GroupId);
            await context.SendMessageAsync(new PipelineFallbackBridge(bridge.GroupId, "group_not_found", null));
            return;
        }

        var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
        await tgs.FireStepsAsync(group, [new WorkflowStep(AgentNames.Qa)], default);
        _logger.LogInformation("[Stage53A] QaStage：enqueue QA + emit RequestPort yield（Group={Id}）", bridge.GroupId);

        await context.SendMessageAsync(new QaCompletionRequest(bridge.GroupId));
    }

    [MessageHandler]
    private async ValueTask HandleResponseAsync(QaCompletionResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        var result = response.Result;

        if (!result.Success)
        {
            // Stage 55B Session B：QA agent failed → 開 qa_failed_intervention BossInteraction + yield 等 Christ
            _logger.LogInformation("[Stage55B] QaStage：result.Success=false → qa_failed_intervention HITL yield（Group={Id}）", state.GroupId);
            state.LastAgentResult = result;
            await PipelineStateHelpers.SaveAsync(context, state);

            await using (var scopeQaFail = _scopeFactory.CreateAsyncScope())
            {
                var taskRepoFail = scopeQaFail.ServiceProvider.GetRequiredService<TaskRepository>();
                var groupFail = await taskRepoFail.GetGroupByIdAsync(state.GroupId, default);
                if (groupFail is null)
                {
                    _logger.LogError("[Stage55B] QaStage：qa_failed yield 前找不到 Group={Id}，fallback", state.GroupId);
                    await context.SendMessageAsync(new PipelineFallbackBridge(state.GroupId, "group_not_found", result));
                    return;
                }
                var qaCoordinationFail = scopeQaFail.ServiceProvider.GetRequiredService<QaCoordinationService>();
                await qaCoordinationFail.NotifyBossQaFailedInterventionAsync(groupFail, $"QA agent 失敗：{result.Summary}", default);
            }
            await PipelineHitlHelper.YieldForChristResponseAsync(
                context, new QaInterventionRequest(state.GroupId), _logger,
                "qa_failed_intervention", state.GroupId);
            return;
        }

        // C2 整合：call HandleQaCompletedAsync 同步 await（內部 routing 邏輯不動）
        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(state.GroupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage53A] QaStage：HandleQaCompletedAsync 前找不到 Group={Id}，fallback", state.GroupId);
            await context.SendMessageAsync(new PipelineFallbackBridge(state.GroupId, "group_not_found", result));
            return;
        }

        var qaCoordination = scope.ServiceProvider.GetRequiredService<QaCoordinationService>();
        await qaCoordination.HandleQaCompletedAsync(group, result, taskRepo, default);

        // 重新讀 group 看 QaFixRound 是否變化（HandleQaCompletedAsync 內部 routing 可能 fire Dev_fix + 增 QaFixRound）
        var refreshedGroup = await taskRepo.GetGroupByIdAsync(state.GroupId, default);
        if (refreshedGroup is null)
        {
            _logger.LogError("[Stage53A] QaStage：HandleQaCompletedAsync 後 Group={Id} 消失，fallback", state.GroupId);
            await context.SendMessageAsync(new PipelineFallbackBridge(state.GroupId, "group_not_found", result));
            return;
        }

        // ⚠️ Stage 55B Session B 自驗 follow-up：Status NeedsIntervention/Failed 必須先檢查（先於 QaFixRound > 0 fix loop）。
        // 理由：QaCoordination 達 QaFixRound 上限時 setStatus=NeedsIntervention 後 return（不 increment），
        // QaFixRound 仍 > 0 → 若先 hit fix loop branch 會錯誤推進 DevFix → Pipeline 進入 fix loop 死循環，
        // 跳過 Session B 預期的 qa_failed_intervention HITL yield。
        // group.Status 變化（needs_intervention / failed）→ Stage 55B Session B：qa_failed_intervention HITL yield 等 Christ
        if (refreshedGroup.Status == AiTeam.Shared.Constants.TaskStatus.NeedsIntervention || refreshedGroup.Status == AiTeam.Shared.Constants.TaskStatus.Failed)
        {
            _logger.LogInformation("[Stage55B] QaStage：HandleQaCompletedAsync 後 group.Status={Status} → qa_failed_intervention HITL yield（Group={Id}）",
                refreshedGroup.Status, state.GroupId);
            state.LastAgentResult = result;
            state.LastAgentName = "QA";
            await PipelineStateHelpers.SaveAsync(context, state);

            // Pipeline 接管 NotifyBoss（QaCoordination 內 escalate_boss / qa_max_rounds path Session A 已標記為 legacy dead，由 Pipeline 自己開）
            var qaCoordinationIntervene = scope.ServiceProvider.GetRequiredService<QaCoordinationService>();
            await qaCoordinationIntervene.NotifyBossQaFailedInterventionAsync(
                refreshedGroup, refreshedGroup.InterventionReason ?? "QA failed/intervention", default);
            await PipelineHitlHelper.YieldForChristResponseAsync(
                context, new QaInterventionRequest(state.GroupId), _logger,
                "qa_failed_intervention", state.GroupId);
            return;
        }

        // 53B：QA fix loop 觸發（Petra 判 code_bug / back_to_reviewer → QaFixRound++，未達上限）
        // QaCoordinationService 內 fire Dev_fix 已議題 F-1 修正 6-e Pipeline path skip
        // Pipeline 自 SendMessage(DevFixStageBridge) 觸發 framework fix loop
        if (refreshedGroup.QaFixRound > 0)
        {
            _logger.LogInformation("[Stage53B] QaStage：HandleQaCompletedAsync 觸發 QA fix loop (QaFixRound={Round}) → DevFixStageBridge（Group={Id}）",
                refreshedGroup.QaFixRound, state.GroupId);
            state.LastAgentResult = result;
            state.LastAgentName = "QA";
            await PipelineStateHelpers.SaveAsync(context, state);
            await context.SendMessageAsync(new DevFixStageBridge(state.GroupId));
            return;
        }

        // happy path：QA passed → DocStageBridge
        state.QaDone = true;
        state.LastAgentResult = result;
        state.LastAgentName = "QA";
        await PipelineStateHelpers.SaveAsync(context, state);
        _logger.LogInformation("[Stage53A] QaStage：QA passed → DocStageBridge（Group={Id}）", state.GroupId);
        await context.SendMessageAsync(new DocStageBridge(state.GroupId));
    }

    /// <summary>Stage 55B Session B：qa_failed_intervention HITL 回應 handler — Christ button click 後 routing。
    /// continue → SendMessage(QaStageBridge) QA self-loop / skip → SendMessage(DocStageBridge) / abort → SetInterventionAndYieldAsync end Pipeline。</summary>
    [MessageHandler]
    private async ValueTask HandleQaInterventionResponseAsync(QaInterventionResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);

        switch (response.Action.ToLower())
        {
            case "continue":
                _logger.LogInformation("[Stage55B] QaStage：qa_intervention continue → QaStageBridge 再試一輪（Group={Id}）", state.GroupId);
                await context.SendMessageAsync(new QaStageBridge(state.GroupId));
                return;

            case "skip":
                _logger.LogInformation("[Stage55B] QaStage：qa_intervention skip → DocStageBridge（Group={Id}）", state.GroupId);
                state.QaDone = true;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new DocStageBridge(state.GroupId));
                return;

            case "abort":
                _logger.LogInformation("[Stage55B] QaStage：qa_intervention abort → SetInterventionAndYieldAsync 結束 Pipeline（Group={Id}）", state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, "QA intervention abort by Christ", state.LastAgentResult);
                return;

            default:
                _logger.LogWarning("[Stage55B] QaStage：未知 qa_intervention action={Action}（Group={Id}）— SetInterventionAndYieldAsync", response.Action, state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, $"未知 qa_intervention action: {response.Action}", state.LastAgentResult);
                return;
        }
    }

    /// <summary>Stage 53B：intervention 統一 helper（qa_failed / qa_intervention）。</summary>
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
