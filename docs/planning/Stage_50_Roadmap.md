# Stage 50：v4 漸進遷移第二步 — Kickoff Meeting 切 MS Agent Framework Group Chat + feature flag

> 對應 Future Feature：v4 漸進遷移 6 Stage 路線第二步（[Stage 48 spike 報告](../experiments/Spike_v1_MsAgentFramework.md) 節 7）— 不對應特定 active FF（v4 路線進入 Stage 工作模式，按 Stage 走不開新 FF）
> 對應版本：**v3.36.0**（v4 漸進遷移第二個產生版本變動的 Stage）
> 建立日期：2026-05-02
> 狀態：📋 **規劃中**（等 Christ 通過計劃書 → Aria 備 Forge prompt → Forge Plan Mode）
> 文件版本：v1.0

---

## 概述

**戰略背景**：[Stage 48 FF 四十九 spike](Stage_48_Roadmap.md) 結論 = 採用 MS Agent Framework，啟動 6 Stage 漸進遷移路線。**Stage 49 v3.35.0 完成 v4 首發**（Cody-Vera-Petra Appeal loop 切 framework Workflow Builder + feature flag + 0 follow-up + production fallback 防呆真實生效）。**Stage 50 是 v4 漸進遷移第二步** — 把 **Kickoff Meeting**（5 人多 Agent 會議：Petra/Rosa/Demi/Cody/Quinn）從手刻 `RunMeetingSession` 模式切到 framework **Group Chat orchestration**（或 fallback Workflow Builder fan-out/fan-in）。

**性質特殊**：本 Stage 是 **混合型**（spike 第一步驗證 + production 整合），有兩條可能路線：

- **路線 A1（首選，spike 驗證通過）**：framework `AgentWorkflowBuilder.CreateGroupChatBuilderWith` + custom `GroupChatManager` 實作「multi-speaker per round」（4 Agent 並行 + Petra 整理）
- **路線 A2（fallback，spike 驗證 A1 不可行）**：直接複用 Stage 49 `WorkflowBuilder` pattern — 4 Executor 並行 fan-out + Petra Aggregator fan-in + loop back（雖名為 Group Chat orchestration 但實作走 Workflow Builder）

**核心策略**：**並行雙系統 + 獨立 feature flag**（`Workflow:UseFrameworkKickoff` AppSettings key，預設 `false`，與 Stage 49 `Workflow:UseFrameworkAppealLoop` 完全獨立）。Christ 在 Dashboard 切 `true` 後新 path 接管，舊 `KickoffMeetingService` 路徑保留至 Stage 54 才砍。

**範圍邊界（B2 路線拍板）**：
- ✅ **遷移**：Kickoff Meeting 主流程
- ❌ **不遷**：Design Meeting 全部留 legacy（保留 Stage 51+ 走 B3 漸進處理）
- ❌ **不遷**：ModifyTaskPlan / Christ 修改流程（沿用 C2：Petra session_id = group.Id 走既有 Claude Code `--resume`）
- ❌ **不遷**：BossInteraction（Stage 51 動 Human-in-the-Loop）

**v4 路線第二步風險預警**：
- framework Group Chat custom manager 介面是否允許「multi-speaker per round」**spike 第一步必驗**（議題 A1/A2 路線決定）
- 5 個並行 Claude Code subprocess 的並行度（既有 `KickoffMeetingService` 已驗，但 framework 包一層後不確定）
- `ClaudeCodeAgentExecutor` 從 [Obsolete] 預留 → 首次真實 production 使用（Stage 49 留下的 wrapper 真實考驗）
- → feature flag 為主要安全網，**非緊急情況不啟用**，先 Mock 驗證再 production 切換

---

## 設計決策（Christ 2026-05-02 拍板）

### 主路線拍板（戰略級）

| 議題 | 拍板 | 替代方案 |
|---|---|---|
| **議題 A：Group Chat 模式對應** | **A4：spike 第一步驗 A1（Custom `GroupChatManager` 一次回多個 next speakers），可行 → A1；不可行 → A2（複用 Stage 49 Workflow Builder fan-out/fan-in pattern）** | A1 直接賭（風險高）/ A3 Round-robin（破壞並行設計，慢 4 倍 + Agent 互相 react 失去獨立視角）|
| **議題 B：遷移範圍** | **B2：只遷 Kickoff Meeting，Design Meeting 留 legacy（Stage 51+ 走 B3 處理）** | B1 兩個都遷（XL 工時爆炸 + Design needs_adjustment 分支 framework Group Chat 無對應內建支援）/ B3 兩個都遷但 Design 只遷會議主迴圈（L 工時，Stage 51 再做更穩）|
| **議題 C：Petra session 持久化** | **C2：沿用 Claude Code `--resume` 機制**（Petra session_id 仍 = group.Id，framework 只管 control flow，Modify 流程 0 變動）| C1 framework state 含 Petra ChatHistory snapshot（雙寫，破壞 Modify 流程設計） |
| **議題 D：feature flag 顆粒度** | **D2：獨立 `Workflow:UseFrameworkKickoff`**（與 Stage 49 `UseFrameworkAppealLoop` 完全獨立，rollback 顆粒度精細）| D1 單一 `UseFrameworkMeetings`（Kickoff + Design 一起切，不漸進）|
| **議題 E：spike 階段** | **三項全驗**（Forge Plan Mode 第一步 spike，沿用 Stage 49 風險點 #1 慣例 — 主動驗證高風險點）| 擇一驗（風險高，Stage 49 教訓：framework Workflows.Generators 套件分離 doc gap 是 Phase 3 才踩到的坑，前置 spike 階段全驗有價值） |

### Aria 拿捏（已決）

| # | 議題 | 決定 |
|---|---|---|
| 1 | `ClaudeCodeAgentExecutor` [Obsolete] 處理 | Stage 50 **移除 [Obsolete] 標記 + production 化使用**（5 個 Kickoff Agent 全用此 wrapper，Stage 49 預留實證生效） |
| 2 | DB schema 設計 | 沿用 Stage 49 pattern：`task_groups` 新增 `KickoffFrameworkStateJson` 欄位（**獨立**於 `FrameworkAppealStateJson`，避免 Appeal/Kickoff state 互相覆蓋）|
| 3 | Crash Recovery 雙系統隔離 | 沿用 Stage 49 R2 緩解 pattern：`MeetingOrchestrationService.RecoverStuckOrchestrationsAsync` 既有 `g.FrameworkAppealStateJson == null` 排除條件**追加** `g.KickoffFrameworkStateJson == null` 條件；新增 `FrameworkKickoffRouter.RecoverStuckFrameworkKickoffsAsync` 走獨立 recovery |
| 4 | 入口分流 | `MeetingOrchestrationService.RunKickoffMeetingAndWaitAsync` 開頭加 feature flag 分流；framework path 走新 `FrameworkKickoffRouter` |
| 5 | ModifyTaskPlan 流程 | 沿用既有 `KickoffMeetingService.ModifyTaskPlanAsync`（C2 拍板下 Petra session 仍可 resume），feature flag 開關**不影響此流程**（Modify 永遠走 legacy KickoffMeetingService） |
| 6 | Token 計費 | 沿用 Stage 49 機制：透過 `ClaudeCodeAgentExecutor` 內部 `tokenLog.LogCliUsageAsync` 記錄（既有 Stage 44 + Stage 47 動態化）|
| 7 | CLAUDE_*.md prompt | 不動（5 個 Agent 既有 prompt 全保留；framework 只接 `RunMeetingSession` 這層；Stage 49 Petra prompt schema hint 微調驗證實證可不動）|
| 8 | Petra session_id 設計 | 沿用既有：Petra = group.Id（Modify resume 用），Rosa/Demi/Cody/Quinn = 臨時 GUID（不需 resume）|
| 9 | Workflow input 設計 | 對齊 Stage 49：`KickoffState` 作為 first input message，Petra/Rosa/Demi/Cody/Quinn Executor 第 1 輪 `[MessageHandler]` 接 `KickoffState`，內部 `SaveAsync` 寫進 framework state |

### Stage 50 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **0** | **Spike 第一步：3 項驗證**（Forge Plan Mode 內含）— 議題 E 三項全驗 | XS |
| **1** | DB schema：`task_groups` 加 `KickoffFrameworkStateJson` + Migration | S |
| **2** | `KickoffState` production 版（對齊既有 5 Agent + Round + ProposalContent + lastPetraOutput 等）| S |
| **3** | `ClaudeCodeAgentExecutor` 移除 [Obsolete] + production 化使用（首次真實 wrapper 使用）| S |
| **4** | `KickoffWorkflowFactory`（依 spike 結論走 A1 Group Chat 或 A2 Workflow Builder fan-out/fan-in）| L |
| **5** | `KickoffCheckpointStore`（沿用 `AppealCheckpointStore` pattern + DB 同步）| M |
| **6** | feature flag `Workflow:UseFrameworkKickoff` + `WorkflowSettingsResolver` method 擴充 + `WorkflowSettings` class 加屬性 | XS |
| **7** | `FrameworkKickoffRouter`（單一 entry method：對應 `RunKickoffMeetingAndWaitAsync` Workflow 觸發 + 結果寫 DB + Discord 通知 + BossInteraction）| M |
| **8** | `MeetingOrchestrationService` 入口分流 + Crash Recovery 雙系統隔離 + Dashboard SystemSettings UI 擴充 | S |
| **9** | Mock 場景擴充（4-5 個 `framework_kickoff_*`）+ Forge 自驗 6 場景 + 結案 | M |

> **不寫工時估算**（workflow_aria.md 第三節 A 第 4 條，2026-05-02 校正）— 各子項規模見上表 XS/S/M/L 範圍描述。

---

## 子項 0：Spike 第一步 — 3 項驗證（Forge Plan Mode 內含）

### 驗證項目（議題 E 三項全驗）

| # | 驗證題 | 驗證方法 | 影響 |
|---|---|---|---|
| **E1** | framework custom `GroupChatManager` 介面是否允許「multi-speaker per round」？ | 讀 `Microsoft.Agents.AI.Workflows` 1.3.0 + Microsoft Learn `agent-framework/workflows/orchestrations/group-chat` 文件 + GitHub canonical sample（`dotnet/samples/03-workflows/orchestrations/`）+ 必要時建小 spike 程式驗證 `RoundRobinGroupChatManager` extension API | **議題 A1/A2 路線決定**：可行 → 子項 4 走 A1；不可行 → 子項 4 走 A2 fallback |
| **E2** | `ICheckpointStore<JsonElement>` 對 Group Chat orchestration 是否同樣可用？ | Stage 49 已驗 `WorkflowBuilder` 可用，但 Group Chat 是不同 builder API（`AgentWorkflowBuilder.CreateGroupChatBuilderWith`），需重驗：建小 spike 跑 `RoundRobinGroupChatManager` + `CheckpointManager.CreateJson` 看 framework 是否接受 | **子項 5 KickoffCheckpointStore 設計依據**：可用 → 直接複用 `AppealCheckpointStore` pattern；不可用 → 自寫 superstep hook 同步寫 DB |
| **E3** | 5 個並行 Claude Code subprocess 的並行度 / token 耗量是否被 framework 限制？ | 跑 1 個小 spike：建 5 個 `ClaudeCodeAgentExecutor` instance（不同 ExecutorId），透過 framework Concurrent edge 並行觸發，觀察 ① 5 個 subprocess 是否真同時跑（既有 `KickoffMeetingService` 用 `Task.WhenAll` 已驗）② token_logs 是否每個 Agent 都正確紀錄 ③ 是否有 framework 層級的 throttle / serialization | **規模驗證**：若 framework 強制序列化 → 退回 Stage 49 Workflow Builder + AddConcurrentEdge 模式（Stage 49 spike 提到的 ConcurrentAgents pattern）|

### Spike 結案產出

- **路線拍板紀錄**（A1 vs A2）寫進 Forge Plan Mode plan 檔最前段
- **3 項驗證證據**（NuGet 文件引用 / GitHub sample 引用 / spike 程式片段 + 跑 log 引用）寫進 plan 檔
- **設計風險升級或降級**：依 spike 結果調整 R1-R6 風險評估

### Spike 階段失敗條件

若以下任一發生 → 暫停 Stage 50 + 回報 Christ 評估：
- E1 不可行且 E3 也驗不過（A2 fallback 也不通）
- framework Group Chat builder API 在 1.3.0 stable 不存在或 prerelease（doc 滯後 vs 真實 SDK）
- ClaudeCodeAgentExecutor 經 production 化後發現 [Obsolete] 設計有 fundamental 錯（如 ctor 簽名不對、IServiceScopeFactory pattern 與 Group Chat 不相容）

---

## 子項 1：DB schema — `task_groups` 加 `KickoffFrameworkStateJson`

### 實作項目

**位置**：`src/AiTeam.Data/Entities.cs` `TaskGroup` class

**新增欄位**（單一 nullable JSON 欄位，獨立於 Stage 49 `FrameworkAppealStateJson`）：

- `KickoffFrameworkStateJson` (string?) — framework Checkpointing 序列化的 superstep state；`null` = 尚未進入 framework Kickoff path（走舊 path）或已完成

**設計理由**：
- 不破壞既有 schema（既有 28 + 1（Stage 49 加的） = 29 欄位不動）
- nullable = 走舊 path 時保持 null，feature flag true 切換時才寫入
- **與 `FrameworkAppealStateJson` 獨立**：Appeal 與 Kickoff 屬不同 framework Workflow / 不同 sessionId 概念，共用單一欄位會 race condition / 互相覆蓋
- Stage 54 收尾砍舊 path 時可考慮把欄位 promote 為非 nullable

**Migration `Stage50TaskGroupKickoffFrameworkState`**：

`dotnet ef migrations add Stage50TaskGroupKickoffFrameworkState --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext`

---

## 子項 2：`KickoffState` production 版

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Kickoff/KickoffState.cs`（新檔，**注意：production main 上的 `Workflows/Kickoff/` 是新資料夾**，與 Stage 49 `Workflows/Appeal/` 獨立）

**對齊既有 production schema**：
- 不只是「3 個欄位」 toy state，要對齊真實 Kickoff 流程：`GroupId / Round / MaxRounds / ProposalContent / LastPetraOutput / RosaSessionId / DemiSessionId / CodySessionId / QuinnSessionId / PetraSessionId / WorkingDir / Owner / Repo`
- `[JsonPropertyName]` 對應 framework Checkpointing 序列化（System.Text.Json camelCase）
- 不含 ClaudeCodeService 等 reference 物件（純資料，不能載 service）

**設計約束**：
- State 必須能 round-trip 序列化 / 反序列化（framework Checkpointing 機制）
- 對齊 Stage 49 `AppealStateHelpers` pattern：靜態 helper class 包 `ReadAsync` / `WriteAsync`（從 `IWorkflowContext` 讀寫）

---

## 子項 3：`ClaudeCodeAgentExecutor` production 化（移除 [Obsolete]）

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Appeal/Executors/ClaudeCodeAgentExecutor.cs`（既有 Stage 49 預留檔案）

**Stage 50 處理**：
1. **移除 `[Obsolete]` attribute**（首行 `[Obsolete("Stage 49 路線 B 不直接引用本 Executor。預留 Stage 50+ ...")]` 整段刪除）
2. **更新 class summary doc**：標記 Stage 50 起 production 使用，「會議多 Agent 直連 IClaudeCodeService」場景生效
3. **調整 `AppealStateHelpers.ReadAsync` 依賴**：原本 wrapper 內 `await AppealStateHelpers.ReadAsync(context)` 是綁 Appeal 路徑的；Stage 50 Group Chat 不一定有 AppealState — 需驗證 Forge Plan Mode 第一步是否要：
   - 選項 a：把 wrapper 改成 generic（接 `KickoffState` 或 `AppealState`），用泛型參數
   - 選項 b：新建 `ClaudeCodeKickoffExecutor` 專為 Kickoff（複製 wrapper 邏輯但讀 KickoffState）
   - 選項 c：把 ReadAsync 抽到呼叫端，wrapper 只接 `string message`（去 state 依賴）
   - **Forge Plan Mode 拍板**：依 Group Chat / Workflow Builder 模式哪個更自然
4. **產生路徑變更**：本 wrapper 從 [Obsolete] 預留升級為 production 化使用，新位置可能搬到 `Workflows/Common/Executors/`（跨 Appeal + Kickoff 共用）— **Forge Plan Mode 拍板搬不搬**

**Cody/Vera/Petra/Rosa/Demi/Quinn 路徑對應**（Kickoff 場景）：
- Petra = `RunMeetingSessionAsync`（含 file system + read tools，stateful session）
- Rosa = `RunMeetingSessionAsync`（read-only tools `MeetingCommons.ReadOnlyTools`）
- Demi = `RunMeetingSessionAsync`（read-only tools）
- Cody = `RunMeetingSessionAsync`（**無限制 tools**，可深入探 codebase）
- Quinn = `RunMeetingSessionAsync`（read-only tools）

---

## 子項 4：`KickoffWorkflowFactory`（依 spike 結論走 A1 或 A2）

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Kickoff/KickoffWorkflowFactory.cs`

### 路線 A1：framework Group Chat orchestration（首選）

**API 入口**：`AgentWorkflowBuilder.CreateGroupChatBuilderWith(agents => new CustomKickoffGroupChatManager(agents) { MaximumIterationCount = kickoffMaxRounds })`

**Custom Manager 設計**（spike E1 結論依據）：
- 繼承 `RoundRobinGroupChatManager` 或實作自定義 manager interface
- override `SelectNextSpeaker` / `BroadcastResponse` / 等 method 實現「每輪：先回 Rosa+Demi+Cody+Quinn 4 並行 → 全 4 回應 broadcast 後再回 Petra → Petra 整理 + 判 decision」邏輯
- 依 Petra decision JSON：consensus → 結束（output `KickoffLoopResult`）/ needs_discussion → 進入下一輪 / escalate → 結束（output `KickoffLoopResult` 含 escalate 標記）

### 路線 A2：Workflow Builder fan-out/fan-in（fallback，spike E1 不可行時）

**API 入口**：對齊 Stage 49 `AppealWorkflowFactory` 用法 — `WorkflowBuilder` + `AddConcurrentEdge`（或 framework 提供的 fan-out edge API） + `AddSwitch` 實作 routing

**拓撲設計**：

```
start → ConcurrentDispatcher
        ├─ RosaExecutor   ┐
        ├─ DemiExecutor   ├→ Aggregator → PetraExecutor → Switch by KickoffPetraDecision:
        ├─ CodyExecutor   │                                ├ "consensus"        → output (KickoffLoopResult.Consensus)
        └─ QuinnExecutor  ┘                                ├ "escalate"          → output (KickoffLoopResult.Escalate)
                                                            └ "needs_discussion"  → ConcurrentDispatcher (loop, Round+1)
```

**Aggregator 設計**：對應 spike 報告 4 提到的「Concurrent Agents with Custom Aggregator」pattern — Custom Executor 接 4 個 input（Rosa/Demi/Cody/Quinn output）整合成 `KickoffRoundCollected`（5 欄位 record），傳給 Petra Executor

### 共用元素（A1/A2 都要實作）

| 元素 | 說明 |
|---|---|
| `KickoffState` 共享狀態 | 子項 2 產出，跨 superstep 持有 round / sessionIds / lastPetraOutput |
| Max iterations | 從 `WorkflowSettingsResolver.GetKickoffMaxRoundsAsync` 動態讀（既有 method，不需新建）|
| Routing decision parsing | 對齊既有 `KickoffMeetingService.TryParsePetraDecision` 邏輯 — Petra 回應最後幾行找 JSON `{"decision": "...", "summary": "...", "discussion_points": [...]}` |
| Output result | `KickoffLoopResult` record：`{ Decision, MeetingLog, TaskPlan, TotalRounds, EscalateReason? }`，對齊既有 `MeetingResult` 欄位 |
| `CheckpointManager` 工廠 method | 對齊 `AppealWorkflowFactory.CreateCheckpointManager()` — 沿用 Stage 49 JSON options（camelCase + JsonStringEnumConverter）|

---

## 子項 5：`KickoffCheckpointStore`

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Kickoff/KickoffCheckpointStore.cs`

**設計**：複用 Stage 49 `AppealCheckpointStore` 的完整 pattern：
- 實作 `ICheckpointStore<JsonElement>`
- in-memory ConcurrentDictionary（sessionId → checkpoint dict + parent links + latest）
- `CreateCheckpointAsync` 同步寫 `task_groups.KickoffFrameworkStateJson`（**寫 Kickoff 欄位，非 Appeal 欄位**）
- `LoadFromDbAsync(Guid groupId)` 從 DB 載 in-memory（Bot 啟動時 router 呼叫）

**設計差異 vs `AppealCheckpointStore`**：
- 寫的是 `KickoffFrameworkStateJson` 欄位（**不是** `FrameworkAppealStateJson`）
- 其他邏輯 1:1 複用（Stage 49 pattern 已驗 production 跑通）

**評估抽 base class**（Forge Plan Mode 決定）：
- 兩個 CheckpointStore 90% 邏輯一致，只差 DB 欄位
- 選項 a：直接 copy-paste `AppealCheckpointStore` → `KickoffCheckpointStore`（簡單，**Aria 推薦這個**，符合「3 次再抽象」原則 — 第 2 次出現先複製，第 3 次（Stage 51+）再抽 base）
- 選項 b：抽 `BaseFrameworkCheckpointStore<TEntity>` base class，子類只 override 寫 DB column 邏輯（提早抽象）

---

## 子項 6：feature flag + `WorkflowSettings` 擴充

### 實作項目

**位置**：
- `src/AiTeam.Bot/Configuration/WorkflowSettings.cs`（既有 class，加屬性 `UseFrameworkKickoff`）
- `src/AiTeam.Bot/Configuration/WorkflowSettingsResolver.cs`（既有 class，加 method `GetUseFrameworkKickoffAsync`）

**WorkflowSettings 新增屬性**：
- `bool UseFrameworkKickoff { get; set; } = false;`

**WorkflowSettingsResolver 新增 method**：
- `Task<bool> GetUseFrameworkKickoffAsync(CancellationToken ct = default)` — 對齊既有 `GetUseFrameworkAppealLoopAsync` pattern

**AppSettings key**：`Workflow:UseFrameworkKickoff`，預設 `false`，與 `Workflow:UseFrameworkAppealLoop` **完全獨立**

---

## 子項 7：`FrameworkKickoffRouter`

### 實作項目

**位置**：`src/AiTeam.Bot/Orchestration/Meeting/FrameworkKickoffRouter.cs`（新檔）

**設計**：對齊 Stage 49 `FrameworkAppealRouter` pattern，但**單一 entry**（vs Stage 49 是 2 個 entry）：

| 元素 | 說明 |
|---|---|
| Entry method | `HandleKickoffMeetingAsync(TaskGroup group, string proposalContent, string owner, string repo, CancellationToken ct)` — 對應 legacy `RunKickoffMeetingAndWaitAsync` |
| ActiveOrchestration marker | 標 `"FrameworkKickoff"`（雙 marker：與 `KickoffFrameworkStateJson` 搭配，區隔 legacy `"Kickoff"` 與 framework path）|
| Workflow input | `KickoffState` 直接作為 first input message（對齊 Stage 49 RunWorkflowAsync pattern）|
| 結果處理 | 從 `WorkflowOutputEvent` 取 `KickoffLoopResult` → 寫進既有 DB 欄位（`KickoffMeetingLog` / `TaskPlan` / `KickoffRound`）|
| Christ 確認流程 | 沿用 legacy 邏輯（Discord embed + 3 buttons + `BossInteraction` + `commandHandler.RegisterKickoffConfirmation`）— **不重寫**，從 `MeetingOrchestrationService.RunKickoffMeetingAndWaitAsync` 抽出對應邏輯複用 |
| 失敗 fallback | 沿用 Stage 49 模式：framework Workflow 跑失敗 → log + cleanup + （Stage 49 慣例為 `NotifyBossInterventionAsync` 但 Kickoff 場景無此 method）→ **Forge Plan Mode 拍板**：發 Discord error 或 fallback to legacy `KickoffMeetingService`|
| Crash Recovery | `RecoverStuckFrameworkKickoffsAsync(CancellationToken ct)` — 對應 Stage 49 `RecoverStuckFrameworkAppealsAsync`，掃 `task_groups.KickoffFrameworkStateJson != null && !IsPaused`，採對應降級策略（清 marker → 既有 dispatcher 重觸發） |

**整合 BossInteraction**（Stage 50 沿用 C2 與 Stage 49 慣例）：
- framework Workflow 跑完 → `FrameworkKickoffRouter` 內部仍**用既有手刻 path**開 BossInteraction（`InteractionService.CreateInteractionAsync("kickoff", ...)`）
- Stage 51 才動 framework Human-in-the-Loop

---

## 子項 8：`MeetingOrchestrationService` 入口分流 + Crash Recovery 雙系統隔離 + Dashboard UI

### 實作項目

#### 入口分流

**位置**：`src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs`

`RunKickoffMeetingAndWaitAsync` 開頭加 feature flag 分流：

```
if (await workflowResolver.GetUseFrameworkKickoffAsync(ct))
{
    await frameworkKickoffRouter.HandleKickoffMeetingAsync(...); // 新 path
    return;
}
// 既有 legacy 邏輯（不動）
```

`RunDesignPhaseAsync` **不動**（B2 拍板：Design 留 legacy）。

#### Crash Recovery 雙系統隔離（風險點 R2 緩解擴充）

**位置**：`MeetingOrchestrationService.RecoverStuckOrchestrationsAsync`

既有篩選邏輯（Stage 49 加的）：

```
.Where(g => g.ActiveOrchestration != null && !g.IsPaused
         && g.FrameworkAppealStateJson == null)
```

**Stage 50 擴充**：

```
.Where(g => g.ActiveOrchestration != null && !g.IsPaused
         && g.FrameworkAppealStateJson == null
         && g.KickoffFrameworkStateJson == null)
```

對應 log 訊息加「Stage50-CrashRecoveryFrameworkKickoff: legacy path 跳過 N 個 framework Kickoff path TaskGroup」(對齊 Stage 49 樣式)。

**新增 Bot 啟動時 hook**：`AgentQueueProcessor` 啟動時呼叫 `frameworkKickoffRouter.RecoverStuckFrameworkKickoffsAsync(ct)`（對齊 Stage 49 既有 `frameworkAppealRouter.RecoverStuckFrameworkAppealsAsync` 慣例）。

#### Dashboard SystemSettings UI 擴充（沿用 Stage 47 升級）

**位置**：`src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor` + `.razor.cs`

既有「v4 漸進遷移控制」區塊（Stage 49 建立）加：
- toggle：「使用 MS Agent Framework Kickoff Meeting」對應 `Workflow:UseFrameworkKickoff`
- 警告文字：「⚠️ 實驗性功能，啟用前請確認 ① ANTHROPIC_API_KEY 設定 ② 跑過 Mock 驗收 ③ Appeal Loop（Stage 49）已通過驗收」

---

## 子項 9：Mock 場景 + Christ 線下驗收 + 結案

### Mock 場景擴充

**位置**：`src/AiTeam.Bot/Services/MockScenarioService.cs`

新增 4-5 個 `framework_kickoff_*` 系列場景（對應 spike POC 的精選子集）：

| 場景 key | 描述 |
|---|---|
| `framework_kickoff_consensus_round1` | 5 Agent 第 1 輪 Petra 判 consensus，產出 TaskPlan |
| `framework_kickoff_consensus_round2` | 第 1 輪 needs_discussion → 第 2 輪 consensus |
| `framework_kickoff_max_iter` | 跑滿 KickoffMaxRounds 仍 needs_discussion，強制結束產出 TaskPlan |
| `framework_kickoff_escalate` | Petra 判 escalate，路由到 BossInteraction（既有手刻 path） |
| `framework_kickoff_crash_recovery` | 中途 simulate crash，重啟後驗 framework Checkpointing 從 DB 還原（對齊 Stage 49 場景 C 測試方法） |

### Christ 線下驗收

見下方 ## 驗收情境 段（6 場景，對齊 Stage 49 結構）。

---

## 驗收情境

> Stage 50 是 v4 漸進遷移第二步，**驗收必須含 production 切換驗證**（feature flag false → true → false 全週期）。沿用 Stage 49 6 場景模式（A 預設 / B 切 framework / C Crash Recovery / D escalate / E 切回 / F token）。

### 場景 A：feature flag 預設 false 時 legacy Kickoff path 不受影響

**怎麼觸發**：
1. push Stage 50 commit → CI/CD 部署
2. AppSettings 表確認 `Workflow:UseFrameworkKickoff` 不存在或為 `"false"`
3. 跑 `/mock new_feature_with_proposal`（既有 Mock 場景，跑完整 Pipeline 含 Kickoff）

**怎麼驗證**：
- ✅ Kickoff 流程跑通與 Stage 49 之前完全一致（既有 production behavior 0 變動）
- ✅ Bot log 沒有 `[Stage50]` 或 framework Group Chat / WorkflowBuilder 相關訊息（沒走新 path）
- ✅ `task_groups.KickoffFrameworkStateJson` 為 null（沒寫入 framework state）
- ✅ Stage 49 `Workflow:UseFrameworkAppealLoop` 若同時為 true，Appeal loop 仍走 framework path（兩個 flag 完全獨立）

### 場景 B：Dashboard 切 feature flag → framework Kickoff path 接管

**怎麼觸發**：
1. Dashboard SystemSettings → v4 漸進遷移控制 → 切「使用 MS Agent Framework Kickoff Meeting」為 ON
2. 點「套用變更」（沿用 Stage 47 立即 ReloadCacheAsync）
3. 跑 `/mock framework_kickoff_consensus_round1`

**怎麼驗證**：
- ✅ Bot log 出現 `[Stage50] HandleKickoffMeetingAsync framework path 接管` + framework Workflow 啟動訊息（含 `Microsoft.Agents.AI.*` logger source）
- ✅ `task_groups.KickoffFrameworkStateJson` 有寫入內容（每 superstep 結束一次）
- ✅ Petra/Rosa/Demi/Cody/Quinn 透過 `ClaudeCodeAgentExecutor` 呼叫，`token_logs` 表 5 個 Agent 都正常記錄（對齊 Stage 44 機制 + Stage 49 case study：production 真實 LLM 才會寫，MockMode 0 行是預期）
- ✅ Petra TaskPlan 寫進 DB（`task_groups.TaskPlan`）+ Discord embed 通知 + BossInteraction 開啟（既有手刻 path 接手）
- ✅ Christ 按鈕「▶️ 繼續開發」能正常路由到 Design 階段（不被 framework 切換破壞）

### 場景 C：framework Checkpointing crash recovery 從 DB 還原

**怎麼觸發**：
1. `/mock framework_kickoff_crash_recovery`（在 Round 2 中段 simulate `docker compose restart aiteam-bot`）
2. 等容器重啟後觀察行為

**怎麼驗證**：
- ✅ Bot 啟動 log 出現 `[Stage50-CrashRecoveryFrameworkKickoff] 啟動：發現 N 個 stuck framework kickoff` 訊息
- ✅ legacy `MeetingOrchestrationService.RecoverStuckOrchestrationsAsync` log 出現 `[Stage50-CrashRecoveryFrameworkKickoff] Crash Recovery：legacy path 跳過 N 個 framework Kickoff path TaskGroup`（雙系統隔離 R2 擴充生效）
- ✅ workflow 接續 Round 2 繼續跑（不是從第 1 輪重來）— 或沿用 Stage 49 case study 降級策略（清 marker + 既有 dispatcher 重觸發），均可接受
- ✅ 最終 Petra 產出 TaskPlan 跟未 crash 情境一致

> 注意：Stage 49 case study 顯示「真 superstep-mid process kill 驗證有 30% 殘留」(由 Christ 線下驗收)，Stage 50 沿用同方法論：Forge 自驗 SQL 模擬 crash state + 雙 marker 隔離；真 superstep-mid kill 由 Christ 線下驗收（30% 殘留可接受）。

### 場景 D：Petra escalate 對接既有 BossInteraction

**怎麼觸發**：
1. `/mock framework_kickoff_escalate`
2. 等 framework Workflow 跑到 Petra 判 escalate

**怎麼驗證**：
- ✅ framework Workflow 跑完 `WorkflowOutputEvent`，`KickoffLoopResult.Decision == "escalate"`
- ✅ `FrameworkKickoffRouter` 內呼叫既有手刻 path 開 BossInteraction（**不直接用 framework Human-in-the-Loop**，那是 Stage 51 範圍）
- ✅ Discord / Dashboard 出現 BossInteraction 卡片，行為與 legacy 一致
- ✅ Christ 按鈕點「▶️ 繼續開發」/「⏹️ 停止」/「✏️ 修改」能正常路由

### 場景 E：feature flag false 切回 → legacy Kickoff path 重新接管

**怎麼觸發**：
1. 場景 B 跑完後，Dashboard 切「使用 MS Agent Framework Kickoff Meeting」回 OFF
2. 點「套用變更」
3. 再跑 `/mock new_feature_with_proposal`

**怎麼驗證**：
- ✅ Kickoff 流程走回 legacy path（與場景 A 行為一致）
- ✅ 既有 task_groups 中 `KickoffFrameworkStateJson` 殘留 row 不影響 legacy path 跑通（legacy 不讀此欄）
- ✅ rollback 路徑驗證安全網有效

### 場景 F：5 Agent 透過 ClaudeCodeAgentExecutor wrapper 維持 token 紀錄 + Christ Modify 流程不受影響

**怎麼觸發**：
1. **Token 紀錄驗收**：場景 B 跑通後查 `token_logs` 表（**MockMode=false 真實 LLM 跑才驗，MockMode 0 行是預期**）
2. **Modify 流程驗收**：場景 B 跑通後，Christ 在 BossInteraction 點「✏️ 修改計劃書」+ 提供修改指引，觀察 Petra Modify 流程

**怎麼驗證**：
- ✅ `token_logs` 5 個 Agent 對應 row 存在（`AgentName = "Petra" / "Rosa" / "Demi" / "Cody" / "Quinn"`，`Stage = "FrameworkKickoff_*"`）
- ✅ TokenLogService 紀錄完整（含 effective tokens / cost）
- ✅ **Modify 流程沿用既有 `KickoffMeetingService.ModifyTaskPlanAsync`**（feature flag 開關不影響）— Petra session_id = group.Id 仍可成功 resume，回應正常
- ✅ C2 拍板（Petra session 沿用 Claude Code `--resume`）production 真實生效

---

## 風險點 / 注意事項

### 1. framework Group Chat custom manager 介面 spike 第一步必驗（高）

**風險**：路線 A1（Custom GroupChatManager 一次回多個 next speakers）是否真的可行 framework 1.3.0 沒明文支援。

**緩解**：
- spike E1 為 Stage 50 第一步（Forge Plan Mode 內），不可行則 fallback A2（複用 Stage 49 Workflow Builder pattern，知識直接複用）
- A2 fallback 已在 Stage 48 spike 報告節 4 證實 LoC 減少 ~53% — 即使走 A2，仍 deliver Stage 50 戰略目標

### 2. framework Checkpointing 與 Stage 49 Appeal 雙系統並存（中）

**風險**：feature flag false 時 legacy `RecoverStuckOrchestrationsAsync` 跑、`UseFrameworkAppealLoop` true 時 framework Appeal Checkpointing 跑、`UseFrameworkKickoff` true 時 framework Kickoff Checkpointing 跑 — 三套 recovery 機制可能同時觸發 collision。

**緩解**：
- Stage 49 R2 緩解 pattern 擴充：legacy `RecoverStuckOrchestrationsAsync` 篩選邏輯加 `g.KickoffFrameworkStateJson == null`
- 兩個 framework path（Appeal vs Kickoff）的 marker 完全獨立（`FrameworkAppealStateJson` vs `KickoffFrameworkStateJson`）— 不會互相覆蓋
- `ActiveOrchestration` 值區隔：`"Kickoff"` (legacy) / `"FrameworkKickoff"` (framework Kickoff) / `"FrameworkAppeal"` (framework Appeal)

### 3. ClaudeCodeAgentExecutor 從 [Obsolete] 升級首次 production 化（中）

**風險**：Stage 49 留下的 wrapper 設計是「預留」未經 production 真實流量考驗，Stage 50 首次使用可能踩 spike 沒驗到的 corner case。

**緩解**：
- Forge Plan Mode 第一步檢視 wrapper signature（特別 `AppealStateHelpers.ReadAsync` 依賴問題）— 子項 3 已預留 3 種解法選項
- 5 個 Kickoff Agent 並行使用 wrapper → spike E3 並行度驗證一併 cover

### 4. Anthropic provider prerelease 持續曝露 production（中）

**風險**：Stage 49 既有風險 — `Microsoft.Agents.AI.Anthropic 1.3.0-preview.260423.1` 仍 prerelease（Stage 49 開工時無升級）。Stage 50 再次曝露 production 期。

**緩解**：
- feature flag 預設 false → 不啟用就 0 影響
- Forge Plan Mode 第一步驗證 NuGet 版本是否升級（沿用 Stage 49 慣例）
- Stage 50 路線 B（C2 + Petra session 沿用 Claude Code）下，Petra **不直接走** framework Anthropic provider — 風險顯著降低
- Cody/Vera/Rosa/Demi/Quinn 也不直接走 framework Anthropic provider（全走 ClaudeCodeAgentExecutor 包 Claude Code CLI）

### 5. Petra session（group.Id）跨 framework + legacy 共用（低-中）

**風險**：Petra session_id = group.Id 在 framework Kickoff 跑期間 + Christ Modify 走 legacy KickoffMeetingService.ModifyTaskPlanAsync resume 同 session — 是否可能 race condition 或 session 狀態 corrupt？

**緩解**：
- 流程上序列：framework Kickoff 結束 → BossInteraction → Christ Modify。三階段時序錯開，Petra session 不會同時被兩端讀寫
- C2 拍板下 Petra session 維持 Claude Code 自管（framework 只管 control flow，不碰 Claude Code session 檔）
- 驗收場景 F 含此驗證

### 6. 不踩 production code 邊界擴大（自省點 #21 + Stage 49 校準）

**Stage 50 動的 production code**：
- 動：`src/AiTeam.Bot/Workflows/Kickoff/`（新資料夾）+ `src/AiTeam.Bot/Workflows/Appeal/Executors/ClaudeCodeAgentExecutor.cs`（移除 [Obsolete]，可能搬路徑）+ `src/AiTeam.Bot/Configuration/WorkflowSettings.cs` + `WorkflowSettingsResolver.cs` + `src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs`（入口分流 + Recovery 篩選擴充）+ `src/AiTeam.Bot/Orchestration/Meeting/FrameworkKickoffRouter.cs`（新檔）+ `src/AiTeam.Data/Entities.cs`（TaskGroup 加欄位）+ Migration + `src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor*` + `src/AiTeam.Bot/Services/MockScenarioService.cs` + `src/AiTeam.Bot/Orchestration/AgentQueueProcessor.cs`（啟動加 Recovery hook）+ `src/Directory.Build.props`（Version bump）
- 不動：legacy `KickoffMeetingService` 內既有邏輯（只在入口加分流，未改）/ `DesignMeetingService` 任何邏輯（B2 拍板）/ `Resources/CLAUDE_*.md` 任何 Agent prompt / Stage 49 既有 framework path（`Workflows/Appeal/`、`FrameworkAppealRouter`）

---

## Model 建議

依 [`workflow_aria_model_effort.md`](../../C:/Users/darkl/.claude/projects/D--Source-Code-AI-Team/memory/workflow_aria_model_effort.md) 四維度：

| 維度 | 評估 |
|---|---|
| **任務複雜度** | 高 — v4 遷移第二步 + framework Group Chat（新 builder API）整合既有 production + spike 第一步路線拍板 + ClaudeCodeAgentExecutor 首次 production 化 |
| **改動範圍** | L — 跨 Bot/Data/Dashboard 多檔 + 新建 4-5 檔（KickoffState / KickoffWorkflowFactory / KickoffCheckpointStore / FrameworkKickoffRouter）+ Migration + ClaudeCodeAgentExecutor 升級 |
| **歷史包袱** | 中 — Kickoff Meeting 是 Stage 25a 起的核心機制（production 用了 13 個 Stage），動到可能踩既有沒驗過邊界。但比 Stage 49 Cody-Vera-Petra Appeal loop 風險低（Kickoff 不含 Appeal loop 反覆迭代邏輯）|
| **判斷品質要求** | 高 — feature flag 邊界 + spike 第一步路線拍板 + Custom GroupChatManager 設計（A1 路線）+ Aggregator pattern 設計（A2 路線）|

**建議**：**Opus 1M + high**

理由：
1. **v4 遷移高判斷品質要求**（規模 L + spike 性質）→ Opus 1M
2. **預估 context 350-550K**（混合型 Stage：spike 第一步 + production 整合 + 5 Agent Workflow 拓撲）→ 對齊 Stage 47/48 校準錨教訓「>180K 直接 Opus 1M」
3. **可能拆 session**：
   - Session A：spike 第一步 + 子項 1-5（DB + State + Executor + Workflow Factory + CheckpointStore），預估 ~250-350K
   - Session B：子項 6-9（feature flag + Router + 入口分流 + Mock 驗收 + 結案），預估 ~150-250K

### Context 預估

依 7 項公式 + 混合型 Stage 校準（spike + production 整合，對齊 Stage 49 校準錨 ×1.25）：
- 開場 ~32K
- 工作 raw（新建 4-5 檔 + 動 5-6 既有檔 + Migration + Dashboard）~120-180K
- Grep / Bash 輸出 ~25-35K（讀 Stage 49 reference + grep 既有 KickoffMeetingService callers + framework docs WebFetch + dotnet build）
- 對話 turn 成本 ~50-80K（Plan Mode + 閘門一 + 結案）
- Edit 反覆對齊 ~30-50K
- Mock 驗收（5 場景）~40-80K
- follow-up 修正 ~30-100K（v4 遷移第二步風險中，預期 0-2 個 follow-up）
- 結案文件寫作 ~10-20K
- **總計約 ~340-580K**（Opus 1M 內 34-58% 負擔，舒適區）

→ 拆 session 建議：若 Forge spike + 子項 1-5 結束時 context > 200K，主動跟 Christ 提「拆下一 session 進子項 6+」。

---

## 與 v4 路線的關係

**Stage 50 是 v4 漸進遷移 6 Stage 的第二步**：

```
Stage 47 ✅ ops 補丁（FF 四十七，v3.34.0，2026-05-02）
Stage 48 ✅ spike Phase A（FF 四十九，採用結論，2026-05-02）
Stage 49 ✅ Cody-Vera-Petra Appeal loop 遷移（v3.35.0，2026-05-01）
   ↓
Stage 50（本 Stage）：Kickoff Meeting → Group Chat orchestration（v3.36.0）
   ↓
Stage 51：BossInteraction → Human-in-the-Loop（沿用 Stage 50 framework Group Chat 基礎）
   ↓
Stage 52：Design Meeting → Group Chat orchestration（B3 路線：Design 主迴圈遷移，前置作業 + needs_adjustment + 拆 task 提案保留 legacy）+ WorkflowEngine 整體 hardcoded pipeline → Workflow Builder（最大遷移點）
   ↓
Stage 53：Crash Recovery 全面切換到 framework Checkpointing
   ↓
Stage 54：收尾 + token middleware + production 切換 + 老 framework code 刪除
   ↓
Stage 55+（評估）：FF 三十六 Phase B 動態流程架構（依 Stage 52 後評估結果）
```

> 註：Stage 50 完成後 v4 漸進遷移進度 **2/6**。若 Stage 50 揭露 framework / Anthropic provider 重大議題 → 暫停 Stage 51+ + 評估是否需要 spike Phase A.5（補做特定模組驗證）。

**Stage 50 結案後對 Stage 51 的影響**：
- 若 Stage 50 順利 → Stage 51 BossInteraction → Human-in-the-Loop 信心 +
- 若 Stage 50 揭露 framework Group Chat custom manager 介面議題 → Stage 52 Design Meeting B3 路線需重評估（可能也走 A2 fallback）

**Stage 50 對 Design Meeting B3 路線的影響**：
- Stage 50 走 A1（Group Chat）→ Stage 52 Design Meeting 主迴圈也可走 A1
- Stage 50 走 A2（Workflow Builder）→ Stage 52 Design Meeting 主迴圈走 A2 機率高（複用 pattern）

---

## 實作紀錄

> Forge 結案第一段補（v3.36.0，2026-05-02 完成）。

### 子項完成度對照（對齊 Aria 計劃書 10 子項）

| # | 子項 | 狀態 | 完成備註 |
|---|---|---|---|
| **0** | Spike 第一步 3 項驗證（議題 E） | ✅ | E1 ❌ Group Chat custom manager 不支援 multi-speaker per round → 走 A2 fallback；E2 ✅ ICheckpointStore 通用；E3 ✅ 並行度不被框架限制（OS-level subprocess） |
| **1** | DB schema `task_groups.KickoffFrameworkStateJson` + Migration `Stage50TaskGroupKickoffFrameworkState` | ✅ | Entities.cs +8 行 / Migration 自動產出 |
| **2** | `KickoffState` production 版（Workflows/Kickoff/KickoffState.cs） | ✅ | 含 4 records（KickoffAgentOutput / KickoffRoundCollected / KickoffPetraVerdict / KickoffLoopResult）+ KickoffStateHelpers ReadAsync/SaveAsync |
| **3** | `ClaudeCodeAgentExecutor` Stage 50 處理 | ✅ | 拍板 reverse Aria 計劃書原案 — 保留 [Obsolete] 預留，僅更新 message 說明（Stage 50 5 Agent 走 service-call 路線 B，wrapper 仍未 production wire） |
| **4** | `KickoffWorkflowFactory` + 6 Executors + `KickoffPrompts` 抽出 | ✅ | A2 路線 fan-out/fan-in + AddSwitch + loop back；KickoffPrompts 抽出後 legacy 8 處 call site 直接 inline（Aria 計劃書範圍擴張認可） |
| **5** | `KickoffCheckpointStore` | ✅ | 90% 複用 Stage 49 AppealCheckpointStore（換 DB 欄位 + namespace + class name） |
| **6** | feature flag `Workflow:UseFrameworkKickoff` + WorkflowSettings/Resolver 擴充 | ✅ | 與 Stage 49 UseFrameworkAppealLoop 完全獨立 |
| **7** | `FrameworkKickoffRouter`（單一 entry + Recovery + escalate 整合） | ✅ | HandleKickoffMeetingAsync + RecoverStuckFrameworkKickoffsAsync + NotifyKickoffFailureAsync（fallback 拍板：不 fallback to legacy）+ escalate 路徑沿用既有 BossInteraction 手刻 path |
| **8** | `MeetingOrchestrationService` 入口分流 + Recovery 雙系統 + Dashboard UI + Program.cs DI + AgentQueueProcessor hook | ✅ | RunKickoffMeetingAndWaitAsync 開頭 feature flag 分流；Recovery 篩選追加 `&& g.KickoffFrameworkStateJson == null` + `[Stage50-CrashRecoveryFrameworkKickoff]` log；Dashboard 加 v4 漸進遷移控制 toggle 第二個 |
| **9** | Mock 場景 5 個 framework_kickoff_* + 結案 + Version → 3.36.0 | ✅ | scenario active key 機制對齊 Stage 49 framework_appeal_loop_*（透過 MockClaudeCodeService.FailScenario static 欄位傳遞），MockClaudeCodeService.RunMeetingSessionAsync 內 Petra 分支依 Round 切換 decision JSON |

### Session A 結案

**commit**：[`7d37a48`](https://github.com/darkleong/AiTeam/commit/7d37a48) `feat(stage50-A): v3.36.0 進行中 — Session A：DB + State + Workflow Factory + CheckpointStore（路線 A2 fan-out/fan-in）`

**範圍**：spike 第一步 + 子項 1-5（11 新檔 + 4 改檔）：
- DB：TaskGroup.KickoffFrameworkStateJson + Migration
- 新建 Workflows/Kickoff/：State / Prompts / WorkflowFactory / CheckpointStore + 6 Executors
- 改：legacy KickoffMeetingService（淨刪 213 行 prompt builders + records，全委派 KickoffPrompts）+ DesignMeetingService using 補（ModifyDecision 跨檔 reference）+ ClaudeCodeAgentExecutor [Obsolete] message 更新

**閘門一**：Aria 抽樣驗證 11 新檔 + 4 修改檔全到位 + Migration schema 對齊 + Workflow 拓撲 100% 對齊 + KickoffStartExecutor partial class + 雙 [MessageHandler] 對齊 Stage 49 pattern → ✅ 通過放行進 Session B

### Session B 結案

**commit**：（本 commit）`feat(stage50): v3.36.0 — v4 漸進遷移第二步，Kickoff Meeting 切 MS Agent Framework Group Chat + feature flag（Session B 收尾）`

**範圍**：子項 6-9（1 新檔 + 6 改檔 + Version bump）：
- 新建 FrameworkKickoffRouter（Orchestration/Meeting/，單一 entry + Recovery + escalate 整合 BossInteraction）
- 改：WorkflowSettings + WorkflowSettingsResolver（feature flag）+ MeetingOrchestrationService（入口分流 + Recovery 雙系統隔離 + BuildKickoffProposalContent internal）+ AgentQueueProcessor（啟動 hook）+ Program.cs（DI 註冊 3 Singleton）+ SystemSettings.razor/.razor.cs（Dashboard toggle）+ MockScenarioService（5 個 framework_kickoff_* 場景）+ MockClaudeCodeService（Petra 分支 Round-aware decision）+ Directory.Build.props（3.35 → 3.36）

### 驗收結果

對齊 Stage 49「Forge 自驗 + 真實 LLM 補驗」結構：

| 場景 | Forge 自驗 | Christ 線下補驗（真實 LLM / Discord UI） |
|---|---|---|
| **A**：feature flag false → legacy 不變 | ✅ build 0 error + 入口分流邏輯確認（`if (await workflowResolver.GetUseFrameworkKickoffAsync) → router; return;` 在 ActiveOrchestration set 前 → false 時走下方 legacy 邏輯不變） | 跑一般 Mock 確認流程通暢，無 [Stage50] log |
| **B**：feature flag true → framework path 接管 | ✅ MockScenarioService 5 場景 + MockClaudeCodeService 切換邏輯邏輯一致；Workflow 拓撲 build 通過；DI graph 全 Singleton 對齊 | Dashboard toggle ON + 跑 `framework_kickoff_consensus_round1` → log 應出現 `[Stage50] HandleKickoffMeetingAsync framework path 接管` + DB `KickoffFrameworkStateJson` 寫入 |
| **C**：framework Checkpointing crash recovery | ✅ RecoverStuckFrameworkKickoffsAsync 篩選 `g.KickoffFrameworkStateJson != null && !g.IsPaused`（Stage 45 紀律）+ Recovery 雙系統隔離 log 對齊 | 跑 `framework_kickoff_crash_recovery` 場景 + Round 2 期間 `docker compose restart aiteam-bot` → 觀察 `[Stage50-CrashRecoveryFrameworkKickoff]` log + 降級策略生效（清 marker → dispatcher 重觸發） |
| **D**：Petra escalate → BossInteraction | ✅ FrameworkKickoffRouter.CreateKickoffConfirmationAsync escalate 路徑邏輯確認（embed Color.Orange + 沿用 kickoff_* button id + InteractionService.KickoffActionsJson） | 跑 `framework_kickoff_escalate` 場景 → 觀察 Discord embed `⚠️ Kick-off 會議需老闆裁決（escalate）` + BossInteraction 卡片 |
| **E**：feature flag false 切回 → legacy 接管 | ✅ 入口分流檢查邏輯在每次呼叫時都執行（不 cache），Dashboard 切 OFF 後下次 RunKickoffMeetingAndWaitAsync 走 legacy | Dashboard 切 OFF + 跑一般 Mock 確認流程與場景 A 一致 |
| **F**：5 Agent token + Christ Modify 沿用 legacy | ✅ KickoffAgentExecutor + KickoffPetraExecutor 全 call MeetingCommons.RunAgentTurnAsync 含 `meetingType: "Kickoff"` 群組計法（對齊 legacy 行為，token_logs AgentName="Meeting-Kickoff"）；Modify 流程 KickoffMeetingService.ModifyTaskPlanAsync 不變 | 真實 LLM 跑 `framework_kickoff_consensus_round1` 後查 token_logs 5 個 Agent + AgentName=Meeting-Kickoff；點 ✏️ 修改計劃書 → Petra session resume 對話 |

### 驗收後修正

> 待 Christ 線下驗收後補（如有）。

### 關鍵設計決策（為什麼這樣選）

| # | 決策 | Why |
|---|---|---|
| 1 | **A2 fallback 路線（WorkflowBuilder fan-out/fan-in）** vs A1（Group Chat custom manager） | spike 第一步 E1 揭露 framework Group Chat 是 star topology + single-speaker turn-by-turn 設計，與「4 Agent 獨立並行視角」相反；Concurrent Orchestration 又不支援 loop back。WorkflowBuilder + AddFanOutEdge/AddFanInBarrierEdge/AddSwitch 是唯一可同時滿足「並行 + 多輪」的 API（agent-framework MapReduce + Loop sample 雙重佐證） |
| 2 | **`ClaudeCodeAgentExecutor` 仍保留 [Obsolete]，Stage 50 不 production wire** | 5 Agent Executor 直接 call legacy `MeetingCommons.RunAgentTurnAsync` 比包 wrapper 乾淨：① 沿用 token Meeting-Kickoff 群組計法（對齊 legacy）② 沿用 cleanup 邏輯（workingDir 由 router 統一管理，wrapper 不擁有生命週期）③ wrapper 預留 Stage 54+ 收尾或 Stage 55+ 動態流程架構真正 wire 用 |
| 3 | **`KickoffPrompts.cs` 抽出 + legacy 全刪 wrapper inline call** | 計劃書 v1.1 line 327「全刪 wrapper + call site 直接寫」優化路線 — 兩條路徑（legacy + framework）共用同 SoT，避免雙寫漂移；純機械化重構，prompt 文字 0 變動（Aria 計劃書範圍擴張認可） |
| 4 | **`KickoffCheckpointStore` 直接複製 vs 抽 base class** | 符合「3 次再抽象」原則 — Appeal + Kickoff 是第 2 次出現相似 pattern，Stage 51+ Design Meeting / Human-in-the-Loop 是第 3 次再評估抽象。當前複製成本（90% 邏輯相同）< 提早抽象的維護成本 |
| 5 | **fallback 拍板：framework Workflow 跑失敗 → 不 fallback to legacy** | Petra session_id = group.Id 已被 framework path 佔用，再走 legacy 會 double session creation；對齊 Stage 49 R2 緩解原則「雙系統不互相 invoke」。改發 Discord error embed + 標 group failed，由 Christ 線下決定 retry |
| 6 | **`KickoffAggregator` 採 SendMessageAsync 顯式模式 + Dictionary（非 ConcurrentDictionary）** | MapReduce sample Shuffler pattern 證實 framework AddFanInBarrierEdge 序列化 deliver（Shuffler 用 List 而非 ConcurrentList），Dictionary 即可；`_expectedRound` 比對 round 自動 reset bucket，loop back 場景無需 reset 每次 SaveAsync 同步寫框架 state（持續 append meeting log） |
| 7 | **Mock 場景透過 `MockClaudeCodeService.FailScenario` static 傳遞 scenario key** | 對齊 Stage 49 既有 `framework_appeal_loop_*` 模式（active scenario key），不依 sessionId pattern（sessionId 對 framework + legacy 都是 group.Id 或臨時 GUID 不能區分）；MockClaudeCodeService.RunMeetingSessionAsync 內 Petra 分支依 (FailScenario, prompt round 標記) 切換 decision JSON |

### 踩坑紀錄彙整

> 給 Stage 51+ 後續 v4 遷移預警。

1. **`Microsoft.Agents.AI.Workflows.Checkpointing.CheckpointManager` namespace 錯**：build error CS0234。實際 `CheckpointManager` 在 `Microsoft.Agents.AI.Workflows`（無 .Checkpointing 後綴），但 `ICheckpointStore<>` 在 `.Checkpointing`。修法：fully qualified 用 `Microsoft.Agents.AI.Workflows.CheckpointManager` 或直接 `using Microsoft.Agents.AI.Workflows;`。對 Stage 51+ 提醒：跨 router fully qualified 寫 method 簽名時注意 framework type 命名空間分裂。
2. **`KickoffMeetingService` 內 `ModifyDecision` record 跨檔共用**：搬 `ModifyDecision` 到 `KickoffPrompts.cs` 後，DesignMeetingService 也用 `ModifyDecision` → CS0246 編譯錯。修法：DesignMeetingService 加 `using AiTeam.Bot.Workflows.Kickoff;`（internal record 同 assembly 跨 namespace）。對 Stage 52 提醒：抽 prompt/parser 共用時跨 service 用同 record 類型，必須 grep 全 callers 補 using。
3. **`TaskRepository.GetByIdAsync` vs `GetTaskByIdAsync`**：直覺寫 GetTaskByIdAsync 但實際 method 名是 GetByIdAsync。修法：grep 確認 method 名後再寫。對齊既有 `legacy MeetingOrchestrationService` 不直接重用 task reference 而是 repo 取（避免 EF tracking 跨 scope）。
4. **`AgentQueueProcessor` Recovery hook 順序**：Stage 49 既有 `RecoverStuckFrameworkAppealsAsync` hook 在 line 73 `RecoverStuckOrchestrationsAsync`（legacy）之後 — Stage 50 同樣加在 Stage 49 hook 之後，**不能搶在 legacy 之前**（legacy 排除條件依賴 framework marker 已 set，順序錯會雙觸發）。
5. **Mock 場景 `framework_kickoff_crash_recovery` 用 PausePoint 機制**：但 Kickoff 不走 dispatcher fire steps 路徑，PausePoint 不適用。改採「Round 1+2 needs_discussion 推進到 Round 2」+ Christ 線下手動 docker restart 驗 Recovery。對 Stage 51 BossInteraction 提醒：framework Workflow 內部 superstep 不對應 dispatcher fire steps，crash recovery Mock 設計需另闢蹊徑。
6. **Petra 第 1 輪 `isFirstMessage` 判定**：4 Agent 第 1 輪都是 first message（新 sessionId），Petra Round 1 也是 first message（state.PetraSessionId 第一次用）。但 KickoffPlanExecutor 跑 Petra plan prompt 時 `isFirstMessage: false`（接續 Petra session）。對 Stage 51+ 提醒：跨 Executor 共用 sessionId 時，必須在每個 Executor 內判斷 `isFirstMessage`，不能用 state.Round == 1 一刀切（KickoffPlanExecutor 在 Round 1 也是 false）。
7. **Mock Petra round 偵測機制**：`prompt.Contains("## 第 N 輪各角色意見")` 對齊 KickoffPrompts.BuildPetraRoundPrompt 格式 line 12。對 Stage 51+ 提醒：若改 prompt 格式必須同步更新 MockClaudeCodeService 偵測字串（建議引用 const string 集中化，避免漂移）。
8. **PetraDecision / ModifyDecision 兩個 record 抽出時注意 internal 跨 namespace**：legacy `KickoffMeetingService.cs` 檔尾兩個 `internal record` 搬到 `KickoffPrompts.cs` 後，跨 namespace 還是 internal（同 assembly 可見），但 caller 必須加 using。對 Stage 52 提醒：抽 record 出來時 grep 全 callers 確認沒漏。

### Aria 校準錨候選（Aria 結案第二段填）

> 預估 ×1.0-1.5（混合型 Stage，spike 第一步 + production 整合）— 實際 Forge 自評：Session A + Session B 全程 ~155K context（spike + 11 新檔 + 7 改檔 + Mock 場景 + 結案文件 + 0 build/runtime fix follow-up），對齊「混合型 ×1.0-1.4 區間」中段。Aria 結案第二段補實際倍率。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-02 | 初版規劃書建立（Aria）—— v4 漸進遷移第二步 Stage：Kickoff Meeting 切 framework Group Chat（A4 路線 + B2 範圍 + C2 Petra session + D2 獨立 flag + E 三項 spike 全驗）|
