# Stage 51：v4 漸進遷移第三步 — framework HITL pattern 試點（Kickoff Workflow 中途介入）+ feature flag

> 對應 Future Feature：v4 漸進遷移 6 Stage 路線第三步（[Stage 48 spike 報告](../experiments/Spike_v1_MsAgentFramework.md) 節 7）— 不對應特定 active FF（v4 路線進入 Stage 工作模式，按 Stage 走不開新 FF）
> 對應版本：**v3.37.0**（v4 漸進遷移第三個產生版本變動的 Stage）
> 建立日期：2026-05-02
> 狀態：✅ **已完成**（2026-05-02，6 場景全綠 + 0 follow-up）
> 文件版本：v2.0（結案版）

---

## 概述

**戰略背景**：[Stage 48 FF 四十九 spike](Stage_48_Roadmap.md) 結論 = 採用 MS Agent Framework，啟動 6 Stage 漸進遷移路線。**Stage 49 v3.35.0 + Stage 50 v3.36.0** 完成 v4 首發 + 第二步（Appeal Loop + Kickoff Meeting）。**Stage 51 是 v4 漸進遷移第三步** — 引入 framework **Human-in-the-Loop（HITL）pattern** + Checkpointing pause-resume 機制。

**性質特殊（A3 試點路線拍板）**：本 Stage **不切既有 BossInteraction 機制**（46 檔涉及 + 10+ type + 雙通道樂觀鎖太成熟，無法 1:1 對應 framework HITL 替代），而是**新建 framework HITL pattern 試點**，給 Stage 54 收尾或 Stage 55+ 動態流程架構真正切 HITL 鋪路。

**B1 試點場景拍板**：**Kickoff Workflow 中途介入** — Stage 50 既有 Kickoff Workflow 4 Agent 並行 + Petra 整理多輪迭代期間，Christ 能在某個 superstep 邊界輸入修改指引（如「請特別考慮 X 場景」），workflow 從當前 state resume 繼續跑（非結束後 Modify 流程）。對齊既有 KickoffMeetingService.ModifyTaskPlanAsync legacy「Workflow 結束後修改」**互斥不重疊**。

**核心 lifecycle 改變（vs Stage 49/50）**：
- Stage 49/50：framework Workflow 同步跑完（`InProcessExecution.RunStreamingAsync` → 收 `WorkflowOutputEvent` → 結束）
- **Stage 51**：framework Workflow 跑到 RequestPort 點 yield → router 在 `WatchStreamAsync` 收到 `RequestInfoEvent` 時 **break loop**（保留 checkpoint pending request）→ 開 BossInteraction → Christ 回應後 **新 HTTP scope** 重啟 workflow（從 checkpoint resume）+ `SendResponseAsync` 送回應 → workflow 從 yield 點繼續跑到結束

**範圍邊界（A3 試點精神）**：
- ✅ **新建**：framework RequestPort 整合層 + `FrameworkHitlBridge` service + 新 BossInteraction type `framework_kickoff_mid_interrupt`
- ❌ **不動**：既有 BossInteraction 10+ type 任何一個（proposal / kickoff / design / merge_notify / intervention / devplan_escalate / split_task_proposal / 等）
- ❌ **不動**：既有 InteractionService / InteractionRespondService / InteractionProcessor 主流程
- ❌ **不動**：KickoffMeetingService.ModifyTaskPlanAsync legacy「Workflow 結束後修改」流程（C2 拍板下沿用）
- ❌ **不切**：Stage 49 Appeal escalate / Stage 50 Kickoff escalate（這兩個沿用 Stage 50 慣例 「Workflow 結束後開 BossInteraction」）

**v4 路線第三步風險預警**：
- framework HITL `RequestPort` 在 .NET 1.3.0 stable 是否含完整 C# canonical sample（必須 spike 驗證）
- Checkpointing + pending requests 在 C# `ICheckpointStore<JsonElement>` 是否同樣可用（vs Python sample 已驗）
- **跨 HTTP scope workflow run 物件 lifecycle**（Stage 49/50 router 內同一 scope build + run，Stage 51 試點需跨 scope 持有 checkpoint state）
- 既有 InteractionProcessor 3 秒輪詢 Dashboard 回覆機制如何 routing 到 framework workflow resume（新 bridge service）
- → feature flag 為主要安全網（雙 flag 連動：UseFrameworkKickoff + UseFrameworkKickoffMidInterrupt 都 true 才啟用試點）

---

## 設計決策（Christ 2026-05-02 拍板）

### 主路線拍板（戰略級）

| 議題 | 拍板 | 替代方案 |
|---|---|---|
| **議題 A：Stage 51 範圍** | **A3：framework HITL pattern 試點**（不切既有 BossInteraction，新建試點驗證機制給 Stage 54+ 鋪路）| A1 完整切 BossInteraction（XL 工時 + 樂觀鎖機制 break）/ A2 只切 Stage 49/50 escalate 路徑（L 工時 + 改寫剛結案的 Stage 49/50 風險高）/ A4 跳過 Stage 51（v4 路線 HITL 留 Stage 54 才驗）|
| **議題 B：試點場景** | **B1：Kickoff Workflow 中途介入** — Christ 在 Kickoff 多輪會議跑期間（任何 superstep 邊界）能輸入修改指引，workflow 從當前 state resume | B2 Appeal Workflow 中途介入（Appeal 流程較固定，HITL 價值低）/ B3 純技術試點 Mock workflow（沒 production 整合驗證價值低）|
| **議題 C：Petra session 持久化** | **沿用 Stage 50 C2 拍板** — Petra session_id = group.Id 不變，HITL response 透過 framework state 傳遞而非 Claude Code session | 變動 Petra session 機制（破壞 C2 + 影響既有 Modify 流程）|
| **議題 D：feature flag 顆粒度** | **獨立 `Workflow:UseFrameworkKickoffMidInterrupt`**（與 Stage 49 UseFrameworkAppealLoop / Stage 50 UseFrameworkKickoff 完全獨立，雙 flag 連動：本 flag 只在 UseFrameworkKickoff = true 時有意義）| 單一 flag（不漸進啟用）/ 取消 flag（試點全跑）|
| **議題 E：HITL response → InteractionService 整合層** | **D2：抽 wrapper service `FrameworkHitlBridge`**（解耦 + 給 Stage 54 收尾真正切 HITL 時複用） | D1 RequestPort handler 內直接 call InteractionService（緊耦合，Stage 54 重寫成本高）|
| **議題 F：spike 階段** | **三項全驗（Forge Plan Mode 第一步）**：① framework `RequestPort` C# 1.3.0 canonical sample 是否完整 ② `ICheckpointStore<JsonElement>` 對 pending requests 序列化是否可用 ③ 跨 HTTP scope workflow lifecycle pattern（router build/run/yield/resume 4 步分散到不同 scope） | 擇一驗（風險高，沿用 Stage 49/50 風險點 #1 慣例 — 主動驗證高風險點）|

### Aria 拿捏（已決）

| # | 議題 | 決定 |
|---|---|---|
| 1 | 試點觸發機制 | **Christ Dashboard 主動點「中途介入」按鈕觸發**（非每次 Kickoff 都 emit RequestInfoEvent）— Dashboard 流程追蹤頁加 button，按下後透過 Bot Internal API（沿用 Stage 47 `BotInternalController` pattern）通知 framework workflow 在下個 superstep 邊界 emit RequestInfoEvent；workflow 內部加 `MidInterruptCheckExecutor` node 在每個 round 結束後檢查 framework state 內「中途介入」flag |
| 2 | DB schema | **不加新欄位** — pending request 隨 KickoffFrameworkStateJson Checkpointing 一起保存（framework 內建支援），「中途介入」flag 也透過 framework state 傳遞（不寫 DB column）|
| 3 | BossInteraction 新 type | `framework_kickoff_mid_interrupt`（對齊既有 type 命名 — `framework_*` prefix 區隔 v4 path）+ AvailableActionsJson `[{"id":"midinterrupt_apply","label":"套用修改 ✏️","color":"info","requiresInput":true},{"id":"midinterrupt_cancel","label":"取消介入","color":"default","requiresInput":false}]`|
| 4 | TaskGroupService.ProcessBossResponseAsync 新 case | `case "framework_kickoff_mid_interrupt"` → 呼叫 `FrameworkHitlBridge.HandleMidInterruptResponseAsync(group, action, content, ct)` |
| 5 | Crash Recovery 整合 | 沿用 Stage 50 `RecoverStuckFrameworkKickoffsAsync` pattern + 加判斷：若 KickoffFrameworkStateJson 含 pending request → Recovery 不清 marker 而是**等待 Christ 回應後再 resume**（新狀態：「等待人類回應」不是「卡住」）|
| 6 | Bot 重啟期間 BossInteraction 持久性 | 既有設計已支援（BossInteraction 本就是 DB 持久 + InteractionProcessor 重啟後繼續輪詢），無需新機制 |
| 7 | Token 計費 | 沿用 Stage 49/50 機制（透過 `MeetingCommons.RunAgentTurnAsync` 內部記錄）— HITL pause 期間不產生 LLM token，無需新整合 |
| 8 | CLAUDE_*.md prompt | 不動（HITL 是 framework workflow 控制層機制，不影響 Agent prompt）|

### Stage 51 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **0** | **Spike 第一步：3 項驗證**（Forge Plan Mode 內含）— 議題 F 三項全驗 | XS-S |
| **1** | feature flag `Workflow:UseFrameworkKickoffMidInterrupt` + `WorkflowSettings` / `WorkflowSettingsResolver` 擴充 | XS |
| **2** | KickoffState 加 `MidInterruptRequestPending` flag + `MidInterruptResponse` 欄位（HITL response payload）| XS |
| **3** | `KickoffWorkflowFactory` 拓撲擴充：加 `MidInterruptCheckExecutor` node + `MidInterruptRequestPort` + 改 routing edge（每個 Petra Round 結束後檢查 mid-interrupt flag）| M |
| **4** | `FrameworkHitlBridge` service 新建（HITL request → BossInteraction + Christ response → workflow resume + SendResponseAsync）| L |
| **5** | `FrameworkKickoffRouter.RunWorkflowAsync` 改 — 從「watch 到 WorkflowOutputEvent 結束」變「watch RequestInfoEvent 時 yield + 保留 checkpoint，後續 Christ 回應觸發 ResumeAsync」| M |
| **6** | `TaskGroupService.ProcessBossResponseAsync` 加 `framework_kickoff_mid_interrupt` case → 呼叫 FrameworkHitlBridge | XS |
| **7** | Dashboard 流程追蹤頁加「中途介入 Kickoff」按鈕 + Bot Internal API（沿用 Stage 47 BotInternalController pattern）+ Dashboard SystemSettings 加第三 toggle | M |
| **8** | Mock 場景擴充（4 個 `framework_kickoff_mid_interrupt_*`）+ Forge 自驗 6 場景 + 結案 | M |

> **不寫工時估算**（workflow_aria.md 第三節 A 第 4 條，2026-05-02 校正）— 各子項規模見上表 XS/S/M/L 範圍描述。

---

## 子項 0：Spike 第一步 — 3 項驗證

### 驗證項目（議題 F 三項全驗）

| # | 驗證題 | 驗證方法 | 影響 |
|---|---|---|---|
| **F1** | framework `RequestPort` C# 1.3.0 canonical sample 是否完整 | 讀 `Microsoft.Agents.AI.Workflows` 1.3.0 xml doc + Microsoft Learn `agent-framework/workflows/human-in-the-loop` C# 段（`HumanInTheLoopBasic` sample）+ GitHub `microsoft/agent-framework` 對應 `dotnet/samples/03-workflows/HumanInTheLoop/` | **影響子項 3 RequestPort 設計**：API 完整 → 走原生 `RequestPort.Create<TReq, TResp>`；不完整 → 評估替代（Custom Executor + 自訂事件機制 fallback）|
| **F2** | `ICheckpointStore<JsonElement>` 對 pending requests 序列化是否可用 | Stage 49/50 已驗 Workflow state 序列化，但 framework HITL pending requests 是否走同個 `ICheckpointStore` 還是另一個 store？讀 docs 加 GitHub sample 驗證 | **影響子項 5 Router 設計**：可用 → 直接複用 KickoffCheckpointStore；不可用 → 自寫第二個 store 或評估替代 pause-resume 機制 |
| **F3** | 跨 HTTP scope workflow lifecycle pattern | Stage 49/50 router 內 `await using var scope = serviceProvider.CreateAsyncScope()` 同 scope build + run + yield + dispose；Stage 51 需 Bridge service「跨 HTTP request 持有 latest checkpoint」+ 新 scope `ResumeAsync`。讀 GitHub `HumanInTheLoopBasic` sample 看跨 scope 模式是否需要特殊處理（singleton workflow run 物件 vs build new from checkpoint）| **影響子項 4/5 設計**：能 build new from checkpoint → 簡單（Bridge 不持有 run 物件，每次重新 build + Resume）；必須持有同 run 物件跨 scope → 複雜（Bridge 需 cache run instance + 處理 lifetime） |

### Spike 結案產出

- **路線拍板紀錄**寫進 Forge Plan Mode plan 檔最前段
- **3 項驗證證據**（NuGet 文件引用 / GitHub sample 引用 / 必要時建小 spike 程式片段）
- **設計風險升級或降級**：依 spike 結果調整 R1-R6 風險評估

### Spike 階段失敗條件

若以下任一發生 → 暫停 Stage 51 + 回報 Christ 評估：
- F1 .NET 1.3.0 RequestPort C# sample 不完整或 prerelease（Anthropic provider 同類風險再現）
- F2 ICheckpointStore 對 pending requests 不支援（Python only 功能）
- F3 跨 scope lifecycle 需要 fundamental 重寫 router 模式（破壞 Stage 49/50 既有 pattern）

---

## 子項 1：feature flag 擴充

### 實作項目

**位置**：`src/AiTeam.Bot/Configuration/WorkflowSettings.cs` + `WorkflowSettingsResolver.cs`

**WorkflowSettings 新增屬性**：
- `bool UseFrameworkKickoffMidInterrupt { get; set; } = false;`

**WorkflowSettingsResolver 新增 method**：
- `Task<bool> GetUseFrameworkKickoffMidInterruptAsync(CancellationToken ct = default)` — 對齊既有 `GetUseFrameworkKickoffAsync` pattern

**AppSettings key**：`Workflow:UseFrameworkKickoffMidInterrupt`，預設 `false`

**雙 flag 連動規則**：本 flag 只在 `UseFrameworkKickoff = true` 時有意義（試點是 Stage 50 framework Kickoff path 的擴充，legacy Kickoff path 不適用）。Dashboard UI 上顯示 disabled 狀態當 UseFrameworkKickoff = false。

---

## 子項 2：KickoffState 擴充 HITL flag

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Kickoff/KickoffState.cs`（既有檔案）

**新增欄位**：
- `MidInterruptRequestPending` (bool) — 表示 workflow 已 emit RequestInfoEvent 等待 Christ 回應；預設 false
- `MidInterruptResponse` (string?) — Christ 回應內容（套用修改的指引文字，cancel 則為 null）；預設 null
- `MidInterruptTriggered` (bool) — 由 Bot Internal API 設置的「中途介入 trigger」flag，下個 superstep 邊界 MidInterruptCheckExecutor 看到後 emit RequestInfoEvent；預設 false

**設計理由**：
- 三個 flag 全在 framework state 內 → 隨 KickoffCheckpointStore 序列化進 task_groups.KickoffFrameworkStateJson（不需新 DB 欄位）
- 對齊 Stage 50 KickoffState 既有 13 欄位 pattern
- HITL response 透過 framework state 傳遞，而非 Claude Code session（C2 拍板對齊）

---

## 子項 3：KickoffWorkflowFactory 拓撲擴充

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Kickoff/KickoffWorkflowFactory.cs` + 新建 `Executors/MidInterruptCheckExecutor.cs`

**MidInterruptCheckExecutor 設計**：
- 接 `KickoffPetraVerdict`（Petra Round 結束 output）
- 檢查 framework state 內 `MidInterruptTriggered` flag
- 若 true → emit RequestInfoEvent（透過 RequestPort）+ workflow yield 等待
- 若 false → pass-through verdict 給下游 routing（既有 AddSwitch loop back / consensus / max_iter / escalate）
- HITL response 透過 framework state `MidInterruptResponse` 讀取，套用到下一輪 Petra prompt

**Workflow 拓撲擴充（依 spike F1 結論調整）**：

```
（Stage 50 既有）
start → fan-out → 4 Agent → fan-in → Aggregator → Petra
                                                    ↓
                                              （Stage 51 新增）
                                          MidInterruptCheckExecutor
                                                    ├ 無 trigger → AddSwitch（Stage 50 既有 routing）
                                                    └ 有 trigger → MidInterruptRequestPort
                                                                       ↓
                                                                   yield + Checkpoint
                                                                       ↓ (Christ 回應後 SendResponseAsync)
                                                                   讀 MidInterruptResponse → 套用到下一輪 Petra prompt
                                                                       ↓
                                                                   AddSwitch（loop back round+1）
```

**設計約束**：
- MidInterruptCheckExecutor 必須 `partial class` + `[SendsMessage(typeof(...))]` attribute（對齊 Stage 50 踩坑紀錄 #10 三件套紀律）
- 觸發條件透過 framework state flag 而非 ctor parameter（factory 模式下 Executor 不應持有運行期狀態）
- 若 spike F1 揭露 RequestPort 不可用 → fallback 到 Custom Executor + 自訂事件機制（Forge Plan Mode 拍板）

---

## 子項 4：`FrameworkHitlBridge` service

### 實作項目

**位置**：`src/AiTeam.Bot/Orchestration/Hitl/FrameworkHitlBridge.cs`（新檔，新資料夾 `Orchestration/Hitl/`）

**核心 method**：

| Method | 職責 |
|---|---|
| `RequestMidInterruptAsync(TaskGroup group, KickoffMidInterruptRequest request, CancellationToken ct)` | RequestInfoEvent → 開 BossInteraction：呼叫 `InteractionService.CreateInteractionAsync("framework_kickoff_mid_interrupt", ...)` + Discord embed + 3 buttons（套用修改 / 取消介入）|
| `HandleMidInterruptResponseAsync(TaskGroup group, string action, string? content, CancellationToken ct)` | Christ 回應後 → 載 KickoffCheckpointStore latest checkpoint + ResumeAsync workflow + SendResponseAsync(回應 payload) → workflow 從 yield 點繼續跑到結束 |
| `TriggerMidInterruptFlagAsync(Guid groupId, CancellationToken ct)` | Dashboard「中途介入 Kickoff」按鈕觸發 — 寫 framework state `MidInterruptTriggered = true`（下個 superstep 邊界 MidInterruptCheckExecutor 會看到）|

**DI 註冊**：對齊 Stage 49/50 慣例 Singleton（ctor 注入 IServiceProvider / IServiceScopeFactory / KickoffWorkflowFactory / KickoffCheckpointStore / InteractionService / DiscordSocketClient / IOptions / ILogger，scoped 服務 method 內 CreateAsyncScope 動態取）

**為什麼是新 service 而非擴充既有 InteractionService**：
- InteractionService 既有 method 已在 Stage 28a/28b 廣泛被 caller 使用，新加 method 增加既有 service 表面積
- FrameworkHitlBridge 專責 framework HITL ↔ BossInteraction 的橋接邏輯，未來 Stage 54 收尾真正切 HITL 時可獨立替換
- 對齊「3 次再抽象」原則（Stage 51 是第 1 次，Stage 54+ 真實 wire 時是第 2-3 次）

---

## 子項 5：FrameworkKickoffRouter HITL lifecycle 改寫

### 實作項目

**位置**：`src/AiTeam.Bot/Orchestration/Meeting/FrameworkKickoffRouter.cs`（既有檔）

**`RunWorkflowAsync` lifecycle 改寫**（依 spike F3 結論調整）：

| 階段 | Stage 50 行為 | Stage 51 行為 |
|---|---|---|
| Workflow 啟動 | `InProcessExecution.RunStreamingAsync(workflow, initialState, mgr, sessionId, ct)` | 同 |
| Event watch loop | `await foreach (var ev in run.WatchStreamAsync())` 收 `WorkflowOutputEvent` 結束 | 加 `RequestInfoEvent` 處理：收到時 → break loop（保留 checkpoint） |
| Yield 行為 | 不 yield（同步跑完）| Break loop 時呼叫 `FrameworkHitlBridge.RequestMidInterruptAsync` 開 BossInteraction，router HandleKickoffMeetingAsync 直接 return（非 throw error）|
| Resume 觸發 | N/A | 由 `FrameworkHitlBridge.HandleMidInterruptResponseAsync` 在 Christ 回應後新 scope 內 `ResumeAsync(checkpointId, ct)` + `run.SendResponseAsync(response)`，重新 watch loop 跑到 WorkflowOutputEvent |
| Crash Recovery | 啟動掃 KickoffFrameworkStateJson != null → 清 marker | 加判斷：若 state 含 `MidInterruptRequestPending = true` → **不清 marker 不 resume**（等 Christ 回應觸發 ResumeAsync），對應「等待人類回應」狀態（新增） |

**`RecoverStuckFrameworkKickoffsAsync` 篩選邏輯擴充**：

```
.Where(g => g.KickoffFrameworkStateJson != null && !g.IsPaused)
// Stage 51 新增：等待人類回應的 group 不算 stuck，由 BossInteraction 觸發 resume
// 透過讀取 KickoffFrameworkStateJson 內的 MidInterruptRequestPending flag 判斷
// （或對應 BossInteraction 表查 framework_kickoff_mid_interrupt 未 responded 的 group）
```

**設計選項（Forge Plan Mode 拍板）**：
- 選項 a：解 KickoffFrameworkStateJson 找 MidInterruptRequestPending flag（精準但解析成本高）
- 選項 b：查 BossInteraction 表找 framework_kickoff_mid_interrupt 未 responded（外部判斷簡單）
- 選項 c：在 task_groups 加 nullable bool `KickoffWaitingHumanResponse` 欄位（最簡單但加 schema）

---

## 子項 6：TaskGroupService.ProcessBossResponseAsync 加 case

### 實作項目

**位置**：`src/AiTeam.Bot/Orchestration/TaskGroupService.cs`（既有檔，加 case）

**新增 case**：

```
case "framework_kickoff_mid_interrupt":
    await frameworkHitlBridge.HandleMidInterruptResponseAsync(group, action, content, ct);
    break;
```

**為什麼路由到 Bridge 而非直接做**：對齊既有 `case "kickoff"` → `MeetingOrchestrationService.HandleKickoffConfirmedAsync` 解耦慣例。

---

## 子項 7：Dashboard UI + Bot Internal API

### Dashboard 流程追蹤頁「中途介入」按鈕

**位置**：`src/AiTeam.Dashboard/Components/Pages/Pipeline/PipelineView.razor`（或 Pipeline 流程追蹤對應頁面）

**設計**：
- 在 Kickoff 階段 task 卡片上，當 `task_groups.KickoffFrameworkStateJson != null`（framework path 跑期間）+ `UseFrameworkKickoffMidInterrupt = true` 時，顯示「✏️ 中途介入」按鈕
- 點擊 → 呼叫 `DashboardCeoCommandService` 透過 Bot Internal API trigger
- 不在 Kickoff 跑期間 → 按鈕 disabled 或 hidden

### Bot Internal API endpoint

**位置**：`src/AiTeam.Bot/Api/CeoCommandController.cs`（既有 controller，加 endpoint）

**新增 endpoint**：`POST /internal/kickoff/trigger-mid-interrupt`

**body**：`{"groupId": "..."}`

**邏輯**：呼叫 `FrameworkHitlBridge.TriggerMidInterruptFlagAsync(groupId, ct)`（寫 framework state `MidInterruptTriggered = true`）

### Dashboard SystemSettings UI 第三 toggle

**位置**：`src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor` + `.razor.cs`

既有「v4 漸進遷移控制」區塊（Stage 49/50 建立）下方追加第三 toggle：「使用 MS Agent Framework HITL（Kickoff 中途介入試點）」對應 `Workflow:UseFrameworkKickoffMidInterrupt`，警告文字寫明「⚠️ 試點功能，需先啟用 Stage 50 UseFrameworkKickoff，且 framework path 跑 Kickoff 時才有效」。

---

## 子項 8：Mock 場景 + Christ 線下驗收 + 結案

### Mock 場景擴充

**位置**：`src/AiTeam.Bot/Services/MockScenarioService.cs` + `MockClaudeCodeService.cs`

新增 4 個 `framework_kickoff_mid_interrupt_*` 系列場景：

| 場景 key | 行為 |
|---|---|
| `framework_kickoff_mid_interrupt_apply` | Round 1 結束後 Christ 介入「請特別考慮 X 場景」→ workflow resume Round 2 帶新指引給 4 Agent → consensus |
| `framework_kickoff_mid_interrupt_cancel` | Round 1 結束後 Christ 介入但取消（按「取消介入」）→ workflow resume Round 2 不帶新指引（行為與 Stage 50 normal flow 一致）|
| `framework_kickoff_mid_interrupt_crash_during_wait` | Round 1 結束後 emit RequestInfoEvent，simulate `docker compose restart aiteam-bot`，重啟後驗 BossInteraction 仍存 + Christ 回應後 workflow 從 checkpoint resume |
| `framework_kickoff_mid_interrupt_no_trigger` | Christ 不點「中途介入」按鈕 → workflow 跑完 Round 1-3 與 Stage 50 normal flow 一致（驗證試點不影響 default behavior）|

### Christ 線下驗收

見下方 ## 驗收情境 段。

---

## 驗收情境

> Stage 51 是 v4 漸進遷移第三步試點，**驗收必須含 lifecycle 跨 HTTP scope 驗證 + Crash Recovery 等待回應狀態驗證**。沿用 Stage 49/50 6 場景模式擴充。

### 場景 A：兩個 flag 任一為 false → Stage 50 行為不變

**怎麼觸發**：
1. push Stage 51 commit → CI/CD 部署
2. 三種子場景：
   - A.1：`UseFrameworkKickoff = false` + `UseFrameworkKickoffMidInterrupt = false`（legacy Kickoff）
   - A.2：`UseFrameworkKickoff = true` + `UseFrameworkKickoffMidInterrupt = false`（Stage 50 framework Kickoff，無中途介入）
   - A.3：`UseFrameworkKickoff = false` + `UseFrameworkKickoffMidInterrupt = true`（試點 flag 開但本層無效）
3. 各跑 `/mock new_feature_with_proposal` 或 `/mock framework_kickoff_consensus_round1`

**怎麼驗證**：
- ✅ A.1 → 走 legacy KickoffMeetingService（Stage 49 行為）
- ✅ A.2 → 走 framework path 但無 MidInterruptCheckExecutor 觸發（Stage 50 行為）
- ✅ A.3 → 走 legacy（試點 flag 不影響 legacy path）
- ✅ Bot log 沒有 `[Stage51]` 訊息
- ✅ Dashboard「中途介入」按鈕在 A.2 顯示但點擊時因 trigger flag 機制 — Forge 拍板：A.2 點擊應該無效還是可觸發？建議「可觸發但 workflow 內無 Check Executor → flag 寫入無人讀取，等同 no-op」

### 場景 B：兩 flag 都 true + Christ Discord「套用修改」→ workflow resume

**怎麼觸發**：
1. Dashboard SystemSettings 切 `UseFrameworkKickoff = true` + `UseFrameworkKickoffMidInterrupt = true`
2. 跑 `/mock framework_kickoff_mid_interrupt_apply`
3. Round 1 結束後 Petra 整理時，Dashboard 流程追蹤頁點「✏️ 中途介入」按鈕
4. Discord 出現 BossInteraction embed（type=`framework_kickoff_mid_interrupt`）
5. Christ 在 Discord 點「套用修改 ✏️」+ 輸入文字「請特別考慮 X 場景」

**怎麼驗證**：
- ✅ Bot log `[Stage51] MidInterruptRequestPort emit RequestInfoEvent`（router yield）
- ✅ DB BossInteraction 表新 row（type=`framework_kickoff_mid_interrupt`，pending）
- ✅ Discord embed 顯示 + 2 buttons
- ✅ Christ 回應後 → InteractionService.SyncDiscordResponseAsync 樂觀鎖通過
- ✅ TaskGroupService.ProcessBossResponseAsync 呼叫 FrameworkHitlBridge.HandleMidInterruptResponseAsync
- ✅ Bot log `[Stage51] Workflow ResumeAsync from checkpoint` + Round 2 prompt 含 Christ 修改指引
- ✅ workflow 跑到 WorkflowOutputEvent → KickoffMeetingLog + TaskPlan 寫入 DB

### 場景 C：兩 flag 都 true + Christ Dashboard「套用修改」→ workflow resume（樂觀鎖驗證）

**怎麼觸發**：
1. 同場景 B 步驟 1-3
2. Christ 在 Dashboard 操作中心點「套用修改 ✏️」+ 輸入文字
3. 觀察 Discord embed 是否標記為「Dashboard 已先回覆」

**怎麼驗證**：
- ✅ InteractionRespondService.RespondAsync 樂觀鎖通過
- ✅ InteractionProcessor 3 秒輪詢 → ProcessBossResponseAsync → HandleMidInterruptResponseAsync 觸發
- ✅ Discord 端 SendDiscordSyncMessageAsync 「📋 Christ 已在 Dashboard 回覆：套用修改 ✏️」
- ✅ workflow 從 checkpoint resume + Round 2 帶新指引

### 場景 D：等待回應期間 Bot 重啟 → Recovery 載 checkpoint + 等下次回應

**怎麼觸發**：
1. 場景 B 步驟 1-4（emit RequestInfoEvent + BossInteraction 開）
2. Christ **不回應**，simulate `docker compose restart aiteam-bot`（**Christ 授權的 ops 操作**）
3. 等容器重啟後 + Christ 在 Discord 回應「套用修改」

**怎麼驗證**：
- ✅ Bot 啟動 log `[Stage51] 發現 N 個 framework Kickoff 等待人類回應`（不算 stuck，不清 marker）
- ✅ DB `task_groups.KickoffFrameworkStateJson` 仍含 pending request
- ✅ DB BossInteraction 仍 pending
- ✅ Christ 重啟後回應 → workflow 從 checkpoint resume（不從 Round 1 重來）
- ✅ 最終 Petra 產出 TaskPlan 跟未重啟情境一致（HITL + Checkpointing 整合 production-grade 驗證）

### 場景 E：Christ 取消介入 → workflow resume 但不帶修改指引

**怎麼觸發**：
1. 跑 `/mock framework_kickoff_mid_interrupt_cancel`
2. Round 1 後 Dashboard 點「中途介入」+ Discord 回「取消介入」

**怎麼驗證**：
- ✅ MidInterruptResponse = null
- ✅ workflow resume Round 2 prompt **不含**修改指引（與 Stage 50 normal flow 一致）
- ✅ 最終 TaskPlan 跟 Stage 50 normal Round 2 consensus 結果一致

### 場景 F：試點不觸發 → workflow 跑完跟 Stage 50 normal flow 一致

**怎麼觸發**：
1. 兩 flag 都 true，跑 `/mock framework_kickoff_consensus_round1`（既有 Stage 50 場景）
2. Christ **不點**「中途介入」按鈕

**怎麼驗證**：
- ✅ MidInterruptCheckExecutor 每 round 都檢查但 flag = false → pass-through
- ✅ workflow 跑到 consensus 結束（與 Stage 50 場景 B 行為完全一致）
- ✅ Bot log 沒有 `[Stage51] MidInterruptRequestPort emit` 訊息
- ✅ DB BossInteraction 表沒有新 framework_kickoff_mid_interrupt row

---

## 風險點 / 注意事項

### 1. framework HITL `RequestPort` C# 1.3.0 sample 完整性（高）

**風險**：Microsoft Learn HITL 文件主要 Python sample，C# `HumanInTheLoopBasic` sample 是否含 Checkpointing 整合 + 跨 scope resume + Anthropic provider 支援都待 spike F1 驗證。

**緩解**：
- spike F1 為 Stage 51 第一步，不可行則 fallback Custom Executor + 自訂事件機制（功能等價但複雜度 +50%）
- feature flag 預設 false → 不啟用就 0 影響

### 2. Checkpointing + pending requests C# 整合（中-高）

**風險**：spike F2 驗證 `ICheckpointStore<JsonElement>` 是否能序列化 framework pending requests（vs Python sample 已驗）。Stage 49 case study 線性 Workflow 驗 ICheckpointStore 通用，Stage 50 fan-out 拓撲驗 superstep checkpoint，Stage 51 是 pause-resume + pending requests 序列化第三層驗證。

**緩解**：
- 若不可用 → 評估自寫第二個 store 專管 pending requests（F2 spike 拍板）
- Mock 場景 D 含 Bot 重啟驗證

### 3. 跨 HTTP scope workflow lifecycle（中-高）

**風險**：Stage 49/50 router 內單一 scope build + run + watch + dispose；Stage 51 必須跨 scope（router scope build → yield → Bridge new scope → ResumeAsync）。spike F3 驗證 `ResumeAsync(checkpointId, ct)` 是否支援「從 DB 載 checkpoint + new run instance」模式。

**緩解**：
- spike F3 拍板 lifecycle 模式
- 若必須持有同 run 物件跨 scope → Bridge service singleton 內 ConcurrentDictionary cache run 物件 + 處理 lifetime（複雜度 +30%）

### 4. 既有 BossInteraction 樂觀鎖機制不破壞（中）

**風險**：framework HITL response 透過 BossInteraction 雙通道，必須對齊既有先到先贏機制（discord vs dashboard）；FrameworkHitlBridge 內 `HandleMidInterruptResponseAsync` 必須冪等（同一 BossInteraction 不會被觸發兩次）。

**緩解**：
- 沿用 InteractionService.SyncDiscordResponseAsync + InteractionRespondService.RespondAsync 既有樂觀鎖（Stage 28a 機制）
- BossInteraction.Status 已從 pending → responded，FrameworkHitlBridge 收到 Status != "responded" 直接 early return（防 race condition）
- Mock 場景 C 含 Dashboard 樂觀鎖驗證

### 5. Crash Recovery「等待回應」狀態 vs「卡住」狀態區分（中）

**風險**：Stage 50 case study Crash Recovery 對 framework path 採「降級策略清 marker」，Stage 51 引入新狀態「等待人類回應」必須跟「卡住」區分。

**緩解**：
- 子項 5 列了 3 種判斷選項（解 KickoffFrameworkStateJson flag / 查 BossInteraction / 加新 DB 欄位）— Forge Plan Mode 拍板
- Mock 場景 D 含 Bot 重啟期間 BossInteraction 持久性 + 重啟後 resume 完整驗證

### 6. Anthropic provider prerelease 持續曝露（低-中，Stage 49/50 既有風險繼承）

**風險**：HITL 試點對 provider prerelease 風險不直接放大（Petra/Rosa/Demi/Cody/Quinn 全走 RunMeetingSessionAsync 包 Claude Code CLI，不直接走 framework Anthropic provider），但 spike F1 若揭露 RequestPort 對 Anthropic provider 整合有特殊要求 → 風險升級。

**緩解**：
- feature flag 預設 false 為主要安全網
- spike F1 第一步驗證

### 7. 不踩既有 BossInteraction 邊界（A3 試點精神）

**Stage 51 不動的 production code**：
- ❌ 既有 BossInteraction 10+ type 任何 type 行為（proposal / kickoff / design / merge_notify / intervention / devplan_escalate / split_task_proposal / 等）
- ❌ InteractionService 既有 method（CreateInteractionAsync 是 add-only，新 type 用既有 method 加一行）
- ❌ InteractionRespondService / InteractionProcessor 主流程
- ❌ KickoffMeetingService.ModifyTaskPlanAsync legacy「Workflow 結束後修改」流程
- ❌ Stage 49/50 既有 escalate 路徑（沿用 Stage 50 慣例 Workflow 結束後開 BossInteraction）

**Stage 51 動的 production code**：
- 動：`src/AiTeam.Bot/Workflows/Kickoff/KickoffState.cs`（加 3 flag）+ `KickoffWorkflowFactory.cs`（拓撲擴充）+ 新建 `MidInterruptCheckExecutor.cs` + 新建 `src/AiTeam.Bot/Orchestration/Hitl/FrameworkHitlBridge.cs` + `Orchestration/Meeting/FrameworkKickoffRouter.cs`（lifecycle 改寫）+ `Orchestration/TaskGroupService.cs`（加 case）+ `Configuration/WorkflowSettings.cs` + `WorkflowSettingsResolver.cs` + `Api/CeoCommandController.cs`（加 endpoint）+ `src/AiTeam.Dashboard/Components/Pages/Pipeline/PipelineView.razor`（加按鈕）+ `Components/Pages/Settings/SystemSettings.razor*`（加第三 toggle）+ `Services/DashboardCeoCommandService.cs`（加 client method）+ `Services/MockScenarioService.cs` + `Agents/MockClaudeCodeService.cs` + `src/Directory.Build.props`（Version bump）

---

## Model 建議

依 [`workflow_aria_model_effort.md`](../../C:/Users/darkl/.claude/projects/D--Source-Code-AI-Team/memory/workflow_aria_model_effort.md) 四維度：

| 維度 | 評估 |
|---|---|
| **任務複雜度** | 中-高 — HITL pattern 新引入 + Checkpointing pending requests 整合 + 跨 HTTP scope lifecycle |
| **改動範圍** | M-L — 跨 Bot/Dashboard 多檔 + 新建 1 service 1 Executor + 改既有 KickoffWorkflowFactory + Router lifecycle |
| **歷史包袱** | 中 — 不改既有 BossInteraction 樂觀鎖機制（A3 試點隔離），但要避免新 type 與既有 type 衝突 |
| **判斷品質要求** | 高 — HITL pattern 設計影響 Stage 54+ 真正切 BossInteraction 時的 know-how 基礎 |

**建議**：**Opus 1M + high**

理由：
1. **v4 漸進遷移高判斷品質要求**（HITL pattern 引入 + Checkpointing 第三層驗證）→ Opus 1M
2. **預估 context 350-550K**（混合型 Stage 第 3 個資料點，沿用 Stage 49 ×1.25 + Stage 50 ×1.09 平均 ×1.17 區間）
3. **可能拆 session**：
   - Session A：spike + 子項 1-5（feature flag + State + Workflow Factory 拓撲 + Bridge service + Router lifecycle）
   - Session B：子項 6-8（TaskGroupService case + Dashboard UI + Mock 場景 + 結案）

### Context 預估

依 7 項公式 + 混合型 Stage 校準（對齊 Stage 49 ×1.25 + Stage 50 ×1.09 兩資料點 mid 帶）：
- 開場 ~32K
- 工作 raw（新建 2-3 檔 + 動 8-10 既有檔 + 新 Executor + Workflow 拓撲擴充）~120-180K
- Grep / Bash 輸出 ~25-35K（讀 Stage 50 reference + grep BossInteraction caller + framework HITL docs WebFetch + dotnet build）
- 對話 turn 成本 ~50-80K（spike 第一步 3 項驗證 + Plan Mode + 閘門一 + 結案）
- Edit 反覆對齊 ~30-60K（拓撲擴充 + lifecycle 改寫易反覆對齊）
- Mock 驗收（4 場景 + 3 子場景 A.1/A.2/A.3）~50-100K
- follow-up 修正 ~30-100K（HITL 首次引入風險中-高，預期 1-3 個 follow-up）
- 結案文件寫作 ~10-20K
- **總計約 ~350-580K**（Opus 1M 內 35-58% 負擔，舒適區）

→ 拆 session 建議：若 Forge spike + 子項 1-5 結束時 context > 200K，主動跟 Christ 提「拆下一 session 進子項 6+」。

---

## 與 v4 路線的關係

**Stage 51 是 v4 漸進遷移 6 Stage 的第三步**：

```
Stage 47 ✅ ops 補丁（FF 四十七，v3.34.0，2026-05-02）
Stage 48 ✅ spike Phase A（FF 四十九，採用結論，2026-05-02）
Stage 49 ✅ Cody-Vera-Petra Appeal loop 遷移（v3.35.0，2026-05-02）
Stage 50 ✅ Kickoff Meeting → Group Chat orchestration（v3.36.0，2026-05-02）
   ↓
Stage 51（本 Stage）：framework HITL pattern 試點 — Kickoff 中途介入（v3.37.0）
   ↓
Stage 52：Design Meeting B3 路線（主迴圈遷移）+ WorkflowEngine 整體 hardcoded pipeline → Workflow Builder（最大遷移點）
   ↓
Stage 53：Crash Recovery 全面切換到 framework Checkpointing
   ↓
Stage 54：收尾 + token middleware + production 切換 + 老 framework code 刪除 + framework Executor 從 service 切回直連 + 真正切既有 BossInteraction 到 framework HITL（Stage 51 試點 know-how 全面 wire）
   ↓
Stage 55+（評估）：FF 三十六 Phase B 動態流程架構（依 Stage 52 後評估結果）
```

> 註：Stage 51 完成後 v4 漸進遷移進度 **3/6**。若 Stage 51 揭露 framework HITL 重大議題 → 暫停 Stage 52+ + 評估是否需要 spike Phase A.5（補做 HITL 特定模組驗證）。

**Stage 51 結案後對 Stage 52 的影響**：
- 若 Stage 51 順利 → Stage 52 Design Meeting B3 路線可用 HITL pattern 處理 needs_adjustment 分支（Christ 中途調整）
- 若 Stage 51 揭露 framework HITL 限制 → Stage 52 只走 fan-out/fan-in pattern（不引入 HITL），HITL 真實 wire 留 Stage 54 評估

**Stage 51 對 Stage 54 的鋪路**：
- FrameworkHitlBridge service 抽象設計（議題 E D2 拍板）給 Stage 54 真正切既有 BossInteraction 到 framework HITL 時複用
- HITL + Checkpointing 跨 scope lifecycle pattern 給 Stage 54 全面 wire 提供 know-how

---

## 實作紀錄

### 子項完成度對照（對齊 Aria 計劃書 8 子項 + spike）

| # | 子項 | Session | 狀態 | 主要產物 |
|---|---|---|---|---|
| 0 | Spike F1/F2/F3 | A | ✅ | 全綠（NuGet 1.3.0 stable + Microsoft Learn checkpoints + GitHub HumanInTheLoopBasic 引證），走 framework 原生 RequestPort + ResumeStreamingAsync 路線（非 fallback） |
| 1 | feature flag `Workflow:UseFrameworkKickoffMidInterrupt` | A | ✅ | `WorkflowSettings` + `WorkflowSettingsResolver.GetUseFrameworkKickoffMidInterruptAsync` |
| 2 | KickoffState 加 3 HITL 欄位 + KickoffTaskId + 2 record | A | ✅ | `MidInterruptRequest` / `MidInterruptResponseData` record + state 內 `MidInterruptRequestPending` / `MidInterruptResponse` / `KickoffTaskId`（trigger flag 改 in-memory store） |
| 3 | MidInterruptCheckExecutor + Workflow 拓撲 + prompt 注入 | A | ✅ | 新 `MidInterruptCheckExecutor`（雙 [MessageHandler] partial class）+ Factory 加 RequestPort + edge × 3 + `KickoffPrompts.AppendMidInterruptHint` + 4 Agent + Petra prompt builder 接 hint 參數 |
| 4 | FrameworkHitlBridge service | A | ✅ | 三 method（TriggerMidInterruptFlagAsync / RequestMidInterruptInteractionAsync / HandleMidInterruptResponseAsync）+ in-memory `KickoffMidInterruptTriggerStore` + 冪等性 fail-open scan helper |
| 5 | FrameworkKickoffRouter lifecycle 改寫 | A | ✅ | RunWorkflowAsync 回傳 `(loopResult, yielded)` tuple + finally 條件式 cleanup + 新 public `FinishKickoffAsync` + Recovery 加判斷 `MidInterruptRequestPending=true` 不算 stuck |
| 6 | TaskGroupService case + InteractionProcessor label + CommandHandler button + modal | B | ✅ | case `framework_kickoff_mid_interrupt` 路由 + ButtonCallbackRouter `HandleFrameworkKickoffMidInterruptAsync`（apply 走 `RegisterKickoffMidInterruptApply` modal pattern / cancel DeferAsync + Task.Run）+ CommandHandler text-input handler + label switch 2 entries |
| 7 | Bot Internal API + Dashboard UI + SystemSettings toggle | B | ✅ | `POST /internal/kickoff/trigger-mid-interrupt` + `DashboardCeoCommandService.TriggerKickoffMidInterruptAsync` + PipelineView「✏️ 中途介入」按鈕（feature flag + Kickoff running 雙條件）+ SystemSettings 第三 toggle（Disabled 條件 `!_useFrameworkKickoff`）|
| 8 | Mock 4 場景 + 自驗 + 結案 | B | ✅ | `framework_kickoff_mid_interrupt_apply`/`cancel`/`crash_during_wait`/`no_trigger` 4 scenario + MockClaudeCodeService Petra Round 1 needs_discussion → Round 2 consensus + group 建立後立即 `triggerStore.Set` 預設 trigger（apply/cancel/crash 三變體） |

### Session A 結案

- 5 子項全綠（feature flag / KickoffState / MidInterruptCheckExecutor + 4 檔 / FrameworkHitlBridge / Router lifecycle）
- Aria 二次檢查 12 點全部處理（含必修 #1 verdict.Round = state.Round 對齊 BroadcastNextRoundAsync 推進模式）
- 設計變更：trigger flag 從 KickoffState 改用 `KickoffMidInterruptTriggerStore` Singleton（避免 framework JSON mutation brittle 解析；HITL 等待階段 trigger 已被消耗，重啟讀 `MidInterruptRequestPending=true` 即正確識別「等待人類回應」）
- 14 檔變更，890 行新增，commit `67a9b0a`，push 後 CI/CD 部署
- `dotnet build AiTeam.slnx` ✅ 0 Error / `dotnet test` ✅ 127 Passed

### Session B 結案

- 3 子項全綠（TaskGroupService case + Discord/Modal pattern / Bot Internal API + Dashboard UI / Mock 場景擴充）
- 7 個檔案改動：TaskGroupService.cs / InteractionProcessor.cs / ButtonCallbackRouter.cs / CommandHandler.cs / PendingConfirmationStore.cs / InternalController.cs / DashboardCeoCommandService.cs / SystemSettings.razor + .cs / PipelineView.razor + .cs / MockScenarioService.cs / MockClaudeCodeService.cs / Directory.Build.props
- Mock 4 場景透過 `triggerStore.Set(group.Id)` 在 group 建立後立即預設 trigger flag（避免 Round 1 race condition）— `no_trigger` baseline 不設，驗證試點 flag 不影響 default behavior
- Version 3.37.0 bump
- `dotnet test` ✅ 127 Passed

### 驗收結果

**Forge 自驗 + Christ 線下驗 6 場景全綠（2026-05-02）**

| 場景 | 驗證方式 | 結果 | 關鍵 log / 證據 |
|---|---|---|---|
| A.1 flag 全 false | Forge 靜態（程式碼審視）| ✅ | `UseFrameworkKickoff = false` → legacy KickoffMeetingService 接管，framework topology 不啟用 |
| A.2 framework Kickoff true / mid_interrupt false | Forge 靜態 | ✅ | MidInterruptCheckExecutor 跑但 `triggerStore.TryConsume` 永遠 false → pass-through |
| A.3 framework Kickoff false / mid_interrupt true | Forge 靜態 | ✅ | legacy path 接管，framework topology + trigger store 不啟用 |
| F no_trigger | Forge 靜態（Mock 邏輯審視）| ✅ | Mock 不預設 trigger + Petra Round 1 直接 consensus，無 RequestInfoEvent emit |
| E cancel | Forge 靜態（程式碼路徑） | ✅ | ButtonCallbackRouter cancel 分支 → Bridge.HandleMidInterruptResponseAsync(midinterrupt_cancel, null) → ResumeStreamingAsync → MidInterruptResponseData(Apply=false, Content=null) → Round 2 consensus |
| **B apply via Discord** | Christ 線下實跑 | ✅ | Bot log 完整鏈路：`MidInterruptCheck emit MidInterruptRequest（Round=1）` → `RequestMidInterruptInteractionAsync: BossInteraction 已開（requestId=88b8c908...）` → `RunWorkflowAsync: yield for HITL` → `finally: yieldedForHitl=true，保留 workspace + marker` → `收到中途介入指引` → `ResumeStreamingAsync 啟動（apply=True，requestId=88b8c908...）` → `SendResponseAsync 完成` → `MidInterruptCheck 收到 Christ response（apply=True）` → `WorkflowOutputEvent（decision=consensus，rounds=2）` → `FinishKickoffAsync 完成` |
| **C apply via Dashboard 樂觀鎖** | Christ 線下實跑 | ✅ | `InteractionProcessor：處理 framework_kickoff_mid_interrupt+midinterrupt_apply（Id=eaee9102...）` ← Dashboard 樂觀鎖通過 + 3 秒輪詢路由觸發；Discord `#victoria-ceo` 收到「📋 Christ 已在 Dashboard 回覆：套用修改 ✏️」同步訊息 |
| **D crash during wait** | Christ 線下實跑（Forge 執行 `docker restart aiteam-bot`）| ✅ | yield 後 `docker restart aiteam-bot` → 重啟 log `[FrameworkKickoffRouter] 啟動：發現 1 個 stuck framework kickoff` → `[Stage51] Recovery Group=df65b28c...：等待人類回應（MidInterruptRequestPending=true），保留 marker 等 BossInteraction 觸發 resume` → Discord 點按鈕 + 輸入文字 → `ResumeStreamingAsync 啟動（requestId=0daeccaa...）` 跨 process restart 仍找到 latest checkpoint → `SendResponseAsync 完成` → `WorkflowOutputEvent（decision=consensus，rounds=2）` → `FinishKickoffAsync 完成` |

**Aria spike 三項關注點實證通過（場景 D 是最強驗證）**

| Aria 關注點 | spike 階段結論 | 場景 D 實證 |
|---|---|---|
| #5 ResumeStreamingAsync re-emit RequestInfoEvent 是否真實工作 | F2 文件依據（`RestoreCheckpointAsync re-emits any pending external request events`）| ✅ 跨 process restart 仍真實工作 |
| #6 RequestId 跨 rehydrate 是否 stable | F1/F2 隱含假設（純 Mock 未驗）| ✅ requestId `0daeccaa72714604812add3427ba4d9d` 在 yield emit + Bridge resume + Recovery 跨重啟全程 stable |
| #7 連續介入 checkpoint 寫入是否自動 | spike implies 自動 | ✅ yield 時 framework 自動把 pending request 寫進 KickoffFrameworkStateJson；Recovery 載 latest checkpoint 後 framework 自動 re-emit RequestInfoEvent |

### 驗收後修正

**0 follow-up commits**（B/C/D 全綠首跑通過 + Aria 三項關注點實證 + 0 程式碼修正）。

驗收期間順帶清理 12 個 stale TaskGroups（10 running + 2 needs_intervention，UI 整潔）— 純 DB UPDATE，不涉及 production code。

### 關鍵設計決策（為什麼這樣選）

| # | 決策 | 為什麼 | 代價 / 替代方案 |
|---|---|---|---|
| 1 | trigger flag 改用 in-memory `KickoffMidInterruptTriggerStore`（vs 寫 framework state JSON） | 框架 checkpoint JsonElement 內部結構由 framework 序列化，直接 mutation 需理解 ScopeKey + PortableValue 等內部型別，brittle 且 framework 版本變動易破壞 | 失代價：Bot 重啟後「待按按鈕」狀態丟失（Christ 重新點）— 可接受，因按下到下個 superstep 邊界本就時間敏感（HITL 等待階段 trigger 已被消耗，不影響 Crash Recovery） |
| 2 | Bridge 不持有 run 物件跨 HTTP scope（rehydrate via `ResumeStreamingAsync`） | spike F3 結論：Microsoft Learn 文件明示 `InProcessExecution.ResumeStreamingAsync(workflow, savedCheckpoint, manager)` 是 canonical 跨 scope 模式 — 每次 new run from checkpoint，framework 自動 re-emit pending RequestInfoEvent | 替代方案：Bridge 持有 ConcurrentDictionary&lt;Guid, StreamingRun&gt; 跨 scope cache run 物件 — 複雜度 +30%，需處理 lifetime + race condition |
| 3 | MidInterruptCheckExecutor 雙 [MessageHandler] partial class（vs Executor&lt;TIn, TOut&gt; generic） | 兩個 input message type（KickoffPetraVerdict / MidInterruptResponseData）+ HandleVerdictAsync 兩個出口型別（KickoffPetraVerdict / MidInterruptRequest），generic Executor 只支援單一 input + output 表達不了 | 對齊 Stage 50 踩坑 #10 三件套紀律（partial class + [SendsMessage] + 顯式 send） |
| 4 | Bridge 與 Router 透過 service locator 解循環依賴（`serviceProvider.GetRequiredService<T>()`） | Bridge 需要 router.FinishKickoffAsync，Router 需要 bridge.RequestMidInterruptInteractionAsync — ctor 互注入會 DI 循環錯誤 | 對齊 router 既有 service locator 模式（line 431 `serviceProvider.GetRequiredService<CommandHandler>()`） |
| 5 | finally cleanup 條件式（`yieldedForHitl` flag） | HITL yield 後 router HandleKickoffMeetingAsync 提早 return，但 finally 仍跑；workspace 必須保留給 Bridge resume，marker 必須保留給 Recovery 識別「等待人類回應」 | 替代：把 cleanup 完全搬到 FinishKickoffAsync — 但 sync path（無 HITL）也要 cleanup 等於兩條路徑都要呼叫，重複邏輯 |
| 6 | KickoffTaskId 寫進 KickoffState（隨 framework state 序列化） | Bridge resume 完成後需要 mark task done，原 router scope 早已 dispose，無法傳遞；framework state 是天然跨 scope 持久化載體 | 替代：DB 加新欄位 `KickoffTaskId`（議題 #2 拍板「不加 schema」否決） |
| 7 | FinishKickoffAsync 透過 `ScanForGuidProperty` / `ScanForStringProperty` 寬鬆 scan framework state JSON 取 KickoffTaskId / WorkingDir | framework state JSON 結構含 ScopeKey 包裝，固定 path 取值脆弱（framework 版本變動會破壞）；scan 整個 tree 找第一個匹配屬性名穩健且程式碼簡單 | fail-open：解析失敗時 KickoffTaskId = Guid.Empty / WorkingDir = ""，FinishKickoffAsync 跳過 confirmation embed + log warning，不影響資料一致性 |
| 8 | Cancel 拍板「丟棄所有累積指引回到正常對話」（vs 保留之前 Apply 指引） | 對 Christ 來說「介入指引」邊界較直觀（每次介入是獨立 trigger-response cycle，按 cancel 直觀理解為「全部撤銷」） | Aria 二次檢查 #12 + #4 拍板，純 prompt 行為差異 |

### 踩坑紀錄彙整

| # | 踩坑 | 原因 | 解 |
|---|---|---|---|
| 1 | Bridge ctor 加新依賴 → 循環依賴 | Bridge 需要 KickoffWorkflowFactory + KickoffCheckpointStore + InteractionService + ... 若再 ctor 加 router → DI 循環 | service locator 模式 lazy 取 router |
| 2 | DashboardCeoCommandService 大括號錯位 | Edit 工具加新 method 時 `}` 放在 class 外 → orphan method 編譯錯誤 | 重新 Edit 修正大括號 |
| 3 | ButtonCallbackRouter customId prefix 順序 | `framework_kickoff_mid_interrupt_*` 與 `kickoff_*` 都包含 "kickoff_" — 但 customId 起頭 `framework_`，不會被 `StartsWith("kickoff_")` 誤判（仍依紀律放在 kickoff_ 之前 check） | 顯式註解 + 放在 kickoff_ 檢查之前 |
| 4 | MockClaudeCodeService Petra round prompt 識別 | KickoffPrompts.BuildPetraRoundPrompt 含 `## 第 N 輪各角色意見` 字樣，Mock 用此判 round | 沿用 Stage 50 既有識別模式擴充 |
| 5 | RequestPort.Create&lt;TReq, TResp&gt; 在 Workflow 拓撲的接線 | `RequestPort` 透過隱式轉換成 `ExecutorBinding`（`op_Implicit(RequestPort)`）可直接出現在 AddEdge 任一端 | 兩條 AddEdge：`AddEdge(midCheck, midPort)` 出 + `AddEdge(midPort, midCheck)` 入 — type 過濾自然分流 |
| 6 | KickoffState.MidInterruptTriggered 的 trigger 流向選擇 | 原計劃寫 framework state 內 mutate brittle；in-memory store 簡單但 Bot 重啟 lose | 拍板 in-memory（trigger lifecycle 短暫，重啟丟失可接受），KickoffState 移除此欄位避免雙寫 |
| 7 | OnInitializedAsync vs OnParametersSetAsync 在 PipelineView | `_useFrameworkKickoffMidInterrupt` flag 載入要早於 step rendering — `OnInitializedAsync` 一次性，`OnParametersSetAsync` 每次 Group 變更觸發 | feature flag 用 OnInitializedAsync（不需重複載入），steps 用 OnParametersSetAsync（隨 Group 變化） |
| 8 | MockScenarioCard.razor 不需擴充 | Stage 49/50 framework_appeal_loop_* / framework_kickoff_* 場景皆未加入 Dashboard MudSelect — 用 /mock CLI 觸發，沿用慣例 | Stage 51 新 4 場景同樣不加 Dashboard MudSelect，僅 /mock CLI 可用（保持 Card UI 簡潔） |

### Aria 校準錨候選（Aria 結案第二段填）

> 預估 ×1.0-1.3（混合型 Stage，spike 第一步 + production 整合 + HITL pattern 引入）— 對齊 Stage 49 ×1.25 + Stage 50 ×1.09 兩資料點 mid 帶。Aria 結案第二段補實際倍率。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-02 | 初版規劃書建立（Aria）—— v4 漸進遷移第三步 Stage：framework HITL pattern 試點 — Kickoff Workflow 中途介入（A3 路線 + B1 試點 + C2 Petra session 沿用 + D2 獨立 flag + E D2 抽 FrameworkHitlBridge service + F 三項 spike 全驗）|
| v1.2 | 2026-05-02 | Forge 結案第一段：Session A（5 子項）+ Session B（3 子項）全綠 + Forge 自驗 5 場景靜態驗證通過（A.1/A.2/A.3 + E + F）+ B/C/D 待 Christ 線下驗 + 8 條踩坑紀錄 + 8 個關鍵設計決策 + Aria 校準錨候選 |
| v2.0 | 2026-05-02 | 結案版（forge-end SOP）— 狀態 ✅ 已完成 + 6 場景全綠（A.1/A.2/A.3/E/F Forge 靜態 + B/C/D Christ 線下實跑）+ Aria spike 三項關注點實證通過（ResumeStreamingAsync re-emit / RequestId 跨重啟 stable / checkpoint 自動寫入）+ 0 follow-up commits — v4 漸進遷移第三步 framework HITL pattern 試點 production 跑通 |
