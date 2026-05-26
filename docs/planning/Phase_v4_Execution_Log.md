# Phase v4-rewrite Execution Log

> **執行模式**：Petra 全能模式、`bypassPermissions`、Christ 不介入細節
> **執行分支**：`v4-rewrite`
> **記錄紀律**：所有問題與決策即時記入、Christ 結束後查看

---

## 執行紀律（自我約束）

1. **每個 Stage 起始記錄**：日期、範圍、預計產出
2. **遇到問題即記**：問題、選項、決策、理由
3. **每 Stage 完成記錄**：實際產出、verify 結果、commit hash
4. **延後拍板項自決後記錄**：原本延後的（SemVer、舊資料、Dashboard 欄位等）、決策、理由
5. **不重複 Roadmap 內容**：只記過程中新增的決策與觀察

---

## Stage 88 — Spike: C# MCP SDK + Claude Code remote MCP 相容性驗證

**開始**：2026-05-26

**範圍**：
- 驗 C# .NET MCP SDK 成熟度（首選官方 / 社群 SDK、fallback 手寫 HTTP/SSE）
- 驗 Claude Code v2.1.32+ 是否支援 remote HTTP MCP server（不只 stdio transport）
- 產出 spike 報告寫入本檔

**預計產出**：
- 本檔內 Stage 88 結論段（SDK 推薦 + Claude Code 連線機制 + 影響 Stage 90 實作路徑）
- commit："spike(stage88): C# MCP SDK + Claude Code remote MCP 相容性驗證"

### 結論（2026-05-26 完成）

**兩個 P0 風險均解除、無 fallback 需求。**

#### 1. C# .NET MCP SDK — 官方 SDK 成熟、production-ready

- **官方 package**：`ModelContextProtocol.AspNetCore` 1.3.0（2026-05-08 release）
- **維護方**：Microsoft + Anthropic + MCP open protocol org 三方合作（非社群孤兒包）
- **三層 package**：
  - `ModelContextProtocol.Core` — 最小依賴（client / low-level server）
  - `ModelContextProtocol` — 主 package（hosting + DI extensions）
  - **`ModelContextProtocol.AspNetCore`** ← Stage 90 要用這個（HTTP server）
- **依賴**：`Microsoft.Extensions.AI`（MS 官方 AI abstraction）
- **目前狀態**：`--prerelease` flag 安裝（已穩定、36 releases、active maintenance）
- **裝法**：`dotnet add package ModelContextProtocol.AspNetCore --prerelease`

#### 2. Claude Code remote HTTP MCP — 官方完整支援

`.mcp.json` 配置完整支援我們設計：

```json
{
  "mcpServers": {
    "aiteam-records": {
      "type": "http",
      "url": "${AITEAM_MCP_URL:-http://localhost:5xxx/mcp}",
      "headers": {
        "Authorization": "Bearer ${AITEAM_MCP_API_KEY}"
      },
      "timeout": 30000
    }
  }
}
```

**關鍵特性**：
- 3 個 scope：local（personal）/ **project（git share / `.mcp.json`）** / user — Stage 94 走 project scope
- 環境變數展開 `${VAR}` / `${VAR:-default}` — API key 適合 env var 存
- 自動 reconnect（5 attempts / exponential backoff）
- 401/403 不 retry（auth 錯誤直接 fail）
- **Agent Teams 自動繼承 project-scoped MCP servers**（teammate 不用個別 config）

CLI 加 server（給 Stage 94 文件用）：
```bash
claude mcp add --transport http aiteam-records --scope project http://localhost:5xxx/mcp \
  --header "Authorization: Bearer YOUR_API_KEY"
```

### 影響 Stage 90 實作路徑

- **用官方 SDK**（不 fallback 手寫）— scope 大幅簡化
- **Bot 容器內嵌 MCP server**：既有 ASP.NET Core 服務直接加 `ModelContextProtocol.AspNetCore` middleware
- **Bearer token auth**：標準 ASP.NET Core middleware 驗 `Authorization` header（不用造輪子）
- **Port 配置**：建議用 `5000` 或 docker-compose 已配的非 80 port、route prefix `/mcp`

### 觀察 / 自決紀錄

- **MCP tool 定義方式**：GitHub README 沒給 code example、Stage 90 實作時 fetch `csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html` 拿完整 sample（不阻塞 spike 結論）
- **`alwaysLoad: true`** 配置可用、避免我們 4 個 record tool 被 deferred loading 機制隱藏、Stage 94 `.mcp.json` 範例加上

### 產出

- 本檔結論段
- 下一步進 Stage 89

### Commit

待 commit："spike(stage88): C# MCP SDK + Claude Code remote MCP 相容性驗證通過"

---
