using AiTeam.Bot.Orchestration.Hitl;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Kickoff.Executors;

/// <summary>
/// Stage 51：Kickoff Workflow HITL 中途介入檢查 Executor（v4 漸進遷移第三步試點）。
///
/// 兩個 [MessageHandler] 多型輸入（對齊 Stage 50 KickoffStartExecutor 雙 handler 慣例）：
///   - HandleVerdictAsync(KickoffPetraVerdict)：接 Petra Round 結束後的 verdict
///       - decision != "needs_discussion" 或 state.MidInterruptTriggered = false → pass-through verdict 給 AddSwitch 既有 routing
///       - state.MidInterruptTriggered = true → 一次性消耗（set false）+ 標 RequestPending=true → 送 MidInterruptRequest 給 RequestPort
///   - HandleResponseAsync(MidInterruptResponseData)：接 RequestPort 回傳的 Christ response
///       - apply → state.MidInterruptResponse = response.Content（Apply 時的修改指引文字，prompt 持續注入）
///       - cancel → state.MidInterruptResponse = null（拍板「丟棄所有累積指引回到正常對話」，每次介入是獨立 cycle）
///       - 重組 verdict 送回 AddSwitch（needs_discussion + Round = state.Round，下游 KickoffStartExecutor 推進 Round + 1）
///
/// 為什麼用顯式 SendsMessage 而非 Executor&lt;TIn, TOut&gt; generic：
///   - 兩個 input message type（KickoffPetraVerdict / MidInterruptResponseData）+ HandleVerdictAsync 兩個出口型別
///     （MidInterruptRequest 進 RequestPort / KickoffPetraVerdict 進 AddSwitch）
///   - generic Executor&lt;TIn, TOut&gt; 只支援單一 input + 單一 output，無法表達；對齊 Stage 50 踩坑 #10 三件套紀律
///
/// DI factory 模式（對齊 Stage 49/50 既有 9 個 Executor）：
///   - ctor 注入 IServiceScopeFactory + ILogger&lt;X&gt;（雖當前 handler 不需 scoped service，對齊既有慣例避免未來擴充重寫 ctor）
///   - 不註冊 DI（每次 Build Workflow 新建 instance）
/// </summary>
[SendsMessage(typeof(KickoffPetraVerdict))]
[SendsMessage(typeof(MidInterruptRequest))]
internal sealed partial class MidInterruptCheckExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MidInterruptCheckExecutor> _logger;

    public MidInterruptCheckExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<MidInterruptCheckExecutor> logger)
        : base("Kickoff-MidInterruptCheck")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleVerdictAsync(KickoffPetraVerdict verdict, IWorkflowContext context)
    {
        var state = await KickoffStateHelpers.ReadAsync(context);

        // consensus / max_iter / escalate 直接 pass-through（非 needs_discussion，無下一輪可介入）
        if (verdict.Decision != "needs_discussion")
        {
            await context.SendMessageAsync(verdict);
            return;
        }

        // trigger 來源：in-memory KickoffMidInterruptTriggerStore（Singleton，跨 scope 共用）
        // 透過 IServiceScopeFactory 在 Executor 內取 scoped service（對齊 Stage 49/50 既有 DI factory pattern）
        await using var scope = _scopeFactory.CreateAsyncScope();
        var triggerStore = scope.ServiceProvider.GetRequiredService<KickoffMidInterruptTriggerStore>();
        var triggered = triggerStore.TryConsume(state.GroupId);

        // 無 trigger：原樣轉發給 AddSwitch
        if (!triggered)
        {
            await context.SendMessageAsync(verdict);
            return;
        }

        // trigger 已被原子消耗 → 標 pending + emit MidInterruptRequest（透過 RequestPort）
        state.MidInterruptRequestPending = true;
        await KickoffStateHelpers.SaveAsync(context, state);

        var request = new MidInterruptRequest(state.GroupId, verdict.Round, verdict.Summary);
        _logger.LogInformation(
            "[Stage51] MidInterruptCheck emit MidInterruptRequest（GroupId={Id}，Round={Round}）",
            state.GroupId, verdict.Round);
        await context.SendMessageAsync(request);
    }

    [MessageHandler]
    private async ValueTask HandleResponseAsync(MidInterruptResponseData response, IWorkflowContext context)
    {
        var state = await KickoffStateHelpers.ReadAsync(context);

        state.MidInterruptRequestPending = false;
        // 拍板（議題 #4 + Aria 二次檢查 #12）：
        //   - Apply：寫入新指引（prompt 持續注入給後續輪次的 4 Agent + Petra）
        //   - Cancel：清回 null（每次介入是獨立 trigger-response cycle，cancel = 丟棄所有累積指引回到正常對話）
        // 對齊 KickoffMeetingService.ModifyTaskPlanAsync「Christ 修改後 Petra 永遠記得」精神（apply 部分），
        // 但讓 cancel 邊界對 Christ 直觀（每次介入獨立決定）。
        state.MidInterruptResponse = response.Apply ? response.Content : null;
        await KickoffStateHelpers.SaveAsync(context, state);

        _logger.LogInformation(
            "[Stage51] MidInterruptCheck 收到 Christ response（GroupId={Id}，apply={Apply}）",
            state.GroupId, response.Apply);

        // 重組 verdict 送回 AddSwitch（needs_discussion 走 loop back → KickoffStartExecutor）
        // ⚠️ Round 必修（Aria 二次檢查 #1）：用 state.Round（保持 N），讓下游 KickoffStartExecutor.BroadcastNextRoundAsync
        // 推進 state.Round = verdict.Round + 1 = N + 1。若用 state.Round - 1 會造成 round 重複。
        var verdict = new KickoffPetraVerdict
        {
            Decision    = "needs_discussion",
            Summary     = response.Apply ? $"中途介入：{response.Content}" : "中途介入取消",
            PetraOutput = state.LastPetraOutput ?? "",
            Round       = state.Round,
            MaxRounds   = state.MaxRounds,
        };
        await context.SendMessageAsync(verdict);
    }
}
