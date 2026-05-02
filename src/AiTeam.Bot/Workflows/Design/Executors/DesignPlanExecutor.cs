using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Design.Executors;

/// <summary>
/// Stage 52：Design Workflow consensus / max_iter / adjustment_approved 三入口共用 Executor（v4 漸進遷移第四步）。
///
/// 雙 [MessageHandler] 處理邏輯不對稱（議題 7 必修）：
///   - HandleVerdictAsync(DesignPetraVerdict)：consensus / max_iter 入口（依 verdict.Decision + Round vs MaxRounds 判別）
///       → 內部 RunAgentTurnAsync Petra session 跑 BuildDesignPetraPlanPrompt 產 plan → YieldOutputAsync(DesignLoopResult)
///   - HandleAdjustmentApprovedAsync(DesignAdjustmentApproved)：adjustment_approved 入口
///       → DesignAdjustmentApproved record 已帶 non-null DesignPlan（DesignAdjustmentExecutor 內保證）
///       → Executor 內**直接** wrap DesignLoopResult，**不再 call BuildDesignPetraPlanPrompt** 避免重複 LLM call
///
/// 兩 handler 共用 [YieldsOutput(typeof(DesignLoopResult))] 一次標即可。
///
/// 對齊 Stage 50 KickoffPlanExecutor pattern + 議題 6+7 Aria 必修。
/// </summary>
[YieldsOutput(typeof(DesignLoopResult))]
internal sealed partial class DesignPlanExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DesignPlanExecutor> _logger;

    public DesignPlanExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DesignPlanExecutor> logger)
        : base("Design-Plan")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleVerdictAsync(DesignPetraVerdict verdict, IWorkflowContext context)
    {
        var state = await DesignStateHelpers.ReadAsync(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp       = scope.ServiceProvider;
        var commons  = sp.GetRequiredService<MeetingCommons>();
        var tokenLog = sp.GetRequiredService<TokenLogService>();
        var config   = sp.GetRequiredService<IConfiguration>();

        var apiKey = config["AITEAM_ANTHROPIC_KEY"] ?? config["Anthropic:ApiKey"] ?? "";
        var model  = config["Agents:PM:Model"] ?? config["Anthropic:DefaultModel"] ?? "claude-sonnet-4-6";

        var designPlan = await commons.RunAgentTurnAsync(
            "Petra", state.PetraSessionId,
            DesignPrompts.BuildDesignPetraPlanPrompt(state.TaskPlan, state.IssuesJson, state.UiSpecContent),
            model, apiKey,
            isFirstMessage: false,        // Petra session 已建立（PetraJudge 階段 isFirstMessage=true）
            workingDir: state.WorkingDir,
            allowedTools: MeetingCommons.ReadOnlyTools,
            ct: default,
            meetingType: "Design",
            round: verdict.Round,
            tokenLogService: tokenLog);

        state.DesignPlan = designPlan;
        state.MeetingLog += $"## 設計規劃書\n{designPlan}\n\n";
        state.TotalRounds = verdict.Round;
        await DesignStateHelpers.SaveAsync(context, state);

        var isMaxIter = verdict.Decision == "needs_discussion" && verdict.Round >= verdict.MaxRounds;
        var result = new DesignLoopResult
        {
            Decision       = isMaxIter ? "max_iter" : "consensus",
            MeetingLog     = state.MeetingLog,
            DesignPlan     = designPlan,
            IssuesJson     = state.IssuesJson,
            IssueUrls      = state.IssueUrls,
            UiSpecContent  = state.UiSpecContent,
            TotalRounds    = verdict.Round,
            PetraSessionId = state.PetraSessionId,
        };

        _logger.LogInformation(
            "[Stage52] Plan executor 完成（decision={Decision}，rounds={Rounds}，GroupId={Id}）",
            result.Decision, result.TotalRounds, state.GroupId);

        await context.YieldOutputAsync(result);
    }

    [MessageHandler]
    private async ValueTask HandleAdjustmentApprovedAsync(DesignAdjustmentApproved approved, IWorkflowContext context)
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
            "[Stage52] Plan executor adjustment_approved 直接 wrap（GroupId={Id}，round={Round}）",
            state.GroupId, approved.Round);

        await context.YieldOutputAsync(result);
    }
}
