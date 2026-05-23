using System.Text.Json;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 84：Stage 81 動態 replan handling-after-signal logic 拆出（detection family 已 healthy 偏離搬 PetraTalentDispatchService）。
///
/// method 範圍（Roadmap子项 3 留下的 handling logic）：
/// - HandleReplanSignalAsync / HandleCapReachedAsync（caller 收 ReplanSignal 後路由 + cap intervention card）
/// - WaitForReplanConfirmationAsync（開 replan_confirm 卡）
/// - ResumeFromReplanConfirmationAsync + ResumeReplanApprove/EditOrRespond/Reject（4-way decision 路由）
/// - ContinueChainFromSubtaskAsync（replan approve 後重 dispatch chain / 用 talentDispatch.DispatchRemainingSubtasksAsync）
///
/// state：_roundRobinCounter（per-Scope 獨立 / 對應 ContinueChainFromSubtaskAsync 內 helper ref 呼叫）
///
/// 單向依賴：DynamicReplan → TalentDispatch（DispatchRemaining + InvokePetraReplan）+ DynamicReplan → GitFinalization（FinalizeGit）+ DynamicReplan → ContextBuilder（BuildSessionContext + BuildSummariesFromSessionMessages）/ 0 循環。
/// </summary>
public class PetraDynamicReplanService(
    AppDbContext db,
    PetraSessionRepository sessionRepo,
    MemoryRepository memoryRepo,
    InteractionService interactionService,
    WorkflowSettingsResolver workflowResolver,
    ITalentFactory talentFactory,
    PetraTalentDispatchService talentDispatch,
    PetraGitFinalizationService gitFinalization,
    PetraContextBuilder contextBuilder,
    ILogger<PetraDynamicReplanService> logger)
{
    // Stage 84：per-Scope 獨立 counter（與 TalentDispatch counter 分開但同 session lifecycle）
    private int _roundRobinCounter;

    /// <summary>Stage 81：plan_confirm + replan_confirm 共用 JSON options。</summary>
    private static readonly JsonSerializerOptions PlanConfirmJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // ========== Stage 81：動態 replan + HITL retry gate（LangGraph cycles 對齊 v1.1 議題 1+2+#3-#8 收口） ==========
    // Stage 84：DispatchOutcome / ReplanSignal / ReplanDecisionJson / ReplanConfirmContext 拆出 PetraOrchestratorDtos.cs

    // Stage 81 議題 #7：trigger 偵測 regex — schema 對齊 CLAUDE_Vera.md L113 `"critical":[{...}]` 非空 + CLAUDE_Quinn.md L75 `"status":"failed"`

    /// <summary>Stage 81：caller 從 DispatchTalentsAsync 收到 ReplanSignal 後分支 — replan_confirm 開卡 / cap_reached 開 intervention。</summary>
    internal async Task<PetraOrchestratorResult> HandleReplanSignalAsync(
        Guid sessionId, string taskInput, ReplanSignal signal, CancellationToken ct)
    {
        if (signal.Kind == "cap_reached_iter" || signal.Kind == "cap_reached_cost")
            return await HandleCapReachedAsync(sessionId, signal, ct);

        // 取 plan_confirm ContextJson 還原 currentSkill / currentTalent（render UI + Resume 用 / single source of truth）
        var planInteraction = await db.BossInteractions
            .Where(x => x.InteractionType == "plan_confirm" && x.ContextJson != null
                        && x.ContextJson.Contains(sessionId.ToString()))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        string currentSkill = "", currentTalentName = "";
        if (planInteraction?.ContextJson is not null)
        {
            try
            {
                var pctx = JsonSerializer.Deserialize<PlanConfirmContext>(planInteraction.ContextJson, PlanConfirmJsonOptions);
                var found = pctx?.Subtasks.FirstOrDefault(s => s.Id == signal.CurrentSubtaskId);
                if (found is not null)
                {
                    currentSkill = found.Skill;
                    currentTalentName = found.TalentName;
                }
            }
            catch (JsonException) { /* render fallback 空字串 */ }
        }

        await WaitForReplanConfirmationAsync(
            sessionId, taskInput, signal.CurrentSubtaskId, currentSkill, currentTalentName,
            signal.TriggerReason, signal.RetryInstruction, signal.LastOutputPreview,
            signal.CompletedCount, signal.ReplanIteration, ct);
        await db.SaveChangesAsync(ct);

        return PetraOrchestratorResult.Replanning(
            sessionId, signal.CurrentSubtaskId, signal.RetryInstruction, signal.TriggerReason);
    }

    /// <summary>Stage 81 場景 G + H：max iter / cost cap 達上限 → 開 intervention 卡 + 寫 task_memory + CancelAsync + Cancelled。</summary>
    internal async Task<PetraOrchestratorResult> HandleCapReachedAsync(
        Guid sessionId, ReplanSignal signal, CancellationToken ct)
    {
        var memoryContent = signal.Kind == "cap_reached_iter"
            ? $"max iterations N={signal.CapValueInt} reached"
            : $"cost cap ${signal.CapValueDecimal:F2} reached";

        try
        {
            await memoryRepo.UpsertTaskMemoryAsync(
                sessionId, projectId: null,
                key: "decision/replan-cap-reached",
                content: memoryContent,
                createdByTalent: "Petra",
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Stage 81 HandleCapReached：UpsertTaskMemoryAsync 失敗（容錯不擋）sessionId={SessionId}", sessionId);
        }

        // 開既有 intervention 卡（Christ 拍板介入 / NotifyActionsJson 「我知道了」ack 按鈕）
        _ = interactionService.CreateInteractionAsync(
            interactionType:      "intervention",
            title:                "動態 replan cap 達上限",
            description:          $"Stage 81：{memoryContent} — session cancelled / chain dispatch 0 啟動。",
            project:              null,
            agentName:            "Petra",
            availableActionsJson: InteractionService.NotifyActionsJson,
            contextJson:          $$"""{"sessionId":"{{sessionId}}","kind":"{{signal.Kind}}","subtaskId":{{signal.CurrentSubtaskId}}}""",
            systemNotes:          $"[Stage 81 replan-cap] sessionId={sessionId.ToString("N")[..8]} — kind={signal.Kind}");

        await sessionRepo.CancelAsync(sessionId, ct);
        await db.SaveChangesAsync(ct);

        return PetraOrchestratorResult.Cancelled(sessionId, Array.Empty<string>(),
            $"Stage 81 replan cap reached：{memoryContent} — session cancelled / intervention card opened.");
    }

    /// <summary>Stage 81：開 BossInteraction replan_confirm 卡 + PauseAsync（對齊 Stage 80 WaitForPlanConfirmationAsync pattern）。</summary>
    internal async Task WaitForReplanConfirmationAsync(
        Guid sessionId, string taskInput, int currentSubtaskId,
        string currentSkillName, string currentTalentName,
        string triggerReason, string retryInstruction, string lastOutputPreview,
        int completedCount, int replanIteration, CancellationToken ct)
    {
        var context = new ReplanConfirmContext(
            SessionId:         sessionId,
            TaskInput:         taskInput,
            CurrentSubtaskId:  currentSubtaskId,
            CurrentSkillName:  currentSkillName,
            CurrentTalentName: currentTalentName,
            TriggerReason:     triggerReason,
            RetryInstruction:  retryInstruction,
            LastOutputPreview: lastOutputPreview,
            CompletedCount:    completedCount,
            ReplanIteration:   replanIteration);

        var contextJson = JsonSerializer.Serialize(context, PlanConfirmJsonOptions);
        var taskFirstLine = (taskInput ?? "").Split('\n').FirstOrDefault() ?? "";
        var title = taskFirstLine.Length > 200 ? taskFirstLine[..200] + "…" : taskFirstLine;
        var description = $"Petra 建議 retry subtask #{currentSubtaskId}（{currentTalentName} / {currentSkillName}）— 觸發：{triggerReason}";
        var systemNotes = $"[Stage 81 動態 replan #{replanIteration + 1}] sessionId={sessionId.ToString("N")[..8]} — 4 decision：approve（同 subtask 重 dispatch）/ edit（修 retry instruction）/ reject（不採納 / 接受原結果繼續）/ respond（補充）";

        _ = interactionService.CreateInteractionAsync(
            interactionType:      "replan_confirm",
            title:                title,
            description:          description,
            project:              null,
            agentName:            "Petra",
            availableActionsJson: InteractionService.ReplanConfirmActionsJson,
            contextJson:          contextJson,
            systemNotes:          systemNotes);

        await sessionRepo.PauseAsync(sessionId, ct);
    }

    /// <summary>Stage 81：Petra LLM call 給 retry instruction（W8 紀律 — 不回新 plan 結構）。
    /// 容錯：JSON parse 失敗 / null shouldReplan → return null caller fallback 不 fire replan（保 chain 不中斷）。</summary>
    /// <summary>Stage 81：HITL replan_confirm 4 decision pattern resume — PlanConfirmationProcessor 看到 responded 拉起 fire。
    /// approve → ContinueChainFromSubtaskAsync 從 currentSubtaskId 重 dispatch with retry instruction（LangGraph cycles）
    /// edit / respond → 重 InvokePetraReplanAsync 含 override context → 新 retry instruction → 開新 replan_confirm 卡（loop）
    /// reject → ContinueChainFromSubtaskAsync 從 currentSubtaskId+1 繼續（接受原 output / 不 cancel session / 議題 1 修法 v1.1）。</summary>
    public virtual async Task<PetraOrchestratorResult> ResumeFromReplanConfirmationAsync(
        Guid sessionId, string decision, string? contextOverride, CancellationToken ct = default)
    {
        var session = await sessionRepo.GetWithMessagesAsync(sessionId, ct);
        if (session is null)
        {
            logger.LogWarning("ResumeFromReplanConfirmationAsync 找不到 sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "session 不存在");
        }

        var replanInteraction = await db.BossInteractions
            .Where(x => x.InteractionType == "replan_confirm" && x.ContextJson != null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.ContextJson!.Contains(sessionId.ToString()), ct);
        if (replanInteraction?.ContextJson is null)
        {
            logger.LogWarning("ResumeFromReplanConfirmationAsync 找不到 replan_confirm ContextJson sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "replan_confirm ContextJson 不存在");
        }

        ReplanConfirmContext? ctxRecord;
        try
        {
            ctxRecord = JsonSerializer.Deserialize<ReplanConfirmContext>(replanInteraction.ContextJson, PlanConfirmJsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "ResumeFromReplanConfirmationAsync ContextJson 解析失敗 sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), $"ContextJson parse failed: {ex.Message}");
        }
        if (ctxRecord is null)
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "replan_confirm ContextJson deserialize null");

        logger.LogInformation(
            "Stage 81：HITL replan_confirm resume decision={Decision} sessionId={SessionId} currentSubtaskId={SubtaskId} iter={Iter}",
            decision, sessionId, ctxRecord.CurrentSubtaskId, ctxRecord.ReplanIteration);

        return decision switch
        {
            "approve" => await ResumeReplanApproveAsync(session, ctxRecord, ct),
            "edit" or "respond"
                      => await ResumeReplanEditOrRespondAsync(session, ctxRecord, decision, contextOverride, ct),
            "reject"  => await ResumeReplanRejectAsync(session, ctxRecord, ct),
            _         => PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), $"未知 decision={decision}"),
        };
    }

    /// <summary>Stage 81 場景 C：approve = LangGraph cycles 同 subtask 重 dispatch with retry instruction（不從頭跑）。</summary>
    internal async Task<PetraOrchestratorResult> ResumeReplanApproveAsync(
        PetraSession session, ReplanConfirmContext ctxRecord, CancellationToken ct)
    {
        await sessionRepo.IncrementReplanIterationAsync(session.Id, ct);
        await db.SaveChangesAsync(ct);

        // 從 currentSubtaskId 起重 dispatch（含 retry instruction prepend）— 對齊 LangGraph cycles
        return await ContinueChainFromSubtaskAsync(
            session, ctxRecord.CurrentSubtaskId, ctxRecord.RetryInstruction, ct);
    }

    /// <summary>Stage 81 場景 D + F：edit / respond = 重 InvokePetraReplanAsync 含 override → 新 retry instruction → 開新 replan_confirm 卡。</summary>
    internal async Task<PetraOrchestratorResult> ResumeReplanEditOrRespondAsync(
        PetraSession session, ReplanConfirmContext ctxRecord, string decision,
        string? contextOverride, CancellationToken ct)
    {
        await sessionRepo.IncrementReplanIterationAsync(session.Id, ct);
        await db.SaveChangesAsync(ct);

        var overrideText = string.IsNullOrWhiteSpace(contextOverride) ? "" : contextOverride;
        await sessionRepo.AppendMessageAsync(session.Id, "user",
            $"[Stage 81 replan_{decision}] {overrideText}", ct: ct);
        await db.SaveChangesAsync(ct);

        // 取 plan_confirm ContextJson 還原 currentSubtask 細節（single source of truth）
        var planInteraction = await db.BossInteractions
            .Where(x => x.InteractionType == "plan_confirm" && x.ContextJson != null
                        && x.ContextJson.Contains(session.Id.ToString()))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (planInteraction?.ContextJson is null)
            return PetraOrchestratorResult.Failure(session.Id, Array.Empty<string>(),
                "edit/respond 路徑找不到 plan_confirm ContextJson（補強 #A 紀律 — single source of truth）");

        var planContext = JsonSerializer.Deserialize<PlanConfirmContext>(planInteraction.ContextJson, PlanConfirmJsonOptions);
        var currentSub = planContext?.Subtasks.FirstOrDefault(s => s.Id == ctxRecord.CurrentSubtaskId);
        if (planContext is null || currentSub is null)
            return PetraOrchestratorResult.Failure(session.Id, Array.Empty<string>(),
                $"plan_confirm ContextJson subtask #{ctxRecord.CurrentSubtaskId} 不存在");

        var subtask = new Subtask(currentSub.Id, currentSub.Skill, currentSub.Description, currentSub.NeedsImageContext);
        var summaries = await contextBuilder.BuildSummariesFromSessionMessagesAsync(session.Id, ct);

        // 合 retry instruction + Christ override → 新 trigger reason
        var mergedTriggerReason = $"{ctxRecord.TriggerReason} + christ_{decision}";
        var augmentedLastOutput = $"{ctxRecord.LastOutputPreview}\n\n[Stage 81 retry instruction 既有]: {ctxRecord.RetryInstruction}\n[Stage 81 Christ {decision.ToUpperInvariant()}]: {overrideText}";

        var ctx = contextBuilder.BuildSessionContext(session.TaskGroupId) with { SessionId = session.Id };
        var newDecision = await talentDispatch.InvokePetraReplanAsync(
            ctxRecord.TaskInput, summaries, subtask,
            mergedTriggerReason, augmentedLastOutput, ctx, ct);

        if (newDecision is null || !newDecision.ShouldReplan)
        {
            logger.LogInformation(
                "[Stage 81] edit/respond Petra 判斷新 plan 不需 replan sessionId={SessionId} — fallback 接受原 output 繼續",
                session.Id);
            return await ContinueChainFromSubtaskAsync(session, ctxRecord.CurrentSubtaskId + 1, null, ct);
        }

        var newPreview = augmentedLastOutput.Length > 500 ? augmentedLastOutput[..500] + "..." : augmentedLastOutput;
        var (newIter, _) = await sessionRepo.GetReplanStateAsync(session.Id, ct);

        await WaitForReplanConfirmationAsync(
            session.Id, ctxRecord.TaskInput, ctxRecord.CurrentSubtaskId,
            ctxRecord.CurrentSkillName, ctxRecord.CurrentTalentName,
            mergedTriggerReason, newDecision.RetryInstruction, newPreview,
            ctxRecord.CompletedCount, newIter, ct);
        await db.SaveChangesAsync(ct);

        return PetraOrchestratorResult.Replanning(
            session.Id, ctxRecord.CurrentSubtaskId, newDecision.RetryInstruction, mergedTriggerReason);
    }

    /// <summary>Stage 81 場景 E：reject = 接受原 worker output / 繼續 chain dispatch 下個 subtask（不 cancel session / iter 不變 / v1.1 議題 1）。</summary>
    internal async Task<PetraOrchestratorResult> ResumeReplanRejectAsync(
        PetraSession session, ReplanConfirmContext ctxRecord, CancellationToken ct)
    {
        // 不 increment iter — reject 不算 replan 輪數（議題 1 v1.1 紀律）
        await sessionRepo.AppendMessageAsync(session.Id, "user",
            $"[Stage 81 replan_reject] 不採納 Petra retry 建議 / 接受原 output 繼續 subtaskId={ctxRecord.CurrentSubtaskId}", ct: ct);
        await db.SaveChangesAsync(ct);

        return await ContinueChainFromSubtaskAsync(session, ctxRecord.CurrentSubtaskId + 1, null, ct);
    }

    /// <summary>Stage 81：從 plan_confirm ContextJson 還原 plan + talent picks → 從 startFromSubtaskId 起繼續 chain dispatch。
    /// 補強 #A 紀律：plan_confirm ContextJson 是 single source of truth — 不另存 plan 在 replan_confirm ContextJson 避重複。</summary>
    internal async Task<PetraOrchestratorResult> ContinueChainFromSubtaskAsync(
        PetraSession session, int startFromSubtaskId, string? retryInstructionForFirst, CancellationToken ct)
    {
        var planInteraction = await db.BossInteractions
            .Where(x => x.InteractionType == "plan_confirm" && x.ContextJson != null
                        && x.ContextJson.Contains(session.Id.ToString()))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (planInteraction?.ContextJson is null)
            return PetraOrchestratorResult.Failure(session.Id, Array.Empty<string>(),
                "ContinueChainFromSubtaskAsync：找不到 plan_confirm ContextJson（補強 #A 紀律違反 — UseDynamicReplanning 應依賴 UseHITLPlanConfirmation）");

        PlanConfirmContext? planContext;
        try { planContext = JsonSerializer.Deserialize<PlanConfirmContext>(planInteraction.ContextJson, PlanConfirmJsonOptions); }
        catch (JsonException ex) { return PetraOrchestratorResult.Failure(session.Id, Array.Empty<string>(), $"ContextJson parse failed: {ex.Message}"); }
        if (planContext is null)
            return PetraOrchestratorResult.Failure(session.Id, Array.Empty<string>(), "plan_confirm ContextJson deserialize null");

        // session 從 paused 改 running
        var sessionRow = await db.PetraSessions.FirstOrDefaultAsync(x => x.Id == session.Id, ct);
        if (sessionRow is not null)
        {
            sessionRow.Status = "running";
            sessionRow.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        // 還原 SubtaskPlan + Talents
        var allSubtasks = planContext.Subtasks
            .Select(s => new Subtask(s.Id, s.Skill, s.Description, s.NeedsImageContext))
            .ToList();
        var allDeps = planContext.Dependencies
            .Select(d => new DependencyEdge(d.From, d.To,
                string.Equals(d.Type, "nested", StringComparison.OrdinalIgnoreCase) ? DependencyType.Nested : DependencyType.Sequential))
            .ToList();
        var fullPlan = new SubtaskPlan(allSubtasks, allDeps);

        var talentsList = await talentFactory.GetAllAsync(ct);
        var talentPicks = new List<ITalent>(allSubtasks.Count);
        foreach (var s in planContext.Subtasks)
        {
            var pick = talentsList.FirstOrDefault(t => string.Equals(t.Name, s.TalentName, StringComparison.OrdinalIgnoreCase))
                       ?? PetraTalentLookupHelper.FindTalentForSkill(s.Skill, talentsList, ref _roundRobinCounter);
            if (pick is null)
                return PetraOrchestratorResult.Failure(session.Id, planContext.Subtasks.Select(x => x.Skill).ToList(),
                    $"ContinueChain：找不到 Talent={s.TalentName}/Skill={s.Skill}");
            talentPicks.Add(pick);
        }

        // 過濾剩餘 subtasks
        var remainingSubtasks = allSubtasks.Where(s => s.Id >= startFromSubtaskId).ToList();
        var ctx = contextBuilder.BuildSessionContext(session.TaskGroupId) with { SessionId = session.Id };

        if (remainingSubtasks.Count == 0)
        {
            // 全部已完成 → 直接 finalize
            logger.LogInformation(
                "[Stage 81] ContinueChain 0 剩餘 subtask sessionId={SessionId} → finalize",
                session.Id);
            var prUrl = await gitFinalization.FinalizeGitAsync(ctx, planContext.TaskInput,
                allSubtasks.Select(s => s.SkillName).ToList(),
                planContext.TalentNames, ct);
            await sessionRepo.CompleteAsync(session.Id, ct);
            await db.SaveChangesAsync(ct);
            return PetraOrchestratorResult.Done(session.Id,
                allSubtasks.Select(s => s.SkillName).ToList(),
                $"Stage 81 ContinueChain：0 剩餘 subtask / 完成 chain" + (prUrl is null ? "。" : $" + PR {prUrl}。"));
        }

        // 還原 summaries from PetraSessionMessages tool rows
        var existingSummaries = await contextBuilder.BuildSummariesFromSessionMessagesAsync(session.Id, ct);

        // Stage 84：DispatchRemainingSubtasksAsync 改回 DispatchOutcome / caller 負責 signal handling + finalize（解循環）
        DispatchOutcome outcome;
        try
        {
            outcome = await talentDispatch.DispatchRemainingSubtasksAsync(
                ctx, planContext.TaskInput, fullPlan, talentPicks, existingSummaries,
                remainingSubtasks, retryInstructionForFirst, planContext.TalentNames, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PetraOrchestratorResult.Failure(session.Id, Array.Empty<string>(), ex.Message);
        }

        if (outcome.Replan is { } signal)
        {
            return await HandleReplanSignalAsync(session.Id, planContext.TaskInput, signal, ct);
        }

        // 全部完成 → finalize
        var capsRemaining = fullPlan.Subtasks.Select(s => s.SkillName).ToList();
        var prUrlRemaining = await gitFinalization.FinalizeGitAsync(ctx, planContext.TaskInput, capsRemaining, planContext.TalentNames, ct);
        await sessionRepo.CompleteAsync(session.Id, ct);
        await db.SaveChangesAsync(ct);
        var summaryRemaining = $"Stage 81 ContinueChain 完成 {planContext.TalentNames.Count} dispatch"
            + (prUrlRemaining is null ? "。" : $" + PR {prUrlRemaining}。");
        return PetraOrchestratorResult.Done(session.Id, capsRemaining, summaryRemaining);
    }
}
