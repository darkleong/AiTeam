using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Design.Executors;

/// <summary>
/// Stage 52：Design Workflow escalate 終結 Executor（Petra 判斷需上呈老闆裁決，v4 漸進遷移第四步）。
///
/// 觸發條件（DesignWorkflowFactory.AddSwitch）：
///   - decision == "escalate"（Petra round 直接判 escalate）
///   - 議題 6 邊界：DesignAdjustmentExecutor needs_meeting 路徑 state.Round >= MaxRounds 時送 escalate verdict 也走此 Executor
///
/// 職責：
///   - 不產出 DesignPlan（沿用 legacy DesignMeetingService.cs:263-268 行為）
///   - YieldOutputAsync DesignLoopResult：Decision = "escalate"，EscalateReason = verdict.EscalateReason ?? Summary
///   - 後續由 FrameworkDesignRouter 接 result 開 BossInteraction 給 Christ 裁決（C2 拍板：BossInteraction 沿用既有手刻 path）
/// </summary>
[YieldsOutput(typeof(DesignLoopResult))]
internal sealed partial class DesignEscalateExecutor : Executor<DesignPetraVerdict>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DesignEscalateExecutor> _logger;

    public DesignEscalateExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DesignEscalateExecutor> logger)
        : base("Design-Escalate")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask HandleAsync(
        DesignPetraVerdict verdict, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = await DesignStateHelpers.ReadAsync(context);

        var result = new DesignLoopResult
        {
            Decision       = "escalate",
            MeetingLog     = state.MeetingLog,
            DesignPlan     = null,
            IssuesJson     = state.IssuesJson,
            IssueUrls      = state.IssueUrls,
            UiSpecContent  = state.UiSpecContent,
            TotalRounds    = verdict.Round,
            PetraSessionId = state.PetraSessionId,
            EscalateReason = verdict.EscalateReason ?? verdict.Summary,
        };

        _logger.LogWarning(
            "[Stage52] Escalate executor 完成（rounds={Rounds}，reason={Reason}，GroupId={Id}）",
            result.TotalRounds, result.EscalateReason, state.GroupId);

        await context.YieldOutputAsync(result);
    }
}
