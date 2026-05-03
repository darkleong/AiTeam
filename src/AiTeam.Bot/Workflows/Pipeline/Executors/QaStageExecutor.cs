using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Orchestration.Qa;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
[SendsMessage(typeof(PipelineFallbackBridge))]
[SendsMessage(typeof(QaCompletionRequest))]
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
            await ClearMarkerAndFallbackAsync(context, bridge.GroupId, "group_not_found", null);
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
            _logger.LogInformation("[Stage53A] QaStage：result.Success=false → fallback qa_failed（Group={Id}）", state.GroupId);
            await ClearMarkerAndFallbackAsync(context, state.GroupId, "qa_failed", result);
            return;
        }

        // C2 整合：call HandleQaCompletedAsync 同步 await（內部 routing 邏輯不動）
        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(state.GroupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage53A] QaStage：HandleQaCompletedAsync 前找不到 Group={Id}，fallback", state.GroupId);
            await ClearMarkerAndFallbackAsync(context, state.GroupId, "group_not_found", result);
            return;
        }

        var qaCoordination = scope.ServiceProvider.GetRequiredService<QaCoordinationService>();
        await qaCoordination.HandleQaCompletedAsync(group, result, taskRepo, default);

        // 重新讀 group 看 QaFixRound 是否變化（HandleQaCompletedAsync 內部 routing 可能 fire Dev_fix + 增 QaFixRound）
        var refreshedGroup = await taskRepo.GetGroupByIdAsync(state.GroupId, default);
        if (refreshedGroup is null)
        {
            _logger.LogError("[Stage53A] QaStage：HandleQaCompletedAsync 後 Group={Id} 消失，fallback", state.GroupId);
            await ClearMarkerAndFallbackAsync(context, state.GroupId, "group_not_found", result);
            return;
        }

        // QA fix loop 觸發（Petra 判 code_bug → fire Dev_fix + QaFixRound++）→ fallback qa_fix_loop
        if (refreshedGroup.QaFixRound > 0)
        {
            _logger.LogInformation("[Stage53A] QaStage：HandleQaCompletedAsync 觸發 QA fix loop (QaFixRound={Round}) → fallback qa_fix_loop（Group={Id}，Dev_fix 已 fire 由 legacy 接管）",
                refreshedGroup.QaFixRound, state.GroupId);
            await ClearMarkerAndFallbackAsync(context, state.GroupId, "qa_fix_loop", result);
            return;
        }

        // group.Status 變化（needs_intervention / failed）→ fallback qa_intervention
        if (refreshedGroup.Status == AiTeam.Shared.Constants.TaskStatus.NeedsIntervention || refreshedGroup.Status == AiTeam.Shared.Constants.TaskStatus.Failed)
        {
            _logger.LogInformation("[Stage53A] QaStage：HandleQaCompletedAsync 後 group.Status={Status} → fallback qa_intervention（Group={Id}）",
                refreshedGroup.Status, state.GroupId);
            await ClearMarkerAndFallbackAsync(context, state.GroupId, "qa_intervention", result);
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
