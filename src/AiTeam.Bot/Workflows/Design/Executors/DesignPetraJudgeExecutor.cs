using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Design.Executors;

/// <summary>
/// Stage 52：前置作業 — Petra needsDemi 判斷 Executor（v4 漸進遷移第四步）。
///
/// 職責：
///   - 接 DesignPreWorkBridge phase="initial"
///   - call MeetingCommons.RunAgentTurnAsync 跑 Petra judge prompt（isFirstMessage:true，新 sessionId）
///   - 解析 needs_demi → 寫進 state.NeedsDemi（影響條件式 fan-out 拓撲）+ append meeting log
///   - SendMessageAsync(DesignPreWorkBridge phase="after_judge") 推進 RosaPreWork
///
/// 對齊 Stage 50 KickoffPetraExecutor 三件套（partial class + [SendsMessage] + 註解）。
/// </summary>
[SendsMessage(typeof(DesignPreWorkBridge))]
internal sealed partial class DesignPetraJudgeExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DesignPetraJudgeExecutor> _logger;

    public DesignPetraJudgeExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DesignPetraJudgeExecutor> logger)
        : base("Design-PetraJudge")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleAsync(DesignPreWorkBridge bridge, IWorkflowContext context)
    {
        var state = await DesignStateHelpers.ReadAsync(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp       = scope.ServiceProvider;
        var commons  = sp.GetRequiredService<MeetingCommons>();
        var tokenLog = sp.GetRequiredService<TokenLogService>();
        var config   = sp.GetRequiredService<IConfiguration>();

        var apiKey = config["AITEAM_ANTHROPIC_KEY"] ?? config["Anthropic:ApiKey"] ?? "";
        var model  = config["Agents:PM:Model"] ?? config["Anthropic:DefaultModel"] ?? "claude-sonnet-4-6";

        var output = await commons.RunAgentTurnAsync(
            "Petra", state.PetraSessionId,
            DesignPrompts.BuildDesignPetraJudgePrompt(state.TaskPlan),
            model, apiKey,
            isFirstMessage: true,
            workingDir: state.WorkingDir,
            allowedTools: MeetingCommons.ReadOnlyTools,
            ct: default,
            meetingType: "Design",
            round: 0,                  // 前置作業階段，DesignRound 為 0
            tokenLogService: tokenLog);

        var needsDemi = DesignPrompts.TryParseNeedsDemi(output);
        state.NeedsDemi = needsDemi;
        state.MeetingLog +=
            "## 前置作業\n\n" +
            "### Petra — 設計需求判斷\n" + output + "\n\n";
        await DesignStateHelpers.SaveAsync(context, state);

        _logger.LogInformation(
            "[Stage52] Petra judge 完成（GroupId={Id}，needsDemi={NeedsDemi}）",
            state.GroupId, needsDemi);

        await context.SendMessageAsync(new DesignPreWorkBridge(state.GroupId, "after_judge"));
    }
}
