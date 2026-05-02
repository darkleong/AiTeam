using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Kickoff.Executors;

/// <summary>
/// Stage 50：Kickoff Meeting Petra 整理 + 判斷 Executor。
///
/// 職責：
///   - 接 KickoffRoundCollected（Aggregator 4 Agent 收齊後送來）
///   - call MeetingCommons.RunAgentTurnAsync 跑 Petra round prompt
///   - append Petra log 到 state.MeetingLog + SaveAsync state.LastPetraOutput
///   - 解析 decision JSON（KickoffPrompts.TryParsePetraDecision，失敗時假設 consensus 對齊 legacy line 152）
///   - 回傳 KickoffPetraVerdict，driving Workflow Switch routing（consensus / needs_discussion / escalate）
///
/// session：state.PetraSessionId = group.Id（C2 拍板沿用 Claude Code --resume 給 Modify 流程）
/// </summary>
internal sealed class KickoffPetraExecutor : Executor<KickoffRoundCollected, KickoffPetraVerdict>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KickoffPetraExecutor> _logger;

    public KickoffPetraExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<KickoffPetraExecutor> logger)
        : base("Kickoff-Petra")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask<KickoffPetraVerdict> HandleAsync(
        KickoffRoundCollected collected, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = await KickoffStateHelpers.ReadAsync(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp       = scope.ServiceProvider;
        var commons  = sp.GetRequiredService<MeetingCommons>();
        var tokenLog = sp.GetRequiredService<TokenLogService>();
        var config   = sp.GetRequiredService<IConfiguration>();

        var apiKey = config["AITEAM_ANTHROPIC_KEY"] ?? config["Anthropic:ApiKey"] ?? "";
        var model  = config["Agents:PM:Model"] ?? config["Anthropic:DefaultModel"] ?? "claude-sonnet-4-6";
        // Stage 51：HITL 中途介入指引也注入 Petra prompt（讓 Petra 整理時也優先考量 Christ 修改方向）
        var prompt = KickoffPrompts.BuildPetraRoundPrompt(
            collected.Rosa, collected.Demi, collected.Cody, collected.Quinn, collected.Round,
            state.MidInterruptResponse);

        var petraOutput = await commons.RunAgentTurnAsync(
            "Petra", state.PetraSessionId, prompt, model, apiKey,
            isFirstMessage: collected.Round == 1,
            workingDir: state.WorkingDir,
            allowedTools: MeetingCommons.ReadOnlyTools,
            ct: cancellationToken,
            meetingType: "Kickoff",
            round: collected.Round,
            tokenLogService: tokenLog);

        // append Petra log
        state.MeetingLog += $"### Petra（綜合整理）\n{petraOutput}\n\n";
        state.LastPetraOutput = petraOutput;
        await KickoffStateHelpers.SaveAsync(context, state);

        // 解析 decision（對齊 legacy line 152 — 解析失敗假設 consensus）
        var decision = KickoffPrompts.TryParsePetraDecision(petraOutput);
        var decisionStr = decision?.Decision ?? "consensus";

        _logger.LogInformation("[Stage50] Petra round {Round} decision={Decision}（GroupId={Id}）",
            collected.Round, decisionStr, state.GroupId);

        return new KickoffPetraVerdict
        {
            Decision    = decisionStr,
            Summary     = decision?.Summary ?? "",
            PetraOutput = petraOutput,
            Round       = collected.Round,
            MaxRounds   = state.MaxRounds,
        };
    }
}
