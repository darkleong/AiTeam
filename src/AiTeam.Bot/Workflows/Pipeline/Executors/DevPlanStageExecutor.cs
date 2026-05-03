using AiTeam.Bot.Agents;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Orchestration.Appeal;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = AiTeam.Shared.Constants.TaskStatus;

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
[SendsMessage(typeof(DevPlanRetryBridge))]
[SendsMessage(typeof(PipelineFallbackBridge))]
[SendsMessage(typeof(DevPlanCompletionRequest))]
[YieldsOutput(typeof(PipelineLoopResult))]
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
        => await EnqueueDevPlanAndYieldAsync(bridge.GroupId, context);

    /// <summary>Stage 53B：DevPlan retry handler — DevPlanRevision++ 後重產 Dev_plan（self-loop via DevPlanRetryBridge）。
    /// reuse EnqueueDevPlanAndYieldAsync 共用邏輯（避免 entry/retry 兩 handler 重寫 fire + RequestPort yield）。</summary>
    [MessageHandler]
    private async ValueTask HandleRetryAsync(DevPlanRetryBridge bridge, IWorkflowContext context)
    {
        _logger.LogInformation("[Stage53B] DevPlanStage：retry handler 觸發（DevPlanRetryBridge, Group={Id}）", bridge.GroupId);
        await EnqueueDevPlanAndYieldAsync(bridge.GroupId, context);
    }

    /// <summary>共用 enqueue + yield 邏輯（entry / retry 兩 handler 共用）。</summary>
    private async ValueTask EnqueueDevPlanAndYieldAsync(Guid groupId, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        state.CurrentStage = "Dev_plan";
        await PipelineStateHelpers.SaveAsync(context, state);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(groupId, default);
        if (group is null)
        {
            _logger.LogError("[Stage53A] DevPlanStage：找不到 Group={Id}，fallback", groupId);
            await context.SendMessageAsync(new PipelineFallbackBridge(groupId, "group_not_found", null));
            return;
        }

        var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
        await tgs.FireStepsAsync(group, [new WorkflowStep("Dev_plan")], default);
        _logger.LogInformation("[Stage53A] DevPlanStage：enqueue Dev_plan + emit RequestPort yield（Group={Id}）", groupId);

        await context.SendMessageAsync(new DevPlanCompletionRequest(groupId));
    }

    [MessageHandler]
    private async ValueTask HandleResponseAsync(DevPlanCompletionResponse response, IWorkflowContext context)
    {
        var state = await PipelineStateHelpers.ReadAsync(context);
        var result = response.Result;

        var devPlan = result.OutputContent ?? result.Summary;
        var (planFailed, _) = PmAgentCommons.IsDevPlanFailed(devPlan);

        // 53B：Dev_plan failed → 內 call HandleDevPlanCompletedAsync（Pipeline path 跳過 Stage 49 framework path + 8 處 side effects 由議題 F-1 修正在 Service 內 skip）
        if (!result.Success || planFailed)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
            var group = await taskRepo.GetGroupByIdAsync(state.GroupId, default);
            if (group is null)
            {
                _logger.LogError("[Stage53B] DevPlanStage：appeal 前找不到 Group={Id}，fallback", state.GroupId);
                await context.SendMessageAsync(new PipelineFallbackBridge(state.GroupId, "group_not_found", result));
                return;
            }

            var projectId = string.IsNullOrWhiteSpace(group.Project)
                ? (Guid?)null
                : await taskRepo.GetProjectIdByNameAsync(group.Project, default);
            var appealOrchestration = scope.ServiceProvider.GetRequiredService<AppealOrchestrationService>();

            // HandleDevPlanCompletedAsync return true = Petra approve（plan 可用，繼續 Dev）/ false = revise+stillFailed retry / escalate
            // Pipeline path 下 method 內部 8 處 fire/UpdateStatus/NotifyBoss 由議題 F-1 修正全 skip
            var shouldContinueDev = await appealOrchestration.HandleDevPlanCompletedAsync(
                group, result, taskRepo, projectId, default);

            // 重讀 group 看內部 routing 結果（DevPlanRevision 增加 / Status 變化）
            var refreshed = await taskRepo.GetGroupByIdAsync(state.GroupId, default);
            if (refreshed is null)
            {
                _logger.LogError("[Stage53B] DevPlanStage：appeal 後 Group={Id} 消失，fallback", state.GroupId);
                await context.SendMessageAsync(new PipelineFallbackBridge(state.GroupId, "group_not_found", result));
                return;
            }

            // Status NeedsIntervention/Failed → intervention 結束（appeal 內部走完仍失敗）
            if (refreshed.Status == TaskStatus.NeedsIntervention || refreshed.Status == TaskStatus.Failed)
            {
                _logger.LogInformation("[Stage53B] DevPlanStage：appeal 後 status={Status} → SetInterventionAndYieldAsync（Group={Id}）",
                    refreshed.Status, state.GroupId);
                await SetInterventionAndYieldAsync(context, state.GroupId,
                    refreshed.InterventionReason ?? "Dev_plan appeal escalate", result);
                return;
            }

            // shouldContinueDev=true（Petra approve plan）→ DevStageBridge
            if (shouldContinueDev)
            {
                _logger.LogInformation("[Stage53B] DevPlanStage：appeal Petra approve → DevStageBridge（Group={Id}）", state.GroupId);
                state.DevPlanDone = true;
                state.LastAgentResult = result;
                state.LastAgentName = "Dev_plan";
                await PipelineStateHelpers.SaveAsync(context, state);
                await context.SendMessageAsync(new DevStageBridge(state.GroupId));
                return;
            }

            // shouldContinueDev=false but status normal → revise+stillFailed retry：DevPlanRevision 已 increment
            // ⚠️ 議題 F-1：HandleDevPlanCompletedAsync 內 fire Dev_plan 已 skip → Pipeline 自 SendMessage(DevPlanRetryBridge) 重跑
            _logger.LogInformation("[Stage53B] DevPlanStage：appeal revise+stillFailed retry → DevPlanRetryBridge 重跑（Group={Id}, DevPlanRevision={N}）",
                state.GroupId, refreshed.DevPlanRevision);
            state.LastAgentResult = result;
            await PipelineStateHelpers.SaveAsync(context, state);
            await context.SendMessageAsync(new DevPlanRetryBridge(state.GroupId));
            return;
        }

        // happy path：Dev_plan passed → DevStageBridge
        state.DevPlanDone = true;
        state.LastAgentResult = result;
        state.LastAgentName = "Dev_plan";
        await PipelineStateHelpers.SaveAsync(context, state);
        _logger.LogInformation("[Stage53A] DevPlanStage：passed → DevStageBridge（Group={Id}）", state.GroupId);
        await context.SendMessageAsync(new DevStageBridge(state.GroupId));
    }

    /// <summary>Stage 53B：intervention 統一 helper（appeal escalate / 內部 status 變化）。</summary>
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
