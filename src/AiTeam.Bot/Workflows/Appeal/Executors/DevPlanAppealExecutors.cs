using System.Text.Json;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Appeal.Executors;

/// <summary>
/// Stage 49：Dev_plan Appeal 路徑 Cody 反駁 Executor（Cody-Petra Appeal loop 中 Cody 角色）。
///
/// 路線 B 拍板（service 包裝）：直接 call DevPlanAppealService.RunCodyDevPlanAppealAsync。
///
/// 多型 input 設計：
///   - HandleInitialAsync(string trigger) — 第 1 輪（state.InitialPetraReview 已由 router 寫入）
///   - HandleReassessAsync(DevPlanAppealRoundResult) — 第 N 輪 loop back（接 Petra 重評）
/// </summary>
internal sealed partial class CodyDevPlanAppealExecutor : Executor
{
    private static readonly JsonSerializerOptions JsonIndented = new() { WriteIndented = true };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CodyDevPlanAppealExecutor> _logger;

    public CodyDevPlanAppealExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<CodyDevPlanAppealExecutor> logger)
        : base("Cody-DevPlanAppeal")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask<CodyDevPlanAppeal> HandleInitialAsync(string trigger, IWorkflowContext context)
        => await ExecuteRoundAsync(context, isFirstRound: true, lastReassess: null);

    [MessageHandler]
    private async ValueTask<CodyDevPlanAppeal> HandleReassessAsync(DevPlanAppealRoundResult lastReassess, IWorkflowContext context)
        => await ExecuteRoundAsync(context, isFirstRound: false, lastReassess: lastReassess);

    private async Task<CodyDevPlanAppeal> ExecuteRoundAsync(
        IWorkflowContext context, bool isFirstRound, DevPlanAppealRoundResult? lastReassess)
    {
        var state = await AppealStateHelpers.ReadAsync(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var taskRepo  = sp.GetRequiredService<TaskRepository>();
        var appealSvc = sp.GetRequiredService<DevPlanAppealService>();

        var group = await taskRepo.GetGroupByIdAsync(state.GroupId, default)
            ?? throw new InvalidOperationException(
                $"[Cody-DevPlanAppeal] TaskGroup {state.GroupId} 不存在");

        if (!isFirstRound)
        {
            state.Round++;
        }

        _logger.LogInformation(
            "[FrameworkAppeal] Cody Dev_plan Round {Round}（Group={Id}）",
            state.Round, state.GroupId);

        // 取目前對 Cody 用的 Petra review（initial 用 state.InitialPetraReview，重評用 lastReassess.Petra）
        PetraReview currentReview;
        string? priorContext;

        if (isFirstRound)
        {
            var snap = state.InitialPetraReview
                ?? throw new InvalidOperationException(
                    "[Cody-DevPlanAppeal] state.InitialPetraReview 為 null（router 未正確寫入）");
            currentReview = new PetraReview(snap.Decision, snap.Summary, [], snap.RevisionInstructions);
            priorContext = $"Petra 初審意見：{snap.Summary}";
        }
        else
        {
            currentReview = lastReassess!.Petra;
            priorContext = $"（已進行 {state.Round - 1} 輪 Appeal，Petra 維持修改意見：{currentReview.Summary}）";
        }

        var codyAppeal = await appealSvc.RunCodyDevPlanAppealAsync(
            group, currentReview, priorContext, default);

        var codyJson = JsonSerializer.Serialize(codyAppeal, JsonIndented);
        state.CodyResponses.Add(codyJson);

        // Cody accept → 寫 log 並推進 round（log 寫入由 Petra Reassess Executor 統一做完整 round）
        if (codyAppeal.Position == "accept")
        {
            AppealLogHelpers.AppendDevPlanAppealLog(group, state.Round,
                $"**Cody 接受修改意見，Appeal 終止。**\n```json\n{codyJson}\n```");
            group.DevPlanAppealRoundA = state.Round;
            await taskRepo.SaveAsync(default);
        }

        await AppealStateHelpers.SaveAsync(context, state);

        return codyAppeal;
    }
}

/// <summary>
/// Stage 49：Dev_plan Appeal 路徑 Petra 重評 Executor（接 Cody 反駁後重新評估）。
///
/// 路線 B 拍板（service 包裝）：直接 call DevPlanAppealService.ReassessDevPlanAsync。
///
/// Output = DevPlanAppealRoundResult（含 Approved 派生 flag 給 framework AddSwitch routing）。
///
/// 短路：Cody Position == "accept" 時直接視同 approved，不真 call Petra（節省 LLM 成本，對齊 legacy
/// RunDevPlanAppealLoopAsync 行為 line 561-571 - Cody accept 直接 return true）。
/// </summary>
internal sealed class PetraDevPlanReassessExecutor : Executor<CodyDevPlanAppeal, DevPlanAppealRoundResult>
{
    private static readonly JsonSerializerOptions JsonIndented = new() { WriteIndented = true };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PetraDevPlanReassessExecutor> _logger;

    public PetraDevPlanReassessExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<PetraDevPlanReassessExecutor> logger)
        : base("Petra-DevPlanReassess")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask<DevPlanAppealRoundResult> HandleAsync(
        CodyDevPlanAppeal codyAppeal, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = await AppealStateHelpers.ReadAsync(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var taskRepo  = sp.GetRequiredService<TaskRepository>();
        var appealSvc = sp.GetRequiredService<DevPlanAppealService>();

        var group = await taskRepo.GetGroupByIdAsync(state.GroupId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"[Petra-DevPlanReassess] TaskGroup {state.GroupId} 不存在");

        // 取得當前對 Cody appeal 的 base review（initial 用 state.InitialPetraReview，後續用前輪 Petra 重評）
        var lastVerdict = state.VeraDecisions.Count > 0
            ? null
            : (PetraReview?)null;
        // ↑ ReviewAppeal 結構不適用此處；DevPlanAppeal 只追 Petra 系列重評，存進 state 自己的欄位
        // 簡化：用 InitialPetraReview 作為 base，重評後新 review 寫進 state.FinalVerdict / RevisionInstructions
        var snap = state.InitialPetraReview!;
        var initialReview = new PetraReview(snap.Decision, snap.Summary, [], snap.RevisionInstructions);

        // Cody accept 短路（對齊 legacy RunDevPlanAppealLoopAsync 行為）
        if (codyAppeal.Position == "accept")
        {
            _logger.LogInformation(
                "[FrameworkAppeal] Petra DevPlan Round {Round}：Cody accept，短路 approved（Group={Id}）",
                state.Round, state.GroupId);

            // legacy 也是 Cody accept 視同共識達成（return true），decision 設 approve
            var stubApprove = new PetraReview("approve", "Cody 接受意見，共識達成", [], null);
            state.FinalVerdict = "approve";
            await AppealStateHelpers.SaveAsync(context, state);

            return new DevPlanAppealRoundResult(
                Petra: stubApprove,
                LastCodyAppeal: codyAppeal,
                Round: state.Round,
                MaxRounds: state.MaxRounds,
                Approved: true);
        }

        _logger.LogInformation(
            "[FrameworkAppeal] Petra DevPlan Round {Round}：Cody disagree → Petra 重評（Group={Id}）",
            state.Round, state.GroupId);

        var newReview = await appealSvc.ReassessDevPlanAsync(
            group, codyAppeal, initialReview, cancellationToken);

        var codyJson  = JsonSerializer.Serialize(codyAppeal, JsonIndented);
        var petraJson = JsonSerializer.Serialize(newReview, JsonIndented);

        AppealLogHelpers.AppendDevPlanAppealLog(group, state.Round,
            $"**Cody 反駁（完整）：**\n```json\n{codyJson}\n```\n\n**Petra 重評（完整）：**\n```json\n{petraJson}\n```");
        group.DevPlanAppealRoundA = state.Round;
        await taskRepo.SaveAsync(cancellationToken);

        var approved = newReview.Decision == "approve";
        state.FinalVerdict = newReview.Decision;
        state.RevisionInstructions = newReview.RevisionInstructions;
        // 把 Petra 重評結果存進 state.InitialPetraReview，下一輪 Cody 用這個當 base
        state.InitialPetraReview = new PetraReviewSnapshot
        {
            Decision = newReview.Decision,
            Summary = newReview.Summary,
            RevisionInstructions = newReview.RevisionInstructions,
        };
        await AppealStateHelpers.SaveAsync(context, state);

        return new DevPlanAppealRoundResult(
            Petra: newReview,
            LastCodyAppeal: codyAppeal,
            Round: state.Round,
            MaxRounds: state.MaxRounds,
            Approved: approved);
    }
}

/// <summary>
/// Stage 49：Dev_plan Appeal max-iter / Cody accept / Petra approve 後的 final Executor。
///
/// 將 DevPlanAppealRoundResult 包成 AppealLoopResult 作為 Workflow 最終 output（framework
/// WorkflowOutputEvent 帶的 payload）。FrameworkAppealRouter 取此結果做 escalate 判斷 + 寫進 DB。
/// </summary>
internal sealed class DevPlanAppealFinalizeExecutor : Executor<DevPlanAppealRoundResult, AppealLoopResult>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DevPlanAppealFinalizeExecutor> _logger;

    public DevPlanAppealFinalizeExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DevPlanAppealFinalizeExecutor> logger)
        : base("DevPlanAppeal-Finalize")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask<AppealLoopResult> HandleAsync(
        DevPlanAppealRoundResult round, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = await AppealStateHelpers.ReadAsync(context);

        // Verdict 判斷：
        //   - Cody accept OR Petra approve → "approve"（共識達成）
        //   - Petra revise && round < max → 不應走到這裡（路由回 Cody loop）
        //   - Petra revise && round >= max → "escalate"（耗盡升級）
        var verdict = round.Approved
            ? "approve"
            : (round.Round >= round.MaxRounds ? "escalate" : "revise");

        var summary = round.Approved
            ? (round.LastCodyAppeal.Position == "accept"
                ? "Cody 接受 Petra 意見，共識達成"
                : $"Petra 重評後改判 approve（Round {round.Round}）")
            : $"Dev_plan Appeal 耗盡 {round.MaxRounds} 輪未達共識";

        state.FinalVerdict = verdict;
        await AppealStateHelpers.SaveAsync(context, state);

        _logger.LogInformation(
            "[FrameworkAppeal] DevPlan Appeal Finalize：{Verdict}（Group={Id}，Round={Round}/{Max}）",
            verdict, state.GroupId, round.Round, round.MaxRounds);

        return new AppealLoopResult(
            Verdict: verdict,
            FinalCriticalIds: [],
            RevisionInstructions: round.Petra.RevisionInstructions,
            Summary: summary,
            ArbitrationTriggered: false,
            Arbitration: null);
    }
}
