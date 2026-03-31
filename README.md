# AiTeam

以 AI 驅動的軟體開發團隊管理系統。Christ 擔任老闆角色，透過 Discord 下達指令，AI 團隊（CEO / Dev / Ops / QA / Doc / Requirements Agent）負責執行軟體開發與部署任務。

---

## 系統架構

```
你（老闆）
    ↓ Discord 指令
CEO Agent（總指揮）—— 從 DB 動態載入 Agent 清單
    ├── Dev Agent          （程式開發、Bug 修復、開 PR）
    ├── Ops Agent          （部署監控、健康檢查告警）
    ├── QA Agent           （自動產生測試、開測試 PR）
    ├── Doc Agent          （自動產出技術文件、開文件 PR）
    └── Requirements Agent （需求拆解、建立 GitHub Issues）
         ↓
    結果回報 Discord + 寫入 Notion + 詳細 log 寫入 PostgreSQL
```

即時狀態透過 **Blazor Dashboard** 可視化（SignalR 推送）。

### 核心工具

| 用途 | 工具 |
|------|------|
| 溝通介面 | Discord（Discord.Net） |
| 規則與記憶 | Notion（Notion.Net） |
| 詳細 Log | PostgreSQL（EF Core + Npgsql） |
| 視覺化 | Blazor Server Dashboard（Telerik UI） |
| LLM | Anthropic Claude API |
| 排程 | Quartz.NET |
| 部署 | Docker Compose on Windows 11（Aspire 編排） |

---

## 專案結構

```
AiTeam.sln
src/
├── AiTeam.AppHost/              ← Aspire 入口（PostgreSQL + Bot + Dashboard 編排）
├── AiTeam.ServiceDefaults/      ← Aspire 共用遙測、健康檢查設定
├── AiTeam.Shared/               ← 共用 DTO、介面、常數（AgentNames 等）
├── AiTeam.Data/                 ← EF Core DbContext、Entities、Repositories、Migrations
├── AiTeam.Bot/                  ← Discord Bot 主程式
│   ├── Agents/                  ← IAgentExecutor、各 AgentService、CeoAgentService
│   ├── Configuration/           ← DiscordSettings、AgentSettings
│   ├── Discord/                 ← DiscordBotService、CommandHandler
│   ├── GitHub/                  ← GitHubService、WebhookController
│   ├── Notion/                  ← NotionService
│   └── Ops/                     ← OpsAgentService、HealthCheckJob
└── AiTeam.Dashboard/            ← Blazor Server Dashboard
    ├── Components/Pages/        ← 首頁、任務中心、部署紀錄、Agent 設定、Team Office
    └── Services/                ← DashboardAgentService、NotionTrustLevelService
docs/
├── 00_Master_Plan.md
├── 01_Vision_and_Architecture.md
├── 02_Infrastructure.md
├── Stage_1_Design.md            ← ✅ 完成
├── Stage_2_Foundation.md        ← ✅ 完成
├── Stage_3_Agents.md            ← ✅ 完成
├── Stage_4_Dashboard.md         ← ✅ 完成
├── Stage_5_Expansion.md         ← ✅ 完成
└── Future_Research.md           ← 未來研究方向
```

---

## 雙層確認機制

```
你下指令（/task）
    ↓
CEO Agent 分析 → 回報決策（Embed + ✅❌ 按鈕）
    ↓ 你核准
執行 Agent 說明即將操作 → 再次確認（Embed + ✅❌ 按鈕）
    ↓ 你核准
實際執行 → 結果回報 Discord + 寫入 Notion + PostgreSQL
```

---

## 動態 Agent 框架

新增 Agent 只需四步，**不需修改 CEO 或 Bot 框架任何程式碼**：

1. DB 新增 `AgentConfig` 記錄（`IsActive = false` 預設停用）
2. 實作 `XxxAgentService : IAgentExecutor`
3. `Program.cs` 加 `AddKeyedScoped<IAgentExecutor, XxxAgentService>(AgentNames.Xxx)`
4. Dashboard 切換 `IsActive = true` → CEO 下次呼叫時自動感知

---

## Discord 斜線指令

| 指令 | 說明 |
|------|------|
| `/task project:[專案] description:[描述]` | 指派任務給 AI 團隊 |
| `/reload-rules` | 強制重新載入 Notion 規則（清除 Cache） |
| `/status` | 查詢各 Agent 目前狀態與啟用清單 |

---

## 快速開始

### 環境需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### 設定 User Secrets

**Bot：**

```bash
cd src/AiTeam.Bot

dotnet user-secrets set "Discord:BotToken"               "你的 Discord Bot Token"
dotnet user-secrets set "Discord:GuildId"                "你的 Discord Server ID"
dotnet user-secrets set "Anthropic:ApiKey"               "你的 Anthropic API Key"
dotnet user-secrets set "Notion:ApiKey"                  "你的 Notion Integration Token"
dotnet user-secrets set "Notion:RulesDatabaseId"         "Notion Rules DB ID"
dotnet user-secrets set "Notion:TaskSummaryDatabaseId"   "Notion Task Summary DB ID"
dotnet user-secrets set "Notion:AgentStatusDatabaseId"   "Notion Agent Status DB ID"
dotnet user-secrets set "GitHub:Token"                   "你的 GitHub PAT"
```

**Dashboard：**

```bash
cd src/AiTeam.Dashboard

dotnet user-secrets set "Notion:ApiKey"                  "你的 Notion Integration Token"
dotnet user-secrets set "Notion:RulesDatabaseId"         "Notion Rules DB ID"
dotnet user-secrets set "Notion:AgentStatusDatabaseId"   "Notion Agent Status DB ID"
```

### 啟動

```bash
dotnet run --project src/AiTeam.AppHost
```

Aspire Dashboard 自動開啟，PostgreSQL、Bot、Blazor Dashboard 一併啟動。EF Core Migration 與初始 Agent seed 資料會在啟動時自動套用。

---

## 開發進度

| Stage | 說明 | 狀態 |
|-------|------|------|
| Stage 1 | 設計與決策 | ✅ 完成 |
| Stage 2 | 基礎建設（Discord Bot、CEO Agent、Notion、PostgreSQL） | ✅ 完成 |
| Stage 3 | Dev Agent、Ops Agent、GitHub Webhook | ✅ 完成 |
| Stage 4 | Blazor Server Dashboard、SignalR 即時推送 | ✅ 完成 |
| Stage 5 | 動態 Agent 框架 + QA / Doc / Requirements Agent | ✅ 完成 |

---

## 編程規範

詳見 `docs/conventions/` 資料夾：

- `csharp.md` — C# 命名、結構、非同步規範
- `blazor.md` — Blazor 組件規範
- `ef-core.md` — EF Core 查詢優化、Repository 模式
- `api-design.md` — RESTful API 設計規範
- `telerik.md` — Telerik 組件使用規範
