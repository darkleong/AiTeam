using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Design.Executors;

/// <summary>
/// Stage 52：Design Meeting 4 Agent 通用 Executor（Rosa/Demi/Cody/Quinn，v4 漸進遷移第四步）。
///
/// 職責（對齊 Stage 50 KickoffAgentExecutor pattern）：
///   - 接 DesignState（fan-out broadcast）
///   - 依 agentKey 取對應 prompt（DesignPrompts）+ sessionId（state 內 4 個 sessionId）
///   - call MeetingCommons.RunAgentTurnAsync（Meeting-Design 群組計法）
///   - 回傳 DesignAgentOutput（含 agentKey 標記，供 DesignAggregator fan-in 區分來源）
///
/// Demi 條件式（spike F1 拍板）：
///   - state.DemiSessionId is null 時 short-circuit pass-through —
///     直接送 DesignAgentOutput { AgentKey="Demi", Output="" }，不跑 LLM call；
///     barrier 仍滿足 4 個收齊（DesignAggregator 不需特殊改）。
///   - 對齊 legacy DesignMeetingService line 187-204 條件式 await pattern。
///
/// 設計（對齊 Stage 49/50 既有慣例）：
///   - 不註冊 DI（factory 模式 new 一次 instance per Workflow）
///   - 透過 IServiceScopeFactory 在 HandleAsync 內取 scoped services
/// </summary>
internal sealed class DesignAgentExecutor : Executor<DesignState, DesignAgentOutput>
{
    private readonly string _agentKey;
    private readonly string _configKey;
    private readonly string[]? _allowedTools;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    public DesignAgentExecutor(
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
        _logger = loggerFactory.CreateLogger($"DesignAgent.{agentKey}");
    }

    public override async ValueTask<DesignAgentOutput> HandleAsync(
        DesignState state, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // Demi short-circuit pass-through（spike F1 拍板）：
        // DemiSessionId is null 時不跑 LLM call，直接回 empty output；barrier 仍滿足 4 個收齊。
        if (_agentKey == "Demi" && state.DemiSessionId is null)
        {
            _logger.LogInformation(
                "[Stage52] Demi short-circuit pass-through round {Round}（GroupId={Id}，DemiSessionId=null）",
                state.Round, state.GroupId);
            return new DesignAgentOutput("Demi", "", state.Round);
        }

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
            "Rosa"  => DesignPrompts.BuildDesignRosaMeetingPrompt(state.IssuesJson, state.Round, state.LastPetraOutput),
            "Demi"  => DesignPrompts.BuildDesignDemiMeetingPrompt(state.UiSpecContent ?? "", state.Round, state.LastPetraOutput),
            "Cody"  => DesignPrompts.BuildDesignCodyPrompt(state.IssuesJson, state.UiSpecContent, state.Round, state.LastPetraOutput),
            "Quinn" => DesignPrompts.BuildDesignQuinnPrompt(state.IssuesJson, state.UiSpecContent, state.Round, state.LastPetraOutput),
            _ => throw new InvalidOperationException($"[DesignAgentExecutor] unknown agentKey {_agentKey}"),
        };
        var sessionId = _agentKey switch
        {
            "Rosa"  => state.RosaSessionId,
            "Demi"  => state.DemiSessionId!,         // null 已在開頭 short-circuit
            "Cody"  => state.CodySessionId,
            "Quinn" => state.QuinnSessionId,
            _ => throw new InvalidOperationException(),
        };

        // Rosa/Demi resume（前置作業已建 session），Cody/Quinn 第 1 輪 isFirstMessage:true，後續 false
        var isFirstMessage = _agentKey switch
        {
            "Rosa" => false,                      // resume 前置作業 session
            "Demi" => false,                      // resume 前置作業 session
            "Cody" or "Quinn" => state.Round == 1,
            _ => false,
        };

        var output = await commons.RunAgentTurnAsync(
            _agentKey, sessionId, prompt, model, apiKey,
            isFirstMessage: isFirstMessage,
            workingDir: state.WorkingDir,
            allowedTools: _allowedTools,
            ct: cancellationToken,
            meetingType: "Design",
            round: state.Round,
            tokenLogService: tokenLog);

        _logger.LogInformation("[Stage52] {Agent} round {Round} 完成（GroupId={Id}，sessionId={SessionId}）",
            _agentKey, state.Round, state.GroupId, sessionId);

        return new DesignAgentOutput(_agentKey, output, state.Round);
    }
}
