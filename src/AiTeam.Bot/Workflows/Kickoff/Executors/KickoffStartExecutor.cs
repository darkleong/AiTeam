using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Kickoff.Executors;

/// <summary>
/// Stage 50：Kickoff Workflow 起點 + loop back 起點 Executor。
///
/// 職責：
///   - 第 1 輪：接 router 傳入的 KickoffState first input → SaveAsync 寫進 framework state → SendMessageAsync 觸發 fan-out 4 Agent
///   - 第 N 輪 loop back：接 KickoffPetraVerdict（needs_discussion 路徑） → state.Round += 1 + state.LastPetraOutput = verdict.PetraOutput → SaveAsync → 再 SendMessageAsync
///
/// 多型 input 設計（對齊 Stage 49 CodyReviewAppealExecutor 雙 [MessageHandler] 模式）：
///   - HandleInitialAsync(KickoffState)
///   - HandleNextRoundAsync(KickoffPetraVerdict)
///   注意 ValueTask 簽名不含 CancellationToken（spike Phase 3 揭露 source generator 嚴格簽名要求）
/// </summary>
[SendsMessage(typeof(KickoffState))]
internal sealed partial class KickoffStartExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KickoffStartExecutor> _logger;

    public KickoffStartExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<KickoffStartExecutor> logger)
        : base("Kickoff-Start")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask BroadcastInitialAsync(KickoffState initialState, IWorkflowContext context)
    {
        await KickoffStateHelpers.SaveAsync(context, initialState);
        _logger.LogInformation("[Stage50] Kickoff Workflow 啟動（GroupId={Id}，maxRounds={Max}）",
            initialState.GroupId, initialState.MaxRounds);
        await context.SendMessageAsync(initialState);
    }

    [MessageHandler]
    private async ValueTask BroadcastNextRoundAsync(KickoffPetraVerdict verdict, IWorkflowContext context)
    {
        var state = await KickoffStateHelpers.ReadAsync(context);
        state.Round = verdict.Round + 1;
        state.LastPetraOutput = verdict.PetraOutput;
        await KickoffStateHelpers.SaveAsync(context, state);
        _logger.LogInformation("[Stage50] Kickoff loop back round {Round}（GroupId={Id}）",
            state.Round, state.GroupId);
        await context.SendMessageAsync(state);
    }
}
