# Stage 81 — B 動態 re-planning + HITL gate 配套 + Trial_v24 3 議題收口

> 對應系統版本：v3.72.0 → v3.73.0
> 規模：L
> 狀態：規劃中
> 文件版本：v1.1
> 性質：新業務功能（動態 re-planning core algorithm — Petra 看 worker output 邊判斷邊決定下一步 + HITL gate 重用 Stage 80 plan_confirm infra + max iterations + cost cap 雙重保險）+ Trial_v24 3 議題收口（🟡 #1 Quinn outputLen=0 baseline 漂移修根因 + 🟡 #2 Petra NeedsImageContext 純文字誤判 + 🟡 #3 DispatchedWorkerCount 命名語意）
> Model + Effort 建議：**Opus 1M + Extra high**（規模 L / 大規模架構級 state machine + spike 多輪可能 + 業務級風險高 / 對齊自省點 #39 反向校準紀律 — L+ baseline 升 Extra high 而非「不慣性推 Extra high」邊界）
> Stage 期間餘額影響：**0 燒 AiTeam 餘額**（Aria + Forge session 走 Claude Code subscription）/ Trial_v25 才燒（預估 $2-4）

---

## 戰略脈絡

**Christ 親口要的業務功能** — Petra 看 subtask result 再決下一步（dynamic re-planning）/ 對齊業界 LangGraph cycles + max iterations + replan threshold 業界紀律（Stage 77 既有 WebSearch 結論拍板過）。

**Trial_v24 業務級實證**（場景 F respond「mobile responsive」→ Petra subtasks=2 → 4 加響應式設計）— HITL gate 真實影響 Petra decision 業務級驗證 ⭐⭐⭐ / Stage 81 動態 replan 沿用 Stage 80 既有 plan_confirm infra 重用 4 decision pattern UI + InteractionProcessor routing。

**真實工程性質**：純內部 business logic 設計 — 把既有業界 pattern（Stage 77 既有 WebSearch 結論「LangGraph cycles + max iterations + replan threshold」拍板過）內化到 AiTeam v5.5 orchestrator + HITL gate 配套（重用 Stage 80 plan_confirm 既有 infra）+ Trial_v24 議題收口。0 third-party framework 真實使用 = **不觸發 WebSearch**（對齊 workflow_aria.md 第三節 A 第 9 條紀律 — 純 v5.5 + Stage 80 既有 pattern 對齊）。

**動態 re-planning 真實設計澄清**（v1.1 Aria 自審 + Christ 拍板）：本 Stage 對齊 LangGraph cycles 業界紀律真實 — replan_approve = **同個 subtask 重 dispatch with retry instruction**（不重 decide 新 plan / 不從頭跑 / 不跳過已完成）。Petra LLM 不回「建議新 plan 結構」/ 而是回「retry instruction」給 currentSubtask 該怎麼重做的指示。對齊「修根因 > 補丁」+ 業界 LangGraph cycles 設計乾淨。「動態 re-planning」wording 對齊 Stage 77 既有 reference 維持 / 但實際語意 = **HITL retry gate（subtask cycles with retry instruction）**。

**Aria 自審 v1.1 修法摘要**（aria-review-plan 自審揭 2 🔴 + 6 🟡 全收口）：
- 🔴 議題 1 ✅ — Christ 拍板 retry 同 subtask（業界 LangGraph cycles）
- 🔴 議題 2 ✅ — Christ 拍板 unit test 驗 method 邏輯（production 留 Trial_v25 真實業務驗）
- 🟡 議題 3-8 全 v1.1 自行收口（reject button label / race partial unique index / SessionCostUsd update 時機 / Critical Files / Vera-Quinn schema verify / dispatched log 對齊）

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
   - 新加 `EvaluateReplanTriggerAsync(subtaskResult, currentSubtaskId, plan, sessionId, ct)` method — Petra LLM 判斷三大觸發條件：
     - Vera review critical（grep subtaskResult 含 `"critical":[{...}]` 非空）
     - Quinn QA failed（grep subtaskResult 含 `"status":"failed"`）
     - chain dispatch exception / timeout（catch 既有 exception path）
   - 若觸發 replan → Petra LLM call 給「**retry instruction**（給 currentSubtask 該怎麼重做的指示）+ replan 觸發原因」JSON → 開 `replan_confirm` BossInteraction 卡 + return Replanning
   - **🟡 議題 #7 verify 紀律**：Forge 規劃時必先 grep Vera output 真實 JSON schema（`"critical":[{file,line,message}]` + summary + impact 三層）+ Quinn output 真實 schema（`status` / `passed_tests` / `failed_tests` field 真實存在性）對齊 trigger 判斷邏輯

**2. `EvaluateReplanTriggerAsync` Petra LLM call 設計（對齊 LangGraph cycles 業界紀律）**
   - Petra prompt 教學 — few-shot 三大觸發場景 + **「retry instruction」結構**（不是「建議新 plan」結構）
   - retry instruction 結構：`{ "shouldReplan": true/false, "reason": "...", "retryInstruction": "Cody 在 PipelineView 5 handler 加 catch Exception 防 Circuit 斷線", "targetSubtaskId": 1 }`
   - 對齊 LangGraph cycles 真實語意 — 同 subtask 重 dispatch 給新 instruction / 不重 decide 整個 plan 結構（議題 1 修法 v1.1）
   - **🟡 議題 #7 verify 紀律延伸**：Vera output schema 真實 fields 對齊 Petra prompt few-shot 教學例

**3. `PetraOrchestratorResult` 加 `Replanning` + `Cancelled` 工廠**
   - `Replanning(sessionId, currentSubtaskId, retryInstruction, replanReason)` 工廠 — 動態 retry 觸發狀態（chain dispatch pause / 等 Christ HITL gate 拍板）
   - `Cancelled(sessionId, caps, summary)` 工廠 — 修 🟡 #3 議題收口（reject path 真實 0 chain dispatch / DispatchedWorkerCount=0 / vs 既有用 `Done` / `Failure` 雜用語意）
   - Stage 80 既有 `ResumeRejectAsync` 改用 `Cancelled` 工廠（語意對齊 / 0 業務行為改變）

**4. `replan_confirm` BossInteraction InteractionType + Resume routing（議題 1 修法 v1.1）**
   - 對齊既有 `plan_confirm` pattern / 0 schema 改動 / InteractionType free-form string 加 `replan_confirm`
   - `PetraOrchestratorService.ResumeFromReplanConfirmationAsync(sessionId, decision, contextOverride, ct)` 新 method 4 decision routing：
     - `replan_approve` → **走 Petra 建議 retry instruction / 重 dispatch currentSubtask 同個 worker**（不重 decide plan 結構 / 不從頭跑 / 不跳過已完成）/ 對齊 LangGraph cycles 業界紀律
     - `replan_edit` / `replan_respond` → Petra 重 `EvaluateReplanTriggerAsync` 含 override context → 新 retry instruction → 開新 replan_confirm 卡（loop until approve / reject）
     - `replan_reject` → **不採納 Petra 的 retry 建議 / 接受原 worker output / 繼續 chain dispatch 下個 subtask**（不 cancel session / 對 Christ 信任原 worker output 選項）/ 對應 button label 修法見「設計決策 9」+ Aria 預警 W6

**5. `PetraSession.ReplanIteration` + `SessionCostUsd` column + max iterations + cost cap**
   - `PetraSession.ReplanIteration` int default 0 — 追蹤 replan 輪數（每次 `EvaluateReplanTriggerAsync` 觸發 replan + Christ 拍 approve / edit / respond 後 +1 / reject 不算）
   - `PetraSession.SessionCostUsd` numeric(18,6) default 0 — 累積 cost
   - **🟡 議題 #5 SessionCostUsd update 時機 + Repository method 簽名**（v1.1 補明）：
     - 時機：`DispatchTalentsAsync` 內每個 worker dispatch 完成後（包括原始 chain + retry dispatch）
     - method：`PetraSessionRepository.UpdateSessionCostUsdAsync(Guid sessionId, decimal deltaUsd, CancellationToken ct)` 從 `token_logs WHERE PetraSessionId=... AND CreatedAt > lastChecked` 累計差異 update
     - 注意：既有 `token_logs` 0 `PetraSessionId` column（grep verify Stage 78c 砍範圍）→ Forge 規劃時必補加 `token_logs.PetraSessionId nullable` column 或改用 `TaskId` 推得 sessionId（Forge spike 揭真實 schema 後拍板）
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
   - 子項 3 已加 `Cancelled` 工廠 / 本子項範圍：
     - Stage 80 `ResumeRejectAsync` 改用 `Cancelled` 工廠（vs 既有 `Done` / `Failure` 雜用 / 0 業務行為改變）
     - **🟡 議題 #8 `PlanConfirmationProcessor` log 訊息對齊**（v1.1 補明）：[PlanConfirmationProcessor.cs](../../src/AiTeam.Bot/Orchestration/Petra/PlanConfirmationProcessor.cs) `logger.LogInformation("PlanConfirmationProcessor 完成 ... dispatched={Count}", ..., result.DispatchedWorkerCount)` 對 cancelled path 顯示 **0**（不雜用 plan.Subtasks.Count / 真實對齊 reject path 0 chain dispatch）
     - **xUnit 覆蓋 reject path 測試 assertion 修正**：[tests/AiTeam.Bot.Tests/Orchestration/PetraOrchestratorServiceTests.cs](../../tests/AiTeam.Bot.Tests/Orchestration/PetraOrchestratorServiceTests.cs) Stage 80 既有 ResumeRejectAsync test fixture 改用 Cancelled 工廠 assertion / DispatchedWorkerCount=0

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

9. **🥇 retry 同 subtask 業界 LangGraph cycles 設計**（議題 1 Christ 拍板 v1.1）— replan_approve = 同個 subtask 重 dispatch with retry instruction（不重 decide 新 plan / 不從頭跑 / 不跳過已完成）/ 0 「已完成 subtask 怎麼辦」邏輯 / cost 最低 / 對齊業界紀律真實
   - replan_reject button label + modal 對齊新意義（議題 #3 修法 v1.1）：button label 改「**不採納（保留原結果）↩**」/ 二次確認 modal 文字「確定不採納 Petra 的 retry 建議 / 接受原結果繼續往下跑嗎？」/ 區分 Stage 80 plan_reject「拒絕 ❌」整個任務取消的語意
   - 對齊 Stage 80 plan_confirm 4 button 設計 pattern / button 顏色：核准 ✅（success）/ 修改 ✏️（info）/ 不採納 ↩（warning vs Stage 80 plan_reject 用 error 區分）/ 補充 💬（info）

10. **🥇 unit test 驗 method 邏輯**（議題 2 Christ 拍板 v1.1）— Stage 81 場景 B-K 全 unit test 為主 / production 真實業務驗留 Trial_v25
    - 對齊 Stage 78c 砍 MockScenarioService 後新 Mock 紀律
    - Forge self-verify Phase 2 跑 xUnit cover method 邏輯 / 0 production API 真實 call / 0 cost
    - Trial_v25 真實業務驗 chain wire 接通 + UX 流暢度（對齊 aria-trial-run skill 第 8 次實踐 + Chrome MCP 自跑紀律延伸）

---

## Critical Files（v1.1 議題 #6 補明）

**新增**：
- `tests/AiTeam.Bot.Tests/Orchestration/PetraOrchestratorServiceTests.cs` — 新加 `EvaluateReplanTriggerAsync` 邏輯 test + `ResumeFromReplanConfirmationAsync` 4 decision test fixtures + 修 Stage 80 既有 `ResumeRejectAsync` 對 Cancelled 工廠 assertion
- `src/AiTeam.Data/Migrations/<timestamp>_Stage81PetraSessionReplanFields.cs` + `.Designer.cs` — AddColumn ReplanIteration + SessionCostUsd default 0 + InsertData seed 3 AppSetting

**修改**：
- `src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs` — chain dispatch loop 加 EvaluateReplanTriggerAsync trigger + ResumeFromReplanConfirmationAsync 4 decision routing + ResumeRejectAsync 改 Cancelled 工廠 + BuildPetraSystemPrompt few-shot 加 NeedsImageContext negative example
- `src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorResult.cs` — 加 Replanning + Cancelled 工廠
- `src/AiTeam.Bot/Orchestration/Petra/PlanConfirmationProcessor.cs` — 擴 routing replan_confirm + MapActionToDecision 加 4 action + log 訊息 dispatched=Count 對齊 cancelled path
- `src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs` — 🟡 #1 Quinn outputLen=0 修根因（Forge spike 揭真實 root cause + 修法）
- `src/AiTeam.Bot/Services/InteractionService.cs` — 加 ReplanConfirmActionsJson 4 button 常數
- `src/AiTeam.Dashboard/Components/Pages/Interactions/InteractionCard.razor` + `.razor.cs` — 加 replan_confirm UI render 段
- `src/AiTeam.Data/Repositories/PetraSessionRepository.cs` — 加 UpdateReplanIterationAsync + UpdateSessionCostUsdAsync method
- `src/AiTeam.Data/AppDbContext.cs` — PetraSession entity 加 ReplanIteration + SessionCostUsd column 對齊
- `src/AiTeam.Bot/Configuration/WorkflowSettings.cs` + `WorkflowSettingsResolver.cs` — 加 3 method（UseDynamicReplanning / MaxReplanIterations / ReplanCostCapUsd）
- `src/Directory.Build.props` — version bump v3.72.0 → v3.73.0

---

## 驗收情境

### 場景 A — flag=false v5.5 + Stage 80 baseline 0 regression

1. SQL `UPDATE app_settings SET "Value"='false' WHERE "Key"='Workflow:UseDynamicReplanning'`
2. SQL `UPDATE app_settings SET "Value"='false' WHERE "Key"='Workflow:UseHITLPlanConfirmation'`（守 Stage 80 baseline）
3. curl `/internal/reload-cache?scope=all`
4. Dashboard 派純文字 prompt（reuse `.tmp/trial_v15_body.json`）
5. **期望**：Petra chain dispatch 跑完不開 replan_confirm 卡 / Bot log 0 `EvaluateReplanTriggerAsync` fire / 對齊 Trial_v22+v23+v24 場景 A baseline

### 場景 B — flag=true 觸發 replan condition + 開 replan_confirm 卡（**🟡 議題 #2 修法 v1.1 — unit test 為主**）

**v1.1 修法**：場景 B 改 unit test 驗 method 邏輯（對齊議題 #2 Christ 拍板 🥇 unit test 路線）。Stage 78c 已砍 `/internal/mock/scenario` endpoint + MockScenarioService — Forge 規劃時不再嘗試走 Mock Scenario endpoint 觸發。

**Layer 1 — unit test 驗 method 邏輯**（Forge self-verify 範圍 / Stage 81 場景 B 主驗）：
1. xUnit test `EvaluateReplanTriggerAsync` method 餵 hardcoded Vera output JSON 含 `"critical":[{...}]` → 驗 method 回 `shouldReplan=true` + retryInstruction 非空
2. xUnit test `EvaluateReplanTriggerAsync` 餵 Quinn output JSON 含 `"status":"failed"` → 驗 method 回 `shouldReplan=true`
3. xUnit test `EvaluateReplanTriggerAsync` 餵 normal Vera output（0 critical）→ 驗 method 回 `shouldReplan=false`
4. xUnit test 走 `PlanConfirmationProcessor` pickup `replan_confirm` action → `ResumeFromReplanConfirmationAsync` 4 decision routing 各自 assertion（mock orchestrator + verify call 簽名 + result）

**Layer 2 — production 真實業務驗**（留 Trial_v25 / 不在 Stage 81 場景 B 驗收範圍）：派真實業務 task 看 chain dispatch 跑得順 + replan_confirm 卡真實開（Christ 派容易引發 critical 的 prompt）。

### 場景 C — Christ 點「replan_approve」（同意 retry instruction / 議題 1 修法 v1.1）

**v1.1 修法**：對齊議題 1 Christ 拍板 🥇 retry 同 subtask 業界 LangGraph cycles 設計。

**Layer 1 — unit test 驗 ResumeReplanApproveAsync routing**：
1. xUnit test `ResumeFromReplanConfirmationAsync(sessionId, "approve", null, ct)` → 走 `ResumeReplanApproveAsync` → 從 ContextJson 取 retryInstruction + targetSubtaskId → 重 dispatch currentSubtask（同個 worker / 對齊 LangGraph cycles）
2. xUnit assert：**chain dispatch 0 從頭跑** / 0 重 call 已完成 worker / 只重 dispatch currentSubtask with new instruction
3. xUnit assert：PetraSession.ReplanIteration +1（追蹤 replan 輪數）

**Layer 2 — production 真實業務驗**（留 Trial_v25）：派真實 task / 觸發 replan / Christ 點「核准」/ 觀察 Bot log 0 從頭跑 + currentSubtask 重 dispatch + 後續 subtask 繼續

### 場景 D — Christ 點「replan_edit」（修改 retry instruction）

xUnit test `ResumeFromReplanConfirmationAsync(sessionId, "edit", "改用其他方式 review", ct)` → 走 `ResumeReplanEditOrRespondAsync` → Petra 重 `EvaluateReplanTriggerAsync` 含 override context → 新 retry instruction → 開新 `replan_confirm` 卡（loop until approve / reject）+ assert PetraSession.ReplanIteration +1

### 場景 E — Christ 點「replan_reject」（不採納 retry / 接受原 output 繼續 / 議題 1+#3 修法 v1.1）

**v1.1 修法**：對齊議題 1 設計 — replan_reject = **不採納 retry 建議 / 接受原 worker output / 繼續 chain dispatch 下個 subtask**（任務不取消 / 跟 Stage 80 plan_reject 整個任務取消行為不同 / 議題 #3 UI 區分修法見「設計決策 9」）。

xUnit test `ResumeFromReplanConfirmationAsync(sessionId, "reject", null, ct)` → 走 `ResumeReplanRejectAsync` → 接受原 worker output / 繼續 chain dispatch 下個 subtask（**不 cancel session** / 對齊 Stage 80 plan_reject 整個 cancel 行為差異）+ assert PetraSession.ReplanIteration 不變（reject 不算 replan 輪數）

### 場景 F — Christ 點「replan_respond」（補充指示）

xUnit test `ResumeFromReplanConfirmationAsync(sessionId, "respond", "Cody 加 mobile responsive 考量", ct)` → 走 `ResumeReplanEditOrRespondAsync`（同 edit path）→ Petra 重 `EvaluateReplanTriggerAsync` 含 respond context → 新 retry instruction + 開新 replan_confirm 卡

### 場景 G — max iterations N=3 reach → abort + intervention 卡（unit test）

xUnit test 模擬 `PetraSession.ReplanIteration=3` 達上限 → `EvaluateReplanTriggerAsync` 不再 fire / 升 BossInteraction `intervention` 卡（既有 pattern）+ 寫 task_memory `decision/replan-cap-reached` content="max iterations N=3 reached" + session=cancelled / chain dispatch 0 啟動

### 場景 H — cost cap > $5 → abort + intervention 卡（unit test）

xUnit test 模擬 `PetraSession.SessionCostUsd > Workflow:ReplanCostCapUsd` 達上限 → abort + 升 intervention 卡 + 寫 task_memory `decision/replan-cap-reached` content="cost cap $X reached" + session=cancelled

### 場景 I — 🟡 #1 Quinn outputLen=0 修根因 verify

**Layer 1 — unit test**：xUnit test `ClaudeCodeChatClientAdapter.RunQaAsync` 對 mock stdout return 真實 12553 tokens output → 驗 ChatClientAdapter return outputLen > 0（修根因 verify）
**Layer 2 — Trial_v25 production**：派真實 task 觸發 Quinn dispatch → PR body 含「[Quinn|qa_testing|outputLen=N]」N > 0 對齊 Trial_v22+v23 baseline 範圍

### 場景 J — 🟡 #2 Petra NeedsImageContext 純文字 false verify

**Layer 1 — unit test**：xUnit test Petra prompt few-shot 含 negative example 後 → mock LLM call 餵純文字 prompt → 驗 SubtaskPlan 所有 subtask.NeedsImageContext=false
**Layer 2 — Trial_v25 production**：flag=true Stage 80 HITL plan_confirm 開卡 + 派純文字 prompt → Dashboard plan_confirm 卡 SubtaskPlan render **無「附圖」chip 顯示** / SQL boss_interactions.ContextJson 所有 subtask.NeedsImageContext 全 false

### 場景 K — 🟡 #3 PetraOrchestratorResult.Cancelled 工廠 + reject path verify（v1.1 #8 補明）

**Layer 1 — unit test**：xUnit test Stage 80 `ResumeRejectAsync` → return `PetraOrchestratorResult.Cancelled` 工廠（vs 既有 Failure 工廠）/ DispatchedWorkerCount=0 / Success=true / Summary 對齊 reject 語意
**Layer 2 — Trial_v25 production**：Stage 80 flag=true 開 plan_confirm 卡 → Dashboard 點「拒絕 ❌」+ 二次確認 → Bot log `PlanConfirmationProcessor 完成 ... decision=reject success=True **dispatched=0**`（不再雜用 Subtasks.Count=4）

### 場景 L — Stage 80 既有 xUnit fixture 對 Cancelled 工廠 assertion 對齊（v1.1 議題 #8 補明）

xUnit test Stage 80 既有 `ResumeRejectAsync` test fixture 改用 Cancelled 工廠 assertion / `result.DispatchedWorkerCount.Should().Be(0)` / `result.Success.Should().BeTrue()` / fixture 修正子項 10 範圍 / 0 業務行為改變（純語意 cleanup）

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

### W7 — replan_confirm 跟 plan_confirm 同 session race 紀律（v1.1 議題 #4 補明）

理論上同 PetraSession 同時有 1 plan_confirm pending（Stage 80）+ 1 replan_confirm pending（Stage 81）race 可能性：
- 真實場景：Petra plan_confirm 卡 pending 時 session=paused / chain dispatch 0 啟動 → 不可能同時觸發 replan_confirm（replan trigger 是 chain dispatch 內 subtask 完成後 / session=paused 時 0 chain dispatch fire）
- 但 Bot 重啟期 race / 或 Stage 80 場景 D edit redecide 期間 → 必驗 race 邏輯

**Forge 規劃時必 grep verify**：
1. Stage 57 既有 `IX_boss_interactions_status_pending` partial unique index 真實 cover 範圍（grep Migrations / `\d boss_interactions` 看 unique index 條件）
2. 若 index 對齊 sessionId（既有 Stage 57 設計）→ 0 race / 0 加新 index
3. 若 index 不對齊（如 cover task_group_id）→ 加新 partial unique index 守「同 PetraSessionId 最多 1 pending interaction」or escalate Christ 拍板

### W8 — LangGraph cycles 業界紀律對齊紀律（v1.1 議題 1 修法後新加）

對齊 LangGraph cycles 真實語意 — replan 是「同 subtask 重 dispatch with retry instruction」/ 不是「動態改 plan 結構」。Forge 實作時必避免：
- ❌ Petra LLM 回「建議新 SubtaskPlan 結構（subtasks + dependencies + picks）」— 是 議題 1 修法前的錯設計 / 不對齊業界紀律
- ✅ Petra LLM 回「retry instruction（如『Cody 在 PipelineView 5 handler 加 catch Exception 防 Circuit 斷線』）+ shouldReplan + reason + targetSubtaskId」— v1.1 修法後對齊業界

`replan_confirm` UI render 也對齊 — 顯示「retry instruction」+「replan 觸發原因」+「當前 currentSubtask 進度」+ 4 button / **不**顯示「Petra 建議新 plan 結構」（會誤導 Christ）。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.1 | 2026-05-20 | **Aria 自審 + Christ 拍板 2 🔴 + 6 🟡 全收口 v1.1 升級**。**🔴 議題 1**（Christ 拍板 🥇 retry 同 subtask 業界 LangGraph cycles）：子項 1/2/3/4 整套重寫 — `EvaluateReplanTriggerAsync` 不再回「建議新 plan 結構」/ 改回「retry instruction（給 currentSubtask 該怎麼重做的指示）」+ `PetraOrchestratorResult.Replanning` 工廠 field 改 `retryInstruction string` + `ResumeFromReplanConfirmationAsync` 4 decision routing 對齊 retry 語意（approve = 同 subtask 重 dispatch / edit/respond = 新 retry instruction / reject = 接受原 output 繼續下個 subtask）+ 戰略脈絡段加澄清「動態 re-planning = HITL retry gate（subtask cycles with retry instruction）」+ 加設計決策 9 LangGraph cycles 路線 + 加 W8 業界紀律對齊預警。**🔴 議題 2**（Christ 拍板 🥇 unit test 驗 method 邏輯）：場景 B-K 全 unit test 為主（Forge self-verify Phase 2 跑 xUnit / 0 cost）+ production 真實業務驗留 Trial_v25 / 加設計決策 10 unit test 紀律（對齊 Stage 78c 砍 MockScenarioService 後新 Mock 紀律）。**🟡 議題 #3 收口**：replan_reject button label 改「不採納（保留原結果）↩」warning 色（vs Stage 80 plan_reject「拒絕 ❌」error 色）/ 二次確認 modal 文字對齊新語意「接受原結果繼續往下跑」/ 設計決策 9 補明。**🟡 議題 #4 收口**：W7 加 race condition 預警 — Forge 規劃時必 grep verify Stage 57 既有 `IX_boss_interactions_status_pending` partial unique index cover 範圍對齊 sessionId / 必要時加新 index 守。**🟡 議題 #5 收口**：子項 5 補明 `PetraSessionRepository.UpdateSessionCostUsdAsync(sessionId, deltaUsd, ct)` method 簽名 + update 時機在 `DispatchTalentsAsync` 內每個 worker dispatch 完成後 + token_logs 0 PetraSessionId column 議題揭。**🟡 議題 #6 收口**：加 Critical Files 段（PetraOrchestratorServiceTests.cs + Migration + 既有 7 修改檔案完整列出 + Directory.Build.props version bump）。**🟡 議題 #7 收口**：子項 1/2 補明 Forge 規劃時必 grep Vera output 真實 JSON schema（`"critical":[{file,line,message}]` 三層）+ Quinn output schema 對齊 trigger 判斷邏輯。**🟡 議題 #8 收口**：子項 10 補明 PlanConfirmationProcessor `dispatched={Count}` log 訊息對 cancelled path 顯示 0（不雜用 plan.Subtasks.Count）+ 場景 K 雙層驗（unit test + Trial_v25）+ 加場景 L Stage 80 既有 xUnit fixture 修正驗。**新增 1 場景 L** + 修場景 B-K 11 場景 → 12 場景 unit test + production 雙層驗收。**v1.1 整體影響**：規劃前置 Aria 自審揭 8 議題（vs 過去 Stage 79/80 自審 0-1 議題）— 自省點候選「Aria 寫完 Roadmap 後系統性 6 維度 ultrathink 自審紀律」（留 /aria-end 統一升級）/ aria-review-plan skill 對齊自我自審紀律生效驗證。 |
| v1.0 | 2026-05-20 | Stage 81 規劃書建立（Aria 撰寫 / Trial_v24 結案後即進）。**核心結構**：動態 replan core（5 子項 — `EvaluateReplanTriggerAsync` Petra LLM evaluator + `PetraOrchestratorResult.Replanning` + `Cancelled` 工廠 + `replan_confirm` InteractionType + Resume routing + `PetraSession.ReplanIteration` + `SessionCostUsd` column + max iterations + cost cap）+ HITL gate 配套（2 子項 — `PlanConfirmationProcessor` 擴 routing `replan_confirm` + InteractionCard.razor `replan_confirm` UI render — 純複用 Stage 80 既有 plan_confirm infra）+ Trial_v24 議題收口（3 子項 — 🟡 #1 Quinn outputLen=0 修根因 + 🟡 #2 Petra NeedsImageContext 純文字誤判 + 🟡 #3 DispatchedWorkerCount 命名語意）+ 8 設計決策拍板 + 11 驗收情境 + 6 Aria 預警。**Effort baseline Opus 1M + Extra high**（規模 L / 大規模架構級 state machine + spike 多輪可能 + 業務級風險高 / 對齊自省點 #39 反向校準紀律 — L+ baseline 升 Extra high 而非「不慣性推 Extra high」邊界）。**0 WebSearch 觸發**（Stage 77 既有結論 reference + 0 third-party framework 真實使用 / 純內部 business logic 設計 + 重用 Stage 80 既有 plan_confirm infra）。**規劃前 grep verify 完整**（PetraOrchestratorResult 4 工廠 / ClaudeCodeChatClientAdapter RunQaAsync line 276 / Petra few-shot prompt builder line 513 / PetraSessionRecoveryService「重啟重跑」紀律 真實狀態 verify）。**Migration**：Stage81PetraSessionReplanFields（AddColumn ReplanIteration + SessionCostUsd default 0 / 2 AppSetting seed Workflow:MaxReplanIterations=3 + Workflow:ReplanCostCapUsd=5）。**AppSetting**：Workflow:UseDynamicReplanning default false。 |
