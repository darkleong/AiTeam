# Stage 27b：Agent 任務序列 — 操作性與可觀察性

> 對應 Future Feature：十（Phase 2）
> 對應版本：v3.13.0
> 建立日期：2026-04-15
> 狀態：📋 規劃中（待 Stage 27a 完成後開始）
> 文件版本：v1.1

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

## 驗收清單

### 27b-1 Agent 狀態管理
- [ ] `/pause Dev` → Cody 停止消費佇列
- [ ] `/resume Dev` → Cody 恢復消費
- [ ] `/stop-all` → 所有 Agent 完成手頭後停止
- [ ] `/queue` → 顯示所有 Agent 佇列狀態
- [ ] Agent 卡片顯示狀態 Badge（Active / Paused / Stopped）

### 27b-2 Dashboard
- [ ] Agent 卡片顯示佇列深度
- [ ] StatusBadge 支援 "queued" 狀態
- [ ] Pipeline View 的 queued 步驟顯示「等待中」
- [ ] SignalR 即時更新佇列變化

### 整體
- [ ] `dotnet build` 零 error
- [ ] `dotnet test` 通過
- [ ] CLAUDE.md 版本號更新

---

## 版本歷史

| 日期       | 版本 | 內容                   |
| ---------- | ---- | ---------------------- |
| 2026-04-15 | v1.0 | Aria 撰寫初版規劃書（從 Stage 27 拆分為 27b） |
| 2026-04-16 | v1.1 | Aria Review 補充：AppSettingsService cache 即時生效、Stopping→Stopped 自動轉換邏輯位置、SignalR 觸發加入 cancel |
