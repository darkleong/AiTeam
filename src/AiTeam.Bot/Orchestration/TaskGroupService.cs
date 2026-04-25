using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Orchestration.Appeal;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Orchestration.Proposal;
using AiTeam.Bot.Orchestration.Qa;
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
/// Stage 36：任務群組管理與主流程 dispatcher（瘦身版，從 2623 行拆至 ~500 行）。
///
/// 職責：
///   - 任務群組 CRUD（CreateGroupAsync）
///   - 主 dispatcher（HandleAgentCompletedAsync：DB 狀態更新 + 依 completedAgent 分派到子 service）
///   - 步驟派工（FireStepsAsync / FireOneStepAsync）
///   - 取消任務（CancelAsync）
///   - Dashboard 回覆分派入口（ProcessBossResponseAsync → Meeting/Appeal/Proposal service）
///   - Notify 系列輔助（Merge / Intervention）
///
/// 拆出的職責：
///   - Kickoff / Design / Crash Recovery → <see cref="MeetingOrchestrationService"/>
///   - Review Appeal + Dev_plan Appeal + Petra 審核 → <see cref="AppealOrchestrationService"/>
///   - QA 路由 → <see cref="QaCoordinationService"/>
///   - Dashboard 路徑 Proposal/Exec Confirm → <see cref="ProposalConfirmationService"/>
/// </summary>
public class TaskGroupService(
    IServiceProvider serviceProvider,
    DiscordSocketClient discordClient,
    IOptions<DiscordSettings> discordSettings,
    IOptions<GitHubSettings> gitHubSettings,
    WorkflowEngine workflowEngine,
    AgentQueueService agentQueueService,
    InteractionService interactionService,
    IHostApplicationLifetime appLifetime,
    MeetingOrchestrationService meetingOrchestration,
    AppealOrchestrationService appealOrchestration,
    QaCoordinationService qaCoordination,
    ProposalConfirmationService proposalConfirmation,
    ILogger<TaskGroupService> logger)
{
    private readonly DiscordSettings _discord = discordSettings.Value;
    private readonly GitHubSettings  _gitHub  = gitHubSettings.Value;

    // ============================================================
    //  任務群組建立
    // ============================================================

    public async Task<TaskGroup> CreateGroupAsync(
        string title,
        string project,
        WorkflowType workflowType,
        string? issueUrlsJson  = null,
        string? uiSpecContent  = null,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();

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

    // ============================================================
    //  主 dispatcher（Stage 36：瘦身版）
    // ============================================================

    /// <summary>
    /// Agent 完成後 dispatcher。依 completedAgent 分派到對應子 service，落底呼 WorkflowEngine。
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
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var group = await taskRepo.GetGroupByIdAsync(groupId, cancellationToken);
        if (group is null)
        {
            logger.LogWarning("HandleAgentCompleted：找不到 TaskGroup（Id={Id}）", groupId);
            return;
        }

        if (group.Status is "done" or "failed")
        {
            logger.LogDebug("HandleAgentCompleted：TaskGroup {Id} 已結束（{Status}），略過", groupId, group.Status);
            return;
        }

        // ── 合併 DB 狀態更新（避免多次 SaveAsync）──
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

        // ── Dev_plan 完成 → Petra 審核 + Appeal（Stage 37：搬至 AppealOrchestrationService.HandleDevPlanCompletedAsync）──
        if (completedAgent.Equals("Dev_plan", StringComparison.OrdinalIgnoreCase))
        {
            var groupProjectId = await GetGroupProjectIdAsync(group, taskRepo, cancellationToken);
            var shouldContinue = await appealOrchestration.HandleDevPlanCompletedAsync(
                group, result, taskRepo, groupProjectId, cancellationToken);
            if (!shouldContinue) return;
        }

        // ── Reviewer 完成 → AppealOrchestrationService ──
        if (completedAgent.Equals("Reviewer", StringComparison.OrdinalIgnoreCase))
        {
            // Stage 39：Skipped 結果（Vera 收到無可審檔案）也走「放行」路徑，跳過 Petra 評審
            if (!result.Success || result.ResultType == AgentResultType.Skipped)
            {
                if (result.ResultType == AgentResultType.Skipped)
                    logger.LogInformation("Vera 略過（{Summary}），跳過 Petra 審核，直接放行", result.Summary);
                else
                    logger.LogWarning("Vera 執行失敗（{Summary}），跳過 Petra 審核，直接放行", result.Summary);
                result = result with { CriticalReviewCount = 0 };
            }
            else
            {
                var groupProjectId = await GetGroupProjectIdAsync(group, taskRepo, cancellationToken);
                var reviewResult = await appealOrchestration.HandleReviewerCompletedAsync(
                    group, result, taskRepo, groupProjectId, cancellationToken);
                if (reviewResult is null) return;
                result = reviewResult;
            }
        }

        // ── Dev / Dev_fix 阻礙 → AppealOrchestrationService ──
        if ((completedAgent.Equals("Dev", StringComparison.OrdinalIgnoreCase)
             || completedAgent.Equals("Dev_fix", StringComparison.OrdinalIgnoreCase))
            && !result.Success
            && result.Summary.StartsWith("[BLOCKED]", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(result.OutputContent))
        {
            logger.LogWarning("Dev 回報阻礙，啟動 Petra 評估：Group={Id}", groupId);
            var groupProjectId = await GetGroupProjectIdAsync(group, taskRepo, cancellationToken);
            await appealOrchestration.HandleDevBlockerAsync(group, result, taskRepo, groupProjectId, cancellationToken);
            return;
        }

        // ── 仲裁後 Dev_fix 完成 → 跳過 Vera，直接 Petra 閘門 ──
        if (completedAgent.Equals("Dev_fix", StringComparison.OrdinalIgnoreCase)
            && result.Success
            && group.SkipReviewerAfterArbitration)
        {
            logger.LogInformation("仲裁後 Dev_fix 完成，跳過 Vera，直接交 Petra 閘門（Group={Id}）", group.Id);
            group.SkipReviewerAfterArbitration = false;
            await taskRepo.SaveAsync(cancellationToken);
            var groupProjectId = await GetGroupProjectIdAsync(group, taskRepo, cancellationToken);
            var petraResult = await appealOrchestration.RunPetraGateAsync(
                group, result, taskRepo, groupProjectId, cancellationToken);
            if (petraResult is null) return;
            result = petraResult;
        }

        // ── QA 修復模式 Dev_fix 完成 → 重新觸發 QA ──
        if (completedAgent.Equals("Dev_fix", StringComparison.OrdinalIgnoreCase)
            && result.Success
            && group.QaFixRound > 0)
        {
            logger.LogInformation("QA 修復後 Dev_fix 完成，重新觸發 QA（Group={Id}, Round={Round}）",
                group.Id, group.QaFixRound);
            await FireStepsAsync(group, [new WorkflowStep(AgentNames.Qa)], cancellationToken);
            return;
        }

        // ── Dev 初次失敗 → 通知老闆介入 ──
        if (completedAgent.Equals("Dev", StringComparison.OrdinalIgnoreCase) && !result.Success)
        {
            logger.LogError("Dev Agent 執行失敗，停止工作流程：Group={Id}，原因：{Summary}",
                group.Id, result.Summary);
            taskRepo.UpdateGroupStatus(group, "failed");
            await taskRepo.SaveAsync(cancellationToken);
            await NotifyBossInterventionAsync(group, cancellationToken);
            return;
        }

        // ── QA 完成 → QaCoordinationService ──
        if (completedAgent.Equals(AgentNames.Qa, StringComparison.OrdinalIgnoreCase) && result.Success)
        {
            await qaCoordination.HandleQaCompletedAsync(group, result, taskRepo, cancellationToken);
            return;
        }

        // ── 落底 WorkflowEngine.GetDecision ──
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

    // ============================================================
    //  Mock Mode 輔助
    // ============================================================

    public async Task FireMockProposalAndContinueAsync(TaskGroup group, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[MockMode] 模擬提案核准完成，觸發 Kickoff 流程");
        await FireStepsAsync(group, [new WorkflowStep(AgentNames.Kickoff)], cancellationToken);
    }

    // ============================================================
    //  觸發 Agent 執行
    // ============================================================

    public async Task FireStepsAsync(
        TaskGroup group,
        IReadOnlyList<WorkflowStep> steps,
        CancellationToken cancellationToken = default)
    {
        if (steps.Count == 0) return;

        foreach (var step in steps)
            await FireOneStepAsync(group, step, cancellationToken);
    }

    private async Task FireOneStepAsync(
        TaskGroup group,
        WorkflowStep step,
        CancellationToken cancellationToken)
    {
        // Kickoff / Design 步驟交由 MeetingOrchestrationService
        if (step.AgentName.Equals(AgentNames.Kickoff, StringComparison.OrdinalIgnoreCase))
        {
            _ = Task.Run(async () =>
            {
                try { await meetingOrchestration.RunKickoffMeetingAndWaitAsync(group, appLifetime.ApplicationStopping); }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TaskGroupService：Kickoff 會議執行失敗（Group={Id}）", group.Id);
                }
            }, appLifetime.ApplicationStopping);
            return;
        }

        if (step.AgentName.Equals(AgentNames.Design, StringComparison.OrdinalIgnoreCase))
        {
            _ = Task.Run(async () =>
            {
                try { await meetingOrchestration.RunDesignPhaseAsync(group, appLifetime.ApplicationStopping); }
                catch (Exception ex)
                {
                    logger.LogError(ex, "TaskGroupService：Design 會議執行失敗（Group={Id}）", group.Id);
                }
            }, appLifetime.ApplicationStopping);
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var projectId = string.IsNullOrWhiteSpace(group.Project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(group.Project, cancellationToken);

        var workflowAgentKey = step.IsFixLoop && step.AgentName == AgentNames.Dev
            ? "Dev_fix"
            : step.AgentName;

        var taskItem = new TaskItem
        {
            Title            = $"{group.Title}（{step.AgentName}）",
            Description      = BuildTaskDescription(group, step),
            TriggeredBy      = "Orchestrator",
            AssignedAgent    = step.AgentName,
            Status           = "queued",
            GroupId          = group.Id,
            ProjectId        = projectId,
            WorkflowAgentKey = workflowAgentKey,
        };

        taskRepo.Add(taskItem);
        await taskRepo.SaveAsync(cancellationToken);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = taskItem.Id,
            GroupId   = group.Id,
            Title     = taskItem.Title,
            AgentName = taskItem.AssignedAgent,
            Status    = "queued"
        });

        await agentQueueService.EnqueueAsync(taskItem, cancellationToken);

        logger.LogInformation("TaskGroupService：{Agent} 任務已入佇列（Task={Id}，Group={GroupId}）",
            step.AgentName, taskItem.Id, group.Id);
    }

    // ============================================================
    //  取消任務（Stage 14）
    // ============================================================

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

        await agentQueueService.CancelQueuedTasksForGroupAsync(groupId, cancellationToken);

        foreach (var task in group.Tasks)
            agentQueueService.TryCancel(task.Id);

        taskRepo.CancelGroupItems(group);
        taskRepo.UpdateGroupStatus(group, "cancelled");
        await taskRepo.SaveAsync(cancellationToken);

        logger.LogInformation("TaskGroup {Id}（{Title}）已取消", groupId, group.Title);
    }

    // ============================================================
    //  通知老闆（public 供子 service 呼叫）
    // ============================================================

    public async Task NotifyBossMergeAsync(TaskGroup group, CancellationToken cancellationToken)
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

        _ = interactionService.CreateInteractionAsync(
            "merge_notify",
            title:                $"全流程完成：{group.Title}",
            description:          $"PR：{prLink}（含 code + tests + docs），請確認後合併。",
            project:              group.Project,
            agentName:            null,
            availableActionsJson: InteractionService.NotifyActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId = ceoChannel.Id.ToString(),
                groupId   = group.Id.ToString(),
                prUrl     = group.DevPrUrl ?? ""
            }),
            taskGroupId: group.Id);
    }

    public async Task NotifyBossInterventionAsync(TaskGroup group, CancellationToken cancellationToken)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null) return;

        await ceoChannel.SendMessageAsync(
            $"⚠️ **{group.Title}** — Vera 在 {group.FixIteration} 次修復後仍發現 🔴 問題，需要您介入處理。\n" +
            $"PR：{group.DevPrUrl ?? "（無）"}");

        logger.LogWarning("TaskGroup {Id} 修復次數超限（{Count} 次），升級給老闆", group.Id, group.FixIteration);

        _ = interactionService.CreateInteractionAsync(
            "intervention",
            title:                $"需要介入：{group.Title}",
            description:          $"Vera 在 {group.FixIteration} 次修復後仍發現問題，需要您介入處理。",
            project:              group.Project,
            agentName:            null,
            availableActionsJson: InteractionService.NotifyActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId    = ceoChannel.Id.ToString(),
                groupId      = group.Id.ToString(),
                prUrl        = group.DevPrUrl ?? "",
                fixIteration = group.FixIteration
            }),
            taskGroupId: group.Id);
    }

    // ============================================================
    //  Meeting service 薄 wrapper（保留 public 簽名供外部 caller）
    // ============================================================

    public Task RecoverStuckOrchestrationsAsync(CancellationToken ct)
        => meetingOrchestration.RecoverStuckOrchestrationsAsync(ct);

    public Task HandleKickoffConfirmedAsync(
        Guid groupId, string decision, string? modifyContent = null, CancellationToken ct = default)
        => meetingOrchestration.HandleKickoffConfirmedAsync(groupId, decision, modifyContent, ct);

    public Task HandleDesignConfirmedAsync(
        Guid groupId, string decision, string petraSessionId,
        string? modifyContent = null, CancellationToken ct = default)
        => meetingOrchestration.HandleDesignConfirmedAsync(groupId, decision, petraSessionId, modifyContent, ct);

    // ============================================================
    //  Dashboard 回覆分派入口（Stage 28a）
    // ============================================================

    public async Task ProcessBossResponseAsync(
        string interactionType, string action, string? contextJson,
        string? responseContent = null, CancellationToken ct = default)
    {
        switch (interactionType)
        {
            case "ceo_confirm":
                if (action == "confirm_yes" && contextJson is not null)
                    await proposalConfirmation.ProcessCeoConfirmAsync(contextJson, ct);
                else
                    logger.LogInformation("InteractionProcessor：CEO 確認取消（action={Action}）", action);
                break;

            case "exec_confirm":
                if (action == "exec_yes" && contextJson is not null)
                    await proposalConfirmation.ProcessExecConfirmAsync(contextJson, ct);
                else if (action == "exec_no" && contextJson is not null)
                    await proposalConfirmation.CancelTaskItemFromContextAsync(contextJson, ct);
                else
                    logger.LogInformation("InteractionProcessor：Agent 執行取消（action={Action}）", action);
                break;

            case "proposal":
                if (action == "propose_yes" && contextJson is not null)
                    await proposalConfirmation.ProcessProposalApprovedAsync(contextJson, ct);
                else if (action == "propose_adjust" && contextJson is not null)
                    await proposalConfirmation.ProcessProposalAdjustAsync(contextJson, responseContent, ct);
                else
                    logger.LogInformation("InteractionProcessor：提案取消（action={Action}）", action);
                break;

            case "kickoff":
            {
                if (contextJson is null) return;
                using var doc  = JsonDocument.Parse(contextJson);
                var groupIdStr = doc.RootElement.GetProperty("groupId").GetString() ?? "";
                if (!Guid.TryParse(groupIdStr, out var groupId)) return;
                var decision   = action.Replace("kickoff_", "");
                await HandleKickoffConfirmedAsync(groupId, decision, responseContent, ct);
                break;
            }

            case "design":
            {
                if (contextJson is null) return;
                using var doc        = JsonDocument.Parse(contextJson);
                var groupIdStr       = doc.RootElement.GetProperty("groupId").GetString() ?? "";
                var petraSessionId   = doc.RootElement.GetProperty("petraSessionId").GetString() ?? "";
                if (!Guid.TryParse(groupIdStr, out var groupId)) return;
                var decision         = action.Replace("design_", "");
                await HandleDesignConfirmedAsync(groupId, decision, petraSessionId, responseContent, ct);
                break;
            }

            case "devplan_escalate":
                if (contextJson is not null)
                    await appealOrchestration.HandleDevPlanEscalationAsync(contextJson, action, ct);
                break;

            default:
                logger.LogInformation("InteractionProcessor：無需處理的互動類型（{Type}）", interactionType);
                break;
        }
    }

    // ============================================================
    //  輔助方法
    // ============================================================

    private async Task<Guid?> GetGroupProjectIdAsync(
        TaskGroup group, TaskRepository taskRepo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(group.Project)) return null;
        var projectId = await taskRepo.GetProjectIdByNameAsync(group.Project, cancellationToken);
        if (projectId is null)
            logger.LogWarning("HandleAgentCompleted：找不到專案名稱 '{Project}'，Petra TaskItem.ProjectId 將為 null", group.Project);
        return projectId;
    }

    /// <summary>
    /// 組建 TaskItem.Description，附帶 CEO 傳遞給 Dev 的上下文 metadata。
    /// </summary>
    private static string BuildTaskDescription(TaskGroup group, WorkflowStep step)
    {
        var desc = group.Title;

        if (step.AgentName.Equals("Dev_plan", StringComparison.OrdinalIgnoreCase))
        {
            var parts = new List<string> { desc };
            var meta  = new List<string> { "dev_plan_mode: true" };

            if (!string.IsNullOrWhiteSpace(group.IssueUrls))
                meta.Add($"issue_urls: {group.IssueUrls}");
            if (!string.IsNullOrWhiteSpace(group.UiSpecContent))
                meta.Add($"ui_spec_content:\n{group.UiSpecContent}");

            if (!string.IsNullOrWhiteSpace(group.DevPlan))
                meta.Add($"prev_dev_plan:\n{group.DevPlan}");

            if (!string.IsNullOrWhiteSpace(group.TaskPlan))
                meta.Add($"kickoff_task_plan:\n{group.TaskPlan}");

            if (!string.IsNullOrWhiteSpace(group.DesignPlan))
                meta.Add($"design_plan:\n{group.DesignPlan}");

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

            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(group.IssueUrls))
                meta.Add($"issue_urls: {group.IssueUrls}");
            if (!string.IsNullOrWhiteSpace(group.UiSpecContent))
                meta.Add($"ui_spec_content:\n{group.UiSpecContent}");

            if (step.AgentName == AgentNames.Dev && !string.IsNullOrWhiteSpace(group.DevPlan))
                meta.Add($"dev_plan:\n{group.DevPlan}");

            if (!string.IsNullOrWhiteSpace(group.TaskPlan))
                meta.Add($"kickoff_task_plan:\n{group.TaskPlan}");

            if (!string.IsNullOrWhiteSpace(group.DesignPlan))
                meta.Add($"design_plan:\n{group.DesignPlan}");

            if ((step.AgentName == AgentNames.Reviewer || step.AgentName == AgentNames.Qa)
                && !string.IsNullOrWhiteSpace(group.ImplementationNote))
                meta.Add($"implementation_note:\n{group.ImplementationNote}");

            if (step.AgentName == AgentNames.Reviewer && !string.IsNullOrWhiteSpace(group.DevPlan))
                meta.Add($"dev_plan:\n{group.DevPlan}");

            if (step.AgentName == AgentNames.Qa)
            {
                if (!string.IsNullOrWhiteSpace(group.IssueUrls))
                    meta.Add($"issues_list: {group.IssueUrls}");
                if (!string.IsNullOrWhiteSpace(group.DevPlan))
                    meta.Add($"dev_plan:\n{group.DevPlan}");
            }

            if (step.AgentName == AgentNames.Doc && !string.IsNullOrWhiteSpace(group.TestReport))
                meta.Add($"test_report:\n{group.TestReport}");

            if (step.IsFixLoop)
            {
                meta.Add("fix_loop: true");
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

    private IMessageChannel? FindChannel(string channelName)
    {
        if (!ulong.TryParse(_discord.GuildId, out var guildId)) return null;
        return discordClient.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }
}
