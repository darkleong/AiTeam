using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Design.Executors;

/// <summary>
/// Stage 52：Design Meeting Petra 整理 + 判斷 Executor（v4 漸進遷移第四步）。
///
/// 職責（對齊 Stage 50 KickoffPetraExecutor pattern）：
///   - 接 DesignRoundCollected（Aggregator 4 Agent 收齊後送來）
///   - call MeetingCommons.RunAgentTurnAsync 跑 Petra round prompt（resume PetraSessionId，isFirstMessage=false 從 round 1 起就接續 PetraJudge session）
///   - append Petra log 到 state.MeetingLog + SaveAsync state.LastPetraOutput
///   - 解析 decision JSON（DesignPrompts.TryParseDesignPetraDecision，失敗時假設 consensus 對齊 legacy）
///   - 回傳 DesignPetraVerdict — 5 分支共用同 verdict 型別（Aria 實作期提醒 #2：consensus / needs_discussion / needs_adjustment / escalate / max_iter 衍生），
///     escalate / needs_adjustment 路徑 EscalateReason / AdjustmentTargets / AdjustmentInstructions 一併帶進
///
/// session：state.PetraSessionId 跨 PetraJudge / Petra round / Petra plan / Petra adjustment eval / split proposal 全程 resume。
/// </summary>
internal sealed class DesignPetraExecutor : Executor<DesignRoundCollected, DesignPetraVerdict>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DesignPetraExecutor> _logger;

    public DesignPetraExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DesignPetraExecutor> logger)
        : base("Design-Petra")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask<DesignPetraVerdict> HandleAsync(
        DesignRoundCollected collected, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = await DesignStateHelpers.ReadAsync(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp       = scope.ServiceProvider;
        var commons  = sp.GetRequiredService<MeetingCommons>();
        var tokenLog = sp.GetRequiredService<TokenLogService>();
        var config   = sp.GetRequiredService<IConfiguration>();

        var apiKey = config["AITEAM_ANTHROPIC_KEY"] ?? config["Anthropic:ApiKey"] ?? "";
        var model  = config["Agents:PM:Model"] ?? config["Anthropic:DefaultModel"] ?? "claude-sonnet-4-6";
        var prompt = DesignPrompts.BuildDesignPetraRoundPrompt(
            collected.Rosa, collected.Demi, collected.Cody, collected.Quinn,
            collected.Round, hasDemi: state.DemiSessionId is not null);

        // PetraSessionId 已在 PetraJudge 階段建立（前置作業 isFirstMessage=true），主迴圈一律 resume
        var petraOutput = await commons.RunAgentTurnAsync(
            "Petra", state.PetraSessionId, prompt, model, apiKey,
            isFirstMessage: false,
            workingDir: state.WorkingDir,
            allowedTools: MeetingCommons.ReadOnlyTools,
            ct: cancellationToken,
            meetingType: "Design",
            round: collected.Round,
            tokenLogService: tokenLog);

        state.MeetingLog += $"### Petra（綜合整理）\n{petraOutput}\n\n";
        state.LastPetraOutput = petraOutput;
        await DesignStateHelpers.SaveAsync(context, state);

        // 解析 decision（失敗時假設 consensus，對齊 legacy line 244-249 fallback 行為）
        var decision = DesignPrompts.TryParseDesignPetraDecision(petraOutput);
        var decisionStr = decision?.Decision ?? "consensus";

        _logger.LogInformation("[Stage52] Petra round {Round} decision={Decision}（GroupId={Id}）",
            collected.Round, decisionStr, state.GroupId);

        return new DesignPetraVerdict
        {
            Decision    = decisionStr,
            Summary     = decision?.Summary ?? "",
            PetraOutput = petraOutput,
            Round       = collected.Round,
            MaxRounds   = state.MaxRounds,
            AdjustmentTargets      = decision?.AdjustmentTargets      ?? [],
            AdjustmentInstructions = decision?.AdjustmentInstructions ?? new Dictionary<string, string>(),
            EscalateReason         = decision?.EscalateReason,
        };
    }
}
