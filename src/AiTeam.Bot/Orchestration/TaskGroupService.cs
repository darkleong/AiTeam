using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    IOptions<WorkflowSettings> workflowSettings,
    RulesService rulesService,
    WorkflowEngine workflowEngine,
    MeetingService meetingService,
    IHostApplicationLifetime appLifetime,
    ILogger<TaskGroupService> logger)
{
    private readonly DiscordSettings  _discord          = discordSettings.Value;
    private readonly GitHubSettings   _gitHub           = gitHubSettings.Value;
    private readonly WorkflowSettings _workflowSettings = workflowSettings.Value;

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

        // 23-2：Dev 完成時儲存 Cody 實作說明
        if ((completedAgent.Equals("Dev", StringComparison.OrdinalIgnoreCase)
             || completedAgent.Equals("Dev_fix", StringComparison.OrdinalIgnoreCase))
            && result.Success
            && !string.IsNullOrWhiteSpace(result.OutputContent))
        {
            group.ImplementationNote = result.OutputContent;
            needsSave = true;
            logger.LogInformation("TaskGroup {Id} 已儲存 Cody 實作說明（{Len} 字）",
                groupId, result.OutputContent.Length);
        }

        // Stage 24：QA 完成時儲存測試報告
        if (completedAgent.Equals(AgentNames.Qa, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(result.TestReport))
        {
            group.TestReport = result.TestReport;
            needsSave = true;
            logger.LogInformation("TaskGroup {Id} 已儲存 Quinn 測試報告（{Len} 字）",
                groupId, result.TestReport.Length);
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
                    // Stage 24：Appeal loop（Cody 反駁 + Petra 重評，純 LLM 緊密迴圈）
                    var appealApproved = await RunDevPlanAppealLoopAsync(group, petraDevPlanReview, taskRepo, cancellationToken);
                    if (appealApproved)
                    {
                        // Appeal 說服成功，直接觸發 Dev（計畫書已在 group.DevPlan）
                        logger.LogInformation("Dev_plan Appeal 說服成功，直接觸發 Dev（Group={Id}）", group.Id);
                        await FireStepsAsync(group, [new WorkflowStep(AgentNames.Dev)], cancellationToken);
                    }
                    else
                    {
                        // Appeal 耗盡，升級老闆
                        logger.LogWarning("Dev_plan Appeal 耗盡，升級老闆（Group={Id}）", group.Id);
                        taskRepo.UpdateGroupStatus(group, "failed");
                        await taskRepo.SaveAsync(cancellationToken);
                        await NotifyBossDevPlanEscalationAsync(group, petraDevPlanReview, cancellationToken);
                    }
                    return;

                default: // escalate
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

        // ── Stage 23：Dev / Dev_fix 阻礙報告 → Petra 仲裁路由 ──
        if ((completedAgent.Equals("Dev", StringComparison.OrdinalIgnoreCase)
             || completedAgent.Equals("Dev_fix", StringComparison.OrdinalIgnoreCase))
            && !result.Success
            && result.Summary.StartsWith("[BLOCKED]", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(result.OutputContent))
        {
            logger.LogWarning("Dev 回報阻礙，啟動 Petra 評估：Group={Id}", groupId);
            var groupProjectId = await GetGroupProjectIdAsync(group, taskRepo, cancellationToken);
            await HandleDevBlockerAsync(group, result, taskRepo, groupProjectId, cancellationToken);
            return;
        }

        // ── Stage 23：仲裁後 Dev_fix 完成 → 跳過 Vera，直接交 Petra 閘門 ──
        if (completedAgent.Equals("Dev_fix", StringComparison.OrdinalIgnoreCase)
            && result.Success
            && group.SkipReviewerAfterArbitration)
        {
            logger.LogInformation("仲裁後 Dev_fix 完成，跳過 Vera，直接交 Petra 閘門（Group={Id}）", group.Id);
            group.SkipReviewerAfterArbitration = false;
            await taskRepo.SaveAsync(cancellationToken);
            var groupProjectId = await GetGroupProjectIdAsync(group, taskRepo, cancellationToken);
            var petraResult = await RunPetraGateAsync(group, result, taskRepo, groupProjectId, cancellationToken);
            if (petraResult is null) return;
            result = petraResult;
            // 繼續走 GetDecision（completedAgent 仍為 Dev_fix，result 已更新）
        }

        // ── Stage 24：QA 修復模式 → Dev_fix 完成後重新觸發 QA（不走 Reviewer）──
        if (completedAgent.Equals("Dev_fix", StringComparison.OrdinalIgnoreCase)
            && result.Success
            && group.QaFixRound > 0)
        {
            logger.LogInformation("QA 修復後 Dev_fix 完成，重新觸發 QA（Group={Id}, Round={Round}）",
                group.Id, group.QaFixRound);
            await FireStepsAsync(group, [new WorkflowStep(AgentNames.Qa)], cancellationToken);
            return;
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

        // ── Stage 24：QA 完成 → Petra 判斷 TestReport，決定路由 ──
        if (completedAgent.Equals(AgentNames.Qa, StringComparison.OrdinalIgnoreCase) && result.Success)
        {
            await HandleQaCompletedAsync(group, result, taskRepo, cancellationToken);
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

    // ---- Mock Mode 輔助 ----

    /// <summary>
    /// Stage 17：MockMode 專用。
    /// 建立 Requirements / PM（Rosa review）/ Designer / PM（Demi review） 四個 mock 完成任務後，
    /// 從 proposal_approved 正式啟動後續工作流程（Dev_plan → Dev → Reviewer → Petra → QA → Doc）。
    /// 讓 Dashboard 能看到完整的含提案流程，但不實際執行 Rosa/Demi/Petra 的 GitHub 操作。
    /// </summary>
    public async Task FireMockProposalAndContinueAsync(
        TaskGroup group,
        CancellationToken cancellationToken = default)
    {
        await using var scope    = serviceProvider.CreateAsyncScope();
        var taskRepo             = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService          = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var projectId = string.IsNullOrWhiteSpace(group.Project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(group.Project, cancellationToken);

        // 模擬提案流程四個步驟（直接建立為 "done" 狀態，不呼叫任何 GitHub / LLM）
        var proposalSteps = new[]
        {
            (Agent: AgentNames.Requirements, Step: "Requirements",      Log: "[MOCK] Rosa 需求分析完成，已產出 1 個模擬 Issue"),
            (Agent: AgentNames.Pm,           Step: "PM（Rosa 審核）",   Log: "[MOCK] Petra 審核 Rosa 完成：approve"),
            (Agent: AgentNames.Designer,     Step: "Designer",          Log: "[MOCK] Demi UI 規格文件產出完成"),
            (Agent: AgentNames.Pm,           Step: "PM（Demi 審核）",   Log: "[MOCK] Petra 審核 Demi 完成：approve"),
        };

        foreach (var (agent, stepName, logMessage) in proposalSteps)
        {
            var taskItem = new TaskItem
            {
                Title         = $"{group.Title}（{stepName}）",
                Description   = "[MOCK] 模擬提案流程步驟",
                TriggeredBy   = "Orchestrator",
                AssignedAgent = agent,
                Status        = "done",
                GroupId       = group.Id,
                ProjectId     = projectId,
            };
            taskRepo.Add(taskItem);
            await taskRepo.SaveAsync(cancellationToken);

            taskRepo.AddLog(new TaskLog
            {
                TaskId    = taskItem.Id,
                Agent     = agent,
                Step      = logMessage,
                Status    = "done",
                CreatedAt = DateTime.UtcNow
            });
            await taskRepo.SaveAsync(cancellationToken);

            await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = taskItem.Id,
                GroupId   = group.Id,
                Title     = taskItem.Title,
                AgentName = agent,
                Status    = "done"
            });

            logger.LogInformation("[MockMode] 模擬提案步驟完成：{Step}", stepName);
        }

        // 從 proposal_approved 啟動正式工作流程（Dev_plan → Dev → Reviewer → Petra → QA → Doc）
        await FireStepsAsync(group, [new WorkflowStep("Dev_plan")], cancellationToken);
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
        // Stage 25a：Kickoff 步驟由 MeetingService 協調執行，不走一般 IAgentExecutor 流程
        if (step.AgentName.Equals(AgentNames.Kickoff, StringComparison.OrdinalIgnoreCase))
        {
            _ = Task.Run(async () =>
            {
                try { await RunKickoffMeetingAndWaitAsync(group, appLifetime.ApplicationStopping); }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TaskGroupService：Kickoff 會議執行失敗（Group={Id}）", group.Id);
                }
            }, appLifetime.ApplicationStopping);
            return;
        }

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
            GroupId   = group.Id,
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
            // Stage 18：Agent 找不到，推送 error 狀態
            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = displayAgent,
                Status           = "error",
                CurrentTaskTitle = $"找不到 Agent 實作：{step.AgentName}"
            });
            return;
        }

        // Stage 14：建立 linked CTS，供外部取消時 kill subprocess
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runningCts[taskItem.Id] = linkedCts;

        try
        {
            var rules = await rulesService.GetRulesAsync(step.AgentName);
            // Stage 18：Agent 開始執行前推送 running 狀態
            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = displayAgent,
                Status           = "running",
                CurrentTaskTitle = group.Title
            });
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
                GroupId   = group.Id,
                Title     = taskItem.Title,
                AgentName = taskItem.AssignedAgent,
                Status    = finalStatus
            });
            // Stage 18：Agent 完成後推送 idle / error 狀態
            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = displayAgent,
                Status           = result.Success ? "idle" : "error",
                CurrentTaskTitle = result.Success ? null : result.Summary
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
                GroupId   = group.Id,
                Title     = taskItem.Title,
                AgentName = taskItem.AssignedAgent,
                Status    = "cancelled"
            });
            // Stage 18：取消後恢復 idle
            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName = displayAgent,
                Status    = "idle"
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
                GroupId   = group.Id,
                Title     = taskItem.Title,
                AgentName = taskItem.AssignedAgent,
                Status    = "failed"
            });
            // Stage 18：例外導致失敗，推送 error 狀態
            await pushService.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName        = displayAgent,
                Status           = "error",
                CurrentTaskTitle = ex.Message
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

            // Stage 25a：附上 Kick-off 任務計劃書（供 Cody 了解需求共識與技術方向）
            if (!string.IsNullOrWhiteSpace(group.TaskPlan))
                meta.Add($"kickoff_task_plan:\n{group.TaskPlan}");

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

            // Stage 25a：附上 Kick-off 任務計劃書（Dev_plan / Dev / Reviewer / QA 步驟參考用）
            if (!string.IsNullOrWhiteSpace(group.TaskPlan))
                meta.Add($"kickoff_task_plan:\n{group.TaskPlan}");

            // 23-2：Reviewer 和 QA 步驟附上 Cody 實作說明（供 Vera 精確審查、QA 精確測試）
            if ((step.AgentName == AgentNames.Reviewer || step.AgentName == AgentNames.Qa)
                && !string.IsNullOrWhiteSpace(group.ImplementationNote))
                meta.Add($"implementation_note:\n{group.ImplementationNote}");

            // 24-4：Reviewer 步驟附上已審核的實作計畫書（供 Vera 驗證實作是否符合計畫）
            if (step.AgentName == AgentNames.Reviewer && !string.IsNullOrWhiteSpace(group.DevPlan))
                meta.Add($"dev_plan:\n{group.DevPlan}");

            // 24-4：QA 步驟附上 Issues 清單（告訴 Quinn 要測什麼）與 Dev_plan（設計背景）
            if (step.AgentName == AgentNames.Qa)
            {
                if (!string.IsNullOrWhiteSpace(group.IssueUrls))
                    meta.Add($"issues_list: {group.IssueUrls}");
                if (!string.IsNullOrWhiteSpace(group.DevPlan))
                    meta.Add($"dev_plan:\n{group.DevPlan}");
            }

            // 24-4：Doc 步驟附上 Quinn 的測試報告（供 Sage 歸檔）
            if (step.AgentName == AgentNames.Doc && !string.IsNullOrWhiteSpace(group.TestReport))
                meta.Add($"test_report:\n{group.TestReport}");

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

    // ---- Stage 24：QA 流程改造 ----

    /// <summary>
    /// Stage 24：QA 完成後，Petra 評估 TestReport 決定路由。
    /// - passed → 走正常流程（Doc 或 merge）
    /// - failed → Petra 判斷 code_bug / back_to_reviewer / env_or_test_issue / escalate_boss
    /// - no_applicable_tests → Petra 判斷是否放行
    /// </summary>
    private async Task HandleQaCompletedAsync(
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        CancellationToken cancellationToken)
    {
        var workflowType = group.WorkflowType switch
        {
            "new_feature"      => WorkflowType.NewFeature,
            "tech_improvement" => WorkflowType.TechImprovement,
            _                  => WorkflowType.BugFix
        };

        // 解析 TestReport JSON
        QaReport? report = null;
        if (!string.IsNullOrWhiteSpace(group.TestReport))
        {
            try
            {
                report = JsonSerializer.Deserialize<QaReport>(group.TestReport,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "HandleQaCompleted：TestReport 解析失敗，視同 passed（Group={Id}）", group.Id);
            }
        }

        var status = report?.Status ?? "passed";
        logger.LogInformation("HandleQaCompleted：Group={Id}, Status={Status}", group.Id, status);

        // passed（或無法解析）→ 正常路由
        if (status == "passed")
        {
            var decision = workflowEngine.GetDecision(workflowType, AgentNames.Qa, result, group.FixIteration);
            if (decision.Action == NextAction.NotifyBossMerge)
            {
                taskRepo.UpdateGroupStatus(group, "done");
                await taskRepo.SaveAsync(cancellationToken);
                await NotifyBossMergeAsync(group, cancellationToken);
            }
            else if (decision.Action == NextAction.FireAgents)
            {
                await FireStepsAsync(group, decision.NextSteps, cancellationToken);
            }
            return;
        }

        // no_applicable_tests → Petra 評估理由
        if (status == "no_applicable_tests")
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var pmService = scope.ServiceProvider.GetRequiredService<PmAgentService>();
            var noTestDecision = await pmService.AssessNoApplicableTestsAsync(group, report?.NoTestReason, cancellationToken);
            logger.LogInformation("Petra QA 無測試評估：{Routing}（Group={Id}）", noTestDecision.Routing, group.Id);

            if (noTestDecision.Routing == "approve")
            {
                // 視同 passed，走正常路由
                var decision = workflowEngine.GetDecision(workflowType, AgentNames.Qa, result, group.FixIteration);
                if (decision.Action == NextAction.NotifyBossMerge)
                {
                    taskRepo.UpdateGroupStatus(group, "done");
                    await taskRepo.SaveAsync(cancellationToken);
                    await NotifyBossMergeAsync(group, cancellationToken);
                }
                else if (decision.Action == NextAction.FireAgents)
                {
                    await FireStepsAsync(group, decision.NextSteps, cancellationToken);
                }
            }
            else
            {
                taskRepo.UpdateGroupStatus(group, "failed");
                await taskRepo.SaveAsync(cancellationToken);
                await NotifyBossInterventionAsync(group, cancellationToken);
            }
            return;
        }

        // failed → Petra 判斷根本原因
        if (group.QaFixRound >= _workflowSettings.QaFixMaxRounds)
        {
            logger.LogWarning("QA 修復超過上限（Round={Round}），升級老闆（Group={Id}）",
                group.QaFixRound, group.Id);
            taskRepo.UpdateGroupStatus(group, "failed");
            await taskRepo.SaveAsync(cancellationToken);
            await NotifyBossInterventionAsync(group, cancellationToken);
            return;
        }

        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var pmService = scope.ServiceProvider.GetRequiredService<PmAgentService>();
            var failureDecision = await pmService.AssessQaFailureAsync(
                group, group.TestReport ?? "", cancellationToken);

            logger.LogInformation("Petra QA 失敗評估：{Routing}（Group={Id}）", failureDecision.Routing, group.Id);

            switch (failureDecision.Routing)
            {
                case "code_bug":
                    // 小修正：Dev_fix 後直接重測（跳過 Vera）
                    group.QaFixRound++;
                    await taskRepo.SaveAsync(cancellationToken);
                    await FireStepsAsync(group, [new WorkflowStep("Dev_fix")], cancellationToken);
                    break;

                case "back_to_reviewer":
                    // 大幅改動：Dev_fix 後回 Vera 正常審查路徑
                    group.QaFixRound = 0;
                    group.FixIteration++;
                    await taskRepo.SaveAsync(cancellationToken);
                    await FireStepsAsync(group, [new WorkflowStep("Dev_fix", IsFixLoop: true)], cancellationToken);
                    break;

                case "env_or_test_issue":
                    // 視同通過，走正常路由
                    var decision = workflowEngine.GetDecision(workflowType, AgentNames.Qa, result, group.FixIteration);
                    if (decision.Action == NextAction.NotifyBossMerge)
                    {
                        taskRepo.UpdateGroupStatus(group, "done");
                        await taskRepo.SaveAsync(cancellationToken);
                        await NotifyBossMergeAsync(group, cancellationToken);
                    }
                    else if (decision.Action == NextAction.FireAgents)
                    {
                        await FireStepsAsync(group, decision.NextSteps, cancellationToken);
                    }
                    break;

                default: // escalate_boss
                    taskRepo.UpdateGroupStatus(group, "failed");
                    await taskRepo.SaveAsync(cancellationToken);
                    await NotifyBossInterventionAsync(group, cancellationToken);
                    break;
            }
        }
    }

    /// <summary>
    /// Vera 執行成功後：
    /// 1. 若無 Critical → 直接走 Petra 閘門（RunPetraGateAsync）
    /// 2. 若有 Critical → 進入 Review Appeal 迴圈 A（Cody-Vera 純對話，最多 maxRounds 輪）
    ///    - 迴圈後無 Critical → Petra 閘門放行
    ///    - 達上限仍有 Critical → Petra 仲裁（RunPetraArbitrationAsync）
    ///    - Cody 全 agree → Petra 閘門（維持 criticals → Dev_fix）
    /// 回傳 null 表示已 escalate（呼叫方應直接 return）。
    /// </summary>
    private async Task<AgentExecutionResult?> HandleReviewerCompletedAsync(
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        // 無 Critical → 跳過 Appeal，直接走 Petra 閘門
        if (result.CriticalReviewCount == 0)
            return await RunPetraGateAsync(group, result, taskRepo, projectId, cancellationToken);

        var maxRounds  = _workflowSettings.ReviewAppealMaxRounds;
        var reviewBody = group.LastReviewBody ?? result.ReviewBody ?? "";

        await using var scope = serviceProvider.CreateAsyncScope();
        var pmService = scope.ServiceProvider.GetRequiredService<PmAgentService>();

        var currentCriticalIds = ExtractCriticalIdsFromReviewBody(reviewBody);
        if (currentCriticalIds.Count == 0)
        {
            // 有 CriticalReviewCount 但解析不到 IDs（格式問題）→ 直接走 Petra 閘門
            logger.LogWarning("有 Critical 但無法解析 ID，直接走 Petra 閘門（Group={Id}）", group.Id);
            return await RunPetraGateAsync(group, result, taskRepo, projectId, cancellationToken);
        }

        var indentedOptions = new JsonSerializerOptions { WriteIndented = true };

        // ── 迴圈 A：Cody-Vera 純對話，最多 maxRounds 輪，不涉及程式碼修改 ──
        while (group.ReviewAppealRoundA < maxRounds && currentCriticalIds.Count > 0)
        {
            var round = group.ReviewAppealRoundA + 1;
            logger.LogInformation("Appeal Round A {Round}（Group={Id}）", round, group.Id);

            // Cody 逐條回應（第二輪起帶入累計紀錄，讓 Cody 只針對剩餘 criticals 回應）
            var priorContext = group.ReviewAppealRoundA > 0 ? group.ReviewAppealLog : null;
            var codyAppeal   = await pmService.RunCodyAppealAsync(
                reviewBody, group.Title, currentCriticalIds, priorContext, cancellationToken);
            var codyJson     = JsonSerializer.Serialize(codyAppeal, indentedOptions);

            var disagrees = codyAppeal.Items.Where(i => i.Response == "disagree").ToList();

            if (disagrees.Count == 0)
            {
                // Cody 全部 agree → 停止迴圈，進修正流程
                AppendAppealLog(group, round,
                    $"**Cody 回應（完整）：**\n```json\n{codyJson}\n```\n\n→ Cody 同意所有 Critical，進入修正流程。");
                group.ReviewAppealRoundA++;
                await taskRepo.SaveAsync(cancellationToken);
                break;
            }

            // Vera 基於程式碼事實重新評估 disagree 項目
            var veraResponse = await pmService.RunVeraAppealAsync(reviewBody, codyJson, cancellationToken);
            var veraJson     = JsonSerializer.Serialize(veraResponse, indentedOptions);

            // 更新剩餘 critical 清單（移除 Vera 接受的）
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

        // 迴圈 A 結束：無剩餘 criticals → Petra 閘門放行
        if (currentCriticalIds.Count == 0)
        {
            result = result with { CriticalReviewCount = 0 };
            return await RunPetraGateAsync(group, result, taskRepo, projectId, cancellationToken);
        }

        // 仍有 criticals 且輪次達上限 → Petra 仲裁
        if (group.ReviewAppealRoundA >= maxRounds)
            return await RunPetraArbitrationAsync(group,
                result with { CriticalReviewCount = currentCriticalIds.Count },
                taskRepo, projectId, cancellationToken);

        // 仍有 criticals 但未達上限（Cody 全 agree 跳出 while）→ Petra 閘門（維持 criticals → Dev_fix）
        result = result with { CriticalReviewCount = currentCriticalIds.Count };
        return await RunPetraGateAsync(group, result, taskRepo, projectId, cancellationToken);
    }

    /// <summary>
    /// Petra 審核閘門：送 ReviewVeraAsync 並依 approve/revise/escalate 決策。
    /// 等同舊版 HandleReviewerCompletedAsync 的核心邏輯。
    /// </summary>
    private async Task<AgentExecutionResult?> RunPetraGateAsync(
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
    /// Appeal 達輪次上限後，由 Petra 仲裁最終決定。
    /// 仲裁後設 SkipReviewerAfterArbitration = true，Dev_fix 完成後跳過 Vera 直交 Petra。
    /// </summary>
    private async Task<AgentExecutionResult?> RunPetraArbitrationAsync(
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var pmService = scope.ServiceProvider.GetRequiredService<PmAgentService>();

        logger.LogInformation("Appeal 達上限，啟動 Petra 仲裁（Group={Id}）", group.Id);
        var arbitration = await pmService.ArbitrateReviewAppealAsync(
            group.LastReviewBody ?? "", group.ReviewAppealLog ?? "", cancellationToken);
        var arbitrationJson = JsonSerializer.Serialize(arbitration,
            new JsonSerializerOptions { WriteIndented = true });

        AppendAppealLog(group, group.ReviewAppealRoundA,
            $"**Petra 仲裁（完整）：**\n```json\n{arbitrationJson}\n```\n\n" +
            $"→ 最終 Critical：{arbitration.FinalCriticals.Count} 項，決定：{arbitration.Decision}");

        // 仲裁後 Dev_fix 完成 → 跳過 Vera，直接交 Petra 閘門
        group.SkipReviewerAfterArbitration = true;
        await taskRepo.SaveAsync(cancellationToken);

        result = result with { CriticalReviewCount = arbitration.FinalCriticals.Count };
        return await RunPetraGateAsync(group, result, taskRepo, projectId, cancellationToken);
    }

    /// <summary>
    /// Dev / Dev_fix 回報阻礙（[BLOCKED] 格式）時，由 Petra 評估路由。
    /// </summary>
    private async Task HandleDevBlockerAsync(
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var pmService   = scope.ServiceProvider.GetRequiredService<PmAgentService>();
        var ceoChannel  = FindChannel(_discord.Channels.CeoChannel);

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

        switch (decision.Routing)
        {
            case "continue":
                // 重觸發 Dev（重試）
                logger.LogInformation("Petra 決定重試 Dev：Group={Id}", group.Id);
                if (ceoChannel is not null)
                    await ceoChannel.SendMessageAsync(
                        $"⚠️ **{group.Title}** — Cody 回報阻礙，Petra 判定可重試，自動重新觸發 Dev。\n" +
                        $"原因：{decision.Instructions}");
                await FireStepsAsync(group, [new WorkflowStep("Dev")], cancellationToken);
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

    // ── Appeal 輔助方法 ──

    private static void AppendAppealLog(TaskGroup group, int round, string content)
    {
        var entry = $"\n\n### Appeal Round {round} — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n{content}";
        group.ReviewAppealLog = (group.ReviewAppealLog ?? "# Review Appeal 紀錄\n") + entry;
    }

    // ---- Stage 24：Dev_plan Appeal ----

    private static void AppendDevPlanAppealLog(TaskGroup group, int round, string content)
    {
        var entry = $"\n\n### DevPlan Appeal Round {round} — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n{content}";
        group.DevPlanAppealLog = (group.DevPlanAppealLog ?? "# Dev_plan Appeal 紀錄\n") + entry;
    }

    /// <summary>
    /// Stage 24：Dev_plan Appeal 緊密迴圈（純 LLM，不跨 Agent 執行）。
    /// Cody 反駁 → Petra 重評 → 迴圈上限 → 回傳 true（說服成功）或 false（耗盡）。
    /// </summary>
    private async Task<bool> RunDevPlanAppealLoopAsync(
        TaskGroup group,
        Agents.PetraReview initialReview,
        TaskRepository taskRepo,
        CancellationToken cancellationToken)
    {
        var maxRounds     = _workflowSettings.DevPlanAppealMaxRounds;
        var currentReview = initialReview;
        var priorContext  = $"Petra 初審意見：{initialReview.Summary}";

        await using var scope = serviceProvider.CreateAsyncScope();
        var pmService = scope.ServiceProvider.GetRequiredService<Agents.PmAgentService>();

        while (group.DevPlanAppealRoundA < maxRounds)
        {
            group.DevPlanAppealRoundA++;

            // Cody 反駁
            var codyAppeal = await pmService.RunCodyDevPlanAppealAsync(
                group, currentReview, priorContext, cancellationToken);
            var codyJson = JsonSerializer.Serialize(codyAppeal);

            if (codyAppeal.Position == "accept")
            {
                // Cody 接受修改意見，共識達成，放行
                AppendDevPlanAppealLog(group, group.DevPlanAppealRoundA,
                    $"**Cody 接受修改意見，Appeal 終止。**\n```json\n{codyJson}\n```");
                await taskRepo.SaveAsync(cancellationToken);
                logger.LogInformation("Dev_plan Appeal Round {Round}：Cody 接受，共識達成（Group={Id}）",
                    group.DevPlanAppealRoundA, group.Id);
                return true;
            }

            // Petra 重評
            var newReview   = await pmService.ReassessDevPlanAsync(group, codyAppeal, currentReview, cancellationToken);
            var petraJson   = JsonSerializer.Serialize(newReview);

            // 完整記錄
            AppendDevPlanAppealLog(group, group.DevPlanAppealRoundA,
                $"**Cody 反駁（完整）：**\n```json\n{codyJson}\n```\n\n**Petra 重評（完整）：**\n```json\n{petraJson}\n```");
            await taskRepo.SaveAsync(cancellationToken);

            logger.LogInformation("Dev_plan Appeal Round {Round}：Petra 決定 {Decision}（Group={Id}）",
                group.DevPlanAppealRoundA, newReview.Decision, group.Id);

            if (newReview.Decision == "approve")
                return true; // 說服成功

            // 更新下輪 context
            priorContext  = $"（已進行 {group.DevPlanAppealRoundA} 輪 Appeal，Petra 維持修改意見：{newReview.Summary}）";
            currentReview = newReview;
        }

        // 耗盡輪次
        logger.LogWarning("Dev_plan Appeal 耗盡 {MaxRounds} 輪，升級老闆（Group={Id}）", maxRounds, group.Id);
        return false;
    }

    /// <summary>
    /// 從審查報告中解析 Critical 段落內的 Issue IDs（格式：[#N]）。
    /// </summary>
    private static IReadOnlyList<int> ExtractCriticalIdsFromReviewBody(string reviewBody)
    {
        if (string.IsNullOrWhiteSpace(reviewBody)) return [];

        // 找到 Critical 段落開頭
        var criticalIdx = reviewBody.IndexOf("必須修改（Critical）", StringComparison.Ordinal);
        if (criticalIdx < 0) return [];

        // 找到下一個段落標頭（### 或 ---）作為結束邊界
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
            GroupId   = group.Id,
            Title     = petraTask.Title,
            AgentName = petraTask.AssignedAgent,
            Status    = "running"
        });
        // Stage 18：PM (Petra) DevPlan 審核開始，推送 running
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
            GroupId   = group.Id,
            Title     = petraTask.Title,
            AgentName = petraTask.AssignedAgent,
            Status    = petraStatus
        });
        // Stage 18：PM (Petra) DevPlan 審核結束，推送 idle / error
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
            GroupId   = group.Id,
            Title     = petraTask.Title,
            AgentName = petraTask.AssignedAgent,
            Status    = "running"
        });
        // Stage 18：PM (Petra) Vera 審核開始，推送 running
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
            GroupId   = group.Id,
            Title     = petraTask.Title,
            AgentName = petraTask.AssignedAgent,
            Status    = petraStatus
        });
        // Stage 18：PM (Petra) Vera 審核結束，推送 idle / error
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

    // ────────────── Stage 25a：Kick-off 會議 ──────────────

    /// <summary>
    /// Stage 25a：執行 Kick-off 會議並進入 Christ 確認等待狀態。
    /// 由 FireOneStepAsync 偵測到 Kickoff 步驟時，在背景 Task.Run 中呼叫。
    /// </summary>
    private async Task RunKickoffMeetingAndWaitAsync(TaskGroup group, CancellationToken ct)
    {
        await using var scope   = serviceProvider.CreateAsyncScope();
        var taskRepo            = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService         = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var owner = _gitHub.Owner;
        var repo  = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;

        logger.LogInformation("TaskGroupService：Kick-off 會議開始（Group={Id}）", group.Id);

        // 推送 Kickoff 進行中狀態到 Dashboard
        await pushService.PushAgentStatusAsync(new Shared.ViewModels.AgentStatusViewModel
        {
            AgentName        = AgentNames.Pm,
            Status           = "running",
            CurrentTaskTitle = $"Kick-off 會議：{group.Title}"
        });

        try
        {
            // 取得提案內容（從 group.Title 加 IssueUrls/UiSpec 組合，同 BuildTaskDescription）
            var proposalContent = BuildKickoffProposalContent(group);

            var meetingResult = await meetingService.RunKickoffMeetingAsync(
                group, proposalContent, owner, repo, ct);

            // 重新載入 group（meeting 執行期間 group 物件可能過時）
            await using var scope2   = serviceProvider.CreateAsyncScope();
            var taskRepo2            = scope2.ServiceProvider.GetRequiredService<TaskRepository>();
            var freshGroup           = await taskRepo2.GetGroupByIdAsync(group.Id, ct);
            if (freshGroup is null)
            {
                logger.LogError("TaskGroupService：Kick-off 完成後找不到 Group={Id}", group.Id);
                return;
            }

            // 儲存會議紀錄與計劃書
            freshGroup.KickoffMeetingLog = meetingResult.MeetingLog;
            freshGroup.TaskPlan          = meetingResult.TaskPlan;
            freshGroup.KickoffRound      = meetingResult.TotalRounds;
            await taskRepo2.SaveAsync(ct);

            logger.LogInformation("TaskGroupService：Kick-off 會議記錄已存入 DB（Group={Id}）", group.Id);

            // 通知 CEO 頻道，進入 Christ 確認等待
            var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
            if (ceoChannel is null)
            {
                logger.LogError("TaskGroupService：找不到 CEO 頻道，無法上呈 Kick-off 結果");
                return;
            }

            // 計劃書摘要（前 500 字）
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
                .WithFooter("▶️ 繼續 = 進入 Dev_plan；⏹️ 停止 = 取消任務；✏️ 修改 = 調整計劃書")
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            var buttons = new ComponentBuilder()
                .WithButton("▶️ 繼續開發",  $"kickoff_continue_{freshGroup.Id}", ButtonStyle.Success)
                .WithButton("⏹️ 停止任務",  $"kickoff_stop_{freshGroup.Id}",     ButtonStyle.Danger)
                .WithButton("✏️ 修改計劃書", $"kickoff_modify_{freshGroup.Id}",  ButtonStyle.Secondary)
                .Build();

            var msg = await ceoChannel.SendMessageAsync(embed: embed, components: buttons);

            // 登記 CommandHandler，供按鈕回調路由
            var commandHandler = serviceProvider.GetRequiredService<Discord.CommandHandler>();
            commandHandler.RegisterKickoffConfirmation(msg.Id, freshGroup.Id, planPreview);

            await pushService.PushAgentStatusAsync(new Shared.ViewModels.AgentStatusViewModel
            {
                AgentName        = AgentNames.Pm,
                Status           = "idle",
                CurrentTaskTitle = null
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TaskGroupService：Kick-off 會議失敗（Group={Id}）", group.Id);
            await pushService.PushAgentStatusAsync(new Shared.ViewModels.AgentStatusViewModel
            {
                AgentName = AgentNames.Pm,
                Status    = "error",
                CurrentTaskTitle = $"Kick-off 失敗：{ex.Message}"
            });
        }
    }

    /// <summary>
    /// Stage 25a：Christ 確認 Kick-off 計劃書後的路由處理。
    /// 由 CommandHandler 按鈕回調呼叫。
    /// </summary>
    public async Task HandleKickoffConfirmedAsync(
        Guid groupId,
        string decision,        // "continue" | "stop" | "modify"
        string? modifyContent = null,
        CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService       = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var group = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null)
        {
            logger.LogError("TaskGroupService：HandleKickoffConfirmed 找不到 Group={Id}", groupId);
            return;
        }

        var owner = _gitHub.Owner;
        var repo  = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;

        switch (decision.ToLower())
        {
            case "continue":
                logger.LogInformation("TaskGroupService：Kick-off 確認繼續（Group={Id}）", groupId);
                await meetingService.CloseAllSessionsAsync(groupId);
                // 觸發 Dev_plan
                await FireStepsAsync(group, [new WorkflowStep("Dev_plan")], ct);
                break;

            case "stop":
                logger.LogInformation("TaskGroupService：Kick-off 確認停止（Group={Id}）", groupId);
                await meetingService.CloseAllSessionsAsync(groupId);
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
                    logger.LogWarning("TaskGroupService：Kick-off 修改意見為空（Group={Id}）", groupId);
                    return;
                }

                logger.LogInformation("TaskGroupService：Kick-off 計劃書修改（Group={Id}）", groupId);

                var modifyResult = await meetingService.ModifyTaskPlanAsync(
                    group, modifyContent, owner, repo, ct);

                // 記錄修改過程（追加至 KickoffMeetingLog）
                var modifyLogEntry =
                    $"\n## Christ 修改 Round {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n" +
                    $"### Christ 修改意見\n{modifyContent}\n\n" +
                    $"### Petra 回應（完整）\n{modifyResult.PetraFullOutput}\n";

                group.KickoffMeetingLog = (group.KickoffMeetingLog ?? "") + modifyLogEntry;

                if (modifyResult.Impact == "small" && !string.IsNullOrWhiteSpace(modifyResult.RevisedPlan))
                    group.TaskPlan = modifyResult.RevisedPlan;

                await taskRepo.SaveAsync(ct);

                // 依修改影響大小回應 Christ
                var ceoChannelModify = FindChannel(_discord.Channels.CeoChannel);
                if (ceoChannelModify is null) return;

                if (modifyResult.Impact == "large")
                {
                    // 大修改：建議重新召開 Kick-off
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
                    var commandHandler = serviceProvider.GetRequiredService<Discord.CommandHandler>();
                    commandHandler.RegisterKickoffConfirmation(reMsg.Id, group.Id,
                        modifyResult.RevisedPlan ?? group.TaskPlan ?? "");
                }
                else
                {
                    // 小修改：直接展示修改後計劃書，再次請 Christ 確認
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
                    var commandHandler = serviceProvider.GetRequiredService<Discord.CommandHandler>();
                    commandHandler.RegisterKickoffConfirmation(reMsg.Id, group.Id, planPreview);
                }
                break;

            case "restart":
                // 重新召開 Kick-off（大修改後 Christ 確認重開）
                logger.LogInformation("TaskGroupService：Kick-off 重新召開（Group={Id}）", groupId);
                await meetingService.CloseAllSessionsAsync(groupId);
                // 重置輪次計數，重觸發 Kickoff 步驟
                group.KickoffRound = 0;
                await taskRepo.SaveAsync(ct);
                await FireStepsAsync(group, [new WorkflowStep(AgentNames.Kickoff)], ct);
                break;

            default:
                logger.LogWarning("TaskGroupService：未知的 Kickoff 決策：{Decision}（Group={Id}）", decision, groupId);
                break;
        }
    }

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
