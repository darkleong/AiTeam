using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Kickoff.Executors;

/// <summary>
/// Stage 50：Kickoff Workflow consensus / max_iter 終結 Executor。
///
/// 觸發條件（KickoffWorkflowFactory.AddSwitch）：
///   - decision == "consensus" → 走此 Executor 產出 TaskPlan
///   - decision == "needs_discussion" && Round >= MaxRounds → 走此 Executor 產出 TaskPlan（強制結束）
///
/// 職責：
///   - call MeetingCommons.RunAgentTurnAsync 跑 BuildPetraPlanPrompt（接續 Petra session，isFirstMessage=false）
///   - append "## 任務計劃書" 到 state.MeetingLog + state.TaskPlan = taskPlan + SaveAsync
///   - YieldOutputAsync KickoffLoopResult：consensus or max_iter
///
/// 對齊 legacy KickoffMeetingService.RunKickoffMeetingAsync line 175-192 行為。
/// </summary>
internal sealed class KickoffPlanExecutor : Executor<KickoffPetraVerdict>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KickoffPlanExecutor> _logger;

    public KickoffPlanExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<KickoffPlanExecutor> logger)
        : base("Kickoff-Plan")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask HandleAsync(
        KickoffPetraVerdict verdict, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = await KickoffStateHelpers.ReadAsync(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp       = scope.ServiceProvider;
        var commons  = sp.GetRequiredService<MeetingCommons>();
        var tokenLog = sp.GetRequiredService<TokenLogService>();
        var config   = sp.GetRequiredService<IConfiguration>();

        var apiKey = config["AITEAM_ANTHROPIC_KEY"] ?? config["Anthropic:ApiKey"] ?? "";
        var model  = config["Agents:PM:Model"] ?? config["Anthropic:DefaultModel"] ?? "claude-sonnet-4-6";

        var taskPlan = await commons.RunAgentTurnAsync(
            "Petra", state.PetraSessionId, KickoffPrompts.BuildPetraPlanPrompt(), model, apiKey,
            isFirstMessage: false,   // Petra session 已建立（Round 1 已 isFirstMessage=true）
            workingDir: state.WorkingDir,
            allowedTools: MeetingCommons.ReadOnlyTools,
            ct: cancellationToken,
            meetingType: "Kickoff",
            round: verdict.Round,
            tokenLogService: tokenLog);

        state.MeetingLog += $"## 任務計劃書\n{taskPlan}\n\n";
        state.TaskPlan = taskPlan;
        await KickoffStateHelpers.SaveAsync(context, state);

        var isMaxIter = verdict.Decision == "needs_discussion" && verdict.Round >= verdict.MaxRounds;
        var result = new KickoffLoopResult
        {
            Decision    = isMaxIter ? "max_iter" : "consensus",
            MeetingLog  = state.MeetingLog,
            TaskPlan    = taskPlan,
            TotalRounds = verdict.Round,
        };

        _logger.LogInformation("[Stage50] Plan executor 完成（decision={Decision}，rounds={Rounds}，GroupId={Id}）",
            result.Decision, result.TotalRounds, state.GroupId);

        await context.YieldOutputAsync(result);
    }
}
