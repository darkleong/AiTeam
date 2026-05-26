# 系統架構全景

> 對應系統版本：**v4.0.0**（v4-rewrite Phase 結案後 / 2026-05-26）
> 最新狀態以 [`/CHANGELOG.md`](../CHANGELOG.md) 為準
> v4.0.0 = AiTeam 從「執行 + 記錄」轉「純記錄 MCP server」/ 執行端搬到 Claude Code Agent Team

---

## 目錄

1. [架構雙端](#架構雙端)
2. [執行端 — Claude Code Agent Team](#執行端--claude-code-agent-team)
3. [記錄端 — AiTeam MCP server + DB + Dashboard](#記錄端--aiteam-mcp-server--db--dashboard)
4. [MCP record tool 流程](#mcp-record-tool-流程)
5. [Discord notification](#discord-notification)
6. [關鍵程式碼位置索引](#關鍵程式碼位置索引)

---

## 架構雙端

```
┌────────────────────────────────────────────────────────────────┐
│ Christ 本機 (Windows 11)                                       │
│                                                                 │
│  Claude Code v2.1.32+ (CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1) │
│                                                                 │
│  $ claude --agent petra-pm                                     │
│                                                                 │
│  ┌──────────┐    ┌──────────┐  ┌──────────┐                   │
│  │ Petra    │ ─→ │ teammate │  │ teammate │                   │
│  │ (Lead)   │    │ (member) │  │ (member) │                   │
│  │ Opus 4.7 │    │ Sonnet   │  │ Sonnet   │                   │
│  └──────────┘    └──────────┘  └──────────┘                   │
│        │              │              │                         │
│        └──────────────┴──────────────┘                         │
│                       │                                         │
│              MCP tool call (HTTP / Bearer auth)                │
└───────────────────────│────────────────────────────────────────┘
                        ▼
┌────────────────────────────────────────────────────────────────┐
│ Docker Compose (Windows 11 本機 / GitHub Actions self-hosted)  │
│                                                                 │
│  ┌──────────────────────────────────────────────────────┐     │
│  │ AiTeam.Bot                                            │     │
│  │  ├─ MCP server endpoint (/mcp)                       │     │
│  │  │   ├─ HealthCheckTool                              │     │
│  │  │   └─ RecordTools (5 method)                       │     │
│  │  ├─ McpBearerAuthMiddleware (驗 InternalApiKey)       │     │
│  │  ├─ RecordNotificationService (Discord push)         │     │
│  │  └─ EF Core Migration runner (啟動 MigrateAsync)     │     │
│  └──────────────────────────────────────────────────────┘     │
│                                                                 │
│  ┌──────────────────────────────────────────────────────┐     │
│  │ PostgreSQL                                            │     │
│  │  ├─ mcp_teams / mcp_teammates                        │     │
│  │  ├─ mcp_tasks / mcp_messages / mcp_token_usage       │     │
│  │  └─ （+ 舊 v5.5 dead table / Phase_v4_Followup F1）  │     │
│  └──────────────────────────────────────────────────────┘     │
│                                                                 │
│  ┌──────────────────────────────────────────────────────┐     │
│  │ AiTeam.Dashboard (Blazor / MudBlazor 8.x)            │     │
│  │  └─ /records 頁 — MudTable × 5 顯示 mcp_* 記錄        │     │
│  └──────────────────────────────────────────────────────┘     │
└────────────────────────────────────────────────────────────────┘
```

---

## 執行端 — Claude Code Agent Team

### 啟動方式

```powershell
$env:CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS = "1"
$env:AITEAM_MCP_URL = "http://localhost:5050/mcp"
$env:AITEAM_MCP_API_KEY = "your-secret-key"
claude --agent petra-pm
```

### Lead persona

`agents/petra-pm.md`（git repo / Christ copy 到 `~/.claude/agents/petra-pm.md`）：
- name: petra-pm / model: opus
- 角色：Project Manager / 不直接 code / 拆 task / spawn teammate / 協調
- 工作流程 11 步（接 intent → register_team → register lead → 拆 task → spawn teammate → 派 task → 過程記錄 → 完成）
- 對 Christ 用繁體中文 / 不 yes-man / 有幽默感

### Spawn teammate（natural language）

Petra 不用 subagent definition file spawn / 用 natural language 描述：
> "Spawn a teammate named cody-1 with model sonnet, role: implement {task}. They should first call record_task action=claim with task_id={X} and teammate_id={Y}."

### `.mcp.json`（project scope / 自動繼承給 teammate）

```json
{
  "mcpServers": {
    "aiteam-records": {
      "type": "http",
      "url": "${AITEAM_MCP_URL:-http://localhost:5050/mcp}",
      "headers": { "Authorization": "Bearer ${AITEAM_MCP_API_KEY}" },
      "alwaysLoad": true,
      "timeout": 30000
    }
  }
}
```

---

## 記錄端 — AiTeam MCP server + DB + Dashboard

### MCP server endpoint

- **Tech**：`ModelContextProtocol.AspNetCore` 1.3.0（MS + Anthropic 官方 / `--prerelease`）
- **Route**：`/mcp`（Bot 容器內嵌 / port 8080）
- **Transport**：HTTP（不是 stdio）
- **Auth**：Bearer token / 重用 `AgentSettings.InternalApiKey`
- **Middleware**：`McpBearerAuthMiddleware` — 對 `/mcp*` 強制驗 Authorization header / 401 if 不對 / 503 if API key 未配

### DB schema（mcp_* 5 表）

| Table | Entity | 用途 |
|---|---|---|
| `mcp_teams` | `AgentTeam` | Claude Code Agent Team session（lead 命名 / Status active/closed） |
| `mcp_teammates` | `AgentTeammate` | Team 內 individual teammate（Role lead/member、Model、SpawnedAt/FinishedAt） |
| `mcp_tasks` | `AgentTask` | Task lifecycle current state（Status pending/in_progress/completed/failed） |
| `mcp_messages` | `AgentMessage` | Teammate 對話 message（Role user/assistant/tool、Content、ToolCallJson） |
| `mcp_token_usage` | `AgentTokenUsage` | LLM call token 消耗（Input/Output/Cache + EstimatedCostUsd） |

### Dashboard `/records` 頁

`Pages/Records/Records.razor` — 1 頁 5 MudTab × 5 MudTable / 每表 Take(100) + OrderByDesc。
無 filter / sort / pagination 進階功能（minimal scope / Phase_v4_Followup F3）。

---

## MCP record tool 流程

8 個 tool（`HealthCheckTool` + `RecordTools` 7 method）：

| Tool | Args | 觸發時機 |
|---|---|---|
| `HealthCheck` | （無 tool args / 自動 inject DbContext） | Christ / Petra 確認 server reachable |
| `register_team` | name, description? | Petra lead 開新 team |
| `close_team` | teamId | Team 收尾（v4.0.2 補 / 寫 Status='closed' + ClosedAt / idempotent）|
| `register_teammate` | teamId, name, model?, role? | Lead 或 member spawn 時 |
| `finish_teammate` | teammateId | Teammate 結束（v4.0.2 補 / 寫 FinishedAt / idempotent）|
| `record_task` | action(create/claim/complete/fail), teamId/taskId, ... | Task lifecycle 每個狀態變化 |
| `record_message` | teammateId, role, content, taskId?, toolCallJson? | Teammate 每個 message turn（v4.0.1 由 `record_conversation` rename / 對齊 `AgentMessage` entity + `mcp_messages` table）|
| `record_token_usage` | teammateId, inputTokens, outputTokens, taskId?, cacheCreation?, cacheRead?, model?, estimatedCostUsd? | 每次 LLM call 後 |

### Tool 註冊（attribute pattern）

```csharp
[McpServerToolType]
public sealed class RecordTools
{
    [McpServerTool, Description("...")]
    public static async Task<string> RegisterTeam(AppDbContext db, RecordNotificationService notify, string name, string? description = null)
    {
        // ...
    }
}
```

DI 自動 inject AppDbContext / RecordNotificationService / 對 LLM 隱藏。

---

## Discord notification

`RecordNotificationService`（Singleton / push TaskUpdates channel「任務動態」）：
- 觸發點 4 個：
  - `RegisterTeam` end → 📋 新 Agent Team 開始
  - `CloseTeam` end → 🏁 Agent Team 收尾（v4.0.2 補）
  - `RecordTask` complete → ✅ Task 完成
  - `RecordTask` fail → ❌ Task 失敗 + errorMessage
- Fire-and-forget（`_ = Task.Run(...)` / Discord 失敗不影響 record 寫入）
- 不 push：register_teammate、finish_teammate、record_task claim、record_message、record_token_usage（避免洗版 / Phase_v4_Followup F2 評估 token milestone push）

---

## 關鍵程式碼位置索引

| 元件 | 位置 |
|---|---|
| MCP server 註冊 | `src/AiTeam.Bot/Program.cs` `AddMcpServer().WithHttpTransport()` |
| MCP middleware | `src/AiTeam.Bot/McpAuth/McpBearerAuthMiddleware.cs` |
| MCP route map | `src/AiTeam.Bot/Program.cs` `app.MapMcp("/mcp")` |
| HealthCheck tool | `src/AiTeam.Bot/McpTools/HealthCheckTool.cs` |
| 5 record tool | `src/AiTeam.Bot/McpTools/RecordTools.cs` |
| Discord push | `src/AiTeam.Bot/Services/RecordNotificationService.cs` |
| Records 5 entity | `src/AiTeam.Data/Records/RecordEntities.cs` |
| AppDbContext DbSet | `src/AiTeam.Data/AppDbContext.cs:7-12`（5 個 AgentXxx DbSet） |
| Migration | `src/AiTeam.Data/Migrations/20260526054149_Stage91McpRecordSchema.cs` |
| Dashboard Records 頁 | `src/AiTeam.Dashboard/Components/Pages/Records/Records.razor` + `.razor.cs` |
| Petra subagent definition | `agents/petra-pm.md` |
| `.mcp.json` 範例 | `agents/.mcp.json.example` |
| E2E 驗證指南 | `docs/planning/Phase_v4_Stage94_E2E_Guide.md` |
