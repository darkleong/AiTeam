# Stage 74 Roadmap — v5.5 Phase 3 Step 8：per-Skill Model + 真並行 dispatch + Skill registry metadata 擴展

> 目標版本：**v3.64.0**（minor — v5.5 Phase 3 第二步 / 一般架構級重構：Talent-Skill-Model 三層架構打磨完整 + DAG fan-out 並行 dispatch + 對齊業界 2026 Agent Skills open standard format）
> 狀態：📝 規劃中
> 文件版本：v1.0
> 範圍：TalentSkill schema 擴展 + Model resolution 三層 fallback chain + ClaudeCodeChatClientAdapter Model 動態選擇 + DAG fan-out 並行 dispatch + ISkillRegistry metadata 擴展 + xUnit + Directory.Build.props bump
> 規模：M+
> 對應 v5.5 規劃：[Future_Feature_v5.5.md](Future_Feature_v5.5.md) Phase 3 Step 8

---

## 戰略脈絡

**Trial_v19 🟢 全綠 + Stage 73 結案後 Phase 3 Step 8 開跑** — 對齊 Christ 2026-05-17 連續兩個戰略 question 點破真實架構缺口 + WebSearch 業界 finding 完全支持。

### Christ 戰略 question 點破歷史（規劃前置）

Stage 74 範圍經三輪戰略 question 拍板成熟：

**第一輪**（Aria 初版）：3 議題討論 — 真並行 dispatch / 3 agent debate 觸發 / 3 agent 是誰（推 Cora Talent）

**第二輪**（Christ 戰略 question 1：仲裁是 Agent 還是權責？）：
- Aria 真實再評估認錯：**撤回新立 Cora 建議** — Petra 本身就是 PM 對最終結果負責 / 「仲裁」是 PM 內建職責不是獨立 Agent
- 但暴露 2 opposing 配置不清楚：既有 6 Talent 沒天然對立視角

**第三輪**（Christ 戰略 question 2：權責能不能設 Model？）：
- Aria grep 實證：**目前只支援 per-Talent Model / 不支援 per-Skill Model**（TalentSkill schema 缺 Provider/Model 欄位）
- 點破真實架構缺口 — debate 機制依賴 per-Skill Model（不只仲裁 / 還有 horizontal scaling / cost optimization 多場景）
- **修根因紀律**：先補完 per-Skill Model 三層架構 / debate 機制延後到 Phase 4 或真實業務需求觸發評估

### 計劃前 WebSearch 結論（2026-05-17 已累積 / 不重複觸發）

業界 2026 finding 完全支持路線 A：

1. **「Model mesh approach」+「Capability-aware routing」** — Claude Sonnet 4.6 跑 reasoning / GPT-4o 跑 real-time / Haiku 跑 triage — 全 single orchestrated architecture（[Enterprise AI Strategy 2026 — CloudHew](https://cloudhew.com/blogs/stop-askingwhich-llm-is-beststart-askingwhich-architecture/)）
2. **Model routing 比 uniform GPT-4o 便宜 60%** — enterprise token 支出 YoY -67%（[Model Routing LLM — abhyashsuchi](https://abhyashsuchi.in/model-routing-llm-2026-best-practices/) + [LLM Cost Optimization in 2026 — Mavik Labs](https://www.maviklabs.com/blog/llm-cost-optimization-2026)）
3. **Agent Skills open standard** — Claude / OpenAI / Google 已 converge：skill 定義含「能做什麼 + parameters JSON Schema + return type + metadata」（[Agent Skills Open Standard — MindStudio](https://www.mindstudio.ai/blog/agent-skills-open-standard-claude-openai-google)）
4. **真並行 dispatch 1.4-2.4× speedup**（[Optimizing Sequential Multi-Step Tasks with Parallel LLM Agents — arXiv](https://arxiv.org/html/2507.08944v1)）
5. **State management 是 production primary 挑戰** — AiTeam 已對齊 ✓（PostgreSQL backed）

### debate 機制撤回紀錄（戰略大重評估後 / 不在 Stage 74 範圍）

- ~~3 agent debate（2 opposing + 1 synthesizer）~~ — 留 Phase 4 候選 / 等真實業務需求觸發評估
- ~~新立 Cora Talent~~ — 撤回 / 「仲裁」是 Petra 內建職責不是獨立 Agent
- ~~debate 觸發條件設計~~ — 留 Phase 4

### 範圍邊界刻意收緊

- ✅ 做：
  - TalentSkill schema 加 Provider + Model nullable 欄位 + Migration
  - Model resolution 三層 fallback chain（per-Skill > per-Talent > Agents:Dev:Model）
  - ClaudeCodeChatClientAdapter Model 動態選擇整合（dispatch 時透過 resolver 取對應 Model）
  - 真並行 dispatch（DAG fan-out / 同 dependency level Task.WhenAll / 線性 chain 仍 sequential）
  - ISkillRegistry SkillDescriptor metadata 擴展對齊 Agent Skills open standard format（簡化版 — description + recommended model tier + return type 描述）
  - xUnit 6-8 case
  - Directory.Build.props v3.63.0 → v3.64.0

- ❌ 不做：
  - **3 agent debate 機制**（撤回 / 留 Phase 4 候選）
  - **新立 Cora 或任何新 Talent**（既有 6 Talent baseline 維持）
  - **prompt caching 接入**（標 FF 候選 / spike Claude Code CLI 路徑能否吃到 caching 後評估）
  - **Anthropic.SDK path 接入 caching**（已撤回 — 對應 v4 既有 4 agent service class 已 0 fire / 0 ROI）
  - **Parameters JSON Schema 完整 metadata**（簡化版即可 / 對齊「自己用爽」+ 避過早 over-engineer / 留真實業務需求觸發擴展）
  - **WebUI Talent-Skill Model 編輯介面**（Phase 3 Step 6 Stage 75 範圍）
  - **兩層 queue 配套**（Stage 76 範圍）
  - **v4 既有 path 4 agent dead code 清理**（Phase 4 候選 / 對齊 CLAUDE.md 4f862ca commit 已紀錄）

---

## 子項清單

### 1. TalentSkill schema 擴展 + Migration

**新增欄位**（對齊 Stage 67 既有 Talent.Provider + Talent.Model nullable pattern）：
- `Provider` (string?, nullable) — per-Skill Provider override（null = fallback per-Talent Talent.Provider）
- `Model` (string?, nullable) — per-Skill Model override（null = fallback per-Talent Talent.Model）

**Migration** `Stage74TalentSkillModel`（純 ADD COLUMN nullable / 0 既有 row 影響 / 對齊 ef-core.md Migration 紀律）。

**對齊既有 pattern**：
- Stage 67 Talent schema（ProjectId + Provider + Model nullable）
- Stage 67 TalentSkill 既有（TalentId / SkillName / IsPrimary / Priority / AssignedAt）
- Stage 72 PromptResolver cache pattern（Singleton + 5-min TTL）

### 2. Model resolution 三層 fallback chain

**新立** [`TalentSkillModelResolver`](src/AiTeam.Bot/Services/TalentSkillModelResolver.cs)（對齊既有 PromptResolver Singleton + IServiceScopeFactory pattern）：

**resolution 方法**：
- `Task<(string Provider, string Model)> ResolveAsync(Guid talentId, string skillName, CancellationToken ct)`

**三層 fallback chain**：
1. **per-Skill**（TalentSkill (TalentId, SkillName).Model / Provider）— 最優先
2. **per-Talent**（Talent.Model / Provider）— 次優先
3. **Agents:Dev:Model**（既有 runtime fallback / appsettings.json）— 末層

**Cache 紀律**（對齊 Stage 72 PromptResolver pattern）：
- Singleton service / IServiceScopeFactory 取 db
- 5-min TTL cache（避每次 dispatch hit DB）
- `InvalidateCache()` method 對齊 reload-cache `scope=all` 觸發

DI 註冊（[`Program.cs`](src/AiTeam.Bot/Program.cs)）：`AddSingleton<TalentSkillModelResolver>`。

### 3. ClaudeCodeChatClientAdapter Model 動態選擇整合

**修改** [`ClaudeCodeChatClientAdapter.cs`](src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs)：
- ctor 加 optional `TalentSkillModelResolver? talentSkillModelResolver` 第 N 個 optional param（對齊 Stage 72 PromptResolver 既有 ctor optional injection pattern）
- `GetResponseAsync` 在 dispatch 前透過 `talentSkillModelResolver.ResolveAsync(talentId, skillName, ct)` 取對應 (Provider, Model)
- 傳進 Claude Code CLI subprocess invocation（既有 model 參數 wire）
- fallback：若 `talentSkillModelResolver` null → 走既有 `_metadata.DefaultModelId`（backwards-compatible 0 regression）

**TalentId / skillName 傳入路徑**：
- 既有 `dispatch worker={Worker} capability={Skill} promptLen=...` log 已含 talent name + skill name
- 需要從 Petra dispatch path 傳 talentId（既有 ITalent.Id）+ skillName 進 adapter
- 修改 PetraOrchestratorService.DispatchTalentsAsync 內 dispatch site 對應傳值

### 4. 真並行 dispatch — DAG fan-out

**修改** [`PetraOrchestratorService.cs`](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs) `DispatchTalentsAsync`（line 532+）：

**既有線性 dispatch 邏輯**：
- 用 `SubtaskPlanTopologicalSort.Sort()` 取 topological order
- `for (var dispatchIndex = 0; dispatchIndex < orderedIds.Count; dispatchIndex++)` 線性 foreach
- 0 並行（即使 subtasks 互相 independent）

**Stage 74 改 DAG fan-out**：
- 新立 `SubtaskPlanLevelGrouping` helper（對齊既有 `SubtaskPlanTopologicalSort` Stage 70 pattern / 同檔內）：依 dependency level 分組（同 level = 依賴都已完成）
- DispatchTalentsAsync 改「按 level 並行 dispatch」：
  - 同 level 多 subtask → `Task.WhenAll` 並行 dispatch
  - 跨 level → sequential（前 level 完成才開後 level）
- 線性 chain（Trial baseline 3 subtask Cody→Vera→Quinn）= 每 level 1 subtask = 自然走 sequential（0 regression）
- 真並行場景（如未來大型任務拆 6-10 subtask 含獨立 subtask）= 同 level 多 subtask 平行 → 1.4-2.4× speedup

**Stage 70 對齊**：保留既有 `SubtaskPlan + Dependencies + TopologicalSort` 設計 / 不破壞 schema / 純擴展 dispatch loop。

### 5. ISkillRegistry SkillDescriptor metadata 擴展對齊 Agent Skills open standard

**修改** [`SkillDescriptor.cs`](src/AiTeam.Bot/Orchestration/Petra/Skills/SkillDescriptor.cs)（既有 record 3 field 簡單擴展）：

**新增 field**（簡化版對齊「自己用爽」精神 / 避過早 over-engineer JSON Schema 全套）：
- `RecommendedModelTier` (string) — 「cost-efficient」/「standard」/「strategic」三 tier 字串（描述用 / 不強制）
- `ReturnTypeDescription` (string) — 一句話描述 skill 輸出格式（如「JSON content + summary」/「test files + JSON report」/「review JSON with critical/warning/info」）

**update DefaultSkillRegistry**（[`ISkillRegistry.cs`](src/AiTeam.Bot/Orchestration/Petra/Skills/ISkillRegistry.cs)）：
- 6 SkillDescriptor 各加 RecommendedModelTier + ReturnTypeDescription（Forge 起草 + Christ Plan Mode review 拍細節）

**用途**：
- Petra orchestrator 動態選 Model 參考（如「strategic tier 任務 → 對應 Talent 沒設 per-Skill Model → fallback Talent.Model 仍可決定 Sonnet/Opus」）
- 未來 Phase 3 Step 6 Stage 75 WebUI Talent CRUD 顯示用
- 對齊業界 Agent Skills open standard format 第一步（完整 JSON Schema parameters 留真實業務需求觸發擴展）

### 6. DbSeeder 既有 6 TalentSkill seed 維持 Provider=Model=null

對齊「runtime fallback Talent.Model 既有 chain」紀律：
- DbSeeder 既有 6 TalentSkill seed（Cody-code_implementation / Cody-ui_design / Cody-release_publishing / Vera-code_review / Quinn-qa_testing / Sage-documentation）**Provider + Model 全 null**
- 對齊「Christ 後續手動 SQL UPDATE 或 WebUI 設定」紀律 / 不強塞推薦值避踩雷
- 未來真實業務需求出現（如「Cody-quick-typo 走 Haiku」）→ Christ 手動 SQL UPDATE 切

### 7. PromptResolver cache invalidate 串接

[`InternalController`](src/AiTeam.Bot/Api/InternalController.cs) 既有 reload-cache `scope=all` endpoint 加 `talentSkillModelResolver.InvalidateCache()` 觸發（對齊 Stage 72 既有 PromptResolver.InvalidateCache 整合 pattern）。

### 8. xUnit test 補強

新立 [`Stage74TalentSkillModelTests.cs`](src/AiTeam.Bot.Tests/Orchestration/Stage74TalentSkillModelTests.cs)（對齊 Stage 73 Stage73UpgradeTests pattern）：

| Test | 對應驗收場景 | 驗證點 |
|---|---|---|
| `T1_TalentSkillModelResolver_PerSkill_OverridesTalentDefault` | 場景 B | TalentSkill.Model="claude-opus" + Talent.Model="claude-sonnet" → resolver 返 "claude-opus" |
| `T2_TalentSkillModelResolver_PerTalent_FallbackWhenPerSkillNull` | 場景 B | TalentSkill.Model=null + Talent.Model="claude-sonnet" → resolver 返 "claude-sonnet" |
| `T3_TalentSkillModelResolver_RuntimeFallback_WhenBothNull` | 場景 B | TalentSkill.Model=null + Talent.Model=null → resolver 返 Agents:Dev:Model 既有 runtime fallback |
| `T4_TalentSkillModelResolver_InvalidateCache_RefreshesNextResolve` | 場景 B 補強 | cache hit → SQL UPDATE → InvalidateCache → 下次 resolve 返新值 |
| `T5_SubtaskPlanLevelGrouping_LinearChain_AllLevels1Subtask` | 場景 E | Trial baseline 3 subtask Cody→Vera→Quinn → 3 levels each 1 subtask（自然 sequential 0 regression）|
| `T6_SubtaskPlanLevelGrouping_DAG_IndependentSubtasksSameLevel` | 場景 D | 構造 4 subtask DAG（subtask 2/3 都 dependsOn=[1] / subtask 4 dependsOn=[2,3]）→ Level 1: [1] / Level 2: [2,3] / Level 3: [4] |
| `T7_SkillDescriptor_NewMetadataFields_PopulatedOnAll6Skills` | 場景 F | DefaultSkillRegistry.All 6 SkillDescriptor 全含 RecommendedModelTier + ReturnTypeDescription 非空 |

### 9. Directory.Build.props v3.63.0 → v3.64.0

---

## 設計決策

1. **per-Skill Model 走 TalentSkill schema 擴展不立新表**（對齊 Stage 67 既有 TalentSkill 設計 / 範圍最小 / 不增 join 成本 / Migration 純 ADD COLUMN nullable 0 既有 row 影響）
2. **三層 fallback chain**（per-Skill > per-Talent > Agents:Dev:Model）對齊 v5.5 既有「nullable fallback」紀律（Stage 67 Talent.Provider/Model nullable + Stage 72 PromptResolver cache fallback）
3. **DAG fan-out 用 Task.WhenAll 既有 .NET 內建**（不引入第三方 parallel framework / 對齊 v5.5 既有 Stage 70 SubtaskPlan dependency graph 設計）
4. **線性 chain 0 regression**（單 subtask 依賴前一個 / 每 level 1 subtask / 自然走 sequential / 對齊 Trial_v10-v19 baseline 3 subtask 線性場景兼容）
5. **Skill registry metadata 簡化版**（不擴 schema 為完整 JSON Schema parameters / 只加 RecommendedModelTier + ReturnTypeDescription / 對齊「自己用爽」精神 + 避過早 over-engineer / 業界 Agent Skills open standard 第一步落地）
6. **DbSeeder TalentSkill Model 推薦初始值 = null**（runtime fallback / Christ 後續手動設定 / 不強塞推薦值避踩雷）
7. **TalentSkillModelResolver Singleton + IServiceScopeFactory pattern**（對齊 Stage 72 PromptResolver 既有 pattern / 5-min TTL cache + InvalidateCache wire）
8. **backwards-compatible 守護 5 層**：v4 既有 path 0 動 / v5 既有 path 0 動 / v5.5 既有 hardcoded path（feature flag fallback）0 動 / Stage 70 SubtaskPlan + Dependencies + TopologicalSort 既有 schema 0 動 / Stage 72+73 既有 PromptResolver + Petra TalentPrompt persona prepend 0 動

---

## 驗收情境

### 場景 A：TalentSkill schema 擴展 + Migration（xUnit + production DB query）

**觸發**：`dotnet ef database update` apply Migration → SQL query talent_skills table

**驗證**：
- `talent_skills` 表新增 `Provider` (text nullable) + `Model` (text nullable) 兩欄位
- 既有 6 row（Cody-code_implementation / Cody-ui_design / Cody-release_publishing / Vera-code_review / Quinn-qa_testing / Sage-documentation）`Provider=NULL` + `Model=NULL`（DbSeeder 維持 null / runtime fallback）
- Migration 0 既有 row 影響（ADD COLUMN nullable / 0 row write）

### 場景 B：TalentSkillModelResolver 三層 fallback chain（xUnit）

**觸發**：手動構造 3 種 (TalentSkill.Model, Talent.Model, AppSettings:Agents:Dev:Model) 組合 → resolver.ResolveAsync

**驗證**：
- per-Skill 優先（TalentSkill.Model="claude-opus" + Talent.Model="claude-sonnet" → 返 "claude-opus"）
- per-Talent fallback（TalentSkill.Model=null + Talent.Model="claude-sonnet" → 返 "claude-sonnet"）
- runtime fallback（兩 null → 返 Agents:Dev:Model 既有 appsettings 值）
- Cache invalidate 真實生效（cache hit → SQL UPDATE → InvalidateCache → 下次 resolve 返新值）

### 場景 C：ClaudeCodeChatClientAdapter dispatch 用對應 Model（xUnit + Bot log）

**觸發**：手動 SQL UPDATE 切 `Cody-code_implementation` Model="claude-opus" + curl reload-cache → 跑 Mock task

**驗證**：
- Bot log 含 `ClaudeCodeChatClientAdapter dispatch worker=Cody capability=code_implementation model=claude-opus`（新加 model field）
- Claude Code CLI subprocess invocation 真實傳 `--model claude-opus`（gh 或 process trace 驗）
- 切回 Cody-code_implementation Model=null 後 → 走 Talent.Model = null → 走 Agents:Dev:Model fallback

### 場景 D：DAG fan-out 真並行 dispatch（xUnit + Bot log）

**觸發**：xUnit 構造 4 subtask DAG（subtask 2/3 都 dependsOn=[1] / subtask 4 dependsOn=[2,3]）→ 跑 DispatchTalentsAsync

**驗證**：
- xUnit：subtask 2 + 3 並行 dispatch（兩個 dispatch start 時間差 < 100ms / 對齊 Task.WhenAll 並行特徵）
- Bot log 含「PetraOrchestrator v5.5 自管 chain dispatch Level=2 並行 subtaskIds=[2,3] talents=[Vera,Quinn]」訊號（新加 Level + 並行 talents log）
- DispatchTalentsAsync 返回 3 dispatch summary（不是線性 4 個 sequential）
- 對照業界 1.4-2.4× speedup 預期（單 round 跑 4 subtask 真並行 vs 線性 baseline）

### 場景 E：線性 chain 0 regression（xUnit + Trial_v20）

**觸發**：xUnit 跑 Trial_v10-v19 baseline 3 subtask 線性 plan（Cody→Vera→Quinn / 每 dependency 1→2→3 線性）+ Trial_v20 真實業務驗

**驗證**：
- xUnit：3 levels each 1 subtask → 走 sequential（前一完成才下一 / 對齊既有線性 dispatch 行為）
- Bot log 含「Level=1 sequential subtaskId=1 talent=Cody」/「Level=2 sequential subtaskId=2 talent=Vera」/「Level=3 sequential subtaskId=3 talent=Quinn」訊號
- Trial_v20 對齊 Trial_v19 baseline cost/outputLen/業務評分（0 regression）
- 既有 Trial baseline 3 subtask 線性場景 100% 兼容

### 場景 F：ISkillRegistry SkillDescriptor metadata 擴展（xUnit）

**觸發**：xUnit 跑 `ISkillRegistry.All` → 檢查 6 SkillDescriptor 新 field 內容

**驗證**：
- 6 SkillDescriptor 全含 `RecommendedModelTier`（值 ∈ {「cost-efficient」/「standard」/「strategic」}）
- 6 SkillDescriptor 全含 `ReturnTypeDescription`（非空字串 / 對齊各 skill 真實輸出格式描述）
- 既有 Name / DisplayName / Description 0 變動（backwards-compatible）

### 場景 G：Trial_v20 真實業務驗（Aria 9-step 模板第 10 次實踐）

**觸發**：沿用 Trial_v6-v19 同 prompt（Dashboard 錯誤處理打磨）+ 開跑前手動 SQL UPDATE TalentSkill：
- `Cody-code_implementation` Model="claude-sonnet-4.5"（既有 baseline）
- `Sage-documentation` Model="claude-haiku-4"（cost-efficient 短期實驗 — 場景 G 觀察 Sage 走 Haiku 後品質 vs cost trade-off）

**驗證**：
- Bot log 含 `model=claude-sonnet-4.5`（Cody）/ `model=claude-haiku-4`（Sage 若 Petra dispatch documentation 任務）
- Trial_v20 cost vs Trial_v19 baseline 對照（Sage 走 Haiku 預期 cost 降 / 業務品質維持 ≥ 4.5/5）
- Petra dispatch path 真實生效 per-Skill Model（gh / Bot log 確認 CLI subprocess 真實傳對應 model arg）

### 場景 H：v4 既有 path 0 regression

**觸發**：SQL 切 `Workflow:UsePetraOrchestratorV5=false`（v4 path） + 跑 Mock task

**驗證**：
- Bot log 0 含「Petra v5.5 path」字樣 / 走 v4 既有 path
- v4 既有 path 0 動 talent_skills schema（v4 path 不讀 TalentSkill 表）
- v4 path 既有 baseline 行為 0 改變

---

## 技術約束

- 環境細節 source of truth 對齊 [workflow_aria.md 第三節 A 第 7 條紀律](../../memory/workflow_aria.md)
- TalentSkill schema 擴展對齊 Stage 67 既有 Talent.Provider/Model nullable pattern + ef-core.md Migration 紀律
- TalentSkillModelResolver Singleton + IServiceScopeFactory pattern 對齊 Stage 72 PromptResolver 既有 cache 設計
- SubtaskPlanLevelGrouping helper 對齊 Stage 70 既有 SubtaskPlanTopologicalSort 同檔內 pattern
- xUnit test 補強對齊 Stage 73 既有 Stage73UpgradeTests baseline pattern
- 對齊 [`docs/conventions/csharp.md`](../conventions/csharp.md) / [`ef-core.md`](../conventions/ef-core.md) / [`refactor-sop.md`](../conventions/refactor-sop.md)
- backwards-compatible 守護 5 層延續：v4 既有 path / v5 既有 path / v5.5 既有 hardcoded path（feature flag fallback） / Stage 70 SubtaskPlan 既有 schema / Stage 72+73 既有 PromptResolver + Petra persona prepend
- 業界 reference 沿用 Future_Feature_v5.5.md + 本檔戰略脈絡段 WebSearch 結論（不重複觸發 / 純內部 refactor + 既有 v5.5 pattern 對齊）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-17 | 規劃書建立 — v3.64.0 / M+ 規模 / v5.5 Phase 3 Step 8 per-Skill Model + 真並行 dispatch + Skill registry metadata 擴展。**範圍**：TalentSkill schema 加 Provider/Model nullable + Migration / TalentSkillModelResolver 三層 fallback chain（per-Skill > per-Talent > Agents:Dev:Model）/ ClaudeCodeChatClientAdapter Model 動態選擇整合 / 真並行 dispatch DAG fan-out（同 dependency level Task.WhenAll / 線性 chain 仍 sequential 0 regression）/ ISkillRegistry SkillDescriptor metadata 擴展（RecommendedModelTier + ReturnTypeDescription 簡化版對齊 Agent Skills open standard format 第一步）/ DbSeeder 既有 6 TalentSkill seed Provider=Model=null 維持 / PromptResolver cache invalidate 串接 / xUnit 7 new case + Directory.Build.props bump。**戰略脈絡**：Christ 2026-05-17 連續兩個戰略 question 點破真實架構缺口（仲裁是 Agent 還是權責？/ 權責能不能設 Model？）→ 修根因紀律先補完 per-Skill Model 三層架構 / debate 機制延後 Phase 4 / 撤回 Cora Talent 建議。**業界 WebSearch 結論延用**（Model mesh approach / Capability-aware routing / Model routing 比 uniform 便宜 60% / Agent Skills open standard / 真並行 1.4-2.4× speedup / State management primary 挑戰 AiTeam 已對齊 ✓）。**核心紀律**：DbSeeder 既有 6 TalentSkill Provider=Model=null 維持（runtime fallback / Christ 後續手動 SQL UPDATE 或 WebUI 設定）+ Skill registry metadata 簡化版避過早 over-engineer JSON Schema 全套。**校準錨預期**：一般架構級重構區間 ×0.43-0.60（Stage 67/68/69/70/72/73 6 資料點 baseline / Stage 74 = 第 7 資料點累積）。**Aria prep-session 預估**：raw read existing files ~20-25K + 機制層 code 改動 ~30-40K + DI propagation chain ~10-15K + xUnit 補強 ~10-15K = raw ~70-95K × 1.6 = **112-152K 總 context**（Sonnet 200K + high 充裕 / 對齊自省點 #37 三步法）。**驗收**：8 場景 — A schema 擴展 + Migration / B Resolver 三層 fallback / C Adapter Model 動態整合 / D DAG fan-out 真並行 / E 線性 chain 0 regression / F SkillDescriptor metadata 擴展 / G **Trial_v20 真實業務驗（Aria 9-step 第 10 次實踐 + Sage Haiku 短期實驗 cost optimization 真實生效）** / H v4 path 0 regression。**下一步**：Forge 實作 + Aria gate1 Tier 0+1+Tier 2 #3 build + Trial_v20 真實任務驗 → 通過後 Stage 76 開（兩層 queue 配套：Petra 接收層 + Worker 執行層 per-Talent 1 task at a time）。**Phase 3 完整收口路徑**：73 ✅ → 74（per-Skill Model + 真並行）→ 76（兩層 queue 配套）→ 75（WebUI Talent CRUD 最後做）→ v5.5 完整收口。 |
