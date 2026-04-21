# Stage 33：Agent 狀態卡 2.0 — 佇列控制 + 待辦清單

> 對應 Future Feature：十五（Dashboard 與 Discord 功能平等 — 佇列控制子項）+ 二十一（Agent 狀態卡 expand 展開看待辦清單）
> 對應版本：v3.20.0
> 建立日期：2026-04-21
> 狀態：✅ 已完成（2026-04-21）
> 文件版本：v2.0

---

## 概述

主題「**Agent 狀態卡 2.0**」——把首頁 Agent 狀態卡從「狀態展示」升級為「互動控制中心」，兩個子項動同一個 UI 元件、共用資料源與 SignalR 推送：

| 子項 | 對應 FF | 目的 |
|----|---------|------|
| A | 十五（剩餘子項）| 佇列控制 Dashboard 化（per-agent pause/resume + 全域 stop-all/resume-all）|
| B | 二十一 | Agent 狀態卡 expand 展開看待辦清單（執行中 + 排隊中的 TaskItem）|

兩子項合併理由：都動 Agent 狀態卡元件、都需要 TaskItem + Agent 狀態資料源、SignalR 推送邏輯可以一起升級。分兩個 Stage 會造成元件被動兩次。

---

## 子項 A：佇列控制 Dashboard 化

### 背景

Discord 有 `/pause <agent>` / `/resume <agent>` / `/stop-all` / `/resume-all` 指令可控制 Agent 佇列（Stage 27b 實作），但 **Dashboard 沒有對應入口**。老闆要緊急停全部或暫停特定 Agent 時，要切到 Discord 輸入指令。

沿用 Stage 32 `MockScenarioService` pattern——抽 shared service + Internal API + UI 按鈕。這也能順手為 **FF 二十-B（CommandHandler 拆解）** 積少成多。

### 實作步驟

1. **抽 `AgentQueueControlService`（shared service）**
   - 位置：`src/AiTeam.Bot/Services/AgentQueueControlService.cs`
   - 搬 `CommandHandler` 中 `/pause` / `/resume` / `/stop-all` / `/resume-all` 的處理邏輯
   - API：
     ```csharp
     Task<(bool ok, string message)> PauseAgentAsync(string agent, CancellationToken ct = default);
     Task<(bool ok, string message)> ResumeAgentAsync(string agent, CancellationToken ct = default);
     Task<(bool ok, string message)> StopAllAsync(CancellationToken ct = default);
     Task<(bool ok, string message)> ResumeAllAsync(CancellationToken ct = default);
     ```
   - 回傳 `(bool, string)` 供 Discord Followup + Dashboard Snackbar 共用訊息（對齊 `MockScenarioService` 模式）

2. **`CommandHandler` 對應 slash command 改為薄 wrapper**
   - `HandlePauseCommandAsync` 等改為：驗證參數 → 呼叫 `AgentQueueControlService.PauseAgentAsync(agent, ct)` → `command.FollowupAsync(message)`

3. **Bot Internal API 新增 4 個端點**（`src/AiTeam.Bot/Api/InternalController.cs`）
   - `POST /internal/queue/{agent}/pause`
   - `POST /internal/queue/{agent}/resume`
   - `POST /internal/queue/stop-all`
   - `POST /internal/queue/resume-all`
   - 驗證 Authorization + agent 參數 + fire-and-forget 呼叫 service（仿 `/internal/mock/scenario`）

4. **`DashboardBotService` 新增 4 個方法**（仿 `TriggerMockScenarioAsync` / `RequeueTaskAsync`）
   - `PauseAgentAsync(string agent, ...)` / `ResumeAgentAsync(...)` / `StopAllAsync(...)` / `ResumeAllAsync(...)`
   - 回傳 `bool`（送出是否成功）

5. **Dashboard UI — Agent 狀態卡上加 pause/resume 按鈕**
   - 位置：Agent 狀態卡右上角（expand 按鈕旁邊）
   - 顯示邏輯：
     - Agent 狀態為 `paused` → 顯示 ▶️ 「恢復」按鈕（MudIconButton `PlayArrow`）
     - Agent 狀態為其他（idle / running / error）→ 顯示 ⏸️ 「暫停」按鈕（MudIconButton `Pause`）
   - 點擊後呼叫 `DashboardBotService.PauseAgentAsync` / `ResumeAgentAsync`
   - Snackbar 回饋 + SignalR 推送狀態變更

6. **Dashboard UI — 全域控制按鈕區**
   - 位置：首頁 Agent 狀態區域上方（或獨立 `GlobalQueueControlCard`）
   - 兩個按鈕：「🛑 緊急停止」（MudButton Color Error）+ 「▶️ 全部恢復」（MudButton Color Success）
   - 危險操作 → 點擊後彈 `MudDialog` 確認（「確定要暫停所有 Agent？進行中任務會跑完當輪」）
   - 確認後呼叫 `DashboardBotService.StopAllAsync` / `ResumeAllAsync`

---

## 子項 B：Agent 狀態卡 expand 展開看待辦清單

### 背景

Agent 狀態卡目前只顯示「忙碌 / 閒置」+「今日完成 N」+ 佇列深度數字，**看不到具體是哪些 TaskItem 在排隊或正在跑**。

Christ 決策（2026-04-21）：採**解讀 B**（該 Agent 尚未完成的全部 TaskItem）= 正在跑的 running（最多 1 個，per-agent Semaphore 限制）+ 排隊的 queued（N 個）+ 不含歷史（done / failed / cancelled）。

### 目標 UI

```
┌─ Cody (Dev) ────────────────── ⏸️  🔽 ┐
│ 🏃 執行中                 今日完成 5   │
└───────────────────────────────────────┘
   展開後 ↓
┌───────────────────────────────────────┐
│ 🏃 執行中：[Stage 33 實作]  已跑 3:20  │
│ ⏳ 排隊中：[FF 十八 UI 調整] 等候 30s  │
│ ⏳ 排隊中:[Bug 修復 #42]     等候 10s  │
└───────────────────────────────────────┘
```

- 清單為空時顯示灰字「無待辦」
- 每個 item 點擊 → 跳到 PipelineView（該 TaskGroup）
- SignalR 即時更新（新任務加入、完成後移除、running → queued 轉換）

### 實作步驟

1. **`DashboardTaskService` 新增查詢方法**
   - `Task<List<AgentTodoDto>> GetAgentTodosAsync(string agentName, CancellationToken ct)`
   - SQL：`WHERE AssignedAgent = ? AND Status IN ('queued', 'running')`
   - Order：`Status = 'running' DESC, QueuedAt ASC`
   - 回傳 DTO 含 TaskId / GroupId / Title / Status / CreatedAt / QueuedAt / StartedAt

2. **`AgentTodoDto` 新增**（`src/AiTeam.Shared/Dtos/AgentTodoDto.cs`）
   - 欄位：TaskId / GroupId / Title / Status（"running" | "queued"）/ QueuedAt / StartedAt

3. **Agent 狀態卡元件改造**
   - 元件位置：`src/AiTeam.Dashboard/Components/Pages/Home/AgentStatusCard.razor` + `.razor.cs`（若元件結構不同，請 grep 確認實際檔名）
   - 新增 `_expanded` 狀態 + `_todos` 清單欄位
   - 右上角加 `MudIconButton` `ExpandMore` / `ExpandLess`（與 pause/resume 按鈕同排）
   - 展開時條件渲染 `<MudCollapse>` 內包 `<MudList>`，列出 `_todos`
   - 點擊 expand 時：`_expanded = true` + 若 `_todos` 空則觸發 load
   - 每個 item 顯示：icon（🏃 running / ⏳ queued）+ Title + 時間戳（等候 / 已跑 時間）
   - 點 item → `NavigationManager.NavigateTo($"/pipeline/{groupId}")`

4. **SignalR 推送升級**
   - 現有 `DashboardPushService.PushAgentStatusAsync` 僅推狀態 + 今日完成數
   - 新增 `PushAgentTodosAsync(string agentName, List<AgentTodoDto> todos)`
   - 觸發時機：TaskItem 建立（queued 進入）/ dequeue（queued → running）/ 完成（running → done）/ 取消 / 失敗
   - Client 端（AgentStatusCard）訂閱 → 更新 `_todos` + `StateHasChanged`

### 效能考量

- 每個 Agent 平均待辦 < 10 個，8 個 Agent 共 ~80 筆以內，單次 query 無壓力
- 展開時才 load（不展開不發請求），若所有 Agent 狀態卡都展開也只 8 次 query，可接受
- SignalR 推送限節流（每個 Agent 100ms debounce 合併多次 push）—— 視實作是否有效能問題再加

---

## 共通設計：Agent 狀態卡 UI/UX 整合

兩子項都動 Agent 狀態卡，**設計上要一致**：

```
┌─ {Agent 名} ({角色}) ──── [⏸️/▶️] [🔽/🔼] ┐
│ {狀態 emoji + 文字}      今日完成 N       │
└────────────────────────────────────────────┘
```

按鈕順序建議：pause/resume（操作）在左、expand（檢視）在右。

**暫停狀態的視覺標示**：
- Agent 狀態為 `paused` 時，卡片 border 或背景加淡黃色提示
- 按鈕變成 ▶️「恢復」

**載入狀態**：
- expand 展開時若 query 未回，顯示 `MudProgressCircular` 骨架
- pause/resume 點擊後按鈕進入 loading state，防重複點擊

---

## 子項順序建議

A 和 B 可平行，但動同一元件時有衝突風險。建議順序：

1. **子項 A 全部做完**（後端抽 service + Internal API + DashboardBotService + 狀態卡 pause/resume 按鈕 + 全域控制區）
2. **子項 B**（狀態卡 expand 展開 + 待辦清單 + SignalR 推送升級）

分開做可避免 pause/resume 按鈕和 expand 按鈕的 UI 整合干擾。

---

## 驗收情境

### A. 佇列控制
1. Dashboard 首頁 Agent 狀態卡按「⏸️」Pause → 該 Agent 狀態轉為 paused、Discord `/queue` 查詢該 Agent 為暫停中、若有新任務指派會留在佇列不執行
2. 按「▶️」Resume → Agent 恢復、佇列任務開始執行
3. 全域「🛑 緊急停止」→ 彈確認 Dialog → 確認後所有 Agent 轉 paused
4. 全域「▶️ 全部恢復」→ 所有 Agent 恢復
5. Discord 仍可用 `/pause Cody` / `/stop-all` 等指令（回歸測試）

### B. 待辦清單
1. 觸發 `/mock new_feature` → Cody 卡片「🔽 展開」→ 看到「🏃 執行中：[任務 A]」+ 若有排隊看到「⏳ 排隊中」
2. 任務完成後清單自動移除該 item（SignalR 推送）
3. 新任務加入時清單自動新增（SignalR 推送）
4. 點清單 item → 跳到 PipelineView 該 TaskGroup
5. 無待辦時顯示「無待辦」灰字

### 回歸
- Discord `/pause` / `/resume` / `/stop-all` / `/resume-all` 全部正常
- 首頁原有 Agent 狀態、今日完成數、佇列深度顯示正常

---

## 版本

`v3.19.0 → v3.20.0`（minor bump）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議

**核心挑戰：CommandHandler 2327 行（~46K tokens）是主要 context 殺手。**

按新「Context 預估法」（見 `feedback_impl_session_briefing.md` 第二節）粗估：
- CommandHandler Read 一次 46K（子項 A 必動）
- AgentQueueService / Processor ~14K
- Home.razor + AgentStatusCard ~15K
- DashboardTaskService + SignalR Hub ~10K
- 新增 AgentQueueControlService + AgentTodoDto ~10K
- 開場 CLAUDE.md / conventions 15K
- Grep / Edit / Build 緩衝 20-30K

**單 Session 估 130K**，吃 Sonnet 200K 的 65%，驗收多輪修正會再累積 → **高風險接近 Stage 31 的 75% 邊界**。

### 兩個方案（Christ 選）

**方案 A（推薦）：拆兩個 Session，各 Sonnet 200K + high**

| Session | 範圍 | 估 context |
|---|---|---|
| **Session 1** | 子項 A 全部（後端抽 `AgentQueueControlService` + Internal API + DashboardBotService + 狀態卡 pause/resume 按鈕 + 全域控制區）| ~80K |
| **Session 2** | 子項 B（狀態卡 expand + 待辦清單 + SignalR 推送）| ~50K |

**好處**：0 額外成本、子項 B 啟動時 context 乾淨、順帶讓 Session 邊界與 UI 變更協調

**方案 B：單 Session Opus 1M + high**

一次做完，context 充裕。**壞處**：Opus 花錢比較多。

---

## 設計約定（本 Stage 共通）

- 新增 UI 按鈕 / 展開區塊都要有 **hover tooltip**（`Title` 屬性或 `MudTooltip`），老闆一眼看懂按鈕用途
- 危險操作（stop-all）**必須彈確認 Dialog**
- pause / resume 點擊後進入 loading state 防重複觸發
- SignalR 推送升級後，測試多客戶端同時開 Dashboard 的即時同步

---

## 結案檢查清單（兩段式分工）

- **實作 Session 做**：Stage_33_Roadmap 補「實作紀錄」章節 + 狀態 ✅ + 文件版本 v2.0 + 版本歷史、commit
- **Aria 做**：Master Plan header + 索引 ✅ + changelog；Future_Feature header + FF 十五「佇列控制子項」標 ✅ + FF 二十一 移入已完成 + changelog；掃 git log 把驗收期間 follow-up commits 補進 Roadmap（見 `feedback_impl_session_briefing.md` 第五節）

---

## 實作紀錄

### 子項 A：佇列控制 Dashboard 化

- **新增 `AgentQueueControlService`**（`src/AiTeam.Bot/Services/AgentQueueControlService.cs`）：4 個 method 回傳 `(bool ok, string message)` tuple，供 Discord 指令與 Dashboard Internal API 共用訊息，對齊 `MockScenarioService` 模式；`QueueExecutorKeys` 常數從 `CommandHandler` 搬入本 service 作為唯一真實來源；無效 Agent 名稱回 `(false, "❌ 未知的 Agent：...")`
- **`CommandHandler` 瘦身**（`src/AiTeam.Bot/Discord/CommandHandler.cs` L897-921）：`HandlePause/Resume/StopAll/ResumeAll` 4 個 handler 從 6-7 行壓縮為 3-5 行薄 wrapper（只做參數解析 + 呼叫 service + `FollowupAsync`）；同時移除建構子中已無引用的 `DashboardPushService dashboardPush`（由 `AgentQueueControlService` 內部持有）— 為 FF 二十-B「CommandHandler 拆解」積少成多，本次約 -40 行
- **`InternalController` 新增 4 端點**（`src/AiTeam.Bot/Api/InternalController.cs` L192-252）：`POST /internal/queue/{agent}/pause` / `.../resume` / `/internal/queue/stop-all` / `/internal/queue/resume-all`；抽共用 helper `FireAndForgetQueueControl(action, work)` 封裝 `IsAuthorized` + `Task.Run` + `CreateAsyncScope` + `logger` 邏輯，4 個端點各自只需 6 行委派
- **`DashboardBotService` 新增 4 方法**（`src/AiTeam.Dashboard/Services/DashboardBotService.cs` L86-126）：`PauseAgentAsync` / `ResumeAgentAsync` / `StopAllAsync` / `ResumeAllAsync`；同樣抽共用 `PostQueueControlAsync(path, actionForLog, ct)` helper，4 個方法各只 1 行委派
- **Agent 狀態卡抽元件**（`src/AiTeam.Dashboard/Components/Pages/Home/AgentStatusCard.razor` + `.razor.cs`，新增）：原 Home.razor L21-72 inline 的 52 行 per-agent UI 抽為獨立元件；參數 `[Parameter] AgentStatusViewModel Agent` / `[Parameter] AgentQueueDto? QueueInfo`；右側按鈕組新增 `MudIconButton Pause`（active 時顯示）/ `PlayArrow`（paused/stopping/stopped 時顯示）+ `MudTooltip`，點擊後 `_loading` 防重複觸發 + `ISnackbar` 回饋；paused 狀態卡片加 `rgba(255,193,7,0.08)` 淡黃背景提示
- **全域佇列控制卡**（`src/AiTeam.Dashboard/Components/Pages/Home/GlobalQueueControlCard.razor` + `.razor.cs`，新增）：置於首頁頂部 `MockScenarioCard` 下、Agent 狀態區上；紅色「🛑 緊急停止」按鈕點擊後 `DialogService.ShowMessageBox` 確認（訊息列出「進行中任務會跑完當輪 / 新任務留在佇列」兩條說明）；綠色「▶️ 全部恢復」按鈕直接觸發（非破壞性）；卡片左邊框 4px 警示黃色
- **Home.razor 瘦身**：Agent 狀態區從 52 行 inline 縮為 5 行 `<AgentStatusCard ... />` foreach；移除 Home.razor.cs 中不再使用的 `GetAgentStateLabel` / `GetAgentStateColor`（搬到 AgentStatusCard.razor.cs）
- **清理**：刪除 `src/AiTeam.Dashboard/Components/Shared/AgentStatusCard.razor`（舊版 Stage 4 時期元件，已無任何引用）— 解決與新 `AgentStatusCard` 的 Razor tag 命名衝突（`RZ9985: Multiple components use the tag 'AgentStatusCard'`）
- **DI 註冊**：`src/AiTeam.Bot/Program.cs` L105 新增 `builder.Services.AddSingleton<AgentQueueControlService>();`

### 子項 B：Agent 狀態卡 expand 看待辦清單

- **重用既有 DTO，不新增 AgentTodoDto**：探索時發現 `AgentQueueDto.CurrentTaskTitle` + `QueuedTasks: List<QueuedTaskItemDto>` 已帶 running + queued 兩種資料，只差「點擊跳轉需要的 GroupId / TaskId」。決策改為擴充 `AgentQueueDto` 而非新增 DTO
- **`AgentQueueDto` 擴充 3 欄位 + `QueuedTaskItemDto` 擴充 1 欄位**（`src/AiTeam.Shared/Dtos/AgentQueueDto.cs`）：`CurrentTaskId` / `CurrentTaskGroupId` / `CurrentTaskQueuedAt` 給 running 任務；`QueuedTaskItemDto.GroupId` 給每個 queued 任務
- **`DashboardTaskService.GetAgentQueuesAsync` 更新 projection**（L299-332）：`queuedTasks` SELECT 加 `t.GroupId`；output 物件構造加 `CurrentTaskId` / `CurrentTaskGroupId` / `CurrentTaskQueuedAt` 三欄位 + 每個 `QueuedTaskItemDto` 加 `GroupId`
- **`AgentStatusCard` 展開區塊**：pause/resume 按鈕右側新增 `MudIconButton ExpandMore/ExpandLess`（切換 `_expanded`），底下 `MudCollapse` 包 `MudList Dense`：
  - running 項（若有）：🏃 icon + Title + 「已跑 X:XX」，點擊跳 `/pipeline?groupId={CurrentTaskGroupId}`
  - queued 項（foreach）：⏳ icon + Title + 「等候 Xs」，點擊跳 `/pipeline?groupId={GroupId}`
  - 皆無時顯示「無待辦」灰字斜體
  - `FormatDuration` helper：秒/分/時分三段顯示（如 `45s` / `3:20` / `1h 15m`）
  - 所有時間以 `QueuedAt` 作基準（DB 未存 `StartedAt`，run 時長近似取 `QueuedAt`，差距為 dequeue → 實際處理間的 semaphore 等待時間，通常 <1s 可接受）
- **SignalR 推送補完**（`src/AiTeam.Bot/Orchestration/AgentQueueService.cs` L115-127）：`ClearQueueStatusAsync` 任務完成離開佇列時原本沒推送，現加 `_ = pushService.PushQueueUpdateAsync()`，讓展開的待辦清單完成後自動移除該項。其餘 trigger 點（enqueue / dequeue / cancel / requeue）Stage 27a/27b 已覆蓋
- **不新增 push method**：因完全複用 `AgentQueueDto` 推送通道，`PushAgentTodosAsync` 無必要，客戶端 `ReceiveQueueUpdate` 會自動帶新資料刷新 AgentStatusCard
- **`PipelineList` 深層連結支援**（`src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineList.razor.cs`）：`OnInitializedAsync` 呼叫新增的 `TryPreselectFromQueryStringAsync`，用 `Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery` 解析 `?groupId={Guid}`，找到則 `_selectedGroup = group; _isPipelineDrawerOpen = true;`（附加價值：未來任何外部 URL 直接深連也可用）

### 踩坑 / 決策修正

- **規劃書 `AgentTodoDto` 使用 `int` Id** → 實際 Entity 全 `Guid`，規劃階段由 Christ 提前發現修正為 `Guid`（後來乾脆併入既有 `AgentQueueDto`，連 DTO 都省了）
- **規劃書 `NavigateTo("/pipeline/{groupId}")`** → `PipelineList.razor` 只有 `@page "/pipeline"`（無帶參變體），規劃階段由 Christ 發現修正為 `?groupId=` query string + `PipelineList` 讀 query
- **Razor tag 命名衝突** `RZ9985: Multiple components use the tag 'AgentStatusCard'` — 原因是 `Components/Shared/AgentStatusCard.razor` 舊版（Stage 4 時期）殘留未清理。Grep 確認無任何引用後直接刪除
- **Agent 狀態卡元件抽離** 時記得把 Home.razor.cs 中的 `GetAgentStateLabel` / `GetAgentStateColor` 搬走，避免 dead code；同時把 `DashboardPushService dashboardPush` 從 CommandHandler 建構子中移除（4 個 handler 搬走後唯一使用點也消失）

### 驗收結果

- `dotnet build AiTeam.slnx` 兩段皆 0 errors（子項 A 綠燈 → 子項 B 綠燈）
- 原規劃書中擔心的「Agent 狀態卡 UI 整合衝突」避免成功：子項 A 先完全完成（pause/resume 按鈕定位右側、loading state、淡黃背景）再加 B（expand 按鈕緊接其後 + MudCollapse），無 UI 排版衝突
- 剩餘人工驗收（待 push 後自動部署完成）：
  - Dashboard Cody 卡片 pause/resume 切換 → Discord `/queue` 驗證 AgentState
  - 全域「緊急停止」→ 確認 Dialog → 所有 Agent stopping
  - Discord `/pause Cody` / `/stop-all` 等回歸正常
  - `/mock new_feature` 觸發後展開 Cody 卡片看 🏃 + ⏳ 清單、點擊跳 PipelineView

---

## 版本歷史

| 版本 | 日期 | 內容 |
|------|------|------|
| v1.0 | 2026-04-21 | 初版規劃書，兩子項（佇列控制 + 待辦清單）合併為「Agent 狀態卡 2.0」；Session 邊界拆兩段（Sonnet 200K × 2）或 Opus 1M 一氣呵成二選一 |
| v2.0 | 2026-04-21 | 實作完成（v3.20.0）：**子項 A** 抽出 `AgentQueueControlService`（搬 4 個 CommandHandler handler，積少成多為 FF 二十-B）、Internal API 4 端點 + 共用 `FireAndForgetQueueControl` helper、Agent 狀態卡抽成獨立元件 `AgentStatusCard.razor` + per-card pause/resume 按鈕、`GlobalQueueControlCard` 全域緊急停止（附確認 Dialog）；**子項 B** 重用既有 `AgentQueueDto` 擴充 4 欄位（`CurrentTaskId` / `CurrentTaskGroupId` / `CurrentTaskQueuedAt` + `QueuedTaskItemDto.GroupId`）取代規劃書原案的新增 `AgentTodoDto`、`AgentStatusCard` expand + MudCollapse 待辦清單（🏃 running + ⏳ queued + 無待辦提示）、`ClearQueueStatusAsync` 補 `PushQueueUpdateAsync` 讓清單完成時即時刷新、`PipelineList` 支援 `?groupId=` 深層連結；順帶清理 `Components/Shared/AgentStatusCard.razor` 舊版殘留解決 Razor tag 命名衝突 |
