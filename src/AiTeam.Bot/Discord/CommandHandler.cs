using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Bot.Notion;
using AiTeam.Bot.Ops;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Discord;

/// <summary>
/// 負責註冊與分派 Discord 斜線指令。
/// </summary>
public class CommandHandler(
    DiscordSocketClient client,
    IOptions<DiscordSettings> settings,
    IOptions<GitHubSettings> gitHubSettings,
    IServiceProvider serviceProvider,
    NotionService notionService,
    OpsAgentService opsAgentService,
    ILogger<CommandHandler> logger)
{
    private readonly DiscordSettings _settings = settings.Value;
    private readonly GitHubSettings _gitHubSettings = gitHubSettings.Value;

    // 等待確認的 CEO 決策暫存（messageId → CeoResponse）
    private readonly Dictionary<ulong, PendingConfirmation> _pendingConfirmations = [];

    /// <summary>
    /// 向 Guild 註冊所有斜線指令，並訂閱互動事件。
    /// </summary>
    public async Task RegisterCommandsAsync()
    {
        if (!ulong.TryParse(_settings.GuildId, out var guildId))
        {
            logger.LogError("GuildId 設定無效：{GuildId}", _settings.GuildId);
            return;
        }

        var guild = client.GetGuild(guildId);
        if (guild is null)
        {
            logger.LogError("找不到 Guild（GuildId={GuildId}），請確認 Bot 已加入伺服器", guildId);
            return;
        }

        var commands = new ApplicationCommandProperties[]
        {
            new SlashCommandBuilder()
                .WithName("task")
                .WithDescription("指派任務給 AI 團隊")
                .AddOption("project", ApplicationCommandOptionType.String, "專案名稱", isRequired: true)
                .AddOption("description", ApplicationCommandOptionType.String, "任務描述", isRequired: true)
                .Build(),

            new SlashCommandBuilder()
                .WithName("reload-rules")
                .WithDescription("強制重新載入 Notion 規則（清除 Cache）")
                .Build(),

            new SlashCommandBuilder()
                .WithName("status")
                .WithDescription("查詢各 Agent 目前狀態")
                .Build(),
        };

        await guild.BulkOverwriteApplicationCommandAsync(commands);
        logger.LogInformation("斜線指令已向 Guild {GuildId} 註冊完成", guildId);

        client.SlashCommandExecuted += OnSlashCommandAsync;
        client.ButtonExecuted += OnButtonExecutedAsync;
    }

    #region 斜線指令分派

    private async Task OnSlashCommandAsync(SocketSlashCommand command)
    {
        logger.LogInformation("收到指令 /{CommandName} 來自 {User}", command.CommandName, command.User.Username);

        await command.DeferAsync();

        try
        {
            await (command.CommandName switch
            {
                "task"         => HandleTaskCommandAsync(command),
                "reload-rules" => HandleReloadRulesAsync(command),
                "status"       => HandleStatusAsync(command),
                _              => command.FollowupAsync("未知指令")
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "處理指令 /{CommandName} 時發生錯誤", command.CommandName);
            await command.FollowupAsync("處理指令時發生錯誤，請查看 log。");
        }
    }

    #endregion

    #region 各指令處理

    private async Task HandleTaskCommandAsync(SocketSlashCommand command)
    {
        var project = command.Data.Options.First(o => o.Name == "project").Value.ToString()!;
        var description = command.Data.Options.First(o => o.Name == "description").Value.ToString()!;

        await using var scope = serviceProvider.CreateAsyncScope();
        var ceoService = scope.ServiceProvider.GetRequiredService<CeoAgentService>();

        var rules = await notionService.GetRulesAsync();
        var agentList = new[] { "Dev", "Ops" };

        // 呼叫 CEO Agent 分析
        var ceoResponse = await ceoService.ProcessAsync(
            description, project, agentList, rules);

        // 雙層確認 — 第一層：CEO 回報決策給老闆審核
        if (ceoResponse.RequireConfirmation && ceoResponse.Action != "reply")
        {
            var confirmMessage = await command.FollowupAsync(
                embed: BuildCeoDecisionEmbed(ceoResponse, project),
                components: BuildConfirmButtons());

            // 注意：不存 TaskRepository，scope 結束後就 dispose，改在 confirm_yes 開新 scope
            _pendingConfirmations[confirmMessage.Id] = new PendingConfirmation(
                ceoResponse, project, description);
        }
        else
        {
            await command.FollowupAsync(ceoResponse.Reply);
        }
    }

    private async Task HandleReloadRulesAsync(SocketSlashCommand command)
    {
        notionService.InvalidateCache();
        await command.FollowupAsync("規則 Cache 已清除，下次任務將重新從 Notion 載入規則。");
    }

    private async Task HandleStatusAsync(SocketSlashCommand command)
    {
        // TODO: Stage 3 — 從資料庫查詢各 Agent 執行中任務數
        await command.FollowupAsync("CEO / Dev / Ops — 所有 Agent 待機中。");
    }

    #endregion

    #region 雙層確認機制

    private async Task OnButtonExecutedAsync(SocketMessageComponent interaction)
    {
        if (!_pendingConfirmations.TryGetValue(interaction.Message.Id, out var pending))
        {
            await interaction.RespondAsync("此確認已過期或不存在。", ephemeral: true);
            return;
        }

        _pendingConfirmations.Remove(interaction.Message.Id);

        if (interaction.Data.CustomId == "confirm_yes")
        {
            await interaction.DeferAsync();

            try
            {
                // 開新 scope 儲存任務（原 HandleTaskCommandAsync 的 scope 已 dispose）
                await using var scope = serviceProvider.CreateAsyncScope();
                var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();

                var task = new TaskItem
                {
                    Title = pending.CeoResponse.Task?.Title ?? pending.Description,
                    TriggeredBy = "Discord",
                    AssignedAgent = pending.CeoResponse.TargetAgent ?? "CEO",
                    Status = "pending"
                };
                taskRepo.Add(task);
                await taskRepo.SaveAsync();

                // 第二層確認：執行層 Agent 說明即將執行的操作
                var agentPlanEmbed = BuildAgentPlanEmbed(pending.CeoResponse, task.Id);
                var agentConfirmMsg = await interaction.FollowupAsync(
                    embed: agentPlanEmbed,
                    components: BuildConfirmButtons("exec_yes", "exec_no"));

                _pendingConfirmations[agentConfirmMsg.Id] = pending with { TaskId = task.Id };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "confirm_yes 處理失敗");
                await interaction.FollowupAsync("❌ 建立任務時發生錯誤，請查看 log。");
            }
        }
        else if (interaction.Data.CustomId == "exec_yes")
        {
            await interaction.DeferAsync();
            await interaction.FollowupAsync(
                $"⏳ {pending.CeoResponse.TargetAgent} Agent 開始執行，完成後通知 #{_settings.Channels.TaskUpdates}。");

            _ = Task.Run(async () =>
            {
                try { await ExecuteAgentTaskAsync(pending); }
                catch (Exception ex)
                {
                    logger.LogError(ex, "背景 Agent 執行失敗（TaskId={TaskId}）", pending.TaskId);
                }
            }, CancellationToken.None);
        }
        else // confirm_no 或 exec_no
        {
            await interaction.RespondAsync("❌ 已取消。");
        }
    }

    #endregion

    #region Agent 執行

    private async Task ExecuteAgentTaskAsync(PendingConfirmation pending)
    {
        var owner = _gitHubSettings.Owner;
        var repo  = pending.Project;

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var task = await taskRepo.GetByIdAsync(pending.TaskId);
        if (task is null)
        {
            logger.LogError("找不到 TaskItem（Id={Id}）", pending.TaskId);
            return;
        }

        taskRepo.UpdateStatus(task, "running");
        await taskRepo.SaveAsync();

        var notifyChannel = FindChannel(_settings.Channels.TaskUpdates);

        try
        {
            if (pending.CeoResponse.TargetAgent == "Dev")
            {
                var devAgent = scope.ServiceProvider.GetRequiredService<DevAgentService>();
                var rules    = await notionService.GetRulesAsync();
                var plan     = await devAgent.BuildPlanAsync(task, owner, repo, rules);
                var prUrl    = await devAgent.ExecuteAsync(task, plan, owner, repo);

                taskRepo.UpdateStatus(task, "done");
                await taskRepo.SaveAsync();

                var embed = new EmbedBuilder()
                    .WithTitle("✅ Dev Agent 執行完成")
                    .WithColor(Color.Green)
                    .AddField("任務", task.Title)
                    .AddField("PR", prUrl)
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();
                if (notifyChannel is not null)
                    await notifyChannel.SendMessageAsync(embed: embed);
            }
            else // Ops 或其他
            {
                // TODO: Ops 任務執行邏輯
                await opsAgentService.AlertAsync(
                    $"📋 Ops 任務待執行：{task.Title}（專案：{repo}）");
                taskRepo.UpdateStatus(task, "done");
                await taskRepo.SaveAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent 執行失敗：{Title}", task.Title);
            taskRepo.UpdateStatus(task, "failed");
            await taskRepo.SaveAsync();

            var alertChannel = FindChannel(_settings.Channels.Alerts);
            if (alertChannel is not null)
                await alertChannel.SendMessageAsync(
                    $"🚨 **{pending.CeoResponse.TargetAgent} Agent 失敗**\n任務：{task.Title}\n錯誤：{ex.Message}");
        }
    }

    private IMessageChannel? FindChannel(string channelName)
    {
        if (!ulong.TryParse(_settings.GuildId, out var guildId)) return null;
        return client.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }

    #endregion

    #region Embed 與按鈕建構

    private static Embed BuildCeoDecisionEmbed(CeoResponse response, string project)
    {
        var builder = new EmbedBuilder()
            .WithTitle("📋 CEO 決策 — 請確認")
            .WithColor(Color.Blue)
            .AddField("回應", response.Reply)
            .AddField("動作", response.Action, inline: true)
            .AddField("負責 Agent", response.TargetAgent ?? "—", inline: true)
            .AddField("專案", project, inline: true);

        if (response.Task is not null)
        {
            builder
                .AddField("任務標題", response.Task.Title)
                .AddField("優先度", response.Task.Priority, inline: true)
                .AddField("描述", response.Task.Description);
        }

        return builder.Build();
    }

    private static Embed BuildAgentPlanEmbed(CeoResponse response, Guid taskId)
        => new EmbedBuilder()
            .WithTitle($"🤖 {response.TargetAgent} Agent — 即將執行")
            .WithColor(Color.Orange)
            .AddField("任務", response.Task?.Title ?? "—")
            .AddField("描述", response.Task?.Description ?? "—")
            .AddField("任務 ID", taskId.ToString())
            .WithFooter("確認後開始執行，取消則中止。")
            .Build();

    private static MessageComponent BuildConfirmButtons(
        string yesId = "confirm_yes",
        string noId = "confirm_no")
        => new ComponentBuilder()
            .WithButton("✅ 確認", yesId, ButtonStyle.Success)
            .WithButton("❌ 取消", noId, ButtonStyle.Danger)
            .Build();

    #endregion
}

/// <summary>
/// 等待確認的暫存資料。
/// </summary>
internal record PendingConfirmation(
    CeoResponse CeoResponse,
    string Project,
    string Description,
    Guid TaskId = default);
