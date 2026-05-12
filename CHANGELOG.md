# Changelog

本專案所有重要變更紀錄於此檔。

格式參照 [Keep a Changelog 1.1.0](https://keepachangelog.com/zh-TW/1.1.0/)，版本號遵循 [Semantic Versioning](https://semver.org/lang/zh-TW/)。
工作單位為 **Stage**，每完成一個 Stage 通常對應 minor 版本 bump。每條 entry 的細節見對應 Stage Roadmap；更深的實作 commit 訊息見 git log。

未實作功能候選見 [Future_Feature.md](docs/planning/Future_Feature.md)。

---

## [Unreleased]

- **Stage 63B 完成（v3.53.0）— FF 三十六 Phase B PoC spike v5 動態架構 production-ready 實作 + Mock 全綠** — 9 子項全部完成 / dotnet test 153 passed（+19 new Petra test）/ main branch 0 動 / feature/v5-poc branch + feature flag default=false v4 production 0 影響。**路線 A 拍板對應實作**：限制 (a) workaround 自寫 PetraOrchestratorService.DecideAsync + BuildSequential（不走 framework GroupChat loop）+ 限制 (b) workaround Worker 走 ChatClientAgent + ClaudeCodeChatClientAdapter : IChatClient（三層 wrapper 真實生效）。**FF 三十六 status 升「進行中 Charter ✅ + API spike ✅ + PoC ✅ + Trial 待」**。
- **下個動作候選**：① **Trial_v9 試驗計劃**（feature/v5-poc branch + Christ 切 feature flag `Workflow:UsePetraOrchestratorV5=true` + Petra Provider→Gemini Flash + 跑 Trial_v6/v7/v8 同 prompt + 5 向對照 v5/v6/v7/v8/v8-dynamic + 7 驗證項 #4 Crash Recovery + #6 遷移成本量化 真實任務驗證 / LLM cost ~$5-15 / 建議 Christ 儲值至 ≥ $30 buffer 才開跑，餘額 $17.22）② Trial_v9 結案後 Christ 拍板路線 A vs B vs C vs D 戰略大重評估關鍵實證

---

## [3.53.0] — 2026-05-12 — [Stage 63B](docs/planning/Stage_63B_Roadmap.md) FF 三十六 Phase B PoC spike v5 動態架構 production-ready 實作 + Mock 全綠

Stage 63B = FF 三十六 Phase B PoC spike — 對齊 Stage 62 Charter + Stage 63A API spike 揭 2 framework limitation 戰略級早期 derisk 後的 production-ready 實作 + Mock 全綠。**Stage 跟 Trial 分開**（Christ 2026-05-11 拍板）— Stage 63B = PoC 架構基底 + Mock 全綠（0 LLM cost）/ Trial_v9 = Stage 63B 結案後另開 `docs/experiments/Trial_v9_Plan.md`（對齊 Trial_v2-v8 既有獨立試驗計劃模式）。**Branch 策略**：feature/v5-poc 開發 / main 完全不動 / feature flag `Workflow:UsePetraOrchestratorV5` default=false v4 production 0 影響 / 失敗 branch 不 merge / Stage 64+ 全量遷移時才 merge。**路線 A 拍板對應實作**（對齊 Stage 63A spike 已驗 path + framework 投資保留 + 三層 wrapper 真實生效）：① **限制 (a) workaround** — 自寫 [`PetraOrchestratorService.DecideAsync`](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs) + `AgentWorkflowBuilder.BuildSequential` + `InProcessExecution.RunStreamingAsync` events 訂閱（不走 framework GroupChat loop）② **限制 (b) workaround** — Worker.CreateAgent factory pattern 包 `ChatClientAgent` + [`ClaudeCodeChatClientAdapter : IChatClient`](src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs)（7 capability dispatch 到 IClaudeCodeService 7 真實 method — RunAsync / RunReadOnlyAsync / RunVictoriaAsync / RunQaAsync / RunReviewAsync / RunMeetingSessionAsync）。**12 新建檔**：PetraSession + PetraSessionMessage entity + PetraSessionRepository + EF Migration `Stage63PetraSessionTables`（82+879+85 行 auto-gen）+ IAgentTool interface（factory pattern `CreateAgent(ctx) → AIAgent`）+ AgentCapabilityAttribute + PetraSessionContext record + ClaudeCodeChatClientAdapter + PetraOrchestratorService + PetraOrchestratorResult + PetraSessionRecoveryService hosted service（Bot 重啟 rebuild context — 紀律：重啟重跑不從 checkpoint resume）+ PetraOrchestratorServiceTests xUnit 179 行（7 test method × 多 InlineData = 26 case + InMemory EF Core test）。**修改檔**：AppDbContext +25（2 DbSet + 2 entity config）/ WorkflowSettings +7（UsePetraOrchestratorV5 flag）/ WorkflowSettingsResolver +5 / CeoAgentService +15（flag forward）/ 7 Worker 各 +14-15（IAgentTool factory + capability attr）/ Program.cs +15（DI multi-registration + PetraOrchestratorService scoped + hosted service register）/ CLAUDE_Petra.md 全砍 -206+255 行（質變定位：品質審核閘門 → 全程動態 orchestrator + FF 五十九 hand-off v5 PoC 期間紀律段）/ 7 partial CLAUDE_*.md 各 +2 行（FF 五十九 hand-off only — Victoria +7 含移除 codebase scan 段）/ Directory.Build.props v3.53.0。**Aria gate1 第二輪通過放行**（v1.1 修正三點全完整：critical 路線 A + important Victoria 簡化紀錄 + nice-to-have xUnit only 自決紀錄）+ **2 點 healthy 範圍變更紀錄到 Roadmap v2.0**：① 7 partial CLAUDE_*.md 簡化為 FF 五十九 hand-off only（Mock 階段足夠 / Stage 64+ 全量遷移時才補完整 partial 重寫）② Forge 揭 7 Worker.CreateAgent 重複 abstract PetraWorkerHelper.cs（49 行）對齊 Stage 36+ 抽 helper SOP 精神。**Charter 04 inconsistency 揭露補釘**（Aria grep source of truth 紀律）：「9 Worker」實際 7 個 AgentService / 「RunWriteAsync」實際 `RunAsync`。**dotnet build 0 error / dotnet test 153 passed**（134 baseline + 19 new Petra test — Stage 63A 含 3 spike test silently pass + Stage 63B 16 new case — 未來新 Stage 看到 153 應對齊本 entry 紀錄理解）。**Aria 校準錨 待 Christ 補 Forge context 數字**（規模 L + 架構級新建 + EF Migration + 7 worker + 8 prompt + feature flag — 預估 mid 750K / 對齊 production-ready 補強 4 Stage 區間 ×0.78-0.99 mid 中段 + Stage 63A 早期 derisk 範圍可控）。**FF 三十六 status 升「進行中 Charter ✅ + API spike ✅ + PoC ✅ + Trial 待」**。**戰略主軸**：Stage 63B PoC 架構基底 ✅ Mock 全綠 → Trial_v9 啟動條件達成（feature/v5-poc branch + 5 向對照 + 7 驗證項真實任務驗證 + LLM cost ~$5-15 / 建議 Christ 儲值至 ≥ $30 buffer 才開跑） → Trial_v9 結案後 Christ 拍板路線 A vs B vs C vs D 戰略大重評估關鍵實證。commits：`a71931c`(規劃 main) + `8b5d339`(Forge 結案 feature/v5-poc / main 0 動)。

## [3.52.0] — 2026-05-11 — [Stage 63A](docs/planning/Stage_63A_Roadmap.md) FF 三十六 Phase B 動態決策 API spike ✅ 硬通過 — 揭 Magentic 命名不存在 + 2 framework limitation（Stage 63B 戰略級早期 derisk）

Stage 63A = FF 三十六 Phase B PoC 拆出的「最小 API 驗證 spike」（Stage 62→63 拆 Stage 模式延伸到 63A→63B — 廉價先驗驗證項 #2 unknown 過了才 commit 63B 大投資 8 prompt 重寫 + 9 Worker 改介面 + EF Migration + Trial_v9）。**throwaway prototype + spike notes 文件 deliverable**：main branch / 0 production code 改動 / 0 EF Migration / 0 DI 結構改動 → Stage 60+61 既有 11 Mock 場景 + dotnet build 0 error / dotnet test baseline 維持自動對齊 0 regression。**✅ 硬通過**（Christ AI Studio Gemini 2.5 Flash key 真打 3 場景 trigger 命中率 100% / 真實 cost $0 免費 tier + 1 次 503 retry success）：① 修 typo → `Cody`（1 agent）→ 1-on-1 trigger ② 跨 5 元件 → `Cody → Vera`（2 agents）→ Design trigger ③ 架構級重構 → `Cody → Vera → Cody → Vera`（4 agents 多輪）→ Kickoff trigger（對齊 Charter `01_Spike_Plan.md` 驗證項 #2 預期數據 100%）。**核心 finding**：Charter 候選 `MagenticOrchestrator<TState>` **不存在於 nuget 1.3.0**（無「Magentic」命名空間 — Aria 想像錯誤）— 動態決策真實 hook = `Microsoft.Agents.AI.Workflows.GroupChatManager.SelectNextAgentAsync` override + `AgentWorkflowBuilder.CreateGroupChatBuilderWith` + `HandoffWorkflowBuilder` 替代 pattern + `AIAgentExtensions.AsAIFunction` Worker-as-Tool 真實 API + `InProcessExecution.RunStreamingAsync` Workflow 執行入口（既有 Stage 49+ 已用）。**驗證項 #2 失敗條件未命中** — 命名落差 ≠ 失敗，Charter 用 errata 補釘到 spike notes 不整篇重寫。**⚠️ 2 framework limitation 揭露**（Phase 2 真打過程中新發現 — 對 Stage 63B 規劃戰略級價值）：① **Limitation (a)**：base `GroupChatManager` subclass **不啟動 manager loop**（GroupChatHost executor 收 input 立刻完成 1 superstep / manager 3 hook 全 0 invoke — 推測 nuget 1.3.0 stable 對 base subclass 未啟動 manager loop / 需 internal seam）→ **Stage 63B 走候選 (B) 自寫 PetraOrchestratorService + BuildSequential**（spike 已驗 path）② **Limitation (b)**：base `AIAgent` subclass **不被 framework workflow dispatch**（MockWorkerAgent.RunCoreAsync 兩 override 都 0 invoke — 推測 framework executor wrapper depends on ChatClientAgent-specific path / IChatClient 注入）→ **Stage 63B 必走 `ChatClientAgent(IChatClient, ...)` ctor + 新建 `ClaudeCodeChatClientAdapter : IChatClient`**（包既有 ClaudeCodeService — 從「可選」升「必走」）。**範圍變更接受 +13%**：prototype 113 行（超 Roadmap 100 行上限 13%）— 揭 framework limitation 後改 `DecideAsync` + `BuildSequential` path + assertion 從「Worker call count」改「PetraDecisions 動態分流序列」聚焦核心命題 — 接受因為揭 framework limitation value 巨大（Stage 63B 早期 derisk）。**5 deliverable**：① `PetraSpikePrototype.cs` **113 行 throwaway**（163→113 移除 GroupChatManager 死路徑 reference 留 spike notes）② `PetraSpikePrototypeTests.cs` 改 assertion 聚焦 `PetraDecisions` ③ `05_Stage_63A_Spike_Notes.md` 5 段完整（nuget API + 3 場景**實測 log** + 2 framework limitation + EF Migration (c) 拍板 + Charter 8 條 + 5 自決點對齊 + Stage 63B 範圍校準 + FF 五十九 hand-off + 結論升「軟通過」→「**硬通過**」）④ `04_Stage_63_PoC_Roadmap_Draft.md` errata 子項 1 候選 (B) 自寫 orchestrator + 子項 4 IChatClient adapter 升「必走」⑤ `Directory.Build.props` v3.51.0 → v3.52.0。**Christ 拍板補充**：EF Migration 路線 **(c) in-memory session**。**Aria gate0 揭露 3 點 Forge 全收**（test path 修正 / FF 五十九 hand-off / Gemini env key 對齊）。**Forge spike 新增第 6 自決點**（跨 assembly `protected` not `protected internal` override CS0507 修正）。**dotnet test baseline 漂移 131 → 134**（3 spike test silently pass 無 GEMINI key / 真打時 3 個 PASS — 未來新 Stage 看到 134 應對齊本 entry 紀錄理解）。**Aria 校準錨 待 Christ 補 Forge context 數字**（spike 規模 S + throwaway prototype + 文件 deliverable 混合型 — 預估 ~200-300K，對齊 Stage 62 ×0.71 純文件 vs Stage 49 ×0.78 spike 混合型）。**戰略主軸**：Stage 63A spike ✅ **硬通過** → **Stage 63B PoC spike 啟動條件達成（含 2 framework limitation 戰略級早期 derisk）**。Stage 63B 必含：feature/v5-poc branch + 候選 (B) 自寫 PetraOrchestratorService + BuildSequential + ClaudeCodeChatClientAdapter : IChatClient + 8 CLAUDE_*.md prompt 重寫 + 9 Worker 走 ChatClientAgent + Capability attribute + EF Migration `Stage63PetraSessionTables` + Trial_v9 5 向對照 / 規模 L / cost ~600-1000K + LLM cost ~$5-15。commits：`a065082`(規劃) + `591b36f`(主實作 + 軟通過結案) + `b56ffd0`(自驗第二輪真打 + 升硬通過 + 揭 2 framework limitation)。

## [3.51.0] — 2026-05-11 — [Stage 62](docs/planning/Stage_62_Roadmap.md) FF 三十六 Phase B Charter spike — v5 動態架構規劃文件 deliverable

Stage 62 = FF 三十六 Phase B Charter spike（**Christ 2026-05-10 拍板路線 D** — Trial_v6/v7/v8 連續揭 6 🔴 + deliver 度持續倒退 = infinite loop 真實實證 = v4 hierarchical static 補強 ROI 為負 → 對齊 6 個月前 brainstorm 既有 roadmap 第 4 選項）。**兩階段拆分**（對齊 Stage 51 spike Charter 模式 + Stage 53A 拆 Stage 模式）：Stage 62 Charter spike + Stage 63 PoC spike — Charter 通過才 commit PoC 投資。**純文件 deliverable**：不動 production code / 不動 EF Migration / 不動 DI 結構 → Stage 60+61 既有 11 Mock 場景仍綠 + dotnet test 131 passed 自動對齊 0 regression。**4 deliverable**：① [`docs/architecture/v5_charter/01_Spike_Plan.md`](docs/architecture/v5_charter/01_Spike_Plan.md) 7 驗證項細節（Victoria Router / Petra 自主調度 / per-task session / Crash Recovery / Mock Gemini Flash / 遷移成本量化 / Hybrid 會議 trigger）+ 預測項（強信心 5 / 中信心 1 / 未知 1）② [`02_Architecture_Wire.md`](docs/architecture/v5_charter/02_Architecture_Wire.md) 4 層 Hierarchy 落具體 service / DI（含 per-task session 多 row table schema 候選 + Tool Set Capability attribute+interface hybrid 候選 + 9 Worker capability mapping）③ [`03_v4_Code_Audit.md`](docs/architecture/v5_charter/03_v4_Code_Audit.md) 三類分類 + LoC 量化（**吸收 ~16,061 LoC ~26%** / 重寫 ~3,991 LoC + 925 prompt 行 ~7% / 全保留 ~38,700+ LoC ~67% — v4 投資保留 + 重寫 = 73% 對齊「換引擎不換車身」精神 + Aria 規劃預估吸收 ~6K 自省揭露補強對齊 +167% 超預估）④ [`04_Stage_63_PoC_Roadmap_Draft.md`](docs/architecture/v5_charter/04_Stage_63_PoC_Roadmap_Draft.md) PoC 6 子項 + 5 向對照 + 規模 L / cost ~600-1000K / 驗收標準。**8 條 Christ 拍板對齊**（Charter 文件 only / Charter main + PoC branch / 保留 v4 不動 / 保留 10 Agent / 同 prompt 任務 / Forge spike healthy 模式 / Stage 51 spike Charter 模板 / minor bump）。**5 Forge spike 自決點 Aria gate1 全通過**：① per-task session 多 row table schema（vs JSON column / 獨立 schema 三選 — 多 row 對齊既有 EF Core PostgreSQL pattern + Stage 27 DB-as-Queue 多 row reference）② Petra prompt 全砍重寫（vs partial 修 — 質變定位「品質審核閘門」→「全程動態 orchestrator」）③ Tool Set Capability attribute + IAgentTool interface hybrid（vs DB-driven runtime 配置 — compile-time + runtime 雙保險）④ v4 audit LoC 量化方法（wc -l + Glob + Grep 工具組合 partial read 紀律）⑤ docs/architecture/v5_charter/ 新資料夾（vs Stage_62_Roadmap inline / docs/planning 多檔）。**FF 動態**：✅ FF 五十七 / 五十八 / 五十九 / 六十（4 個 close 不做 — v5 動態架構吸收）+ FF 三十六 升「🟡 進行中 Phase B Charter spike #1」+ FF 五十四子項 2/3「保留評估」+ FF 二十五/四十六/四十八「保留」（Cody Worker prompt v5 仍適用）。**Top 5 重排**：FF 三十六 #1（進行中 Charter spike）/ Stage 63 PoC 候選 #2 / FF 五十四 #3（子項 1 ✅ Stage 59 / 子項 2/3 待）/ 戰略大重評估候選 #4（Charter+PoC 後）/ 二十五/四十六/四十八 保留群組 #5。**Aria 規劃預估校準揭露**：Aria 預估吸收 ~6K vs 實際 ~16K（+167% 超預估）— 主因 Workflows 全資料夾 7864 LoC + Meeting legacy 2331 LoC + Pm/* 1415 LoC + Appeal Orchestration 1375 LoC 累積遠超預期，Aria 自省揭露補強對齊。**dotnet build 0 error / dotnet test 131 passed 預期自動對齊**（純文件 deliverable）。**Trial_v8 結案 ⭐ 戰略級成功 vs 業務級失敗**：cost $1.2023 / 13 LLM call / 揭 2 🔴 戰略級新類型（Trial 試驗框架認知錯位升級 + 第 7 routing retry/abort silent 卡死）+ 連續 3 Trial 揭 6 🔴 = infinite loop pattern 真實實證 = 戰略大重評估時機到 → Christ 拍板路線 D。詳見 [Stage 62 Roadmap](docs/planning/Stage_62_Roadmap.md) + [v5_charter/ README](docs/architecture/v5_charter/README.md)。

## [3.50.0] — 2026-05-10 — [Stage 61](docs/planning/Stage_61_Roadmap.md) Petra/Cody prompt 對齊群組 + Pipeline UI refresh + Dashboard 補強（Trial_v8 開跑前最後清掃）

Stage 61 = Trial_v8 開跑前最後一塊清掃 — Stage 60 收口 1 🔴（v4 邊角 user actions legacy）後，剩 Trial_v6/v7 揭露的 6 🟡 系統性議題群組一次清完，避免 noise 干擾 Trial_v8 v4 framework ROI 純度量化。**子項 8 全 PASS**：① **Petra prompt 5 位置同步紀律段**（FF 五十六 — CLAUDE_Petra.md +「議題層次紀律 + 給定見紀律 + 工時禁字紀律」段 + 4 個 prompt builder 共用 AppendPetraDisciplineSection helper / KickoffPrompts 2 method + DesignPrompts 2 method）② **Cody prompt 對齊群組**（FF 二十五 Dev_plan 結構規範新段 + FF 四十六 ImplementationNote 強制標題 + Sage 備援 source DocAgentService prompt 引導 PR Body / git log fallback + CLAUDE_Sage.md 品質下限改 fallback path + FF 四十八 Cody Dev_plan maxTurns 從 default **10** 提升至 80，IClaudeCodeService.RunReadOnlyAsync 加 int? maxTurns 參數 4 處同步擴 + 3 處既有 caller 加 named ct: 對齊）③ **議題 #B Reload 修根因**（KickoffStageExecutor.HandleEntryAsync line 99 加 db.Entry(group).ReloadAsync — inner Workflow 子 executor 各自 scope 寫 DB UPDATE，outer scope DbContext tracking cache 沒拿最新 → embed 顯示「無計劃書」修根因；Stage 60 modify path 同 scope 同 entity reference 不踩同類根因）④ **Dashboard token IsEstimated 視覺**（FF 五十 — TokenAgentSummaryDto 加 HasEstimated + InternalController + DashboardTokenService 兩處聚合查詢加 BOOL_OR + TokenMonitoring.razor 卡片 MudIcon Warning + Tooltip + 表格 row icon + cost「~」前綴）⑤ **Christ action supersede + intervention 動態化**（FF 四十五 — ButtonCallbackRouter SupersedePriorFailedTasks helper 兩處呼叫 + TaskGroupService.MarkGroupDoneOrInterventionAsync InterventionReason 動態列出真實 escalate source）⑥ **Dashboard epic UI 接線**（FF 四十 — PipelineList row IsEpic / sub-task 視覺標 + EpicPaused chip / PipelineView Epic section 顯示 sub-task 列表 + ⏸️ 暫停 epic / ▶️ 恢復 epic 按鈕）⑦ Mock 場景 7 alias ⑧ v3.50.0 bump。**Forge spike 5 處自決全 Aria gate 通過**：Petra 紀律段 5 位置 inline + 2 prompts class 各自 helper / Cody Dev_plan maxTurns 靜態 80 / Sage 備援 source ~25 LOC ≤ 100 條件納入 / 議題 #B Reload / generic intervention 動態化 ~8 LOC ≤ 50 條件納入。**Forge 自驗 7 Mock 場景**（5 PASS + 2 Christ 視覺驗收 PASS 含場景 7 follow-up fix Stage 46 後端 SubTasks 漏填 自抓自修 commit `dca1830`）。**Forge 自驗能力物理限制揭露**：SupersedePriorFailedTasks 依賴 Discord button callback path（MockMode auto-approve 走 dashboard source 不踩）+ generic intervention 動態化 `??=` 不踩已 set specific reason 場景 → Trial_v8 真實 Christ 互動才驗。**範圍縮小 YAGNI 揭露**：SupersedePriorFailedTasks 只 cover Dev_plan path 兩處（escalate_devplan_skip / abort）— 其他 3 申訴 path 沒實證同類，立 **FF 五十八** Trial_v8 後重新評估 candidate standby。**dotnet build 0 error / dotnet test 131 passed**。**Aria 校準錨 ×0.99**（419K vs 預估 mid 425K，混合型第 15 資料點 mid 中段，接近 Stage 50 ×1.09 / Stage 56 ×0.92 / Stage 60 ×0.80 — production-ready 補強 4 Stage 區間 ×0.78-0.99 驗證）。**結案第二段 step 0 升級 4 處**：① **workflow_aria.md 第 7 條延伸**（加「prompt builder 檔名 + method 數量 + config default value」進 source of truth 範圍 — Stage 56→59 port 5052 + Stage 57+58 5051 + Stage 61 prompt 檔名 + maxTurns default 同類根因第三次累積修根因）② **workflow_aria_session_lessons.md 自省點 #29**（Aria 規劃 prompt builder / config 數值漏 grep 同類根因第三次累積 — 第 7 條紀律延伸具體化原因紀律）③ **新立 FF 五十七** Petra prompt 5 位置 SoT 維護紀律 candidate standby（5 位置漂移風險，是否抽 prompt template helper 累積到必要時拆 Stage）④ **新立 FF 五十八** 其他 3 申訴 path supersede 評估 candidate standby（Trial_v8 真實使用揭露才動工）。**驗收期間 3 commits**（1 fix + 2 docs）已對齊「驗收期分工空隙」紀律 Forge 自補完整。**Trial_v8 前置條件全綠**。詳見 Stage 61 Roadmap v2.1。commits：`628a52e`(規劃) + `218f150`(主實作) + `a9afe84`(Roadmap v2.0) + `dca1830`(SubTasks 補抓 fix) + `b299938`(Roadmap v2.1)。

## [3.49.0] — 2026-05-10 — [Stage 60](docs/planning/Stage_60_Roadmap.md) FF 五十五 — v4 framework 邊角 user actions legacy 遷移 + meeting subprocess fail-fast 統一（議題 C2/H1 收口 + Trial_v7 反例修根因驗證）

Stage 60 = FF 五十五（Trial_v7 結案揭露 1 🔴 戰略級新類型議題收口）— 推翻 Trial_v6「3 🔴 收口 = v4 production-ready」假設。**Trial_v7 揭露 root cause**：Christ 點 Kickoff modify → Bot 走 legacy `KickoffMeetingService.ModifyTaskPlanAsync` → Petra subprocess 失敗 → `MeetingCommons.RunAgentTurnAsync` 三條 swallow path 全 swallow 成 placeholder string → caller silent skip 寫入 DB → TaskPlan 從 6,279 字 → 5 字「（Petra 無回應）」+ Stage 58 第 7 routing 沒 catch（因走 legacy path）。**子項拆分**：① **MeetingCommons.RunAgentTurnAsync silent failure → fail-fast 治本**（subprocess !result.Success / 空 output / catch Exception 三條 swallow path 全改 throw `MeetingSubprocessFailureException`，先檢測 LlmApiFailureException 直接 re-throw 對齊 Stage 58 既有 marker pattern）② **KickoffMeetingService.ModifyTaskPlanAsync 遷 framework Kickoff revise round**（議題 C2 收口）③ **DesignMeetingService.ModifyDesignPlanAsync 遷 framework Design revise round**（議題 H1 收口 — 對齊 Stage 52 既有 pattern）④ **Stage 58 marker pattern 擴展新 `[SUBPROCESS_FAILURE]` 命名語意**（catch path 而非 marker check — Forge spike 議題 2 修正對齊 Pipeline framework architecture）⑤ Forge spike 盤點 3 獨立 user actions 收口（kickoff_modify / design_modify / kickoff_restart + 1 modify large-impact 子分支）⑥ Mock 4 場景全 PASS（taskplan/designplan modify happy + subprocess_failure + modify_during_subprocess_failure）⑦ v3.49.0 bump。**Forge spike 揭露 3 議題 Aria gate 通過全 Forge 自決**：① catch 點在 KickoffStageExecutor / DesignStageExecutor 不在 AgentQueueProcessor（Meeting subprocess 不走 AgentQueueProcessor — Aria 規劃漏掃 framework 真實 architecture）② `[SUBPROCESS_FAILURE]` 命名語意 wire catch 不做 marker check（Stage Executor 收 KickoffCompletionResponse 不是 AgentExecutionResult — Aria 規劃漏掃 response type 差異）③ 新加 2 Port `KickoffAgentApiFailurePortId` + `DesignAgentApiFailurePortId` + Petra-{Stage} 命名（per-stage Port pattern 對齊 Stage 58 紀律延續，BossInteraction type 仍 reuse `agent_api_failure_intervention` 不新加 routing type）。**Forge 自加 WorkflowExceptionHelper.FindInner**（unwrap framework 1.x WorkflowErrorEvent.Exception InnerException chain — Aria 預掃不到 framework 1.x event wrap 細節，Forge 補強健康）。**4 Mock 場景全 PASS**：A taskplan_modify TaskPlan 10302 字 / B designplan_modify DesignPlan 10302 字 / C subprocess_failure → 第 7 routing fire BossInteraction agent="Petra-Kickoff" / D modify-during-failure 不 silent skip TaskPlan 仍 124 字（**Trial_v7 反例修根因驗證 ✓**）。**dotnet build 0 error / dotnet test 131 tests pass**。**Aria 校準錨 ×0.80**（438K vs 預估 mid 550K，混合型第 14 資料點 mid 帶下半，接近 Stage 56 ×0.78 / Stage 58 ×0.94 中間）— 對齊 Aria 教訓主動套入紀律生效（Trial_v7 揭露 + Stage 50/51/52/58 範本累積 + Aria gate1 通過 0 critical + Forge spike 補強 3 議題自決）。**結案第二段 step 0 升級**：① workflow_aria_session_lessons.md 自省點 #27（Aria 規劃 framework Workflow decision routing 必連帶 grep Pipeline Stage Executor 對應 decision BossInteraction 開卡行為 + Pipeline 接管條件擴範圍配對紀律 — Stage 60 踩坑 #4+#5 修根因）② workflow_aria_session_lessons.md 自省點 #28（aria-prep-session skill 開場 prompt 模板必對齊既有 skill 觸發紀律 — Stage 60 揭露 Aria 開場 prompt 漏對齊 forge-self-verify 觸發紀律 + 已立修紀律 2）③ workflow_aria.md 第三節 A 第 8 條（Aria 規劃 Pipeline 接管 decision 時計劃書必明列「同步 Stage Executor case 處理」+「decision 拓撲 + BossInteraction 開卡行為」配對檢查）。**驗收期間 4 commits**（Forge 自診修補強 + Roadmap v2.0）已對齊「驗收期分工空隙」紀律 Forge 自補完整。**0 follow-up commits 待 Aria 補**。詳見 Stage 60 Roadmap。commits：`6993858`(規劃) + `8d0637d`(主實作) + `32b2bc3`(MockMode auto-approve scenario-aware) + `0962509`(framework_modify_designplan_happy Petra escalate path) + `5eefeb8`(Forge 實作紀錄) + `0c1b4ea`(Roadmap v2.0)。

## [3.48.0] — 2026-05-10 — [Stage 59](docs/planning/Stage_59_Roadmap.md) FF 五十四子項 1 — TaskGroupService 怪物大檔拆解 -54% 瘦身（4 子 service / Boss/Epic/Routing 3 子目錄）

Stage 59 = FF 五十四子項 1（最痛 + 最大）— Stage 36 後 v4 漸進遷移路線（49-58）一路加 routing / NotifyBoss / TryRoute 累積 +1043 行 → TaskGroupService 從拆解後 baseline 716 漲到 **1759 行**超 refactor-sop.md 警戒線 1.3x。對齊 Stage 34-36 FF 二十系列既有拆解 SOP 全 6 項。**子項拆分**：① 主檔 1759 → **808 行 -54%**（保留 dispatch / guard / 路由 3 主入口 method）② **BossNotificationService**（5 NotifyBoss helpers + FindChannel，208 行）③ **BossResponseHandlerService**（D 區段 4 case body，267 行）④ **EpicChainService**（Epic 自動拆任務 5 method，372 行）⑤ **PipelineRoutingService**（7 type-specific TryRoute + TryGetPipelineGroup，204 行）⑥ Boss/Epic/Routing 3 子目錄（single-theme pattern 對齊 Stage 35）。**Forge spike 揭露 3 踩坑全 SOP 升級**：① **C# child namespace shadow 規則**（namespace 與 entity 同名衝突 → TaskGroup/ 統一目錄改 Boss/Epic/Routing 3 子目錄）② **caller 改動成本評估三層分**（ctor 注入 vs IServiceProvider field vs scope.GetRequiredService — 22+ call site / 0 ctor 改動）③ **dispatch / guard / 路由型主檔瘦身典型 -50%~-60%**（vs 純拆 -73%~-85%）。**Aria 結案第二段 step 0 升級首次實踐**：refactor-sop.md SOP 2/6 + 實戰數據 v1.1 升級 + forge-self-verify skill port 5051→5052 + X-Api-Key 補（修 Stage 56→59 跨 Stage 踩坑根因）+ workflow_aria.md 第三節 A 加第 7 條「環境細節 reference 標 source of truth 不憑印象寫」。**Forge V1-V6 自驗全綠**（build / 131 tests pass / 7 routing Mock 4 type 觸發確認 dispatch + 3 type Mock scenario 範圍邊界非 regression / framework_pipeline_happy_path 完整 pipeline 跑通 / 行數驗證 / DI 啟動 0 exception）。**Aria 校準錨 ×1.09**（402K vs 預估 370K，混合型第 13 資料點 mid 中段，接近 Stage 50 ×1.09 / Stage 56 ×0.92）— **戰略結論：FF 二十系列拆解倍率從 Stage 34-36 平均 ×1.58 降到 ×1.09（-31%）**證明 SOP 累積 + workflow_aria.md 第 5+6+7 條紀律持續生效（Stage 59 計劃書 203 行 -65% vs Stage 58 v1.1 580 行 — 新立紀律首次驗證大幅超預期）。**0 follow-up commits**。**FF 五十四子項 2/3** 評估動工依 Stage 59 ROI（ButtonCallbackRouter 1091 / DevAgentService 958）— Trial_v7 後排程。詳見 Stage 59 Roadmap。commits：`acda735`(規劃) + `c7a68eb`(主實作) + `a89b700`(自驗結案)。

## [3.47.0] — 2026-05-10 — [Stage 58](docs/planning/Stage_58_Roadmap.md) v4 framework production-ready 補強第二波 — API 餘額容錯性 ⭐ Trial_v6 揭露 3 🔴 全收口 🎉

Stage 58 = Trial_v6 揭露 3 🔴 戰略級議題最後一個（FF 五十三 API 餘額容錯性，Stage 57 已收口前兩個）。**路線 A marker pattern**（v1.0 Aria「Stage Executor catch」設計疏忽 #3 — Stage Executor 與 AgentQueueProcessor 是不同 async path，throw 從未跨 callback boundary — Forge spike 揭露後 v1.1 修正為：catch 在 AgentQueueProcessor + 用 `[API_FAILURE]` summary marker 跨 callback 傳遞，對齊 Stage 53B `[BLOCKED]` 既有 marker pattern）。**子項實作**：① 新建 `LlmApiFailureException`（含 `LlmProviderType` enum Anthropic/Gemini/Unknown + `RawError` capped 500 chars）② ClaudeCodeService 3 subprocess 方法加 `DetectApiFailureSignal` helper（Credit balance / insufficient_balance / 401 / authentication_error 字串配對 + fallback 既有 result.Success=false path 不破壞）③ AnthropicProvider catch SDK exception pattern match 轉拋同 exception ④ AgentQueueProcessor specific catch（generic catch 之前）build `[API_FAILURE]` summary 前綴 result + call HandleAgentCompletedAsync 走正常 callback flow ⑤ 4 Pipeline Stage Executor `HandleResponseAsync` 第一行 marker check + fire `agent_api_failure_intervention` + yield 等 Christ + `HandleAgentApiFailureResponseAsync` **Christ 拍板真三選獨立 path**（continue → 跳該 Agent 進下游 Bridge / retry → re-invoke 同 stage / abort → SetIntervention end）⑥ 第 7 routing wiring：統一 type `agent_api_failure_intervention`（context.agent 區分）+ per-stage 4 PortId + per-stage 4 Request/Response records + 4 typed thin wrapper（路線 a 不動 ResumeWithResponseAsync 既有簽名）⑦ MockMode auto-approve 預設 `api_failure_continue`（Aria 反 Forge 提案 retry 拍板理由：retry 預設無限迴圈卡死 Mock + continue 對齊 Stage 56/57 推進精神 + 4 agent 一次跑通驗 fire interaction）⑧ 統一 1 Mock alias `framework_pipeline_agent_api_failure`。**Forge 自驗 V1-V8 全 PASS**（V2/V4 ROI skip + V3 4 agent 一次跑通驗 4 fire + dotnet test 131 passed） + 1 follow-up backlog（Dev_plan stage API failure 走既有 dev_plan_unable routing graceful — 不擴 Stage 58 範圍）。**Aria 校準錨 ×0.94**（439K vs 預估 465K，混合型第 12 資料點 mid 中段，接近 Stage 51 ×0.96 / Stage 56 ×0.92） — **戰略結論**：Stage 57 ×1.36 → 58 ×0.94 大幅下降**推翻「production-ready 補強性質倍率系統性偏高」假設**（Stage 58 0 self-diag fix vs Stage 57 4 self-diag + 1 patch race，證明 Aria 教訓主動套入 + Forge spike 揭露 callback boundary 紀律生效大幅降低 Aria 設計疏忽）。**Trial_v6 揭露 3 🔴 全收口 🎉** → 可進入 Trial_v7+ 重跑 Trial_v6 量化 v4 framework hierarchical static 真實 ROI。**規模**：21 檔變更 / 711 insertions / 8 deletions（含新檔 LlmApiFailureException.cs）。詳見 Stage 58 Roadmap。commits：`b2fac5f`(規劃 v1.1 bump) + `40737c7`(主實作) + `a69e263`(自驗結案)。

## [3.46.0] / [3.46.1] — 2026-05-09 — [Stage 57](docs/planning/Stage_57_Roadmap.md) v4 framework production-ready 補強第一波 — race condition 雙層防 + Vera fix loop HITL routing 第 6 routing

Trial_v6 揭露 3 🔴 戰略級議題前兩個合併修（FF 五十三 API 容錯獨立 Stage 58）。**FF 五十一 race condition 雙層防**：① fire 端 — 抽 `InteractionService.TryCreateUniqueInteractionAsync` helper（race-prone interaction 防雙 fire wrapper + 未來複用，BossInteractionRepository.HasPendingForGroupAndTypeAsync idempotent 鍵查詢）+ `PauseEpicAndNotifyAsync` swap to TryCreate ② handler 端 — `HandleEpicPartialPausedAsync` epic_resume / epic_abort 雙 case 加 `BeginTransactionAsync` + `AsNoTracking().FirstOrDefaultAsync` fresh read idempotent，繞過 EF tracker cache。**FF 五十二 第 6 routing**（命名 `reviewer_fix_loop_limit` 對齊 Stage 55B Session B 5 routing prefix 慣例）：actions JSON const + Request/Response records + PortId + 3 AddEdge wiring（含 reviewerStage→docStage skip_qa edge）+ ReviewerStageExecutor FixIteration≥3 case 改 fire type-specific interaction + yield 等 Christ + `HandleReviewerFixLoopLimitResponseAsync` **Christ 拍板真三選獨立 path**（mark_done → QaStageBridge 走完整 QA 給 Quinn 獨立驗證 / skip_qa → DocStageBridge 急推進 / abort → SetIntervention end）+ TaskGroupService NotifyBossReviewerFixLoopLimit + TryRoute helper + dispatch case + FrameworkPipelineRouter Resume thin wrapper + InteractionProcessor 3 label mapping + MockMode auto-approve default fix_loop_mark_done。**驗收後 patch v3.46.1**：Forge 自驗 V1 揭露 `TryCreateUniqueInteractionAsync` TOCTOU race window（HasPending → Create 兩 transaction，並行 thread 都 read 0 → 都 create → DB 真出 2 卡，functional 由 V2 handler idempotent 擋住但 UI 層 race 沒擋）→ Christ 拍板路線 a 趁熱補：partial unique index `(TaskGroupId, InteractionType) WHERE Status='pending'` + Migration `Stage57BossInteractionPendingUniqueIndex` + DbUpdateException 23505 catch（DB constraint 雙保險擋 read-then-write race window）。**4 self-diag fix + 1 patch — 全 0 escalate Forge 自診自修**（race Mock polling / auto-approve epic_resume case 補 / NpgsqlRetryingExecutionStrategy wrap CreateExecutionStrategy.ExecuteAsync 包 user transaction / 23505 catch dead code 修正 emit 正確 fix-specific log）。**Aria 校準錨**：待 Forge context 數字補。**規模**：12 檔變更（含新 Migration），不動 Stage 55B Session B 既有 5 routing dispatch 鏈路（純加第 6 routing 對齊既有 pattern）。詳見 Stage 57 Roadmap。commits：`711a010`(主) + `6ba851a`/`78a616d`/`ffe2027`(self-diag) + `500158a`(自驗 docs) + `62afaf8`/`c12ae21`(v3.46.1 patch) + `772aad2`(驗收後修正紀錄)。

## [3.45.0] — 2026-05-05 — [Stage 56](docs/planning/Stage_56_Roadmap.md) Trial_v6 前置條件統包 — Dashboard MockScenarioCard 補全 33 場景 + FF 四十二/四十三 修 + conventions 補 2 段

v4 路線 9/9 達成後第一個觀察類整理 Stage，Trial_v6 開跑前工具完備。**4 件事一氣呵成**：① Dashboard MockScenarioCard 補全 33 framework_* 場景（Stage 49-55B 全到位）② FF 四十三 修（路線 b + spike-2 選項 B）— TotalCostUsd 99.7% NULL → 100% 寫入；兩 path 中央寫入點補 + 新建 TokenCostEstimator + IsEstimated flag + Migration ③ FF 四十二 修 — TryParseDesignIssues 改 line-iteration + try-deserialize pattern + 新建 AiTeam.Bot.Tests xUnit project ④ conventions 補 2 段（WorkflowType/WorkflowStep fundamental type + Stage 48 PATHEXT 解法落地）。**Aria 閘門一 4 critical 揭露**（Stage 47 model pricing 設施前提錯誤 / API path 修法位置模糊 / AiTeam.Bot.Tests 不存在 / 兩 path 根因混淆）→ Forge Plan Mode 二輪修正 + 議題 spike-2 三選項拍 B（hardcoded dict）。**範圍變更**：子項 7 Dashboard 視覺區分跳過 → 立 FF 五十 follow-up。**0 follow-up bug**。**Aria 校準錨 ×0.92**（272K / 中位 297K，混合型第 10 資料點 mid 帶下半，10 資料點區間穩定 ×0.73-1.42）。詳見 Stage 56 Roadmap。commits：`8054f64`(主) + `43e5454`(範圍變更補正) + `e8e35ad`(自驗結果)。

## [3.44.0] — 2026-05-05 — [Stage 55B Session B](docs/planning/Stage_55B_Roadmap.md) ⭐ v4 漸進遷移第九步完整結案 — 5 routing types HITL refactor + v4 路線 9/9 達成 🎉

**v4 漸進遷移完整路線 9/9 達成 🎉** — Stage 55B Session B 完成 5 routing types HITL refactor（dev_failed_intervention / qa_failed_intervention / devplan_escalate / dev_plan_unable / split_task_proposal）— Pipeline executor 從 SetIntervention end 改 yield-resume + legacy handler 加 Pipeline 分支（議題 5 = 5A）。**核心戰略級 Forge 缺口 6 揭露**：5 type-specific BossInteraction 在 Pipeline path 下部分已 fire（不是統一 generic intervention）→ refactor 策略**比預期更輕**（不需新加 type-specific BossInteraction）。**首次拆 Session 戰術完整實踐 + Compact know-how 揭露 ⭐**：Forge 用 Compact 模式（vs 新開 Forge session）— Session A 4 戰略議題拍板脈絡保留 + 對話連貫 + Aria 工作量單線程，**比新開 session 更乾淨**。**Stage 55B 整體完整結案** — Session A（v3.43.0 PipelineHitlHelper + 16 處 skip 精簡 + F-α 移除）+ Session B（v3.44.0 5 routing HITL）= **Stage 51 試點 framework HITL pattern 全面 wire 完成**（1 type → 11 type）。**Aria 校準錨整體 ×1.42**（876K = Session A 450K + Session B 426K vs 中位 615K — **混合型新上界**：拆 Session + Compact 戰術 trade-off + 1M compact 風險低）。詳見 Stage 55B Roadmap。Session B commits：`641594d` ~ `194dff1` 10 個 + Session A `6b4c6f9` / `a484ff9`。

## [3.43.0] — 2026-05-04 — [Stage 55B Session A](docs/planning/Stage_55B_Roadmap.md) v4 漸進遷移第九步（拆 Session A/B 第一段）— PipelineHitlHelper + AppealOrchestrationService 16 處 skip 精簡 + F-α 排除條件移除

**Stage 55B Session A only**（v4 漸進遷移第九步拆 55A/55B 第一段拆 Session A/B）— Session B 留 v3.44.0。**4 戰略議題 Christ 拍板**：① **1A** proposal 留 Stage 56（Forge spike F6 揭露 ProposalConfirmationService group lifecycle 整合衝突）② **2C** Pattern A 主 + Stage 51 試點 mid_interrupt 獨立保留 ③ **3A** intervention/merge_notify 留 fire-and-forget（ack only no routing 切 yield-resume 收益 = 0）④ **4A** 拆 Session B（5 routing HITL 規模 ~600-900 LOC，**首次拆 session**）。**Session A 範圍**：① PipelineHitlHelper 共用 helper（議題 2C 比 base class 更務實）② AppealOrchestration 11 + QaCoordination 5 = 16 處 skip 全清（dead code 結論）③ F-α 排除條件 4 處移除 ④ 8 處 calling site comment ⑤ Production DB pre-check（Forge 主動，dead code 移除 production safe）。**Aria 校準錨 Session A ×0.73**（450K / 中位 615K，**半 Stage 校準錨計算非典型** — 完整 Stage 55B 校準錨等 Session B 結案後重新評估）。**Stage 55B 整體 8.5/9 達成**。詳見 Stage 55B Roadmap。commits：`6b4c6f9`(主) + `a484ff9`(結案)。

## [3.42.0] — 2026-05-04 — [Stage 55A](docs/planning/Stage_55A_Roadmap.md) v4 漸進遷移第八步（拆 55A/55B 第一段）Kickoff/Design 整合到 Pipeline + sub-task 整合 + 6 hooks 移除 + 刪 WorkflowEngine.cs

**議題 G3 真正解決** — Pipeline 從 Kickoff 階段啟動（Stage 53A 方案 C 留的核心戰略級 TODO）。4 件子工作合一：① inner FrameworkKickoffRouter / FrameworkDesignRouter 加 skipFinalize + 改回傳 Outcome record ② Pipeline 主 Workflow 加 KickoffStage / DesignStage Executor + 拓撲擴展（5→7 RequestPort + 8→10 stage）③ MeetingOrchestrationService HandleKickoff/Design ConfirmedAsync continue/stop 走 Pipeline ResumeAfterKickoff/Design ④ sub-task 整合（FireOneStepAsync entry guard 兩入口分流：parent → Kickoff / sub-task → Dev_plan + PipelineState.IsSubTask + PipelineStartExecutor 兩出口路由）⑤ HandleAgentCompletedAsync 6+1 hooks 全段移除 ⑥ WorkflowEngine.cs 精簡（保留 enum + record）。**Forge Plan Mode 主動揭露 3 個 Aria 預掃缺口拍板**（method 名已存在 / sub-task first step ≠ Kickoff / EpicChain 不依賴 6 hooks）。**Aria gate1 揭露 1 critical**（Mock 場景擴充 + Forge 自驗未做 — Forge 不知道剛寫好的 forge-self-verify skill 時序）+ 修正 commit `b6e0764`。**驗收期 follow-up #1（戰略級）**：Forge 自驗場景 E 揭露 Stage 54 既有遺留 bug（split_task_proposal MockMode auto-approve switch 漏）→ 自抓自修 commit `492d2db` — Stage 53B/54 自驗能力進化在 Stage 55A 真正生效。**Christ 視覺驗收 4 張截圖**確認場景 B/E。**Aria 校準錨 ×0.88**（482K / 中位 545K，混合型第 8 資料點 mid 中段；8 資料點區間穩定 ×0.73-1.25）。**9 Stage 遷移 8/9 達成**。詳見 Stage 55A Roadmap。commits：`1cddaef`(主) + `b6e0764`(Aria fix) + `492d2db`(split fix) + `b9365a6`(v2.0)。

## [3.41.0] — 2026-05-04 — [Stage 54](docs/planning/Stage_54_Roadmap.md) v4 漸進遷移第七步 Crash Recovery 全切 + 4 CheckpointStore base class + idempotency

純機制升級 + 重構 + idempotency 加固，無新功能。4 件事一氣呵成：① 抽 4 CheckpointStore base class（833 → 360 行 **-473 淨減**，`FrameworkCheckpointStoreBase<TStore>` generic）② 3 router RecoverStuck*Async 升級 ResumeStreamingAsync（對齊 Stage 53A Pipeline 議題 12 既有 know-how）③ **B2 round-aware idempotency 戰略級修正**（B1 → B2：Forge gate1 揭露 B1 用 state.IssueUrls check 會破壞 needs_adjustment 多輪業務 → Christ 重拍板「Adjustment 觸發都會踩」→ TaskGroups 加 LastIssueCreatedRound int? + Migration `Stage54TaskGroupIssueCreatedMarker`）④ Stage 53B follow-up #1/#2/#4 搭車（MarkGroupDoneOrIntervention 廣義化 / MockMode auto-approve hook / MockClaudeCodeService [Obsolete]）。**驗收 8 場景全綠 + 1 follow-up bug 修復**（Forge 自驗時自抓自修：MockMode auto-approve source='mock' → 'dashboard' — 84bd874）。**Aria 校準錨 ×0.77**（421K / 中位 545K，混合型第 7 資料點 mid 帶下半 — 接近 Stage 53A ×0.73；7 資料點區間穩定 ×0.73-1.25）。**8 Stage 遷移 7/8 達成**，剩 Stage 55 戰略級收尾。詳見 Stage 54 Roadmap。commits：`36033f0`(主) + `84bd874`(fix) + `3c192e3`(驗收) + `82317cd`(v2.0)。

## [3.40.0] — 2026-05-03 — [Stage 53B](docs/planning/Stage_53B_Roadmap.md) ⭐ v4 漸進遷移第六步 子流程 + 5 fallback 移除

NewFeature 主路徑 + 子流程**完整 Pipeline framework 化**達成。11 議題拍板：A1 一個 Stage 全切 / B1 fix loop loop back / C1 拓撲擴張 / D1 4 子流程 + 5 fallback 移除 / F 兩個必修都修（議題 G3 grep 紀律升級 + 議題 12 Agent task 層 mapping +1 entry）/ J1 既有 6 hooks 保留 / K1 mapping helper 保留 switch case。**核心實作**：① 新建 DevFixStageExecutor + Pipeline-DevFixCompletion PortId（5→6 RequestPort + 7→8 stage）② 4 stage Executor 加 routing：Reviewer fix loop / DevPlan appeal self-loop / Dev appeal + intervention / QA fix loop ③ **議題 F-1 16 處 skip 修正**（AppealOrchestration 11 + QaCoordination 5）+ HandleDevBlocker signature 升級給 Pipeline 自接管 routing ④ 5 fallback dispatch 全移除（Completed 語義變更含 intervention）⑤ 6 Mock 場景 dynamic + Round counter + PmRouting Mock 分支。**驗收能力突破**：`/internal/mock/scenario` HTTP API + auto-approve → **Forge 自驗全 6 場景含 SIGTERM/SIGKILL Crash Recovery**（Christ 線下實跑模式從「必要」轉「選擇性」）。**Aria 校準錨 ×0.88**（578K / Charter 中位 655K，混合型第 6 資料點 mid 帶中段；6 資料點區間 ×0.73-1.25 拆 Stage 守區間精神持續驗證）。詳見 Stage 53B Roadmap。commits：`cc07fcf`(主) + `49f4d5a`/`7fbac77`(fix) + `6d473db`(驗收紀錄)。

## [3.39.0] — 2026-05-03 — [Stage 53A](docs/planning/Stage_53A_Roadmap.md) ⭐ v4 漸進遷移第五步 macro pipeline NewFeature happy path

v4 路線最大遷移點之一：**macro-orchestration framework 化首次達成**（vs Stage 49-52「節點內部」單層 framework）。**Aria Session A 子項 5 實作期揭露議題 G3 假設失誤**（inner Meeting router post-meeting actions vs Pipeline 推進職責衝突）→ 即時跨 session 拍板 **方案 C 範圍縮小**：53A 範圍從「整個 pipeline」縮成「Pipeline 從 Dev_plan 啟動 + Kickoff/Design 留 legacy」（規模 -40% 守混合型 ×0.96-1.25 區間 + 戰略價值 ~70% 保留 + Stage 55 收尾統一整合）。**v4 路線 7→8 Stage**（53 拆 53A happy path + 53B 子流程）。核心實作：FrameworkPipelineRouter 4 method（HandlePipeline / ResumeAfterAgent J1 yield-resume / RecoverStuckPipeline 議題 12 升級 ResumeStreamingAsync rehydrate / FinalizePipeline 9 fallback dispatch）+ 9 stage Bridge record + 5 RequestPort + F-α 4 router 排除條件追加 + I2 fallback to legacy 反向設計（Stage 55 收尾移除）。**驗收期 4 follow-up**：① 戰略級 — 議題 G3 同類問題在 QA 重演（規劃紀律必升級為「對所有既有 service finalize/post-completion actions 都 grep」）② NotifyMergeStage 補 ③ 留 Stage 53B ④ Pipeline Recovery 接管 Bot restart 邊界 failed Agent task requeue（Stage 53B/54 必補設計）。新建 14 檔 ~1500 LoC + Migration `Stage53ATaskGroupPipelineFrameworkState`。**Aria 校準錨 ×0.73**（562K / Charter 中位 770K，混合型第 5 資料點 mid 帶下半 — 區間擴展為 ×0.73-1.25；方案 C 拆 Stage + Stage 51 know-how 複用 + 0 Aria gate1 揭露 + 全程一個 session 跑沒拆 Session 四因素疊加）。**Christ 拍板 production 保留 UseFrameworkPipeline=true**。詳見 Stage 53A Roadmap。commits：`296d44e` ~ `c424f67` 7 個。

## [3.38.0] — 2026-05-03 — [Stage 52](docs/planning/Stage_52_Roadmap.md) v4 漸進遷移第四步 Design Meeting B3 路線

議題 A 拆 Stage：原 v4 路線 Stage 52 含「Design + WorkflowEngine pipeline」一氣呵成，Aria 規劃時拆 Stage 守混合型 ×0.96-1.25 區間精神 — Stage 52 = Design Meeting B3 only / Stage 53 = WorkflowEngine macro / Stage 54 = Crash Recovery / Stage 55 = 收尾切 BossInteraction（v4 路線 6→7 Stage）。Design Meeting 三層 Stage 50 沒踩過拓撲擴展：① 條件式 Demi（needsDemi=false short-circuit）② needs_adjustment B2 子流程 ③ 拆 task 提案 router 後置（C2 抽 DesignSplitProposalEvaluator helper SoT）。Spike F1/F2 兩項全綠。**驗收期 2 follow-up**：① fix#1 Mock agentName 識別補（Stage 50 踩坑 #11 預警命中）② **fix#2 戰略級 framework 1.3.0 行為揭露**：「AddEdge type-based dispatch 不 source-aware」— 修法拆 plan executor（Stage 53+ 拓撲設計新預警）。**6 場景全綠**（含 SIGTERM+SIGKILL crash recovery 兩跑）。新建 17 檔 + DesignAdjustmentPlanExecutor + FrameworkDesignRouter 572 行 + Migration `Stage52TaskGroupDesignFrameworkState`。**Aria 校準錨 ×1.05**（609K / Charter 中位 580K，混合型第 4 資料點 mid 中段；混合型 ×0.96-1.25 四資料點區間穩定）。詳見 Stage 52 Roadmap。commits：`3b2343a` + `8b3ead1` + `b5dac50` + `806b22b`/`27ce0b7`(fix) + `d35ec80`/`754ff34`/`d951e6c`。

## [3.37.0] — 2026-05-02 — [Stage 51](docs/planning/Stage_51_Roadmap.md) ⭐ v4 漸進遷移第三步 framework HITL 試點

A3 試點精神 — 不切既有 BossInteraction 10+ type，新建 `framework_kickoff_mid_interrupt` type + `FrameworkHitlBridge` service 橋接層；既有 InteractionService / InteractionProcessor 主流程不動。B1 試點 = Christ 在 Kickoff 多輪會議跑期間 Dashboard 點「中途介入」按鈕 → workflow 跑到 RequestPort 點 yield → 開 BossInteraction → Christ 回應後新 HTTP scope rehydrate workflow（`InProcessExecution.ResumeStreamingAsync` 對齊 spike F3 結論）+ SendResponseAsync → workflow 從 yield 點繼續跑。Spike F1/F2/F3 三項全綠（RequestPort C# stable / ICheckpointStore 對 pending requests 序列化可用 / 跨 HTTP scope rehydrate）。Forge 主動範圍變更（Aria 認可）：trigger flag 改用 in-memory `KickoffMidInterruptTriggerStore`。**6 場景全綠 + 0 follow-up + Aria spike 三項關注點實證通過**（場景 D requestId `0daeccaa...` 跨 process restart stable 證據鏈）。新建 4 檔 ~600 LoC。**Aria 校準錨 ×0.96**（448K / Charter 中位 465K，混合型第 3 資料點 mid 帶下半最低；混合型 ×0.96-1.25 三資料點驗證）。詳見 Stage 51 Roadmap。commits：`67a9b0a` + `e65a4b3` + `3bb7f28`(v2.0)。

## [3.36.0] — 2026-05-02 — [Stage 50](docs/planning/Stage_50_Roadmap.md) v4 漸進遷移第二步

Kickoff Meeting 5 Agent 切 framework Workflow Builder fan-out/fan-in（A2 路線）+ feature flag。Spike E1 ❌ → A2 fallback 路線拍板（framework Group Chat 不支援 multi-speaker per round + Concurrent 不支援 loop back，唯一路徑 `WorkflowBuilder` + `AddFanOutEdge` + `AddFanInBarrierEdge` + `AddSwitch` + loop back）；E2/E3 ✅。**3 follow-up fix（戰略級 framework 1.3.0 fan-out 拓撲首次 production 整合踩坑）**：① RunAsync → RunStreamingAsync（fan-out 必須 streaming dispatch）② 顯式 SendMessageAsync/YieldOutputAsync Executor 加 `[SendsMessage]`/`[YieldsOutput]` + `partial class`（type validation MAFGENWF003）③ Mock Petra 角色識別補。Forge 自驗 6 場景 + 2 bonus 全綠 + 場景 C marker 100% cleared。新建 11 檔 ~1100 LoC + 改 11 檔（KickoffMeetingService 淨刪 213 行）。**Aria 校準錨 ×1.09**（500K / Charter 中位 460K，混合型第 2 資料點 mid 中心）。詳見 Stage 50 Roadmap。commits：`7d37a48` + `24b62dc` + `a50059c`/`cd6d61a`/`1023104`(fix) + `ff6a26f` + `b443546`(v2.0)。

## [3.35.0] — 2026-05-02 — [Stage 49](docs/planning/Stage_49_Roadmap.md) ⭐ v4 漸進遷移首發

Cody-Vera-Petra Appeal loop 切 framework Workflow Builder + Checkpointing + feature flag 並行雙系統。**0 follow-up + production fallback 防呆生效**（tech_improvement task Cody Dev_plan 缺結構 marker → IsDevPlanFailed=true → FrameworkAppealRouter 自動 fallback to legacy `HandleDevPlanCompletedAsync`，Forge Session B 主動加防呆）。「換引擎不換車身」首發：5 Agent prompt 完全不動 + DB 加 1 nullable 欄位 + Discord/Dashboard/ClaudeCodeService 包裝層保留 + 換 Appeal loop 編排層用 framework。**核心拍板**：① 並行雙系統 + feature flag 預設 false ② framework Checkpointing 為主（採 `ICheckpointStore<JsonElement>` 首選路徑）③ **路線 B service 包裝**（v1.1 修正 Aria Roadmap 3 Agent 不同層整合不一致 — 三 Executor 都包既有 service method）④ **DI factory 模式**（Forge 主動發現 — 不註冊 DI + IServiceScopeFactory 解 Singleton+Scoped 陷阱）⑤ FrameworkAppealRouter F3 scope 精簡（5 entry → 2 真實分流）⑥ Crash Recovery 雙系統隔離。新建 13 檔 ~2700 LoC + Migration。**Aria 校準錨 ×1.25**（606K vs Charter 中位 485K）。詳見 Stage 49 Roadmap。commits：`90c6ed3`(A) + `3400e5b`(B) + `5debc96` + `33bf51c`(v2.0)。

## [3.34.0] — 2026-05-02 — [Stage 47](docs/planning/Stage_47_Roadmap.md)

FF 四十七 Token limit SoT 統一（路線 b：DB AppSettings 動態化）+ 順帶完成 FF 十一 Dashboard 可調 Token 守門 — 解 Trial_v5 議題 C（appsettings 改了 docker env override 靜默無效）+ 議題 D（手動 docker restart 不 reload env，認知差非 CI/CD 缺陷，CI/CD `--force-recreate` 已就緒）；`AgentConfig` 加 `DailyTokenLimitK?` / `MonthlyTokenLimitK?` 欄位（呼應 Stage 38 Provider/Model 模式）+ Migration `Stage47AgentConfigTokenLimits`；`AppSettingsService.GetIntAsync(key, fallback=0)` + `>0` 防呆判斷確保 DB row="0" 也走 fallback；`AgentConfigCache` cache tuple 擴成 4-tuple（Provider/Model/DailyTokenLimitK/MonthlyTokenLimitK）；`TokenTrackingProvider` 4 個 Check 改 DB-first → appsettings fallback；Check 4 警報訊息指向 Dashboard【系統設定 → Token 守門設定】；`SystemSettings.razor` 新增「Token 守門設定」區塊 + 儲存後立即 `ReloadCacheAsync("all")`；`AgentSettings.razor` 加 per-agent Token Limit 欄位（nullable int，0=fallback）+ `UpdateTokenLimitsAsync`；移除 `docker-compose.prod.yml` 24 個 Token env（Bot 2 + Dashboard 22）；CLAUDE.md 加「ops 配置改動 SoP」段；DbSeeder **不動**（v2 修正：DB 不 seed Token 預設值，讓 fallback 安全網真實可達）；Aria 兩輪審查機制揭露 v1「DbSeeder 自動 seed 讓 fallback 永遠走不到」核心矛盾並完整修正；驗收 4 場景待 Christ 線下實跑（A/B/C/D），其中場景 D（CI/CD push 後容器啟動）+ 場景 C（DB 空值 fallback）為最關鍵安全網；**Forge Sonnet 200K + High 中段 compact 一次**（Aria 校準錨教訓：估 ~260K 卻推 Sonnet 200K 自我矛盾，下次 >180K 直接推 Opus 1M）

## [3.33.0] — 2026-04-29 — [Stage 46](docs/planning/Stage_46_Roadmap.md)

FF 三十五 自動拆任務 ⭐ 戰略級 + 搭車 FF 三十九 — Petra 在 Design 階段 propose 拆 N 個依賴 sub-task → Christ 採納 → Sequential 鏈執行（Phase 1 done → Phase 2 → Phase 3 → epic done） → 各自獨立 PR；解 Trial_v4「Cody 對大需求縮水」根因（12 Issue → 1 Issue）；TaskGroup 加 4 欄位（ParentGroupId / EpicPaused / PhaseNumber / PhaseDescription）+ partial index；BuildEpicSubTasksAsync v1.1 三層防護（idempotent + fresh read + scope 隔離）；Petra 雙層判斷（規則層 EvaluateAndProposeSplitAsync + Petra 層 RunPetraSplitTaskProposalAsync 復用 PetraSessionId）；2 個新 BossInteraction type（split_task_proposal / epic_partial_paused）；Internal API + DashboardBotService client（pause-epic / resume-epic 含找最大 done 啟動鏈）；CLAUDE_Petra.md 拆 task 判準新章節（80%+ 邊界覆蓋）；FF 三十九 EndsWith 寬鬆比對 + 清 InterventionReason；Mock 8-1 split_task_propose_accept 完整可驗（8-2 follow-up Trial_v5）；驗收期 3 個 fix commits（[MOCK] prefix / 12 Issue 路徑 / TryParseSplitProposal LastIndexOf bug）+ 揭露 **Stage 25b TryParseDesignIssues 既有 bug**（從未端到端跑過 — 風險點 #4 預測命中）；**Trial_v5 鎖死前置條件最後一塊完成 🎉**

## [3.32.0] — 2026-04-29 — [Stage 45](docs/planning/Stage_45_Roadmap.md)

FF 三十四 TaskGroup 流程暫停機制 + 搭車 FF 三十七 escalate skip status 殘留 — AiTeam 第三層暫停機制（與 Agent pause Stage 27b + 全域緊急停止 Stage 33 並列）：採方案 Ba（被動阻擋下階段）+ 議題 4/5 B（暫停與 BossInteraction / Appeal flow 兩機制獨立）；TaskGroup 加 4 欄位（IsPaused / PausedAt / PausedBy / PendingStepsJson SoT 解避免 Resume 重做 routing）；FireStepsAsync 統一閘門（22 caller 自動受保護，超越 Roadmap 預想）；Crash Recovery 對齊 IsPaused 篩選（落點 MeetingOrchestrationService.cs:432，**Aria 校準錨 #1：façade 不是 method body**）；FF 三十七 真實搭車範圍 1 處 ButtonCallbackRouter:241（**Aria 校準錨 #2：4 處 → 1 處**）；6 驗收場景全 PASS + 0 follow-up + 路線 C race condition 0 實際觀察；驗收期意外發現 FF 三十九（Dashboard 點「跳過審核」靜默變「放棄任務」）

## [3.31.0] — 2026-04-29 — [Stage 44](docs/planning/Stage_44_Roadmap.md)

FF 三十三 Token 計費機制 CLI Agent 涵蓋 — Trial_v4 揭露 token_logs 6% 涵蓋率盲點補完：`ClaudeCodeService.ParseJsonOutput` 解 `usage` + `total_cost_usd` → `ClaudeCodeResult.Usage`；token_logs 加 5 nullable 欄位（`Stage` / `Round` / `CacheCreationTokens` / `CacheReadTokens` / `TotalCostUsd HasPrecision(18,6)`）；`TokenLogService` 共用 helper 內建 try-catch + 獨立 scope（保證硬規則「不阻塞主流程」）；16 個 CLI caller（含搭車 Rosa/Demi）+ 21 處 MeetingCommons call site 全對齊；Stage 22 守門公式升級為等效 token（`input + output + cache_creation × 1.25 + cache_read × 0.1`，整數運算 EF translate）；Victoria 真實 CLI 對話實證 cache 占比 95.5% 等效 50,450 vs 純 input+output 2,265

## [3.30.0] — 2026-04-29 — [Stage 43](docs/planning/Stage_43_Roadmap.md)

FF 三十二 Orchestrator 改動類三子項（A+B+E）+ Sage F 搭車 — Self-implement 完整性閘門下半場：DevPlan 重產機制（上限 2，超限 escalate）+ Dev/Dev_fix failed 中止 fix loop + QA 失敗 needs_intervention（與 failed 語意分離）+ TaskGroup done 判定統一守門 + DocAgentService 認 Sage escalate JSON + PR URL hardcode 修。新增 `needs_intervention` Status / InterventionReason 欄位 / 4 個 BossInteraction type / 4 個 Mock 場景；驗收期搭車修 Stage 24 既有缺漏（Dev_fix 進 SemaphoreGroups + GetExecutorKey）— 揭露 QA fix loop 程式碼活 ≠ 從未端到端跑過

## [3.29.0] — 2026-04-28 — [Stage 42](docs/planning/Stage_42_Roadmap.md)

FF 三十二 prompt 補強類四子項（C+D+F+G）— Self-implement 完整性閘門上半場：Petra 範圍縮水升級規則 + Vera Server Circuit Critical 邊界（含 MudBlazor 事件鏈）+ Sage 無實作 escalate + Cody PR 自我檢查（80% 門檻，`⚠️ ESCALATE_NEEDED` marker 三檔字面一致）

## [3.28.0] — 2026-04-27 — [Stage 41](docs/planning/Stage_41_Roadmap.md)

`tests/Generated/` 編譯與執行修復（FF 三十一）+ CLAUDE_Quinn.md 兩條結構性 bug 防護 — 補完「Vera 審查 + Petra 閘門 + Quinn 測試」三層品質保證迴圈

## [3.27.0] — 2026-04-26 — [Stage 40](docs/planning/Stage_40_Roadmap.md)

`CLAUDE_Vera.md` + `CLAUDE_Petra.md` 判準補強（FF 二十九 + FF 二十五 Petra 子項）— Trial_v4 前置條件閉環

## [3.26.0] — 2026-04-25 — [Stage 39](docs/planning/Stage_39_Roadmap.md)

Vera 審查擴及 `.razor` / `.css`（FF 二十八）；新增 `AgentResultType.Skipped` 結果型別 + Dashboard 全鏈路 teal 配色

## [3.25.0] — 2026-04-25 — [Stage 38](docs/planning/Stage_38_Roadmap.md)

Dashboard Provider/Model 動態化（FF 四第二階段 2-A）：DB SoT + `AgentConfigCache` + `LlmModels.cs` 常數白名單

## [3.24.0] — 2026-04-25 — [Stage 37](docs/planning/Stage_37_Roadmap.md)

GeminiProvider API 層（FF 四第一階段）+ Crash Recovery 全面涵蓋（5 種 `ActiveOrchestration`）

## [3.23.0] — 2026-04-22 — [Stage 36](docs/planning/Stage_36_Roadmap.md)

TaskGroupService + CommandHandler 拆解（FF 二十 A+B 合併）：4795 行 → 1272 行（-73%）；**AiTeam 四怪物級檔案技術債清零** 🎉

## [3.22.0] — 2026-04-22 — [Stage 35](docs/planning/Stage_35_Roadmap.md)

PmAgentService 拆解（FF 二十-D）：1388 行 → 6 個子 service；首次實踐 SOP 6（子資料夾 `Agents/Pm/`）

## [3.21.0] — 2026-04-22 — [Stage 34](docs/planning/Stage_34_Roadmap.md)

MeetingService 拆解（FF 二十-C）：1415 行 → KickoffMeetingService + DesignMeetingService + Commons + Results

## [3.20.0] — 2026-04-22 — [Stage 33](docs/planning/Stage_33_Roadmap.md)

Agent 狀態卡 2.0：佇列控制 Dashboard 化（per-agent pause/resume + 全域 stop-all）+ 待辦清單 expand + 深層連結

## [3.19.0] — 2026-04-21 — [Stage 32](docs/planning/Stage_32_Roadmap.md)

`/mock` Dashboard 化 + Mock Delay / WorkflowSettings 動態化（從 AppSettings 讀，免重啟容器）

## [3.18.0] — 2026-04-20 — [Stage 31](docs/planning/Stage_31_Roadmap.md)

可靠性補強：Dashboard 重試按鈕 + 會議 Crash Recovery + Appeal 對抗紀錄 UI（FF 十七 + 十八）

## [3.17.0] — 2026-04-20 — [Stage 30](docs/planning/Stage_30_Roadmap.md)

申訴迴圈 LLM API → Claude Code CLI 全面升級（5 個環節新開 session + 唯讀工具）

## [3.16.1] — 2026-04-19 — Hotfix

MockMode 提案核准重複建 TaskGroup bug 修正（Dashboard 路徑補 GroupId 防護對齊 Discord 路徑）

## [3.16.0] — 2026-04-19 — [Stage 29](docs/planning/Stage_29_Roadmap.md)

Dashboard 操作性收尾 + CEO 指令通道擴充（Dashboard 直接下指令給 Victoria，含圖片附件）

## [3.15.0] — 2026-04-17 — [Stage 28b](docs/planning/Stage_28b_Roadmap.md)

Dashboard 雙向操作中心 — 文字輸入互動 + 歷史紀錄篩選

## [3.14.0] — 2026-04-17 — [Stage 28a](docs/planning/Stage_28a_Roadmap.md)

Dashboard 雙向操作中心 — 基礎架構 + 8 個確認點按鈕回覆 + 樂觀鎖先到先贏

## [3.13.0] — 2026-04-16 — [Stage 27b](docs/planning/Stage_27b_Roadmap.md)

Agent 任務序列 — 操作性與可觀察性（5 個 Discord 指令 + Dashboard 佇列視覺化 + SignalR）

## [3.12.0] — 2026-04-16 — [Stage 27a](docs/planning/Stage_27a_Roadmap.md)

Agent 任務序列 — 核心佇列機制（DB-as-Queue + AgentQueueService + per-agent SemaphoreSlim + Crash Recovery）

## [3.11.0] — 2026-04-14 — [Stage 26](docs/planning/Stage_26_Roadmap.md)

驗收基礎設施（PipelineView 折疊面板 + MockMode 修正）+ 版本號集中管理（`Directory.Build.props`）

## [3.10.0] — 2026-04-14 — [Stage 25b](docs/planning/Stage_25b_Roadmap.md)

開發流程重構 Phase 1d — 設計規劃階段（5 人設計會議 + 條件式 Christ 確認）

## [3.9.0] — 2026-04-14 — [Stage 25a](docs/planning/Stage_25a_Roadmap.md)

開發流程重構 Phase 1c — Kick-off 會議機制（Claude Code 持續對話 session + 多 Agent 會議）

## [3.8.0] — 2026-04-13 — [Stage 24](docs/planning/Stage_24_Roadmap.md)

開發流程重構 Phase 1b — QA Petra 介入 + Dev_plan 審核強化 + TestReport 結構化存 DB

## [3.7.0] — 2026-04-12 — [Stage 23](docs/planning/Stage_23_Roadmap.md)

開發流程重構 Phase 1a — Review Appeal 迴圈 + Sage 轉型歸檔員 + Git Tag 自動化

## [3.6.0] — 2026-04-12 — [Stage 22](docs/planning/Stage_22_Roadmap.md)

Dashboard 存取分層（localhost bypass）+ Token 守門 4 層攔截 + `#指令中心` 頻道清理

## [3.5.0] — 2026-04-11 — [Stage 21](docs/planning/Stage_21_Roadmap.md)

`docs/` 資料夾重整（architecture / planning 子資料夾）+ SemVer 導入

## [3.4.0] — 2026-04-11 — [Stage 20](docs/planning/Stage_20_Roadmap.md)

Dashboard 全面換 MudBlazor Layout（MainLayout → MudLayout + Dark Mode → MudThemeProvider）

## [3.3.0] — 2026-04-10 / 04-11 — [Stage 19](docs/planning/Stage_19_Roadmap.md)

Dashboard UI 全面打磨（三批 18 項：StatusBadge / MudChip / MudIcon / MudStack / 側邊欄 localStorage 等）

## [3.2.0] — 2026-04-09 — [Stage 18](docs/planning/Stage_18_Roadmap.md)

Dashboard 可觀測性升級：Agent 狀態卡即時更新 + Pipeline View（MudStepper + MudTimeline）

## [3.1.0] — 2026-04-08 — [Stage 17](docs/planning/Stage_17_Roadmap.md)

Mock Mode：`IClaudeCodeService` 代理模式 + Dashboard 開關 + 4 種 `/mock` 流程

## [3.0.0] — 2026-04-07 — [Stage 16](docs/planning/Stage_16_Roadmap.md)

**MAJOR**：PM Agent（Petra）品質審核閘門；Vera / QA 重構為單一 Claude Code session

## [2.4.0] — 2026-04-06 — [Stage 15](docs/planning/Stage_15_Roadmap.md)

Victoria 接上 Claude Code + Session 對話持久化 + 長期記憶

## [2.3.0] — 2026-04-06 — [Stage 14](docs/planning/Stage_14_Roadmap.md)

CEO 分類補強：技術改善分類 + Release / Ops / Doc 直接路由 + 任務取消能力

## [2.2.0] — 2026-04-06 — [Stage 13](docs/planning/Stage_13_Roadmap.md)

系統穩定性與流程修正：Dev → Reviewer → QA → Doc 串行 + 單一 PR + Closes #XX 自動關 Issues

## [2.1.0] — 2026-04-06 — [Stage 12](docs/planning/Stage_12_Roadmap.md)

提案流程全面升級：Rosa / Demi 串行協作 + 唯讀探索 + UI 規格存 DB + Discord 附件

## [2.0.0] — 2026-04-05 — [Stage 11](docs/planning/Stage_11_Roadmap.md)

**MAJOR**：Dev Agent（Cody）驅動 Claude Code CLI 自主開發

## [1.4.0] — 2026-04-03 — [Stage 10](docs/planning/Stage_10_Roadmap.md)

開發流程自動閉環：CEO Orchestrator + WorkflowEngine + Review 閉環 + Ops Rollback

## [1.3.1] — 2026-04-04 — Hotfix

Stage 10 驗收後 7 項修正（Race Condition / IssueUrls 重複 / PushStatus / dead code 清理 / EF Index）

## [1.3.0] — 2026-04-03 — [Stage 9](docs/planning/Stage_9_Roadmap.md)

CEO 升級 + 可觀測性：Token 監控 Dashboard + CEO 智慧分類 + 提案模式 + QA Playwright

## [1.2.0] — 2026-04-02 — [Stage 8](docs/planning/Stage_8_Roadmap.md)

系統可靠性與操作體驗：動態 AppSettings + per-agent Rules + Dark Mode + Notion 移除

## [1.1.0] — 2026-04-02 — [Stage 7](docs/planning/Stage_7_Roadmap.md)

Software Team 完全體：Reviewer / Release / Designer Agent + CI/CD + Discord 重設計 + 自然語言對話

## [1.0.0] — 2026-04-01 — [Stage 6](docs/_archive/early-stages/Stage_6_Roadmap.md)

**MAJOR**：強化、驗收與技術債清償（Discord Vision、MudBlazor、Requirements 三層確認、E2E 驗收等 12 項）

## [0.4.0] — 2026-04-01 — [Stage 5](docs/_archive/early-stages/Stage_5_Expansion.md)

擴充 Agent：QA / Doc / Requirements + 動態 Agent 框架

## [0.3.0] — 2026-03-31 — [Stage 4](docs/_archive/early-stages/Stage_4_Dashboard.md)

Blazor Web App Dashboard（Identity + SignalR + Aspire 基礎）

## [0.2.0] — 2026-03-31 — [Stage 3](docs/_archive/early-stages/Stage_3_Agents.md)

第一批 Agent 上線：CEO / Dev / Ops（Anthropic Claude API）

## [0.1.0] — 2026-03-31 — [Stage 1](docs/_archive/early-stages/Stage_1_Design.md) + [Stage 2](docs/_archive/early-stages/Stage_2_Foundation.md)

基礎建設：系統設計確定 + Discord Bot + Aspire AppHost + PostgreSQL
