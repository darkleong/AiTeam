using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Bot.Configuration;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        if (status == "passed")
        {
            var decision = workflowEngine.GetDecision(workflowType, AgentNames.Qa, result, group.FixIteration);
            if (decision.Action == NextAction.NotifyBossMerge)
            {
                taskRepo.UpdateGroupStatus(group, "done");
                await taskRepo.SaveAsync(cancellationToken);
                await tgs.NotifyBossMergeAsync(group, cancellationToken);
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
                var decision = workflowEngine.GetDecision(workflowType, AgentNames.Qa, result, group.FixIteration);
                if (decision.Action == NextAction.NotifyBossMerge)
                {
                    taskRepo.UpdateGroupStatus(group, "done");
                    await taskRepo.SaveAsync(cancellationToken);
                    await tgs.NotifyBossMergeAsync(group, cancellationToken);
                }
                else if (decision.Action == NextAction.FireAgents)
                {
                    await tgs.FireStepsAsync(group, decision.NextSteps, cancellationToken);
                }
            }
            else
            {
                taskRepo.UpdateGroupStatus(group, "failed");
                await taskRepo.SaveAsync(cancellationToken);
                await tgs.NotifyBossInterventionAsync(group, cancellationToken);
            }
            return;
        }

        // failed
        var qaFixMaxRounds = await workflowResolver.GetQaFixMaxRoundsAsync(cancellationToken);
        if (group.QaFixRound >= qaFixMaxRounds)
        {
            logger.LogWarning("QA 修復超過上限（Round={Round}），升級老闆（Group={Id}）",
                group.QaFixRound, group.Id);
            taskRepo.UpdateGroupStatus(group, "failed");
            await taskRepo.SaveAsync(cancellationToken);
            await tgs.NotifyBossInterventionAsync(group, cancellationToken);
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
                    await tgs.FireStepsAsync(group, [new WorkflowStep("Dev_fix")], cancellationToken);
                    break;

                case "back_to_reviewer":
                    group.QaFixRound = 0;
                    group.FixIteration++;
                    await taskRepo.SaveAsync(cancellationToken);
                    await tgs.FireStepsAsync(group, [new WorkflowStep("Dev_fix", IsFixLoop: true)], cancellationToken);
                    break;

                case "env_or_test_issue":
                    var decision = workflowEngine.GetDecision(workflowType, AgentNames.Qa, result, group.FixIteration);
                    if (decision.Action == NextAction.NotifyBossMerge)
                    {
                        taskRepo.UpdateGroupStatus(group, "done");
                        await taskRepo.SaveAsync(cancellationToken);
                        await tgs.NotifyBossMergeAsync(group, cancellationToken);
                    }
                    else if (decision.Action == NextAction.FireAgents)
                    {
                        await tgs.FireStepsAsync(group, decision.NextSteps, cancellationToken);
                    }
                    break;

                default: // escalate_boss
                    taskRepo.UpdateGroupStatus(group, "failed");
                    await taskRepo.SaveAsync(cancellationToken);
                    await tgs.NotifyBossInterventionAsync(group, cancellationToken);
                    break;
            }
        }
    }
}
