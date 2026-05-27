# Stage 74 Roadmap — v5.5 Phase 3 Step 8：per-Skill Model + 真並行 dispatch + Skill registry metadata 擴展

> 目標版本：**v3.64.0**（minor — v5.5 Phase 3 第二步 / 一般架構級重構：Talent-Skill-Model 三層架構打磨完整 + DAG fan-out 並行 dispatch + 對齊業界 2026 Agent Skills open standard format）
> 狀態：✅ 已完成（2026-05-17）
> 文件版本：v2.0
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

## 實作紀錄

> **Forge 結案第一段 — 2026-05-17 完成**
> commit [`3a66f88`](https://github.com/darkleong/AiTeam/commit/3a66f88) — feat(stage74): per-Skill Model + 真並行 dispatch + Skill registry metadata 擴展 v3.64.0 — v5.5 Phase 3 Step 8
> 規模：18 files changed / 1956 insertions / 123 deletions（純機制升級層 / 0 文案層）

### 實作完成項目（依 9 子項對照）

| Roadmap 子項 | 對應檔案 | 完成情況 |
|---|---|---|
| 1. TalentSkill schema 擴展 + Migration | [`src/AiTeam.Data/Entities.cs:340`](../../src/AiTeam.Data/Entities.cs) + [`Migrations/20260517072347_Stage74TalentSkillModel.cs`](../../src/AiTeam.Data/Migrations/20260517072347_Stage74TalentSkillModel.cs) | ✅ 純 ADD COLUMN nullable / 0 既有 row 影響 / production Migration apply 成功（log `Applying migration '20260517072347_Stage74TalentSkillModel'`）|
| 2. TalentSkillModelResolver 三層 fallback chain | [`src/AiTeam.Bot/Services/TalentSkillModelResolver.cs`](../../src/AiTeam.Bot/Services/TalentSkillModelResolver.cs)（新立 / 117 行）| ✅ Singleton + 5-min TTL + SemaphoreSlim + double-check lock + IServiceScopeFactory 對齊 Stage 72 PromptResolver pattern / T1-T4 xUnit 全綠 |
| 3. ClaudeCodeChatClientAdapter Model 動態整合 | [`src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs`](../../src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs) | ✅ ctor 加 `Guid? talentId` + `TalentSkillModelResolver?` 兩 optional / GetResponseAsync 內 resolvedModel 動態 / DispatchAsync 簽名 propagate / log 加 `model={Model}` field |
| 3′. DI propagation chain（C′）| [`PetraWorkerHelper.cs`](../../src/AiTeam.Bot/Orchestration/Petra/PetraWorkerHelper.cs) + [`GenericAgentTool.cs`](../../src/AiTeam.Bot/Orchestration/Petra/GenericAgentTool.cs) + [`ITalentFactory.cs`](../../src/AiTeam.Bot/Orchestration/Petra/ITalentFactory.cs) + [`Program.cs:108`](../../src/AiTeam.Bot/Program.cs) | ✅ BuildAgent + GenericAgentTool ctor + DefaultTalentFactory ctor 全鏈打通 / C# default value 自動套 0 既有 caller 改 / DI Singleton 註冊 |
| 4. 真並行 dispatch DAG fan-out | [`SubtaskPlan.cs:181-237`](../../src/AiTeam.Bot/Orchestration/Petra/SubtaskPlan.cs) `SubtaskPlanLevelGrouping`（新立 helper）+ [`PetraOrchestratorService.cs:532+`](../../src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs) `DispatchTalentsAsync` level-based 改寫 + 抽 `BuildInputMessagesForSubtaskAsync` + `ProcessSubtaskResultAsync` 2 helper | ✅ Kahn-style BFS 分層 / 路線 A 紀律生效（LLM dispatch 並行 / DB write 並行段結束後 sequential）/ T5 + T6 xUnit 全綠 |
| 5. ISkillRegistry SkillDescriptor metadata 擴展 | [`SkillDescriptor.cs`](../../src/AiTeam.Bot/Orchestration/Petra/Skills/SkillDescriptor.cs) record 加 2 field + [`ISkillRegistry.cs`](../../src/AiTeam.Bot/Orchestration/Petra/Skills/ISkillRegistry.cs) 6 row 對齊 Christ 拍板 tier | ✅ standard×3 + strategic×2 + cost-efficient×1 對齊 議題 2 拍板 / T7 xUnit 全綠 |
| 6. DbSeeder 維持 Provider=Model=null | （0 改動）| ✅ production SQL 驗：既有 6 row（Cody×3 / Vera×1 / Quinn×1 / Sage×1）Provider 與 Model 皆 NULL |
| 7. reload-cache 串接 | [`InternalController.cs:60-64`](../../src/AiTeam.Bot/Api/InternalController.cs) | ✅ scope=all path 加 `talentSkillModelResolver.InvalidateCache()` / production `curl /internal/reload-cache?scope=all` 驗返 200 |
| 8. xUnit 7 case | [`Stage74TalentSkillModelTests.cs`](../../src/AiTeam.Bot.Tests/Orchestration/Stage74TalentSkillModelTests.cs)（新立 / 240 行）| ✅ T1-T7 全綠（含 T4 InvalidateCache 刷新補強）+ Test23 baseline update 對齊 Linear factory 修根因 |
| 9. Directory.Build.props bump | [`src/Directory.Build.props`](../../src/Directory.Build.props) | ✅ v3.63.0 → v3.64.0 |

### 關鍵設計決策

**1. 真並行 dispatch 內 AppDbContext thread-safety 採路線 A（Christ 議題 1 拍板）**：LLM dispatch 並行 / DB write 並行段結束後 sequential — 對齊「session message order sequential」既有紀律 + speedup 主來源（LLM 慢）保留 + 0 race risk。並行段內 `talentAgent.RunAsync` 各自獨立（IClaudeCodeService subprocess 各自獨立 / TokenLogService 自開 scope / Resolver 自管 lock）— 與 ctor 注入的 db 0 衝突。

**2. Resolver 保 `(Provider, Model)` tuple return（Christ 議題 3 拍板）**：Stage 74 範圍 Adapter 只 propagate `Model` 進 `IClaudeCodeService.RunXxxAsync`（既有簽名只吃 model + apiKey）/ Provider tuple 保留為 Phase 3 真實切 GPT-4o / Gemini 鋪路（evaluate IClaudeCodeService DI proxy 升級 IModelDispatcher）。

**3. SkillDescriptor metadata 簡化版（Christ 議題 2 拍板）**：6 row tier 分類（standard×3 / strategic×2 / cost-efficient×1）+ 一句話 ReturnTypeDescription / 不擴 JSON Schema parameters 全套 — 對齊「自己用爽」精神避過早 over-engineer / 業界 Agent Skills open standard 第一步落地。

**4. ctor 加 optional param vs new 介面**：對齊 Stage 72 既有 `PromptResolver` optional injection pattern — Adapter / BuildAgent / GenericAgentTool / DefaultTalentFactory 全加 optional param + C# default value 自動套 / 0 既有 caller 改（含 xUnit `NewAdapter` helper 8 param named arg 0 改動）。

**5. TalentSkillModelResolver Singleton + IServiceScopeFactory + 5-min TTL**：完全對齊 Stage 72 PromptResolver pattern — 解 Singleton-Scoped 雷 + InvalidateCache wire 對齊 reload-cache `scope=all`。

**6. DbSeeder 既有 6 TalentSkill seed Provider=Model=null 維持**：對齊 Roadmap 紀律 — runtime fallback / Christ 後續手動 SQL UPDATE 或 WebUI 設定切（如 Trial_v20 場景 G 預想 Sage Haiku 短期實驗）/ 不強塞推薦值避踩雷。

**7. Level grouping 用獨立 helper `SubtaskPlanLevelGrouping`**：不破壞 Stage 70 既有 `SubtaskPlanTopologicalSort` 設計 + Test25 baseline 0 衝突 / 同檔內接續 pattern。

### 驗收後修正（Forge spike 揭架構盲點修根因 — 對齊 Stage 58 結論第 N 次累積）

**SubtaskPlan.Linear factory Stage 70 設定 0 deps 在 DAG fan-out 引入後會被誤為「全並行 level 0」破壞 Trial baseline sequential 紀律**：

- **問題揭露時序**：Forge 實作 §D 完成跑 `dotnet test` → T5 失敗（`Assert.Single(levels[0])` 不滿足 — Linear 3 subtask 0 deps → LevelGrouping 把全 3 subtask 放在 level 0 → 視為「全並行」）
- **真實根因**：Linear chain 業務語意 = 後 Talent 吃前 Talent output（`BuildNextWorkerInput(taskInput, summaries)` 把 prior summaries 全餵下個）— **必須 sequential**。Stage 70 設定 0 deps 是 DAG fan-out 未引入前的設計簡化 / Stage 74 引入 LevelGrouping 後此設計 misalign
- **修法**：`SubtaskPlan.Linear` factory 加 sequential edges（1→2, 2→3, ..., n-1→n）對齊真實語意
- **影響面**：Test23 baseline assertion update 1 處（`Assert.Empty(plan.Dependencies)` → `Assert.Equal(2, plan.Dependencies.Count)` + 驗 2 條 sequential edges + 補單 skill 0 edges case + 命名 `ZeroDependencies` → `SequentialEdges`）
- **TopologicalSort 兼容**：Test25 case 1 仍正確（chain with deps 仍回 [1,2,3] 升序）/ 既有 Stage 70 路徑 0 regression

對齊 Stage 58 結論「Forge spike 揭露架構盲點紀律生效」第 N 次累積 — 規劃層難預見的 architecture gap 由 Forge 實作層揭露 + 自診修根因 + 既有 baseline test 同步更新。

### Mock 覆蓋情況

Stage 74 純機制升級層（schema + Resolver + DAG fan-out + metadata）— **無對應 Mock scenario 設計**。場景驗收分配：

- 場景 A（schema + Migration）→ production SQL `\d talent_skills` + 既有 row Provider=Model=NULL 驗 ✅
- 場景 B（Resolver 三層 fallback）→ xUnit T1-T4 ✅
- 場景 D（DAG fan-out 並行）→ xUnit T6 ✅
- 場景 E（線性 chain 0 regression）→ xUnit T5 + Test30 既有 dispatch baseline 0 regression ✅
- 場景 F（SkillDescriptor metadata）→ xUnit T7 ✅
- **場景 B 補強**（reload-cache wire）→ production `curl /internal/reload-cache?scope=all` 驗 200 ✅
- 場景 C（Adapter dispatch model field 真實 propagate）→ **Aria gate2 範圍**（需手動 SQL UPDATE + 跑真實 dispatch + grep Bot log `model=...` field）
- 場景 G（Trial_v20 真實業務驗 — Sage Haiku cost optimization 短期實驗）→ **Aria gate2 + Aria 9-step 模板第 10 次實踐範圍**
- 場景 H（v4 path 0 regression — flag UsePetraOrchestratorV5=false）→ **Aria gate2 範圍**

### 踩坑紀錄

**1. PowerShell Bash psql column reference 歧義（Trial_v7 紀律延伸第 N 次踩）**：
production 自驗時跑 `SELECT "SkillName", "Provider", "Model", t."Name" ... FROM talent_skills ts JOIN talents t ...` → `ERROR: column reference "Provider" is ambiguous`（talents 表 + talent_skills 表都有 Provider 欄位）。修法：所有 column 加 table alias qualified ref（`ts."Provider"` / `t."Name"`）。對齊 SQL JOIN best practice。

**2. Linear factory 0 deps Stage 70 設計與 Stage 74 DAG fan-out 語意衝突**：
已在「驗收後修正」段詳述。揭露時序：Forge 實作 §D 完成跑 xUnit → T5 fail → 自診修根因。

**3. Aria 二檢 6 條 Warning gate1 自驗紀律真實生效**：
W1（appsettings.json Agents:Dev:Model default）→ 對齊 BuildSessionContext 既有 fallback chain 同源 / W4（AIAgent.RunAsync thread-safety）→ WorkerDispatchSummary immutable record + TokenLogService 自開 scope 雙保障 / W6（SkillDescriptor caller grep）→ 唯一 caller DefaultSkillRegistry compile-time 強制更新。grep verify 紀律真實避了「憑印象寫」風險 — 對齊「自省點 #37 source of truth 紀律」第 N 次累積。

### Gate1 自驗紀律已套（對齊 Aria 二檢 6 條 Warning）

- ✅ W1: `GetRuntimeModel` fallback chain 對齊 BuildSessionContext 同源（`Agents:Dev:Model ?? Anthropic:DefaultModel ?? "claude-opus-4-6"`）
- ✅ W2: `DispatchAsync` switch 7 case 全 cover（含既有 requirements_extraction v5 fallback path）
- ✅ W3: GenericAgentTool 既有 explicit ctor pattern 對齊（不無謂 refactor）
- ✅ W4: AIAgent.RunAsync thread-safety（agents independent）+ WorkerDispatchSummary immutable record + TokenLogService 自開 scope 三重保障
- ✅ W5: T6 xUnit case 用 `DependencyType.Sequential` 對齊 Stage 70 既有 enum value
- ✅ W6: SkillDescriptor record 加 2 field — dotnet build 0 error / dotnet test 0 既有 caller 漏改

### 本機驗證結果

| 項目 | 結果 |
|---|---|
| `dotnet ef migrations add Stage74TalentSkillModel` | ✅ 純 ADD COLUMN nullable / 0 既有 row 影響 |
| `dotnet build AiTeam.slnx` | ✅ 0 Error / 102 Warning（全 PR Playwright 既有 + obsolete fallback / 無新增） |
| `dotnet test` | ✅ **82/82 全綠**（7 新 Stage 74 + 既有 Test1-50 + Stage 73 + ClaudeCodeChatClientAdapterTests 全保留 + Test23 baseline 升級） |
| CI/CD 部署 | ✅ `Deploy main (3a66f88)（done）` |
| Migration production apply | ✅ `Applying migration '20260517072347_Stage74TalentSkillModel'` |
| 場景 A schema + 既有 6 row NULL 維持 | ✅ production SQL 驗 |
| 場景 B 補強 reload-cache wire | ✅ `curl /internal/reload-cache?scope=all` 返 200 |

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-17 | **Forge 結案第一段** — 8 子項 + 9 修根因實作完整收口（commit `3a66f88` / 18 files changed / 1956 insertions / 123 deletions / dotnet test 82/82 全綠 / production Migration apply + 既有 6 row NULL 維持 + reload-cache wire 全驗）。**3 議題 Christ 拍板採納**：① 路線 A LLM 並行 / DB write sequential ② 6 row tier 草稿全採納（standard×3 / strategic×2 / cost-efficient×1）③ Resolver 保 `(Provider, Model)` tuple return 為未來 Phase 3 真實切 GPT-4o / Gemini 鋪路。**Forge spike 揭架構盲點修根因（對齊 Stage 58 結論第 N 次累積）**：SubtaskPlan.Linear factory Stage 70 設定 0 deps 在 DAG fan-out 引入後被誤為「全並行 level 0」破壞 Trial baseline sequential 紀律 → Linear factory 加 sequential edges (1→2, 2→3, ..., n-1→n) 對齊真實語意 + Test23 baseline update 1 處。**Aria 二檢 6 條 Warning gate1 自驗全綠**（W1-W6 grep verify + dotnet build/test 雙重保障 — appsettings model fallback / 7 case cover / GenericAgentTool ctor / AIAgent thread-safety / DependencyType enum / SkillDescriptor caller）。**自驗範圍 A-F 透過 xUnit + production SQL + reload-cache 全綠**（場景 C/G/H 範圍外 — Aria gate2 + Trial_v20 範疇）。 |
| v1.0 | 2026-05-17 | 規劃書建立 — v3.64.0 / M+ 規模 / v5.5 Phase 3 Step 8 per-Skill Model + 真並行 dispatch + Skill registry metadata 擴展。**範圍**：TalentSkill schema 加 Provider/Model nullable + Migration / TalentSkillModelResolver 三層 fallback chain（per-Skill > per-Talent > Agents:Dev:Model）/ ClaudeCodeChatClientAdapter Model 動態選擇整合 / 真並行 dispatch DAG fan-out（同 dependency level Task.WhenAll / 線性 chain 仍 sequential 0 regression）/ ISkillRegistry SkillDescriptor metadata 擴展（RecommendedModelTier + ReturnTypeDescription 簡化版對齊 Agent Skills open standard format 第一步）/ DbSeeder 既有 6 TalentSkill seed Provider=Model=null 維持 / PromptResolver cache invalidate 串接 / xUnit 7 new case + Directory.Build.props bump。**戰略脈絡**：Christ 2026-05-17 連續兩個戰略 question 點破真實架構缺口（仲裁是 Agent 還是權責？/ 權責能不能設 Model？）→ 修根因紀律先補完 per-Skill Model 三層架構 / debate 機制延後 Phase 4 / 撤回 Cora Talent 建議。**業界 WebSearch 結論延用**（Model mesh approach / Capability-aware routing / Model routing 比 uniform 便宜 60% / Agent Skills open standard / 真並行 1.4-2.4× speedup / State management primary 挑戰 AiTeam 已對齊 ✓）。**核心紀律**：DbSeeder 既有 6 TalentSkill Provider=Model=null 維持（runtime fallback / Christ 後續手動 SQL UPDATE 或 WebUI 設定）+ Skill registry metadata 簡化版避過早 over-engineer JSON Schema 全套。**校準錨預期**：一般架構級重構區間 ×0.43-0.60（Stage 67/68/69/70/72/73 6 資料點 baseline / Stage 74 = 第 7 資料點累積）。**Aria prep-session 預估**：raw read existing files ~20-25K + 機制層 code 改動 ~30-40K + DI propagation chain ~10-15K + xUnit 補強 ~10-15K = raw ~70-95K × 1.6 = **112-152K 總 context**（Sonnet 200K + high 充裕 / 對齊自省點 #37 三步法）。**驗收**：8 場景 — A schema 擴展 + Migration / B Resolver 三層 fallback / C Adapter Model 動態整合 / D DAG fan-out 真並行 / E 線性 chain 0 regression / F SkillDescriptor metadata 擴展 / G **Trial_v20 真實業務驗（Aria 9-step 第 10 次實踐 + Sage Haiku 短期實驗 cost optimization 真實生效）** / H v4 path 0 regression。**下一步**：Forge 實作 + Aria gate1 Tier 0+1+Tier 2 #3 build + Trial_v20 真實任務驗 → 通過後 Stage 76 開（兩層 queue 配套：Petra 接收層 + Worker 執行層 per-Talent 1 task at a time）。**Phase 3 完整收口路徑**：73 ✅ → 74（per-Skill Model + 真並行）→ 76（兩層 queue 配套）→ 75（WebUI Talent CRUD 最後做）→ v5.5 完整收口。 |
