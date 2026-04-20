using System.Text.Json;
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
using Microsoft.EntityFrameworkCore;
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
    DashboardPushService dashboardPush,
    ConversationContextStore contextStore,
    TaskGroupService taskGroupService,
    InteractionService interactionService,
    ILogger<CommandHandler> logger)
{
    private readonly DiscordSettings _settings = settings.Value;
    private readonly GitHubSettings _gitHubSettings = gitHubSettings.Value;

    // 等待確認的 CEO 決策暫存（messageId → PendingConfirmation）
    private readonly Dictionary<ulong, PendingConfirmation> _pendingConfirmations = [];

    // Stage 10：等待「✏️ 需調整」的修改說明輸入（userId → PendingConfirmation）
    private readonly Dictionary<ulong, PendingConfirmation> _pendingAdjustments = [];

    // Stage 25a：Kick-off 確認等待（messageId → groupId，供 kickoff_ 按鈕識別）
    private readonly Dictionary<ulong, Guid> _pendingKickoffConfirmations = [];

    // Stage 25a：等待 Christ 輸入修改意見（userId → groupId）
    private readonly Dictionary<ulong, Guid> _pendingKickoffModify = [];

    // Stage 25b：Design 確認等待（messageId → (groupId, petraSessionId)）
    private readonly Dictionary<ulong, (Guid GroupId, string PetraSessionId)> _pendingDesignConfirmations = [];

    // Stage 25b：等待 Christ 輸入設計修改意見（userId → (groupId, petraSessionId)）
    private readonly Dictionary<ulong, (Guid GroupId, string PetraSessionId)> _pendingDesignModify = [];

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

            new SlashCommandBuilder()
                .WithName("pause")
                .WithDescription("暫停指定 Agent 的佇列消費（不中斷正在執行的任務）")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("agent")
                    .WithDescription("要暫停的 Agent")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .AddChoice("Dev（Cody）",           AgentNames.Dev)
                    .AddChoice("Reviewer（Vera）",       AgentNames.Reviewer)
                    .AddChoice("QA（Quinn）",            AgentNames.Qa)
                    .AddChoice("Doc（Sage）",            AgentNames.Doc)
                    .AddChoice("Requirements（Rosa）",   AgentNames.Requirements)
                    .AddChoice("Designer（Demi）",       AgentNames.Designer)
                    .AddChoice("Release（Rena）",        AgentNames.Release)
                    .AddChoice("Ops（Maya）",            AgentNames.Ops))
                .Build(),

            new SlashCommandBuilder()
                .WithName("resume")
                .WithDescription("恢復指定 Agent 的佇列消費")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("agent")
                    .WithDescription("要恢復的 Agent")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .AddChoice("Dev（Cody）",           AgentNames.Dev)
                    .AddChoice("Reviewer（Vera）",       AgentNames.Reviewer)
                    .AddChoice("QA（Quinn）",            AgentNames.Qa)
                    .AddChoice("Doc（Sage）",            AgentNames.Doc)
                    .AddChoice("Requirements（Rosa）",   AgentNames.Requirements)
                    .AddChoice("Designer（Demi）",       AgentNames.Designer)
                    .AddChoice("Release（Rena）",        AgentNames.Release)
                    .AddChoice("Ops（Maya）",            AgentNames.Ops))
                .Build(),

            new SlashCommandBuilder()
                .WithName("stop-all")
                .WithDescription("讓所有 Agent 完成手頭任務後停止，不再接新任務")
                .Build(),

            new SlashCommandBuilder()
                .WithName("resume-all")
                .WithDescription("恢復所有 Agent 的佇列消費（對應 /stop-all）")
                .Build(),

            new SlashCommandBuilder()
                .WithName("queue")
                .WithDescription("顯示 Agent 佇列狀態（排隊中 / 執行中的任務）")
                .AddOption("agent", ApplicationCommandOptionType.String, "（選用）指定 Agent 名稱，省略顯示全部", isRequired: false)
                .Build(),

            new SlashCommandBuilder()
                .WithName("mock")
                .WithDescription("【Mock Mode 限定】直接觸發指定工作流程，不呼叫 LLM，供流程測試使用")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("workflow")
                    .WithDescription("要測試的流程類型")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .AddChoice("新功能（new_feature）", "new_feature")
                    .AddChoice("新功能（含提案）", "new_feature_with_proposal")
                    .AddChoice("Bug 修復（bug_fix）", "bug_fix")
                    .AddChoice("技術改善（tech_improvement）", "tech_improvement")
                    .AddChoice("【失敗測試】Review Appeal（Vera 拒絕 → Cody 反駁）", "fail_review")
                    .AddChoice("【失敗測試】QA 失敗（Quinn 失敗 → Petra 路由）", "fail_qa")
                    .AddChoice("【失敗測試】Dev_plan Appeal（Petra 拒絕 → Cody 反駁）", "fail_dev_plan"))
                .AddOption("title", ApplicationCommandOptionType.String, "（選用）模擬任務標題", isRequired: false)
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

    /// <summary>
    /// Stage 25a：供 TaskGroupService 呼叫，登記 Kick-off 確認訊息的 groupId。
    /// 後續按鈕回調會以 messageId 查詢 groupId，再呼叫 HandleKickoffConfirmedAsync。
    /// </summary>
    public void RegisterKickoffConfirmation(ulong messageId, Guid groupId, string taskPlanSummary)
    {
        _pendingKickoffConfirmations[messageId] = groupId;
        logger.LogInformation("CommandHandler：Kick-off 確認已登記（messageId={MsgId}，groupId={GroupId}）",
            messageId, groupId);
    }

    /// <summary>
    /// Stage 25b：供 TaskGroupService 呼叫，登記 Design 確認訊息的 groupId 與 Petra session ID。
    /// </summary>
    public void RegisterDesignConfirmation(ulong messageId, Guid groupId, string petraSessionId, string? escalateReason)
    {
        _pendingDesignConfirmations[messageId] = (groupId, petraSessionId);
        logger.LogInformation("CommandHandler：Design 確認已登記（messageId={MsgId}，groupId={GroupId}）",
            messageId, groupId);
    }

    /// <summary>
    /// Stage 28b：供 TaskGroupService（Dashboard 路徑）呼叫，登記提案確認訊息。
    /// CeoResponse 在提案核准流程（ExecuteProposalApprovedAsync）中不被讀取，傳入最小化實例即可。
    /// </summary>
    public void RegisterProposalConfirmation(ulong messageId, Guid taskId, string project, string description)
    {
        _pendingConfirmations[messageId] = new PendingConfirmation(
            new CeoResponse(), project, description, TaskId: taskId, IsProposal: true);
        logger.LogInformation("CommandHandler：Proposal 確認已登記（messageId={MsgId}，taskId={TaskId}）",
            messageId, taskId);
    }

    /// <summary>
    /// Stage 29-5：Dashboard 下達指令後，依 CeoResponse.Action 路由至對應的 Discord/互動流程。
    /// 與 Discord 路徑共用同一套私有方法（ShowProposalAsync / ShowDirectAgentConfirmAsync），
    /// 差異僅在目標頻道改用 ceoChannelId 直接查詢，以及 TriggeredBy 標記為 "Dashboard"。
    /// </summary>
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

        // 防護：action=reply 但有 target_agent，強制修正
        if (!string.IsNullOrWhiteSpace(ceoResponse.TargetAgent) && ceoResponse.Action == "reply")
        {
            logger.LogWarning("CEO 回傳 action=reply 但 target_agent={Agent}（Dashboard 路徑），強制修正為 delegate", ceoResponse.TargetAgent);
            ceoResponse.Action = "delegate";
        }

        var finalProject = ceoResponse.Task?.Project ?? "";

        // Dashboard 路徑的圖片不會實體附到 Discord（簡化版），改以文字提示讓 Discord 端知道老闆傳了圖
        var imagesNote = images is { Count: > 0 }
            ? $"📎 _（附 {images.Count} 張圖片）_\n\n"
            : "";

        if (ceoResponse.Action == "reply")
        {
            // 純回覆：發到 CEO 頻道 + 建 BossInteraction（ceo_reply）供 Dashboard 操作中心確認
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
        }
        else if (ceoResponse.Action == "propose")
        {
            await ceoChannel.SendMessageAsync(imagesNote + ceoResponse.Reply);
            await ShowProposalAsync(
                async (embed, comps) => await ceoChannel.SendMessageAsync(embed: embed, components: comps),
                ceoResponse, finalProject, userInput,
                images: images,
                channelId: ceoChannelId,
                triggeredBy: "Dashboard");
        }
        else if (ceoResponse.Action == "cancel")
        {
            await ceoChannel.SendMessageAsync($"✋ {ceoResponse.Reply ?? "取消指令已收到，但目前無進行中的任務可取消。"}");
        }
        else
        {
            // delegate：CEO 確認或直接 Agent 確認
            if (await appSettings.GetBoolAsync("SkipCeoConfirm"))
            {
                await ShowDirectAgentConfirmAsync(
                    async (embed, comps) => await ceoChannel.SendMessageAsync(embed: embed, components: comps),
                    ceoResponse, finalProject, userInput,
                    channelId: ceoChannelId,
                    triggeredBy: "Dashboard");
            }
            else
            {
                var confirmMsg = await ceoChannel.SendMessageAsync(
                    embed: BuildCeoDecisionEmbed(ceoResponse, finalProject),
                    components: BuildConfirmButtons());

                _pendingConfirmations[confirmMsg.Id] = new PendingConfirmation(
                    ceoResponse, finalProject, userInput);

                _ = interactionService.CreateInteractionAsync(
                    "ceo_confirm",
                    title:                ceoResponse.Task?.Title ?? userInput,
                    description:          ceoResponse.Reply ?? userInput,
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
        }
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

        // Stage 25a：若使用者剛按了「✏️ 修改計劃書」按鈕，將本訊息視為 Kick-off 修改意見
        if (_pendingKickoffModify.TryGetValue(msg.Author.Id, out var kickoffGroupId))
        {
            _pendingKickoffModify.Remove(msg.Author.Id);
            var modifyText = msg.CleanContent;
            logger.LogInformation("收到 Kick-off 修改意見（UserId={UserId}，GroupId={GroupId}）", msg.Author.Id, kickoffGroupId);

            await msg.Channel.SendMessageAsync(
                "✏️ 收到修改意見，Petra 正在評估影響並調整計劃書，請稍候...");

            _ = Task.Run(async () =>
            {
                try
                {
                    await taskGroupService.HandleKickoffConfirmedAsync(
                        kickoffGroupId, "modify", modifyText, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Kick-off 修改計劃書失敗（GroupId={Id}）", kickoffGroupId);
                    await msg.Channel.SendMessageAsync("❌ 修改計劃書時發生錯誤，請查看 log。");
                }
            }, CancellationToken.None);
            return;
        }

        // Stage 25b：若使用者剛按了「✏️ 修改設計」按鈕，將本訊息視為 Design 修改意見
        if (_pendingDesignModify.TryGetValue(msg.Author.Id, out var designModifyInfo))
        {
            _pendingDesignModify.Remove(msg.Author.Id);
            var designModifyText = msg.CleanContent;
            logger.LogInformation("收到 Design 修改意見（UserId={UserId}，GroupId={GroupId}）", msg.Author.Id, designModifyInfo.GroupId);

            await msg.Channel.SendMessageAsync(
                "✏️ 收到設計修改指引，Petra 正在調整設計規劃書，請稍候...");

            _ = Task.Run(async () =>
            {
                try
                {
                    await taskGroupService.HandleDesignConfirmedAsync(
                        designModifyInfo.GroupId, "modify", designModifyInfo.PetraSessionId, designModifyText, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Design 修改設計規劃書失敗（GroupId={Id}）", designModifyInfo.GroupId);
                    await msg.Channel.SendMessageAsync("❌ 修改設計規劃書時發生錯誤，請查看 log。");
                }
            }, CancellationToken.None);
            return;
        }

        // Stage 10：若使用者剛按了 ✏️「需調整」按鈕，將本訊息視為調整指示
        if (_pendingAdjustments.TryGetValue(msg.Author.Id, out var adjustPending))
        {
            _pendingAdjustments.Remove(msg.Author.Id);
            var adjustmentText = msg.CleanContent;
            logger.LogInformation("收到提案調整指示（UserId={UserId}）：{Text}", msg.Author.Id, adjustmentText);

            await msg.Channel.SendMessageAsync(
                $"✏️ 收到調整意見，CEO 正在重新產出提案書，請稍候...");

            // 重新進入提案流程（帶入原有資訊 + 調整意見）
            var augmentedDescription = $"{adjustPending.Description}\n\n【老闆調整意見】{adjustmentText}";
            await ShowProposalAsync(
                async (embed, comps) => await msg.Channel.SendMessageAsync(embed: embed, components: comps),
                adjustPending.CeoResponse,
                adjustPending.Project,
                augmentedDescription,
                images: adjustPending.Images,
                channelId: msg.Channel.Id);
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
                images: images.Count > 0 ? images : null,
                channelId: msg.Channel.Id);
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
                    ceoResponse, finalProject, msg.CleanContent,
                    channelId: msg.Channel.Id);
            }
            else
            {
                var confirmMessage = await msg.Channel.SendMessageAsync(
                    embed: BuildCeoDecisionEmbed(ceoResponse, finalProject),
                    components: BuildConfirmButtons());

                _pendingConfirmations[confirmMessage.Id] = new PendingConfirmation(
                    ceoResponse, finalProject, msg.CleanContent);

                // Stage 28a：寫入 BossInteraction（ceo_confirm）
                _ = interactionService.CreateInteractionAsync(
                    "ceo_confirm",
                    title:                ceoResponse.Task?.Title ?? msg.CleanContent,
                    description:          ceoResponse.Reply ?? msg.CleanContent,
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

        // Stage 28a：寫入 BossInteraction（ceo_confirm，直接指派 Agent）
        _ = interactionService.CreateInteractionAsync(
            "ceo_confirm",
            title:                fakeResponse.Task?.Title ?? msg.CleanContent,
            description:          fakeResponse.Reply,
            project:              project,
            agentName:            agentName,
            availableActionsJson: InteractionService.CeoConfirmActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId       = msg.Channel.Id.ToString(),
                ceoResponseJson = JsonSerializer.Serialize(fakeResponse),
                project,
                description     = msg.CleanContent
            }),
            discordMessageId: (decimal)confirmMessage.Id);
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
                "mock"         => HandleMockCommandAsync(command),
                "pause"        => HandlePauseCommandAsync(command),
                "resume"       => HandleResumeCommandAsync(command),
                "stop-all"     => HandleStopAllCommandAsync(command),
                "resume-all"   => HandleResumeAllCommandAsync(command),
                "queue"        => HandleQueueCommandAsync(command),
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
                images: images.Count > 0 ? images : null,
                channelId: command.Channel.Id);
        }
        else if (ceoResponse.Action != "reply")
        {
            // RequireConfirmation 欄位不可信（LLM 可能回傳 false），只要 action 非 reply 就一律顯示確認 Embed
            if (await appSettings.GetBoolAsync("SkipCeoConfirm"))
            {
                // 跳過 CEO 派工確認，直接進入 Agent 執行確認
                await ShowDirectAgentConfirmAsync(
                    async (embed, comps) => await command.FollowupAsync(embed: embed, components: comps),
                    ceoResponse, project, description,
                    channelId: command.Channel.Id);
            }
            else
            {
                var confirmMessage = await command.FollowupAsync(
                    embed: BuildCeoDecisionEmbed(ceoResponse, project),
                    components: BuildConfirmButtons());

                _pendingConfirmations[confirmMessage.Id] = new PendingConfirmation(
                    ceoResponse, project, description);

                // Stage 28a：寫入 BossInteraction（ceo_confirm，/task 指令）
                _ = interactionService.CreateInteractionAsync(
                    "ceo_confirm",
                    title:                ceoResponse.Task?.Title ?? description,
                    description:          ceoResponse.Reply ?? description,
                    project:              project,
                    agentName:            ceoResponse.TargetAgent,
                    availableActionsJson: InteractionService.CeoConfirmActionsJson,
                    contextJson:          JsonSerializer.Serialize(new
                    {
                        channelId       = command.Channel.Id.ToString(),
                        ceoResponseJson = JsonSerializer.Serialize(ceoResponse),
                        project,
                        description
                    }),
                    discordMessageId: (decimal)confirmMessage.Id);
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

    /// <summary>
    /// Stage 17：/mock — MockMode 限定指令，直接注入模擬 TaskGroup 觸發指定工作流程。
    /// 繞過 Victoria 分類與所有提案流程，讓測試者可立即驗證完整 Agent 執行鏈。
    /// </summary>
    /// <summary>
    /// Stage 32：本方法已重構為薄 wrapper，核心邏輯移至 <see cref="MockScenarioService"/>，
    /// 讓 Dashboard Internal API 可共用同一份情境實作（FF 十五）。
    /// </summary>
    private async Task HandleMockCommandAsync(SocketSlashCommand command)
    {
        var workflowStr = command.Data.Options.First(o => o.Name == "workflow").Value.ToString()!;
        var customTitle = command.Data.Options.FirstOrDefault(o => o.Name == "title")?.Value?.ToString();

        var mockService = serviceProvider.GetRequiredService<MockScenarioService>();
        var (_, message) = await mockService.RunScenarioAsync(workflowStr, customTitle);
        await command.FollowupAsync(message);
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

    // 27b-1：Agent 佇列操作指令

    private static readonly string[] QueueExecutorKeys =
    [
        AgentNames.Dev, AgentNames.Reviewer, AgentNames.Qa, AgentNames.Doc,
        AgentNames.Requirements, AgentNames.Designer, AgentNames.Release, AgentNames.Ops
    ];

    private async Task HandlePauseCommandAsync(SocketSlashCommand command)
    {
        var agent = command.Data.Options.First(o => o.Name == "agent").Value.ToString()!;
        await appSettings.SetAsync($"AgentState:{agent}", "paused");
        _ = dashboardPush.PushQueueUpdateAsync();
        await command.FollowupAsync($"⏸️ **{agent}** 已暫停佇列消費，正在執行的任務不受影響。\n使用 `/resume agent:{agent}` 恢復。");
    }

    private async Task HandleResumeCommandAsync(SocketSlashCommand command)
    {
        var agent = command.Data.Options.First(o => o.Name == "agent").Value.ToString()!;
        await appSettings.SetAsync($"AgentState:{agent}", "active");
        _ = dashboardPush.PushQueueUpdateAsync();
        await command.FollowupAsync($"▶️ **{agent}** 已恢復佇列消費。");
    }

    private async Task HandleStopAllCommandAsync(SocketSlashCommand command)
    {
        foreach (var key in QueueExecutorKeys)
            await appSettings.SetAsync($"AgentState:{key}", "stopping");

        _ = dashboardPush.PushQueueUpdateAsync();
        await command.FollowupAsync("🛑 所有 Agent 已進入 **Stopping** 狀態，完成手頭任務後將自動停止。\n使用 `/resume` 指定個別 Agent 恢復，或 `/resume-all` 全部恢復。");
    }

    private async Task HandleResumeAllCommandAsync(SocketSlashCommand command)
    {
        foreach (var key in QueueExecutorKeys)
            await appSettings.SetAsync($"AgentState:{key}", "active");

        _ = dashboardPush.PushQueueUpdateAsync();
        await command.FollowupAsync("▶️ 所有 Agent 已恢復佇列消費。");
    }

    private async Task HandleQueueCommandAsync(SocketSlashCommand command)
    {
        var agentFilter = command.Data.Options.FirstOrDefault(o => o.Name == "agent")?.Value?.ToString();

        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AiTeam.Data.AppDbContext>();

        var query = db.Set<TaskItem>()
            .AsNoTracking()
            .Where(t => t.QueueStatus == "queued" || t.QueueStatus == "processing");

        if (!string.IsNullOrWhiteSpace(agentFilter))
        {
            // Dev_plan 歸在 Dev group
            var matchAgents = agentFilter == AgentNames.Dev
                ? new[] { AgentNames.Dev, "Dev_plan" }
                : new[] { agentFilter };
            query = query.Where(t => matchAgents.Contains(t.AssignedAgent));
        }

        var tasks = await query.OrderBy(t => t.QueuedAt).ToListAsync();

        if (tasks.Count == 0)
        {
            var targetLabel = string.IsNullOrWhiteSpace(agentFilter) ? "所有 Agent" : agentFilter;
            await command.FollowupAsync($"✅ {targetLabel} 佇列為空，目前無待執行任務。");
            return;
        }

        var lines = tasks.Select(t =>
        {
            var waitTime = t.QueuedAt.HasValue
                ? $"（等待 {(DateTime.UtcNow - t.QueuedAt.Value).TotalMinutes:F0} 分鐘）"
                : "";
            var statusIcon = t.QueueStatus == "processing" ? "🔄" : "⏳";
            return $"{statusIcon} [{t.AssignedAgent}] {t.Title} {waitTime}";
        });

        var embed = new EmbedBuilder()
            .WithTitle("📋 Agent 佇列狀態")
            .WithDescription(string.Join("\n", lines))
            .WithColor(Color.Blue)
            .WithFooter($"共 {tasks.Count} 個任務（🔄 執行中 / ⏳ 排隊中）")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        await command.FollowupAsync(embed: embed);
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
        string description,
        ulong channelId = 0,
        string triggeredBy = "Discord")
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
            TriggeredBy   = triggeredBy,
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

        // Stage 28a：寫入 BossInteraction（exec_confirm，SkipCeoConfirm 模式）
        _ = interactionService.CreateInteractionAsync(
            "exec_confirm",
            title:                ceoResponse.Task?.Title ?? description,
            description:          $"即將由 {ceoResponse.TargetAgent} 執行",
            project:              project,
            agentName:            ceoResponse.TargetAgent,
            availableActionsJson: InteractionService.ExecConfirmActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId       = channelId.ToString(),
                ceoResponseJson = JsonSerializer.Serialize(ceoResponse),
                project,
                description,
                taskId          = task.Id.ToString()
            }),
            discordMessageId: (decimal)agentConfirmMsg.Id,
            taskItemId:       task.Id);
    }

    #region 雙層確認機制

    // ────────────── Stage 25a：Kickoff 確認按鈕處理 ──────────────

    /// <summary>
    /// Stage 25a：處理 Kick-off 確認按鈕（kickoff_continue / kickoff_stop / kickoff_modify / kickoff_restart）。
    /// CustomId 格式：kickoff_{action}_{groupId}。
    /// </summary>
    private async Task HandleKickoffButtonAsync(SocketMessageComponent interaction)
    {
        // 解析 CustomId：kickoff_{action}_{groupId}
        var parts = interaction.Data.CustomId.Split('_', 3);
        if (parts.Length < 3 || !Guid.TryParse(parts[2], out var groupId))
        {
            await interaction.RespondAsync("⚠️ 無法解析 Kick-off 確認按鈕資訊。", ephemeral: true);
            return;
        }

        var action = parts[1]; // continue / stop / modify / restart

        _pendingKickoffConfirmations.Remove(interaction.Message.Id);

        if (action == "modify")
        {
            // 進入等待修改意見輸入狀態
            _pendingKickoffModify[interaction.User.Id] = groupId;
            await interaction.RespondAsync(
                "✏️ 請直接輸入你的修改意見，Petra 將基於完整的會議 context 評估並調整計劃書。",
                ephemeral: true);

            // Stage 28b：同步更新 BossInteraction 狀態，讓 Dashboard 按鈕 disable
            _ = interactionService.SyncDiscordResponseAsync((decimal)interaction.Message.Id, "kickoff_modify");
            return;
        }

        await interaction.DeferAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                await taskGroupService.HandleKickoffConfirmedAsync(groupId, action, ct: CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HandleKickoffButtonAsync：{Action} 失敗（groupId={Id}）", action, groupId);
                try { await interaction.FollowupAsync($"❌ 執行 Kickoff {action} 時發生錯誤，請查看 log。"); } catch { /* ignore */ }
            }
        }, CancellationToken.None);

        var actionText = action switch
        {
            "continue" => "▶️ 繼續開發，即將進入設計規劃階段...",
            "stop"     => "⏹️ 任務已停止。",
            "restart"  => "🔄 重新召開 Kick-off 會議...",
            _          => $"✅ 已執行 {action}。"
        };
        await interaction.FollowupAsync(actionText);
    }

    // ────────────── Stage 25b：Design 確認按鈕處理 ──────────────

    /// <summary>
    /// Stage 25b：處理設計規劃確認按鈕（design_continue / design_stop / design_modify）。
    /// CustomId 格式：design_{action}_{groupId}。
    /// </summary>
    private async Task HandleDesignButtonAsync(SocketMessageComponent interaction)
    {
        var parts = interaction.Data.CustomId.Split('_', 3);
        if (parts.Length < 3 || !Guid.TryParse(parts[2], out var groupId))
        {
            await interaction.RespondAsync("⚠️ 無法解析 Design 確認按鈕資訊。", ephemeral: true);
            return;
        }

        var action = parts[1]; // continue / stop / modify

        _pendingDesignConfirmations.TryGetValue(interaction.Message.Id, out var designInfo);
        _pendingDesignConfirmations.Remove(interaction.Message.Id);
        var petraSessionId = designInfo.PetraSessionId ?? "";

        if (action == "modify")
        {
            _pendingDesignModify[interaction.User.Id] = (groupId, petraSessionId);
            await interaction.RespondAsync(
                "✏️ 請直接輸入你的設計指引，Petra 將基於完整設計會議 context 調整設計規劃書。",
                ephemeral: true);

            // Stage 28b：同步更新 BossInteraction 狀態，讓 Dashboard 按鈕 disable
            _ = interactionService.SyncDiscordResponseAsync((decimal)interaction.Message.Id, "design_modify");
            return;
        }

        await interaction.DeferAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                await taskGroupService.HandleDesignConfirmedAsync(groupId, action, petraSessionId, ct: CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HandleDesignButtonAsync：{Action} 失敗（groupId={Id}）", action, groupId);
                try { await interaction.FollowupAsync($"❌ 執行 Design {action} 時發生錯誤，請查看 log。"); } catch { /* ignore */ }
            }
        }, CancellationToken.None);

        var actionText = action switch
        {
            "continue" => "▶️ 繼續開發，Cody 即將開始規劃實作計畫書...",
            "stop"     => "⏹️ 任務已停止。",
            _          => $"✅ 已執行 {action}。"
        };
        await interaction.FollowupAsync(actionText);
    }

    private async Task OnButtonExecutedAsync(SocketMessageComponent interaction)
    {
        // Stage 28a：先到先贏 — 嘗試標記 Discord 回覆。若 Dashboard 已先回覆，early return
        var discordMsgId = (decimal)interaction.Message.Id;
        var isFirstToRespond = await interactionService.SyncDiscordResponseAsync(discordMsgId, interaction.Data.CustomId);
        if (!isFirstToRespond)
        {
            await interaction.RespondAsync("✅ 已在 Dashboard 回覆，流程繼續中。", ephemeral: true);
            return;
        }

        // Stage 25b：Design 確認按鈕（CustomId 以 design_ 開頭）
        if (interaction.Data.CustomId.StartsWith("design_", StringComparison.Ordinal))
        {
            await HandleDesignButtonAsync(interaction);
            return;
        }

        // Stage 25a：Kickoff 確認按鈕（CustomId 以 kickoff_ 開頭，不依賴 _pendingConfirmations）
        if (interaction.Data.CustomId.StartsWith("kickoff_", StringComparison.Ordinal))
        {
            await HandleKickoffButtonAsync(interaction);
            return;
        }

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

                // Stage 28a：寫入 BossInteraction（exec_confirm）
                _ = interactionService.CreateInteractionAsync(
                    "exec_confirm",
                    title:                pending.CeoResponse.Task?.Title ?? pending.Description,
                    description:          $"即將由 {pending.CeoResponse.TargetAgent} 執行",
                    project:              pending.Project,
                    agentName:            pending.CeoResponse.TargetAgent,
                    availableActionsJson: InteractionService.ExecConfirmActionsJson,
                    contextJson:          JsonSerializer.Serialize(new
                    {
                        channelId       = interaction.Channel.Id.ToString(),
                        ceoResponseJson = JsonSerializer.Serialize(pending.CeoResponse),
                        project         = pending.Project,
                        description     = pending.Description,
                        taskId          = task.Id.ToString()
                    }),
                    discordMessageId: (decimal)agentConfirmMsg.Id,
                    taskItemId:       task.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "confirm_yes 處理失敗");
                await interaction.FollowupAsync("❌ 建立任務時發生錯誤，請查看 log。");
            }
        }
        else if (interaction.Data.CustomId == "propose_yes")
        {
            // 提案書核准：建立 TaskGroup 並觸發 Kick-off 會議
            await interaction.DeferAsync();
            await interaction.FollowupAsync(
                $"✅ 提案已核准！即將召開 Kick-off 會議，請稍候...");

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

            // Stage 28b：同步更新 BossInteraction 狀態，讓 Dashboard 按鈕 disable
            _ = interactionService.SyncDiscordResponseAsync((decimal)interaction.Message.Id, "propose_adjust");
        }
        else if (interaction.Data.CustomId == "escalate_skip")
        {
            // Stage 25b：提案階段已移除 Rosa/Demi，此按鈕不再適用
            await interaction.RespondAsync("⚠️ 此操作在目前版本已不適用（提案階段已簡化）。", ephemeral: true);
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
    /// <summary>
    /// Stage 25b：Victoria 提案流程簡化（移除 Rosa/Demi 提案階段）。
    /// 直接以需求描述建立提案書，Rosa/Demi 的工作移至 Kickoff 之後的設計階段。
    /// </summary>
    private async Task ShowProposalAsync(
        Func<Embed, MessageComponent, Task<IUserMessage>> sendAsync,
        CeoResponse ceoResponse,
        string project,
        string description,
        IReadOnlyList<ImageAttachment>? images = null,
        ulong channelId = 0,
        string triggeredBy = "Discord")
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var projectId = string.IsNullOrWhiteSpace(project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(project);

        var task = new TaskItem
        {
            Title         = ceoResponse.Task?.Title ?? description,
            Description   = ceoResponse.Task?.Description ?? description,
            TriggeredBy   = triggeredBy,
            AssignedAgent = "CEO",
            Status        = "pending",
            ProjectId     = projectId,
        };
        taskRepo.Add(task);
        await taskRepo.SaveAsync();

        try
        {
            var proposalEmbed = BuildProposalEmbed(task.Title, task.Description ?? description);
            var confirmMsg    = await sendAsync(proposalEmbed, BuildProposalConfirmButtons());

            _pendingConfirmations[confirmMsg.Id] = new PendingConfirmation(
                ceoResponse, project, description,
                TaskId: task.Id,
                IsProposal: true,
                Images: images);

            // Stage 28a：寫入 BossInteraction（proposal）
            _ = interactionService.CreateInteractionAsync(
                "proposal",
                title:                task.Title,
                description:          task.Description ?? description,
                project:              project,
                agentName:            null,
                availableActionsJson: InteractionService.ProposalActionsJson,
                contextJson:          JsonSerializer.Serialize(new
                {
                    channelId   = channelId.ToString(),
                    taskId      = task.Id.ToString(),
                    project,
                    description
                }),
                discordMessageId: (decimal)confirmMsg.Id,
                taskItemId:       task.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CEO 提案模式失敗");
            var ceoChannel = FindChannel(_settings.Channels.CeoChannel);
            if (ceoChannel is not null)
                await ceoChannel.SendMessageAsync("❌ 提案書產出失敗，請查看 log 或重新下指令。");
        }
    }

    /// <summary>
    /// Stage 25b：提案核准後建立 TaskGroup，觸發 Kick-off 會議。
    /// Issues 和 UI 規格改在 Kick-off 後的設計階段由 Rosa/Demi 產出。
    /// </summary>
    private async Task ExecuteProposalApprovedAsync(PendingConfirmation pending)
    {
        await using var scope  = serviceProvider.CreateAsyncScope();
        var taskRepo           = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService        = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var task = await taskRepo.GetByIdAsync(pending.TaskId);
        if (task is null)
        {
            logger.LogError("提案核准：找不到 TaskItem（Id={Id}）", pending.TaskId);
            return;
        }

        taskRepo.UpdateStatus(task, "done");
        await taskRepo.SaveAsync();

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = task.Id,
            Title     = task.Title,
            AgentName = task.AssignedAgent,
            Status    = "done"
        });

        var notifyChannel = FindChannel(_settings.Channels.TaskUpdates);
        var embed = new EmbedBuilder()
            .WithTitle("✅ 提案已核准 — 即將召開 Kick-off 會議")
            .WithColor(Color.Green)
            .AddField("任務", task.Title)
            .WithTimestamp(DateTimeOffset.UtcNow);

        if (notifyChannel is not null)
            await notifyChannel.SendMessageAsync(embed: embed.Build());

        // Stage 25b：提案核准後觸發 Kick-off 會議（Issues/UI 規格由設計階段產出）
        // MockMode 下 task 已有 GroupId（預先建立），直接用現有 group；一般流程則新建。
        try
        {
            AiTeam.Data.TaskGroup group;
            if (task.GroupId.HasValue && task.GroupId != Guid.Empty)
            {
                var existingGroup = await taskRepo.GetGroupByIdAsync(task.GroupId.Value);
                if (existingGroup is null)
                    throw new InvalidOperationException($"找不到 TaskGroup（Id={task.GroupId}）");
                group = existingGroup;
            }
            else
            {
                group = await taskGroupService.CreateGroupAsync(
                    task.Title,
                    pending.Project,
                    Orchestration.WorkflowType.NewFeature);
            }

            await taskGroupService.FireStepsAsync(group,
                [new Orchestration.WorkflowStep(AgentNames.Kickoff)]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "提案核准後觸發 Kick-off 失敗（TaskId={Id}）", task.Id);
            var ceoChannel = FindChannel(_settings.Channels.CeoChannel);
            if (ceoChannel is not null)
                await ceoChannel.SendMessageAsync("⚠️ 提案已核准，但 Kick-off 會議觸發失敗，請手動下指令。");
        }
    }

    /// <summary>
    /// Stage 25b：提案書 Embed（簡化版，僅顯示需求描述，不含 Issues/UI 規格）。
    /// Issues 和 UI 規格由設計階段產出。
    /// </summary>
    internal static Embed BuildProposalEmbed(string title, string? description)
    {
        var descPreview = string.IsNullOrWhiteSpace(description)
            ? "（無描述）"
            : description.Length > 800 ? description[..800] + "\n…（已截斷）" : description;

        return new EmbedBuilder()
            .WithTitle("📋 CEO 提案書 — 請確認")
            .WithColor(Color.Purple)
            .AddField("功能名稱", title)
            .AddField("需求說明", descPreview)
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
    internal static MessageComponent BuildProposalConfirmButtons()
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
