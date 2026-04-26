# AiTeam

以 AI 驅動的軟體開發團隊管理系統。Christ 擔任老闆角色，透過 Discord 下達自然語言指令，AI 團隊負責執行軟體開發與部署任務——從接需求、設計、實作、Code Review、測試、文件、到通知 merge PR，全流程自動閉環。

> **目前版本與最新狀態見 [CHANGELOG.md](./CHANGELOG.md)。**
> 系統演進歷史 + 各 Stage 細節見 [`docs/planning/`](./docs/planning/)。

---

## 系統架構

```
你（老闆）
    ↓ Discord 自然語言（在 #victoria-ceo 說話）
    或 Dashboard 操作中心（雙通道，先到先贏）
        │
CEO Agent（Victoria）—— 從 DB 動態載入 Agent 清單
    │
    ├── PM Agent（Petra）        （品質審核閘門：Rosa/Demi/Dev_plan/Vera 產出皆需過審）
    ├── Requirements Agent（Rosa）（需求拆解、建立 GitHub Issues）
    ├── Designer Agent（Demi）   （需求 → MudBlazor UI 規格）
    ├── Dev Agent（Cody）        （Claude Code CLI 自主開發、Bug 修復、開 PR）
    ├── QA Agent（Quinn）        （Claude Code 產生測試 + dotnet build 驗證）
    ├── Reviewer Agent（Vera）   （Claude Code session Code Review + 影響範圍分析）
    ├── Doc Agent（Sage）        （收尾歸檔員：CHANGELOG + archive）
    ├── Release Agent（Rena）    （版本管理、Git Release）
    ├── Ops Agent（Maya）        （部署監控、健康檢查告警）
    │
    └── 結果回報 Discord + Dashboard + 詳細 log 寫入 PostgreSQL
```

即時狀態透過 **Blazor Web App Dashboard** 可視化（SignalR 推送）。

### 核心工具

| 用途 | 工具 |
|------|------|
| 溝通介面 | Discord（Discord.Net）自然語言對話 + Dashboard 操作中心（雙通道） |
| 規則管理 | PostgreSQL `rules` 資料表（Dashboard CRUD） |
| 詳細 Log | PostgreSQL（EF Core + Npgsql） |
| 視覺化 | Blazor Web App Dashboard（MudBlazor 8.x，InteractiveServer） |
| LLM | Anthropic Claude API + Google Gemini API（Provider/Model 經 Dashboard 動態配置） |
| Claude Code CLI | Victoria / Cody / Vera / Quinn / Petra（session-based） |
| 排程 | Quartz.NET |
| 部署 | Docker Compose + GitHub Actions self-hosted runner |

---

## 專案結構

```
AiTeam.slnx                          ← 解決方案（注意是 .slnx）
src/
├── AiTeam.AppHost/                  ← Aspire 入口（PostgreSQL + Bot + Dashboard 編排）
├── AiTeam.ServiceDefaults/          ← Aspire 共用遙測、健康檢查
├── AiTeam.Shared/                   ← 共用 DTO、介面、常數
├── AiTeam.Data/                     ← EF Core DbContext、Entities、Repositories、Migrations
├── AiTeam.Bot/                      ← Discord Bot 主程式（含各 Agent 邏輯）
│   ├── Agents/                      ← 各 AgentService、ClaudeCodeService、TokenTrackingProvider
│   │   └── Pm/                      ← Petra 子模組（Stage 35 拆解後）
│   ├── Orchestration/               ← MeetingService / 流程協調 services
│   ├── Resources/                   ← CLAUDE_*.md（Agent 行為約束 template）
│   ├── Discord/                     ← DiscordBotService、CommandHandler、Routers
│   ├── Api/                         ← Internal API
│   └── Services/                    ← AppSettingsService、AgentQueueService 等
├── AiTeam.Dashboard/                ← Blazor Web App Dashboard
└── AiTeam.Tests.Playwright/         ← Playwright E2E 截圖測試
docs/
├── README.md                        ← 資料夾導覽入口
├── architecture/                    ← 願景、基礎建設、老闆角色描述
├── planning/                        ← 各 Stage Roadmap + Future_Feature
├── conventions/                     ← 編程規範（C# / Blazor / MudBlazor / EF Core / API / refactor-sop）
├── agents/                          ← Agent 角色 lore
├── experiments/                     ← Self-implement 試驗紀錄
└── _archive/                        ← 歷史歸檔
```

> 建置：`dotnet build AiTeam.slnx`（從 repo root）

---

## Discord 頻道結構

```
📁 Software Team
  # victoria-ceo        ← 主要指令中心，自然語言跟 CEO 說話
  # cody-dev / # petra-pm / # maya-ops / # quinn-qa / # sage-doc
  # rosa-requirements / # vera-reviewer / # rena-release / # demi-designer
  ↑ 各 Agent log + 可直接指派任務（CC 給 CEO）

📁 系統
  # 任務動態 / # 警報 / # 每日摘要
```

---

## 雙層確認機制

```
你對 CEO 說自然語言 (#victoria-ceo) 或從 Dashboard 操作中心
    ↓
CEO Agent 解讀意圖 → 提案／決策（Embed + ✅❌按鈕 / Dashboard 卡片）
    ↓ 你核准（Discord 或 Dashboard 任一端，先到先贏）
執行 Agent 說明即將操作 → 再次確認
    ↓ 你核准
實際執行 → 結果回報 Discord + Dashboard + PostgreSQL
```

> **SkipCeoConfirm**：Dashboard → 系統設定可開啟，跳過第一層 CEO 確認，5 分鐘內生效。

---

## 動態 Agent 框架

新增 Agent 只需四步，**不需修改 CEO 或 Bot 框架程式碼**：

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
Self-hosted Runner（Christ 本機 Windows 11）
  3. docker compose pull
  4. docker compose up -d --force-recreate
```

- **Bot Image**：`ghcr.io/darkleong/aiteam-bot:latest`
- **Dashboard**：`http://localhost:5051`（區網 / Tailscale Funnel 對外）
- **Secrets**：`C:\Users\darkl\aiteam\.env`（不進版控）

---

## Discord 斜線指令

| 指令 | 說明 |
|------|------|
| 自然語言 | 直接在 `#victoria-ceo` 說話，不需格式 |
| `/reload-rules` | 強制重新載入 DB 規則（清除記憶體快取） |
| `/status` | 查詢各 Agent 目前狀態與啟用清單 |
| `/new-session` | 清除 Victoria 對話 session（長期記憶不受影響） |
| `/mock` | Mock Mode 限定，觸發指定工作流程（多種情境） |
| `/pause <agent>` / `/resume <agent>` | 暫停/恢復指定 Agent 的任務佇列 |
| `/stop-all` / `/resume-all` | 全域緊急停止 / 恢復所有 Agent |
| `/queue` | 查看當前任務佇列狀態 |

---

## 快速開始

### 環境需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Node.js 22+（Claude Code CLI 需要）

### 設定 User Secrets

**Bot：**

```bash
cd src/AiTeam.Bot

dotnet user-secrets set "Discord:BotToken"               "你的 Discord Bot Token"
dotnet user-secrets set "Discord:GuildId"                "你的 Discord Server ID"
dotnet user-secrets set "Anthropic:ApiKey"               "你的 Anthropic API Key"
dotnet user-secrets set "Gemini:ApiKey"                  "你的 Google Gemini API Key（可選）"
dotnet user-secrets set "GitHub:PersonalAccessToken"     "你的 GitHub PAT"
dotnet user-secrets set "GitHub:Owner"                   "你的 GitHub 帳號"
dotnet user-secrets set "GitHub:DefaultRepo"             "預設 Repo 名稱"
dotnet user-secrets set "AgentSettings:InternalApiKey"   "Dashboard 呼叫 Bot 用的 API Key"
```

**Dashboard：**

```bash
cd src/AiTeam.Dashboard

dotnet user-secrets set "BotSettings:InternalApiKey"     "Dashboard 呼叫 Bot 用的 API Key"
dotnet user-secrets set "BotSettings:BaseUrl"            "http://localhost:5050"
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

## 編程規範

實作前必讀 [`docs/conventions/`](./docs/conventions/) 內所有規範文件：

| 文件 | 內容 |
|---|---|
| `csharp.md` | C# 命名、結構、非同步、Primary Constructor、ILogger |
| `blazor.md` | Blazor 組件規範、@rendermode、SignalR 即時更新 |
| `mudblazor.md` | MudBlazor 8.x 使用規範、常見陷阱（必讀） |
| `ef-core.md` | EF Core 查詢優化、PostgreSQL 例外處理、Migration 流程 |
| `api-design.md` | RESTful API、Internal API、SignalR Hub 設計規範 |
| `refactor-sop.md` | 服務層大檔案拆解守則（FF 二十實踐累積） |

---

## 文件導覽

| 想看的東西 | 去哪查 |
|---|---|
| 完整版本變更紀錄 | [CHANGELOG.md](./CHANGELOG.md) |
| 待實作功能候選 | [Future_Feature.md](./docs/planning/Future_Feature.md) |
| 系統願景與架構細節 | [docs/architecture/01_Vision_and_Architecture.md](./docs/architecture/01_Vision_and_Architecture.md) |
| 基礎建設（Discord 頻道、PostgreSQL 等） | [docs/architecture/02_Infrastructure.md](./docs/architecture/02_Infrastructure.md) |
| 各 Stage 詳細實作 | [docs/planning/Stage_*_Roadmap.md](./docs/planning/) |
| 老闆角色描述 | [docs/architecture/About_Boss.md](./docs/architecture/About_Boss.md) |
| Agent 角色 lore | [docs/agents/](./docs/agents/) |
| Self-implement 試驗紀錄 | [docs/experiments/](./docs/experiments/) |
