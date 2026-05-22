# Future Feature v5.5 — 升級規劃 reference

> 建立日期：2026-05-15
> 狀態：**v5.5 完整收口進 production 自然累積期**（Stage 83 結案 / 2026-05-21 / v3.75.0）
> 對應主檔：[Future_Feature.md](Future_Feature.md)
> 詳細實作紀錄：[CHANGELOG.md](../../CHANGELOG.md) + 各 Stage Roadmap

## 一、為什麼有 v5.5 這個概念

**v5 動態架構（Stage 63B PoC + Stage 64-66 production-ready）已在 2026-05-14 上線**，但跟 Stage 62 Charter 規劃比，PoC 階段為了 derisk + 縮範圍簡化掉幾條關鍵設計：

- **PetraSessionRepository.ResumeAsync** 是「mark 既有 session done + 開新 session」（不是真實 resume）
- **BuildSessionContext** fallback hardcode `Agents:Dev:Model` 給 7 worker 共用（沒 per-worker config）
- **Petra 一次決策整 chain** 跑完不重新思考（不是真正動態 orchestrator）
- **Worker 是 one-shot subprocess** 沒持續記憶
- **沒拆解任務機制**（Petra 直接 dispatch worker / 沒先把指令拆成多個 subtask）

**v5.5 = 把 PoC 簡化掉的部分補回來 + 對齊業界 2026 best practice + 對齊 Stage 62 Charter 既有規劃**。

---

## 二、Christ 描述的設計核心（2026-05-14/15 討論累積）

**精神**：Agent 像人類處理事件 — 接到任務後累積思考 / 中間別事打斷不會忘記 / 重啟接著做還記得來龍去脈。

**設計五要素**：

1. **Petra 拆解指令** — 把一個指令拆成多個 subtask / 不是直接整包丟 worker
2. **每個 subtask 呼叫 Agent 執行** — Petra 看 subtask 性質決定哪個 worker
3. **Agent 對任務有持續記憶** — DB 持久記憶（不靠 subprocess context）
4. **記憶分層**（業界共識 hybrid 模式）：
   - **共用層**（task-level）：任務目標 / 各 agent deliverable 摘要 / 關鍵決策歷史 — 跨 agent 可讀
   - **私有層**（per-agent role）：Cody implementation 細節 / Vera review checklist — 只該 agent 自己讀寫
5. **Talent-Skill separation + horizontal scaling**：
   - **Skill（職務）= code-defined** — code 寫死的 capability + 對應 tool wiring
   - **Talent（Agent）= DB-driven WebUI 動態加** — 可動態 CRUD 的 instance
   - **多對多 assignment**：1 Talent 可擔任 N Skill / 1 Skill 可由 N Talent 擔任
   - **Horizontal scaling**：「Coding 任務太多 → 加 Cody-2 也擔任 Coding skill」= Christ 擴 Talent pool / 不用寫 code
   - **新職務還是要走 Stage**：例如「Discord 職務」是 code-defined / WebUI 不能加新職務（避「Agent role explosion」反模式）

**Subprocess 角色釐清**：還需要 subprocess（執行容器：grep / read / edit / commit）/ 不需要 resume（每次新開 + 從 DB load 記憶 input）/ 對齊「stateless web server + database」pattern。

---

## 三、WebSearch 業界 finding（2026-05-15 探索）

### 3.1 持久記憶設計

**業界推 4 條 best practice**：
1. **持久記憶存進 DB / vector store**（不存 context window）— Agent query 取相關記憶
2. **Compression 主動做** — 60-70% context 就 compact / 不等自動觸發
3. **Shared context layer**（central state store）— Agent transient / shared layer persistent
4. **Stateless orchestrator + Stateful memory**

**雷區**：Context drift 65% enterprise failures / Context window 是 RAM 不是 storage / Claude Code resume bug ([#14472](https://github.com/anthropics/claude-code/issues/14472) / [#43696](https://github.com/anthropics/claude-code/issues/43696)) / Agent 35 分鐘退化定律 / Memory conflict trap。

### 3.2 Talent-Skill separation + horizontal scaling

**Christ 直覺命中業界主流方向** — OneManCompany (OMC) framework / AI Agent Staffing / Spring AI Skills / Agent Identity URI（agent:// scheme）。

**Centralized orchestrator 壓制錯誤放大實證**：Independent multi-agent systems amplified errors by 17.2x / Centralized systems with an orchestrator contained amplification to just 4.4x — 對齊 AiTeam Petra orchestrator 設計 ✓

### 3.3 多 Agent 討論（debate / meeting pattern）

**業界 2026 共識 pattern**：**「orchestrator + isolated subagents with summary returns」**（Anthropic / Cognition / OpenAI / AutoGen / LangChain 全部收斂）。

**雷區**：MIT 數學論文「without genuinely new information entering the system, adding agents is guaranteed to perform no better than a single well-designed agent」/ 集體幻覺 / Cost 暴漲 15x / 「Bag of Agents」反模式 + 14 種失敗模式。

**業界 15x cost 對齊 AiTeam 數據**：Trial_v6 (v4 完整 Meeting) $15.81 vs Trial_v12 (v5 線性 chain) $2.34 = **6.8x**（接近業界 15x）。

---

## 四、v5.5 設計建議（兩層架構）

| 層級 | 設計 | 場景 | 對應業界 pattern |
|---|---|---|---|
| **Day-to-day 任務** | Petra orchestrator + 拆解 + dispatch isolated subagent + DB 持久記憶 | 90% 日常任務 | orchestrator + isolated subagents |
| **戰略決策** | 3 agent multi-agent debate（2 opposing + 1 synthesizer）| 大型新 feature 設計 / 架構決策 — 給 Christ 拍板用 | 多 agent debate（避 MIT 雷靠 Christ 拍板提供新資訊）|

---

## 五、跟既有 v5 / FF 的關係

| 既有 | v5.5 升級內容 |
|---|---|
| **v5 PoC**（Stage 63B + 64-66）| 保留 — 是 v5.5 的基礎 |
| **FF 三十六 Phase B 全程動態 candidate** | v5.5 是這條的具體化 |
| **FF 六十一補強清單** | 部分被 v5.5 吸收 |
| **Stage 62 Charter** | v5.5 對齊 Charter 規劃補回 PoC 簡化部分 |
| **prompt DB 化候選** | 跟 v5.5 持久記憶整合 / 順帶吸收 |
| **v4 既有 Kickoff/Design Meeting** | 「戰略決策」層級可參考 / 不照搬重新設計避雷 |
| **v4 既有 module** | Stage 78a/b/c 已全砍（2026-05-19）|

---

## 六、Phase 1-4 完整收口（2026-05-15 → 2026-05-21）

### Phase 1：基礎重構（Talent-Skill separation）✅

| Stage | 版本 | 主題 |
|---|---|---|
| [Stage 67](Stage_67_Roadmap.md) | v3.57.0 | Talent-Skill separation 重構基底 / Migration `Stage67TalentSkillSeparation` |
| [Stage 68](Stage_68_Roadmap.md) | v3.58.0 | production-ready 補強（AppendMessage async + ef-core.md nullable unique pattern）|

### Phase 2：核心動態化 ✅

| Stage | 版本 | 主題 |
|---|---|---|
| [Stage 69](Stage_69_Roadmap.md) | v3.59.0 | 跨 session 長期持久記憶（TaskMemory + TalentMemory schema）|
| [Stage 70](Stage_70_Roadmap.md) | v3.60.0 | Petra 拆解指令精準度（hierarchical decomposition + dependency graph）|
| [Stage 71](Stage_71_Roadmap.md) | v3.61.0 | Trial_v15+v16 揭 2 議題收口（Petra prompt 線性整包紀律 + memory 空 content guard）|
| [Stage 72](Stage_72_Roadmap.md) | v3.62.0 | Prompt DB 化（SkillPrompts + TalentPrompts 兩層 schema + versioning）|

### Phase 3：進階機制 + 動態化開放 ✅

| Stage | 版本 | 主題 |
|---|---|---|
| [Stage 73](Stage_73_Roadmap.md) | v3.63.0 | Prompt content 升級 + Petra TalentPrompt persona seed |
| [Stage 74](Stage_74_Roadmap.md) | v3.64.0 | per-Skill Model + 真並行 dispatch DAG fan-out + Skill registry metadata |
| [Stage 75](Stage_75_Roadmap.md) | v3.65.0 | 兩層 queue 配套（PetraInbox + per-Talent SemaphoreSlim）|
| [Stage 76](Stage_76_Roadmap.md) | v3.66.0 | task retry / resume 機制（4 欄 + PetraErrorClassifier + Dead Letter）|
| [Stage 77](Stage_77_Roadmap.md) | v3.67.0 | fire-and-forget A2 業界推薦完整版（Channel + multi-consumer + graceful shutdown）|

### Phase 4：v4 path 砍 + 動態 orchestrator 完整 + WebUI 重設計 ✅

| Stage | 版本 | 主題 |
|---|---|---|
| [Stage 78a](Stage_78_Roadmap.md) | v3.68.0 | v4 path dead code 砍除（Rosa/Demi/Release 3 純 v4 class + 4 雙路徑 class v4 method）|
| [Stage 78b](Stage_78b_Roadmap.md) | v3.69.0 | v4 path dead caller 砍除（ButtonCallbackRouter v4 routing + CeoAgentService.ProcessAsync）|
| [Stage 78c](Stage_78c_Roadmap.md) | v3.70.0 | **MAJOR** v4 Pipeline framework 整套砍除（108 檔變動 net -22690 行）|
| [Stage 79](Stage_79_Roadmap.md) | v3.71.0 | v5.5 image flow 補完（PetraInbox.Attachments + GeminiProvider multimodal + 條件性 worker propagation）|
| [Stage 80](Stage_80_Roadmap.md) | v3.72.0 | A HITL plan confirmation 閘門（業界 LangGraph interrupt + 4 decision pattern）|
| [Stage 81](Stage_81_Roadmap.md) | v3.73.0 | B 動態 re-planning + HITL retry gate（LangGraph cycles + max iterations + cost cap）|
| [Stage 82](Stage_82_Roadmap.md) | v3.74.0 | Quinn outputLen 修根因（stream-json accumulate）+ PM PetraSessionId AsyncLocal 透傳 |
| [Stage 83](Stage_83_Roadmap.md) | v3.75.0 | **WebUI 全砍重設計**（3 大分區 + Home + Auth / Christ「最後測驗」精神達成）|

### Trial 真實業務驗（Phase 4 期間）

| Trial | 結果 | 對應 Stage |
|---|---|---|
| [Trial_v23](../experiments/Trial_v23_Plan.md) | 🟡 部分過 | Stage 78a+78b+78c+79 連 4 Stage |
| [Trial_v24](../experiments/Trial_v24_Plan.md) | 🟢 全綠 | Stage 80 HITL plan_confirm |
| [Trial_v25](../experiments/Trial_v25_Plan.md) | 🟡 部分過 | Stage 81 動態 replan / 揭 Quinn outputLen 修根因錯方向 |
| [Trial_v26](../experiments/Trial_v26_Plan.md) | 🟢 | Stage 82 雙修法 production 真實生效驗證 + 業界 supervisor pattern 對齊驗證 |
| [Trial_v27](../experiments/Trial_v27_Plan.md) | 🟡 戰略性結案 | 跳出 Trial→Fix 迴圈業界共識對齊（LLM alignment 雙重 safety net 實證）|

---

## 七、Stage 84+ 候選（v5.5 production 自然累積期 / 真實業務痛點觸發評估）

從 Stage 83 phased delivery 留的 12 條 + FF C2：

| # | 候選 | 規模 | 來源 |
|---|---|---|---|
| 1 | ExtractPrNumber DRY refactor（TaskHub reuse 既有 PrNumberHelper） | XS | Stage 83 |
| 2 | Chrome MCP click stale spike（Aria 視覺驗收環境議題 / 不影響人類 click） | S | Stage 83 |
| 3 | WorkflowFlags UseHITLPlanConfirmation + UseDynamicReplanning toggle on production 拍板 | XS | Stage 83 |
| 4 | Theme 即時切換 C# event 改造（MainLayout setDark + MudProviders subscribe）| S | Stage 83 |
| 5 | Bug 4 既有 row PR URL retroactive backfill（從 PetraSessionMessages parse） | S | Stage 83 |
| 6 | Monitoring 警戒線 MudChart ReferenceLine + per-Skill 維度 | M | Stage 83 |
| 7 | Bot /internal/health Discord 連線真實 check（非 placeholder） | XS | Stage 83 |
| 8 | MockMode 4 流程觸發 UI button | S | Stage 83 |
| 9 | env naming convention 紀律升級 → `CLAUDE.md` ops SoP（已部分完成 / 待 review）| XS | Stage 83 |
| 10 | MudThemeProvider IsDarkMode binding 紀律 → `docs/conventions/mudblazor.md` | XS | Stage 83 |
| 11 | FF C2 AppSettingsService 5 分鐘 re-read（21 Workflow:* flag 動態 reload 不需重啟 Bot）| M | Stage 83 子項 3 議題 C 衍生 |
| 12 | DashboardService 進一步重組（Home / TaskHub PetraSession query 直接走 Repository）| S | Stage 83 |

**追加候選**（Trial_v26 戰略 finding）：
- 🟡 Vera review 紀律升級（system prompt / few-shot 更多「該標 critical 的反例」）
- 🟡 Read-only Codebase Explorer agent（獨立 worker / 給 Petra 第二來源訊號）
- 🟡 SubtaskPlanParser 對 Petra refuse JSON fallback 升級（detect `{"error":"dispatch_rejected"}` pattern → 直接 escalate）

**評估時機**：Christ 真實使用累積痛點觸發後動工 / Aria 不主動推（對齊 v5.5 完整收口 + production 自然累積期路線）。

---

## 八、跨 Phase 紀律

- **每 Phase 結束跑 Trial 驗證**（真實任務驗 + Aria 9-step 模板）
- **每個 Stage 加完做 Aria gate1 + Forge 自驗 + 真實任務 Trial**
- **Phase 1+2 完成前不開放 WebUI Talent CRUD**（避「Agent role explosion」反模式）— Stage 83 後 WebUI Settings 分區 TALENTS / SKILLPROMPTS / TALENTPROMPTS full CRUD 已 deliver

---

## 九、v5.5 6 Talent + 4 Final Skill Baseline

完整描述見 [`docs/agents/v5.5_team_plan.md`](../agents/v5.5_team_plan.md)（v5.5 SoT）。

**4 Final Skill**（Stage 78c 後 / Stage 83 production SQL DELETE 2 row 清乾淨）：
1. `code_implementation` — Cody（寫 code）
2. `code_review` — Vera（找 bug / 對齊 coding style / production safety check）
3. `qa_testing` — Quinn（測試 + 寫 test case + Playwright）
4. `documentation` — Sage（寫文件 + README + commit message + PR body）

**6 預設 Talent**：Victoria（CEO flag forward）/ Petra（PM orchestrator）/ Cody（Dev）/ Vera（Code Reviewer）/ Quinn（QA）/ Sage（Doc）。

**❌ 砍 4 個既有 Agent + 2 個 skill**（Stage 78a/c）：
- Rosa（Requirements）/ Demi（UI Design）/ Rena（Release）/ Maya（Ops）整套砍
- `requirements_extraction` / `ui_design` / `release_publishing` 3 個 skill 砍（後兩個原 Cody 兼任 / Stage 83 production SQL DELETE 清）

**未來 Skill 候選**（不急 / production 痛點觸發評估）：`commit_messages` / `planning_review` / `security_audit` / `design_consultation`

---

## 十、不確定性 + 待驗證

1. **戰略決策層的 3 agent 配置** — 哪 2 個 opposing + 哪 1 個 synthesizer 對 AiTeam 場景最有效（Trial_v26 揭 Vera review 紀律升級 + Read-only Codebase Explorer 兩條 path 候選）
2. **Petra 拆解指令的精準度極限** — Stage 81 動態 replan 已實裝 / Trial_v25-v27 驗證業界 supervisor pattern 對齊 / 持續觀察 production fire 結果
3. **Provider 切換的 cost-quality trade-off** — Stage 82 Sonnet 4.6 production active default 對齊 reliability > cost 拍板 / Gemini Flash 留 fallback

---

## 十一、Sources（業界 finding reference）

### 持久記憶 / Context drift
- [State of AI Agent Memory 2026 — mem0](https://mem0.ai/blog/state-of-ai-agent-memory-2026)
- [Context Drift Causes 65% of Enterprise AI Agent Failures — MemU](https://memu.pro/blog/ai-context-drift-enterprise-agent-memory)

### Claude Code resume bug
- [Cannot resume session when context exceeds limit — claude-code #14472](https://github.com/anthropics/claude-code/issues/14472)
- [--continue and --resume do not restore prior conversation context — claude-code #43696](https://github.com/anthropics/claude-code/issues/43696)

### 多 Agent 討論
- [Why Your Multi-Agent System is Failing: 17x Error Trap of "Bag of Agents" — Towards Data Science](https://towardsdatascience.com/why-your-multi-agent-system-is-failing-escaping-the-17x-error-trap-of-the-bag-of-agents/)
- [Why Do Multi-Agent LLM Systems Fail? — arXiv 2503.13657](https://arxiv.org/html/2503.13657v1)
- [Multi-Agent in Production in 2026: What Actually Survived — Medium](https://medium.com/@Micheal-Lanham/multi-agent-in-production-in-2026-what-actually-survived-f86de8bb1cd1)
- [Agent Architecture Patterns: 2026 Taxonomy Guide — DigitalApplied](https://www.digitalapplied.com/blog/agent-architecture-patterns-taxonomy-2026)

### 業界 supervisor pattern 驗證（Trial_v26）
- LangGraph supervisor / Databricks supervisor agent / Claude Agent SDK 共識 — Petra 純 LLM API call + Worker Claude Code CLI subprocess + HITL 兜底三層分工對齊業界主流

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-15 | 文件建立 — Christ + Aria 2026-05-14/15 跨 2 日討論累積 → v5.5 升級規劃 reference 文件化 |
| **v2.0** | **2026-05-22** | **v5.5 完整收口 + 大砍** — Phase 1-4 全 deliver（Stage 67-83 / v3.57.0-v3.75.0）/ Trial_v23-v27 真實業務驗證 / Stage 78a/b/c v4 path 完整砍除 / Stage 80+81 HITL 雙保險 / Stage 82 Quinn outputLen 修根因 / Stage 83 WebUI 全砍重設計達成 Christ「最後測驗」精神 / **本檔 936 → ~250 行（-73%）** 砍除 Phase 1-4 已完成段詳述（細節改 reference Roadmap + CHANGELOG）/ 對齊「對冗餘不容忍 / 三重 propagate」整理原則 / Stage 84+ 候選 12 條保留為 active 段（production 自然累積期評估動工）|
