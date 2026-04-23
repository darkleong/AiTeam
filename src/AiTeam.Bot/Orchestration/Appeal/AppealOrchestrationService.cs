using System.Text.Json;
using System.Text.RegularExpressions;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord;
using AiTeam.Bot.GitHub;
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

namespace AiTeam.Bot.Orchestration.Appeal;

/// <summary>
/// Stage 36：Review / Dev_plan Appeal 編排（從 TaskGroupService 拆解）。
///
/// 職責：
///   - Review Appeal 迴圈 A / Petra 仲裁（HandleReviewerCompleted / RunPetraGate / RunPetraArbitration）
///   - Dev_plan Appeal 迴圈（RunDevPlanAppealLoop / RunPetraDevPlanReview / FinalizePetraDevPlanTask / NotifyBossDevPlanEscalation）
///   - Dev_plan escalate 路由（HandleDevPlanEscalation）
///   - Dev Blocker 仲裁（HandleDevBlocker）
/// </summary>
public class AppealOrchestrationService(
    IServiceProvider serviceProvider,
    DiscordSocketClient discordClient,
    IOptions<DiscordSettings> discordSettings,
    IOptions<GitHubSettings> gitHubSettings,
    WorkflowSettingsResolver workflowResolver,
    InteractionService interactionService,
    ILogger<AppealOrchestrationService> logger)
{
    private readonly DiscordSettings _discord = discordSettings.Value;
    private readonly GitHubSettings  _gitHub  = gitHubSettings.Value;

    // ============================================================
    //  Review Appeal（Stage 23-24）
    // ============================================================

    /// <summary>
    /// Vera 執行成功後：
    /// - 無 Critical → Petra 閘門
    /// - 有 Critical → Cody-Vera Appeal 迴圈（maxRounds 輪），仍有 → Petra 仲裁，全 agree → Petra 閘門維持
    /// 回傳 null 表示已 escalate。
    /// </summary>
    public async Task<AgentExecutionResult?> HandleReviewerCompletedAsync(
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        // Stage 37：Crash Recovery 標記。涵蓋短路（直接 Petra Gate）與 Appeal 迴圈兩條路徑，
        // 因為 RunPetraGateAsync 內部仍會呼叫 Petra CLI subprocess，同樣有卡住風險。
        await using var dbScope = serviceProvider.CreateAsyncScope();
        var db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == group.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, "ReviewAppeal"),
                cancellationToken);

        try
        {
            if (result.CriticalReviewCount == 0)
                return await RunPetraGateAsync(group, result, taskRepo, projectId, cancellationToken);

            var maxRounds  = await workflowResolver.GetReviewAppealMaxRoundsAsync(cancellationToken);
            var reviewBody = group.LastReviewBody ?? result.ReviewBody ?? "";

            await using var scope = serviceProvider.CreateAsyncScope();
            var pmService = scope.ServiceProvider.GetRequiredService<ReviewAppealService>();

            var currentCriticalIds = ExtractCriticalIdsFromReviewBody(reviewBody);
            if (currentCriticalIds.Count == 0)
            {
                logger.LogWarning("有 Critical 但無法解析 ID，直接走 Petra 閘門（Group={Id}）", group.Id);
                return await RunPetraGateAsync(group, result, taskRepo, projectId, cancellationToken);
            }

            var indentedOptions = new JsonSerializerOptions { WriteIndented = true };

            while (group.ReviewAppealRoundA < maxRounds && currentCriticalIds.Count > 0)
            {
                var round = group.ReviewAppealRoundA + 1;
                logger.LogInformation("Appeal Round A {Round}（Group={Id}）", round, group.Id);

                var priorContext = group.ReviewAppealRoundA > 0 ? group.ReviewAppealLog : null;
                var codyAppeal   = await pmService.RunCodyAppealAsync(
                    group, reviewBody, group.Title, currentCriticalIds, priorContext, cancellationToken);
                var codyJson     = JsonSerializer.Serialize(codyAppeal, indentedOptions);

                var disagrees = codyAppeal.Items.Where(i => i.Response == "disagree").ToList();

                if (disagrees.Count == 0)
                {
                    AppendAppealLog(group, round,
                        $"**Cody 回應（完整）：**\n```json\n{codyJson}\n```\n\n→ Cody 同意所有 Critical，進入修正流程。");
                    group.ReviewAppealRoundA++;
                    await taskRepo.SaveAsync(cancellationToken);
                    break;
                }

                var veraResponse = await pmService.RunVeraAppealAsync(group, reviewBody, codyJson, cancellationToken);
                var veraJson     = JsonSerializer.Serialize(veraResponse, indentedOptions);

                currentCriticalIds = currentCriticalIds
                    .Where(id => !veraResponse.AcceptedIds.Contains(id))
                    .ToList();

                AppendAppealLog(group, round,
                    $"**Cody 回應（完整）：**\n```json\n{codyJson}\n```\n\n" +
                    $"**Vera 重評（完整）：**\n```json\n{veraJson}\n```\n\n" +
                    $"→ Vera 接受 {veraResponse.AcceptedIds.Count} 項，維持 {veraResponse.MaintainedIds.Count} 項，" +
                    $"剩餘 Critical：{currentCriticalIds.Count}");
                group.ReviewAppealRoundA++;
                await taskRepo.SaveAsync(cancellationToken);
            }

            if (currentCriticalIds.Count == 0)
            {
                result = result with { CriticalReviewCount = 0 };
                return await RunPetraGateAsync(group, result, taskRepo, projectId, cancellationToken);
            }

            if (group.ReviewAppealRoundA >= maxRounds)
                return await RunPetraArbitrationAsync(group,
                    result with { CriticalReviewCount = currentCriticalIds.Count },
                    taskRepo, projectId, cancellationToken);

            result = result with { CriticalReviewCount = currentCriticalIds.Count };
            return await RunPetraGateAsync(group, result, taskRepo, projectId, cancellationToken);
        }
        finally
        {
            await db.TaskGroups.Where(g => g.Id == group.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, (string?)null),
                    CancellationToken.None);
        }
    }

    /// <summary>Petra 審核閘門：送 ReviewVeraAsync 並依 approve/revise/escalate 決策。</summary>
    public async Task<AgentExecutionResult?> RunPetraGateAsync(
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var petraVeraReview = await RunPetraVeraReviewAsync(group, result, projectId, cancellationToken);
        switch (petraVeraReview.Decision)
        {
            case "approve":
                return result with { CriticalReviewCount = 0 };

            case "revise":
                result = result with { CriticalReviewCount = 1 };
                if (!string.IsNullOrWhiteSpace(petraVeraReview.RevisionInstructions))
                {
                    group.LastReviewBody =
                        (group.LastReviewBody ?? "") +
                        "\n\n【Petra 修正指示】" + petraVeraReview.RevisionInstructions;
                    await taskRepo.SaveAsync(cancellationToken);
                }
                return result;

            default: // escalate
                taskRepo.UpdateGroupStatus(group, "failed");
                await taskRepo.SaveAsync(cancellationToken);
                var tgs = serviceProvider.GetRequiredService<TaskGroupService>();
                await tgs.NotifyBossInterventionAsync(group, cancellationToken);
                return null;
        }
    }

    /// <summary>Appeal 達輪次上限後 Petra 仲裁；仲裁後設 SkipReviewerAfterArbitration = true。</summary>
    public async Task<AgentExecutionResult?> RunPetraArbitrationAsync(
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var pmService = scope.ServiceProvider.GetRequiredService<ReviewAppealService>();

        logger.LogInformation("Appeal 達上限，啟動 Petra 仲裁（Group={Id}）", group.Id);
        var arbitration = await pmService.ArbitrateReviewAppealAsync(
            group, group.LastReviewBody ?? "", group.ReviewAppealLog ?? "", cancellationToken);
        var arbitrationJson = JsonSerializer.Serialize(arbitration,
            new JsonSerializerOptions { WriteIndented = true });

        AppendAppealLog(group, group.ReviewAppealRoundA,
            $"**Petra 仲裁（完整）：**\n```json\n{arbitrationJson}\n```\n\n" +
            $"→ 最終 Critical：{arbitration.FinalCriticals.Count} 項，決定：{arbitration.Decision}");

        group.SkipReviewerAfterArbitration = true;
        await taskRepo.SaveAsync(cancellationToken);

        result = result with { CriticalReviewCount = arbitration.FinalCriticals.Count };
        return await RunPetraGateAsync(group, result, taskRepo, projectId, cancellationToken);
    }

    /// <summary>Dev / Dev_fix 回報阻礙（[BLOCKED] 格式）時，由 Petra 評估路由。</summary>
    public async Task HandleDevBlockerAsync(
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var pmService  = scope.ServiceProvider.GetRequiredService<PmRoutingService>();
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);

        BlockerDecision decision;
        try
        {
            decision = await pmService.AssessBlockerAsync(result.OutputContent!, group.Title, cancellationToken);
            logger.LogInformation("Petra Blocker 評估：Group={Id}，routing={Routing}", group.Id, decision.Routing);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HandleDevBlockerAsync Petra 評估失敗，fallback escalate_boss");
            decision = new BlockerDecision("escalate_boss", "Blocker 評估失敗，升級給老闆");
        }

        var tgs = serviceProvider.GetRequiredService<TaskGroupService>();

        switch (decision.Routing)
        {
            case "continue":
                logger.LogInformation("Petra 決定重試 Dev：Group={Id}", group.Id);
                if (ceoChannel is not null)
                    await ceoChannel.SendMessageAsync(
                        $"⚠️ **{group.Title}** — Cody 回報阻礙，Petra 判定可重試，自動重新觸發 Dev。\n" +
                        $"原因：{decision.Instructions}");
                await tgs.FireStepsAsync(group, [new WorkflowStep("Dev")], cancellationToken);
                break;

            case "escalate_victoria":
                logger.LogWarning("Blocker 升級給 Victoria：Group={Id}", group.Id);
                taskRepo.UpdateGroupStatus(group, "failed");
                await taskRepo.SaveAsync(cancellationToken);
                if (ceoChannel is not null)
                    await ceoChannel.SendMessageAsync(
                        $"🚫 **{group.Title}** — Cody 開發阻礙，需要 Victoria CEO 決策。\n" +
                        $"阻礙詳情：{result.Summary}\nPetra 建議：{decision.Instructions}");
                break;

            default: // escalate_boss
                logger.LogWarning("Blocker 升級給老闆：Group={Id}", group.Id);
                taskRepo.UpdateGroupStatus(group, "failed");
                await taskRepo.SaveAsync(cancellationToken);
                if (ceoChannel is not null)
                    await ceoChannel.SendMessageAsync(
                        $"🚫 **{group.Title}** — Cody 開發阻礙，需要您介入。\n" +
                        $"阻礙詳情：{result.Summary}\nPetra 分析：{decision.Instructions}");
                break;
        }
    }

    // ============================================================
    //  Dev_plan 審核 + Appeal
    // ============================================================

    /// <summary>
    /// Stage 37：Dev_plan 完成後 Petra 審核 + Appeal 流程的上層入口。
    /// 從 TaskGroupService dispatcher 搬遷而來（搭車修正 Stage 36 未完全搬進的遺漏），
    /// 集中 Crash Recovery try-finally。
    ///
    /// 回傳：
    ///   - true  → caller 應繼續 fall through 走後續 dispatcher（Petra approve）
    ///   - false → 已處理（revise 成功觸發 Dev、revise 耗盡 escalate、或 Petra escalate），caller 應 return
    /// </summary>
    public async Task<bool> HandleDevPlanCompletedAsync(
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var dbScope = serviceProvider.CreateAsyncScope();
        var db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == group.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, "DevPlanAppeal"),
                cancellationToken);

        try
        {
            group.DevPlan = result.OutputContent ?? result.Summary;
            await taskRepo.SaveAsync(cancellationToken);

            var (petraDevPlanReview, petraDevPlanTaskId) =
                await RunPetraDevPlanReviewAsync(group, result, projectId, cancellationToken);

            switch (petraDevPlanReview.Decision)
            {
                case "approve":
                    return true; // 繼續走後續 dispatcher → 觸發 Dev

                case "revise":
                    var appealApproved = await RunDevPlanAppealLoopAsync(
                        group, petraDevPlanReview, taskRepo, cancellationToken);
                    await FinalizePetraDevPlanTaskAsync(
                        petraDevPlanTaskId, appealApproved, group, cancellationToken);
                    if (appealApproved)
                    {
                        logger.LogInformation("Dev_plan Appeal 說服成功，直接觸發 Dev（Group={Id}）", group.Id);
                        var tgs = dbScope.ServiceProvider.GetRequiredService<TaskGroupService>();
                        await tgs.FireStepsAsync(group, [new WorkflowStep(AgentNames.Dev)], cancellationToken);
                    }
                    else
                    {
                        logger.LogWarning("Dev_plan Appeal 耗盡，升級老闆（Group={Id}）", group.Id);
                        taskRepo.UpdateGroupStatus(group, "failed");
                        await taskRepo.SaveAsync(cancellationToken);
                        await NotifyBossDevPlanEscalationAsync(group, petraDevPlanReview, cancellationToken);
                    }
                    return false;

                default: // escalate
                    taskRepo.UpdateGroupStatus(group, "failed");
                    await taskRepo.SaveAsync(cancellationToken);
                    await NotifyBossDevPlanEscalationAsync(group, petraDevPlanReview, cancellationToken);
                    return false;
            }
        }
        finally
        {
            await db.TaskGroups.Where(g => g.Id == group.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, (string?)null),
                    CancellationToken.None);
        }
    }

    /// <summary>
    /// 建立 Petra TaskItem、呼叫 ReviewDevPlanAsync、推送狀態、通知 #petra-pm 頻道。
    /// </summary>
    public async Task<(PetraReview, Guid)> RunPetraDevPlanReviewAsync(
        TaskGroup group,
        AgentExecutionResult devPlanResult,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo     = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService  = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var pmService    = scope.ServiceProvider.GetRequiredService<PmReviewService>();
        var gitHubService= scope.ServiceProvider.GetRequiredService<GitHubService>();

        var petraTask = new TaskItem
        {
            Title         = $"[Petra→Dev_plan] {group.Title}",
            Description   = "審核 Cody 實作計畫書",
            TriggeredBy   = "Orchestrator",
            AssignedAgent = AgentNames.Pm,
            Status        = "running",
            GroupId       = group.Id,
            ProjectId     = projectId,
        };
        taskRepo.Add(petraTask);
        await taskRepo.SaveAsync(cancellationToken);
        taskRepo.AddLog(new TaskLog { TaskId = petraTask.Id, Agent = AgentNames.Pm, Step = "Petra 審核實作計畫書中...", Status = "running" });
        await taskRepo.SaveAsync(cancellationToken);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = petraTask.Id,
            GroupId   = group.Id,
            Title     = petraTask.Title,
            AgentName = petraTask.AssignedAgent,
            Status    = "running"
        });
        await pushService.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = AgentNames.Pm,
            Status           = "running",
            CurrentTaskTitle = group.Title
        });

        var petraChannel = FindChannel(_discord.Channels.PmChannel);
        if (petraChannel is not null)
            await petraChannel.SendMessageAsync(
                $"🔍 **Petra 審核 Cody 實作計畫書**\n任務：{group.Title}（第 {group.DevPlanRevision + 1} 輪）");

        var owner     = _gitHub.Owner;
        var repo      = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;
        var localPath = "";
        PetraReview petraReview;

        try
        {
            localPath = gitHubService.CloneOrPull(owner, repo, $"petra-{group.Id:N}"[..12]);
            petraReview = await pmService.ReviewDevPlanAsync(
                group.Title,
                devPlanResult.OutputContent ?? devPlanResult.Summary,
                group.IssueUrls,
                group.UiSpecContent,
                localPath,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RunPetraDevPlanReviewAsync workspace 失敗，fallback approve");
            petraReview = new PetraReview("approve", "Petra workspace 失敗，自動放行", [], null);
        }
        finally
        {
            if (!string.IsNullOrEmpty(localPath))
                gitHubService.CleanupLocalRepo(localPath);
        }

        var petraStatus = petraReview.Decision == "revise" ? "revision"
                        : petraReview.Decision == "escalate" ? "failed"
                        : "done";
        taskRepo.AddLog(new TaskLog { TaskId = petraTask.Id, Agent = AgentNames.Pm, Step = petraReview.Summary, Status = petraStatus });
        taskRepo.UpdateStatus(petraTask, petraStatus);
        await taskRepo.SaveAsync(cancellationToken);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = petraTask.Id,
            GroupId   = group.Id,
            Title     = petraTask.Title,
            AgentName = petraTask.AssignedAgent,
            Status    = petraStatus
        });
        await pushService.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = AgentNames.Pm,
            Status           = petraStatus is "failed" ? "error" : "idle",
            CurrentTaskTitle = petraStatus is "failed" ? petraReview.Summary : null
        });

        if (petraChannel is not null)
            await petraChannel.SendMessageAsync(
                $"📋 **Petra 審核結果**（Dev_plan 第 {group.DevPlanRevision + 1} 輪）：**{petraReview.Decision.ToUpper()}**\n{petraReview.Summary}");

        logger.LogInformation("Petra Dev_plan 審核：Group={Id}，decision={Decision}", group.Id, petraReview.Decision);
        return (petraReview, petraTask.Id);
    }

    /// <summary>Dev_plan Appeal 結束後，將 [Petra→Dev_plan] TaskItem 最終狀態更新並推送 SignalR。</summary>
    public async Task FinalizePetraDevPlanTaskAsync(
        Guid petraTaskId,
        bool approved,
        TaskGroup group,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var task = await taskRepo.GetByIdAsync(petraTaskId, cancellationToken);
        if (task is null) return;

        var finalStatus = approved ? "done" : "failed";
        taskRepo.UpdateStatus(task, finalStatus);
        await taskRepo.SaveAsync(cancellationToken);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = task.Id,
            GroupId   = group.Id,
            Title     = task.Title,
            AgentName = task.AssignedAgent,
            Status    = finalStatus
        });
    }

    /// <summary>
    /// Stage 24：Dev_plan Appeal 緊密迴圈（純 LLM，不跨 Agent 執行）。
    /// Cody 反駁 → Petra 重評 → 迴圈上限 → 回傳 true（說服成功）或 false（耗盡）。
    /// </summary>
    public async Task<bool> RunDevPlanAppealLoopAsync(
        TaskGroup group,
        PetraReview initialReview,
        TaskRepository taskRepo,
        CancellationToken cancellationToken)
    {
        var maxRounds     = await workflowResolver.GetDevPlanAppealMaxRoundsAsync(cancellationToken);
        var currentReview = initialReview;
        var priorContext  = $"Petra 初審意見：{initialReview.Summary}";

        await using var scope = serviceProvider.CreateAsyncScope();
        var pmService = scope.ServiceProvider.GetRequiredService<DevPlanAppealService>();

        while (group.DevPlanAppealRoundA < maxRounds)
        {
            group.DevPlanAppealRoundA++;

            var codyAppeal = await pmService.RunCodyDevPlanAppealAsync(
                group, currentReview, priorContext, cancellationToken);
            var codyJson = JsonSerializer.Serialize(codyAppeal);

            if (codyAppeal.Position == "accept")
            {
                AppendDevPlanAppealLog(group, group.DevPlanAppealRoundA,
                    $"**Cody 接受修改意見，Appeal 終止。**\n```json\n{codyJson}\n```");
                await taskRepo.SaveAsync(cancellationToken);
                logger.LogInformation("Dev_plan Appeal Round {Round}：Cody 接受，共識達成（Group={Id}）",
                    group.DevPlanAppealRoundA, group.Id);
                return true;
            }

            var newReview = await pmService.ReassessDevPlanAsync(group, codyAppeal, currentReview, cancellationToken);
            var petraJson = JsonSerializer.Serialize(newReview);

            AppendDevPlanAppealLog(group, group.DevPlanAppealRoundA,
                $"**Cody 反駁（完整）：**\n```json\n{codyJson}\n```\n\n**Petra 重評（完整）：**\n```json\n{petraJson}\n```");
            await taskRepo.SaveAsync(cancellationToken);

            logger.LogInformation("Dev_plan Appeal Round {Round}：Petra 決定 {Decision}（Group={Id}）",
                group.DevPlanAppealRoundA, newReview.Decision, group.Id);

            if (newReview.Decision == "approve")
                return true;

            priorContext  = $"（已進行 {group.DevPlanAppealRoundA} 輪 Appeal，Petra 維持修改意見：{newReview.Summary}）";
            currentReview = newReview;
        }

        logger.LogWarning("Dev_plan Appeal 耗盡 {MaxRounds} 輪，升級老闆（Group={Id}）", maxRounds, group.Id);
        return false;
    }

    /// <summary>Dev_plan Petra 審核超限，發帶 Skip/Abort 按鈕的 Embed 給老闆。</summary>
    public async Task NotifyBossDevPlanEscalationAsync(
        TaskGroup group,
        PetraReview petraReview,
        CancellationToken cancellationToken)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null) return;

        var blockingText = petraReview.Issues.Where(i => i.Severity == "blocking").ToList();
        var blockingField = blockingText.Count > 0
            ? string.Join("\n", blockingText.Select(i => $"• {i.Description}"))
            : petraReview.Summary;
        if (blockingField.Length > 1000) blockingField = blockingField[..1000] + "...";

        var devPlanPreview = string.IsNullOrWhiteSpace(group.DevPlan)
            ? "（無）"
            : group.DevPlan.Length > 800 ? group.DevPlan[..800] + "\n...（完整計畫書見 #cody-dev）" : group.DevPlan;

        var embed = new EmbedBuilder()
            .WithTitle("⚠️ Petra 升級通知：Dev_plan 審核未通過")
            .WithColor(Color.Orange)
            .AddField("任務", group.Title)
            .AddField("問題", $"Cody 實作計畫書經過 {group.DevPlanRevision} 輪審核仍未通過")
            .AddField("Petra 發現的問題", blockingField)
            .AddField("Cody 計畫書摘要", devPlanPreview)
            .WithFooter("⏭️ 跳過審核 = 直接讓 Cody coding；❌ 放棄 = 結束此任務")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        var buttons = new ComponentBuilder()
            .WithButton("⏭️ 跳過審核，直接 coding", "escalate_devplan_skip",  ButtonStyle.Secondary)
            .WithButton("❌ 放棄此任務",             "escalate_devplan_abort", ButtonStyle.Danger)
            .Build();

        var msg = await ceoChannel.SendMessageAsync(embed: embed, components: buttons);

        var commandHandler = serviceProvider.GetRequiredService<CommandHandler>();
        commandHandler.RegisterDevPlanEscalation(msg.Id, group.Id);

        logger.LogWarning("TaskGroup {Id} Dev_plan 審核超限，升級給老闆", group.Id);

        _ = interactionService.CreateInteractionAsync(
            "devplan_escalate",
            title:                $"Dev_plan 升級：{group.Title}",
            description:          $"Cody 實作計畫書經過 {group.DevPlanRevision} 輪審核仍未通過",
            project:              group.Project,
            agentName:            AgentNames.Pm,
            availableActionsJson: InteractionService.DevPlanEscalateActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId = ceoChannel.Id.ToString(),
                groupId   = group.Id.ToString()
            }),
            discordMessageId: (decimal)msg.Id,
            taskGroupId:      group.Id);
    }

    /// <summary>Dashboard Dev_plan escalate 路由（skip / abort）。</summary>
    public async Task HandleDevPlanEscalationAsync(string contextJson, string action, CancellationToken ct)
    {
        using var doc    = JsonDocument.Parse(contextJson);
        var groupIdStr   = doc.RootElement.TryGetProperty("groupId", out var g) ? g.GetString() : null;
        if (!Guid.TryParse(groupIdStr, out var groupId)) return;

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group             = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null)
        {
            logger.LogWarning("HandleDevPlanEscalationAsync：找不到 TaskGroup ({Id})", groupId);
            return;
        }

        if (action == "devplan_skip")
        {
            taskRepo.UpdateGroupStatus(group, "running");
            await taskRepo.SaveAsync(ct);
            var tgs = serviceProvider.GetRequiredService<TaskGroupService>();
            await tgs.FireStepsAsync(group, [new WorkflowStep("Dev")], ct);
        }
        else // devplan_abort
        {
            taskRepo.UpdateGroupStatus(group, "failed");
            await taskRepo.SaveAsync(ct);
        }
    }

    // ============================================================
    //  Helpers
    // ============================================================

    private async Task<PetraReview> RunPetraVeraReviewAsync(
        TaskGroup group,
        AgentExecutionResult veraResult,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo     = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService  = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var pmService    = scope.ServiceProvider.GetRequiredService<PmReviewService>();

        var petraTask = new TaskItem
        {
            Title         = $"[Petra→Vera] {group.Title}",
            Description   = "審核 Vera Code Review 嚴重度",
            TriggeredBy   = "Orchestrator",
            AssignedAgent = AgentNames.Pm,
            Status        = "running",
            GroupId       = group.Id,
            ProjectId     = projectId,
        };
        taskRepo.Add(petraTask);
        await taskRepo.SaveAsync(cancellationToken);
        taskRepo.AddLog(new TaskLog { TaskId = petraTask.Id, Agent = AgentNames.Pm, Step = "Petra 審核 Vera Code Review 嚴重度...", Status = "running" });
        await taskRepo.SaveAsync(cancellationToken);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = petraTask.Id,
            GroupId   = group.Id,
            Title     = petraTask.Title,
            AgentName = petraTask.AssignedAgent,
            Status    = "running"
        });
        await pushService.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = AgentNames.Pm,
            Status           = "running",
            CurrentTaskTitle = group.Title
        });

        var petraChannel = FindChannel(_discord.Channels.PmChannel);
        if (petraChannel is not null)
            await petraChannel.SendMessageAsync(
                $"🔍 **Petra 審核 Vera Code Review**\n任務：{group.Title}");

        PetraReview petraReview;
        try
        {
            petraReview = await pmService.ReviewVeraAsync(
                group.Title,
                group.LastReviewBody ?? veraResult.ReviewBody ?? veraResult.Summary,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RunPetraVeraReviewAsync LLM 失敗，fallback approve");
            petraReview = new PetraReview("approve", "Petra LLM 失敗，自動放行", [], null);
        }

        var petraStatus = petraReview.Decision == "revise" ? "revision"
                        : petraReview.Decision == "escalate" ? "failed"
                        : "done";
        taskRepo.AddLog(new TaskLog { TaskId = petraTask.Id, Agent = AgentNames.Pm, Step = petraReview.Summary, Status = petraStatus });
        taskRepo.UpdateStatus(petraTask, petraStatus);
        await taskRepo.SaveAsync(cancellationToken);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = petraTask.Id,
            GroupId   = group.Id,
            Title     = petraTask.Title,
            AgentName = petraTask.AssignedAgent,
            Status    = petraStatus
        });
        await pushService.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = AgentNames.Pm,
            Status           = petraStatus is "failed" ? "error" : "idle",
            CurrentTaskTitle = petraStatus is "failed" ? petraReview.Summary : null
        });

        if (petraChannel is not null)
            await petraChannel.SendMessageAsync(
                $"📋 **Petra 審核結果**（Vera Code Review）：**{petraReview.Decision.ToUpper()}**\n{petraReview.Summary}");

        logger.LogInformation("Petra Vera 審核：Group={Id}，decision={Decision}", group.Id, petraReview.Decision);
        return petraReview;
    }

    private static void AppendAppealLog(TaskGroup group, int round, string content)
    {
        var entry = $"\n\n### Appeal Round {round} — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n{content}";
        group.ReviewAppealLog = (group.ReviewAppealLog ?? "# Review Appeal 紀錄\n") + entry;
    }

    private static void AppendDevPlanAppealLog(TaskGroup group, int round, string content)
    {
        var entry = $"\n\n### DevPlan Appeal Round {round} — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n{content}";
        group.DevPlanAppealLog = (group.DevPlanAppealLog ?? "# Dev_plan Appeal 紀錄\n") + entry;
    }

    /// <summary>從審查報告中解析 Critical 段落內的 Issue IDs（格式：[#N]）。
    /// Stage 37：改為 internal static，供 MeetingOrchestrationService.RestartReviewAppealAsync 重建 result 使用。</summary>
    internal static IReadOnlyList<int> ExtractCriticalIdsFromReviewBody(string reviewBody)
    {
        if (string.IsNullOrWhiteSpace(reviewBody)) return [];

        var criticalIdx = reviewBody.IndexOf("必須修改（Critical）", StringComparison.Ordinal);
        if (criticalIdx < 0) return [];

        var nextHeaderIdx = reviewBody.IndexOf("\n###", criticalIdx + 10);
        var nextSepIdx    = reviewBody.IndexOf("\n---", criticalIdx + 10);
        var sectionEnd    = (nextHeaderIdx, nextSepIdx) switch
        {
            (>= 0, >= 0) => Math.Min(nextHeaderIdx, nextSepIdx),
            (>= 0, < 0)  => nextHeaderIdx,
            (< 0,  >= 0) => nextSepIdx,
            _             => reviewBody.Length
        };

        var sectionText = reviewBody[criticalIdx..sectionEnd];
        var matches     = Regex.Matches(sectionText, @"\[#(\d+)\]");
        return matches.Select(m => int.Parse(m.Groups[1].Value)).Distinct().ToList();
    }

    private IMessageChannel? FindChannel(string channelName)
    {
        if (!ulong.TryParse(_discord.GuildId, out var guildId)) return null;
        return discordClient.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }
}
