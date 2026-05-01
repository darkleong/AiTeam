using System.Text.Json;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Appeal.Executors;

/// <summary>
/// Stage 49：Review Appeal max-iter 後 Petra 仲裁 Executor。
///
/// 路線 B 拍板（service 包裝）：
///   - 直接 call ReviewAppealService.ArbitrateReviewAppealAsync
///   - 對齊 legacy AppealOrchestrationService.RunPetraArbitrationAsync 行為
///
/// 仲裁後設 group.SkipReviewerAfterArbitration = true（讓後續 Cody fix 直接交 Petra Gate，跳過 Vera）。
/// </summary>
internal sealed class PetraReviewArbitrationExecutor : Executor<VeraAppealRoundResult, AppealLoopResult>
{
    private static readonly JsonSerializerOptions JsonIndented = new() { WriteIndented = true };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PetraReviewArbitrationExecutor> _logger;

    public PetraReviewArbitrationExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<PetraReviewArbitrationExecutor> logger)
        : base("Petra-ReviewArbitration")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask<AppealLoopResult> HandleAsync(
        VeraAppealRoundResult lastVera, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = await AppealStateHelpers.ReadAsync(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var taskRepo  = sp.GetRequiredService<TaskRepository>();
        var appealSvc = sp.GetRequiredService<ReviewAppealService>();

        var group = await taskRepo.GetGroupByIdAsync(state.GroupId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"[Petra-ReviewArbitration] TaskGroup {state.GroupId} 不存在");

        _logger.LogInformation(
            "[FrameworkAppeal] Petra 仲裁啟動（Group={Id}，Round={Round}/{Max}）",
            state.GroupId, state.Round, state.MaxRounds);

        var arbitration = await appealSvc.ArbitrateReviewAppealAsync(
            group,
            state.LastReviewBody,
            group.ReviewAppealLog ?? "",
            cancellationToken);

        var arbitrationJson = JsonSerializer.Serialize(arbitration, JsonIndented);

        AppealLogHelpers.AppendReviewAppealLog(group, state.Round,
            $"**Petra 仲裁（完整）：**\n```json\n{arbitrationJson}\n```\n\n" +
            $"→ 最終 Critical：{arbitration.FinalCriticals.Count} 項，決定：{arbitration.Decision}");

        group.SkipReviewerAfterArbitration = true;
        await taskRepo.SaveAsync(cancellationToken);

        // state 寫最終結果
        state.FinalCriticalIds = arbitration.FinalCriticals.ToList();
        state.FinalVerdict = arbitration.FinalCriticals.Count == 0
            ? "max_iter_arbitration_approve"
            : "max_iter_arbitration_reject";
        await AppealStateHelpers.SaveAsync(context, state);

        return new AppealLoopResult(
            Verdict: state.FinalVerdict,
            FinalCriticalIds: arbitration.FinalCriticals,
            RevisionInstructions: null,
            Summary: $"Petra max-iter 仲裁：最終 {arbitration.FinalCriticals.Count} 個 Critical，決定 {arbitration.Decision}",
            ArbitrationTriggered: true,
            Arbitration: arbitration);
    }
}

/// <summary>
/// Stage 49：Review Appeal Vera approved 後 Petra Gate Executor（單輪 Petra 審核 Vera 嚴重度）。
///
/// 路線 B 拍板（service 包裝）：
///   - 直接 call PmReviewService.ReviewVeraAsync
///   - 對齊 legacy AppealOrchestrationService.RunPetraGateAsync 行為
///
/// 注意：本 Executor 走在「Vera 已 approve」路徑（state.RemainingCriticalIds.Count == 0）。
/// Petra Gate 仍需審 Vera review 嚴重度（避免 Vera Critical 全 agree 但仍有 blocking 問題）。
/// </summary>
internal sealed class PetraReviewGateExecutor : Executor<VeraAppealRoundResult, AppealLoopResult>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PetraReviewGateExecutor> _logger;

    public PetraReviewGateExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<PetraReviewGateExecutor> logger)
        : base("Petra-ReviewGate")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask<AppealLoopResult> HandleAsync(
        VeraAppealRoundResult lastVera, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = await AppealStateHelpers.ReadAsync(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var taskRepo  = sp.GetRequiredService<TaskRepository>();
        var pmReview  = sp.GetRequiredService<PmReviewService>();

        var group = await taskRepo.GetGroupByIdAsync(state.GroupId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"[Petra-ReviewGate] TaskGroup {state.GroupId} 不存在");

        _logger.LogInformation(
            "[FrameworkAppeal] Petra Gate 啟動（Group={Id}，Round={Round}）",
            state.GroupId, state.Round);

        var petraReview = await pmReview.ReviewVeraAsync(
            group.Title,
            group.LastReviewBody ?? state.LastReviewBody,
            cancellationToken);

        // Petra revise 路徑：把指示寫進 LastReviewBody（對齊 legacy RunPetraGateAsync 行為）
        if (petraReview.Decision == "revise" && !string.IsNullOrWhiteSpace(petraReview.RevisionInstructions))
        {
            group.LastReviewBody =
                (group.LastReviewBody ?? "") +
                "\n\n【Petra 修正指示】" + petraReview.RevisionInstructions;
            await taskRepo.SaveAsync(cancellationToken);
        }

        state.FinalVerdict = petraReview.Decision;
        state.RevisionInstructions = petraReview.RevisionInstructions;
        await AppealStateHelpers.SaveAsync(context, state);

        return new AppealLoopResult(
            Verdict: petraReview.Decision,
            FinalCriticalIds: petraReview.Decision == "revise" ? state.RemainingCriticalIds.ToList() : [],
            RevisionInstructions: petraReview.RevisionInstructions,
            Summary: $"Petra Gate：{petraReview.Decision} — {petraReview.Summary}",
            ArbitrationTriggered: false,
            Arbitration: null);
    }
}
