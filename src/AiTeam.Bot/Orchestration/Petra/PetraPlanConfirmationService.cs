using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 84：Stage 80 HITL plan_confirm 4-way 分支路由 + DispatchAndFinalize approve path 拆出（對齊 Roadmap子项 2）。
///
/// method 範圍：
/// - WaitForPlanConfirmationAsync（開 plan_confirm 卡 + pause session）
/// - ResumeFromPlanConfirmationAsync + ResumeApprove/EditOrRespond/Reject（4-way 分支）
/// - DispatchAndFinalizeAsync（approve path 走 dispatch + replan signal handling + git finalize）
///
/// 單向依賴：PlanConfirmation → TalentDispatch + DynamicReplan + GitFinalization + ContextBuilder / 0 反向。
/// </summary>
public class PetraPlanConfirmationService(
    AppDbContext db,
    PetraSessionRepository sessionRepo,
    InteractionService interactionService,
    PetraTalentDispatchService talentDispatch,
    PetraDynamicReplanService dynamicReplan,
    PetraGitFinalizationService gitFinalization,
    PetraContextBuilder contextBuilder,
    WorkflowSettingsResolver workflowResolver,
    ITalentFactory talentFactory,
    MemoryRepository memoryRepo,
    ILogger<PetraPlanConfirmationService> logger)
{
    // Stage 84：local _roundRobinCounter（per-Scope / 對應 ResumeApproveAsync fallback lookup 用）
    private int _roundRobinCounter;

    // Stage 80: PlanConfirmContext 解析 JSON options 暫留（DispatchAndFinalize 內 ContextJson 解析）
    // 注意：與 PetraTalentDispatchService.PlanConfirmJsonOptions 並存（pure refactor 紀律 / 不抽 Commons / cross-file static field 維護成本低）

    private static readonly JsonSerializerOptions PlanConfirmJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Stage 80：開 BossInteraction plan_confirm 卡 + pause session（chain dispatch 0 啟動 / 等 Christ 4 decision 拍板）。
    /// ContextJson 含 SubtaskPlan + talent picks + sessionId + taskInput（resume edit/respond path 重 decide 用）。
    /// 對齊 InteractionService.CreateInteractionAsync 既有 pattern — fire-and-forget 失敗只 log（plan_confirm 漏卡也不擋 PetraDispatchWorker row 完成）。</summary>
    internal async Task WaitForPlanConfirmationAsync(
        Guid sessionId,
        string taskInput,
        SubtaskPlan plan,
        IReadOnlyList<ITalent> talentPicks,
        IReadOnlyList<string> decidedCapabilities,
        CancellationToken ct)
    {
        var context = new PlanConfirmContext(
            SessionId: sessionId,
            TaskInput: taskInput,
            Subtasks: plan.Subtasks
                .Select((s, i) => new PlanConfirmSubtask(s.Id, s.SkillName, s.Description, talentPicks[i].Name, s.NeedsImageContext))
                .ToList(),
            Dependencies: plan.Dependencies
                .Select(d => new PlanConfirmDependency(d.FromId, d.ToId, d.Type.ToString().ToLowerInvariant()))
                .ToList(),
            TalentNames: talentPicks.Select(t => t.Name).ToList());

        var contextJson = JsonSerializer.Serialize(context, PlanConfirmJsonOptions);

        var taskFirstLine = (taskInput ?? "").Split('\n').FirstOrDefault() ?? "";
        var title = taskFirstLine.Length > 80 ? taskFirstLine[..80] + "…" : taskFirstLine;
        var description = $"Petra 拆 {plan.Subtasks.Count} subtask（{string.Join(" → ", talentPicks.Select(t => t.Name))}）— 等 Christ 4 decision 拍板。";
        var systemNotes = $"[Stage 80 HITL] sessionId={sessionId.ToString("N")[..8]} — 4 decision pattern：approve（核准）/ edit（修改）/ reject（拒絕）/ respond（補充）。";

        _ = interactionService.CreateInteractionAsync(
            interactionType:      "plan_confirm",
            title:                title,
            description:          description,
            project:              null,
            agentName:            "Petra",
            availableActionsJson: InteractionService.PlanConfirmActionsJson,
            contextJson:          contextJson,
            systemNotes:          systemNotes);

        await sessionRepo.PauseAsync(sessionId, ct);
    }

    /// <summary>Stage 80：HITL plan_confirm 4 decision pattern resume — PlanConfirmationProcessor 看到 responded 拉起 fire。
    /// approve → 沿用既有 SubtaskPlan + 重建 talentPicks → DispatchAndFinalize（chain dispatch + FinalizeGit + CompleteAsync）
    /// edit / respond → 重 DecideTalentsWithPlanAsync（taskInput + override content prefix） → 新 plan_confirm 卡（loop until approve / reject）
    /// reject → 寫 task_memory `decision/plan-rejected` + sessionRepo.CancelAsync / chain dispatch 0 啟動。</summary>
    public virtual async Task<PetraOrchestratorResult> ResumeFromPlanConfirmationAsync(
        Guid sessionId,
        string decision,
        string? contextOverride,
        CancellationToken ct = default)
    {
        var session = await sessionRepo.GetWithMessagesAsync(sessionId, ct);
        if (session is null)
        {
            logger.LogWarning("ResumeFromPlanConfirmationAsync 找不到 sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "session 不存在");
        }

        // 找最新 plan_confirm BossInteraction 拿 ContextJson（SubtaskPlan + talent names 還原 dispatch 用）
        var planInteraction = await db.BossInteractions
            .Where(x => x.InteractionType == "plan_confirm" && x.ContextJson != null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.ContextJson!.Contains(sessionId.ToString()), ct);

        if (planInteraction?.ContextJson is null)
        {
            logger.LogWarning("ResumeFromPlanConfirmationAsync 找不到 plan_confirm ContextJson sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "plan_confirm ContextJson 不存在");
        }

        PlanConfirmContext? planContext;
        try
        {
            planContext = JsonSerializer.Deserialize<PlanConfirmContext>(planInteraction.ContextJson, PlanConfirmJsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "ResumeFromPlanConfirmationAsync ContextJson 解析失敗 sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), $"ContextJson parse failed: {ex.Message}");
        }
        if (planContext is null)
        {
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "ContextJson deserialize null");
        }

        logger.LogInformation(
            "Stage 80：HITL plan_confirm resume decision={Decision} sessionId={SessionId} subtasks={Count} contentLen={Len}",
            decision, sessionId, planContext.Subtasks.Count, contextOverride?.Length ?? 0);

        switch (decision)
        {
            case "approve":
                return await ResumeApproveAsync(session, planContext, ct);

            case "edit":
            case "respond":
                return await ResumeEditOrRespondAsync(session, planContext, decision, contextOverride, ct);

            case "reject":
                return await ResumeRejectAsync(session, planContext, contextOverride, ct);

            default:
                logger.LogWarning("ResumeFromPlanConfirmationAsync 未知 decision={Decision} sessionId={SessionId}", decision, sessionId);
                return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), $"未知 decision={decision}");
        }
    }

    /// <summary>Stage 80：approve path — 沿用 plan_confirm 既存 SubtaskPlan + 重建 talentPicks + DispatchAndFinalize。
    /// 對齊 StartAsync v5.5 path L131-169 真實 dispatch 路徑：DispatchTalentsAsync + FinalizeGitAsync + sessionRepo.CompleteAsync。</summary>
    internal async Task<PetraOrchestratorResult> ResumeApproveAsync(
        PetraSession session,
        PlanConfirmContext planContext,
        CancellationToken ct)
    {
        // 還原 SubtaskPlan + Talents（從 ContextJson 重建 / 不重 call Petra LLM）
        var subtasks = planContext.Subtasks
            .Select(s => new Subtask(s.Id, s.Skill, s.Description, s.NeedsImageContext))
            .ToList();
        var deps = planContext.Dependencies
            .Select(d => new DependencyEdge(
                d.From, d.To,
                string.Equals(d.Type, "nested", StringComparison.OrdinalIgnoreCase) ? DependencyType.Nested : DependencyType.Sequential))
            .ToList();
        var plan = new SubtaskPlan(subtasks, deps);

        var talentsList = await talentFactory.GetAllAsync(ct);
        var talentPicks = new List<ITalent>(planContext.Subtasks.Count);
        foreach (var s in planContext.Subtasks)
        {
            var pick = talentsList.FirstOrDefault(t => string.Equals(t.Name, s.TalentName, StringComparison.OrdinalIgnoreCase))
                       ?? PetraTalentLookupHelper.FindTalentForSkill(s.Skill, talentsList, ref _roundRobinCounter);
            if (pick is null)
            {
                logger.LogError(
                    "Stage 80 approve：找不到 Talent={TalentName} 也找不到 Skill={Skill} 的 fallback Talent — abort sessionId={SessionId}",
                    s.TalentName, s.Skill, session.Id);
                await sessionRepo.EscalateAsync(session.Id, CancellationToken.None);
                await db.SaveChangesAsync(CancellationToken.None);
                return PetraOrchestratorResult.Failure(session.Id, planContext.Subtasks.Select(x => x.Skill).ToList(),
                    $"approve 路徑找不到 Talent={s.TalentName}/Skill={s.Skill}");
            }
            talentPicks.Add(pick);
        }

        // session 從 paused 改回 running（PetraSessionRecoveryService 對齊 / 結束時自然 CompleteAsync）
        var sessionRow = await db.PetraSessions.FirstOrDefaultAsync(x => x.Id == session.Id, ct);
        if (sessionRow is not null)
        {
            sessionRow.Status = "running";
            sessionRow.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var ctx = contextBuilder.BuildSessionContext(session.TaskGroupId) with { SessionId = session.Id };
        return await DispatchAndFinalizeAsync(
            ctx, planContext.TaskInput, plan, talentPicks, planContext.TalentNames, images: null, ct);
    }

    /// <summary>Stage 80：edit / respond path — 用 override content 重 DecideTalentsWithPlanAsync + 開新 plan_confirm 卡（loop until approve / reject）。
    /// 同 session 內 redecide（不開新 PetraSession）— append `[Christ EDIT]: content` 進 session messages 維持 audit trail。</summary>
    internal async Task<PetraOrchestratorResult> ResumeEditOrRespondAsync(
        PetraSession session,
        PlanConfirmContext planContext,
        string decision,
        string? contextOverride,
        CancellationToken ct)
    {
        var contentPart = string.IsNullOrWhiteSpace(contextOverride) ? "" : $"\n\n[Christ {decision.ToUpperInvariant()}]: {contextOverride}";
        var mergedInput = planContext.TaskInput + contentPart;

        await sessionRepo.AppendMessageAsync(session.Id, "user", $"[plan_confirm {decision}] {contextOverride ?? ""}", ct: ct);
        await db.SaveChangesAsync(ct);

        // session 從 paused 改回 running（redecide 進行中）
        var sessionRow = await db.PetraSessions.FirstOrDefaultAsync(x => x.Id == session.Id, ct);
        if (sessionRow is not null)
        {
            sessionRow.Status = "running";
            sessionRow.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var talentsList = await talentFactory.GetAllAsync(ct);
        var ctx = contextBuilder.BuildSessionContext(session.TaskGroupId) with { SessionId = session.Id };

        var (plan, talentPicks) = await talentDispatch.DecideTalentsWithPlanAsync(mergedInput, talentsList, ctx, ct, images: null);
        if (talentPicks.Count == 0)
        {
            logger.LogWarning("Stage 80 redecide 回空序列 sessionId={SessionId} decision={Decision}", session.Id, decision);
            await sessionRepo.CompleteAsync(session.Id, ct);
            await db.SaveChangesAsync(ct);
            return PetraOrchestratorResult.Empty(session.Id);
        }
        var decidedCapabilities = plan.Subtasks.Select(s => s.SkillName).ToList();

        // 重開 plan_confirm 卡（loop until approve / reject）
        await WaitForPlanConfirmationAsync(session.Id, mergedInput, plan, talentPicks, decidedCapabilities, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Stage 80：HITL plan_confirm redecide 完成 sessionId={SessionId} decision={Decision} subtasks={Count}",
            session.Id, decision, plan.Subtasks.Count);

        return PetraOrchestratorResult.Paused(session.Id, decidedCapabilities,
            $"Petra 已依 Christ {decision} 重拆 {plan.Subtasks.Count} subtask 等再次拍板。");
    }

    /// <summary>Stage 80：reject path — 寫 task_memory `decision/plan-rejected` + sessionRepo.CancelAsync / chain dispatch 0 啟動。</summary>
    internal async Task<PetraOrchestratorResult> ResumeRejectAsync(
        PetraSession session,
        PlanConfirmContext planContext,
        string? contextOverride,
        CancellationToken ct)
    {
        var note = string.IsNullOrWhiteSpace(contextOverride)
            ? "Christ rejected plan via HITL plan_confirm 閘門"
            : $"Christ rejected plan: {contextOverride}";
        try
        {
            await memoryRepo.UpsertTaskMemoryAsync(
                session.Id, projectId: null,
                key: "decision/plan-rejected",
                content: note,
                createdByTalent: "Petra",
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Stage 80 reject：UpsertTaskMemoryAsync 失敗（容錯不擋 reject 完成）sessionId={SessionId}", session.Id);
        }

        await sessionRepo.CancelAsync(session.Id, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Stage 80：HITL plan_confirm reject sessionId={SessionId} subtasks={Count}",
            session.Id, planContext.Subtasks.Count);

        // Stage 81 子項 10（議題 #3 + #8 命名語意收口）：Cancelled 工廠取代 Done — DispatchedWorkerCount=0 真實對齊 reject path 0 chain dispatch。
        return PetraOrchestratorResult.Cancelled(session.Id, planContext.Subtasks.Select(s => s.Skill).ToList(),
            $"Petra plan 被 Christ HITL reject — task_memory `decision/plan-rejected` 已寫入 / session cancelled。");
    }

    /// <summary>Stage 80：抽出 StartAsync v5.5 path 既有 dispatch + finalize 收尾段 — 供 approve resume + 既有 StartAsync 共用。
    /// 對齊既有 L131-169 邏輯：建 talentAgents + talentNameToIdMap + DispatchTalentsAsync + FinalizeGitAsync + sessionRepo.CompleteAsync。</summary>
    internal async Task<PetraOrchestratorResult> DispatchAndFinalizeAsync(
        PetraSessionContext ctx,
        string taskInput,
        SubtaskPlan plan,
        IReadOnlyList<ITalent> talentPicks,
        IReadOnlyList<string> dispatchNames,
        IReadOnlyList<ImageAttachment>? images,
        CancellationToken ct)
    {
        try
        {
            var talentAgents = new AIAgent[talentPicks.Count];
            for (var i = 0; i < talentPicks.Count; i++)
            {
                talentAgents[i] = talentPicks[i].CreateAgent(ctx, plan.Subtasks[i].SkillName);
            }

            var useV5Memory = await workflowResolver.GetUseV5MemoryAsync(ct);
            var names = talentPicks.Select(t => t.Name).Distinct().ToList();
            var talentNameToIdMap = await db.Talents
                .Where(t => names.Contains(t.Name) && t.ProjectId == null)
                .ToDictionaryAsync(t => t.Name, t => t.Id, ct);

            var outcome = await talentDispatch.DispatchTalentsAsync(
                ctx.SessionId, taskInput, plan, talentPicks, talentAgents,
                useV5Memory, talentNameToIdMap, images, ctx, ct);
            await db.SaveChangesAsync(ct);

            // Stage 81：replan signal handling（DispatchAndFinalize 也走同 flow / approve resume 期間可能再觸發 replan loop）
            if (outcome.Replan is { } signal)
            {
                return await dynamicReplan.HandleReplanSignalAsync(ctx.SessionId, taskInput, signal, ct);
            }

            var decidedCapabilities = plan.Subtasks.Select(s => s.SkillName).ToList();
            var prUrl = await gitFinalization.FinalizeGitAsync(ctx, taskInput, decidedCapabilities, dispatchNames, ct);

            await sessionRepo.CompleteAsync(ctx.SessionId, ct);
            await db.SaveChangesAsync(ct);

            var summary = $"Petra 完成 {dispatchNames.Count} dispatch（{string.Join(" → ", dispatchNames)}）"
                + (prUrl is null ? "。" : $" + PR {prUrl}。");
            return PetraOrchestratorResult.Done(ctx.SessionId, decidedCapabilities, summary);
        }
        catch (OperationCanceledException)
        {
            await sessionRepo.EscalateAsync(ctx.SessionId, CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DispatchAndFinalizeAsync 失敗 sessionId={SessionId}", ctx.SessionId);
            await sessionRepo.EscalateAsync(ctx.SessionId, CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
            return PetraOrchestratorResult.Failure(ctx.SessionId, Array.Empty<string>(), ex.Message);
        }
    }
}
