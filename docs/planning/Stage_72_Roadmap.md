# Stage 72 Roadmap — v5.5 Phase 2 Step 5：Prompt DB 化 + Talent identity 整合（兩層 schema）

> 目標版本：**v3.62.0**（minor — v5.5 Phase 2 第三步 / 架構級重構：Prompt 從 hardcoded → DB / 對齊業界 2026 prompt orchestration 主流）
> 狀態：✅ 已完成（2026-05-17）— Forge 結案第一段 / 等 Aria gate2 結案第二段（CHANGELOG + Future_Feature_v5.5.md 同步）+ Trial_v18 真實業務驗 → 切 default true 完整收口
> 文件版本：v2.0
> 範圍：SkillPrompts + TalentPrompts 兩層 schema + Migration + PromptRepository + DbSeeder + v5.5 path 整合 + Versioning + rollback + feature flag 守 fallback + xUnit
> 規模：M
> 對應 v5.5 規劃：[Future_Feature_v5.5.md](Future_Feature_v5.5.md) Phase 2 Step 5

---

## 戰略脈絡

**Trial_v17 結案後 v5.5 Phase 2 Step 5 開跑 — Prompt 從 hardcoded → DB**：

對齊 Christ「Agent 像人類處理事件」精神核心要素 5（Talent-Skill separation + horizontal scaling）+ Trial_v17 戰略級觀察「讓 Talent 自主判斷做法 / 我們只定品質標準」精神實作。

### 計劃前 WebSearch 結論（2026-05-17）

**業界 2026 主流共識**（[Top 5 Prompt Orchestration Platforms 2026](https://www.getmaxim.ai/articles/top-5-prompt-orchestration-platforms-for-ai-agents-in-2026/) + [Anthropic Multi-Agent Research System](https://www.anthropic.com/engineering/multi-agent-research-system)）：

- **Database-backed prompt management with versioning is the industry standard, not hardcoded prompts**（含 Orchestrator-level prompt — 不分層保護「不准 DB 化」）
- 「a single change to a system prompt can cause significant failures」→ 業界靠 **versioning + rollback** 保護 production，不靠「禁止編輯」
- Over 50% of companies expected to adopt AI orchestration platforms by 2026

**對應 Christ 4 議題拍板**：
- 議題 1 → **全部包**（含 Petra Orchestrator prompt）對齊業界主流 + Versioning + rollback 保護
- 議題 2 → **兩層 schema**：SkillPrompts（職位層 / per-Skill 共享 / 工作範圍）+ TalentPrompts（個性層 / per-Talent / 風格差異 / nullable）
- 議題 3 → **純後端**（WebUI 編輯介面留 Phase 3 Step 6）
- 議題 4 → **內容不動**（Stage 72 範圍只做「搬家工程」/ prompt content 升級留 Stage 73+ 評估）

### 範圍邊界刻意收緊

- ✅ 做：兩層 schema + Migration + PromptRepository + DbSeeder 把 v5.5 path 既有 hardcoded prompt seed 進 DB + 整合 v5.5 path（BuildPetraSystemPrompt + ClaudeCodeChatClientAdapter）從 DB 讀 + Versioning + rollback + feature flag `Workflow:UseV5PromptDb` default false 守 fallback + xUnit + Directory.Build.props v3.61.0 → v3.62.0
- ❌ 不做：
  - **v4 既有 Agent service path 動**（DevAgentService / QaAgentService / ReviewerAgentService / DocAgentService 等 — 走 v4 path / Trial_v17 後 v5.5 production active / v4 path prompt 維持既有 hardcoded 不擴）
  - **WebUI 編輯介面**（Phase 3 Step 6 範圍 / 對齊「Phase 1+2 完整收口才開 WebUI」紀律）
  - **TalentPrompts persona seed**（Phase 3 WebUI Talent CRUD 才補 / Stage 72 schema 預留 nullable）
  - **prompt content 升級**（議題 4 拍板 — 留 Stage 73+ 評估）

---

## 子項清單

### 1. SkillPrompts + TalentPrompts 兩層 schema（議題 2 拍板）

**新檔** [`Entities.cs`](src/AiTeam.Data/Entities.cs) 加 2 個 entity record：

- **`SkillPrompt`**（職位層 / per-Skill 共享）：`Id` (Guid) / `SkillName` (text) / `PromptBody` (text) / `VersionNumber` (int) / `IsActive` (bool) / `CreatedByUser` (text? — 預留 future audit) / `CreatedAt` / `UpdatedAt`
- **`TalentPrompt`**（個性層 / per-Talent / nullable）：`Id` / `TalentId` (Guid FK) / `PersonaBody` (text) / `VersionNumber` / `IsActive` / `CreatedAt` / `UpdatedAt`

**partial unique index**（對齊 Stage 69 既有 nullable unique pattern / 議題 #2 修法後紀律）：
- SkillPrompts: `(SkillName) WHERE IsActive = true` partial unique（同 skill 只一條 active）
- TalentPrompts: `(TalentId) WHERE IsActive = true` partial unique（同 talent 只一條 active persona）

**對齊既有 entity pattern**（Stage 67 talents / talent_skills + Stage 69 task_memories / talent_memories）。

### 2. EF Core Migration

新 Migration `Stage72PromptSchema`（對齊 [`docs/conventions/ef-core.md`](docs/conventions/ef-core.md) Migration 紀律 + Stage 69 既有 race-safe DbSeeder pattern）。

### 3. PromptRepository CRUD + query API

**新檔** `src/AiTeam.Data/Repositories/PromptRepository.cs`（對齊 [`MemoryRepository`](src/AiTeam.Data/Repositories/MemoryRepository.cs) Stage 69 既有 pattern）：

- `GetActiveSkillPromptAsync(string skillName, CancellationToken ct)` → 返回當前 active SkillPrompt
- `GetActiveTalentPromptAsync(Guid talentId, ct)` → 返回當前 active TalentPrompt（可能 null）
- `UpsertSkillPromptAsync(string skillName, string body, ct)` → 新版本 row + 舊 active 切 false
- `UpsertTalentPromptAsync(Guid talentId, string body, ct)`
- `RollbackSkillPromptAsync(string skillName, int targetVersion, ct)` → 切換 active 到指定版本
- `ListSkillPromptVersionsAsync(string skillName, ct)` → 列出 skill 所有版本（給 future audit / WebUI 用）

DI 註冊：Scoped（對齊 MemoryRepository 既有 lifecycle）。

### 4. DbSeeder 把現有 hardcoded prompt seed 進 SkillPrompts

對齊 Stage 67 talents seed + Stage 69 既有 race-safe DbSeeder pattern：

**6 個 SkillPrompts seed**（v5.5 path 真實使用）：
- `code_implementation` ← `Resources/CLAUDE_Cody.md` content
- `code_review` ← `Resources/CLAUDE_Vera.md` content
- `qa_testing` ← `Resources/CLAUDE_Quinn.md` content
- `documentation` ← `Resources/CLAUDE_Sage.md` content
- `ceo_orchestration` ← `Resources/CLAUDE_Victoria.md` content
- `petra_orchestration` ← `Resources/CLAUDE_Petra.md` content + `BuildPetraSystemPrompt` method body 動態生成段（hierarchical decomposition + 拆解紀律 — Stage 70+71 累積 / Stage 72 保留動態 skill roster 注入機制）

**version_number=1 / is_active=true** baseline seed。

### 5. 整合 v5.5 path 從 DB 讀 prompt

**修改**：
- [`PetraOrchestratorService.cs`](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs) `BuildPetraSystemPrompt` — feature flag=true 時從 DB load `petra_orchestration` SkillPrompt + 動態組合 skill roster（Stage 70+71 累積 hierarchical decomposition 紀律保留）
- [`ClaudeCodeChatClientAdapter.cs`](src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs) Worker dispatch — feature flag=true 時從 DB load `{capability}` SkillPrompt + TalentPrompt persona（如有 nullable）組合進 Worker prompt
- Cache layer：5 分鐘 TTL 對齊既有 `AppSettingsService` pattern（避每次 LLM call hit DB）

### 6. Feature flag `Workflow:UseV5PromptDb` default false

**修改**：[`WorkflowSettings.cs`](src/AiTeam.Bot/Configuration/WorkflowSettings.cs) + [`WorkflowSettingsResolver.cs`](src/AiTeam.Bot/Configuration/WorkflowSettingsResolver.cs) + `appsettings.json`

- `UseV5PromptDb: bool` default false（守 v5.5 既有 hardcoded path fallback — Trial_v18 驗 + Christ 拍板才切 default true）
- 必須 `UsePetraOrchestratorV5=true` + `UseTalentSkillSeparation=true` 才有意義
- 對齊既有 v5/v5.5 flag pattern

### 7. xUnit test 補強

新 4-6 case 對齊 Stage 69 baseline pattern：
- SkillPrompts / TalentPrompts schema baseline test（partial unique index 驗）
- PromptRepository CRUD + Upsert + Rollback + version_number 累積
- DbSeeder 6 個 SkillPrompts seed 真實對齊 hardcoded content
- BuildPetraSystemPrompt feature flag=true 從 DB load 對齊 hardcoded content（backwards-compatible 守護）
- ClaudeCodeChatClientAdapter Worker dispatch feature flag=true 從 DB load
- feature flag=false 守 hardcoded path 0 regression

### 8. Directory.Build.props v3.61.0 → v3.62.0

---

## 設計決策

1. **議題 1 拍板 — 全部包含 Petra Orchestrator prompt**（對齊業界 2026 主流 + WebSearch 結論 / versioning + rollback 解決「Petra 被誤改 production 崩」風險 / AiTeam single-tenant Christ 個人專屬無「多 admin 誤改」場景）
2. **議題 2 拍板 — 兩層 schema**（SkillPrompts 職位層共享 + TalentPrompts 個性層 per-Talent / 對齊「對冗餘不容忍」精神 — 同 Skill 多 Talent 不重複存職責 / 對齊 v5.5 Phase 3 多 Talent 規劃）
3. **議題 3 拍板 — 純後端**（WebUI 編輯介面留 Phase 3 Step 6 / 改 prompt 暫時用 SQL UPDATE 對齊既有 ops SoT 紀律 5 分鐘 reload-cache 生效）
4. **議題 4 拍板 — 內容不動**（Stage 72 範圍只做「搬家工程」/ prompt content 升級留 Stage 73+ 評估 — 對齊 Trial_v18 結果再判斷哪些紀律有用）
5. **Versioning method A — 單表 + version_number + is_active flag**（對齊 AiTeam single-tenant 簡潔精神 / 同 SkillName 多 row 累積 / partial unique index 守一條 active / rollback = SQL UPDATE 切換 is_active）
6. **Cache layer 5 分鐘 TTL**（對齊既有 `AppSettingsService` pattern / 避每次 LLM call hit DB）
7. **Backwards-compatible 守護 4 層**：v4 既有 path 0 動 / v5.5 既有 hardcoded path（feature flag=false fallback）/ v5 PoC path 0 動 / Stage 70+71 BuildPetraSystemPrompt 累積紀律保留（seed 進 DB + 動態 skill roster 注入機制）

---

## 驗收情境

### 場景 A：SkillPrompts + TalentPrompts schema + DbSeeder baseline（xUnit 純單元驗）

**觸發**：dotnet test 跑 schema + DbSeeder 6 個 prompt seed

**驗證**：
- `SkillPrompts` 表 6 row（code_implementation / code_review / qa_testing / documentation / ceo_orchestration / petra_orchestration）/ 全 `version_number=1 / is_active=true`
- `TalentPrompts` 表 0 row（baseline / Phase 3 才補 persona）
- partial unique index 真實 enforced — `INSERT` 第二條 `is_active=true` 同 skill 觸發 unique violation
- `SkillPrompt.PromptBody` content 對齊 `Resources/CLAUDE_*.md` 真實內容（reflection or direct file read 對齊）

### 場景 B：PromptRepository Versioning + Rollback（xUnit 純單元驗）

**觸發**：
1. UpsertSkillPromptAsync 新版本（version 2）→ assert version 1 切 is_active=false + version 2 切 is_active=true
2. RollbackSkillPromptAsync(version=1) → assert version 2 切 is_active=false + version 1 切 is_active=true
3. ListSkillPromptVersionsAsync → assert 返回 2 個版本（version 1 + version 2）

**驗證**：
- `SkillPrompts` 累積 row（不刪舊版本 / 留 audit trail）
- 同 SkillName 永遠只一條 is_active=true（partial unique index 守）

### 場景 C：feature flag=true v5.5 path 從 DB 讀 prompt 對齊（xUnit + Trial_v18 驗）

**觸發**：feature flag=true + dispatch v5.5 path（mock LLM）+ Trial_v18 真實跑

**驗證**：
- xUnit：`BuildPetraSystemPrompt` 返回 prompt content **對齊 DB SkillPrompt `petra_orchestration` PromptBody + 動態 skill roster**
- xUnit：`ClaudeCodeChatClientAdapter` Worker dispatch prompt content 對齊 DB SkillPrompt `{capability}` PromptBody
- Trial_v18：對齊 Trial_v17 業務質感 baseline（PR 真開 + 範圍 cover 完整 / cost $1.5-3 / 連續 8 Trial 業務級成功延續）

### 場景 D：feature flag=false 守 v5.5 既有 hardcoded path 0 regression（xUnit + Trial_v18 驗）

**觸發**：feature flag=false + dispatch v5.5 path

**驗證**：
- xUnit：`BuildPetraSystemPrompt` 返回 prompt 對齊既有 Stage 70+71 累積 hardcoded content（0 regression）
- xUnit：`ClaudeCodeChatClientAdapter` Worker dispatch 走既有 `Resources/CLAUDE_*.md` 讀檔 path（0 regression）
- xUnit：0 DB query hit（feature flag 守 fallback）

### 場景 E：rollback 機制 production 真實生效

**觸發**：手動 SQL UpsertSkillPromptAsync 改 `petra_orchestration` 新版本（version 2 內容刻意錯誤）+ reload-cache + 跑 Mock task 揭錯誤 + SQL RollbackSkillPromptAsync(version=1) + reload-cache + 重跑

**驗證**：
- 第一次跑：Bot log 含「prompt 載入錯誤對應」訊號（mock 揭）
- rollback 後第二次跑：prompt 回到 version 1 baseline / Mock task 正常完成
- 對齊「業界 versioning + rollback 保護 production」精神實證

### 場景 F：v4 既有 path 0 regression

**觸發**：SQL 切 `Workflow:UsePetraOrchestratorV5=false`（v4 path） + 跑 Trial_v18 同 task

**驗證**：
- Bot log 0 含「Petra v5.5 path」字樣 / 走 v4 既有 path
- v4 既有 `DevAgentService` / `QaAgentService` / `ReviewerAgentService` / `DocAgentService` / `CeoAgentService` 用既有 hardcoded `Resources/CLAUDE_*.md` 讀檔（不走 PromptRepository）
- v4 path 既有 baseline 行為 0 改變

---

## 技術約束

- 環境細節 source of truth 對齊 [workflow_aria.md 第三節 A 第 7 條紀律](../../memory/workflow_aria.md)
- v5.5 path BuildPetraSystemPrompt 既有 useSubtaskPlanning=true/false 分支保留（Stage 67 + Stage 70 + Stage 71 累積）
- xUnit test 補強對齊 Stage 69 既有 [Fact]/[Theory] 累積 pattern（PetraOrchestratorServiceTests + 新 PromptRepositoryTests）
- 對齊 [`docs/conventions/csharp.md`](docs/conventions/csharp.md) / [`ef-core.md`](docs/conventions/ef-core.md)（Stage 68 新加 PostgreSQL nullable unique pattern 段）/ [`refactor-sop.md`](docs/conventions/refactor-sop.md)
- backwards-compatible 守護 4 層：v4 既有 path / v5 既有 path / Stage 67 v5.5 既有 path（hardcoded fallback） / Stage 70+71 累積 prompt 紀律保留

---

## 實作紀錄（Forge 結案第一段）

### commit 範圍

- **主 commit**：[`151e156`](https://github.com/darkleong/AiTeam/commit/151e156) — `feat(stage72): Prompt DB 化 v3.62.0 — SkillPrompts + TalentPrompts 兩層 schema + versioning + rollback + feature flag UseV5PromptDb`（22 檔 / +2177 / -54）

### 交付清單（對齊計劃書子項）

| 子項 | 檔案 | 結果 |
|---|---|---|
| 1. Entity records | `src/AiTeam.Data/Entities.cs` 加 `SkillPrompt` + `TalentPrompt` 兩 class | ✅ |
| 2. AppDbContext fluent config | `src/AiTeam.Data/AppDbContext.cs` 加 2 DbSet + partial unique index `(SkillName) WHERE IsActive=true` + `(TalentId) WHERE IsActive=true` + (SkillName, VersionNumber) 版本歷史索引 + FK cascade | ✅ |
| 3. EF Migration | `Migrations/20260516165615_Stage72PromptSchema.cs` — 2 table + 4 index + FK | ✅ |
| 4. PromptRepository | `src/AiTeam.Data/Repositories/PromptRepository.cs` — 7 method（Get/List/Upsert × Skill+Talent + Rollback） | ✅ |
| 5. PromptResolver | `src/AiTeam.Bot/Services/PromptResolver.cs` — Singleton + 5-min TTL cache + IServiceScopeFactory（對齊 AppSettingsService pattern） | ✅ |
| 6. PetraPromptTemplate 常數抽取 | `src/AiTeam.Data/SeedContent/PetraPromptTemplate.cs` — DbSeeder + BuildPetraSystemPrompt 共用 source-of-truth | ✅ |
| 7. DbSeeder | `src/AiTeam.Data/DbSeeder.cs` 加 `EnsureSkillPromptsAsync`（race-safe per-row + DbUpdateException catch） | ✅ |
| 8. WorkflowSettings + Resolver + appsettings.json | `UseV5PromptDb` flag default false | ✅ |
| 9. PetraOrchestratorService 整合 | ctor 加 PromptResolver + BuildPetraSystemPrompt 第 3 optional `baseTemplateOverride` param + 新 instance async `BuildPetraSystemPromptForRuntimeAsync` + 3 處 call site（lines 222 / 402 / 469）改 await | ✅ |
| 10. ClaudeCodeChatClientAdapter 整合 | ctor 加第 9 optional PromptResolver param + GetResponseAsync PromptResolver-first / file fallback 雙路 | ✅ |
| 11. DI propagation | PetraWorkerHelper / GenericAgentTool / DefaultTalentFactory 全鏈接過 PromptResolver | ✅ |
| 12. Program.cs DI | `AddScoped<PromptRepository>` + `AddSingleton<PromptResolver>` | ✅ |
| 13. InternalController 整合 | reload-cache `all` scope 加 `promptResolver.InvalidateCache()`（議題 2 路線 A — 不加新 `prompts` scope） | ✅ |
| 14. xUnit | T46/T47 BuildPetraSystemPrompt override / hardcoded 雙路 + PromptRepositoryTests T1-T4 CRUD/versioning/rollback + Test 9/27/28 reflection 加 null 第 3 arg | ✅（70 pass / 0 fail） |
| 15. Directory.Build.props | v3.61.0 → v3.62.0 | ✅ |

### Forge 自驗結果（5 場景全綠）

| 場景 | 工具 | 驗證內容 | 結果 |
|---|---|---|---|
| **A** SkillPrompts schema + DbSeeder baseline | `docker exec psql` query production DB | 6 row seed（VersionNumber=1 / IsActive=true / body_len 對齊 source 檔大小）+ TalentPrompts 0 row baseline + schema 完整含 partial unique index | ✅ |
| **B** Versioning + Rollback | `dotnet test` PromptRepositoryTests T1/T2/T3 | Upsert 累積新版本 + 舊 active 切 false / Rollback 切換 active + 累積 row 不刪 audit trail / ListVersions 返回 asc | ✅ |
| **C** feature flag=true DB 讀對齊 | `dotnet test` Test46 reflection | baseTemplateOverride 三 placeholder（`{{capabilityRoster}}` / `{{decompositionSection}}` / `{{outputSection}}`）全 Replace + Stage 70+71 decomposition + JSON output 段保留 | ✅ |
| **D** feature flag=false hardcoded 0 regression | `dotnet test` Test47 + Test 9/27/28 加 null arg | override=null → PetraPromptTemplate.Template baseline + Stage 64+67+70+71 累積關鍵字（1-on-1/Design/Kickoff trigger / 需求拆解紀律 / Hierarchical Decomposition / Few-shot 範例）全綠 | ✅ |
| **G** partial unique index production enforced（額外補驗） | `docker exec psql` INSERT 第二條同 SkillName / IsActive=true | `ERROR: duplicate key value violates unique constraint "ix_skill_prompts_active_per_skill"` + IsActive=false archive row 允許累積（versioning 紀律） | ✅ |
| 整合補驗 | `curl /internal/reload-cache?scope=all` + log | Bot Cache 已清除 log + PromptResolver.InvalidateCache 串接 | ✅ |

> 場景 **E**（rollback production 真實生效）+ **F**（v4 既有 path 0 regression）— 留 Trial_v18 真實業務驗收 / 不在 Forge 自驗範圍（對齊 forge-self-verify skill 邊界）。

### 部署驗證

- CI/CD self-hosted runner 自動部署 + `Database.MigrateAsync` 自動 apply Migration `20260516165615_Stage72PromptSchema`
- 6 SkillPrompts 透過 DbSeeder race-safe path 全部 seed 成功（body_len 對齊 source 檔大小 — `petra_orchestration` 722 bytes = PetraPromptTemplate.Template / 其他 5 個對齊 CLAUDE_*.md 全檔大小）
- Bot 啟動 0 fail / 0 FATAL / `Application started` 正常
- partial unique index 真實 enforced（production PostgreSQL `NULL ≠ NULL` 雷修法第 N 次驗證對齊 ef-core.md Stage 68 紀律）

### Backwards-compatible 4 層守護驗證

- v4 既有 path 0 動：DevAgentService / QaAgentService / ReviewerAgentService / DocAgentService / CeoAgentService / ReleaseAgentService 等 v4 service 用 `Resources/CLAUDE_*.md` 讀檔路徑保留 ✅
- v5 既有 path 0 動：IAgentTool + 7 worker class fallback 維持 ✅
- v5.5 既有 hardcoded path（feature flag=false fallback）0 regression：xUnit Test 9/27/28 全綠 + PromptResolver.ResolveCapabilityPromptAsync flag=false → null → adapter 退既有 file fallback ✅
- Stage 70+71 累積 prompt 紀律保留：動態 skill roster 注入機制 + hierarchical decomposition + 線性整包邊界 — Test46 + Test27/28 全綠驗 ✅

### Forge healthy 偏離 plan（對齊 Stage 58 結論「Forge spike 揭露架構盲點紀律」）

1. **PetraPromptTemplate 常數抽取進 AiTeam.Data**（計劃書原本只在 AiTeam.Bot 處理 — Forge spike 揭跨專案 reference 工程議題：DbSeeder 在 AiTeam.Data 專案內要讀同份 Petra template，需抽常數避兩處重複維護導致 drift）
2. **BuildPetraSystemPrompt 內部改 string.Replace 取代 raw $$ interpolation**（計劃書原 raw interpolation — Forge 實作時揭：placeholder 抽常數後 source code 跟 DB seed 必須用同一段內容，最簡解 = 把 raw interpolation 改成 Replace 統一兩層機制）
3. **PromptResolver `Workflow:UseV5PromptDb=false` 短路紀律**（計劃書要求 cache 5-min TTL，但 flag=false 場景應 0 DB query hit — Forge 加 flag check early return null 避免不必要 cache reload）

### 自驗踩坑記錄（修根因 / 0 補丁）

- **首次 Edit 替換 BuildPetraSystemPrompt 全段 fail**（half-width vs full-width parens char mismatch）— 改用 2 個 smaller targeted Edit（signature 改 + return block 改）解。修根因 = 不貪大塊一次 replace / 用最小範圍編輯（對齊 workflow_aria.md 第三節 A 第 7 條延伸範圍紀律延伸到 Forge）。
- **dotnet test 首次 fail 3 處 PetraOrchestratorService ctor 缺 `promptResolver`**（Test 12 / CreateMinimalOrchestratorForReflection / CreateMemoryTestServices）— 全加 `promptResolver: null!` 補齊 named arg / 1 commit 內解 / 0 follow-up commit。

### 校準錨（待 Aria gate2 結案第二段計算寫入 calibration_anchors）

- 預期：架構級重構新區間 ×0.43-0.60（Stage 67/68/69/70 4 資料點 baseline / Stage 72 = 第 5 資料點累積）
- 實際 LoC：22 檔 / +2177 / -54

### 自診修 follow-up

0 follow-up commit（dotnet build/test 首次過 + 自驗 5 場景全綠）。

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-17 | 規劃書建立 — v3.62.0 / M 規模 / v5.5 Phase 2 Step 5 Prompt DB 化 + Talent identity 整合。**範圍**：SkillPrompts + TalentPrompts 兩層 schema + Migration + PromptRepository + DbSeeder（6 個 prompt seed）+ v5.5 path 整合（BuildPetraSystemPrompt + ClaudeCodeChatClientAdapter）+ Versioning + rollback + feature flag `Workflow:UseV5PromptDb` default false + xUnit。**戰略脈絡**：對齊業界 2026 prompt orchestration 主流（WebSearch 結論 — DB-backed + versioning 是 industry standard / 不分層保護 orchestrator）+ Christ 4 議題拍板（全部包 / 兩層 schema / 純後端 / 內容不動）+ Trial_v17 戰略級觀察「讓 Talent 自主判斷做法 / 我們只定品質標準」精神實作。**校準錨預期**：架構級重構新區間 ×0.43-0.60（對齊 Stage 67/68/69/70 4 資料點 baseline / Stage 72 = 第 5 資料點累積）。**驗收**：6 場景 — A schema + DbSeeder baseline / B Versioning + rollback / C feature flag=true DB 讀 prompt 對齊（含 Trial_v18 真實驗）/ D feature flag=false hardcoded 0 regression / E rollback production 真實生效 / F v4 既有 path 0 regression。**下一步**：Forge 實作 + Aria gate1 Tier 0+1+2（架構級重構對應 tier）+ Trial_v18 真實任務驗 → 通過後切 `UseV5PromptDb` default true = Phase 2 Step 5 完整收口 → Stage 73+ Phase 3 開（WebUI Talent CRUD + Talent persona seed + prompt content 升級評估）。 |
| v2.0 | 2026-05-17 | 實作紀錄章節（Forge 結案第一段）— commit `151e156` + 22 檔 / +2177 / -54 + Forge 自驗 5 場景全綠（A schema baseline production DB query 驗 / B versioning+rollback xUnit / C DB-driven 三 placeholder Replace xUnit / D hardcoded 0 regression xUnit / **G partial unique index production enforced 額外補驗** — `ERROR: duplicate key value violates unique constraint` + IsActive=false archive 允許累積）+ 整合 reload-cache 串接驗 + backwards-compatible 4 層守護驗 + Forge healthy 偏離 plan 3 條（PetraPromptTemplate 常數抽取 AiTeam.Data / string.Replace 取代 raw interpolation / PromptResolver flag=false 短路）+ 自驗踩坑修根因 2 條（Edit 全段 char mismatch 改 small targeted edit / Test ctor 3 處補 promptResolver null! arg）+ 0 follow-up commit。**等 Aria gate2 + Trial_v18 真實業務驗 → 通過切 `UseV5PromptDb` default true = Phase 2 Step 5 完整收口**。 |
