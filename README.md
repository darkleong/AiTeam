# AiTeam

以 AI 驅動的軟體開發團隊管理系統。Christ 擔任老闆角色，透過 Discord 下達指令，AI 團隊（CEO / Dev / Ops Agent）負責執行軟體開發與部署任務。

---

## 系統架構

```
你（老闆）
    ↓ Discord 指令
CEO Agent（總指揮）
    ├── Dev Agent（開發助手）
    └── Ops Agent（部署管理）
         ↓
    結果回報 Discord + 寫入 Notion + 詳細 log 寫入 PostgreSQL
```

### 核心工具

| 用途 | 工具 |
|------|------|
| 溝通介面 | Discord（Discord.Net） |
| 規則與記憶 | Notion（Notion.Net） |
| 詳細 Log | PostgreSQL（EF Core + Npgsql） |
| LLM | Anthropic Claude API |
| 排程 | Quartz.NET |
| 通知備援 | MailKit（Gmail SMTP） |
| 部署 | Docker Compose on Windows 11 |

---

## 專案結構

```
AiTeam.slnx
src/
├── AiTeam.AppHost/              ← Aspire 入口（PostgreSQL + Bot 編排）
├── AiTeam.ServiceDefaults/      ← Aspire 共用遙測、健康檢查設定
└── AiTeam.Bot/                  ← Discord Bot 主程式
    ├── Agents/                  ← ILlmProvider、AnthropicProvider、CeoAgentService
    ├── Configuration/           ← DiscordSettings、AgentSettings
    ├── Data/                    ← EF Core DbContext、Entities、TaskRepository
    ├── Discord/                 ← DiscordBotService、CommandHandler
    ├── Migrations/              ← EF Core Migrations
    └── Notion/                  ← NotionService、NotionSettings
docs/
├── 00_Master_Plan.md
├── 01_Vision_and_Architecture.md
├── 02_Infrastructure.md
├── Stage_1_Design.md
├── Stage_2_Foundation.md        ← ✅ 已完成
├── Stage_3_Agents.md
├── Stage_4_Dashboard.md
└── Stage_5_Expansion.md
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

## Discord 斜線指令

| 指令 | 說明 |
|------|------|
| `/task project:[專案] description:[描述]` | 指派任務給 AI 團隊 |
| `/reload-rules` | 強制重新載入 Notion 規則（清除 Cache） |
| `/status` | 查詢各 Agent 目前狀態 |

---

## 快速開始

### 環境需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### 設定 User Secrets

```bash
cd src/AiTeam.Bot

dotnet user-secrets set "Discord:BotToken"   "你的 Discord Bot Token"
dotnet user-secrets set "Discord:GuildId"    "你的 Discord Server ID"
dotnet user-secrets set "Anthropic:ApiKey"   "你的 Anthropic API Key"
dotnet user-secrets set "Notion:ApiKey"      "你的 Notion Integration Token"
dotnet user-secrets set "Notion:RulesDatabaseId"       "Notion 規則庫 DB ID"
dotnet user-secrets set "Notion:TaskSummaryDatabaseId" "Notion 任務摘要 DB ID"
dotnet user-secrets set "Notion:AgentStatusDatabaseId" "Notion Agent 狀態 DB ID"
```

### 啟動

```bash
dotnet run --project src/AiTeam.AppHost
```

Aspire Dashboard 將自動開啟，PostgreSQL 與 Bot 一併啟動。資料庫 Migration 會在 Bot 啟動時自動套用。

---

## 開發進度

| Stage | 說明 | 狀態 |
|-------|------|------|
| Stage 1 | 設計與決策 | ✅ 完成 |
| Stage 2 | 基礎建設（Discord Bot、CEO Agent、Notion、PostgreSQL） | ✅ 完成 |
| Stage 3 | Dev Agent、Ops Agent、GitHub Webhook | ⏳ 待開始 |
| Stage 4 | Blazor Dashboard | ⏳ 待開始 |
| Stage 5 | 擴充更多 Agent | ⏳ 依需求展開 |

---

## 編程規範

詳見 `docs/conventions/` 資料夾：

- `csharp.md` — C# 命名、結構、非同步規範
- `blazor.md` — Blazor 組件規範
- `ef-core.md` — EF Core 查詢優化、Repository 模式
- `api-design.md` — RESTful API 設計規範
- `telerik.md` — Telerik 組件使用規範
