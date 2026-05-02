using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Design.Executors;

/// <summary>
/// Stage 52：Design Workflow consensus / max_iter 入口 Executor（v4 漸進遷移第四步）。
///
/// 設計演進（驗收期 follow-up #2 修正）：
///   - 原計畫單一 Executor 雙 [MessageHandler] 接 DesignPetraVerdict + DesignAdjustmentApproved（Aria 議題 7 必修）
///   - 驗收期實證 framework 1.3.0 AddEdge type-based dispatch 不 source-aware：
///     `AddEdge(adjust, plan)` 把 adjust needs_meeting 路徑送的 DesignPetraVerdict 也 dispatch 給 plan，
///     造成 plan 跑 LLM + 跟 adjust SaveAsync state 同 superstep 衝突（WorkflowErrorEvent: Expected exactly one update for key 'singleton'）
///   - 修法：拆 plan 成兩個 Executor，type-explicit 自然分流：
///     · DesignPlanExecutor 只接 DesignPetraVerdict（main loop consensus / max_iter 入口）
///     · DesignAdjustmentPlanExecutor 只接 DesignAdjustmentApproved（adjust approved 入口直接 wrap）
///   - framework AddEdge type filter 對 plan 沒 DesignAdjustmentApproved handler → adjust 送 DesignPetraVerdict 不會誤觸發 plan
///
/// 對齊 Stage 50 KickoffPlanExecutor pattern + 議題 6+7 Aria 必修。
/// </summary>
[YieldsOutput(typeof(DesignLoopResult))]
internal sealed partial class DesignPlanExecutor : Executor<DesignPetraVerdict>
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

    public override async ValueTask HandleAsync(
        DesignPetraVerdict verdict, IWorkflowContext context, CancellationToken cancellationToken = default)
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
            ct: cancellationToken,
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
}
