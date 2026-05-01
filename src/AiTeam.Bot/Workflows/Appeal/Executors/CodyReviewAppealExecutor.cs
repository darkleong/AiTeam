using System.Text.Json;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Appeal.Executors;

/// <summary>
/// Stage 49：Review Appeal 路徑 Cody 反駁 Executor（Cody-Vera-Petra Appeal loop 中 Cody 角色）。
///
/// 職責：
///   - 第 1 輪：接 Workflow start string trigger → 第一輪 Cody appeal
///   - 第 2-N 輪：接 VeraAppealRoundResult → 接續第 N 輪 Cody appeal（依 Vera 維持的 critical 反駁）
///
/// 路線 B 拍板（service 包裝）：
///   - 不重組 prompt，直接 call ReviewAppealService.RunCodyAppealAsync
///   - prompt SoT 統一在 ReviewAppealService 內，feature flag 兩條路徑 prompt 不漂移
///
/// 多型 input 設計（spike Phase 3 已驗 [MessageHandler] partial class 模式）：
///   - HandleInitialAsync(string) — 第 1 輪
///   - HandleRevisionAsync(VeraAppealRoundResult) — 第 N 輪 loop back
///   注意 ValueTask 簽名不含 CancellationToken（spike 揭露 source generator 嚴格簽名要求）
/// </summary>
internal sealed partial class CodyReviewAppealExecutor : Executor
{
    private static readonly JsonSerializerOptions JsonIndented = new() { WriteIndented = true };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CodyReviewAppealExecutor> _logger;

    public CodyReviewAppealExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<CodyReviewAppealExecutor> logger)
        : base("Cody-ReviewAppeal")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask<CodyAppeal> HandleInitialAsync(string trigger, IWorkflowContext context)
        => await ExecuteRoundAsync(context, isFirstRound: true);

    [MessageHandler]
    private async ValueTask<CodyAppeal> HandleRevisionAsync(VeraAppealRoundResult veraResult, IWorkflowContext context)
        => await ExecuteRoundAsync(context, isFirstRound: false);

    private async Task<CodyAppeal> ExecuteRoundAsync(IWorkflowContext context, bool isFirstRound)
    {
        var state = await AppealStateHelpers.ReadAsync(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var taskRepo  = sp.GetRequiredService<TaskRepository>();
        var appealSvc = sp.GetRequiredService<ReviewAppealService>();

        var group = await taskRepo.GetGroupByIdAsync(state.GroupId, default)
            ?? throw new InvalidOperationException(
                $"[Cody-ReviewAppeal] TaskGroup {state.GroupId} 不存在");

        // 第 1 輪不推進 Round（router 已設 1）；第 N 輪 += 1
        if (!isFirstRound)
        {
            state.Round++;
        }

        var priorContext = isFirstRound ? null : group.ReviewAppealLog;

        _logger.LogInformation(
            "[FrameworkAppeal] Cody Round {Round}（Group={Id}，剩餘 critical {Count}）",
            state.Round, state.GroupId, state.RemainingCriticalIds.Count);

        var codyAppeal = await appealSvc.RunCodyAppealAsync(
            group,
            state.LastReviewBody,
            group.Title,
            state.RemainingCriticalIds,
            priorContext,
            default);

        var codyJson = JsonSerializer.Serialize(codyAppeal, JsonIndented);
        state.CodyResponses.Add(codyJson);
        await AppealStateHelpers.SaveAsync(context, state);

        return codyAppeal;
    }
}
