# Stage 60 Roadmap — v4 framework 邊角 user actions legacy 遷移 + meeting subprocess failure → fail-fast 統一（FF 五十五）

> 目標版本：**v3.49.0**（minor — v4 framework production-ready 補強第三波）
> 狀態：✅ **已完成（2026-05-10）**
> 文件版本：v2.0
> 範圍：FF 五十五（Trial_v7 結案揭露 1 🔴 戰略級新類型議題收口）
> 規模：M-L

---

## 戰略脈絡

Trial_v7 結案揭露第 4 🔴 戰略級議題 — 直接推翻 Trial_v6 結案宣稱「3 🔴 收口 = v4 production-ready」假設：

- **議題揭露**：Christ 點「需要修改」→ Bot 走 legacy `KickoffMeetingService.ModifyTaskPlanAsync` → Petra subprocess 失敗 → `MeetingCommons.RunAgentTurnAsync` 把失敗 swallow 成 placeholder string → caller silent skip 寫入 DB → TaskPlan 從 6,279 字 → 5 字（"（Petra 無回應）"）→ Stage 58 第 7 routing 沒 catch（因走 legacy path）
- **同類根因延伸**：`DesignMeetingService.ModifyDesignPlanAsync` 同 path 同設計缺口（FrameworkDesignRouter `:34` 註解明寫「議題 H1 — Stage 55+ 真切 framework HITL」延宕至今）

**對齊 Trial_v6 結案紀律**：「v4 framework 主路徑 9/9 達成」≠「v4 邊角 user actions 全遷移」+「Mock 全綠 ≠ production 全綠」自省點 #25 第二次驗證（Trial_v7 揭 Mock 沒涵蓋的 modify path）。

---

## 子項清單

### 1. MeetingCommons silent failure → fail-fast 治本（核心子項）

`src/AiTeam.Bot/Orchestration/Meeting/MeetingCommons.cs` 內 `RunAgentTurnAsync(string agentDisplayName, string sessionId, ...)` method（line 34-74）三條 swallow 路徑修法：
- subprocess `!result.Success`（line 62-63）：目前只 LogWarning 不 throw → 改 throw 新 `MeetingSubprocessFailureException`
- 空 output 回傳 placeholder（line 65-67）：歸入同一 fail-fast 路徑
- catch 一般 Exception（line 69-73）：目前 swallow 成 placeholder → 改 re-throw（含 LlmApiFailureException 對齊 Stage 58 既有 marker pattern）

**影響面評估**（Forge spike 階段必跑）：grep 全 caller 確認所有 RunAgentTurnAsync 呼叫站（Kickoff Round 1-3 各 4 Agent + Petra / Design Round 各 Agent / Modify path 等）能容受 throw 而非 placeholder。**這是 regression 風險最大子項**。

### 2. KickoffMeetingService.ModifyTaskPlanAsync 遷移 framework Kickoff revise round（議題 C2 收口）

`src/AiTeam.Bot/Orchestration/Meeting/KickoffMeetingService.cs:212` `ModifyTaskPlanAsync` 改為走 framework path。對齊 Stage 50 framework Kickoff Group Chat orchestration + Stage 51 KickoffMidInterrupt HITL 試點 既有 pattern。

新 framework executor 接管 modify 場景：input = Christ 修改指引 + 現有 TaskPlan + 完整會議 context；output = TaskPlan v2 + 對應 BossInteraction `kickoff` 帶 TaskPlan 摘要進 ContextJson。

對齊 FrameworkKickoffRouter `:32` 註解既有 TODO 說明（「C2 拍板：Petra session_id = group.Id 仍可 resume」— 議題 C2 留的 framework 遷移 TODO 此 Stage 收口）。

### 3. DesignMeetingService.ModifyDesignPlanAsync 遷移 framework Design revise round（議題 H1 收口）

`src/AiTeam.Bot/Orchestration/Meeting/DesignMeetingService.cs:359` `ModifyDesignPlanAsync` 改為走 framework path。對齊 Stage 52 framework Design Meeting Workflow（fan-out/fan-in + 條件式 Demi + needs_adjustment B2 子流程）既有 pattern。

對齊 FrameworkDesignRouter `:34` 註解既有 TODO 說明（「議題 H1，Stage 55+ 真切 framework HITL」— 此 Stage 收口）。

### 4. Stage 58 marker pattern 擴展接管 meeting subprocess failure

對齊 Stage 58 既有 `[API_FAILURE]` marker pattern（PipelineState `:281` HandleResponseAsync marker check + AgentQueueProcessor specific catch + 4 Stage Executor wire 第 7 routing），新增通用 `[SUBPROCESS_FAILURE]` marker：

- AgentQueueProcessor catch 新 `MeetingSubprocessFailureException` → build `[SUBPROCESS_FAILURE]` summary 前綴 result → call HandleAgentCompletedAsync 走正常 callback flow
- Pipeline Stage Executor `HandleResponseAsync` 內既有 `[API_FAILURE]` marker check 旁加 `[SUBPROCESS_FAILURE]` marker check → 統一接 `agent_api_failure_intervention` 第 7 routing（語意：兩類 failure 都是 Agent 不可恢復失敗，Christ 三選 continue/retry/abort 統一）

不新增 routing type — 重用 Stage 58 第 7 routing。

### 5. 盤點 + 處理剩餘 v4 邊角 user actions legacy paths（Forge spike 範圍）

Forge Plan Mode 階段 grep 找：
- 所有 ResponseAction 對應 handler 走 legacy path（不只 `kickoff_modify` / `design_modify`，還有 `kickoff_restart` / `kickoff_stop` / 其他 modify-style action）
- 對齊 InteractionProcessor `src/AiTeam.Bot/Orchestration/InteractionProcessor.cs:132` dispatch table 全盤點

預期 spike 揭露 2-4 個邊角 actions 待遷移。**範圍變更條件**：spike 揭露 > 4 個或設計衝突 → escalate Christ 拍板分 Stage 60A/60B（modify 兩 path 在 60A / 其他 actions 在 60B）。

### 6. Mock 場景補強

- `framework_modify_taskplan_happy`：Christ 點 modify → framework Kickoff revise round → Petra 跑 → TaskPlan v2 DB 寫入 ≥ 4000 字 + BossInteraction kickoff 帶 TaskPlan 摘要進 ContextJson
- `framework_modify_designplan_happy`：同上 Design path
- `meeting_subprocess_failure`：MeetingCommons subprocess 失敗 mock → fail-fast → AgentQueueProcessor build `[SUBPROCESS_FAILURE]` result → 第 7 routing fire BossInteraction `agent_api_failure_intervention` + context.agent="Petra" → MockMode auto-approve `api_failure_continue` → 流程推進
- `meeting_modify_during_subprocess_failure`：Christ 點 modify 同時 subprocess 失敗 → 走 routing 不 silent skip（驗證 fail-fast 對 modify path 同樣有效）

### 7. Version bump v3.49.0

`src/Directory.Build.props` `<Version>` 標籤 + Dashboard 頁腳自動讀。

---

## 設計決策

### 1. 路線 A 主路徑真正遷移 vs 路線 B 純 fail-fast 補強

| 路線 | 內容 | 評估 |
|---|---|---|
| **A 主路徑遷移**（推薦）| ModifyTaskPlan / ModifyDesignPlan 真切 framework path（子項 2+3） | 對齊 Stage 49-58 既有遷移紀律治本，徹底解決 v4 邊角 legacy 缺口 |
| B 純 fail-fast 補強 | 保留 legacy path 只補 MeetingCommons fail-fast（子項 1+4） | 最小修改但不解決根因，FF 五十五描述明寫「邊角 user actions legacy 遷移」必須做 |

→ **拍板路線 A** — Stage 60 範圍含 1+2+3+4+5+6+7 全子項。

### 2. Marker pattern 設計：擴用 [API_FAILURE] vs 新 [SUBPROCESS_FAILURE]

| 選項 | 評估 |
|---|---|
| 擴用 `[API_FAILURE]` | 簡單但語意混淆（不只 API 失敗，含 subprocess 異常） |
| 新增 `[SUBPROCESS_FAILURE]`（推薦）| 精準語意 + 對齊 Stage 58 marker pattern 累積；兩 marker 並存複雜度可控（第 7 routing wire 加一行 check） |

→ **拍板新 marker `[SUBPROCESS_FAILURE]`** — 語意清晰，未來其他 subprocess failure（不只 meeting）也可用同 marker 接 routing。

### 3. routing 設計：新增第 8 routing vs 重用第 7 routing

→ **拍板重用第 7 routing `agent_api_failure_intervention`** — 語意：兩類 failure 都是「Agent 不可恢復失敗」，Christ 三選 continue/retry/abort 統一。新增 routing 不 justify。

### 4. ModifyExecutor 設計層級：Workflow 主迴路 vs 獨立 mini Workflow

| 選項 | 評估 |
|---|---|
| 主迴路 Workflow 內加 ModifyExecutor | 對齊 Stage 51 KickoffMidInterrupt HITL 既有 pattern；framework state 統一 | 推薦 |
| 獨立 mini Workflow（modify 獨立啟動）| 邏輯獨立但 framework state 拆兩處 | 不推薦 |

→ **拍板主迴路 Workflow 內 ModifyExecutor**（Forge Plan Mode 細節設計）。

### 5. Forge spike 預期揭露議題

- 子項 1 MeetingCommons fail-fast 修法的 caller 影響面（Round 1-3 主流程 vs Modify path 是否能容受 throw）
- 子項 2+3 framework executor wire 點（對齊 Stage 50/52 既有 pattern 是否需擴）
- 子項 5 邊角 user actions 數量盤點（決定範圍變更需求）

---

## 驗收情境

### Mock 場景驗收（Forge 自驗）

#### 場景 A：framework_modify_taskplan_happy

**觸發**：`curl -X POST http://localhost:5052/internal/mock/scenario -H "X-Api-Key: $API_KEY" -d '{"scenario": "framework_modify_taskplan_happy"}'`（port 5052 + X-Api-Key 對齊 forge-self-verify skill）

**驗證**：
- Mock Kickoff Round 1-3 跑完 → BossInteraction `kickoff` fire → MockMode auto-approve `kickoff_modify` 帶修改指引
- Bot log 出現 `[Stage60] Framework Kickoff modify executor 啟動`（不是 legacy `KickoffMeetingService.ModifyTaskPlan`）
- DB query：`SELECT LENGTH("TaskPlan") FROM task_groups WHERE "Id" = '<group_id>'` ≥ 4000 字（不是 5 字 placeholder）
- DB query：`SELECT "ContextJson" FROM boss_interactions WHERE "InteractionType" = 'kickoff' AND "TaskGroupId" = '<group_id>' ORDER BY "CreatedAt" DESC LIMIT 1` 含 TaskPlan 摘要欄位

#### 場景 B：framework_modify_designplan_happy

同場景 A 結構，對應 Design Meeting + ModifyDesignPlan path。驗證 DesignPlan 欄位。

#### 場景 C：meeting_subprocess_failure

**觸發**：mock subprocess 失敗（MeetingCommons.RunAgentTurnAsync caller mock injection 模擬 subprocess 異常）

**驗證**：
- Bot log 出現 `[Stage60] MeetingCommons subprocess failure → throw MeetingSubprocessFailureException`（不是 silent skip placeholder）
- AgentQueueProcessor catch 新 exception → build `[SUBPROCESS_FAILURE]` result → log `[Stage60] AgentQueueProcessor catch MeetingSubprocessFailureException → build [SUBPROCESS_FAILURE] result`
- Pipeline Stage Executor marker check → fire BossInteraction `agent_api_failure_intervention` + context.agent="Petra"
- DB query：`SELECT "InteractionType", "Status" FROM boss_interactions WHERE "TaskGroupId" = '<group_id>' AND "InteractionType" = 'agent_api_failure_intervention'` 應有 1 row
- MockMode auto-approve `api_failure_continue` → 流程推進

#### 場景 D：meeting_modify_during_subprocess_failure

**觸發**：場景 A 流程中對 modify Petra subprocess 注入 mock 失敗

**驗證**：modify path 觸發 subprocess failure → 走第 7 routing 不 silent skip（不寫 placeholder TaskPlan）。對齊 Trial_v7 反例修根因。

### Christ 視覺驗收（必驗）

- Christ Discord 點 Kickoff modify embed → Discord 看到「修改進行中」狀態提示（framework 中途過程）+ 完成後 BossInteraction kickoff embed 顯示 TaskPlan v2 摘要（不是「無計劃書」）
- Christ Dashboard 點 Kickoff modify → 同上效果（雙通道一致）

### 0 regression 確認

- Trial_v6/v7 既有 Kickoff/Design Round 1-3 主流程行為不變（cost / 時長 / TaskPlan 字數對齊既有 baseline）
- 既有 33 framework_* Mock 場景仍綠（dotnet test + Mock 場景全跑）

---

## 實作紀錄（v2.0 — 2026-05-10 Forge 結案）

### 實作完成項目（依子項對應）

**子項 1 — MeetingCommons.RunAgentTurnAsync fail-fast** ✅
- 新建 [`MeetingSubprocessFailureException.cs`](../../src/AiTeam.Bot/Agents/MeetingSubprocessFailureException.cs)（含 AgentDisplayName / SessionId / RawError props，對齊 Stage 58 [`LlmApiFailureException.cs`](../../src/AiTeam.Bot/Agents/LlmApiFailureException.cs) 設計）
- [`MeetingCommons.cs`](../../src/AiTeam.Bot/Orchestration/Meeting/MeetingCommons.cs) `RunAgentTurnAsync` 三條 swallow 路徑全改 throw（subprocess !result.Success / 空 output / catch Exception）— catch 順序：`MeetingSubprocessFailureException` re-throw → `LlmApiFailureException` re-throw（保留 Stage 58 type）→ `OperationCanceledException` 透傳 → 其他 wrap 成 `MeetingSubprocessFailureException`

**子項 2 — Kickoff Modify 真切遷 framework Pipeline（議題 C2 收口）** ✅
- [`KickoffStageExecutor.cs`](../../src/AiTeam.Bot/Workflows/Pipeline/Executors/KickoffStageExecutor.cs) `HandleResponseAsync` 加 `case "modify"` + `case "restart"`，`HandleEntryAsync` sync await 外圍加 try/catch 接 routing
- 新增私有 helper：`HandleKickoffModifyAsync` / `ResetKickoffStateForRestartAsync` / `FireKickoffAgentApiFailureRoutingAsync` / `HandleAgentApiFailureResponseAsync`
- [`FrameworkKickoffRouter.cs`](../../src/AiTeam.Bot/Orchestration/Meeting/FrameworkKickoffRouter.cs) 新增 `RunKickoffModifyAsync`（內部委派既有 [`KickoffMeetingService.ModifyTaskPlanAsync`](../../src/AiTeam.Bot/Orchestration/Meeting/KickoffMeetingService.cs) — 共用 Petra session resume + modify prompt + JSON parse 邏輯避免雙寫漂移）+ `CreateKickoffConfirmationAfterModifyAsync`（small/large impact 雙路徑重開 BossInteraction）
- `HandleKickoffMeetingAsync` 加 `MeetingSubprocessFailureException` / `LlmApiFailureException` specific catch 在 generic catch 之前 → re-throw 給 KickoffStageExecutor

**子項 3 — Design Modify 真切遷 framework Pipeline（議題 H1 收口）** ✅
- [`DesignStageExecutor.cs`](../../src/AiTeam.Bot/Workflows/Pipeline/Executors/DesignStageExecutor.cs) 對稱改造（含 `HandleDesignModifyAsync` 從 BossInteraction.contextJson 取 petraSessionId / `FireDesignAgentApiFailureRoutingAsync` / `HandleAgentApiFailureResponseAsync` / `ResetDesignStateForRetryAsync`）
- [`FrameworkDesignRouter.cs`](../../src/AiTeam.Bot/Orchestration/Meeting/FrameworkDesignRouter.cs) 新增 `RunDesignModifyAsync` + `CreateDesignConfirmationAfterModifyAsync` + specific catch re-throw

**子項 4 — Stage 58 marker pattern 擴展接 meeting subprocess failure** ✅
- 新建 [`WorkflowExceptionHelper.cs`](../../src/AiTeam.Bot/Workflows/Pipeline/WorkflowExceptionHelper.cs) `FindInner<T>(Exception?)` helper — 從 framework `WorkflowErrorEvent.Exception` InnerException chain / AggregateException 走訪抓 `MeetingSubprocessFailureException` / `LlmApiFailureException`
- `RunWorkflowAsync` watch loop 偵測 → throw 出讓 router 上層 specific catch fire 第 7 routing
- [`PipelineState.cs`](../../src/AiTeam.Bot/Workflows/Pipeline/PipelineState.cs) 加 `KickoffAgentApiFailureRequest` / `KickoffAgentApiFailureResponse` / `DesignAgentApiFailureRequest` / `DesignAgentApiFailureResponse` 4 record
- [`PipelineWorkflowFactory.cs`](../../src/AiTeam.Bot/Workflows/Pipeline/PipelineWorkflowFactory.cs) 加 2 PortId 常數 + 2 RequestPort.Create + 4 AddEdge 雙向 wiring
- [`FrameworkPipelineRouter.cs`](../../src/AiTeam.Bot/Orchestration/Meeting/FrameworkPipelineRouter.cs) 加 `ResumeAfterKickoffAgentApiFailureAsync` / `ResumeAfterDesignAgentApiFailureAsync` 2 typed thin wrapper
- [`PipelineRoutingService.cs`](../../src/AiTeam.Bot/Orchestration/Routing/PipelineRoutingService.cs) `TryRoutePipelineAgentApiFailureAsync` switch 加 `case "Petra-Kickoff"` / `case "Petra-Design"` dispatch
- BossInteraction type 仍 reuse `agent_api_failure_intervention`（Roadmap 拍板「不新增 routing type」對齊 ✓）— context.agent 區分 Petra-Kickoff / Petra-Design

**子項 5 — 邊角 user actions 範圍盤點 + 處理** ✅
- spike 揭露 3 獨立 actions + 1 modify 子分支：`kickoff_modify`（子項 2 收口）/ `design_modify`（子項 3 收口）/ `kickoff_restart`（子項 2 case "restart"）/ `kickoff_modify` large-impact 子分支（CreateKickoffConfirmationAfterModifyAsync impact==large 路徑）
- [`MeetingOrchestrationService.cs`](../../src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs) Pipeline path 接管條件擴 `modify` + `restart`（Kickoff line 651）/ `modify`（Design line 831）

**子項 6 — Mock 場景補強（4 場景）** ✅
- [`MockClaudeCodeService.cs`](../../src/AiTeam.Bot/Agents/MockClaudeCodeService.cs) `RunMeetingSessionAsync` 開頭加 4 個 FailScenario 偵測分支（meeting_subprocess_failure / meeting_modify_during_subprocess_failure / framework_modify_taskplan_happy / framework_modify_designplan_happy modify path）
- isFrameworkDesignScenario 條件擴 framework_modify_designplan_happy + Petra Round decision 切 `escalate` 開 design BossInteraction
- [`MockScenarioService.cs`](../../src/AiTeam.Bot/Services/MockScenarioService.cs) 加 4 alias scenario 設定 + workflowType switch 4 entry

**子項 7 — v3.49.0 bump** ✅ — [`Directory.Build.props`](../../src/Directory.Build.props)

**衍生 — MockMode auto-approve scenario-aware**（Forge 自驗第一輪揭露補強）
- [`InteractionService.cs`](../../src/AiTeam.Bot/Services/InteractionService.cs) auto-approve switch 加 scenario-aware 三條 case：`framework_modify_taskplan_happy → kickoff_modify` / `framework_modify_designplan_happy → design_modify` / `meeting_modify_during_subprocess_failure → kickoff_modify`，並產出 mock modifyContent 文字
- [`BossInteractionRepository.RespondAsync`](../../src/AiTeam.Data/Repositories/BossInteractionRepository.cs) 加 responseContent overload（modify path 必填）

### 關鍵設計決策（Forge spike 揭露 + Aria 拍板全 Forge 自決）

| 議題 | Roadmap 原拍板 | spike 揭露 | 落地解法 |
|---|---|---|---|
| 1. catch 點 | AgentQueueProcessor catch | Meeting subprocess 不走 AgentQueueProcessor — 走 Pipeline Stage Executor sync await router | catch 點移到 KickoffStageExecutor / DesignStageExecutor + Router specific re-throw + RunWorkflowAsync watch loop unwrap WorkflowErrorEvent |
| 2. marker 接法 | HandleResponseAsync marker check（對齊 Stage 58 4 stage executor） | Stage Executor 收的是 `KickoffCompletionResponse` 不是 `AgentExecutionResult`，無 Summary 可 marker check | `[SUBPROCESS_FAILURE]` 改命名語意（log + BossInteraction.Description 前綴），wire 用 catch path |
| 3. routing 接法 | 重用第 7 routing 不新加 Port | 第 7 routing dispatch 依 contextJson.agent 切 4 typed wrapper（Dev/Reviewer/QA/Doc）— Petra 不在 case | 新加 2 Port（KickoffAgentApiFailure / DesignAgentApiFailure）+ 2 wrapper + Petra-{Stage} 命名 — BossInteraction type 仍 reuse `agent_api_failure_intervention`（不新加 routing type）|

**「真切遷移」實質意義拍板**：framework Pipeline modify 不是要建獨立 Modify Workflow factory（過度工程），而是 **entry 改走 Pipeline framework lifecycle**（KickoffStageExecutor.HandleResponseAsync case "modify"），內部委派既有 `KickoffMeetingService.ModifyTaskPlanAsync` / `DesignMeetingService.ModifyDesignPlanAsync`（共用 Petra session resume + modify prompt + JSON parse 邏輯避免雙寫漂移）。「framework-ness」體現在：① wire-up 在 Pipeline 級 ② subprocess 失敗自動 fail-fast 接第 7 routing ③ legacy MeetingOrchestrationService case "modify" 仍保留為 PipelineFrameworkStateJson IS NULL 的歷史殘留 fallback safety net。

### 驗收後修正（Forge 自驗第一+第二輪揭露 + 補強）

**第一輪揭露**：MockMode auto-approve 預設 fire `kickoff_continue` / `design_continue`，不會觸發 modify path → DB plan_len 仍只有 124 字（初始 Kickoff plan）。

→ 補強 commit [32b2bc3](https://github.com/darkleong/AiTeam/commit/32b2bc3)：InteractionService MockMode auto-approve 加 scenario-aware switch + RespondAsync 擴 ResponseContent overload。

**第二輪揭露**：Pipeline DesignStageExecutor 收 ConsensusNoSplit decision 不開 design BossInteraction（直接 → DevPlanStageBridge），導致 `design_modify` 自動回應沒有 trigger 點。

→ 補強 commit [0962509](https://github.com/darkleong/AiTeam/commit/0962509)：MockClaudeCodeService 對 framework_modify_designplan_happy 場景把 Petra Round decision 切 `escalate` → DesignFinalizationDecision = EscalateConfirmationOpened → 開 design BossInteraction → auto-approve `design_modify` → modify path runs。

兩輪都是 Forge 自驗 spike 揭露 + 自補修正，不需 escalate Christ。

### Mock 覆蓋情況（4 Mock 場景全 PASS）

| 場景 | 觸發條件 | DB 驗證 | Bot log 驗證 |
|---|---|---|---|
| A `framework_modify_taskplan_happy` | MockMode auto-approve `kickoff_modify`（scenario-aware）→ RunKickoffModifyAsync | TaskPlan **10302 字** ≥ 4000 ✓ | `[Stage60] Framework Kickoff modify executor 啟動` ✓ |
| B `framework_modify_designplan_happy` | Petra escalate → design BossInteraction → auto-approve `design_modify` | DesignPlan **10302 字** ≥ 4000 ✓ | `[Stage60] Framework Design modify executor 啟動` ✓ |
| C `meeting_subprocess_failure` | MeetingCommons subprocess Success=false → throw → KickoffStageExecutor catch | BossInteraction `agent_api_failure_intervention` agent="Petra-Kickoff" 1 row ✓ | `[Stage60] MeetingCommons subprocess failure → throw MeetingSubprocessFailureException`（Demi）+ `[Stage60] KickoffStage catch → fire agent_api_failure_intervention (agent=Petra-Kickoff)` + `[Stage58] ProcessBossResponseAsync agent_api_failure_intervention Pipeline 接管` ✓ |
| D `meeting_modify_during_subprocess_failure` | modify path 觸發時 subprocess Success=false → throw → 第 7 routing | TaskPlan 仍是初始 124 字（**不被覆寫成 placeholder** — Trial_v7 反例修根因驗證）✓ | `[Stage60] KickoffStage modify` + `subprocess failure → throw` + `fire agent_api_failure_intervention` ✓ |

### 踩坑紀錄

1. **MockMode 沒啟用 第一次 trigger 失敗** — Bot 啟動時 MockMode = false，需 DB 改 + reload-cache scope=agents 才生效（reload-cache scope=appsettings 不存在，正確 scope 是 agents）。記入 SOP：production deploy 後跑 Mock 之前要先確認 DB `app_settings.MockMode = true`。

2. **TaskStatus alias 與 namespace import 共存** — KickoffStageExecutor 加 `using AiTeam.Shared.Constants;` 後與既有 `using TaskStatus = AiTeam.Shared.Constants.TaskStatus;` 共存無衝突（C# alias 優先）。但要確認 namespace 內沒有與 alias 同名類別 — Stage 60 期間多次踩同名變數 `output` collision（local scope shadowing），改名 `modifyOutput` 解決。

3. **Mock 大文字 JSON escape 細節** — 為驗 plan ≥ 4000 字，MockClaudeCodeService 內 `bigPlan.ToString().Replace("\"", "\\\"").Replace("\n", "\\n")` 把 80 章節文字 escape 成單行 JSON string，最終 parse 後 plan 為 10302 字（含換行）。`KickoffPrompts.TryParseModifyDecision` 正確 parse JSON。

4. **第二輪揭露 + 補強流程** — design BossInteraction 不開的問題在 spike Plan Mode 階段沒看穿（FrameworkDesignRouter FinalizeDesignAsync 三 decision path 之中只 EscalateConfirmationOpened 開卡，ConsensusNoSplit / SplitProposalOpened 不開 design embed）。Mock escalate path 補強之後 design path 全綠。**自反省候選**：spike 揭露 framework Workflow decision 拓撲時，要連帶看 Pipeline Stage Executor 對應 decision 的 BossInteraction 開 / 不開行為（不只看主路徑）。

5. **Pipeline path 接管條件擴範圍紀律** — MeetingOrchestrationService 既有條件 `(continue || stop)` 擴成 `(continue || stop || modify || restart)` 是必要 — 缺一個 lowerDecision 字面 match，Pipeline path 不接管 → 走 legacy switch case → 違背 Stage 60 真切遷移精神。改動完整紀律：每加一個 Pipeline 接管 decision，同步在 KickoffStageExecutor.HandleResponseAsync / DesignStageExecutor.HandleResponseAsync 加對應 case 處理。

6. **rebase 衝突小坑** — push 期間 origin/main 有他人 commit（FF36 doc 補強 094e9b5），git pull --rebase + push 解決。Forge 工作中段 push 前先 fetch + rebase 是更穩妥流程。

---

## 技術約束

- 對齊 workflow_aria.md 第三節 A 第 5+6+7 條紀律（不寫 code 範例 + 大檔 reference 標精準 line + 環境細節 reference 標 source of truth）
- **環境細節 source of truth**：Bot Internal API port `5052` 見 `docker-compose.prod.yml` / X-Api-Key value 取 `docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey` / Internal endpoint 真實 path 見 `src/AiTeam.Bot/Api/InternalController.cs`
- 對齊 Stage 50/51/52/58 framework Meeting + HITL + marker pattern 既有累積
- MeetingCommons.RunAgentTurnAsync 修法影響面廣（全 Meeting subprocess caller） — Forge spike 必跑 caller 影響面 grep + Mock regression 全跑
- 不新增 EF Migration（不動 schema — 對齊 Stage 58 marker pattern 不動 DB）
- 不引入新 routing type — 重用 Stage 58 第 7 `agent_api_failure_intervention`

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-10 | 初版計劃書建立（Aria）— Stage 60 = FF 五十五（Trial_v7 結案揭露 1 🔴 戰略級新類型議題收口）。範圍：① MeetingCommons.RunAgentTurnAsync silent failure → fail-fast 治本核心 ② KickoffMeetingService.ModifyTaskPlanAsync 遷 framework Kickoff revise round（議題 C2 收口）③ DesignMeetingService.ModifyDesignPlanAsync 遷 framework Design revise round（議題 H1 收口）④ Stage 58 marker pattern 擴展新 `[SUBPROCESS_FAILURE]` 接第 7 routing 統一 ⑤ Forge spike 盤點剩餘 v4 邊角 user actions legacy paths ⑥ Mock 場景補強 4 場景（含 modify happy 雙 path + subprocess failure + modify during subprocess failure）⑦ v3.49.0 bump。**設計決策 5 條**：路線 A 主路徑真正遷移 / 新 marker `[SUBPROCESS_FAILURE]` / 重用第 7 routing / ModifyExecutor 主迴路內 / Forge spike 預期揭露 caller 影響面。**規劃前期已 grep 驗證**：MeetingCommons line 34-74 silent failure 三條路徑（subprocess !result.Success / 空 output / catch Exception 全 swallow placeholder）+ ModifyTaskPlanAsync line 212-263 + ModifyDesignPlanAsync line 359 + FrameworkKickoffRouter :32 + FrameworkDesignRouter :34 既有 TODO 註解 + Stage 58 marker pattern PipelineState :281 + 4 Stage Executor HandleResponseAsync 對齊位置 + InteractionProcessor :132 dispatch table。**規模 M-L** / Opus 1M + medium-high / 預估 ~450-650K（對齊 Trial_v7 結案紀錄）。Mock 全綠 + Forge 自驗 4 場景 + Christ 視覺驗收 modify 雙通道一致。 |
| v2.0 | 2026-05-10 | **Forge 結案** — v3.49.0 deployed + 4 Mock 場景全 PASS + 實作紀錄章節登錄。**4 commit 一氣呵成**：① [`8d0637d`](https://github.com/darkleong/AiTeam/commit/8d0637d) 主實作（925+/15- 變動 / 13 file modified / 2 new file）② [`32b2bc3`](https://github.com/darkleong/AiTeam/commit/32b2bc3) MockMode auto-approve scenario-aware + RespondAsync ResponseContent overload（Forge 自驗第一輪揭露 modify path 沒被 auto-approve fire 補強）③ [`0962509`](https://github.com/darkleong/AiTeam/commit/0962509) framework_modify_designplan_happy 加 Petra escalate path（Forge 自驗第二輪揭露 design BossInteraction 沒開補強）④ [`5eefeb8`](https://github.com/darkleong/AiTeam/commit/5eefeb8) Roadmap 實作紀錄登錄。**Forge spike 揭露 3 議題 Aria 拍板全 Forge 自決**：① catch 點在 KickoffStageExecutor 而非 AgentQueueProcessor ② `[SUBPROCESS_FAILURE]` 命名語意 wire catch 不做 marker check ③ 新加 2 Port + Petra-{Stage} 命名（per-stage Port pattern 對齊 Stage 58 紀律延續）。**4 Mock 場景全 PASS**：A taskplan_modify TaskPlan 10302 字 / B designplan_modify DesignPlan 10302 字 / C subprocess_failure → 第 7 routing fire / D modify-during-failure 不 silent skip（Trial_v7 反例修根因驗證）。**dotnet build clean + dotnet test 131 tests pass**。**踩坑 6 條**：MockMode 預設 false 需先 reload / TaskStatus alias 共存 / output 變數 shadowing / Mock 大文字 JSON escape / Pipeline 接管條件擴需配對 Stage Executor case / git rebase 中段 push 衝突。**等 Christ 觸發 Aria gate2 + Discord/Dashboard modify 雙通道視覺驗收**。 |
