# Stage 67 Roadmap — v5.5 Phase 1 Step 2：Talent-Skill separation 重構基底

> 目標版本：**v3.57.0**（minor — v5.5 升級首發 / 架構級重構 / 6 Talent + 6 Skill baseline 落地）
> 狀態：✅ 已完成（2026-05-15）
> 文件版本：v2.0
> 範圍：Skill registry + Talent registry DB schema + 多對多 assignment + GenericAgentService 收斂 7 worker + Petra dispatch 改用 Talent pool + Migration 既有 7 worker → 6 預設 Talent + Feature flag 守 v5 既有 path
> 規模：L
> 對應 v5.5 規劃：[Future_Feature_v5.5.md](Future_Feature_v5.5.md) Phase 1 Step 2

---

## 戰略脈絡

**v5 動態架構 2026-05-14 正式上線後第一個架構級升級 — Talent-Skill separation 重構基底**：

- v5.5 升級候選 v5 PoC 簡化部分補回來 + 對齊業界 2026 OneManCompany / Talent-Skill 主流模式
- Christ 2026-05-15 拍板 5 條決議（CLAUDE_X.md archive / Talent per-Project 隔離 / Phase 1 範圍守緊 / feature flag 回滾 / Trial_v13 驗）
- Phase 1 Step 1 Baseline 已拍板（Final 6 Skill + 預設 6 Talent + 砍/合併 4 Agent — 見 v5.5 文件第六節）

**計劃前置 WebSearch 結論**（對齊 workflow_aria.md 第三節 A 第 9 條紀律）：
- ✅ **Microsoft Agent Framework `AddAIAgent` 內建 factory + key pattern**（`(sp, key) => agent`）直接支援 multi-Talent multi-registration
- ✅ **.NET 8+ Keyed Services GA**（`AddKeyedScoped` / `[FromKeyedServices]`）對齊 AiTeam 既有 .NET stack
- ⚠️ **Multi-registration 進階文件不完整** — 業界 sample 偏 single-key-per-agent / Forge plan 階段必 spike 確認真實 wire（對齊 Stage 64-66 framework 結論誤判教訓）
- ⚠️ **Captive dependency 雷** — Petra orchestrator (Singleton) 內 resolve scoped Talent 必走 `IServiceScopeFactory` pattern

**範圍邊界刻意收緊**（Christ 2026-05-15 拍板決議 3「只做基底」）：
- ✅ 做：Skill registry / Talent DB schema / 多對多 assignment / GenericAgentService 收斂 7 worker / Petra dispatch 改寫 / Migration 7 worker → 6 預設 Talent / archive 砍掉的 4 .md / feature flag 回滾
- ❌ 不做：WebUI Talent CRUD（留 Phase 3 Step 6）/ Prompt DB 化（留 Phase 2 Step 5）/ DB 持久記憶（留 Phase 2 Step 3）/ Petra 拆解指令（留 Phase 2 Step 4）/ 動態 Talent 加（Phase 3 開放後 Christ 才加）

**對 Phase 1 Step 2 完成後預期效果**：
- 既有 7 worker 收斂成 6 預設 Talent + 6 Skill（從 10 Agent + 7 capability 砍到）
- Petra dispatch 看 Skill → 找 Talent pool（baseline 1 instance / 預備 future horizontal scaling）
- v5 既有 path 仍可走（feature flag default false）— 0 production regression
- Trial_v13 驗 baseline 業務級成功（沿用 Trial_v6-v12 同 prompt + 9-step 模板）

---

## 子項清單

### 1. Skill registry 建立（既有 capability 抽象成 Skill）

**修法位置**：新檔 + 既有 `[AgentCapability]` attribute 演進

- 新建 [src/AiTeam.Bot/Orchestration/Petra/Skills/ISkillRegistry.cs](src/AiTeam.Bot/Orchestration/Petra/Skills/ISkillRegistry.cs) — 介面 + 預設實作
- 新建 [src/AiTeam.Bot/Orchestration/Petra/Skills/SkillDescriptor.cs](src/AiTeam.Bot/Orchestration/Petra/Skills/SkillDescriptor.cs) — record（Name / DisplayName / Description / DispatchTarget IClaudeCodeService method）
- 6 Final Skill code-defined：`code_implementation` / `code_review` / `qa_testing` / `documentation` / `ui_design` / `release_publishing`
- 砍 `requirements_extraction`（合進 Petra orchestrator system prompt — Petra prompt 補拆需求紀律）

**設計決策（Forge spike）**：
- Skill 用 `[Skill("code_implementation", DispatchTarget = ...)]` attribute pattern（對齊既有 `[AgentCapability]` 演進）
- 還是 `ISkillRegistry` 純 DI 註冊 + 抽 SkillDescriptor record
- Forge plan 階段拍

### 2. Talent registry DB schema + Migration

**修法位置**：新增 EF Core entity + Migration

- 新建 entity `AiTeam.Data/Entities/Talent.cs` — 欄位：`Id` / `Name`（Cody / Vera / Quinn / Sage / Petra / Victoria）/ `DisplayName` / `Description` / `Provider` / `Model` / `ProjectId`（**nullable** — 對齊 Christ 決議 2「per-Project 隔離 / null = 全域共用」）/ `IsActive` / `CreatedAt` / `UpdatedAt`
- 新建 entity `AiTeam.Data/Entities/TalentSkill.cs` — 多對多 join：`Id` / `TalentId` / `SkillName`（FK 到 Skill registry by name）/ `IsPrimary`（主 vs 兼）/ `Priority`（同 Talent 多 Skill 排序）
- 新建 EF Migration `Stage67TalentSkillSeparation` — 建 `talents` 表 + `talent_skills` 表 + index
- 對齊既有 `app_settings` / `agent_configs` / `petra_sessions` snake_case 表 + PascalCase quote 欄位紀律

**Migration script seed 預設 6 Talent + Skill assignment**（對齊 Phase 1 Step 1 Baseline 拍板）：
- Victoria（orchestrator role / 0 Skill assignment）
- Petra（orchestrator role / 0 Skill assignment）
- Cody（code_implementation 主 / ui_design + release_publishing 兼）
- Vera（code_review 主）
- Quinn（qa_testing 主）
- Sage（documentation 主）

### 3. GenericAgentService 收斂既有 7 worker class

**修法位置**：新檔取代 7 個 worker 獨立 class

**現狀**：[src/AiTeam.Bot/Orchestration/Petra/IAgentTool.cs:14](src/AiTeam.Bot/Orchestration/Petra/IAgentTool.cs#L14) interface + 7 實作 class（Cody/Vera/Quinn/Sage/Rosa/Demi/Release 等）

**改法**：
- 新建 [src/AiTeam.Bot/Orchestration/Petra/GenericAgentTool.cs](src/AiTeam.Bot/Orchestration/Petra/GenericAgentTool.cs) — 實作 `IAgentTool` + ctor 注入 `Talent` entity（從 DB 取） + 動態建 `ClaudeCodeChatClientAdapter` 對應 Skill 的 dispatch target
- DI 註冊改用 `AddAIAgent(key, factory)` factory pattern + 對應 Talent.Name as key（對齊 WebSearch 揭 Microsoft Agent Framework 內建支援）
- Petra orchestrator 改用 `IServiceScopeFactory` pattern resolve per-Talent IAIAgent（避 captive dependency 雷）
- 既有 7 worker 獨立 class **archive**（搬 `src/AiTeam.Bot/Orchestration/Petra/archive/` — 不被 DI scan）

**Forge spike 必驗**（對齊 WebSearch 揭 multi-registration 文件不完整風險）：
- `AddAIAgent` factory + key 真實 API 簽名 + 對 `IChatClient` adapter 是否能正常 register
- `IServiceProvider.GetKeyedService<IAIAgent>(talentName)` 動態查 — Petra dispatch 時 resolve

### 4. Petra dispatch 改用 Talent pool

**修法位置**：[src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs:241](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L241) `DispatchWorkersAsync` method

**改法**：
- `picks` 從 `List<IAgentTool>` 改成 `List<TalentDispatchPlan>`（含 SkillName + 候選 Talent pool）
- 看 Skill 找 Talent pool（baseline 1 instance / future horizontal scaling 多 instance）
- 多 Talent 時 round-robin 選一個（baseline 1 instance 無感 / future Cody-2 加進來自然分流）
- DecideAsync 改成回 `List<SkillName>`（既有 capability 序列字串對齊新 Skill 名稱不變）+ 內部 lookup 對應 Talent pool

**Forge spike 必驗**：
- BuildNextWorkerInput / BuildToolMessage 既有 helper 對齊新 TalentDispatchPlan 結構
- xUnit Test 12+13 既有 chain pass-through case 改寫對齊新結構

### 5. CLAUDE_X.md 砍掉的 4 份 archive

**修法位置**：`src/AiTeam.Bot/Resources/`

- 砍 4 份對應 archive 移到 `src/AiTeam.Bot/Resources/archive/`：
  - `CLAUDE_Rosa.md` → `archive/CLAUDE_Rosa.md`
  - `CLAUDE_Demi.md` → `archive/CLAUDE_Demi.md`
  - `CLAUDE_Release.md`（如有）→ `archive/CLAUDE_Release.md`
  - `CLAUDE_Maya.md`（如有）→ `archive/CLAUDE_Maya.md`
- ClaudeCodeChatClientAdapter 載入 path 不變 — `archive/` 不被掃
- Phase 2 Step 5 prompt DB 化時整套（含 archive）統一進 DB
- 對齊 Christ 決議 1 拍板「B archive」

**新增 Cody mode-based prompt 段**（既有 [src/AiTeam.Bot/Resources/CLAUDE_Cody.md](src/AiTeam.Bot/Resources/CLAUDE_Cody.md)）：
- Cody 兼 ui_design + release_publishing → CLAUDE_Cody.md 加新段「兼任職務紀律」（接到 ui_design Skill dispatch 時看 archive/CLAUDE_Demi.md 對齊精神 / release_publishing 同理）
- 不重複 archive 內容 — 引用即可
- Phase 2 Step 5 prompt DB 化後 mode-based 改成 DB-driven Skill prompt 變體

### 6. Petra prompt 補拆需求紀律

**修法位置**：[src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs:494](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L494) `BuildPetraSystemPrompt` method

- 加新段「需求拆解紀律」對應砍掉的 `requirements_extraction` skill
- 紀律：Petra 接到任務後先拆需求（用內部 reasoning 不 dispatch worker）+ 再決 Skill 序列
- 範例：「打磨 Dashboard 錯誤處理體驗」→ Petra 拆「跨 5 範圍 + 中等改動」→ 命中 Design trigger `code_implementation|code_review`

### 7. Feature flag `Workflow:UseTalentSkillSeparation` default false

**修法位置**：[src/AiTeam.Bot/Configuration/WorkflowSettings.cs](src/AiTeam.Bot/Configuration/WorkflowSettings.cs)

- 新增 flag `UseTalentSkillSeparation` default `false`
- v5 既有 path（IAgentTool + 7 worker class）保留作為 fallback
- 對齊 Christ 決議 4「A 加新 flag」拍板 + 既有 v4/v5 切換 pattern
- Trial_v13 驗 + Christ 拍板才切 default true

### 8. xUnit test 補強

**修法位置**：[src/AiTeam.Bot.Tests/Orchestration/PetraOrchestratorServiceTests.cs](src/AiTeam.Bot.Tests/Orchestration/PetraOrchestratorServiceTests.cs)

- 既有 28 case（Stage 66 升級到 28）改寫對齊新 Talent-Skill structure
- 新增 case：
  - `Test 14`：Skill registry 6 Skill 完整載入
  - `Test 15`：Talent pool 找 Talent dispatch（baseline 1 instance）
  - `Test 16`：Petra DecideAsync 回 Skill 序列 lookup Talent 對齊
  - `Test 17`：Feature flag default false 守 v5 既有 path 0 regression
- 預估 baseline 28 → 32+ case PASS

---

## 設計決策

1. **Skill 是 code-defined**（Christ 拍板）— 不開放動態加 Skill / 新 Skill 走 Stage（避「Agent role explosion」反模式 + 業界 6% Copilot pilot 雷區）
2. **Talent 是 DB-driven baseline 6 instance**（Christ 拍板）— Phase 3 才開放 WebUI 動態 CRUD
3. **per-Project 隔離 ProjectId nullable**（Christ 決議 2 拍板）— null = 全域共用 / 對齊未來客戶專案場景
4. **Phase 1 範圍守緊**（Christ 決議 3 拍板）— 不開 WebUI CRUD / 不含 prompt DB 化 / 不含持久記憶 / 不含拆解
5. **Feature flag 回滾**（Christ 決議 4 拍板）— v5 既有 path 留 fallback / Trial_v13 驗 + Christ 拍板才切 default
6. **多對多 mapping table**（不用 JSON column）— 對齊 EF Core query + index 性能
7. **CLAUDE_X.md 4 份 archive**（Christ 決議 1 拍板）— 不砍不吸收 / Phase 2 Step 5 統一進 DB
8. **AddAIAgent factory + Keyed Services pattern**（WebSearch 揭 Microsoft Agent Framework 內建支援）— 對齊框架 best practice
9. **IServiceScopeFactory pattern**（避 captive dependency 雷）— Petra (Singleton) 內 resolve scoped Talent

---

## 驗收情境

> 對齊 workflow_aria.md 第三節 A 計劃書格式硬規則：本節獨立列驗收場景 + 每個都具體到「怎麼觸發 + 怎麼驗證」。

### 場景 A：Skill registry 6 Skill 完整載入（基底驗）

**觸發**：Bot 啟動

**驗證**：
- `dotnet test --filter Test14` PASS
- Bot log 啟動段含 `Skill registry loaded: 6 skills (code_implementation, code_review, qa_testing, documentation, ui_design, release_publishing)`
- `requirements_extraction` 不在 Skill 列表（已合進 Petra orchestrator）

### 場景 B：Talent registry Migration 6 預設 Talent 落地（DB 驗）

**觸發**：`dotnet ef database update` Stage67 Migration 跑完

**驗證**：
- SQL `SELECT "Name", "Provider", "Model", "ProjectId", "IsActive" FROM talents ORDER BY "Name"` 預設 6 row（Cody / Petra / Quinn / Sage / Vera / Victoria — 全 ProjectId=null 全域共用 / 全 IsActive=true）
- SQL `SELECT t."Name" AS talent, ts."SkillName", ts."IsPrimary" FROM talent_skills ts JOIN talents t ON ts."TalentId" = t."Id" ORDER BY t."Name"` 含：
  - Cody: code_implementation (Primary) + ui_design + release_publishing
  - Vera: code_review (Primary)
  - Quinn: qa_testing (Primary)
  - Sage: documentation (Primary)
  - Victoria + Petra: 0 row（orchestrator role）

### 場景 C：Petra dispatch 看 Skill 找 Talent（核心驗）

**觸發**：feature flag `UseTalentSkillSeparation=true` + 送 Trial_v6-v12 同 prompt（Dashboard 錯誤處理打磨）

**驗證**：
- Bot log `Petra DecideAsync 完成 — raw=「code_implementation|code_review」picks=Cody → Vera`
- Bot log `PetraOrchestrator dispatch 1/2 talent=Cody skill=code_implementation`（vs Stage 66 既有 `worker=Cody capability=code_implementation`）
- Bot log `PetraOrchestrator dispatch 2/2 talent=Vera skill=code_review inputMsgs=2`
- PR 真開 + 對齊 Trial_v12 級業務品質

### 場景 D：Feature flag default false 守 v5 既有 path 0 regression（保護驗）

**觸發**：feature flag `UseTalentSkillSeparation=false`（default）+ 送同 prompt

**驗證**：
- Bot log 走 v5 既有 path（[src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs:241](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L241) `DispatchWorkersAsync` 既有 `IAgentTool` path）
- 0 含 `talent=` 字樣 / 仍 `worker=Cody capability=code_implementation`
- v5 既有 7 worker class 仍可被 DI scan（archive 後保留 fallback）
- PR 真開 + 對齊 Trial_v12 級業務品質

### 場景 E：Talent pool 多 instance schema 預備（future horizontal scaling 驗）

**觸發**：Mock 測試手動 SQL `INSERT INTO talents (..., "Name", ...) VALUES (..., 'Cody-2', ...)` + `INSERT INTO talent_skills VALUES (..., Cody-2 id, 'code_implementation', false, 100)`

**驗證**：
- Petra dispatch code_implementation Skill 時 pool 含 2 個 Talent（Cody-1 + Cody-2）
- Round-robin 選一個（baseline 簡單實作 / 不要 fancy load balancing）
- xUnit Test 15 cover

### 場景 F：CLAUDE_X.md archive 後 ClaudeCodeChatClientAdapter 載入仍正常

**觸發**：Bot 啟動 + 送任務

**驗證**：
- Bot log 含 `CLAUDE template 載入 worker=Cody template=CLAUDE_Cody.md len=...`（既有 5 份仍載入）
- Bot log 0 含 `CLAUDE_Rosa.md` / `CLAUDE_Demi.md` 載入（archive 不被掃）
- workspace `src/AiTeam.Bot/Resources/archive/` 4 份檔存在但不被 production load

### 場景 G：v4 既有 production path 0 regression（守護驗）

**觸發**：v4 mock 場景跑既有完整 path（kickoff / design / dev_plan / implementation / appeal / qa / 5 routing HITL）

**驗證**：
- `Workflow:UsePetraOrchestratorV5=false`（v4 path）+ `Workflow:UseTalentSkillSeparation=false` 走 v4 既有 168+ test 全 PASS
- v4 既有 7 worker AgentService 全套 path 0 regression

---

## 技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第三節 A 第 7 條紀律（port 5052 + X-Api-Key + container 名 `aiteam-aiteam-bot-1` + Petra session 表 snake_case PascalCase quote）
- **Stage 67 在 main branch 直接做**（對齊 Stage 65/66 模式）
- feature flag `Workflow:UseTalentSkillSeparation` default false 維持（Trial_v13 ✅ 後 Christ 拍板才切 default true）
- 對齊 workflow_aria.md 第三節 A 第 5+6 條紀律（不寫整段 code 範例 + 大檔 reference 標精準 line + 簽名）
- 對齊 workflow_aria.md 第三節 A 第 7 條延伸範圍段 #3+#4（Aria reference 既有 method / framework version 必 grep 真實 + 既有 caller positional args 順序）
- 對齊計劃前置 WebSearch 紀律（第 9 條）— 已執行 Microsoft Agent Framework dynamic registration / Keyed Services / DI multi-registration 三方向 finding 進戰略脈絡段
- Migration 紀律：`dotnet ef migrations add Stage67TalentSkillSeparation --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext`

---

## 實作紀錄（Forge 2026-05-15）

### 對應 commit

| commit | 性質 | 內容 |
|---|---|---|
| `58ed302` | feat 主實作 | Stage 67 8 子項完整實作 + 1871 insertions / -27 deletions（21 files / 5 新檔 / 2 git mv archive/）|
| `6fd9472` | fix Forge 自驗 follow-up | DbSeeder race condition + PostgreSQL NULL ≠ NULL unique 雷三層修根因 / 1099 insertions / -13 deletions（含 Migration `Stage67FixTalentPartialUniqueIndex`）|

### Plan Mode Spike 揭 4 議題 — Aria 二檢 endorse 拍板結論

對齊 workflow_aria.md healthy 偏離 plan pattern 紀律 + 計劃前置 WebSearch 紀律。Forge spike Microsoft Agent Framework 真實 API + 既有 codebase 揭 4 點 Roadmap v1.0 預設前提錯誤：

| 議題 | Roadmap v1.0 預設 | Aria 2026-05-15 endorse 結論 |
|---|---|---|
| 1. Petra lifecycle | Singleton + IServiceScopeFactory 必走 | **Scoped**（[Program.cs:98](src/AiTeam.Bot/Program.cs#L98)）/ 直接 `IEnumerable` inject / 設計決策 9 + 戰略脈絡「captive dependency 雷」段刪除 |
| 2. xUnit baseline | 28 → 32+ case | 真實 **13 case**（Stage 66 升 13 / Roadmap 28 誤 propagate）→ 校正 13 → 17+ |
| 3 ⭐ Multi-agent register pattern | AddAIAgent factory + Keyed Services（設計決策 8）| **路線 B 保留 IAgentTool 演進為 ITalent**（framework AddAIAgent 對「1 agent 1 capability」設計 / AiTeam Talent 兼多 Skill 超出 framework 預設範圍 / 0 新 NuGet / xUnit Mock 0 重做 / 對齊「修根因 > 補丁」+「production-ready 漸進 path 優先」）|
| 4 ⭐ 7 worker class 處理 | archive 7 worker class | **不 archive 保留 v5 既有 fallback path**（守 Christ 決議 4 fallback 紀律 + 對齊驗收場景 G）/ 只 archive 2 CLAUDE_X.md（Rosa/Demi 真實存在 / Release/Maya 不存在不需搬）|

### 8 子項實作完成項目

1. **Skill registry 建立** — 3 新檔（[SkillDescriptor.cs](src/AiTeam.Bot/Orchestration/Petra/Skills/SkillDescriptor.cs) + [ISkillRegistry.cs](src/AiTeam.Bot/Orchestration/Petra/Skills/ISkillRegistry.cs) 含 `DefaultSkillRegistry` 實作）— 6 Skill code-defined：`code_implementation` / `code_review` / `qa_testing` / `documentation` / `ui_design` / `release_publishing` / 砍 `requirements_extraction` 合進 Petra orchestrator prompt（子項 6）/ DI Singleton register
2. **Talent + TalentSkill entity + Migration + DbSeeder** — [Entities.cs](src/AiTeam.Data/Entities.cs) 加 2 class（檔末追加）/ [AppDbContext.cs](src/AiTeam.Data/AppDbContext.cs) 加 2 DbSet + Fluent config（對齊既有 PetraSession pattern）/ Migration `20260515135610_Stage67TalentSkillSeparation` 建 talents + talent_skills 表 + 2 unique index / [DbSeeder.cs](src/AiTeam.Data/DbSeeder.cs) 加 `EnsureTalentsAsync()` seed 6 Talent + 6 TalentSkill（race-safe v2.0 修法見「驗收後修正」段）
3. **ITalent interface + GenericAgentTool + ITalentFactory** — 3 新檔（演進自 IAgentTool / `Skills` rename from `Capabilities` / `CreateAgent(ctx, skill)` 加 skill 動態傳解 Talent 兼多 Skill）/ **ITalentFactory 取代 plan 早期 `IEnumerable<ITalent>` DI scan**（Forge 實作時 spike — DI service collection register 必須在 `app.Build()` 之前完成 / DB migrate 在 `app.Build()` 之後跑 / 矛盾 → 改用 runtime factory pattern 副作用解 Phase 3 dynamic CRUD 自然）
4. **PetraOrchestratorService dispatch 改用 Talent pool** — ctor 加注入 `ITalentFactory` + `WorkflowSettingsResolver` / `StartAsync` 內 runtime flag 分支 v5 既有 path / v5.5 path / 新 method `DecideTalentsAsync` + `DispatchTalentsAsync` + `FindTalentForSkill`（round-robin 簡單實作 baseline 1 instance + future horizontal scaling）/ `FinalizeGitAsync` + `BuildPrBody` `picks` 抽 `dispatchNames` string list 統一 v5 / v5.5 兩 path
5. **archive 2 CLAUDE_X.md** — `git mv` Resources/CLAUDE_Rosa.md / CLAUDE_Demi.md → Resources/archive/（CLAUDE_Release.md / CLAUDE_Maya.md 既有不存在不需搬）/ `.csproj` `<Content Include="Resources\CLAUDE_*.md">` glob 只掃當層 / `CLAUDE_Cody.md` 加「兼任職務紀律」段引用 archive/CLAUDE_Demi.md 精神 + release_publishing 從零自定義紀律（archive/CLAUDE_Release.md 不存在）
6. **Petra prompt 補需求拆解紀律段** — [PetraOrchestratorService.cs:494](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L494) `BuildPetraSystemPrompt` 加「【需求拆解紀律】」段（trigger / 判準 / 範例 / 紀律 四段式對齊既有 prompt 風格 — Aria nice-to-have 提醒）
7. **Feature flag `Workflow:UseTalentSkillSeparation`** — [WorkflowSettings.cs](src/AiTeam.Bot/Configuration/WorkflowSettings.cs) 加 property default false / [WorkflowSettingsResolver.cs](src/AiTeam.Bot/Configuration/WorkflowSettingsResolver.cs) 加 `GetUseTalentSkillSeparationAsync()` / `appsettings.json` WorkflowSettings 加 entry
8. **xUnit test 13 → 17 case** — 4 新 Test14-17：① DefaultSkillRegistry 6 Skill 完整載入 + 0 含 requirements_extraction ② Talent pool round-robin（Cody / Cody-2 多 instance）③ Petra DecideAsync lookup Talent 對齊 ④ Feature flag default false 守 v5 既有 path / Test 12 ctor 加 ITalentFactory + WorkflowSettingsResolver null! 兩參數 / 加 `FakeTalent` helper + `CreateMinimalOrchestratorForReflection` + `InvokeFindTalentForSkill` reflection helper

### 關鍵設計決策

1. **ITalentFactory pattern 取代 plan 早期 DI scan**（Forge 實作時新 spike）— 解 app.Build 時序問題 + 副作用 Phase 3 dynamic CRUD 自然解
2. **保留 v5 既有 7 worker class + IAgentTool 不 archive**（Aria 議題 4 endorse 對齊 Christ 決議 4 fallback 紀律）— DI 永遠 register 兩條 path / runtime PetraOrchestratorService 看 DB flag 切換 dispatch 哪條 path
3. **CLAUDE_Cody.md 兼任職務紀律段**「ui_design 引用 archive/CLAUDE_Demi.md / release_publishing 從零自定義紀律」（Aria nice-to-have #2 修正 — archive/CLAUDE_Release.md 既有不存在）
4. **round-robin baseline 簡單實作**（pool[counter++ % pool.Count]）— 避 fancy load balancing（Roadmap 子項 4 拍板）/ session-scoped counter 對齊 PetraOrchestratorService Scoped lifecycle 無需 thread-safe
5. **partial unique index v2.0 修法**（Forge 自驗 follow-up）— 拆 2 partial unique 解 PostgreSQL `NULL ≠ NULL` 真根因（per-Project 群組 + 全域 NULL 群組獨立 enforce unique）

### 驗收後修正（Forge 自驗 follow-up commit `6fd9472`）

**Production blocker 揭真實根因鏈**（Stage 67 commit 58ed302 push 後 Bot container restart loop / exit 139）：

1. Bot + Dashboard 啟動同時跑 `DbSeeder.SeedAsync` → race condition 都看 existing=null → 都 Add → 都 SaveChanges
2. **PostgreSQL `NULL ≠ NULL` 語義**：unique index `(ProjectId, Name)` 對 ProjectId=null 不阻擋 → 兩個 race winner 都 commit 成功 → DB 每 Talent 重複 2 筆
3. 第二次啟動 `EnsureTalentsAsync.ToDictionaryAsync` 拋 `ArgumentException("Quinn key duplicate")` → Bot crash → CI/CD restart loop

**三層修根因**（對齊「修根因 > 補丁」精神）：

- **DB-level（Migration `Stage67FixTalentPartialUniqueIndex`）**：拆 2 partial unique：
  - `(ProjectId, Name) WHERE ProjectId IS NOT NULL` — per-Project Talent name 唯一
  - `(Name) WHERE ProjectId IS NULL` — 全域 Talent name 唯一（真正 enforce NULL 群組）
- **application-level**：`DbSeeder.EnsureTalentsAsync` per-Talent SaveChanges + `catch DbUpdateException` ignore race loser + `Entry.State = Detached` 還原 EF context state
- **防禦**：`ToDictionaryAsync` 改 `GroupBy.First()` dedupe — 萬一 race 漏網 / 歷史 row 也不爆

**止血**：手動 `docker exec psql DELETE FROM talents` 清重複 row（留每 Name 最早 CreatedAt 一筆）

### Mock 覆蓋情況

| 場景 | Forge 自驗 結果 | 驗證方式 |
|---|---|---|
| A. Skill registry 6 Skill 完整載入 | ✅ PASS | Test 14 PASS / 0 含 requirements_extraction |
| B. Talent registry Migration 6 Talent + 6 TalentSkill seed 落地 | ✅ PASS（production SQL 真實驗）| 6 Talent 全 ProjectId=null IsActive=true / Cody 兼 3 skill + Vera/Quinn/Sage Primary 1 skill / Victoria + Petra 0 skill |
| C. Petra dispatch 看 Skill 找 Talent（核心驗）| ⏸️ 留 Trial_v13 | 真實 LLM dispatch — 自驗範圍外 / 啟動條件達成 |
| D. Feature flag default false 守 v5 既有 path | ⏸️ 部分驗 | Test 17 PASS / Mock 真實場景留 Trial_v13 |
| E. Talent pool round-robin schema 預備 | ✅ PASS | Test 15 PASS（Cody/Cody-2/Cody 對齊）|
| F. CLAUDE_X.md archive 後 Adapter 載入仍正常 | ✅ PASS | Build output Resources/ 6 份 / archive/ 不在 build output |
| G. v4 既有 168+ test 0 regression | ✅ PASS | dotnet test 178 PASS（51 + 127）|

### 踩坑紀錄（Forge 自驗 follow-up know-how 累積）

#### 踩坑 1 ⭐：PostgreSQL `NULL ≠ NULL` unique constraint 語義

**現象**：EF Core `e.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique()` 對 `ProjectId=null` 兩筆同 Name row 不阻擋 commit。

**根因**：PostgreSQL 對 NULL 語義是「不可比較」— `NULL = NULL` 結果是 NULL（不是 true）— unique constraint 視為「不衝突」放行兩筆。

**修法**：拆 partial unique index — `(Name) WHERE ProjectId IS NULL` + `(ProjectId, Name) WHERE ProjectId IS NOT NULL`。**partial unique 對 WHERE 過濾出的群組內 NULL 也 enforce unique**。

**對齊既有 codebase pattern**：AgentConfig `(Name)` 純單欄 unique 沒踩此坑（Name 非 nullable）— Talent 引入 nullable ProjectId 是第一次踩。**未來新 entity 含 nullable 欄位 + unique constraint 時必須評估 partial index**。

#### 踩坑 2：Bot + Dashboard 並行啟動 race condition（既有但未暴露）

**現象**：Bot + Dashboard 都 register `DbSeeder.SeedAsync` call - 啟動時並行跑兩次 SeedAsync。

**既有 AgentConfig 也有相同 race pattern 但沒暴露**（Name 非 nullable / unique 阻擋 race loser SaveChanges 拋 `DbUpdateException` → 容器 restart → 重啟時 existing 已 set → skip add → 不爆）— 沉默踩坑但 process restart 一次解決。

**Talent 為什麼暴露**：partial unique 修根因前的舊 unique index 對 NULL 不阻擋 → race loser SaveChanges 也成功 → DB 真實塞重複 row → 第二次啟動 ToDictionaryAsync 才爆。

**修法**：per-Talent SaveChanges + catch DbUpdateException + Entity detach（顯式 race-safe pattern — 未來新 DbSeeder seed 路線都可對齊）。

#### 踩坑 3：Plan 早期 `IEnumerable<ITalent>` DI scan pattern 不可行（Forge 實作時新 spike）

**現象**：plan 寫「對每個 Talent `services.AddScoped<ITalent>(sp => new GenericAgentTool(talent, ...))` per Talent」— 想用既有 `IEnumerable<IAgentTool>` DI scan pattern。

**根因**：DI service collection register 必須在 `app.Build()` **之前**完成，但 `db.Database.MigrateAsync()` + `DbSeeder.SeedAsync` 在 `app.Build()` **之後**才跑（Program.cs L237-261）— 矛盾無法 from DB load Talent 來 register。

**修法**：改用 `ITalentFactory` runtime DB query pattern（Singleton service + `IServiceScopeFactory` 解 Scoped DbContext 雷）— `GetAllAsync(ct)` 每次 query DB 取最新 Talent + 即時建 GenericAgentTool list。**副作用優勢**：Phase 3 dynamic Talent CRUD 自然解（runtime 加 Talent 立刻 pickup / 不需 register hot reload）。

**對齊既有 pattern**：`AppSettingsService` / `DiscordBotService` / `InternalController` 已用 `IServiceScopeFactory` 解 Singleton 內 resolve Scoped 雷 — Stage 67 第 4 次實踐。

### 規模 + LoC 統計

| 維度 | 預估 | 真實 | 倍率 |
|---|---|---|---|
| **plan v2.1 預估 LoC** | 755-1120 | feat 1871 + fix 1099 = **2970 LoC**（含 Designer.cs auto-gen ~700 + Migration ~85 × 2）| - |
| **production code LoC**（扣除 auto-gen Migration Designer.cs ~1400 + Migration .cs ~170）| 755-1120 | feat ~1170 + fix ~50 = **~1220** | ×1.09 |
| **Aria Roadmap v1.0 預估** | mid 600-800K | ~1220 production LoC | ×1.53-2.03 |

**校準錨 ×1.53-2.03**（vs Aria mid 預估 600-800K）— 純 production code 對 plan v2.1 預估 ×1.09 對齊（Forge plan 階段已修正 Aria 預估偏低 +40% — 對齊 workflow_aria.md 自省點 #28 同類根因第 N 次累積）。

**Forge 自驗 follow-up fix LoC 比例**：~50 / ~1220 = **4.1%**（健康 — Stage 54 follow-up fix `84bd874` 比例對齊範圍）

### Trial_v13 啟動條件

- ✅ Stage 67 Mock 全綠（場景 A/B/E/F/G PASS）
- ✅ Aria gate1 Tier 0+1+2+3 通過（Plan Mode 4 議題 endorse + nice-to-have polish + 結案 Tier 0/1/2 信任 Forge 自驗）
- ✅ Forge 自驗主要場景 PASS + follow-up bug 自抓自修
- ⏸️ Trial_v13 待 Christ + 新 Aria session 觸發 — 沿用 Trial_v6-v12 同 prompt + Aria 全程自跑 9-step 模板第 4 次實踐 → 通過 → Christ 拍板切 `Workflow:UseTalentSkillSeparation` default true = v5.5 Phase 1 完成 + 進 Phase 2 Step 3（DB 持久記憶 schema 設計）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-15 | 實作紀錄章節（Forge 結案第一段）— Stage 67 8 子項完整實作 commit `58ed302` + Forge 自驗 follow-up fix commit `6fd9472`（DbSeeder race condition + PostgreSQL NULL ≠ NULL unique 雷三層修根因）。**Plan Mode Spike 揭 4 議題 Aria endorse 結論**：① Petra 是 Scoped 不是 Singleton / 設計決策 9 刪 ② xUnit baseline 真實 13 case 不是 28 / 校正 13 → 17+ ③ 路線 B 保留 IAgentTool 演進為 ITalent（framework AddAIAgent 對 Talent 兼多 Skill 場景沒明顯加分 / 0 新 NuGet / xUnit Mock 0 重做）④ 7 worker class 不 archive 保留 v5 既有 fallback / 只 archive 2 CLAUDE_X.md（Rosa/Demi 真實存在）。**ITalentFactory pattern 取代 plan 早期 IEnumerable<ITalent> DI scan**（Forge 實作時新 spike — 解 app.Build 時 DB 還沒 ready 的時序問題 + 副作用 Phase 3 dynamic CRUD 自然解 / 對齊既有 AppSettingsService / DiscordBotService / InternalController IServiceScopeFactory pattern 第 4 次實踐）。**Forge 自驗 7 場景結果**：A/B（production SQL 真實驗 6 Talent + 6 TalentSkill seed 落地）/ E（round-robin Test 15）/ F（archive build output Resources/ 6 份 + archive/ 不在 build output）/ G（178 PASS 對齊 168+ test 0 regression） ✅；C/D 留 Trial_v13。**Forge 自驗 follow-up bug 自抓自修踩坑紀錄 3 條**：① ⭐ PostgreSQL NULL ≠ NULL unique constraint 語義（拆 partial unique index 真正 enforce NULL 群組） ② Bot + Dashboard 並行啟動 race condition（既有 AgentConfig pattern 沒暴露 / Talent 因 nullable + NULL 雷暴露） ③ Plan 早期 IEnumerable<ITalent> DI scan 不可行（Forge 實作時新 spike — DI register 必須在 app.Build 之前 / DB migrate 在 之後 矛盾）。**規模統計**：feat 1871 + fix 1099 = 2970 LoC（含 Designer.cs auto-gen ~1400 + Migration ~170）/ production code ~1220 / 對 plan v2.1 預估 755-1120 ×1.09 對齊 / 對 Aria Roadmap v1.0 預估 mid 600-800K ×1.53-2.03（純 production code）/ Forge 自驗 follow-up fix LoC ~50 / ~1220 = 4.1%（健康）。**Trial_v13 啟動條件全達成**（Stage 67 Mock 全綠 + Aria gate1 通過 + Forge 自驗主要場景 PASS + follow-up bug 自抓自修）— 等 Christ + 新 Aria session 觸發。 |
| v1.0 | 2026-05-15 | 規劃書建立（Aria）— Stage 67 = v5.5 升級首發（Phase 1 Step 2 Talent-Skill separation 重構基底）。**戰略脈絡**：v5 動態架構 2026-05-14 正式上線後第一個架構級升級 / 對齊 v5.5 規劃 Phase 1 Step 2 / Christ 2026-05-15 拍板 5 條決議（CLAUDE_X.md archive / Talent per-Project 隔離 / Phase 1 範圍守緊 / feature flag 回滾 / Trial_v13 驗）/ Phase 1 Step 1 Baseline 已拍板（Final 6 Skill + 預設 6 Talent + 砍/合併 4 Agent）。**計劃前置 WebSearch 結論**：Microsoft Agent Framework `AddAIAgent` 內建 factory + key pattern 直接支援 multi-Talent multi-registration / .NET 8+ Keyed Services GA 對齊 / Multi-registration 進階文件不完整 Forge plan 階段必 spike / Captive dependency 雷必走 IServiceScopeFactory pattern。**8 子項**：① Skill registry 建立 6 Final Skill（合 requirements_extraction 進 Petra）② Talent registry DB schema + Migration（per-Project ProjectId nullable）+ seed 6 預設 Talent ③ GenericAgentService 收斂 7 worker class + AddAIAgent factory + key pattern + IServiceScopeFactory ④ Petra dispatch 改用 Talent pool（看 Skill 找 Talent / round-robin）⑤ CLAUDE_X.md 砍 4 份 archive（Rosa/Demi/Rena/Maya） + CLAUDE_Cody.md 加兼任職務紀律段 ⑥ Petra prompt 補拆需求紀律（合 requirements_extraction）⑦ Feature flag UseTalentSkillSeparation default false 守 v5 既有 path ⑧ xUnit test 28 → 32+ case。**9 設計決策** + **7 驗收場景**（A Skill registry 載入 / B Talent Migration / C Petra dispatch 核心驗 / D feature flag 守 v5 既有 path / E Talent pool 多 instance schema 預備 / F CLAUDE_X.md archive / G v4 既有 production 0 regression）。**範圍邊界刻意收緊**（Christ 決議 3）：不開 WebUI CRUD / 不含 prompt DB 化 / 不含持久記憶 / 不含拆解。**規模 L 預估 mid 600-800K**（架構級重構 + 8 子項 + 對齊 v5 PoC 二次架構升級規模）。**Trial_v13 啟動條件**：Stage 67 Mock 全綠 + Aria gate1 Tier 0+1+2 通過 + Forge 自驗 7 場景 PASS → Trial_v13 沿用 Trial_v6-v12 同 prompt + Aria 全程自跑 9-step 模板第 4 次實踐 → 通過 → Christ 拍板切 `Workflow:UseTalentSkillSeparation` default true = v5.5 Phase 1 完成 + 進 Phase 2 Step 3。 |
