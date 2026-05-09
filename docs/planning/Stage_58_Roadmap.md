# Stage 58：v4 framework production-ready 補強第二波 — API 餘額容錯性（FF 五十三）

> 對應 Future Feature：FF 五十三（API 餘額用盡時容錯性缺口）— Trial_v6 揭露 3 🔴 戰略級議題最後一個（前兩個 Stage 57 v3.46.0 + v3.46.1 已修）
> 對應版本：**v3.47.0**（Stage 57 v3.46.1 + minor bump）
> 建立日期：2026-05-09
> 狀態：📋 規劃中
> 文件版本：v1.0

---

## 概述

**戰略背景**：Trial_v6 v2.0 揭露 v4 framework 9/9 達成的 production-ready 邊界 3 個 🔴 缺口，Stage 57 已收口 race condition + Vera fix loop HITL routing 兩個（v3.46.0 + v3.46.1）。Stage 58 = 最後一個 🔴 = API 餘額用盡時的容錯性 — 修完進入 Trial_v7+ 重跑 Trial_v6 量化 v4 framework hierarchical static 真實 ROI（去掉 race / 卡死 / API 容錯三 noise 後）。

### 範圍邊界

- ✅ **API 失敗偵測**：CLI path（ClaudeCodeService subprocess stdout 偵測 API 401 / insufficient_balance signal）+ API path（Anthropic SDK exception catch）兩條路統一拋業務 exception `LlmApiFailureException`
- ✅ **4 Agent 統一 fail-fast 行為**（Dev / Reviewer / QA / Doc — 全走 ClaudeCodeService CLI path）：catch 業務 exception → task failed + fire 新 routing interaction（取代 Trial_v6 揭露的 silent skip 行為）
- ✅ **新 BossInteraction routing**：1 統一 type `agent_api_failure_intervention`（context.agent 區分）+ Christ 拍板真三選 button（continue / retry / abort）+ 對齊 Stage 55B Session B 5 routing yield-resume pattern + Stage 57 第 6 routing 對齊 — 第 7 routing
- ✅ **4 Pipeline Stage Executor handler**：DevStage / ReviewerStage / QaStage / DocStage 各自 catch + fire `agent_api_failure_intervention` 並 yield 等 Christ — 對齊 Stage 57 ReviewerStageExecutor.HandleReviewerFixLoopLimitResponseAsync 設計
- ✅ **Mock 場景補強**：1 個（`framework_pipeline_agent_api_failure`）+ 對應 Dashboard MockScenarioCard

- ❌ **不動**：FF 三十六 Phase B 動態流程架構（等 Stage 58 完成後啟動）
- ❌ **不動**：Quinn `qa_failed_intervention` 既有 fix loop routing（不同性質 — fix loop ≠ API 爆，獨立 routing）
- ❌ **不動**：USD billing 守門模式（Christ 拍板 = B 容錯模式，不做 USD billing 預先攔截 — 戰略價值優先 vs 預算敏感度）

### v4 framework production-ready 達成判定

Stage 58 完成後 = **Trial_v6 揭露 3 🔴 全收口**：
- race condition（Stage 57 v3.46.0 + v3.46.1）✅
- Vera fix loop HITL routing（Stage 57 v3.46.0）✅
- API 餘額容錯性（Stage 58 v3.47.0）= 本 Stage

→ 可進入 **Trial_v7+ 重跑 Trial_v6** 對照新 baseline（同 Trial_v5 → v6 對照模式）量化 v4 framework hierarchical static 真實 ROI（去掉三 noise 後）。

### Trial_v6 真實傷害數據（Stage 58 修法後消除）

對齊 Trial_v6 v2.0 Checkpoint 13 + 議題 #15：
- TokenTrackingProvider 守門用 token count（10M/month）不用 USD billing — 沒擋住 API 401 / insufficient_balance
- Vera 最危險：cost 0 + task done + 流程繼續（silent skip，沒 fail-fast）
- Quinn 半明確：cost 0 + task failed + qa_failed_intervention（但是因為 git commit 失敗副作用，不是 catch API 401）
- Sage：cost 0 + task done + 「無輸出略過提交」silent skip + epic_partial_paused
- 表面看「No changes; nothing to commit」誤導真實根因（API billing fail）

### Stage 57 教訓套入（避免重蹈 Aria 設計疏忽）

Stage 57 揭露 Aria 計劃書兩個設計層盲點，Stage 58 主動規避：

| Stage 57 教訓 | Stage 58 預防措施 |
|---|---|
| TryCreateUniqueInteractionAsync TOCTOU race window（helper 內 read-write 假設 atomic）| Stage 58 不引入新 idempotent helper（容錯路徑都是 fire-and-forget BossInteraction，不需 unique check）— 若 Forge spike 揭露需 idempotent，**先 grep codebase 既有 atomic primitive 用法**（partial unique index / pg advisory lock / EF Core ExecutionStrategy）|
| HandleEpicPartialPaused user transaction 沒考慮 NpgsqlRetryingExecutionStrategy 衝突 | Stage 58 不引入新 user transaction（fire-and-forget interaction + Pipeline yield-resume 不需 transaction wrap）— 若 Forge spike 揭露需要 transaction，**包 `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` 內**對齊 Stage 57 ffe2027 self-diag fix |

→ 計劃書硬規則升級：**Aria 設計新 helper / transaction 時，先 grep codebase 既有 atomic primitive 用法**（不憑印象寫，避免重蹈 Stage 57 兩處設計疏忽）— 自省點 #26 候選。

---

## 設計決策（Christ 已拍板 + Aria 拿捏）

### 主路線（Christ 拍板）

| 議題 | 拍板 | 理由 |
|---|---|---|
| **議題 1：模式選擇** | **B 容錯模式** — catch API 401 / insufficient_balance error → 4 Agent 統一 fail-fast 行為 | 對齊 Christ「碰到爆再儲值就好」態度（戰略價值優先 vs 預算敏感度，user_christ.md），守門模式 over-engineering |
| **議題 2：routing button** | **真三選** `continue` / `retry` / `abort` | 對齊 Quinn `qa_failed_intervention` 既有三選 pattern 但語意不同（API 爆 retry 是「儲值後重試該 agent」非「再跑一輪」）|
| **議題 3：拆 Session 戰術** | **一個 session 跑** | 兩件事高度耦合（catch API 錯誤 + 統一 fail-fast 是同一條 code path），拆 Session 反增複雜度 |

### Aria 拿捏（已決，純內部實作不對外議題）

| # | 議題 | 決定 |
|---|---|---|
| 1 | BossInteraction type 統一 vs 分 4 type | **統一 1 type `agent_api_failure_intervention`** — 4 Agent fail behavior 一致（cost 0 + API 401 同根因），UI button 一致，分 type 沒額外資訊；context.agent 區分（Cody/Vera/Quinn/Sage）|
| 2 | RequestPort 統一 vs per-stage | **per-stage 4 port**（DevStage / ReviewerStage / QaStage / DocStage）— 對齊 Stage 55B Session B 5 routing per-agent pattern + framework 1.3.0 RequestPort type-bound 限制 |
| 3 | NotifyBoss helper 統一 vs 分 4 method | **統一 1 method `NotifyBossAgentApiFailureAsync(group, agentName, ...)`** — fire-and-forget pattern 共用，agentName 參數區分 context |
| 4 | API 失敗 catch 在哪層 | **CLI path：ClaudeCodeService 偵測 stdout signal 拋 `LlmApiFailureException`**（Forge spike 第一步驗證 stdout pattern — `insufficient_balance` / `401` / specific Anthropic CLI error format）+ **API path：TokenTrackingProvider 或 AnthropicProvider 內 catch SDK exception 拋同 exception**（Forge spike 確認 Anthropic SDK exception type）+ **4 Pipeline Stage Executor catch 統一處理** |
| 5 | retry button 行為 | **直接 re-invoke 同一個 Agent task**（state 不動 — Anthropic key 不換只是 balance 變）；對應 ContinuationAction = SendMessage(同 Bridge) 重跑該 stage |
| 6 | continue button 行為 | **跳過該 Agent 進下階段**（state.AgentDone=true + SendMessage 下游 Bridge）— 對齊 Stage 57 ReviewerStageExecutor skip_qa case pattern |
| 7 | abort button 行為 | **SetInterventionAndYieldAsync end Pipeline** — 對齊既有 abort 行為 |
| 8 | Token 計費紀錄行為 | API 401 場景**不寫 token_logs**（real cost = 0，沒實際 LLM 呼叫）— 對齊 LlmApiFailureException 拋出時略過 LogCliUsageAsync / TokenTrackingProvider 寫入路徑 |
| 9 | Mock 場景命名 | `framework_pipeline_agent_api_failure` — 對齊 Stage 49-57 既有 framework_pipeline_* 命名慣例 |
| 10 | Mock 觸發機制 | MockClaudeCodeService 加 FailScenario `agent_api_failure` → 偵測到時拋 `LlmApiFailureException`（模擬 API 401）— 各 Pipeline Stage Executor catch 走 fail-fast path |
| 11 | Migration / schema | **不動**（純 routing 邏輯 + exception handling 邏輯，無 DB schema 改動）|
| 12 | CLAUDE_*.md prompt | 不動（不引入新 LLM call / Agent prompt 不變）|

### Stage 58 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **0** | **Spike 第一步：read 對齊範圍**（Forge Plan Mode 第一步）— ① ClaudeCodeService CLI subprocess 對 API 401 的 stdout pattern grep（Trial_v6 觀察 + 既有 catch 邏輯）② Anthropic SDK exception type spike（`Anthropic.SDK.AnthropicAPIException` 或 `HttpRequestException` 子類型）③ Stage 55B Session B 5 routing + Stage 57 第 6 routing dispatch 鏈路對照表（既有 6 routing pattern）④ 4 Pipeline Stage Executor 既有 catch 點 + AgentExecutionResult.Success=false 處理路徑 ⑤ TokenTrackingProvider + LogCliUsageAsync 在 cost 0 時的既有寫入行為 | XS |
| **1** | 業務 exception 定義 + 雙 path 偵測：① 新建 `LlmApiFailureException`（含 ProviderType / RawError 欄位）② ClaudeCodeService CLI subprocess stdout 偵測 API 失敗 signal 拋 exception ③ AnthropicProvider catch Anthropic SDK exception 轉拋同 exception ④ TokenTrackingProvider 確認 cost 0 + 不寫 token_logs（API 失敗時略過寫入路徑）| M |
| **2** | 4 Pipeline Stage Executor 加 LlmApiFailureException catch + fire `agent_api_failure_intervention` + yield 等 Christ：DevStageExecutor / ReviewerStageExecutor / QaStageExecutor / DocStageExecutor — 對齊 Stage 57 ReviewerStageExecutor FixIteration≥3 case 的 fire+yield pattern | M |
| **3** | 第 7 routing 完整 wiring：① InteractionService AgentApiFailureActionsJson const + auto-approve switch case ② AgentApiFailureRequest / Response records（PipelineState.cs，含 agentName context）③ 4 PortId const + 4 RequestPort + 4 AddEdge wiring（含 per-stage continue 跳下游 Bridge edge — Dev→Reviewer / Reviewer→Qa / Qa→Doc / Doc→終）④ HandleAgentApiFailureResponseAsync 真三選獨立 path（continue→下游 Bridge / retry→同 stage Bridge re-invoke / abort→SetIntervention end）| M |
| **4** | TaskGroupService 加 `NotifyBossAgentApiFailureAsync(group, agentName, ...)` 統一 helper + `TryRoutePipelineAgentApiFailureAsync` dispatch + `case "agent_api_failure_intervention"` ProcessBossResponseAsync dispatch + FrameworkPipelineRouter 4 ResumeAfterAgentApiFailure thin wrapper（per-stage） + InteractionProcessor 3 label mapping | M |
| **5** | Mock 場景補強：① MockClaudeCodeService 加 FailScenario `agent_api_failure` 觸發拋 `LlmApiFailureException` ② MockScenarioService 加 alias case + scenario switch + emoji + frameworkHint ③ Dashboard MockScenarioCard 加 1 MudSelectItem | S |
| **6** | Forge 自驗：① 跑 agent_api_failure Mock 場景 → 4 Agent 任一爆觸發 `agent_api_failure_intervention` interaction（auto-approve 預設 retry 或 abort 待議題 13 拍）② SQL 查 BossInteraction Type=`agent_api_failure_intervention` + context.agent ✅ ③ 手動 SQL 驗 continue / retry / abort 三 path ④ regression：Stage 55B Session B 5 routing + Stage 57 第 6 routing 既有 6 routing 仍綠 + Stage 56 token_logs 寫入率不被新「不寫」邏輯誤擋（只 API 失敗時不寫，正常呼叫仍寫）| S |
| **7** | Version bump v3.47.0 + 結案文件（Roadmap 實作紀錄章節 + CHANGELOG / Future_Feature 同步交給 Aria 結案第二段）| XS |

> **不寫工時估算**（workflow_aria.md 第三節 A 第 4 條）— 各子項規模見上表 XS/S/M 範圍描述。

### Aria 拍板待 Forge spike 後 finalize（純內部）

| # | 議題 | 待 spike 後拍 |
|---|---|---|
| 13 | MockMode auto-approve `agent_api_failure_intervention` 預設 action | `retry` 預設（對齊「儲值後重試」直覺）vs `continue`（直接跳該 agent 進下階段） — Forge spike 後依 Mock 場景驗收順暢度拍 |
| 14 | LlmApiFailureException 的 RawError 欄位內容 | Anthropic SDK exception message vs CLI stdout 摘要 vs 統一 enum — Forge spike 後對齊既有 exception 結構拍 |

---

## 子項 0：Spike 第一步 — read 對齊範圍

### read 對齊範本

| # | read 對象 | 目的 |
|---|---|---|
| F1 | `src/AiTeam.Bot/Agents/ClaudeCodeService.cs` 既有 catch 區塊（line 556 / 597）+ subprocess stdout 解析邏輯 | 子項 1 CLI path 偵測點 — 確認 API 401 stdout signal pattern + 既有 catch 邏輯不重複 |
| F2 | `src/AiTeam.Bot/Agents/AnthropicProvider.cs`（無 catch — 透過 Anthropic SDK）+ Anthropic.SDK package exception types grep | 子項 1 API path 偵測點 — 確認 SDK 拋什麼 exception type（`AnthropicAPIException` / `HttpRequestException` / etc）|
| F3 | `src/AiTeam.Bot/Agents/TokenTrackingProvider.cs` line 113-138 inner provider call + token_logs 寫入路徑 | 子項 1 確認 cost 0 不寫 token_logs 邏輯插入點 — API 失敗時 throw 直接逃出，不執行 line 121-136 寫入 |
| F4 | Stage 55B Session B 5 routing + Stage 57 第 6 routing dispatch 對照（QaStageExecutor.cs / DevPlanStageExecutor.cs / ReviewerStageExecutor.cs / DesignStageExecutor.cs / DevStageExecutor.cs / FrameworkPipelineRouter.cs ResumeAfter*）| 子項 2/3/4 對齊既有 6 routing pattern — fire + SendsMessage + InteractionProcessor dispatch + ContinuationAction → SendMessage 下游 Bridge / SetIntervention end |
| F5 | `src/AiTeam.Bot/Workflows/Pipeline/Executors/` 4 Stage Executor（DevStageExecutor / ReviewerStageExecutor / QaStageExecutor / DocStageExecutor）既有 result.Success=false 處理路徑 | 子項 2 catch 點插入位置確認（不破壞既有 fix loop / qa_failed_intervention 等流程）|
| F6 | `src/AiTeam.Bot/Services/InteractionService.cs` line 64 EpicPartialPausedActionsJson + auto-approve switch case + Stage 57 ReviewerFixLoopLimitActionsJson | 子項 3 actions JSON const 加在哪 + auto-approve switch case 對齊 |
| F7 | `src/AiTeam.Bot/Orchestration/InteractionProcessor.cs` line 141-159 Stage 55B + 57 routing label mapping | 子項 4 InteractionProcessor 加 3 label mapping 位置 |

### 寫入點 spike 報告（在計劃書 Plan Mode 內）

Forge 完成 read 後在 Plan Mode 計劃書內報告：

1. **API 失敗 signal pattern 確認**：① CLI path stdout 含 `insufficient_balance` / `401` / Anthropic-specific error 格式（Trial_v6 觀察樣本）② API path Anthropic SDK 拋 exception type（提供 1-2 候選 + 偏好），Aria 拍（Christ 看不到此實作層細節）
2. **6 routing dispatch 鏈路對照表**：對照 Stage 55B Session B 5 + Stage 57 第 6 既有 routing 的 fire 點 + SendsMessage + InteractionProcessor + ContinuationAction → 下游 Bridge — 第 7 routing 對齊（純執行，無需 Christ 拍）
2. **4 Pipeline Stage Executor catch 點**：每個 stage executor 在哪行 catch + 是否需要動 result.Success=false 既有處理路徑（純執行，Aria 拍）
3. **MockMode auto-approve 預設 action 提案**（議題 13）：retry vs continue 待 spike 後依 Mock 場景驗收順暢度拍（純內部，Aria 拍）

---

## 子項 1：業務 exception 定義 + 雙 path 偵測

### 修法策略

#### 新建 `LlmApiFailureException`

放在 `src/AiTeam.Bot/Agents/`，含 `ProviderType`（Anthropic / Gemini / 預留 / Unknown）+ `RawError` 欄位（議題 14 spike 後 finalize）。

#### CLI path（ClaudeCodeService）偵測

在 subprocess result 解析後加偵測：若 stdout / stderr 含 API 失敗 signal（spike F1 確認 pattern）→ 拋 `LlmApiFailureException`（不破壞既有 catch 邏輯 line 556 / 597）。

#### API path（AnthropicProvider）catch

在 `client.Messages.GetClaudeMessageAsync` 加 try/catch SDK exception → 轉拋 `LlmApiFailureException`。

#### TokenTrackingProvider 確認

API 失敗 throw 時 line 113 `var response = await inner.CompleteAsync(...)` 直接拋出 — line 121-136 token_logs 寫入路徑自然略過（C# exception 流自動跳過後續 statement，不需顯式 try/finally）。✅ pure additive 不需動 TokenTrackingProvider。

---

## 子項 2：4 Pipeline Stage Executor 加 catch + fire + yield

### 修法策略（對齊 Stage 57 ReviewerStageExecutor FixIteration≥3 case pattern）

每個 Stage Executor（DevStageExecutor / ReviewerStageExecutor / QaStageExecutor / DocStageExecutor）在呼叫 Agent service 處包 try/catch `LlmApiFailureException`：

```
catch (LlmApiFailureException apiEx)
{
    _logger.LogWarning("[Stage58] {Stage}：LLM API failure → fire agent_api_failure_intervention + yield 等 Christ（Group={Id}, Agent={Agent}）",
        stageName, state.GroupId, agentName);
    state.LastAgentResult = ...; state.LastAgentName = agentName;
    await PipelineStateHelpers.SaveAsync(context, state);
    var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
    await tgs.NotifyBossAgentApiFailureAsync(group, agentName, apiEx, default);
    await PipelineHitlHelper.YieldForChristResponseAsync(
        context, new <Stage>AgentApiFailureRequest(state.GroupId), _logger,
        "agent_api_failure_intervention", state.GroupId);
    return;
}
```

每個 Stage Executor 加 `[SendsMessage(typeof(<Stage>AgentApiFailureRequest))]` attribute + `HandleAgentApiFailureResponseAsync` handler 三選獨立 path（同 Stage 57 ReviewerStageExecutor.HandleReviewerFixLoopLimitResponseAsync 結構）。

---

## 子項 3：第 7 routing 完整 wiring

對齊 Stage 55B Session B 5 routing + Stage 57 第 6 routing 既有 pattern。詳細實作細節 Forge spike 後 finalize（命名 / dispatch 對照表 / Port 數量 1 統一 vs 4 per-stage）。

---

## 驗收情境

> 計劃書硬規則：本節獨立列出，不分散到子項內。每個非顯然點都有 Mock 場景或手動驗證步驟。

### V1：API failure 觸發 → fire `agent_api_failure_intervention`（取代 silent skip）

**觸發**：開 Dashboard → MockScenarioCard → 選 `framework_pipeline_agent_api_failure` → 觸發 → Pipeline 跑到任一 Agent stage → MockClaudeCodeService 拋 `LlmApiFailureException`

**驗證**：
- SQL：`SELECT "InteractionType", "Status", "AvailableActionsJson" FROM boss_interactions ORDER BY "CreatedAt" DESC LIMIT 1` = `agent_api_failure_intervention` + 3 button JSON（修前 = silent skip task done 無 interaction）
- Bot log：`[Stage58] {Stage}：LLM API failure → fire agent_api_failure_intervention + yield 等 Christ（Agent={Agent}）`
- group.Status = `needs_intervention`（非修前的 `done`）
- token_logs **無新 row**（API 失敗 cost 0 不寫）

### V2：retry button → re-invoke 同 Agent

**觸發**：（V1 觸發後）手動 SQL update interaction `ResponseAction='api_failure_retry'` + Status='responded'

**驗證**：
- Pipeline re-invoke 同一個 Agent task（同 stage Bridge SendMessage）
- 若 Mock 仍設 FailScenario=agent_api_failure → 再 fire 一張 interaction（不無限迴圈靜默跑，每次都需 click）
- 若 Mock 改回 normal → Agent 正常完成 → Pipeline 推進下一 stage

### V3：continue button → 跳過該 Agent 進下階段

**觸發**：（V1 觸發後）手動 SQL update `ResponseAction='api_failure_continue'`

**驗證**：
- state.{Agent}Done=true + SendMessage 下游 Bridge
- Pipeline 推進到下一 stage（Dev→Reviewer / Reviewer→Qa / Qa→Doc / Doc→終）
- group.Status 最終 = `done`（跳過該 Agent 仍完成）

### V4：abort button → SetIntervention end Pipeline

**觸發**：（V1 觸發後）手動 SQL update `ResponseAction='api_failure_abort'`

**驗證**：
- group.Status = `needs_intervention`（保留終止退路）
- Pipeline executor 收 abort → SetInterventionAndYieldAsync end Workflow

### V5：4 Agent 各自 catch 行為一致

**觸發**：依序在 4 個 stage 觸發 Mock API failure（修改 MockScenarioService 內 `failAtAgent` 參數或多 alias case 各自指定）

**驗證**：
- Cody/Vera/Quinn/Sage 4 個 agent 分別 fire interaction 時 context.agent 正確（SQL 查 `JSON_EXTRACT(ContextJson, '$.agentName')`）
- 4 stage executor 都正確 yield + 收 response 後路由（continue/retry/abort 三 path 各別 SendMessage 對的 Bridge）

### V6：regression — Stage 55B Session B 5 routing + Stage 57 第 6 routing 6 場景仍綠

**觸發**：依序跑既有 6 routing Mock（dev_intervention / qa_intervention / devplan_escalate / dev_plan_unable / split_task_proposal / reviewer_fix_loop_limit）

**驗證**：6/6 場景 dispatch 正確（type-specific interaction → user response → Pipeline 推進），不破壞既有設計。

### V7：regression — Stage 56 token_logs 寫入率不被新邏輯誤擋

**觸發**：跑 Mock `new_feature_with_proposal` 完整 pipeline（無 API failure）

**驗證**：
- SQL：token_logs 寫入率仍 100%（vs Stage 56 baseline）
- 新「API 失敗時不寫 token_logs」邏輯只在 LlmApiFailureException 拋出時生效，正常呼叫仍寫入

### V8：build / regression 不破壞

**觸發**：`dotnet build AiTeam.slnx`

**驗證**：
- 0 errors / 0 new warnings
- v3.47.0 version bump 在 `src/Directory.Build.props` 正確套用
- Dashboard 既有 33+10+2 framework_* MudSelectItem 不受新加場景干擾（regression）

---

## 技術約束

- v3.47.0 version bump（Stage 57 v3.46.1 + minor）
- `dotnet build AiTeam.slnx` 0 errors
- 不引入新 Migration（純 routing 邏輯 + exception handling，無 DB schema 改動）
- 不引入新 user transaction（避免 Stage 57 NpgsqlRetryingExecutionStrategy 衝突重蹈）
- 不引入新 idempotent helper（避免 Stage 57 TOCTOU race window 重蹈 — 若 Forge spike 揭露需要，必先 grep codebase 既有 atomic primitive 用法）
- 不動 Stage 55B Session B 既有 5 routing + Stage 57 第 6 routing dispatch 鏈路（純加第 7 routing 對齊既有 pattern）
- 不動 Quinn `qa_failed_intervention` fix loop routing（語意不同 — fix loop ≠ API 爆，獨立 routing）
- 不動 USD billing 守門（Christ 拍板 = B 容錯模式）
- Mock 場景對齊既有 framework_pipeline_* 命名慣例

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-09 | 初版規劃書建立（Aria）— Stage 58 = Trial_v6 揭露 3 🔴 戰略級議題最後一個（FF 五十三 API 餘額容錯性），Stage 57 已收口前兩個（race + Vera fix loop HITL routing）。**Christ 拍板 3 議題**：① 模式 = B 容錯（catch API 401 → 4 Agent 統一 fail-fast，不做 USD billing 守門 over-engineering）② button = 真三選 continue / retry / abort ③ 拆 Session = A 一個 session 跑（兩件事高度耦合）。**Aria 拿捏 12 件純內部議題**（統一 type / per-stage 4 port / 統一 NotifyBoss helper / catch 在 ClaudeCodeService + AnthropicProvider 雙層 / retry 直接 re-invoke / continue 跳下游 / abort SetIntervention end / API 失敗不寫 token_logs / Mock 命名 / 不動 Migration / 不動 prompt）。**Stage 57 教訓主動套入**：不引入新 user transaction（避免 NpgsqlRetryingExecutionStrategy 衝突）+ 不引入新 idempotent helper（避免 TOCTOU race），若 Forge spike 揭露需要必先 grep codebase 既有 atomic primitive 用法。**規劃前期已 grep**：TokenTrackingProvider line 113-138 + AnthropicProvider 無 catch + ClaudeCodeService 既有 catch 區塊 + qa_failed_intervention 既有 NotifyBoss + Pipeline pattern + 4 Stage Executor 既有 result.Success=false 處理路徑 + InteractionProcessor type+action mapping 既有 6 routing label — 對齊自省點 #23 規劃前期 grep 紀律。**Aria 校準錨預估**：×1.2-1.5（混合型「production-ready 補強」性質倍率系統性偏高，Stage 57 ×1.36 + Stage 58 同性質基準），預估 Forge context ~500-700K / Opus 1M + high。
