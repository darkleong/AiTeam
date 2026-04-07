using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
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
    RulesService rulesService,
    AppSettingsService appSettings,
    ConversationContextStore contextStore,
    TaskGroupService taskGroupService,
    ILogger<CommandHandler> logger)
{
    private readonly DiscordSettings _settings = settings.Value;
    private readonly GitHubSettings _gitHubSettings = gitHubSettings.Value;

    // 等待確認的 CEO 決策暫存（messageId → PendingConfirmation）
    private readonly Dictionary<ulong, PendingConfirmation> _pendingConfirmations = [];

    // Stage 10：等待「✏️ 需調整」的修改說明輸入（userId → PendingConfirmation）
    private readonly Dictionary<ulong, PendingConfirmation> _pendingAdjustments = [];

    // Stage 12：已處理訊息 ID 去重快取，防止 Discord gateway 阻塞時重複派送（messageId → 處理時間）
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, DateTime> _processedMessages = new();
    private const int ProcessedMessageTtlSeconds = 60;

    // Stage 14：等待老闆選擇要取消哪個任務（userId → running TaskGroups 清單）
    private readonly Dictionary<ulong, List<AiTeam.Data.TaskGroup>> _pendingCancelSelections = [];

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

            new SlashCommandBuilder()
                .WithName("new-session")
                .WithDescription("清除 Victoria 的對話記憶，開始全新 Session（長期記憶不受影響）")
                .Build(),
        };

        await guild.BulkOverwriteApplicationCommandAsync(commands);
        logger.LogInformation("斜線指令已向 Guild {GuildId} 註冊完成", guildId);

        client.SlashCommandExecuted += OnSlashCommandAsync;
        client.ButtonExecuted        += OnButtonExecutedAsync;
        client.MessageReceived       += OnMessageReceivedAsync;
    }

    /// <summary>
    /// Stage 16：供 TaskGroupService 呼叫，註冊 Dev_plan escalation 的 pending state。
    /// 按鈕 handler 會用 messageId 查到 groupId，再繼續或放棄流程。
    /// </summary>
    public void RegisterDevPlanEscalation(ulong messageId, Guid groupId)
    {
        _pendingConfirmations[messageId] = new PendingConfirmation(
            CeoResponse: new CeoResponse { Action = "devplan_escalate" },
            Project: "",
            Description: "",
            GroupId: groupId,
            EscalateStage: "devplan");
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

        // Stage 12：去重快取，防止 Discord gateway 阻塞時同一訊息被重複處理
        var now = DateTime.UtcNow;
        if (!_processedMessages.TryAdd(msg.Id, now))
        {
            logger.LogDebug("略過重複訊息（Id={MsgId}）", msg.Id);
            return;
        }
        // 順帶清理超過 TTL 的舊記錄，避免無限增長
        foreach (var kvp in _processedMessages)
        {
            if ((now - kvp.Value).TotalSeconds > ProcessedMessageTtlSeconds)
                _processedMessages.TryRemove(kvp.Key, out _);
        }

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
        // Stage 14：若使用者正在選擇取消哪個任務，攔截本訊息為取消選擇
        if (_pendingCancelSelections.TryGetValue(msg.Author.Id, out var cancelGroups))
        {
            _pendingCancelSelections.Remove(msg.Author.Id);
            await HandleCancelSelectionAsync(msg, cancelGroups);
            return;
        }

        // Stage 10：若使用者剛按了 ✏️「需調整」按鈕，將本訊息視為調整指示
        if (_pendingAdjustments.TryGetValue(msg.Author.Id, out var adjustPending))
        {
            _pendingAdjustments.Remove(msg.Author.Id);
            var adjustmentText = msg.CleanContent;
            logger.LogInformation("收到提案調整指示（UserId={UserId}）：{Text}", msg.Author.Id, adjustmentText);

            await msg.Channel.SendMessageAsync(
                $"✏️ 收到調整意見，CEO 正在重新協調 Demi 修改規格，請稍候...");

            // 重新進入提案流程（帶入原有資訊 + 調整意見 + 第一版產出）
            var augmentedDescription = $"{adjustPending.Description}\n\n【老闆調整意見】{adjustmentText}";
            await ShowProposalAsync(
                async (embed, comps) => await msg.Channel.SendMessageAsync(embed: embed, components: comps),
                adjustPending.CeoResponse,
                adjustPending.Project,
                augmentedDescription,
                images: adjustPending.Images,
                previousIssues: adjustPending.PreviewIssues,
                previousUiSpec: adjustPending.UiSpecMarkdown);
            return;
        }

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
                var mediaType   = DetectImageMediaType(bytes) ?? attachment.ContentType ?? "image/png";
                images.Add(new ImageAttachment(Convert.ToBase64String(bytes), mediaType));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "附圖下載失敗，略過");
            }
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var ceoService  = scope.ServiceProvider.GetRequiredService<CeoAgentService>();
        var agentRepo   = scope.ServiceProvider.GetRequiredService<AgentRepository>();

        var rules        = await rulesService.GetRulesAsync(AgentNames.Ceo);
        var activeAgents = await agentRepo.GetActiveExecutorAgentsAsync();
        var agentList    = activeAgents.Select(a => new AgentDescriptor(a.Name, a.Description)).ToList();

        // 從歷史對話嘗試提取專案名稱（取最後一次明確提到的專案）
        var projectName      = ExtractProjectFromHistory(history, msg.CleanContent);
        var taskRepo         = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var availableProjects = await taskRepo.GetActiveProjectNamesAsync();

        // Stage 15：使用 Claude Code 模式（含 Session 對話歷史 + 長期記憶）
        var ceoResponse = await ceoService.ProcessWithClaudeCodeAsync(
            msg.CleanContent,
            msg.Author.Id.ToString(),
            projectName, agentList, rules,
            images: images.Count > 0 ? images : null,
            availableProjects: availableProjects);

        // 防護修正（同 /task 指令邏輯）
        if (!string.IsNullOrWhiteSpace(ceoResponse.TargetAgent) && ceoResponse.Action == "reply")
        {
            logger.LogWarning("CEO 回傳 action=reply 但 target_agent={Agent}，強制修正為 delegate", ceoResponse.TargetAgent);
            ceoResponse.Action = "delegate";
        }

        // Stage 15：對話歷史已由 ProcessWithClaudeCodeAsync 持久化至 DB，不再由 CommandHandler 管理

        if (ceoResponse.Action == "reply")
        {
            // CEO 反問或純回覆，直接傳送文字，等待老闆下一輪回應
            await msg.Channel.SendMessageAsync(ceoResponse.Reply);
        }
        else if (ceoResponse.Action == "propose")
        {
            // CEO 判定為新功能，進入提案模式（Rosa 先、Demi 後串行產出，然後給老闆確認）
            contextStore.Clear(msg.Channel.Id);
            var finalProject = ceoResponse.Task?.Project ?? projectName;
            await msg.Channel.SendMessageAsync(ceoResponse.Reply);
            await ShowProposalAsync(
                async (embed, comps) => await msg.Channel.SendMessageAsync(embed: embed, components: comps),
                ceoResponse, finalProject, msg.CleanContent,
                images: images.Count > 0 ? images : null);
        }
        else if (ceoResponse.Action == "cancel")
        {
            // Stage 14：取消任務流程
            contextStore.Clear(msg.Channel.Id);
            await HandleCancelRequestAsync(msg);
        }
        else
        {
            // 進入確認流程後清除對話歷史（任務已理解，不需繼續累積）
            contextStore.Clear(msg.Channel.Id);

            // 若 CEO 尚未取得專案名稱，以 task.project 補充
            var finalProject = ceoResponse.Task?.Project ?? projectName;

            if (await appSettings.GetBoolAsync("SkipCeoConfirm"))
            {
                // 跳過 CEO 派工確認，直接進入 Agent 執行確認
                await ShowDirectAgentConfirmAsync(
                    async (embed, comps) => await msg.Channel.SendMessageAsync(embed: embed, components: comps),
                    ceoResponse, finalProject, msg.CleanContent);
            }
            else
            {
                var confirmMessage = await msg.Channel.SendMessageAsync(
                    embed: BuildCeoDecisionEmbed(ceoResponse, finalProject),
                    components: BuildConfirmButtons());

                _pendingConfirmations[confirmMessage.Id] = new PendingConfirmation(
                    ceoResponse, finalProject, msg.CleanContent);
            }
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
        var projectRaw = ExtractProjectFromChannelName(msg.Channel.Name);
        var project    = string.IsNullOrEmpty(projectRaw) ? _gitHubSettings.DefaultRepo : projectRaw;
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
                "new-session"  => HandleNewSessionAsync(command),
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
                var bytes     = await http.GetByteArrayAsync(attachment.Url);
                var base64    = Convert.ToBase64String(bytes);
                var mediaType = DetectImageMediaType(bytes) ?? attachment.ContentType ?? "image/png";
                images.Add(new ImageAttachment(base64, mediaType));
                logger.LogInformation("附圖已下載並轉為 Base64（{ContentType}，{Bytes} bytes）",
                    mediaType, bytes.Length);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "附圖下載失敗，將忽略圖片繼續處理");
            }
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var ceoService  = scope.ServiceProvider.GetRequiredService<CeoAgentService>();
        var agentRepo   = scope.ServiceProvider.GetRequiredService<AgentRepository>();

        var rules        = await rulesService.GetRulesAsync();
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
        if (ceoResponse.Action == "propose")
        {
            // 新功能提案模式：Rosa 先、Demi 後串行產出提案書
            await command.FollowupAsync(ceoResponse.Reply);
            await ShowProposalAsync(
                async (embed, comps) => await command.FollowupAsync(embed: embed, components: comps),
                ceoResponse, project, description,
                images: images.Count > 0 ? images : null);
        }
        else if (ceoResponse.Action != "reply")
        {
            // RequireConfirmation 欄位不可信（LLM 可能回傳 false），只要 action 非 reply 就一律顯示確認 Embed
            if (await appSettings.GetBoolAsync("SkipCeoConfirm"))
            {
                // 跳過 CEO 派工確認，直接進入 Agent 執行確認
                await ShowDirectAgentConfirmAsync(
                    async (embed, comps) => await command.FollowupAsync(embed: embed, components: comps),
                    ceoResponse, project, description);
            }
            else
            {
                var confirmMessage = await command.FollowupAsync(
                    embed: BuildCeoDecisionEmbed(ceoResponse, project),
                    components: BuildConfirmButtons());

                _pendingConfirmations[confirmMessage.Id] = new PendingConfirmation(
                    ceoResponse, project, description);
            }
        }
        else
        {
            await command.FollowupAsync(ceoResponse.Reply);
        }
    }

    /// <summary>Stage 15：/new-session — 清除 Victoria 的 in-memory 對話暫存，下次訊息將自動開啟新 DB session。</summary>
    private async Task HandleNewSessionAsync(SocketSlashCommand command)
    {
        // 清除 in-memory context store（proposal / adjustment flow 的暫存狀態）
        contextStore.Clear(command.Channel.Id);

        // DB session 不需主動刪除：CeoConversationRepository.GetActiveSessionIdAsync
        // 在下次訊息進來時，若距離最後一筆超過 30 分鐘，會自動回傳新 Guid 開啟新 session。
        // 若本指令是在 30 分鐘內執行，下次訊息時仍會繼承舊 SessionId，使用者可接受此行為（
        // 因為真正重置語境的方式是等 30 分鐘或傳訊息讓 Victoria 知道「開始新話題」）。

        await command.FollowupAsync(
            "✅ Session 已重置。Victoria 的對話語境已清空，下次回應將以全新上下文開始。\n" +
            "（長期記憶不受影響，Victoria 仍記得你過去記錄的設計決策與偏好。）");
    }

    private async Task HandleReloadRulesAsync(SocketSlashCommand command)
    {
        rulesService.InvalidateCache();
        await command.FollowupAsync("規則 Cache 已清除，下次任務將重新從資料庫載入規則。");
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

    /// <summary>
    /// 跳過 CEO 確認，直接建立任務並顯示 Agent 執行確認（第二層）。
    /// 由 AppSettings["SkipCeoConfirm"] 控制是否啟用（可從 Dashboard 即時修改）。
    /// </summary>
    private async Task ShowDirectAgentConfirmAsync(
        Func<Embed, MessageComponent, Task<IUserMessage>> sendAsync,
        CeoResponse ceoResponse,
        string project,
        string description)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo  = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var projectId = string.IsNullOrWhiteSpace(project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(project);

        var task = new TaskItem
        {
            Title         = ceoResponse.Task?.Title ?? description,
            Description   = ceoResponse.Task?.Description,
            TriggeredBy   = "Discord",
            AssignedAgent = ceoResponse.TargetAgent ?? "CEO",
            Status        = "pending",
            ProjectId     = projectId,
        };
        taskRepo.Add(task);
        await taskRepo.SaveAsync();

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = task.Id,
            Title     = task.Title,
            AgentName = task.AssignedAgent,
            Status    = task.Status
        });

        var agentPlanEmbed  = BuildAgentPlanEmbed(ceoResponse, task.Id);
        var agentConfirmMsg = await sendAsync(agentPlanEmbed, BuildConfirmButtons("exec_yes", "exec_no"));

        _pendingConfirmations[agentConfirmMsg.Id] = new PendingConfirmation(
            ceoResponse, project, description) with { TaskId = task.Id };
    }

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

                var projectId = string.IsNullOrWhiteSpace(pending.Project)
                    ? (Guid?)null
                    : await taskRepo.GetProjectIdByNameAsync(pending.Project);

                var task = new TaskItem
                {
                    Title         = pending.CeoResponse.Task?.Title ?? pending.Description,
                    Description   = pending.CeoResponse.Task?.Description,
                    TriggeredBy   = "Discord",
                    AssignedAgent = pending.CeoResponse.TargetAgent ?? "CEO",
                    Status        = "pending",
                    ProjectId     = projectId,
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
        else if (interaction.Data.CustomId == "propose_yes")
        {
            // 提案書核准：建立 GitHub Issues（從 Rosa 分析結果）
            await interaction.DeferAsync();
            await interaction.FollowupAsync(
                $"✅ 提案已核准！Rosa 開始建立 {pending.PreviewIssues?.Count ?? 0} 個 GitHub Issues...");

            _ = Task.Run(async () =>
            {
                try { await ExecuteProposalApprovedAsync(pending); }
                catch (Exception ex)
                {
                    logger.LogError(ex, "提案核准後執行失敗（TaskId={TaskId}）", pending.TaskId);
                }
            }, CancellationToken.None);
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
                // Stage 14：若為 BugFix / TechImprovement，建立 TaskGroup 讓 Orchestrator 接管後續流程
                var wfType = ResolveWorkflowType(pending.CeoResponse);
                TaskGroup? createdGroup = null;
                if (wfType.HasValue && pending.GroupId == Guid.Empty)
                {
                    createdGroup = await taskGroupService.CreateGroupAsync(
                        pending.CeoResponse.Task?.Title ?? pending.Description,
                        pending.Project,
                        wfType.Value);
                    pending = pending with { GroupId = createdGroup.Id };
                }

                // Stage 16：TechImprovement 先產 Dev_plan 計畫書，由 TaskGroupService 全程接管
                if (wfType == Orchestration.WorkflowType.TechImprovement && createdGroup is not null)
                {
                    await interaction.FollowupAsync(
                        "⏳ CEO Orchestrator 啟動：Cody 開始制定實作計畫書，Petra 審核後自動進入 coding...");
                    var groupForPipeline = createdGroup;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await taskGroupService.FireStepsAsync(groupForPipeline,
                                [new Orchestration.WorkflowStep("Dev_plan")]);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "TechImprovement Dev_plan 觸發失敗（Group={Id}）", groupForPipeline.Id);
                        }
                    }, CancellationToken.None);
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
        }
        else if (interaction.Data.CustomId == "cancel_yes")
        {
            // Stage 14：確認取消任務
            await interaction.DeferAsync();
            if (pending.GroupId != Guid.Empty)
            {
                await taskGroupService.CancelAsync(pending.GroupId);
                await interaction.FollowupAsync($"✅ 已取消任務：**{pending.Description}**");
            }
            else
            {
                await interaction.FollowupAsync("❌ 找不到要取消的任務。");
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
        else if (interaction.Data.CustomId == "propose_adjust")
        {
            // 老闆要求調整提案：等待老闆在 CEO 頻道說明調整方向
            await interaction.RespondAsync(
                "✏️ 請在此頻道說明您希望如何調整提案方向（一則訊息即可）：\n" +
                "例如：「UI 規格的表格欄位要加日期範圍篩選，其他沒問題」", ephemeral: true);

            _pendingAdjustments[interaction.User.Id] = pending;
            logger.LogInformation("提案調整待命：UserId={UserId}，TaskId={TaskId}", interaction.User.Id, pending.TaskId);
        }
        else if (interaction.Data.CustomId == "escalate_skip")
        {
            // Petra escalate 後老闆選擇跳過審核，沿用當前產出繼續流程
            await interaction.DeferAsync();

            if (pending.EscalateStage == "rosa")
            {
                await interaction.FollowupAsync("⏭️ 已跳過 Rosa 規格審核，繼續進行 Demi UI 規格設計...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ShowProposalAsync(
                            async (embed, components) =>
                                await interaction.Channel.SendMessageAsync(embed: embed, components: components),
                            pending.CeoResponse, pending.Project, pending.Description,
                            images: pending.Images,
                            previousIssues: pending.PreviewIssues,
                            skipRosaReview: true);
                    }
                    catch (Exception ex) { logger.LogError(ex, "escalate_skip (rosa) 失敗"); }
                }, CancellationToken.None);
            }
            else if (pending.EscalateStage == "demi")
            {
                await interaction.FollowupAsync("⏭️ 已跳過 Demi 審核，直接發送提案書...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var ceoChannel = FindChannel(_settings.Channels.CeoChannel);
                        if (ceoChannel is null) return;

                        var issues  = pending.PreviewIssues ?? [];
                        var uiSpec  = pending.UiSpecMarkdown ?? "";
                        var proposalEmbed = BuildProposalEmbed(pending.Description, issues, uiSpec);
                        var confirmMsg    = await ceoChannel.SendMessageAsync(
                            embed: proposalEmbed, components: BuildProposalConfirmButtons());

                        _pendingConfirmations[confirmMsg.Id] = pending with
                        {
                            IsProposal    = true,
                            EscalateStage = ""
                        };

                        if (!string.IsNullOrWhiteSpace(uiSpec))
                        {
                            var bytes = System.Text.Encoding.UTF8.GetBytes(uiSpec);
                            using var stream = new System.IO.MemoryStream(bytes);
                            await ceoChannel.SendFileAsync(stream, "ui-spec.md", "📄 UI 規格文件（提案附件）");
                        }
                    }
                    catch (Exception ex) { logger.LogError(ex, "escalate_skip (demi) 失敗"); }
                }, CancellationToken.None);
            }
            else
            {
                await interaction.FollowupAsync("⚠️ 無法判斷跳過的位置，請重新下指令。");
            }
        }
        else if (interaction.Data.CustomId == "escalate_abort")
        {
            // Petra escalate 後老闆選擇放棄
            await interaction.RespondAsync("❌ 已放棄此提案。若需重新規劃，請重新下指令。");
            logger.LogInformation("老闆放棄 Petra escalate 提案：TaskId={Id}", pending.TaskId);
        }
        else if (interaction.Data.CustomId == "escalate_devplan_skip")
        {
            // Dev_plan Petra escalate 後老闆選擇跳過審核，直接進 Dev coding
            await interaction.DeferAsync();
            await interaction.FollowupAsync("⏭️ 已跳過 Dev_plan 審核，Cody 將直接開始 coding...");
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = serviceProvider.CreateAsyncScope();
                    var groupRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
                    var group = await groupRepo.GetGroupByIdAsync(pending.GroupId);
                    if (group is null) return;

                    // 直接觸發 Dev 步驟（跳過計畫審核）
                    await taskGroupService.FireStepsAsync(
                        group, [new Orchestration.WorkflowStep("Dev")], CancellationToken.None);
                }
                catch (Exception ex) { logger.LogError(ex, "escalate_devplan_skip 失敗"); }
            }, CancellationToken.None);
        }
        else if (interaction.Data.CustomId == "escalate_devplan_abort")
        {
            // Dev_plan Petra escalate 後老闆選擇放棄整個任務
            await interaction.DeferAsync();
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = serviceProvider.CreateAsyncScope();
                    var groupRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
                    var group = await groupRepo.GetGroupByIdAsync(pending.GroupId);
                    if (group is not null)
                    {
                        groupRepo.UpdateGroupStatus(group, "failed");
                        await groupRepo.SaveAsync();
                    }
                }
                catch (Exception ex) { logger.LogError(ex, "escalate_devplan_abort 失敗"); }
            }, CancellationToken.None);
            await interaction.FollowupAsync("❌ 已放棄此任務的開發流程。");
            logger.LogInformation("老闆放棄 Dev_plan escalation：GroupId={Id}", pending.GroupId);
        }
        else // confirm_no、exec_no、req_no、propose_no
        {
            await interaction.RespondAsync("❌ 已取消。");

            // propose_no：清理 Demi 已 commit 的孤立 UI 規格文件
            if (interaction.Data.CustomId == "propose_no" &&
                !string.IsNullOrWhiteSpace(pending.UiSpecPath))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await using var scope = serviceProvider.CreateAsyncScope();
                        var gh = scope.ServiceProvider.GetRequiredService<GitHub.GitHubService>();
                        await gh.DeleteFileAsync(
                            _gitHubSettings.Owner,
                            _gitHubSettings.DefaultRepo,
                            pending.UiSpecPath,
                            $"docs: remove cancelled proposal spec - {pending.CeoResponse.Task?.Title}");
                        logger.LogInformation("已清理取消提案的 UI 規格文件：{Path}", pending.UiSpecPath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "清理 UI 規格文件失敗：{Path}", pending.UiSpecPath);
                    }
                });
            }
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

    #region CEO 提案模式（Stage 9）

    /// <summary>
    /// CEO 判定為新功能時，先呼叫 Rosa（需求分析），再呼叫 Demi（UI 規格），產出提案書讓老闆確認。
    /// Stage 12：串行化（Rosa 先 → Demi 後）、圖片傳遞、UI 規格改存 DB（不 commit 到 GitHub）。
    /// </summary>
    private async Task ShowProposalAsync(
        Func<Embed, MessageComponent, Task<IUserMessage>> sendAsync,
        CeoResponse ceoResponse,
        string project,
        string description,
        IReadOnlyList<ImageAttachment>? images = null,
        IReadOnlyList<RequirementIssuePreview>? previousIssues = null,
        string? previousUiSpec = null,
        bool skipRosaReview = false)  // true = 老闆 Skip Rosa escalate，直接以 previousIssues 進 Demi
    {
        var notifyMessage = "🔍 CEO 正在協調 Rosa 和 Demi 產出提案書，請稍候...";

        await using var scope   = serviceProvider.CreateAsyncScope();
        var taskRepo            = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var reqService          = scope.ServiceProvider.GetRequiredService<RequirementsAgentService>();
        var designerService     = scope.ServiceProvider.GetRequiredService<DesignerAgentService>();
        var gitHubService       = scope.ServiceProvider.GetRequiredService<GitHub.GitHubService>();
        var pushService         = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var pmService           = scope.ServiceProvider.GetRequiredService<Agents.PmAgentService>();

        // 建立提案任務（狀態 pending，後續核准再執行）
        var projectId = string.IsNullOrWhiteSpace(project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(project);

        var task = new TaskItem
        {
            Title         = ceoResponse.Task?.Title ?? description,
            Description   = ceoResponse.Task?.Description ?? description,
            TriggeredBy   = "Discord",
            AssignedAgent = "CEO",
            Status        = "pending",
            ProjectId     = projectId,
        };
        taskRepo.Add(task);
        await taskRepo.SaveAsync();

        var ceoChannel = FindChannel(_settings.Channels.CeoChannel);
        if (ceoChannel is not null)
            await ceoChannel.SendMessageAsync(notifyMessage);

        var readonlyWorkspace = "";
        try
        {
            var owner         = _gitHubSettings.Owner;
            var defaultRepo   = _gitHubSettings.DefaultRepo;
            var designerRules = await rulesService.GetRulesAsync(AgentNames.Designer);

            // 統一 clone 一次唯讀 workspace，Rosa / Demi 共用，減少 clone 次數
            readonlyWorkspace = gitHubService.CloneOrPull(owner, defaultRepo,
                $"ro-{task.Id:N}"[..10]);

            var petraChannel  = FindChannel(_settings.Channels.PmChannel);
            var rosaChannel   = FindChannel(_settings.Channels.RequirementsChannel);
            var demiChannel   = FindChannel(_settings.Channels.DesignerChannel);
            var updateChannel = FindChannel(_settings.Channels.TaskUpdates);

            // ── Stage 16：Rosa 迴圈（首次 + 最多 2 次 revise）──
            List<RequirementIssuePreview> issues        = [];
            var rosaRevCount                            = 0;
            string? rosaRevisionContext                 = null;
            IReadOnlyList<RequirementIssuePreview>? rosaPreviousIssues = previousIssues;

            // 老闆跳過 Rosa 審核：直接用 previousIssues，不再跑 Rosa + Petra
            if (skipRosaReview && previousIssues is { Count: > 0 })
            {
                issues = previousIssues.ToList();
                if (ceoChannel is not null)
                    await ceoChannel.SendMessageAsync(
                        $"⏭️ 已跳過 Rosa 規格審核，沿用現有 Issues（共 {issues.Count} 條），繼續進行 Demi UI 規格設計...");
            }
            else for (var rosaRound = 0; rosaRound <= 2; rosaRound++)
            {
                // 建立 Rosa TaskItem
                var rosaTask = new TaskItem
                {
                    Title         = $"[Rosa] {task.Title}",
                    Description   = task.Description,
                    TriggeredBy   = "Proposal",
                    AssignedAgent = AgentNames.Requirements,
                    Status        = "running",
                    ProjectId     = task.ProjectId,
                };
                taskRepo.Add(rosaTask);
                await taskRepo.SaveAsync();
                await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
                {
                    TaskId    = rosaTask.Id,
                    Title     = rosaTask.Title,
                    AgentName = rosaTask.AssignedAgent,
                    Status    = rosaTask.Status
                });

                var rosaRoundLabel = rosaRound == 0 ? "開始分析需求" : $"Petra 修正後重新分析（第 {rosaRound} 次）";
                if (rosaChannel is not null)
                    await rosaChannel.SendMessageAsync($"🔍 **Rosa** 正在分析需求（{rosaRoundLabel}）\n任務：{task.Title}");
                if (updateChannel is not null)
                    await updateChannel.SendMessageAsync($"🔍 **Rosa** 需求分析中（{rosaRoundLabel}）— {task.Title}");

                issues = await reqService.AnalyzeOnlyAsync(
                    task,
                    repoLocalPath: readonlyWorkspace,
                    images: images,
                    previousIssues: rosaPreviousIssues,
                    revisionContext: rosaRevisionContext);

                var rosaStatus = issues.Count > 0 ? "done" : "failed";
                taskRepo.UpdateStatus(rosaTask, rosaStatus);
                await taskRepo.SaveAsync();
                await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
                {
                    TaskId    = rosaTask.Id,
                    Title     = rosaTask.Title,
                    AgentName = rosaTask.AssignedAgent,
                    Status    = rosaStatus
                });

                if (issues.Count == 0) break; // 分析失敗，直接離開迴圈

                // 建立 Petra TaskItem（審核 Rosa）
                var petraRosaTask = new TaskItem
                {
                    Title         = $"[Petra→Rosa] {task.Title}（第 {rosaRound + 1} 輪）",
                    Description   = "審核 Rosa Issues 規格",
                    TriggeredBy   = "Proposal",
                    AssignedAgent = AgentNames.Pm,
                    Status        = "running",
                    ProjectId     = task.ProjectId,
                };
                taskRepo.Add(petraRosaTask);
                await taskRepo.SaveAsync();
                await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
                {
                    TaskId    = petraRosaTask.Id,
                    Title     = petraRosaTask.Title,
                    AgentName = petraRosaTask.AssignedAgent,
                    Status    = petraRosaTask.Status
                });

                if (petraChannel is not null)
                    await petraChannel.SendMessageAsync(
                        $"🔍 **Petra 審核 Rosa Issues**（第 {rosaRound + 1} 輪）\n任務：{task.Title}");

                var rosaReview = await pmService.ReviewRosaAsync(task, issues, readonlyWorkspace);

                var petraRosaStatus = rosaReview.Decision == "revise" ? "revision" : rosaReview.Decision == "escalate" ? "failed" : "done";
                taskRepo.UpdateStatus(petraRosaTask, petraRosaStatus);
                await taskRepo.SaveAsync();
                await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
                {
                    TaskId    = petraRosaTask.Id,
                    Title     = petraRosaTask.Title,
                    AgentName = petraRosaTask.AssignedAgent,
                    Status    = petraRosaStatus
                });

                if (petraChannel is not null)
                    await petraChannel.SendMessageAsync(
                        $"📋 **Petra 審核結果**（Rosa，第 {rosaRound + 1} 輪）：**{rosaReview.Decision.ToUpper()}**\n{rosaReview.Summary}");

                if (rosaReview.Decision == "approve") break;

                if (rosaReview.Decision == "escalate" || rosaRevCount >= 2)
                {
                    taskRepo.UpdateStatus(task, "failed");
                    await taskRepo.SaveAsync();
                    if (ceoChannel is not null)
                    {
                        // 列出 Rosa 產出的 Issues 標題供老闆評估
                        var issueListText = issues.Count > 0
                            ? string.Join("\n", issues.Select((iss, i) => $"{i + 1}. {iss.Title}"))
                            : "（無）";
                        if (issueListText.Length > 1000) issueListText = issueListText[..1000] + "\n...（更多）";

                        // 列出 Petra 的 blocking 問題
                        var blockingText = rosaReview.Issues.Where(i => i.Severity == "blocking").ToList();
                        var blockingField = blockingText.Count > 0
                            ? string.Join("\n", blockingText.Select(i => $"• {i.Description}"))
                            : rosaReview.Summary;
                        if (blockingField.Length > 1000) blockingField = blockingField[..1000] + "...";

                        var escalateEmbed = new EmbedBuilder()
                            .WithTitle("⚠️ Petra 升級通知：需要您介入")
                            .WithColor(Color.Orange)
                            .AddField("任務", task.Title)
                            .AddField("問題", $"Rosa Issues 規格經過 {rosaRevCount + 1} 輪審核仍未通過")
                            .AddField("Petra 發現的問題", blockingField)
                            .AddField($"Rosa 目前產出（共 {issues.Count} 條 Issue）", issueListText)
                            .WithTimestamp(DateTimeOffset.UtcNow)
                            .Build();
                        var escalateMsg = await ceoChannel.SendMessageAsync(
                            embed: escalateEmbed,
                            components: BuildEscalateButtons());
                        _pendingConfirmations[escalateMsg.Id] = new PendingConfirmation(
                            ceoResponse, project, description,
                            TaskId: task.Id,
                            IsProposal: false,
                            Images: images,
                            PreviewIssues: issues,
                            EscalateStage: "rosa");
                    }
                    return;
                }

                // revise：更新下一輪參數
                rosaRevCount++;
                rosaRevisionContext  = rosaReview.RevisionInstructions;
                rosaPreviousIssues   = issues; // 讓 Rosa 對照上版修改
            }

            // ── Stage 16：Demi 迴圈（首次 + 最多 2 次 revise）──
            var uiSpec               = "";
            var demiRevCount         = 0;
            string? demiRevisionContext = null;
            string? demiPreviousUiSpec  = previousUiSpec;

            for (var demiRound = 0; demiRound <= 2; demiRound++)
            {
                // 建立 Demi TaskItem
                var demiTask = new TaskItem
                {
                    Title         = $"[Demi] {task.Title}",
                    Description   = task.Description,
                    TriggeredBy   = "Proposal",
                    AssignedAgent = AgentNames.Designer,
                    Status        = "running",
                    ProjectId     = task.ProjectId,
                };
                taskRepo.Add(demiTask);
                await taskRepo.SaveAsync();
                await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
                {
                    TaskId    = demiTask.Id,
                    Title     = demiTask.Title,
                    AgentName = demiTask.AssignedAgent,
                    Status    = demiTask.Status
                });

                var demiRoundLabel = demiRound == 0 ? "開始設計 UI 規格" : $"Petra 修正後重新設計（第 {demiRound} 次）";
                if (demiChannel is not null)
                    await demiChannel.SendMessageAsync($"🎨 **Demi** 正在設計 UI 規格（{demiRoundLabel}）\n任務：{task.Title}");
                if (updateChannel is not null)
                    await updateChannel.SendMessageAsync($"🎨 **Demi** UI 規格設計中（{demiRoundLabel}）— {task.Title}");

                uiSpec = await designerService.GenerateDraftAsync(
                    task.Title,
                    task.Description ?? task.Title,
                    designerRules,
                    repoLocalPath: readonlyWorkspace,
                    rosaIssues: issues,
                    images: images,
                    previousUiSpec: demiPreviousUiSpec,
                    revisionContext: demiRevisionContext);

                taskRepo.UpdateStatus(demiTask, "done");
                await taskRepo.SaveAsync();
                await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
                {
                    TaskId    = demiTask.Id,
                    Title     = demiTask.Title,
                    AgentName = demiTask.AssignedAgent,
                    Status    = "done"
                });

                // 建立 Petra TaskItem（審核 Demi）
                var petraDemiTask = new TaskItem
                {
                    Title         = $"[Petra→Demi] {task.Title}（第 {demiRound + 1} 輪）",
                    Description   = "審核 Demi UI 規格",
                    TriggeredBy   = "Proposal",
                    AssignedAgent = AgentNames.Pm,
                    Status        = "running",
                    ProjectId     = task.ProjectId,
                };
                taskRepo.Add(petraDemiTask);
                await taskRepo.SaveAsync();
                await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
                {
                    TaskId    = petraDemiTask.Id,
                    Title     = petraDemiTask.Title,
                    AgentName = petraDemiTask.AssignedAgent,
                    Status    = petraDemiTask.Status
                });

                if (petraChannel is not null)
                    await petraChannel.SendMessageAsync(
                        $"🔍 **Petra 審核 Demi UI 規格**（第 {demiRound + 1} 輪）\n任務：{task.Title}");

                var demiReview = await pmService.ReviewDemiAsync(task, issues, uiSpec, readonlyWorkspace);

                var petraDemiStatus = demiReview.Decision == "revise" ? "revision" : demiReview.Decision == "escalate" ? "failed" : "done";
                taskRepo.UpdateStatus(petraDemiTask, petraDemiStatus);
                await taskRepo.SaveAsync();
                await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
                {
                    TaskId    = petraDemiTask.Id,
                    Title     = petraDemiTask.Title,
                    AgentName = petraDemiTask.AssignedAgent,
                    Status    = petraDemiStatus
                });

                if (petraChannel is not null)
                    await petraChannel.SendMessageAsync(
                        $"📋 **Petra 審核結果**（Demi，第 {demiRound + 1} 輪）：**{demiReview.Decision.ToUpper()}**\n{demiReview.Summary}");

                if (demiReview.Decision == "approve") break;

                if (demiReview.Decision == "escalate" || demiRevCount >= 2)
                {
                    taskRepo.UpdateStatus(task, "failed");
                    await taskRepo.SaveAsync();
                    if (ceoChannel is not null)
                    {
                        // 列出 Petra 的 blocking 問題
                        var blockingText = demiReview.Issues.Where(i => i.Severity == "blocking").ToList();
                        var blockingField = blockingText.Count > 0
                            ? string.Join("\n", blockingText.Select(i => $"• {i.Description}"))
                            : demiReview.Summary;
                        if (blockingField.Length > 1000) blockingField = blockingField[..1000] + "...";

                        var escalateEmbed = new EmbedBuilder()
                            .WithTitle("⚠️ Petra 升級通知：需要您介入")
                            .WithColor(Color.Orange)
                            .AddField("任務", task.Title)
                            .AddField("問題", $"Demi UI 規格經過 {demiRevCount + 1} 輪審核仍未通過")
                            .AddField("Petra 發現的問題", blockingField)
                            .WithFooter("UI 規格全文見下方附件")
                            .WithTimestamp(DateTimeOffset.UtcNow)
                            .Build();
                        var escalateMsg = await ceoChannel.SendMessageAsync(
                            embed: escalateEmbed,
                            components: BuildEscalateButtons());
                        _pendingConfirmations[escalateMsg.Id] = new PendingConfirmation(
                            ceoResponse, project, description,
                            TaskId: task.Id,
                            IsProposal: false,
                            Images: images,
                            PreviewIssues: issues,
                            UiSpecMarkdown: uiSpec,
                            EscalateStage: "demi");

                        // 附上 Demi UI 規格全文供老闆評估
                        if (!string.IsNullOrWhiteSpace(uiSpec))
                        {
                            var uiBytes = System.Text.Encoding.UTF8.GetBytes(uiSpec);
                            using var uiStream = new System.IO.MemoryStream(uiBytes);
                            await ceoChannel.SendFileAsync(uiStream, "demi-ui-spec.md", "📄 Demi UI 規格全文（供評估是否 Skip）");
                        }
                    }
                    return;
                }

                // revise：更新下一輪參數
                demiRevCount++;
                demiRevisionContext = demiReview.RevisionInstructions;
                demiPreviousUiSpec  = uiSpec; // 讓 Demi 對照上版修改
            }

            // 全部成功，清理 workspace
            gitHubService.CleanupLocalRepo(readonlyWorkspace);
            readonlyWorkspace = "";

            // Stage 12：UI 規格不再 commit 到 GitHub，改以 Discord 附件傳送
            var proposalEmbed = BuildProposalEmbed(task.Title, issues, uiSpec);
            var confirmMsg    = await sendAsync(proposalEmbed, BuildProposalConfirmButtons());

            _pendingConfirmations[confirmMsg.Id] = new PendingConfirmation(
                ceoResponse, project, description,
                TaskId: task.Id,
                PreviewIssues: issues,
                UiSpecMarkdown: uiSpec,
                IsProposal: true,
                Images: images);

            // 另發一則訊息帶 UI 規格附件（若有內容）
            if (!string.IsNullOrWhiteSpace(uiSpec) && ceoChannel is not null)
            {
                var uiSpecBytes = System.Text.Encoding.UTF8.GetBytes(uiSpec);
                using var stream = new System.IO.MemoryStream(uiSpecBytes);
                await ceoChannel.SendFileAsync(stream, "ui-spec.md", "📄 UI 規格文件（提案附件）");
            }
        }
        catch (Exception ex)
        {
            // 失敗時保留 workspace 供 debug，僅 log
            if (!string.IsNullOrEmpty(readonlyWorkspace))
                logger.LogWarning("唯讀 workspace 保留供 debug：{Path}", readonlyWorkspace);

            logger.LogError(ex, "CEO 提案模式失敗");
            if (ceoChannel is not null)
                await ceoChannel.SendMessageAsync("❌ 提案書產出失敗，請查看 log 或重新下指令。");
        }
    }

    /// <summary>
    /// 提案核准後：建立 GitHub Issues，然後建立 TaskGroup 並透過 Orchestrator 自動派工 Dev。
    /// </summary>
    private async Task ExecuteProposalApprovedAsync(PendingConfirmation pending)
    {
        if (pending.PreviewIssues is null or { Count: 0 }) return;

        var owner = _gitHubSettings.Owner;
        var repo  = string.IsNullOrWhiteSpace(pending.Project)
            ? _gitHubSettings.DefaultRepo
            : pending.Project;

        await using var scope  = serviceProvider.CreateAsyncScope();
        var taskRepo           = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var reqService         = scope.ServiceProvider.GetRequiredService<RequirementsAgentService>();
        var pushService        = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var task = await taskRepo.GetByIdAsync(pending.TaskId);
        if (task is null)
        {
            logger.LogError("提案核准：找不到 TaskItem（Id={Id}）", pending.TaskId);
            return;
        }

        taskRepo.UpdateStatus(task, "running");
        await taskRepo.SaveAsync();

        var result = await reqService.CreateIssuesFromPreviewAsync(
            task, owner, repo, pending.PreviewIssues);

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

        var notifyChannel = FindChannel(_settings.Channels.TaskUpdates);
        var embed = new EmbedBuilder()
            .WithTitle(result.Success ? "✅ 提案已執行 — Issues 建立完成" : "❌ Issues 建立失敗")
            .WithColor(result.Success ? Color.Green : Color.Red)
            .AddField("任務", task.Title)
            .AddField("摘要", result.Summary)
            .WithTimestamp(DateTimeOffset.UtcNow);

        if (!string.IsNullOrEmpty(result.OutputUrl))
            embed.AddField("第一個 Issue", result.OutputUrl);

        if (notifyChannel is not null)
            await notifyChannel.SendMessageAsync(embed: embed.Build());

        // Stage 10：提案核准後建立 TaskGroup，透過 Orchestrator 自動派工 Dev
        if (result.Success)
        {
            try
            {
                // 將 Issue URLs 序列化為 JSON（存入 TaskGroup.IssueUrls）
                // 優先使用 OutputUrls（所有 Issue URL），fallback 到 OutputUrl（第一個）
                var issueUrlsList = (result.OutputUrls is { Count: > 0 }
                    ? result.OutputUrls
                    : (result.OutputUrl is not null ? [result.OutputUrl] : Array.Empty<string>()))
                    .Where(u => !string.IsNullOrEmpty(u))
                    .ToList();
                var issueUrlsJson = System.Text.Json.JsonSerializer.Serialize(issueUrlsList);

                // Stage 12：UI 規格改存 DB（UiSpecContent），不再用 UiSpecPath
                var group = await taskGroupService.CreateGroupAsync(
                    task.Title,
                    pending.Project,
                    Orchestration.WorkflowType.NewFeature,
                    issueUrlsJson,
                    uiSpecContent: pending.UiSpecMarkdown);

                // Stage 16：proposal_approved → Dev_plan（計畫書），Petra 審核通過後才 coding
                var steps = new[] { new Orchestration.WorkflowStep("Dev_plan") };
                await taskGroupService.FireStepsAsync(group, steps);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Orchestrator 派工 Dev 失敗（TaskId={Id}）", task.Id);
                var ceoChannel = FindChannel(_settings.Channels.CeoChannel);
                if (ceoChannel is not null)
                    await ceoChannel.SendMessageAsync("⚠️ Issues 已建立，但 CEO Orchestrator 派工 Dev 失敗，請手動下指令。");
            }
        }
    }

    private static Embed BuildProposalEmbed(
        string title,
        IReadOnlyList<RequirementIssuePreview> issues,
        string uiSpec)
    {
        var issueLines = issues.Count > 0
            ? string.Join("\n", issues.Take(10).Select((i, idx) => $"{idx + 1}. **{i.Title}**"))
            : "（無需求分析結果）";

        // UI Spec 截斷避免超過 Discord embed 上限（完整版見附件 ui-spec.md）
        var specPreview = uiSpec.Length > 500 ? uiSpec[..500] + "\n…（已截斷，完整版見附件 ui-spec.md）" : uiSpec;

        return new EmbedBuilder()
            .WithTitle("📋 CEO 提案書 — 請確認")
            .WithColor(Color.Purple)
            .AddField("功能名稱", title)
            .AddField($"需求清單（共 {issues.Count} 項）", issueLines)
            .AddField("UI 規格摘要", string.IsNullOrWhiteSpace(specPreview) ? "（無）" : specPreview)
            .WithFooter("✅ 核准開始開發 ｜ ✏️ 需調整 ｜ ❌ 取消")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();
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
            var rules  = await rulesService.GetRulesAsync(pending.CeoResponse.TargetAgent);
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

            // Stage 10：Agent 完成後，若任務屬於某個 TaskGroup，觸發 Orchestrator 決定下一步
            if (pending.GroupId != Guid.Empty && result.Success)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await taskGroupService.HandleAgentCompletedAsync(
                            pending.GroupId,
                            task.AssignedAgent,
                            result,
                            result.OutputUrl ?? "");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Orchestrator HandleAgentCompleted 失敗（Group={Id}）", pending.GroupId);
                    }
                }, CancellationToken.None);
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

    #region Stage 14：取消任務

    /// <summary>
    /// 判斷 delegate 任務應使用哪個 WorkflowType（是否需要 Orchestrator Pipeline）。
    /// Release / Ops / Doc 為單次任務，回傳 null（不建 TaskGroup）。
    /// </summary>
    private static WorkflowType? ResolveWorkflowType(CeoResponse ceoResponse)
    {
        // 單次任務：不走 Orchestrator pipeline
        if (ceoResponse.TargetAgent is AgentNames.Release or AgentNames.Ops or AgentNames.Doc)
            return null;

        return ceoResponse.WorkflowType switch
        {
            "tech_improvement" => WorkflowType.TechImprovement,
            _                  => WorkflowType.BugFix
        };
    }

    /// <summary>
    /// 處理 CEO 判定為「取消任務」的請求。
    /// 查詢 running TaskGroup，若一個直接確認，若多個等老闆選擇，若無則回覆。
    /// </summary>
    private async Task HandleCancelRequestAsync(SocketUserMessage msg)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var runningGroups = await taskRepo.GetRunningGroupsAsync();

        if (runningGroups.Count == 0)
        {
            await msg.Channel.SendMessageAsync("目前沒有進行中的任務。");
            return;
        }

        if (runningGroups.Count == 1)
        {
            var group = runningGroups[0];
            var confirmMsg = await msg.Channel.SendMessageAsync(
                embed: BuildCancelConfirmEmbed(group),
                components: BuildConfirmButtons("cancel_yes", "cancel_no"));
            _pendingConfirmations[confirmMsg.Id] = new PendingConfirmation(
                CeoResponse: new CeoResponse { Action = "cancel" },
                Project: group.Project,
                Description: group.Title,
                GroupId: group.Id);
        }
        else
        {
            // 多個任務：列出清單，等老闆回覆序號後攔截
            var lines = runningGroups
                .Select((g, i) => $"{i + 1}. **{g.Title}**（{g.Project}，{g.CreatedAt:MM/dd HH:mm}）")
                .ToList();
            await msg.Channel.SendMessageAsync(
                $"目前有 {runningGroups.Count} 個進行中的任務，請回覆序號選擇要取消哪一個：\n" +
                string.Join("\n", lines));
            _pendingCancelSelections[msg.Author.Id] = runningGroups;
        }
    }

    /// <summary>
    /// 老闆選擇要取消哪個任務（輸入序號或任務名稱）。
    /// </summary>
    private async Task HandleCancelSelectionAsync(
        SocketUserMessage msg,
        List<AiTeam.Data.TaskGroup> runningGroups)
    {
        var input = msg.CleanContent.Trim();

        AiTeam.Data.TaskGroup? selected = null;

        // 嘗試解析序號
        if (int.TryParse(input, out var index) && index >= 1 && index <= runningGroups.Count)
            selected = runningGroups[index - 1];

        // 嘗試名稱比對（包含比對）
        if (selected is null)
            selected = runningGroups.FirstOrDefault(g =>
                g.Title.Contains(input, StringComparison.OrdinalIgnoreCase));

        if (selected is null)
        {
            await msg.Channel.SendMessageAsync(
                $"❌ 找不到符合「{Truncate(input, 100)}」的任務，請重新輸入序號或任務名稱。");
            // 重新等待老闆輸入
            _pendingCancelSelections[msg.Author.Id] = runningGroups;
            return;
        }

        var confirmMsg = await msg.Channel.SendMessageAsync(
            embed: BuildCancelConfirmEmbed(selected),
            components: BuildConfirmButtons("cancel_yes", "cancel_no"));
        _pendingConfirmations[confirmMsg.Id] = new PendingConfirmation(
            CeoResponse: new CeoResponse { Action = "cancel" },
            Project: selected.Project,
            Description: selected.Title,
            GroupId: selected.Id);
    }

    private static Embed BuildCancelConfirmEmbed(AiTeam.Data.TaskGroup group)
        => new EmbedBuilder()
            .WithTitle("⚠️ 確認取消任務")
            .WithColor(Color.Orange)
            .AddField("任務", group.Title)
            .AddField("專案", string.IsNullOrWhiteSpace(group.Project) ? "—" : group.Project, inline: true)
            .AddField("建立時間", group.CreatedAt.ToString("MM/dd HH:mm"), inline: true)
            .WithFooter("確認後立即停止執行中的 Agent，已 push 的 commit 不會回滾。")
            .Build();

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

    /// <summary>Stage 16：Petra escalate 後，讓老闆決定是否跳過審核或放棄。</summary>
    private static MessageComponent BuildEscalateButtons()
        => new ComponentBuilder()
            .WithButton("⏭️ 跳過此審核",  "escalate_skip",  ButtonStyle.Secondary)
            .WithButton("❌ 放棄此提案",   "escalate_abort", ButtonStyle.Danger)
            .Build();

    /// <summary>Stage 10：提案書確認按鈕（三個：核准 / 需調整 / 取消）。</summary>
    private static MessageComponent BuildProposalConfirmButtons()
        => new ComponentBuilder()
            .WithButton("✅ 核准，開始開發", "propose_yes",    ButtonStyle.Success)
            .WithButton("✏️ 需要調整",       "propose_adjust", ButtonStyle.Primary)
            .WithButton("❌ 取消",           "propose_no",     ButtonStyle.Danger)
            .Build();

    #endregion

    // ────────────── Helpers ──────────────

    /// <summary>
    /// 從 magic bytes 偵測圖片真實媒體類型，避免信任 Discord 回傳的 ContentType。
    /// </summary>
    private static string? DetectImageMediaType(byte[] bytes)
    {
        if (bytes.Length < 4) return null;

        // PNG: 89 50 4E 47
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "image/png";

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        // GIF: 47 49 46 38
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
            return "image/gif";

        // WebP: RIFF????WEBP
        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return "image/webp";

        return null;
    }
}

/// <summary>
/// 等待確認的暫存資料。
/// IsProposal = true 時代表這是 CEO 提案書，確認後才建立 Issues。
/// </summary>
internal record PendingConfirmation(
    CeoResponse CeoResponse,
    string Project,
    string Description,
    Guid TaskId = default,
    Guid GroupId = default,
    IReadOnlyList<RequirementIssuePreview>? PreviewIssues = null,
    string? UiSpecMarkdown = null,
    string? UiSpecPath = null,
    bool IsProposal = false,
    IReadOnlyList<ImageAttachment>? Images = null,
    string EscalateStage = "");  // "rosa" | "demi" — 供 escalate_skip 判斷繼續點
