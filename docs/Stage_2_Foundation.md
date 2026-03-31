# Stage 2 — 基礎建設

> 所屬專案：AI 團隊實作總規劃  
> 狀態：✅ 已完成
> 最後更新：2026-03-31

---

## 目標

建立所有 Agent 共用的地基：Discord Bot、CEO Agent、Notion 串接、PostgreSQL 串接、雙層確認機制。

---

## 交付項目

- [x] Discord Bot 框架（接收指令、發送確認訊息）
- [x] CEO Agent 基本實作（接指令 → 分析 → 回報）
- [x] Notion API 串接（讀取規則含 Cache、寫入任務摘要）
- [x] PostgreSQL 串接（tasks / task_logs EF Core）
- [x] 雙層確認機制實作
- [x] `/reload-rules` 指令實作
- [x] `appsettings.json` 系統參數設定

---

## 技術選型

| 元件 | 技術 |
|------|------|
| Discord Bot 套件 | Discord.Net |
| LLM API | Anthropic Claude API |
| Notion 套件 | Notion.Net |
| Notion Cache TTL | 1 小時（可 `/reload-rules` 強制更新） |
| TTL 設定位置 | `appsettings.json` → `AgentSettings.NotionCacheTtlMinutes` |
| PostgreSQL ORM | EF Core + Npgsql Provider |
| Agent 清單 | 動態從資料庫載入（新增 Agent 不需改程式碼） |
| CEO 回應格式 | 固定 JSON 結構 |
| 後端框架 | ASP.NET Core + Aspire |
| 任務排程 | Quartz.NET |

---

## CEO Prompt 結構

```
System Prompt
  ├── 角色定義
  ├── 可用 Agent 清單（動態注入，從資料庫載入）
  ├── 規則清單（從 Notion Cache 拉取）
  └── 可自主執行清單（從 Notion Cache 拉取）

User Message
  ├── 當前專案清單（從 PostgreSQL 撈取）
  ├── 近期相關任務紀錄（從 PostgreSQL 撈取）
  └── 使用者輸入
```

## CEO 回應 JSON 格式

```json
{
  "reply": "收到，我來處理預約重疊的 Bug",
  "action": "delegate",
  "target_agent": "Dev",
  "task": {
    "title": "修復預約時間重疊 Bug",
    "project": "ProjectA",
    "description": "時間選 12:00 送出後顯示錯誤",
    "priority": "high"
  },
  "require_confirmation": true
}
```

---

## 服務架構

```
Discord Bot 程式（ASP.NET Core + Aspire）
  ├── DiscordBotService   → 接收指令、發送確認訊息
  ├── AgentService        → 呼叫 Claude API、解析 JSON 回應
  ├── NotionService       → 讀規則（含 Cache）、寫任務摘要
  └── TaskRepository      → EF Core 讀寫 tasks / task_logs
```

---

## 部署方式

| 環境 | 方式 |
|------|------|
| 開發時 | Visual Studio 直接跑 `AiTeam.AppHost` |
| 正式部署 | Docker Compose（Windows 11 + Docker Desktop）|

Aspire 可直接產出 `docker-compose.yml`，所有服務（Bot、PostgreSQL）跑在 container 內，伺服器重開機後自動重啟。實際安裝與設定交由 Claude Code 協助執行。

---

## 錯誤處理機制

### 錯誤重試策略

| 錯誤類型 | 處理方式 |
|----------|---------|
| API 暫時失敗（網路、超時） | 自動重試 3 次，失敗才通知你 |
| API 認證失敗（Token 過期） | 立刻通知你，重試無意義 |
| Agent 回傳格式錯誤 | 自動重試 1 次，失敗通知你 |
| GitHub 操作失敗 | 立刻通知你，可能影響 repo |
| Discord 發送失敗 | 靜默重試，改寫 log |

**核心原則：可以自動恢復的就重試，需要人介入的就立刻通知。**

### 任務執行中途失敗

| 情境 | 處理方式 |
|------|---------|
| 純讀取失敗（查規則、撈紀錄） | 自動重試，不影響已執行部分 |
| 寫入失敗（Notion、PostgreSQL） | 保留已完成部分，通知你哪個步驟失敗 |
| Git 操作失敗（commit、PR） | 立刻停止，通知你，等你指示，不自動回滾 |
| 部署失敗 | 立刻停止 + 通知你 + **自動回滾**到上一個穩定版本 |

---

## 通知管道

| 管道 | 負責內容 |
|------|---------|
| **Discord** | 即時警報、任務確認請求、狀態更新 |
| **Email** | 每日摘要、重要任務完成紀錄、Discord Bot 失效時的備援警報 |

Email 作為備援的原因：Discord Bot 本身若掛掉，Discord 通知就失效了，Email 基礎設施更穩定，幾乎不會同時失效。

**Email 套件：** MailKit  
**寄件方式：** 開發期用 Gmail SMTP，正式環境評估 SendGrid / Mailgun

---

## Token 用量控管策略

四個方向同時採用：

**1. Prompt 精簡（基本功）**
- 歷史任務紀錄：只撈最近 5 筆相關任務
- 規則清單：只帶入與本次任務相關的規則

**2. 定時用量報告（每日兩次）**
每天早上 9 點與晚上 9 點自動發送至 Discord：
```
[系統] Token 用量報告 — 2026-03-29 21:00
─────────────────────────
CEO   今日：2,450  本月：48,200
Dev   今日：5,120  本月：98,600
Ops   今日：  890  本月：15,300
─────────────────────────
本月總計：162,100 / 1,000,000
預估本月總用量：270,000（正常範圍）
```

**3. 用量警報（即時通知）**
當任一 Agent 達到設定門檻時，立刻發送 Discord 警報：
- 每個 Agent 可獨立設定每日上限與每月上限
- 達到 80% 時發出警告，達到 100% 時暫停該 Agent 自動觸發任務

設定位置 `appsettings.json`：
```json
"Agents": {
  "CEO": {
    "Provider": "Anthropic",
    "Model": "claude-sonnet-4-5",
    "DailyTokenLimitK": 10,
    "MonthlyTokenLimitK": 200
  },
  "Dev": {
    "Provider": "Anthropic",
    "Model": "claude-sonnet-4-5",
    "DailyTokenLimitK": 20,
    "MonthlyTokenLimitK": 400
  },
  "Ops": {
    "Provider": "Google",
    "Model": "gemini-2.0-flash",
    "DailyTokenLimitK": 5,
    "MonthlyTokenLimitK": 100
  }
}
```

**4. 每月總用量上限（硬限制）**
```json
"AgentSettings": {
  "MonthlyTokenLimitK": 1000,
  "DailyReportCron": "0 9,21 * * *"
}
```
達到總上限後暫停所有自動觸發任務，你主動下的指令仍然執行。

---

## 多 LLM 供應商設計

不同 Agent 可使用不同供應商的模型，平衡成本與品質。Agent 的模型設定已整合進上方的 `appsettings.json` Token 控管設定中，每個 Agent 獨立設定 Provider、Model 與用量上限。

### 程式架構（介面隔離）

```csharp
public interface ILlmProvider
{
    Task<string> CompleteAsync(string systemPrompt, string userMessage);
}

public class AnthropicProvider : ILlmProvider { ... }
public class GoogleProvider : ILlmProvider { ... }
```

新增供應商只需加一個 `ILlmProvider` 實作，不需要動 Agent 核心邏輯。

### 注意事項
- 換模型後需測試，確認 JSON 輸出格式符合預期
- 不同供應商對 System Prompt 的遵從度略有差異

---

---

## 實作重點紀錄

### 專案結構
- SDK 用 `Microsoft.NET.Sdk.Web`（非 Worker），才支援 HTTP Controller（Webhook 用）
- `nuget.config` 加 `<clear />`，繞過 Telerik NuGet 401 錯誤
- `AiTeam.Bot` 改為 ASP.NET Core Web Application，`Program.cs` 使用 `WebApplication.CreateBuilder(args)`

### 已知陷阱
- Discord.Net 的 API 是 `BulkOverwriteApplicationCommandAsync`（無尾 S）
- 斜線指令必須在 `OnReady()` 裡註冊，不能在 `ExecuteAsync` 裡（Guild 快取尚未就緒）
- `appsettings.json` 不可有 `"Urls"` 欄位，會與 Aspire 動態 Port 管理衝突
- `DailyReportCron` 需用 Quartz **6 欄**格式：`"0 0 9,21 * * ?"`（非標準 5 欄 cron）
- `TaskItem.TeamId` 需設為可為 null（否則插入 `Guid.Empty` 會觸發 FK 違反約束）

### 雙層確認機制設計注意
- `PendingConfirmation` record **不可儲存** `TaskRepository`
  - 原因：`HandleTaskCommandAsync` 的 `await using scope` 函式結束即 dispose
  - 修法：`confirm_yes` 處理時開新 scope 取新的 `TaskRepository`
- `exec_yes` 背景執行用 `Task.Run(..., CancellationToken.None)`
  - 原因：Discord interaction 的 CT 生命週期短，背景任務需獨立 CT

### Aspire 設定
- `AppHost.cs` 需加 `.WithHttpEndpoint(port: 5050, name: "webhook")` 讓 Aspire 管理 Webhook Port
- Bot 啟動時自動執行 `db.Database.MigrateAsync()` 套用所有 EF Core Migrations
- `WaitFor(postgres)` 確保 PostgreSQL 容器就緒後才啟動 Bot
