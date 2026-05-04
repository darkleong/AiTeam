using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord;
using AiTeam.Bot.Discord.Routing;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Orchestration.Hitl;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Dtos;
using AiTeam.Shared.ViewModels;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Services;

/// <summary>
/// Stage 32：/mock 情境觸發邏輯的 shared service，供 Discord 指令與 Dashboard Internal API 共用。
/// 原本集中在 CommandHandler.HandleMockCommandAsync / HandleMockProposalFlowAsync，
/// 抽離後同一份邏輯可被 Dashboard 卡片觸發，實現 FF 十五「Discord 與 Dashboard 功能平等」。
/// </summary>
public class MockScenarioService(
    AppSettingsService appSettings,
    TaskGroupService taskGroupService,
    DiscordSocketClient discordClient,
    IOptions<DiscordSettings> discordSettings,
    IOptions<GitHubSettings> gitHubSettings,
    IServiceProvider serviceProvider,
    KickoffMidInterruptTriggerStore midInterruptTriggerStore,
    ILogger<MockScenarioService> logger)
{
    private readonly DiscordSettings _discord   = discordSettings.Value;
    private readonly GitHubSettings  _gitHub    = gitHubSettings.Value;

    /// <summary>
    /// 執行指定情境（new_feature / bug_fix / tech_improvement / new_feature_with_proposal /
    /// fail_review / fail_qa / fail_dev_plan / review_skipped），建立 TaskGroup 並觸發流程。
    /// </summary>
    /// <returns>(ok, message)：ok 表示是否成功啟動；message 供呼叫端顯示（Discord Followup / Dashboard Snackbar）。</returns>
    public async Task<(bool ok, string message)> RunScenarioAsync(
        string scenario,
        string? customTitle = null,
        string? customProject = null,
        CancellationToken ct = default)
    {
        // MockMode 守門
        if (!await appSettings.GetBoolAsync("MockMode", false, ct))
        {
            return (false,
                "⚠️ `/mock` 僅在 **Mock Mode 啟用**時有效。請至 Dashboard → 系統設定 啟用 Mock Mode 後再試。");
        }

        // 失敗測試情境：設定 FailScenario 狀態（與原 CommandHandler 行為一致）
        if (scenario == "fail_review")
            MockClaudeCodeService.FailScenario = "review_appeal";
        else if (scenario == "fail_qa")
            MockClaudeCodeService.FailScenario = "qa_failure";
        else if (scenario == "fail_dev_plan")
            MockClaudeCodeService.FailScenario = "dev_plan_appeal";
        else if (scenario == "review_skipped")
            MockClaudeCodeService.FailScenario = "review_skipped"; // Stage 39：Vera 略過驗收情境
        // ── Stage 43 新增 4 場景 ──
        else if (scenario == "dev_plan_fail_retry")
            MockClaudeCodeService.FailScenario = "dev_plan_retry_round1";   // 第 1 次失敗 → accept → 第 2 次成功
        else if (scenario == "dev_plan_fail_escalate")
            MockClaudeCodeService.FailScenario = "dev_plan_escalate_loop";  // 連續失敗到 DevPlanRevision >= 2 → escalate
        else if (scenario == "dev_failed_intervention")
            MockClaudeCodeService.FailScenario = "dev_failed_after_review"; // Dev 成功 → Vera Critical → Dev_fix 失敗
        else if (scenario == "qa_failed_fix_then_intervention")
            MockClaudeCodeService.FailScenario = "qa_fix_loop_fail";        // QA 連 N 輪失敗 → escalate
        // ── Stage 45 新增 3 個暫停場景 ──
        else if (scenario == "pause_resume_with_boss_interaction")
            MockClaudeCodeService.FailScenario = "dev_failed_after_review"; // 複用 dev_failed_intervention 邏輯製造 BossInteraction
        // pause_at_kickoff_end / pause_during_dev 不需 FailScenario（PausePoint 已足夠）
        // ── Stage 46-FF 三十五：拆 task 2 個場景 ──
        // RunReadOnlyAsync 回 12 Issue + RunMeetingSessionAsync [SPLIT-TASK] 回 phases JSON 觸發拆 task 提案
        // 失敗版本另在 sub-task 啟動後製造 Cody Dev_plan failed → epic_partial_paused
        else if (scenario is "split_task_propose_accept" or "split_task_subtask_fail_intervention")
            MockClaudeCodeService.FailScenario = scenario;
        // ── Stage 49 v4 漸進遷移：framework Appeal Loop 5 個 Mock 場景 ──
        // Christ 線下驗收時必須先 toggle Dashboard → 系統設定 → 使用 MS Agent Framework Appeal Loop = ON
        // 否則 feature flag 為 false，AppealOrchestrationService 走 legacy path，這 5 個場景就跟 fail_review 等價，無 framework path 可驗
        else if (scenario == "framework_appeal_loop_fast_approve")
            MockClaudeCodeService.FailScenario = null;  // 無失敗，Vera 第 1 輪 approve
        else if (scenario is "framework_appeal_loop_max_iter_approve"
                          or "framework_appeal_loop_max_iter_reject"
                          or "framework_appeal_loop_max_iter_escalate")
            MockClaudeCodeService.FailScenario = "review_appeal";  // 同 fail_review，框架 path 走 max-iter Petra arbitration
        else if (scenario == "framework_appeal_loop_crash_recovery")
            MockClaudeCodeService.FailScenario = "review_appeal";  // 觸發 framework path Round 2，配合下方 PausePoint 設定模擬 crash
        // ── Stage 50 v4 漸進遷移第二步：framework Kickoff Meeting 5 個 Mock 場景 ──
        // Christ 線下驗收時必須先 toggle Dashboard → 系統設定 → 使用 MS Agent Framework Kickoff Meeting = ON
        // 否則 feature flag 為 false，KickoffMeetingService 走 legacy path，這 5 個場景就跟一般 new_feature 等價
        // 機制：scenario key 透過 MockClaudeCodeService.FailScenario 傳遞（active scenario key 模式對齊 Stage 49）
        else if (scenario is "framework_kickoff_consensus_round1"
                          or "framework_kickoff_consensus_round2"
                          or "framework_kickoff_max_iter"
                          or "framework_kickoff_escalate"
                          or "framework_kickoff_crash_recovery")
            MockClaudeCodeService.FailScenario = scenario;
        // ── Stage 51 v4 漸進遷移第三步：framework HITL 中途介入 4 個 Mock 場景 ──
        // Christ 線下驗收時必須先 toggle Dashboard → 系統設定 → ① 使用 MS Agent Framework Kickoff Meeting = ON
        // 且 ② 使用 MS Agent Framework HITL（Kickoff 中途介入試點）= ON，否則試點 flag 不影響 framework Kickoff
        // 機制：scenario key 透過 MockClaudeCodeService.FailScenario 傳遞（active scenario key 模式對齊 Stage 49/50）
        // apply / cancel / crash_during_wait 在 group 建立後立刻設 trigger flag（避免 Round 1 跑完前 race condition）
        else if (scenario is "framework_kickoff_mid_interrupt_apply"
                          or "framework_kickoff_mid_interrupt_cancel"
                          or "framework_kickoff_mid_interrupt_crash_during_wait"
                          or "framework_kickoff_mid_interrupt_no_trigger")
            MockClaudeCodeService.FailScenario = scenario;
        // ── Stage 52 v4 漸進遷移第四步：framework Design Meeting 6 個 Mock 場景 ──
        // Christ 線下驗收時必須先 toggle Dashboard → 系統設定 → 使用 MS Agent Framework Design Meeting = ON
        // 否則 feature flag 為 false，DesignMeetingService 走 legacy path，這 6 個場景就跟一般 new_feature_with_proposal 等價
        // 機制：scenario key 透過 MockClaudeCodeService.FailScenario 傳遞（active scenario key 模式對齊 Stage 49/50/51）
        // 6 場景全部 issuesJson < 8 + plan < 500 + no phase 標記 → DesignSplitProposalEvaluator 規則層 0 觸發 → fall through fire Dev_plan
        // （Stage 52 不重驗 split proposal 路徑，由 Stage 46 既有 Mock 驗證）
        else if (scenario is "framework_design_consensus_round1"
                          or "framework_design_consensus_round2"
                          or "framework_design_needs_adjustment_approved"
                          or "framework_design_needs_adjustment_needs_meeting"
                          or "framework_design_no_demi"
                          or "framework_design_crash_recovery_during_round")
            MockClaudeCodeService.FailScenario = scenario;
        // Stage 53A v4 漸進遷移第五步：framework Pipeline 6 場景（Aria 方案 C 拍板：Pipeline 從 Dev_plan 階段啟動）
        // 機制：scenario key 透過 MockClaudeCodeService.FailScenario 傳遞 — Kickoff/Design 階段 Mock 走 default consensus（legacy path），
        // Dev_plan 起進 Pipeline framework path（feature flag UseFrameworkPipeline=true 時 FireOneStepAsync line 461 第三條分流接管）
        else if (scenario is "framework_pipeline_happy_path"
                          or "framework_pipeline_dev_plan_resume"
                          or "framework_pipeline_dev_resume"
                          or "framework_pipeline_qa_no_tests"
                          or "framework_pipeline_reviewer_fallback"
                          or "framework_pipeline_dev_plan_failed_escalate")
            MockClaudeCodeService.FailScenario = scenario;
        // ── Stage 53B v4 漸進遷移第六步：framework Pipeline 4 子流程 + 5 fallback 移除 6 場景 ──
        // 對應 53B 子項 10 — fix loop / appeal / QA fix loop / intervention 真跑通
        // 機制：scenario key 透過 FailScenario 傳遞 + MockClaudeCodeService 內 round counter 控制動態 routing
        else if (scenario is "framework_pipeline_fix_loop_recover_round1"
                          or "framework_pipeline_fix_loop_max_iter"
                          or "framework_pipeline_dev_blocker_appeal"
                          or "framework_pipeline_qa_no_tests_dynamic"
                          or "framework_pipeline_reviewer_fallback_dynamic"
                          or "framework_pipeline_fix_loop_crash_recovery")
        {
            MockClaudeCodeService.FailScenario = scenario;
            MockClaudeCodeService.ResetScenarioRoundCounters();  // Stage 53B：清 round counter 確保新 run 從 Round 1 開始
        }
        // ── Stage 54 v4 漸進遷移第七步：framework Recovery 升級 + idempotency 驗證 2 新 scenario alias ──
        // 觸發既有 Mock 邏輯 + 提供驗收明確命名（plan 對應場景 D / G）
        // - framework_design_crash_recovery_issue_idempotency ⭐：對齊 framework_design_consensus_round1 邏輯，
        //   Forge 線下可手動 docker restart 並驗 LastIssueCreatedRound marker + GitHub Issue 數量不重複
        // - pipeline_dev_blocker_retry_idempotency ⭐：對齊 framework_pipeline_dev_blocker_appeal 邏輯，
        //   驗 MarkGroupDoneOrIntervention 修法後 Round 2 success → group.Status=done 不誤判 needs_intervention
        else if (scenario == "framework_design_crash_recovery_issue_idempotency")
            MockClaudeCodeService.FailScenario = "framework_design_consensus_round1";
        else if (scenario == "pipeline_dev_blocker_retry_idempotency")
        {
            MockClaudeCodeService.FailScenario = "framework_pipeline_dev_blocker_appeal";
            MockClaudeCodeService.ResetScenarioRoundCounters();
        }
        // ── Stage 55A v4 漸進遷移第八步：Kickoff/Design 整合到 Pipeline framework 4 alias ──
        // 議題 G3 解法 — Pipeline 從 Kickoff 階段啟動，Pipeline KickoffStage / DesignStage Executor 接管 finalize
        // 對應驗收場景 B/C/D/E（對齊既有 Mock 邏輯 + 提供驗收明確命名）：
        //   - framework_pipeline_kickoff_to_merge_full ⭐：Pipeline 從 Kickoff 啟動跑通完整 pipeline（場景 B）
        //   - framework_pipeline_kickoff_crash_recovery ⭐：Kickoff 階段 Crash Recovery（場景 C）
        //   - framework_pipeline_design_crash_recovery_issue_idempotency_v2 ⭐：Design 階段 Issue idempotency（場景 D）
        //   - framework_pipeline_subtask_chain ⭐：sub-task 整合 Pipeline framework path（場景 E）
        else if (scenario == "framework_pipeline_kickoff_to_merge_full")
            MockClaudeCodeService.FailScenario = "framework_pipeline_happy_path";
        else if (scenario == "framework_pipeline_kickoff_crash_recovery")
            MockClaudeCodeService.FailScenario = "framework_pipeline_happy_path";
        else if (scenario == "framework_pipeline_design_crash_recovery_issue_idempotency_v2")
            MockClaudeCodeService.FailScenario = "framework_design_consensus_round1";
        else if (scenario == "framework_pipeline_subtask_chain")
            MockClaudeCodeService.FailScenario = "split_task_propose_accept";

        var (workflowType, workflowLabel, initialStep) = scenario switch
        {
            "bug_fix"                       => (WorkflowType.BugFix,          "Bug 修復",                  "Dev"),
            "tech_improvement"              => (WorkflowType.TechImprovement, "技術改善",                  "Dev_plan"),
            "new_feature_with_proposal"     => (WorkflowType.NewFeature,      "新功能（含提案）",           "Dev_plan"),
            "fail_review"                   => (WorkflowType.NewFeature,      "失敗測試-ReviewAppeal",     "Dev"),
            "fail_qa"                       => (WorkflowType.NewFeature,      "失敗測試-QA失敗",           "Dev"),
            "fail_dev_plan"                 => (WorkflowType.NewFeature,      "失敗測試-DevPlanAppeal",     "Dev_plan"),
            "review_skipped"                => (WorkflowType.NewFeature,      "Vera 略過驗收",             "Dev"),
            // Stage 43
            "dev_plan_fail_retry"           => (WorkflowType.NewFeature,      "失敗測試-DevPlanRetry",     "Dev_plan"),
            "dev_plan_fail_escalate"        => (WorkflowType.NewFeature,      "失敗測試-DevPlanEscalate",  "Dev_plan"),
            "dev_failed_intervention"       => (WorkflowType.NewFeature,      "失敗測試-DevFailedIntervention", "Dev"),
            "qa_failed_fix_then_intervention" => (WorkflowType.NewFeature,    "失敗測試-QAFixIntervention",  "Dev"),
            // Stage 45：3 個暫停場景
            "pause_at_kickoff_end"               => (WorkflowType.NewFeature, "暫停測試-KickoffEnd",         "Kickoff"),
            "pause_during_dev"                   => (WorkflowType.NewFeature, "暫停測試-Dev進行中",          "Dev"),
            "pause_resume_with_boss_interaction" => (WorkflowType.NewFeature, "暫停測試-跨BossInteraction",  "Dev"),
            // Stage 46-FF 三十五：拆 task 2 個場景，從 Kickoff 起跑（必須完整跑會議才到 Design 拆 task 階段）
            "split_task_propose_accept"            => (WorkflowType.NewFeature, "拆task-採納成功",             "Kickoff"),
            "split_task_subtask_fail_intervention" => (WorkflowType.NewFeature, "拆task-Phase2失敗",          "Kickoff"),
            // Stage 49 v4 漸進遷移：framework Appeal Loop 5 個場景（initialStep="Dev" 直接進入 Reviewer 路徑驗 ReviewAppeal Workflow）
            "framework_appeal_loop_fast_approve"      => (WorkflowType.NewFeature, "Framework-FastApprove",   "Dev"),
            "framework_appeal_loop_max_iter_approve"  => (WorkflowType.NewFeature, "Framework-MaxIterApprove", "Dev"),
            "framework_appeal_loop_max_iter_reject"   => (WorkflowType.NewFeature, "Framework-MaxIterReject",  "Dev"),
            "framework_appeal_loop_max_iter_escalate" => (WorkflowType.NewFeature, "Framework-MaxIterEscalate","Dev"),
            "framework_appeal_loop_crash_recovery"    => (WorkflowType.NewFeature, "Framework-CrashRecovery",  "Dev"),
            // Stage 50 v4 漸進遷移第二步：framework Kickoff 5 個場景（initialStep="Kickoff" 從 Kickoff 起跑驗 fan-out/fan-in Workflow）
            "framework_kickoff_consensus_round1" => (WorkflowType.NewFeature, "FrameworkKickoff-ConsensusR1",  "Kickoff"),
            "framework_kickoff_consensus_round2" => (WorkflowType.NewFeature, "FrameworkKickoff-ConsensusR2",  "Kickoff"),
            "framework_kickoff_max_iter"         => (WorkflowType.NewFeature, "FrameworkKickoff-MaxIter",      "Kickoff"),
            "framework_kickoff_escalate"         => (WorkflowType.NewFeature, "FrameworkKickoff-Escalate",     "Kickoff"),
            "framework_kickoff_crash_recovery"   => (WorkflowType.NewFeature, "FrameworkKickoff-CrashRecovery","Kickoff"),
            // Stage 51 HITL 試點 4 場景（從 Kickoff 起跑驗 framework HITL pause-resume lifecycle）
            "framework_kickoff_mid_interrupt_apply"              => (WorkflowType.NewFeature, "FrameworkKickoff-MidInterruptApply",         "Kickoff"),
            "framework_kickoff_mid_interrupt_cancel"             => (WorkflowType.NewFeature, "FrameworkKickoff-MidInterruptCancel",        "Kickoff"),
            "framework_kickoff_mid_interrupt_crash_during_wait"  => (WorkflowType.NewFeature, "FrameworkKickoff-MidInterruptCrashWait",     "Kickoff"),
            "framework_kickoff_mid_interrupt_no_trigger"         => (WorkflowType.NewFeature, "FrameworkKickoff-MidInterruptNoTrigger",     "Kickoff"),
            // Stage 52：framework Design Meeting 6 場景（從 Kickoff 起跑，Kickoff 階段 Mock 走 default consensus → Design 階段 Mock 依 FailScenario 切換）
            "framework_design_consensus_round1"                  => (WorkflowType.NewFeature, "FrameworkDesign-ConsensusR1",                "Kickoff"),
            "framework_design_consensus_round2"                  => (WorkflowType.NewFeature, "FrameworkDesign-ConsensusR2",                "Kickoff"),
            "framework_design_needs_adjustment_approved"         => (WorkflowType.NewFeature, "FrameworkDesign-AdjustmentApproved",         "Kickoff"),
            "framework_design_needs_adjustment_needs_meeting"    => (WorkflowType.NewFeature, "FrameworkDesign-AdjustmentNeedsMeeting",     "Kickoff"),
            "framework_design_no_demi"                           => (WorkflowType.NewFeature, "FrameworkDesign-NoDemi",                     "Kickoff"),
            "framework_design_crash_recovery_during_round"       => (WorkflowType.NewFeature, "FrameworkDesign-CrashRecovery",              "Kickoff"),
            // Stage 53A：framework Pipeline 6 場景（Aria 方案 C 拍板：從 Kickoff 起跑，Kickoff/Design 走 legacy/framework，Dev_plan 起進 Pipeline framework path）
            "framework_pipeline_happy_path"        => (WorkflowType.NewFeature, "FrameworkPipeline-HappyPath",       "Kickoff"),
            "framework_pipeline_dev_plan_resume"   => (WorkflowType.NewFeature, "FrameworkPipeline-DevPlanResume",   "Kickoff"),
            "framework_pipeline_dev_resume"        => (WorkflowType.NewFeature, "FrameworkPipeline-DevResume",       "Kickoff"),
            "framework_pipeline_qa_no_tests"       => (WorkflowType.NewFeature, "FrameworkPipeline-QaNoTests",       "Kickoff"),
            "framework_pipeline_reviewer_fallback" => (WorkflowType.NewFeature, "FrameworkPipeline-ReviewerFallback","Kickoff"),
            "framework_pipeline_dev_plan_failed_escalate"  => (WorkflowType.NewFeature, "FrameworkPipeline-DevPlanFailedEscalate", "Kickoff"),
            // Stage 53B：4 子流程 framework 化 6 場景（initialStep="Kickoff" 完整跑流程）
            "framework_pipeline_fix_loop_recover_round1"  => (WorkflowType.NewFeature, "FrameworkPipeline-FixLoopRecoverR1",     "Kickoff"),
            "framework_pipeline_fix_loop_max_iter"        => (WorkflowType.NewFeature, "FrameworkPipeline-FixLoopMaxIter",       "Kickoff"),
            "framework_pipeline_dev_blocker_appeal"       => (WorkflowType.NewFeature, "FrameworkPipeline-DevBlockerAppeal",     "Kickoff"),
            "framework_pipeline_qa_no_tests_dynamic"      => (WorkflowType.NewFeature, "FrameworkPipeline-QaNoTestsDynamic",     "Kickoff"),
            "framework_pipeline_reviewer_fallback_dynamic"=> (WorkflowType.NewFeature, "FrameworkPipeline-ReviewerFallbackDyn",  "Kickoff"),
            "framework_pipeline_fix_loop_crash_recovery"  => (WorkflowType.NewFeature, "FrameworkPipeline-FixLoopCrashRecovery", "Kickoff"),
            // Stage 54：framework Recovery 升級 + idempotency 驗證 alias
            "framework_design_crash_recovery_issue_idempotency" => (WorkflowType.NewFeature, "Stage54-DesignIssueIdempotency", "Kickoff"),
            "pipeline_dev_blocker_retry_idempotency"            => (WorkflowType.NewFeature, "Stage54-DevBlockerRetryIdempotency", "Kickoff"),
            // Stage 55A：Pipeline 從 Kickoff 啟動 4 alias（場景 B/C/D/E）— 議題 G3 解法驗證
            "framework_pipeline_kickoff_to_merge_full"          => (WorkflowType.NewFeature, "Stage55A-KickoffToMergeFull",        "Kickoff"),
            "framework_pipeline_kickoff_crash_recovery"         => (WorkflowType.NewFeature, "Stage55A-KickoffCrashRecovery",      "Kickoff"),
            "framework_pipeline_design_crash_recovery_issue_idempotency_v2" => (WorkflowType.NewFeature, "Stage55A-DesignIssueIdempotencyV2", "Kickoff"),
            "framework_pipeline_subtask_chain"                  => (WorkflowType.NewFeature, "Stage55A-SubtaskChain",              "Kickoff"),
            _                               => (WorkflowType.NewFeature,      "新功能",                    "Dev_plan")
        };

        var title   = string.IsNullOrWhiteSpace(customTitle)
            ? $"[MOCK] 模擬{workflowLabel}任務（{DateTime.Now:HH:mm:ss}）"
            : customTitle!;
        var project = string.IsNullOrWhiteSpace(customProject) ? _gitHub.DefaultRepo : customProject!;

        logger.LogInformation("[MockMode] MockScenarioService：scenario={Scenario}，title={Title}", scenario, title);

        if (scenario == "new_feature_with_proposal")
        {
            return await RunProposalFlowAsync(title, project, workflowType, ct);
        }

        // 一般流程：建立 TaskGroup，直接觸發起始步驟
        var group = await taskGroupService.CreateGroupAsync(
            title, project, workflowType,
            issueUrlsJson: "[\"https://github.com/mock/repo/issues/1\"]",
            uiSpecContent: "[MOCK] 模擬 UI 規格，供 Mock Mode 測試使用。",
            cancellationToken: ct);

        // Stage 45：依 scenario 設 PausePoint，FireStepsAsync 進入時偵測到 (groupId, beforeStep) 匹配自動暫停
        if (scenario == "pause_at_kickoff_end")
            MockClaudeCodeService.PausePoint = (group.Id, "Design");        // Kickoff done → 即將 fire Design 時暫停
        else if (scenario == "pause_during_dev")
            MockClaudeCodeService.PausePoint = (group.Id, "Reviewer");      // Dev done → 即將 fire Reviewer 時暫停（被動延遲）
        else if (scenario == "pause_resume_with_boss_interaction")
            MockClaudeCodeService.PausePoint = (group.Id, "Reviewer");      // 老闆按 dev_intervention_skip → fire Reviewer 時暫停
        // Stage 49：framework_appeal_loop_crash_recovery 場景
        // 觸發點對齊「framework path round 2 期間 simulate crash」拍板（路線 ③ pause + 手動 docker restart）
        // PausePoint 設在 Dev 完成後（即將 fire Reviewer），Christ 線下驗收按以下步驟：
        //   1. 跑此 Mock，等流程觸發 Vera Appeal round 1 開始（Bot log 觀察 framework path 啟動）
        //   2. 在 Dashboard 操作中心觀察 IsPaused 狀態，確認流程暫停
        //   3. 手動 `docker compose restart aiteam-bot`
        //   4. Bot 重啟後 RecoverStuckFrameworkAppealsAsync 掃 FrameworkAppealStateJson != null
        //   5. 觀察 framework recovery log + 流程是否能繼續（暫降級策略：清 marker 後重觸發 entry）
        else if (scenario == "framework_appeal_loop_crash_recovery")
            MockClaudeCodeService.PausePoint = (group.Id, "Reviewer");
        // Stage 51：HITL 中途介入 — apply / cancel / crash_during_wait 在 group 建立後立刻設 trigger flag
        // （MidInterruptCheckExecutor 在 Petra Round 1 結束後消耗）。no_trigger 不設，驗證 baseline 不影響。
        else if (scenario is "framework_kickoff_mid_interrupt_apply"
                          or "framework_kickoff_mid_interrupt_cancel"
                          or "framework_kickoff_mid_interrupt_crash_during_wait")
        {
            midInterruptTriggerStore.Set(group.Id);
            logger.LogInformation(
                "[MockMode][Stage51] Mid-Interrupt trigger flag 已預設（GroupId={Id}，scenario={Scenario}）",
                group.Id, scenario);
        }

        _ = Task.Run(() => taskGroupService.FireStepsAsync(group, [new WorkflowStep(initialStep)]));

        var emoji = scenario switch
        {
            "bug_fix"                                              => "🐛",
            "tech_improvement"                                     => "🔧",
            "framework_appeal_loop_fast_approve"                   => "🚀",
            "framework_appeal_loop_max_iter_approve"               => "🚀",
            "framework_appeal_loop_max_iter_reject"                => "🚀",
            "framework_appeal_loop_max_iter_escalate"              => "🚀",
            "framework_appeal_loop_crash_recovery"                 => "🚀",
            // Stage 50：framework Kickoff 5 場景（v4 漸進遷移第二步）
            "framework_kickoff_consensus_round1"                   => "🤝",
            "framework_kickoff_consensus_round2"                   => "🤝",
            "framework_kickoff_max_iter"                           => "🤝",
            "framework_kickoff_escalate"                           => "🤝",
            "framework_kickoff_crash_recovery"                     => "🤝",
            // Stage 51：HITL 中途介入試點 4 場景
            "framework_kickoff_mid_interrupt_apply"                => "✏️",
            "framework_kickoff_mid_interrupt_cancel"               => "✏️",
            "framework_kickoff_mid_interrupt_crash_during_wait"    => "✏️",
            "framework_kickoff_mid_interrupt_no_trigger"           => "✏️",
            _                                                       => "✨"
        };

        // Stage 49 v4 漸進遷移：framework Mock 場景啟動時提示 Christ 確認 feature flag
        var frameworkHint = scenario.StartsWith("framework_appeal_loop_")
            ? "\n⚠️ **v4 漸進遷移驗收**：請先於 Dashboard → 系統設定 → **使用 MS Agent Framework Appeal Loop = ON**，否則此 Mock 走 legacy path 無法驗 framework Workflow。"
            : scenario.StartsWith("framework_kickoff_mid_interrupt_")
                ? "\n⚠️ **v4 漸進遷移第三步試點驗收**：請先於 Dashboard → 系統設定 → 同時啟用 ① **使用 MS Agent Framework Kickoff Meeting = ON** 與 ② **使用 MS Agent Framework HITL（Kickoff 中途介入試點） = ON**，否則此 Mock 走 legacy KickoffMeetingService 或試點 flag 不生效。" +
                  (scenario == "framework_kickoff_mid_interrupt_apply"
                      ? "\n💡 場景 B 流程：trigger 已預設 → Round 1 Petra 結束會 emit RequestInfoEvent → Discord/Dashboard 出 BossInteraction「✏️ 套用修改」按鈕 → 點按鈕後輸入修改指引文字 → workflow resume Round 2 帶指引 → consensus。"
                      : scenario == "framework_kickoff_mid_interrupt_cancel"
                          ? "\n💡 場景 E 流程：trigger 已預設 → Round 1 結束 emit RequestInfoEvent → 在 Discord/Dashboard 點「取消介入」→ workflow resume Round 2 不帶指引 → consensus。"
                          : scenario == "framework_kickoff_mid_interrupt_crash_during_wait"
                              ? "\n💡 場景 D 流程：trigger 已預設 → Round 1 結束 emit RequestInfoEvent + BossInteraction → 不回應 → 手動 `docker compose restart aiteam-bot` → 重啟後 Recovery 識別「等待人類回應」不清 marker → 重啟後在 Discord 點「套用修改」→ workflow resume → consensus。"
                              : scenario == "framework_kickoff_mid_interrupt_no_trigger"
                                  ? "\n💡 場景 F 流程：未預設 trigger → workflow 跑完 Round 1 consensus（無 RequestInfoEvent emit），驗證試點不影響 default behavior。"
                                  : "")
                : scenario.StartsWith("framework_kickoff_")
                    ? "\n⚠️ **v4 漸進遷移第二步驗收**：請先於 Dashboard → 系統設定 → **使用 MS Agent Framework Kickoff Meeting = ON**，否則此 Mock 走 legacy KickoffMeetingService。" +
                      (scenario == "framework_kickoff_crash_recovery"
                          ? "\n💡 場景 C 流程：等 Round 2 4 Agent 並行進行中（log 觀察）→ 手動 `docker compose restart aiteam-bot` → 觀察 Bot 啟動時 [Stage50-CrashRecoveryFrameworkKickoff] log 與降級策略。"
                          : "")
                    : "";

        return (true,
            $"{emoji} **[MOCK] {workflowLabel}流程已啟動**\n" +
            $"任務：`{title}`\n" +
            $"起始步驟：`{initialStep}` → 後續由 Orchestrator 自動推進\n" +
            $"請至 Dashboard → 任務中心 觀察流程進度，所有輸出將標記 `[MOCK]`。" +
            frameworkHint);
    }

    /// <summary>
    /// new_feature_with_proposal 情境：建立 CEO 提案 Embed + 登記 pending confirmation + 建立 BossInteraction。
    /// 與 Discord 觸發完全等價（共用 CommandHandler._pendingConfirmations 字典）。
    /// </summary>
    private async Task<(bool ok, string message)> RunProposalFlowAsync(
        string title,
        string project,
        WorkflowType workflowType,
        CancellationToken ct)
    {
        var ceoChannel = FindCeoChannel();
        if (ceoChannel is null)
            return (false, "❌ 找不到 CEO 頻道，無法發送提案書。請確認 DiscordSettings.Channels.CeoChannel 設定正確。");

        // 1. 建立 TaskGroup（無 issueUrls/uiSpec，設計階段再填入）
        var group = await taskGroupService.CreateGroupAsync(title, project, workflowType, cancellationToken: ct);

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var projectId = string.IsNullOrWhiteSpace(project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(project);

        // 2. 建立 CEO 主任務（pending，掛 GroupId）
        var ceoTask = new TaskItem
        {
            Title         = title,
            Description   = "[MOCK] 模擬新功能提案流程",
            TriggeredBy   = "Discord",
            AssignedAgent = "CEO",
            Status        = "pending",
            GroupId       = group.Id,
            ProjectId     = projectId,
        };
        taskRepo.Add(ceoTask);
        await taskRepo.SaveAsync(ct);
        var ceoTaskId = ceoTask.Id;

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = ceoTask.Id,
            GroupId   = group.Id,
            Title     = ceoTask.Title,
            AgentName = ceoTask.AssignedAgent,
            Status    = "pending"
        });

        // 3. 傳送與真實流程相同的提案 Embed + 確認按鈕
        const string description = "[MOCK] 模擬新功能需求，供 Mock Mode 測試流程使用。";
        var proposalEmbed = ButtonCallbackRouter.BuildProposalEmbed(title, description);
        var proposalMsg   = await ceoChannel.SendMessageAsync(embed: proposalEmbed, components: ButtonCallbackRouter.BuildProposalConfirmButtons());

        // 4. 登記 _pendingConfirmations（延遲解析 CommandHandler 避免循環依賴）
        var commandHandler = serviceProvider.GetRequiredService<CommandHandler>();
        commandHandler.RegisterProposalConfirmation(proposalMsg.Id, ceoTaskId, project, description);

        // 5. 建立 BossInteraction，讓 Dashboard 操作中心顯示待處理卡片
        await using var interactionScope = serviceProvider.CreateAsyncScope();
        var interactionSvc = interactionScope.ServiceProvider.GetRequiredService<InteractionService>();
        _ = interactionSvc.CreateInteractionAsync(
            "proposal",
            title:                title,
            description:          description,
            project:              project,
            agentName:            null,
            availableActionsJson: InteractionService.ProposalActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId   = ceoChannel.Id.ToString(),
                taskId      = ceoTaskId.ToString(),
                project,
                description
            }),
            discordMessageId: (decimal)proposalMsg.Id,
            taskItemId:       ceoTaskId);

        return (true,
            $"📋 **[MOCK] 新功能（含提案）流程已啟動**\n" +
            $"任務：`{title}`\n" +
            $"請至 <#{ceoChannel.Id}> 或 Dashboard 操作中心點擊確認，流程才會繼續。");
    }

    /// <summary>依 DiscordSettings.Channels.CeoChannel（名稱）從 Guild 取得頻道。</summary>
    private IMessageChannel? FindCeoChannel()
    {
        if (!ulong.TryParse(_discord.GuildId, out var guildId)) return null;
        return discordClient.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == _discord.Channels.CeoChannel);
    }
}
