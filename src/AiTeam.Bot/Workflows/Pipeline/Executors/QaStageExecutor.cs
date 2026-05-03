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
/// 職責（dual handler）：
///   - HandleEntryAsync(QaStageBridge)：state.CurrentStage = "QA" → fetch fresh group → call FireStepsAsync(QA) enqueue → SendMessageAsync(QaCompletionRequest) yield
///   - HandleResponseAsync(QaCompletionResponse)：
///     ① result.Success=false → fallback qa_failed
///     ② result.Success=true → call qaCoordination.HandleQaCompletedAsync 同步 await（C2 拍板 — 內部 routing 邏輯不動：no_tests / passed / code_bug fix loop / env_or_test_issue / escalate_boss）
///         - HandleQaCompletedAsync 內部可能 fire Dev_fix（QaFixRound++）— 議題 9 fallback 時序紀律：sync await 完後立即檢查 group.QaFixRound > 0 → fallback qa_fix_loop（先清 marker → SendMessage）
///         - 否則 happy path：QA passed → SendMessageAsync(DocStageBridge)
///
/// Stage 53A 範圍邊界：
///   - bypass QA fix loop 子流程（屬 Stage 53B）
///   - HandleQaCompletedAsync 內部 fire Dev_fix 後 Pipeline marker 已清 → Dev_fix callback 自然走 legacy ✅
///
/// 紀律：fallback 時序（先清 marker → 再 SendMessage）+ type-explicit Bridge record + Stage 50 三件套。
/// </summary>
[SendsMessage(typeof(DocStageBridge))]
[SendsMessage(typeof(DevFixStageBridge))]
[SendsMessage(typeof(PipelineFallbackBridge))]
[SendsMessage(typeof(QaCompletionRequest))]
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
            _logger.LogInformation("[Stage53B] QaStage：result.Success=false → SetInterventionAndYieldAsync（Group={Id}）", state.GroupId);
            await SetInterventionAndYieldAsync(context, state.GroupId, $"QA 失敗：{result.Summary}", result);
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

        // 53B：QA fix loop 觸發（Petra 判 code_bug / back_to_reviewer → QaFixRound++）
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

        // group.Status 變化（needs_intervention / failed）→ 53B：SetInterventionAndYieldAsync 結束 Workflow
        if (refreshedGroup.Status == AiTeam.Shared.Constants.TaskStatus.NeedsIntervention || refreshedGroup.Status == AiTeam.Shared.Constants.TaskStatus.Failed)
        {
            _logger.LogInformation("[Stage53B] QaStage：HandleQaCompletedAsync 後 group.Status={Status} → SetInterventionAndYieldAsync（Group={Id}）",
                refreshedGroup.Status, state.GroupId);
            await SetInterventionAndYieldAsync(context, state.GroupId,
                refreshedGroup.InterventionReason ?? "QA failed/intervention", result);
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
