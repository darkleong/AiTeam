using System.Text.Json;
using TaskStatus = AiTeam.Shared.Constants.TaskStatus;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Orchestration.Qa;

/// <summary>
/// Stage 36：QA 流程編排（從 TaskGroupService 拆解）。
///
/// Stage 24：Petra 判斷 TestReport → 4 路由（code_bug / back_to_reviewer / env_or_test_issue / escalate_boss）。
/// </summary>
public class QaCoordinationService(
    IServiceProvider serviceProvider,
    WorkflowSettingsResolver workflowResolver,
    WorkflowEngine workflowEngine,
    ILogger<QaCoordinationService> logger)
{
    /// <summary>
    /// QA 完成後，Petra 評估 TestReport 決定路由。
    /// - passed → 走正常流程（Doc 或 merge）
    /// - failed → Petra 判斷 code_bug / back_to_reviewer / env_or_test_issue / escalate_boss
    /// - no_applicable_tests → Petra 判斷是否放行
    /// </summary>
    public async Task HandleQaCompletedAsync(
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        CancellationToken cancellationToken)
    {
        // Stage 37：Crash Recovery 標記。包整個 Petra 路由判斷，
        // 涵蓋 passed / failed / no_applicable_tests 三條路徑（每條都可能呼叫 Petra CLI subprocess 卡住）。
        await using var dbScope = serviceProvider.CreateAsyncScope();
        var db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == group.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, "QaRouting"),
                cancellationToken);

        try
        {
            await HandleQaCompletedInnerAsync(group, result, taskRepo, cancellationToken);
        }
        finally
        {
            await db.TaskGroups.Where(g => g.Id == group.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, (string?)null),
                    CancellationToken.None);
        }
    }

    private async Task HandleQaCompletedInnerAsync(
        TaskGroup group,
        AgentExecutionResult result,
        TaskRepository taskRepo,
        CancellationToken cancellationToken)
    {
        // Stage 53B 議題 F-1 修正 6-e：Pipeline path 接管 — failed 路徑 5 處 side effects 全 skip（QaFixRound++/FixIteration++/Save 保留供 Pipeline 重讀）
        var isPipelinePath = group.PipelineFrameworkStateJson != null;

        var workflowType = group.WorkflowType switch
        {
            "new_feature"      => WorkflowType.NewFeature,
            "tech_improvement" => WorkflowType.TechImprovement,
            _                  => WorkflowType.BugFix
        };

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

        var tgs = serviceProvider.GetRequiredService<TaskGroupService>();

        // Stage 53A 驗收期 follow-up #1（Aria 議題 G3 同類問題在 QA 重演）：
        // Pipeline QaStageExecutor 第二 handler 內 sync await call 本 method 後再 SendMessageAsync(DocStageBridge)。
        // 若本 method 內部 happy path 自動 FireStepsAsync(Doc) → Doc 重複 enqueue 2 次 +
        // 第 2 次 callback 進來時 PipelineFrameworkStateJson 已被 FinalizePipelineAsync 清 null → 走 legacy → 開第 2 個 merge_notify。
        // 修法：Pipeline path 下跳過 legacy GetDecision + FireStepsAsync + Mark Done + NotifyBoss（由 Pipeline NotifyMergeStageExecutor 接管）。
        if (status == "passed" && group.PipelineFrameworkStateJson != null)
        {
            logger.LogInformation("[Stage53A] HandleQaCompletedAsync passed：Pipeline path 接管推進，跳過 legacy GetDecision/FireStepsAsync/MarkDone（Group={Id}）", group.Id);
            return;
        }

        if (status == "passed")
        {
            var decision = workflowEngine.GetDecision(workflowType, AgentNames.Qa, result, group.FixIteration);
            if (decision.Action == NextAction.NotifyBossMerge)
            {
                // Stage 43-E：透過守門 method 統一 mark done
                await tgs.MarkGroupDoneOrInterventionAsync(group, taskRepo, cancellationToken);
                if (group.Status == TaskStatus.Done)
                    await tgs.NotifyBossMergeAsync(group, cancellationToken);
                else
                    await tgs.NotifyBossInterventionAsync(group, cancellationToken);
            }
            else if (decision.Action == NextAction.FireAgents)
            {
                await tgs.FireStepsAsync(group, decision.NextSteps, cancellationToken);
            }
            return;
        }

        if (status == "no_applicable_tests")
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var pmService = scope.ServiceProvider.GetRequiredService<PmRoutingService>();
            var noTestDecision = await pmService.AssessNoApplicableTestsAsync(group, report?.NoTestReason, cancellationToken);
            logger.LogInformation("Petra QA 無測試評估：{Routing}（Group={Id}）", noTestDecision.Routing, group.Id);

            if (noTestDecision.Routing == "approve")
            {
                // Stage 53A 驗收期 follow-up #1：Pipeline path 跳過 legacy fire next（同 passed 路徑修法）
                if (group.PipelineFrameworkStateJson != null)
                {
                    logger.LogInformation("[Stage53A] HandleQaCompletedAsync no_applicable_tests + approve：Pipeline path 接管推進，跳過 legacy GetDecision/FireStepsAsync/MarkDone（Group={Id}）", group.Id);
                    return;
                }

                var decision = workflowEngine.GetDecision(workflowType, AgentNames.Qa, result, group.FixIteration);
                if (decision.Action == NextAction.NotifyBossMerge)
                {
                    // Stage 43-E：透過守門 method 統一 mark done
                    await tgs.MarkGroupDoneOrInterventionAsync(group, taskRepo, cancellationToken);
                    if (group.Status == TaskStatus.Done)
                        await tgs.NotifyBossMergeAsync(group, cancellationToken);
                    else
                        await tgs.NotifyBossInterventionAsync(group, cancellationToken);
                }
                else if (decision.Action == NextAction.FireAgents)
                {
                    await tgs.FireStepsAsync(group, decision.NextSteps, cancellationToken);
                }
            }
            else
            {
                // Stage 43-E：no_applicable_tests + reject = Christ 介入後可恢復 → needs_intervention
                taskRepo.UpdateGroupStatus(group, TaskStatus.NeedsIntervention);
                group.InterventionReason = $"QA no_applicable_tests，Petra 判斷不可放行：{noTestDecision.Routing}";
                await taskRepo.SaveAsync(cancellationToken);
                await NotifyBossQaFailedInterventionAsync(group, $"QA 無適用測試且 Petra 判斷不可放行", cancellationToken);
            }
            return;
        }

        // failed
        var qaFixMaxRounds = await workflowResolver.GetQaFixMaxRoundsAsync(cancellationToken);
        if (group.QaFixRound >= qaFixMaxRounds)
        {
            logger.LogWarning("QA 修復超過上限（Round={Round}），升級老闆（Group={Id}）",
                group.QaFixRound, group.Id);
            // Stage 43-E：QaFixRound 超限 = 介入後可恢復 → needs_intervention
            taskRepo.UpdateGroupStatus(group, TaskStatus.NeedsIntervention);
            group.InterventionReason = $"QA 修復連 {group.QaFixRound} 輪失敗（上限 {qaFixMaxRounds}）";
            await taskRepo.SaveAsync(cancellationToken);
            // Stage 53B 議題 F-1 修正 6-e：Pipeline QaStage 自接管 intervention，skip legacy NotifyBoss
            if (!isPipelinePath)
                await NotifyBossQaFailedInterventionAsync(group, $"QA 修復連 {group.QaFixRound} 輪失敗", cancellationToken);
            return;
        }

        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var pmService = scope.ServiceProvider.GetRequiredService<PmRoutingService>();
            var failureDecision = await pmService.AssessQaFailureAsync(
                group, group.TestReport ?? "", cancellationToken);

            logger.LogInformation("Petra QA 失敗評估：{Routing}（Group={Id}）", failureDecision.Routing, group.Id);

            switch (failureDecision.Routing)
            {
                case "code_bug":
                    group.QaFixRound++;
                    await taskRepo.SaveAsync(cancellationToken);
                    // Stage 53B 議題 F-1 修正 6-e：Pipeline QaStage 重讀 group 看 QaFixRound > 0 → 自 SendMessage(DevFixStageBridge)
                    if (!isPipelinePath)
                        await tgs.FireStepsAsync(group, [new WorkflowStep("Dev_fix")], cancellationToken);
                    else
                        logger.LogInformation("[Stage53B] code_bug Pipeline path skip FireStepsAsync（Pipeline 自 SendMessage DevFixStageBridge）");
                    break;

                case "back_to_reviewer":
                    group.QaFixRound = 0;
                    group.FixIteration++;
                    await taskRepo.SaveAsync(cancellationToken);
                    if (!isPipelinePath)
                        await tgs.FireStepsAsync(group, [new WorkflowStep("Dev_fix", IsFixLoop: true)], cancellationToken);
                    else
                        logger.LogInformation("[Stage53B] back_to_reviewer Pipeline path skip FireStepsAsync");
                    break;

                case "env_or_test_issue":
                    // Stage 53B 議題 F-1 修正 6-e（Forge 主動拍板）：Pipeline path 下 env_or_test_issue 視為 passed
                    // → skip 全部 side effects（GetDecision/Mark/NotifyBoss/FireSteps），return 後 Pipeline QaStage 重讀 group 看 QaFixRound==0 + Status normal → SendMessage DocStageBridge
                    if (isPipelinePath)
                    {
                        logger.LogInformation("[Stage53B] env_or_test_issue Pipeline path skip GetDecision（Pipeline 自接管 推進 Doc）");
                        break;
                    }
                    var decision = workflowEngine.GetDecision(workflowType, AgentNames.Qa, result, group.FixIteration);
                    if (decision.Action == NextAction.NotifyBossMerge)
                    {
                        // Stage 43-E：透過守門 method 統一 mark done
                        await tgs.MarkGroupDoneOrInterventionAsync(group, taskRepo, cancellationToken);
                        if (group.Status == TaskStatus.Done)
                            await tgs.NotifyBossMergeAsync(group, cancellationToken);
                        else
                            await tgs.NotifyBossInterventionAsync(group, cancellationToken);
                    }
                    else if (decision.Action == NextAction.FireAgents)
                    {
                        await tgs.FireStepsAsync(group, decision.NextSteps, cancellationToken);
                    }
                    break;

                default: // escalate_boss
                    // Stage 43-E：Petra 判斷 escalate_boss = 介入後可恢復 → needs_intervention
                    taskRepo.UpdateGroupStatus(group, TaskStatus.NeedsIntervention);
                    group.InterventionReason = $"QA 失敗 Petra 判斷 escalate_boss：{failureDecision.Instructions}";
                    await taskRepo.SaveAsync(cancellationToken);
                    // Stage 53B 議題 F-1 修正 6-e：Pipeline QaStage 自接管 intervention，skip legacy NotifyBoss
                    if (!isPipelinePath)
                        await NotifyBossQaFailedInterventionAsync(group, failureDecision.Instructions ?? "Petra escalate_boss", cancellationToken);
                    break;
            }
        }
    }

    /// <summary>
    /// Stage 43-E：QA failed → 中止流程，通知老闆介入。
    /// 與 NotifyBossInterventionAsync（Vera fix loop 超限走 intervention type）區分用 qa_failed_intervention 細類。
    /// </summary>
    private async Task NotifyBossQaFailedInterventionAsync(
        TaskGroup group, string failReason, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var interactionService = scope.ServiceProvider.GetRequiredService<InteractionService>();
        var discordClient      = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
        var discordSettings    = scope.ServiceProvider.GetRequiredService<IOptions<DiscordSettings>>().Value;

        var ceoChannelId = discordSettings.Channels.CeoChannel;
        if (ulong.TryParse(ceoChannelId, out var ceoId) &&
            discordClient.GetChannel(ceoId) is IMessageChannel ceoChannel)
        {
            await ceoChannel.SendMessageAsync(
                $"⚠️ **{group.Title}** — QA 階段失敗連續 {group.QaFixRound} 輪，已中止流程。\n" +
                $"原因：{(failReason.Length > 300 ? failReason[..300] + "..." : failReason)}\n" +
                $"PR：{group.DevPrUrl ?? "（無）"}");
        }

        logger.LogWarning("TaskGroup {Id} QA failed 中止（Reason={R}）", group.Id, failReason);

        _ = interactionService.CreateInteractionAsync(
            "qa_failed_intervention",
            title:                $"QA 失敗：{group.Title}",
            description:          $"QA 階段失敗連續 {group.QaFixRound} 輪，需要您決定後續處理。原因：{(failReason.Length > 500 ? failReason[..500] + "..." : failReason)}",
            project:              group.Project,
            agentName:            AgentNames.Qa,
            availableActionsJson: InteractionService.QaFailedInterventionActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId   = ceoChannelId,
                groupId     = group.Id.ToString(),
                qaFixRound  = group.QaFixRound,
                prUrl       = group.DevPrUrl ?? ""
            }),
            taskGroupId: group.Id);
    }
}
