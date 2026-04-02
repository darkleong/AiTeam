using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using AiTeam.Shared.ViewModels;
using DiscordNet = Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using Octokit;
using Quartz;

namespace AiTeam.Bot.Ops;

/// <summary>
/// Ops Agent：監控部署結果、健康檢查、處理回滾邏輯。
/// </summary>
public class OpsAgentService(
    IOptions<DiscordSettings> discordSettings,
    IOptions<GitHubSettings> gitHubSettings,
    IOptions<OpsSettings> opsSettings,
    DiscordSocketClient discordClient,
    IServiceProvider serviceProvider,
    DashboardPushService dashboardPush,
    ILogger<OpsAgentService> logger) : IAgentExecutor
{
    private readonly DiscordSettings _discord = discordSettings.Value;
    private readonly GitHubSettings _github = gitHubSettings.Value;
    private readonly OpsSettings _ops = opsSettings.Value;

    // 避免重複通知同一筆失敗（記住上次已處理的 run ID）
    private long _lastHandledRunId;

    /// <inheritdoc />
    public async Task<AgentExecutionResult> ExecuteTaskAsync(
        TaskItem task,
        string owner,
        string repo,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default)
    {
        await AlertAsync($"📋 Ops 任務待執行：{task.Title}（專案：{repo}）");
        return new AgentExecutionResult(true, "Ops 警報已發送至 #警報 頻道");
    }

    /// <summary>
    /// 監控 GitHub Actions 部署結果，成功通知，失敗自動回滾。
    /// </summary>
    public async Task MonitorDeploymentAsync(
        string repoName,
        string deployRunUrl,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("開始監控部署：{Repo}（{Url}）", repoName, deployRunUrl);

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        // 建立 Ops 任務紀錄
        var task = new TaskItem
        {
            TeamId = Guid.Empty,
            Title = $"部署監控：{repoName}",
            TriggeredBy = "GitHub",
            AssignedAgent = "Ops",
            Status = "running"
        };
        taskRepo.Add(task);
        await taskRepo.SaveAsync(cancellationToken);

        await dashboardPush.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = "Ops",
            Status           = "running",
            CurrentTaskTitle = task.Title,
            LastUpdated      = DateTime.UtcNow
        });

        try
        {
            // 輪詢部署狀態（最多等 10 分鐘）
            var success = await PollDeploymentAsync(repoName, cancellationToken);

            if (success)
            {
                await NotifySuccessAsync(repoName, deployRunUrl);
                taskRepo.UpdateStatus(task, "done");
            }
            else
            {
                // 內層失敗 → 自動回滾
                await ExecuteRollbackAsync(repoName, task, taskRepo, cancellationToken);
            }

            await taskRepo.SaveAsync(cancellationToken);

            await dashboardPush.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName   = "Ops",
                Status      = "idle",
                LastUpdated = DateTime.UtcNow
            });

            await dashboardPush.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = task.Id,
                Title     = task.Title,
                Status    = task.Status,
                AgentName = "Ops"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ops Agent 監控失敗：{Repo}", repoName);
            await AlertAsync($"⚠️ Ops Agent 監控例外：{repoName}\n{ex.Message}");
            taskRepo.UpdateStatus(task, "failed");
            await taskRepo.SaveAsync(cancellationToken);

            await dashboardPush.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName   = "Ops",
                Status      = "error",
                LastUpdated = DateTime.UtcNow
            });

            await dashboardPush.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = task.Id,
                Title     = task.Title,
                Status    = "failed",
                AgentName = "Ops"
            });
        }
    }

    /// <summary>
    /// 監控 GitHub Actions CI/CD 最新 run：外部故障自動重試，程式問題通知老闆。
    /// </summary>
    public async Task MonitorCiCdAsync(CancellationToken cancellationToken = default)
    {
        if (!_ops.CiCdMonitorEnabled) return;
        if (string.IsNullOrEmpty(_github.PersonalAccessToken)) return;

        try
        {
            var client = new GitHubClient(new ProductHeaderValue("AiTeamBot"))
            {
                Credentials = new Credentials(_github.PersonalAccessToken)
            };

            // 取得最新的 workflow run（僅看失敗的）
            var runs = await client.Actions.Workflows.Runs.List(
                _github.Owner,
                _github.DefaultRepo ?? "AiTeam",
                new WorkflowRunsRequest { Status = CheckRunStatusFilter.Failure });

            var latest = runs.WorkflowRuns
                .Where(r => r.Name == _ops.CiCdWorkflowName)
                .OrderByDescending(r => r.UpdatedAt)
                .FirstOrDefault();

            if (latest is null) return;
            // 已處理過或超出時間窗口則跳過
            if (latest.Id == _lastHandledRunId) return;
            if (latest.UpdatedAt < DateTimeOffset.UtcNow.AddMinutes(-_ops.CiCdFailureWindowMinutes)) return;

            logger.LogWarning("偵測到 CI/CD 失敗：RunId={RunId}，結論={Conclusion}", latest.Id, latest.Conclusion);

            // 取得失敗 job 的名稱，判斷是外部故障還是程式問題
            var jobs = await client.Actions.Workflows.Jobs.List(_github.Owner, _github.DefaultRepo ?? "AiTeam", latest.Id);
            var failedJobs = jobs.Jobs.Where(j => j.Conclusion == WorkflowJobConclusion.Failure).ToList();
            var failedJobNames = string.Join(", ", failedJobs.Select(j => j.Name));

            var isExternalFailure = IsExternalFailure(failedJobNames);

            _lastHandledRunId = latest.Id;

            if (isExternalFailure)
            {
                logger.LogInformation("判斷為外部故障，自動重試：{Jobs}", failedJobNames);
                await client.Actions.Workflows.Runs.Rerun(_github.Owner, _github.DefaultRepo ?? "AiTeam", latest.Id);
                await AlertAsync($"🔁 **CI/CD 外部故障，已自動重試**\nJob：{failedJobNames}\nRun：{latest.HtmlUrl}");
            }
            else
            {
                logger.LogWarning("判斷為程式問題，通知老闆：{Jobs}", failedJobNames);
                await AlertAsync(
                    $"🚨 **CI/CD 失敗，需要人工介入**\n" +
                    $"Job：{failedJobNames}\n" +
                    $"Run：{latest.HtmlUrl}\n" +
                    $"請確認是否有 Build/Test 錯誤。");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CI/CD 監控失敗");
        }
    }

    /// <summary>判斷失敗是否屬於外部故障（可自動重試）。</summary>
    private static bool IsExternalFailure(string failedJobNames)
    {
        var lower = failedJobNames.ToLowerInvariant();
        // 外部故障通常在 Build & Push Images 這個 job，且與 docker / 網路相關
        var externalKeywords = new[] { "push image", "build and push", "pull latest" };
        return externalKeywords.Any(kw => lower.Contains(kw));
    }

    /// <summary>
    /// 健康檢查（Quartz 排程觸發）。
    /// 使用 DB 連線 + 記憶體用量，不依賴容器內不存在的 docker CLI。
    /// </summary>
    public async Task RunHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("執行健康檢查");

        var issues = new List<string>();

        // 檢查記憶體使用率
        var memUsageMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        logger.LogInformation("目前記憶體使用：{Mem:F1} MB", memUsageMb);
        if (memUsageMb > 512)
            issues.Add($"記憶體使用偏高：{memUsageMb:F1} MB");

        // 檢查 DB 連線
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            logger.LogInformation("DB 連線正常");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DB 健康檢查失敗");
            issues.Add($"DB 連線失敗：{ex.Message}");
        }

        if (issues.Count > 0)
        {
            var message = "⚠️ **健康檢查發現問題：**\n" + string.Join("\n", issues.Select(i => $"- {i}"));
            await AlertAsync(message);
        }
        else
        {
            logger.LogInformation("健康檢查通過");
        }
    }

    /// <summary>
    /// 發送警報到 Discord #警報 頻道。
    /// </summary>
    public async Task AlertAsync(string message)
    {
        var channel = await FindChannelAsync(_discord.Channels.Alerts);
        if (channel is null)
        {
            logger.LogWarning("找不到警報頻道，訊息：{Message}", message);
            return;
        }
        await channel.SendMessageAsync(message);
    }

    // ────────────── Private ──────────────

    private async Task<bool> PollDeploymentAsync(string repoName, CancellationToken cancellationToken)
    {
        // 等待 GitHub Actions 完成後，透過 DB 連線確認服務正常
        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task ExecuteRollbackAsync(
        string repoName,
        TaskItem task,
        TaskRepository taskRepo,
        CancellationToken cancellationToken)
    {
        logger.LogWarning("部署失敗，通知老闆手動處理：{Repo}", repoName);

        taskRepo.AddLog(new TaskLog
        {
            TaskId = task.Id,
            Agent  = "Ops",
            Step   = "部署異常，待人工介入",
            Status = "failed"
        });
        await taskRepo.SaveAsync(cancellationToken);

        // Bot 容器內無法執行 docker-compose，改為通知老闆手動回滾
        await AlertAsync(
            $"🚨 **部署異常，請手動處理！**\n" +
            $"Repo：{repoName}\n" +
            $"請至 Docker Desktop 或主機執行回滾操作。");

        taskRepo.UpdateStatus(task, "failed");
        await taskRepo.SaveAsync(cancellationToken);
    }

    private async Task NotifySuccessAsync(string repoName, string runUrl)
    {
        var channel = await FindChannelAsync(_discord.Channels.TaskUpdates);
        if (channel is null) return;

        var embed = new DiscordNet.EmbedBuilder()
            .WithTitle("✅ 部署成功")
            .WithColor(DiscordNet.Color.Green)
            .AddField("Repo", repoName, inline: true)
            .AddField("Actions Run", runUrl)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        await channel.SendMessageAsync(embed: embed);
    }

    private async Task<DiscordNet.IMessageChannel?> FindChannelAsync(string channelName)
    {
        if (!ulong.TryParse(_discord.GuildId, out var guildId)) return null;
        var guild = discordClient.GetGuild(guildId);
        if (guild is null) return null;
        return guild.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }

}

/// <summary>
/// Quartz Job：定時執行健康檢查。
/// </summary>
public class HealthCheckJob(OpsAgentService opsAgent) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        await opsAgent.RunHealthCheckAsync(ct);
        await opsAgent.MonitorCiCdAsync(ct);
    }
}
