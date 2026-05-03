using AiTeam.Bot.Agents;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Bot.Orchestration;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Pipeline.Executors;

/// <summary>
/// Stage 53A：DevPlan stage Executor（J1 yield-resume 機制 — Agent 型 stage 第 1 個）。
///
/// 職責（dual handler）：
///   - HandleEntryAsync(DevPlanStageBridge)：state.CurrentStage = "Dev_plan" → fetch fresh group → call FireStepsAsync(Dev_plan) enqueue legacy AgentQueueService（PipelineFrameworkStateJson != null 時 FireOneStepAsync 分流條件失敗 → 走 legacy）→ SendMessageAsync(DevPlanCompletionRequest) 進 RequestPort yield 等 callback
///   - HandleResponseAsync(DevPlanCompletionResponse)：收 result → result.Success=false 或 IsDevPlanFailed=true → fallback dev_plan_failed_escalate（先清 marker→ SendMessage Fallback）/ passed → state.DevPlanDone=true + LastAgentResult → SendMessageAsync(DevStageBridge)
///
/// Stage 53A 範圍邊界（happy path 限定）：
///   - bypass Petra Dev_plan review + appeal loop（屬 Stage 53B「appeal 子流程」）
///   - dev_plan_failed_escalate fallback 後 FinalizePipelineAsync 主動 call AppealOrchestrationService.HandleDevPlanCompletedAsync 接管 Petra review + appeal + Stage 43-A DevPlanRevision 重產
///
/// 紀律：
///   - 三件套（Stage 50 踩坑 #10）：[SendsMessage] + partial class + 註解
///   - type-explicit Bridge record（Stage 52 fix#2）：DevPlanCompletionRequest/Response 各自獨立型別
///   - fallback 時序紀律（Aria 拍板）：先清 marker（同步 await ExecuteUpdateAsync）→ 再 SendMessageAsync(PipelineFallbackBridge)
/// </summary>
[SendsMessage(typeof(DevStageBridge))]
[SendsMessage(typeof(PipelineFallbackBridge))]
[SendsMessage(typeof(DevPlanCompletionRequest))]
internal sealed partial class DevPlanStageExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DevPlanStageExecutor> _logger;

    public DevPlanStageExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DevPlanStageExecutor> logger)
        : base("Pipeline-DevPlanStage")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleEntryAsync(DevPlanStageBridge bridge, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        state.CurrentStage = "Dev_plan";
        await PipelineStateHelpers.SaveAsync(context, state);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(bridge.GroupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage53A] DevPlanStage：找不到 Group={Id}，fallback", bridge.GroupId);
            await ClearMarkerAndFallbackAsync(context, bridge.GroupId, "group_not_found", null);
            return;
        }

        var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
        await tgs.FireStepsAsync(group, [new WorkflowStep("Dev_plan")], default);
        _logger.LogInformation("[Stage53A] DevPlanStage：enqueue Dev_plan + emit RequestPort yield（Group={Id}）", bridge.GroupId);

        await context.SendMessageAsync(new DevPlanCompletionRequest(bridge.GroupId));
    }

    [MessageHandler]
    private async ValueTask HandleResponseAsync(DevPlanCompletionResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        var result = response.Result;

        if (!result.Success)
        {
            _logger.LogInformation("[Stage53A] DevPlanStage：result.Success=false → fallback dev_plan_failed_escalate（Group={Id}）", state.GroupId);
            await ClearMarkerAndFallbackAsync(context, state.GroupId, "dev_plan_failed_escalate", result);
            return;
        }

        // Inline IsDevPlanFailed 檢查（bypass Petra review，但保留 Stage 43-A plan 失敗判定）
        var devPlan = result.OutputContent ?? result.Summary;
        var (planFailed, planFailReason) = PmAgentCommons.IsDevPlanFailed(devPlan);
        if (planFailed)
        {
            _logger.LogInformation(
                "[Stage53A] DevPlanStage：IsDevPlanFailed=true（reason={Reason}）→ fallback dev_plan_failed_escalate（Group={Id}）",
                planFailReason, state.GroupId);
            await ClearMarkerAndFallbackAsync(context, state.GroupId, "dev_plan_failed_escalate", result);
            return;
        }

        state.DevPlanDone = true;
        state.LastAgentResult = result;
        state.LastAgentName = "Dev_plan";
        await PipelineStateHelpers.SaveAsync(context, state);
        _logger.LogInformation("[Stage53A] DevPlanStage：passed → DevStageBridge（Group={Id}）", state.GroupId);
        await context.SendMessageAsync(new DevStageBridge(state.GroupId));
    }

    /// <summary>fallback 時序紀律：先清 marker（同步 await ExecuteUpdateAsync）→ 再 SendMessageAsync(PipelineFallbackBridge)。
    /// 確保 Dev_fix / 重產 callback 觸發時 PipelineFrameworkStateJson 已 null → 自然走 legacy。</summary>
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
