using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using AiTeam.Shared.ViewModels;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Orchestration;

/// <summary>
/// Stage 10：任務群組管理與自動流程推進服務。
/// 負責：建立 TaskGroup、並行觸發 Agent、任務完成後查流程表決定下一步。
/// 與 CommandHandler 互補：CommandHandler 處理 Discord 互動（按鈕、embed）；
/// TaskGroupService 處理後台的自動流程推進（不需要老闆介入的部分）。
/// </summary>
public class TaskGroupService(
    IServiceProvider serviceProvider,
    DiscordSocketClient discordClient,
    IOptions<DiscordSettings> discordSettings,
    IOptions<GitHubSettings> gitHubSettings,
    RulesService rulesService,
    WorkflowEngine workflowEngine,
    ILogger<TaskGroupService> logger)
{
    private readonly DiscordSettings _discord    = discordSettings.Value;
    private readonly GitHubSettings  _gitHub     = gitHubSettings.Value;

    // ---- 任務群組建立 ----

    /// <summary>
    /// 建立任務群組並存入 DB。
    /// </summary>
    public async Task<TaskGroup> CreateGroupAsync(
        string title,
        string project,
        WorkflowType workflowType,
        string? issueUrlsJson = null,
        string? uiSpecPath    = null,
        CancellationToken cancellationToken = default)
    {
        await using var scope   = serviceProvider.CreateAsyncScope();
        var taskRepo            = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var group = new TaskGroup
        {
            Title        = title,
            Project      = project,
            Status       = "running",
            WorkflowType = workflowType == WorkflowType.NewFeature ? "new_feature" : "bug_fix",
            IssueUrls    = issueUrlsJson,
            UiSpecPath   = uiSpecPath,
        };

        taskRepo.AddGroup(group);
        await taskRepo.SaveAsync(cancellationToken);

        logger.LogInformation("TaskGroup 建立：{Id}（{Title}，{Type}）",
            group.Id, group.Title, group.WorkflowType);

        return group;
    }

    // ---- 自動流程推進 ----

    /// <summary>
    /// Agent 執行完成後，查 WorkflowEngine 的流程表決定下一步並執行。
    /// 此方法由 CommandHandler.ExecuteAgentTaskAsync 在 Agent 完成後呼叫（背景，不 await）。
    /// </summary>
    public async Task HandleAgentCompletedAsync(
        Guid groupId,
        string completedAgent,
        AgentExecutionResult result,
        string devPrUrl = "",
        CancellationToken cancellationToken = default)
    {
        if (groupId == Guid.Empty) return;

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var group = await taskRepo.GetGroupByIdAsync(groupId, cancellationToken);
        if (group is null)
        {
            logger.LogWarning("HandleAgentCompleted：找不到 TaskGroup（Id={Id}）", groupId);
            return;
        }

        // 更新 DevPrUrl（若本次有 PR 產出）
        if (!string.IsNullOrWhiteSpace(devPrUrl) && string.IsNullOrWhiteSpace(group.DevPrUrl))
        {
            group.DevPrUrl = devPrUrl;
            await taskRepo.SaveAsync(cancellationToken);
        }

        var workflowType = group.WorkflowType == "new_feature"
            ? WorkflowType.NewFeature
            : WorkflowType.BugFix;

        var decision = workflowEngine.GetDecision(
            workflowType, completedAgent, result, group.FixIteration);

        logger.LogInformation(
            "WorkflowEngine 決策：Group={Id}，completedAgent={Agent}，action={Action}",
            groupId, completedAgent, decision.Action);

        switch (decision.Action)
        {
            case NextAction.FireAgents:
                // 若為修復迭代，遞增計數
                if (decision.NextSteps.Any(s => s.IsFixLoop))
                {
                    group.FixIteration++;
                    await taskRepo.SaveAsync(cancellationToken);
                }
                await FireStepsAsync(group, decision.NextSteps, cancellationToken);
                break;

            case NextAction.NotifyBossMerge:
                taskRepo.UpdateGroupStatus(group, "done");
                await taskRepo.SaveAsync(cancellationToken);
                await NotifyBossMergeAsync(group, cancellationToken);
                break;

            case NextAction.NotifyBossIntervention:
                taskRepo.UpdateGroupStatus(group, "failed");
                await taskRepo.SaveAsync(cancellationToken);
                await NotifyBossInterventionAsync(group, cancellationToken);
                break;

            case NextAction.Nothing:
                break;
        }
    }

    // ---- 觸發 Agent 執行 ----

    /// <summary>
    /// 依流程步驟清單建立 TaskItem 並並行觸發 Agent 執行。
    /// </summary>
    public async Task FireStepsAsync(
        TaskGroup group,
        IReadOnlyList<WorkflowStep> steps,
        CancellationToken cancellationToken = default)
    {
        if (steps.Count == 0) return;

        var parallel = steps.Where(s => s.RunInParallel || steps.Count == 1).ToList();
        var serial   = steps.Where(s => !s.RunInParallel && steps.Count > 1).ToList();

        // 並行步驟：同時觸發
        if (parallel.Count > 0)
        {
            var tasks = parallel.Select(step =>
                FireOneStepAsync(group, step, cancellationToken));
            await Task.WhenAll(tasks);
        }

        // 序列步驟（目前流程表裡不存在，保留擴充彈性）
        foreach (var step in serial)
            await FireOneStepAsync(group, step, cancellationToken);
    }

    private async Task FireOneStepAsync(
        TaskGroup group,
        WorkflowStep step,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService       = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var owner = _gitHub.Owner;
        var repo  = string.IsNullOrWhiteSpace(group.Project)
            ? _gitHub.DefaultRepo
            : group.Project;

        // 建立 TaskItem
        var description = BuildTaskDescription(group, step);
        var taskItem = new TaskItem
        {
            Title         = $"{group.Title}（{step.AgentName}）",
            Description   = description,
            TriggeredBy   = "Orchestrator",
            AssignedAgent = step.AgentName,
            Status        = "running",
            GroupId       = group.Id,
        };

        taskRepo.Add(taskItem);
        await taskRepo.SaveAsync(cancellationToken);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = taskItem.Id,
            Title     = taskItem.Title,
            AgentName = taskItem.AssignedAgent,
            Status    = "running"
        });

        // 通知 Agent 頻道
        var agentChannelName = GetAgentChannelName(step.AgentName);
        var agentChannel     = FindChannel(agentChannelName);
        if (agentChannel is not null)
            await agentChannel.SendMessageAsync(
                $"🚀 CEO Orchestrator 自動觸發：**{step.AgentName}** 開始執行任務《{group.Title}》");

        // 執行 Agent
        var executor = scope.ServiceProvider.GetKeyedService<IAgentExecutor>(step.AgentName);
        if (executor is null)
        {
            logger.LogError("TaskGroupService：找不到 Agent 實作：{Agent}", step.AgentName);
            taskRepo.UpdateStatus(taskItem, "failed");
            await taskRepo.SaveAsync(cancellationToken);
            return;
        }

        try
        {
            var rules  = await rulesService.GetRulesAsync(step.AgentName);
            var result = await executor.ExecuteTaskAsync(taskItem, owner, repo, rules, cancellationToken);

            var finalStatus = result.Success ? "done" : "failed";
            taskRepo.UpdateStatus(taskItem, finalStatus);
            await taskRepo.SaveAsync(cancellationToken);

            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = taskItem.Id,
                Title     = taskItem.Title,
                AgentName = taskItem.AssignedAgent,
                Status    = finalStatus
            });

            // 推送結果到頻道
            var embed = new EmbedBuilder()
                .WithTitle(result.Success
                    ? $"✅ {step.AgentName} Agent 執行完成（Orchestrator）"
                    : $"❌ {step.AgentName} Agent 執行失敗（Orchestrator）")
                .WithColor(result.Success ? Color.Green : Color.Red)
                .AddField("任務", taskItem.Title)
                .AddField("摘要", result.Summary)
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (!string.IsNullOrEmpty(result.OutputUrl))
                embed.AddField("連結", result.OutputUrl);

            if (agentChannel is not null)
                await agentChannel.SendMessageAsync(embed: embed.Build());

            // 遞迴觸發下一步（背景）
            _ = Task.Run(async () =>
            {
                try
                {
                    var prUrl = result.OutputUrl ?? group.DevPrUrl ?? "";
                    // 更新 completedAgent：若為修復迭代，用 "Dev" 讓 WorkflowEngine 正確查表
                    var agentKey = step.IsFixLoop && step.AgentName == AgentNames.Dev
                        ? "Dev_fix"
                        : step.AgentName;
                    await HandleAgentCompletedAsync(group.Id, agentKey, result, prUrl);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TaskGroupService：遞迴觸發下一步失敗（Group={Id}）", group.Id);
                }
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TaskGroupService：Agent {Agent} 執行失敗（Task={Id}）",
                step.AgentName, taskItem.Id);
            taskRepo.UpdateStatus(taskItem, "failed");
            await taskRepo.SaveAsync(cancellationToken);

            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = taskItem.Id,
                Title     = taskItem.Title,
                AgentName = taskItem.AssignedAgent,
                Status    = "failed"
            });
        }
    }

    // ---- 通知老闆 ----

    private async Task NotifyBossMergeAsync(TaskGroup group, CancellationToken cancellationToken)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null) return;

        var prLink = string.IsNullOrWhiteSpace(group.DevPrUrl)
            ? "（無 PR 連結）"
            : group.DevPrUrl;

        await ceoChannel.SendMessageAsync(
            $"✅ **{group.Title}** — Vera 審查通過，無 🔴 問題！\n" +
            $"PR：{prLink}\n" +
            $"請確認後即可合併 👆");

        logger.LogInformation("TaskGroup {Id} 通知老闆可以 merge PR", group.Id);
    }

    private async Task NotifyBossInterventionAsync(TaskGroup group, CancellationToken cancellationToken)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null) return;

        await ceoChannel.SendMessageAsync(
            $"⚠️ **{group.Title}** — Vera 在 {group.FixIteration} 次修復後仍發現 🔴 問題，需要您介入處理。\n" +
            $"PR：{group.DevPrUrl ?? "（無）"}");

        logger.LogWarning("TaskGroup {Id} 修復次數超限（{Count} 次），升級給老闆", group.Id, group.FixIteration);
    }

    // ---- 輔助方法 ----

    /// <summary>
    /// 組建 TaskItem.Description，附帶 CEO 傳遞給 Dev 的上下文 metadata。
    /// </summary>
    private static string BuildTaskDescription(TaskGroup group, WorkflowStep step)
    {
        var desc = group.Title;

        if (step.AgentName is AgentNames.Dev or AgentNames.Reviewer or AgentNames.Qa or AgentNames.Doc)
        {
            var parts = new List<string> { desc };

            if (!string.IsNullOrWhiteSpace(group.DevPrUrl))
                parts.Add($"PR 連結：{group.DevPrUrl}");

            // 附上 metadata block（Dev 制定計畫時解析使用）
            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(group.IssueUrls))
                meta.Add($"issue_urls: {group.IssueUrls}");
            if (!string.IsNullOrWhiteSpace(group.UiSpecPath))
                meta.Add($"ui_spec_path: {group.UiSpecPath}");
            if (step.IsFixLoop)
                meta.Add("fix_loop: true");

            if (meta.Count > 0)
            {
                parts.Add("---");
                parts.AddRange(meta);
                parts.Add("---");
            }

            return string.Join("\n", parts);
        }

        return desc;
    }

    private string GetAgentChannelName(string agentName) => agentName switch
    {
        AgentNames.Dev          => _discord.Channels.DevChannel,
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
