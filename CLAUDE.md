# AiTeam 專案指引

## 專案背景

AiTeam **v4.0.0 純記錄系統**（執行端 + 記錄端 分離架構）：

- **執行端**：Christ 本機 Claude Code Agent Team（v2.1.32+ / `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1`）
- **記錄端**：AiTeam Bot 容器（MCP server endpoint / HTTP / Bearer auth / `/mcp` route）+ PostgreSQL + Blazor Dashboard
- **Lead persona**：`agents/petra-pm.md`（Petra / model=opus / Claude Code Agent Team lead）
- **執行流程**：Christ → `claude --agent petra-pm` → Petra spawn teammate（natural language）→ teammate work + call MCP record tool 寫 AiTeam DB
- **部署**：Windows 11 本機 Docker Compose（非雲端）

> v4.0.0 之前的 v5.5 6 Talent / HITL / Aria-Forge 工作模式：請查 git log 看 v3.79.0 commit / 已全砍（v4-rewrite Stage 88-95）。

---

## 規劃文件

```
docs/
  Architecture.md             ← v4.0.0 系統架構全景（MCP server + 5 mcp_* 表 + Claude Code Agent Team 集成）
  planning/
    Phase_v4_Roadmap.md       ← v4-rewrite Phase 規劃書（Stage 88-95）
    Phase_v4_Execution_Log.md ← v4-rewrite 執行紀錄（含每 Stage 自決紀錄）
    Phase_v4_Stage94_E2E_Guide.md  ← 端到端驗證指南（Christ 本機跑）
    Phase_v4_Followup.md      ← v4 結案後 follow-up 候選清單
  conventions/                ← 編程規範
```

---

## 編程規範

實作前讀 `docs/conventions/`：

| 檔 | 用途 |
|---|---|
| `csharp.md` | C# 命名 / 結構 / 非同步 / Primary Constructor / ILogger |
| `blazor.md` | Blazor 組件 / @rendermode / SignalR 即時更新 |
| `mudblazor.md` | MudBlazor 8.x（必讀）|
| `ef-core.md` | EF Core 查詢 / PostgreSQL 例外 / Migration 流程 |
| `api-design.md` | RESTful API / Internal API / SignalR Hub |
| `refactor-sop.md` | 服務層大檔案拆解守則 |

UI 元件庫 = **MudBlazor 8.x**。

---

## 專案結構

```
AiTeam.slnx         ← 解決方案檔（注意 .slnx 不是 .sln）
  ├── agents/
  │   ├── petra-pm.md       ← Petra subagent definition（Christ copy 到 ~/.claude/agents/）
  │   └── .mcp.json.example ← Claude Code .mcp.json 範例
  └── src/
      ├── AiTeam.AppHost          ← Aspire 入口
      ├── AiTeam.ServiceDefaults  ← 共用遙測 / 健康檢查
      ├── AiTeam.Bot              ← MCP server endpoint + 記錄寫入 + Discord 通知
      │   ├── McpAuth/            ← Bearer auth middleware
      │   ├── McpTools/           ← 6 個 MCP tool（HealthCheckTool + RecordTools 5 method）
      │   ├── Services/RecordNotificationService.cs ← Discord push TaskUpdates channel
      │   └── ...
      ├── AiTeam.Dashboard        ← Blazor Web App / MudTable × 5 顯示 mcp_* 記錄
      ├── AiTeam.Data             ← EF Core DbContext / Records/ entity / Migrations
      └── AiTeam.Shared           ← 共用 DTO
```

建置：`dotnet build AiTeam.slnx`（從 repo root）

---

## MCP server endpoint

- **Route**：`/mcp`（Bot 容器內嵌、port 8080）
- **Transport**：HTTP（不是 stdio）
- **Auth**：Bearer token / `Authorization: Bearer {AgentSettings.InternalApiKey}`
- **Tools**：8 個
  - `HealthCheck` — Bot 連線 + DB ready 確認
  - `register_team` — Claude Code Agent Team session 註冊
  - `close_team` — Team 收尾（Status='closed' + ClosedAt）（v4.0.2 補）
  - `register_teammate` — Lead 或 member spawn 時註冊
  - `finish_teammate` — Teammate 結束（FinishedAt）（v4.0.2 補）
  - `record_task` — Task lifecycle（action: create / claim / complete / fail）
  - `record_message` — Teammate 對話 message（v4.0.1 由 `record_conversation` rename / 對齊 `AgentMessage` entity + `mcp_messages` table）
  - `record_token_usage` — LLM call token 消耗
- **Tech**：`ModelContextProtocol.AspNetCore` 1.3.0（Microsoft + Anthropic 官方合作 SDK）

Claude Code 端 `.mcp.json` 配置範例見 `agents/.mcp.json.example`。

---

## 部署環境

Windows 11 本機 Docker Compose：
- Bot / Dashboard / PostgreSQL 均本機容器
- Bot 容器內無法執行 host `docker` / `docker compose` 指令
- 涉及容器操作走 GitHub Actions self-hosted runner

**自動部署**：push to main → GitHub Actions self-hosted runner 自動 `docker compose build + up`。

---

## ops 配置改動 SoP

| 改動類型 | 路徑 | 生效時間 |
|---|---|---|
| Token limit / 系統設定 | Dashboard 設定頁 → reload-cache 自動觸發 | 5 分鐘內（不重啟）|
| docker-compose.prod.yml / appsettings.json 預設值 | commit + push → CI/CD `docker compose up -d --force-recreate` | push 後 ~5 分鐘 |

⚠️ **不要單獨 `docker restart aiteam-bot`** — restart 不 reload env / 必須 recreate。

### env naming convention

- **Bot 自己** service env：`AgentSettings__*` prefix（如 `AgentSettings__InternalApiKey`）
- **Dashboard 視 Bot 為外部 service** env：`Bot__*` prefix（如 `Bot__InternalApiKey` / `Bot__InternalUrl`）

---

## 版本號管理（SemVer）

格式 `MAJOR.MINOR.PATCH`：
- **patch**：hotfix
- **minor**：Stage 完成（每 Stage 通常 minor bump）
- **major**：架構層面重大改變（如 v4.0.0 = 純記錄系統重構）

修改：`src/Directory.Build.props` `<Version>` 標籤。
目前版本 / 最新 Stage 以 [`/CHANGELOG.md`](./CHANGELOG.md) 為準。

---

## EF Core Migration

新 Migration 指令：

```
dotnet ef migrations add {Name} --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
dotnet ef database update --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
```

**AiTeam 特殊紀律**：
- `startup-project` 必須 `src/AiTeam.Dashboard`（含 `Microsoft.EntityFrameworkCore.Design`）/ 用 `AppHost` 找不到 DLL
- 多 DbContext 必加 `--context AppDbContext`
- Bot 容器啟動自動 `MigrateAsync()` AppDbContext / push 後容器 recreate 自動套用

---

## 開發語言

程式碼註解使用繁體中文 / 變數與方法名使用英文。
