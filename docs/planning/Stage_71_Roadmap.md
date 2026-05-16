# Stage 71 Roadmap — v5.5 Phase 2 Step 3+4 production-ready 補強（Trial_v15+v16 揭 2 議題收口）

> 目標版本：**v3.61.0**（minor — production-ready 補強對齊 Stage 71 + CLAUDE.md 版本紀律「每 Stage 完成 minor bump」）
> 狀態：📋 規劃中
> 文件版本：v1.0
> 範圍：Petra prompt 升級「拆=真不同 scope」紀律 + Stage 69 memory 寫入空 content guard
> 規模：S
> 對應 v5.5 規劃：[Future_Feature_v5.5.md](Future_Feature_v5.5.md) Phase 2 Step 4.5（production-ready 補強）

---

## 戰略脈絡

**Trial_v15+v16 結案後 v5.5 Phase 2 Step 3+4 production-ready 補強**：

Trial_v15+v16 跑完揭 Stage 69+70 真實生效 + 核心驗收（跨 session memory inject）達標，但揭 2 個 production code 議題 + 1 個 Aria 操作紀律議題。Stage 71 範圍守緊只做 production code 修法（2 項），紀律議題走 `/aria-memory` 自省點 #34 處理（不在 Stage 71 範圍）。

### Trial_v15+v16 揭 2 議題與修法對齊

**議題 #1：Petra 過度拆 code_implementation 為 3 段重複工作**

Trial_v15.2 + Trial_v16 兩次跑 Petra 都把 Dashboard 錯誤處理打磨任務拆成 5 subtask（Cody×3 → Vera → Quinn），Cody 三段重複做同樣工作。
- **症狀**：Cody token 累積 20-26K × 3 段 / PR 規模反小 +32（vs Trial_v14 線性整包 +232）/ total cost +21% vs Trial_v14 baseline
- **root cause**：`BuildPetraSystemPrompt` Stage 70 加的 hierarchical decomposition few-shot 範例沒給 LLM 明確「拆 = 真的不同 scope」紀律 — LLM 看到「打磨多個 form」場景判斷為「分階段做」而非「線性整包」
- **修法方向**：few-shot 範例補強對齊邊界 — 「打磨多 form / 跨檔小修補」場景線性整包（1 個 code_implementation subtask）/ 「真不同 scope 任務 + 跨 Skill 串接」場景才拆 N subtask
- **影響範圍**：production cost 從 +21% 降回 baseline / Trial_v17 重驗 → 切 `UseV5SubtaskPlanning` default true

**議題 #2：Stage 69 memory 寫入沒檢查 outputLen=0 跳過**

Trial_v15.2 Quinn 因 Claude Code CLI 偶發 exit 1 outputLen=0，但 Petra 寫入路徑沒 guard，仍寫空 content 進 task_memories + talent_memories。Trial_v16 第二次跑 Quinn dispatch 時 inject 進空 content memory block 污染 prompt。
- **症狀**：task_memories / talent_memories 累積空 content row / 跨 session inject 污染下次 LLM prompt
- **root cause**：`PetraOrchestratorService` dispatch loop 寫入 path 沒檢查 outputText.Length=0 / `UpsertTaskMemoryAsync` + `UpsertTalentMemoryAsync` 接受任意 content 含空字串
- **修法方向**：寫入前 `if (outputText.Length == 0)` guard 跳過 + 一行 warning log（Worker output empty skip memory write）/ 既有寫入 path 對齊「不污染下次 prompt」紀律
- **影響範圍**：production memory 健康（避空 content 隨時間累積）/ Trial_v17 重驗 → 切 `UseV5Memory` default true

### 議題 #3 / #4 / #5 範圍邊界（不在 Stage 71 範圍）

**議題 #3 FinalizeGitAsync permission denied** — 走 Aria 紀律修正（自省點 #34），不修 production code：
- root cause = Aria 用 docker exec root 動 workspace 違反 Stage 65 entrypoint chown 紀律
- Bot service 是 appuser 沒 root → 跑不了 chown 防呆（要 sudo / setuid 違反 production 安全紀律）
- production 真實場景 Petra 自己跑（appuser）+ 容器 recreate 時 docker-compose entrypoint chown 自動修 — 不會踩
- 修法 = `/aria-memory` 加自省點 #34：Aria 自跑 Trial workspace cleanup 不要用 `docker exec sh -c "git ..."`，改用 `docker exec -u appuser` 或直接信任 Bot CloneOrPull 自處理

**議題 #4（已結案 transient）+ 議題 #5（已立自省候選）** — 不需要 production code 動

### 範圍邊界刻意收緊

- ✅ 做：`BuildPetraSystemPrompt` few-shot 補強「拆=真不同 scope」紀律 + Petra dispatch 寫入 path 加 outputLen=0 guard + xUnit test 補強 + Directory.Build.props v3.60.0 → v3.61.0
- ❌ 不做：
  - 議題 #3 production code 防呆（前述技術原因 + 違反「修根因 > 補丁」紀律）
  - Step 5 Prompt DB 化 + Talent identity 整合（性質不同 — 新功能擴展不是 production-ready 補強 / 規模 M 留 Stage 72 / 範圍刻意收緊紀律連續 7 Stage 0 follow-up）
  - Stage 70 既有 SubtaskPlan schema / DispatchTalentsAsync 簽名 / topological sort 邏輯（已驗 production 真實生效 / 0 動）
  - Stage 69 既有 MemoryRepository CRUD / compact 紀律 / 整合 dispatch 邏輯（已驗 / 只加 outputLen=0 guard）

---

## 子項清單

### 1. Petra prompt 升級「拆=真不同 scope」紀律

**修改**：[`PetraOrchestratorService.cs`](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs) `BuildPetraSystemPrompt`（既有 useSubtaskPlanning=true 段對齊 Stage 70 加的 hierarchical decomposition few-shot）

- few-shot 範例 2-3 個補強（對齊 Trial_v6-v14 真實場景）：
  - **線性整包反例**：「打磨多 form 錯誤處理 toast」/「跨檔小修補同類改動」→ 1 個 `code_implementation` subtask（不拆 N 段）
  - **真分 scope 正例**：「重構 module X + 新增 module Y + 文件升級」→ 拆 3 subtask 不同 Skill 配
  - **判斷邊界明文**：「同類 + 同 Skill + 同 scope」線性整包 / 「真不同 scope 跨 Skill 串接」拆解
- 既有 Stage 70 「hierarchical decomposition 紀律」段保留 + 加判斷邊界子段

**Forge spike 必驗**：
- few-shot 文案調整對齊 AiTeam 真實 prompt baseline（Trial_v6-v14 任務 vs 真複雜跨 module 任務）
- 既有 simple task 線性 chain 維持 0 regression（backwards-compatible 紀律）

### 2. Stage 69 memory 寫入 outputLen=0 guard

**修改**：[`PetraOrchestratorService.cs`](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs) v5.5 path dispatch loop 內 memory 寫回邏輯

- 既有 `if (memoryEnabled && talentNameToIdMap!.TryGetValue(talentName, out var talentIdForWrite))` 條件加 `outputText.Length > 0` guard
- 寫入前 outputLen=0 → 跳過 `UpsertTaskMemoryAsync` + `UpsertTalentMemoryAsync` + 一行 warning log（含 talent name + skill + sessionId 觀察點）
- 對 outputLen > 0 既有 truncate 500 char + upsert 邏輯完全不變
- 補 Bot log milestone：`Petra v5.5 dispatch worker output empty skip memory write talent={Talent} skill={Skill} sessionId={SessionId}`

**Forge spike 必驗**：
- 既有 outputLen > 0 path 完全不變（backwards-compatible）
- Worker output empty 場景 task_memories / talent_memories 0 寫入

### 3. xUnit test 補強

新 2-3 case 對齊 Stage 69 baseline pattern：

- BuildPetraSystemPrompt few-shot 升級後 prompt template 含「拆=真不同 scope」判斷邊界子段（assert prompt 字串含特定關鍵字）
- dispatch loop Worker outputLen=0 場景 → task_memories / talent_memories 0 寫入（mock LLM 回空字串）
- dispatch loop Worker outputLen > 0 場景 → 既有 upsert 邏輯仍生效（既有 baseline 對齊不退化）

### 4. Directory.Build.props v3.60.0 → v3.61.0

---

## 設計決策

1. **議題 #3 走紀律修正不修 production code**（修根因 > 補丁紀律 / 為 Aria 自身疏漏改 production code 開 sudo / setuid root 違反 production 安全紀律 / 真實 production 場景 docker-compose entrypoint 已自動防呆）
2. **範圍刻意收緊 — 2 項補強不擴 Step 5**（連續 7 Stage 0 follow-up 紀律延續 / 性質分離 production-ready 補強 vs 新功能擴展）
3. **Backwards-compatible 守護**：既有 Trial_v6-v14 線性 chain prompt baseline 完全不變 / outputLen > 0 既有寫入邏輯完全不變 / feature flag default 不動（Trial_v17 驗 + Christ 拍板才切 default true）
4. **few-shot 設計對齊 AiTeam 真實場景**（線性整包反例用 Trial_v6-v14 任務本身 / 真分 scope 正例用「跨 module」場景）
5. **Worker output empty 處理紀律**：跳過 memory 寫入 + log warning（不擋 dispatch 流程 / 對齊「容錯紀律」既有精神）

---

## 驗收情境

### 場景 A：Petra prompt 線性整包反例（Trial_v17 真實場景驗收）

**觸發**：Trial_v17 沿用 Trial_v6-v14 同 prompt（Dashboard 錯誤處理打磨 toast 通知 — 「打磨多 form 跨檔小修補」場景）

**驗證**：
- Bot log `Petra v5.5 Step 4 DecideTalentsWithPlanAsync 完成 — subtasks=N dependencies=N-1 picks=...`
- subtasks **≤ 2**（線性整包 — 1 個 code_implementation + 可選 1 個 code_review，**不拆 N 個 Cody 段**）
- 對比 Trial_v15.2 / Trial_v16 5 subtasks 過拆 = 紀律生效
- Cody token 總量降回 Trial_v14 baseline 區間（~16-30K / vs Trial_v15.2 20-26K × 3 段）

### 場景 B：xUnit prompt 升級 assert（純單元驗）

**觸發**：dotnet test 跑 `BuildPetraSystemPrompt(skillRoster, useSubtaskPlanning: true)` 返回 string

**驗證**：
- prompt 字串含「線性整包」+「真不同 scope」+「判斷邊界」3 個關鍵字段（assert exact substring match）
- prompt 字串含 ≥ 2 個 few-shot 範例（含「打磨多 form」反例 + 「跨 module」正例）
- prompt 長度比 Stage 70 baseline 多 ~500-1500 char（補的 few-shot + 判斷邊界子段）

### 場景 C：Worker outputLen=0 跳過 memory 寫入（xUnit 純單元驗）

**觸發**：dotnet test 跑 dispatch loop 用 mock LLM 回空字串（outputText = ""）

**驗證**：
- task_memories 0 寫入（`UpsertTaskMemoryAsync` 0 fire）
- talent_memories 0 寫入（`UpsertTalentMemoryAsync` 0 fire）
- Bot log 含「Petra v5.5 dispatch worker output empty skip memory write talent=...」warning
- dispatch loop 繼續 fire 下個 subtask 不擋流程（既有 `outputText ?? ""` fallback 邏輯仍生效）

### 場景 D：Worker outputLen > 0 既有寫入邏輯 0 regression（xUnit + Trial_v17 驗）

**觸發**：xUnit dispatch loop 用 mock LLM 回非空字串（outputText = "Build 通過，0 error。"）+ Trial_v17 真實跑

**驗證**：
- xUnit：`UpsertTaskMemoryAsync` 1 fire（key=`decision/{Talent}-output-summary` / content=truncated 500 char）
- xUnit：`UpsertTalentMemoryAsync` 1 fire（key=`last-task-summary` / content=truncated）
- Trial_v17：task_memories + talent_memories row 數對齊預期（每 outputLen > 0 worker 各寫 1 條）
- Trial_v17：第二次跑時 dispatch 1/N 注入 `talentMemoryCount > 0`（跨 session inject 持續生效）

### 場景 E：feature flag default false 守 v5.5 既有 path 0 regression

**觸發**：SQL 切回 `Workflow:UseV5Memory=false` + `Workflow:UseV5SubtaskPlanning=false` + reload-cache scope=all + Trial_v17 跑同 task

**驗證**：
- Bot log 0 含「Petra v5.5 Step 4 DecideTalentsWithPlanAsync」字樣（fallback 走 Stage 67 DecideTalentsAsync 線性 Skill 序列）
- Bot log 0 含「Petra v5.5 dispatch 注入 memory」字樣
- Bot log 0 含「寫回 TaskMemory / TalentMemory」字樣
- 對齊 Stage 67 v5.5 既有 baseline 行為（Cody → Vera 2 picks 對齊 Trial_v14 線性 chain）

### 場景 F：v4 既有 production path 0 regression

**觸發**：SQL 切 `Workflow:UsePetraOrchestratorV5=false`（v4 path） + Trial_v17 跑同 task

**驗證**：
- Bot log 0 含「PetraOrchestrator 啟動」字樣
- 走 v4 既有 hierarchical static path（DesignMeetingService / Cody DevAgentService 等）
- Trial_v6-v14 既有 baseline 行為 0 改變

---

## 技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第三節 A 第 7 條紀律
- v5.5 path BuildPetraSystemPrompt 既有 useSubtaskPlanning=true / false 分支保留（Stage 67 + Stage 70 累積 baseline）
- xUnit test 補強對齊 Stage 69 既有 [Fact]/[Theory] 累積 pattern（PetraOrchestratorServiceTests）
- 對齊既有 conventions/csharp.md / ef-core.md / refactor-sop.md
- backwards-compatible 守護：v4 既有 path / v5 既有 path / Stage 67 v5.5 既有 path / Stage 70 v5.5 升級 path 4 層完全 0 regression

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-16 | 規劃書建立 — v3.61.0 / S 規模 / v5.5 Phase 2 Step 3+4 production-ready 補強。**範圍**：Petra prompt 升級「拆=真不同 scope」紀律（few-shot 補強對齊 AiTeam 真實場景）+ Stage 69 memory 寫入 outputLen=0 guard（避空 content 污染下次 prompt）+ xUnit test 補強。**戰略脈絡**：Trial_v15+v16 揭 2 production code 議題（議題 #1 過度拆 cost +21% / 議題 #2 空 content memory 污染）+ 議題 #3 走 Aria 紀律修正不修 production code（修根因 > 補丁紀律 / Bot 沒 root 不能 chown / docker-compose entrypoint 已自動防呆）+ Step 5 Prompt DB 化留 Stage 72 不擴範圍（連續 7 Stage 0 follow-up 紀律延續）。**校準錨預期**：production-ready 補強區間 ×0.78-0.99（對齊 Stage 56/58/60/61/64/65 6 資料點 / Stage 71 = 第 7 資料點累積）。**驗收**：6 場景 — A Trial_v17 真實場景驗收（線性整包反例 subtasks ≤ 2）/ B xUnit prompt 升級 assert / C Worker outputLen=0 跳過寫入 / D Worker outputLen > 0 既有邏輯 0 regression / E feature flag default false 守 fallback / F v4 既有 path 0 regression。**下一步**：Forge 實作 + Aria gate1 Tier 0+1（production-ready 補強對應 tier）+ Trial_v17 真實任務重驗 → 通過後切兩個 default flag = Phase 2 Step 3+4 正式完整收口 → Stage 72 Step 5 Prompt DB 化。 |
