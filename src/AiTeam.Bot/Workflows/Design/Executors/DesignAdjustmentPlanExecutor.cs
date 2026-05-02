using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Design.Executors;

/// <summary>
/// Stage 52：Design Workflow adjustment_approved 入口收尾 Executor（v4 漸進遷移第四步，驗收期 follow-up #2 拆出）。
///
/// 設計演進（驗收期 follow-up #2 修正）：
///   - 原 DesignPlanExecutor 雙 [MessageHandler] 接 DesignPetraVerdict + DesignAdjustmentApproved（Aria 議題 7 必修）
///   - 驗收期實證 framework 1.3.0 `AddEdge(adjust, plan)` type-based dispatch 把 adjust needs_meeting 路徑送的
///     DesignPetraVerdict 也 dispatch 給 plan，造成 plan 跑 LLM + state 同 superstep 衝突
///   - 修法拆 plan：DesignPlanExecutor 只接 DesignPetraVerdict / DesignAdjustmentPlanExecutor 只接 DesignAdjustmentApproved
///   - framework AddEdge type filter 對本 Executor 沒 DesignPetraVerdict handler → 不會被 adjust needs_meeting 誤觸發
///
/// 職責（議題 7 主路徑）：
///   - 接 DesignAdjustmentApproved（DesignAdjustmentExecutor 內保證已帶 non-null DesignPlan：evalDecision.DesignPlan 直接帶 / fallback BuildDesignPetraPlanPrompt 補產）
///   - 寫 state.DesignPlan / state.MeetingLog / state.TotalRounds
///   - YieldOutputAsync DesignLoopResult — **直接 wrap，不再 call BuildDesignPetraPlanPrompt 避免重複 LLM call**（議題 7 拍板）
/// </summary>
[YieldsOutput(typeof(DesignLoopResult))]
internal sealed partial class DesignAdjustmentPlanExecutor : Executor<DesignAdjustmentApproved>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DesignAdjustmentPlanExecutor> _logger;

    public DesignAdjustmentPlanExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DesignAdjustmentPlanExecutor> logger)
        : base("Design-AdjustmentPlan")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask HandleAsync(
        DesignAdjustmentApproved approved, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = await DesignStateHelpers.ReadAsync(context);

        // approved record 已帶 non-null DesignPlan（DesignAdjustmentExecutor 內保證）→ 直接 wrap，不再 call LLM
        var meetingLog = approved.MeetingLog;
        if (!meetingLog.EndsWith("\n## 設計規劃書\n", StringComparison.Ordinal))
            meetingLog += $"## 設計規劃書\n{approved.DesignPlan}\n\n";

        state.DesignPlan = approved.DesignPlan;
        state.MeetingLog = meetingLog;
        state.TotalRounds = approved.Round;
        await DesignStateHelpers.SaveAsync(context, state);

        var result = new DesignLoopResult
        {
            Decision       = "consensus",
            MeetingLog     = meetingLog,
            DesignPlan     = approved.DesignPlan,
            IssuesJson     = state.IssuesJson,
            IssueUrls      = state.IssueUrls,
            UiSpecContent  = state.UiSpecContent,
            TotalRounds    = approved.Round,
            PetraSessionId = state.PetraSessionId,
        };

        _logger.LogInformation(
            "[Stage52] AdjustmentPlan executor 直接 wrap（GroupId={Id}，round={Round}）",
            state.GroupId, approved.Round);

        await context.YieldOutputAsync(result);
    }
}
