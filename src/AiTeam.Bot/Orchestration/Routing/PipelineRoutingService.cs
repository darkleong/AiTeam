using System.Text.Json;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Orchestration.Routing;

/// <summary>
/// Stage 59：Pipeline framework path 接管 routing 集中管理（從 TaskGroupService F 區段拆出）。
///
/// 7 type-specific intervention HITL Pipeline 路由分支（對齊 Stage 55A kickoff/design 既有 pattern —
/// return true = Pipeline path 接管完成；false = 走 legacy fallback）：
///   ① dev_failed_intervention            (Stage 55B Session B)
///   ② qa_failed_intervention              (Stage 55B Session B)
///   ③ devplan_escalate                    (Stage 55B Session B)
///   ④ dev_plan_unable                     (Stage 55B Session B)
///   ⑤ split_task_proposal                 (Stage 55B Session B)
///   ⑥ reviewer_fix_loop_limit             (Stage 57)
///   ⑦ agent_api_failure_intervention      (Stage 58)
///
/// 不依賴其他子 service — 只透過 IServiceProvider scope resolve TaskRepository / WorkflowSettingsResolver /
/// FrameworkPipelineRouter（既有 Stage 53A+ Pipeline framework 注入鏈）。
/// </summary>
public class PipelineRoutingService(
    IServiceProvider serviceProvider,
    ILogger<PipelineRoutingService> logger)
{
    /// <summary>共用 Pipeline 路由前置檢查 — parse contextJson 取 groupId + 確認 group 存在 + Pipeline path active。</summary>
    private async Task<Data.TaskGroup?> TryGetPipelineGroupAsync(string contextJson, CancellationToken ct)
    {
        Guid groupId;
        try
        {
            using var doc = JsonDocument.Parse(contextJson);
            if (!doc.RootElement.TryGetProperty("groupId", out var g)
                || !Guid.TryParse(g.GetString(), out groupId))
                return null;
        }
        catch { return null; }

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var group = await taskRepo.GetGroupByIdAsync(groupId, ct);
        if (group?.PipelineFrameworkStateJson is null) return null;

        var workflowResolver = scope.ServiceProvider.GetRequiredService<WorkflowSettingsResolver>();
        if (!await workflowResolver.GetUseFrameworkPipelineAsync(ct)) return null;

        return group;
    }

    public async Task<bool> TryRoutePipelineDevInterventionAsync(string contextJson, string action, CancellationToken ct)
    {
        var group = await TryGetPipelineGroupAsync(contextJson, ct);
        if (group is null) return false;

        var actionShort = action.StartsWith("dev_intervention_") ? action.Substring("dev_intervention_".Length) : action;
        await using var scope = serviceProvider.CreateAsyncScope();
        var router = scope.ServiceProvider.GetRequiredService<FrameworkPipelineRouter>();
        logger.LogInformation("[Stage55B] ProcessBossResponseAsync dev_failed_intervention Pipeline 接管（Group={Id}, action={Action}）", group.Id, actionShort);
        await router.ResumeAfterDevInterventionAsync(group, actionShort, ct);
        return true;
    }

    public async Task<bool> TryRoutePipelineQaInterventionAsync(string contextJson, string action, CancellationToken ct)
    {
        var group = await TryGetPipelineGroupAsync(contextJson, ct);
        if (group is null) return false;

        var actionShort = action.StartsWith("qa_intervention_") ? action.Substring("qa_intervention_".Length) : action;
        await using var scope = serviceProvider.CreateAsyncScope();
        var router = scope.ServiceProvider.GetRequiredService<FrameworkPipelineRouter>();
        logger.LogInformation("[Stage55B] ProcessBossResponseAsync qa_failed_intervention Pipeline 接管（Group={Id}, action={Action}）", group.Id, actionShort);
        await router.ResumeAfterQaInterventionAsync(group, actionShort, ct);
        return true;
    }

    public async Task<bool> TryRoutePipelineDevPlanEscalateAsync(string contextJson, string action, CancellationToken ct)
    {
        var group = await TryGetPipelineGroupAsync(contextJson, ct);
        if (group is null) return false;

        // devplan_skip / devplan_abort → skip / abort
        var actionShort = action.StartsWith("devplan_") ? action.Substring("devplan_".Length) : action;
        await using var scope = serviceProvider.CreateAsyncScope();
        var router = scope.ServiceProvider.GetRequiredService<FrameworkPipelineRouter>();
        logger.LogInformation("[Stage55B] ProcessBossResponseAsync devplan_escalate Pipeline 接管（Group={Id}, action={Action}）", group.Id, actionShort);
        await router.ResumeAfterDevPlanEscalateAsync(group, actionShort, ct);
        return true;
    }

    public async Task<bool> TryRoutePipelineDevPlanUnableAsync(string contextJson, string action, CancellationToken ct)
    {
        var group = await TryGetPipelineGroupAsync(contextJson, ct);
        if (group is null) return false;

        // devplan_unable_skip / devplan_unable_abort → skip / abort
        var actionShort = action.StartsWith("devplan_unable_") ? action.Substring("devplan_unable_".Length) : action;
        await using var scope = serviceProvider.CreateAsyncScope();
        var router = scope.ServiceProvider.GetRequiredService<FrameworkPipelineRouter>();
        logger.LogInformation("[Stage55B] ProcessBossResponseAsync dev_plan_unable Pipeline 接管（Group={Id}, action={Action}）", group.Id, actionShort);
        await router.ResumeAfterDevPlanUnableAsync(group, actionShort, ct);
        return true;
    }

    public async Task<bool> TryRoutePipelineReviewerFixLoopLimitAsync(string contextJson, string action, CancellationToken ct)
    {
        var group = await TryGetPipelineGroupAsync(contextJson, ct);
        if (group is null) return false;

        // fix_loop_mark_done / fix_loop_skip_qa / fix_loop_abort → mark_done / skip_qa / abort
        var actionShort = action.StartsWith("fix_loop_") ? action.Substring("fix_loop_".Length) : action;
        await using var scope = serviceProvider.CreateAsyncScope();
        var router = scope.ServiceProvider.GetRequiredService<FrameworkPipelineRouter>();
        logger.LogInformation("[Stage57] ProcessBossResponseAsync reviewer_fix_loop_limit Pipeline 接管（Group={Id}, action={Action}）", group.Id, actionShort);
        await router.ResumeAfterReviewerFixLoopLimitAsync(group, actionShort, ct);
        return true;
    }

    /// <summary>Stage 58-FF 五十三：Agent API 失敗 Pipeline path 接管 routing（第 7 routing — 對齊 Stage 55B Session B / Stage 57 既有 TryRoute pattern）。
    /// 從 contextJson 取 agentName + action 去 api_failure_ 前綴 → 依 agentName dispatch 到 4 個 typed thin wrapper（路線 a：對齊 Stage 57 typed pattern 不動 ResumeWithResponseAsync 簽名）。</summary>
    public async Task<bool> TryRoutePipelineAgentApiFailureAsync(string contextJson, string action, CancellationToken ct)
    {
        var group = await TryGetPipelineGroupAsync(contextJson, ct);
        if (group is null) return false;

        // 從 contextJson 取 agentName（NotifyBossAgentApiFailureAsync 寫入時的 "agent" 欄位）
        string? agentName;
        try
        {
            using var doc = JsonDocument.Parse(contextJson);
            agentName = doc.RootElement.TryGetProperty("agent", out var a) ? a.GetString() : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Stage58] TryRoutePipelineAgentApiFailureAsync：parse contextJson 取 agent 失敗（Group={Id}），略過", group.Id);
            return false;
        }
        if (string.IsNullOrEmpty(agentName))
        {
            logger.LogWarning("[Stage58] TryRoutePipelineAgentApiFailureAsync：contextJson 無 agent 欄位（Group={Id}），略過", group.Id);
            return false;
        }

        // api_failure_continue / api_failure_retry / api_failure_abort → continue / retry / abort
        var actionShort = action.StartsWith("api_failure_") ? action.Substring("api_failure_".Length) : action;

        await using var scope = serviceProvider.CreateAsyncScope();
        var router = scope.ServiceProvider.GetRequiredService<FrameworkPipelineRouter>();
        logger.LogInformation("[Stage58] ProcessBossResponseAsync agent_api_failure_intervention Pipeline 接管（Group={Id}, agent={Agent}, action={Action}）",
            group.Id, agentName, actionShort);

        switch (agentName)
        {
            case "Dev":
                await router.ResumeAfterDevAgentApiFailureAsync(group, actionShort, ct);
                return true;
            case "Reviewer":
                await router.ResumeAfterReviewerAgentApiFailureAsync(group, actionShort, ct);
                return true;
            case "QA":
                await router.ResumeAfterQaAgentApiFailureAsync(group, actionShort, ct);
                return true;
            case "Doc":
                await router.ResumeAfterDocAgentApiFailureAsync(group, actionShort, ct);
                return true;
            default:
                logger.LogWarning("[Stage58] TryRoutePipelineAgentApiFailureAsync：未知 agentName={Agent}（Group={Id}），略過",
                    agentName, group.Id);
                return false;
        }
    }

    public async Task<bool> TryRoutePipelineSplitTaskProposalAsync(string contextJson, string action, string? responseContent, CancellationToken ct)
    {
        var group = await TryGetPipelineGroupAsync(contextJson, ct);
        if (group is null) return false;

        // split_accept / split_modify / split_reject / split_abort → accept / modify / reject / abort
        var actionShort = action.StartsWith("split_") ? action.Substring("split_".Length) : action;
        var modifyContent = action == "split_modify" ? responseContent : null;

        // accept path 需 splitProposalJson — 從 BossInteraction.contextJson 取
        string? splitProposalJson = null;
        if (actionShort == "accept")
        {
            try
            {
                using var doc = JsonDocument.Parse(contextJson);
                if (doc.RootElement.TryGetProperty("splitProposalJson", out var sp))
                    splitProposalJson = sp.GetString();
            }
            catch { /* ignore parse fail，HandleSplitTaskProposalResponseAsync accept path 會 fallback */ }
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var router = scope.ServiceProvider.GetRequiredService<FrameworkPipelineRouter>();
        logger.LogInformation("[Stage55B] ProcessBossResponseAsync split_task_proposal Pipeline 接管（Group={Id}, action={Action}）", group.Id, actionShort);
        await router.ResumeAfterSplitTaskProposalAsync(group, actionShort, modifyContent, splitProposalJson, ct);
        return true;
    }
}
