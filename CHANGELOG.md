# Changelog

本專案所有重要變更紀錄於此檔。

格式參照 [Keep a Changelog 1.1.0](https://keepachangelog.com/zh-TW/1.1.0/)，版本號遵循 [Semantic Versioning](https://semver.org/lang/zh-TW/)。
工作單位為 **Stage**，每完成一個 Stage 通常對應 minor 版本 bump。每條 entry 的細節見對應 Stage Roadmap；更深的實作 commit 訊息見 git log。

未實作功能候選見 [Future_Feature.md](docs/planning/Future_Feature.md)。

---

## [Unreleased]

- **🎉 Stage 54 v4 漸進遷移第七步完成**：[Stage 54](docs/planning/Stage_54_Roadmap.md) Crash Recovery 全切 framework Checkpointing + 4 CheckpointStore 抽 base class + B2 round-aware idempotency + Stage 53B follow-up 搭車 — **8 場景驗收全綠 + 1 follow-up bug 修復（Forge 自驗時自抓自修）**。**7/8 達成**。
- **下個動作候選**：① **Stage 55 戰略級收尾**（Kickoff/Design + sub-task 整合 + BossInteraction 切 framework HITL + 移除 J1 6 hooks）/ ② FF 四十三 / ③ FF 四十二 / ④ Stage 53B follow-up #3 立 FF（Dashboard MockScenarioCard 補 Stage 49-53A framework_* 場景）
- **8 Stage 遷移路線進度**：✅ **49 Appeal loop** ✅ **50 Kickoff Meeting** ✅ **51 framework HITL 試點** ✅ **52 Design Meeting** ✅ **53A NewFeature happy path** ✅ **53B 子流程 + 5 fallback 移除** ✅ **54 Crash Recovery 全切 + base class + idempotency** → 55 戰略級收尾
- **FF 三十六 Phase B 動態流程架構**：路線 = **Stage 55 後再評估**（macro pipeline 7/8 達成）
- **FF 三十二 ✅** / **FF 三十三 ✅** / **FF 三十四 ✅ + FF 三十七 ✅** / **FF 三十五 ✅ + FF 三十九 ✅** / **FF 四十七 ✅ + FF 十一 ✅** / **FF 四十九 ✅** / **Stage 49 v4 首發 ✅** / **Stage 50 v4 第二步 ✅** / **Stage 51 v4 第三步 ✅ ⭐** / **Stage 52 v4 第四步 ✅** / **Stage 53A v4 第五步 ✅ ⭐** / **Stage 53B v4 第六步 ✅ ⭐** / **Stage 54 v4 第七步 ✅**
- **新立 FF 四十 / 四十一 / 四十二**（Stage 46 驗收期 follow-up 採集）
- **Stage 48 揭露候選 FF**（待 Christ 拍板）：Windows-only Process.Start + UseShellExecute=false 不 honor PATHEXT for `.cmd`（production hardening FF）

---

## [3.41.0] — 2026-05-04 — [Stage 54](docs/planning/Stage_54_Roadmap.md) v4 漸進遷移第七步 Crash Recovery 全切 + 4 CheckpointStore base class + idempotency

純機制升級 + 重構 + idempotency 加固，無新功能。4 件事一氣呵成：① **抽 4 CheckpointStore base class**（833 → 360 行 **-473 淨減**，`FrameworkCheckpointStoreBase<TStore>` generic 對齊既有 logger category 不破壞 production logging）② **3 router RecoverStuck*Async 升級 ResumeStreamingAsync**（對齊 Stage 53A Pipeline 議題 12 既有 know-how — Appeal 兩種 workflow 用 ScanForIntProperty `kind` 切換 / Kickoff 保留 Stage 51 試點 MidInterruptRequestPending check / Design 直接對齊 + R6 保守 OutputEvent 處理避免 Recovery 期間誤觸發 Discord 通知）③ **B2 round-aware idempotency 戰略級修正**（B1 → B2：Forge gate1 揭露 B1 用 `state.IssueUrls` check 會破壞 needs_adjustment 多輪業務 → Christ 重拍板「Adjustment 觸發都會踩，不是機率問題」→ TaskGroups 加 `LastIssueCreatedRound` int? + Migration `Stage54TaskGroupIssueCreatedMarker` + 4 處 check：DesignRosaPreWork Round 0 / DesignAdjustment Round N round-aware + Kickoff/Design CreateInteractionAsync 用既有 `BossInteractionRepository.GetLatestForGroupByTypeAsync` lookup）④ **Stage 53B follow-up 搭車**：#1 `MarkGroupDoneOrInterventionAsync` 廣義化「同 AssignedAgent newer success task 取代的舊 failed task」覆蓋 fix loop + dev_blocker 兩場景（Forge 揭露原 plan IsFixLoop=true 嚴格條件對 dev_blocker 場景無效）/ #2 `InteractionService` MockMode auto-approve hook（驗收期 fix `84bd874` source='dashboard' 對齊 InteractionProcessor 消費路徑）/ #4 `MockClaudeCodeService` 三 method [Obsolete] attribute（caller 已搬到 agent service early return）。**驗收 8 場景全綠**（A baseline / B Appeal Recovery / C Kickoff Recovery + idempotency / D ⭐ Design Recovery + Issue idempotency / E Stage 51 know-how 保留 / F Pipeline regression / G ⭐ DevStage [BLOCKED] retry idempotency / H MockMode auto-approve）+ Forge 自驗能力首次處理 follow-up bug（自跑場景 H 揭露 source 字串對齊問題自修）。**8 Stage 遷移 7/8 達成**，剩 Stage 55 戰略級收尾。詳見 Stage 54 Roadmap。commits：`36033f0`(主) + `84bd874`(fix) + `3c192e3`(驗收) + `82317cd`(v2.0)。

## [3.40.0] — 2026-05-03 — [Stage 53B](docs/planning/Stage_53B_Roadmap.md) ⭐ v4 漸進遷移第六步 子流程 + 5 fallback 移除

NewFeature 主路徑 + 子流程**完整 Pipeline framework 化**達成。11 議題拍板：A1 一個 Stage 全切 / B1 fix loop loop back / C1 現有 Pipeline 拓撲擴張 / D1 一次完成 4 子流程 + 5 fallback 移除 / E sub-task 沿用排除 / F 兩個必修都修（議題 G3 grep 紀律升級 + 議題 12 Agent task 層 mapping +1 entry）/ G1 spike 範圍極窄 / H1 沿用單一 UseFrameworkPipeline / I-mid 6 場景 / J1 既有 6 hooks 保留 / K1 mapping helper 保留 switch case 擴 6 entries。**核心實作**：① 新建 DevFixStageExecutor + Pipeline-DevFixCompletion PortId（5→6 RequestPort + 7→8 stage Executor）+ 4 record（DevFixStageBridge / DevFixCompletionRequest/Response + DevPlanRetryBridge / DevRetryBridge self-loop type-explicit Bridge record）② 4 stage Executor 加 routing：ReviewerStage fix loop（Petra fail → DevFixStage / FixIteration>=3 → SetInterventionAndYieldAsync）/ DevPlanStage appeal（call HandleDevPlanCompletedAsync 用 bool return + DevPlanRetryBridge self-loop）/ DevStage appeal + intervention（call HandleDevBlockerAsync 用 BlockerDecision + DevRetryBridge）/ QaStage QA fix loop（QaFixRound > 0 → DevFixStageBridge）③ **議題 F-1 16 處 skip 修正**（AppealOrchestrationService 11 處 + QaCoordinationService 5 處）+ HandleDevBlockerAsync signature `Task` → `Task<BlockerDecision>` 升級給 Pipeline 自接管 routing（3 caller backward compat）④ **5 fallback dispatch 全移除**（FrameworkPipelineRouter.FinalizePipelineAsync 留邊界 case dev_failed/qa_failed/qa_intervention/doc_failed/group_not_found + Completed 語義變更含 intervention，PipelineLoopResult comment 補完整⚠️ 紀錄）+ 4 stage Executor ClearMarkerAndFallbackAsync helper 移除 ⑤ 6 Mock 場景 dynamic（fix_loop_recover/max_iter/dev_blocker_appeal/qa_no_tests_dynamic ⭐/reviewer_fallback_dynamic ⭐/fix_loop_crash_recovery）+ MockClaudeCodeService scenario round counter + PmReviewService/PmRoutingService Mock 分支。**Forge 主動拍板**：QaCoordinationService Pipeline path 下 env_or_test_issue 視為 passed（skip 全部 side effects → Pipeline QaStage 重讀 group → DocStageBridge）。**驗收期 2 follow-up + 1 既有議題揭露**：① #1 7fbac77 — Mock 53B branches 搬到 3 agent service（ReviewerAgentService/DevAgentService/QaAgentService）MockMode early return 內（Stage 53B 主 commit 寫在 MockClaudeCodeService 但 agent early return bypass）② Dashboard MockScenarioCard 補 53B 6 場景 MudSelectItem ③ 既有議題：Pipeline 主 Workflow 跨 stage 共享 IServiceScopeFactory + 各 Executor 自 CreateAsyncScope。**驗收能力突破**：Stage 32 既有 `/internal/mock/scenario` HTTP API + docker exec psql auto-approve BossInteraction → **Forge 自驗全 6 場景含 SIGTERM/SIGKILL Crash Recovery**（Christ 線下實跑模式從「必要」轉「選擇性」）。新建 1 Executor + 改 8 既有檔。**Aria 校準錨 ×0.88**（578K vs Charter 中位 655K，混合型第 6 資料點 mid 帶中段；6 資料點區間 ×0.73-1.25 拆 Stage 守區間精神持續驗證 — 議題 F-1 規劃前期 grep 紀律升級成功 + Forge 自驗能力突破）。詳見 Stage 53B Roadmap。commits：`cc07fcf`(主) + `49f4d5a`/`7fbac77` 兩 fix + `6d473db`(驗收期紀錄)。

## [3.39.0] — 2026-05-03 — [Stage 53A](docs/planning/Stage_53A_Roadmap.md) ⭐ v4 漸進遷移第五步 macro pipeline NewFeature happy path

v4 路線最大遷移點之一：macro-orchestration framework 化首次達成（vs Stage 49-52「節點內部」單層 framework）。**Aria Session A 子項 5 實作期揭露議題 G3 假設失誤**（inner FrameworkKickoffRouter / FrameworkDesignRouter 的 post-meeting actions 跟 Pipeline 推進職責衝突）→ 即時跨 session 拍板 **方案 C 範圍縮小**：53A 範圍從「整個 pipeline」縮成「Pipeline 從 Dev_plan 階段啟動 + Kickoff/Design 留 legacy」（規模 -40% 守混合型 ×0.96-1.25 區間 + 戰略價值 ~70% 保留 + Stage 55 收尾統一整合）。**v4 路線 7→8 Stage**（53 拆成 53A happy path + 53B fix loop / appeal 子流程）。核心實作：① 9 stage Bridge record + 5 stage 獨立 RequestPort PortId/Type 對齊 Stage 52 fix#2 type-explicit 紀律 ② FrameworkPipelineRouter 4 method（HandlePipelineAsync 主入口 + ResumeAfterAgentAsync J1 yield-resume callback resume + RecoverStuckFrameworkPipelineAsync **議題 12 升級 ResumeStreamingAsync rehydrate**（不採降級重跑）+ FinalizePipelineAsync 9 fallback dispatch 主動 call legacy）③ 入口分流兩處（FireOneStepAsync line 461 加 Dev_plan 第三條 single point of entry + sub-task ParentGroupId 排除 + HandleAgentCompletedAsync line 168 後 callback resume 議題 10 修法保留 DB 欄位寫入）④ F-α 4 既有 framework router 排除條件追加 PipelineFrameworkStateJson == null（首次跨 Stage 修改既有 framework code）⑤ I2 fallback to legacy 反向設計（5 fallback 點主動 call 沿用 Stage 51 know-how，臨時設計 Stage 55 統一移除）。**驗收 6 場景**：A baseline / B happy_path / C dev_plan_resume / D dev_resume（含 SIGTERM/SIGKILL 兩跑）4 dynamic 全綠 + E qa_no_tests / F reviewer_fallback Mock 特殊行為留 **Stage 53B 補 dynamic**（Christ 拍板 2 caveat）。**驗收期 4 follow-up**：① #1 7a100e7 — **議題 G3 同類問題在 QA 重演**（QaCoordinationService.HandleQaCompletedAsync passed 路徑內部 fire Doc 衝突，Aria 規劃前期 grep 不夠深 — 規劃紀律必升級為「對所有既有 service finalize/post-completion actions 都 grep」）② #2 7a100e7 — NotifyMergeStage 補 MarkGroupDoneOrInterventionAsync ③ #3 留 Stage 53B（Mock 場景 E/F 跟 fix loop 一起做）④ #4 dc5ff37 — **Pipeline Recovery 接管 Bot restart 邊界 failed Agent task requeue**（Pipeline framework + AgentQueueService 整合 unknown，議題 12 framework state 層已驗但漏 Agent task 層 — Stage 53B/54 必補設計）。新建 14 檔 ~1500 LoC（Workflows/Pipeline/ 13 + FrameworkPipelineRouter）+ 改 9 既有檔 + Migration `Stage53ATaskGroupPipelineFrameworkState`。**Aria 校準錨 ×0.73**（562K vs Charter 中位 770K，混合型第 5 資料點 mid 帶下半 — 區間擴展為 ×0.73-1.25；方案 C 拆 Stage 縮 -40% + Stage 51 既有 know-how 直接複用 + 0 Aria gate1 揭露問題 + Forge 全程一個 session 跑沒拆 Session 三因素疊加）。**Christ 拍板 production 保留 UseFrameworkPipeline=true**（Pipeline framework path 全自動恢復機制已驗，Christ 真實 NewFeature 任務走 framework path）。詳見 Stage 53A Roadmap。commits：`296d44e`(A 1/2/3/4/6) + `b23b760`(A 5) + `4ec7a35`(B 7) + `b22a8b0`(C 8/9/10) + `7a100e7`(fix #1+#2) + `dc5ff37`(fix #4) + `c424f67`(v2.1)。

## [3.38.0] — 2026-05-03 — [Stage 52](docs/planning/Stage_52_Roadmap.md) v4 漸進遷移第四步 Design Meeting B3 路線

議題 A 拆 Stage：原 v4 路線 Stage 52 含「Design Meeting + WorkflowEngine pipeline」一氣呵成，Aria 規劃時拆 Stage 守混合型 ×0.96-1.25 區間精神 — Stage 52 = Design Meeting B3 only / Stage 53 = WorkflowEngine macro / Stage 54 = Crash Recovery / Stage 55 = 收尾切 BossInteraction（v4 路線 6→7 Stage）。Design Meeting 三層 Stage 50 沒踩過的拓撲擴展：① 條件式 Demi（needsDemi=false short-circuit pass-through 對齊 Stage 51 MidInterruptCheckExecutor pattern）② needs_adjustment B2 子流程（DesignAdjustmentExecutor 兩出口：approved → DesignPlanExecutor.HandleAdjustmentApprovedAsync 直接 wrap / needs_meeting → escalate 邊界先處理對齊 legacy line 290-298）③ 拆 task 提案 router 後置（C2 抽 DesignSplitProposalEvaluator helper SoT 給 framework + legacy 共用，避免 Stage 46-FF 三十五 戰略級機制漂移）。Spike F1/F2 兩項全綠。**驗收期 2 follow-up**：① fix#1 Mock agentName 識別補 Petra plan + adjustment eval 兩 prompt（Stage 50 踩坑 #11 預警命中，commit `806b22b`）② **fix#2 戰略級 framework 1.3.0 行為揭露**（commit `27ce0b7`）：「AddEdge type-based dispatch 不 source-aware」— 原計畫 DesignPlanExecutor 雙 [MessageHandler] 接 DesignPetraVerdict + DesignAdjustmentApproved 被 needs_meeting 路徑送的 verdict 誤觸發，修法拆成 DesignPlanExecutor + DesignAdjustmentPlanExecutor 讓 type filter 自然分流（Stage 53+ 拓撲設計新預警）。**6 場景全綠**（A baseline / B consensus_round1 / C ⭐ adjustment_approved / D ⭐ adjustment_needs_meeting / E ⭐ no_demi / F crash recovery 兩跑 SIGTERM+SIGKILL）。新建 17 檔 + DesignAdjustmentPlanExecutor（fix#2）+ FrameworkDesignRouter 572 行 + 改 12 既有檔（含 CreateSplitTaskProposalInteractionAsync private→internal 共用 SoT）+ Migration `Stage52TaskGroupDesignFrameworkState`。**Aria 校準錨 ×1.05**（609K vs Charter 中位 580K，混合型第 4 個資料點 mid 中段；混合型 ×0.96-1.25 四資料點區間穩定驗證）。詳見 Stage 52 Roadmap。commits：`3b2343a`(A) + `8b3ead1`(B) + `b5dac50`(v2.0) + `806b22b`/`27ce0b7` 兩 fix + `d35ec80`/`754ff34`/`d951e6c`(v2.1/v2.2/header)。

## [3.37.0] — 2026-05-02 — [Stage 51](docs/planning/Stage_51_Roadmap.md) ⭐ v4 漸進遷移第三步 framework HITL 試點

A3 試點精神 — 不切既有 BossInteraction 10+ type，新建 `framework_kickoff_mid_interrupt` type + `FrameworkHitlBridge` service 橋接層；既有 InteractionService / InteractionProcessor 主流程不動。B1 試點 = Christ 在 Kickoff 多輪會議跑期間 Dashboard 點「中途介入」按鈕 → workflow 跑到 RequestPort 點 yield → 開 BossInteraction → Christ 回應後新 HTTP scope rehydrate workflow（`InProcessExecution.ResumeStreamingAsync` 對齊 spike F3 結論）+ SendResponseAsync → workflow 從 yield 點繼續跑。Spike F1/F2/F3 三項全綠（RequestPort C# stable / ICheckpointStore 對 pending requests 序列化可用 / 跨 HTTP scope rehydrate）。Forge 主動範圍變更（Aria 認可）：trigger flag 改用 in-memory `KickoffMidInterruptTriggerStore`（避免 framework state JSON mutation brittleness）。**6 場景全綠 + 0 follow-up + Aria spike 三項關注點實證通過**（場景 D requestId `0daeccaa...` 跨 process restart stable 證據鏈）。新建 4 檔 ~600 LoC + 改 ~15 既有檔。**Aria 校準錨 ×0.96**（448K vs Charter 中位 465K，混合型第 3 個資料點 mid 帶下半最低；混合型 ×0.96-1.25 三資料點驗證）。詳見 Stage 51 Roadmap。commits：`67a9b0a`(A) + `e65a4b3`(B) + `3bb7f28`(v2.0)。

## [3.36.0] — 2026-05-02 — [Stage 50](docs/planning/Stage_50_Roadmap.md) v4 漸進遷移第二步

Kickoff Meeting 5 Agent 切 framework Workflow Builder fan-out/fan-in（A2 路線）+ feature flag。Spike E1 ❌ → A2 fallback 路線拍板（framework Group Chat 不支援 multi-speaker per round + Concurrent 不支援 loop back，唯一路徑 `WorkflowBuilder` + `AddFanOutEdge` + `AddFanInBarrierEdge` + `AddSwitch` + loop back）；E2/E3 ✅。**3 follow-up fix（戰略級 framework 1.3.0 fan-out 拓撲首次 production 整合踩坑）**：① RunAsync → RunStreamingAsync（fan-out 必須 streaming dispatch，Stage 49 線性串聯沒踩到）② 顯式 SendMessageAsync/YieldOutputAsync Executor 加 `[SendsMessage]`/`[YieldsOutput]` + `partial class`（type validation MAFGENWF003）③ Mock Petra 角色識別補特徵字串。Forge 自驗 6 場景 + 2 bonus 全綠 + 場景 C marker 100% cleared（vs Stage 49「30% 殘留」）+ 結案 Forge 自做 v2.0（forge-end skill 升級）。新建 11 檔 ~1100 LoC + 改 11 檔（含 KickoffMeetingService 淨刪 213 行 prompt builders 全委派 KickoffPrompts）。**Aria 校準錨 ×1.09**（500K vs Charter 中位 460K，混合型第 2 個資料點 mid 中心）。詳見 Stage 50 Roadmap。commits：`7d37a48`(A) + `24b62dc`(B) + `a50059c`/`cd6d61a`/`1023104` 三 fix + `ff6a26f` + `b443546`(v2.0)。

## [3.35.0] — 2026-05-02 — [Stage 49](docs/planning/Stage_49_Roadmap.md) ⭐ v4 漸進遷移首發

Cody-Vera-Petra Appeal loop 切 framework Workflow Builder + Checkpointing + feature flag 並行雙系統。**0 follow-up + production fallback 防呆生效**（tech_improvement task Cody Dev_plan 缺結構 marker → IsDevPlanFailed=true → FrameworkAppealRouter 自動 fallback to legacy `HandleDevPlanCompletedAsync`，Forge Session B 主動加防呆）。「換引擎不換車身」首發：5 Agent prompt 完全不動 + DB 加 1 nullable 欄位 + Discord/Dashboard/ClaudeCodeService 包裝層保留 + 換 Appeal loop 編排層用 framework。**核心拍板**：① 並行雙系統 + feature flag 預設 false ② framework Checkpointing 為主（採 `ICheckpointStore<JsonElement>` 首選路徑）③ **路線 B service 包裝**（v1.1 修正 Aria Roadmap 3 Agent 不同層整合不一致 — 三 Executor 都包既有 service method）④ **DI factory 模式**（Forge 主動發現 — 不註冊 DI + IServiceScopeFactory 解 Singleton+Scoped 陷阱）⑤ FrameworkAppealRouter F3 scope 精簡（5 entry → 2 真實分流）⑥ Crash Recovery 雙系統隔離。新建 13 檔 ~2700 LoC + Migration。**Aria 校準錨 ×1.25**（606K vs Charter 中位 485K）。詳見 Stage 49 Roadmap。commits：`90c6ed3`(A) + `3400e5b`(B) + `5debc96` + `33bf51c`(v2.0)。

## [3.34.0] — 2026-05-02 — [Stage 47](docs/planning/Stage_47_Roadmap.md)

FF 四十七 Token limit SoT 統一（路線 b：DB AppSettings 動態化）+ 順帶完成 FF 十一 Dashboard 可調 Token 守門 — 解 Trial_v5 議題 C（appsettings 改了 docker env override 靜默無效）+ 議題 D（手動 docker restart 不 reload env，認知差非 CI/CD 缺陷，CI/CD `--force-recreate` 已就緒）；`AgentConfig` 加 `DailyTokenLimitK?` / `MonthlyTokenLimitK?` 欄位（呼應 Stage 38 Provider/Model 模式）+ Migration `Stage47AgentConfigTokenLimits`；`AppSettingsService.GetIntAsync(key, fallback=0)` + `>0` 防呆判斷確保 DB row="0" 也走 fallback；`AgentConfigCache` cache tuple 擴成 4-tuple（Provider/Model/DailyTokenLimitK/MonthlyTokenLimitK）；`TokenTrackingProvider` 4 個 Check 改 DB-first → appsettings fallback；Check 4 警報訊息指向 Dashboard【系統設定 → Token 守門設定】；`SystemSettings.razor` 新增「Token 守門設定」區塊 + 儲存後立即 `ReloadCacheAsync("all")`；`AgentSettings.razor` 加 per-agent Token Limit 欄位（nullable int，0=fallback）+ `UpdateTokenLimitsAsync`；移除 `docker-compose.prod.yml` 24 個 Token env（Bot 2 + Dashboard 22）；CLAUDE.md 加「ops 配置改動 SoP」段；DbSeeder **不動**（v2 修正：DB 不 seed Token 預設值，讓 fallback 安全網真實可達）；Aria 兩輪審查機制揭露 v1「DbSeeder 自動 seed 讓 fallback 永遠走不到」核心矛盾並完整修正；驗收 4 場景待 Christ 線下實跑（A/B/C/D），其中場景 D（CI/CD push 後容器啟動）+ 場景 C（DB 空值 fallback）為最關鍵安全網；**Forge Sonnet 200K + High 中段 compact 一次**（Aria 校準錨教訓：估 ~260K 卻推 Sonnet 200K 自我矛盾，下次 >180K 直接推 Opus 1M）

## [3.33.0] — 2026-04-29 — [Stage 46](docs/planning/Stage_46_Roadmap.md)

FF 三十五 自動拆任務 ⭐ 戰略級 + 搭車 FF 三十九 — Petra 在 Design 階段 propose 拆 N 個依賴 sub-task → Christ 採納 → Sequential 鏈執行（Phase 1 done → Phase 2 → Phase 3 → epic done） → 各自獨立 PR；解 Trial_v4「Cody 對大需求縮水」根因（12 Issue → 1 Issue）；TaskGroup 加 4 欄位（ParentGroupId / EpicPaused / PhaseNumber / PhaseDescription）+ partial index；BuildEpicSubTasksAsync v1.1 三層防護（idempotent + fresh read + scope 隔離）；Petra 雙層判斷（規則層 EvaluateAndProposeSplitAsync + Petra 層 RunPetraSplitTaskProposalAsync 復用 PetraSessionId）；2 個新 BossInteraction type（split_task_proposal / epic_partial_paused）；Internal API + DashboardBotService client（pause-epic / resume-epic 含找最大 done 啟動鏈）；CLAUDE_Petra.md 拆 task 判準新章節（80%+ 邊界覆蓋）；FF 三十九 EndsWith 寬鬆比對 + 清 InterventionReason；Mock 8-1 split_task_propose_accept 完整可驗（8-2 follow-up Trial_v5）；驗收期 3 個 fix commits（[MOCK] prefix / 12 Issue 路徑 / TryParseSplitProposal LastIndexOf bug）+ 揭露 **Stage 25b TryParseDesignIssues 既有 bug**（從未端到端跑過 — 風險點 #4 預測命中）；**Trial_v5 鎖死前置條件最後一塊完成 🎉**

## [3.32.0] — 2026-04-29 — [Stage 45](docs/planning/Stage_45_Roadmap.md)

FF 三十四 TaskGroup 流程暫停機制 + 搭車 FF 三十七 escalate skip status 殘留 — AiTeam 第三層暫停機制（與 Agent pause Stage 27b + 全域緊急停止 Stage 33 並列）：採方案 Ba（被動阻擋下階段）+ 議題 4/5 B（暫停與 BossInteraction / Appeal flow 兩機制獨立）；TaskGroup 加 4 欄位（IsPaused / PausedAt / PausedBy / PendingStepsJson SoT 解避免 Resume 重做 routing）；FireStepsAsync 統一閘門（22 caller 自動受保護，超越 Roadmap 預想）；Crash Recovery 對齊 IsPaused 篩選（落點 MeetingOrchestrationService.cs:432，**Aria 校準錨 #1：façade 不是 method body**）；FF 三十七 真實搭車範圍 1 處 ButtonCallbackRouter:241（**Aria 校準錨 #2：4 處 → 1 處**）；6 驗收場景全 PASS + 0 follow-up + 路線 C race condition 0 實際觀察；驗收期意外發現 FF 三十九（Dashboard 點「跳過審核」靜默變「放棄任務」）

## [3.31.0] — 2026-04-29 — [Stage 44](docs/planning/Stage_44_Roadmap.md)

FF 三十三 Token 計費機制 CLI Agent 涵蓋 — Trial_v4 揭露 token_logs 6% 涵蓋率盲點補完：`ClaudeCodeService.ParseJsonOutput` 解 `usage` + `total_cost_usd` → `ClaudeCodeResult.Usage`；token_logs 加 5 nullable 欄位（`Stage` / `Round` / `CacheCreationTokens` / `CacheReadTokens` / `TotalCostUsd HasPrecision(18,6)`）；`TokenLogService` 共用 helper 內建 try-catch + 獨立 scope（保證硬規則「不阻塞主流程」）；16 個 CLI caller（含搭車 Rosa/Demi）+ 21 處 MeetingCommons call site 全對齊；Stage 22 守門公式升級為等效 token（`input + output + cache_creation × 1.25 + cache_read × 0.1`，整數運算 EF translate）；Victoria 真實 CLI 對話實證 cache 占比 95.5% 等效 50,450 vs 純 input+output 2,265

## [3.30.0] — 2026-04-29 — [Stage 43](docs/planning/Stage_43_Roadmap.md)

FF 三十二 Orchestrator 改動類三子項（A+B+E）+ Sage F 搭車 — Self-implement 完整性閘門下半場：DevPlan 重產機制（上限 2，超限 escalate）+ Dev/Dev_fix failed 中止 fix loop + QA 失敗 needs_intervention（與 failed 語意分離）+ TaskGroup done 判定統一守門 + DocAgentService 認 Sage escalate JSON + PR URL hardcode 修。新增 `needs_intervention` Status / InterventionReason 欄位 / 4 個 BossInteraction type / 4 個 Mock 場景；驗收期搭車修 Stage 24 既有缺漏（Dev_fix 進 SemaphoreGroups + GetExecutorKey）— 揭露 QA fix loop 程式碼活 ≠ 從未端到端跑過

## [3.29.0] — 2026-04-28 — [Stage 42](docs/planning/Stage_42_Roadmap.md)

FF 三十二 prompt 補強類四子項（C+D+F+G）— Self-implement 完整性閘門上半場：Petra 範圍縮水升級規則 + Vera Server Circuit Critical 邊界（含 MudBlazor 事件鏈）+ Sage 無實作 escalate + Cody PR 自我檢查（80% 門檻，`⚠️ ESCALATE_NEEDED` marker 三檔字面一致）

## [3.28.0] — 2026-04-27 — [Stage 41](docs/planning/Stage_41_Roadmap.md)

`tests/Generated/` 編譯與執行修復（FF 三十一）+ CLAUDE_Quinn.md 兩條結構性 bug 防護 — 補完「Vera 審查 + Petra 閘門 + Quinn 測試」三層品質保證迴圈

## [3.27.0] — 2026-04-26 — [Stage 40](docs/planning/Stage_40_Roadmap.md)

`CLAUDE_Vera.md` + `CLAUDE_Petra.md` 判準補強（FF 二十九 + FF 二十五 Petra 子項）— Trial_v4 前置條件閉環

## [3.26.0] — 2026-04-25 — [Stage 39](docs/planning/Stage_39_Roadmap.md)

Vera 審查擴及 `.razor` / `.css`（FF 二十八）；新增 `AgentResultType.Skipped` 結果型別 + Dashboard 全鏈路 teal 配色

## [3.25.0] — 2026-04-25 — [Stage 38](docs/planning/Stage_38_Roadmap.md)

Dashboard Provider/Model 動態化（FF 四第二階段 2-A）：DB SoT + `AgentConfigCache` + `LlmModels.cs` 常數白名單

## [3.24.0] — 2026-04-25 — [Stage 37](docs/planning/Stage_37_Roadmap.md)

GeminiProvider API 層（FF 四第一階段）+ Crash Recovery 全面涵蓋（5 種 `ActiveOrchestration`）

## [3.23.0] — 2026-04-22 — [Stage 36](docs/planning/Stage_36_Roadmap.md)

TaskGroupService + CommandHandler 拆解（FF 二十 A+B 合併）：4795 行 → 1272 行（-73%）；**AiTeam 四怪物級檔案技術債清零** 🎉

## [3.22.0] — 2026-04-22 — [Stage 35](docs/planning/Stage_35_Roadmap.md)

PmAgentService 拆解（FF 二十-D）：1388 行 → 6 個子 service；首次實踐 SOP 6（子資料夾 `Agents/Pm/`）

## [3.21.0] — 2026-04-22 — [Stage 34](docs/planning/Stage_34_Roadmap.md)

MeetingService 拆解（FF 二十-C）：1415 行 → KickoffMeetingService + DesignMeetingService + Commons + Results

## [3.20.0] — 2026-04-22 — [Stage 33](docs/planning/Stage_33_Roadmap.md)

Agent 狀態卡 2.0：佇列控制 Dashboard 化（per-agent pause/resume + 全域 stop-all）+ 待辦清單 expand + 深層連結

## [3.19.0] — 2026-04-21 — [Stage 32](docs/planning/Stage_32_Roadmap.md)

`/mock` Dashboard 化 + Mock Delay / WorkflowSettings 動態化（從 AppSettings 讀，免重啟容器）

## [3.18.0] — 2026-04-20 — [Stage 31](docs/planning/Stage_31_Roadmap.md)

可靠性補強：Dashboard 重試按鈕 + 會議 Crash Recovery + Appeal 對抗紀錄 UI（FF 十七 + 十八）

## [3.17.0] — 2026-04-20 — [Stage 30](docs/planning/Stage_30_Roadmap.md)

申訴迴圈 LLM API → Claude Code CLI 全面升級（5 個環節新開 session + 唯讀工具）

## [3.16.1] — 2026-04-19 — Hotfix

MockMode 提案核准重複建 TaskGroup bug 修正（Dashboard 路徑補 GroupId 防護對齊 Discord 路徑）

## [3.16.0] — 2026-04-19 — [Stage 29](docs/planning/Stage_29_Roadmap.md)

Dashboard 操作性收尾 + CEO 指令通道擴充（Dashboard 直接下指令給 Victoria，含圖片附件）

## [3.15.0] — 2026-04-17 — [Stage 28b](docs/planning/Stage_28b_Roadmap.md)

Dashboard 雙向操作中心 — 文字輸入互動 + 歷史紀錄篩選

## [3.14.0] — 2026-04-17 — [Stage 28a](docs/planning/Stage_28a_Roadmap.md)

Dashboard 雙向操作中心 — 基礎架構 + 8 個確認點按鈕回覆 + 樂觀鎖先到先贏

## [3.13.0] — 2026-04-16 — [Stage 27b](docs/planning/Stage_27b_Roadmap.md)

Agent 任務序列 — 操作性與可觀察性（5 個 Discord 指令 + Dashboard 佇列視覺化 + SignalR）

## [3.12.0] — 2026-04-16 — [Stage 27a](docs/planning/Stage_27a_Roadmap.md)

Agent 任務序列 — 核心佇列機制（DB-as-Queue + AgentQueueService + per-agent SemaphoreSlim + Crash Recovery）

## [3.11.0] — 2026-04-14 — [Stage 26](docs/planning/Stage_26_Roadmap.md)

驗收基礎設施（PipelineView 折疊面板 + MockMode 修正）+ 版本號集中管理（`Directory.Build.props`）

## [3.10.0] — 2026-04-14 — [Stage 25b](docs/planning/Stage_25b_Roadmap.md)

開發流程重構 Phase 1d — 設計規劃階段（5 人設計會議 + 條件式 Christ 確認）

## [3.9.0] — 2026-04-14 — [Stage 25a](docs/planning/Stage_25a_Roadmap.md)

開發流程重構 Phase 1c — Kick-off 會議機制（Claude Code 持續對話 session + 多 Agent 會議）

## [3.8.0] — 2026-04-13 — [Stage 24](docs/planning/Stage_24_Roadmap.md)

開發流程重構 Phase 1b — QA Petra 介入 + Dev_plan 審核強化 + TestReport 結構化存 DB

## [3.7.0] — 2026-04-12 — [Stage 23](docs/planning/Stage_23_Roadmap.md)

開發流程重構 Phase 1a — Review Appeal 迴圈 + Sage 轉型歸檔員 + Git Tag 自動化

## [3.6.0] — 2026-04-12 — [Stage 22](docs/planning/Stage_22_Roadmap.md)

Dashboard 存取分層（localhost bypass）+ Token 守門 4 層攔截 + `#指令中心` 頻道清理

## [3.5.0] — 2026-04-11 — [Stage 21](docs/planning/Stage_21_Roadmap.md)

`docs/` 資料夾重整（architecture / planning 子資料夾）+ SemVer 導入

## [3.4.0] — 2026-04-11 — [Stage 20](docs/planning/Stage_20_Roadmap.md)

Dashboard 全面換 MudBlazor Layout（MainLayout → MudLayout + Dark Mode → MudThemeProvider）

## [3.3.0] — 2026-04-10 / 04-11 — [Stage 19](docs/planning/Stage_19_Roadmap.md)

Dashboard UI 全面打磨（三批 18 項：StatusBadge / MudChip / MudIcon / MudStack / 側邊欄 localStorage 等）

## [3.2.0] — 2026-04-09 — [Stage 18](docs/planning/Stage_18_Roadmap.md)

Dashboard 可觀測性升級：Agent 狀態卡即時更新 + Pipeline View（MudStepper + MudTimeline）

## [3.1.0] — 2026-04-08 — [Stage 17](docs/planning/Stage_17_Roadmap.md)

Mock Mode：`IClaudeCodeService` 代理模式 + Dashboard 開關 + 4 種 `/mock` 流程

## [3.0.0] — 2026-04-07 — [Stage 16](docs/planning/Stage_16_Roadmap.md)

**MAJOR**：PM Agent（Petra）品質審核閘門；Vera / QA 重構為單一 Claude Code session

## [2.4.0] — 2026-04-06 — [Stage 15](docs/planning/Stage_15_Roadmap.md)

Victoria 接上 Claude Code + Session 對話持久化 + 長期記憶

## [2.3.0] — 2026-04-06 — [Stage 14](docs/planning/Stage_14_Roadmap.md)

CEO 分類補強：技術改善分類 + Release / Ops / Doc 直接路由 + 任務取消能力

## [2.2.0] — 2026-04-06 — [Stage 13](docs/planning/Stage_13_Roadmap.md)

系統穩定性與流程修正：Dev → Reviewer → QA → Doc 串行 + 單一 PR + Closes #XX 自動關 Issues

## [2.1.0] — 2026-04-06 — [Stage 12](docs/planning/Stage_12_Roadmap.md)

提案流程全面升級：Rosa / Demi 串行協作 + 唯讀探索 + UI 規格存 DB + Discord 附件

## [2.0.0] — 2026-04-05 — [Stage 11](docs/planning/Stage_11_Roadmap.md)

**MAJOR**：Dev Agent（Cody）驅動 Claude Code CLI 自主開發

## [1.4.0] — 2026-04-03 — [Stage 10](docs/planning/Stage_10_Roadmap.md)

開發流程自動閉環：CEO Orchestrator + WorkflowEngine + Review 閉環 + Ops Rollback

## [1.3.1] — 2026-04-04 — Hotfix

Stage 10 驗收後 7 項修正（Race Condition / IssueUrls 重複 / PushStatus / dead code 清理 / EF Index）

## [1.3.0] — 2026-04-03 — [Stage 9](docs/planning/Stage_9_Roadmap.md)

CEO 升級 + 可觀測性：Token 監控 Dashboard + CEO 智慧分類 + 提案模式 + QA Playwright

## [1.2.0] — 2026-04-02 — [Stage 8](docs/planning/Stage_8_Roadmap.md)

系統可靠性與操作體驗：動態 AppSettings + per-agent Rules + Dark Mode + Notion 移除

## [1.1.0] — 2026-04-02 — [Stage 7](docs/planning/Stage_7_Roadmap.md)

Software Team 完全體：Reviewer / Release / Designer Agent + CI/CD + Discord 重設計 + 自然語言對話

## [1.0.0] — 2026-04-01 — [Stage 6](docs/_archive/early-stages/Stage_6_Roadmap.md)

**MAJOR**：強化、驗收與技術債清償（Discord Vision、MudBlazor、Requirements 三層確認、E2E 驗收等 12 項）

## [0.4.0] — 2026-04-01 — [Stage 5](docs/_archive/early-stages/Stage_5_Expansion.md)

擴充 Agent：QA / Doc / Requirements + 動態 Agent 框架

## [0.3.0] — 2026-03-31 — [Stage 4](docs/_archive/early-stages/Stage_4_Dashboard.md)

Blazor Web App Dashboard（Identity + SignalR + Aspire 基礎）

## [0.2.0] — 2026-03-31 — [Stage 3](docs/_archive/early-stages/Stage_3_Agents.md)

第一批 Agent 上線：CEO / Dev / Ops（Anthropic Claude API）

## [0.1.0] — 2026-03-31 — [Stage 1](docs/_archive/early-stages/Stage_1_Design.md) + [Stage 2](docs/_archive/early-stages/Stage_2_Foundation.md)

基礎建設：系統設計確定 + Discord Bot + Aspire AppHost + PostgreSQL
