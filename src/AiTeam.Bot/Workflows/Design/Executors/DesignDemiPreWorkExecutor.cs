using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Design.Executors;

/// <summary>
/// Stage 52：前置作業 — Demi UI 規格 Executor（條件式 short-circuit，v4 漸進遷移第四步）。
///
/// 職責（spike F1 拍板：Executor 內 short-circuit pass-through）：
///   - 接 DesignPreWorkBridge phase="after_rosa"
///   - state.NeedsDemi=true → 動態建立 DemiSessionId + 跑 Claude Code call 產 UiSpec → 寫進 state.UiSpecContent + append meeting log
///   - state.NeedsDemi=false → short-circuit pass-through：DemiSessionId 維持 null + state.UiSpecContent 維持 null +
///                              meeting log 加「### Demi — 此任務不需要 UI 設計」段（建議補強 1，對齊 legacy line 159-160）
///   - SendMessageAsync(DesignPreWorkBridge phase="after_demi") 推進 RoundStart
///
/// short-circuit 不踩 framework type validation（送同一 record type，行為等同 legacy 跳 Demi await pattern）。
/// </summary>
[SendsMessage(typeof(DesignPreWorkBridge))]
internal sealed partial class DesignDemiPreWorkExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DesignDemiPreWorkExecutor> _logger;

    public DesignDemiPreWorkExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DesignDemiPreWorkExecutor> logger)
        : base("Design-DemiPreWork")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleAsync(DesignPreWorkBridge bridge, IWorkflowContext context)
    {
        var state = await DesignStateHelpers.ReadAsync(context);

        if (!state.NeedsDemi)
        {
            // F1 short-circuit：DemiSessionId 維持 null，meeting log 加「跳過」標記（對齊 legacy line 159-160）
            state.MeetingLog += "### Demi — 此任務不需要 UI 設計\n\n";
            await DesignStateHelpers.SaveAsync(context, state);

            _logger.LogInformation(
                "[Stage52] Demi pre-work short-circuit pass-through（GroupId={Id}，needsDemi=false）",
                state.GroupId);

            await context.SendMessageAsync(new DesignPreWorkBridge(state.GroupId, "after_demi"));
            return;
        }

        // 動態建立 DemiSessionId（首次 isFirstMessage=true）
        state.DemiSessionId = Guid.NewGuid().ToString();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp       = scope.ServiceProvider;
        var commons  = sp.GetRequiredService<MeetingCommons>();
        var tokenLog = sp.GetRequiredService<TokenLogService>();
        var config   = sp.GetRequiredService<IConfiguration>();

        var apiKey = config["AITEAM_ANTHROPIC_KEY"] ?? config["Anthropic:ApiKey"] ?? "";
        var model  = config["Agents:Designer:Model"] ?? config["Anthropic:DefaultModel"] ?? "claude-sonnet-4-6";

        var output = await commons.RunAgentTurnAsync(
            "Demi", state.DemiSessionId,
            DesignPrompts.BuildDesignDemiPreWorkPrompt(state.TaskPlan, state.IssuesJson),
            model, apiKey,
            isFirstMessage: true,
            workingDir: state.WorkingDir,
            allowedTools: MeetingCommons.ReadOnlyTools,
            ct: default,
            maxTurns: 25,
            meetingType: "Design",
            round: 0,
            tokenLogService: tokenLog);

        state.UiSpecContent = output;
        state.MeetingLog +=
            "### Demi — UI/UX 規格\n" + output + "\n\n";
        await DesignStateHelpers.SaveAsync(context, state);

        _logger.LogInformation(
            "[Stage52] Demi pre-work 完成（GroupId={Id}，sessionId={SessionId}）",
            state.GroupId, state.DemiSessionId);

        await context.SendMessageAsync(new DesignPreWorkBridge(state.GroupId, "after_demi"));
    }
}
