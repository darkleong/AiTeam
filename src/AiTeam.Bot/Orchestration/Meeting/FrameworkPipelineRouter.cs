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
/// fallback reason（Stage 53B 移除 5 fallback to legacy dispatch — 4 子流程 framework 化全接管）：
///   - dev_failed                    → tgs.NotifyBossDevFailedInterventionAsync + group.Status=NeedsIntervention（極端邊界）
///   - qa_failed / qa_intervention   → tgs.NotifyBossInterventionAsync（Pipeline QaStage 已 set group.Status）
///   - doc_failed                    → tgs.NotifyBossInterventionAsync + group.Status=NeedsIntervention
///   - group_not_found               → log error 即可（邊界）
///
/// Stage 53B 移除（4 子流程 framework 化全接管）：
///   - reviewer_critical / dev_plan_failed_escalate / dev_blocker / arbitration_skip_reviewer / qa_fix_loop
///   - 對應 legacy dispatch 全刪 — Pipeline Executor 內 SetInterventionAndYieldAsync 自接管 intervention（Completed=true）
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
            //    Stage 55A 缺口 2：bridge 帶 IsSubTask = group.ParentGroupId != null（PipelineStart 路由 parent → KickoffStage / sub-task → DevPlanStage）
            var sessionId = group.Id.ToString();
            var isSubTask = group.ParentGroupId != null;
            var initialBridge = new PipelineStartBridge(group.Id, isSubTask);
            _logger.LogInformation("[Stage55A] HandlePipelineAsync IsSubTask={IsSubTask}（Group={Id}, ParentGroupId={ParentId}）",
                isSubTask, group.Id, group.ParentGroupId);
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
    //  Stage 55A：ResumeAfterKickoff / ResumeAfterDesign（議題 G3 解法 — Pipeline 接管 Kickoff/Design button callback）
    // ============================================================

    /// <summary>
    /// Stage 55A：Kickoff 按鈕 callback resume（HandleKickoffConfirmedAsync 改後 call 此 method）。
    /// 對齊 ResumeAfterAgentAsync 機制 — ResumeStreamingAsync from latest checkpoint，找 Pipeline-KickoffCompletion PortId
    /// → SendResponseAsync(KickoffCompletionResponse(decision, modifyContent)) → 繼續 watch 下個 yield/finalize。
    ///
    /// decision = "continue" / "stop" 餵給 KickoffStageExecutor.HandleResponseAsync。
    /// modify / restart 不走此路（HandleKickoffConfirmedAsync 內既有 legacy 邏輯處理 — Pipeline 仍 yield 等下一輪 button）。
    /// </summary>
    public Task ResumeAfterKickoffAsync(TaskGroup group, string decision, string? modifyContent, CancellationToken ct)
        => ResumeWithResponseAsync(
            group,
            PipelineWorkflowFactory.KickoffCompletionPortId,
            new KickoffCompletionResponse(decision, modifyContent),
            $"Kickoff(decision={decision})",
            ct);

    /// <summary>
    /// Stage 55A：Design 按鈕 callback resume（HandleDesignConfirmedAsync 改後 call 此 method）。
    /// 對齊 ResumeAfterKickoffAsync 機制。
    /// </summary>
    public Task ResumeAfterDesignAsync(TaskGroup group, string decision, string? modifyContent, CancellationToken ct)
        => ResumeWithResponseAsync(
            group,
            PipelineWorkflowFactory.DesignCompletionPortId,
            new DesignCompletionResponse(decision, modifyContent),
            $"Design(decision={decision})",
            ct);

    // ── Stage 55B Session B：5 type-specific intervention HITL Resume methods ──
    //
    // 對齊 ResumeAfterKickoff/Design 純 thin wrapper pattern — delegate to ResumeWithResponseAsync helper。
    // 由 TaskGroupService.ProcessBossResponseAsync 5 case Pipeline 分支觸發（議題 5 = 5A 加 Pipeline 分支保留 legacy handler）。

    /// <summary>Stage 55B：Dev intervention（dev_failed_intervention）button callback resume。
    /// action = "skip" / "retry" / "abort"（從 dev_intervention_skip / _retry / _abort 去前綴）。</summary>
    public Task ResumeAfterDevInterventionAsync(TaskGroup group, string action, CancellationToken ct)
        => ResumeWithResponseAsync(
            group,
            PipelineWorkflowFactory.DevInterventionPortId,
            new DevInterventionResponse(action),
            $"DevIntervention(action={action})",
            ct);

    /// <summary>Stage 55B：QA intervention（qa_failed_intervention）button callback resume。
    /// action = "continue" / "skip" / "abort"（從 qa_intervention_continue / _skip / _abort 去前綴）。</summary>
    public Task ResumeAfterQaInterventionAsync(TaskGroup group, string action, CancellationToken ct)
        => ResumeWithResponseAsync(
            group,
            PipelineWorkflowFactory.QaInterventionPortId,
            new QaInterventionResponse(action),
            $"QaIntervention(action={action})",
            ct);

    /// <summary>Stage 55B：DevPlan escalate（devplan_escalate）button callback resume。
    /// action = "skip" / "abort"（從 devplan_skip / _abort 去前綴）。</summary>
    public Task ResumeAfterDevPlanEscalateAsync(TaskGroup group, string action, CancellationToken ct)
        => ResumeWithResponseAsync(
            group,
            PipelineWorkflowFactory.DevPlanEscalatePortId,
            new DevPlanEscalateResponse(action),
            $"DevPlanEscalate(action={action})",
            ct);

    /// <summary>Stage 55B：DevPlan unable（dev_plan_unable）button callback resume。
    /// action = "skip" / "abort"（從 devplan_unable_skip / _unable_abort 去 devplan_unable_ 前綴）。</summary>
    public Task ResumeAfterDevPlanUnableAsync(TaskGroup group, string action, CancellationToken ct)
        => ResumeWithResponseAsync(
            group,
            PipelineWorkflowFactory.DevPlanUnablePortId,
            new DevPlanUnableResponse(action),
            $"DevPlanUnable(action={action})",
            ct);

    /// <summary>Stage 55B：Split task proposal（split_task_proposal）button callback resume。
    /// action = "accept" / "modify" / "reject" / "abort"（從 split_accept / _modify / _reject / _abort 去前綴）。
    /// modifyContent = Christ TextInputDialog 修改的 phases JSON（modify path 用）；
    /// splitProposalJson = BossInteraction.ContextJson 內既有 Petra 原 proposal JSON（accept path 用）。</summary>
    public Task ResumeAfterSplitTaskProposalAsync(
        TaskGroup group, string action, string? modifyContent, string? splitProposalJson, CancellationToken ct)
        => ResumeWithResponseAsync(
            group,
            PipelineWorkflowFactory.SplitTaskProposalPortId,
            new SplitTaskProposalResponse(action, modifyContent, splitProposalJson),
            $"SplitTaskProposal(action={action})",
            ct);

    /// <summary>
    /// Stage 55A：Pipeline ResumeStreamingAsync 共用 helper — ResumeAfterAgentAsync / ResumeAfterKickoff/DesignAsync 共用核心邏輯。
    /// 流程：LoadFromDb → ResumeStreamingAsync from latest → 找 expectedPortId 的 RequestInfoEvent → SendResponseAsync → 繼續 watch 直到下個 yield/finalize。
    /// </summary>
    private async Task ResumeWithResponseAsync(
        TaskGroup group, string expectedPortId, object responseObj, string contextLabel, CancellationToken ct)
    {
        _logger.LogInformation(
            "[Stage55A] ResumeWithResponseAsync 觸發（Group={Id}, expectedPortId={Port}, context={Ctx}）",
            group.Id, expectedPortId, contextLabel);

        await _checkpointStore.LoadFromDbAsync(group.Id, ct);
        var sessionId = group.Id.ToString();
        var latest = _checkpointStore.GetLatestCheckpoint(sessionId);
        if (latest is null)
        {
            _logger.LogWarning(
                "[Stage55A] ResumeWithResponseAsync：latest checkpoint 不存在（Group={Id}），略過",
                group.Id);
            return;
        }

        var workflow = _workflowFactory.CreatePipelineWorkflow();
        var manager = _workflowFactory.CreateCheckpointManager();
        await using var run = await InProcessExecution.ResumeStreamingAsync(workflow, latest, manager, ct);

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
                        "[Stage55A] ResumeWithResponseAsync SendResponseAsync 完成（Group={Id}, PortId={Port}, Ctx={Ctx}）",
                        group.Id, expectedPortId, contextLabel);
                    continue;
                }
                _logger.LogInformation(
                    "[Stage55A] ResumeWithResponseAsync 下個 stage yield（Group={Id}, 新 PortId={Port}）— 保留 marker 等下次 callback",
                    group.Id, requestEvt.Request.PortInfo.PortId);
                return;
            }
            if (ev is WorkflowOutputEvent outputEvt && outputEvt.Is<PipelineLoopResult>(out var r))
            {
                loopResult = r;
                _logger.LogInformation(
                    "[Stage55A] ResumeWithResponseAsync WorkflowOutputEvent（Group={Id}，completed={Completed}，fallbackReason={Reason}）",
                    group.Id, r.Completed, r.FallbackReason ?? "(none)");
            }
            else if (ev is WorkflowErrorEvent errEvt)
            {
                _logger.LogError("[Stage55A] ResumeWithResponseAsync WorkflowErrorEvent（Group={Id}）：{Exception}",
                    group.Id, errEvt.Exception?.ToString() ?? "(null)");
            }
            else if (ev is ExecutorFailedEvent failedEvt)
            {
                _logger.LogError("[Stage55A] ResumeWithResponseAsync ExecutorFailedEvent（executorId={ExecutorId}）：{Data}",
                    failedEvt.ExecutorId, failedEvt.Data?.ToString() ?? "(null)");
            }
        }

        if (loopResult is not null)
        {
            await FinalizePipelineAsync(group, loopResult, ct);
        }
        else if (!sentResponse)
        {
            _logger.LogWarning(
                "[Stage55A] ResumeWithResponseAsync watch loop 結束但未送 response（Group={Id}, expectedPortId={Port}）— 異常狀況，清 marker",
                group.Id, expectedPortId);
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
                        PipelineWorkflowFactory.DevFixCompletionPortId   => "Dev_fix",  // Stage 53B 新加（議題 12 升級對齊 K1 mapping helper）
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
    /// Stage 53B：Pipeline 收尾。
    ///   - Completed=true → 清 marker（happy path 完成 / intervention 完成 — Executor 內 SetInterventionAndYieldAsync 已 call NotifyBoss）
    ///   - Completed=false → 邊界 fallback case（Pipeline 4 子流程 framework 化後僅剩 group_not_found / dev_failed / qa_failed / qa_intervention / doc_failed 邊界）
    ///
    /// Stage 53B 移除 5 fallback to legacy dispatch case：reviewer_critical / dev_plan_failed_escalate / dev_blocker / arbitration_skip_reviewer / qa_fix_loop
    /// （4 子流程 framework 化全接管 — Pipeline 不再 fallback 給 legacy 推進）
    /// </summary>
    public async Task FinalizePipelineAsync(TaskGroup group, PipelineLoopResult result, CancellationToken ct)
    {
        if (result.Completed)
        {
            // Stage 53B：Completed=true 包含兩種語義：
            //   ① happy path（NotifyMergeStageExecutor 已 call NotifyBossMergeAsync）
            //   ② intervention 完成（Executor SetInterventionAndYieldAsync 已 set group.Status + call NotifyBossInterventionAsync）
            // 兩者都只需清 marker（不重複 call NotifyBoss）
            await ClearMarkersAsync(group.Id, ct);
            _logger.LogInformation(
                "[Stage53B] FinalizePipelineAsync Completed=true 完成 — marker 已清（Group={Id}）",
                group.Id);
            return;
        }

        // fallback path（Completed=false）— Stage 53B 4 子流程 framework 化後僅邊界場景（group_not_found 等）
        await ClearMarkersAsync(group.Id, ct);

        var reason = result.FallbackReason ?? "unknown";
        var lastResult = result.LastResult;
        _logger.LogInformation(
            "[Stage53B] FinalizePipelineAsync 邊界 fallback（Group={Id}，reason={Reason}）",
            group.Id, reason);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var freshGroup = await taskRepo.GetGroupByIdAsync(group.Id, ct);
        if (freshGroup is null)
        {
            _logger.LogError("[Stage53B] FinalizePipelineAsync 找不到 Group={Id}（group_not_found 邊界）", group.Id);
            return;
        }

        switch (reason)
        {
            case "dev_failed":
                {
                    // Pipeline DevStage 已 set group.Status=NeedsIntervention（intervention helper），這裡發 Discord notify
                    // 注意：此 case 在 53B 設計改為由 SetInterventionAndYieldAsync 統一處理（YieldOutput Completed=true）
                    // 保留作為極端邊界 — Executor 走 fallback 路徑（如 group_not_found 後又遞迴觸發 dev_failed）
                    var failSummary = lastResult?.Summary ?? "Dev 執行失敗（無詳細訊息）";
                    taskRepo.UpdateGroupStatus(freshGroup, AiTeam.Shared.Constants.TaskStatus.NeedsIntervention);
                    freshGroup.InterventionReason = $"Dev 失敗（Pipeline framework path）：{failSummary}";
                    await taskRepo.SaveAsync(ct);
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.NotifyBossDevFailedInterventionAsync(freshGroup, isFixLoop: false, failSummary, ct);
                    _logger.LogInformation("[Stage53B] dev_failed → NotifyBossDevFailedInterventionAsync（Group={Id}）", group.Id);
                }
                break;

            case "qa_failed":
            case "qa_intervention":
                {
                    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                    await tgs.NotifyBossInterventionAsync(freshGroup, ct);
                    _logger.LogInformation("[Stage53B] {Reason} → NotifyBossInterventionAsync（Group={Id}）", reason, group.Id);
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
                    _logger.LogInformation("[Stage53B] doc_failed → NotifyBossInterventionAsync（Group={Id}）", group.Id);
                }
                break;

            case "group_not_found":
                _logger.LogError("[Stage53B] group_not_found 邊界（Group={Id}）— marker 已清，無法接管", group.Id);
                break;

            default:
                _logger.LogWarning("[Stage53B] 未識別 reason={Reason}（Group={Id}）— Pipeline 應已涵蓋全 path（4 子流程 framework 化後）", reason, group.Id);
                break;
        }
    }

    // ============================================================
    //  Helpers
    // ============================================================

    /// <summary>解析 completedAgent → 對應 PortId + Response 物件（Stage 53B：6 stage-distinct request/response 型別，K1 拍板擴 5 → 6 entry）。</summary>
    private static (string? PortId, object? ResponseObj) BuildAgentCompletionResponse(
        string completedAgent, AgentExecutionResult result)
    {
        return completedAgent switch
        {
            "Dev_plan" => (PipelineWorkflowFactory.DevPlanCompletionPortId,  new DevPlanCompletionResponse(result)),
            "Dev"      => (PipelineWorkflowFactory.DevCompletionPortId,      new DevCompletionResponse(result)),
            "Dev_fix"  => (PipelineWorkflowFactory.DevFixCompletionPortId,   new DevFixCompletionResponse(result)),  // Stage 53B 新加
            "Reviewer" => (PipelineWorkflowFactory.ReviewerCompletionPortId, new ReviewerCompletionResponse(result)),
            "QA"       => (PipelineWorkflowFactory.QaCompletionPortId,       new QaCompletionResponse(result)),
            "Doc"      => (PipelineWorkflowFactory.DocCompletionPortId,      new DocCompletionResponse(result)),
            _          => (null, null),  // 其他不在 Pipeline 範圍
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
