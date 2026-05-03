using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration;
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
