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

### F4：Channels / docker-compose env 整理

**範圍**：v4-rewrite 後 dead env / config：
- `DiscordSettings.Channels.CeoChannel/PmChannel/DevChannel/ReviewerChannel/QaChannel/DocChannel` C# field 仍存在（appsettings.json / docker-compose 已砍但 class field 留）
- 其他 v4/v5 残留 config field

**為何延後**：不影響 build / runtime / 純整潔考量。

---

## 低優先（重評時機）

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
