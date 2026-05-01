using System.Text.Json;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Appeal.Executors;

/// <summary>
/// Stage 49：Review Appeal 路徑 Vera 重評 Executor（接 Cody appeal JSON 後重新評估）。
///
/// 路線 B 拍板（service 包裝）：
///   - 不重組 prompt，直接 call ReviewAppealService.RunVeraAppealAsync
///
/// Output = VeraAppealRoundResult（含 Approved 派生 flag + Round / MaxRounds 給 framework AddSwitch routing）。
///
/// 同步寫 group.ReviewAppealLog + ReviewAppealRoundA（對齊 legacy AppealOrchestrationService 行為，
/// 讓 Dashboard UI 解析既有欄位完全 work）。
/// </summary>
internal sealed class VeraReviewAppealExecutor : Executor<CodyAppeal, VeraAppealRoundResult>
{
    private static readonly JsonSerializerOptions JsonIndented = new() { WriteIndented = true };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VeraReviewAppealExecutor> _logger;

    public VeraReviewAppealExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<VeraReviewAppealExecutor> logger)
        : base("Vera-ReviewAppeal")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask<VeraAppealRoundResult> HandleAsync(
        CodyAppeal codyAppeal, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = await AppealStateHelpers.ReadAsync(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var taskRepo  = sp.GetRequiredService<TaskRepository>();
        var appealSvc = sp.GetRequiredService<ReviewAppealService>();

        var group = await taskRepo.GetGroupByIdAsync(state.GroupId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"[Vera-ReviewAppeal] TaskGroup {state.GroupId} 不存在");

        var codyJson = JsonSerializer.Serialize(codyAppeal, JsonIndented);

        // 短路：Cody 全 agree → 不需 Vera 重評，視同 approved
        var disagrees = codyAppeal.Items.Where(i => i.Response == "disagree").ToList();
        if (disagrees.Count == 0)
        {
            _logger.LogInformation(
                "[FrameworkAppeal] Vera Round {Round}：Cody 全 agree，視同 approved（Group={Id}）",
                state.Round, state.GroupId);

            AppealLogHelpers.AppendReviewAppealLog(group, state.Round,
                $"**Cody 回應（完整）：**\n```json\n{codyJson}\n```\n\n→ Cody 同意所有 Critical，進入修正流程。");
            group.ReviewAppealRoundA = state.Round;
            await taskRepo.SaveAsync(cancellationToken);

            // 短路 approved：建一個空 VeraAppealResponse 結構
            var emptyVera = new VeraAppealResponse(
                state.RemainingCriticalIds,  // 全接受
                [],
                "Cody 全 agree，無需 Vera 重評");

            state.RemainingCriticalIds = [];
            await AppealStateHelpers.SaveAsync(context, state);

            return new VeraAppealRoundResult(
                Vera: emptyVera,
                Round: state.Round,
                MaxRounds: state.MaxRounds,
                Approved: true,
                RemainingCriticalCount: 0);
        }

        _logger.LogInformation(
            "[FrameworkAppeal] Vera Round {Round}：Cody {Count} disagree → 進入 Vera 重評（Group={Id}）",
            state.Round, disagrees.Count, state.GroupId);

        var veraResponse = await appealSvc.RunVeraAppealAsync(
            group,
            state.LastReviewBody,
            codyJson,
            cancellationToken);

        var veraJson = JsonSerializer.Serialize(veraResponse, JsonIndented);

        // 更新 RemainingCriticalIds（扣除 Vera 接受的）
        var newRemaining = state.RemainingCriticalIds
            .Where(id => !veraResponse.AcceptedIds.Contains(id))
            .ToList();

        // 寫 group.ReviewAppealLog + ReviewAppealRoundA（對齊 legacy 行為）
        AppealLogHelpers.AppendReviewAppealLog(group, state.Round,
            $"**Cody 回應（完整）：**\n```json\n{codyJson}\n```\n\n" +
            $"**Vera 重評（完整）：**\n```json\n{veraJson}\n```\n\n" +
            $"→ Vera 接受 {veraResponse.AcceptedIds.Count} 項，維持 {veraResponse.MaintainedIds.Count} 項，" +
            $"剩餘 Critical：{newRemaining.Count}");
        group.ReviewAppealRoundA = state.Round;
        await taskRepo.SaveAsync(cancellationToken);

        // 寫 state
        state.RemainingCriticalIds = newRemaining;
        var veraDecision = new VeraDecision
        {
            Approved = newRemaining.Count == 0,
            Feedback = veraResponse.UpdatedSummary,
            AcceptedIds = veraResponse.AcceptedIds.ToList(),
            MaintainedIds = veraResponse.MaintainedIds.ToList(),
            Round = state.Round,
        };
        state.VeraDecisions.Add(veraDecision);
        await AppealStateHelpers.SaveAsync(context, state);

        return new VeraAppealRoundResult(
            Vera: veraResponse,
            Round: state.Round,
            MaxRounds: state.MaxRounds,
            Approved: newRemaining.Count == 0,
            RemainingCriticalCount: newRemaining.Count);
    }
}
