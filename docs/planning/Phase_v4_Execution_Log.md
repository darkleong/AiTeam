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

`3fb091b` — spike(stage88): C# MCP SDK + Claude Code remote MCP 相容性驗證通過

---

## Stage 89 — 砍舊架構

**開始**：2026-05-26（接 Stage 88 同日）

**範圍**：砍 Bot 6 Talent worker / LlmProviderFactory / HITL / Dashboard HITL/Talent 頁 / 對應 DI 註冊。DB schema entity 砍延後到 Stage 91（自決）。

### 砍範圍（74 檔）

**Bot 端（46 檔）**：
- `src/AiTeam.Bot/Orchestration/Petra/` 整個 directory（27 檔 / Petra orchestrator + sub-services + Skills + spike）
- `src/AiTeam.Bot/Agents/` 17 檔（LlmProviderFactory / AnthropicProvider / GeminiProvider / MockLlmProvider / TokenTrackingProvider / ClaudeCodeProxy / ClaudeCodeService / MockClaudeCodeService / IClaudeCodeService / CeoAgentService / CeoResponse / QaReport / AgentDescriptor / ILlmProvider / MeetingSubprocessFailureException / LlmApiFailureException）
- 保留：`TokenUsage.cs` + `TokenCostEstimator.cs`（Stage 91 重用）
- `src/AiTeam.Bot/Services/`：TalentMetaCache + PromptResolver + TalentDispatchLockService + TalentSkillModelResolver + InteractionService（5 檔）
- `src/AiTeam.Bot/Discord/Routing/`：ButtonCallbackRouter + RoutingTypes + PendingConfirmationStore（3 檔）
- `src/AiTeam.Bot/Discord/CommandHandler.cs`
- `src/AiTeam.Bot/Api/CeoCommandController.cs`
- `src/AiTeam.Bot/Configuration/WorkflowSettingsResolver.cs`
- `src/AiTeam.Bot/Resources/CLAUDE_{Victoria,Petra,Cody,Vera,Quinn,Sage}.md`（6 檔 / 6 Talent persona template）

**Dashboard 端（13 檔）**：
- `Pages/Tasks/` 全 6 檔（TasksHitl / TasksRedirect / TasksActive / TasksHistory / TasksInbox + .razor.cs）
- `Pages/Settings/SettingsTalents.razor` + `.razor.cs`
- `Pages/Settings/SettingsSkillPrompts.razor` + `.razor.cs`
- `Pages/Settings/SettingsTalentPrompts.razor` + `.razor.cs`
- `Pages/Interactions/` 全 3 檔（InteractionCard / TextInputDialog / InteractionsRedirect）
- `Pages/Monitoring/MonitoringAgents.razor` + `.razor.cs`
- `Pages/Home/` 全 6 檔（Home / AgentStatusCard / QuickCommandCard + 對應 .razor.cs）
- `Services/DashboardInteractionQueryService.cs`

**Data 層（6 檔）**：
- `Repositories/PetraInboxRepository.cs` + `PetraSessionRepository.cs` + `MemoryRepository.cs` + `PromptRepository.cs`
- `SeedContent/PetraPersonaSeed.cs` + `PetraPromptTemplate.cs`

**Tests（14 檔）**：
- `Bot.Tests/Spike/PetraSpikePrototypeTests.cs`
- `Bot.Tests/Orchestration/` 11 檔（Petra* / Stage7X* / SubtaskPlan / ClaudeCodeChatClient / PromptRepository）
- `Bot.Tests/Agents/ClaudeCodeServiceParseJsonOutputTests.cs` + `TokenTrackingProviderTests.cs`

### 改檔（7 檔）

- `Bot/Program.cs` — DI 註冊大砍（CeoAgentService / Petra orchestrator 6 service / PetraInboxChannel + Processor + DispatchWorker / PlanConfirmationProcessor / MemoryRepository / PromptRepository / PromptResolver / TalentSkillModelResolver / TalentDispatchLockService / Skill+Talent factory / ClaudeCodeService / MockClaudeCodeService / ClaudeCodeProxy / IClaudeCodeService / CommandHandler / InteractionService / ButtonCallbackRouter / PendingConfirmationStore / TalentMetaCache / Anthropic SDK / Gemini HttpClient / LlmProviderFactory）+ DbSeeder.WarmupAsync 砍
- `Bot/AiTeam.Bot.csproj` — 砍 Anthropic.SDK 5.10.0 / Microsoft.Agents.AI 1.3.0 × 3 package / Resources Content glob 砍
- `Bot/Configuration/WorkflowSettings.cs` — 砍 14 個 v4/v5 flag、只留 AlertRateLimitMinutes
- `Bot/Services/DiscordAlertService.cs` — 砍 WorkflowSettingsResolver dep、改 IOptions&lt;WorkflowSettings&gt; 直接讀
- `Bot/Discord/DiscordBotService.cs` — 砍 CommandHandler dep + RegisterCommandsAsync call + 6 Talent 專屬頻道 ensure（CEO/PM/Dev/Reviewer/Qa/Doc）
- `Bot/Api/InternalController.cs` — 砍 TalentMetaCache + PromptResolver + TalentSkillModelResolver ctor + reload-cache 內對應段
- `Data/DbSeeder.cs` — 整檔重寫（砍 Stage 67 EnsureTalentsAsync / Stage 72 EnsureSkillPromptsAsync / Stage 73 UpgradeSkillPromptsToV2Async + EnsurePetraTalentPromptAsync / 只留 Team default + Rules baseline + AppSettings TokenPricing）
- `Data/Extensions/DataServiceExtensions.cs` — 砍 PetraInbox + PetraSession repository 註冊
- `Dashboard/Program.cs` — 砍 DashboardInteractionQueryService 註冊
- `Dashboard/Components/Layout/NavMenu.razor` — 砍任務中心整 group + 首頁 link + /settings/talents / skill-prompts / talent-prompts / /monitoring/agents（Stage 92 補 Records 頁時重整全 NavMenu）

### 自決紀錄

1. **DB schema entity drop 延後到 Stage 91**：talents / SkillPrompt / TalentPrompt / PetraSession / PetraInbox / Memory / BossInteraction 等 entity 暫留（避免雙重 schema 改動 / 隨 Stage 91 新表設計一起做 Migration）
2. **NavMenu 暫精簡**（Stage 92 重整）：留下會通的 link 5 個（Tokens / Deployments / Health / Workflow / Tokens 守門 / System / Rules / Projects）、其他全砍
3. **InteractionService 整檔砍**（不是改造）：原 caller（CommandHandler / CeoCommandController）都砍 / Bot 端 0 caller / 不留 service。Stage 91 真的要寫 BossInteraction 時新造一個 record-focused service
4. **TokenLogService 留**（Stage 91 重用 token record 邏輯）
5. **GitHubService 留**（PR / Issue 操作 / 後續 phase 評估）
6. **OpsAgentService 留**（HealthCheckJob 依賴）

### Build / Test 結果

- `dotnet build AiTeam.slnx`：**Build succeeded** / 0 warnings
- 接續 Stage 90 起點是純 record system base，無 v4/v5 6 Talent / HITL 殘留

### 影響後續 Stage

- Stage 90：MCP server endpoint 在已 clean base 上加 — 不會有 reference 衝突
- Stage 91：DB schema 新表設計時、順帶 drop talents / SkillPrompt / TalentPrompt / PetraSession / PetraInbox / Memory entity + Migration
- Stage 92：Dashboard 重整 NavMenu + 補 Records 頁 + 首頁 placeholder

### Commit

`b7b83b8` — chore(stage89): 砍舊架構（17,142 行刪 / 105 行新）+ push v4-rewrite branch

---

## Stage 90 — MCP server endpoint

**開始 / 完成**：2026-05-26

**範圍**：Bot 容器內嵌 MCP server endpoint、HTTP transport、Bearer auth、minimal HealthCheckTool 驗接通。

### 新檔（4 檔）

- `src/AiTeam.Bot/McpTools/HealthCheckTool.cs` — `[McpServerToolType]` class + `[McpServerTool]` HealthCheck method（DI 自動注入 AppDbContext / 驗 DB.CanConnectAsync + Bot uptime + UTC time）
- `src/AiTeam.Bot/McpAuth/McpBearerAuthMiddleware.cs` — 對 `/mcp*` path 強制 `Authorization: Bearer {InternalApiKey}` / 通過 → 入 MCP pipeline / 失敗 → 401（API key 未配 → 503）
- `Phase_v4_Execution_Log.md` Stage 90 段（本檔）

### 改檔（5 檔）

- `src/AiTeam.Bot/AiTeam.Bot.csproj` — 加 `ModelContextProtocol.AspNetCore` 1.3.0
- `src/AiTeam.Bot/Program.cs` — 加 using McpAuth + McpTools + `builder.Services.AddMcpServer().WithHttpTransport().WithTools<HealthCheckTool>()` + `app.UseMiddleware<McpBearerAuthMiddleware>()` + `app.MapMcp("/mcp")`
- `src/AiTeam.Bot/appsettings.json` — 砍 6 Talent specific Discord.Channels（CeoChannel/PmChannel/DevChannel/ReviewerChannel/QaChannel/DocChannel）+ 整 Gemini section + Agents.Petra section + WorkflowSettings v4/v5 dead flag（TargetVersion / UseV5Memory / V5MemoryCompact* / UseV5SubtaskPlanning / UseV5PromptDb / PausedSessionTimeoutHours）
- `docker-compose.prod.yml` — 同樣砍 6 Talent channel env / Anthropic__ApiKey / Gemini__* / AgentSettings__SkipCeoConfirm

### 設計拍板

| 項 | 決策 | 理由 |
|---|---|---|
| API key | 重用 `AgentSettings.InternalApiKey` | Dashboard / GitHub Actions / docker-compose 已配 / 不另開新 env var / lean |
| Port | 不另開 / 用 Bot 既有 ASP.NET Core port | Aspire dev 5050 / Docker prod 5052→8080 / MCP route `/mcp` 同 port |
| Route prefix | `/mcp` | 標準 / 對齊 sample |
| Tool 註冊 | `[McpServerToolType] + [McpServerTool]` attribute | 官方 sample pattern / DI 自動處理 / 0 樣板碼 |

### 自決紀錄

1. **curl 自驗延後**：Stage 90 build 通即過、curl 自驗合併到 Stage 94 端到端驗證（避免 Stage 90 卡本機 PG 啟動）
2. **MCP server `Stateless`**：用 default（無 explicit `Stateless = false`）— Stage 91 record tool 不需 server-to-client sampling / 簡化
3. **CORS**：未配置（不開放 browser direct access / MCP client 是 Claude Code CLI / 無 CORS 需求）
4. **MCP_TOOL_TIMEOUT**：未設 / 用 default

### Build 結果

- `dotnet build AiTeam.slnx`：**0 Error / 54 Warning**（warning 全為 OpenTelemetry/MailKit 既有 NU1902 vulnerability / 不關 Stage 90）

### 影響後續 Stage

- Stage 91：在 `/mcp` 上加 4 個 record tool（register_team / record_task / record_conversation / record_token_usage）+ DB schema 新表 + entity drop
- Stage 94：用 curl + Bearer 驗 `/mcp` endpoint 通 / 同時測完整 health_check tool call

### Commit

`314292e` — feat(stage90): MCP server endpoint — ModelContextProtocol.AspNetCore + Bearer auth + /mcp route + HealthCheckTool

---

## Stage 91 — MCP record tools + DB schema 新表

**開始 / 完成**：2026-05-26

**範圍**：5 個新 entity（Agent* prefix）+ 5 個 MCP record tool + EF Migration 生成。

### 新檔（3 檔）

- `src/AiTeam.Data/Records/RecordEntities.cs` — 5 個 entity（AgentTeam / AgentTeammate / AgentTask / AgentMessage / AgentTokenUsage）
- `src/AiTeam.Bot/McpTools/RecordTools.cs` — `[McpServerToolType]` class 內含 5 個 `[McpServerTool]` method
- `src/AiTeam.Data/Migrations/20260526054149_Stage91McpRecordSchema.cs` + Designer — EF 自動生成

### 改檔（2 檔）

- `src/AiTeam.Data/AppDbContext.cs` — 加 5 個 DbSet + 5 個 OnModelCreating 段（用 mcp_* table prefix）
- `src/AiTeam.Bot/Program.cs` — `.WithTools<RecordTools>()` 註冊

### Schema 設計

| Entity | Table | 用途 |
|---|---|---|
| AgentTeam | mcp_teams | Claude Code Agent Team execution session（lead 命名 / Status active/closed） |
| AgentTeammate | mcp_teammates | Team 內 individual teammate（Role lead/member、Model、SpawnedAt/FinishedAt） |
| AgentTask | mcp_tasks | Task lifecycle current state（Status pending/in_progress/completed/failed、DependenciesJson、ErrorMessage） |
| AgentMessage | mcp_messages | Teammate 對話 message（Role user/assistant/tool、Content、ToolCallJson） |
| AgentTokenUsage | mcp_token_usage | LLM call token 消耗（Input/Output/CacheCreation/CacheRead Tokens + EstimatedCostUsd） |

### MCP Tool 設計

| Tool | Args | Returns |
|---|---|---|
| register_team | name, description? | team_id |
| register_teammate | teamId, name, model?, role? | teammate_id |
| record_task | action(create/claim/complete/fail), teamId/taskId, ... | task_id 或狀態字串 |
| record_conversation | teammateId, role, content, taskId?, toolCallJson? | message_id |
| record_token_usage | teammateId, inputTokens, outputTokens, taskId?, cacheCreation?, cacheRead?, model?, estimatedCostUsd? | usage_id |

### 自決紀錄

1. **加第 5 個 tool `register_teammate`**：原 Stage 91 task description 只列 4 個 tool / 但 teammate spawn 是運作必經事件 / 不寫等於資料不完整。對齊運作流程加入。
2. **record_task 用 action-based**（4 action 在 1 tool）：對齊 task description「1 個 record_task tool」/ 保持 5 個 tool 總數可控。Trade-off：違反「tool 單一職責」但保 5-tool minimal scope。
3. **舊 entity drop 延後到 Stage 95**：talents / SkillPrompt / TalentPrompt / PetraSession / PetraSessionMessage / TaskMemory / TalentMemory / PetraInbox + 對應 DbSet + Entities.cs class + Migration drop table — Stage 95 結案前一次處理（避免 Stage 91 cascade reference 修 DashboardAgentService / TokenLogService / TalentDto / MonitoringTokens 等多檔）。
4. **Entity / Table 命名**：Class 用 `Agent` prefix（區分既有 Team entity / 人員團隊）/ Table 用 `mcp_` prefix（強調 MCP write 來源）。

### Build 結果

- `dotnet build AiTeam.slnx`：**0 Error / 96 Warning**（warning 全為既有 NU1902 vulnerability + Playwright MSTEST0037）
- `dotnet ef migrations add Stage91McpRecordSchema --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext`：**成功**

### 影響後續 Stage

- Stage 92：Dashboard 5 個 MudTable 表格頁顯示這 5 個表
- Stage 93：Discord notification 改造可用 RecordTools 觸發點（task completed / token milestone 等）
- Stage 94：端到端驗證跑 register_team → register_teammate → record_task → record_conversation → record_token_usage 完整鏈

### Commit

`bb242d8` — feat(stage91): MCP record tools + DB schema 新表

---

## Stage 92 — Dashboard 表格頁（minimal）

**開始 / 完成**：2026-05-26

**範圍**：1 頁 5 MudTab 顯示 5 個 mcp_* 表 + Home placeholder + NavMenu 重整。

### 新檔（3 檔）

- `src/AiTeam.Dashboard/Components/Pages/Records/Records.razor` — `@page "/records"` / MudTabs × 5 / MudTable × 5（Teams / Teammates / Tasks / Messages / Token Usage）
- `src/AiTeam.Dashboard/Components/Pages/Records/Records.razor.cs` — `[Inject] AppDbContext` / OnInitializedAsync 載 5 list / Take(100) + OrderByDesc
- `src/AiTeam.Dashboard/Components/Pages/Home/Home.razor` — placeholder（v4 純記錄系統介紹 + Records 入口 + Token 統計入口）

### 改檔（1 檔）

- `src/AiTeam.Dashboard/Components/Layout/NavMenu.razor` — 重整：首頁 + MCP Records + 監控中心（3）+ 設定中心（5）

### 設計拍板

| 項 | 決策 | 理由 |
|---|---|---|
| 表格結構 | 1 頁 5 MudTab | 對齊拍板 #12「新增 1 頁」/ NavMenu 1 link |
| Data source | 直接 AppDbContext.AsNoTracking() | minimal scope / 無 Repository / 無 service 層 |
| 顯示量 | Take(100) | 防大量資料卡 UI / 無 pagination（後 phase 重評）|
| 欄位顯示 | Guid 顯示前 8 字元 + ellipsis | 可讀性 / 完整 Guid 過長 |
| 時間顯示 | ToLocalTime() + "yyyy-MM-dd HH:mm:ss" | Christ 在 Windows / 本地時區友善 |
| 進階功能 | filter / sort / pagination 全延後 | 對齊拍板 #12「無其他功能」/ 視覺化 stats 全延後 |

### Build 結果

- `dotnet build AiTeam.slnx`：**0 Error**

### 影響後續 Stage

- Stage 93：Discord notification 改造 — RecordTools 內加 fire-and-forget push（minimal）
- Stage 94：端到端驗證 — 開 Records 頁看 MCP tool 寫入是否真到 DB

### Commit

待 commit："feat(stage92): Dashboard MCP Records 表格頁（1 頁 5 tab）+ Home placeholder + NavMenu 重整"

---
