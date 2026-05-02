using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Kickoff.Executors;

/// <summary>
/// Stage 50：Kickoff Workflow escalate 終結 Executor（Petra 判斷需上呈老闆裁決）。
///
/// 觸發條件（KickoffWorkflowFactory.AddSwitch）：decision == "escalate"
///
/// 職責：
///   - 不產出 TaskPlan（沿用 legacy KickoffMeetingService.RunKickoffMeetingAsync line 161-166 行為，escalate 直接 break loop 不跑 BuildPetraPlanPrompt）
///   - YieldOutputAsync KickoffLoopResult：Decision = "escalate"，EscalateReason = verdict.Summary
///   - 後續由 FrameworkKickoffRouter 接 result 開 BossInteraction 給 Christ 裁決（C2 拍板：BossInteraction 沿用既有手刻 path，Stage 51 才動 framework Human-in-the-Loop）
/// </summary>
[YieldsOutput(typeof(KickoffLoopResult))]
internal sealed partial class KickoffEscalateExecutor : Executor<KickoffPetraVerdict>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KickoffEscalateExecutor> _logger;

    public KickoffEscalateExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<KickoffEscalateExecutor> logger)
        : base("Kickoff-Escalate")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask HandleAsync(
        KickoffPetraVerdict verdict, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = await KickoffStateHelpers.ReadAsync(context);

        var result = new KickoffLoopResult
        {
            Decision       = "escalate",
            MeetingLog     = state.MeetingLog,
            TaskPlan       = "",
            TotalRounds    = verdict.Round,
            EscalateReason = verdict.Summary,
        };

        _logger.LogWarning("[Stage50] Escalate executor 完成（rounds={Rounds}，reason={Reason}，GroupId={Id}）",
            result.TotalRounds, result.EscalateReason, state.GroupId);

        await context.YieldOutputAsync(result);
    }
}
