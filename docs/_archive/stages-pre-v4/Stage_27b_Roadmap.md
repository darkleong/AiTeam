# Stage 27b：Agent 任務序列 — 操作性與可觀察性

> 對應 Future Feature：十（Phase 2）
> 對應版本：v3.13.0
> 建立日期：2026-04-15
> 狀態：✅ 已完成（2026-04-16）
> 文件版本：v2.0

---

## 概述

Stage 27a 建立了核心佇列機制（Per-Agent Queue + WorkflowEngine 整合 + Crash Recovery），系統已是佇列驅動。

Stage 27b 在此基礎上加入操作性和可觀察性：
- Agent 狀態管理（Active / Paused / Stopped）— 讓 Christ 可以暫停/停止 Agent
- Dashboard 佇列視覺化 — 讓 Christ 看到每個 Agent 的佇列狀態

---

## 27b-1. Agent 狀態管理（Active / Paused / Stopped）

### 現狀

Stage 27a 完成後，所有 Agent 永遠處於可執行狀態。AgentQueueProcessor 會無條件消費佇列中的任務，無法暫停或停止。

### 狀態定義

```
Active（預設）— 正常處理佇列中的任務
    ├── /pause {agent} → Paused — 凍結佇列，不消費任務
    └── /stop-all → 所有 Agent → Stopping → Stopped

Paused — 佇列凍結，任務持續累積
    └── /resume {agent} → Active

Stopping — 完成手頭任務後停止，不接新任務
    └── 手頭完成 → Stopped

Stopped — 完全停止
    └── /resume {agent} → Active
```

### 需要做的事

1. **AgentQueueProcessor 整合**：
   - 每次輪詢時讀取 `AgentState:{agentName}`（透過 AppSettingsService，有 cache）
   - `active`：正常 dequeue 執行
   - `paused` / `stopped`：跳過該 Agent
   - `stopping`：不 dequeue 新任務，但允許正在執行的任務完成 → 完成後自動設為 `stopped`
   - **Stopping → Stopped 自動轉換**：在 `AgentQueueProcessor.ExecuteTaskAsync` 的 finally 區塊中，任務完成後檢查該 agent 狀態——若為 `stopping`，則透過 AppSettingsService 寫入 `stopped`，並推送 Dashboard 狀態更新

2. **Discord 指令**：
   - `/pause {agent}` — 暫停指定 Agent
   - `/resume {agent}` — 恢復指定 Agent
   - `/stop-all` — 所有 Agent 進入 Stopping
   - `/queue {agent?}` — 顯示指定 Agent（或全部）的佇列狀態

3. **AppSettingsService 寫入（含 cache 即時生效）**：
   - CommandHandler 呼叫 `appSettings.SetAsync("AgentState:{agentName}", "paused")`
   - AgentQueueProcessor 透過 `appSettings.GetAsync("AgentState:{agentName}", "active")` 讀取
   - **重要**：`SetAsync` 寫入 DB 時須同步更新本地 cache（或 invalidate），確保同一 Bot 進程中 Processor 的下一次輪詢（最多 3 秒後）即可讀到新狀態。若現有 AppSettingsService 的 cache TTL 過長，需在 `SetAsync` 中加入 cache invalidation

4. **Dashboard Agent 卡片**：
   - 在現有的 Agent 狀態卡上加入狀態指示（Active / Paused / Stopped 的 Badge 或圖標）
   - 僅顯示，不做操作按鈕（操作按鈕留給 Future Feature 九 — Dashboard 雙向操作中心）

---

## 27b-2. Dashboard 佇列視覺化

### 現狀

Dashboard 的 Agent 狀態卡（Team Office 頁面）只顯示「正在執行的任務名稱」和 status badge，沒有佇列資訊。Stage 27a 加入佇列後，Dashboard 無法顯示排隊中的任務。

### 需要做的事

1. **Agent 狀態卡增強**：
   - 加入佇列深度指示（例如 `佇列：3 個任務等待中`）
   - 加入 Agent 狀態 Badge（Active / Paused / Stopped）
   - SignalR 即時更新（enqueue / dequeue / cancel 時推送）

2. **TaskItem 新增 "queued" 狀態的 Dashboard 顯示**：
   - StatusBadge 新增 `"queued"` 對應顏色（建議 Default / Gray）
   - 任務列表頁面：queued 狀態的任務顯示排隊中
   - Pipeline View：queued 步驟顯示為「等待中」

3. **新增 API + DTO**：
   - `AgentQueueDto`：AgentName / State / QueueDepth / CurrentTask / QueuedTasks[]
   - `DashboardTaskService.GetAgentQueuesAsync()` — 回傳所有 Agent 的佇列狀態
   - SignalR Hub 新增 `QueueUpdate` 事件

4. **佇列總覽**（可選）：
   - 如果 Agent 卡片不夠用，可在 Team Office 下方加一個折疊式佇列清單
   - 顯示每個 Agent 的排隊任務（標題 + 等待時間）

---

## 建議實作順序

```
27b-1（Agent 狀態管理）    ← 操作性（Discord 指令 + Processor 整合）
  ↓
27b-2（Dashboard 視覺化）   ← 可觀察性（Agent 卡片 + StatusBadge + SignalR）
```

### 版本號

v3.13.0（Directory.Build.props）

---

## 實作紀錄

### 關鍵設計決策

**Stopping → Stopped 雙保險機制：**
原始規劃只在 `ExecuteTaskAsync` finally 區塊處理 stopping 轉換，但存在空轉問題：若 `/stop-all` 下達時 Agent 恰好閒置（semaphore 未被占用），沒有任務在跑就不會觸發 finally，Agent 會永久卡在 `stopping`。

解法：主迴圈增設獨立分支——`semaphore.Wait(0)` 成功即代表「沒有任務在執行」，此時直接轉為 `stopped`：
```csharp
if (agentState is "stopping")
{
    // semaphore 取得 = 無任務在跑 = 可安全停止
    await appSettings.SetAsync($"AgentState:{semaphoreKey}", "stopped");
    _ = pushService.PushQueueUpdateAsync();
    semaphore.Release();
    continue;
}
```
`ExecuteTaskAsync` finally 區塊保留作為安全網，處理「任務跑到一半時 /stop-all」的 race condition。兩處都寫 `stopped` 是冪等的，不會衝突。

**Discord `/pause` / `/resume` 使用 AddChoice() 提供下拉選單：**
原規劃用 string 參數 + 手動驗證。改用 `AddChoice()` 列出 8 個合法 executor key，Discord 自動提供下拉選單，完全避免打錯字。

**CommandHandler 參數命名衝突：**
`ShowDirectAgentConfirmAsync` 內有 local `var pushService = scope...GetRequiredService<DashboardPushService>()`，若建構子注入的參數也叫 `pushService` 會被 shadow。統一將建構子注入的 DashboardPushService 命名為 `dashboardPush` 規避衝突。

**PM (Petra) 不在佇列中：**
Petra 是 `TaskGroupService` 中 `await` 的內嵌閘門，不是獨立的 BackgroundService，也不在 SemaphoreGroups 的 8 個 key 中（Dev/Reviewer/QA/Doc/Requirements/Designer/Release/Ops）。`/stop-all` 不影響 Petra 的運作，Dashboard 的 CEO/PM 卡片不會出現狀態 Badge。

### 新增檔案

| 檔案 | 說明 |
|------|------|
| `src/AiTeam.Shared/Dtos/AgentQueueDto.cs` | `AgentQueueDto` + `QueuedTaskItemDto` |

### 修改檔案摘要

| 檔案 | 變更 |
|------|------|
| `AppSettingsService.cs` | 新增 `SetAsync`（DB upsert + cache 同步更新） |
| `AgentQueueProcessor.cs` | 注入 AppSettingsService；主迴圈狀態檢查（paused/stopped 跳過，stopping 直轉 stopped）；finally 安全網 |
| `AgentQueueService.cs` | 注入 DashboardPushService；EnqueueAsync / DequeueAsync / CancelAsync 三處推送 |
| `CommandHandler.cs` | 注入 DashboardPushService（命名 `dashboardPush`）；新增 `/pause`、`/resume`、`/stop-all`、`/resume-all`、`/queue` 五個指令 |
| `DashboardPushService.cs` | 新增 `PushQueueUpdateAsync()` |
| `AgentStatusHub.cs` | 新增 `ReceiveQueueUpdate` 常數 |
| `AgentStatusController.cs` | 新增 `POST /internal/agent-status/queue` 端點 |
| `DashboardTaskService.cs` | 新增 `GetAgentQueuesAsync()`（Dev group 對應 Dev + Dev_plan） |
| `StatusBadge.razor` | 新增 `"queued"` → "排隊中" |
| `app.css` | 新增 `--color-status-queued: #9e9e9e` + `.status-queued` |
| `Home.razor.cs` | 載入 `_agentQueues`、訂閱 `ReceiveQueueUpdate`、狀態輔助方法 |
| `Home.razor` | Agent 卡片新增狀態 Badge + 佇列深度 Chip |
| `Directory.Build.props` | v3.12.0 → v3.13.0 |

### SignalR 推送鏈路

```
AgentQueueService / CommandHandler
  → DashboardPushService.PushQueueUpdateAsync()（fire-and-forget）
  → POST /internal/agent-status/queue
  → AgentStatusController
  → hubContext.Clients.All.SendAsync(ReceiveQueueUpdate)
  → Home.razor ReceiveQueueUpdate handler
  → TaskService.GetAgentQueuesAsync() → StateHasChanged
```

---

## 驗收清單

### 27b-1 Agent 狀態管理
- [x] `/pause Dev` → Cody 停止消費佇列
- [x] `/resume Dev` → Cody 恢復消費
- [x] `/stop-all` → 所有 Agent 完成手頭後停止
- [x] `/resume-all` → 所有 Agent 一次恢復（補充指令）
- [x] `/queue` → Embed 顯示所有 Agent 佇列狀態
- [x] Agent 卡片顯示狀態 Badge（暫停 Warning / 停止中 Warning / 已停止 Error）

### 27b-2 Dashboard
- [x] Agent 卡片顯示佇列深度 Chip（> 0 時顯示）
- [x] StatusBadge 支援 "queued" → "排隊中"（#9e9e9e）
- [x] SignalR 即時更新佇列變化（enqueue / dequeue / cancel / pause / resume / stop）

### 整體
- [x] `dotnet build` 零 error
- [x] `dotnet test` 通過
- [x] v3.13.0 版本號更新

---

## 版本歷史

| 日期       | 版本 | 內容                   |
| ---------- | ---- | ---------------------- |
| 2026-04-15 | v1.0 | Aria 撰寫初版規劃書（從 Stage 27 拆分為 27b） |
| 2026-04-16 | v1.1 | Aria Review 補充：AppSettingsService cache 即時生效、Stopping→Stopped 自動轉換邏輯位置、SignalR 觸發加入 cancel |
| 2026-04-16 | v2.0 | Stage 27b 實作完成結案：補充踩坑三件組、SignalR 鏈路圖、新增 /resume-all 指令、驗收清單全 ✅ |
