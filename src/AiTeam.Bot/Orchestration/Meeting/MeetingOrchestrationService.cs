using System.Text.Json;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord;
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

namespace AiTeam.Bot.Orchestration.Meeting;

/// <summary>
/// Stage 36：Kick-off / Design 會議編排（從 TaskGroupService 拆解而來）。
///
/// 職責：
///   - 執行 Kick-off 會議（RunKickoffMeetingAndWaitAsync）
///   - 執行 Design 會議（RunDesignPhaseAsync）
///   - Crash Recovery 掃描卡住會議（RecoverStuckMeetingsAsync，Stage 31）
///   - Christ 按鈕確認 / 修改路由（HandleKickoffConfirmedAsync / HandleDesignConfirmedAsync）
///
/// 不做：
///   - FireStepsAsync 本體（留在 TaskGroupService，透過 IServiceProvider 回呼）
///   - NotifyBoss 系列（留在 TaskGroupService）
/// </summary>
public class MeetingOrchestrationService(
    IServiceProvider serviceProvider,
    DiscordSocketClient discordClient,
    IOptions<DiscordSettings> discordSettings,
    IOptions<GitHubSettings> gitHubSettings,
    KickoffMeetingService kickoffMeetingService,
    DesignMeetingService designMeetingService,
    MeetingCommons meetingCommons,
    InteractionService interactionService,
    ILogger<MeetingOrchestrationService> logger)
{
    private readonly DiscordSettings _discord = discordSettings.Value;
    private readonly GitHubSettings  _gitHub  = gitHubSettings.Value;

    // ============================================================
    //  Kick-off 會議（Stage 25a）
    // ============================================================

    /// <summary>
    /// 執行 Kick-off 會議並進入 Christ 確認等待狀態。
    /// 由 FireOneStepAsync 偵測到 Kickoff 步驟時，在背景 Task.Run 中呼叫。
    /// </summary>
    public async Task RunKickoffMeetingAndWaitAsync(TaskGroup group, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var owner = _gitHub.Owner;
        var repo  = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;

        await db.TaskGroups
            .Where(g => g.Id == group.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveMeetingType, "Kickoff"), CancellationToken.None);

        logger.LogInformation("MeetingOrchestration：Kick-off 會議開始（Group={Id}）", group.Id);

        var projectId = string.IsNullOrWhiteSpace(group.Project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(group.Project, ct);

        var kickoffTask = new TaskItem
        {
            Title         = $"[Kickoff] {group.Title}",
            Description   = "Kick-off 多 Agent 會議",
            TriggeredBy   = "Orchestrator",
            AssignedAgent = AgentNames.Kickoff,
            Status        = "running",
            GroupId       = group.Id,
            ProjectId     = projectId,
        };
        taskRepo.Add(kickoffTask);
        await taskRepo.SaveAsync(ct);
        taskRepo.AddLog(new TaskLog { TaskId = kickoffTask.Id, Agent = AgentNames.Kickoff, Step = "Kick-off 多 Agent 會議進行中...", Status = "running" });
        await taskRepo.SaveAsync(ct);

        await pushService.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = AgentNames.Pm,
            Status           = "running",
            CurrentTaskTitle = $"Kick-off 會議：{group.Title}"
        });
        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = kickoffTask.Id,
            GroupId   = group.Id,
            Title     = kickoffTask.Title,
            AgentName = AgentNames.Kickoff,
            Status    = "running"
        });

        try
        {
            var proposalContent = BuildKickoffProposalContent(group);

            var meetingResult = await kickoffMeetingService.RunKickoffMeetingAsync(
                group, proposalContent, owner, repo, ct);

            await using var scope2 = serviceProvider.CreateAsyncScope();
            var taskRepo2 = scope2.ServiceProvider.GetRequiredService<TaskRepository>();
            var freshGroup = await taskRepo2.GetGroupByIdAsync(group.Id, ct);
            if (freshGroup is null)
            {
                logger.LogError("MeetingOrchestration：Kick-off 完成後找不到 Group={Id}", group.Id);
                return;
            }

            freshGroup.KickoffMeetingLog = meetingResult.MeetingLog;
            freshGroup.TaskPlan          = meetingResult.TaskPlan;
            freshGroup.KickoffRound      = meetingResult.TotalRounds;
            await taskRepo2.SaveAsync(ct);

            logger.LogInformation("MeetingOrchestration：Kick-off 會議記錄已存入 DB（Group={Id}）", group.Id);

            var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
            if (ceoChannel is null)
            {
                logger.LogError("MeetingOrchestration：找不到 CEO 頻道，無法上呈 Kick-off 結果");
                return;
            }

            var planPreview = string.IsNullOrWhiteSpace(freshGroup.TaskPlan)
                ? "（無計劃書）"
                : freshGroup.TaskPlan.Length > 500
                    ? freshGroup.TaskPlan[..500] + "\n...\n（完整內容請查看 Dashboard）"
                    : freshGroup.TaskPlan;

            var embed = new EmbedBuilder()
                .WithTitle("🚀 Kick-off 會議完成")
                .WithColor(Color.Blue)
                .AddField("任務", freshGroup.Title)
                .AddField("會議輪次", meetingResult.TotalRounds.ToString())
                .AddField("任務計劃書摘要", planPreview)
                .WithFooter("▶️ 繼續 = 進入設計規劃；⏹️ 停止 = 取消任務；✏️ 修改 = 調整計劃書")
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            var buttons = new ComponentBuilder()
                .WithButton("▶️ 繼續開發",  $"kickoff_continue_{freshGroup.Id}", ButtonStyle.Success)
                .WithButton("⏹️ 停止任務",  $"kickoff_stop_{freshGroup.Id}",     ButtonStyle.Danger)
                .WithButton("✏️ 修改計劃書", $"kickoff_modify_{freshGroup.Id}",  ButtonStyle.Secondary)
                .Build();

            var msg = await ceoChannel.SendMessageAsync(embed: embed, components: buttons);

            var commandHandler = serviceProvider.GetRequiredService<CommandHandler>();
            commandHandler.RegisterKickoffConfirmation(msg.Id, freshGroup.Id, planPreview);

            _ = interactionService.CreateInteractionAsync(
                "kickoff",
                title:                $"Kickoff 確認：{freshGroup.Title}",
                description:          planPreview,
                project:              freshGroup.Project,
                agentName:            AgentNames.Pm,
                availableActionsJson: InteractionService.KickoffActionsJson,
                contextJson:          JsonSerializer.Serialize(new
                {
                    channelId = ceoChannel.Id.ToString(),
                    groupId   = freshGroup.Id.ToString()
                }),
                discordMessageId: (decimal)msg.Id,
                taskGroupId:      freshGroup.Id);

            taskRepo.AddLog(new TaskLog { TaskId = kickoffTask.Id, Agent = AgentNames.Kickoff, Step = $"Kick-off 完成（共 {meetingResult.TotalRounds} 輪）", Status = "done" });
            taskRepo.UpdateStatus(kickoffTask, "done");
            await taskRepo.SaveAsync(ct);
            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = kickoffTask.Id,
                GroupId   = group.Id,
                Title     = kickoffTask.Title,
                AgentName = AgentNames.Kickoff,
                Status    = "done"
            });

            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = AgentNames.Pm,
                Status           = "idle",
                CurrentTaskTitle = null
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MeetingOrchestration：Kick-off 會議失敗（Group={Id}）", group.Id);

            taskRepo.AddLog(new TaskLog { TaskId = kickoffTask.Id, Agent = AgentNames.Kickoff, Step = $"Kick-off 失敗：{ex.Message}", Status = "failed" });
            taskRepo.UpdateStatus(kickoffTask, "failed");
            await taskRepo.SaveAsync(CancellationToken.None);
            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = kickoffTask.Id,
                GroupId   = group.Id,
                Title     = kickoffTask.Title,
                AgentName = AgentNames.Kickoff,
                Status    = "failed"
            });

            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName = AgentNames.Pm,
                Status    = "error",
                CurrentTaskTitle = $"Kick-off 失敗：{ex.Message}"
            });
        }
        finally
        {
            await db.TaskGroups
                .Where(g => g.Id == group.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveMeetingType, (string?)null),
                    CancellationToken.None);
        }
    }

    // ============================================================
    //  Design 會議（Stage 25b）
    // ============================================================

    /// <summary>
    /// 執行設計規劃會議。
    /// consensus → 直接 FireStepsAsync Dev_plan（不需 Christ 確認）
    /// escalate  → 發送 Discord embed，等待 Christ 確認（design_continue/stop/modify）
    /// </summary>
    public async Task RunDesignPhaseAsync(TaskGroup group, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var owner = _gitHub.Owner;
        var repo  = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;

        await db.TaskGroups
            .Where(g => g.Id == group.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveMeetingType, "Design"), CancellationToken.None);

        logger.LogInformation("MeetingOrchestration：設計規劃會議開始（Group={Id}）", group.Id);

        var designProjectId = string.IsNullOrWhiteSpace(group.Project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(group.Project, ct);

        var designTask = new TaskItem
        {
            Title         = $"[Design] {group.Title}",
            Description   = "設計規劃多 Agent 會議",
            TriggeredBy   = "Orchestrator",
            AssignedAgent = AgentNames.Design,
            Status        = "running",
            GroupId       = group.Id,
            ProjectId     = designProjectId,
        };
        taskRepo.Add(designTask);
        await taskRepo.SaveAsync(ct);
        taskRepo.AddLog(new TaskLog { TaskId = designTask.Id, Agent = AgentNames.Design, Step = "設計規劃多 Agent 會議進行中...", Status = "running" });
        await taskRepo.SaveAsync(ct);

        await pushService.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = AgentNames.Pm,
            Status           = "running",
            CurrentTaskTitle = $"設計規劃會議：{group.Title}"
        });
        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = designTask.Id,
            GroupId   = group.Id,
            Title     = designTask.Title,
            AgentName = AgentNames.Design,
            Status    = "running"
        });

        try
        {
            var designResult = await designMeetingService.RunDesignMeetingAsync(group, owner, repo, ct);

            await using var scope2 = serviceProvider.CreateAsyncScope();
            var taskRepo2  = scope2.ServiceProvider.GetRequiredService<TaskRepository>();
            var freshGroup = await taskRepo2.GetGroupByIdAsync(group.Id, ct);
            if (freshGroup is null)
            {
                logger.LogError("MeetingOrchestration：Design 完成後找不到 Group={Id}", group.Id);
                return;
            }

            freshGroup.DesignMeetingLog = designResult.MeetingLog;
            freshGroup.DesignPlan       = designResult.DesignPlan;
            freshGroup.DesignRound      = designResult.TotalRounds;
            if (!string.IsNullOrWhiteSpace(designResult.IssueUrls))
                freshGroup.IssueUrls = designResult.IssueUrls;
            if (!string.IsNullOrWhiteSpace(designResult.UiSpecContent))
                freshGroup.UiSpecContent = designResult.UiSpecContent;
            await taskRepo2.SaveAsync(ct);

            logger.LogInformation("MeetingOrchestration：設計規劃會議記錄已存入 DB（Group={Id}）", group.Id);

            taskRepo.AddLog(new TaskLog { TaskId = designTask.Id, Agent = AgentNames.Design, Step = $"設計規劃完成（共 {designResult.TotalRounds} 輪）", Status = "done" });
            taskRepo.UpdateStatus(designTask, "done");
            await taskRepo.SaveAsync(ct);
            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = designTask.Id,
                GroupId   = group.Id,
                Title     = designTask.Title,
                AgentName = AgentNames.Design,
                Status    = "done"
            });

            if (designResult.FinalDecision == "consensus")
            {
                logger.LogInformation("MeetingOrchestration：設計規劃 consensus，直接進入 Dev_plan（Group={Id}）", group.Id);
                var tgs = serviceProvider.GetRequiredService<TaskGroupService>();
                await tgs.FireStepsAsync(freshGroup, [new WorkflowStep("Dev_plan")], ct);
            }
            else
            {
                var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
                if (ceoChannel is null)
                {
                    logger.LogError("MeetingOrchestration：找不到 CEO 頻道，無法上呈設計規劃結果");
                    return;
                }

                var planPreview = string.IsNullOrWhiteSpace(freshGroup.DesignPlan)
                    ? "（無設計規劃書）"
                    : freshGroup.DesignPlan.Length > 600
                        ? freshGroup.DesignPlan[..600] + "\n...\n（完整內容請查看 Dashboard）"
                        : freshGroup.DesignPlan;

                var embed = new EmbedBuilder()
                    .WithTitle("🎨 設計規劃會議需上呈確認")
                    .WithColor(Color.Purple)
                    .AddField("任務", freshGroup.Title)
                    .AddField("上呈原因", designResult.EscalateReason ?? "設計存在分歧，需老闆裁決")
                    .AddField("設計規劃書摘要", planPreview)
                    .AddField("會議輪次", designResult.TotalRounds.ToString())
                    .WithFooter("▶️ 繼續 = 進入 Dev_plan；⏹️ 停止 = 取消任務；✏️ 修改 = 提供設計指引")
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                var buttons = new ComponentBuilder()
                    .WithButton("▶️ 繼續開發",  $"design_continue_{freshGroup.Id}", ButtonStyle.Success)
                    .WithButton("⏹️ 停止任務",  $"design_stop_{freshGroup.Id}",     ButtonStyle.Danger)
                    .WithButton("✏️ 修改設計",  $"design_modify_{freshGroup.Id}",   ButtonStyle.Secondary)
                    .Build();

                var msg = await ceoChannel.SendMessageAsync(embed: embed, components: buttons);

                var commandHandler = serviceProvider.GetRequiredService<CommandHandler>();
                commandHandler.RegisterDesignConfirmation(msg.Id, freshGroup.Id, designResult.PetraSessionId, designResult.EscalateReason);

                _ = interactionService.CreateInteractionAsync(
                    "design",
                    title:                $"設計確認：{freshGroup.Title}",
                    description:          planPreview,
                    project:              freshGroup.Project,
                    agentName:            AgentNames.Pm,
                    availableActionsJson: InteractionService.DesignActionsJson,
                    contextJson:          JsonSerializer.Serialize(new
                    {
                        channelId      = ceoChannel.Id.ToString(),
                        groupId        = freshGroup.Id.ToString(),
                        petraSessionId = designResult.PetraSessionId
                    }),
                    discordMessageId: (decimal)msg.Id,
                    taskGroupId:      freshGroup.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MeetingOrchestration：設計規劃會議失敗（Group={Id}）", group.Id);

            taskRepo.AddLog(new TaskLog { TaskId = designTask.Id, Agent = AgentNames.Design, Step = $"設計規劃失敗：{ex.Message}", Status = "failed" });
            taskRepo.UpdateStatus(designTask, "failed");
            await taskRepo.SaveAsync(CancellationToken.None);
            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = designTask.Id,
                GroupId   = group.Id,
                Title     = designTask.Title,
                AgentName = AgentNames.Design,
                Status    = "failed"
            });
        }
        finally
        {
            await db.TaskGroups
                .Where(g => g.Id == group.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveMeetingType, (string?)null),
                    CancellationToken.None);

            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = AgentNames.Pm,
                Status           = "idle",
                CurrentTaskTitle = null
            });
        }
    }

    // ============================================================
    //  Crash Recovery（Stage 31）
    // ============================================================

    /// <summary>Bot 啟動時掃描 ActiveMeetingType != null 的 TaskGroup，自動重跑被中斷的會議。</summary>
    public async Task RecoverStuckMeetingsAsync(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stuckGroups = await db.TaskGroups
            .Where(g => g.ActiveMeetingType != null)
            .ToListAsync(ct);

        if (stuckGroups.Count == 0) return;

        logger.LogWarning("會議 Crash Recovery：{N} 個卡住的會議等待重跑：{Details}",
            stuckGroups.Count,
            string.Join(", ", stuckGroups.Select(g => $"{g.ActiveMeetingType} × {g.Id}")));

        foreach (var group in stuckGroups)
        {
            var meetingType = group.ActiveMeetingType;
            try
            {
                if (meetingType == "Kickoff")
                    await RunKickoffMeetingAndWaitAsync(group, ct);
                else if (meetingType == "Design")
                    await RunDesignPhaseAsync(group, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "會議恢復失敗（GroupId={Id}，MeetingType={Type}）",
                    group.Id, meetingType);
            }
        }
    }

    // ============================================================
    //  Christ 按鈕確認路由
    // ============================================================

    /// <summary>Stage 25a：Christ 確認 Kick-off 計劃書後的路由處理。</summary>
    public async Task HandleKickoffConfirmedAsync(
        Guid groupId,
        string decision,
        string? modifyContent = null,
        CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var group = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null)
        {
            logger.LogError("MeetingOrchestration：HandleKickoffConfirmed 找不到 Group={Id}", groupId);
            return;
        }

        var owner = _gitHub.Owner;
        var repo  = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;
        var tgs   = serviceProvider.GetRequiredService<TaskGroupService>();

        switch (decision.ToLower())
        {
            case "continue":
                logger.LogInformation("MeetingOrchestration：Kick-off 確認繼續（Group={Id}）", groupId);
                await meetingCommons.CloseAllSessionsAsync(groupId);
                await tgs.FireStepsAsync(group, [new WorkflowStep(AgentNames.Design)], ct);
                break;

            case "stop":
                logger.LogInformation("MeetingOrchestration：Kick-off 確認停止（Group={Id}）", groupId);
                await meetingCommons.CloseAllSessionsAsync(groupId);
                taskRepo.UpdateGroupStatus(group, "cancelled");
                await taskRepo.SaveAsync(ct);

                var ceoChannelStop = FindChannel(_discord.Channels.CeoChannel);
                if (ceoChannelStop is not null)
                    await ceoChannelStop.SendMessageAsync(
                        $"⏹️ 任務《{group.Title}》已停止，Kick-off 會議後由老闆決定取消。");
                break;

            case "modify":
                if (string.IsNullOrWhiteSpace(modifyContent))
                {
                    logger.LogWarning("MeetingOrchestration：Kick-off 修改意見為空（Group={Id}）", groupId);
                    return;
                }

                logger.LogInformation("MeetingOrchestration：Kick-off 計劃書修改（Group={Id}）", groupId);

                var modifyResult = await kickoffMeetingService.ModifyTaskPlanAsync(
                    group, modifyContent, owner, repo, ct);

                var modifyLogEntry =
                    $"\n## Christ 修改 Round {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n" +
                    $"### Christ 修改意見\n{modifyContent}\n\n" +
                    $"### Petra 回應（完整）\n{modifyResult.PetraFullOutput}\n";

                group.KickoffMeetingLog = (group.KickoffMeetingLog ?? "") + modifyLogEntry;

                if (modifyResult.Impact == "small" && !string.IsNullOrWhiteSpace(modifyResult.RevisedPlan))
                    group.TaskPlan = modifyResult.RevisedPlan;

                await taskRepo.SaveAsync(ct);

                var ceoChannelModify = FindChannel(_discord.Channels.CeoChannel);
                if (ceoChannelModify is null) return;

                if (modifyResult.Impact == "large")
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("⚠️ Petra 評估：建議重新召開 Kick-off")
                        .WithColor(Color.Orange)
                        .AddField("任務", group.Title)
                        .AddField("Petra 評估", modifyResult.PetraFullOutput.Length > 800
                            ? modifyResult.PetraFullOutput[..800] + "..." : modifyResult.PetraFullOutput)
                        .WithFooter("請選擇：重新開會 或 取消任務")
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .Build();

                    var buttons = new ComponentBuilder()
                        .WithButton("🔄 重新召開 Kick-off", $"kickoff_restart_{group.Id}", ButtonStyle.Primary)
                        .WithButton("⏹️ 取消任務",          $"kickoff_stop_{group.Id}",    ButtonStyle.Danger)
                        .Build();

                    var reMsg = await ceoChannelModify.SendMessageAsync(embed: embed, components: buttons);
                    var commandHandler = serviceProvider.GetRequiredService<CommandHandler>();
                    commandHandler.RegisterKickoffConfirmation(reMsg.Id, group.Id,
                        modifyResult.RevisedPlan ?? group.TaskPlan ?? "");

                    _ = interactionService.CreateInteractionAsync(
                        "kickoff",
                        title:                $"Kickoff 確認：{group.Title}",
                        description:          modifyResult.RevisedPlan ?? group.TaskPlan ?? "",
                        project:              group.Project,
                        agentName:            AgentNames.Pm,
                        availableActionsJson: InteractionService.KickoffActionsJson,
                        contextJson:          JsonSerializer.Serialize(new
                        {
                            channelId = ceoChannelModify.Id.ToString(),
                            groupId   = group.Id.ToString()
                        }),
                        discordMessageId: (decimal)reMsg.Id,
                        taskGroupId:      group.Id);
                }
                else
                {
                    var planPreview = (modifyResult.RevisedPlan ?? group.TaskPlan ?? "").Length > 500
                        ? (modifyResult.RevisedPlan ?? group.TaskPlan ?? "")[..500] + "\n..."
                        : (modifyResult.RevisedPlan ?? group.TaskPlan ?? "");

                    var embed = new EmbedBuilder()
                        .WithTitle("✏️ 任務計劃書已更新")
                        .WithColor(Color.Green)
                        .AddField("任務", group.Title)
                        .AddField("更新後計劃書摘要", planPreview)
                        .WithFooter("▶️ 繼續 = 進入開發；⏹️ 停止 = 取消；✏️ 修改 = 繼續調整")
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .Build();

                    var buttons = new ComponentBuilder()
                        .WithButton("▶️ 繼續開發",  $"kickoff_continue_{group.Id}", ButtonStyle.Success)
                        .WithButton("⏹️ 停止任務",  $"kickoff_stop_{group.Id}",     ButtonStyle.Danger)
                        .WithButton("✏️ 修改計劃書", $"kickoff_modify_{group.Id}",  ButtonStyle.Secondary)
                        .Build();

                    var reMsg = await ceoChannelModify.SendMessageAsync(embed: embed, components: buttons);
                    var commandHandler = serviceProvider.GetRequiredService<CommandHandler>();
                    commandHandler.RegisterKickoffConfirmation(reMsg.Id, group.Id, planPreview);

                    _ = interactionService.CreateInteractionAsync(
                        "kickoff",
                        title:                $"Kickoff 確認：{group.Title}",
                        description:          planPreview,
                        project:              group.Project,
                        agentName:            AgentNames.Pm,
                        availableActionsJson: InteractionService.KickoffActionsJson,
                        contextJson:          JsonSerializer.Serialize(new
                        {
                            channelId = ceoChannelModify.Id.ToString(),
                            groupId   = group.Id.ToString()
                        }),
                        discordMessageId: (decimal)reMsg.Id,
                        taskGroupId:      group.Id);
                }
                break;

            case "restart":
                logger.LogInformation("MeetingOrchestration：Kick-off 重新召開（Group={Id}）", groupId);
                await meetingCommons.CloseAllSessionsAsync(groupId);
                group.KickoffRound = 0;
                await taskRepo.SaveAsync(ct);
                await tgs.FireStepsAsync(group, [new WorkflowStep(AgentNames.Kickoff)], ct);
                break;

            default:
                logger.LogWarning("MeetingOrchestration：未知的 Kickoff 決策：{Decision}（Group={Id}）", decision, groupId);
                break;
        }
    }

    /// <summary>Stage 25b：Christ 確認設計規劃後的路由處理。</summary>
    public async Task HandleDesignConfirmedAsync(
        Guid groupId,
        string decision,
        string petraSessionId,
        string? modifyContent = null,
        CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var group = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null)
        {
            logger.LogError("MeetingOrchestration：HandleDesignConfirmed 找不到 Group={Id}", groupId);
            return;
        }

        var owner = _gitHub.Owner;
        var repo  = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;
        var tgs   = serviceProvider.GetRequiredService<TaskGroupService>();

        switch (decision.ToLower())
        {
            case "continue":
                logger.LogInformation("MeetingOrchestration：設計規劃確認繼續（Group={Id}）", groupId);
                await tgs.FireStepsAsync(group, [new WorkflowStep("Dev_plan")], ct);
                break;

            case "stop":
                logger.LogInformation("MeetingOrchestration：設計規劃確認停止（Group={Id}）", groupId);
                await meetingCommons.CloseAllSessionsAsync(groupId);
                taskRepo.UpdateGroupStatus(group, "cancelled");
                await taskRepo.SaveAsync(ct);

                var ceoChannelStop = FindChannel(_discord.Channels.CeoChannel);
                if (ceoChannelStop is not null)
                    await ceoChannelStop.SendMessageAsync(
                        $"⏹️ 任務《{group.Title}》已停止，設計規劃會議後由老闆決定取消。");
                break;

            case "modify":
                if (string.IsNullOrWhiteSpace(modifyContent))
                {
                    logger.LogWarning("MeetingOrchestration：設計規劃修改意見為空（Group={Id}）", groupId);
                    return;
                }

                logger.LogInformation("MeetingOrchestration：設計規劃修改（Group={Id}）", groupId);

                await pushService.PushAgentStatusAsync(new AgentStatusViewModel
                {
                    AgentName        = AgentNames.Pm,
                    Status           = "running",
                    CurrentTaskTitle = $"設計規劃修改：{group.Title}"
                });

                try
                {
                    var modifyResult = await designMeetingService.ModifyDesignPlanAsync(
                        group, modifyContent, petraSessionId, owner, repo, ct);

                    var modifyLogEntry =
                        $"\n## Christ 設計修改 {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n" +
                        $"### Christ 修改指引\n{modifyContent}\n\n" +
                        $"### Petra 修改後設計規劃書\n{modifyResult.RevisedPlan}\n";
                    group.DesignMeetingLog = (group.DesignMeetingLog ?? "") + modifyLogEntry;

                    if (!string.IsNullOrWhiteSpace(modifyResult.RevisedPlan))
                        group.DesignPlan = modifyResult.RevisedPlan;
                    await taskRepo.SaveAsync(ct);

                    var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
                    if (ceoChannel is null) return;

                    var planPreview = (modifyResult.RevisedPlan ?? group.DesignPlan ?? "").Length > 600
                        ? (modifyResult.RevisedPlan ?? group.DesignPlan ?? "")[..600] + "\n..."
                        : (modifyResult.RevisedPlan ?? group.DesignPlan ?? "");

                    var embed = new EmbedBuilder()
                        .WithTitle("✏️ 設計規劃書已修改")
                        .WithColor(Color.Green)
                        .AddField("任務", group.Title)
                        .AddField("修改後設計規劃書摘要", planPreview)
                        .WithFooter("▶️ 繼續 = 進入 Dev_plan；⏹️ 停止 = 取消；✏️ 修改 = 繼續調整")
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .Build();

                    var buttons = new ComponentBuilder()
                        .WithButton("▶️ 繼續開發", $"design_continue_{group.Id}", ButtonStyle.Success)
                        .WithButton("⏹️ 停止任務", $"design_stop_{group.Id}",    ButtonStyle.Danger)
                        .WithButton("✏️ 修改設計", $"design_modify_{group.Id}",  ButtonStyle.Secondary)
                        .Build();

                    var reMsg = await ceoChannel.SendMessageAsync(embed: embed, components: buttons);
                    var commandHandler = serviceProvider.GetRequiredService<CommandHandler>();
                    commandHandler.RegisterDesignConfirmation(reMsg.Id, group.Id, petraSessionId, null);

                    _ = interactionService.CreateInteractionAsync(
                        "design",
                        title:                $"設計確認：{group.Title}",
                        description:          planPreview,
                        project:              group.Project,
                        agentName:            AgentNames.Pm,
                        availableActionsJson: InteractionService.DesignActionsJson,
                        contextJson:          JsonSerializer.Serialize(new
                        {
                            channelId     = ceoChannel.Id.ToString(),
                            groupId       = group.Id.ToString(),
                            petraSessionId
                        }),
                        discordMessageId: (decimal)reMsg.Id,
                        taskGroupId:      group.Id);
                }
                finally
                {
                    await pushService.PushAgentStatusAsync(new AgentStatusViewModel
                    {
                        AgentName        = AgentNames.Pm,
                        Status           = "idle",
                        CurrentTaskTitle = null
                    });
                }
                break;

            default:
                logger.LogWarning("MeetingOrchestration：未知的 Design 決策：{Decision}（Group={Id}）", decision, groupId);
                break;
        }
    }

    // ============================================================
    //  Helpers
    // ============================================================

    /// <summary>Stage 25a：組建 Kick-off 會議的提案說明內容。</summary>
    private static string BuildKickoffProposalContent(TaskGroup group)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(group.Title);

        if (!string.IsNullOrWhiteSpace(group.IssueUrls))
            sb.AppendLine($"\nIssue URLs：{group.IssueUrls}");

        if (!string.IsNullOrWhiteSpace(group.UiSpecContent))
        {
            sb.AppendLine("\nUI 規格說明：");
            sb.AppendLine(group.UiSpecContent);
        }

        return sb.ToString();
    }

    private IMessageChannel? FindChannel(string channelName)
    {
        if (!ulong.TryParse(_discord.GuildId, out var guildId)) return null;
        return discordClient.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }
}
