using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Orchestration;
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
/// </summary>
[ApiController]
[Route("internal")]
public class InternalController(
    IOptions<AgentSettings> agentSettings,
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime appLifetime,
    AppSettingsService appSettings,
    RulesService rulesService,
    AgentQueueService queueService,
    AgentConfigCache agentConfigCache,
    ILogger<InternalController> logger) : ControllerBase
{
    private readonly string _apiKey = agentSettings.Value.InternalApiKey;

    /// <summary>
    /// 清除 Bot 端 Cache，下次存取時自動從 DB 重新載入。
    /// scope: rules | agents | agent-config | all（預設 all）
    /// - agents       = AppSettings 資料表快取（AppSettingsService；legacy 命名，對應 app_settings 資料表）
    /// - agent-config = Stage 38 新增：AgentConfig 資料表的 Provider/Model 快取（AgentConfigCache）
    /// </summary>
    [HttpPost("reload-cache")]
    public IActionResult ReloadCache([FromQuery] string scope = "all")
    {
        if (!IsAuthorized()) return Unauthorized();

        if (scope is "rules" or "all")
            rulesService.InvalidateCache();

        // legacy：清 app_settings 資料表快取（系統設定）
        if (scope is "agents" or "all")
            appSettings.InvalidateCache();

        // Stage 38：清 AgentConfig 資料表的 Provider/Model 快取（Dashboard 改完 Agent 設定頁呼叫）
        if (scope is "agent-config" or "all")
            agentConfigCache.InvalidateCache();

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
                        (totalInput / 1000m) * inputRate + (totalOutput / 1000m) * outputRate, 4)
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
    /// 將 failed / cancelled TaskItem 重新推入佇列（供 Dashboard 重試按鈕呼叫）。
    /// </summary>
    [HttpPost("tasks/{taskId}/requeue")]
    public async Task<IActionResult> RequeueTask(Guid taskId, CancellationToken cancellationToken)
    {
        if (!IsAuthorized()) return Unauthorized();

        var (success, reason) = await queueService.RequeueTaskAsync(taskId, cancellationToken);
        if (!success) return BadRequest(new { message = reason });

        logger.LogInformation("TaskItem {Id} 重新入佇列（透過 Dashboard）", taskId);
        return Ok(new { message = "已重新入佇列" });
    }

    /// <summary>
    /// Stage 37 admin：手動重新觸發已完成 TaskItem 的 dispatcher（呼叫 HandleAgentCompletedAsync）。
    /// 用途：當 RequeueTaskAsync 在修 group.Status 同步前發生過、導致 dispatcher 因 group.Status='failed'
    /// 略過後續流程（[TaskGroupService.cs:122]），用此 endpoint 重建 result 並重新呼叫 dispatcher。
    /// 重建的 result 為 minimal（Success=true、Summary=replay 標記、OutputUrl=group.DevPrUrl），
    /// 對 Dev/Reviewer/QA 完成階段足以讓 WorkflowEngine.GetDecision 走後續分支。
    /// </summary>
    [HttpPost("admin/replay-completion/{taskId}")]
    public async Task<IActionResult> ReplayCompletion(Guid taskId, CancellationToken cancellationToken)
    {
        if (!IsAuthorized()) return Unauthorized();

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var taskGroupService = scope.ServiceProvider.GetRequiredService<TaskGroupService>();

        var task = await db.Set<TaskItem>().FindAsync([taskId], cancellationToken);
        if (task is null) return BadRequest(new { message = "任務不存在" });
        if (task.Status != "done") return BadRequest(new { message = $"任務狀態 {task.Status} 不允許 replay（僅接受 done）" });
        if (task.GroupId is not { } groupId) return BadRequest(new { message = "任務無 GroupId（非 group 任務不適用）" });

        var group = await db.Set<TaskGroup>().FindAsync([groupId], cancellationToken);
        if (group is null) return BadRequest(new { message = "TaskGroup 不存在" });

        var result = new AgentExecutionResult(
            Success: true,
            Summary: $"[Manual replay] {task.AssignedAgent} 完成",
            OutputUrl: group.DevPrUrl
        );

        logger.LogWarning("Admin replay-completion：TaskId={TaskId}, Agent={Agent}, GroupId={GroupId}",
            taskId, task.AssignedAgent, groupId);

        await taskGroupService.HandleAgentCompletedAsync(
            groupId, task.AssignedAgent, result, group.DevPrUrl ?? "", cancellationToken);

        return Ok(new { message = "已重新觸發 dispatcher", taskId, agent = task.AssignedAgent, groupId });
    }

    /// <summary>
    /// Stage 33：暫停指定 Agent 的佇列消費（Dashboard 用）。fire-and-forget。
    /// </summary>
    [HttpPost("queue/{agent}/pause")]
    public IActionResult PauseAgent(string agent)
        => FireAndForgetQueueControl($"pause {agent}", scope =>
        {
            var svc = scope.ServiceProvider.GetRequiredService<AgentQueueControlService>();
            return svc.PauseAgentAsync(agent);
        });

    /// <summary>Stage 33：恢復指定 Agent 的佇列消費（Dashboard 用）。fire-and-forget。</summary>
    [HttpPost("queue/{agent}/resume")]
    public IActionResult ResumeAgent(string agent)
        => FireAndForgetQueueControl($"resume {agent}", scope =>
        {
            var svc = scope.ServiceProvider.GetRequiredService<AgentQueueControlService>();
            return svc.ResumeAgentAsync(agent);
        });

    /// <summary>Stage 33：緊急停止所有 Agent（Dashboard 用）。fire-and-forget。</summary>
    [HttpPost("queue/stop-all")]
    public IActionResult StopAll()
        => FireAndForgetQueueControl("stop-all", scope =>
        {
            var svc = scope.ServiceProvider.GetRequiredService<AgentQueueControlService>();
            return svc.StopAllAsync();
        });

    /// <summary>Stage 33：恢復所有 Agent 的佇列消費（Dashboard 用）。fire-and-forget。</summary>
    [HttpPost("queue/resume-all")]
    public IActionResult ResumeAll()
        => FireAndForgetQueueControl("resume-all", scope =>
        {
            var svc = scope.ServiceProvider.GetRequiredService<AgentQueueControlService>();
            return svc.ResumeAllAsync();
        });

    private IActionResult FireAndForgetQueueControl(string action, Func<AsyncServiceScope, Task<(bool ok, string message)>> work)
    {
        if (!IsAuthorized()) return Unauthorized();

        logger.LogInformation("/internal/queue/{Action} 觸發", action);

        Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var (ok, message) = await work(scope);
                logger.LogInformation("/internal/queue/{Action} 背景完成：ok={Ok}，message={Message}", action, ok, message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "/internal/queue/{Action} 背景執行失敗", action);
            }
        });

        return Accepted(new { message = "已送出指令" });
    }

    /// <summary>
    /// Stage 32：觸發 /mock 情境（Dashboard 用）。fire-and-forget，立即回 202，
    /// 後續進度透過 SignalR push 給 Dashboard 任務中心。
    /// </summary>
    [HttpPost("mock/scenario")]
    public IActionResult TriggerMockScenario([FromBody] MockScenarioRequest request)
    {
        if (!IsAuthorized()) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Scenario))
            return BadRequest(new { message = "scenario 欄位必填" });

        logger.LogInformation("/internal/mock/scenario 觸發：scenario={Scenario}", request.Scenario);

        // Fire-and-forget：另開 scope 執行，避免阻塞 HTTP 回應
        Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var mockService = scope.ServiceProvider.GetRequiredService<MockScenarioService>();
                var (ok, message) = await mockService.RunScenarioAsync(
                    request.Scenario, request.Title, request.Project);
                logger.LogInformation("/internal/mock/scenario 背景完成：ok={Ok}，message={Message}", ok, message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "/internal/mock/scenario 背景執行失敗（scenario={Scenario}）", request.Scenario);
            }
        });

        return Accepted(new { message = "Mock 情境已觸發，請至任務中心觀察進度" });
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

public record MockScenarioRequest(string Scenario, string? Title, string? Project);
