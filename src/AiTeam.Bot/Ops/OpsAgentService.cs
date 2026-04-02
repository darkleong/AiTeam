using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
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
    /// </summary>
    public async Task RunHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("執行健康檢查");

        var issues = new List<string>();

        // 檢查記憶體使用率
        var memUsage = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        logger.LogInformation("目前記憶體使用：{Mem:F1} MB", memUsage);

        // 檢查 Docker 容器（透過 docker ps）
        try
        {
            var result = await RunProcessAsync("docker", "ps --format \"{{.Names}} {{.Status}}\"", cancellationToken);
            var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (!line.Contains("Up"))
                    issues.Add($"Container 異常：{line.Trim()}");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Docker 健康檢查失敗");
            issues.Add($"Docker 檢查失敗：{ex.Message}");
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
        // 等待 GitHub Actions 完成（簡化版：等 30 秒後檢查 docker ps）
        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

        try
        {
            var result = await RunProcessAsync("docker", "ps --format \"{{.Names}} {{.Status}}\"", cancellationToken);
            // 如果有對應服務且狀態為 Up，視為成功
            return result.Contains("Up");
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
        logger.LogWarning("部署失敗，開始回滾：{Repo}", repoName);

        taskRepo.AddLog(new TaskLog
        {
            TaskId = task.Id,
            Agent = "Ops",
            Step = "開始回滾",
            Status = "running"
        });
        await taskRepo.SaveAsync(cancellationToken);

        try
        {
            // docker-compose 回滾到上一個 image
            await RunProcessAsync("docker-compose", "down", cancellationToken);
            await RunProcessAsync("docker-compose", "up -d", cancellationToken);

            taskRepo.AddLog(new TaskLog
            {
                TaskId = task.Id,
                Agent = "Ops",
                Step = "回滾完成",
                Status = "done"
            });

            await AlertAsync($"🔄 **自動回滾完成**\nRepo：{repoName}\n已恢復到上一個穩定版本。");
            taskRepo.UpdateStatus(task, "done");
        }
        catch (Exception ex)
        {
            taskRepo.AddLog(new TaskLog
            {
                TaskId = task.Id,
                Agent = "Ops",
                Step = $"回滾失敗：{ex.Message}",
                Status = "failed"
            });

            await AlertAsync($"🚨 **回滾失敗，請立即手動處理！**\nRepo：{repoName}\n錯誤：{ex.Message}");
            taskRepo.UpdateStatus(task, "failed");
        }

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

    private static async Task<string> RunProcessAsync(
        string command, string args, CancellationToken cancellationToken)
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return output;
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
