# Stage 81 — B 動態 re-planning + HITL gate 配套 + Trial_v24 3 議題收口

> 對應系統版本：v3.72.0 → v3.73.0
> 規模：L
> 狀態：規劃中
> 文件版本：v1.0
> 性質：新業務功能（動態 re-planning core algorithm — Petra 看 worker output 邊判斷邊決定下一步 + HITL gate 重用 Stage 80 plan_confirm infra + max iterations + cost cap 雙重保險）+ Trial_v24 3 議題收口（🟡 #1 Quinn outputLen=0 baseline 漂移修根因 + 🟡 #2 Petra NeedsImageContext 純文字誤判 + 🟡 #3 DispatchedWorkerCount 命名語意）
> Model + Effort 建議：**Opus 1M + Extra high**（規模 L / 大規模架構級 state machine + spike 多輪可能 + 業務級風險高 / 對齊自省點 #39 反向校準紀律 — L+ baseline 升 Extra high 而非「不慣性推 Extra high」邊界）
> Stage 期間餘額影響：**0 燒 AiTeam 餘額**（Aria + Forge session 走 Claude Code subscription）/ Trial_v25 才燒（預估 $2-4）

---

## 戰略脈絡

**Christ 親口要的業務功能** — Petra 看 subtask result 再決下一步（dynamic re-planning）/ 對齊業界 LangGraph cycles + max iterations + replan threshold 業界紀律（Stage 77 既有 WebSearch 結論拍板過）。

**Trial_v24 業務級實證**（場景 F respond「mobile responsive」→ Petra subtasks=2 → 4 加響應式設計）— HITL gate 真實影響 Petra decision 業務級驗證 ⭐⭐⭐ / Stage 81 動態 replan 沿用 Stage 80 既有 plan_confirm infra 重用 4 decision pattern UI + InteractionProcessor routing。

**真實工程性質**：純內部 business logic 設計 — 把既有業界 pattern（Stage 77 既有 WebSearch 結論「LangGraph cycles + max iterations + replan threshold」拍板過）內化到 AiTeam v5.5 orchestrator + HITL gate 配套（重用 Stage 80 plan_confirm 既有 infra）+ Trial_v24 議題收口。0 third-party framework 真實使用 = **不觸發 WebSearch**（對齊 workflow_aria.md 第三節 A 第 9 條紀律 — 純 v5.5 + Stage 80 既有 pattern 對齊）。

### Phase 4 路徑（Trial_v24 後修正）

```
Stage 78a ✅ → 78b ✅ → 78c ✅ → 79 ✅ → Trial_v23 ✅
            → 80 ✅ → Trial_v24 ✅
            → 81（B 動態 re-planning + HITL gate 配套 + Trial_v24 議題收口 / L）
            → Trial_v25（驗動態 replan 業務體驗 + 3 議題收口）
            → WebUI Stage（v4 entity drop + Dashboard 重設計）
            → v5.5 完整收口
```

---

## 計劃前 WebSearch 結論（業界 reference — Forge 起草時 reference 用 / 不重複觸發）

**Stage 77 既有 WebSearch 結論 reference**（[Stage_77_Roadmap.md:40](Stage_77_Roadmap.md#L40)）：「**動態 re-planning** — 業界紀律成熟但規模 L / **必配 max iterations + replan threshold + cost cap + checkpoint replay** / Phase 4+ 候選」— Stage 81 內化此既有結論 / 0 新 WebSearch 觸發。

**Stage 80 既有 HITL plan_confirm infra 內化結論**：4 decision pattern（approve / edit / reject / respond）對齊 InteractionType + PlanConfirmActionsJson + PlanConfirmationProcessor BackgroundService + ResumeFromPlanConfirmationAsync 4 decision routing — Stage 81 replan_confirm 純複用此 infra（0 新 UI 框架 / 0 新 routing 框架）。

**對齊 AiTeam 既有 InteractionType pattern**（[BossInteraction.cs:13-15](../../src/AiTeam.Data/BossInteraction.cs#L13)）：既有 8 pattern 加 `replan_confirm` 對齊既有架構。

---

## 子項清單（10 子項 / 動態 replan core 5 + HITL gate 配套 2 + Trial_v24 議題收口 3）

### 動態 replan core（子項 1-5）

**1. `PetraOrchestratorService` chain dispatch 加 replan trigger evaluator**
   - 切位置：[PetraOrchestratorService.cs:610-661](../../src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L610) 自管 chain dispatch loop 內 / 每個 subtask 完成後 + 下個 subtask dispatch 前
   - 新加 `EvaluateReplanTriggerAsync(subtaskResult, currentLevel, plan, sessionId, ct)` method — Petra LLM 判斷三大觸發條件：
     - Vera review critical（grep subtaskResult 含 `"critical":[{...}]` 非空）
     - Quinn QA failed（grep subtaskResult 含 `"status":"failed"`）
     - chain dispatch exception / timeout（catch 既有 exception path）
   - 若觸發 replan → Petra LLM call 給「replan 觸發原因 + 建議新 plan」JSON → 開 `replan_confirm` BossInteraction 卡 + return Paused

**2. `EvaluateReplanTriggerAsync` Petra LLM call + JSON SubtaskPlan 重 decide**
   - Petra prompt 教學 — few-shot 三大觸發場景 + 建議新 plan 結構（subtasks + dependencies + picks）
   - 對齊既有 `DecideTalentsWithPlanAsync` JSON SubtaskPlan parser 紀律（0 新 parser / 既有 SubtaskPlanParser 重用）

**3. `PetraOrchestratorResult` 加 `Replanning` + `Cancelled` 工廠**
   - `Replanning(sessionId, currentSubtaskId, replanReason, suggestedPlan)` 工廠 — 動態 replan 觸發狀態（chain dispatch pause / 等 Christ HITL gate）
   - `Cancelled(sessionId, caps, summary)` 工廠 — 修 🟡 #3 議題收口（reject path 真實 0 chain dispatch / DispatchedWorkerCount=0 / vs 既有用 `Done` / `Failure` 雜用語意）
   - Stage 80 `ResumeRejectAsync` 改用 `Cancelled` 工廠

**4. `replan_confirm` BossInteraction InteractionType + Resume routing**
   - 對齊既有 `plan_confirm` pattern / 0 schema 改動
   - `PetraOrchestratorService.ResumeFromReplanConfirmationAsync(sessionId, decision, contextOverride, ct)` 新 method 4 decision routing：
     - `replan_approve` → 走 Petra 建議新 plan / 從 currentSubtaskId 繼續 chain dispatch（不從頭跑）
     - `replan_edit` / `replan_respond` → Petra 重 `EvaluateReplanTriggerAsync` 含 override context → 開新 replan_confirm 卡（loop until approve / reject）
     - `replan_reject` → fallback 原 plan 繼續 chain dispatch（不 cancel session / 對 Christ 信任原 plan 選項）

**5. `PetraSession.ReplanIteration` + `SessionCostUsd` column + max iterations + cost cap**
   - `PetraSession.ReplanIteration` int default 0 — 追蹤 replan 輪數
   - `PetraSession.SessionCostUsd` numeric(18,6) default 0 — 累積 cost（每次 worker dispatch 後從 token_logs 累計）
   - AppSetting `Workflow:MaxReplanIterations` default 3 + `Workflow:ReplanCostCapUsd` default 5
   - 超過 max iterations OR cost cap → abort + 升 BossInteraction `intervention` 卡（既有 InteractionType / 0 新 UI）+ 寫 task_memory `decision/replan-cap-reached` + session=cancelled

### HITL gate 配套（子項 6-7 / 重用 Stage 80 既有 infra）

**6. `PlanConfirmationProcessor` 擴 routing `replan_confirm`**
   - 既有 `MapActionToDecision(action)` 擴 4 action：`replan_approve` / `replan_edit` / `replan_reject` / `replan_respond` → decision string
   - 既有 polling loop 加 `InteractionType` filter `OR ("InteractionType", "replan_confirm")`
   - dispatch 入口分支 — interactionType=plan_confirm 走 ResumeFromPlanConfirmationAsync / interactionType=replan_confirm 走 ResumeFromReplanConfirmationAsync

**7. `InteractionCard.razor` `replan_confirm` UI render**
   - 對齊既有 plan_confirm SubtaskPlan render pattern / 新加「replan 觸發原因」+「Petra 建議新 plan」+「當前進度（subtaskId / completed count）」段
   - 重用既有 4 button helper（label 改 `replan_approve` / `replan_edit` / `replan_reject` / `replan_respond` action ID）
   - `InteractionService.ReplanConfirmActionsJson` 新常數對齊 PlanConfirmActionsJson 結構

### Trial_v24 議題收口（子項 8-10）

**8. 🟡 #1 Quinn outputLen=0 baseline 漂移修根因**
   - 修法調查：grep [ClaudeCodeChatClientAdapter.cs:276](../../src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs#L276) `RunQaAsync` 對 Quinn stdout parsing 邏輯 / 比對 RunDevAsync / RunReviewAsync 為何 Cody/Vera output 正常但 Quinn outputLen=0
   - 可能 root cause：Quinn LLM 真實 12553 tokens 輸出但 Claude Code subprocess stdout parsing edge case（如 JSON 結構 / status field / log marker）/ 或 ChatClientAdapter return path 對 Quinn capability 特殊處理錯
   - Forge spike 揭真實 root cause + 修根因（vs 補丁）

**9. 🟡 #2 Petra `NeedsImageContext` 純文字誤判 true 修法**
   - Petra prompt few-shot 補強 — [PetraOrchestratorService.cs:513](../../src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L513) `BuildPetraSystemPrompt` few-shot 教學加 **negative example**：
     - 「純文字 prompt（無 attachment）→ 所有 subtask NeedsImageContext=false」
     - 「prompt 含 image attachment 但 UI 改動不大（如純 docs / backend logic）→ NeedsImageContext=false」
     - 「prompt 含 image attachment 且 UI 修改（如 razor / razor.cs / CSS）→ 對應 subtask NeedsImageContext=true」
   - 對齊既有 few-shot 3 範例（UI bug case true / 後端 false / docs false）擴 negative example

**10. 🟡 #3 `PetraOrchestratorResult.DispatchedWorkerCount` 命名語意收口**
   - 子項 3 已加 `Cancelled` 工廠 / 本子項：
     - Stage 80 `ResumeRejectAsync` 改用 `Cancelled` 工廠（vs 既有 `Done` / `Failure` 雜用）
     - `PlanConfirmationProcessor` log 訊息對齊 — `dispatched={Count}` 對 cancelled path 顯示 0（不雜用 Subtasks.Count）
     - xUnit 覆蓋 reject path 測試 assertion 修正

---

## 設計決策（8 議題拍板）

1. **動態 replan 三大觸發路徑** — Vera review critical + Quinn QA failed + chain dispatch exception / timeout — 對齊既有 Stage 53B/77 fix-loop 紀律延伸 + Trial_v24 揭真實業務場景需求

2. **HITL gate 路線 🥇** — replan 觸發後開 `replan_confirm` 卡（重用 Stage 80 既有 plan_confirm infra / 0 新 UI 框架 / 一致 / Christ 控制度紀律）— 對齊「Christ 自己用爽」精神 + 對自動化結果信任度尚未到 silent auto 階段

3. **max iterations N=3 + cost cap $5 雙重保險** — 業界 best practice 既有 Stage 77 WebSearch 結論 cover / 對齊「業界紀律完整 cover」精神

4. **checkpoint replay 沿用既有 infra** — `PetraSessionRecoveryService` 「重啟重跑不從 checkpoint resume」紀律對齊（[PetraSessionRecoveryService.cs:11](../../src/AiTeam.Bot/Orchestration/Petra/PetraSessionRecoveryService.cs#L11)）/ 0 新 schema / 0 新 method / replan state（ReplanIteration + SessionCostUsd column）由 PetraSession 既有 column 加擴

5. **replan_confirm InteractionType 對齊 plan_confirm pattern** — 不重寫 InteractionProcessor / InteractionCard 框架 / 對齊「修根因 > 補丁」+「自己用爽不重寫」精神 / 重用 PlanConfirmationProcessor BackgroundService（既有 polling 對齊紀律）

6. **AppSetting flag default false 守 Stage 80 baseline** — `Workflow:UseDynamicReplanning` 預設 false / Trial_v25 開時切 true → 結案切回 false（對齊 aria-trial-summary skill flag 切回紀律）

7. **`PetraOrchestratorResult.Cancelled` 工廠加 + reject path 對齊** — 修 🟡 #3 命名語意 + Stage 80 ResumeRejectAsync 改用 Cancelled / 0 業務行為改變（純語意 cleanup）

8. **Stage 81 整套做（vs 拆 81a/b）** — 動態 replan core + HITL gate 配套 + 3 議題收口同 Stage 修上下文集中 / 對齊「修根因 > 補丁」+ Stage 81 規模 L 範圍明確 / Aria gate1 Tier 0+1+2+Tier 3 #11 升級補規模 L+ 風險

---

## 驗收情境

### 場景 A — flag=false v5.5 + Stage 80 baseline 0 regression

1. SQL `UPDATE app_settings SET "Value"='false' WHERE "Key"='Workflow:UseDynamicReplanning'`
2. SQL `UPDATE app_settings SET "Value"='false' WHERE "Key"='Workflow:UseHITLPlanConfirmation'`（守 Stage 80 baseline）
3. curl `/internal/reload-cache?scope=all`
4. Dashboard 派純文字 prompt（reuse `.tmp/trial_v15_body.json`）
5. **期望**：Petra chain dispatch 跑完不開 replan_confirm 卡 / Bot log 0 `EvaluateReplanTriggerAsync` fire / 對齊 Trial_v22+v23+v24 場景 A baseline

### 場景 B — flag=true 觸發 replan condition + 開 replan_confirm 卡

1. SQL flag UseDynamicReplanning=true + UseHITLPlanConfirmation=true + reload-cache
2. Mock Vera review 回 critical（curl `/internal/mock/scenario` 觸發 mock `vera_review_critical`）
3. Dashboard 派 prompt → Petra DecideTalentsWithPlanAsync → chain dispatch Cody 完成 → Vera dispatch
4. **期望**：Vera 完成回 critical → `EvaluateReplanTriggerAsync` fire → Petra LLM 判斷 replan 觸發 → 開 `replan_confirm` BossInteraction 卡 + ContextJson 含「原 plan + replan 觸發原因（Vera critical 詳情）+ Petra 建議新 plan」+ session=paused / chain dispatch 0 啟動

### 場景 C — Christ 點「replan_approve」（同意新 plan）

1. 場景 B 後 / Dashboard 點 replan_confirm 卡「核准 ✅」
2. **期望**：`PlanConfirmationProcessor` pickup `interactionId=... action=replan_approve` → `ResumeFromReplanConfirmationAsync(sessionId, "approve", null, ct)` → 走 Petra 建議新 plan / 從 currentSubtaskId 繼續 chain dispatch（不從頭跑 / 不重 call Cody）→ session paused → running → 跑完 chain → PR

### 場景 D — Christ 點「replan_edit」（修改 Petra 建議）

1. 場景 B 後 / Dashboard 點「修改 ✏️」+ 輸入「不要新增 Quinn subtask」
2. **期望**：`ResumeFromReplanConfirmationAsync(sessionId, "edit", "不要新增 Quinn subtask", ct)` → Petra `EvaluateReplanTriggerAsync` 含 override context → 開新 replan_confirm 卡（loop until approve / reject）

### 場景 E — Christ 點「replan_reject」（拒絕新 plan / fallback 原 plan）

1. 場景 B 後 / Dashboard 點「拒絕 ❌」+ 二次確認「確定」
2. **期望**：`ResumeFromReplanConfirmationAsync(sessionId, "reject", null, ct)` → fallback 原 plan 繼續 chain dispatch（不 cancel session / 對 Christ 信任原 plan 選項）→ Vera critical 接受 / Quinn QA 接著跑 → PR

### 場景 F — Christ 點「replan_respond」（補充指示）

1. 場景 B 後 / Dashboard 點「補充 💬」+ 輸入「換 Cody 跑兩輪試試」
2. **期望**：`ResumeFromReplanConfirmationAsync(sessionId, "respond", "換 Cody 跑兩輪試試", ct)` → Petra 重 `EvaluateReplanTriggerAsync` 含 respond context → 開新 replan_confirm 卡（loop until approve / reject）

### 場景 G — max iterations N=3 reach → abort + intervention 卡

1. flag=true / 連續 3 輪 replan loop（mock Vera critical 連續 3 次）
2. **期望**：`PetraSession.ReplanIteration=3` 達上限 → `EvaluateReplanTriggerAsync` 不再 fire / 升 BossInteraction `intervention` 卡（既有 pattern）+ 寫 task_memory `decision/replan-cap-reached` content="max iterations N=3 reached" + session=cancelled / chain dispatch 0 啟動

### 場景 H — cost cap > $5 → abort + intervention 卡

1. flag=true + AppSetting Workflow:ReplanCostCapUsd=0.5（測試用低 cap）+ reload-cache
2. 派 prompt → chain dispatch 累積 SessionCostUsd > 0.5
3. **期望**：`PetraSession.SessionCostUsd > 0.5` 達上限 → abort + 升 intervention 卡 + 寫 task_memory `decision/replan-cap-reached` content="cost cap $0.5 reached" + session=cancelled

### 場景 I — 🟡 #1 Quinn outputLen=0 修根因 verify

1. flag=false v5.5 baseline + 派 prompt（同 Trial_v24 場景 A 觸發條件）
2. **期望**：Quinn dispatch 完成後 PR body 含「[Quinn|qa_testing|outputLen=N]」N > 0（真實 QA 內容）/ 對齊 Trial_v22+v23 baseline Quinn outputLen 749 / 1300 範圍
3. SQL `token_logs` Quinn cost > $0 + chain dispatch 完成日誌 outputLen > 0 雙驗

### 場景 J — 🟡 #2 Petra NeedsImageContext 純文字 false verify

1. flag=true Stage 80 HITL plan_confirm 開卡 + 派純文字 prompt（無 image attachment）
2. **期望**：Dashboard plan_confirm 卡 SubtaskPlan render — **無「附圖」chip 顯示**（所有 subtask NeedsImageContext=false）/ SQL boss_interactions.ContextJson 解析所有 subtask.NeedsImageContext 全 false

### 場景 K — 🟡 #3 PetraOrchestratorResult.Cancelled 工廠 + reject path verify

1. Stage 80 flag=true 開 plan_confirm 卡 → Dashboard 點「拒絕」+ 二次確認
2. **期望**：Bot log `PlanConfirmationProcessor 完成 ... decision=reject success=True **dispatched=0**`（不再雜用 Subtasks.Count=4）/ `PetraOrchestratorResult.Cancelled` 工廠 fire / DispatchedWorkerCount=0 對齊真實 reject path 0 chain dispatch

---

## Aria 預警

### W1 — 動態 replan 觸發條件設計紀律

`EvaluateReplanTriggerAsync` 三大觸發路徑（Vera critical / Quinn failed / chain exception）對齊既有 Stage 53B/77 fix-loop 紀律 — 不擴增新觸發路徑（avoid scope creep）/ 業務級 replan 必要時可 Stage 82+ 加更多觸發條件。

### W2 — HITL gate 重用 Stage 80 既有 infra 紀律

`replan_confirm` 純複用 plan_confirm pattern — 0 新 UI 框架 / 0 新 routing 框架 / 0 新 BackgroundService（PlanConfirmationProcessor 擴 routing 即可）。Forge 實作時必對齊 Stage 80 既有 pattern / 不重寫框架。

### W3 — max iterations + cost cap 雙保險 / cost 計算紀律

SessionCostUsd 累積每次 worker dispatch 後從 token_logs 累計（既有 token_logs schema）— 不新加 cost tracking infra / 對齊「修根因 > 補丁」+「重用既有 infra」精神。max iterations 是 hard cap / cost cap 是 soft cap（達上限升 intervention 卡讓 Christ 決定）。

### W4 — Trial_v24 議題收口工程量 vs 動態 replan core 平衡

3 議題收口工程量都不大（🟡 #2 + 🟡 #3 純語意 / 🟡 #1 中等工程 — 調查 + 修根因）/ 不擴 Stage 81 規模 / 對齊「同 Stage 修上下文集中」精神 / Aria gate1 Tier 0+1+2+Tier 3 #11 升級補規模 L+ 風險。

### W5 — Migration `Stage81PetraSessionReplanFields` nullable 紀律

新加 `ReplanIteration int` + `SessionCostUsd numeric(18,6)` 兩 column / **必 default 0**（既有 row 不擾 / backwards-compatible）/ Migration AddColumn 兩個 default 0 對齊既有 Stage 76/79 pattern。

### W6 — `PetraOrchestratorResult.Cancelled` 工廠 reject path 對齊紀律

Stage 80 既有 `ResumeRejectAsync` 用 `Failure` 工廠（status="failed" 雜用語意 / DispatchedWorkerCount 雜用 Subtasks.Count）→ Stage 81 改用 `Cancelled` 工廠 / **必驗 Stage 80 既有 xUnit 測試對 reject path assertion 對齊**（測試 fixture 修正子項 10 範圍）。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-20 | Stage 81 規劃書建立（Aria 撰寫 / Trial_v24 結案後即進）。**核心結構**：動態 replan core（5 子項 — `EvaluateReplanTriggerAsync` Petra LLM evaluator + `PetraOrchestratorResult.Replanning` + `Cancelled` 工廠 + `replan_confirm` InteractionType + Resume routing + `PetraSession.ReplanIteration` + `SessionCostUsd` column + max iterations + cost cap）+ HITL gate 配套（2 子項 — `PlanConfirmationProcessor` 擴 routing `replan_confirm` + InteractionCard.razor `replan_confirm` UI render — 純複用 Stage 80 既有 plan_confirm infra）+ Trial_v24 議題收口（3 子項 — 🟡 #1 Quinn outputLen=0 修根因 + 🟡 #2 Petra NeedsImageContext 純文字誤判 + 🟡 #3 DispatchedWorkerCount 命名語意）+ 8 設計決策拍板 + 11 驗收情境 + 6 Aria 預警。**Effort baseline Opus 1M + Extra high**（規模 L / 大規模架構級 state machine + spike 多輪可能 + 業務級風險高 / 對齊自省點 #39 反向校準紀律 — L+ baseline 升 Extra high 而非「不慣性推 Extra high」邊界）。**0 WebSearch 觸發**（Stage 77 既有結論 reference + 0 third-party framework 真實使用 / 純內部 business logic 設計 + 重用 Stage 80 既有 plan_confirm infra）。**規劃前 grep verify 完整**（PetraOrchestratorResult 4 工廠 / ClaudeCodeChatClientAdapter RunQaAsync line 276 / Petra few-shot prompt builder line 513 / PetraSessionRecoveryService「重啟重跑」紀律 真實狀態 verify）。**Migration**：Stage81PetraSessionReplanFields（AddColumn ReplanIteration + SessionCostUsd default 0 / 2 AppSetting seed Workflow:MaxReplanIterations=3 + Workflow:ReplanCostCapUsd=5）。**AppSetting**：Workflow:UseDynamicReplanning default false。 |
