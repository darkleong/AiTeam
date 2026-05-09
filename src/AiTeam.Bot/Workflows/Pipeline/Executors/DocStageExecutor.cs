using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Orchestration.Boss;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Pipeline.Executors;

/// <summary>
/// Stage 53A：Doc stage Executor（J1 yield-resume 機制 — Agent 型 stage 第 5 個 / 最後一個 Agent stage）。
///
/// 職責（dual handler）：
///   - HandleEntryAsync(DocStageBridge)：state.CurrentStage = "Doc" → fetch fresh group → call FireStepsAsync(Doc) enqueue → SendMessageAsync(DocCompletionRequest) yield
///   - HandleResponseAsync(DocCompletionResponse)：
///     ① result.Success=false → fallback doc_failed
///     ② result.Success=true → state.DocDone=true → SendMessageAsync(NotifyMergeStageBridge)
///
/// Stage 53A 範圍邊界（happy path）：Doc 失敗罕見，fallback 到 legacy 處理（doc_failed reason 在 FinalizePipelineAsync 對應 NotifyBoss intervention）。
///
/// 紀律：fallback 時序 + type-explicit Bridge record + Stage 50 三件套。
/// </summary>
[SendsMessage(typeof(NotifyMergeStageBridge))]
[SendsMessage(typeof(PipelineFallbackBridge))]
[SendsMessage(typeof(DocCompletionRequest))]
// Stage 58-FF 五十三：Agent API 失敗 RequestPort（第 7 routing）
[SendsMessage(typeof(DocStageBridge))]
[SendsMessage(typeof(DocAgentApiFailureRequest))]
[YieldsOutput(typeof(PipelineLoopResult))]
internal sealed partial class DocStageExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocStageExecutor> _logger;

    public DocStageExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DocStageExecutor> logger)
        : base("Pipeline-DocStage")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleEntryAsync(DocStageBridge bridge, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        state.CurrentStage = "Doc";
        await PipelineStateHelpers.SaveAsync(context, state);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(bridge.GroupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage53A] DocStage：找不到 Group={Id}，fallback", bridge.GroupId);
            await ClearMarkerAndFallbackAsync(context, bridge.GroupId, "group_not_found", null);
            return;
        }

        var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
        await tgs.FireStepsAsync(group, [new WorkflowStep("Doc")], default);
        _logger.LogInformation("[Stage53A] DocStage：enqueue Doc + emit RequestPort yield（Group={Id}）", bridge.GroupId);

        await context.SendMessageAsync(new DocCompletionRequest(bridge.GroupId));
    }

    [MessageHandler]
    private async ValueTask HandleResponseAsync(DocCompletionResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        var result = response.Result;

        // Stage 58-FF 五十三：API 失敗 marker check（先於既有 Success=false fallback — 對齊 Stage 53B [BLOCKED] pattern）
        if (result.Summary.StartsWith("[API_FAILURE]", StringComparison.Ordinal))
        {
            _logger.LogWarning("[Stage58] DocStage：result [API_FAILURE] marker → fire agent_api_failure_intervention + yield 等 Christ（Group={Id}）",
                state.GroupId);
            state.LastAgentResult = result;
            state.LastAgentName = "Doc";
            await PipelineStateHelpers.SaveAsync(context, state);

            await using var apiFailScope = _scopeFactory.CreateAsyncScope();
            var apiFailRepo = apiFailScope.ServiceProvider.GetRequiredService<TaskRepository>();
            var apiFailGroup = await apiFailRepo.GetGroupByIdAsync(state.GroupId, default);
            if (apiFailGroup is null)
            {
                await ClearMarkerAndFallbackAsync(context, state.GroupId, "group_not_found", result);
                return;
            }
            var apiFailBossNotification = apiFailScope.ServiceProvider.GetRequiredService<BossNotificationService>();
            await apiFailBossNotification.NotifyBossAgentApiFailureAsync(apiFailGroup, "Doc", result.Summary, default);
            await PipelineHitlHelper.YieldForChristResponseAsync(
                context, new DocAgentApiFailureRequest(state.GroupId), _logger,
                "agent_api_failure_intervention", state.GroupId);
            return;
        }

        if (!result.Success)
        {
            _logger.LogInformation("[Stage53A] DocStage：result.Success=false → fallback doc_failed（Group={Id}）", state.GroupId);
            await ClearMarkerAndFallbackAsync(context, state.GroupId, "doc_failed", result);
            return;
        }

        state.DocDone = true;
        state.LastAgentResult = result;
        state.LastAgentName = "Doc";
        await PipelineStateHelpers.SaveAsync(context, state);
        _logger.LogInformation("[Stage53A] DocStage：passed → NotifyMergeStageBridge（Group={Id}）", state.GroupId);
        await context.SendMessageAsync(new NotifyMergeStageBridge(state.GroupId));
    }

    /// <summary>Stage 58-FF 五十三：Doc agent_api_failure HITL 回應 handler — Christ button click 後真三選 routing。
    /// continue → state.DocDone=true + SendMessage(NotifyMergeStageBridge) 跳下游（Doc 是最後 Agent stage）/ retry → SendMessage(DocStageBridge) re-invoke 同 stage / abort → SetIntervention via fallback path。</summary>
    [MessageHandler]
    private async ValueTask HandleAgentApiFailureResponseAsync(DocAgentApiFailureResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);

        switch (response.Action.ToLower())
        {
            case "continue":
                _logger.LogInformation("[Stage58] DocStage：agent_api_failure continue → NotifyMergeStageBridge（state.DocDone=true，跳過 Doc 進終結階段）（Group={Id}）", state.GroupId);
                state.DocDone = true;
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new NotifyMergeStageBridge(state.GroupId));
                return;

            case "retry":
                _logger.LogInformation("[Stage58] DocStage：agent_api_failure retry → DocStageBridge re-invoke 同 stage（儲值後）（Group={Id}）", state.GroupId);
                await context.SendMessageAsync(new DocStageBridge(state.GroupId));
                return;

            case "abort":
                _logger.LogInformation("[Stage58] DocStage：agent_api_failure abort → SetIntervention via fallback path（Group={Id}）", state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, "Doc API failure abort by Christ", state.LastAgentResult);
                return;

            default:
                _logger.LogWarning("[Stage58] DocStage：未知 agent_api_failure action={Action}（Group={Id}）— fallback intervention", response.Action, state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId, $"未知 agent_api_failure action: {response.Action}", state.LastAgentResult);
                return;
        }
    }

    /// <summary>Stage 58-FF 五十三：DocStage abort path intervention helper（DocStage 既有設計只有 fallback path，這裡新增同 Stage 53B/55B/57 pattern 的 intervention helper）。</summary>
    private async ValueTask SetInterventionAndYieldAsync(
        IWorkflowContext context, Guid groupId, string interventionReason, AgentExecutionResult? lastResult)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.Status, AiTeam.Shared.Constants.TaskStatus.NeedsIntervention)
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
