using System.Text.Json;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Bot.Orchestration.Epic;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Orchestration.Boss;

/// <summary>
/// Stage 59：Dashboard 回覆 5 case handler 集中管理（從 TaskGroupService D 區段拆出）。
///
/// ProcessBossResponseAsync 主 switch 留 TaskGroupService 主檔（dispatcher 入口語義 + caller 不變）；
/// 4 case body（dev_failed_intervention / qa_failed_intervention / sage_escalate / split_task_proposal）抽出：
///   ① HandleDevFailedInterventionAsync   — Stage 43-B：Dev/Dev_fix failed → skip / retry / abort
///   ② HandleQaFailedInterventionAsync    — Stage 43-E：QA failed → continue / skip / abort
///   ③ HandleSageEscalateAsync            — Stage 43-F：Sage escalate → retry / skip / abort（skip 走 MarkGroupDone）
///   ④ HandleSplitTaskProposalAsync       — Stage 46-FF 三十五：split → accept(Build sub-task) / modify / reject(fire Dev_plan) / abort
///
/// （HandleEpicPartialPaused 從 D 搬入到 EpicChainService — 集中管理 epic 機制；對齊 Roadmap E 區段設計）
///
/// 跨 service 依賴：透過 IServiceProvider lazy resolve TaskGroupService.FireStepsAsync / MarkGroupDoneOrInterventionAsync +
/// EpicChainService.BuildEpicSubTasksAsync（避免循環依賴 — 對齊 Stage 36 既有 IServiceProvider lazy resolve pattern）。
/// </summary>
public class BossResponseHandlerService(
    IServiceProvider serviceProvider,
    ILogger<BossResponseHandlerService> logger)
{
    /// <summary>Stage 43-B：Dev / Dev_fix failed intervention 路由（skip / retry / abort）。</summary>
    public async Task HandleDevFailedInterventionAsync(string contextJson, string action, CancellationToken ct)
    {
        using var doc  = JsonDocument.Parse(contextJson);
        if (!doc.RootElement.TryGetProperty("groupId", out var g)
            || !Guid.TryParse(g.GetString(), out var groupId))
            return;

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group    = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null) return;

        switch (action)
        {
            case "dev_intervention_skip":
                // 略過進下一階段 → fire Reviewer
                logger.LogInformation("Dev failed 介入：略過進 Reviewer（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "running");
                group.InterventionReason = null;
                await taskRepo.SaveAsync(ct);
                {
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.FireStepsAsync(group, [new WorkflowStep(AgentNames.Reviewer)], ct);
                }
                break;

            case "dev_intervention_retry":
                // 重啟 Dev
                logger.LogInformation("Dev failed 介入：重啟 Dev（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "running");
                group.InterventionReason = null;
                await taskRepo.SaveAsync(ct);
                {
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.FireStepsAsync(group, [new WorkflowStep(AgentNames.Dev)], ct);
                }
                break;

            case "dev_intervention_abort":
                // 放棄任務
                logger.LogInformation("Dev failed 介入：放棄任務（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "failed");
                await taskRepo.SaveAsync(ct);
                break;

            default:
                logger.LogWarning("Unknown dev_failed_intervention action: {A}", action);
                break;
        }
    }

    /// <summary>Stage 43-E：QA failed intervention 路由（continue / skip / abort）。</summary>
    public async Task HandleQaFailedInterventionAsync(string contextJson, string action, CancellationToken ct)
    {
        using var doc  = JsonDocument.Parse(contextJson);
        if (!doc.RootElement.TryGetProperty("groupId", out var g)
            || !Guid.TryParse(g.GetString(), out var groupId))
            return;

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group    = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null) return;

        switch (action)
        {
            case "qa_intervention_continue":
                // 再試一輪 QA
                logger.LogInformation("QA failed 介入：再試一輪 QA（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "running");
                group.InterventionReason = null;
                await taskRepo.SaveAsync(ct);
                {
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.FireStepsAsync(group, [new WorkflowStep(AgentNames.Qa)], ct);
                }
                break;

            case "qa_intervention_skip":
                // 略過進 Doc
                logger.LogInformation("QA failed 介入：略過進 Doc（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "running");
                group.InterventionReason = null;
                await taskRepo.SaveAsync(ct);
                {
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.FireStepsAsync(group, [new WorkflowStep(AgentNames.Doc)], ct);
                }
                break;

            case "qa_intervention_abort":
                logger.LogInformation("QA failed 介入：放棄任務（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "failed");
                await taskRepo.SaveAsync(ct);
                break;

            default:
                logger.LogWarning("Unknown qa_failed_intervention action: {A}", action);
                break;
        }
    }

    /// <summary>Stage 43-F：Sage escalate 路由（retry / skip / abort）。</summary>
    public async Task HandleSageEscalateAsync(string contextJson, string action, CancellationToken ct)
    {
        using var doc  = JsonDocument.Parse(contextJson);
        if (!doc.RootElement.TryGetProperty("groupId", out var g)
            || !Guid.TryParse(g.GetString(), out var groupId))
            return;

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group    = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null) return;

        switch (action)
        {
            case "sage_retry":
                // 重跑 Doc 階段
                logger.LogInformation("Sage escalate：重跑 Doc（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "running");
                group.InterventionReason = null;
                await taskRepo.SaveAsync(ct);
                {
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.FireStepsAsync(group, [new WorkflowStep(AgentNames.Doc)], ct);
                }
                break;

            case "sage_skip":
                // 略過歸檔，標完成（透過守門 method 確認）
                logger.LogInformation("Sage escalate：略過歸檔標完成（Group={Id}）", groupId);
                group.InterventionReason = null;
                await taskRepo.SaveAsync(ct);
                {
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.MarkGroupDoneOrInterventionAsync(group, taskRepo, ct);
                }
                break;

            case "sage_abort":
                logger.LogInformation("Sage escalate：保持 needs_intervention（Group={Id}）", groupId);
                // 保持 needs_intervention 狀態（不變）
                break;

            default:
                logger.LogWarning("Unknown sage_escalate action: {A}", action);
                break;
        }
    }

    /// <summary>
    /// Stage 46-FF 三十五：split_task_proposal BossInteraction 4 按鈕分派。
    /// - split_accept → BuildEpicSubTasksAsync（EpicChainService）
    /// - split_modify → 解析 responseContent 改寫的 phases JSON，失敗 fallback 到 split_reject
    /// - split_reject → 不拆，照舊 fire Dev_plan
    /// - split_abort  → mark cancelled
    /// </summary>
    public async Task HandleSplitTaskProposalAsync(
        string contextJson, string action, string? responseContent, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(contextJson);
        if (!doc.RootElement.TryGetProperty("groupId", out var g)
            || !Guid.TryParse(g.GetString(), out var groupId))
            return;
        var splitProposalJson = doc.RootElement.TryGetProperty("splitProposalJson", out var sp)
            ? sp.GetString() ?? ""
            : "";

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group    = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group is null)
        {
            logger.LogWarning("HandleSplitTaskProposal：找不到 TaskGroup ({Id})", groupId);
            return;
        }

        switch (action)
        {
            case "split_accept":
            {
                var proposal = DesignSplitProposalEvaluator.TryParseSplitProposal(splitProposalJson);
                if (proposal is null || !proposal.ShouldSplit || proposal.Phases is { Count: 0 })
                {
                    logger.LogWarning("split_accept：原始 splitProposalJson 解析失敗，fallback 到 split_reject");
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.FireStepsAsync(group, [new WorkflowStep("Dev_plan")], ct);
                    return;
                }
                var epicChain = scope.ServiceProvider.GetRequiredService<EpicChainService>();
                await epicChain.BuildEpicSubTasksAsync(groupId, proposal, ct);
                break;
            }
            case "split_modify":
            {
                // v1.1 Aria 回饋 #2：Christ 從 TextInputDialog 改的 phases JSON 不一定合 schema，需防呆
                SplitProposal? modified = null;
                try { modified = DesignSplitProposalEvaluator.TryParseSplitProposal(responseContent ?? ""); }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "split_modify：Christ 改寫的 phases JSON 解析失敗，fallback 到 split_reject");
                }

                if (modified is null || !modified.ShouldSplit || modified.Phases is { Count: 0 })
                {
                    logger.LogInformation("split_modify fallback to split_reject（解析失敗或內容無效）");
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.FireStepsAsync(group, [new WorkflowStep("Dev_plan")], ct);
                    return;
                }

                var epicChain = scope.ServiceProvider.GetRequiredService<EpicChainService>();
                await epicChain.BuildEpicSubTasksAsync(groupId, modified, ct);
                break;
            }
            case "split_reject":
                logger.LogInformation("split_reject：老闆選擇不拆，照舊 fire Dev_plan（Group={Id}）", groupId);
                {
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.FireStepsAsync(group, [new WorkflowStep("Dev_plan")], ct);
                }
                break;

            case "split_abort":
                logger.LogInformation("split_abort：老闆取消任務（Group={Id}）", groupId);
                taskRepo.UpdateGroupStatus(group, "cancelled");
                await taskRepo.SaveAsync(ct);
                break;

            default:
                logger.LogWarning("HandleSplitTaskProposal：未識別 action={Action}", action);
                break;
        }
    }
}
