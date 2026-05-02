using AiTeam.Bot.Agents;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Appeal.Executors;

/// <summary>
/// Stage 49：把 ClaudeCodeService.RunMeetingSessionAsync 包成 framework Executor，
/// 對齊既有 ReviewAppealService / DevPlanAppealService 的 Cody/Vera/Petra appeal 對話模式。
///
/// **Stage 49 路線 B + Stage 50 service-call 路線拍板（2026-05-02）**：本通用 wrapper **不直接被 production Workflow 引用**。
///
/// Stage 49 路線 B 採「framework Executor 直接調用既有 service method」（stateful prompt SoT 統一），
/// Stage 50 5 Agent Executor（Rosa/Demi/Cody/Quinn/Petra Kickoff）直接 call MeetingCommons.RunAgentTurnAsync — wrapper 仍未 production wire。
///
/// 本檔案保留作為 **Stage 54+ 收尾或 Stage 55+ 動態流程架構真正 wire 時的預留**：
///   - Stage 54 收尾若決定 framework Executor 從 service 切回直連 IClaudeCodeService 時用
///   - Stage 55+ 動態流程架構（FF 三十六 Phase B）若需 multi-Agent 互相 talk Executor 直連時用
///
/// DI 模式（驗證 B 結論，Stage 49 適用 + Stage 50+ 適用）：
///   - framework Executor 不註冊到 DI（factory 模式）
///   - 透過 IServiceScopeFactory 在 HandleAsync 內取 scoped services
///   - 避免 Singleton 持有 Scoped DbContext 跨 superstep 失效
/// </summary>
[Obsolete("Stage 49 路線 B + Stage 50 5 Agent Executor 走直接 call MeetingCommons / ReviewAppealService 模式（service 包裝路線），本 wrapper 仍預留 Stage 54+ 收尾若決定切回 Executor 直連 IClaudeCodeService 或 Stage 55+ 動態流程架構真正 wire 用。", error: false)]
public sealed class ClaudeCodeAgentExecutor : Executor<string, string>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ClaudeCodeAgentExecutorOptions _options;
    private readonly ILogger<ClaudeCodeAgentExecutor> _logger;

    public ClaudeCodeAgentExecutor(
        IServiceScopeFactory scopeFactory,
        ClaudeCodeAgentExecutorOptions options,
        ILogger<ClaudeCodeAgentExecutor> logger)
        : base(options.ExecutorId)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public override async ValueTask<string> HandleAsync(
        string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = await AppealStateHelpers.ReadAsync(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp           = scope.ServiceProvider;
        var claudeCode   = sp.GetRequiredService<IClaudeCodeService>();
        var commons      = sp.GetRequiredService<PmAgentCommons>();
        var tokenLog     = sp.GetRequiredService<TokenLogService>();
        var taskRepo     = sp.GetRequiredService<TaskRepository>();

        // 取 TaskGroup（Executor 內不持有 entity；每次 superstep 新讀避免 EF tracking 衝突）
        var group = await taskRepo.GetGroupByIdAsync(state.GroupId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"[{_options.AgentName}] TaskGroup {state.GroupId} 不存在（FrameworkAppeal Executor）");

        var (workingDir, model, apiKey) = commons.PrepareClaudeCodeEnv(group, _options.ConfigKey);
        var sessionId = Guid.NewGuid().ToString();

        try
        {
            ClaudeCodeResult result;
            try
            {
                result = await claudeCode.RunMeetingSessionAsync(
                    workingDir,
                    sessionId,
                    message,
                    model,
                    apiKey,
                    isFirstMessage: true,
                    maxTurns: _options.MaxTurns,
                    allowedTools: _options.AllowedTools,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[{Agent}] FrameworkAppeal Executor CLI 呼叫失敗（GroupId={Id}，Round={Round}）",
                    _options.AgentName, state.GroupId, state.Round);
                throw new InvalidOperationException(
                    $"[{_options.AgentName}] ClaudeCodeService 呼叫例外：{ex.Message}", ex);
            }

            // Token 紀錄（Stage 44 機制；Stage 名稱對齊既有 ReviewAppeal_cody / ReviewAppeal_vera / DevPlanAppeal_cody）
            var stageName = $"FrameworkAppeal_{_options.AgentName}";
            await tokenLog.LogCliUsageAsync(
                _options.AgentName, model, stageName, state.Round, taskId: null, result.Usage, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "[{Agent}] FrameworkAppeal Executor 失敗（exitCode={Code}，GroupId={Id}）",
                    _options.AgentName, result.ExitCode, state.GroupId);
                throw new InvalidOperationException(
                    $"[{_options.AgentName}] ClaudeCodeService failed (exit {result.ExitCode}). " +
                    $"Tail: {Truncate(result.Output, 500)}");
            }

            return result.Output;
        }
        finally
        {
            commons.CleanupAppealRepo(workingDir, $"FrameworkAppeal:{_options.AgentName}");
        }
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}

/// <summary>
/// Stage 49：執行期不變的設定（factory 建立 Executor 時提供）。
/// 對齊 production 5 個申訴 service 既有呼叫模式。
/// </summary>
public sealed record ClaudeCodeAgentExecutorOptions(
    string ExecutorId,                      // framework Workflow node identity（如 "Cody-ReviewAppeal"）
    string AgentName,                       // token_logs / log 用名稱（"Cody" / "Vera"）
    string ConfigKey,                       // 對 PmAgentCommons.PrepareClaudeCodeEnv 取 model/apiKey（"Dev" / "Reviewer" / "PM"）
    string[] AllowedTools,                  // ["Glob", "Grep", "Read"] 等
    int MaxTurns = 10);
