# Stage 27a：Agent 任務序列 — 核心佇列機制

> 對應 Future Feature：十（Phase 1）
> 對應版本：v3.12.0
> 建立日期：2026-04-15
> 狀態：📋 規劃中
> 文件版本：v1.0

---

## 概述

目前系統建立在「一次只跑一個完整流程」的隱性假設上。`FireOneStepAsync` 直接 `await executor.ExecuteTaskAsync()`，執行完再 fire-and-forget 呼叫 `HandleAgentCompletedAsync` 觸發下一步。除了 Victoria 有 `SemaphoreSlim(1,1)` 保護 CLAUDE.md 檔案交換，其他 Agent 完全沒有並行保護。

一旦多個流程同時進行（例如 Cody 正在開發任務 A，同時有新任務需要 Cody 執行），兩個 Cody session 會操作同一個 workspace，導致 git branch 衝突、檔案互相覆蓋。

Stage 27a 建立核心佇列機制：Per-Agent FIFO Queue + WorkflowEngine 整合 + Crash Recovery。完成後系統即為佇列驅動，每個 Agent 同一時間只處理一件任務。

Stage 27b 接續加入 Agent 狀態管理（Active / Paused / Stopped）和 Dashboard 佇列視覺化。

### 設計原則

1. **DB 即是 Queue**：不引入 Redis / RabbitMQ 等外部佇列。TaskItem 已有 AssignedAgent + Status + CreatedAt，加上 `queued` 狀態即成為天然的佇列條目
2. **最小化現有架構破壞**：只重構 `FireOneStepAsync` 的執行路徑（enqueue 取代 direct await），其餘流程邏輯不動
3. **Victoria / Petra / 會議保持原樣**：Victoria 的 SemaphoreSlim 是檔案交換鎖（不是佇列），Petra 是 inline 呼叫，Kickoff/Design 會議是 fire-and-forget 多 Agent session——這三者不需要改為佇列模式
4. **Queue 範圍**：只佇列化透過 `IAgentExecutor` 分派的標準 Agent 步驟（Dev / Reviewer / QA / Doc / Release / Ops / Requirements / Designer）

---

## 27a-1. AgentQueueProcessor + 資料模型

### 現狀

`FireOneStepAsync`（TaskGroupService.cs line ~378）直接 `await executor.ExecuteTaskAsync()`，同步阻塞直到 Agent 完成。沒有佇列概念，也沒有 per-agent 的執行保護。

Agent 的 DI 註冊（Program.cs）使用 Keyed Scoped/Singleton：
```
Dev / Reviewer / QA / Doc / Requirements / Designer → AddKeyedScoped
Ops → AddKeyedSingleton
```

### 資料模型變更

**TaskItem 新增欄位：**

```
QueuedAt     DateTime?    — 進入佇列的時間（排序用）
QueueStatus  string?      — "queued" / "processing" / null（不在佇列中）
```

> `QueueStatus` 與現有 `Status` 分開：`Status` 記錄任務生命週期（running/done/failed），`QueueStatus` 記錄佇列狀態。TaskItem 被 dequeue 執行時，`QueueStatus` 從 "queued" 變 "processing"，執行完變 null。

### 需要做的事

1. **EF Migration**：TaskItem 新增 `QueuedAt` (DateTime?) 和 `QueueStatus` (string?) 欄位

2. **AgentQueueProcessor**（新建 `AiTeam.Bot/Orchestration/AgentQueueProcessor.cs`）：
   - 繼承 `BackgroundService`
   - 為每個可佇列化的 Agent 維護一個 `SemaphoreSlim(1,1)`
   - 主迴圈輪詢 DB，每 3 秒檢查一次（或透過 `ManualResetEventSlim` 由 enqueue 觸發喚醒）
   - 執行流程：
     ```
     foreach agent:
       if (semaphore not available) skip
       dequeue oldest queued task for this agent
       if (no task) skip
       acquire semaphore → Task.Run:
         set QueueStatus = "processing"
         resolve IAgentExecutor via keyed DI
         await executor.ExecuteTaskAsync(...)
         set QueueStatus = null
         fire HandleAgentCompletedAsync
         release semaphore
     ```

3. **AgentQueueService**（新建 `AiTeam.Bot/Orchestration/AgentQueueService.cs`）：
   - `EnqueueAsync(TaskItem task)`：設定 `QueueStatus = "queued"`, `QueuedAt = DateTime.UtcNow`，存入 DB
   - `DequeueAsync(string agentName)`：查詢該 Agent 最早的 queued TaskItem，回傳並標記為 processing
   - `GetQueueDepthAsync(string agentName)`：回傳佇列深度
   - `GetQueueAsync(string agentName)`：回傳該 Agent 的完整佇列清單
   - `SignalNewTask()`：喚醒 AgentQueueProcessor 立即檢查（避免等 3 秒）

4. **DI 註冊**（Program.cs）：
   - `AddSingleton<AgentQueueService>()`
   - `AddHostedService<AgentQueueProcessor>()`

---

## 27a-2. FireOneStepAsync 重構 + WorkflowEngine 整合

### 現狀

`FireOneStepAsync` 的執行鏈：
```
建立 TaskItem → 推送 Dashboard → 解析 IAgentExecutor → await executor.ExecuteTaskAsync()
→ 更新狀態 → fire-and-forget HandleAgentCompletedAsync
```

全部在一個方法內同步完成。需要拆成「enqueue」和「execute」兩段。

### 需要做的事

1. **重構 FireOneStepAsync**：
   
   **Before（現有）：**
   ```
   建立 TaskItem(status="running") → await executor.ExecuteTaskAsync() → HandleAgentCompletedAsync
   ```
   
   **After（佇列化）：**
   ```
   建立 TaskItem(status="queued") → AgentQueueService.EnqueueAsync() → return
   ```
   
   FireOneStepAsync 變成純 enqueue 操作，不再直接執行 Agent。TaskItem 的初始 status 從 `"running"` 改為 `"queued"`，Dashboard 推送 status 也改為 `"queued"`。

2. **AgentQueueProcessor 的 execute 邏輯**（從 FireOneStepAsync 搬過來）：
   ```
   dequeue TaskItem → set status="running" → 推送 Dashboard
   → 解析 owner/repo/rules（從 group 和 config 取得）
   → await executor.ExecuteTaskAsync()
   → 更新 TaskItem status(done/failed)
   → 推送 Dashboard
   → fire-and-forget HandleAgentCompletedAsync
   ```

3. **保留不佇列化的特殊路徑**：
   - **Kickoff**（line ~384）：保持 `Task.Run` fire-and-forget，不走佇列
   - **Design**（line ~398）：同上
   - **Dev_plan → Dev 的 executor mapping**（line ~463）：在 enqueue 時就記錄正確的 executor key

4. **FireStepsAsync 簡化**：
   - 現有的 parallel / serial 分支 → 全部改為 enqueue（佇列本身就是 serial per-agent）
   - 若未來需要跨 Agent 並行（例如同時 enqueue Dev 和 Designer），佇列天然支援

5. **_runningCts 追蹤**（line ~484）：
   - 移到 AgentQueueProcessor 中管理
   - enqueue 階段不需要 CTS，execute 階段才建立
   - `CancelAsync` 方法改為從 processor 取得 CTS 並取消

6. **HandleAgentCompletedAsync 不變**：
   - 呼叫時機從 FireOneStepAsync 移到 AgentQueueProcessor
   - 邏輯本身不需要改動（決策 + enqueue 下一步）

### 需要注意的邊界案例

- **Dev_plan executor mapping**：FireOneStepAsync 目前用 `step.AgentName == "Dev_plan" ? AgentNames.Dev : step.AgentName` 來解析 executor。enqueue 時需要把 executor key 存在 TaskItem 上（或在 processor dequeue 時再映射）
- **IsFixLoop flag**：影響 HandleAgentCompletedAsync 的 agent key 判斷。需要在 TaskItem 上保留或在 enqueue 時記錄
- **rules 載入**：目前在 FireOneStepAsync 中透過 `ruleRepo.GetActiveRulesAsync()` 載入。改為在 processor 的 execute 階段載入

---

## 27a-3. Crash Recovery + 啟動恢復

### 現狀

系統重啟時，正在執行的任務直接消失。TaskItem status 停留在 `"running"`，沒有機制偵測並恢復。

### 需要做的事

1. **啟動掃描**（AgentQueueProcessor.StartAsync 或 ExecuteAsync 開頭）：
   ```
   掃描 DB：所有 QueueStatus = "processing" 的 TaskItem
   → 標記為 QueueStatus = "queued"（重新排入佇列）
   → 記錄 log：「系統重啟，{N} 個任務重新排入佇列」
   ```

2. **TaskItem status 恢復**：
   - `QueueStatus = "processing"` 且 `Status = "running"` → 重設 `Status = "queued"`
   - Dashboard 推送狀態更新

3. **重複執行防護**：
   - 被中斷的任務可能已經做了一半（例如 Cody 已經 push 了部分 code）
   - 對於 Claude Code 類 Agent，重新執行不會衝突（Claude Code 本身有冪等性）
   - 但 GitHub 操作（建立 PR、Issues）可能重複 → Agent 內部需處理冪等（已有：ExtractPrNumber 會先查是否已存在）

4. **Graceful Shutdown 整合**：
   - `IHostApplicationLifetime.ApplicationStopping` 觸發時：
     - 停止接受新的 enqueue
     - 等待正在執行的任務完成（timeout 60 秒）
     - timeout 後強制取消（CTS cancel）
     - 正在執行但被中斷的任務保持 `QueueStatus = "processing"`，下次啟動時恢復

---

## 建議實作順序

```
27a-1（資料模型 + Processor）   ← 基礎設施
  ↓
27a-2（FireOneStepAsync 重構）   ← 核心整合，改完系統就是佇列驅動
  ↓
27a-3（Crash Recovery）          ← 系統可靠性
```

27a-1 和 27a-2 必須一起做（否則 enqueue 了沒人消費）。

### 版本號

v3.12.0（Directory.Build.props）

---

## 驗收清單

### 27a-1 資料模型 + Processor
- [ ] EF Migration：TaskItem 新增 QueuedAt / QueueStatus 欄位
- [ ] AgentQueueProcessor 啟動後每 3 秒輪詢（或 signal 喚醒）
- [ ] 每個 Agent 有獨立的 SemaphoreSlim(1,1)
- [ ] `dotnet build` 零 error

### 27a-2 WorkflowEngine 整合
- [ ] FireOneStepAsync 改為 enqueue（不直接 await executor）
- [ ] TaskItem 初始 status 為 "queued"
- [ ] AgentQueueProcessor dequeue 後正確執行 Agent
- [ ] 執行完畢正確觸發 HandleAgentCompletedAsync
- [ ] Kickoff / Design 保持 fire-and-forget 不走佇列
- [ ] Dev_plan → Dev executor mapping 正確
- [ ] CancelAsync 可取消佇列中或執行中的任務
- [ ] MockMode 全流程：`/mock proposal` 跑通（每步驟正確排隊 → 執行 → 下一步）
- [ ] MockMode 全流程：`/mock bugfix` 跑通

### 27a-3 Crash Recovery
- [ ] 系統重啟後，processing 狀態的任務重新排入佇列
- [ ] 重啟後任務恢復執行不產生重複操作
- [ ] Graceful Shutdown：正在執行的任務完成後才停止
- [ ] Graceful Shutdown：timeout 後強制取消

### 整體
- [ ] `dotnet build` 零 error
- [ ] `dotnet test` 通過
- [ ] CLAUDE.md 版本號更新

---

## 版本歷史

| 日期       | 版本 | 內容                   |
| ---------- | ---- | ---------------------- |
| 2026-04-15 | v1.0 | Aria 撰寫初版規劃書（從 Stage 27 拆分為 27a） |
