# AiTeam

以 AI 驅動的軟體開發團隊管理系統。Christ 擔任老闆角色，透過 Discord 下達自然語言指令，AI 團隊（9 個 Agent）負責執行軟體開發與部署任務。

---

## 系統架構

```
你（老闆）
    ↓ Discord 自然語言（在 #victoria-ceo 說話）
CEO Agent（Victoria）—— 從 DB 動態載入 Agent 清單
    ├── Dev Agent（Cody）        （程式開發、Bug 修復、開 PR）
    ├── Ops Agent（Maya）        （部署監控、健康檢查告警）
    ├── QA Agent（Quinn）        （自動產生測試、開測試 PR）
    ├── Doc Agent（Sage）        （自動產出技術文件、開文件 PR）
    ├── Requirements Agent（Rosa）（需求拆解、建立 GitHub Issues）
    ├── Reviewer Agent（Vera）   （Code Review、在 GitHub PR 留審查意見）
    ├── Release Agent（Rena）    （版本管理、Changelog、建立 GitHub Release）
    └── Designer Agent（Demi）   （需求 → MudBlazor UI 規格文件）
         ↓
    結果回報 Discord + 寫入 Notion + 詳細 log 寫入 PostgreSQL
```

即時狀態透過 **Blazor Dashboard** 可視化（SignalR 推送）。

### 核心工具

| 用途 | 工具 |
|------|------|
| 溝通介面 | Discord（Discord.Net）自然語言對話 |
| 規則與記憶 | Notion（Notion.Net） |
| 詳細 Log | PostgreSQL（EF Core + Npgsql） |
| 視覺化 | Blazor Server Dashboard（MudBlazor） |
| LLM | Anthropic Claude API |
| 排程 | Quartz.NET |
| 部署 | Docker Compose + GitHub Actions CI/CD |

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
│   ├── Configuration/           ← DiscordSettings、AgentSettings、GitHubSettings
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
├── Stage_6_Roadmap.md           ← ✅ 完成
├── Stage_7_Roadmap.md           ← ✅ 完成
└── Future_Feature.md            ← 未來功能候選清單
```

---

## Discord 頻道結構

```
📁 Software Team
  # victoria-ceo        ← 主要指令中心，用自然語言跟 CEO 說話
  # cody-dev            ← Dev Agent log，可直接指派任務
  # maya-ops            ← Ops Agent log，可直接指派任務
  # quinn-qa            ← QA Agent log，可直接指派任務
  # sage-doc            ← Doc Agent log，可直接指派任務
  # rosa-requirements   ← Requirements Agent log，可直接指派任務
  # vera-reviewer       ← Reviewer Agent log，可直接指派任務
  # rena-release        ← Release Agent log，可直接指派任務
  # demi-designer       ← Designer Agent log，可直接指派任務

📁 系統
  # 任務動態
  # 警報
  # 每日摘要
```

---

## 雙層確認機制

```
你對 CEO 說自然語言（在 #victoria-ceo）
    ↓
CEO Agent 解讀意圖 → 回報決策（Embed + ✅❌ 按鈕）
    ↓ 你核准
執行 Agent 說明即將操作 → 再次確認（Embed + ✅❌ 按鈕）
    ↓ 你核准
實際執行 → 結果回報 Discord + 寫入 Notion + PostgreSQL
```

> 亦可在各 Agent 專屬頻道直接說話，繞過 CEO 直接指派，CEO 頻道會收到 CC 通知。

---

## 動態 Agent 框架

新增 Agent 只需四步，**不需修改 CEO 或 Bot 框架任何程式碼**：

1. DB 新增 `AgentConfig` 記錄（`IsActive = false` 預設停用）
2. 實作 `XxxAgentService : IAgentExecutor`
3. `Program.cs` 加 `AddKeyedScoped<IAgentExecutor, XxxAgentService>(AgentNames.Xxx)`
4. Dashboard 切換 `IsActive = true` → CEO 下次呼叫時自動感知

---

## 部署架構（Production）

```
git push origin main
    ↓
GitHub Actions（ubuntu-latest）
  1. dotnet build + test
  2. Docker build → push to ghcr.io
    ↓
Self-hosted Runner（Windows 11 本機）
  3. docker compose pull
  4. docker compose up -d --force-recreate
```

- **Bot Image**：`ghcr.io/darkleong/aiteam-bot:latest`
- **Dashboard**：`http://localhost:5051`（區網可用 `192.168.x.x:5051`）
- **Secrets**：`C:\Users\darkl\aiteam\.env`（不進版控）

---

## Discord 斜線指令

| 指令 | 說明 |
|------|------|
| 自然語言 | 直接在 `#victoria-ceo` 說話，不需格式 |
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
dotnet user-secrets set "GitHub:PersonalAccessToken"     "你的 GitHub PAT"
dotnet user-secrets set "GitHub:Owner"                   "你的 GitHub 帳號"
dotnet user-secrets set "GitHub:DefaultRepo"             "預設 Repo 名稱"
```

**Dashboard：**

```bash
cd src/AiTeam.Dashboard

dotnet user-secrets set "Notion:ApiKey"                  "你的 Notion Integration Token"
dotnet user-secrets set "Notion:RulesDatabaseId"         "Notion Rules DB ID"
dotnet user-secrets set "Notion:AgentStatusDatabaseId"   "Notion Agent Status DB ID"
```

### 啟動（開發模式）

```bash
dotnet run --project src/AiTeam.AppHost
```

Aspire Dashboard 自動開啟，PostgreSQL、Bot、Blazor Dashboard 一併啟動。

### 啟動（Production）

```bash
cd ~/aiteam
docker compose --env-file .env up -d
```

---

## 開發進度

| Stage | 說明 | 狀態 |
|-------|------|------|
| Stage 1 | 設計與決策 | ✅ 完成 |
| Stage 2 | 基礎建設（Discord Bot、CEO Agent、Notion、PostgreSQL） | ✅ 完成 |
| Stage 3 | Dev Agent、Ops Agent、GitHub Webhook | ✅ 完成 |
| Stage 4 | Blazor Server Dashboard、SignalR 即時推送 | ✅ 完成 |
| Stage 5 | 動態 Agent 框架 + QA / Doc / Requirements Agent | ✅ 完成 |
| Stage 6 | Discord Vision、MudBlazor、Requirements 三層確認、E2E 驗收等 12 項強化 | ✅ 完成 |
| Stage 7 | Reviewer / Release / Designer Agent、CI/CD、自然語言對話、Agent 專屬頻道 | ✅ 完成 |
| Stage 8 | 規劃中 | 🔵 規劃中 |

---

## 編程規範

詳見 `docs/conventions/` 資料夾：

- `csharp.md` — C# 命名、結構、非同步規範
- `blazor.md` — Blazor 組件規範
- `ef-core.md` — EF Core 查詢優化、Repository 模式
- `api-design.md` — RESTful API 設計規範
