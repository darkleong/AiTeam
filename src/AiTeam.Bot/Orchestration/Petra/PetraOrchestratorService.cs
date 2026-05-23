using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：Petra Orchestrator — v5.5 動態架構主入口。
/// Stage 84：怪物大檔拆解 — 主入口 ≤ 250 行（從 2266 瘦身 ≥ 89%）。
///
/// 5 sub-service 注入 + 4 caller 全 lazy resolve 0 改動：
/// - PetraGitFinalizationService：FinalizeGitAsync（git commit/push/PR）
/// - PetraContextBuilder：BuildSessionContext / BuildResumeInput / BuildPetraSystemPrompt / BuildMemoryContext（跨 service 共用）
/// - PetraTalentDispatchService：DecideTalents / DispatchTalents / detection family（CheckReplan + InvokePetraReplan / 健康偏離 Roadmap 子项 3 移到 dispatcher）
/// - PetraDynamicReplanService：HandleReplanSignal / 4-way replan_confirm Resume / ContinueChainFromSubtask
/// - PetraPlanConfirmationService：WaitForPlanConfirmation / 4-way plan_confirm Resume / DispatchAndFinalize
///
/// PetraTalentLookupHelper static helper：FindTalentForSkill（解 TalentDispatch ↔ DynamicReplan ctor 循環依賴）
/// </summary>
public class PetraOrchestratorService(
    ITalentFactory talentFactory,
    WorkflowSettingsResolver workflowResolver,
    PetraSessionRepository sessionRepo,
    AppDbContext db,
    PetraGitFinalizationService gitFinalization,    // Stage 84：FinalizeGitAsync 拆出 service
    PetraContextBuilder contextBuilder,             // Stage 84：BuildSessionContext / BuildResumeInput / BuildPetraSystemPrompt / BuildMemoryContext 拆出 Commons
    PetraTalentDispatchService talentDispatch,      // Stage 84：DecideTalents / DispatchTalents / detection family 拆出
    PetraDynamicReplanService dynamicReplan,        // Stage 84：HandleReplanSignal / Resume*Replan / ContinueChainFromSubtask 拆出
    PetraPlanConfirmationService planConfirmation,  // Stage 84：plan_confirm 4-way 拆出
    ILogger<PetraOrchestratorService> logger)
{

    /// <summary>啟動新 session — Petra 動態決策 + BuildSequential dispatch。taskGroupId 可為 null（spike forward path 無 TaskGroup）。
    /// Stage 77：標 virtual 供 xUnit T6 test-only subclass override stub（Stage 76 retry path 搬遷整合 regression test cover）。
    /// Stage 79：v5.5 image flow 補完 — 加 images param（PetraDispatchWorker 從 PetraInbox.Attachments 反序列化後傳入）。
    /// images null / 0 count → 0 image propagation 純文字 dispatch 對齊 Trial_v22 baseline 0 行為改變。</summary>
    public virtual async Task<PetraOrchestratorResult> StartAsync(
        Guid? taskGroupId,
        string taskInput,
        CancellationToken ct = default,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        var ctx = contextBuilder.BuildSessionContext(taskGroupId);
        var session = sessionRepo.Start(taskGroupId);
        await db.SaveChangesAsync(ct);
        var sessionWithCtx = ctx with { SessionId = session.Id };

        await sessionRepo.AppendMessageAsync(session.Id, "user", taskInput, ct: ct);
        await db.SaveChangesAsync(ct);

        try
        {
            // Stage 84：v5 IAgentTool path 整套砍 — v5.5 為 production active 唯一 path（對齊 Stage 78a 砍 v4 ecosystem pattern）。
            var talentsList = await talentFactory.GetAllAsync(ct);
            // Stage 70：v5.5 Phase 2 Step 4 — UseV5SubtaskPlanning flag 切 JSON SubtaskPlan path
            var useV5SubtaskPlanning = await workflowResolver.GetUseV5SubtaskPlanningAsync(ct);
            logger.LogInformation(
                "PetraOrchestrator 啟動 (v5.5 Talent-Skill path) — sessionId={SessionId} taskGroupId={TaskGroupId} talentsCount={Count} useV5SubtaskPlanning={SubtaskPlanning} workingDir={Dir}",
                session.Id, taskGroupId, talentsList.Count, useV5SubtaskPlanning, sessionWithCtx.WorkingDir);

            // 1. Decide — useV5SubtaskPlanning=true 走 JSON SubtaskPlan / false 走 Stage 69 既有 Skill 序列線性 chain（Linear 包 SubtaskPlan 統一介面）
            // Stage 79：images 傳 Petra LLM call sites（GeminiProvider multimodal 真實看圖 / Petra 拍板 NeedsImageContext per subtask）
            SubtaskPlan plan;
            List<ITalent> talentPicks;
            if (useV5SubtaskPlanning)
            {
                (plan, talentPicks) = await talentDispatch.DecideTalentsWithPlanAsync(taskInput, talentsList, sessionWithCtx, ct, images);
            }
            else
            {
                var (skills, picks) = await talentDispatch.DecideTalentsAsync(taskInput, talentsList, sessionWithCtx, ct, images);
                plan = SubtaskPlan.Linear(skills);
                talentPicks = picks;
            }
            if (talentPicks.Count == 0)
            {
                logger.LogWarning("Petra v5.5 動態決策回空序列 sessionId={SessionId}", session.Id);
                await sessionRepo.CompleteAsync(session.Id, ct);
                await db.SaveChangesAsync(ct);
                return PetraOrchestratorResult.Empty(session.Id);
            }
            var decidedCapabilities = plan.Subtasks.Select(s => s.SkillName).ToList();

            // Stage 80：HITL plan_confirm 閘門 — flag=true 走 HITL path（開 BossInteraction plan_confirm 卡 + pause session + return Paused）
            // flag=false 維持 v5.5 baseline auto dispatch 0 行為改變（守 production 0 regression / Trial_v24 開時切 true）
            var useHITLPlanConfirmation = await workflowResolver.GetUseHITLPlanConfirmationAsync(ct);
            if (useHITLPlanConfirmation)
            {
                await planConfirmation.WaitForPlanConfirmationAsync(
                    session.Id, taskInput, plan, talentPicks, decidedCapabilities, ct);
                await db.SaveChangesAsync(ct);
                logger.LogInformation(
                    "Stage 80：HITL plan_confirm 閘門 fire — sessionId={SessionId} subtasks={SubtaskCount} talents=[{Talents}] 等 Christ 4 decision 拍板",
                    session.Id, plan.Subtasks.Count, string.Join(",", talentPicks.Select(t => t.Name)));
                return PetraOrchestratorResult.Paused(session.Id, decidedCapabilities,
                    $"Petra 已拆 {plan.Subtasks.Count} subtask 等 Christ HITL plan_confirm 拍板（approve/edit/reject/respond）。");
            }

            // 2. v5.5 自管 chain dispatch
            // Stage 70：plan.Subtasks index 與 talentPicks index 對齊（CreateAgent 用 plan.Subtasks[i].SkillName 動態傳）
            var talentAgents = new AIAgent[talentPicks.Count];
            for (var i = 0; i < talentPicks.Count; i++)
            {
                talentAgents[i] = talentPicks[i].CreateAgent(sessionWithCtx, plan.Subtasks[i].SkillName);
            }

            // Stage 69 v2.1：v5Memory flag — scope = PetraSession（不是 v4 TaskGroup）— session.Id 100% 有值
            var useV5Memory = await workflowResolver.GetUseV5MemoryAsync(ct);
            // Stage 75：talentNameToIdMap 提前 build — per-Talent serialization lock 永遠需要 Talent Id（不分 memory flag）
            var names = talentPicks.Select(t => t.Name).Distinct().ToList();
            var talentNameToIdMap = await db.Talents
                .Where(t => names.Contains(t.Name) && t.ProjectId == null)
                .ToDictionaryAsync(t => t.Name, t => t.Id, ct);

            var outcome = await talentDispatch.DispatchTalentsAsync(
                session.Id, taskInput, plan, talentPicks, talentAgents,
                useV5Memory, talentNameToIdMap, images, sessionWithCtx, ct);
            await db.SaveChangesAsync(ct);

            // Stage 81：replan signal handling（cap_reached → intervention + cancelled / replan_confirm → 開卡 + paused）
            if (outcome.Replan is { } signal)
            {
                return await dynamicReplan.HandleReplanSignalAsync(session.Id, taskInput, signal, ct);
            }

            var dispatchNames = talentPicks.Select(t => t.Name).ToList();

            // Stage 64 子項 2：Workers 完成後 git commit/push/PR 接通（沿用 v4 GitHubService.CommitAll/Push/OpenPullRequestAsync API）。
            // 無 git diff → 不誤建 PR。Mock 階段 workingDir 不是 git repo → FinalizeGitAsync 內捕例外 + log warning 不擋流程（adapter 跑 Mock 時 workingDir 通常為空）。
            var prUrl = await gitFinalization.FinalizeGitAsync(sessionWithCtx, taskInput, decidedCapabilities, dispatchNames, ct);

            // Stage 83 v5 Bug 4：prUrl 寫進 PetraSession.ResultPrUrl（Dashboard Tasks 歷史 tab 顯示 PR link）
            await sessionRepo.CompleteAsync(session.Id, ct, prUrl);
            await db.SaveChangesAsync(ct);

            var summary = $"Petra 完成 {dispatchNames.Count} dispatch（{string.Join(" → ", dispatchNames)}）"
                + (prUrl is null ? "。" : $" + PR {prUrl}。");
            return PetraOrchestratorResult.Done(session.Id, decidedCapabilities, summary);
        }
        catch (OperationCanceledException)
        {
            await sessionRepo.EscalateAsync(session.Id, CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PetraOrchestrator 執行失敗 sessionId={SessionId}", session.Id);
            await sessionRepo.EscalateAsync(session.Id, CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
            return PetraOrchestratorResult.Failure(session.Id, Array.Empty<string>(), ex.Message);
        }
    }

    /// <summary>
    /// 重啟 rebuild context（PetraSessionRecoveryService 用）— 紀律：重啟重跑不從 checkpoint resume。
    /// 從 task 原始 input + 已 responded BossInteraction 紀錄重跑 DecideAsync + BuildSequential。
    /// </summary>
    public async Task<PetraOrchestratorResult> ResumeAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await sessionRepo.GetWithMessagesAsync(sessionId, ct);
        if (session is null)
        {
            logger.LogWarning("PetraOrchestrator.ResumeAsync 找不到 sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "session 不存在");
        }

        // 從原始 user message + 已 responded BossInteraction 拼成 task input（重啟重跑不雙重 ask）
        var taskInput = await contextBuilder.BuildResumeInputAsync(session, ct);
        logger.LogInformation(
            "PetraOrchestrator resume sessionId={SessionId} 重新從 task input 跑（重啟重跑紀律）",
            sessionId);

        // 直接走 StartAsync 但是用既有 session（不建新 session）
        // PoC 簡化：mark 既有 session done + 開新 session
        await sessionRepo.CompleteAsync(sessionId, ct);
        await db.SaveChangesAsync(ct);
        return await StartAsync(session.TaskGroupId, taskInput, ct);
    }

    // Stage 84：caller 4 處 0 改動紀律 — Resume 入口 forwarder 主入口保留 / 內部委派 sub-service
    public virtual Task<PetraOrchestratorResult> ResumeFromPlanConfirmationAsync(
        Guid sessionId, string decision, string? contextOverride, CancellationToken ct = default)
        => planConfirmation.ResumeFromPlanConfirmationAsync(sessionId, decision, contextOverride, ct);

    public virtual Task<PetraOrchestratorResult> ResumeFromReplanConfirmationAsync(
        Guid sessionId, string decision, string? contextOverride, CancellationToken ct = default)
        => dynamicReplan.ResumeFromReplanConfirmationAsync(sessionId, decision, contextOverride, ct);
}
