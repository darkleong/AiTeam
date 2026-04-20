# Stage 31：可靠性補強 + Appeal 對抗紀錄 UI

> 對應 Future Feature：十七（可靠性補強）+ 十八（Appeal 對抗紀錄 UI 呈現）
> 對應版本：v3.18.0
> 建立日期：2026-04-20
> 狀態：🟡 待實作
> 文件版本：v1.0

---

## 概述

本階段合併兩個 🟡 中優先級 FF，主題是「讓老闆在 dogfooding 時不被卡住、看得見」：

| FF | 子項 | 目的 |
|----|------|------|
| 十七 A | Dashboard 失敗任務重試按鈕 | `failed` / `cancelled` 的 TaskItem 不用下 SQL 就能重試 |
| 十七 B | 會議 Crash Recovery（TaskGroup 欄位追蹤） | Bot 重啟後 Kickoff / Design 會議自動重跑，不再卡住要人工介入 |
| 十八 | Appeal 對抗紀錄 UI 呈現 | Stage 23/24 已在 DB 存完整對抗紀錄，Dashboard 加折疊面板直接呈現 |

三項合計規模 S-M，共用一次 Migration（十七 B），其他都是既有模式的 pure additive。

---

## 項目一：Dashboard 失敗任務重試按鈕（FF 十七 A）

### 背景

TaskItem 失敗 / 取消後目前沒有重試入口：
- Discord 重發 `/task` 會建新 TaskItem，不繼承失敗的 GroupId / 上下文
- 只能手改 DB：`UPDATE task_items SET status='queued', queue_status='queued' WHERE id=?` + 喚醒佇列

Q1 已保證 Agent 輸入全部落在 DB（Stage 23~26 成果），不需要重組上下文——技術上可行，只差 UI 入口。

### 實作步驟

1. **後端新增 `AgentQueueService.RequeueTaskAsync(taskId, ct)`**
   - 驗證當前 `status ∈ {failed, cancelled}`，否則拋 `InvalidOperationException`
   - `UpdateStatus(task, "queued")`
   - `SetQueueStatus(task.Id, "queued")`
   - `SignalOne(agentName)` 喚醒對應 Agent executor
   - 回傳 `(bool success, string? reason)`

2. **Bot internal API 新增端點**
   - `POST /internal/tasks/{taskId}/requeue`
   - 既有 Dashboard → Bot 通道模式（仿 Stage 29-3 `/internal/reload-cache`）

3. **Dashboard 流程追蹤頁 / 任務列表頁加「🔁 重試」按鈕**
   - 條件：`task.Status ∈ {failed, cancelled}`
   - 按下後呼叫 Bot API；成功後 SignalR 會自然推送狀態變化
   - 失敗走 FF 十六 Snackbar 規格（但 FF 十六未做，本 Stage 先用 MudAlert + `ISnackbar` 雙通道 ad-hoc）

### 驗收情境

- 手動製造一個 `failed` TaskItem（MockMode `fail_review` 走到最終 escalate）
- Dashboard 按「🔁 重試」
- 確認：任務從 queued 重新執行、佇列狀態即時更新、TaskLog 新增 requeue 事件

---

## 項目二：會議 Crash Recovery（FF 十七 B）

### 背景

Kickoff / Design / Petra 同步會議**不走 AgentQueueProcessor**，不在 Stage 27a `RecoverStuckTasksAsync` 的掃描範圍。Bot 重啟時中斷的會議會永遠卡在 running，需要人工介入。

**Christ 決策（2026-04-19）**：採方案 2（TaskGroup 欄位追蹤），不升級執行模型。理由：「Bot 重啟不常發生，會議重跑幾分鐘可接受」——卡住要人工介入才是痛點。

**明確放棄**：中斷點續跑（太複雜，不值得）。整場重開。

### 實作步驟

1. **EF Migration：`TaskGroup` 新增 `ActiveMeetingType` 欄位**
   - 型別：`string?`（nullable）
   - 值域：`"Kickoff"` / `"Design"` / `null`
   - Migration 指令：
     ```
     dotnet ef migrations add AddActiveMeetingType --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
     ```

2. **`TaskGroupService.HandleKickoffAsync` / `HandleDesignAsync` set/clear**
   - 開始時：`group.ActiveMeetingType = "Kickoff"`（或 `"Design"`）+ `SaveChangesAsync`
   - 結束時（不論成功 / 失敗 / 例外）：`finally { group.ActiveMeetingType = null; SaveChangesAsync; }`

3. **新增 `RecoverStuckMeetingsAsync`**
   - 放置：`TaskGroupService` 或獨立 `MeetingRecoveryService`
   - Bot 啟動時呼叫（在 `Program.cs` / HostedService 中，位置參考 `AgentQueueProcessor.RecoverStuckTasksAsync` 的呼叫點）
   - 掃 `db.TaskGroups.Where(g => g.ActiveMeetingType != null)`
   - 針對每個 Group，依 `ActiveMeetingType` 呼叫 `HandleKickoffAsync` / `HandleDesignAsync` 重跑
   - Log warning：`"Recovered {N} stuck meetings: {MeetingType} × {GroupId}"`

### 驗收情境

- 手動製造中斷：MockMode 觸發 Kickoff，在會議跑到一半時 Ctrl+C 殺 Bot（或改 ActiveMeetingType 欄位後重啟）
- Bot 啟動後觀察 log，確認 `RecoverStuckMeetingsAsync` 有掃到並重跑
- 會議跑完後 `ActiveMeetingType` 應被 clear

---

## 項目三：Appeal 對抗紀錄 UI 呈現（FF 十八）

### 背景

Stage 23 / 24 已在 DB 存完整對抗紀錄，**Dashboard 完全沒呈現**（Grep Dashboard + Shared DTOs 零引用）：

| 欄位 | 內容 |
|------|------|
| `TaskGroup.ReviewAppealLog` | 每輪 Cody 回應 + Vera 重評 + Petra 仲裁（Markdown）|
| `TaskGroup.ReviewAppealRoundA` | Review Appeal 輪次計數 |
| `TaskGroup.DevPlanAppealLog` | 每輪 Cody 反駁 + Petra 重評（Markdown）|
| `TaskGroup.DevPlanAppealRoundA` | Dev_plan Appeal 輪次計數 |

Stage 30 上線後對抗資訊量會增加（Cody / Vera / Petra 都帶 codebase 脈絡進場），沒 UI 呈現就白白浪費這些資料。

### 實作步驟

1. **`TaskGroupDto` 擴充 4 個欄位**
   - `string? ReviewAppealLog`
   - `int ReviewAppealRoundA`
   - `string? DevPlanAppealLog`
   - `int DevPlanAppealRoundA`
   - 所有 `TaskGroup → TaskGroupDto` 的 mapping 點同步更新

2. **Dashboard PipelineView 新增兩個折疊面板**（與既有歸檔報告 / 驗收報告 / 測試報告同一區塊）
   - 位置：Stage 26 已有 7 個折疊面板，加兩個變 9 個
   - 條件顯示：`!string.IsNullOrEmpty(group.ReviewAppealLog)` / `!string.IsNullOrEmpty(group.DevPlanAppealLog)`
   - 標題：
     - 「🗣️ Review Appeal 對抗紀錄（共 {N} 輪）」
     - 「🗣️ Dev_plan Appeal 對抗紀錄（共 {N} 輪）」
   - 內容：用 Markdown renderer（參考現有 ArchiveContent 呈現方式）直接吐 Log 全文

3. **無 DB / Migration 改動**（欄位早已存在）

### 驗收情境

- 跑 `/mock fail_review` 走完整 Review Appeal 流程
- Dashboard 流程詳情頁展開「Review Appeal 對抗紀錄」折疊面板
- 確認：逐輪 Cody 反駁 + Vera 重評 + Petra 仲裁 JSON/Markdown 完整呈現
- 同樣驗 `/mock fail_dev_plan` 的 Dev_plan Appeal 面板

---

## 子項順序建議

實作上這三項彼此獨立，可平行或任意順序。建議順序：

1. **項目三（Appeal UI）** — 最機械化，純 DTO + UI，先做暖身
2. **項目一（重試按鈕）** — 既有 Internal API 模式可抄
3. **項目二（Crash Recovery）** — 涉及 Migration + HostedService，留最後並獨立驗

---

## 版本

`v3.17.0 → v3.18.0`（minor bump，Stage 完成時遞增）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議（Aria 起手 briefing 用）

**推薦：Sonnet 200K（整個 Stage 一次做完）**

四維度評估：
- **Context 量**：輕（跨 3-4 個檔案，各子項局部）
- **邏輯複雜度**：輕-中（既有模式：Internal API、Migration、DTO 擴充、折疊面板都有範本）
- **風險代價**：中（項目二動 Bot 啟動流程，但範圍窄）
- **範本可用度**：高（Stage 27a Crash Recovery、Stage 29-3 Internal API、Stage 26 折疊面板都可抄）

Sonnet 200K 完全勝任。無需 Opus。

---

## 結案檢查清單

完成後記得「三件套」同步（見 `feedback_impl_session_briefing.md` 第五節）：

- ① Master Plan：header 版本 bump + 索引狀態 + changelog
- ② Future_Feature：header 版本 bump + 最後更新 + FF 十七 & 十八 移至已完成摘要
- ③ Stage_31_Roadmap：header 狀態 + 文件版本 v2.0 + 補「實作紀錄」章節 + 版本歷史

---

## 版本歷史

| 版本 | 日期 | 內容 |
|------|------|------|
| v1.0 | 2026-04-20 | 初版規劃書，合併 FF 十七 + FF 十八，三大項目 |
