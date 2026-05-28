# Changelog

本專案所有重要變更紀錄於此檔。

格式參照 [Keep a Changelog 1.1.0](https://keepachangelog.com/zh-TW/1.1.0/)，版本號遵循 [Semantic Versioning](https://semver.org/lang/zh-TW/)。
工作單位為 **Stage**，每完成一個 Stage 通常對應 minor 版本 bump。每條 entry 的細節見對應 Stage Roadmap；更深的實作 commit 訊息見 git log。

未實作功能候選見 [Future_Feature.md](docs/planning/Future_Feature.md)。

## Entry 紀律

- **新 entry format**：`## [X.Y.Z] — date — [Stage XX](path) 主題` + 換行 + body 段（~100-200 字）
- 細節 link Stage Roadmap 不複述 / 超字數 → 砍重複 / 補 reference 而非展開
- **寫完自審 step**（B 紀律機械化 / 對抗 LLM 自然 inflation）：寫完 entry **必對比上一條 entry 字數**（visual / grep 行數估算）/ 超太多 → 砍細節 + 補「細節見 Stage Roadmap」reference / 不靠記憶力守紀律
- v3.54.0 以下純 link 保留為歷史 format（早期 Sage 自動寫紀律 / 不追溯改）
- **漂移反例**（C 具體警示 / 比抽象紀律直觀）：Stage 84 v3.76.0 寫成 ~300 字（5 sub-service 名全列 + 7 record 細節 + Healthy 偏離 3 條全攤）/ 修為 ~150 字（commit 40da039）— 同類根因第 6 次累積（context 充滿細節時 LLM 自然偏「都寫進去比較完整」）/ 落地寫完自審 step 防再犯

---

## [Unreleased]

—

---

## [4.1.3] — 2026-05-28 — Agent 命名去人格化（A 路線）+ reviewer / syncer 加入

**PATCH — Christ 拍 A 全 commit 走純職能命名**：repo 內 agent 全部脫人格化（給團隊通用易交接）/ 個人 personality 版本搬到 user-level `~/.claude/agents/` 作熟悉隊友 overlay。

- **repo agents 去人格化**：`lead.md`（name: `petra-pm` → `lead` / 移除「Petra」「女性稱謂妳」「不過度情感化」「有幽默感」personality 段）+ `coder.md`（name: `cody` → `coder` / 「妳」→「你」/ 「Christ」→「boss」/ 對齊純職能 PM SOP）+ 大幅瘦身（對話 register / 觀察異常段刪 / 已在 `team.md` 涵蓋）
- **新加 reviewer + syncer**：`reviewer.md`（read-only diff review / sonnet / 無 Edit/Write tool 硬隔離 / 補 coder→commit 中間 review 洞 / 嚴重度分級 Blocker/Concern/Suggestion）+ `syncer.md`（doc-only sync / sonnet / 只動 `docs/` + root markdown / 嚴禁動 `src/` 與 `.claude/`）
- **personality 版搬 user-level**：`~/.claude/agents/petra-pm.md` + `~/.claude/agents/cody.md` 重建保留 Petra / Cody / 「妳」女性稱謂 / personality 段 / 未來 Christ 個人專案可繼續用熟悉隊友
- **settings 連動**：`.claude/settings.json` `agent: petra-pm` → `agent: lead`
- **CLAUDE.md 同步**：Lead persona 引用 + 加 reviewer/syncer 段 + onboarding 段補個人 overlay 路線
- **`.gitignore` 細化**：補 `.claude/worktrees/` ignore（個人 git worktree state / runtime）+ 註解標清楚共享項（agents/output-styles/skills/settings.json/.mcp.json）vs 個人覆蓋項。`.claude/skills/` **不** ignore（對齊 agents 邏輯 / 未來 project skill 跟 team 走）

Christ 拍板理由：「給團隊就該完全通用」+「熟悉的隊友留我身邊」雙軌設計（repo 純職能 / user-level 個人 personality）。

---

## [4.1.2] — 2026-05-28 — 團隊 *.md 配置整理 + `.claude/` 進 git

**PATCH — Christ 拍 A 路線 / Petra 自處（規模小 + 零設計決策 + post-deliver polish 三條件滿足）**：為現實生活團隊成員 onboarding 預備、把分散在 repo `agents/` + 個人 `~/.claude/` 的 *.md 配置整理進 `.claude/` 並 commit。

- **`.gitignore` 鬆綁**：原本 `.claude/` + `.mcp.json` 全 ignore、改為只 ignore 個人覆蓋（`settings.local.json` / `scheduled_tasks.lock` / `.credentials.json` / `projects/`）
- **agents 搬家 + 改職能命名**：`agents/petra-pm.md` → `.claude/agents/lead.md`（frontmatter `name: petra-pm` 保留）/ `.claude/agents/cody.md` → `.claude/agents/coder.md`（frontmatter `name: cody` 保留）— 檔名脫鉤人名、依 [Claude Code docs](https://code.claude.com/docs/en/sub-agents) 「identity comes only from the `name` frontmatter field」
- **output-style 升專案級**：建 `.claude/output-styles/team.md`（語言 / register / 工作態度 / 簡潔 / 觀察異常 / 太高深訊號）+ `.claude/settings.json` 加 `"outputStyle": "team"`、個人偏好（稱謂等）走 `~/.claude/output-styles/` overlay
- **通用技術知識搬 repo**：個人 memory `reference_aspire_url.md` → `docs/reference/aspire-url.md`
- **CLAUDE.md 加 onboarding 段**：團隊成員 5 步驟啟動指引（env var / Agent Teams flag / `claude` 啟動）
- **清空殼**：`agents/` 資料夾 + `agents/.mcp.json.example` 砍（`.mcp.json` 進 git 後不再需要 example）

---

## [4.1.1] — 2026-05-27 — [Phase_v4_Followup F18-F19](docs/planning/Phase_v4_Followup.md) v4.1.0 post-release polish

**PATCH — 前 Petra 交接 6 條 cody 範圍外發現一次清**（2 cody spawn / 全程 cody 自呼叫 MCP record 落地 / F14 紀律首輪實證生效）：

- **Records 並發 thread-safety**：Records.razor.cs DbContext → IDbContextFactory（對齊 MonitoringTokens 既有 factory pattern / SignalR callback + OnInitialized 並發 race 修）+ RecordsTrends 加 SignalR（對齊 Records.razor F16 ReloadAll pattern / 不必 F5 刷新）+ Trends 7/30 天 UTC group → local date group（修跨日邊界資料錯歸日 / XAxis label local 不變）
- **dead code 清**：DailyReportCron env 砍（Bot 0 caller / docker-compose + appsettings.json + AgentSettings.cs property 3 處）+ v3 TokenLog dead chain 整套砍（TokenLogService / TokenCostEstimator / TokenUsage DTO 3 file delete + Program.cs DI 砍 + Entities.cs TokenLog.PetraSessionId column 砍 + AppDbContext HasIndex 連動 + Migration drop_column）+ MonitoringTokens per-PetraSession dimension dead query 砍（razor + razor.cs）+ InternalController L42-48 註解對齊 v4-rewrite 實際 scope（rules/agents/all / 砍 TalentMetaCache + PromptResolver 過期描述）

詳細落地見 git log 2026-05-27 + Phase_v4_Followup.md F18-F19 commit hash。

---

## [4.1.0] — 2026-05-27 — [Phase_v4_Followup](docs/planning/Phase_v4_Followup.md) Records 系列大清理 + 進階 UI + SignalR + 每日 Discord 彙總

**MINOR — multiple follow-up 一次清**（Christ 拍 C 全自動 / Petra 4 patch / cody 5 spawn / 16 task / 全程 AiTeam MCP record 落地）：

- **dead code 大清**（淨 -2987 行）：F1 v3-v5 9 entity drop（Talent/TalentSkill/TaskMemory/TalentMemory/SkillPrompt/TalentPrompt/PetraSession/PetraSessionMessage/PetraInbox）+ Migration drop_tables / F5 設定中心 5 子頁 + 對應 Service / F4 6 dead Discord Channels field / F11 mcp_token_usage CacheRead+CostUSD 欄位（Migration drop column × 3）
- **Records 進階 UI**（F3）：5 表 sort（每欄 MudTableSortLabel）+ paging（MudTablePager 25/50/100/200 / 去 Take(100) 限制）+ filter（每表 2 維度自決）+ drill-down /records/team/{id} 完整 timeline + /records/trends 3 個 MudChart 折線
- **即時通知**（F16）：Bot 寫 MCP record → Dashboard SignalR push → Records 自動 reload 不必 F5 / F2 每日 09:00 Discord 彙總（24h SUM + per model + active team + completed task + grand total）
- **基礎建設**：F10 container TZ=Asia/Taipei / F12 Tasks 表加 Description 欄 / F13 Records 5 分頁拆 NavMenu sub-route + @switch render / F15 record_task claim cross teammate reject / F6 petra-pm.md 加 teammate 命名 + token 估算規則 / F14 cody.md tools list 加 MCP record tool

詳細執行 16 task + 5 patch series（v4.0.4 → v4.0.7）見 git log 2026-05-27 + [Phase_v4_Followup.md](docs/planning/Phase_v4_Followup.md) 各條 commit hash。

---

## [4.0.2] — 2026-05-26 — [Phase_v4_Followup F8](docs/planning/Phase_v4_Followup.md) Team / Teammate lifecycle 收尾方法補 + Petra SOP 邊界

**PATCH — design gap 補洞**：v4.0.0 漏設計、`AgentTeam` / `AgentTeammate` 欄位有 `ClosedAt` / `FinishedAt` 但無 MCP tool 寫入、team 完成後永遠 active / teammate 永遠未 finished（hello-world smoke test 觀察到）。加 2 個 standalone MCP tool：`close_team`（Status='closed' + ClosedAt + Discord 🏁 push）、`finish_teammate`（FinishedAt / 不 push 避免洗版）/ 都 idempotent / 純 additive 無 Migration。Petra `agents/petra-pm.md` 工作流程 step 11/12 加 lifecycle close、補「PM 可直接動手邊界」B 選項 SOP（規模小 + 零設計決策 + post-deliver polish 三條件同時成立才適用 / Christ 拍板）。共 8 個 MCP tool（HealthCheck + RecordTools 7 method）。

---

## [4.0.1] — 2026-05-26 — [Phase_v4_Followup F7](docs/planning/Phase_v4_Followup.md) MCP tool 命名一致性修

**PATCH — polish refactor**：MCP tool `record_conversation` → `record_message` rename / 對齊 `AgentMessage` entity + `mcp_messages` table。hello-world smoke test 觀察到 outlier 命名（record 單位是「一則 message」/ 不是整段 conversation）。改 1 個 C# method 名 + 4 個 doc 檔字串、無 schema 變動、無 Migration。Petra 本機跑時 tool list 會自動帶新名上線、`.mcp.json` 配置不變。

---

## [4.0.0] — 2026-05-26 — [Phase v4-rewrite](docs/planning/Phase_v4_Roadmap.md) AiTeam 純記錄系統改造（Stage 88-95）

**MAJOR — 架構級重構**：AiTeam 從「執行 + 記錄」雙重身份 → 「純記錄 MCP server」。執行端整套搬到 Christ 本機 **Claude Code Agent Team**（v2.1.32+ experimental / Opus 4.6 推出）。8 Stage 大砍大改、單日完成（Stage 88 spike → 89 砍舊 → 90 MCP endpoint → 91 record tools + DB schema → 92 Dashboard → 93 Discord 改通知 → 94 E2E 指南 → 95 結案）。砍範圍：6 Talent（Victoria/Petra/Cody/Vera/Quinn/Sage）整套 + LlmProviderFactory + HITL + Petra orchestrator + Aria-Forge 工作模式（17,142 行刪 / 105 行新 / Stage 89 一個 commit）。新範圍：MCP server endpoint（`ModelContextProtocol.AspNetCore` 1.3.0 / `/mcp` route / Bearer auth）+ 5 mcp_* 新表 + 6 個 MCP tool（HealthCheck + register_team / register_teammate / record_task / record_conversation / record_token_usage）+ Dashboard Records 表格頁（1 頁 5 tab）+ Discord push TaskUpdates channel + `agents/petra-pm.md` lead persona + E2E 驗證指南。Petra「全能模式」（大授權 / bypassPermissions / Christ 不介入）首次驗證、subagent 不污染主 session 紀律落地。詳細執行紀錄 + 自決決策見 [Phase_v4_Execution_Log.md](docs/planning/Phase_v4_Execution_Log.md)、follow-up 工作 6 項見 [Phase_v4_Followup.md](docs/planning/Phase_v4_Followup.md)。

---

## [3.79.0] — 2026-05-25 — [Stage 87](docs/planning/Stage_87_Roadmap.md) Dashboard 改造收口 + v4 LLM 配置 SoT 統一

三軸合併 catch-all L+：A 軸 v4 LLM 配置 SoT 統一（`agent_configs` 表 drop + Petra 配置遷 `talents.Name="Petra"` row + Bot 端 `AgentConfigCache` 砍 → `TalentMetaCache` 取代 + AGENTS 分頁砍 + TALENTS 擴 Provider/Model + Token Limit UI）+ B 軸 nav 階層化（3 Hub 拆 14 sub-page + URL 階層 routing + NavMenu MudNavGroup 二層 + SignalR 細粒度訂閱）+ C 軸 Rules v4 9 row DELETE + UI fallback 程式碼砍。1 Migration（raw SQL idempotent UPDATE COALESCE + DELETE rules + DROP TABLE）+ 0 LLM call / 0 燒餘額。Forge 7 commit + Christ 視覺驗收 13 commit follow-up（14 議題 / 細節見 Stage Roadmap）。xUnit 130 全綠 / 0 新 warning。升 refactor-sop v1.7 + mudblazor.md v1.7（5 條紀律補強）。

---

## [3.78.0] — 2026-05-24 — [Stage 86](docs/planning/Stage_86_Roadmap.md) Dashboard 改造（sidebar + 視覺/UX 一致性 + Theme 機制 + 附檔預覽 + Monitoring + Rules 清理）

7 子項全 deliver：Rules dropdown 砍 8 個 v4 dead 角色 + 全 Dashboard UI 顯示文字砍版本/Stage 編號 + 視覺一致性 catch-all 7 項 + PetraInbox 附檔 MudDialog base64 預覽 + Monitoring 時間範圍 filter（6 選項）+ Sidebar hamburger hover overlay + click pin + Theme palette 重設計（深灰色 baseline + IThemeService Scoped event 修 Stage 83 v4 Bug 9 根因）。Forge 7 commit + Christ 視覺驗收 22 commit follow-up（細節見 Stage Roadmap）。0 Migration / xUnit 130 全綠 / 0 新 warning。升 refactor-sop v1.6 + mudblazor.md v1.6（5 條紀律補強）。

---

## [3.77.0] — 2026-05-24 — [Stage 85](docs/planning/Stage_85_Roadmap.md) Dashboard 救火（修 bug + 系統 alert + dead code 清理）

5 子項全 deliver：5 DashboardServices 切 IDbContextFactory（終結並發 bug 同類根因第 3 次累積）+ 三層 alert 機制（Discord push + SignalR + MudSnackbar toast + AlertRateLimiter rate-limit）+ v4 dead flag 11 個整套清（9 檔 / grep 0 業務 match）+ 砍 MOCKMODE tab dup + Discord placeholder 卡 + 3 筆 Stage 80 殘留 paused session 清 + 24h timeout cleanup loop。Forge 4 commit + Chrome MCP UI 視覺驗 5 條 + 3 條留 Christ 真實 fire。升 refactor-sop v1.4 + v1.5（Test session cleanup + Razor 大檔砍工具選擇）。0 Migration / xUnit 130 全綠 / 0 新 warning。

---

## [3.76.0] — 2026-05-24 — [Stage 84](docs/planning/Stage_84_Roadmap.md) PetraOrchestratorService 怪物大檔拆解（pure refactor / 91.5% 瘦身）

`PetraOrchestratorService.cs` 從 2266 → **193 行**（瘦身 91.5%）。新增 8 檔：5 sub-service + 1 static helper（解 TalentDispatch ↔ DynamicReplan 雙向循環）+ 1 DTO 集中 + 1 Commons。v5 IAgentTool ecosystem 整套砍（4 worker class + interface + attribute + flag）對齊 Stage 78a pattern。0 行為改變 / caller 4 處 0 改動 / xUnit 130 passed + 2 skip。SOP 累積第 5 次 single-session 完成 M+ 規模新里程碑 / 升 refactor-sop.md v1.3 3 條 know-how（細節見 [Stage Roadmap](docs/planning/Stage_84_Roadmap.md)）。

---

## [3.75.0] — 2026-05-21 — [Stage 83](docs/planning/Stage_83_Roadmap.md) WebUI 全砍重設計（3 大分區 + Home + Auth）「最後測驗」戰略節點達成

Dashboard 11 頁全砍 → Tasks + Settings + Monitoring 3 大分區 + Home + Auth 獨立 + Office 砍。Migration `Stage83PetraSessionResultPrUrl`。Forge L+++ scope 5 輪修補 + Aria 5 輪 gate1 + Chrome MCP 視覺驗 3 輪 + 9/11 修根因典範 = v5.5 dynamic orchestrator + Dashboard 真實對齊 / v5.5 完整收口進 production 自然累積期。

---

## [3.74.0] — 2026-05-21 — [Stage 82](docs/planning/Stage_82_Roadmap.md) Quinn outputLen 修根因（路線 A stream-json）+ Trial_v25 三 🟡 議題收口

ClaudeCodeService 切 `stream-json --verbose` + ParseJsonOutput NDJSON line-by-line accumulate（解 Quinn tool-heavy 場景 final turn tool_use → result 空 議題）。TokenTrackingProvider AsyncLocal `PetraSessionAmbient` 透傳 PM call site。SubtaskPlanParser 加 StripPreambleAndPostamble robust 防呆。Petra Sonnet 4.6 切 production active default（Stage 38 DB SoT）。

---

## [3.73.0] — 2026-05-20 — [Stage 81](docs/planning/Stage_81_Roadmap.md) B 動態 re-planning + HITL retry gate 配套 + Trial_v24 議題收口

Christ 親口要的業務功能。`DetectReplanTrigger` Regex（Vera critical / Quinn fail）+ `InvokePetraReplanAsync` retry instruction（LangGraph cycles 業界紀律）+ 4 decision routing（approve / edit / reject / respond）+ `replan_confirm` 卡 UI 重用 Stage 80 infra。`ReplanIteration` + `SessionCostUsd` + AppSetting 3 cap default 守 baseline。3 know-how 升級到 conventions/skill。

---

## [3.72.0] — 2026-05-20 — [Stage 80](docs/planning/Stage_80_Roadmap.md) A HITL plan confirmation 閘門 + Trial_v23 議題收口

Christ 親口要的業務功能。BossInteraction `plan_confirm` type + HITL pause point + 4 decision resume routing（approve / edit / reject / respond）+ InteractionCard UI + `PlanConfirmationProcessor` BackgroundService（Stage 78c 砍 InteractionProcessor 後新建 / 對齊既有 PetraInboxProcessor 紀律）+ AppSetting `UseHITLPlanConfirmation` default false 守 baseline。修 Trial_v23 揭 Blazor InteractiveServer 並行 Scoped DbContext 根因（IDbContextFactory 並存註冊）。2 know-how 升級到 conventions/skill。

---

## [3.71.0] — 2026-05-19 — [Stage 79](docs/planning/Stage_79_Roadmap.md) v5.5 image flow 補完

Stage 75 PetraInbox 設計遺漏 image flow 修根因。PetraInbox.Attachments jsonb + Migration + SubtaskPlan.NeedsImageContext 條件性 worker propagation（業界「pass images only to worker agents that need them」）+ ClaudeCodeChatClientAdapter workspace .tmp/images/ 寫圖檔 + GeminiProvider multimodal native + Dashboard 4 層 validate 動態化（AppSetting MaxAttachmentsPerTask=5 + MaxAttachmentSizeMB=5）。Trial_v23 啟動條件達成。

---

## [3.70.0] — 2026-05-19 — [Stage 78c](docs/planning/Stage_78c_Roadmap.md) v5.5 Phase 4 候選 C 最終收口 — v4 Pipeline framework 整套砍除（**MAJOR refactor / 108 檔變動 net -22690 行**）

v4 path 全部砍乾淨（0 dead code / 0 dead caller / 0 dead routing / v5.5 path 唯一 source）。6 鏈砍：Pipeline + Meeting+Appeal+HITL+Boss + Queue+Group + IAgentExecutor 部分 + Discord routing 重整（ButtonCallbackRouter 713→~120 / SlashCommandRouter 整檔砍）+ WebhookController 整檔砍。0 Migration（agent_queues 表真實不存在 / Stage 78c spike 揭）。Build 0 warning（歷史最低）。WebUI Stage 預備重設計範圍。

---

## [3.69.0] — 2026-05-19 — [Stage 78b](docs/planning/Stage_78b_Roadmap.md) v5.5 Phase 4 候選 C 後續 — v4 path dead caller 整套砍除

純 refactor。ButtonCallbackRouter v4 routing 砍 + HandleConfirmYesAsync v4 body cascade 砍 + OpsAgentService IAgentExecutor 實作砍（保 HealthCheckJob production active）+ `/task` slash command 砍 + GitHub Issue webhook handler 砍 + CeoAgentService.ProcessAsync v4 fallback 砍。8 檔 net -787 行 / build warning 102 → 59 (-42%)。Stage 78c 預留 v4 Pipeline 整套砍。

---

## [3.68.0] — 2026-05-18 — [Stage 78a](docs/planning/Stage_78_Roadmap.md) v5.5 Phase 4 C — v4 path dead code 砍除

砍 3 純 v4 class（Rosa/Demi/Release ~1150 行）+ 4 雙路徑 class v4 method ~2900 行（Doc/Dev/Reviewer/Qa 留 v5.5 IAgentTool）+ Adapter capability 7→4 + DefaultSkillRegistry 6→4。LlmProviderFactory 系列全保留（Petra 3 call sites active）。CLAUDE.md production path 修根因（Petra LlmProviderFactory / 非 Claude Code CLI）。Stage 78b 預留 ButtonCallbackRouter v4 routing + IAgentExecutor + AgentQueueProcessor + OpsAgent。

---

## [3.67.0] — 2026-05-18 — [Stage 77](docs/planning/Stage_77_Roadmap.md) v5.5 Phase 3 補強 — fire-and-forget A2 業界推薦完整版

PetraInboxChannel BoundedChannel + PetraDispatchWorker N=3 multi-consumer Task.WhenAll + PetraInboxProcessor 退化 pure producer + StopAsync graceful shutdown drain 4 階段 30 min timeout + `Workflow:MaxConcurrentPetra=3`。Stage 76 retry path 整套搬遷 0 邏輯改變。業界 7 議題 WebSearch 完整 incorporated。Trial_v22 啟動條件達成。

---

## [3.66.0] — 2026-05-18 — [Stage 76](docs/planning/Stage_76_Roadmap.md) v5.5 Phase 3 補強 — task retry / resume 機制

PetraInbox schema 擴 4 欄（AttemptCount/MaxAttempts/NextRetryAt/DeadAt）+ Migration + retry path 3 路分支（Transient retry exponential backoff 30s×2×max3 + ±20% jitter / BusinessRule+Permanent fail-fast）+ PetraErrorClassifier + Dead Letter pattern + Dashboard 重跑按鈕。ef-core.md 升級 Migration AddColumn defaultValue 對齊 entity C# initializer 紀律。

---

## [3.65.0] — 2026-05-17 — [Stage 75](docs/planning/Stage_75_Roadmap.md) v5.5 Phase 3 兩層 queue 配套

Petra 接收層 + Worker 執行層 per-Talent serialization：PetraInbox table + PetraInboxProcessor BackgroundService + CeoAgentService 寫 inbox + TalentDispatchLockService SemaphoreSlim per-Talent + DispatchTalentsAsync per-Talent lock wire。Forge spike 修根因 `talentNameToIdMap` unconditional build。

---

## [3.64.0] — 2026-05-17 — [Stage 74](docs/planning/Stage_74_Roadmap.md) v5.5 Phase 3 per-Skill Model + 真並行 dispatch

TalentSkill schema 擴 Provider/Model + TalentSkillModelResolver 三層 fallback + ClaudeCodeChatClientAdapter 動態 Model 整合 + SubtaskPlanLevelGrouping DAG fan-out 路線 A + SkillDescriptor metadata 對齊 Agent Skills open standard。

---

## [3.63.0] — 2026-05-17 — [Stage 73](docs/planning/Stage_73_Roadmap.md) v5.5 Phase 3 Prompt content 升級 + Petra TalentPrompt persona seed

6 SkillPrompt v1→v2 走 versioning path + Petra 4 拍板特質 persona（謹慎拍板 / 對冗餘不容忍 / 持續迭代 / 對等和互相）。對齊「品質 > 做法」精神。

---

## [3.62.0] — 2026-05-17 — [Stage 72](docs/planning/Stage_72_Roadmap.md) v5.5 Phase 2 Prompt DB 化 + Talent identity 整合

SkillPrompts + TalentPrompts 兩層 schema + Versioning + rollback + PromptRepository CRUD + PromptResolver 5-min TTL cache。對齊業界 2026 prompt orchestration 主流。

---

## [3.61.0] — 2026-05-16 — [Stage 71](docs/planning/Stage_71_Roadmap.md) v5.5 Phase 2 production-ready 補強

Trial_v15+v16 揭 2 議題收口 — Petra prompt 線性整包紀律 + memory 空 content guard。

---

## [3.60.0] — 2026-05-16 — [Stage 70](docs/planning/Stage_70_Roadmap.md) v5.5 Phase 2 Petra 拆解指令精準度

hierarchical decomposition + dependency graph / SubtaskPlan + Parser + TopoSort / Backwards-compatible 4 層守護。

---

## [3.59.0] — 2026-05-16 — [Stage 69](docs/planning/Stage_69_Roadmap.md) v5.5 Phase 2 跨 session 長期持久記憶基底

TaskMemory + TalentMemory schema 整合 v5.5 dispatch。v2.1 scope pivot TaskGroup → PetraSession 修 Aria 漏掃 v5.5 path 根因。

---

## [3.58.0] — 2026-05-16 — [Stage 68](docs/planning/Stage_68_Roadmap.md) v5.5 Phase 1 完整收口前 production-ready 補強

AppendMessage async + v5 PoC post-confirm 收尾 + ef-core.md nullable unique pattern 升級。

---

## [3.57.0] — 2026-05-15 — [Stage 67](docs/planning/Stage_67_Roadmap.md) v5.5 升級首發 Phase 1 Talent-Skill separation 重構基底

架構級重構 / Migration `Stage67TalentSkillSeparation` / Trial_v13 啟動條件達成。

---

## [3.56.0] — 2026-05-14 — [Stage 66](docs/planning/Stage_66_Roadmap.md) v5 動態架構 Trial_v11 議題收口

production-ready 補強第三波 / v5 上線前最後一個工程 Stage / Trial_v12 啟動條件達成。

---

## [3.55.0] — 2026-05-14 — [Stage 65](docs/planning/Stage_65_Roadmap.md) v5 動態架構 Trial_v10 議題收口

production-ready 補強第二波 + 結案後 merge feature/v5-poc → main。

## [3.54.0] — 2026-05-13 — [Stage 64](docs/planning/Stage_64_Roadmap.md) v5 動態架構 production-ready 收口（Trial_v9 揭 7 議題 + Stage 63A errata）

## [3.53.0] — 2026-05-12 — [Stage 63B](docs/planning/Stage_63B_Roadmap.md) FF 三十六 Phase B PoC spike v5 動態架構 production-ready 實作 + Mock 全綠

## [3.52.0] — 2026-05-11 — [Stage 63A](docs/planning/Stage_63A_Roadmap.md) FF 三十六 Phase B 動態決策 API spike ✅ 硬通過 — 揭 Magentic 命名不存在 + 2 framework limitation（Stage 63B 戰略級早期 derisk）

## [3.51.0] — 2026-05-11 — [Stage 62](docs/planning/Stage_62_Roadmap.md) FF 三十六 Phase B Charter spike — v5 動態架構規劃文件 deliverable

## [3.50.0] — 2026-05-10 — [Stage 61](docs/planning/Stage_61_Roadmap.md) Petra/Cody prompt 對齊群組 + Pipeline UI refresh + Dashboard 補強（Trial_v8 開跑前最後清掃）

## [3.49.0] — 2026-05-10 — [Stage 60](docs/planning/Stage_60_Roadmap.md) FF 五十五 — v4 framework 邊角 user actions legacy 遷移 + meeting subprocess fail-fast 統一（議題 C2/H1 收口 + Trial_v7 反例修根因驗證）

## [3.48.0] — 2026-05-10 — [Stage 59](docs/planning/Stage_59_Roadmap.md) FF 五十四子項 1 — TaskGroupService 怪物大檔拆解 -54% 瘦身（4 子 service / Boss/Epic/Routing 3 子目錄）

## [3.47.0] — 2026-05-10 — [Stage 58](docs/planning/Stage_58_Roadmap.md) v4 framework production-ready 補強第二波 — API 餘額容錯性 ⭐ Trial_v6 揭露 3 🔴 全收口 🎉

## [3.46.0] / [3.46.1] — 2026-05-09 — [Stage 57](docs/planning/Stage_57_Roadmap.md) v4 framework production-ready 補強第一波 — race condition 雙層防 + Vera fix loop HITL routing 第 6 routing

## [3.45.0] — 2026-05-05 — [Stage 56](docs/planning/Stage_56_Roadmap.md) Trial_v6 前置條件統包 — Dashboard MockScenarioCard 補全 33 場景 + FF 四十二/四十三 修 + conventions 補 2 段

## [3.44.0] — 2026-05-05 — [Stage 55B Session B](docs/planning/Stage_55B_Roadmap.md) ⭐ v4 漸進遷移第九步完整結案 — 5 routing types HITL refactor + v4 路線 9/9 達成 🎉

## [3.43.0] — 2026-05-04 — [Stage 55B Session A](docs/planning/Stage_55B_Roadmap.md) v4 漸進遷移第九步（拆 Session A/B 第一段）— PipelineHitlHelper + AppealOrchestrationService 16 處 skip 精簡 + F-α 排除條件移除

## [3.42.0] — 2026-05-04 — [Stage 55A](docs/planning/Stage_55A_Roadmap.md) v4 漸進遷移第八步（拆 55A/55B 第一段）Kickoff/Design 整合到 Pipeline + sub-task 整合 + 6 hooks 移除 + 刪 WorkflowEngine.cs

## [3.41.0] — 2026-05-04 — [Stage 54](docs/planning/Stage_54_Roadmap.md) v4 漸進遷移第七步 Crash Recovery 全切 + 4 CheckpointStore base class + idempotency

## [3.40.0] — 2026-05-03 — [Stage 53B](docs/planning/Stage_53B_Roadmap.md) ⭐ v4 漸進遷移第六步 子流程 + 5 fallback 移除

## [3.39.0] — 2026-05-03 — [Stage 53A](docs/planning/Stage_53A_Roadmap.md) ⭐ v4 漸進遷移第五步 macro pipeline NewFeature happy path

## [3.38.0] — 2026-05-03 — [Stage 52](docs/planning/Stage_52_Roadmap.md) v4 漸進遷移第四步 Design Meeting B3 路線

## [3.37.0] — 2026-05-02 — [Stage 51](docs/planning/Stage_51_Roadmap.md) ⭐ v4 漸進遷移第三步 framework HITL 試點

## [3.36.0] — 2026-05-02 — [Stage 50](docs/planning/Stage_50_Roadmap.md) v4 漸進遷移第二步

## [3.35.0] — 2026-05-02 — [Stage 49](docs/planning/Stage_49_Roadmap.md) ⭐ v4 漸進遷移首發

## [3.34.0] — 2026-05-02 — [Stage 47](docs/planning/Stage_47_Roadmap.md)

## [3.33.0] — 2026-04-29 — [Stage 46](docs/planning/Stage_46_Roadmap.md)

## [3.32.0] — 2026-04-29 — [Stage 45](docs/planning/Stage_45_Roadmap.md)

## [3.31.0] — 2026-04-29 — [Stage 44](docs/planning/Stage_44_Roadmap.md)

## [3.30.0] — 2026-04-29 — [Stage 43](docs/planning/Stage_43_Roadmap.md)

## [3.29.0] — 2026-04-28 — [Stage 42](docs/planning/Stage_42_Roadmap.md)

## [3.28.0] — 2026-04-27 — [Stage 41](docs/planning/Stage_41_Roadmap.md)

## [3.27.0] — 2026-04-26 — [Stage 40](docs/planning/Stage_40_Roadmap.md)

## [3.26.0] — 2026-04-25 — [Stage 39](docs/planning/Stage_39_Roadmap.md)

## [3.25.0] — 2026-04-25 — [Stage 38](docs/planning/Stage_38_Roadmap.md)

## [3.24.0] — 2026-04-25 — [Stage 37](docs/planning/Stage_37_Roadmap.md)

## [3.23.0] — 2026-04-22 — [Stage 36](docs/planning/Stage_36_Roadmap.md)

## [3.22.0] — 2026-04-22 — [Stage 35](docs/planning/Stage_35_Roadmap.md)

## [3.21.0] — 2026-04-22 — [Stage 34](docs/planning/Stage_34_Roadmap.md)

## [3.20.0] — 2026-04-22 — [Stage 33](docs/planning/Stage_33_Roadmap.md)

## [3.19.0] — 2026-04-21 — [Stage 32](docs/planning/Stage_32_Roadmap.md)

## [3.18.0] — 2026-04-20 — [Stage 31](docs/planning/Stage_31_Roadmap.md)

## [3.17.0] — 2026-04-20 — [Stage 30](docs/planning/Stage_30_Roadmap.md)

## [3.16.1] — 2026-04-19 — Hotfix

MockMode 提案核准重複建 TaskGroup bug 修正（Dashboard 路徑補 GroupId 防護對齊 Discord 路徑）

## [3.16.0] — 2026-04-19 — [Stage 29](docs/planning/Stage_29_Roadmap.md)

## [3.15.0] — 2026-04-17 — [Stage 28b](docs/planning/Stage_28b_Roadmap.md)

## [3.14.0] — 2026-04-17 — [Stage 28a](docs/planning/Stage_28a_Roadmap.md)

## [3.13.0] — 2026-04-16 — [Stage 27b](docs/planning/Stage_27b_Roadmap.md)

## [3.12.0] — 2026-04-16 — [Stage 27a](docs/planning/Stage_27a_Roadmap.md)

## [3.11.0] — 2026-04-14 — [Stage 26](docs/planning/Stage_26_Roadmap.md)

## [3.10.0] — 2026-04-14 — [Stage 25b](docs/planning/Stage_25b_Roadmap.md)

## [3.9.0] — 2026-04-14 — [Stage 25a](docs/planning/Stage_25a_Roadmap.md)

## [3.8.0] — 2026-04-13 — [Stage 24](docs/planning/Stage_24_Roadmap.md)

## [3.7.0] — 2026-04-12 — [Stage 23](docs/planning/Stage_23_Roadmap.md)

## [3.6.0] — 2026-04-12 — [Stage 22](docs/planning/Stage_22_Roadmap.md)

## [3.5.0] — 2026-04-11 — [Stage 21](docs/planning/Stage_21_Roadmap.md)

## [3.4.0] — 2026-04-11 — [Stage 20](docs/planning/Stage_20_Roadmap.md)

## [3.3.0] — 2026-04-10 / 04-11 — [Stage 19](docs/planning/Stage_19_Roadmap.md)

## [3.2.0] — 2026-04-09 — [Stage 18](docs/planning/Stage_18_Roadmap.md)

## [3.1.0] — 2026-04-08 — [Stage 17](docs/planning/Stage_17_Roadmap.md)

## [3.0.0] — 2026-04-07 — [Stage 16](docs/planning/Stage_16_Roadmap.md)

## [2.4.0] — 2026-04-06 — [Stage 15](docs/planning/Stage_15_Roadmap.md)

## [2.3.0] — 2026-04-06 — [Stage 14](docs/planning/Stage_14_Roadmap.md)

## [2.2.0] — 2026-04-06 — [Stage 13](docs/planning/Stage_13_Roadmap.md)

## [2.1.0] — 2026-04-06 — [Stage 12](docs/planning/Stage_12_Roadmap.md)

## [2.0.0] — 2026-04-05 — [Stage 11](docs/planning/Stage_11_Roadmap.md)

## [1.4.0] — 2026-04-03 — [Stage 10](docs/planning/Stage_10_Roadmap.md)

## [1.3.1] — 2026-04-04 — Hotfix

Stage 10 驗收後 7 項修正（Race Condition / IssueUrls 重複 / PushStatus / dead code 清理 / EF Index）

## [1.3.0] — 2026-04-03 — [Stage 9](docs/planning/Stage_9_Roadmap.md)

## [1.2.0] — 2026-04-02 — [Stage 8](docs/planning/Stage_8_Roadmap.md)

## [1.1.0] — 2026-04-02 — [Stage 7](docs/planning/Stage_7_Roadmap.md)

## [1.0.0] — 2026-04-01 — [Stage 6](docs/_archive/early-stages/Stage_6_Roadmap.md)

## [0.4.0] — 2026-04-01 — [Stage 5](docs/_archive/early-stages/Stage_5_Expansion.md)

## [0.3.0] — 2026-03-31 — [Stage 4](docs/_archive/early-stages/Stage_4_Dashboard.md)

## [0.2.0] — 2026-03-31 — [Stage 3](docs/_archive/early-stages/Stage_3_Agents.md)

## [0.1.0] — 2026-03-31 — [Stage 1](docs/_archive/early-stages/Stage_1_Design.md) + [Stage 2](docs/_archive/early-stages/Stage_2_Foundation.md)
