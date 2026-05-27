# Stage 18 — Dashboard 可觀測性升級

> 版本：v2.0
> 建立日期：2026-04-08
> 完成日期：2026-04-09
> 狀態：✅ 已完成

---

## 目標

讓 Dashboard 從「看任務清單」升級為「看流水線在動」。新增 Agent 狀態卡即時更新 + Pipeline View 任務流程可視化，搭配 Mock Mode 進行開發測試。

---

## 一、Agent 狀態卡即時更新（Future Feature 十）

### 1.1 現況

- `DashboardPushService` 已有 `PushAgentStatusAsync()` → POST `/internal/agent-status`
- `AgentStatusHub` 已有 `ReceiveAgentStatus` 事件
- `Home.razor.cs` 已訂閱 SignalR `ReceiveAgentStatus` 並更新 UI

**問題：** Bot 端 `TaskGroupService.FireOneStepAsync` 在 Agent 開始/完成任務時**沒有呼叫 `PushAgentStatusAsync`**，所以 Dashboard 的 Agent 卡片不會即時更新。

### 1.2 修改方案

在 `TaskGroupService` 中：
- Agent 開始執行前 → `PushAgentStatusAsync(agentName, "running", taskTitle)`
- Agent 執行完成後 → `PushAgentStatusAsync(agentName, "idle", null)`
- Agent 執行失敗後 → `PushAgentStatusAsync(agentName, "error", errorMessage)`

改動極小：只需在 `FireOneStepAsync` 的 executor 呼叫前後各加一行。

### 1.3 驗收條件

- [x] Agent 開始任務時，Dashboard 首頁對應卡片即時切換為「執行中」（藍色）
- [x] Agent 完成任務時，即時切換為「閒置」（灰色）
- [x] Agent 失敗時，即時切換為「錯誤」（紅色）
- [x] 用 `/mock` 測試時可觀察到狀態卡隨流程進度即時變化

---

## 二、Pipeline View 任務流程可視化（Future Feature 十一）

### 2.1 設計方案

在任務中心點擊 TaskGroup 時，展開 Pipeline View：

```
 ┌──────┐    ┌──────┐    ┌──────┐    ┌──────┐    ┌──────┐    ┌──────┐
 │ Rosa │ ─→ │Petra │ ─→ │ Demi │ ─→ │Petra │ ─→ │ Cody │ ─→ │ Vera │ ...
 │  ✅  │    │  ✅  │    │  ✅  │    │  🔄  │    │ 待命 │    │ 待命 │
 └──────┘    └──────┘    └──────┘    └──────┘    └──────┘    └──────┘
```

### 2.2 UI 元件選型

**MudBlazor 8.x 已安裝**，`MudStepper` 和 `MudTimeline` 可直接使用。

| 元件 | 用途 |
|------|------|
| **MudStepper**（主視圖） | 水平 Pipeline，每個節點 = 一個 Agent 步驟 |
| **MudTimeline**（詳情展開） | 垂直時間線，點擊節點後顯示該步驟的 TaskLog |

### 2.3 MudStepper 節點設計

每個節點顯示：
- Agent 名稱（Rosa / Petra / Demi / Cody / Vera / Quinn / Sage）
- 狀態色：灰（待命）/ 藍（執行中）/ 綠（完成）/ 橘（審核中/revision）/ 紅（失敗）
- 耗時（CompletedAt - CreatedAt）

特殊標示：
- Petra 打回（revise）→ 回退箭頭 + 次數
- Escalate → 紅色警示圖標
- 老闆確認 → 菱形節點（人工介入）
- `[MOCK]` 任務 → 特殊標記（如虛線邊框）

### 2.4 MudTimeline 詳情

點擊 MudStepper 節點 → 下方展開 MudTimeline：
- 各 TaskLog 步驟（開始 → 執行中 → Claude Code 啟動 → 完成）
- 開始時間、結束時間、耗時
- Agent 產出摘要（Summary）
- Petra 審核結果（approve / revise / escalate + revision instructions）

### 2.5 資料來源

**已有的資料完全足夠：**

| 需求 | 來源 |
|------|------|
| 流程步驟列表 | TaskGroup → Tasks（按 CreatedAt 排序） |
| 每步驟狀態 | TaskItem.Status（pending/running/done/failed/revision/reviewing） |
| 每步驟 Agent | TaskItem.AssignedAgent |
| 耗時 | TaskItem.CreatedAt / CompletedAt |
| 詳細 log | TaskItem → TaskLogs |
| Petra 打回次數 | TaskGroup.DevPlanRevision / FixIteration |
| Workflow 類型 | TaskGroup.WorkflowType |

### 2.6 新增元件

| 檔案 | 說明 |
|------|------|
| `Components/Pages/Tasks/PipelineView.razor` | 主元件：MudStepper + MudTimeline |
| `Components/Pages/Tasks/PipelineView.razor.cs` | Code-behind：載入 TaskGroup + Tasks + Logs |

### 2.7 觸發方式

在現有的任務中心頁面（`TaskCenter.razor`）中，點擊 TaskGroup 行 → 展開 PipelineView（取代或增強現有的 TaskLogDrawer）。

### 2.8 即時更新

Pipeline View 開啟時，訂閱 SignalR `ReceiveTaskUpdate` 事件。當流程進行中（例如 Mock 模式下），Pipeline 的節點狀態會即時更新，不需手動刷新。

### 2.9 驗收條件

- [x] 任務中心點擊 TaskGroup 展開 Pipeline View
- [x] MudStepper 正確顯示各 Agent 步驟及狀態色
- [x] 點擊節點展開 MudTimeline 顯示詳細 TaskLog
- [x] Petra 打回標記正確顯示（revise 次數）
- [x] 耗時正確計算並顯示
- [x] SignalR 即時更新：Mock 模式下流程進行時，Pipeline 節點即時變色
- [x] NewFeature（含提案）/ BugFix 兩種流程類型正確渲染步驟（已驗收）
- [x] 失敗的步驟顯示紅色 + 錯誤摘要
- [x] `[MOCK]` 任務有視覺標記區分

---

## 三、開發策略

**全程使用 Mock Mode 開發測試：**

1. Dashboard 開啟 Mock Mode
2. `/mock workflow:新功能（含提案）` 跑一次完整流程
3. 任務中心出現完整的 TaskGroup（含 Rosa → Petra → Demi → ... 全部步驟）
4. 用這筆資料開發 Pipeline View

不需要花 $5 跑真實流程，就能有完整的測試資料。

---

## 四、風險評估

| 風險 | 影響 | 緩解措施 |
|------|------|---------|
| MudStepper 自定義程度不夠 | 無法顯示回退箭頭等特殊標示 | 先用 MudStepper 基本功能，特殊標示用圖標 + tooltip 替代 |
| Pipeline View 載入慢（TaskLog 資料量大） | 頁面卡頓 | 懶載入：點擊節點才讀 TaskLog，不一次全部載入 |
| SignalR 即時更新頻率過高 | UI 閃爍 | 加 debounce，500ms 內多次更新合併為一次重繪 |

---

---

## 五、實作重點紀錄

### 5.1 新增檔案

| 檔案 | 說明 |
|------|------|
| `AiTeam.Shared/Dtos/TaskGroupDto.cs` | TaskGroup 顯示 DTO（Id、Title、Status、WorkflowType、DevPrUrl 等，無 CompletedAt） |
| `AiTeam.Dashboard/Components/Pages/Tasks/PipelineView.razor` | 垂直 MudStepper（NonLinear）+ MudTimeline 懶載入 |
| `AiTeam.Dashboard/Components/Pages/Tasks/PipelineView.razor.cs` | Code-behind：HandleTaskUpdateAsync 公開方法供父元件轉發 SignalR |

### 5.2 修改檔案

| 檔案 | 修改內容 |
|------|---------|
| `TaskGroupService.cs` | `FireOneStepAsync` 前後各加 `PushAgentStatusAsync`（running/idle/error）；`RunPetraDevPlanReviewAsync` / `RunPetraVeraReviewAsync` 也補上 |
| `TaskItemDto.cs` | 新增 `GroupId` 欄位 |
| `TaskUpdateViewModel.cs` | 新增 `GroupId`（nullable Guid） |
| `DashboardTaskService.cs` | 新增 `GetTaskGroupsPagedAsync` + `GetTaskItemsByGroupAsync` |
| `TaskCenter.razor` / `.cs` | 新增「流程追蹤」Tab、TaskGroup 列表、MudDrawer、PipelineView @ref；SignalR 改用 `On<TaskUpdateViewModel>` 統一轉發 |
| `CommandHandler.cs` | `HandleMockProposalFlowAsync` 重構：先 CreateGroupAsync，提案步驟全掛 GroupId + CompletedAt |

### 5.3 踩坑五件組

1. **雙重訂閱 HubConnection 導致 Stepper 永遠不更新（根本原因）**
   - `TaskCenter` 用 `On<object>`，`PipelineView` 用 `On<TaskUpdateViewModel>`，同一 HubConnection 同一 method name 雙重訂閱，SignalR Client dispatch 衝突
   - **修復**：移除 PipelineView 獨立訂閱；TaskCenter 統一用 `On<TaskUpdateViewModel>` → 直接呼叫 `_pipelineViewRef.HandleTaskUpdateAsync(update)`

2. **Rosa/Demi/Petra 提案步驟不出現在 Pipeline View**
   - `HandleMockProposalFlowAsync` 先建提案 TaskItem 再建 TaskGroup，步驟無 GroupId
   - **修復**：先 `CreateGroupAsync`，再建 CEO 主任務和四個提案步驟，全部掛 `GroupId = group.Id`

3. **提案步驟沒有完成時間**
   - 直接建立 `Status = "done"` 的 TaskItem，未走 `UpdateStatus`，`CompletedAt = null`
   - **修復**：建立時加 `CompletedAt = DateTime.UtcNow`

4. **Pipeline View header 徽章不即時更新（執行中→完成）**
   - `_selectedGroup` 是 row click 時的 DTO snapshot，不會隨 DB 更新
   - **修復**：`HandleTaskUpdateAsync` 結尾根據 `_steps` 推算 Group.Status 並直接 mutate DTO

5. **群組列表列狀態落後（表格仍顯示「執行中」）**
   - 最後 SignalR 事件觸發時，Bot 的 `FireStepsAsync` 還沒把 `TaskGroup.Status` 寫入 DB
   - **修復**：`PipelineView` 透過 `OnGroupStatusChanged EventCallback` 通知 `TaskCenter`，延遲 1.5 秒再 `ReloadServerData()`

### 5.4 架構決策

- **PipelineView 無獨立 SignalR 訂閱**：由父元件 TaskCenter 統一接收，直接呼叫子元件 `HandleTaskUpdateAsync` 方法，避免多 handler 型別衝突
- **Group.Status 本地推算**：根據 `_steps` 狀態推算（有 failed → failed；全 done/cancelled → done；有 running → running），直接 mutate DTO，無需額外 DB 查詢
- **延遲刷新策略**：終態（done/failed/cancelled）觸發 1.5 秒後的群組列表 reload
- **MudStepper 用法**：`NonLinear="true"` + `@bind-ActiveIndex`，`Completed` / `HasError` / `Skipped` 由步驟狀態動態控制

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-08 | v1.0 初版建立 |
| 2026-04-09 | v2.0 實作完成並驗收（新功能含提案 + Bug Fix 兩種流程全通過）；補充踩坑五件組、架構決策、實作重點 |
