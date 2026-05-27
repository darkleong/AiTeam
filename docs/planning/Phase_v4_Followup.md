# Phase v4-rewrite Follow-up 候選清單

> Phase v4-rewrite 結案後留下的 follow-up 工作 / 後續 phase 評估 + 處理 / 不影響 v4.0.0 已 deliver 範圍。
> 對齊 Stage 95 結案紀律：scope 不擴大、follow-up 集中記錄、不混進結案 commit。

---

## 高優先（影響 DB / production 穩定性）

### F1：舊 entity drop + cascading reference 修

**範圍**：Stage 89 大砍時保留的舊 entity / table 真實 drop。

砍 entity（C# class）：
- `Talent` / `TalentSkill`（`Entities.cs`）
- `TaskMemory` / `TalentMemory`
- `SkillPrompt` / `TalentPrompt`
- `PetraSession` / `PetraSessionMessage`
- `PetraInbox`

對應 AppDbContext DbSet + OnModelCreating 段砍。

Migration drop table：
- `talents` / `talent_skills`
- `task_memories` / `talent_memories`
- `skill_prompts` / `talent_prompts`
- `petra_sessions` / `petra_session_messages`
- `petra_inbox`

cascading reference 修（grep 顯示至少 8 檔）：
- `Dashboard/Services/DashboardAgentService.cs`
- `Dashboard/Components/Pages/Monitoring/MonitoringTokens.razor` + `.razor.cs`
- `Dashboard/Components/Pages/Settings/SettingsWorkflow.razor.cs`
- `Bot/Services/TokenLogService.cs`
- `Shared/Dtos/TalentDto.cs`
- `Shared/Dtos/AlertEventDto.cs`
- `Bot/Configuration/DiscordSettings.cs`（6 Talent channel field）

**為何延後**：cascading reference 修範圍大、Stage 91 scope 控制紀律下延 follow-up。production DB 仍含 dead table，浪費 storage 但不影響正確性。

---

## 中優先（功能完善）

### F2：Token milestone Discord push

**範圍**：record_token_usage 每筆都不 push（避免洗版），但達 milestone 時 push（例如累積 cost > $X / 累積 token > N）。

**設計**：RecordTokenUsage method 內加 milestone 判斷（讀 DB sum）/ 或抽 background service 定期算。

**為何延後**：Stage 93 minimal scope 不做、Christ 真實使用後再決策 threshold。

---

### F3：Dashboard Records 表格進階功能

**範圍**：對應拍板 #12 延後項目：
- filter（by team / teammate / status / date range）
- sort
- pagination（目前 Take 100 / 大量資料會卡）
- 視覺化 stats（token cost 趨勢圖 / task completion rate / model usage 分布）
- Token 排行 / 對話 log 完整檢視器（drill-down）
- 結構化的 team detail page（點 team_id 看完整 timeline）

**為何延後**：拍板 #12「新增 1 頁、無其他功能」minimal scope。Christ 真實使用後再評估痛點。

---

### ~~F10：Dashboard / Bot container timezone 設定（Asia/Taipei）~~（2026-05-27 處理 / commit f4b1901）

**範圍**（2026-05-26 / hello-world smoke test 觀察 / Christ 拍板處理）：

Dashboard MCP Records 表格顯示時間全是 UTC、Christ 在 UTC+8、看 timestamp 需心算 +8。

**根因**：Razor 端 5 張 MudTable 全部已用 `ToLocalTime()` render（`src/AiTeam.Dashboard/Components/Pages/Records/Records.razor` 第 25/26/46/47/67/68/87/112 行）/ 寫法正確。問題在 Docker container 沒設 `TZ` 環境變數、container OS local time = UTC、`.ToLocalTime()` 結果 = UTC。

**修法**：`docker-compose.prod.yml` + `docker-compose.yml` 對 Dashboard 服務加：

```yaml
environment:
  - TZ=Asia/Taipei
```

建議同步給 Bot 服務加（Discord push timestamp / Bot log timestamp 同樣受影響、Christ 觀察各種 timeline 體驗一致）。

**涉及檔**：
- `docker-compose.prod.yml`
- `docker-compose.yml`

**不動**：Razor code（已正確）/ entity timestamp 欄位（DB 仍存 UTC、正確紀律）。

**為何延後**：UX 改善 / 不影響資料正確性 / Christ 已確認暫不影響閱讀。

---

### ~~F11：移除 `mcp_token_usage` 的 CacheRead / CostUSD 欄位~~（2026-05-27 處理 / commit 780548b）

**範圍**（2026-05-26 / hello-world smoke test 觀察 / Christ 拍板「暫時用不到」處理）：

`AgentTokenUsage` entity 有 3 個欄位 Christ 確認暫時不用：
- `CacheCreationTokens` (int?)
- `CacheReadTokens` (int?)
- `EstimatedCostUsd` (decimal?)

**修法**：entity + Migration + MCP tool + Dashboard + 文件全砍。

**涉及檔**：
- `src/AiTeam.Data/Records/RecordEntities.cs`（第 83-86 行 / `AgentTokenUsage` 3 個 property drop）
- `src/AiTeam.Data/Migrations/{stamp}_RemoveCacheAndCostFromTokenUsage.cs`（drop column × 3）
- `src/AiTeam.Bot/McpTools/RecordTools.cs`（`RecordTokenUsage` method 移除 3 個 optional 參數 `cacheCreationTokens` / `cacheReadTokens` / `estimatedCostUsd` + Description 文案修）
- `src/AiTeam.Dashboard/Components/Pages/Records/Records.razor`（第 100-101 / 110-111 行 / 砍 CacheRead + Cost USD 兩欄）
- `agents/petra-pm.md`（step 9 文案 / 移除 cache / cost 提示）
- `CLAUDE.md` / `docs/Architecture.md`（token usage tool 描述 / DB schema 段更新）

**不動**：
- 舊 TokenLog 系統（不同 table `token_logs` / 6 Talent 時代 / 已是 dead 但不在本 follow-up scope）
- `record_token_usage` 主參數 `inputTokens` / `outputTokens` / `model`（保留）

**為何延後**：scope 小 / 但動 Migration / 仍走正規 Stage commit。

---

### ~~F12：Tasks 表加 Description 顯示~~（2026-05-27 處理 / commit da7a152）

**範圍**（2026-05-26 / hello-world smoke test 觀察 / Christ 拍板處理）：

Dashboard MCP Records / Tasks 表只顯示 TeamId / TeammateId / Title / Status / CreatedAt / CompletedAt、看不到 `record_task(action="create")` 寫入的 `description`（已落 DB 但 UI 沒 render）。

對齊：Teams 表已有 Description 欄（`Records.razor` 第 16/23 行）/ Tasks 表沒有 / 行為不一致。

**修法**：`Records.razor` 第 52-71 行 Tasks MudTable 加：

```razor
<MudTh>Description</MudTh>
...
<MudTd>@(context.Description ?? "—")</MudTd>
```

（位置：Title 之後或表尾 / 對齊 Teams 表樣式 / 或 truncate 顯示前 N 字避免列高暴衝）

**涉及檔**：
- `src/AiTeam.Dashboard/Components/Pages/Records/Records.razor`

**為何延後**：純 UI 加欄 / Christ 已確認 description 有正確寫 DB / 不影響資料正確性。

---

### ~~F13：MCP Records 5 分頁拆到 NavMenu 子項~~（2026-05-27 處理 / commit 780548b）

**範圍**（2026-05-26 / hello-world smoke test 觀察 / Christ 拍板處理）：

現狀：左側 NavMenu「MCP Records」是單一 `MudNavLink Href="/records"`、頁內用 `MudTabs` 切 5 個 panel（Teams / Teammates / Tasks / Messages / Token Usage）。

目標：5 個分頁變成左側 NavMenu「MCP Records」展開後的子項目、對齊「監控中心」/「設定中心」既有 `MudNavGroup` + 子 `MudNavLink` pattern（`NavMenu.razor` 第 6-10 行 / 12-18 行）。

**修法**（推薦 route param 折衷方案 / 改動最小）：

NavMenu（`NavMenu.razor` 第 4 行 NavLink 改 MudNavGroup）：
```razor
<MudNavGroup Title="MCP Records" Icon="@Icons.Material.Filled.TableChart" Expanded="true">
    <MudNavLink Href="/records/teams"        Match="NavLinkMatch.All">Teams</MudNavLink>
    <MudNavLink Href="/records/teammates"    Match="NavLinkMatch.All">Teammates</MudNavLink>
    <MudNavLink Href="/records/tasks"        Match="NavLinkMatch.All">Tasks</MudNavLink>
    <MudNavLink Href="/records/messages"     Match="NavLinkMatch.All">Messages</MudNavLink>
    <MudNavLink Href="/records/token-usage"  Match="NavLinkMatch.All">Token Usage</MudNavLink>
</MudNavGroup>
```

Records.razor：
- 加 route：`@page "/records"` + `@page "/records/{Section}"` / default Section="teams"
- 拿掉 `MudTabs` + 5 個 `MudTabPanel`、改成依 `Section` 參數條件 render 對應 MudTable
- 載資料 logic 不動（一次撈 5 類 / 切頁不重撈）

替代方案：拆 5 個 Razor 檔 + 共用 layout / service 撈資料 / 對齊更乾淨但檔數翻倍、看 Christ 偏好。

**涉及檔**：
- `src/AiTeam.Dashboard/Components/Layout/NavMenu.razor`（第 4 行 NavLink → MudNavGroup + 5 個 NavLink）
- `src/AiTeam.Dashboard/Components/Pages/Records/Records.razor` + 對應 `.razor.cs`（如有 / route param + 條件 render）

**不動**：後端 / Repository / Records 資料載入 logic。

**為何延後**：UX 改善 / 不影響資料正確性 / 跟 F10 / F11 / F12 同屬 Records 系列改動、建議合併到同一個 v4.0.3 patch Stage 處理。

---

### F4：Channels / docker-compose env 整理

**範圍**：v4-rewrite 後 dead env / config：
- `DiscordSettings.Channels.CeoChannel/PmChannel/DevChannel/ReviewerChannel/QaChannel/DocChannel` C# field 仍存在（appsettings.json / docker-compose 已砍但 class field 留）
- 其他 v4/v5 残留 config field

**為何延後**：不影響 build / runtime / 純整潔考量。

---

### F14：`.claude/agents/cody.md` tools list 沒含 MCP record tool

**範圍**（2026-05-27 / F11+F13 spawn cody-1 過程觀察）：

`.claude/agents/cody.md` 第 4 行 `tools: Read, Edit, Write, Grep, Glob, Bash` / 沒含任何 `mcp__aiteam-records__*` tool。當 Petra spawn cody 處理 task / cody 無法自呼叫：

- `record_task(action="claim")`
- `record_message`
- `record_token_usage`
- `record_task(action="complete")`

違反 `agents/petra-pm.md` step 8/9 紀律「teammate 自己做、非 lead」。

**當前 workaround**：Petra 代記錄（spawn cody → cody return → Petra 用 cody 的 return 內容代 call MCP record / token usage 用 Agent tool return 的 total_tokens 估算拆 input/output）。F11+F13 已用此 workaround 完成。

**修法選項**：
- **推薦 A**：cody.md tools list 加 `mcp__aiteam-records__record_task`, `mcp__aiteam-records__record_message`, `mcp__aiteam-records__record_token_usage` 3 個 / cody 直接自呼叫 / 對齊既有紀律。
- **B**：放寬 `agents/petra-pm.md` step 8/9 為「teammate 自做或 lead 代做」/ 不改 cody.md / 但邏輯上 lead 代做不精準（teammate 自己的 LLM token 該算在 teammate 自己帳上 / Agent tool return 的 total_tokens 拆 input/output 是估算 / 不準）。

**涉及檔**（推薦 A）：
- `.claude/agents/cody.md`（tools list 加 3 個 MCP tool）

**為何延後**：F11+F13 已用 workaround 完成 / 不阻塞當下交付 / 下次 spawn cody 前處理即可。

---

## 低優先（重評時機）

### F15：`record_task(action="claim")` 重複 claim 沒 reject

**範圍**（2026-05-27 / F11+F13 開 team 過程觀察）：

`record_task(action="claim", taskId, teammateId)` 對**已 claimed task** 重複呼叫 / 直接 overwrite TeammateId / 返回 "claimed"（沒 reject "already claimed by X"）。

**落地觀察**：Petra 先誤 claim task 給 `petra-pm` teammate_id / 後再 claim 給 `cody-1` teammate_id / DB 直接 overwrite / 兩次都返回 "claimed" / 無 warning。

**行為合理性議題**：
- 若視為「容錯」 → 當前行為 OK（允許 re-assign / 改換 worker）
- 若視為「應 reject 避免 race condition / 誤覆蓋」 → 應 idempotent + reject if already claimed by different teammate（同 teammate 重 claim 仍 OK / cross teammate reject）

**修法**：`record_task(action="claim")` 邏輯 — 若 `task.TeammateId` 已存在且 != 新 teammateId → 返回 `"already claimed by {existing teammateId}"` / 不 overwrite。

**涉及檔**：
- `src/AiTeam.Bot/McpTools/RecordTools.cs` `RecordTask` method claim branch

**為何延後**：當前單 Petra session 自處 claim 流程不易出錯 / race condition 風險未來真 multi-Petra session 場景才會曝露 / 但留紀錄當設計議題。

---

### F5：NavMenu 結構重評

**範圍**：v4-rewrite 後 NavMenu 簡化為「首頁 + MCP Records + 監控中心 + 設定中心」。但 `/settings/workflow` 內容（v5/v6 flag）已大砍、頁面可能空 / 無內容。

**處理**：Christ 用 v4 一段時間後、評估哪些頁面真的有用、再砍 / 整理。

---

### F6：Petra-pm subagent definition 微調

**範圍**：`agents/petra-pm.md` 第一版完成。Christ 真實使用後可能需要：
- 工作流程細節調整
- spawn teammate 命名規範
- record_message 寫入頻率（每 turn vs 每 N turn）

**處理**：Christ 用 1-2 個 task 後反饋、Petra 自己修 `agents/petra-pm.md`。

---

### F9：Claude Code MCP client tool list 不 hot reload — upstream blocker

**範圍**（open / 2026-05-26 v4.0.1 + v4.0.2 升 tool 過程觀察 / 2026-05-27 完整實測確認根因）：

每次 AiTeam Bot 升級 MCP tool（加 / rename / 移除）後、**現有 Claude Code Petra session 拉不到新 tool list / tool schema 凍結在 session 啟動時 snapshot**。Christ 必須關掉舊 session 重開 `claude --agent petra-pm` 才能拿到新 tool。

**根因確認**（非 AiTeam server 端 bug / 是 Claude Code MCP client 端已知 issue）：

| 三方對照 | 狀態 |
|---|---|
| MCP spec | ✅ 規範清楚：server declare `listChanged: true` capability + 變更時 emit `notifications/tools/list_changed` / client 收到後 re-call `tools/list` |
| AiTeam Bot v4.0.2（ModelContextProtocol.AspNetCore 1.3.0）| ✅ 實測：`initialize` response 回 `capabilities.tools.listChanged=true` / `tools/list` 回完整 8 tool 含新加 close_team/finish_teammate/rename 後 record_message |
| Claude Code MCP client | ❌ 已知 bug [#13646](https://github.com/anthropics/claude-code/issues/13646)：定義了 Zod schema 但 **沒 register `setNotificationHandler` for `notifications/tools/list_changed`** / `listTools` 只在 connection setup 時 call 一次 / session 內永遠不 refresh |

**短期 workaround**（v4.0.x 開發期落地）：每次 push 含 MCP tool 變更的 commit 後 / Christ 必須關掉現有 Petra session 重開、新 session 才能用新 tool。AiTeam 文件已記錄：

- `agents/petra-pm.md` PM 工作流程不需改（teammate spawn 模式與此無關）
- `CLAUDE.md` MCP server 段已列 8 tool（妳的 reference 不變）
- 升 tool 的 CHANGELOG entry 可加註「Petra session 需重開才生效」（未強制紀律 / 視真實使用痛點再加）

**長期解**（等 upstream）：Anthropic 修 issue #13646 → Christ 升 Claude Code 版本（v2.1.32+ 開始的 experimental Agent Team 仍在 active dev）→ 之後 tool 變更可 hot reload / 不必重開 session。

**為何不在 AiTeam server 端 workaround**：tool list 是 client 控的快取 / server 無權 invalidate client cache。能做的只剩 Discord push「請重開 session」alarm、但 Christ 已經透過 Dashboard / push 看得到變化、紀律上 push alarm 增加噪音不抵價值（未來真有痛點再加）。

**追蹤**：每次升 Claude Code 主版本後重測一次（call v4.0.2 tool 看新 session 行為 / 觀察 issue #13646 是否 closed）。

---

### ~~F8：Team / Teammate lifecycle 收尾方法缺~~（v4.0.2 處理）

**範圍**（已處理 / 2026-05-26 / hello-world smoke test 觀察到 Dashboard team 一直 active）：

`AgentTeam` 有 `Status` + `ClosedAt` 欄位、`AgentTeammate` 有 `FinishedAt` 欄位、但 MCP server **沒有對應寫入 tool**。team 完成後永遠 Status='active' / ClosedAt 永遠 null / teammate FinishedAt 永遠 null。

修法：加 2 個 MCP tool。

- `close_team(teamId)` — Status='closed' + ClosedAt=now / idempotent（已 closed 回 'already closed'）/ Discord push 🏁 Agent Team 收尾通知
- `finish_teammate(teammateId)` — FinishedAt=now / idempotent / 不 push（避免洗版）

涉及檔：
- `src/AiTeam.Bot/McpTools/RecordTools.cs`（加 2 個 method `CloseTeam` + `FinishTeammate`）
- `src/AiTeam.Bot/McpTools/HealthCheckTool.cs` + `Program.cs`（註解 tool 數 4 → 6 record tool / 共 8 個 MCP tool）
- `agents/petra-pm.md`（工作流程 step 11 加 finish_teammate / step 12 加 close_team / 補「PM 可直接動手邊界」B 選項 SOP）
- `CLAUDE.md` + `docs/Architecture.md`（tool 清單 6 → 8 / Discord 觸發點 3 → 4）

不動：
- entity schema（欄位早就存在 / 無 Migration）
- 既有 5 個 MCP tool（純 additive）

---

### ~~F7：MCP tool 名 `record_conversation` 與 entity / table 命名不一致~~（v4.0.1 處理）

**範圍**（已處理 / 2026-05-26 / hello-world smoke test 觀察到）：

MCP tool 名 `record_conversation` 與 C# entity `AgentMessage` + DB table `mcp_messages` 命名不一致。entity / table 命名正確（record 單位是 **一則 message**、不是整段 conversation）/ tool 名是 outlier。

修法：rename MCP tool `record_conversation` → `record_message`。

涉及檔：
- `src/AiTeam.Bot/McpTools/RecordTools.cs`（method `RecordConversation` → `RecordMessage` + Description 文案修）
- `src/AiTeam.Bot/McpTools/HealthCheckTool.cs` + `src/AiTeam.Bot/Program.cs`（註解）
- `agents/petra-pm.md`（Petra 工作流程指引 step 8）
- `CLAUDE.md` + `docs/Architecture.md` + `docs/planning/Phase_v4_Roadmap.md` + `docs/planning/Phase_v4_Stage94_E2E_Guide.md`（doc 字串）

不動：
- `AgentMessage` entity / `mcp_messages` table / `AgentMessages` DbSet（已對齊、不需 Migration）
- `Phase_v4_Execution_Log.md` 歷史執行紀錄 + `CHANGELOG.md` v4.0.0 entry（保留時間戳記真實感）

---

## 標記紀律

新增 follow-up：append 到對應優先級下、加日期 + 來源 reference（哪個 Stage 觀察到）。
處理完：劃線 + 標 `~~F1~~（v4.1.0 處理 / commit xxxxxxx）` / 不刪 entry。
