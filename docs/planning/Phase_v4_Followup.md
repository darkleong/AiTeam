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
- record_conversation 寫入頻率（每 turn vs 每 N turn）

**處理**：Christ 用 1-2 個 task 後反饋、Petra 自己修 `agents/petra-pm.md`。

---

## 標記紀律

新增 follow-up：append 到對應優先級下、加日期 + 來源 reference（哪個 Stage 觀察到）。
處理完：劃線 + 標 `~~F1~~（v4.1.0 處理 / commit xxxxxxx）` / 不刪 entry。
