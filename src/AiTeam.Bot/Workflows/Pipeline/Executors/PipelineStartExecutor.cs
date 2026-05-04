using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Pipeline.Executors;

/// <summary>
/// Stage 53A：Pipeline Workflow 起點 Executor（v4 漸進遷移第五步 macro-orchestration）。
///
/// Stage 55A：兩入口路由（議題 G3 解法 + 缺口 2 sub-task 整合）：
///   - parent group（state.IsSubTask == false）→ SendMessage(KickoffStageBridge) — Pipeline 從 Kickoff 階段啟動
///   - sub-task （state.IsSubTask == true）  → SendMessage(DevPlanStageBridge) — skip Kickoff/Design（Stage 46 業務語義保留）
///
/// 職責：
///   - 接 router 傳入的 PipelineStartBridge first input
///   - 讀 PipelineState（router 已寫 IsSubTask）→ 路由判斷
///   - SaveAsync 寫進 framework state（CurrentStage = "Kickoff" / "Dev_plan"）
///   - SendMessageAsync 觸發第一 stage
///
/// 對齊 Stage 50 KickoffStartExecutor 慣例（顯式 [SendsMessage] 三件套）。
/// </summary>
[SendsMessage(typeof(KickoffStageBridge))]
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
        // 讀既有 state（首次啟動為新實例）+ 從 bridge 讀 IsSubTask
        var state = await PipelineStateHelpers.ReadAsync(context);
        state.GroupId = bridge.GroupId;
        state.IsSubTask = bridge.IsSubTask;  // Stage 55A 缺口 2：sub-task 標記由 router HandlePipelineAsync 帶入
        if (string.IsNullOrEmpty(state.WorkflowType))
            state.WorkflowType = "new_feature";

        if (state.IsSubTask)
        {
            // Stage 55A 缺口 2：sub-task 從 Dev_plan 階段啟動（Stage 46 業務語義保留）
            state.CurrentStage = "Dev_plan";
            await PipelineStateHelpers.SaveAsync(context, state);
            _logger.LogInformation(
                "[Stage55A] Pipeline Workflow 啟動（sub-task）— skip Kickoff/Design 直接進 DevPlanStage（GroupId={Id}）",
                bridge.GroupId);
            await context.SendMessageAsync(new DevPlanStageBridge(bridge.GroupId));
        }
        else
        {
            // Stage 55A 議題 G3 解法：parent group 從 Kickoff 階段啟動
            state.CurrentStage = "Kickoff";
            await PipelineStateHelpers.SaveAsync(context, state);
            _logger.LogInformation(
                "[Stage55A] Pipeline Workflow 啟動（parent group）— 從 Kickoff 階段啟動（GroupId={Id}）",
                bridge.GroupId);
            await context.SendMessageAsync(new KickoffStageBridge(bridge.GroupId));
        }
    }
}
