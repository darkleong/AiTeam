using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Pipeline.Executors;

/// <summary>
/// Stage 53A：Pipeline Workflow 起點 Executor（v4 漸進遷移第五步 macro-orchestration）。
///
/// 職責：
///   - 接 router 傳入的 PipelineStartBridge first input
///   - 初始化 PipelineState（CurrentStage="Dev_plan"，Aria 方案 C 拍板：53A 範圍從 Dev_plan 階段啟動）
///   - SaveAsync 寫進 framework state
///   - SendMessageAsync(DevPlanStageBridge) 觸發第一 stage
///
/// 對齊 Stage 50 KickoffStartExecutor 慣例（單一 [SendsMessage]）。
///
/// 為什麼用顯式 SendsMessage 而非 Executor&lt;TIn, TOut&gt; generic：
///   - input PipelineStartBridge / output DevPlanStageBridge 是不同型別
///   - 顯式三件套對齊 Stage 50 踩坑 #10 紀律
/// </summary>
[SendsMessage(typeof(DevPlanStageBridge))]
internal sealed partial class PipelineStartExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PipelineStartExecutor> _logger;

    public PipelineStartExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<PipelineStartExecutor> logger)
        : base("Pipeline-Start")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleStartAsync(PipelineStartBridge bridge, IWorkflowContext context)
    {
        var state = new PipelineState
        {
            GroupId = bridge.GroupId,
            WorkflowType = "new_feature",
            CurrentStage = "Dev_plan",
        };
        await PipelineStateHelpers.SaveAsync(context, state);
        _logger.LogInformation(
            "[Stage53A] Pipeline Workflow 啟動（GroupId={Id}，從 Dev_plan 階段啟動，Kickoff/Design 留 legacy）",
            bridge.GroupId);
        await context.SendMessageAsync(new DevPlanStageBridge(bridge.GroupId));
    }
}
