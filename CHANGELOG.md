# Changelog

本專案所有重要變更紀錄於此檔。

格式參照 [Keep a Changelog 1.1.0](https://keepachangelog.com/zh-TW/1.1.0/)，版本號遵循 [Semantic Versioning](https://semver.org/lang/zh-TW/)。
工作單位為 **Stage**，每完成一個 Stage 通常對應 minor 版本 bump。每條 entry 的細節見對應 Stage Roadmap；更深的實作 commit 訊息見 git log。

未實作功能候選見 [Future_Feature.md](docs/planning/Future_Feature.md)。

---

## [Unreleased]

- **Stage 56 Trial_v6 前置條件統包完成** — Dashboard MockScenarioCard 補全 33 framework_* 場景 + FF 四十二 + FF 四十三 + conventions 補 2 段 + Stage 48 PATHEXT 候選 FF 落地 conventions（不立 FF）+ WorkflowEngine.cs enum/record 殘留 grep 揭露為跨 23 service fundamental type（補 conventions 註明不可移除）。**Trial_v6 開跑前工具完備**。
- **下個動作候選**：① **Trial_v6 排程**（v4 動態架構驗證 — Petra Magentic Orchestration / per-task session 行為驗證 + Stage 56 修法 production 自然驗 token_logs.TotalCostUsd 95%+ 寫入率達標）/ ② FF 三十六 Phase B 動態流程架構評估（v4 路線 9/9 已達成，Stage 56 完成，可進入評估）/ ③ 新立 FF 五十（Dashboard token 統計頁 IsEstimated 視覺區分，Stage 56 範圍變更留的 follow-up）
- **v4 漸進遷移完整路線 9/9 達成 🎉 + Trial_v6 前置條件就緒**
- **FF 四十二 ✅ + FF 四十三 ✅**（Stage 56 完成）
- **Stage 48 PATHEXT 候選 FF 落地** ✅ — 寫入 `docs/conventions/csharp.md`（Linux Docker production 無此問題不立 FF，Windows dev 機 onboarding 預警）
- **新立 FF 五十**（Stage 56 follow-up）：Dashboard token 統計頁 IsEstimated 視覺區分（estimated 標記 + tooltip）

---

## [3.45.0] — 2026-05-05 — [Stage 56](docs/planning/Stage_56_Roadmap.md) Trial_v6 前置條件統包 — Dashboard MockScenarioCard 補全 33 場景 + FF 四十二/四十三 修 + conventions 補 2 段

v4 路線 9/9 達成後第一個觀察類整理 Stage，Trial_v6 開跑前工具完備。**4 件事一氣呵成**：① Dashboard MockScenarioCard 補全 33 framework_* 場景（Stage 49-55B 全到位）② FF 四十三 修（路線 b + spike-2 選項 B）— TotalCostUsd 99.7% NULL → 100% 寫入；兩 path 中央寫入點補 + 新建 TokenCostEstimator + IsEstimated flag + Migration ③ FF 四十二 修 — TryParseDesignIssues 改 line-iteration + try-deserialize pattern + 新建 AiTeam.Bot.Tests xUnit project ④ conventions 補 2 段（WorkflowType/WorkflowStep fundamental type + Stage 48 PATHEXT 解法落地）。**Aria 閘門一 4 critical 揭露**（Stage 47 model pricing 設施前提錯誤 / API path 修法位置模糊 / AiTeam.Bot.Tests 不存在 / 兩 path 根因混淆）→ Forge Plan Mode 二輪修正 + 議題 spike-2 三選項拍 B（hardcoded dict）。**範圍變更**：子項 7 Dashboard 視覺區分跳過 → 立 FF 五十 follow-up。**0 follow-up bug**。**Aria 校準錨 ×0.92**（272K / 中位 297K，混合型第 10 資料點 mid 帶下半，10 資料點區間穩定 ×0.73-1.42）。詳見 Stage 56 Roadmap。commits：`8054f64`(主) + `43e5454`(範圍變更補正) + `e8e35ad`(自驗結果)。

## [3.44.0] — 2026-05-05 — [Stage 55B Session B](docs/planning/Stage_55B_Roadmap.md) ⭐ v4 漸進遷移第九步完整結案 — 5 routing types HITL refactor + v4 路線 9/9 達成 🎉

**v4 漸進遷移完整路線 9/9 達成 🎉** — Stage 55B Session B 完成 5 routing types HITL refactor（dev_failed_intervention / qa_failed_intervention / devplan_escalate / dev_plan_unable / split_task_proposal）— Pipeline executor 從 SetIntervention end 改 yield-resume + legacy handler 加 Pipeline 分支（議題 5 = 5A）。**核心戰略級 Forge 缺口 6 揭露**：5 type-specific BossInteraction 在 Pipeline path 下部分已 fire（不是統一 generic intervention）→ refactor 策略**比預期更輕**（不需新加 type-specific BossInteraction）。**首次拆 Session 戰術完整實踐 + Compact know-how 揭露 ⭐**：Forge 用 Compact 模式（vs 新開 Forge session）— Session A 4 戰略議題拍板脈絡保留 + 對話連貫 + Aria 工作量單線程，**比新開 session 更乾淨**。**Stage 55B 整體完整結案** — Session A（v3.43.0 PipelineHitlHelper + 16 處 skip 精簡 + F-α 移除）+ Session B（v3.44.0 5 routing HITL）= **Stage 51 試點 framework HITL pattern 全面 wire 完成**（1 type → 11 type）。**Aria 校準錨整體 ×1.42**（876K = Session A 450K + Session B 426K vs 中位 615K — **混合型新上界**：拆 Session + Compact 戰術 trade-off + 1M compact 風險低）。詳見 Stage 55B Roadmap。Session B commits：`641594d` ~ `194dff1` 10 個 + Session A `6b4c6f9` / `a484ff9`。

## [3.43.0] — 2026-05-04 — [Stage 55B Session A](docs/planning/Stage_55B_Roadmap.md) v4 漸進遷移第九步（拆 Session A/B 第一段）— PipelineHitlHelper + AppealOrchestrationService 16 處 skip 精簡 + F-α 排除條件移除

**Stage 55B Session A only**（v4 漸進遷移第九步拆 55A/55B 第一段拆 Session A/B）— Session B 留 v3.44.0。**4 戰略議題 Christ 拍板**：① **1A** proposal 留 Stage 56（Forge spike F6 揭露 ProposalConfirmationService group lifecycle 整合衝突）② **2C** Pattern A 主 + Stage 51 試點 mid_interrupt 獨立保留 ③ **3A** intervention/merge_notify 留 fire-and-forget（ack only no routing 切 yield-resume 收益 = 0）④ **4A** 拆 Session B（5 routing HITL 規模 ~600-900 LOC，**首次拆 session**）。**Session A 範圍**：① PipelineHitlHelper 共用 helper（議題 2C 比 base class 更務實）② AppealOrchestration 11 + QaCoordination 5 = 16 處 skip 全清（dead code 結論）③ F-α 排除條件 4 處移除 ④ 8 處 calling site comment ⑤ Production DB pre-check（Forge 主動，dead code 移除 production safe）。**Aria 校準錨 Session A ×0.73**（450K / 中位 615K，**半 Stage 校準錨計算非典型** — 完整 Stage 55B 校準錨等 Session B 結案後重新評估）。**Stage 55B 整體 8.5/9 達成**。詳見 Stage 55B Roadmap。commits：`6b4c6f9`(主) + `a484ff9`(結案)。

## [3.42.0] — 2026-05-04 — [Stage 55A](docs/planning/Stage_55A_Roadmap.md) v4 漸進遷移第八步（拆 55A/55B 第一段）Kickoff/Design 整合到 Pipeline + sub-task 整合 + 6 hooks 移除 + 刪 WorkflowEngine.cs

**議題 G3 真正解決** — Pipeline 從 Kickoff 階段啟動（Stage 53A 方案 C 留的核心戰略級 TODO）。4 件子工作合一：① inner FrameworkKickoffRouter / FrameworkDesignRouter 加 skipFinalize + 改回傳 Outcome record ② Pipeline 主 Workflow 加 KickoffStage / DesignStage Executor + 拓撲擴展（5→7 RequestPort + 8→10 stage）③ MeetingOrchestrationService HandleKickoff/Design ConfirmedAsync continue/stop 走 Pipeline ResumeAfterKickoff/Design ④ sub-task 整合（FireOneStepAsync entry guard 兩入口分流：parent → Kickoff / sub-task → Dev_plan + PipelineState.IsSubTask + PipelineStartExecutor 兩出口路由）⑤ HandleAgentCompletedAsync 6+1 hooks 全段移除 ⑥ WorkflowEngine.cs 精簡（保留 enum + record）。**Forge Plan Mode 主動揭露 3 個 Aria 預掃缺口拍板**（method 名已存在 / sub-task first step ≠ Kickoff / EpicChain 不依賴 6 hooks）。**Aria gate1 揭露 1 critical**（Mock 場景擴充 + Forge 自驗未做 — Forge 不知道剛寫好的 forge-self-verify skill 時序）+ 修正 commit `b6e0764`。**驗收期 follow-up #1（戰略級）**：Forge 自驗場景 E 揭露 Stage 54 既有遺留 bug（split_task_proposal MockMode auto-approve switch 漏）→ 自抓自修 commit `492d2db` — Stage 53B/54 自驗能力進化在 Stage 55A 真正生效。**Christ 視覺驗收 4 張截圖**確認場景 B/E。**Aria 校準錨 ×0.88**（482K / 中位 545K，混合型第 8 資料點 mid 中段；8 資料點區間穩定 ×0.73-1.25）。**9 Stage 遷移 8/9 達成**。詳見 Stage 55A Roadmap。commits：`1cddaef`(主) + `b6e0764`(Aria fix) + `492d2db`(split fix) + `b9365a6`(v2.0)。

## [3.41.0] — 2026-05-04 — [Stage 54](docs/planning/Stage_54_Roadmap.md) v4 漸進遷移第七步 Crash Recovery 全切 + 4 CheckpointStore base class + idempotency

純機制升級 + 重構 + idempotency 加固，無新功能。4 件事一氣呵成：① 抽 4 CheckpointStore base class（833 → 360 行 **-473 淨減**，`FrameworkCheckpointStoreBase<TStore>` generic）② 3 router RecoverStuck*Async 升級 ResumeStreamingAsync（對齊 Stage 53A Pipeline 議題 12 既有 know-how）③ **B2 round-aware idempotency 戰略級修正**（B1 → B2：Forge gate1 揭露 B1 用 state.IssueUrls check 會破壞 needs_adjustment 多輪業務 → Christ 重拍板「Adjustment 觸發都會踩」→ TaskGroups 加 LastIssueCreatedRound int? + Migration `Stage54TaskGroupIssueCreatedMarker`）④ Stage 53B follow-up #1/#2/#4 搭車（MarkGroupDoneOrIntervention 廣義化 / MockMode auto-approve hook / MockClaudeCodeService [Obsolete]）。**驗收 8 場景全綠 + 1 follow-up bug 修復**（Forge 自驗時自抓自修：MockMode auto-approve source='mock' → 'dashboard' — 84bd874）。**Aria 校準錨 ×0.77**（421K / 中位 545K，混合型第 7 資料點 mid 帶下半 — 接近 Stage 53A ×0.73；7 資料點區間穩定 ×0.73-1.25）。**8 Stage 遷移 7/8 達成**，剩 Stage 55 戰略級收尾。詳見 Stage 54 Roadmap。commits：`36033f0`(主) + `84bd874`(fix) + `3c192e3`(驗收) + `82317cd`(v2.0)。

## [3.40.0] — 2026-05-03 — [Stage 53B](docs/planning/Stage_53B_Roadmap.md) ⭐ v4 漸進遷移第六步 子流程 + 5 fallback 移除

NewFeature 主路徑 + 子流程**完整 Pipeline framework 化**達成。11 議題拍板：A1 一個 Stage 全切 / B1 fix loop loop back / C1 拓撲擴張 / D1 4 子流程 + 5 fallback 移除 / F 兩個必修都修（議題 G3 grep 紀律升級 + 議題 12 Agent task 層 mapping +1 entry）/ J1 既有 6 hooks 保留 / K1 mapping helper 保留 switch case。**核心實作**：① 新建 DevFixStageExecutor + Pipeline-DevFixCompletion PortId（5→6 RequestPort + 7→8 stage）② 4 stage Executor 加 routing：Reviewer fix loop / DevPlan appeal self-loop / Dev appeal + intervention / QA fix loop ③ **議題 F-1 16 處 skip 修正**（AppealOrchestration 11 + QaCoordination 5）+ HandleDevBlocker signature 升級給 Pipeline 自接管 routing ④ 5 fallback dispatch 全移除（Completed 語義變更含 intervention）⑤ 6 Mock 場景 dynamic + Round counter + PmRouting Mock 分支。**驗收能力突破**：`/internal/mock/scenario` HTTP API + auto-approve → **Forge 自驗全 6 場景含 SIGTERM/SIGKILL Crash Recovery**（Christ 線下實跑模式從「必要」轉「選擇性」）。**Aria 校準錨 ×0.88**（578K / Charter 中位 655K，混合型第 6 資料點 mid 帶中段；6 資料點區間 ×0.73-1.25 拆 Stage 守區間精神持續驗證）。詳見 Stage 53B Roadmap。commits：`cc07fcf`(主) + `49f4d5a`/`7fbac77`(fix) + `6d473db`(驗收紀錄)。

## [3.39.0] — 2026-05-03 — [Stage 53A](docs/planning/Stage_53A_Roadmap.md) ⭐ v4 漸進遷移第五步 macro pipeline NewFeature happy path

v4 路線最大遷移點之一：**macro-orchestration framework 化首次達成**（vs Stage 49-52「節點內部」單層 framework）。**Aria Session A 子項 5 實作期揭露議題 G3 假設失誤**（inner Meeting router post-meeting actions vs Pipeline 推進職責衝突）→ 即時跨 session 拍板 **方案 C 範圍縮小**：53A 範圍從「整個 pipeline」縮成「Pipeline 從 Dev_plan 啟動 + Kickoff/Design 留 legacy」（規模 -40% 守混合型 ×0.96-1.25 區間 + 戰略價值 ~70% 保留 + Stage 55 收尾統一整合）。**v4 路線 7→8 Stage**（53 拆 53A happy path + 53B 子流程）。核心實作：FrameworkPipelineRouter 4 method（HandlePipeline / ResumeAfterAgent J1 yield-resume / RecoverStuckPipeline 議題 12 升級 ResumeStreamingAsync rehydrate / FinalizePipeline 9 fallback dispatch）+ 9 stage Bridge record + 5 RequestPort + F-α 4 router 排除條件追加 + I2 fallback to legacy 反向設計（Stage 55 收尾移除）。**驗收期 4 follow-up**：① 戰略級 — 議題 G3 同類問題在 QA 重演（規劃紀律必升級為「對所有既有 service finalize/post-completion actions 都 grep」）② NotifyMergeStage 補 ③ 留 Stage 53B ④ Pipeline Recovery 接管 Bot restart 邊界 failed Agent task requeue（Stage 53B/54 必補設計）。新建 14 檔 ~1500 LoC + Migration `Stage53ATaskGroupPipelineFrameworkState`。**Aria 校準錨 ×0.73**（562K / Charter 中位 770K，混合型第 5 資料點 mid 帶下半 — 區間擴展為 ×0.73-1.25；方案 C 拆 Stage + Stage 51 know-how 複用 + 0 Aria gate1 揭露 + 全程一個 session 跑沒拆 Session 四因素疊加）。**Christ 拍板 production 保留 UseFrameworkPipeline=true**。詳見 Stage 53A Roadmap。commits：`296d44e` ~ `c424f67` 7 個。

## [3.38.0] — 2026-05-03 — [Stage 52](docs/planning/Stage_52_Roadmap.md) v4 漸進遷移第四步 Design Meeting B3 路線

議題 A 拆 Stage：原 v4 路線 Stage 52 含「Design + WorkflowEngine pipeline」一氣呵成，Aria 規劃時拆 Stage 守混合型 ×0.96-1.25 區間精神 — Stage 52 = Design Meeting B3 only / Stage 53 = WorkflowEngine macro / Stage 54 = Crash Recovery / Stage 55 = 收尾切 BossInteraction（v4 路線 6→7 Stage）。Design Meeting 三層 Stage 50 沒踩過拓撲擴展：① 條件式 Demi（needsDemi=false short-circuit）② needs_adjustment B2 子流程 ③ 拆 task 提案 router 後置（C2 抽 DesignSplitProposalEvaluator helper SoT）。Spike F1/F2 兩項全綠。**驗收期 2 follow-up**：① fix#1 Mock agentName 識別補（Stage 50 踩坑 #11 預警命中）② **fix#2 戰略級 framework 1.3.0 行為揭露**：「AddEdge type-based dispatch 不 source-aware」— 修法拆 plan executor（Stage 53+ 拓撲設計新預警）。**6 場景全綠**（含 SIGTERM+SIGKILL crash recovery 兩跑）。新建 17 檔 + DesignAdjustmentPlanExecutor + FrameworkDesignRouter 572 行 + Migration `Stage52TaskGroupDesignFrameworkState`。**Aria 校準錨 ×1.05**（609K / Charter 中位 580K，混合型第 4 資料點 mid 中段；混合型 ×0.96-1.25 四資料點區間穩定）。詳見 Stage 52 Roadmap。commits：`3b2343a` + `8b3ead1` + `b5dac50` + `806b22b`/`27ce0b7`(fix) + `d35ec80`/`754ff34`/`d951e6c`。

## [3.37.0] — 2026-05-02 — [Stage 51](docs/planning/Stage_51_Roadmap.md) ⭐ v4 漸進遷移第三步 framework HITL 試點

A3 試點精神 — 不切既有 BossInteraction 10+ type，新建 `framework_kickoff_mid_interrupt` type + `FrameworkHitlBridge` service 橋接層；既有 InteractionService / InteractionProcessor 主流程不動。B1 試點 = Christ 在 Kickoff 多輪會議跑期間 Dashboard 點「中途介入」按鈕 → workflow 跑到 RequestPort 點 yield → 開 BossInteraction → Christ 回應後新 HTTP scope rehydrate workflow（`InProcessExecution.ResumeStreamingAsync` 對齊 spike F3 結論）+ SendResponseAsync → workflow 從 yield 點繼續跑。Spike F1/F2/F3 三項全綠（RequestPort C# stable / ICheckpointStore 對 pending requests 序列化可用 / 跨 HTTP scope rehydrate）。Forge 主動範圍變更（Aria 認可）：trigger flag 改用 in-memory `KickoffMidInterruptTriggerStore`。**6 場景全綠 + 0 follow-up + Aria spike 三項關注點實證通過**（場景 D requestId `0daeccaa...` 跨 process restart stable 證據鏈）。新建 4 檔 ~600 LoC。**Aria 校準錨 ×0.96**（448K / Charter 中位 465K，混合型第 3 資料點 mid 帶下半最低；混合型 ×0.96-1.25 三資料點驗證）。詳見 Stage 51 Roadmap。commits：`67a9b0a` + `e65a4b3` + `3bb7f28`(v2.0)。

## [3.36.0] — 2026-05-02 — [Stage 50](docs/planning/Stage_50_Roadmap.md) v4 漸進遷移第二步

Kickoff Meeting 5 Agent 切 framework Workflow Builder fan-out/fan-in（A2 路線）+ feature flag。Spike E1 ❌ → A2 fallback 路線拍板（framework Group Chat 不支援 multi-speaker per round + Concurrent 不支援 loop back，唯一路徑 `WorkflowBuilder` + `AddFanOutEdge` + `AddFanInBarrierEdge` + `AddSwitch` + loop back）；E2/E3 ✅。**3 follow-up fix（戰略級 framework 1.3.0 fan-out 拓撲首次 production 整合踩坑）**：① RunAsync → RunStreamingAsync（fan-out 必須 streaming dispatch）② 顯式 SendMessageAsync/YieldOutputAsync Executor 加 `[SendsMessage]`/`[YieldsOutput]` + `partial class`（type validation MAFGENWF003）③ Mock Petra 角色識別補。Forge 自驗 6 場景 + 2 bonus 全綠 + 場景 C marker 100% cleared。新建 11 檔 ~1100 LoC + 改 11 檔（KickoffMeetingService 淨刪 213 行）。**Aria 校準錨 ×1.09**（500K / Charter 中位 460K，混合型第 2 資料點 mid 中心）。詳見 Stage 50 Roadmap。commits：`7d37a48` + `24b62dc` + `a50059c`/`cd6d61a`/`1023104`(fix) + `ff6a26f` + `b443546`(v2.0)。

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
