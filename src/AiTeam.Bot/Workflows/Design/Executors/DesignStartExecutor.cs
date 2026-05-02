using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Design.Executors;

/// <summary>
/// Stage 52：Design Workflow 起點 Executor（v4 漸進遷移第四步）。
///
/// 職責：
///   - 接 router 傳入的 initial DesignState（front-of-queue StartExecutor）
///   - SaveAsync 寫進 framework state
///   - SendMessageAsync(DesignPreWorkBridge phase="initial") 推進前置段（PetraJudge）
///
/// 為什麼用顯式 SendsMessage：framework state 寫完後送 bridge record 給下游，需 [SendsMessage(typeof(DesignPreWorkBridge))]
/// 對齊 Stage 50 KickoffStartExecutor 三件套（Stage 50 踩坑 #10）。
/// </summary>
[SendsMessage(typeof(DesignPreWorkBridge))]
internal sealed partial class DesignStartExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DesignStartExecutor> _logger;

    public DesignStartExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DesignStartExecutor> logger)
        : base("Design-Start")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleInitialAsync(DesignState initialState, IWorkflowContext context)
    {
        await DesignStateHelpers.SaveAsync(context, initialState);
        _logger.LogInformation("[Stage52] Design Workflow 啟動（GroupId={Id}，maxRounds={Max}）",
            initialState.GroupId, initialState.MaxRounds);
        await context.SendMessageAsync(new DesignPreWorkBridge(initialState.GroupId, "initial"));
    }
}
