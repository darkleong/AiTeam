# Stage 70 Roadmap — v5.5 Phase 2 Step 4：Petra 拆解指令精準度（subtask + dependency graph）

> 目標版本：**v3.60.0**（minor — v5.5 Phase 2 第二步 / Petra prompt engineering 升級 + dispatch wire 支援 dependency）
> 狀態：規劃中
> 範圍：Petra prompt 升級 hierarchical decomposition + SubtaskPlan in-memory schema + DispatchTalentsAsync 支援 dependency dispatch + feature flag 守 fallback
> 規模：M
> 對應 v5.5 規劃：[Future_Feature_v5.5.md](Future_Feature_v5.5.md) Phase 2 Step 4

---

## 戰略脈絡

**Stage 69 結案後 v5.5 Phase 2 第二步 — Petra 拆解指令精準度**：

對齊 Christ「Agent 像人類處理事件」精神 — Agent 接到複雜任務時應該能拆解成可獨立執行的 subtask + 識別依賴關係 + 派合適 Talent 處理。既有 Petra DecideTalentsAsync = 「Skill 序列線性 chain」（單 path / 0 依賴建模）— 對複雜任務只能線性處理。Step 4 升級 Petra 為真正 hierarchical orchestrator。

### 計劃前 WebSearch 結論（2026-05-16）

**WebSearch 1：Microsoft Agent Framework Orchestration Patterns（避 Stage 64-69 framework 結論誤判紀律）**

- Framework 內建 5 orchestration patterns：sequential / concurrent / handoff / group chat / magentic（[Microsoft Agent Framework Workflows Orchestrations](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/)）
- ⚠️ **framework 無 hierarchical orchestrator primary pattern** — 但業界 hub-and-spoke / hierarchical 是 2026 production standard（[AI Agent Orchestration Patterns Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/ai-agent-design-patterns)）
- → **Stage 70 範圍 = 在 v5.5 自管 chain 上層加 Petra hierarchical decomposition + dependency dispatch**（不依賴 framework — 對齊 Stage 66 自管 chain 取代 BuildSequential 精神）

**WebSearch 2：LLM Task Decomposition 2026 best practice 已成熟**

- ✅ **業界已有明確 pattern**（vs v5.5.md 半年前「無 best practice 要 spike」結論已過時）— Step 4 從「純 spike」降為「對齊業界 pattern 落地實作」
- **核心 3 pattern**：Chain-of-Thought / Tree of Thoughts / Hierarchical decomposition
- **依賴建模 2026 advancement**（[P2P's Code Judge](https://www.emergentmind.com/topics/task-decomposition-strategies)）：sequential（1→2 gated）/ independent（(1,2)）/ nested（1.1, 1.2）
- **prompt engineering best practice**：Instruction-driven decomposition + Few-shot prompting 示範拆解格式 + 顆粒度
- 🎯 **AiTeam 對齊精神**：Petra prompt 升級「hierarchical decomposition + dependency graph」+ 給 few-shot 範例對齊 AiTeam 真實場景（Cody/Vera/Quinn/Sage Skill 分配）

### 範圍邊界刻意收緊

- ✅ 做：Petra prompt 升級拆解能力 + SubtaskPlan in-memory record + DispatchTalentsAsync 接收 subtask + sequential dependency dispatch（chain pattern 對齊 v5.5 既有 wire）+ feature flag `Workflow:UseV5SubtaskPlanning` default false 守 fallback + xUnit test
- ❌ 不做：並行 dispatch（independent subtask 純設計 surface / 真實 dispatch 仍 sequential — 留 Phase 3 評估真並行）/ 持久 SubtaskPlan DB schema（純 in-memory / 留 Phase 3 評估）/ LLM-based replanning（拆 1 次 dispatch 完即結束）/ 跨 subtask retry / 跨 subtask BossInteraction / Stage 69 memory layer 擴張（subtask 各自仍走 sessionId memory inject 不改 scope）

---

## 子項清單

### 1. Petra prompt 升級 — hierarchical decomposition + dependency graph

**修改**：[`PetraOrchestratorService.cs`](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs) `BuildPetraSystemPrompt`（既有 v5.5 path / Stage 67 加「需求拆解紀律」段對齊）

- 既有「需求拆解紀律」段升級加「hierarchical decomposition 紀律」子段
- Few-shot 2-3 範例對齊 AiTeam 真實場景（簡單 task → 線性 Skill 序列 / 複雜 task → 拆 N subtask + dependency graph）
- 輸出格式從既有「`code_implementation|code_review`」Skill 序列字串 → 升級 JSON `SubtaskPlan`（含 subtask list + dependency edges）
- 對既有 simple task 維持線性 chain（backwards-compatible / 拆解能力是擴展不是取代）

**Forge spike 必驗**：
- 既有 `DecideTalentsAsync` 返回 `(List<string> Skills, List<ITalent> TalentPicks)` tuple → 升級為 `(List<SubtaskPlan>, ...)` 或加新 method
- prompt template 內 few-shot 範例設計（對齊 Trial_v6-v14 prompt baseline 真實場景）

### 2. SubtaskPlan in-memory record schema

**新檔** `src/AiTeam.Bot/Orchestration/Petra/SubtaskPlan.cs` — record 結構（不持久 DB / 對齊「對冗餘不容忍」+ 漸進 path 精神）：

- `SubtaskPlan` record 含：`Subtasks: List<Subtask>` + `Dependencies: List<DependencyEdge>`
- `Subtask` record：`Id` (sequence number int) / `SkillName` (對應 ISkillRegistry) / `Description` text / `TalentName` (Petra 選定)
- `DependencyEdge` record：`FromId` int / `ToId` int / `Type` enum (Sequential / Nested)

**範圍守緊**：不存 DB（純 in-memory + dispatch 完成即丟）/ 不開 Independent dependency 真實並行（純 design surface / 留 Phase 3）

### 3. PetraOrchestratorService DispatchTalentsAsync 升級 — dependency dispatch

**修改**：[`PetraOrchestratorService.cs`](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs) v5.5 path 既有 `DispatchTalentsAsync`

- 簽名升級：`Dispatch(SubtaskPlan plan, ...)` 取代 `Dispatch(skills, talentPicks, ...)`
- dispatch 邏輯：topological sort dependency graph → sequential dispatch order（baseline 起步）
- 對 simple plan（單 chain）行為跟既有 Stage 69 v5.5 path 完全一致（backwards-compatible / 0 regression）
- Bot log 升級 — 拆解結果 + dispatch order 觀察點

**Forge spike 必驗**：
- 既有 chain pass-through（Stage 66 自管 chain）對齊 dependency dispatch wire
- 既有 Stage 69 memory inject/寫回邏輯（per subtask 仍走 sessionId scope 不改）

### 4. Feature flag `Workflow:UseV5SubtaskPlanning` default false

**修改**：[`WorkflowSettings.cs`](src/AiTeam.Bot/Configuration/WorkflowSettings.cs) + [`WorkflowSettingsResolver.cs`](src/AiTeam.Bot/Configuration/WorkflowSettingsResolver.cs) + `appsettings.json`

- `UseV5SubtaskPlanning: bool` default false（守 v5.5 既有 path fallback — Trial_v16 驗 + Christ 拍板才切 default true）
- 必須 `UsePetraOrchestratorV5=true` + `UseTalentSkillSeparation=true` 才有意義
- 對齊既有 7 條 framework / v5 / v5.5 flag pattern

### 5. xUnit test 補強

新 3-5 case 對齊 Stage 69 baseline pattern：

- SubtaskPlan record + DependencyEdge schema baseline test
- DecideTalentsAsync 升級返回 SubtaskPlan（mock LLM JSON 輸出）
- DispatchTalentsAsync 接收 SubtaskPlan 後 topological sort dispatch order 對齊
- Feature flag default false 守 v5.5 既有 path 0 regression
- 既有 Stage 69 memory inject/寫回邏輯跟 subtask dispatch 對齊（per subtask 走 sessionId）

### 6. Directory.Build.props v3.59.0 → v3.60.0

---

## 設計決策

1. **不依賴 framework hierarchical orchestrator pattern**（framework 無此 primary pattern — 對齊 Stage 66 自管 chain 精神）
2. **業界 2026 best practice 對齊**：Chain-of-Thought / Tree of Thoughts / Hierarchical decomposition + dependency graph（sequential / nested / independent）
3. **SubtaskPlan 純 in-memory record 起步**（不持久 DB / 對齊「漸進 path」+「不引入 schema 複雜度」精神）
4. **Independent dependency 純設計 surface**（真實 dispatch 仍 sequential — 留 Phase 3 評估真並行 / 避免「並行 LLM call cost 暴漲」風險）
5. **Backwards-compatible**：simple task 維持線性 Skill 序列（Trial_v6-v14 既有 prompt baseline 完全不變 / 拆解能力是擴展）
6. **Stage 69 memory layer 不擴**：per subtask 仍走 sessionId scope（一個 PetraSession = 一個 Task event 對齊）
7. **Feature flag `UseV5SubtaskPlanning` default false 守 fallback**（Trial_v16 驗 + Christ 拍板才切）

---

## 驗收情境

### 場景 A：Petra prompt 拆解能力（simple task 線性 vs 複雜 task 拆解）

**觸發**：xUnit Test 用 mock LLM JSON 輸出
- simple task input → 預期 Petra 回單 chain Skill 序列（對齊 Trial_v6-v14 既有 baseline）
- 複雜 task input → 預期 Petra 回 SubtaskPlan 含 ≥ 2 subtask + ≥ 1 dependency edge

**驗證**：
- simple task：SubtaskPlan.Subtasks.Count == 1 / Dependencies 空 / 行為對齊 Stage 69 既有
- 複雜 task：Subtasks.Count ≥ 2 / Dependencies.Count ≥ 1 / Type=Sequential

### 場景 B：SubtaskPlan record + DispatchTalentsAsync 升級接收 subtask list（schema 驗）

**觸發**：xUnit Test mock SubtaskPlan 含 3 subtask（1→2→3 sequential chain）

**驗證**：
- DispatchTalentsAsync topological sort 後 dispatch order = [subtask1, subtask2, subtask3]
- 每 subtask 走既有 v5.5 chain dispatch wire（adapter + ClaudeCodeChatClientAdapter / 對齊 Stage 66 自管 chain）
- Bot log 「v5.5 subtask dispatch i/N talent=X skill=Y dependencyOn=[...]」

### 場景 C：跟 Stage 69 memory layer 整合（per subtask 走 sessionId scope）

**觸發**：feature flag `UseV5SubtaskPlanning=true` + `UseV5Memory=true` + Mock 複雜 task → SubtaskPlan 3 subtask dispatch

**驗證**：
- SQL `SELECT COUNT(*) FROM task_memories WHERE PetraSessionId='<session>'` ≥ 3（每 subtask 各寫一條 `decision/{talent}-output-summary`）
- 每 subtask dispatch 前 GetTaskMemoriesAsync 注入累積 memory（前面 subtask 結果可供下一 subtask 參考 — 對齊「Agent 像人類處理事件」精神）

### 場景 D：feature flag default false 守 v5.5 既有 path 0 regression（保護驗）

**觸發**：
- DB flag `Workflow:UseV5SubtaskPlanning=false`（default）+ UseTalentSkillSeparation=true + UsePetraOrchestratorV5=true
- /mock framework_pipeline 跑

**驗證**：
- Bot log 0 含「v5.5 subtask dispatch」字樣（走既有 DecideTalentsAsync 線性 chain）
- 行為對齊 Stage 69 既有 v5.5 path（Cody → Vera 線性 chain / 對齊 Trial_v14 baseline）
- dotnet test 既有 PetraOrchestratorServiceTests case 全 PASS（regression baseline 維持）

### 場景 E：v4 既有 production path 0 regression（守護驗）

**觸發**：DB flag UsePetraOrchestratorV5=false（v4 path）+ /mock framework_kickoff_happy 跑

**驗證**：v4 path 完整跑通 0 regression / dotnet test 183+ PASS

### 場景 F：複雜 task 真實 dispatch order 驗（核心驗 — Trial_v16 留真實任務）

**觸發**：Mock 真實複雜 prompt（含「先 X 再 Y 最後 Z」依賴語意）→ Petra 拆 3 subtask + sequential dependency

**驗證**：
- Bot log dispatch order 對齊 prompt 語意依賴
- 中間 subtask output 真實注入下一 subtask input（透過 Stage 69 memory layer / 對齊場景 C）
- 最終 PR 真實 cover 全 3 subtask 工作

---

## 技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第三節 A 第 7 條紀律
- Stage 70 在 main branch 跑（含 Stage 69 全 commits + v2.1 scope pivot）
- 預估規模 M（Petra prompt 升級 + SubtaskPlan record + DispatchTalentsAsync 升級 + flag + test）
- 預估 Forge context mid 350-450K（對齊 Stage 67/68/69 連續 ×0.58/0.60/0.43 架構級重構新區間 — Stage 70 無 Migration 不可逆風險 / 預估真實 200-300K）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-16 | 規劃書建立（Aria）— Stage 70 = v5.5 Phase 2 第二步 Petra 拆解指令精準度（hierarchical decomposition + dependency graph）。**計劃前 WebSearch 結論**：① Microsoft Agent Framework 5 orchestration patterns 無 hierarchical orchestrator primary — 對齊 Stage 66 自管 chain 精神在 v5.5 上層加 Petra hierarchical decomposition ② 業界 2026 LLM task decomposition best practice 已成熟（vs v5.5.md 半年前「無 best practice」結論過時）— Chain-of-Thought / Tree of Thoughts / Hierarchical decomposition + dependency 建模（sequential / nested / independent / P2P Code Judge 2026 advancement）— Step 4 從「純 spike」降為「對齊業界 pattern 落地實作」。**6 子項**：① Petra prompt 升級 hierarchical decomposition + few-shot AiTeam 真實場景範例 + JSON SubtaskPlan 輸出格式 ② SubtaskPlan + Subtask + DependencyEdge in-memory record schema ③ PetraOrchestratorService DispatchTalentsAsync 升級接收 SubtaskPlan + topological sort sequential dispatch（baseline）④ Feature flag `Workflow:UseV5SubtaskPlanning` default false 守 fallback ⑤ xUnit test 3-5 case ⑥ Directory.Build.props v3.60.0 bump。**6 驗收場景**：A Petra prompt 拆解能力（simple task 線性 vs 複雜 task 拆解）/ B SubtaskPlan + Dispatch 接收 subtask list 驗 / C 跟 Stage 69 memory layer 整合（per subtask 走 sessionId）/ D feature flag false 守 v5.5 既有 0 regression / E v4 既有 production path 0 regression / F 複雜 task 真實 dispatch order（Trial_v16 留真實任務驗）。**範圍邊界刻意收緊**：不開並行 dispatch（independent 純設計 surface / 真實 sequential / Phase 3 評估）/ 不持久 SubtaskPlan DB schema / 不 LLM-based replanning / 不擴 Stage 69 memory scope。**Backwards-compatible**：simple task 維持線性 Skill 序列（Trial_v6-v14 baseline 不變 / 拆解是擴展）。**Trial_v16 啟動條件**：Stage 70 Mock 全綠 + Aria gate1 通過 → 複雜 task 真實任務驗（場景 F）→ 通過 → Christ 拍板切 `Workflow:UseV5SubtaskPlanning` default true = v5.5 Phase 2 Step 4 完整收口 → 進 Step 5 Prompt DB 化 + Talent identity 整合。**規模 M 預估 Forge context mid 350-450K**（對齊 Stage 67/68/69 架構級重構新區間 / 無 Migration 風險低 / 預估真實 200-300K）。 |
