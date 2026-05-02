using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Design.Executors;

/// <summary>
/// Stage 52：主迴圈 round loop 起點 + loop back 起點 Executor（mainStart node，v4 漸進遷移第四步）。
///
/// 雙 [MessageHandler] 多型輸入（對齊 Stage 50 KickoffStartExecutor 雙 handler pattern）：
///   - HandleAfterPreWorkAsync(DesignPreWorkBridge phase="after_demi")：前置完成 → state.Round=1 → 推進主迴圈 fan-out
///   - HandleLoopBackAsync(DesignPetraVerdict from needs_discussion 路徑)：Petra needs_discussion → state.Round=verdict.Round+1 + lastPetraOutput → 推進下一輪 fan-out
///
/// 出口：DesignState（fan-out 4 Agent 接收）。對齊 spike F2 拍板：同一 WorkflowBuilder 內串接，state 跨 superstep 共享。
///
/// 三件套：[SendsMessage(typeof(DesignState))] + partial class + 註解（Stage 50 踩坑 #10）。
/// </summary>
[SendsMessage(typeof(DesignState))]
internal sealed partial class DesignRoundStartExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DesignRoundStartExecutor> _logger;

    public DesignRoundStartExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DesignRoundStartExecutor> logger)
        : base("Design-RoundStart")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleAfterPreWorkAsync(DesignPreWorkBridge bridge, IWorkflowContext context)
    {
        var state = await DesignStateHelpers.ReadAsync(context);
        state.Round = 1;
        state.MeetingLog += $"## Round {state.Round}\n\n";
        await DesignStateHelpers.SaveAsync(context, state);

        _logger.LogInformation("[Stage52] Design 主迴圈 Round 1 啟動（GroupId={Id}）", state.GroupId);
        await context.SendMessageAsync(state);
    }

    [MessageHandler]
    private async ValueTask HandleLoopBackAsync(DesignPetraVerdict verdict, IWorkflowContext context)
    {
        var state = await DesignStateHelpers.ReadAsync(context);
        state.Round = verdict.Round + 1;
        state.LastPetraOutput = verdict.PetraOutput;
        state.MeetingLog += $"## Round {state.Round}\n\n";
        await DesignStateHelpers.SaveAsync(context, state);

        _logger.LogInformation("[Stage52] Design 主迴圈 loop back Round {Round}（GroupId={Id}）",
            state.Round, state.GroupId);
        await context.SendMessageAsync(state);
    }
}
