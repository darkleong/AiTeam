# Stage 53B：v4 漸進遷移第六步 — fix loop / appeal / QA fix loop / intervention 子流程切 framework + 5 fallback to legacy 點移除

> 對應 Future Feature：v4 漸進遷移 8 Stage 路線第六步（Stage 53A 拆 53A/53B 後）— 不對應特定 active FF
> 對應版本：**v3.40.0**（v4 漸進遷移第六個產生版本變動的 Stage）
> 建立日期：2026-05-03
> 狀態：📋 計劃書建立完成，待 Forge 開工
> 文件版本：v1.0

---

## 概述

**戰略背景**：[Stage 53A](Stage_53A_Roadmap.md) 完成 NewFeature 主路徑 happy path framework 化（5 Agent stage：DevPlan/Dev/Reviewer/QA/Doc + NotifyMerge）+ I2 反向設計留 5 fallback to legacy 點。**Stage 53B 完成 v4 漸進遷移第六步** — 把 Stage 53A 留的 4 子流程（fix loop / appeal / QA fix loop / intervention）切 framework + 移除 5 fallback to legacy 點，達成 NewFeature 主路徑 + 子流程**完整 Pipeline framework 化**。

**Stage 53A 揭露的兩個戰略級議題在 53B 規劃時必處理**：
1. **議題 G3 在 QA 重演**（Stage 53A follow-up #1）— Aria 規劃前期 grep 紀律升級為「對所有既有 service finalize/post-completion actions 都 grep」。**53B 規劃前期已 grep**：AppealOrchestrationService 4 method 內部 **10+ 處 call site**（HandleDevPlanCompletedAsync 5 處 fire Dev_plan/Dev + UpdateGroupStatus + NotifyBoss / HandleDevBlockerAsync 3 處 / HandleReviewerCompletedAsync 2 處 / RunPetraGateAsync 1 處 SkipReviewerAfterArbitration），53B Pipeline 路徑必須 skip 這些 internal side effects（議題 F-1 修正方案）
2. **議題 12 ResumeStreamingAsync rehydrate 已驗 framework state 層**（Stage 53A follow-up #4）但漏 Agent task 層 failed→requeue 缺口已補（5 PortId → AgentName mapping helper）。**53B 加 DevFixStage 需要 Pipeline-DevFixCompletion PortId + mapping +1 entry**（K1 拍板保留現狀 switch case 擴 6 entries）

**A1 拍板 53B 一個 Stage 全切**（vs 再拆 53B-1/53B-2）— 4 子流程互相 coupling 高（fix loop ↔ Petra 閘門 / Dev_blocker ↔ appeal / QA fix loop ↔ 仲裁後 Dev_fix），拆兩 Stage 邊界難畫；Stage 53A know-how 全複用（RequestPort dual-handler / yield-resume / FrameworkPipelineRouter 4 method / ClearMarkerAndFallbackAsync helper）規模可控。

**範圍邊界（A1 + D1 + E 沿用拍板）**：
- ✅ **4 子流程 framework 化**：fix loop（Reviewer 🔴 → Petra 閘門 → Dev_fix max 3 輪）+ appeal（Dev_plan failed escalate / Dev 阻礙 [BLOCKED]）+ QA fix loop（QaCoordinationService 內部 fire Dev_fix）+ intervention（Dev failed needs_intervention / 仲裁後 Dev_fix）
- ✅ **移除 Stage 53A 留的 5 fallback to legacy 點**（D1 拍板）：reviewer_critical / dev_plan_failed_escalate / dev_blocker / arbitration_skip_reviewer / qa_fix_loop
- ✅ **議題 F-1 修正**：AppealOrchestrationService 4 method 內部 10+ 處 call site 加 Pipeline path skip 判斷（議題 G3 同類修法在 4 method 重演，對齊 Stage 53A QaCoordinationService skip 修法）
- ✅ **Stage 53A follow-up #3 補 dynamic**：Mock 場景 E/F（qa_no_tests / reviewer_fallback）跟 fix loop 一起做
- ✅ **K1 mapping helper 加 1 entry**：5 → 6 PortId（Pipeline-DevFixCompletion）對齊 K1 保留 switch case 擴展
- ❌ **不動**：HandleAgentCompletedAsync 既有 6 hooks（J1 拍板保留作為 legacy fallback safety net，feature flag false 時可用）
- ❌ **不動**：sub-task（E 沿用 Stage 53A 排除 ParentGroupId == null，留 Stage 55 收尾統一整合 Kickoff/Design + sub-task）
- ❌ **不動**：既有 BossInteraction 10+ type / InteractionService（A3 試點精神延續，留 Stage 55 收尾 BossInteraction 切 framework HITL）

**v4 路線第六步風險預警**：
- **議題 F-1 多處 skip 修正規模**：AppealOrchestrationService 4 method 內部 10+ 處 call site 加 Pipeline path skip — 跟 Stage 53A QaCoordinationService 1 處 skip 規模差距大，對齊紀律但工作量增加
- **fix loop max 3 輪計數**：對齊 WorkflowEngine.MaxFixIterations=3（既有常數）+ group.FixIteration DB 欄位（既有），ReviewerStage 第二 handler 內判 group.FixIteration >= 3 → SendMessage intervention
- **HandleQaCompletedAsync passed 路徑 fire Doc 衝突**（Stage 53A follow-up #1 已修 QaStage Executor 內 skip QaCoordinationService 自動 fire next） — 53B QA fix loop 路徑也要對齊，HandleQaCompletedAsync 內 fire Dev_fix（QaFixRound > 0）路徑同樣需 Pipeline path skip
- **5 fallback 點移除順序**：4 子流程 framework 化完成 + 驗證通過 → 才能移除 5 fallback 點（避免「子流程沒做完先移 fallback → fix loop 觸發時無路可走」）

→ feature flag UseFrameworkPipeline=true 為主要安全網（Stage 53A 已 production 啟用，53B 沿用 H1 不加新 flag）。

---

## 設計決策（Christ 2026-05-03 拍板）

### 主路線拍板（戰略級）

| 議題 | 拍板 | 替代方案 |
|---|---|---|
| **議題 A：Stage 53B 範圍** | **A1：一個 Stage 全切** 4 子流程 + 5 fallback 移除 + Mock 補驗 — 4 子流程互相 coupling 高（fix loop ↔ Petra 閘門 / Dev_blocker ↔ appeal / QA fix loop ↔ 仲裁後 Dev_fix），拆兩 Stage 邊界難畫；Stage 53A know-how 全複用規模可控 | A2 再拆 53B-1（fix loop + Petra 閘門）+ 53B-2（appeal + QA fix loop + intervention + 5 fallback 移除）|
| **議題 B：fix loop framework 拓撲設計** | **B1：在現有 Pipeline 拓撲加 loop back edge**（ReviewerStage → DevFixStage → 回 ReviewerStage，max 3 輪迴圈，對齊 Stage 50 KickoffWorkflowFactory loop back pattern）| B2 新獨立 sub-Workflow（FixLoop sub-Workflow）+ Pipeline 主呼叫（引入 framework-in-framework 第二次踩坑風險）|
| **議題 C：Pipeline Workflow 拓撲擴展方式** | **C1：現有 Pipeline 拓撲擴張**（加 fix loop edges + DevFixStage Executor + Petra 閘門 dual-handler routing）— 對齊議題 G3 修正方案 C 精神（Pipeline 主迴圈統一接管推進）+ 避免 framework-in-framework 第二次踩坑 | C2 新 Workflow 跟 Pipeline 並存（規模放大）|
| **議題 D：5 fallback 點移除策略** | **D1：53B 一次完成 4 子流程 + 5 fallback 點移除**（避免 53B-1/53B-2 兩輪重複工作；Stage 55 收尾範圍應守「Kickoff/Design + sub-task 整合 + BossInteraction 切 framework HITL」三戰略級工作）| D2 53B 先完成 4 子流程 / 5 fallback 移除留 Stage 55 |
| **議題 E：sub-task 整合** | **沿用 Stage 53A 議題 A 確認 2 拍板** — sub-task 仍排除（FireOneStepAsync 分流條件保留 `ParentGroupId == null`），守 Stage 55 收尾統一整合 Kickoff/Design + sub-task 三戰略級工作精神 | 53B 處理 sub-task（規模放大 + 跟 Stage 46 機制 coupling 高）|
| **議題 F：Aria 規劃紀律升級（53A 揭露的兩個戰略級議題必修）** | **F-1（必修 1）議題 G3 在 QA 重演** — 規劃前期 grep 紀律升級為「對所有既有 service finalize/post-completion actions 都 grep」。**53B 規劃前期已 grep AppealOrchestrationService 4 method 內部 10+ 處 call site**，53B Pipeline 路徑必須 skip 這些 internal side effects（對齊 Stage 53A QaCoordinationService skip 修法）。**F-2（必修 2）議題 12 Agent task 層 mapping**：5 PortId → AgentName mapping helper（Stage 53A follow-up #4）加 DevFixStage Pipeline-DevFixCompletion 第 6 entry | 不修（守 Stage 53A 教訓不放棄）|
| **議題 G：spike 第一步驗範圍** | **G1：spike 範圍極窄**（不另建 spike 程式片段，read 即可） — Stage 50 KickoffWorkflowFactory loop back pattern 已驗 + Stage 53A RequestPort + ResumeStreamingAsync 已驗，53B 是「同 pattern 不同子流程」的擴展。Forge Plan Mode 第一步 read 對齊 Stage 50 loop back + Stage 53A FrameworkPipelineRouter 即可 | G2 仍需 spike fix loop 計數 + Petra 閘門 callback resume 整合（過度 spike）|
| **議題 H：feature flag 顆粒度** | **H1：沿用單一 `Workflow:UseFrameworkPipeline`** + 三 flag 連動（53B 完成後 flag ON = 全 Pipeline framework path 涵蓋 happy path + 4 子流程 + 5 fallback 移除）；對齊 Christ production 已拍板「保留 UseFrameworkPipeline=true」精神 — 53B 完成 push 後 production 自然涵蓋全 Pipeline | H2 加 UseFrameworkPipelineFixLoop 第二 flag（漸進啟用，但維護成本 + Stage 55 收尾還要移除 flag）|
| **議題 I：Mock 場景設計（I-mid 6 場景）** | `framework_pipeline_fix_loop_recover_round1` / `framework_pipeline_fix_loop_max_iter` / `framework_pipeline_dev_blocker_appeal` / `framework_pipeline_qa_no_tests_dynamic` ⭐（**Stage 53A follow-up #3 補 dynamic**）/ `framework_pipeline_reviewer_fallback_dynamic` ⭐（**Stage 53A follow-up #3 補 dynamic**）/ `framework_pipeline_fix_loop_crash_recovery`（議題 12 Agent task 層整合驗證）— 對齊 Stage 49-53A 6 場景慣例；dev_plan_failed_escalate dynamic / Dev failed intervention dynamic / 仲裁後 Dev_fix dynamic 由 Forge 改 prompt 代驗（機制相似不獨立 Mock）| I-min 4 場景（漏 Crash Recovery / max_iter）/ I-full 8+ 場景（over-coverage Trial 跑完整流程已涵蓋）|
| **議題 J：HandleAgentCompletedAsync 既有 6 hooks 處理** | **J1：既有 6 hooks 保留作為 legacy fallback safety net** — feature flag false 時走 legacy 6 hooks 機制完整可用（feature flag true 時 framework path 接管）；對齊 Stage 49/50/52 既有「並存設計」慣例。**D1 + J1 共存**：D1 移除「5 fallback 點」 = framework path 內 Pipeline Executor 主動 call legacy method 的 fallback bridge；J1 保留「6 hooks」 = legacy path 的判斷邏輯，兩者不同層級不衝突 | J2 53B 移除既有 6 hooks（違反並存設計）/ J3 Stage 55 統一移除（Stage 55 範圍應守三戰略級工作）|
| **議題 K：Crash Recovery Agent task 層 mapping helper** | **K1：保留現狀（switch case 直接擴 6 entries）** — 守「3 次再抽象」原則，53B 加 1 個 PortId 不算戰略級擴張；Stage 55 收尾評估抽 base class（連同 4 CheckpointStore 抽象議題） | K2 抽 PortIdToAgentNameMapping helper class（規模放大，跟 4 CheckpointStore 議題不對等） |

### Aria 拿捏（已決）

| # | 議題 | 決定 |
|---|---|---|
| 1 | DB schema | **不加新欄位** — 沿用 Stage 53A `task_groups.PipelineFrameworkStateJson` + 既有 `FixIteration` / `QaFixRound` / `SkipReviewerAfterArbitration` 既有 fix loop / appeal 欄位 |
| 2 | ActiveOrchestration marker | 沿用 Stage 53A `"FrameworkPipeline"`（雙 marker 不變）|
| 3 | 入口分流位置 | 沿用 Stage 53A 既有兩處（FireOneStepAsync line 478 / HandleAgentCompletedAsync line 168 後）— 不動 |
| 4 | F-α 4 router 排除條件 | 沿用 Stage 53A 既有 4 router 各 +1 行 `&& g.PipelineFrameworkStateJson == null` — 不動 |
| 5 | 議題 F-1 多處 skip 修正位置（10+ 處）| AppealOrchestrationService 4 method 內部 fire/MarkDone/NotifyBoss side effects 全加 `if (group.PipelineFrameworkStateJson != null) skip` 判斷（對齊 Stage 53A QaCoordinationService skip 修法）：HandleDevPlanCompletedAsync 5 處（fire Dev_plan/Dev + UpdateGroupStatus + NotifyBoss）+ HandleDevBlockerAsync 3 處 + HandleReviewerCompletedAsync 2 處 + RunPetraGateAsync 1 處（SkipReviewerAfterArbitration） |
| 6 | QaCoordinationService 修正 | Stage 53A follow-up #1 已修 QaStage Executor skip QaCoordinationService 自動 fire next（passed 路徑 fire Doc）；53B QA fix loop 路徑（fire Dev_fix）也要對齊，HandleQaCompletedAsync 內 fire Dev_fix 處同樣需 Pipeline path skip |
| 7 | Token 計費 | 沿用既有機制 — fix loop / appeal 各 LLM call 走既有 ClaudeCodeService / MeetingCommons.RunAgentTurnAsync tokenLogService |
| 8 | CLAUDE_*.md prompt | 不動（沿用 Stage 49-53A 慣例）|
| 9 | BossInteraction 整合 | A3 試點精神延續 — 沿用既有 intervention BossInteraction type；Stage 53B 主 Workflow 不引入新 BossInteraction type |
| 10 | Stage Executor 命名 | 新建：`DevFixStageExecutor`（fix loop 用，跟 DevStage 不同因為 IsFixLoop=true）；既有 ReviewerStage / DevPlanStage / DevStage / QaStage 加 routing 邏輯 |
| 11 | Mock 場景觸發機制 | 對齊 Stage 49-53A `MockClaudeCodeService.FailScenario` static 傳遞 scenario key 慣例 |
| 12 | 5 fallback 點移除順序 | 4 子流程 framework 化完成 + Mock 場景驗證通過 + dotnet build 0 Error → 才移除 5 fallback 點（避免子流程沒做完先移 fallback → fix loop 觸發時無路可走） |

### Stage 53B 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **0** | **Spike 第一步：G1 read 範圍**（Forge Plan Mode 第一步）— read Stage 50 KickoffWorkflowFactory loop back pattern + Stage 53A FrameworkPipelineRouter 4 method + AppealOrchestrationService 4 method 既有結構（議題 F-1 規劃前期 grep 已做） | XS |
| **1** | DevFixStageExecutor + Pipeline-DevFixCompletion PortId + DevFixCompletionRequest/Response 型別 + K1 mapping helper +1 entry | M |
| **2** | ReviewerStageExecutor 加 fix loop routing：Petra fail（CriticalReviewCount > 0）→ 判 group.FixIteration >= 3 → intervention；< 3 → SendMessage DevFixStageBridge 觸發 fix loop；既有 Petra approve / Vera Skipped / Vera failed 三 routing 不動 | M |
| **3** | DevPlanStageExecutor 加 appeal routing：Dev_plan failed → 內 call AppealOrchestrationService.HandleDevPlanCompletedAsync（Pipeline path skip 內部 side effects）→ 視結果（Petra approve retry / escalate intervention）routing | M |
| **4** | DevStageExecutor 加 appeal routing：Dev [BLOCKED] → 內 call AppealOrchestrationService.HandleDevBlockerAsync（Pipeline path skip）→ 視結果 routing；DevStageExecutor 加 intervention routing：Dev failed → set NeedsIntervention + NotifyBossDevFailedInterventionAsync | S |
| **5** | QaStageExecutor 加 QA fix loop routing：HandleQaCompletedAsync 內 fire Dev_fix 路徑（QaFixRound > 0）由 Pipeline path skip 後，QaStage 自己 SendMessage DevFixStageBridge 觸發 fix loop；QA failed / intervention routing 不動（Stage 53A 已含） | S |
| **6** | 議題 F-1 修正：AppealOrchestrationService 4 method 內部 10+ 處 call site 加 `if (group.PipelineFrameworkStateJson != null) skip` 判斷（規劃前期 grep 已揭露具體位置）+ QaCoordinationService 內 fire Dev_fix 處對齊 skip | M |
| **7** | PipelineWorkflowFactory 拓撲擴展：加 DevFixStage Executor + 6 PortId（5 → 6）+ fix loop 拓撲 edge（ReviewerStage → DevFixStage → ReviewerStage loop back）+ DevPlanStage / DevStage / QaStage routing 出口（appeal escalate / fix loop / intervention） | M |
| **8** | FrameworkPipelineRouter.ResumeAfterAgentAsync mapping helper +1 entry（Pipeline-DevFixCompletion → "Dev_fix"）；FinalizePipelineAsync 移除 5 fallback dispatch（reviewer_critical / dev_plan_failed_escalate / dev_blocker / arbitration_skip_reviewer / qa_fix_loop case 全刪），保留邊界 reason（dev_failed / qa_failed / qa_intervention / doc_failed / group_not_found）；改 Completed=false fallback path 純 log + 清 marker（不主動 call legacy，因 fix loop / appeal / intervention framework 已接管） | S |
| **9** | Stage Executor ClearMarkerAndFallbackAsync helper 移除（5 fallback 點移除後不再需要時序紀律 helper）；ReviewerStage / DevPlanStage / DevStage / QaStage 對應 fallback SendMessage 改為直接 SendMessage 下一 stage / 結束 Workflow | XS |
| **10** | Mock 場景擴充 6 個 framework_pipeline_fix_loop_* / dev_blocker_appeal / qa_no_tests_dynamic / reviewer_fallback_dynamic + Forge 自驗 6 場景 + Crash Recovery 場景 SIGTERM/SIGKILL 兩跑（議題 12 Agent task 層整合驗證）| M |
| **11** | Version bump v3.40.0 + 結案文件（Roadmap 實作紀錄章節 + CHANGELOG / Future_Feature 同步交給 Aria 結案第二段） | XS |

> **不寫工時估算**（workflow_aria.md 第三節 A 第 4 條）— 各子項規模見上表 XS/S/M 範圍描述。

---

## 子項 0：Spike 第一步 — G1 read 範圍（規模極窄）

### read 對齊範本

| # | read 對象 | 目的 |
|---|---|---|
| F1 | `src/AiTeam.Bot/Workflows/Kickoff/KickoffWorkflowFactory.cs` | Stage 50 既有 loop back pattern（AddSwitch + max_iter 邊界），對齊 53B fix loop max 3 輪 routing |
| F2 | `src/AiTeam.Bot/Workflows/Pipeline/PipelineWorkflowFactory.cs` | Stage 53A 既有拓撲，53B 擴展加 DevFixStage + fix loop edge 對齊既有 5 RequestPort + 7 stage Executor pattern |
| F3 | `src/AiTeam.Bot/Orchestration/Meeting/FrameworkPipelineRouter.cs` 4 method | Stage 53A 既有 ResumeAfterAgentAsync mapping helper + FinalizePipelineAsync 5 fallback dispatch + ClearMarkersAsync helper — 53B 加 1 mapping entry + 移除 5 fallback case |
| F4 | `src/AiTeam.Bot/Orchestration/Appeal/AppealOrchestrationService.cs` 4 method 內部 10+ 處 call site | 議題 F-1 規劃前期 grep 已做（HandleDevPlanCompletedAsync 5 / HandleDevBlockerAsync 3 / HandleReviewerCompletedAsync 2 / RunPetraGateAsync 1）— 53B 實作期再 grep 確認具體位置 + 加 Pipeline path skip 判斷 |

### Spike 結案產出（Forge Plan Mode 內含）

- 路線拍板紀錄寫進 Plan 檔最前段（fix loop 拓撲 + Petra 閘門整合 + intervention 結束流程）
- F1-F4 read 證據引用（既有 line 號 + 對齊 pattern）
- 議題 F-1 多處 skip 具體位置 list（10+ 處 call site）

### Spike 階段失敗條件（極低風險）

Stage 53A know-how 全複用，無新 framework 機制需驗。若實作期揭露 fix loop loop back + Petra 閘門 callback resume 整合衝突 → 暫停 + 回報 Christ 評估。

---

## 子項 1-9：實作細節（對齊 Aria 拿捏 + 議題 F-1）

> 詳細實作位置 / 程式碼片段由 Forge Plan Mode 拍板。Aria 計劃書層級提供 scope + 邊界。

### 子項 1：DevFixStageExecutor + 新 PortId

新建 `src/AiTeam.Bot/Workflows/Pipeline/Executors/DevFixStageExecutor.cs`（對齊 Stage 53A DevStageExecutor pattern）：
- dual handler：HandleEntryAsync(DevFixStageBridge) → enqueue Dev (IsFixLoop=true) → SendMessageAsync(DevFixCompletionRequest) yield；HandleResponseAsync(DevFixCompletionResponse) → 判 result.Success → SendMessage ReviewerStageBridge（重審）/ Dev failed → fallback intervention
- 新 record type：DevFixStageBridge / DevFixCompletionRequest / DevFixCompletionResponse（對齊 Stage 52 fix#2 type-explicit Bridge record 紀律）
- PipelineWorkflowFactory 加 PortId 常數：`Pipeline-DevFixCompletion`

### 子項 2-5：4 子流程 routing

| stage | 加 routing 邏輯 |
|---|---|
| **ReviewerStage** | Petra fail（CriticalReviewCount > 0）→ 判 group.FixIteration >= 3（對齊 WorkflowEngine.MaxFixIterations=3）→ intervention（YieldOutput 結束 Workflow）；< 3 → group.FixIteration++ + SendMessage DevFixStageBridge 觸發 fix loop。**移除原 reviewer_critical fallback** |
| **DevPlanStage** | Dev_plan failed（result.Success=false）→ 內 call AppealOrchestrationService.HandleDevPlanCompletedAsync（議題 F-1 Pipeline path skip 內部 side effects）→ 視 method 回傳值 routing：Petra approve retry → SendMessage DevPlanStageBridge 自身重跑 / escalate intervention → SendMessage intervention 結束。**移除原 dev_plan_failed_escalate fallback** |
| **DevStage** | Dev [BLOCKED]（result.Summary.StartsWith("[BLOCKED]")）→ 內 call AppealOrchestrationService.HandleDevBlockerAsync（議題 F-1 Pipeline path skip）→ 視結果 routing；Dev failed（!result.Success && !IsBlocked）→ set NeedsIntervention + NotifyBossDevFailedInterventionAsync → YieldOutput 結束。**移除原 dev_blocker / dev_failed fallback**（dev_failed 仍保留作為邊界 reason，但路徑 framework 內處理） |
| **QaStage** | HandleQaCompletedAsync 內 fire Dev_fix 路徑（QaFixRound > 0）由議題 F-1 修正 QaCoordinationService Pipeline path skip 後，QaStage 第二 handler 重讀 group 檢查 QaFixRound > 0 → SendMessage DevFixStageBridge 觸發 fix loop；既有 QA passed → DocStageBridge / QA failed / intervention routing 不動。**移除原 qa_fix_loop fallback** |

### 子項 6：議題 F-1 修正（10+ 處 skip）

對齊 Stage 53A QaCoordinationService skip 修法 — AppealOrchestrationService 4 method 內部 fire/MarkDone/NotifyBoss side effects 全加：

```
if (group.PipelineFrameworkStateJson != null)
{
    // Pipeline path 接管推進，skip 既有 service 內部 side effect
    return; // 或 continue 視具體 method 結構
}
// 既有 legacy side effect 邏輯
```

具體位置（規劃前期 grep 已揭露，Forge 實作期再 grep 確認）：
- HandleDevPlanCompletedAsync line 320 / 340 / 352 / 371 / 384 / 391 / 397 / 399 / 404 / 320 等 5+ 處 fire/UpdateStatus/NotifyBoss
- HandleDevBlockerAsync line 251 fire Dev / 256 / 266 UpdateStatus 共 3 處
- HandleReviewerCompletedAsync line 181 UpdateStatus / 184 NotifyBossInterventionAsync 共 2 處
- RunPetraGateAsync line 210 SkipReviewerAfterArbitration set 共 1 處

QaCoordinationService 內 fire Dev_fix 處對齊 skip（Stage 53A follow-up #1 已修 fire Doc 處）。

### 子項 7：PipelineWorkflowFactory 拓撲擴展

加：
- DevFixStage Executor + Pipeline-DevFixCompletion RequestPort
- fix loop edge：ReviewerStage → DevFixStage → ReviewerStage（loop back）
- DevPlanStage / DevStage / QaStage routing 出口擴增（appeal escalate / fix loop / intervention）

對齊 Stage 53A 既有拓撲設計（5 → 6 RequestPort + 7 → 8 stage Executor）。

### 子項 8：FrameworkPipelineRouter 修改

| Method | 修改 |
|---|---|
| `BuildAgentCompletionResponse` | 加 `"Dev_fix" => (PipelineWorkflowFactory.DevFixCompletionPortId, new DevFixCompletionResponse(result))` 第 6 entry（K1 拍板 switch case 擴）|
| `FinalizePipelineAsync` | **移除 5 fallback dispatch case**：reviewer_critical / dev_plan_failed_escalate / dev_blocker / arbitration_skip_reviewer / qa_fix_loop；保留邊界 reason case（dev_failed / qa_failed / qa_intervention / doc_failed / group_not_found），改成純 log + 清 marker（不主動 call legacy，因 fix loop / appeal / intervention framework 已接管） |

### 子項 9：Stage Executor ClearMarkerAndFallbackAsync helper 移除

5 fallback 點移除後不再需要時序紀律 helper（無 race condition 風險）。ReviewerStage / DevPlanStage / DevStage / QaStage 對應 fallback SendMessage 改為直接 SendMessage 下一 stage / YieldOutput 結束 Workflow。

---

## 子項 10：Mock 場景擴充 + Forge 自驗

### Mock 6 場景（議題 I-mid）

| 場景 key | 行為 |
|---|---|
| `framework_pipeline_fix_loop_recover_round1` | Round 1 Reviewer 🔴 → Petra 閘門 fail → DevFixStage Round 2 ✅ → ReviewerStage 重審 passed → QaStage → DocStage → NotifyMerge |
| `framework_pipeline_fix_loop_max_iter` | 連續 3 輪 Reviewer 🔴 → Petra 閘門 3 次 fail → group.FixIteration=3 → ReviewerStage YieldOutput intervention 結束 |
| `framework_pipeline_dev_blocker_appeal` | Dev 第 N 輪回 [BLOCKED] → DevStage 內 call HandleDevBlockerAsync（Pipeline path skip 內部 side effects）→ Petra 評估 → routing |
| `framework_pipeline_qa_no_tests_dynamic` ⭐ | **Stage 53A follow-up #3 補 dynamic** — QaStage 內 call HandleQaCompletedAsync no_tests routing 真實跑通 |
| `framework_pipeline_reviewer_fallback_dynamic` ⭐ | **Stage 53A follow-up #3 補 dynamic** — Vera Critical + Petra fail → fix loop 真實跑通（vs Stage 53A 靜態審視通過）|
| `framework_pipeline_fix_loop_crash_recovery` | fix loop Round 2 DevFixStage yield 期間 docker compose restart → ResumeStreamingAsync 恢復 + DevFixStage callback 推進到 ReviewerStage（議題 12 Agent task 層整合驗證 + K1 mapping helper +1 entry 驗證） |

dev_plan_failed_escalate dynamic / Dev failed intervention dynamic / 仲裁後 Dev_fix dynamic 由 Forge 改 prompt 代驗（機制相似不獨立 Mock）。

### Forge 自驗（對齊 Stage 53A 範圍 — 4 dynamic + 2 靜態 / 全 6 dynamic）

對齊 Stage 53A forge-end SOP：建議 Forge 跑 4 dynamic（fix loop main / max_iter / qa_no_tests / reviewer_fallback）+ 2 靜態（dev_blocker_appeal / fix_loop_crash_recovery 路徑審視）+ Christ 線下實跑 fix_loop_crash_recovery（SIGTERM/SIGKILL 兩跑）。實際範圍 Forge 結案期決定。

---

## 驗收情境

> Stage 53B 是 v4 漸進遷移第六步 4 子流程 framework 化 + 5 fallback 移除，**驗收必須含 fix loop max 3 輪邊界 + appeal 路徑 + QA fix loop dynamic + Crash Recovery 完整循環**。沿用 Stage 49-53A 6 場景模式擴充。

### 場景 A：UseFrameworkPipeline = false → Stage 49/50/51/52/53A + legacy 4 子流程行為不變

**怎麼觸發**：
1. push Stage 53B commit → CI/CD 部署
2. Dashboard SystemSettings 切 `UseFrameworkPipeline = false`（暫時 toggle off — Christ production 已拍板保留 true，驗收期暫切 off）
3. 跑 `/mock new_feature_with_proposal` 走完整新功能流程含 fix loop 路徑

**怎麼驗證**：
- ✅ pipeline 走 legacy `TaskGroupService.HandleAgentCompletedAsync` 既有 6 hooks（J1 保留 safety net）
- ✅ Bot log 沒有 `[Stage53A]` framework path 訊息
- ✅ 既有 fix loop / appeal / QA fix loop / intervention 路徑跑通
- ✅ AppealOrchestrationService 4 method 內 Pipeline path skip 判斷 falsy（PipelineFrameworkStateJson == null）→ 既有 side effects 全跑

### 場景 B：三 flag true + fix loop main path → Round 1 fail → Round 2 passed → 推進 QA

**怎麼觸發**：
1. UseFrameworkPipeline = true（三 flag 連動）
2. 跑 `/mock framework_pipeline_fix_loop_recover_round1`

**怎麼驗證**：
- ✅ Bot log `[Stage53A] HandlePipelineAsync framework path 接管`
- ✅ Round 1 Reviewer 🔴 → Petra 閘門 fail → ReviewerStage SendMessage DevFixStageBridge（**不**走 reviewer_critical fallback，已移除）
- ✅ DevFixStage 跑 Dev (IsFixLoop=true) Round 2 → ReviewerStage 重審 passed → QaStage 推進
- ✅ group.FixIteration = 1（Round 2 ✅ 後不再 increment）
- ✅ NotifyMergeStage 完成 + Pipeline marker 清

### 場景 C：fix loop max 3 輪 → intervention（max_iter 邊界）

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_pipeline_fix_loop_max_iter`

**怎麼驗證**：
- ✅ Round 1/2/3 Reviewer 🔴 → Petra 閘門 3 次 fail → group.FixIteration=3
- ✅ ReviewerStage 第 4 次入 → 判 FixIteration >= 3（對齊 WorkflowEngine.MaxFixIterations）→ YieldOutput intervention 結束 Workflow
- ✅ group.Status = NeedsIntervention + InterventionReason 寫入
- ✅ Discord NotifyBossInterventionAsync 開卡

### 場景 D：Dev [BLOCKED] → appeal 路徑 ⭐

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_pipeline_dev_blocker_appeal`

**怎麼驗證**：
- ✅ DevStage 第二 handler 偵測 result.Summary.StartsWith("[BLOCKED]") → 內 call HandleDevBlockerAsync
- ✅ HandleDevBlockerAsync 內部 fire/UpdateStatus/NotifyBoss 3 處 side effects 全 skip（議題 F-1 Pipeline path skip 判斷）
- ✅ Pipeline 內接管 routing（Petra 評估後 Pipeline 自己 SendMessage 下一 stage / 結束）
- ✅ **不**走 dev_blocker fallback（已移除）

### 場景 E：QA no_tests routing dynamic（Stage 53A follow-up #3 補驗）⭐

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_pipeline_qa_no_tests_dynamic`

**怎麼驗證**：
- ✅ QaStage 第二 handler 內 call HandleQaCompletedAsync no_tests routing 真實跑通（vs Stage 53A 靜態審視）
- ✅ HandleQaCompletedAsync 內部 fire/MarkDone 處 skip Pipeline path（議題 F-1 對齊修正含 QaCoordinationService）
- ✅ QaStage 自己判 group 狀態 routing 推進 Doc

### 場景 F：Reviewer 🔴 + Petra fail dynamic（Stage 53A follow-up #3 補驗）⭐

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_pipeline_reviewer_fallback_dynamic`

**怎麼驗證**：
- ✅ Vera Critical → ReviewerStage 內 call RunPetraGateAsync → Petra fail → SendMessage DevFixStageBridge fix loop 真實跑通
- ✅ **不**走 reviewer_critical fallback（已移除）
- ✅ DevFixStage Round 2 → ReviewerStage 重審 → 跑通

### 場景 G（含於 6 場景）：fix loop Crash Recovery（**Christ 線下實跑** SIGTERM/SIGKILL 兩跑）

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_pipeline_fix_loop_crash_recovery`
3. fix loop Round 2 DevFixStage yield 期間 Forge 執行 `docker compose restart aiteam-bot`（**Christ 授權的 ops 操作**）

**怎麼驗證**：
- ✅ 重啟前 PipelineFrameworkStateJson != null + state.CurrentStage="Dev_fix"
- ✅ 重啟後 RecoverStuckFrameworkPipelineAsync ResumeStreamingAsync rehydrate state（議題 12 升級驗證）
- ✅ DevFixStage callback 觸發 ResumeAfterAgentAsync → 比對 Pipeline-DevFixCompletion PortId（K1 mapping helper +1 entry 驗證）→ SendResponseAsync → ReviewerStage 重審推進
- ✅ 跨 process restart Pipeline-DevFixCompletion requestId stable（沿用 Stage 51 試點 know-how）

---

## 風險點 / 注意事項

### 1. 議題 F-1 多處 skip 修正規模（中-高）

**風險**：AppealOrchestrationService 4 method 內部 10+ 處 call site 加 Pipeline path skip — 比 Stage 53A QaCoordinationService 1 處規模大很多。每處 skip 判斷必須對齊 method 結構（return / continue / 略過某段邏輯），實作期需 grep 全 callers 確認。

**緩解**：
- 子項 6 獨立列出，Forge 實作期專注修正
- 規劃前期 grep 已揭露具體位置（HandleDevPlanCompletedAsync 5 處 / HandleDevBlockerAsync 3 處 / HandleReviewerCompletedAsync 2 處 / RunPetraGateAsync 1 處），Forge Plan Mode 第一步再確認
- 對齊 Stage 53A QaCoordinationService skip 修法 pattern

### 2. fix loop loop back + Petra 閘門 callback resume 整合（中）

**風險**：framework Workflow 內 fix loop loop back（ReviewerStage → DevFixStage → ReviewerStage）需要 RequestPort callback resume 兩次（Dev_fix completion → Reviewer completion）+ ReviewerStage 第二 handler 內含同步 RunPetraGateAsync — 整合 sequence 複雜度高。

**緩解**：
- 對齊 Stage 50 KickoffWorkflowFactory loop back pattern（max_iter 邊界 + AddSwitch routing）
- 對齊 Stage 53A ReviewerStage 既有 RunPetraGateAsync 同步 await 整合（已驗）
- Mock 場景 B（fix_loop_recover_round1）+ C（max_iter）覆蓋 routing

### 3. 5 fallback 點移除順序（中）

**風險**：4 子流程 framework 化沒做完先移 5 fallback 點 → fix loop / appeal 觸發時無路可走（Pipeline 卡死或異常）。

**緩解**：
- 子項 8（移除 5 fallback dispatch）排在子項 1-7（4 子流程 framework 化 + Mock）之後
- Forge 實作期紀律：先全跑通 Mock 場景 B-G（4 dynamic + 2 靜態）+ dotnet build 0 Error → 才移除 5 fallback
- legacy fallback safety net 保留（J1 既有 6 hooks），feature flag false 時退路完整

### 4. HandleQaCompletedAsync 內 fire Dev_fix 衝突（低-中，Stage 53A follow-up #1 同類）

**風險**：Stage 53A follow-up #1 修了 QaCoordinationService.HandleQaCompletedAsync passed 路徑 fire Doc 衝突，但**沒修 fire Dev_fix 路徑**（QaFixRound > 0 場景）— 53B QA fix loop framework 化時必須同步加 skip 判斷。

**緩解**：
- 子項 6 議題 F-1 修正範圍含 QaCoordinationService（不只 AppealOrchestrationService）
- Mock 場景 E（qa_no_tests_dynamic）測 passed 路徑 + 觀察 fix loop 觸發

### 5. Stage 53A 揭露的議題 G3 規劃紀律延續（低）

**Aria 53B 規劃前期已 grep AppealOrchestrationService 4 method 內部 10+ 處 call site**（議題 F-1 修正範圍依此規劃）— 對齊「對所有既有 service finalize/post-completion actions 都 grep」紀律。

**Stage 53B/55 後續預警**：規劃任何 framework Workflow 同步 await call 既有 service method 時，必須做完整 grep（含 transitive callers）。

### 6. 不踩既有 BossInteraction 邊界（A3 試點精神延續）

**Stage 53B 不動 production code**：
- ❌ 既有 BossInteraction 10+ type / InteractionService 既有 method
- ❌ HandleAgentCompletedAsync 既有 6 hooks（J1 保留作為 legacy fallback safety net）
- ❌ Stage 49/50/52/53A 既有 framework path（除既有 F-α 4 router 排除條件不動）
- ❌ sub-task（E 沿用 Stage 53A 排除）
- ❌ Stage 55 範圍：Kickoff/Design 整合到 Pipeline / sub-task 整合 / BossInteraction 切 framework HITL

**Stage 53B 動的 production code**：
- 動：`AppealOrchestrationService.cs` 4 method 內部 10+ 處加 Pipeline path skip
- 動：`QaCoordinationService.cs` 內 fire Dev_fix 處加 Pipeline path skip
- 動：`PipelineWorkflowFactory.cs` 拓撲擴展（加 DevFixStage + 6 PortId + fix loop edges）
- 動：`PipelineState.cs` 加 DevFixStageBridge + DevFixCompletionRequest/Response record + DevFixDone marker
- 動：`PipelineMessages.cs`（如有獨立檔，否則同 PipelineState.cs）
- 動：`FrameworkPipelineRouter.cs` BuildAgentCompletionResponse +1 entry + FinalizePipelineAsync 移除 5 fallback case
- 動：`ReviewerStageExecutor.cs`（fix loop routing + 移除 reviewer_critical fallback）
- 動：`DevPlanStageExecutor.cs`（appeal routing + 移除 dev_plan_failed_escalate fallback）
- 動：`DevStageExecutor.cs`（appeal + intervention routing + 移除 dev_blocker fallback）
- 動：`QaStageExecutor.cs`（QA fix loop routing + 移除 qa_fix_loop fallback）
- 動：4 stage Executor 移除 ClearMarkerAndFallbackAsync helper（Stage 53A 子項 9 helper 不再需要）
- 動：`MockScenarioService.cs` + `MockClaudeCodeService.cs`（6 個新場景）
- 動：`Directory.Build.props`（Version bump）
- 新建：`src/AiTeam.Bot/Workflows/Pipeline/Executors/DevFixStageExecutor.cs`

---

## Model 建議

依 [`workflow_aria_model_effort.md`](../../C:/Users/darkl/.claude/projects/D--Source-Code-AI-Team/memory/workflow_aria_model_effort.md) 四維度：

| 維度 | 評估 |
|---|---|
| **任務複雜度** | 高 — fix loop loop back + Petra 閘門整合 + 4 method 10+ 處 skip 修正 + 5 fallback 移除 + Mock 場景 dynamic 補驗 |
| **改動範圍** | M-L — 新建 1 Executor + 改既有 8-10 檔 + 議題 F-1 跨 Stage 修改既有 service code（首次跨多 method 多處 skip 修正）|
| **歷史包袱** | 中 — 議題 F-1 同類 G3 修法在 4 method 重演（規模放大但 pattern 已 Stage 53A 驗證）+ J1 既有 6 hooks 保留作為 legacy safety net |
| **判斷品質要求** | 中-高 — fix loop max 3 輪邊界 + appeal routing + QA fix loop 整合 dynamic 驗證；5 fallback 移除順序紀律 |

**建議**：**Opus 1M + high**

理由：
1. **混合型 Stage 第 6 個資料點**（沿用 Stage 49-53A ×0.73-1.25 區間，53B 偏 mid 中段，因規模回升 + 議題 F-1 多處 skip 增加 turn 成本）
2. **預估 context 600-1100K**（規模回升 vs Stage 53A 562K — 議題 F-1 10+ 處 skip 修正 + 6 dynamic Mock 場景 + 4 子流程 framework 化整合）
3. **可能拆 session 2 段**（Stage 53A Forge 全程一個 session 跑沒拆 — Opus 1M 56% 充裕；53B 規模回升可能拆）：
   - Session A：spike + 子項 1-7（DevFixStageExecutor + 4 stage routing + 議題 F-1 修正 + 拓撲擴展）
   - Session B：子項 8-11（FrameworkPipelineRouter 修改 + 5 fallback 移除 + Mock 6 場景 + Version bump + 結案）

### Context 預估

依 7 項公式 + 混合型 Stage 校準（×0.73-1.25 區間，53B 偏 mid 中段預估）：
- 開場 ~32K
- 工作 raw（新建 1 檔 + 改既有 8-10 檔 + 議題 F-1 多處 skip 修正）~150-220K
- Grep / Bash 輸出 ~40-60K（議題 F-1 4 method 全 grep + Stage 53A reference + dotnet build）
- 對話 turn 成本 ~70-110K（spike read + Plan Mode + 閘門一 + 結案）
- Edit 反覆對齊 ~50-100K（議題 F-1 10+ 處 skip 對齊 + fix loop loop back routing 多 stage 對齊）
- Mock 驗收（6 場景 4 dynamic + 2 靜態）~80-150K
- follow-up 修正 ~40-150K（議題 F-1 多處 skip 可能踩坑 + fix loop callback resume 整合）
- 結案文件寫作 ~10-20K
- **總計約 ~470-840K**（Opus 1M 內 47-84% 負擔，舒適區到接近邊界）

→ 拆 session 建議：若 Forge spike + 子項 1-7 結束時 context > 280K，主動跟 Christ 提拆 Session B。

---

## 與 v4 路線的關係

**Stage 53B 是 v4 漸進遷移 8 Stage 的第六步**：

```
Stage 47 ✅ ops 補丁（v3.34.0）
Stage 48 ✅ spike Phase A（v3.34.0 不變）
Stage 49 ✅ Cody-Vera-Petra Appeal loop 遷移（v3.35.0）
Stage 50 ✅ Kickoff Meeting → Group Chat orchestration（v3.36.0）
Stage 51 ✅ framework HITL pattern 試點（v3.37.0）
Stage 52 ✅ Design Meeting B3 路線（v3.38.0）
Stage 53A ✅ macro pipeline NewFeature 主路徑切 framework（v3.39.0）
   ↓
Stage 53B（本 Stage）：fix loop / appeal / QA fix loop / intervention 子流程切 framework + 5 fallback to legacy 點移除（v3.40.0）
   ↓
Stage 54：Crash Recovery 全面切 framework Checkpointing（含 4 個 CheckpointStore 抽 base class 評估 + Agent task 層 mapping helper 抽 base class 評估）
   ↓
Stage 55：收尾 + token middleware + production 切換 + 老 framework code 刪除（含 WorkflowEngine.cs / 5 fallback 點殘留 / framework Executor 從 service 切回直連）+ **Kickoff/Design 整合到 Pipeline framework**（議題 G3 真正解決，Pipeline 從 Kickoff 階段啟動）+ **sub-task 整合到 Pipeline framework**（Stage 46 機制接 Pipeline）+ 真正切既有 BossInteraction 到 framework HITL（Stage 51 試點 know-how 全面 wire）+ 移除 J1 既有 6 hooks legacy safety net
```

> 註：Stage 53B 完成後 v4 漸進遷移進度 **6/8**。NewFeature 主路徑 + 子流程**完整 Pipeline framework 化**達成。

**Stage 53B 結案後對 Stage 54 的影響**：
- 4 CheckpointStore 第 4 次出現 + Agent task 層 mapping helper 第 6 entry → Stage 54 評估抽 base class（兩議題一起評估抽象）
- Pipeline framework 完整接管 NewFeature 主路徑 + 子流程 → Stage 54 Crash Recovery 全切 framework Checkpointing 統一架構

**Stage 53B 對 Stage 55 的鋪路**：
- 5 fallback to legacy 點全移除 → Stage 55 收尾範圍純粹（守「Kickoff/Design + sub-task 整合 + BossInteraction 切 framework HITL」三戰略級工作）
- 議題 F-1 修正紀錄（AppealOrchestrationService 4 method skip + QaCoordinationService skip）給 Stage 55 移除 J1 既有 6 hooks 時，service method 已是「Pipeline path skip + legacy 路徑」乾淨架構，移除 6 hooks 時 service method 內 skip 邏輯也一併移除（因 Pipeline 已涵蓋全 NewFeature 主路徑）

---

## 實作紀錄

### 子項完成度對照（Forge 結案第一段填，2026-05-03）

| # | 子項 | 狀態 | 動的檔案 |
|---|---|---|---|
| 0 | Spike F1-F4 read 範圍 | ✅ | 無新建（純 read 對齊） |
| 1 | DevFixStageExecutor + 新 PortId + 新 record types（含 DevPlanRetryBridge / DevRetryBridge） | ✅ | 新建 [DevFixStageExecutor.cs](../../src/AiTeam.Bot/Workflows/Pipeline/Executors/DevFixStageExecutor.cs) / 改 [PipelineState.cs](../../src/AiTeam.Bot/Workflows/Pipeline/PipelineState.cs)（5 record + Loop 語義 comment 補強）/ 改 [PipelineWorkflowFactory.cs](../../src/AiTeam.Bot/Workflows/Pipeline/PipelineWorkflowFactory.cs)（PortId const）|
| 2 | ReviewerStageExecutor 加 fix loop routing + SetInterventionAndYieldAsync helper | ✅ | 改 [ReviewerStageExecutor.cs](../../src/AiTeam.Bot/Workflows/Pipeline/Executors/ReviewerStageExecutor.cs) |
| 3 | DevPlanStageExecutor 加 appeal routing + 第二 [MessageHandler] DevPlanRetryBridge | ✅ | 改 [DevPlanStageExecutor.cs](../../src/AiTeam.Bot/Workflows/Pipeline/Executors/DevPlanStageExecutor.cs) |
| 4 | DevStageExecutor 加 appeal + intervention routing + 第二 [MessageHandler] DevRetryBridge | ✅ | 改 [DevStageExecutor.cs](../../src/AiTeam.Bot/Workflows/Pipeline/Executors/DevStageExecutor.cs) |
| 5 | QaStageExecutor 加 QA fix loop routing | ✅ | 改 [QaStageExecutor.cs](../../src/AiTeam.Bot/Workflows/Pipeline/Executors/QaStageExecutor.cs) |
| 6 | 議題 F-1 多處 skip 修正（**16 處**：Appeal 11 + QaCoord 5）+ HandleDevBlocker signature `Task` → `Task<BlockerDecision>` | ✅ | 改 [AppealOrchestrationService.cs](../../src/AiTeam.Bot/Orchestration/Appeal/AppealOrchestrationService.cs)（RunPetraGate 1 + HandleDevBlocker 入口判斷 + 3 處 skip + signature 改 + HandleDevPlanCompleted 入口 + 8 處 skip）+ 改 [QaCoordinationService.cs](../../src/AiTeam.Bot/Orchestration/Qa/QaCoordinationService.cs)（QaFixRound 超限 + 4 case skip）|
| 7 | PipelineWorkflowFactory 拓撲擴展（DevFixStage edges + 2 self-loop edges） | ✅ | 改 [PipelineWorkflowFactory.cs](../../src/AiTeam.Bot/Workflows/Pipeline/PipelineWorkflowFactory.cs)（+1 RequestPort + 1 Executor + 5 新 edge + WithOutputFrom 擴 7 終結點）|
| 8 | FrameworkPipelineRouter 修改（BuildAgentCompletionResponse +1 entry / RecoverStuck mapping +1 entry / FinalizePipelineAsync 移除 5 fallback dispatch） | ✅ | 改 [FrameworkPipelineRouter.cs](../../src/AiTeam.Bot/Orchestration/Meeting/FrameworkPipelineRouter.cs) |
| 9 | 4 stage Executor ClearMarkerAndFallbackAsync helper 移除（保留 group_not_found inline SendMessage） | ✅ | 改 4 stage Executor（移除 helper + group_not_found 改 inline）|
| 10 | Mock 場景擴充 6 個 framework_pipeline_* + scenario round counter 機制 | ✅ | 改 [MockScenarioService.cs](../../src/AiTeam.Bot/Services/MockScenarioService.cs)（6 entries）/ 改 [MockClaudeCodeService.cs](../../src/AiTeam.Bot/Agents/MockClaudeCodeService.cs)（round counter dict + Reviewer/Dev/QA mock 分支）/ 改 [PmReviewService.cs](../../src/AiTeam.Bot/Agents/Pm/PmReviewService.cs)（Petra Vera revise 分支）/ 改 [PmRoutingService.cs](../../src/AiTeam.Bot/Agents/Pm/PmRoutingService.cs)（Blocker continue + NoTests approve 分支）|
| 11 | Version bump v3.40.0 + Roadmap 結案紀錄 | ✅ | [Directory.Build.props](../../src/Directory.Build.props) v3.40.0 |

### Session 結案

**Session A**（spike + 子項 1-7）：context ~500-600K，dotnet build 0 Error 87 Warning（warning 全是既有 NU1902 + MUD0002）。

**Session B**（子項 8-11，同 session 跑）：Christ 拍板 context 充裕繼續同 session，5 fallback dispatch 移除 + 4 helper 移除 + Mock 6 場景 + Version bump，dotnet build 0 Error 依舊。

### 關鍵設計決策（Session 期間 Forge 主動拍板）

1. **HandleDevBlockerAsync signature 改 `Task` → `Task<BlockerDecision>`**（子項 4 設計衝突揭露）：原設計 void return Pipeline 無法分辨 Petra continue/escalate；改 return BlockerDecision 給 Pipeline DevStage 自接管 routing，3 caller backward compat（legacy `await` 忽略 return）
2. **QaCoordinationService Pipeline path 下 env_or_test_issue 視為 passed**（子項 6-e Forge 主動拍板）：Pipeline path skip 全部 side effects（GetDecision/Mark/NotifyBoss/FireSteps），return 後 Pipeline QaStage 重讀 group 看 QaFixRound==0 + Status normal → SendMessage DocStageBridge
3. **PipelineLoopResult.Completed 語義變更**（建議補強 2）：Stage 53B 起 Completed=true 包含「intervention 完成」（不只 happy path），FinalizePipelineAsync 只 ClearMarkersAsync（不重複 call NotifyBoss）。Comment 加在 PipelineState.cs class doc
4. **DevPlanRetryBridge / DevRetryBridge type-explicit Bridge record**（建議補強 3）：避免 framework AddEdge self-loop 不支援的實作期切換，直接走 type-explicit 設計 + 第二 [MessageHandler]
5. **scenario round counter 機制**（子項 10）：MockClaudeCodeService 加 static `Dictionary<string, int> _scenarioRoundCounters` + `ResetScenarioRoundCounters()` / `GetAndIncrementRound(agentKey)` helper，per-scenario per-agent round 計數動態切換 Mock 行為（fix loop Round 1 Critical / Round 2+ pass）

### 踩坑紀錄彙整

> 53B 屬「同 pattern 不同子流程」擴展，Stage 53A know-how 全複用 — 無新 framework 機制踩坑。實作期主要的設計衝突（HandleDevBlockerAsync signature）由議題 F-1 修正範圍涵蓋。簡潔紀律：跨 Stage 預警價值高的紀錄保留如下。

1. **既有 service method signature 升級給 Pipeline path 用**（跨 Stage 預警）：Pipeline 內 call legacy service void method 時，可能無法分辨內部 routing 結果 → 議題 F-1 修正期主動評估 method signature 升級（`Task` → `Task<TDecision>`）給 Pipeline 用，比加 transient marker DB 欄位乾淨。Stage 55 收尾移除 5 fallback / J1 6 hooks 時可考慮回退 signature（若 legacy caller 全消除）

### Aria 校準錨候選

待 Aria 結案第二段填（context 預估 470-840K，實際 ~500-600K Session A+B + 驗收期 ~250K = 總 ~750-850K，規模對齊「混合型第 6 資料點」中-上半範圍）

---

## 驗收期紀錄（2026-05-03，Forge 自驗 6 場景全綠 + 2 follow-up + 1 既有議題揭露）

### 驗收能力突破：/internal/mock/scenario HTTP API + BossInteraction auto-approver

Forge 自驗期發現：Stage 32 既有 `POST /internal/mock/scenario` HTTP API 可從 host curl 直接觸發 Mock 場景（不需 Discord/Dashboard），加上 docker exec psql 直接 update boss_interactions auto-approve kickoff/design pending interactions，Forge 可**完整自驗 6 場景**（不需 Christ 動手）。

→ Christ 拍板「Forge 權限完整可控 docker」配合 API 探索能力 → Stage 53B 起 Forge 自驗範圍可涵蓋全 6 場景（含 Crash Recovery docker compose restart）。

### Follow-up #1（[7fbac77](https://github.com/darkleong/AiTeam/commit/7fbac77)）：Mock 53B 場景搬到 3 agent service MockMode early return

**根因**：3 agent service（ReviewerAgentService / DevAgentService / QaAgentService）在 MockMode 下 early return 直接 hard-code 回傳，**完全 bypass** MockClaudeCodeService 的 RunReviewAsync/RunAsync/RunQaAsync。Stage 53B 主 commit 把 53B Mock branches 寫在 MockClaudeCodeService 內 — 設計失誤，agent 根本沒 call 它。

**修法**：53B Mock branches 搬到 3 agent service 內 + MockClaudeCodeService.GetAndIncrementRound 改 public + MockClaudeCodeService 內 53B branches 移除（避免冗餘 + 加 Mock arch 註解）。

**跨 Stage 預警**：MockMode early return pattern 必須在 Agent service 內處理 Mock 場景，不能假設 MockClaudeCodeService 是 Mock 唯一入口。Stage 54+ 加新 Mock 場景時必先 grep 對應 agent service 的 MockMode early return path。

### Follow-up #0（[49f4d5a](https://github.com/darkleong/AiTeam/commit/49f4d5a)）：Dashboard MockScenarioCard 補 53B 6 場景

Dashboard MockScenarioCard 漏 Stage 49-53B 全部 framework_* 場景（53A 也漏，是長期 follow-up gap）。53B 範圍補 6 場景進 Dashboard 讓 Christ 從 Dashboard 觸發更方便（Discord `/mock framework_pipeline_*` 仍可用）。Stage 49-53A 缺的 framework 場景**沒補**（守 53B 簡潔紀律）— 留 follow-up FF 紀錄。

### 6 場景驗收結果

| 場景 | TaskGroup | Status | FixIteration | 驗證 |
|---|---|---|---|---|
| **B** fix_loop_recover_round1 | v3 | ✅ done | 1 | log 證據鏈完整：Vera Round 1 Critical → Petra revise → ReviewerStage SendMessage(DevFixStageBridge) → DevFixStage enqueue Dev_fix (IsFixLoop:true) → DevFixStage passed → ReviewerStageBridge loop back → Vera Round 2 passed → FinalizePipelineAsync Completed=true |
| **C** fix_loop_max_iter | v4 | ✅ needs_intervention | 3 | InterventionReason="Vera fix loop 超 3 次仍有問題"（53B SetInterventionAndYieldAsync 設） |
| **D** dev_blocker_appeal | v4 | ⚠️ needs_intervention | 0 | 53B framework path 完整跑通（log 證實 Dev Round 1 [BLOCKED] → Petra continue → Dev Round 2 passed），但 NotifyMergeStage MarkGroupDoneOrInterventionAsync 看到 Round 1 殘留 failed task → 標 needs_intervention（**production 既有議題不是 53B 引入**，legacy 路徑同樣會這樣，立 follow-up FF）|
| **E** qa_no_tests_dynamic | v4 | ✅ done | 0 | qa_no_tests routing → Petra approve → Pipeline 自接管推進 Doc → NotifyMerge |
| **F** reviewer_fallback_dynamic | v4 | ✅ done | 1 | 同 B 機制（Vera Round 1 Critical + Petra revise → fix loop） |
| **G** fix_loop_crash_recovery | v7 | ✅ done | 1 | Forge 代勞 docker compose restart 在 DevFix yield 期間 + Recovery 完整證據鏈：`stuck framework pipeline rehydrate（議題 12 ResumeStreamingAsync）` + `pending PortId=Pipeline-DevFixCompletion`（**53B K1 mapping helper +1 entry 真實生效**）+ `requeue failed Agent task（agent=Dev_fix）`（**53A follow-up #4 + 53B Dev_fix 整合驗證**）|

### Follow-up FF 候選（未在本 Stage 修，留紀錄）

1. **Pipeline DevStage [BLOCKED] retry 後 Round 1 failed task 殘留 → MarkGroupDoneOrInterventionAsync 誤判 needs_intervention**：production 既有議題（legacy 也會這樣）。建議方向：① DevStage retry 時把舊 failed task 標 superseded / ② MarkGroupDone 忽略 IsFixLoop=true 後續有 newer success task 的舊 failed task。Stage 54+ 評估
2. **Mock 場景跨 Stage（Kickoff/Design）需要手動 DB approve BossInteraction 才能推進**：Stage 49-53B 既有議題（不是 53B 引入），Forge 自驗期靠 BossInteraction auto-approver Monitor 解。建議方向：MockMode 模式下 BossInteraction 自動 approve（避免 Christ/Forge 每次手動 DB approve）
3. **Dashboard MockScenarioCard 補 Stage 49-53A framework_* 場景**：53B 只補 53B 6 場景，Stage 49-53A 共 22+ 個 framework_* 場景仍缺
4. **MockClaudeCodeService 內 RunReviewAsync/RunAsync/RunQaAsync 在 Mock arch 下 dead code**（被 3 agent service early return bypass）：3 個 method 內留註解說明，但實際邏輯永不執行 — Stage 54+ 評估清理

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-03 | 初版規劃書建立（Aria）—— v4 漸進遷移第六步 Stage 53B：fix loop / appeal / QA fix loop / intervention 4 子流程切 framework + 5 fallback to legacy 點移除 + Stage 53A follow-up #3 場景 E/F 補 dynamic（A1 一個 Stage 全切 + B1 fix loop loop back + C1 現有 Pipeline 拓撲擴張 + D1 一次完成 + E sub-task 沿用排除 + F 兩個必修都修 + G1 spike 範圍極窄 + H1 沿用單一 flag + I-mid 6 場景 + J1 既有 6 hooks 保留 + K1 mapping helper 保留現狀）。**規劃前期已 grep AppealOrchestrationService 4 method 內部 10+ 處 call site**（議題 F-1 規劃紀律升級對齊 Stage 53A 議題 G3 在 QA 重演揭露的教訓）|
