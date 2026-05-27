# Stage 73 Roadmap — v5.5 Phase 3 Step 7：Prompt content 升級 + Petra TalentPrompt persona seed

> 目標版本：**v3.63.0**（minor — v5.5 Phase 3 第一步 / 一般架構級重構：6 SkillPrompt content 從「步驟紀律」升級「品質目標 + 業界 best practice」+ Petra TalentPrompt persona seed 新加）
> 狀態：✅ 已完成（2026-05-17）
> 文件版本：v2.0
> 範圍：6 SkillPrompt content 升級 + Petra TalentPrompt persona seed + PetraPromptTemplate.Template 同步 + CLAUDE_*.md source 檔同步 + xUnit baseline 對齊 + Directory.Build.props bump
> 規模：M+
> 對應 v5.5 規劃：[Future_Feature_v5.5.md](Future_Feature_v5.5.md) Phase 3 Step 7

---

## 戰略脈絡

**Stage 72 結案 + Trial_v18 🟢 全綠 + v5.5 Phase 2 完整收口後 Phase 3 Step 7 開跑** — 在 Stage 72 已建立的「兩層 prompt schema + versioning + rollback + feature flag」搬家基礎上，**升級內容**對齊「品質 > 做法」精神（自省點 #35 — Trial_v17 戰略級觀察延伸）+ 對齊業界 Orchestrator persona pattern。

### Stage 72 留給 Stage 73 的兩條軌道

| 軌道 | Stage 72 完成 | Stage 73 範圍 |
|---|---|---|
| **Skill 層（職位）** | 6 SkillPrompt row baseline seed（VersionNumber=1 / IsActive=true / content 對齊既有 CLAUDE_*.md + PetraPromptTemplate）| **content 升級為「品質目標 + 業界 best practice」VersionNumber=2 / 舊版保留 audit trail** |
| **Talent 層（個性）** | TalentPrompt 表 schema 預留 / 0 row baseline | **Petra TalentPrompt persona seed 新加 1 row**（PM 個性風格 4 拍板特質）|

### 設計精神（Christ 親口拍板）

- **「品質 > 做法」**（2026-05-16 拍板）— prompt 升級重點在「品質目標」不在「步驟紀律」。Trial_v17 觀察 Cody 已主動補測試 / 主動識別「不適用」場景 = Talent 自主判斷做法 OK，我們只定品質標準
- **「自己用爽」優先**（一貫精神）— 業界 best practice 參考不照搬，AiTeam 是 Christ 個人專屬工具
- **「持續迭代」**（2026-05-15 拍板）— prompt 內容會隨真實 Trial 觀察持續演進，Versioning + rollback 已守住 production 安全（Stage 72 機制）
- **「對等和互相」**（2026-05-16 拍板）— Aria/Forge/Cody/Vera 之間是合作關係不是命令鏈，prompt 升級時把這個精神帶進員工指引

### 業界 reference（既有 WebSearch 結論延用 / 不重複觸發）

[Future_Feature_v5.5.md 三、業界 finding](Future_Feature_v5.5.md) + [Future_Feature_v5.5.md 八、Sources](Future_Feature_v5.5.md) 已累積完整 reference：
- **Orchestrator persona 業界主流**（2026-05-17 上一輪 WebSearch — Petra TalentPrompt persona 不是 AiTeam 獨創 / 業界 70% production Orchestrator-Worker 含 persona 設定）
- **Skill prompt 品質目標導向**（OneManCompany Talent-Skill / Spring AI Skills / gstack 6+15 skills — 業界共識 skill prompt 寫「what to deliver」優於「how to do step by step」）
- **「ambiguous instructions 被不同 agent 解讀差」反模式**（業界 14 種失敗模式之一）— Stage 73 升級重點明確「品質下限」避免模糊

### 範圍邊界刻意收緊

- ✅ 做：
  - 6 個 SkillPrompt content 升級（VersionNumber=1 → 2 + Upsert 保 audit trail）
  - Petra TalentPrompt persona seed 1 row（DbSeeder + ResolveTalentPersonaAsync 整合）
  - PetraPromptTemplate.Template 對齊新版 petra_orchestration content（Stage 72 跨專案 source-of-truth 紀律延續）
  - CLAUDE_*.md source 檔同步升級（DbSeeder seed 來源 + v4 既有 path fallback 對齊）
  - DbSeeder 幂等紀律延續（同 SkillName + IsActive=true 已存在則 skip / VersionNumber=2 升級走 PromptRepository Upsert 手動觸發 path）
  - xUnit baseline 對齊新 content
  - Directory.Build.props v3.62.0 → v3.63.0
- ❌ 不做：
  - **`Workflow:UseV5PromptDb` flag 切換**（Stage 72 已 production active true / Stage 73 沿用）
  - **schema 變更**（Stage 72 兩層 schema + Migration 已落定 / Stage 73 純 content 升級）
  - **rollback UI**（Phase 3 Step 8 Stage 75 WebUI 範圍）
  - **真並行 dispatch + 3 agent debate**（Stage 74 範圍）
  - **兩層 queue 配套**（Stage 76 範圍）
  - **Trial_v18 議題 1 SystemSettings inconsistent pattern 直接修 .razor.cs code**（在 spike branch / not main / PR close 自動消失 — Stage 73 改透過 Cody Skill prompt 升級「production-grade UX consistent pattern」紀律間接守 / 未來 Cody 跑類似任務時自然對齊）

---

## 子項清單

### 1. 6 個 SkillPrompt content 升級對齊「品質 > 做法」精神

**核心紀律**（給 Forge 起草用）：
- **從「步驟紀律」降為「品質目標」**：既有 prompt 多寫「先做 1 再做 2 再做 3」/ 升級後寫「最終要交付什麼品質」+「業界 best practice」+「邊界紅線」
- **保留紅線**：禁止行為 / 安全紅線（如 Vera 唯二 Critical / Cody 不改測試檔）/ 結構性硬規則（JSON schema / 輸出格式）— 這些不是「做法」是「界面契約」保留
- **加入「對等和互相」精神**：員工是合作夥伴不是執行者 / 自主判斷 / 不確定時主動 escalate vs 猜
- **加入「production-grade 自驗精神」**：對齊 Trial_v17/v18 觀察（Cody 主動補測試 / 自驗整合 / 主動識別不適用場景）— 明文化進對應 skill prompt

**6 個 SkillPrompt 升級重點**（具體文案由 Forge 起草 + Christ Plan Mode review）：

| Skill | 升級重點方向 |
|---|---|
| `code_implementation`（Cody 主）| **品質目標**：production-grade（含 consistent pattern / UX 反應 / 邊界處理）+ **自驗紀律**（dotnet build/test + Playwright UX 自驗 + Self-check / 廣範圍對照表 — 既有紀律保留）+ **對等精神**（不確定時 escalate vs 猜）+ **production-grade UX consistent pattern**（對齊 Trial_v18 議題 1 觀察 — 同檔 N method 對齊一致錯誤狀態 reset / loading flag pattern）|
| `code_review`（Vera 主）| **品質目標**：擋住「會出事」的 critical（既有「偏好放行」精神保留）+ **唯二 Critical 紅線保留**（target=_blank rel=noopener / Server Circuit await unhandled）+ **品質目標 vs 步驟降比**（不寫「先掃 .cs 再掃 .razor」步驟）+ **對等精神**（給 Cody review 是合作不是挑刺）|
| `qa_testing`（Quinn 主）| **品質目標**：測試標的存在驗證（既有 Stage 41 教訓保留）+ unverifiable_targets escalate 紀律保留 + **品質目標**：寫的 test 真的測到關鍵 wire 不被 mock 騙過（對齊 Aria gate1 Tier 1「不被 mock 騙過」紀律）+ **production-grade 邊界**（每 public method 2+ test happy + edge）|
| `documentation`（Sage 主）| **品質目標**：歸檔內容對 Christ 未來 review 有價值（不是 boilerplate）+ 備援 fallback 紀律保留 + **對等精神**（Sage 是收尾夥伴不是模板填表機）|
| `ceo_orchestration`（Victoria 主）| **品質目標**：分類精準 / 老闆指令理解到位 + ACTION JSON 結構保留（系統解析契約）+ **對等精神**（Victoria 是技術顧問兼協調者不是純路由）|
| `petra_orchestration`（Petra 主）| **品質目標**：拆解任務「真不同 scope」紀律（Stage 71 已立 — 保留）+ 三 trigger 條件具體判斷準則保留 + **{{capabilityRoster}} / {{decompositionSection}} / {{outputSection}} placeholder 保留**（Stage 72 跨專案 source-of-truth 機制 / runtime 注入動態值）+ **對等精神**（Petra 是 PM 不是命令鏈頂端 / 對 Cody/Vera/Quinn/Sage 是派工夥伴）|

**升級執行路徑**：
- **DbSeeder 幂等紀律延續**：startup 看到 same SkillName + IsActive=true 已存在 → skip（不主動覆蓋 production active row）
- **Stage 73 升級 path**：透過 `PromptRepository.UpsertSkillPromptAsync(skillName, newBody, ct)` 走 versioning path = 新 VersionNumber=2 row + 舊 VersionNumber=1 切 IsActive=false（audit trail 保留 / rollback ready）
- **觸發時機**：Stage 73 結案 / Trial_v19 開跑前 — Forge 實作時透過 DbSeeder 內新立 `UpgradeSkillPromptsToV2Async` helper（幂等：同 SkillName VersionNumber=2 已存在則 skip）
- **CLAUDE_*.md source 檔同步升級**：v4 既有 path（DevAgentService / QaAgentService 等）仍走 file 讀路徑 fallback — source 檔升級 = v4 path 自動拿到新 content / v5.5 path 走 DB 拿到新 content / 兩 path 對齊不分裂

### 2. Petra TalentPrompt persona seed 新加

**設計**：DbSeeder 加 1 條 TalentPrompt row 對齊 Stage 72 預留 schema：
- `TalentId` = Petra Talent.Id（既有 baseline 6 Talent 之一 / EnsureTalentsAsync 先跑）
- `PersonaBody` = PM 個性風格內容（具體文案由 Forge 起草 + Christ Plan Mode review）
- `VersionNumber=1 / IsActive=true` baseline
- race-safe per-row SaveChanges + DbUpdateException catch（對齊 Stage 67 EnsureTalentsAsync + Stage 72 EnsureSkillPromptsAsync pattern）

**Petra persona 4 拍板特質**（Christ 2026-05-15+16+17 親口累積拍板 / Forge 起草時對齊）：

1. **謹慎拍板** — 不亂派工 / 拆 subtask 前先確認 scope / 同類任務不機械重複拆（Stage 71 「拆=真不同 scope」紀律延伸到 persona 層）
2. **對冗餘不容忍** — 派工避免重複 / 同 Skill 多 Talent 時 round-robin 而非全派 / 不過度規劃
3. **持續迭代** — 接收 Christ 任意時刻新需求 / 不擋 user（對齊 Stage 76 兩層 queue 配套精神鋪墊）
4. **對等和互相** — 派工是合作不是指令 / Cody/Vera/Quinn/Sage 是夥伴 / 收到 escalate / blocked 時認真理解不打回

**整合 path**（既有 Stage 72 PromptResolver.ResolveTalentPersonaAsync 已實作）：
- `BuildPetraSystemPrompt` 加組合段：base template + persona prepend（feature flag UseV5PromptDb=true + TalentPrompt 存在時才注入 / 不存在 fallback 純 base template）
- Forge 實作確認既有 PromptResolver.ResolveTalentPersonaAsync 在 Petra dispatch path 真實調用（gate1 grep 驗）

### 3. PetraPromptTemplate.Template 同步升級

對齊子項 1 `petra_orchestration` SkillPrompt 升級後 content（Stage 72 跨專案 source-of-truth 紀律延續）：
- 文件路徑 `src/AiTeam.Data/SeedContent/PetraPromptTemplate.cs`
- 含 `{{capabilityRoster}}` / `{{decompositionSection}}` / `{{outputSection}}` placeholder 保留（runtime 動態注入機制保留）
- DbSeeder `EnsureSkillPromptsAsync` `petra_orchestration` 走 `<<INLINE>>` path 取 `PetraPromptTemplate.Template`（既有 Stage 72 baseline 機制保留）

### 4. CLAUDE_*.md source 檔同步升級

5 個 CLAUDE_*.md 對齊新版 SkillPrompt content（除 Petra — Petra 走 PetraPromptTemplate 不走 .md）：

| Skill | source 檔 | 用途 |
|---|---|---|
| `code_implementation` | `Resources/CLAUDE_Cody.md` | DbSeeder seed 來源 + v4 既有 DevAgentService fallback path |
| `code_review` | `Resources/CLAUDE_Vera.md` | DbSeeder seed 來源 + v4 既有 ReviewerAgentService fallback path |
| `qa_testing` | `Resources/CLAUDE_Quinn.md` | DbSeeder seed 來源 + v4 既有 QaAgentService fallback path |
| `documentation` | `Resources/CLAUDE_Sage.md` | DbSeeder seed 來源 + v4 既有 DocAgentService fallback path |
| `ceo_orchestration` | `Resources/CLAUDE_Victoria.md` | DbSeeder seed 來源 + v4 既有 CeoAgentService fallback path |

> Petra 用 `PetraPromptTemplate.Template`（`<<INLINE>>` path）不走 `.md`，所以 CLAUDE_Petra.md 不在 source 檔升級範圍（Stage 72 議題 1 路線 A 決議延續）。

### 5. UpgradeSkillPromptsToV2Async helper（DbSeeder 內新立）

**新立** DbSeeder 內 helper（幂等 + race-safe）：
- 對 6 個 SkillName 逐個檢查 VersionNumber=2 active row 是否已存在 → 存在 skip / 不存在走 `PromptRepository.UpsertSkillPromptAsync`
- VersionNumber=2 body 來源：`code_implementation/review/qa_testing/documentation/ceo_orchestration` 從升級後 CLAUDE_*.md 讀 / `petra_orchestration` 從升級後 `PetraPromptTemplate.Template` 讀
- 對齊既有 EnsureSkillPromptsAsync / EnsureTalentsAsync race-safe pattern（per-row SaveChanges + DbUpdateException catch + Detach）
- 在 SeedAsync 主流程加 call（Talents → SkillPrompts baseline → SkillPrompts upgrade v2 → TalentPrompts Petra persona seed）

### 6. PromptResolver cache invalidate 觸發

Stage 73 升級透過 DbSeeder helper 在 Bot 啟動時自動跑 → DB content 真實改 → `PromptResolver` 5-min TTL cache 自然過期生效（Stage 72 既有機制延用）。若需要立即生效（不等 TTL）→ Forge 實作 + 結案後 Aria 一次性 `curl /internal/reload-cache?scope=all`（既有 Stage 72 PromptResolver.InvalidateCache 串接機制）。

### 7. xUnit test 補強

新 3-5 case 對齊 Stage 72 既有 PromptRepositoryTests baseline pattern：
- `UpgradeSkillPromptsToV2Async` 幂等驗（同 SkillName VersionNumber=2 已存在則 skip）
- `UpgradeSkillPromptsToV2Async` 真實升級驗（從 v1 active 升 v2 / 舊 v1 切 IsActive=false / 新 v2 IsActive=true / 累積 row 不刪 audit trail）
- Petra TalentPrompt seed 真實寫入 + PromptResolver.ResolveTalentPersonaAsync 真實返回非 null content
- BuildPetraSystemPrompt feature flag=true + Petra TalentPrompt 存在 → 組合 base template + persona prepend
- BuildPetraSystemPrompt feature flag=true + Petra TalentPrompt 不存在 → 純 base template 0 persona prepend（backwards-compatible 守護）
- 既有 Test 9/27/28/46/47 baseline 對齊新 content（Forge 實作後對齊 — 若新 content 含關鍵字變動 → 同步 test assertion）

### 8. Directory.Build.props v3.62.0 → v3.63.0

對齊 Stage 結案版本歷史紀律。

---

## 設計決策

1. **升級走 versioning path 不直接覆寫 v1**（對齊 Stage 72 versioning + rollback 機制 / 真實生效實證 / production 安全紀律延續）
2. **DbSeeder helper 幂等 + race-safe**（對齊 Stage 67 EnsureTalentsAsync + Stage 72 EnsureSkillPromptsAsync pattern / Bot+Dashboard 並行 seed 場景 0 race issue）
3. **CLAUDE_*.md source 檔同步升級**（v4 既有 path 自動拿到新 content / v5.5 path 走 DB 拿到新 content / 兩 path 0 分裂 / 對齊「修根因 > 補丁」哲學 — 不走「v5.5 升級但 v4 留舊版」分裂 path）
4. **Petra persona 走 TalentPrompt 不走 SkillPrompt**（對齊 Stage 72 兩層 schema 設計精神 — Skill=職位共享 / Talent=個性風格 / Petra 個性差異走 Talent 層而非把個性塞進職位 prompt）
5. **Trial_v18 議題 1 不修 Code 改修 prompt**（在 spike branch / not main / PR close 自動消失 / Stage 73 改透過 Cody Skill prompt 升級「production-grade UX consistent pattern」紀律間接守 / 未來 Cody 跑類似任務時自然對齊 — 修根因紀律延伸）
6. **具體 prompt 文案由 Forge 起草 + Christ Plan Mode review**（Aria Roadmap 寫 scope / 設計原則 / 邊界 / 驗收 — 具體文字 Forge 寫 implementation plan 時 Christ 拍板細節 / 對齊「Aria scope 層 Forge 具體實作層」分工紀律）
7. **業界 reference 參考不照搬**（對齊「自己用爽」精神 — Christ 個人專屬工具 / 業界 OneManCompany / Spring AI Skills / Anthropic multi-agent best practice 參考方向不照抄全套）
8. **Backwards-compatible 守護 4 層延續**（Stage 72 既有機制）：v4 既有 path / v5 既有 path / v5.5 既有 hardcoded path（feature flag=false fallback）/ Stage 70+71+72 累積 prompt 紀律保留

---

## 驗收情境

### 場景 A：6 個 SkillPrompt 升級到 v2 + 舊 v1 保留 audit trail（xUnit + DB query 驗）

**觸發**：Bot 啟動 → DbSeeder.SeedAsync → UpgradeSkillPromptsToV2Async 跑 → SQL query skill_prompts table

**驗證**：
- 同 SkillName 累積 2 row（VersionNumber=1 IsActive=false / VersionNumber=2 IsActive=true）
- 6 個 SkillName 全部完成升級（code_implementation / code_review / qa_testing / documentation / ceo_orchestration / petra_orchestration）
- partial unique index `(SkillName) WHERE IsActive = true` 真實 enforced 0 違反（Stage 72 機制延續）
- xUnit `UpgradeSkillPromptsToV2Async` test case 全綠

### 場景 B：DbSeeder UpgradeSkillPromptsToV2Async 幂等

**觸發**：Bot 重啟（第二次 / 第三次 startup）→ DbSeeder.SeedAsync 再跑

**驗證**：
- 同 SkillName VersionNumber=2 已存在 → skip（不重複塞 v3 row）
- skill_prompts table row count 0 增加（穩定在 12 row = 6 skill × 2 version）
- xUnit 幂等 test case 全綠

### 場景 C：Petra TalentPrompt persona seed + ResolveTalentPersonaAsync 真實返回

**觸發**：Bot 啟動 → DbSeeder seed Petra TalentPrompt → `PromptResolver.ResolveTalentPersonaAsync(petraTalentId, ct)` 真實調用

**驗證**：
- talent_prompts table 1 row（TalentId = Petra.Id / PersonaBody 非空 / VersionNumber=1 IsActive=true）
- `ResolveTalentPersonaAsync` 返回 PersonaBody 含 Petra persona 4 拍板特質關鍵字（「謹慎」/「冗餘」/「持續」/「對等」或其同義語）
- xUnit Petra persona seed test case 全綠

### 場景 D：feature flag=true + Petra TalentPrompt 存在 → BuildPetraSystemPrompt 組合 base + persona

**觸發**：Workflow:UseV5PromptDb=true（production active）+ BuildPetraSystemPromptForRuntimeAsync 真實調用 + Petra TalentPrompt 存在

**驗證**：
- 返回 prompt 含 base template 三 trigger 條件段（Stage 72 既有）+ persona prepend 4 拍板特質段
- promptLen > Trial_v18 baseline（1674 char）— 含 persona 段累積
- xUnit BuildPetraSystemPrompt + persona test case 全綠

### 場景 E：feature flag=true + Petra TalentPrompt 不存在 → 純 base template 0 persona

**觸發**：Workflow:UseV5PromptDb=true + 手動 SQL UPDATE 切 Petra TalentPrompt IsActive=false（or 0 row 場景）→ BuildPetraSystemPromptForRuntimeAsync

**驗證**：
- 返回 prompt = 純 base template（Stage 72 既有 v1 baseline / 0 persona prepend）
- backwards-compatible 守護真實生效（升級失敗 → 自然 fallback / 0 production crash）
- xUnit fallback test case 全綠

### 場景 F：Trial_v19 真實業務驗（Aria 全程自跑 9-step 模板第 5 次實踐）

**觸發**：Trial_v18 同 prompt（Dashboard 錯誤處理打磨 / cost 預估 $1.5-3 / 5-15 min）

**驗證**：
- ① Bot log 含 `PromptResolver: cache reloaded N skills / 1 talents`（Petra persona seed 真實 hit cache）
- ② Bot log 含 `Petra v5.5 BuildPetraSystemPrompt persona prepended talentName=Petra`（persona 真實組合）
- ③ Cody Code 品質對齊 Trial_v18 baseline（4.7/5 / production-grade UX / 範圍 cover 完整 / consistent pattern）
- ④ Vera review 質感對齊 Trial_v18 baseline（找 critical + 偏好放行）
- ⑤ Quinn 測試覆蓋對齊 Trial_v18 baseline（19+ test / unverifiable_targets 正確 escalate）
- ⑥ PR 真開 + 業務 UX 對齊 Christ 預期
- ⑦ Aria 評分 ≥ 4.5/5 整體（對齊自省點 #35「品質 > 做法」評估框架 + Trial_v18 6 維度業務評分機制）
- ⑧ cost 對齊預估 $1.5-3 + Forge session ~$1.5（自省點 #38 雙因子）
- 對齊「連續 9 Trial 業務級成功延續 v10-v19」紀律

### 場景 G：rollback 機制驗（v2 升級後可 rollback 回 v1）

**觸發**：手動 SQL `PromptRepository.RollbackSkillPromptAsync("petra_orchestration", targetVersion: 1)` + `curl /internal/reload-cache?scope=all` + 跑 Mock task

**驗證**：
- skill_prompts table：petra_orchestration VersionNumber=1 切 IsActive=true / VersionNumber=2 切 IsActive=false
- `PromptResolver.ResolveCapabilityPromptAsync("petra_orchestration")` 返回 v1 baseline content
- Mock task 走 v1 baseline 正常完成（rollback 真實生效保 production 安全 / 對齊「業界 versioning + rollback 保護 production」精神延續）

### 場景 H：v4 既有 path 0 regression

**觸發**：SQL 切 `Workflow:UsePetraOrchestratorV5=false`（v4 path） + 跑同 task

**驗證**：
- Bot log 0 含「Petra v5.5 path」字樣 / 走 v4 既有 path
- v4 既有 DevAgentService / ReviewerAgentService / QaAgentService / DocAgentService / CeoAgentService 用升級後 `Resources/CLAUDE_*.md` 讀檔（v4 path 自動拿到新 content / source 檔同步升級紀律 4 真實生效）
- v4 path 既有 baseline 行為 0 改變

---

## 技術約束

- 環境細節 source of truth 對齊 [workflow_aria.md 第三節 A 第 7 條紀律](../../memory/workflow_aria.md)
- DbSeeder 幂等 + race-safe 對齊 Stage 67 EnsureTalentsAsync + Stage 72 EnsureSkillPromptsAsync pattern
- xUnit test 補強對齊 Stage 72 既有 PromptRepositoryTests + Test 46/47 baseline pattern
- 對齊 [`docs/conventions/csharp.md`](../conventions/csharp.md) / [`ef-core.md`](../conventions/ef-core.md) / [`refactor-sop.md`](../conventions/refactor-sop.md)
- backwards-compatible 守護 4 層延續：v4 既有 path / v5 既有 path / v5.5 既有 hardcoded path（feature flag=false fallback）/ Stage 70+71+72 累積 prompt 紀律保留
- 具體 prompt 文案由 Forge 起草 + Christ Plan Mode review（Aria scope 層 Forge 具體實作層分工）
- 對齊「品質 > 做法」評估框架（自省點 #35）+「對等和互相」精神（自省點 #36 + Christ 親口拍板）

---

## 實作紀錄（v2.0）

**Forge 實作 commit**：[`cb47648`](https://github.com/darkleong/AiTeam/commit/cb47648) — feat(stage73): Prompt content 升級 v3.63.0 — 6 SkillPrompt v1→v2 + Petra TalentPrompt persona seed
**實作日期**：2026-05-17
**Forge session 規模**：12 檔變動（705 ++ / 418 −−）+ 2 新檔（PetraPersonaSeed.cs / Stage73UpgradeTests.cs）

### 實作完成項目（對齊 Roadmap 8 子項）

| 子項 | 實作摘要 |
|---|---|
| **1. 6 SkillPrompt content 升級** | 5 .md source 檔（Cody/Vera/Quinn/Sage/Victoria）+ PetraPromptTemplate.cs 全 overwrite「品質目標 + 業界 best practice + 邊界紅線 + 對等和互相」內容。DB skill_prompts table 6 SkillName 各從 v1 升 v2（v1 切 IsActive=false / v2 IsActive=true + CreatedByUser='stage73-upgrade'）— production SQL 驗證 12 rows 對齊。 |
| **2. Petra TalentPrompt persona seed** | 新立 [`PetraPersonaSeed.cs`](../../src/AiTeam.Data/SeedContent/PetraPersonaSeed.cs) 常數檔（對齊 PetraPromptTemplate.cs source-of-truth symmetric pattern）+ DbSeeder.EnsurePetraTalentPromptAsync helper（race-safe 單 entity detach）+ talent_prompts production 1 row 真實寫入（907 chars / 4 拍板特質關鍵字「謹慎/冗餘/持續/對等」全 hit）。 |
| **3. PetraPromptTemplate.Template 同步** | [`PetraPromptTemplate.cs`](../../src/AiTeam.Data/SeedContent/PetraPromptTemplate.cs) Template 常數對齊新版 petra_orchestration content（開頭 `v5 動態架構` → `v5.5 動態架構` + 新加「派工夥伴」對等精神段 + 「品質目標」3 點 / 3 placeholder `{{capabilityRoster}}` / `{{decompositionSection}}` / `{{outputSection}}` 完整保留 runtime 注入機制）。 |
| **4. CLAUDE_*.md 5 source 檔同步升級** | 5 檔 overwrite（v4 既有 DevAgentService/ReviewerAgentService/QaAgentService/DocAgentService/CeoAgentService fallback path 自動拿到新 content / v5.5 path 走 DB / 兩 path 0 分裂）。 |
| **5. UpgradeSkillPromptsToV2Async helper** | DbSeeder 內新立（[`DbSeeder.cs`](../../src/AiTeam.Data/DbSeeder.cs)）— 走 PromptRepository.UpsertSkillPromptAsync versioning path / 同 SkillName VersionNumber>=2 active row 已存在 → skip 幂等 / race-safe 單 entity detach 對齊 Stage 72 EnsureSkillPromptsAsync pattern。SeedAsync 接入順序：`EnsureSkillPromptsAsync` → `UpgradeSkillPromptsToV2Async` → `EnsurePetraTalentPromptAsync`。 |
| **6. PromptResolver cache invalidate** | reload-cache API wire 真實生效驗（Aria 結案後 `curl /internal/reload-cache?scope=all` 觸發 / Bot log `Bot Cache 已清除（scope=all）`）— 完整 BuildPetraSystemPromptForRuntimeAsync persona prepend 真實 hit 留 Trial_v19 真實 Petra dispatch 場景。 |
| **7. xUnit test 補強** | 新立 [`Stage73UpgradeTests.cs`](../../src/AiTeam.Bot.Tests/Orchestration/Stage73UpgradeTests.cs) 5 case：T1 v1→v2 升級（INLINE path）/ T2 幂等 skip v2 已存在 / T3 Petra persona seed + 4 關鍵字驗 / T4 EnsurePetra... 幂等 / T5 PetraPromptTemplate 升級 marker 驗 / 既有 Test47 `v5 動態架構` → `v5.5 動態架構` assertion 同步 update。75/75 全綠。 |
| **8. Directory.Build.props bump** | v3.62.0 → v3.63.0。 |

### 關鍵設計決策

1. **PetraPersonaSeed.cs 走抽常數 vs inline 進 DbSeeder.cs** — Plan Mode 議題 1 拍板走抽常數 / 對齊 PetraPromptTemplate.cs 既有「SeedContent/ 抽常數」前例 symmetric pattern（DbSeeder seed + 未來 xUnit / WebUI rollback 都可共用 source-of-truth / 0 重複維護）。
2. **BuildPetraSystemPromptForRuntimeAsync persona prepend 位置** — Plan Mode 議題 4 拍板走 base template 上方 + separator（`────────────────────────────` 四個全形破折號 / 視覺分區明顯 / persona 個性框架先讀、職務 base 後讀 — 對齊「個性會帶進所有 dispatch 決策」邏輯順序）。
3. **PetraOrchestratorService.BuildPetraSystemPromptForRuntimeAsync 用既有注入 db** — Gate1 verified `AppDbContext db` 直接注入（非 IDbContextFactory / line 40 primary constructor）— 直接用 `db.Talents.AsNoTracking().FirstOrDefaultAsync(...)` 不需重構 DI / 0 lifecycle 雷。
4. **Quinn JSON schema 維持既有不改**（Aria 二檢 C1 修法）— `QaReport` class 5 欄位 `{status, passed_tests, failed_tests, no_test_reason, summary}` + Write tool 直寫測試檔 pattern + status 三值 + 兩類失敗語意分離全保留 — 只升級文字 wording / 紀律強調 / 對等和互相段，避免 production breaking change（`QaCoordinationService.HandleQaCompletedAsync` 依 status 走 3 條 routing path）。
5. **fresh DB v1+v2 假 audit trail 接受現狀**（Aria 二檢 W3 修法）— fresh DB 是 rare event（CI test / 開發機初始化）/ production 升級才是主場景 / 改進方案會破壞 Stage 72 既有 EnsureSkillPromptsAsync 幂等紀律 — trade-off 不值得，已在 commit message + 本紀錄段明寫。
6. **Petra persona 第 3 條「持續迭代」wording 對齊 Stage 76 兩層 queue 配套**（Aria 二檢 W4 修法）— evaluate dispatch 序列但**不取消已派出的 Worker 既有 subtask**（Worker 執行層 per-Talent 1 task at a time / 對齊 Stage 76 未來範疇）。
7. **DbSeeder helper race-safe 用單 entity detach**（Aria 二檢 W2 修法）— `db.Entry(newEntity).State = EntityState.Detached`（對齊 Stage 72 既有 `EnsureSkillPromptsAsync` pattern，避免 over-engineered detach 全部 ChangeTracker entries 影響 loop 內後續 query state）。

### 驗收後修正

**無**。一次 commit `cb47648` 通過 dotnet build / dotnet test / production SQL 驗證 / 6 場景自驗（A-D 內 Forge 範圍全綠 / E-H 留 Aria gate2 + Trial_v19 範疇）— 0 follow-up bug fix commit。

### Mock 覆蓋情況

不適用 — Stage 73 是 prompt content + DbSeeder + DI wire 升級，**無 Mock scenario 範疇**。驗收走 production SQL query + Bot startup log 證據鏈 + xUnit unit test 三層。

### 踩坑紀錄

1. **Branch 對齊 origin/main 多 3 docs commit**（rebase 前發現）— Aria 在 main 平行跑 3 docs commit（Stage_73_Roadmap.md 規劃書建立 + Future_Feature_v5.5 補強 Stage 76 兩層 queue 配套 + Phase 3 順序重排）。修法：`git stash` → `git pull --rebase origin main` → `git stash pop` → commit + push。0 source code 衝突（純 docs commit）。對齊 workflow_aria_stage_closing.md「掃 git log 必含 git fetch 紀律」（Stage 67 結案踩雷 follow-up）。
2. **petra_orchestration v2 SkillPrompt body 不含「對等」字符是 expected** — Petra base template 用「派工夥伴」表達同概念 / 「對等和互相」明文化在 Petra TalentPrompt persona body（兩層分離 / 不重複）— production SQL 驗證時揭露 cross-check「對等」keyword 在 5 .md / 不在 petra_orchestration base template / 在 talent_prompts persona body。架構正確、非 bug。

### Gate1 自驗紀律已套（Aria 規劃書要求）

1. `grep "v5 動態架構"` 全 Tests 範圍 → 揭唯一 assertion 衝突 = Test47 line 1047 → 同步 update `v5.5 動態架構`。
2. `PetraOrchestratorService` ctor 注入確認為 `AppDbContext db`（非 IDbContextFactory / line 40）→ `ResolvePetraPersonaAsync` 直接用 db 不需重構 DI。
3. `dotnet test --filter "FullyQualifiedName~PetraOrchestratorServiceTests"` 確認 Test9/27/28/46/47 全綠（含 Test47 update）。

### 本機驗證結果

| 項目 | 結果 |
|---|---|
| `dotnet build AiTeam.slnx` | ✅ Build succeeded 0 error（102 warnings 全 pre-existing）|
| `dotnet test AiTeam.Bot.Tests` | ✅ 75/75 全綠（含新 5 Stage 73 case + Test47 v5→v5.5 update + 既有 Test9/27/28/46 baseline 全保留）|
| CI/CD self-hosted runner deploy | ✅ `Deploy main (cb47648)（done）` InternalController log 確認 |
| Production DB skill_prompts | ✅ 12 rows（6 SkillName × 2 version）/ partial unique index `(SkillName) WHERE IsActive=true` 真實 enforced |
| Production DB talent_prompts | ✅ 1 row Petra / IsActive=true / 907 chars / 4 拍板特質關鍵字全 hit |
| reload-cache wire | ✅ `Bot Cache 已清除（scope=all）` log 真實生效 |

### 場景驗收對應

| 場景 | 對應驗證手段 | 結果 |
|---|---|---|
| A：6 SkillPrompt v1→v2 + audit trail | SQL query skill_prompts + Bot startup log 6 UPDATE+6 INSERT | ✅ Forge 範圍 |
| B：DbSeeder Upgrade 幂等 | xUnit T2 case | ✅ Forge 範圍 |
| C：Petra TalentPrompt persona seed + 4 關鍵字 | SQL query talent_prompts + POSITION 關鍵字驗 | ✅ Forge 範圍 |
| D：BuildPetraSystemPrompt persona prepend | reload-cache wire 驗 + xUnit T5 content marker 驗 / 完整 runtime prepend 留 Trial_v19 | ✅ 部分 Forge 範圍 |
| E：persona missing fallback | xUnit T5 + Test47 baseline 保留驗（v1 baseline path 0 regression） | ✅ Forge 範圍 |
| F：Trial_v19 真實業務驗 | Aria gate2 / Trial_v19 範疇 | ⏳ 留 Aria + Christ 觸發 |
| G：rollback 機制驗 | Aria 結案後手動 SQL + curl reload-cache 驗 | ⏳ 留 Aria 範疇 |
| H：v4 path 0 regression | Aria gate2 / 切 flag 跑 mock task 驗 | ⏳ 留 Aria 範疇 |

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-17 | **Forge 實作完成 + 本機驗證 + Forge 自驗通過 — commit [`cb47648`](https://github.com/darkleong/AiTeam/commit/cb47648)**。8 子項全交付（6 SkillPrompt v1→v2 升級 + Petra TalentPrompt persona seed + PetraPromptTemplate.Template 同步 + CLAUDE_*.md 5 source 檔同步 + DbSeeder 2 新 helper + PromptResolver cache reload-cache wire + xUnit 5 新 case + Directory.Build.props bump）。本機驗證：dotnet build 0 error + dotnet test 75/75 全綠 + CI/CD 部署成功 + production SQL 6×2=12 SkillPrompt rows + 1 Petra TalentPrompt row（4 拍板特質關鍵字全 hit）+ reload-cache wire 真實生效。**踩坑 2 條**（branch 對齊 origin/main 多 3 docs commit rebase / petra_orchestration v2 不含「對等」字符 expected — 兩層分離 base template vs persona body）。**0 follow-up bug fix commit**。場景 F-H 留 Aria gate2 + Trial_v19 範疇。 |
| v1.0 | 2026-05-17 | 規劃書建立 — v3.63.0 / M+ 規模 / v5.5 Phase 3 Step 7 Prompt content 升級 + Petra TalentPrompt persona seed。**範圍**：6 個 SkillPrompt content 升級對齊「品質 > 做法」精神（VersionNumber=1 → 2 走 UpsertSkillPromptAsync versioning path / 舊版保留 audit trail）+ Petra TalentPrompt persona seed 1 row（4 拍板特質：謹慎拍板 / 對冗餘不容忍 / 持續迭代 / 對等和互相）+ PetraPromptTemplate.Template 對齊 + CLAUDE_*.md 5 source 檔同步升級（v4 fallback path 自動拿到新 content）+ DbSeeder UpgradeSkillPromptsToV2Async helper（幂等 + race-safe）+ xUnit 3-5 case + Directory.Build.props bump。**戰略脈絡**：Stage 72 schema 已搬家完成 / Stage 73 升級「內容」對齊「品質 > 做法」精神（自省點 #35 / Trial_v17 戰略級觀察延伸）+ 業界 Orchestrator persona pattern（既有 Future_Feature_v5.5.md WebSearch 結論延用 / 不重複觸發）。**核心紀律**：具體 prompt 文案由 Forge 起草 + Christ Plan Mode review（Aria 寫 scope / 設計原則 / 邊界 / 驗收）。**校準錨預期**：一般架構級重構區間 ×0.43-0.60（Stage 67/68/69/70/72 5 資料點 baseline / Stage 73 = 第 6 資料點累積）。**驗收**：8 場景 — A 6 SkillPrompt 升級 v2 + audit trail / B 升級幂等 / C Petra TalentPrompt persona seed + 真實返回 / D feature flag=true + persona 組合 / E feature flag=true + persona 不存在 fallback / F **Trial_v19 真實業務驗（Aria 9-step 第 5 次實踐）** / G rollback 真實生效 / H v4 path 0 regression。**下一步**：Forge 實作 + Aria gate1 Tier 0+1 + Trial_v19 真實任務驗 → 通過後 Stage 74 開（真並行 dispatch + 3 agent debate）。**Phase 3 完整收口路徑**：73（prompt 升級）→ 74（並行 + debate）→ 76（兩層 queue 配套）→ 75（WebUI Talent CRUD 最後做）→ v5.5 完整收口。 |
