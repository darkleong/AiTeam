using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Kickoff.Executors;

/// <summary>
/// Stage 50：Kickoff Meeting 4 Agent 通用 Executor（Rosa/Demi/Cody/Quinn）。
///
/// 職責：
///   - 接 KickoffState（fan-out broadcast）
///   - 依 agentKey 取對應 prompt（KickoffPrompts）+ sessionId（state 內 4 個臨時 GUID）
///   - call MeetingCommons.RunAgentTurnAsync — 含 token Meeting-Kickoff 群組計法 + error 容錯（對齊 legacy KickoffMeetingService.RunKickoffMeetingAsync line 96-115 行為）
///   - 回傳 KickoffAgentOutput（含 agentKey 標記，供 KickoffAggregator fan-in 區分來源）
///
/// 設計（對齊 Stage 49 路線 B service-call 模式）：
///   - 不註冊 DI（factory 模式 new 一次 instance per Workflow）
///   - 透過 IServiceScopeFactory 在 HandleAsync 內取 scoped services
/// </summary>
internal sealed class KickoffAgentExecutor : Executor<KickoffState, KickoffAgentOutput>
{
    private readonly string _agentKey;
    private readonly string _configKey;
    private readonly string[]? _allowedTools;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    public KickoffAgentExecutor(
        string executorId,
        string agentKey,
        string configKey,
        string[]? allowedTools,
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory)
        : base(executorId)
    {
        _agentKey = agentKey;
        _configKey = configKey;
        _allowedTools = allowedTools;
        _scopeFactory = scopeFactory;
        _logger = loggerFactory.CreateLogger($"KickoffAgent.{agentKey}");
    }

    public override async ValueTask<KickoffAgentOutput> HandleAsync(
        KickoffState state, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp       = scope.ServiceProvider;
        var commons  = sp.GetRequiredService<MeetingCommons>();
        var tokenLog = sp.GetRequiredService<TokenLogService>();
        var config   = sp.GetRequiredService<IConfiguration>();

        var apiKey = config["AITEAM_ANTHROPIC_KEY"] ?? config["Anthropic:ApiKey"] ?? "";
        var model  = config[$"Agents:{_configKey}:Model"]
                     ?? config["Anthropic:DefaultModel"]
                     ?? "claude-sonnet-4-6";

        var prompt = _agentKey switch
        {
            "Rosa"  => KickoffPrompts.BuildRosaPrompt (state.ProposalContent, state.Round, state.LastPetraOutput),
            "Demi"  => KickoffPrompts.BuildDemiPrompt (state.ProposalContent, state.Round, state.LastPetraOutput),
            "Cody"  => KickoffPrompts.BuildCodyPrompt (state.ProposalContent, state.Round, state.LastPetraOutput),
            "Quinn" => KickoffPrompts.BuildQuinnPrompt(state.ProposalContent, state.Round, state.LastPetraOutput),
            _ => throw new InvalidOperationException($"[KickoffAgentExecutor] unknown agentKey {_agentKey}"),
        };
        var sessionId = _agentKey switch
        {
            "Rosa"  => state.RosaSessionId,
            "Demi"  => state.DemiSessionId,
            "Cody"  => state.CodySessionId,
            "Quinn" => state.QuinnSessionId,
            _ => throw new InvalidOperationException(),
        };

        var output = await commons.RunAgentTurnAsync(
            _agentKey, sessionId, prompt, model, apiKey,
            isFirstMessage: state.Round == 1,
            workingDir: state.WorkingDir,
            allowedTools: _allowedTools,
            ct: cancellationToken,
            meetingType: "Kickoff",
            round: state.Round,
            tokenLogService: tokenLog);

        _logger.LogInformation("[Stage50] {Agent} round {Round} 完成（GroupId={Id}，sessionId={SessionId}）",
            _agentKey, state.Round, state.GroupId, sessionId);

        return new KickoffAgentOutput(_agentKey, output, state.Round);
    }
}
