using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using AiTeam.Shared.ViewModels;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Orchestration;

/// <summary>
/// Stage 27a：Agent 佇列處理器。
/// BackgroundService，為每個 executor key 維護一個 SemaphoreSlim(1,1)，
/// 保證同一 Agent 同一時間只處理一件任務（per-agent FIFO Queue）。
/// </summary>
public class AgentQueueProcessor(
    IServiceProvider serviceProvider,
    AgentQueueService queueService,
    TaskGroupService taskGroupService,
    DashboardPushService pushService,
    AppSettingsService appSettings,
    RulesService rulesService,
    DiscordSocketClient discordClient,
    IOptions<GitHubSettings> gitHubSettings,
    IOptions<DiscordSettings> discordSettings,
    IHostApplicationLifetime appLifetime,
    ILogger<AgentQueueProcessor> logger) : BackgroundService
{
    private readonly GitHubSettings  _gitHub  = gitHubSettings.Value;
    private readonly DiscordSettings _discord = discordSettings.Value;

    // Semaphore key → 可匹配的 AssignedAgent 名稱列表
    // Dev_plan 和 Dev 共用 "Dev" semaphore，避免同時操作同一 workspace
    private static readonly Dictionary<string, string[]> SemaphoreGroups = new()
    {
        [AgentNames.Dev]          = [AgentNames.Dev, "Dev_plan"],
        [AgentNames.Reviewer]     = [AgentNames.Reviewer],
        [AgentNames.Qa]           = [AgentNames.Qa],
        [AgentNames.Doc]          = [AgentNames.Doc],
        [AgentNames.Requirements] = [AgentNames.Requirements],
        [AgentNames.Designer]     = [AgentNames.Designer],
        [AgentNames.Release]      = [AgentNames.Release],
        [AgentNames.Ops]          = [AgentNames.Ops],
    };

    // Per-executor-key 信號量：key = executor key（AgentNames 常數）
    private readonly Dictionary<string, SemaphoreSlim> _semaphores =
        SemaphoreGroups.Keys.ToDictionary(k => k, _ => new SemaphoreSlim(1, 1));

    /// <summary>AssignedAgent → executor key（"Dev_plan" 映射到 "Dev"）。</summary>
    private static string GetExecutorKey(string assignedAgent) => assignedAgent switch
    {
        "Dev_plan" => AgentNames.Dev,
        _          => assignedAgent
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 27a-3：啟動掃描，恢復被中斷的任務
        await RecoverStuckTasksAsync(stoppingToken);
        // Stage 31：啟動掃描，恢復被中斷的 Kickoff / Design 會議
        await taskGroupService.RecoverStuckMeetingsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            queueService.WaitForSignal(3000);

            foreach (var (semaphoreKey, agentNames) in SemaphoreGroups)
            {
                if (stoppingToken.IsCancellationRequested) break;

                var semaphore = _semaphores[semaphoreKey];
                if (!semaphore.Wait(0)) continue; // 已有任務在跑，跳過

                // 27b-1：讀取 Agent 狀態，決定是否消費佇列
                var agentState = await appSettings.GetAsync($"AgentState:{semaphoreKey}") ?? "active";
                if (agentState is "paused" or "stopped")
                {
                    semaphore.Release();
                    continue;
                }
                if (agentState is "stopping")
                {
                    // semaphore 已取得 = 無任務在跑 = 可安全轉為 stopped
                    await appSettings.SetAsync($"AgentState:{semaphoreKey}", "stopped");
                    _ = pushService.PushQueueUpdateAsync();
                    semaphore.Release();
                    continue;
                }

                var task = await queueService.DequeueAsync(agentNames, stoppingToken);
                if (task is null) { semaphore.Release(); continue; }

                var executorKey = GetExecutorKey(task.AssignedAgent);

                // Fire-and-forget：semaphore 在 ExecuteTaskAsync 的 finally 內釋放
                _ = Task.Run(
                    () => ExecuteTaskAsync(task, executorKey, semaphore, appLifetime.ApplicationStopping),
                    appLifetime.ApplicationStopping);
            }
        }

        // 27a-3：Graceful Shutdown — 等待所有執行中任務完成（最多 60 秒）
        await WaitForRunningTasksAsync(TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// 執行 dequeue 出來的 TaskItem。
    /// 從 FireOneStepAsync 的執行邏輯搬移過來，保持相同的 Discord 通知 / Dashboard 推送行為。
    /// </summary>
    private async Task ExecuteTaskAsync(
        TaskItem task,
        string executorKey,
        SemaphoreSlim semaphore,
        CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        // task 是從 DequeueAsync 的 scope 返回的 detached entity（那個 scope 已 dispose）。
        // 重新附加到當前 scope 的 DbContext，讓 UpdateStatus / AddLog 的變更能正確被追蹤並存入 DB。
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Attach(task);

        // 取得 TaskGroup（owner / repo 資訊）
        if (task.GroupId is null)
        {
            logger.LogError("AgentQueueProcessor：TaskItem {Id} 沒有 GroupId，略過執行", task.Id);
            semaphore.Release();
            return;
        }

        var group = await taskRepo.GetGroupByIdAsync(task.GroupId.Value, ct);
        if (group is null)
        {
            logger.LogError("AgentQueueProcessor：找不到 TaskGroup {GroupId}（Task={Id}）", task.GroupId, task.Id);
            semaphore.Release();
            return;
        }

        var owner = _gitHub.Owner;
        var repo  = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;

        // 推送 Dashboard（running）
        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = task.Id,
            GroupId   = group.Id,
            Title     = task.Title,
            AgentName = task.AssignedAgent,
            Status    = "running"
        });

        // 通知 Agent 頻道
        var agentChannelName = GetAgentChannelName(task.AssignedAgent);
        var agentChannel     = FindChannel(agentChannelName);
        if (agentChannel is not null)
            await agentChannel.SendMessageAsync(
                $"🚀 CEO Orchestrator 自動觸發：**{task.AssignedAgent}** 開始執行任務《{group.Title}》");

        // 解析 IAgentExecutor
        var executor = scope.ServiceProvider.GetKeyedService<IAgentExecutor>(executorKey);
        if (executor is null)
        {
            logger.LogError("AgentQueueProcessor：找不到 Agent 實作：{Agent}（executorKey={Key}）",
                task.AssignedAgent, executorKey);
            taskRepo.UpdateStatus(task, "failed");
            await taskRepo.SaveAsync(CancellationToken.None);
            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = task.AssignedAgent,
                Status           = "error",
                CurrentTaskTitle = $"找不到 Agent 實作：{task.AssignedAgent}"
            });
            await queueService.ClearQueueStatusAsync(task.Id, CancellationToken.None);
            semaphore.Release();
            return;
        }

        // 建立 linked CTS，供外部取消時 kill subprocess
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        queueService.RegisterCts(task.Id, linkedCts);

        try
        {
            var rules = await rulesService.GetRulesAsync(executorKey);

            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = task.AssignedAgent,
                Status           = "running",
                CurrentTaskTitle = group.Title
            });

            var result = await executor.ExecuteTaskAsync(task, owner, repo, rules, linkedCts.Token);

            var finalStatus = result.Success ? "done" : "failed";
            taskRepo.UpdateStatus(task, finalStatus);
            taskRepo.AddLog(new TaskLog
            {
                TaskId = task.Id,
                Agent  = task.AssignedAgent,
                Step   = result.Summary,
                Status = finalStatus,
            });
            await taskRepo.SaveAsync(CancellationToken.None);

            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = task.Id,
                GroupId   = group.Id,
                Title     = task.Title,
                AgentName = task.AssignedAgent,
                Status    = finalStatus
            });
            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = task.AssignedAgent,
                Status           = result.Success ? "idle" : "error",
                CurrentTaskTitle = result.Success ? null : result.Summary
            });

            // Discord embed
            var embed = new EmbedBuilder()
                .WithTitle(result.Success
                    ? $"✅ {task.AssignedAgent} Agent 執行完成（Orchestrator）"
                    : $"❌ {task.AssignedAgent} Agent 執行失敗（Orchestrator）")
                .WithColor(result.Success ? Color.Green : Color.Red)
                .AddField("任務", task.Title)
                .AddField("摘要", result.Summary)
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (!string.IsNullOrEmpty(result.OutputUrl))
                embed.AddField("連結", result.OutputUrl);

            if (agentChannel is not null)
                await agentChannel.SendMessageAsync(embed: embed.Build());

            // fire-and-forget：觸發 HandleAgentCompletedAsync（決定下一步）
            var prUrl         = result.OutputUrl ?? group.DevPrUrl ?? "";
            var workflowKey   = task.WorkflowAgentKey ?? task.AssignedAgent;
            _ = Task.Run(async () =>
            {
                try
                {
                    await taskGroupService.HandleAgentCompletedAsync(
                        group.Id, workflowKey, result, prUrl, appLifetime.ApplicationStopping);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "AgentQueueProcessor：遞迴觸發下一步失敗（Group={Id}）", group.Id);
                }
            }, appLifetime.ApplicationStopping);
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // 外部取消（TaskGroupService.CancelAsync 呼叫），標記 cancelled
            logger.LogInformation("AgentQueueProcessor：Agent {Agent}（Task={Id}）被外部取消",
                task.AssignedAgent, task.Id);
            taskRepo.UpdateStatus(task, "cancelled");
            await taskRepo.SaveAsync(CancellationToken.None);
            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = task.Id,
                GroupId   = group.Id,
                Title     = task.Title,
                AgentName = task.AssignedAgent,
                Status    = "cancelled"
            });
            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName = task.AssignedAgent,
                Status    = "idle"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AgentQueueProcessor：Agent {Agent} 執行失敗（Task={Id}）",
                task.AssignedAgent, task.Id);
            taskRepo.UpdateStatus(task, "failed");
            taskRepo.AddLog(new TaskLog
            {
                TaskId = task.Id,
                Agent  = task.AssignedAgent,
                Step   = ex.Message,
                Status = "failed",
            });
            await taskRepo.SaveAsync(CancellationToken.None);
            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = task.Id,
                GroupId   = group.Id,
                Title     = task.Title,
                AgentName = task.AssignedAgent,
                Status    = "failed"
            });
            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = task.AssignedAgent,
                Status           = "error",
                CurrentTaskTitle = ex.Message
            });
        }
        finally
        {
            queueService.TryRemoveCts(task.Id, out _);
            await queueService.ClearQueueStatusAsync(task.Id, CancellationToken.None);

            // 27b-1：安全網 — 若 /stop-all 在任務執行中觸發，任務完成後自動轉為 stopped
            var stateAfter = await appSettings.GetAsync($"AgentState:{executorKey}") ?? "active";
            if (stateAfter == "stopping")
            {
                await appSettings.SetAsync($"AgentState:{executorKey}", "stopped");
                _ = pushService.PushQueueUpdateAsync();
            }

            semaphore.Release();
        }
    }

    // ---- 27a-3：Crash Recovery ----

    /// <summary>
    /// 啟動時掃描 QueueStatus = "processing" 的任務，重設為 "queued" 讓佇列重新處理。
    /// 這些任務是上次系統關閉時被中斷的執行中任務。
    /// </summary>
    private async Task RecoverStuckTasksAsync(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db                = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stuck = await db.Set<TaskItem>()
            .Where(t => t.QueueStatus == "processing")
            .ToListAsync(ct);

        if (stuck.Count == 0) return;

        foreach (var task in stuck)
        {
            task.QueueStatus = "queued";
            task.Status      = "queued";
        }

        await db.SaveChangesAsync(ct);
        logger.LogWarning("AgentQueueProcessor Crash Recovery：{N} 個任務重新排入佇列", stuck.Count);

        // 喚醒主迴圈立即處理
        queueService.WaitForSignal(0);
    }

    // ---- 27a-3：Graceful Shutdown ----

    /// <summary>
    /// 等待所有 semaphore 可取得（代表無任務在執行），或 timeout 後強制取消。
    /// </summary>
    private async Task WaitForRunningTasksAsync(TimeSpan timeout)
    {
        logger.LogInformation("AgentQueueProcessor：開始 Graceful Shutdown（timeout={Timeout}s）",
            timeout.TotalSeconds);

        using var timeoutCts = new CancellationTokenSource(timeout);

        try
        {
            // 嘗試取得所有 semaphore（若都能取得，代表所有任務完成）
            var tasks = _semaphores.Values
                .Select(s => s.WaitAsync(timeoutCts.Token))
                .ToArray();

            await Task.WhenAll(tasks);
            logger.LogInformation("AgentQueueProcessor：所有任務已完成，Graceful Shutdown 成功");

            // 釋放取得的 semaphore
            foreach (var s in _semaphores.Values) s.Release();
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("AgentQueueProcessor：Graceful Shutdown timeout，強制取消所有執行中任務");

            // 強制取消所有執行中的 CTS
            foreach (var (semaphoreKey, agentNames) in SemaphoreGroups)
            {
                // 找出所有正在執行的 TaskItem 並取消（best effort）
                await using var scope = serviceProvider.CreateAsyncScope();
                var db                = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var processing        = await db.Set<TaskItem>()
                    .Where(t => agentNames.Contains(t.AssignedAgent) && t.QueueStatus == "processing")
                    .Select(t => t.Id)
                    .ToListAsync(CancellationToken.None);

                foreach (var taskId in processing)
                    queueService.TryCancel(taskId);
            }
        }
    }

    // ---- 輔助方法 ----

    private string GetAgentChannelName(string agentName) => agentName switch
    {
        AgentNames.Dev          => _discord.Channels.DevChannel,
        "Dev_plan"              => _discord.Channels.DevChannel,
        AgentNames.Ops          => _discord.Channels.OpsChannel,
        AgentNames.Qa           => _discord.Channels.QaChannel,
        AgentNames.Doc          => _discord.Channels.DocChannel,
        AgentNames.Requirements => _discord.Channels.RequirementsChannel,
        AgentNames.Reviewer     => _discord.Channels.ReviewerChannel,
        AgentNames.Release      => _discord.Channels.ReleaseChannel,
        AgentNames.Designer     => _discord.Channels.DesignerChannel,
        _                       => _discord.Channels.TaskUpdates
    };

    private IMessageChannel? FindChannel(string channelName)
    {
        if (!ulong.TryParse(_discord.GuildId, out var guildId)) return null;
        return discordClient.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }
}
