# Changelog

本專案所有重要變更紀錄於此檔。

格式參照 [Keep a Changelog 1.1.0](https://keepachangelog.com/zh-TW/1.1.0/)，版本號遵循 [Semantic Versioning](https://semver.org/lang/zh-TW/)。
工作單位為 **Stage**，每完成一個 Stage 通常對應 minor 版本 bump。每條 entry 的細節見對應 Stage Roadmap；更深的實作 commit 訊息見 git log。

未實作功能候選見 [Future_Feature.md](docs/planning/Future_Feature.md)。

---

## [Unreleased]

- **🎉 Stage 51 v4 漸進遷移第三步完成 ⭐**：[Stage 51](docs/planning/Stage_51_Roadmap.md) framework HITL pattern 試點（Kickoff Workflow 中途介入）+ feature flag — **6 場景全綠 + 0 follow-up + Aria spike 三項關注點實證通過（含跨 process restart requestId stable 證據鏈）**。v4 漸進遷移路線 6 Stage 遷移 **3/6 達成**。
- **下個動作候選**：① **Stage 52 = Design Meeting B3 路線（主迴圈遷移）+ WorkflowEngine 整體 → Workflow Builder（最大遷移點）**（v4 漸進遷移第 4 步，估 4-6 週，必拆 session 2-3 段）/ ② FF 四十三（token_logs.TotalCostUsd 99.7% NULL）/ ③ FF 四十二（TryParseDesignIssues Stage 25b 既有 bug）
- **6 Stage 遷移路線進度**（spike 報告節 7）：✅ **Stage 49 Appeal loop** ✅ **Stage 50 Kickoff Meeting** ✅ **Stage 51 framework HITL 試點** → Stage 52 WorkflowEngine + Design Meeting → Workflow Builder（最大遷移點）/ 53 Crash Recovery → Checkpointing / 54 收尾 + production 切換 + 真正切既有 BossInteraction 到 framework HITL（Stage 51 試點 know-how 全面 wire）
- **FF 三十六 Phase B 動態流程架構**：Phase A 採用解鎖 Phase B 啟動條件，但 Christ 路線 = **Stage 52 漸進遷移過半再評估 Phase B**
- **FF 三十二 ✅** / **FF 三十三 ✅** / **FF 三十四 ✅ + FF 三十七 ✅** / **FF 三十五 ✅ + FF 三十九 ✅** / **FF 四十七 ✅ + FF 十一 ✅** / **FF 四十九 ✅** / **Stage 49 v4 首發 ✅** / **Stage 50 v4 第二步 ✅** / **Stage 51 v4 第三步 ✅ ⭐**
- **新立 FF 四十 / 四十一 / 四十二**（Stage 46 驗收期 follow-up 採集）
- **Stage 48 揭露候選 FF**（待 Christ 拍板）：Windows-only Process.Start + UseShellExecute=false 不 honor PATHEXT for `.cmd`（production hardening FF）

---

## [3.37.0] — 2026-05-02 — [Stage 51](docs/planning/Stage_51_Roadmap.md) v4 漸進遷移第三步 ⭐ framework HITL 試點

v4 漸進遷移 6 Stage 路線第三步完成 — framework **Human-in-the-Loop（HITL）pattern 試點** + Checkpointing pause-resume 機制（Kickoff Workflow 中途介入）+ 獨立 feature flag 雙 flag 連動。**6 場景全綠 + 0 follow-up commits + Aria spike 三項關注點實證通過**（含跨 process restart requestId stable 證據鏈）。

「換引擎不換車身」第三步實踐：**A3 試點精神** — 既有 BossInteraction 10+ type 任何一個都不切（46 檔涉及 + 雙通道樂觀鎖機制成熟，無法 1:1 對應 framework HITL 替代），新建 `framework_kickoff_mid_interrupt` BossInteraction type + `FrameworkHitlBridge` service 作橋接層；既有 InteractionService / InteractionRespondService / InteractionProcessor 主流程不動。**B1 試點場景** = Christ 在 Kickoff 多輪會議跑期間 Dashboard 點「✏️ 中途介入」按鈕，下個 Petra Round 邊界 framework workflow yield 等回應，Christ 透過 BossInteraction（Discord 或 Dashboard）回「套用修改 + 指引文字」或「取消介入」，workflow 從 checkpoint resume 帶新指引繼續跑。

**核心 lifecycle 質變（vs Stage 49/50 同步跑完）**：framework Workflow 跑到 RequestPort 點 yield → router 在 `WatchStreamAsync` 收到 `RequestInfoEvent` 時 break loop（保留 checkpoint pending request）→ 開 BossInteraction → Christ 回應後**新 HTTP scope** rehydrate workflow（`InProcessExecution.ResumeStreamingAsync(workflow, savedCheckpoint, manager)` 對齊 spike F3 結論）+ `SendResponseAsync` 送回應 → workflow 從 yield 點繼續跑到結束。

**Spike 第一步 F1/F2/F3 三項全綠**：① `RequestPort.Create<TReq, TResp>` C# 1.3.0 stable API（含 ExecutorBinding 隱式轉換進 Workflow 拓撲）② `ICheckpointStore<JsonElement>` 對 pending requests 序列化可用（framework 內部隨 checkpoint 序列化 + RestoreCheckpointAsync 自動 re-emit RequestInfoEvent）③ 跨 HTTP scope rehydrate 模式（Bridge 不持有 run 物件，每次新 scope 內 ResumeStreamingAsync）。

**核心拍板**（Christ 2026-05-02）：① A3 試點不切既有 BossInteraction ② B1 Kickoff 中途介入場景 ③ C2 Petra session 沿用 Claude Code `--resume` 機制（Modify 流程不動）④ D2 獨立 `Workflow:UseFrameworkKickoffMidInterrupt` flag（雙 flag 連動：本 flag 只在 UseFrameworkKickoff = true 時有意義）⑤ E D2 抽 `FrameworkHitlBridge` service（解耦給 Stage 54 收尾真正切 HITL 時複用）⑥ F spike 三項全驗。

**Forge 主動範圍變更（Aria 認可）**：trigger flag 改用 **in-memory `KickoffMidInterruptTriggerStore`** Singleton（vs 計劃書原 framework state JSON mutation helper / Plan B DB fallback）— 避免 framework checkpoint JsonElement 內部結構 mutation brittleness（framework 版本變動易破壞），代價是 Bot 重啟「待按按鈕」狀態丟失（Christ 重新點即可，按下到下個 superstep 邊界本就時間敏感，HITL 等待 phase 已轉化為 KickoffState.MidInterruptRequestPending 持久化）。

**8 個關鍵設計決策**：① in-memory TriggerStore 第三條路 ② Bridge 不持有 run 物件 rehydrate 模式 ③ MidInterruptCheckExecutor 雙 [MessageHandler] partial class（對齊 Stage 50 踩坑 #10 三件套紀律）④ Bridge ⇄ Router service locator 解循環依賴 ⑤ finally cleanup 條件式（`yieldedForHitl` flag）⑥ KickoffTaskId 寫進 KickoffState 跨 scope 持久化 ⑦ `ScanForGuidProperty`/`ScanForStringProperty` 寬鬆 scan framework state JSON（fail-open 設計，避 framework 版本變動 break）⑧ Cancel 拍板「丟棄所有累積指引回到正常對話」每次介入是獨立 trigger-response cycle。

**戰略級驗收結果（Aria spike 三項關注點實證通過）**：場景 D（crash during wait）是最強驗證 — yield 後 `docker restart aiteam-bot` → 重啟 log `[FrameworkKickoffRouter] 啟動：發現 1 個 stuck framework kickoff` → `[Stage51] Recovery Group=df65b28c...：等待人類回應（MidInterruptRequestPending=true），保留 marker 等 BossInteraction 觸發 resume` → Discord 點按鈕 + 輸入文字 → `ResumeStreamingAsync 啟動（requestId=0daeccaa...）` **跨 process restart 仍找到 latest checkpoint** → `SendResponseAsync 完成` → `WorkflowOutputEvent（decision=consensus，rounds=2）` → `FinishKickoffAsync 完成`。requestId `0daeccaa72714604812add3427ba4d9d` 在 yield emit + Bridge resume + Recovery 跨重啟全程 stable。**Aria 校準錨：實際 448K / Charter 中位 465K = ×0.96**（混合型 Stage 第 3 個資料點，落 mid 帶**下半**，比 Stage 50 ×1.09 / Stage 49 ×1.25 還準）。

**新建 4 檔 ~600 LoC**：`Workflows/Kickoff/Executors/MidInterruptCheckExecutor.cs` 111 LoC（雙 [MessageHandler] partial class）+ `Orchestration/Hitl/FrameworkHitlBridge.cs` 353 LoC + `Orchestration/Hitl/KickoffMidInterruptTriggerStore.cs` 35 LoC + ~600 LoC Bridge service；改檔 ~15 個（含 KickoffWorkflowFactory 拓撲擴充 + KickoffState 加 4 欄位 + 2 record + Router lifecycle 改寫 + ButtonCallbackRouter customId 路由 + CommandHandler modal handler + Dashboard PipelineView 按鈕 + SystemSettings 第三 toggle + Bot Internal API + 4 Mock scenario）。

**Forge 自驗能力擴張第三次完整實踐**（Stage 49 → Stage 50 → Stage 51）：5 場景靜態自驗（A.1/A.2/A.3 + E + F 程式碼路徑審視）+ B/C/D Christ 線下實跑（HITL 真實互動性質決定，Forge 主動誠實標明 + 提供完整 step-by-step 操作步驟）+ 結案 Forge 自做 Roadmap v2.0（forge-end skill 沿用 Stage 50 慣例）+ 12 stale TaskGroups DB 清理紀錄。**8 條踩坑紀錄**含戰略級對 Stage 52+ 預警（`RequestPort` 隱式轉換 ExecutorBinding 接線方式 / Blazor lifecycle 選擇 / customId prefix 順序紀律）。

commits：`67a9b0a`（Session A）+ `e65a4b3`（Session B 收尾）+ `3bb7f28`（Roadmap v2.0 forge-end SOP）。

## [3.36.0] — 2026-05-02 — [Stage 50](docs/planning/Stage_50_Roadmap.md) v4 漸進遷移第二步

v4 漸進遷移 6 Stage 路線第二步完成 — Kickoff Meeting 5 Agent 會議切 MS Agent Framework Workflow Builder fan-out/fan-in（A2 路線）+ feature flag 並行雙系統。**3 follow-up fix commits**（驗收期揭露 framework 1.3.0 對 fan-out/fan-in 拓撲 vs Stage 49 線性串聯的不同要求）。

「換引擎不換車身」第二步實踐：5 個 Kickoff Agent prompt 完全不動（抽到 `KickoffPrompts.cs` 共用，legacy + framework 兩條路徑同 SoT）+ DB schema 加 1 nullable 欄位（`task_groups.KickoffFrameworkStateJson`，與 Stage 49 `FrameworkAppealStateJson` 完全獨立）+ Discord/Dashboard/ClaudeCodeService 包裝層保留 + 換 Kickoff Meeting 編排層用 framework Workflow Builder。

**Spike 第一步驗證結論驅動路線拍板**：① E1 ❌ — framework Group Chat custom manager 不允許「multi-speaker per round」（star topology + single-speaker turn-by-turn 設計，與「Rosa/Demi/Cody/Quinn 4 個獨立並行視角」相反）→ **走 A2 fallback：`WorkflowBuilder` + `AddFanOutEdge` + `AddFanInBarrierEdge` + `AddSwitch` + loop back**（對齊 MapReduce + Loop sample）② E2 ✅ `ICheckpointStore<JsonElement>` 通用（Stage 49 pattern 100% 複用）③ E3 ✅ 5 個並行 Claude Code subprocess 不被 framework 限制（OS-level subprocess 並行）。

**核心拍板**（Christ 2026-05-02）：① A4：spike 第一步驗 A1 不可行 → A2 fallback ② B2：只遷 Kickoff，Design 留 legacy 給 Stage 52 走 B3 漸進 ③ C2：Petra session 沿用 Claude Code `--resume` 機制（Modify 流程不動）④ D2：獨立 `Workflow:UseFrameworkKickoff` flag（與 Stage 49 `UseFrameworkAppealLoop` 完全獨立）⑤ E：spike 第一步三項全驗。

**驗收期 3 follow-up fix（戰略級 framework 1.3.0 踩坑揭露）**：① `a50059c` `RunAsync` → `RunStreamingAsync` + `WatchStreamAsync` foreach（fan-out + fan-in barrier 拓撲必須 streaming dispatch，Stage 49 線性串聯沒踩到）② `cd6d61a` 4 個顯式 `SendMessageAsync`/`YieldOutputAsync` 的 Executor 加 `[SendsMessage(typeof(T))]`/`[YieldsOutput(typeof(T))]` attribute + 3 個 class 加 `partial` 修飾子（framework 1.3.0 type validation MAFGENWF003，Stage 49 用 `[MessageHandler] ValueTask<T>` generic return 模式 generator 自動推導沒踩此坑）③ `1023104` Mock Petra 角色識別補 "Kick-off 會議已結束" 特徵字串（抽 prompt builders 共用後 BuildPetraPlanPrompt 沒「你是 Petra」字樣）。

**Forge 自驗 6 場景全綠 + 2 bonus 子場景**：A flag false → legacy 不變 / B flag true → framework 完整跑通（4 Agent 並行 + Aggregator 收齊 + Petra consensus + Plan + WorkflowOutputEvent + DB 寫入）/ C Recovery 雙系統隔離 + 降級策略 marker **100% cleared**（vs Stage 49 case study「30% 殘留」更乾淨）/ D escalate → KickoffEscalateExecutor / E flag 切回 → framework 重新接管 / F MockMode token 0 行（預期）/ Bonus consensus_round2 Switch loop back + max_iter Switch case round>=max。

**Forge 自驗能力擴張第二次完整實踐**（Stage 49 case study 後）：跑全 6 場景揭露 3 個真實 production bug + 自己診斷修根因 + 對齊 framework canonical sample 補佐證 + 寫踩坑紀錄 + 戰略洞察段；Christ 線下補驗縮減到只剩 3 項（Discord embed 視覺 + 真實 LLM token_logs + Modify resume 對話）；**結案 Forge 自做 Roadmap v2.0**（forge-end skill 升級 — Stage 49 是 Aria 做 v2.0，Stage 50 Forge 自做）。

**Aria 校準錨：×1.09**（500K 實際 vs Charter 中位 460K，混合型 Stage 第 2 個資料點，比 Stage 49 ×1.25 更接近 mid 中心；混合型 Stage 倍率穩定在 ×1.0-1.3 區間，Stage 49 + Stage 50 兩資料點驗證不到 ×1.4 上界）。

**新建 11 檔 ~1100 LoC**：`src/AiTeam.Bot/Workflows/Kickoff/`（KickoffState / KickoffPrompts / KickoffWorkflowFactory / KickoffCheckpointStore + 6 個 Executor）+ `Orchestration/Meeting/FrameworkKickoffRouter.cs` 499 LoC + `Migration Stage50TaskGroupKickoffFrameworkState`；改檔 11 個（含 KickoffMeetingService 淨刪 213 行 prompt builders 全委派 KickoffPrompts、ClaudeCodeAgentExecutor [Obsolete] message 更新、Program.cs 3 Singleton DI、Dashboard SystemSettings 第二 toggle）。

**踩坑紀錄 11 條**（Forge 結案第一段補完）給 Stage 51+ v4 遷移預警，含 3 條驗收期戰略級踩坑（🔴 #9 fan-out 拓撲 streaming dispatch / 🔴 #10 顯式 send/yield 三件套 / 🟡 #11 Mock 角色識別覆蓋）；新增「戰略洞察：Stage 49 vs Stage 50 整合層級差異」段對 Stage 52 Design Meeting B3 路線是否複用 Stage 50 pattern 給出明確指引。

## [3.35.0] — 2026-05-02 — [Stage 49](docs/planning/Stage_49_Roadmap.md) ⭐ v4 漸進遷移首發

v4 漸進遷移 6 Stage 路線首發完成 — Cody-Vera-Petra Appeal loop 切 MS Agent Framework Workflow Builder + Checkpointing + feature flag 並行雙系統，**0 follow-up commits + production 真實任務驗證 fallback 防呆生效**。

「換引擎不換車身」首發實踐：Cody/Vera/Petra/Quinn/Sage 5 個 Agent prompt 完全不動 + DB schema 加 1 nullable 欄位（`task_groups.FrameworkAppealStateJson`）+ Discord/Dashboard/ClaudeCodeService 包裝層保留 + 換 Appeal loop 編排層用 framework Workflow Builder + Checkpointing。

**核心拍板**（Christ 2026-05-02）：① 並行雙系統 + feature flag（`Workflow:UseFrameworkAppealLoop` 預設 false，舊 path 保留至 Stage 54）② framework Checkpointing 為主 + superstep 結束同步寫既有 task_groups（採風險點 #4 首選路徑成功，實作 `ICheckpointStore<JsonElement>` framework 擴充點）③ POC 重寫 production 版本 spike branch 留 reference ④ Petra 切 framework 但暫保留 LlmProviderFactory wrapper 維持 TokenLogService（Stage 54 才完全切原生）⑤ BossInteraction 不包進 Stage 49（Stage 51 才動 HITL）。

**Forge 揭露 + Aria 拍板路線 B 設計**（v1.1 修正 Aria Roadmap 內部不一致）：framework Executor 整合層級從「底層接 IClaudeCodeService / LlmProviderFactory」改為「**包既有 service method**」（CodyReviewAppealExecutor → `ReviewAppealService.RunCodyAppealAsync` / VeraReviewAppealExecutor → `ReviewAppealService.RunVeraReviewAsync` / PetraReviewExecutors → `PmReviewService.ReviewVeraAsync` 等），三 Agent 同層整合 + Prompt SoT 統一消解 R4 prompt drift 風險 + 工時 -30%。Stage 54 才把 framework Executor 從 service 切回直連（+1-1.5 天）。`ClaudeCodeAgentExecutor.cs` 標 `[Obsolete]` 預留 Stage 50+ Group Chat orchestration（會議多 Agent 直連需要）。

**Forge DI factory 模式**（Session A 主動發現比 Aria 建議更穩）：framework Executor 不註冊 DI，由 `AppealWorkflowFactory` 內 new Executor + 注入 `IServiceScopeFactory`，`HandleAsync` 內 `CreateAsyncScope()` 取 scoped services（DbContext / LlmProviderFactory / ReviewAppealService）— 完整解 Singleton + Scoped 陷阱（既有 ClaudeCodeAgentExecutor lifecycle undocumented 議題完整解）。

**FrameworkAppealRouter F3 scope 精簡**（5 entry → 2 真實分流）：`HandleReviewerCompletedAsync` + `HandleDevPlanCompletedAsync` 才建 framework Workflow，其他 3 entry（`RunPetraGate` / `HandleDevBlocker` / `HandleDevPlanEscalation`）pass-through 走 legacy 避免循環依賴。Crash Recovery 雙系統隔離：`MeetingOrchestrationService.RecoverStuckOrchestrationsAsync` 加排除條件 `g.FrameworkAppealStateJson == null` + `AgentQueueProcessor` 啟動 hook `frameworkRouter.RecoverStuckFrameworkAppealsAsync` + 雙 marker 區隔（`ActiveOrchestration = "FrameworkAppeal"` + `FrameworkAppealStateJson != null`）。

**戰略級驗收結果**：6 場景全部通過，0 follow-up commits（驗收期全部跑在 production 容器，0 行程式碼修正）+ **意外驗到防呆生效**（production 真實 tech_improvement 任務 Cody Dev_plan 缺結構 marker → `IsDevPlanFailed=true` → FrameworkAppealRouter **自動 fallback to legacy** `HandleDevPlanCompletedAsync` — Forge Session B 主動加的防呆 production 真實觸發）。Aria 校準錨：實際 606K / Charter 中位 485K = **×1.25**（混合型 Stage 新資料點，落 Charter 預估 ×1.0-1.5 mid 帶）。

**新建 13 檔 ~2700 LoC**：`src/AiTeam.Bot/Workflows/Appeal/` 全新資料夾（AppealState / AppealWorkflowFactory / AppealCheckpointStore 209 LoC / AppealMessages / AppealLogHelpers / 5 個 Executor）+ `Orchestration/Appeal/FrameworkAppealRouter.cs` 397 LoC + `Migration Stage49TaskGroupFrameworkState`。

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
