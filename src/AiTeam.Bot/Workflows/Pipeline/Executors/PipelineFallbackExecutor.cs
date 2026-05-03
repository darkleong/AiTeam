using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Pipeline.Executors;

/// <summary>
/// Stage 53A：Pipeline fallback 終結 Executor。
///
/// 職責：
///   - 接收 PipelineFallbackBridge（5 fallback 點 + 4 邊界 reason 觸發）
///   - YieldOutputAsync(PipelineLoopResult Completed=false + FallbackReason + LastResult)
///   - router 收到 PipelineLoopResult Completed=false 後 call FinalizePipelineAsync 主動 call legacy method 接管（議題 9 修法）
///
/// fallback reason 對照（議題 9 + 邊界擴充）：
///   - dev_plan_failed_escalate → call AppealOrchestrationService.HandleDevPlanCompletedAsync
///   - dev_blocker → call AppealOrchestrationService.HandleDevBlockerAsync
///   - dev_failed → call NotifyBossDevFailedInterventionAsync（含意 fix loop 失敗 / Dev 失敗 needs_intervention）
///   - reviewer_critical → 模擬 legacy WorkflowEngine Reviewer fail：FixIteration++ + FireStepsAsync(Dev, IsFixLoop:true)
///   - qa_fix_loop → 已 fire Dev_fix（HandleQaCompletedAsync 內），fallback 純清 marker（自然走 legacy）
///   - qa_failed / qa_intervention → call NotifyBoss intervention（具體機制 router 內處理）
///   - doc_failed → 通用 intervention notify
///   - group_not_found / arbitration_skip_reviewer → 邊界 / Stage 53B 範圍預留
/// </summary>
[YieldsOutput(typeof(PipelineLoopResult))]
internal sealed partial class PipelineFallbackExecutor : Executor<PipelineFallbackBridge>
{
    private readonly ILogger<PipelineFallbackExecutor> _logger;

    public PipelineFallbackExecutor(ILogger<PipelineFallbackExecutor> logger)
        : base("Pipeline-Fallback")
    {
        _logger = logger;
    }

    public override async ValueTask HandleAsync(
        PipelineFallbackBridge bridge, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Stage53A] PipelineFallback：fallback 觸發 → YieldOutput Completed=false（Group={Id}, reason={Reason}）",
            bridge.GroupId, bridge.Reason);
        await context.YieldOutputAsync(new PipelineLoopResult
        {
            GroupId = bridge.GroupId,
            Completed = false,
            FallbackReason = bridge.Reason,
            LastResult = bridge.LastResult,
        });
    }
}
