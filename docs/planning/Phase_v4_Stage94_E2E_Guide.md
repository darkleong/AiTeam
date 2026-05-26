# Phase v4 Stage 94 — 端到端驗證指南

> Christ 本機跑這份指南、驗證 v4-rewrite 完整鏈：
> Claude Code Agent Team → MCP record tools → AiTeam DB → Dashboard 看得到記錄 + Discord 收到通知。
>
> 對應 Stage 94 task 拍板。

---

## 0. 前置檢查

### 0.1 Claude Code 版本
```powershell
claude --version  # 必須 v2.1.32+（Agent Teams 需要）
```

### 0.2 啟用 Agent Teams（experimental flag）
```powershell
$env:CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS = "1"
```

或永久寫到 `~/.claude/settings.json`：
```json
{
  "env": {
    "CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS": "1"
  }
}
```

---

## 1. 啟動 AiTeam 後端

### 1.1 Aspire AppHost 啟動（含 PostgreSQL container）
```powershell
dotnet run --project src/AiTeam.AppHost
```

啟動後：
- Bot 在 `http://localhost:5050`（MCP endpoint `/mcp`）
- Dashboard 在 `http://localhost:5051`
- PostgreSQL container 自動建（Aspire 管）

### 1.2 確認 Migration 套用（含 Stage 91 mcp_* 5 個表）
Bot 啟動時自動跑 `db.Database.MigrateAsync()`。若有問題、手動跑：
```powershell
dotnet ef database update --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
```

### 1.3 設 InternalApiKey
透過 Dashboard 設定頁、或 user secrets：
```powershell
dotnet user-secrets set "AgentSettings:InternalApiKey" "your-secret-key" --project src/AiTeam.Bot
```

---

## 2. 配置 Claude Code 端

### 2.1 設環境變數（給 .mcp.json 用）
```powershell
$env:AITEAM_MCP_URL = "http://localhost:5050/mcp"
$env:AITEAM_MCP_API_KEY = "your-secret-key"   # 跟 1.3 同
```

### 2.2 配 .mcp.json（project scope）
在你的工作 project 根目錄 / copy `agents/.mcp.json.example` 為 `.mcp.json`：
```json
{
  "mcpServers": {
    "aiteam-records": {
      "type": "http",
      "url": "${AITEAM_MCP_URL:-http://localhost:5050/mcp}",
      "headers": {
        "Authorization": "Bearer ${AITEAM_MCP_API_KEY}"
      },
      "alwaysLoad": true,
      "timeout": 30000
    }
  }
}
```

### 2.3 配 Petra subagent definition
把 `agents/petra-pm.md` copy 到：
- `~/.claude/agents/petra-pm.md`（user scope / 全 project 可用）/ 或
- `.claude/agents/petra-pm.md`（project scope / 限該 project）

---

## 3. 啟動 Claude Code with Petra as lead

```powershell
claude --agent petra-pm
```

啟動 header 應顯示 `@petra-pm`（active agent）。

---

## 4. 驗證 task：給 Petra 一個 hello-world 任務

對 Petra 講：
> 妳幫我組一個 team 做 hello world 測試。組 3 個 teammate 各做一件小事（譬如各跑一個 echo 命令）、全部記錄到 AiTeam。

Petra 應該（依 petra-pm.md 第 1-11 步）：
1. 呼叫 `register_team` 寫 1 筆 `mcp_teams`
2. 呼叫 `register_teammate` 註冊自己（lead）
3. 呼叫 `record_task` × 3（create 3 個 task）
4. natural language spawn 3 個 teammate
5. 各 teammate `register_teammate`（member）
6. 各 teammate `record_task` claim + 工作 + complete
7. 期間 `record_conversation` 記對話
8. 期間 `record_token_usage` 記 token

---

## 5. 驗證結果

### 5.1 Dashboard 檢查
開 `http://localhost:5051/records` / 5 tab 應該看到 row：

| Tab | 期望 row 數 |
|---|---|
| Teams | 1（hello-world team）|
| Teammates | 4（petra-pm lead + 3 member）|
| Tasks | 3（status=completed）|
| Messages | N（依對話量 / 至少 6+：每 teammate 至少 user+assistant 各 1）|
| Token Usage | N（依 LLM call 次數）|

### 5.2 Discord 檢查
TaskUpdates channel（預設 #任務動態）應收到：
- 📋 新 Agent Team 開始 — hello-world (id=xxxxxxxx)
- ✅ Task 完成 — Task 1 名稱 (id=xxxxxxxx)
- ✅ Task 完成 — Task 2 名稱
- ✅ Task 完成 — Task 3 名稱

---

## 6. Troubleshooting

| 症狀 | 可能原因 | 解法 |
|---|---|---|
| MCP server 401 | API key 不一致 | 檢查 `$env:AITEAM_MCP_API_KEY` 跟 Bot `AgentSettings:InternalApiKey` 一致 |
| MCP server 404 | Bot 沒跑 / 跑在不同 port | curl `http://localhost:5050/health`（不需 auth / 應 200） |
| Petra 沒 call record tool | subagent definition 沒讀到 | 確認 `claude --agent petra-pm` startup header 顯示 `@petra-pm` |
| Petra 不知有 MCP tool | `.mcp.json` 沒讀到 | `/mcp` 在 Claude Code 內看連線狀態、`aiteam-records` 應為 connected |
| Records 表格空 | Migration 沒套 | 跑 1.2 的手動 migration 指令 |
| Discord 沒通知 | Bot Discord 連線失敗 / TaskUpdates channel 不存在 | 看 Bot log / Discord BotToken 設對嗎 / Channel 名「任務動態」存在嗎 |

---

## 7. 通過判定

3 點全綠 = Stage 94 通過、可進 Stage 95（main 切換）：

- [ ] Dashboard `/records` 5 個 tab 各有 row
- [ ] Discord TaskUpdates channel 收到 4 則通知（1 team + 3 task）
- [ ] curl `http://localhost:5050/health` 200 OK

---

## 8. 自動化驗證（optional / Petra 自己跑）

如果 Petra session 想自驗 MCP server 接通：
```powershell
curl -X POST http://localhost:5050/mcp `
  -H "Authorization: Bearer $env:AITEAM_MCP_API_KEY" `
  -H "Content-Type: application/json" `
  -H "Accept: application/json, text/event-stream" `
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

應回 JSON 列出 6 個 tool（HealthCheck + 5 record tool）。
