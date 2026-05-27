# AiTeam

**Claude Code Agent Team 執行記錄系統** — Christ 本機跑 Petra（Project Manager）拆 task、spawn cody（worker）執行、全程記錄到 PostgreSQL / Dashboard + Discord 即時看。

> **目前版本與最新狀態見 [CHANGELOG.md](./CHANGELOG.md)。**
> 系統演進歷史 + 各 Stage 細節見 [`docs/planning/`](./docs/planning/) 與 [`docs/_archive/`](./docs/_archive/)。
> v4.0.0 前 v5.5 6 Talent 架構：請查 git history（v3.79.0 commit 為界 / v4-rewrite Stage 88-95 全砍）。

---

## 系統架構（v4 純記錄系統 / 執行 + 記錄分離）

```
┌─────────────────────────────────────────────────┐
│ Christ 本機 (Windows 11)                        │
│  Claude Code v2.1.32+ + agents/petra-pm.md      │
│                                                 │
│  $ claude --agent petra-pm                      │
│                                                 │
│  ┌─────────┐    ┌──────────┐  ┌──────────┐    │
│  │ Petra   │ ─→ │ cody-1   │  │ cody-2   │    │
│  │ (Lead)  │    │ (worker) │  │ (worker) │    │
│  │ Opus    │    │ Sonnet   │  │ Sonnet   │    │
│  └─────────┘    └──────────┘  └──────────┘    │
│           MCP tool call (HTTP / Bearer)         │
└─────────────────────────│───────────────────────┘
                          ▼
┌─────────────────────────────────────────────────┐
│ Docker Compose (Windows 11 / GH Actions runner) │
│                                                 │
│  AiTeam.Bot  ── MCP server endpoint (/mcp)      │
│              ── Bearer auth                     │
│              ── Discord push (任務動態 channel) │
│                                                 │
│  PostgreSQL  ── mcp_teams / mcp_teammates       │
│              ── mcp_tasks / mcp_messages        │
│              ── mcp_token_usage                 │
│                                                 │
│  Dashboard   ── Blazor + MudBlazor 8.x          │
│              ── /records (5 MudTable + SignalR) │
│              ── /monitoring (token trends)      │
└─────────────────────────────────────────────────┘
```

執行端 / 記錄端透過 **MCP HTTP** 連線（`/mcp` route + `Authorization: Bearer {InternalApiKey}`）。

---

## 核心技術 stack

| 層 | 技術 |
|---|---|
| **執行端**（本機）| Claude Code v2.1.32+ / `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1` |
| Lead persona | [`agents/petra-pm.md`](./agents/petra-pm.md) — Petra / model: opus / 拆 task + spawn teammate + 不直接 code |
| Worker subagent | `.claude/agents/cody.md`（本機 / gitignore）— cody / model: sonnet / 含 MCP record tool 自呼叫紀律 |
| MCP 配置 | [`agents/.mcp.json.example`](./agents/.mcp.json.example) — HTTP transport + Bearer auth |
| **記錄端**（Docker）| .NET 10 + Aspire（AppHost + ServiceDefaults） |
| MCP server | `ModelContextProtocol.AspNetCore` 1.3.0（Microsoft + Anthropic 官方）|
| Bot | ASP.NET Core 容器 / port 8080 / `/mcp` route + Discord push |
| Dashboard | Blazor Web App / MudBlazor 8.x / SignalR 即時推 |
| DB | PostgreSQL（5 個 `mcp_*` table）|
| **部署** | Docker Compose + GitHub Actions self-hosted runner（Christ 本機 Windows 11）|

---

## MCP tools（8 個）

| Tool | 用途 |
|---|---|
| `HealthCheck` | Bot 連線 + DB ready 確認 |
| `register_team` | Petra lead 開新 Agent Team session |
| `close_team` | Team 收尾（v4.0.2 補 / `Status='closed'` + `ClosedAt` / idempotent）|
| `register_teammate` | Lead 或 member spawn 時 |
| `finish_teammate` | Teammate 結束（v4.0.2 補 / `FinishedAt` / idempotent）|
| `record_task` | Task lifecycle（action: create / claim / complete / fail）|
| `record_message` | Teammate 對話 message turn |
| `record_token_usage` | 每次 LLM call 後 token 消耗 |

Bot → Discord push 4 個觸發點：📋 開 team / 🏁 收 team / ✅ task 完成 / ❌ task 失敗。
其他事件不 push（避免洗版 / token milestone 改每日 09:00 彙總）。

詳細工作流程 + DB schema 見 [docs/Architecture.md](./docs/Architecture.md)。

---

## 專案結構

```
AiTeam.slnx                          ← 解決方案（注意是 .slnx）
agents/
├── petra-pm.md                      ← Petra subagent definition（Christ copy 到 ~/.claude/agents/）
└── .mcp.json.example                ← Claude Code .mcp.json 範例
src/
├── AiTeam.AppHost                   ← Aspire 入口（PostgreSQL + Bot + Dashboard 編排）
├── AiTeam.ServiceDefaults           ← 共用遙測 + 健康檢查
├── AiTeam.Shared                    ← 共用 DTO
├── AiTeam.Data                      ← EF Core DbContext + Records/ entity + Migrations
├── AiTeam.Bot                       ← MCP server endpoint + 記錄寫入 + Discord 通知
│   ├── McpAuth/                     ← Bearer auth middleware
│   ├── McpTools/                    ← 8 個 MCP tool（HealthCheck + 7 record method）
│   └── Services/RecordNotificationService.cs ← Discord push
└── AiTeam.Dashboard                 ← Blazor + MudBlazor 8.x
tests/
└── AiTeam.Tests.Generated           ← xUnit 測試
docs/
├── README.md                        ← 資料夾導覽
├── Architecture.md                  ← v4 系統架構全景
├── planning/                        ← Phase v4 規劃 + Stage 7-87 歷史 Roadmap
├── conventions/                     ← 編程規範（必讀）
├── experiments/                     ← Self-implement 試驗紀錄
└── _archive/                        ← 歷史歸檔
```

建置：`dotnet build AiTeam.slnx`（從 repo root）。

---

## 快速開始

### 環境需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Claude Code](https://docs.anthropic.com/en/docs/claude-code) v2.1.32+（執行端 / 需 Node.js 22+）

### User Secrets（dev mode）

**Bot**：

```bash
cd src/AiTeam.Bot

dotnet user-secrets set "Discord:BotToken"             "你的 Discord Bot Token"
dotnet user-secrets set "Discord:GuildId"              "你的 Discord Server ID"
dotnet user-secrets set "AgentSettings:InternalApiKey" "MCP Bearer auth key（自訂）"
```

**Dashboard**：

```bash
cd src/AiTeam.Dashboard

dotnet user-secrets set "Bot:InternalApiKey" "與 Bot 同一把 key"
dotnet user-secrets set "Bot:InternalUrl"    "http://localhost:5052"
```

### 執行端 — Claude Code Agent Team

```powershell
$env:CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS = "1"
$env:AITEAM_MCP_URL = "http://localhost:5050/mcp"
$env:AITEAM_MCP_API_KEY = "與 Bot 同一把 key"

# 第一次：copy Petra subagent definition 到 user-level
Copy-Item agents/petra-pm.md ~/.claude/agents/petra-pm.md

claude --agent petra-pm
```

### 記錄端 — Aspire dev mode（local）

```bash
dotnet run --project src/AiTeam.AppHost
```

Aspire Dashboard 自動開啟 / PostgreSQL + Bot + Dashboard 一併啟動。

### 記錄端 — Docker Compose prod mode

```bash
cd ~/aiteam
docker compose --env-file .env up -d
```

- **Bot image**：`ghcr.io/darkleong/aiteam-bot:latest`
- **Dashboard**：`http://localhost:5051`（區網 / Tailscale Funnel 對外）
- **Secrets**：`C:\Users\darkl\aiteam\.env`（不進版控）

push to main → GitHub Actions self-hosted runner 自動 build + `docker compose up -d --force-recreate`（~5 分鐘）。

---

## 編程規範

實作前必讀 [`docs/conventions/`](./docs/conventions/)：

| 文件 | 內容 |
|---|---|
| `csharp.md` | C# 命名 / 結構 / 非同步 / Primary Constructor / ILogger |
| `blazor.md` | Blazor 組件 / @rendermode / SignalR 即時更新 |
| `mudblazor.md` | MudBlazor 8.x（必讀）|
| `ef-core.md` | EF Core 查詢 / PostgreSQL 例外 / Migration 流程 |
| `api-design.md` | RESTful API / Internal API / SignalR Hub |
| `refactor-sop.md` | 服務層大檔案拆解守則（Stage 34-36 + 59 + 84-87 SOP 累積）|

---

## 文件導覽

| 想看 | 去哪 |
|---|---|
| 版本變更紀錄 | [CHANGELOG.md](./CHANGELOG.md) |
| 系統架構全景 | [docs/Architecture.md](./docs/Architecture.md) |
| Petra subagent SoT | [agents/petra-pm.md](./agents/petra-pm.md) |
| Active 功能候選 | [docs/planning/Future_Feature.md](./docs/planning/Future_Feature.md) |
| Phase v4-rewrite 規劃 + 執行紀錄 | [Phase_v4_Roadmap.md](./docs/planning/Phase_v4_Roadmap.md) + [Phase_v4_Execution_Log.md](./docs/planning/Phase_v4_Execution_Log.md) |
| Phase v4 follow-up 清單 | [Phase_v4_Followup.md](./docs/planning/Phase_v4_Followup.md) |
| 端到端驗證指南 | [Phase_v4_Stage94_E2E_Guide.md](./docs/planning/Phase_v4_Stage94_E2E_Guide.md) |
| Stage 詳細實作（v3-v5 歷史 + v4-rewrite）| [Stage_*_Roadmap.md](./docs/planning/) |
| Self-implement 試驗（v5 時代）| [docs/experiments/](./docs/experiments/) |
| 早期設計歸檔 | [docs/_archive/](./docs/_archive/) |
