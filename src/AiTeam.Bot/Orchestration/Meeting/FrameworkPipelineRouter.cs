using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Orchestration.Appeal;
using AiTeam.Bot.Orchestration.Qa;
using AiTeam.Bot.Workflows.Pipeline;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Orchestration.Meeting;

/// <summary>
/// Stage 53A：framework Pipeline Workflow router（v4 漸進遷移第五步 macro-orchestration）。
///
/// Aria 方案 C 拍板（2026-05-03）：53A 範圍縮小 — Pipeline 主 Workflow 從 Dev_plan 階段啟動
/// （Kickoff/Design 留 legacy；Stage 55 收尾統一整合）。
///
/// 對齊 Stage 49/50/52 既有 framework router 慣例（Singleton + IServiceProvider service locator 解循環依賴 +
/// 雙 marker {ActiveOrchestration="FrameworkPipeline" + PipelineFrameworkStateJson != null} + ICheckpointStore&lt;JsonElement&gt;）。
///
/// 核心 4 method：
///   - HandlePipelineAsync(group, ct)：主入口（FireOneStepAsync line 461 第三條 Dev_plan 分流啟動）— set marker → build workflow → RunStreamingAsync + WatchStreamAsync 收 RequestInfoEvent yield 等 callback / WorkflowOutputEvent finalize
///   - ResumeAfterAgentAsync(group, completedAgent, result, ct)：callback resume（J1 機制核心）— 由 HandleAgentCompletedAsync 8A 入口分流呼叫；新 HTTP scope LoadFromDbAsync + ResumeStreamingAsync from latest checkpoint + 比對 PortId 對應 LastAgentName → CreateResponse(對應 Response 型別) + SendResponseAsync → 繼續 watch 直到下個 RequestInfoEvent（下個 stage yield）或 PipelineLoopResult（finalize）
///   - RecoverStuckFrameworkPipelineAsync(ct)：Crash Recovery — 議題 12 升級 ResumeStreamingAsync（沿用 Stage 51 試點 know-how，不採降級重跑）；rehydrate state 後等下次 Agent callback 自然推進
///   - FinalizePipelineAsync(group, result, ct)：收尾 — Completed=true 清 marker / Completed=false（fallback）已由 Executor ClearMarkerAndFallbackAsync 清 marker，主動 call legacy method 對應 reason 接管（議題 9 修法）
///
/// fallback reason → legacy method dispatch（議題 9 + 邊界）：
///   - reviewer_critical             → group.FixIteration++ + tgs.FireStepsAsync([WorkflowStep("Dev", IsFixLoop:true)])
///   - dev_plan_failed_escalate      → appealOrchestration.HandleDevPlanCompletedAsync
///   - dev_blocker                   → appealOrchestration.HandleDevBlockerAsync
///   - dev_failed                    → tgs.NotifyBossDevFailedInterventionAsync + group.Status=NeedsIntervention
///   - qa_fix_loop                   → 已 fire Dev_fix（HandleQaCompletedAsync 內），無需 call（純清 marker）
///   - qa_failed / qa_intervention   → tgs.NotifyBossInterventionAsync（依 group.Status 已 set）
///   - doc_failed                    → tgs.NotifyBossInterventionAsync + group.Status=NeedsIntervention
///   - group_not_found               → log error 即可（邊界）
///   - arbitration_skip_reviewer     → 預留 Stage 53B（53A happy path 不會觸發）
/// </summary>
public sealed class FrameworkPipelineRouter
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PipelineWorkflowFactory _workflowFactory;
    private readonly PipelineCheckpointStore _checkpointStore;
    private readonly WorkflowSettingsResolver _workflowResolver;
    private readonly ILogger<FrameworkPipelineRouter> _logger;

    public FrameworkPipelineRouter(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        PipelineWorkflowFactory workflowFactory,
        PipelineCheckpointStore checkpointStore,
        WorkflowSettingsResolver workflowResolver,
        ILogger<FrameworkPipelineRouter> logger)
    {
        _serviceProvider = serviceProvider;
        _scopeFactory = scopeFactory;
        _workflowFactory = workflowFactory;
        _checkpointStore = checkpointStore;
        _workflowResolver = workflowResolver;
        _logger = logger;
    }

    // ============================================================
    //  1. HandlePipelineAsync 主入口
    // ============================================================

    /// <summary>
    /// Stage 53A：Pipeline 主入口（FireOneStepAsync 第三條 Dev_plan 分流啟動）。
    /// set ActiveOrchestration="FrameworkPipeline" → LoadFromDbAsync → build workflow → RunStreamingAsync +
    /// WatchStreamAsync 收 events（yield 時 break，finalize 時 call FinalizePipelineAsync）。
    /// </summary>
    public async Task HandlePipelineAsync(TaskGroup group, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. set ActiveOrchestration marker（雙 marker 第一個）
        await db.TaskGroups.Where(g => g.Id == group.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ActiveOrchestration, "FrameworkPipeline"), ct);

        _logger.LogInformation("[Stage53A] HandlePipelineAsync 啟動（Group={Id}）", group.Id);

        try
        {
            // 2. LoadFromDbAsync（new session 時無 checkpoint）
            await _checkpointStore.LoadFromDbAsync(group.Id, ct);

            // 3. Build workflow + CheckpointManager
            var workflow = _workflowFactory.CreatePipelineWorkflow();
            var manager = _workflowFactory.CreateCheckpointManager();

            // 4. RunStreamingAsync 第一次：傳入 PipelineStartBridge 觸發 PipelineStartExecutor
            //    對齊 FrameworkKickoffRouter L409 / FrameworkDesignRouter L382 5-arg signature（workflow, initialState, manager, sessionId, ct）
            var sessionId = group.Id.ToString();
            var initialBridge = new PipelineStartBridge(group.Id);
            await using var run = await InProcessExecution.RunStreamingAsync(workflow, initialBridge, manager, sessionId, ct);

            // 5. WatchStreamAsync 收 events 直到 yield（RequestInfoEvent）or finalize（WorkflowOutputEvent）
            PipelineLoopResult? loopResult = null;
            await foreach (var ev in run.WatchStreamAsync().WithCancellation(ct))
            {
                if (ev is RequestInfoEvent requestEvt)
                {
                    // Agent stage yield 等 callback — 保留 marker 退出 loop（HandleAgentCompletedAsync 8A 分流會觸發 ResumeAfterAgentAsync）
                    _logger.LogInformation(
                        "[Stage53A] HandlePipelineAsync yield 等 Agent callback（Group={Id}, PortId={Port}）— 保留 marker，等 callback resume",
                        group.Id, requestEvt.Request.PortInfo.PortId);
                    return;  // ⚠️ 不清 marker（保留 PipelineFrameworkStateJson != null 給 callback resume 用）
                }
                if (ev is WorkflowOutputEvent outputEvt && outputEvt.Is<PipelineLoopResult>(out var r))
                {
                    loopResult = r;
                    _logger.LogInformation(
                        "[Stage53A] HandlePipelineAsync WorkflowOutputEvent（Group={Id}，completed={Completed}，fallbackReason={Reason}）",
                        group.Id, r.Completed, r.FallbackReason ?? "(none)");
                }
                else if (ev is WorkflowErrorEvent errEvt)
                {
                    _logger.LogError(
                        "[Stage53A] WorkflowErrorEvent（Group={Id}，exception={Exception}）",
                        group.Id, errEvt.Exception?.ToString() ?? "(null)");
                }
                else if (ev is ExecutorFailedEvent failedEvt)
                {
                    _logger.LogError(
                        "[Stage53A] ExecutorFailedEvent（executorId={ExecutorId}，data={Data}）",
                        failedEvt.ExecutorId, failedEvt.Data?.ToString() ?? "(null)");
                }
            }

            // 6. finalize（happy path 不太可能在第一次 RunStreamingAsync 直接走完，因為第一個 stage DevPlan 一定 yield）
            if (loopResult is not null)
            {
                await FinalizePipelineAsync(group, loopResult, ct);
            }
            else
            {
                _logger.LogWarning(
                    "[Stage53A] HandlePipelineAsync watch loop 結束但無 PipelineLoopResult 也無 yield（Group={Id}）— 異常狀況，清 marker",
                    group.Id);
                await ClearMarkersAsync(group.Id, default);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Stage53A] HandlePipelineAsync 異常（Group={Id}）— 清 marker + group.Status=NeedsIntervention", group.Id);
            await ClearMarkersAsync(group.Id, default);
            await SetNeedsInterventionAsync(group.Id, $"Pipeline framework 異常：{ex.Message}", default);
        }
    }

    // ============================================================
    //  2. ResumeAfterAgentAsync callback resume（J1 機制核心）
    // ============================================================

    /// <summary>
    /// Stage 53A：Agent callback resume（HandleAgentCompletedAsync 8A 分流呼叫）。
    /// 沿用 Stage 51 FrameworkHitlBridge.HandleMidInterruptResponseAsync L207-282 ResumeStreamingAsync know-how。
    ///
    /// 跨 HTTP scope 重 build workflow + LoadFromDbAsync + ResumeStreamingAsync from latest checkpoint，
    /// 收第一個 RequestInfoEvent 比對 PortId 對應 completedAgent → CreateResponse(對應 Response 型別) + SendResponseAsync →
    /// 繼續 watch 直到下個 RequestInfoEvent（下個 stage yield）或 PipelineLoopResult（finalize）。
    /// </summary>
    public async Task ResumeAfterAgentAsync(
        TaskGroup group, string completedAgent, AgentExecutionResult result, CancellationToken ct)
    {
        _logger.LogInformation(
            "[Stage53A] ResumeAfterAgentAsync framework path 觸發 ResumeStreamingAsync（Group={Id}，completedAgent={Agent}）",
            group.Id, completedAgent);

        // 1. LoadFromDbAsync + Get latest checkpoint
        await _checkpointStore.LoadFromDbAsync(group.Id, ct);
        var sessionId = group.Id.ToString();
        var latest = _checkpointStore.GetLatestCheckpoint(sessionId);
        if (latest is null)
        {
            _logger.LogWarning(
                "[Stage53A] ResumeAfterAgentAsync：latest checkpoint 不存在（Group={Id}），略過",
                group.Id);
            return;
        }

        // 2. 解析 completedAgent → 對應 PortId + Response 物件
        var (expectedPortId, responseObj) = BuildAgentCompletionResponse(completedAgent, result);
        if (expectedPortId is null || responseObj is null)
        {
            _logger.LogWarning(
                "[Stage53A] ResumeAfterAgentAsync：completedAgent={Agent} 不在 Pipeline 範圍（5 Agent stage 之外），略過 — Pipeline marker 應已被 fallback 清除，正常 callback 不會走到這",
                completedAgent);
            return;
        }

        // 3. ResumeStreamingAsync from latest checkpoint
        var workflow = _workflowFactory.CreatePipelineWorkflow();
        var manager = _workflowFactory.CreateCheckpointManager();
        await using var run = await InProcessExecution.ResumeStreamingAsync(workflow, latest, manager, ct);

        // 4. WatchStreamAsync 收 events
        //    第一個 RequestInfoEvent 應對應 expectedPortId（framework 自動 re-emit pending request）→ SendResponseAsync
        //    後續：可能下個 stage 又 yield（新 RequestInfoEvent）→ break 等下次 callback / 或 PipelineLoopResult finalize
        PipelineLoopResult? loopResult = null;
        var sentResponse = false;
        await foreach (var ev in run.WatchStreamAsync().WithCancellation(ct))
        {
            if (ev is RequestInfoEvent requestEvt)
            {
                if (!sentResponse && requestEvt.Request.PortInfo.PortId == expectedPortId)
                {
                    var externalResponse = requestEvt.Request.CreateResponse(responseObj);
                    await run.SendResponseAsync(externalResponse);
                    sentResponse = true;
                    _logger.LogInformation(
                        "[Stage53A] ResumeAfterAgentAsync SendResponseAsync 完成（Group={Id}，PortId={Port}，agent={Agent}）",
                        group.Id, expectedPortId, completedAgent);
                    continue;
                }

                // 下個 stage 又 yield — 保留 marker 退出 loop 等下次 callback
                _logger.LogInformation(
                    "[Stage53A] ResumeAfterAgentAsync 下個 stage yield（Group={Id}，新 PortId={Port}）— 保留 marker 等下次 callback",
                    group.Id, requestEvt.Request.PortInfo.PortId);
                return;
            }
            if (ev is WorkflowOutputEvent outputEvt && outputEvt.Is<PipelineLoopResult>(out var r))
            {
                loopResult = r;
                _logger.LogInformation(
                    "[Stage53A] ResumeAfterAgentAsync WorkflowOutputEvent（Group={Id}，completed={Completed}，fallbackReason={Reason}）",
                    group.Id, r.Completed, r.FallbackReason ?? "(none)");
            }
            else if (ev is WorkflowErrorEvent errEvt)
            {
                _logger.LogError(
                    "[Stage53A] ResumeAfterAgentAsync WorkflowErrorEvent（Group={Id}，exception={Exception}）",
                    group.Id, errEvt.Exception?.ToString() ?? "(null)");
            }
            else if (ev is ExecutorFailedEvent failedEvt)
            {
                _logger.LogError(
                    "[Stage53A] ResumeAfterAgentAsync ExecutorFailedEvent（executorId={ExecutorId}，data={Data}）",
                    failedEvt.ExecutorId, failedEvt.Data?.ToString() ?? "(null)");
            }
        }

        // 5. finalize
        if (loopResult is not null)
        {
            await FinalizePipelineAsync(group, loopResult, ct);
        }
        else
        {
            _logger.LogWarning(
                "[Stage53A] ResumeAfterAgentAsync watch loop 結束但無 PipelineLoopResult 也無新 yield（Group={Id}）— 異常狀況，清 marker",
                group.Id);
            await ClearMarkersAsync(group.Id, default);
        }
    }

    // ============================================================
    //  3. RecoverStuckFrameworkPipelineAsync（議題 12 升級 ResumeStreamingAsync）
    // ============================================================

    /// <summary>
    /// Stage 53A：Bot 啟動掃 task_groups.PipelineFrameworkStateJson != null 的 group rehydrate state。
    /// 議題 12 升級（沿用 Stage 51 試點 ResumeStreamingAsync know-how，不採 Stage 49/50/52 降級重跑）：
    ///   1. LoadFromDbAsync + 取 latest checkpoint
    ///   2. ResumeStreamingAsync rehydrate framework state（Recovery 階段無 callback signal，不 SendResponseAsync）
    ///   3. WatchStreamAsync 收第一個 RequestInfoEvent → 確認 Pipeline 在 yield 等 callback → break 保留 marker
    ///   4. 等下次 Agent callback 觸發 ResumeAfterAgentAsync 自然推進
    ///
    /// Stage 45 紀律：paused TaskGroup 不參與 crash recovery（暫停意圖保留）。
    /// </summary>
    public async Task RecoverStuckFrameworkPipelineAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stuckGroupIds = await db.TaskGroups
            .Where(g => g.PipelineFrameworkStateJson != null && !g.IsPaused)
            .Select(g => g.Id)
            .ToListAsync(ct);

        if (stuckGroupIds.Count == 0)
        {
            _logger.LogInformation("[FrameworkPipelineRouter] 啟動：無 stuck framework pipeline");
            return;
        }

        _logger.LogWarning(
            "[FrameworkPipelineRouter] 啟動：發現 {Count} 個 stuck framework pipeline，採 ResumeStreamingAsync 升級策略 rehydrate（議題 12）",
            stuckGroupIds.Count);

        foreach (var groupId in stuckGroupIds)
        {
            try
            {
                await _checkpointStore.LoadFromDbAsync(groupId, ct);
                var sessionId = groupId.ToString();
                var latest = _checkpointStore.GetLatestCheckpoint(sessionId);
                if (latest is null)
                {
                    _logger.LogWarning(
                        "[FrameworkPipelineRouter] Recovery Group={Id}：PipelineFrameworkStateJson 有值但 latest checkpoint 不存在，清 marker",
                        groupId);
                    await ClearMarkersAsync(groupId, ct);
                    continue;
                }

                _logger.LogInformation(
                    "[FrameworkPipelineRouter] Recovery Group={Id}：ResumeStreamingAsync rehydrate（latest={Ckpt}）— 等下次 Agent callback 推進",
                    groupId, latest.CheckpointId);

                // ResumeStreamingAsync rehydrate（不 SendResponseAsync — Recovery 階段無 callback signal）
                var workflow = _workflowFactory.CreatePipelineWorkflow();
                var manager = _workflowFactory.CreateCheckpointManager();
                await using var run = await InProcessExecution.ResumeStreamingAsync(workflow, latest, manager, ct);

                // 收第一個 RequestInfoEvent 確認 Pipeline 在 yield 等 callback → break 保留 marker
                var seenPendingRequest = false;
                string? pendingPortId = null;
                await foreach (var ev in run.WatchStreamAsync().WithCancellation(ct))
                {
                    if (ev is RequestInfoEvent requestEvt)
                    {
                        seenPendingRequest = true;
                        pendingPortId = requestEvt.Request.PortInfo.PortId;
                        _logger.LogInformation(
                            "[FrameworkPipelineRouter] Recovery Group={Id}：rehydrate 完成（pending PortId={Port}），等下次 Agent callback 觸發 ResumeAfterAgentAsync",
                            groupId, pendingPortId);
                        break;
                    }
                    if (ev is WorkflowOutputEvent)
                    {
                        // 罕見：rehydrate 直接走完（fallback 邊界），交給 FinalizePipelineAsync
                        _logger.LogInformation("[FrameworkPipelineRouter] Recovery Group={Id}：rehydrate 直接 emit WorkflowOutputEvent，交給 FinalizePipelineAsync", groupId);
                        break;
                    }
                }

                if (!seenPendingRequest)
                {
                    _logger.LogWarning(
                        "[FrameworkPipelineRouter] Recovery Group={Id}：rehydrate 未見 pending RequestInfoEvent — 異常狀況，清 marker（避免無人推進）",
                        groupId);
                    await ClearMarkersAsync(groupId, ct);
                    continue;
                }

                // Stage 53A 驗收期 follow-up #4：Pipeline 接管 failed Agent task requeue
                // 場景：Bot restart 期間 Agent task 跑到一半被 OperationCanceledException 標 failed →
                // RecoverStuckTasksAsync 只 requeue QueueStatus=processing 不處理 failed task →
                // Pipeline 永遠卡在對應 PortId 等永遠不來的 callback。
                // 修法：Recovery 偵測 pending PortId 後，找該 stage 對應 Agent 的 failed task 重 requeue（Pipeline 自己接管整體 Recovery 完整性）
                if (pendingPortId is not null)
                {
                    var pendingAgent = pendingPortId switch
                    {
                        PipelineWorkflowFactory.DevPlanCompletionPortId  => "Dev_plan",
                        PipelineWorkflowFactory.DevCompletionPortId      => "Dev",
                        PipelineWorkflowFactory.ReviewerCompletionPortId => "Reviewer",
                        PipelineWorkflowFactory.QaCompletionPortId       => "QA",
                        PipelineWorkflowFactory.DocCompletionPortId      => "Doc",
                        _                                                => null,
                    };
                    if (pendingAgent is not null)
                    {
                        await RequeueFailedAgentTaskAsync(groupId, pendingAgent, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FrameworkPipelineRouter] Recovery Group={Id} 異常 — 清 marker", groupId);
                try { await ClearMarkersAsync(groupId, CancellationToken.None); } catch { /* swallow */ }
            }
        }
    }

    // ============================================================
    //  4. FinalizePipelineAsync 收尾 + 5 fallback 點 dispatch
    // ============================================================

    /// <summary>
    /// Stage 53A：Pipeline 收尾。
    ///   - Completed=true → 清 marker（成功完成）
    ///   - Completed=false（fallback）→ 執行 Executor ClearMarkerAndFallbackAsync 已清的 marker 確保（idempotent）+ 主動 call legacy method 對應 reason 接管（議題 9 修法）
    ///
    /// 避免遞迴關鍵：fallback 主動 call legacy 時 PipelineFrameworkStateJson 已 null →
    /// HandleAgentCompletedAsync 8A 分流條件失敗 → 走 legacy 路徑（不會遞迴回 framework path）。
    /// </summary>
    public async Task FinalizePipelineAsync(TaskGroup group, PipelineLoopResult result, CancellationToken ct)
    {
        if (result.Completed)
        {
            // happy path：NotifyMergeStageExecutor 已 call NotifyBossMergeAsync，這裡只需清 marker
            await ClearMarkersAsync(group.Id, ct);
            _logger.LogInformation(
                "[Stage53A] FinalizePipelineAsync happy path 完成 — marker 已清（Group={Id}）",
                group.Id);
            return;
        }

        // fallback path（Completed=false）— ClearMarkerAndFallbackAsync 已清 marker（議題 9 + Aria 時序紀律），這裡 idempotent 再清一次確保
        await ClearMarkersAsync(group.Id, ct);

        var reason = result.FallbackReason ?? "unknown";
        var lastResult = result.LastResult;
        _logger.LogInformation(
            "[Stage53A-FallbackToLegacy] FinalizePipelineAsync fallback dispatch（Group={Id}，reason={Reason}）",
            group.Id, reason);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var freshGroup = await taskRepo.GetGroupByIdAsync(group.Id, ct);
        if (freshGroup is null)
        {
            _logger.LogError("[Stage53A-FallbackToLegacy] FinalizePipelineAsync 找不到 Group={Id}，無法 dispatch", group.Id);
            return;
        }

        switch (reason)
        {
            case "reviewer_critical":
                // 模擬 legacy WorkflowEngine Reviewer fail routing：FixIteration++ + FireStepsAsync(Dev, IsFixLoop:true)
                freshGroup.FixIteration++;
                await taskRepo.SaveAsync(ct);
                _logger.LogInformation(
                    "[Stage53A-FallbackToLegacy] reviewer_critical → FixIteration={Iter} + FireStepsAsync(Dev, IsFixLoop:true)（Group={Id}）",
                    freshGroup.FixIteration, group.Id);
                {
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.FireStepsAsync(freshGroup, [new WorkflowStep("Dev", IsFixLoop: true)], ct);
                }
                break;

            case "dev_plan_failed_escalate":
                if (lastResult is null)
                {
                    _logger.LogWarning("[Stage53A-FallbackToLegacy] dev_plan_failed_escalate 無 lastResult，無法 call HandleDevPlanCompletedAsync（Group={Id}）", group.Id);
                    break;
                }
                {
                    var appealOrch = scope.ServiceProvider.GetRequiredService<AppealOrchestrationService>();
                    var projectId = string.IsNullOrWhiteSpace(freshGroup.Project)
                        ? (Guid?)null
                        : await taskRepo.GetProjectIdByNameAsync(freshGroup.Project, ct);
                    await appealOrch.HandleDevPlanCompletedAsync(freshGroup, lastResult, taskRepo, projectId, ct);
                    _logger.LogInformation("[Stage53A-FallbackToLegacy] dev_plan_failed_escalate → HandleDevPlanCompletedAsync 接管（Group={Id}）", group.Id);
                }
                break;

            case "dev_blocker":
                if (lastResult is null)
                {
                    _logger.LogWarning("[Stage53A-FallbackToLegacy] dev_blocker 無 lastResult，無法 call HandleDevBlockerAsync（Group={Id}）", group.Id);
                    break;
                }
                {
                    var appealOrch = scope.ServiceProvider.GetRequiredService<AppealOrchestrationService>();
                    var projectId = string.IsNullOrWhiteSpace(freshGroup.Project)
                        ? (Guid?)null
                        : await taskRepo.GetProjectIdByNameAsync(freshGroup.Project, ct);
                    await appealOrch.HandleDevBlockerAsync(freshGroup, lastResult, taskRepo, projectId, ct);
                    _logger.LogInformation("[Stage53A-FallbackToLegacy] dev_blocker → HandleDevBlockerAsync 接管（Group={Id}）", group.Id);
                }
                break;

            case "dev_failed":
                {
                    var failSummary = lastResult?.Summary ?? "Dev 執行失敗（無詳細訊息）";
                    taskRepo.UpdateGroupStatus(freshGroup, AiTeam.Shared.Constants.TaskStatus.NeedsIntervention);
                    freshGroup.InterventionReason = $"Dev 失敗（Pipeline framework path）：{failSummary}";
                    await taskRepo.SaveAsync(ct);
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.NotifyBossDevFailedInterventionAsync(freshGroup, isFixLoop: false, failSummary, ct);
                    _logger.LogInformation("[Stage53A-FallbackToLegacy] dev_failed → NotifyBossDevFailedInterventionAsync（Group={Id}）", group.Id);
                }
                break;

            case "qa_fix_loop":
                // HandleQaCompletedAsync 內已 fire Dev_fix（QaFixRound > 0），無需主動 call legacy
                // Pipeline marker 已清 → Dev_fix callback 觸發時 HandleAgentCompletedAsync 8A 分流自然失敗 → 走 legacy ✅
                _logger.LogInformation("[Stage53A-FallbackToLegacy] qa_fix_loop → 已由 HandleQaCompletedAsync 內 fire Dev_fix，無需主動 call（Group={Id}）", group.Id);
                break;

            case "qa_failed":
            case "qa_intervention":
                {
                    // group.Status 已由 HandleQaCompletedAsync 內 set（NeedsIntervention/failed），這裡 NotifyBossInterventionAsync
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.NotifyBossInterventionAsync(freshGroup, ct);
                    _logger.LogInformation("[Stage53A-FallbackToLegacy] {Reason} → NotifyBossInterventionAsync（Group={Id}）", reason, group.Id);
                }
                break;

            case "doc_failed":
                {
                    var failSummary = lastResult?.Summary ?? "Doc 執行失敗（無詳細訊息）";
                    taskRepo.UpdateGroupStatus(freshGroup, AiTeam.Shared.Constants.TaskStatus.NeedsIntervention);
                    freshGroup.InterventionReason = $"Doc 失敗（Pipeline framework path）：{failSummary}";
                    await taskRepo.SaveAsync(ct);
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.NotifyBossInterventionAsync(freshGroup, ct);
                    _logger.LogInformation("[Stage53A-FallbackToLegacy] doc_failed → NotifyBossInterventionAsync（Group={Id}）", group.Id);
                }
                break;

            case "group_not_found":
                _logger.LogError("[Stage53A-FallbackToLegacy] group_not_found 邊界（Group={Id}）— marker 已清，無法接管", group.Id);
                break;

            case "arbitration_skip_reviewer":
                // Stage 53B 範圍預留 — 53A happy path 不會觸發
                _logger.LogWarning("[Stage53A-FallbackToLegacy] arbitration_skip_reviewer 預留 Stage 53B 範圍（Group={Id}）", group.Id);
                break;

            default:
                _logger.LogError("[Stage53A-FallbackToLegacy] 未識別 reason={Reason}（Group={Id}）— marker 已清", reason, group.Id);
                break;
        }
    }

    // ============================================================
    //  Helpers
    // ============================================================

    /// <summary>解析 completedAgent → 對應 PortId + Response 物件（5 stage-distinct request/response 型別）。</summary>
    private static (string? PortId, object? ResponseObj) BuildAgentCompletionResponse(
        string completedAgent, AgentExecutionResult result)
    {
        return completedAgent switch
        {
            "Dev_plan" => (PipelineWorkflowFactory.DevPlanCompletionPortId,  new DevPlanCompletionResponse(result)),
            "Dev"      => (PipelineWorkflowFactory.DevCompletionPortId,      new DevCompletionResponse(result)),
            "Reviewer" => (PipelineWorkflowFactory.ReviewerCompletionPortId, new ReviewerCompletionResponse(result)),
            "QA"       => (PipelineWorkflowFactory.QaCompletionPortId,       new QaCompletionResponse(result)),
            "Doc"      => (PipelineWorkflowFactory.DocCompletionPortId,      new DocCompletionResponse(result)),
            _          => (null, null),  // Dev_fix / 其他不在 Pipeline 範圍
        };
    }

    /// <summary>Stage 53A 驗收期 follow-up #4：Recovery 期間找對應 stage 的 failed Agent task 重 requeue。
    /// Bot restart 期間 Agent task 跑到一半被 OperationCanceledException 標 Status="failed"，
    /// AgentQueueProcessor.RecoverStuckTasksAsync 只 requeue QueueStatus="processing" 不處理 failed task →
    /// Pipeline 卡在對應 PortId 等永遠不來的 callback。本 helper 補上 Pipeline 接管的 requeue。
    /// 篩選邏輯：群組內 AssignedAgent 對應 + Status="failed" + QueueStatus 任意 → 設成 queued/queued。
    /// 取最近一個（同 stage 多次 fix loop 場景，Stage 53A happy path 限定不會發生但保留紀律）。</summary>
    private async Task RequeueFailedAgentTaskAsync(Guid groupId, string agentName, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var failedTask = await db.Set<TaskItem>()
            .Where(t => t.GroupId == groupId && t.AssignedAgent == agentName && t.Status == "failed")
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (failedTask is null)
        {
            _logger.LogInformation(
                "[FrameworkPipelineRouter] Recovery Group={Id}：pending agent={Agent} 無 failed task 待 requeue（task 可能已正常完成 callback 跑到一半）",
                groupId, agentName);
            return;
        }

        var rows = await db.Set<TaskItem>()
            .Where(t => t.Id == failedTask.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, "queued")
                .SetProperty(t => t.QueueStatus, "queued"), ct);

        if (rows > 0)
        {
            _logger.LogWarning(
                "[FrameworkPipelineRouter] Recovery Group={Id}：requeue failed Agent task（agent={Agent}, taskId={TaskId}）— follow-up #4 修法（Pipeline 接管 Bot restart 邊界 task 中斷邊界）",
                groupId, agentName, failedTask.Id);
        }
    }

    /// <summary>清雙 marker（PipelineFrameworkStateJson + ActiveOrchestration = null）— idempotent。</summary>
    private async Task ClearMarkersAsync(Guid groupId, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.PipelineFrameworkStateJson, (string?)null)
                .SetProperty(g => g.ActiveOrchestration, (string?)null), ct);
    }

    /// <summary>set group.Status=NeedsIntervention + InterventionReason（exception 邊界用）。</summary>
    private async Task SetNeedsInterventionAsync(Guid groupId, string reason, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TaskGroups.Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.Status, AiTeam.Shared.Constants.TaskStatus.NeedsIntervention)
                .SetProperty(g => g.InterventionReason, reason), ct);
    }
}
