using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord.Routing;
using AiTeam.Bot.Services;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Discord;

/// <summary>
/// Stage 36：Discord 互動主協調器（瘦身版，從 2172 行拆至 ~500 行）。
///
/// 職責：
///   - Discord 事件訂閱（SlashCommandExecuted / ButtonExecuted / MessageReceived）
///   - 自然語言訊息路由（CEO 頻道 / Agent 頻道）
///   - Dashboard 路徑入口（HandleCeoResponseFromDashboardAsync，Stage 29-5）
///   - Register* 薄 wrapper → 委派到 PendingConfirmationStore
///
/// 拆出的職責：
///   - Slash command  → <see cref="SlashCommandRouter"/>
///   - Button callback → <see cref="ButtonCallbackRouter"/>（含 ShowProposalAsync / ShowDirectAgentConfirmAsync 等共用 UI flow）
///   - 6 個 confirmation Dictionary → <see cref="PendingConfirmationStore"/>
/// </summary>
public class CommandHandler(
    DiscordSocketClient client,
    IOptions<DiscordSettings> settings,
    IServiceProvider serviceProvider,
    RulesService rulesService,
    AppSettingsService appSettings,
    ConversationContextStore contextStore,
    InteractionService interactionService,
    PendingConfirmationStore store,
    SlashCommandRouter slashRouter,
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

    /// <summary>向 Guild 註冊所有斜線指令，並訂閱互動事件。</summary>
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

        var commands = SlashCommandRouter.BuildCommandDefinitions();
        await guild.BulkOverwriteApplicationCommandAsync(commands);
        logger.LogInformation("斜線指令已向 Guild {GuildId} 註冊完成", guildId);

        client.SlashCommandExecuted += slashRouter.RouteAsync;
        client.ButtonExecuted        += buttonRouter.RouteAsync;
        client.MessageReceived       += OnMessageReceivedAsync;
    }

    // ===================================================================
    //  Stage 36：Register* 薄 wrapper（委派到 PendingConfirmationStore）
    //  保留 public 簽名以相容外部 caller（TaskGroupService / MockScenarioService）
    // ===================================================================

    public void RegisterDevPlanEscalation(ulong messageId, Guid groupId)
    {
        store.RegisterConfirmation(messageId, new PendingConfirmation(
            CeoResponse: new CeoResponse { Action = "devplan_escalate" },
            Project: "",
            Description: "",
            GroupId: groupId,
            EscalateStage: "devplan"));
    }

    public void RegisterKickoffConfirmation(ulong messageId, Guid groupId, string taskPlanSummary)
    {
        store.RegisterKickoffConfirmation(messageId, groupId);
        logger.LogInformation("CommandHandler：Kick-off 確認已登記（messageId={MsgId}，groupId={GroupId}）",
            messageId, groupId);
    }

    public void RegisterDesignConfirmation(ulong messageId, Guid groupId, string petraSessionId, string? escalateReason)
    {
        store.RegisterDesignConfirmation(messageId, groupId, petraSessionId);
        logger.LogInformation("CommandHandler：Design 確認已登記（messageId={MsgId}，groupId={GroupId}）",
            messageId, groupId);
    }

    public void RegisterProposalConfirmation(ulong messageId, Guid taskId, string project, string description)
    {
        store.RegisterConfirmation(messageId, new PendingConfirmation(
            new CeoResponse(), project, description, TaskId: taskId, IsProposal: true));
        logger.LogInformation("CommandHandler：Proposal 確認已登記（messageId={MsgId}，taskId={TaskId}）",
            messageId, taskId);
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
            await ceoChannel.SendMessageAsync(imagesNote + ceoResponse.Reply);
            var replyTitle = ceoResponse.Reply?.Length > 50
                ? ceoResponse.Reply[..50] + "…"
                : ceoResponse.Reply ?? userInput;
            // Stage 55B 範圍邊界：ceo_reply 仍 fire-and-forget — 純通知 ack，不在 framework Workflow 內等回應（Discord 命令層）
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
            await buttonRouter.ShowProposalAsync(
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
            if (await appSettings.GetBoolAsync("SkipCeoConfirm"))
            {
                await buttonRouter.ShowDirectAgentConfirmAsync(
                    async (embed, comps) => await ceoChannel.SendMessageAsync(embed: embed, components: comps),
                    ceoResponse, finalProject, userInput,
                    channelId: ceoChannelId,
                    triggeredBy: "Dashboard");
            }
            else
            {
                var confirmMsg = await ceoChannel.SendMessageAsync(
                    embed: ButtonCallbackRouter.BuildCeoDecisionEmbed(ceoResponse, finalProject),
                    components: ButtonCallbackRouter.BuildConfirmButtons());

                store.RegisterConfirmation(confirmMsg.Id,
                    new PendingConfirmation(ceoResponse, finalProject, userInput));

                // Stage 55B 範圍邊界：ceo_confirm 仍 fire-and-forget — Discord 命令層 CEO 收到命令後請 Christ 確認派工，
                // 不在 framework Pipeline Workflow 內等回應（ProposalConfirmationService 流程，後續 exec_confirm 接力）
                _ = interactionService.CreateInteractionAsync(
                    "ceo_confirm",
                    title:                ceoResponse.Task?.Title ?? userInput,
                    // Stage 39 搭車修：補上 Task.Description（CEO 解析出的具體任務內容），讓 Dashboard 操作中心顯示完整資訊
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
        }
    }

    // ===================================================================
    //  自然語言訊息路由（Stage 7）
    // ===================================================================

    private async Task OnMessageReceivedAsync(SocketMessage rawMessage)
    {
        if (rawMessage is not SocketUserMessage msg) return;
        if (msg.Author.IsBot) return;
        if (string.IsNullOrWhiteSpace(msg.CleanContent)) return;

        var channelName = (msg.Channel as SocketTextChannel)?.Name ?? "";
        var isCeoChannel = channelName.Equals(_settings.Channels.CeoChannel, StringComparison.OrdinalIgnoreCase);

        var channelAgentMap = BuildChannelAgentMap();
        var isAgentChannel  = channelAgentMap.TryGetValue(channelName, out var targetAgent);

        if (!isCeoChannel && !isAgentChannel) return;

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
    /// CEO 頻道（#victoria-ceo）的自然語言處理。
    /// 保留對話歷史供多輪對話使用，支援 CEO 反問機制。
    /// </summary>
    private async Task HandleCeoChannelMessageAsync(SocketUserMessage msg)
    {
        // Stage 14：若使用者正在選擇取消哪個任務，攔截為取消選擇
        if (store.TryGetCancelSelection(msg.Author.Id, out var cancelGroups))
        {
            store.RemoveCancelSelection(msg.Author.Id);
            await buttonRouter.HandleCancelSelectionAsync(msg, cancelGroups);
            return;
        }

        // Stage 25a：Kickoff 修改意見
        if (store.TryGetKickoffModify(msg.Author.Id, out var kickoffGroupId))
        {
            store.RemoveKickoffModify(msg.Author.Id);
            var modifyText = msg.CleanContent;
            logger.LogInformation("收到 Kick-off 修改意見（UserId={UserId}，GroupId={GroupId}）", msg.Author.Id, kickoffGroupId);

            await msg.Channel.SendMessageAsync(
                "✏️ 收到修改意見，Petra 正在評估影響並調整計劃書，請稍候...");

            _ = Task.Run(async () =>
            {
                try
                {
                    await using var innerScope = serviceProvider.CreateAsyncScope();
                    var tgs = innerScope.ServiceProvider.GetRequiredService<Orchestration.TaskGroupService>();
                    await tgs.HandleKickoffConfirmedAsync(
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

        // Stage 51：HITL 中途介入 Apply 修改指引文字
        if (store.TryGetKickoffMidInterruptApply(msg.Author.Id, out var midInterruptGroupId))
        {
            store.RemoveKickoffMidInterruptApply(msg.Author.Id);
            var hintText = msg.CleanContent;
            logger.LogInformation(
                "[Stage51] 收到中途介入指引（UserId={UserId}，GroupId={GroupId}）",
                msg.Author.Id, midInterruptGroupId);

            await msg.Channel.SendMessageAsync(
                "✏️ 收到中途介入指引，會議將從 checkpoint 繼續，下一輪 4 Agent + Petra 會優先考量你的指引...");

            _ = Task.Run(async () =>
            {
                try
                {
                    await using var innerScope = serviceProvider.CreateAsyncScope();
                    var taskRepo = innerScope.ServiceProvider.GetRequiredService<Data.Repositories.TaskRepository>();
                    var group    = await taskRepo.GetGroupByIdAsync(midInterruptGroupId, CancellationToken.None);
                    if (group is null)
                    {
                        await msg.Channel.SendMessageAsync("❌ 找不到對應的 TaskGroup，無法套用中途介入。");
                        return;
                    }
                    var bridge = innerScope.ServiceProvider
                        .GetRequiredService<Orchestration.Hitl.FrameworkHitlBridge>();
                    await bridge.HandleMidInterruptResponseAsync(
                        group, "midinterrupt_apply", hintText, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "[Stage51] 套用中途介入失敗（GroupId={Id}）", midInterruptGroupId);
                    await msg.Channel.SendMessageAsync("❌ 套用中途介入時發生錯誤，請查看 log。");
                }
            }, CancellationToken.None);
            return;
        }

        // Stage 25b：Design 修改意見
        if (store.TryGetDesignModify(msg.Author.Id, out var designModifyInfo))
        {
            store.RemoveDesignModify(msg.Author.Id);
            var designModifyText = msg.CleanContent;
            logger.LogInformation("收到 Design 修改意見（UserId={UserId}，GroupId={GroupId}）", msg.Author.Id, designModifyInfo.GroupId);

            await msg.Channel.SendMessageAsync(
                "✏️ 收到設計修改指引，Petra 正在調整設計規劃書，請稍候...");

            _ = Task.Run(async () =>
            {
                try
                {
                    await using var innerScope = serviceProvider.CreateAsyncScope();
                    var tgs = innerScope.ServiceProvider.GetRequiredService<Orchestration.TaskGroupService>();
                    await tgs.HandleDesignConfirmedAsync(
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

        // Stage 10：提案調整輸入
        if (store.TryGetAdjustment(msg.Author.Id, out var adjustPending))
        {
            store.RemoveAdjustment(msg.Author.Id);
            var adjustmentText = msg.CleanContent;
            logger.LogInformation("收到提案調整指示（UserId={UserId}）：{Text}", msg.Author.Id, adjustmentText);

            await msg.Channel.SendMessageAsync(
                "✏️ 收到調整意見，CEO 正在重新產出提案書，請稍候...");

            var augmentedDescription = $"{adjustPending.Description}\n\n【老闆調整意見】{adjustmentText}";
            await buttonRouter.ShowProposalAsync(
                async (embed, comps) => await msg.Channel.SendMessageAsync(embed: embed, components: comps),
                adjustPending.CeoResponse,
                adjustPending.Project,
                augmentedDescription,
                images: adjustPending.Images,
                channelId: msg.Channel.Id);
            return;
        }

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
                var mediaType  = SlashCommandRouter.DetectImageMediaType(bytes) ?? attachment.ContentType ?? "image/png";
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

        var projectName      = ExtractProjectFromHistory(history, msg.CleanContent);
        var taskRepo         = scope.ServiceProvider.GetRequiredService<TaskRepository>();
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
            await msg.Channel.SendMessageAsync(ceoResponse.Reply);
        }
        else if (ceoResponse.Action == "propose")
        {
            contextStore.Clear(msg.Channel.Id);
            var finalProject = ceoResponse.Task?.Project ?? projectName;
            await msg.Channel.SendMessageAsync(ceoResponse.Reply);
            await buttonRouter.ShowProposalAsync(
                async (embed, comps) => await msg.Channel.SendMessageAsync(embed: embed, components: comps),
                ceoResponse, finalProject, msg.CleanContent,
                images: images.Count > 0 ? images : null,
                channelId: msg.Channel.Id);
        }
        else if (ceoResponse.Action == "cancel")
        {
            contextStore.Clear(msg.Channel.Id);
            await buttonRouter.HandleCancelRequestAsync(msg);
        }
        else
        {
            contextStore.Clear(msg.Channel.Id);

            var finalProject = ceoResponse.Task?.Project ?? projectName;

            if (await appSettings.GetBoolAsync("SkipCeoConfirm"))
            {
                await buttonRouter.ShowDirectAgentConfirmAsync(
                    async (embed, comps) => await msg.Channel.SendMessageAsync(embed: embed, components: comps),
                    ceoResponse, finalProject, msg.CleanContent,
                    channelId: msg.Channel.Id);
            }
            else
            {
                var confirmMessage = await msg.Channel.SendMessageAsync(
                    embed: ButtonCallbackRouter.BuildCeoDecisionEmbed(ceoResponse, finalProject),
                    components: ButtonCallbackRouter.BuildConfirmButtons());

                store.RegisterConfirmation(confirmMessage.Id,
                    new PendingConfirmation(ceoResponse, finalProject, msg.CleanContent));

                _ = interactionService.CreateInteractionAsync(
                    "ceo_confirm",
                    title:                ceoResponse.Task?.Title ?? msg.CleanContent,
                    // Stage 39 搭車修：補上 Task.Description（與 Dashboard 路徑對稱）
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
        }
    }

    /// <summary>
    /// 各 Agent 專屬頻道的直接對話處理。
    /// 自動 CC CEO 頻道，並直接路由到對應 Agent 走確認流程。
    /// </summary>
    private async Task HandleDirectAgentChannelMessageAsync(SocketUserMessage msg, string agentName)
    {
        var ceoChannel = buttonRouter.FindChannel(_settings.Channels.CeoChannel);
        if (ceoChannel is not null)
        {
            var ccEmbed = new EmbedBuilder()
                .WithTitle($"📋 老闆直接指派給 {agentName} Agent")
                .WithColor(Color.LightGrey)
                .AddField("來源頻道", $"#{msg.Channel.Name}", inline: true)
                .AddField("指派內容", ButtonCallbackRouter.Truncate(msg.CleanContent, 512))
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();
            await ceoChannel.SendMessageAsync(embed: ccEmbed);
        }

        var projectRaw = "";
        var project    = string.IsNullOrEmpty(projectRaw) ? "" : projectRaw;
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
            embed: ButtonCallbackRouter.BuildCeoDecisionEmbed(fakeResponse, project),
            components: ButtonCallbackRouter.BuildConfirmButtons());

        store.RegisterConfirmation(confirmMessage.Id,
            new PendingConfirmation(fakeResponse, project, msg.CleanContent));

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

    // ===================================================================
    //  內部工具
    // ===================================================================

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
        foreach (var turn in history.Reverse())
        {
            if (!string.IsNullOrWhiteSpace(turn.Content))
                return "";
        }
        return "";
    }

    /// <summary>截斷任務標題為不超過 100 字元的短標題。</summary>
    private static string TruncateTitle(string input)
    {
        if (string.IsNullOrEmpty(input)) return "直接指派任務";
        var firstLine = input.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? input;
        return firstLine.Length <= 100 ? firstLine : firstLine[..97] + "…";
    }

    /// <summary>
    /// Stage 39 搭車修：組裝 ceo_confirm BossInteraction 的 description，含 Reply + Task.Description。
    /// 原本只放 ceoResponse.Reply，Dashboard 操作中心看不到 CEO 解析出的具體任務內容；
    /// 改為兩段式：上半 Reply（短摘要 + 路由說明）、下半 Task.Description（具體任務描述）。
    /// </summary>
    private static string BuildCeoConfirmDescription(CeoResponse ceoResponse, string fallback)
    {
        var reply = ceoResponse.Reply ?? fallback;
        var taskDescription = ceoResponse.Task?.Description;
        return string.IsNullOrWhiteSpace(taskDescription)
            ? reply
            : $"{reply}\n\n---\n\n{taskDescription}";
    }
}
