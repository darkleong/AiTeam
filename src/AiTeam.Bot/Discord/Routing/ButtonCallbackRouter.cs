using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.GitHub;
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

namespace AiTeam.Bot.Discord.Routing;

/// <summary>
/// Stage 36：Discord 按鈕回調 Router（從 CommandHandler 拆解而來）。
///
/// 本類承載三大類 button handler：
/// 1. Kickoff 按鈕（Stage 25a：kickoff_continue/stop/modify/restart）
/// 2. Design 按鈕（Stage 25b：design_continue/stop/modify）
/// 3. 通用 CEO/Agent/Proposal 按鈕（confirm_/exec_/propose_/req_/cancel_/escalate_*）
///
/// 同時暴露共用 UI Flow methods（internal）供 SlashCommandRouter 與 CommandHandler 的自然語言路徑共用：
///   ShowProposalAsync / ShowDirectAgentConfirmAsync / HandleCancelRequestAsync /
///   BuildCeoDecisionEmbed / BuildConfirmButtons / BuildProposalConfirmButtons / 等
/// </summary>
public class ButtonCallbackRouter(
    DiscordSocketClient client,
    IOptions<DiscordSettings> settings,
    IOptions<GitHubSettings> gitHubSettings,
    IServiceProvider serviceProvider,
    RulesService rulesService,
    TaskGroupService taskGroupService,
    InteractionService interactionService,
    PendingConfirmationStore store,
    ILogger<ButtonCallbackRouter> logger)
{
    private readonly DiscordSettings _settings = settings.Value;
    private readonly GitHubSettings _gitHubSettings = gitHubSettings.Value;

    // ========== Public entry ==========

    public async Task RouteAsync(SocketMessageComponent interaction)
    {
        // Stage 28a：先到先贏 — 嘗試標記 Discord 回覆。若 Dashboard 已先回覆，early return
        var discordMsgId = (decimal)interaction.Message.Id;
        var isFirstToRespond = await interactionService.SyncDiscordResponseAsync(discordMsgId, interaction.Data.CustomId);
        if (!isFirstToRespond)
        {
            await interaction.RespondAsync("✅ 已在 Dashboard 回覆，流程繼續中。", ephemeral: true);
            return;
        }

        if (interaction.Data.CustomId.StartsWith("design_", StringComparison.Ordinal))
        {
            await HandleDesignButtonAsync(interaction);
            return;
        }
        // Stage 51：framework HITL 中途介入 — 必須在 kickoff_ 之前 check（customId 起頭 framework_kickoff_mid_interrupt_）
        if (interaction.Data.CustomId.StartsWith("framework_kickoff_mid_interrupt_", StringComparison.Ordinal))
        {
            await HandleFrameworkKickoffMidInterruptAsync(interaction);
            return;
        }
        if (interaction.Data.CustomId.StartsWith("kickoff_", StringComparison.Ordinal))
        {
            await HandleKickoffButtonAsync(interaction);
            return;
        }

        if (!store.TryGetConfirmation(interaction.Message.Id, out var pending))
        {
            await interaction.RespondAsync("此確認已過期或不存在。", ephemeral: true);
            return;
        }
        store.RemoveConfirmation(interaction.Message.Id);

        await HandleGenericButtonAsync(interaction, pending);
    }

    // ========== Kickoff / Design 按鈕 ==========

    private async Task HandleKickoffButtonAsync(SocketMessageComponent interaction)
    {
        var parts = interaction.Data.CustomId.Split('_', 3);
        if (parts.Length < 3 || !Guid.TryParse(parts[2], out var groupId))
        {
            await interaction.RespondAsync("⚠️ 無法解析 Kick-off 確認按鈕資訊。", ephemeral: true);
            return;
        }

        var action = parts[1]; // continue / stop / modify / restart
        store.RemoveKickoffConfirmation(interaction.Message.Id);

        if (action == "modify")
        {
            store.RegisterKickoffModify(interaction.User.Id, groupId);
            await interaction.RespondAsync(
                "✏️ 請直接輸入你的修改意見，Petra 將基於完整的會議 context 評估並調整計劃書。",
                ephemeral: true);
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

    /// <summary>
    /// Stage 51：framework HITL 中途介入按鈕處理（v4 漸進遷移第三步試點）。
    /// customId 格式：framework_kickoff_mid_interrupt_{action}_{groupId}（action = apply / cancel）。
    ///
    /// apply：複用 kickoff_modify pattern — 註冊 PendingKickoffMidInterruptApply，
    ///        Christ 在頻道輸入指引文字後由 CommandHandler.HandleCeoChannelMessageAsync 接續觸發
    ///        Bridge.HandleMidInterruptResponseAsync(group, "midinterrupt_apply", text)
    /// cancel：複用 kickoff_continue/stop pattern — DeferAsync + Task.Run 直接呼叫
    ///        Bridge.HandleMidInterruptResponseAsync(group, "midinterrupt_cancel", null)
    /// </summary>
    private async Task HandleFrameworkKickoffMidInterruptAsync(SocketMessageComponent interaction)
    {
        // customId = framework_kickoff_mid_interrupt_{action}_{groupId}
        const string prefix = "framework_kickoff_mid_interrupt_";
        var rest = interaction.Data.CustomId[prefix.Length..];
        var sepIdx = rest.IndexOf('_');
        if (sepIdx < 0
            || !Guid.TryParse(rest[(sepIdx + 1)..], out var groupId))
        {
            await interaction.RespondAsync("⚠️ 無法解析中途介入按鈕資訊。", ephemeral: true);
            return;
        }
        var action = rest[..sepIdx]; // apply / cancel

        if (action == "apply")
        {
            store.RegisterKickoffMidInterruptApply(interaction.User.Id, groupId);
            await interaction.RespondAsync(
                "✏️ 請直接輸入你的中途介入指引（一則訊息），4 Agent + Petra 下一輪會議將優先考量你的指引。",
                ephemeral: true);
            logger.LogInformation(
                "[Stage51] Mid-Interrupt Apply 待命：UserId={UserId}，GroupId={GroupId}",
                interaction.User.Id, groupId);
            return;
        }

        if (action == "cancel")
        {
            await interaction.DeferAsync();
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = serviceProvider.CreateAsyncScope();
                    var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
                    var group    = await taskRepo.GetGroupByIdAsync(groupId, CancellationToken.None);
                    if (group is null) return;
                    var bridge = scope.ServiceProvider
                        .GetRequiredService<AiTeam.Bot.Orchestration.Hitl.FrameworkHitlBridge>();
                    await bridge.HandleMidInterruptResponseAsync(
                        group, "midinterrupt_cancel", content: null, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "[Stage51] Mid-Interrupt Cancel 觸發失敗（GroupId={Id}）", groupId);
                    try { await interaction.FollowupAsync("❌ 取消中途介入時發生錯誤，請查看 log。"); } catch { /* ignore */ }
                }
            }, CancellationToken.None);
            await interaction.FollowupAsync("✅ 已取消中途介入，會議繼續進行下一輪。");
            return;
        }

        await interaction.RespondAsync($"⚠️ 未知的中途介入 action：{action}", ephemeral: true);
    }

    private async Task HandleDesignButtonAsync(SocketMessageComponent interaction)
    {
        var parts = interaction.Data.CustomId.Split('_', 3);
        if (parts.Length < 3 || !Guid.TryParse(parts[2], out var groupId))
        {
            await interaction.RespondAsync("⚠️ 無法解析 Design 確認按鈕資訊。", ephemeral: true);
            return;
        }

        var action = parts[1]; // continue / stop / modify
        store.TryGetDesignConfirmation(interaction.Message.Id, out var designInfo);
        store.RemoveDesignConfirmation(interaction.Message.Id);
        var petraSessionId = designInfo.PetraSessionId ?? "";

        if (action == "modify")
        {
            store.RegisterDesignModify(interaction.User.Id, groupId, petraSessionId);
            await interaction.RespondAsync(
                "✏️ 請直接輸入你的設計指引，Petra 將基於完整設計會議 context 調整設計規劃書。",
                ephemeral: true);
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

    // ========== 通用按鈕（confirm/exec/propose/req/cancel/escalate） ==========

    private async Task HandleGenericButtonAsync(SocketMessageComponent interaction, PendingConfirmation pending)
    {
        var id = interaction.Data.CustomId;

        if (id == "confirm_yes")
        {
            await HandleConfirmYesAsync(interaction, pending);
        }
        else if (id == "propose_yes")
        {
            await interaction.DeferAsync();
            await interaction.FollowupAsync("✅ 提案已核准！即將召開 Kick-off 會議，請稍候...");
            _ = Task.Run(async () =>
            {
                try { await ExecuteProposalApprovedAsync(pending); }
                catch (Exception ex) { logger.LogError(ex, "提案核准後執行失敗（TaskId={TaskId}）", pending.TaskId); }
            }, CancellationToken.None);
        }
        else if (id == "exec_yes")
        {
            await HandleExecYesAsync(interaction, pending);
        }
        else if (id == "cancel_yes")
        {
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
        else if (id == "propose_adjust")
        {
            await interaction.RespondAsync(
                "✏️ 請在此頻道說明您希望如何調整提案方向（一則訊息即可）：\n" +
                "例如：「UI 規格的表格欄位要加日期範圍篩選，其他沒問題」", ephemeral: true);
            store.RegisterAdjustment(interaction.User.Id, pending);
            logger.LogInformation("提案調整待命：UserId={UserId}，TaskId={TaskId}", interaction.User.Id, pending.TaskId);
            _ = interactionService.SyncDiscordResponseAsync((decimal)interaction.Message.Id, "propose_adjust");
        }
        else if (id == "escalate_skip")
        {
            await interaction.RespondAsync("⚠️ 此操作在目前版本已不適用（提案階段已簡化）。", ephemeral: true);
        }
        else if (id == "escalate_abort")
        {
            await interaction.RespondAsync("❌ 已放棄此提案。若需重新規劃，請重新下指令。");
            logger.LogInformation("老闆放棄 Petra escalate 提案：TaskId={Id}", pending.TaskId);
        }
        else if (id == "escalate_devplan_skip")
        {
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

                    // FF 三十七：清 status / interventionReason，避免 Dashboard UI 顯示「需介入」誤導
                    groupRepo.UpdateGroupStatus(group, "running");
                    group.InterventionReason = null;
                    // Stage 61-FF 四十五：Christ 「跳過審核」action 標前置 failed task cancelled，
                    // 避免後續 MarkGroupDoneOrIntervention 跨 Agent 看到舊 failed task 誤判 needs_intervention（Trial_v6 議題 #9）
                    SupersedePriorFailedTasks(group, "Christ 跳過 Dev_plan 審核");
                    await groupRepo.SaveAsync();

                    await taskGroupService.FireStepsAsync(
                        group, [new WorkflowStep("Dev")], CancellationToken.None);
                }
                catch (Exception ex) { logger.LogError(ex, "escalate_devplan_skip 失敗"); }
            }, CancellationToken.None);
        }
        else if (id == "escalate_devplan_abort")
        {
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
                        // Stage 61-FF 四十五：abort 也標前置 failed task cancelled（避免 Dashboard / 後續邏輯誤判）
                        SupersedePriorFailedTasks(group, "Christ 放棄 Dev_plan 流程");
                        groupRepo.UpdateGroupStatus(group, "failed");
                        await groupRepo.SaveAsync();
                    }
                }
                catch (Exception ex) { logger.LogError(ex, "escalate_devplan_abort 失敗"); }
            }, CancellationToken.None);
            await interaction.FollowupAsync("❌ 已放棄此任務的開發流程。");
            logger.LogInformation("老闆放棄 Dev_plan escalation：GroupId={Id}", pending.GroupId);
        }
        else // confirm_no、exec_no、propose_no
        {
            await interaction.RespondAsync("❌ 已取消。");

            if (id == "propose_no" && !string.IsNullOrWhiteSpace(pending.UiSpecPath))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await using var scope = serviceProvider.CreateAsyncScope();
                        var gh = scope.ServiceProvider.GetRequiredService<GitHubService>();
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

    private async Task HandleConfirmYesAsync(SocketMessageComponent interaction, PendingConfirmation pending)
    {
        await interaction.DeferAsync();

        // Stage 68：v5/v5.5 path 收尾（Trial_v12 揭 stale exec_confirm 卡議題）— Petra 已動態調度完成，
        // 不需建立 TaskItem（無下個 worker）也不 fire exec_confirm 卡。守 Discord button 路徑與 Dashboard 路徑對等。
        if (pending.CeoResponse.Action == CeoResponseActions.PetraV5Dispatched)
        {
            logger.LogInformation("confirm_yes：v5 path Petra 已完成（Action={Action}），跳過 TaskItem + exec_confirm fire", pending.CeoResponse.Action);
            await interaction.FollowupAsync($"✅ Petra 已動態調度完成 — {pending.Description}");
            return;
        }

        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
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

            var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = task.Id,
                Title     = task.Title,
                AgentName = task.AssignedAgent,
                Status    = task.Status
            });

            var agentPlanEmbed  = BuildAgentPlanEmbed(pending.CeoResponse, task.Id);
            var agentConfirmMsg = await interaction.FollowupAsync(
                embed: agentPlanEmbed,
                components: BuildConfirmButtons("exec_yes", "exec_no"));

            store.RegisterConfirmation(agentConfirmMsg.Id, pending with { TaskId = task.Id });

            // Stage 55B 範圍邊界：exec_confirm 仍 fire-and-forget — Discord button callback 流程（CEO confirm 後執行確認），
            // 不在 framework Pipeline Workflow 內等回應（ProposalConfirmationService.ProcessExecConfirm 接力 enqueue agent）
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

    private async Task HandleExecYesAsync(SocketMessageComponent interaction, PendingConfirmation pending)
    {
        await interaction.DeferAsync();

        // Stage 78a：v4 Requirements 第三層確認 path 砍（RequirementsAgentService 整套砍 / type reference 連帶清理）
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

        if (wfType == WorkflowType.TechImprovement && createdGroup is not null)
        {
            await interaction.FollowupAsync(
                "⏳ CEO Orchestrator 啟動：Cody 開始制定實作計畫書，Petra 審核後自動進入 coding...");
            var groupForPipeline = createdGroup;
            _ = Task.Run(async () =>
            {
                try
                {
                    await taskGroupService.FireStepsAsync(groupForPipeline,
                        [new WorkflowStep("Dev_plan")]);
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

    // Stage 78a：v4 Requirements 第三層確認 path 砍（RequirementsAgentService class 整套砍 / type reference 連帶清理）—
    // 既有 ShowRequirementsPreviewAsync / ExecuteRequirementsFromPreviewAsync / BuildRequirementsPreviewEmbed 全砍。
    // v5.5 path Petra orchestrator 走 PetraInbox + dynamic dispatch / 0 經過此 v4 Requirements branch。

    // ========== 提案流程（共用 UI Flow — 暴露 internal 供 CommandHandler / SlashCommandRouter） ==========

    /// <summary>Stage 25b：Victoria 提案流程（供 /task 指令、CEO 頻道自然語言、Dashboard 路徑共用）。</summary>
    internal async Task ShowProposalAsync(
        Func<Embed, MessageComponent, Task<IUserMessage>> sendAsync,
        CeoResponse ceoResponse,
        string project,
        string description,
        IReadOnlyList<ImageAttachment>? images = null,
        ulong channelId = 0,
        string triggeredBy = "Discord")
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();

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

            store.RegisterConfirmation(confirmMsg.Id, new PendingConfirmation(
                ceoResponse, project, description,
                TaskId: task.Id,
                IsProposal: true,
                Images: images));

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

    private async Task ExecuteProposalApprovedAsync(PendingConfirmation pending)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

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

        try
        {
            TaskGroup group;
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
                    WorkflowType.NewFeature);
            }

            await taskGroupService.FireStepsAsync(group,
                [new WorkflowStep(AgentNames.Kickoff)]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "提案核准後觸發 Kick-off 失敗（TaskId={Id}）", task.Id);
            var ceoChannel = FindChannel(_settings.Channels.CeoChannel);
            if (ceoChannel is not null)
                await ceoChannel.SendMessageAsync("⚠️ 提案已核准，但 Kick-off 會議觸發失敗，請手動下指令。");
        }
    }

    // ========== Agent 執行 & 直派 ==========

    /// <summary>跳過 CEO 確認，直接建立任務並顯示 Agent 執行確認（共用 UI Flow）。</summary>
    internal async Task ShowDirectAgentConfirmAsync(
        Func<Embed, MessageComponent, Task<IUserMessage>> sendAsync,
        CeoResponse ceoResponse,
        string project,
        string description,
        ulong channelId = 0,
        string triggeredBy = "Discord")
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
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

        store.RegisterConfirmation(agentConfirmMsg.Id,
            new PendingConfirmation(ceoResponse, project, description) with { TaskId = task.Id });

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

    private async Task ExecuteAgentTaskAsync(PendingConfirmation pending)
    {
        var owner = _gitHubSettings.Owner;
        var repo  = string.IsNullOrWhiteSpace(pending.Project)
            ? _gitHubSettings.DefaultRepo
            : pending.Project;

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

            if (notifyChannel is not null)
                await notifyChannel.SendMessageAsync(embed: builtEmbed);

            var agentChannelName = GetAgentChannelName(task.AssignedAgent);
            var agentChannel     = FindChannel(agentChannelName);
            if (agentChannel is not null && agentChannel.Id != notifyChannel?.Id)
                await agentChannel.SendMessageAsync(embed: builtEmbed);

            if (task.AssignedAgent == AgentNames.Designer && result.Success)
            {
                var specLog = task.Logs.FirstOrDefault(l => l.Step == "ui-spec-output");
                if (specLog?.Payload is not null)
                {
                    try
                    {
                        var payload  = JsonSerializer.Deserialize<JsonElement>(specLog.Payload);
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

    // ========== Cancel 流程（Stage 14） ==========

    /// <summary>共用 UI Flow：CEO 判定為「取消任務」時呼叫。</summary>
    internal async Task HandleCancelRequestAsync(SocketUserMessage msg)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();

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
            store.RegisterConfirmation(confirmMsg.Id, new PendingConfirmation(
                CeoResponse: new CeoResponse { Action = "cancel" },
                Project: group.Project,
                Description: group.Title,
                GroupId: group.Id));
        }
        else
        {
            var lines = runningGroups
                .Select((g, i) => $"{i + 1}. **{g.Title}**（{g.Project}，{g.CreatedAt:MM/dd HH:mm}）")
                .ToList();
            await msg.Channel.SendMessageAsync(
                $"目前有 {runningGroups.Count} 個進行中的任務，請回覆序號選擇要取消哪一個：\n" +
                string.Join("\n", lines));
            store.RegisterCancelSelection(msg.Author.Id, runningGroups);
        }
    }

    /// <summary>供 CommandHandler 訊息處理器呼叫：老闆選擇要取消哪個任務。</summary>
    internal async Task HandleCancelSelectionAsync(SocketUserMessage msg, List<TaskGroup> runningGroups)
    {
        var input = msg.CleanContent.Trim();

        TaskGroup? selected = null;

        if (int.TryParse(input, out var index) && index >= 1 && index <= runningGroups.Count)
            selected = runningGroups[index - 1];

        selected ??= runningGroups.FirstOrDefault(g =>
                g.Title.Contains(input, StringComparison.OrdinalIgnoreCase));

        if (selected is null)
        {
            await msg.Channel.SendMessageAsync(
                $"❌ 找不到符合「{Truncate(input, 100)}」的任務，請重新輸入序號或任務名稱。");
            store.RegisterCancelSelection(msg.Author.Id, runningGroups);
            return;
        }

        var confirmMsg = await msg.Channel.SendMessageAsync(
            embed: BuildCancelConfirmEmbed(selected),
            components: BuildConfirmButtons("cancel_yes", "cancel_no"));
        store.RegisterConfirmation(confirmMsg.Id, new PendingConfirmation(
            CeoResponse: new CeoResponse { Action = "cancel" },
            Project: selected.Project,
            Description: selected.Title,
            GroupId: selected.Id));
    }

    // ========== 工具：WorkflowType 解析 ==========

    private static WorkflowType? ResolveWorkflowType(CeoResponse ceoResponse)
    {
        if (ceoResponse.TargetAgent is AgentNames.Release or AgentNames.Ops or AgentNames.Doc)
            return null;

        return ceoResponse.WorkflowType switch
        {
            "tech_improvement" => WorkflowType.TechImprovement,
            _                  => WorkflowType.BugFix
        };
    }

    // ========== Embed 與按鈕建構（共用 UI Flow） ==========

    internal static string Truncate(string? value, int max = 1024)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        return value.Length <= max ? value : value[..(max - 3)] + "…";
    }

    internal static Embed BuildCeoDecisionEmbed(CeoResponse response, string project)
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

    internal static Embed BuildAgentPlanEmbed(CeoResponse response, Guid taskId)
        => new EmbedBuilder()
            .WithTitle($"🤖 {response.TargetAgent} Agent — 即將執行")
            .WithColor(Color.Orange)
            .AddField("任務", Truncate(response.Task?.Title))
            .AddField("描述", Truncate(response.Task?.Description))
            .AddField("任務 ID", taskId.ToString())
            .WithFooter("確認後開始執行，取消則中止。")
            .Build();

    /// <summary>
    /// Stage 61-FF 四十五：Christ「跳過審核」/「放棄」action 標前置 failed task 為 cancelled。
    /// 修根因 Trial_v6 議題 #9 — MarkGroupDoneOrIntervention 跨 Agent 看到舊 failed task（如 [Petra→Dev_plan] failed）
    /// 誤判 needs_intervention + 建 generic intervention BossInteraction 訊息誤導實際根因。
    /// 標 cancelled 後 MarkGroupDoneOrIntervention line 489-494 既有 supersede 邏輯仍會 cover（cancelled 不是 failed/needs_intervention 不踩過濾）。
    /// </summary>
    private void SupersedePriorFailedTasks(TaskGroup group, string reason)
    {
        if (group.Tasks is null) return;
        var supersededCount = 0;
        foreach (var t in group.Tasks)
        {
            if (t.Status is "failed" or "needs_intervention")
            {
                t.Status = "cancelled";
                supersededCount++;
            }
        }
        if (supersededCount > 0)
        {
            logger.LogInformation(
                "[Stage61-FF45] SupersedePriorFailedTasks：group {Id} 標 {Count} 個前置 failed/needs_intervention task → cancelled（reason={Reason}）",
                group.Id, supersededCount, reason);
        }
    }

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

    internal static MessageComponent BuildConfirmButtons(string yesId = "confirm_yes", string noId = "confirm_no")
        => new ComponentBuilder()
            .WithButton("✅ 確認", yesId, ButtonStyle.Success)
            .WithButton("❌ 取消", noId,  ButtonStyle.Danger)
            .Build();

    internal static MessageComponent BuildEscalateButtons()
        => new ComponentBuilder()
            .WithButton("⏭️ 跳過此審核",  "escalate_skip",  ButtonStyle.Secondary)
            .WithButton("❌ 放棄此提案",   "escalate_abort", ButtonStyle.Danger)
            .Build();

    internal static MessageComponent BuildProposalConfirmButtons()
        => new ComponentBuilder()
            .WithButton("✅ 核准，開始開發", "propose_yes",    ButtonStyle.Success)
            .WithButton("✏️ 需要調整",       "propose_adjust", ButtonStyle.Primary)
            .WithButton("❌ 取消",           "propose_no",     ButtonStyle.Danger)
            .Build();

    private static Embed BuildCancelConfirmEmbed(TaskGroup group)
        => new EmbedBuilder()
            .WithTitle("⚠️ 確認取消任務")
            .WithColor(Color.Orange)
            .AddField("任務", group.Title)
            .AddField("專案", string.IsNullOrWhiteSpace(group.Project) ? "—" : group.Project, inline: true)
            .AddField("建立時間", group.CreatedAt.ToString("MM/dd HH:mm"), inline: true)
            .WithFooter("確認後立即停止執行中的 Agent，已 push 的 commit 不會回滾。")
            .Build();

    // ========== Discord helpers ==========

    internal string GetAgentChannelName(string agentName) => agentName switch
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

    internal IMessageChannel? FindChannel(string channelName)
    {
        if (!ulong.TryParse(_settings.GuildId, out var guildId)) return null;
        return client.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }
}
