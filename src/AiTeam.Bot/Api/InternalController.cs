using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Api;

/// <summary>
/// 僅供內部呼叫的管理 API（Dashboard 重啟 Bot、GitHub Actions 寫入部署記錄）。
/// 透過 X-Api-Key header 進行驗證。
///
/// Stage 78c：v4 Pipeline framework 整套砍後 InternalController 縮為 v5.5 essential endpoints：
///   - reload-cache（Bot 端 Cache 重新載入 / Dashboard 改完設定 / forge-self-verify skill 必用）
///   - restart（Bot 容器重啟 / Dashboard 用）
///   - deployment（GitHub Actions self-hosted runner 寫入部署記錄）
///   - tokens（Token 用量彙總 / Dashboard Token 監控頁）
///
/// 砍範圍（Stage 78c）：
///   - queue/{agent}/pause + resume + stop-all + resume-all（v4 AgentQueueControlService 砍）
///   - tasks/{taskId}/requeue（v4 AgentQueueService 砍）
///   - admin/replay-completion/{taskId}（v4 TaskGroupService + AgentExecutionResult 砍）
///   - taskgroup/{groupId}/pause + resume + pause-epic + resume-epic（v4 TaskGroupService 砍）
///   - kickoff/trigger-mid-interrupt（v4 FrameworkHitlBridge 砍 / Stage 51 HITL）
///   - mock/scenario（v4 MockScenarioService 砍 / 議題 7 拍板）
/// </summary>
[ApiController]
[Route("internal")]
public class InternalController(
    IOptions<AgentSettings> agentSettings,
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime appLifetime,
    AppSettingsService appSettings,
    RulesService rulesService,
    ILogger<InternalController> logger) : ControllerBase
{
    private readonly string _apiKey = agentSettings.Value.InternalApiKey;

    /// <summary>
    /// 清除 Bot 端 Cache，下次存取時自動從 DB 重新載入。
    /// scope: rules | agents | agent-config | all（預設 all）
    /// - agents       = AppSettings 資料表快取（AppSettingsService；legacy 命名，對應 app_settings 資料表）
    /// - agent-config = Stage 87 A2：talents 資料表的 Provider/Model/TokenLimit 快取（TalentMetaCache / 取代 Stage 38 AgentConfigCache / scope 名稱保留對齊 Dashboard 既有 ReloadCacheAsync("agent-config") caller）
    /// - all          = Stage 72 新增：含 PromptResolver（skill_prompts / talent_prompts 快取 — production rollback SQL UPDATE + reload-cache `all` 5 分鐘內生效）
    /// </summary>
    [HttpPost("reload-cache")]
    public IActionResult ReloadCache([FromQuery] string scope = "all")
    {
        if (!IsAuthorized()) return Unauthorized();

        // v4-rewrite：talents / SkillPrompt / TalentPrompt cache 整套砍（6 Talent / v5.5 Prompt DB 全砍）
        if (scope is "rules" or "all")
            rulesService.InvalidateCache();
        if (scope is "agents" or "all")
            appSettings.InvalidateCache();

        logger.LogInformation("Bot Cache 已清除（scope={Scope}）", scope);
        return Ok(new { message = "已套用變更", scope });
    }

    /// <summary>
    /// 重啟 Bot：呼叫後 Bot 容器退出，由 Docker restart:always 自動重新啟動。
    /// </summary>
    [HttpPost("restart")]
    public IActionResult Restart()
    {
        if (!IsAuthorized()) return Unauthorized();

        logger.LogWarning("收到重啟請求，Bot 即將停止...");

        // 延遲 1 秒讓回應先送出，再觸發停止
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            appLifetime.StopApplication();
        });

        return Ok(new { message = "Bot 重啟中，請稍後..." });
    }

    /// <summary>
    /// 寫入部署記錄：由 GitHub Actions 在 Deploy job 完成後呼叫。
    /// </summary>
    [HttpPost("deployment")]
    public async Task<IActionResult> RecordDeployment(
        [FromBody] DeploymentRecordRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized()) return Unauthorized();

        await using var scope = scopeFactory.CreateAsyncScope();
        var db      = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var team    = await db.Teams.FirstAsync(cancellationToken);

        var shortSha  = request.Sha?.Length >= 7 ? request.Sha[..7] : request.Sha ?? "unknown";
        var refName   = request.Ref ?? "unknown";

        // 從 "owner/repo" 取出 repo 名稱，比對 DB 中的專案
        var repoName  = request.Project?.Contains('/') == true
            ? request.Project.Split('/').Last()
            : request.Project;
        var project   = repoName is not null
            ? await db.Projects.FirstOrDefaultAsync(p => p.Name == repoName, cancellationToken)
            : null;

        var task = new TaskItem
        {
            TeamId        = team.Id,
            ProjectId     = project?.Id,
            Title         = $"Deploy {refName} ({shortSha})",
            Description   = $"Project: {request.Project}\nRef: {request.Ref}\nSHA: {request.Sha}\nStatus: {request.Status}",
            TriggeredBy   = "GitHubActions",
            AssignedAgent = "Ops",
            Status        = request.Status == "success" ? "done" : "failed",
            CompletedAt   = DateTime.UtcNow
        };

        repo.Add(task);
        await repo.SaveAsync(cancellationToken);

        logger.LogInformation("部署記錄已寫入：{Title}（{Status}）", task.Title, task.Status);
        return Ok(new { taskId = task.Id });
    }

    /// <summary>
    /// 查詢指定時間區間的 Token 用量彙總，供 Dashboard Token 監控頁使用。
    /// </summary>
    [HttpGet("tokens")]
    public async Task<IActionResult> GetTokenSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized()) return Unauthorized();

        var fromDate = from?.ToUniversalTime() ?? DateTime.UtcNow.Date;
        var toDate   = to?.ToUniversalTime()   ?? DateTime.UtcNow;

        // 讀取費率設定（每千 token 美金，依模型設定）
        var inputRateStr  = await appSettings.GetAsync("TokenPricing:InputPer1kUsd",  cancellationToken) ?? "0.003";
        var outputRateStr = await appSettings.GetAsync("TokenPricing:OutputPer1kUsd", cancellationToken) ?? "0.015";
        decimal.TryParse(inputRateStr,  System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var inputRate);
        decimal.TryParse(outputRateStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var outputRate);

        await using var scope = scopeFactory.CreateAsyncScope();
        var tokenRepo = scope.ServiceProvider.GetRequiredService<TokenRepository>();
        var logs = await tokenRepo.GetByPeriodAsync(fromDate, toDate, cancellationToken);

        // 依 Agent 彙總
        var agentSummaries = logs
            .GroupBy(l => l.AgentName)
            .Select(g =>
            {
                var totalInput  = g.Sum(l => l.InputTokens);
                var totalOutput = g.Sum(l => l.OutputTokens);
                return new TokenAgentSummaryDto
                {
                    AgentName          = g.Key,
                    Model              = g.OrderByDescending(l => l.CreatedAt).First().Model,
                    TotalInputTokens   = totalInput,
                    TotalOutputTokens  = totalOutput,
                    EstimatedCostUsd   = Math.Round(
                        (totalInput / 1000m) * inputRate + (totalOutput / 1000m) * outputRate, 4),
                    // Stage 61-FF 五十：任一筆 IsEstimated=true → 整 Agent 標 HasEstimated（Dashboard 視覺區分）
                    HasEstimated       = g.Any(l => l.IsEstimated)
                };
            })
            .OrderBy(s => s.AgentName)
            .ToList();

        // 每日數據點（供折線圖）
        var dailyPoints = logs
            .GroupBy(l => (l.CreatedAt.Date, l.AgentName))
            .Select(g => new TokenDailyDataPointDto
            {
                Date       = g.Key.Date,
                AgentName  = g.Key.AgentName,
                TotalTokens = g.Sum(l => l.InputTokens + l.OutputTokens)
            })
            .OrderBy(p => p.Date)
            .ToList();

        return Ok(new TokenSummaryDto
        {
            AgentSummaries  = agentSummaries,
            DailyDataPoints = dailyPoints
        });
    }

    /// <summary>
    /// Stage 83 子項 4 補做：Bot 容器健康檢測（Dashboard Monitoring 分區 SystemHealth tab 用）。
    /// 回 Bot Process uptime + PostgreSQL CanConnect + Discord state（state 未 inject — 簡化 placeholder）。
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        if (!IsAuthorized()) return Unauthorized();

        bool dbOk     = false;
        string dbDetail = "";
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbOk     = await db.Database.CanConnectAsync(ct);
            dbDetail = dbOk ? "AppDbContext 連線正常" : "Cannot connect";
        }
        catch (Exception ex) { dbDetail = ex.Message.Length > 100 ? ex.Message[..100] + "..." : ex.Message; }

        var uptimeMin = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalMinutes;
        return Ok(new HealthStatusDto(
            BotProcessUp:    true,
            BotProcessDetail: $"Bot Process uptime {uptimeMin:F1} 分鐘",
            DbConnected:     dbOk,
            DbDetail:        dbDetail,
            DiscordConnected: false,  // 簡化 — DiscordSocketClient state 未 inject / Stage 84+ 候選
            DiscordDetail:    "（Discord state inject 留 Stage 84+ 補）",
            Timestamp:       DateTime.UtcNow));
    }

    private bool IsAuthorized()
    {
        if (string.IsNullOrEmpty(_apiKey)) return false;
        Request.Headers.TryGetValue("X-Api-Key", out var key);
        return key == _apiKey;
    }
}

public record DeploymentRecordRequest(
    string? Project,
    string? Ref,
    string? Sha,
    string? Status,
    string? TriggeredBy);

/// <summary>Stage 83 子項 4 補做：Bot 健康檢測 response（Dashboard SystemHealth tab 用）。</summary>
public record HealthStatusDto(
    bool BotProcessUp, string BotProcessDetail,
    bool DbConnected, string DbDetail,
    bool DiscordConnected, string DiscordDetail,
    DateTime Timestamp);
