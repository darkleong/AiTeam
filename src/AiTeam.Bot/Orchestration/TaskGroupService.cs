using System.Collections.Concurrent;
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
using Microsoft.Extensions.Hosting;
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
    IHostApplicationLifetime appLifetime,
    ILogger<TaskGroupService> logger)
{
    private readonly DiscordSettings _discord    = discordSettings.Value;
    private readonly GitHubSettings  _gitHub     = gitHubSettings.Value;

    // Stage 14：記錄每個執行中 TaskItem 的 CTS，供取消時 kill subprocess
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningCts = new();

    // ---- 任務群組建立 ----

    /// <summary>
    /// 建立任務群組並存入 DB。
    /// </summary>
    public async Task<TaskGroup> CreateGroupAsync(
        string title,
        string project,
        WorkflowType workflowType,
        string? issueUrlsJson  = null,
        string? uiSpecContent  = null,
        CancellationToken cancellationToken = default)
    {
        await using var scope   = serviceProvider.CreateAsyncScope();
        var taskRepo            = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var group = new TaskGroup
        {
            Title          = title,
            Project        = project,
            Status         = "running",
            WorkflowType   = workflowType switch
            {
                WorkflowType.NewFeature      => "new_feature",
                WorkflowType.TechImprovement => "tech_improvement",
                _                            => "bug_fix"
            },
            IssueUrls      = issueUrlsJson,
            UiSpecContent  = uiSpecContent,
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

        // 防止 Race Condition：若 TaskGroup 已結束（done/failed），不重複推進
        if (group.Status is "done" or "failed")
        {
            logger.LogDebug("HandleAgentCompleted：TaskGroup {Id} 已結束（{Status}），略過", groupId, group.Status);
            return;
        }

        // Stage 13：合併更新 DevPrUrl 與 LastReviewBody，只呼叫一次 SaveAsync（避免並行 Agent 競態覆蓋）
        var needsSave = false;

        if (!string.IsNullOrWhiteSpace(devPrUrl) && string.IsNullOrWhiteSpace(group.DevPrUrl))
        {
            group.DevPrUrl = devPrUrl;
            needsSave = true;
        }

        if (!string.IsNullOrWhiteSpace(result.ReviewBody))
        {
            group.LastReviewBody = result.ReviewBody;
            needsSave = true;
            logger.LogInformation("TaskGroup {Id} 已儲存 Vera 審查報告（{Len} 字）",
                groupId, result.ReviewBody.Length);
        }

        if (needsSave)
            await taskRepo.SaveAsync(cancellationToken);

        // ── Stage 16：Dev_plan 完成 → Petra 審核實作計畫 ──
        if (completedAgent.Equals("Dev_plan", StringComparison.OrdinalIgnoreCase))
        {
            // 儲存計畫書（優先用 OutputContent，fallback 到 Summary）
            group.DevPlan = result.OutputContent ?? result.Summary;
            await taskRepo.SaveAsync(cancellationToken);

            // 按需查詢 ProjectId（僅在進入 Petra 審核分支時才查，避免無謂的 DB 壓力）
            var groupProjectId = await GetGroupProjectIdAsync(group, taskRepo, cancellationToken);
            var petraDevPlanReview = await RunPetraDevPlanReviewAsync(group, result, groupProjectId, cancellationToken);
            switch (petraDevPlanReview.Decision)
            {
                case "approve":
                    // 計畫通過，繼續往下走 GetDecision → 觸發 Dev
                    break;

                case "revise":
                    if (group.DevPlanRevision >= 2) goto default; // 超過上限 → escalate
                    group.DevPlanRevision++;
                    // 將修正指示附加到 DevPlan，讓下一輪帶入 meta block
                    if (!string.IsNullOrWhiteSpace(petraDevPlanReview.RevisionInstructions))
                        group.DevPlan += "\n\n【Petra 修正指示】" + petraDevPlanReview.RevisionInstructions;
                    await taskRepo.SaveAsync(cancellationToken);
                    await FireStepsAsync(group, [new WorkflowStep("Dev_plan")], cancellationToken);
                    return; // 不走 GetDecision

                default: // escalate 或超過上限
                    taskRepo.UpdateGroupStatus(group, "failed");
                    await taskRepo.SaveAsync(cancellationToken);
                    await NotifyBossDevPlanEscalationAsync(group, petraDevPlanReview, cancellationToken);
                    return;
            }
        }

        // ── Stage 16：Vera 完成 → Petra 審核 Review 嚴重度 ──
        if (completedAgent.Equals("Reviewer", StringComparison.OrdinalIgnoreCase))
        {
            // Vera 執行失敗（例如找不到 PR）→ 不送 Petra 審，直接視為無 blocking，繼續流程
            if (!result.Success)
            {
                logger.LogWarning("Vera 執行失敗（{Summary}），跳過 Petra 審核，直接放行", result.Summary);
                result = result with { CriticalReviewCount = 0 };
                // 繼續走 GetDecision，不 return
            }
            else
            {
                // 按需查詢 ProjectId（僅在進入 Petra 審核分支時才查，避免無謂的 DB 壓力）
                var groupProjectId = await GetGroupProjectIdAsync(group, taskRepo, cancellationToken);
                var reviewResult = await HandleReviewerCompletedAsync(group, result, taskRepo, groupProjectId, cancellationToken);
                if (reviewResult is null) return; // escalate，已更新狀態並通知老闆
                result = reviewResult;
            }
        }

        // ── Stage 16：Dev 初次開發失敗（非 fix loop）→ 停止流程，通知老闆介入 ──
        // （fix loop 失敗用 "Dev_fix" 為 completedAgent，不會被這裡攔截）
        if (completedAgent.Equals("Dev", StringComparison.OrdinalIgnoreCase) && !result.Success)
        {
            logger.LogError("Dev Agent 執行失敗，停止工作流程：Group={Id}，原因：{Summary}",
                group.Id, result.Summary);
            taskRepo.UpdateGroupStatus(group, "failed");
            await taskRepo.SaveAsync(cancellationToken);
            await NotifyBossInterventionAsync(group, cancellationToken);
            return;
        }

        var workflowType = group.WorkflowType switch
        {
            "new_feature"      => WorkflowType.NewFeature,
            "tech_improvement" => WorkflowType.TechImprovement,
            _                  => WorkflowType.BugFix
        };

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
        // 查詢專案 ID（供任務中心顯示專案欄位）
        var projectId = string.IsNullOrWhiteSpace(group.Project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(group.Project, cancellationToken);

        // Stage 16：Dev_plan 步驟由 Cody（Dev）執行，AssignedAgent 顯示為 Dev 避免 Dashboard 出現未知 Agent
        var displayAgent = step.AgentName.Equals("Dev_plan", StringComparison.OrdinalIgnoreCase)
            ? AgentNames.Dev
            : step.AgentName;

        var taskItem = new TaskItem
        {
            Title         = $"{group.Title}（{step.AgentName}）",
            Description   = description,
            TriggeredBy   = "Orchestrator",
            AssignedAgent = displayAgent,
            Status        = "running",
            GroupId       = group.Id,
            ProjectId     = projectId,
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

        // 執行 Agent（Stage 16：Dev_plan 映射到 Dev executor，用不同 prompt 驅動）
        var executorKey = step.AgentName.Equals("Dev_plan", StringComparison.OrdinalIgnoreCase)
            ? AgentNames.Dev
            : step.AgentName;
        var executor = scope.ServiceProvider.GetKeyedService<IAgentExecutor>(executorKey);
        if (executor is null)
        {
            logger.LogError("TaskGroupService：找不到 Agent 實作：{Agent}", step.AgentName);
            taskRepo.UpdateStatus(taskItem, "failed");
            await taskRepo.SaveAsync(cancellationToken);
            return;
        }

        // Stage 14：建立 linked CTS，供外部取消時 kill subprocess
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runningCts[taskItem.Id] = linkedCts;

        try
        {
            var rules  = await rulesService.GetRulesAsync(step.AgentName);
            var result = await executor.ExecuteTaskAsync(taskItem, owner, repo, rules, linkedCts.Token);

            var finalStatus = result.Success ? "done" : "failed";
            taskRepo.UpdateStatus(taskItem, finalStatus);
            // Stage 13：補全最終 TaskLog，讓 Dashboard 能顯示完成原因或失敗原因
            taskRepo.AddLog(new TaskLog
            {
                TaskId = taskItem.Id,
                Agent  = step.AgentName,
                Step   = result.Summary,
                Status = finalStatus,
            });
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

            // Stage 13：改用 ApplicationStopping token，Bot 關閉時背景工作可被取消
            _ = Task.Run(async () =>
            {
                try
                {
                    var prUrl = result.OutputUrl ?? group.DevPrUrl ?? "";
                    // 更新 completedAgent：若為修復迭代，用 "Dev_fix" 讓 WorkflowEngine 正確查表
                    var agentKey = step.IsFixLoop && step.AgentName == AgentNames.Dev
                        ? "Dev_fix"
                        : step.AgentName;
                    await HandleAgentCompletedAsync(group.Id, agentKey, result, prUrl,
                        appLifetime.ApplicationStopping);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TaskGroupService：遞迴觸發下一步失敗（Group={Id}）", group.Id);
                }
            }, appLifetime.ApplicationStopping);
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Stage 14：外部取消（CancelAsync 呼叫），標記為 cancelled
            logger.LogInformation("TaskGroupService：Agent {Agent}（Task={Id}）被外部取消", step.AgentName, taskItem.Id);
            taskRepo.UpdateStatus(taskItem, "cancelled");
            await taskRepo.SaveAsync(CancellationToken.None);
            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = taskItem.Id,
                Title     = taskItem.Title,
                AgentName = taskItem.AssignedAgent,
                Status    = "cancelled"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TaskGroupService：Agent {Agent} 執行失敗（Task={Id}）",
                step.AgentName, taskItem.Id);
            taskRepo.UpdateStatus(taskItem, "failed");
            // Stage 13：補全失敗 TaskLog，讓 Dashboard 能顯示例外訊息
            taskRepo.AddLog(new TaskLog
            {
                TaskId = taskItem.Id,
                Agent  = step.AgentName,
                Step   = ex.Message,
                Status = "failed",
            });
            await taskRepo.SaveAsync(cancellationToken);

            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = taskItem.Id,
                Title     = taskItem.Title,
                AgentName = taskItem.AssignedAgent,
                Status    = "failed"
            });
        }
        finally
        {
            _runningCts.TryRemove(taskItem.Id, out _);
        }
    }

    // ---- 取消任務（Stage 14）----

    /// <summary>
    /// 取消指定 TaskGroup 的所有進行中任務。
    /// 1. 呼叫 CTS.Cancel() 中斷正在執行的 subprocess
    /// 2. 將 TaskGroup / 未完成 TaskItem 狀態改為 cancelled
    /// </summary>
    public async Task CancelAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var group = await taskRepo.GetGroupByIdAsync(groupId, cancellationToken);
        if (group is null)
        {
            logger.LogWarning("CancelAsync：找不到 TaskGroup（Id={Id}）", groupId);
            return;
        }

        // Kill 正在執行的 subprocess（best effort）
        foreach (var task in group.Tasks)
        {
            if (_runningCts.TryRemove(task.Id, out var cts))
            {
                try { cts.Cancel(); }
                catch (Exception ex) { logger.LogWarning(ex, "CancelAsync：Cancel CTS 失敗（TaskId={Id}）", task.Id); }
            }
        }

        // 更新 DB 狀態
        taskRepo.CancelGroupItems(group);
        taskRepo.UpdateGroupStatus(group, "cancelled");
        await taskRepo.SaveAsync(cancellationToken);

        logger.LogInformation("TaskGroup {Id}（{Title}）已取消", groupId, group.Title);
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
            $"✅ **{group.Title}** — 全流程完成！\n" +
            $"PR：{prLink}（含 code + tests + docs）\n" +
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

    /// <summary>
    /// Stage 16：Dev_plan Petra 審核超限，發帶 Skip/Abort 按鈕的 Embed 給老闆。
    /// </summary>
    private async Task NotifyBossDevPlanEscalationAsync(
        TaskGroup group,
        Agents.PetraReview petraReview,
        CancellationToken cancellationToken)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null) return;

        // 列出 Petra 的 blocking 問題
        var blockingText = petraReview.Issues.Where(i => i.Severity == "blocking").ToList();
        var blockingField = blockingText.Count > 0
            ? string.Join("\n", blockingText.Select(i => $"• {i.Description}"))
            : petraReview.Summary;
        if (blockingField.Length > 1000) blockingField = blockingField[..1000] + "...";

        // 附上計畫書摘要（前 800 字）
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

        // 讓 CommandHandler 記錄這個 pending，button handler 才能處理
        var commandHandler = serviceProvider.GetRequiredService<Discord.CommandHandler>();
        commandHandler.RegisterDevPlanEscalation(msg.Id, group.Id);

        logger.LogWarning("TaskGroup {Id} Dev_plan 審核超限，升級給老闆", group.Id);
    }

    // ---- 輔助方法 ----

    /// <summary>
    /// 組建 TaskItem.Description，附帶 CEO 傳遞給 Dev 的上下文 metadata。
    /// Stage 16：Dev_plan 模式加 dev_plan_mode 標記；Dev 模式加已審核計畫書。
    /// </summary>
    private static string BuildTaskDescription(TaskGroup group, WorkflowStep step)
    {
        var desc = group.Title;

        // Stage 16：Dev_plan 模式（產出計畫書，不寫程式碼）
        if (step.AgentName.Equals("Dev_plan", StringComparison.OrdinalIgnoreCase))
        {
            var parts = new List<string> { desc };
            var meta  = new List<string> { "dev_plan_mode: true" };

            if (!string.IsNullOrWhiteSpace(group.IssueUrls))
                meta.Add($"issue_urls: {group.IssueUrls}");
            if (!string.IsNullOrWhiteSpace(group.UiSpecContent))
                meta.Add($"ui_spec_content:\n{group.UiSpecContent}");

            // revision 時：帶入上一版計畫書（含 Petra 修正指示）
            if (!string.IsNullOrWhiteSpace(group.DevPlan))
                meta.Add($"prev_dev_plan:\n{group.DevPlan}");

            parts.Add("---");
            parts.AddRange(meta);
            parts.Add("---");
            return string.Join("\n", parts);
        }

        if (step.AgentName is AgentNames.Dev or AgentNames.Reviewer or AgentNames.Qa or AgentNames.Doc)
        {
            var parts = new List<string> { desc };

            if (!string.IsNullOrWhiteSpace(group.DevPrUrl))
                parts.Add($"PR 連結：{group.DevPrUrl}");

            // 附上 metadata block（Dev 制定計畫時解析使用）
            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(group.IssueUrls))
                meta.Add($"issue_urls: {group.IssueUrls}");
            if (!string.IsNullOrWhiteSpace(group.UiSpecContent))
                meta.Add($"ui_spec_content:\n{group.UiSpecContent}");

            // Stage 16：Dev 模式時帶入已審核的實作計畫書
            if (step.AgentName == AgentNames.Dev && !string.IsNullOrWhiteSpace(group.DevPlan))
                meta.Add($"dev_plan:\n{group.DevPlan}");

            if (step.IsFixLoop)
            {
                meta.Add("fix_loop: true");
                // 把 Vera 最新的審查報告帶給 Dev，讓 Dev 知道要修什麼
                if (!string.IsNullOrWhiteSpace(group.LastReviewBody))
                    meta.Add($"vera_review:\n{group.LastReviewBody}");
            }

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

    // ---- Stage 16：Petra 審核輔助方法 ----

    /// <summary>
    /// 按需查詢 TaskGroup 對應的 ProjectId（避免無謂的 DB 壓力）。
    /// </summary>
    private async Task<Guid?> GetGroupProjectIdAsync(
        TaskGroup group,
        TaskRepository taskRepo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(group.Project)) return null;
        var projectId = await taskRepo.GetProjectIdByNameAsync(group.Project, cancellationToken);
        if (projectId is null)
            logger.LogWarning("HandleAgentCompleted：找不到專案名稱 '{Project}'，Petra TaskItem.ProjectId 將為 null", group.Project);
        return projectId;
    }

    /// <summary>
    /// Vera 執行成功後，送 Petra 審核並依決策回傳更新後的 result。
    /// 回傳 null 表示已 escalate（呼叫方應直接 return）。
    /// </summary>
    private async Task<AgentExecutionResult?> HandleReviewerCompletedAsync(
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
                await NotifyBossInterventionAsync(group, cancellationToken);
                return null;
        }
    }

    /// <summary>
    /// 建立 Petra TaskItem、呼叫 ReviewDevPlanAsync、推送狀態、通知 #petra-pm 頻道。
    /// </summary>
    private async Task<Agents.PetraReview> RunPetraDevPlanReviewAsync(
        TaskGroup group,
        AgentExecutionResult devPlanResult,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var scope   = serviceProvider.CreateAsyncScope();
        var taskRepo            = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService         = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var pmService           = scope.ServiceProvider.GetRequiredService<Agents.PmAgentService>();
        var gitHubService       = scope.ServiceProvider.GetRequiredService<GitHub.GitHubService>();

        // 建立 Petra TaskItem（projectId 由呼叫方傳入，避免跨 scope 混用 DbContext）
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

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = petraTask.Id,
            Title     = petraTask.Title,
            AgentName = petraTask.AssignedAgent,
            Status    = "running"
        });

        var petraChannel = FindChannel(_discord.Channels.PmChannel);
        if (petraChannel is not null)
            await petraChannel.SendMessageAsync(
                $"🔍 **Petra 審核 Cody 實作計畫書**\n任務：{group.Title}（第 {group.DevPlanRevision + 1} 輪）");

        // 準備 workspace（唯讀）
        var owner       = _gitHub.Owner;
        var repo        = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;
        var localPath   = "";
        Agents.PetraReview petraReview;

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
            petraReview = new Agents.PetraReview("approve", "Petra workspace 失敗，自動放行", [], null);
        }
        finally
        {
            if (!string.IsNullOrEmpty(localPath))
                gitHubService.CleanupLocalRepo(localPath);
        }

        var petraStatus = petraReview.Decision == "revise" ? "revision"
                        : petraReview.Decision == "escalate" ? "failed"
                        : "done";
        taskRepo.UpdateStatus(petraTask, petraStatus);
        await taskRepo.SaveAsync(cancellationToken);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = petraTask.Id,
            Title     = petraTask.Title,
            AgentName = petraTask.AssignedAgent,
            Status    = petraStatus
        });

        if (petraChannel is not null)
            await petraChannel.SendMessageAsync(
                $"📋 **Petra 審核結果**（Dev_plan 第 {group.DevPlanRevision + 1} 輪）：**{petraReview.Decision.ToUpper()}**\n{petraReview.Summary}");

        logger.LogInformation("Petra Dev_plan 審核：Group={Id}，decision={Decision}", group.Id, petraReview.Decision);
        return petraReview;
    }

    /// <summary>
    /// 建立 Petra TaskItem、呼叫 ReviewVeraAsync、推送狀態、通知 #petra-pm 頻道。
    /// </summary>
    private async Task<Agents.PetraReview> RunPetraVeraReviewAsync(
        TaskGroup group,
        AgentExecutionResult veraResult,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var scope   = serviceProvider.CreateAsyncScope();
        var taskRepo            = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService         = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        var pmService           = scope.ServiceProvider.GetRequiredService<Agents.PmAgentService>();

        // 建立 Petra TaskItem（projectId 由呼叫方傳入，避免跨 scope 混用 DbContext）
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

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = petraTask.Id,
            Title     = petraTask.Title,
            AgentName = petraTask.AssignedAgent,
            Status    = "running"
        });

        var petraChannel = FindChannel(_discord.Channels.PmChannel);
        if (petraChannel is not null)
            await petraChannel.SendMessageAsync(
                $"🔍 **Petra 審核 Vera Code Review**\n任務：{group.Title}");

        Agents.PetraReview petraReview;
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
            petraReview = new Agents.PetraReview("approve", "Petra LLM 失敗，自動放行", [], null);
        }

        var petraStatus = petraReview.Decision == "revise" ? "revision"
                        : petraReview.Decision == "escalate" ? "failed"
                        : "done";
        taskRepo.UpdateStatus(petraTask, petraStatus);
        await taskRepo.SaveAsync(cancellationToken);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = petraTask.Id,
            Title     = petraTask.Title,
            AgentName = petraTask.AssignedAgent,
            Status    = petraStatus
        });

        if (petraChannel is not null)
            await petraChannel.SendMessageAsync(
                $"📋 **Petra 審核結果**（Vera Code Review）：**{petraReview.Decision.ToUpper()}**\n{petraReview.Summary}");

        logger.LogInformation("Petra Vera 審核：Group={Id}，decision={Decision}", group.Id, petraReview.Decision);
        return petraReview;
    }

    private string GetAgentChannelName(string agentName) => agentName switch
    {
        AgentNames.Dev          => _discord.Channels.DevChannel,
        "Dev_plan"              => _discord.Channels.DevChannel,  // Stage 16：Dev_plan 用 Dev 頻道
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
