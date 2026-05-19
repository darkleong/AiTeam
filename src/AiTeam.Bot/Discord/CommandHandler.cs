using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord.Routing;
using AiTeam.Bot.Services;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using AiTeam.Shared.ViewModels;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Discord;

/// <summary>
/// Discord 互動主協調器（v5.5 active path only）。
///
/// Stage 36 拆出 / Stage 78c：v4 Pipeline framework 整套砍後 CommandHandler 縮為純 v5.5 path 入口：
///   - Discord 事件訂閱（ButtonExecuted / MessageReceived — SlashCommandExecuted 砍 / Discord slash command 全清空 議題 4 拍板）
///   - CEO 頻道自然語言訊息路由（Discord @mention chat → CeoAgentService.ProcessWithClaudeCodeAsync v5.5 path）
///   - Dashboard 路徑入口（HandleCeoResponseFromDashboardAsync / Stage 29-5）
///
/// 砍範圍（Stage 78b + 78c 累積）：
///   - SlashCommand 註冊段（Discord slash command list 整套清空 / Stage 78c 議題 4 拍板）
///   - Agent 頻道直接訊息 path（HandleDirectAgentChannelMessageAsync + BuildChannelAgentMap + TruncateTitle / W8 議題拍板）
///   - SkipCeoConfirm path（v4 Discord ShowDirectAgentConfirmAsync 唯一 caller 砍後）
///   - Cancel selection / Kickoff modify / HITL mid interrupt / Design modify / 提案調整輸入 path（v4 only）
///   - Register* helper：RegisterDevPlanEscalation / RegisterKickoffConfirmation / RegisterDesignConfirmation / RegisterProposalConfirmation
///     （0 v5.5 caller / TaskGroupService + MockScenarioService 砍後 0 caller）
///   - ctor dep：slashRouter / appSettings 2 dep 砍
///
/// DetectImageMediaType 從 SlashCommandRouter 移入（SlashCommandRouter 整檔砍 / line 405 唯一 caller）。
/// </summary>
public class CommandHandler(
    DiscordSocketClient client,
    IOptions<DiscordSettings> settings,
    IServiceProvider serviceProvider,
    RulesService rulesService,
    ConversationContextStore contextStore,
    InteractionService interactionService,
    PendingConfirmationStore store,
    ButtonCallbackRouter buttonRouter,
    ILogger<CommandHandler> logger)
{
    private readonly DiscordSettings _settings = settings.Value;

    // Stage 12：已處理訊息 ID 去重快取，防止 Discord gateway 阻塞時重複派送
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, DateTime> _processedMessages = new();
    private const int ProcessedMessageTtlSeconds = 60;

    // ===================================================================
    //  Discord 事件註冊
    // ===================================================================

    /// <summary>向 Guild 清空 slash command list（Stage 78c 議題 4 拍板）並訂閱互動事件。</summary>
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

        // Stage 78c 議題 4：SlashCommandRouter 整檔砍 — Discord slash command 全清空（0 production 使用 / Internal API forge-self-verify 取代）
        await guild.BulkOverwriteApplicationCommandAsync(Array.Empty<ApplicationCommandProperties>());
        logger.LogInformation("Stage 78c：Discord slash command list 清空完成（Guild={GuildId}）", guildId);

        client.ButtonExecuted  += buttonRouter.RouteAsync;
        client.MessageReceived += OnMessageReceivedAsync;
    }

    // ===================================================================
    //  Stage 29-5：Dashboard 操作中心入口
    // ===================================================================

    public async Task HandleCeoResponseFromDashboardAsync(
        CeoResponse ceoResponse,
        string userInput,
        ulong ceoChannelId,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        if (!ulong.TryParse(_settings.GuildId, out var guildId)) return;
        var ceoChannel = client.GetGuild(guildId)?.GetTextChannel(ceoChannelId);
        if (ceoChannel is null)
        {
            logger.LogWarning("HandleCeoResponseFromDashboardAsync：找不到 CEO 頻道（id={Id}）", ceoChannelId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(ceoResponse.TargetAgent) && ceoResponse.Action == "reply")
        {
            logger.LogWarning("CEO 回傳 action=reply 但 target_agent={Agent}（Dashboard 路徑），強制修正為 delegate", ceoResponse.TargetAgent);
            ceoResponse.Action = "delegate";
        }

        var finalProject = ceoResponse.Task?.Project ?? "";
        var imagesNote = images is { Count: > 0 }
            ? $"📎 _（附 {images.Count} 張圖片）_\n\n"
            : "";

        if (ceoResponse.Action == "reply")
        {
            // defensive 留：v5.5 path 0 fire（Victoria 寫 PetraInbox 後返回 PetraV5Dispatched），保 default branch 防呆
            await ceoChannel.SendMessageAsync(imagesNote + ceoResponse.Reply);
            var replyTitle = ceoResponse.Reply?.Length > 50
                ? ceoResponse.Reply[..50] + "…"
                : ceoResponse.Reply ?? userInput;
            _ = interactionService.CreateInteractionAsync(
                "ceo_reply",
                title:                $"Victoria 回覆：{replyTitle}",
                description:          ceoResponse.Reply ?? "",
                project:              finalProject,
                agentName:            "Victoria",
                availableActionsJson: InteractionService.NotifyActionsJson,
                contextJson:          JsonSerializer.Serialize(new { channelId = ceoChannelId.ToString() }));
            return;
        }

        // v5.5 main flow（PetraV5Dispatched / 其他 defensive action）：BuildCeoDecisionEmbed + BuildConfirmButtons + ceo_confirm BossInteraction
        // confirm_yes 點擊後 ButtonCallbackRouter.HandleConfirmYesAsync Stage 68 短路 ack（Petra 已完成派工）
        var confirmMsg = await ceoChannel.SendMessageAsync(
            embed: ButtonCallbackRouter.BuildCeoDecisionEmbed(ceoResponse, finalProject),
            components: ButtonCallbackRouter.BuildConfirmButtons());

        store.RegisterConfirmation(confirmMsg.Id,
            new PendingConfirmation(ceoResponse, finalProject, userInput));

        _ = interactionService.CreateInteractionAsync(
            "ceo_confirm",
            title:                ceoResponse.Task?.Title ?? userInput,
            description:          BuildCeoConfirmDescription(ceoResponse, userInput),
            project:              finalProject,
            agentName:            ceoResponse.TargetAgent,
            availableActionsJson: InteractionService.CeoConfirmActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId       = ceoChannelId.ToString(),
                ceoResponseJson = JsonSerializer.Serialize(ceoResponse),
                project         = finalProject,
                description     = userInput
            }),
            discordMessageId: (decimal)confirmMsg.Id);
    }

    // ===================================================================
    //  自然語言訊息路由（Stage 7 / Stage 78c 簡化 — 只 CEO 頻道）
    // ===================================================================

    private async Task OnMessageReceivedAsync(SocketMessage rawMessage)
    {
        if (rawMessage is not SocketUserMessage msg) return;
        if (msg.Author.IsBot) return;
        if (string.IsNullOrWhiteSpace(msg.CleanContent)) return;

        var channelName = (msg.Channel as SocketTextChannel)?.Name ?? "";
        var isCeoChannel = channelName.Equals(_settings.Channels.CeoChannel, StringComparison.OrdinalIgnoreCase);

        // Stage 78c：agent 頻道直接訊息 path 砍（W8 議題拍板） — 只處理 CEO 頻道
        if (!isCeoChannel) return;

        // Stage 12：去重快取
        var now = DateTime.UtcNow;
        if (!_processedMessages.TryAdd(msg.Id, now))
        {
            logger.LogDebug("略過重複訊息（Id={MsgId}）", msg.Id);
            return;
        }
        foreach (var kvp in _processedMessages)
        {
            if ((now - kvp.Value).TotalSeconds > ProcessedMessageTtlSeconds)
                _processedMessages.TryRemove(kvp.Key, out _);
        }

        using var typing = msg.Channel.EnterTypingState();
        try
        {
            await HandleCeoChannelMessageAsync(msg);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "處理自然語言訊息時發生錯誤（頻道={Channel}）", channelName);
            try { await msg.Channel.SendMessageAsync("❌ 處理訊息時發生錯誤，請查看 log。"); }
            catch { /* 發送錯誤訊息失敗時靜默忽略 */ }
        }
    }

    /// <summary>
    /// CEO 頻道（#victoria-ceo）的自然語言處理（v5.5 path）。
    /// CeoAgentService.ProcessWithClaudeCodeAsync 寫 PetraInbox row + return PetraV5Dispatched action → confirm_yes Stage 68 短路 ack。
    /// </summary>
    private async Task HandleCeoChannelMessageAsync(SocketUserMessage msg)
    {
        var history = contextStore.GetHistory(msg.Channel.Id);

        // 下載圖片附件
        var images = new List<ImageAttachment>();
        foreach (var attachment in msg.Attachments)
        {
            if (attachment.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
                continue;
            try
            {
                using var http = new HttpClient();
                var bytes      = await http.GetByteArrayAsync(attachment.Url);
                var mediaType  = DetectImageMediaType(bytes) ?? attachment.ContentType ?? "image/png";
                images.Add(new ImageAttachment(Convert.ToBase64String(bytes), mediaType));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "附圖下載失敗，略過");
            }
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var ceoService = scope.ServiceProvider.GetRequiredService<CeoAgentService>();
        var agentRepo  = scope.ServiceProvider.GetRequiredService<AgentRepository>();

        var rules        = await rulesService.GetRulesAsync(AgentNames.Ceo);
        var activeAgents = await agentRepo.GetActiveExecutorAgentsAsync();
        var agentList    = activeAgents.Select(a => new AgentDescriptor(a.Name, a.Description)).ToList();

        var projectName       = ExtractProjectFromHistory(history, msg.CleanContent);
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var availableProjects = await taskRepo.GetActiveProjectNamesAsync();

        var ceoResponse = await ceoService.ProcessWithClaudeCodeAsync(
            msg.CleanContent,
            msg.Author.Id.ToString(),
            projectName, agentList, rules,
            images: images.Count > 0 ? images : null,
            availableProjects: availableProjects);

        if (!string.IsNullOrWhiteSpace(ceoResponse.TargetAgent) && ceoResponse.Action == "reply")
        {
            logger.LogWarning("CEO 回傳 action=reply 但 target_agent={Agent}，強制修正為 delegate", ceoResponse.TargetAgent);
            ceoResponse.Action = "delegate";
        }

        if (ceoResponse.Action == "reply")
        {
            // defensive 留：v5.5 path 0 fire，保 default branch 防呆
            await msg.Channel.SendMessageAsync(ceoResponse.Reply);
            return;
        }

        // v5.5 main flow（PetraV5Dispatched / 其他 defensive action）：BuildCeoDecisionEmbed + BuildConfirmButtons + ceo_confirm BossInteraction
        contextStore.Clear(msg.Channel.Id);
        var finalProject = ceoResponse.Task?.Project ?? projectName;

        var confirmMessage = await msg.Channel.SendMessageAsync(
            embed: ButtonCallbackRouter.BuildCeoDecisionEmbed(ceoResponse, finalProject),
            components: ButtonCallbackRouter.BuildConfirmButtons());

        store.RegisterConfirmation(confirmMessage.Id,
            new PendingConfirmation(ceoResponse, finalProject, msg.CleanContent));

        _ = interactionService.CreateInteractionAsync(
            "ceo_confirm",
            title:                ceoResponse.Task?.Title ?? msg.CleanContent,
            description:          BuildCeoConfirmDescription(ceoResponse, msg.CleanContent),
            project:              finalProject,
            agentName:            ceoResponse.TargetAgent,
            availableActionsJson: InteractionService.CeoConfirmActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId       = msg.Channel.Id.ToString(),
                ceoResponseJson = JsonSerializer.Serialize(ceoResponse),
                project         = finalProject,
                description     = msg.CleanContent
            }),
            discordMessageId: (decimal)confirmMessage.Id);
    }

    // ===================================================================
    //  內部工具
    // ===================================================================

    /// <summary>
    /// 從對話歷史中嘗試找出專案名稱（取最後一次明確提到的專案）。
    /// 找不到時回傳空字串，讓 CEO 自行判斷或反問。
    /// </summary>
    private static string ExtractProjectFromHistory(
        IReadOnlyList<ConversationTurn> history, string currentInput)
    {
        foreach (var turn in history.Reverse())
        {
            if (!string.IsNullOrWhiteSpace(turn.Content))
                return "";
        }
        return "";
    }

    /// <summary>
    /// Stage 39 搭車修：組裝 ceo_confirm BossInteraction 的 description，含 Reply + Task.Description。
    /// 兩段式：上半 Reply（短摘要 + 路由說明）、下半 Task.Description（具體任務描述）。
    /// </summary>
    private static string BuildCeoConfirmDescription(CeoResponse ceoResponse, string fallback)
    {
        var reply = ceoResponse.Reply ?? fallback;
        var taskDescription = ceoResponse.Task?.Description;
        return string.IsNullOrWhiteSpace(taskDescription)
            ? reply
            : $"{reply}\n\n---\n\n{taskDescription}";
    }

    /// <summary>
    /// Stage 78c：從 SlashCommandRouter 移入（SlashCommandRouter 整檔砍 議題 4 / 唯一 caller HandleCeoChannelMessageAsync 圖片下載段）。
    /// 偵測 byte[] 內容對應的 MIME type（PNG/JPEG/GIF/WEBP）。
    /// </summary>
    private static string? DetectImageMediaType(byte[] bytes)
    {
        if (bytes.Length < 4) return null;

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "image/png";

        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
            return "image/gif";

        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return "image/webp";

        return null;
    }
}
