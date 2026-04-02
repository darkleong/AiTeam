using AiTeam.Bot.Configuration;
using AiTeam.Data;
using AiTeam.Data.Repositories;
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
    ILogger<InternalController> logger) : ControllerBase
{
    private readonly string _apiKey = agentSettings.Value.InternalApiKey;

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
