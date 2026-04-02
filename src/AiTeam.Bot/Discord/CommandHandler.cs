using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Bot.Notion;
using AiTeam.Shared.Constants;
using AiTeam.Shared.ViewModels;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Discord;

/// <summary>
/// 負責註冊斜線指令，以及監聽各 Agent 頻道的自然語言訊息並路由。
/// </summary>
public class CommandHandler(
    DiscordSocketClient client,
    IOptions<DiscordSettings> settings,
    IOptions<GitHubSettings> gitHubSettings,
    IServiceProvider serviceProvider,
    NotionService notionService,
    ConversationContextStore contextStore,
    ILogger<CommandHandler> logger)
{
    private readonly DiscordSettings _settings = settings.Value;
    private readonly GitHubSettings _gitHubSettings = gitHubSettings.Value;

    // 等待確認的 CEO 決策暫存（messageId → PendingConfirmation）
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
                .AddOption("image", ApplicationCommandOptionType.Attachment, "（選用）附圖截圖", isRequired: false)
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
        client.ButtonExecuted        += OnButtonExecutedAsync;
        client.MessageReceived       += OnMessageReceivedAsync;
    }

    #region 自然語言訊息路由（Stage 7）

    /// <summary>
    /// 監聽頻道訊息，將 CEO 頻道與各 Agent 頻道的訊息路由到對應的處理邏輯。
    /// </summary>
    private async Task OnMessageReceivedAsync(SocketMessage rawMessage)
    {
        // 忽略 Bot 自己的訊息與系統訊息
        if (rawMessage is not SocketUserMessage msg) return;
        if (msg.Author.IsBot) return;

        // 若 MessageContent Intent 未啟用，content 會是空字串，靜默跳過
        if (string.IsNullOrWhiteSpace(msg.CleanContent)) return;

        var channelName = (msg.Channel as SocketTextChannel)?.Name ?? "";
        var isCeoChannel = channelName.Equals(_settings.Channels.CeoChannel, StringComparison.OrdinalIgnoreCase);

        var channelAgentMap = BuildChannelAgentMap();
        var isAgentChannel  = channelAgentMap.TryGetValue(channelName, out var targetAgent);

        if (!isCeoChannel && !isAgentChannel) return;

        using var typing = msg.Channel.EnterTypingState();

        try
        {
            if (isCeoChannel)
                await HandleCeoChannelMessageAsync(msg);
            else
                await HandleDirectAgentChannelMessageAsync(msg, targetAgent!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "處理自然語言訊息時發生錯誤（頻道={Channel}）", channelName);
            try { await msg.Channel.SendMessageAsync("❌ 處理訊息時發生錯誤，請查看 log。"); }
            catch { /* 發送錯誤訊息失敗時靜默忽略 */ }
        }
    }

    /// <summary>
    /// 在 CEO 頻道（#victoria-ceo）的自然語言處理。
    /// 保留對話歷史供多輪對話使用，支援 CEO 反問機制。
    /// </summary>
    private async Task HandleCeoChannelMessageAsync(SocketUserMessage msg)
    {
        var history = contextStore.GetHistory(msg.Channel.Id);

        // 下載圖片附件（若有）
        var images = new List<ImageAttachment>();
        foreach (var attachment in msg.Attachments)
        {
            if (attachment.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
                continue;
            try
            {
                using var http  = new HttpClient();
                var bytes       = await http.GetByteArrayAsync(attachment.Url);
                images.Add(new ImageAttachment(Convert.ToBase64String(bytes), attachment.ContentType));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "附圖下載失敗，略過");
            }
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var ceoService  = scope.ServiceProvider.GetRequiredService<CeoAgentService>();
        var agentRepo   = scope.ServiceProvider.GetRequiredService<AgentRepository>();

        var rules        = await notionService.GetRulesAsync();
        var activeAgents = await agentRepo.GetActiveExecutorAgentsAsync();
        var agentList    = activeAgents.Select(a => new AgentDescriptor(a.Name, a.Description)).ToList();

        // 從歷史對話嘗試提取專案名稱（取最後一次明確提到的專案）
        var projectName = ExtractProjectFromHistory(history, msg.CleanContent);

        var ceoResponse = await ceoService.ProcessAsync(
            msg.CleanContent, projectName, agentList, rules,
            images: images.Count > 0 ? images : null,
            history: history);

        // 防護修正（同 /task 指令邏輯）
        if (!string.IsNullOrWhiteSpace(ceoResponse.TargetAgent) && ceoResponse.Action == "reply")
        {
            logger.LogWarning("CEO 回傳 action=reply 但 target_agent={Agent}，強制修正為 delegate", ceoResponse.TargetAgent);
            ceoResponse.Action = "delegate";
        }

        // 更新對話歷史
        contextStore.AddTurn(msg.Channel.Id, "user",      msg.CleanContent);
        contextStore.AddTurn(msg.Channel.Id, "assistant", ceoResponse.Reply);

        if (ceoResponse.Action == "reply")
        {
            // CEO 反問或純回覆，直接傳送文字，等待老闆下一輪回應
            await msg.Channel.SendMessageAsync(ceoResponse.Reply);
        }
        else
        {
            // 進入確認流程後清除對話歷史（任務已理解，不需繼續累積）
            contextStore.Clear(msg.Channel.Id);

            // 若 CEO 尚未取得專案名稱，以 task.project 補充
            var finalProject = ceoResponse.Task?.Project ?? projectName;

            var confirmMessage = await msg.Channel.SendMessageAsync(
                embed: BuildCeoDecisionEmbed(ceoResponse, finalProject),
                components: BuildConfirmButtons());

            _pendingConfirmations[confirmMessage.Id] = new PendingConfirmation(
                ceoResponse, finalProject, msg.CleanContent);
        }
    }

    /// <summary>
    /// 在各 Agent 專屬頻道（如 #cody-dev）的直接對話處理。
    /// 自動 CC CEO 頻道，並直接路由到對應 Agent 走確認流程。
    /// </summary>
    private async Task HandleDirectAgentChannelMessageAsync(SocketUserMessage msg, string agentName)
    {
        // CC CEO 頻道：通知老闆繞過 CEO 直接找 Agent
        var ceoChannel = FindChannel(_settings.Channels.CeoChannel);
        if (ceoChannel is not null)
        {
            var ccEmbed = new EmbedBuilder()
                .WithTitle($"📋 老闆直接指派給 {agentName} Agent")
                .WithColor(Color.LightGrey)
                .AddField("來源頻道", $"#{msg.Channel.Name}", inline: true)
                .AddField("指派內容", Truncate(msg.CleanContent, 512))
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();
            await ceoChannel.SendMessageAsync(embed: ccEmbed);
        }

        // 建立模擬 CeoResponse 直接走第一層確認流程
        var project = ExtractProjectFromChannelName(msg.Channel.Name);
        var fakeResponse = new CeoResponse
        {
            Action      = "delegate",
            TargetAgent = agentName,
            Reply       = $"老闆直接指示，由 {agentName} Agent 處理。",
            Task        = new CeoTaskPayload
            {
                Title       = TruncateTitle(msg.CleanContent),
                Description = msg.CleanContent,
                Project     = project,
                Priority    = "normal"
            }
        };

        var confirmMessage = await msg.Channel.SendMessageAsync(
            embed: BuildCeoDecisionEmbed(fakeResponse, project),
            components: BuildConfirmButtons());

        _pendingConfirmations[confirmMessage.Id] = new PendingConfirmation(
            fakeResponse, project, msg.CleanContent);
    }

    /// <summary>頻道名稱 → Agent 名稱的對應表。</summary>
    private Dictionary<string, string> BuildChannelAgentMap()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            [_settings.Channels.DevChannel]          = AgentNames.Dev,
            [_settings.Channels.OpsChannel]          = AgentNames.Ops,
            [_settings.Channels.QaChannel]           = AgentNames.Qa,
            [_settings.Channels.DocChannel]          = AgentNames.Doc,
            [_settings.Channels.RequirementsChannel] = AgentNames.Requirements,
            [_settings.Channels.ReviewerChannel]     = AgentNames.Reviewer,
            [_settings.Channels.ReleaseChannel]      = AgentNames.Release,
            [_settings.Channels.DesignerChannel]     = AgentNames.Designer,
        };

    /// <summary>
    /// 從對話歷史中嘗試找出專案名稱（取最後一次明確提到的專案）。
    /// 找不到時回傳空字串，讓 CEO 自行判斷或反問。
    /// </summary>
    private static string ExtractProjectFromHistory(
        IReadOnlyList<ConversationTurn> history, string currentInput)
    {
        // 從最新的一輪往前找，看是否有明確提到專案名稱
        foreach (var turn in history.Reverse())
        {
            if (!string.IsNullOrWhiteSpace(turn.Content))
                return ""; // 讓 CEO 從對話內容自行理解
        }
        return "";
    }

    /// <summary>從 Agent 頻道名稱推測可能的專案名稱（無法確定時回傳空字串）。</summary>
    private static string ExtractProjectFromChannelName(string channelName) => "";

    /// <summary>截斷任務標題為不超過 100 字元的短標題。</summary>
    private static string TruncateTitle(string input)
    {
        if (string.IsNullOrEmpty(input)) return "直接指派任務";
        var firstLine = input.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? input;
        return firstLine.Length <= 100 ? firstLine : firstLine[..97] + "…";
    }

    #endregion

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
        var project     = command.Data.Options.First(o => o.Name == "project").Value.ToString()!;
        var description = command.Data.Options.First(o => o.Name == "description").Value.ToString()!;

        // 處理圖片附件（若有）
        var images = new List<ImageAttachment>();
        var attachmentOption = command.Data.Options.FirstOrDefault(o => o.Name == "image");
        if (attachmentOption?.Value is IAttachment attachment &&
            attachment.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                using var http = new HttpClient();
                var bytes  = await http.GetByteArrayAsync(attachment.Url);
                var base64 = Convert.ToBase64String(bytes);
                images.Add(new ImageAttachment(base64, attachment.ContentType));
                logger.LogInformation("附圖已下載並轉為 Base64（{ContentType}，{Bytes} bytes）",
                    attachment.ContentType, bytes.Length);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "附圖下載失敗，將忽略圖片繼續處理");
            }
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var ceoService  = scope.ServiceProvider.GetRequiredService<CeoAgentService>();
        var agentRepo   = scope.ServiceProvider.GetRequiredService<AgentRepository>();

        var rules        = await notionService.GetRulesAsync();
        var activeAgents = await agentRepo.GetActiveExecutorAgentsAsync();
        var agentList    = activeAgents
            .Select(a => new AgentDescriptor(a.Name, a.Description))
            .ToList();

        // 呼叫 CEO Agent 分析（含圖片）
        var ceoResponse = await ceoService.ProcessAsync(
            description, project, agentList, rules,
            images: images.Count > 0 ? images : null);

        // LLM 有時會把 action 填為 "reply" 但 target_agent 卻有值（應為 delegate）
        // 此處做防護修正，確保行為一致
        if (!string.IsNullOrWhiteSpace(ceoResponse.TargetAgent) && ceoResponse.Action == "reply")
        {
            logger.LogWarning(
                "CEO 回傳 action=reply 但 target_agent={Agent}，強制修正為 delegate",
                ceoResponse.TargetAgent);
            ceoResponse.Action = "delegate";
        }

        // 雙層確認 — 第一層：CEO 回報決策給老闆審核
        // RequireConfirmation 欄位不可信（LLM 可能回傳 false），只要 action 非 reply 就一律顯示確認 Embed
        if (ceoResponse.Action != "reply")
        {
            var confirmMessage = await command.FollowupAsync(
                embed: BuildCeoDecisionEmbed(ceoResponse, project),
                components: BuildConfirmButtons());

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
        await using var scope = serviceProvider.CreateAsyncScope();
        var agentRepo = scope.ServiceProvider.GetRequiredService<AgentRepository>();
        var agents    = await agentRepo.GetActiveExecutorAgentsAsync();

        var agentLines = agents.Count > 0
            ? string.Join("\n", agents.Select(a => $"• {a.Name} — {(a.IsActive ? "啟用" : "停用")}"))
            : "（尚未設定 Agent）";

        await command.FollowupAsync($"**Agent 狀態**\n{agentLines}");
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
                await using var scope  = serviceProvider.CreateAsyncScope();
                var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();

                var task = new TaskItem
                {
                    Title        = pending.CeoResponse.Task?.Title ?? pending.Description,
                    Description  = pending.CeoResponse.Task?.Description,
                    TriggeredBy  = "Discord",
                    AssignedAgent = pending.CeoResponse.TargetAgent ?? "CEO",
                    Status       = "pending"
                };
                taskRepo.Add(task);
                await taskRepo.SaveAsync();

                // 任務建立後立即 push，讓任務中心即時顯示（狀態 pending）
                var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
                await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
                {
                    TaskId    = task.Id,
                    Title     = task.Title,
                    AgentName = task.AssignedAgent,
                    Status    = task.Status
                });

                // 第二層確認：執行層 Agent 說明即將執行的操作
                var agentPlanEmbed  = BuildAgentPlanEmbed(pending.CeoResponse, task.Id);
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

            // Requirements Agent 有第三層確認：先展示 Issue 清單，讓老闆確認後才建立
            if (pending.CeoResponse.TargetAgent == AgentNames.Requirements)
            {
                await ShowRequirementsPreviewAsync(interaction, pending);
            }
            else
            {
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
        }
        else if (interaction.Data.CustomId == "req_yes")
        {
            // 第三層確認通過：根據已分析的 Issue 清單實際建立
            await interaction.DeferAsync();
            await interaction.FollowupAsync(
                $"⏳ Requirements Agent 開始建立 {pending.PreviewIssues?.Count ?? 0} 個 Issues，完成後通知 #{_settings.Channels.TaskUpdates}。");

            _ = Task.Run(async () =>
            {
                try { await ExecuteRequirementsFromPreviewAsync(pending); }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Requirements 背景執行失敗（TaskId={TaskId}）", pending.TaskId);
                }
            }, CancellationToken.None);
        }
        else // confirm_no、exec_no、req_no
        {
            await interaction.RespondAsync("❌ 已取消。");
        }
    }

    #endregion

    #region Requirements 第三層確認

    /// <summary>
    /// exec_yes 後，針對 Requirements Agent 先做 LLM 分析並展示 Issue 預覽清單。
    /// </summary>
    private async Task ShowRequirementsPreviewAsync(
        SocketMessageComponent interaction,
        PendingConfirmation pending)
    {
        try
        {
            await using var scope   = serviceProvider.CreateAsyncScope();
            var taskRepo            = scope.ServiceProvider.GetRequiredService<TaskRepository>();
            var reqService          = scope.ServiceProvider.GetRequiredService<RequirementsAgentService>();

            // TaskItem 已在 confirm_yes 時建立
            var task = await taskRepo.GetByIdAsync(pending.TaskId);
            if (task is null)
            {
                await interaction.FollowupAsync("❌ 找不到任務記錄，請查看 log。");
                return;
            }

            await interaction.FollowupAsync("🔍 Requirements Agent 正在分析需求，請稍候...");

            var issues = await reqService.AnalyzeOnlyAsync(task);
            if (issues.Count == 0)
            {
                await interaction.FollowupAsync("❌ 需求分析未能產出有效 Issue，請調整描述後重新下指令。");
                return;
            }

            // 展示 Issue 預覽清單（第三層確認）
            var previewMsg = await interaction.FollowupAsync(
                embed: BuildRequirementsPreviewEmbed(task.Title, issues),
                components: BuildConfirmButtons("req_yes", "req_no"));

            _pendingConfirmations[previewMsg.Id] = pending with { PreviewIssues = issues };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Requirements 需求預覽失敗");
            await interaction.FollowupAsync("❌ 分析需求時發生錯誤，請查看 log。");
        }
    }

    /// <summary>
    /// req_yes 後，根據已確認的 Issue 清單實際建立 GitHub Issues。
    /// </summary>
    private async Task ExecuteRequirementsFromPreviewAsync(PendingConfirmation pending)
    {
        var owner = _gitHubSettings.Owner;
        var repo  = pending.Project;

        await using var scope   = serviceProvider.CreateAsyncScope();
        var taskRepo            = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var reqService          = scope.ServiceProvider.GetRequiredService<RequirementsAgentService>();

        var task = await taskRepo.GetByIdAsync(pending.TaskId);
        if (task is null)
        {
            logger.LogError("找不到 TaskItem（Id={Id}）", pending.TaskId);
            return;
        }

        taskRepo.UpdateStatus(task, "running");
        await taskRepo.SaveAsync();

        var pushService   = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var notifyChannel = FindChannel(_settings.Channels.TaskUpdates);
        var alertChannel  = FindChannel(_settings.Channels.Alerts);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = task.Id,
            Title     = task.Title,
            AgentName = task.AssignedAgent,
            Status    = "running"
        });

        var result = await reqService.CreateIssuesFromPreviewAsync(
            task, owner, repo, pending.PreviewIssues!);

        var finalStatus = result.Success ? "done" : "failed";
        taskRepo.UpdateStatus(task, finalStatus);
        await taskRepo.SaveAsync();

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = task.Id,
            Title     = task.Title,
            AgentName = task.AssignedAgent,
            Status    = finalStatus
        });

        var embed = new EmbedBuilder()
            .WithTitle(result.Success ? "✅ Requirements Agent 執行完成" : "❌ Requirements Agent 執行失敗")
            .WithColor(result.Success ? Color.Green : Color.Red)
            .AddField("任務", task.Title)
            .AddField("摘要", result.Summary)
            .WithTimestamp(DateTimeOffset.UtcNow);

        if (!string.IsNullOrEmpty(result.OutputUrl))
            embed.AddField("連結", result.OutputUrl);

        if (notifyChannel is not null)
            await notifyChannel.SendMessageAsync(embed: embed.Build());
        else if (!result.Success && alertChannel is not null)
            await alertChannel.SendMessageAsync(
                $"🚨 **Requirements Agent 失敗**\n任務：{task.Title}\n錯誤：{result.Summary}");
    }

    #endregion

    #region Agent 執行（動態分派）

    private async Task ExecuteAgentTaskAsync(PendingConfirmation pending)
    {
        var owner = _gitHubSettings.Owner;
        var repo  = string.IsNullOrWhiteSpace(pending.Project)
            ? _gitHubSettings.DefaultRepo
            : pending.Project;

        await using var scope    = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var task = await taskRepo.GetByIdAsync(pending.TaskId);
        if (task is null)
        {
            logger.LogError("找不到 TaskItem（Id={Id}）", pending.TaskId);
            return;
        }

        taskRepo.UpdateStatus(task, "running");
        await taskRepo.SaveAsync();

        var pushService   = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var notifyChannel = FindChannel(_settings.Channels.TaskUpdates);
        var alertChannel  = FindChannel(_settings.Channels.Alerts);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = task.Id,
            Title     = task.Title,
            AgentName = task.AssignedAgent,
            Status    = "running"
        });

        // 動態取得 Agent 實作（keyed DI）
        var executor = scope.ServiceProvider.GetKeyedService<IAgentExecutor>(
            pending.CeoResponse.TargetAgent);

        if (executor is null)
        {
            logger.LogError("找不到 Agent 實作：{Agent}", pending.CeoResponse.TargetAgent);
            taskRepo.UpdateStatus(task, "failed");
            await taskRepo.SaveAsync();
            if (alertChannel is not null)
                await alertChannel.SendMessageAsync(
                    $"🚨 找不到 Agent 實作：**{pending.CeoResponse.TargetAgent}**\n任務：{task.Title}");
            return;
        }

        try
        {
            var rules  = await notionService.GetRulesAsync();
            var result = await executor.ExecuteTaskAsync(task, owner, repo, rules);

            var finalStatus = result.Success ? "done" : "failed";
            taskRepo.UpdateStatus(task, finalStatus);
            await taskRepo.SaveAsync();

            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = task.Id,
                Title     = task.Title,
                AgentName = task.AssignedAgent,
                Status    = finalStatus
            });

            var embedColor = result.Success ? Color.Green : Color.Red;
            var embedTitle = result.Success
                ? $"✅ {pending.CeoResponse.TargetAgent} Agent 執行完成"
                : $"❌ {pending.CeoResponse.TargetAgent} Agent 執行失敗";

            var embed = new EmbedBuilder()
                .WithTitle(embedTitle)
                .WithColor(embedColor)
                .AddField("任務", task.Title)
                .AddField("摘要", result.Summary)
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (!string.IsNullOrEmpty(result.OutputUrl))
                embed.AddField("連結", result.OutputUrl);

            var builtEmbed = embed.Build();

            // 推送到 #任務動態（現有）
            if (notifyChannel is not null)
                await notifyChannel.SendMessageAsync(embed: builtEmbed);

            // 同時推送到 Agent 自己的頻道（Stage 7 新增）
            var agentChannelName = GetAgentChannelName(task.AssignedAgent);
            var agentChannel     = FindChannel(agentChannelName);
            if (agentChannel is not null && agentChannel.Id != notifyChannel?.Id)
                await agentChannel.SendMessageAsync(embed: builtEmbed);

            // Designer Agent：將 UI 規格書 Markdown 以檔案附件傳送到頻道
            if (task.AssignedAgent == AgentNames.Designer && result.Success)
            {
                var specLog = task.Logs.FirstOrDefault(l => l.Step == "ui-spec-output");
                if (specLog?.Payload is not null)
                {
                    try
                    {
                        var payload  = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(specLog.Payload);
                        var markdown = payload.GetProperty("markdown").GetString();
                        if (!string.IsNullOrWhiteSpace(markdown))
                        {
                            var fileName = $"ui-spec-{DateTime.UtcNow:yyyyMMdd-HHmm}.md";
                            var stream   = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));
                            var targetChannel = agentChannel ?? notifyChannel;
                            if (targetChannel is not null)
                                await targetChannel.SendFileAsync(stream, fileName, "📄 UI 規格文件");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "傳送 UI 規格文件附件失敗");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent 執行失敗：{Title}", task.Title);
            taskRepo.UpdateStatus(task, "failed");
            await taskRepo.SaveAsync();

            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = task.Id,
                Title     = task.Title,
                AgentName = task.AssignedAgent,
                Status    = "failed"
            });

            if (alertChannel is not null)
                await alertChannel.SendMessageAsync(
                    $"🚨 **{pending.CeoResponse.TargetAgent} Agent 失敗**\n任務：{task.Title}\n錯誤：{ex.Message}");
        }
    }

    /// <summary>Agent 名稱 → 對應的 Discord 頻道名稱。</summary>
    private string GetAgentChannelName(string agentName) => agentName switch
    {
        AgentNames.Dev          => _settings.Channels.DevChannel,
        AgentNames.Ops          => _settings.Channels.OpsChannel,
        AgentNames.Qa           => _settings.Channels.QaChannel,
        AgentNames.Doc          => _settings.Channels.DocChannel,
        AgentNames.Requirements => _settings.Channels.RequirementsChannel,
        AgentNames.Reviewer     => _settings.Channels.ReviewerChannel,
        AgentNames.Release      => _settings.Channels.ReleaseChannel,
        AgentNames.Designer     => _settings.Channels.DesignerChannel,
        _                       => _settings.Channels.TaskUpdates
    };

    private IMessageChannel? FindChannel(string channelName)
    {
        if (!ulong.TryParse(_settings.GuildId, out var guildId)) return null;
        return client.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }

    #endregion

    #region Embed 與按鈕建構

    /// <summary>截斷字串，確保不超過 Discord Embed field 的 1024 字元上限。</summary>
    private static string Truncate(string? value, int max = 1024)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        return value.Length <= max ? value : value[..(max - 3)] + "…";
    }

    private static Embed BuildCeoDecisionEmbed(CeoResponse response, string project)
    {
        var builder = new EmbedBuilder()
            .WithTitle("📋 CEO 決策 — 請確認")
            .WithColor(Color.Blue)
            .AddField("回應", Truncate(response.Reply))
            .AddField("動作", Truncate(response.Action), inline: true)
            .AddField("負責 Agent", response.TargetAgent ?? "—", inline: true)
            .AddField("專案", string.IsNullOrWhiteSpace(project) ? "—" : project, inline: true);

        if (response.Task is not null)
        {
            builder
                .AddField("任務標題", Truncate(response.Task.Title))
                .AddField("優先度", string.IsNullOrWhiteSpace(response.Task.Priority) ? "—" : response.Task.Priority, inline: true)
                .AddField("描述", Truncate(response.Task.Description));
        }

        return builder.Build();
    }

    private static Embed BuildAgentPlanEmbed(CeoResponse response, Guid taskId)
        => new EmbedBuilder()
            .WithTitle($"🤖 {response.TargetAgent} Agent — 即將執行")
            .WithColor(Color.Orange)
            .AddField("任務", Truncate(response.Task?.Title))
            .AddField("描述", Truncate(response.Task?.Description))
            .AddField("任務 ID", taskId.ToString())
            .WithFooter("確認後開始執行，取消則中止。")
            .Build();

    private static Embed BuildRequirementsPreviewEmbed(
        string taskTitle,
        IReadOnlyList<RequirementIssuePreview> issues)
    {
        var issueLines = issues.Select((iss, i) =>
        {
            var labels = iss.Labels.Count > 0 ? string.Join(", ", iss.Labels) : "無";
            return $"{i + 1}. **{iss.Title}** — `{labels}`";
        });

        var issueList = string.Join("\n", issueLines);

        // Discord embed description 上限 4096 字元
        if (issueList.Length > 3900)
            issueList = issueList[..3900] + "\n…（清單過長，已截斷）";

        return new EmbedBuilder()
            .WithTitle("📋 Requirements Agent — 請確認 Issue 清單")
            .WithColor(Color.Gold)
            .AddField("任務", taskTitle)
            .WithDescription(issueList)
            .WithFooter($"共 {issues.Count} 個 Issue，確認後開始建立，取消則中止。")
            .Build();
    }

    private static MessageComponent BuildConfirmButtons(
        string yesId = "confirm_yes",
        string noId  = "confirm_no")
        => new ComponentBuilder()
            .WithButton("✅ 確認", yesId, ButtonStyle.Success)
            .WithButton("❌ 取消", noId,  ButtonStyle.Danger)
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
    Guid TaskId = default,
    IReadOnlyList<RequirementIssuePreview>? PreviewIssues = null);
