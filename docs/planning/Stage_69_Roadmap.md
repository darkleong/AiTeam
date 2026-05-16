# Stage 69 Roadmap — v5.5 Phase 2 Step 3：DB 持久記憶 schema + token budget compact

> 目標版本：**v3.59.0**（minor — v5.5 Phase 2 第一步 / 架構級新建 / 跨 session 長期持久記憶基底）
> 狀態：✅ 已完成（2026-05-16）
> 文件版本：v2.1
> 範圍：TaskMemory + TalentMemory entity + Migration + MemoryRepository + token budget compact 紀律 + 整合 v5.5 PetraOrchestratorService dispatch（feature flag 守 fallback）
> 規模：M-L
> 對應 v5.5 規劃：[Future_Feature_v5.5.md](Future_Feature_v5.5.md) Phase 2 Step 3

---

## 戰略脈絡

**Trial_v14 結案後 v5.5 Phase 1 完整收口 ✅ → 進 Phase 2 第一步 = 跨 session 長期持久記憶基底**：

「Agent 像人類處理事件」核心精神實作 — 接到任務後累積思考 / 中間別事打斷不會忘記 / 重啟接著做還記得來龍去脈。

### 計劃前 WebSearch 結論（2026-05-16 — 對齊 workflow_aria.md 第三節 A 第 9 條紀律）

**WebSearch 1：Microsoft Agent Framework AgentSession + Memory API**（避 Stage 64-67 framework 結論誤判修根因紀律）

- ✅ framework 已內建 `AgentSession`（conversation state container）+ `AIAgent.SerializeSession` / `DeserializeSessionAsync` + `AIContextProvider`（store memory ids / messages / session-specific values）
- ⚠️ **關鍵區別**：framework AgentSession 是 **short-term session 對話 history 容器** — 跟 v5/v5.5 既有 `PetraSessionRepository` + `PetraSessionMessages` entity **角色重疊**（短期 working memory）
- ❌ framework AgentSession **不 cover「跨 session 長期持久記憶」**（business knowledge / 個人偏好 / 過往任務經驗累積）
- → **Stage 69 範圍 = 額外一層「跨 session 長期記憶」上層**（不取代 PetraSessionRepository，而是分層架構）

**WebSearch 2：業界 2026 LLM Agent Memory best practice**

- ✅ **Hybrid memory 是 production standard**：三層 tier（context-window main memory + searchable recall database + vector-indexed archive）
- ✅ **Per-task per-agent scoping 是 norm**：每個 agent 自己 memory directory / 共用 knowledge 走 designated curator pattern
- ✅ Production frameworks：Mem0 / Zep / Letta(MemGPT) — 業界成熟工具
- 🎯 **AiTeam 對齊精神**（「Christ 自己用爽」+「不引入 dependency」）：PostgreSQL self-built schema 對齊業界 pattern / 不 import Mem0/Zep / 不開 vector index 起步（升級留 Phase 3 評估）

### 範圍邊界刻意收緊（對齊 Phase 2 第一步 + Phase 1 完整收口前最後一塊精神）

- ✅ 做：TaskMemory + TalentMemory entity（hybrid 雙層 schema 起步）+ Migration + MemoryRepository（CRUD + 簡單 query）+ token budget compact 紀律（threshold-based summarize）+ 整合 v5.5 PetraOrchestratorService dispatch（注入 + 寫回）+ feature flag `Workflow:UseV5Memory` default false 守 fallback
- ❌ 不做：vector index（PostgreSQL pgvector / Mem0/Zep dependency 引入 — 留 Phase 3 升級評估）/ LLM-based compact（複雜 summarize 邏輯 — 留 Phase 2 Step 4 評估 / Step 3 用 threshold + simple deletion oldest entries 起步）/ 跨 Project shared memory（per-Project 隔離對齊 Stage 67 Talent schema）/ WebUI 編輯（留 Phase 3）

---

## 子項清單

### 1. DB schema + Migration

**新 entity**（對齊 Stage 67 Talent / TalentSkill pattern 風格）：

- **`TaskMemory`**（per-Task 共用層 — Petra dispatch 多 Talent 共看）
  - `Id` Guid PK / `TaskGroupId` Guid FK 到 task_groups / `ProjectId` Guid? nullable / `Key` text 對齊 KV 風格（如 `decision/cody-output-summary` / `context/business-rule`）/ `Content` text / `CreatedByTalent` text / `CreatedAt` / `UpdatedAt`
  - Unique partial index：`(TaskGroupId, Key)` WHERE TaskGroupId IS NOT NULL（對齊 Stage 67 PostgreSQL nullable unique partial index 紀律 — `docs/conventions/ef-core.md` 新段）

- **`TalentMemory`**（per-Talent 私有層 — 個人記憶 / 跨 task 累積）
  - `Id` Guid PK / `TalentId` Guid FK 到 talents / `ProjectId` Guid? nullable（對齊 Talent per-Project 隔離 pattern）/ `Key` text / `Content` text / `Tags` text[]（簡單 keyword search 用）/ `CreatedAt` / `UpdatedAt`
  - Unique partial index：`(TalentId, Key, ProjectId)` WHERE ProjectId IS NOT NULL + `(TalentId, Key)` WHERE ProjectId IS NULL（對齊紀律）

- **Migration `Stage69MemorySchema`**：建 `task_memories` + `talent_memories` 表 + index + FK constraint

### 2. MemoryRepository CRUD + query API

**新檔** `src/AiTeam.Data/Repositories/MemoryRepository.cs` — 對齊既有 `BossInteractionRepository` / `PetraSessionRepository`（Stage 68 改 async）pattern：

- `Task<List<TaskMemory>> GetTaskMemoriesAsync(Guid taskGroupId, CancellationToken ct)` — Petra dispatch 前注入用
- `Task<List<TalentMemory>> GetTalentMemoriesAsync(Guid talentId, Guid? projectId, string[]? tagFilter, CancellationToken ct)` — Talent prompt 前注入用
- `Task<TaskMemory> AppendTaskMemoryAsync(...)` / `Task<TalentMemory> AppendTalentMemoryAsync(...)` — 寫入用（caller SaveChanges）
- `Task<int> CompactTaskMemoryAsync(Guid taskGroupId, int targetCount, CancellationToken ct)` — Token budget compact（threshold-based delete oldest）
- `Task<int> CompactTalentMemoryAsync(Guid talentId, Guid? projectId, int targetCount, CancellationToken ct)` — 同上

**Forge spike 必驗**：grep BossInteractionRepository / PetraSessionRepository（Stage 68 async）caller pattern 對齊 — 避免 invent API 風格

### 3. Token budget compact 紀律（threshold-based simple）

**設計**：
- 每個 Memory entry 標記估算 tokens（`Content` length / 4 簡單估）
- 達 60-70% memory size threshold（per-Task / per-Talent 各自）→ Petra orchestrator 觸發 `CompactXxxMemoryAsync` 刪 oldest entries（保留 newest N 條）
- threshold 從 `Workflow:V5MemoryCompactThresholdPercent` config 讀（default 60 / DB SoT 對齊 Stage 47 pattern）
- N entries 保留數從 `Workflow:V5MemoryCompactKeepCount` config 讀（default 50）

**範圍守緊**：不做 LLM-based summarize（複雜 + cost）— 純 delete oldest 起步 / 升級留 Phase 2 Step 4 評估（對齊「漸進 path」精神）

### 4. 整合 v5.5 PetraOrchestratorService dispatch

**修改**：[`PetraOrchestratorService.cs`](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs) 既有 v5.5 path `DispatchTalentsAsync`

- Dispatch 前：query TaskMemory（taskGroupId）+ TalentMemory（picks[i].TalentId, projectId）→ 拼進 Talent input prompt（額外 system context 段）
- Dispatch 後：Talent output 簡單 KV extraction（如 `decision/cody-output-summary = <Cody output 前 500 char>`）→ AppendTaskMemoryAsync 寫回
- Talent 個人記憶（per-Talent）寫回邏輯：Forge spike 拍（候選 A 完成後寫 `last-task-summary` / 候選 B 從 Talent output 解析 `[REMEMBER]` marker 寫入）— 規劃階段拍候選 A 起步 simpler

**Forge spike 必驗**：
- 既有 v5.5 path dispatch wire 細節（PetraOrchestratorService.cs line 60+ / 196 / 265 / 312 / 373 / 455 對齊 Stage 68 async AppendMessageAsync caller）
- Talent input prompt 注入 memory 段位置（vs CLAUDE template / vs user prompt prepend Stage 66 既有 pattern）
- 寫回邏輯落點（dispatch 完成後 vs FinalizeGitAsync 之前）

### 5. Feature flag `Workflow:UseV5Memory` default false

**修改**：[`WorkflowSettings.cs`](src/AiTeam.Bot/Configuration/WorkflowSettings.cs) + [`WorkflowSettingsResolver.cs`](src/AiTeam.Bot/Configuration/WorkflowSettingsResolver.cs) + `appsettings.json`

- `UseV5Memory: bool` default false（守 v5.5 既有 path fallback — Trial_v15 驗 + Christ 拍板才切 default true）
- 必須 `UsePetraOrchestratorV5=true` + `UseTalentSkillSeparation=true` 才有意義（Phase 2 是 Phase 1 之上演進）
- 對齊既有 6 條 framework / v5 / v5.5 flag pattern

### 6. xUnit test 補強

新 4-6 case 對齊 Stage 67 baseline pattern：

- TaskMemory + TalentMemory entity load / write / unique constraint
- MemoryRepository CRUD + query + compact
- PetraOrchestratorService dispatch 整合（v5.5 path + memory flag）
- Feature flag default false 守 v5.5 既有 path 0 regression

### 7. Directory.Build.props v3.58.0 → v3.59.0

---

## 設計決策

1. **跟 framework AgentSession 分層架構**：v5/v5.5 既有 PetraSessionRepository = short-term session 對話 history / 新 TaskMemory + TalentMemory = 跨 session 長期記憶。兩層獨立 entity / 不取代既有
2. **不引入 vector index / Mem0/Zep dependency**（對齊「自己用爽」+ Phase 3 升級評估 path）
3. **per-Task 共用層 + per-Talent 私有層 hybrid**（對齊業界 2026 standard + Christ 設計核心 2026-05-14/15 討論累積）
4. **Token compact 純 threshold-based delete oldest 起步**（不開 LLM-based summarize — 留 Phase 2 Step 4 評估）
5. **per-Project 隔離對齊 Stage 67 Talent schema ProjectId pattern**（nullable + partial unique index 紀律 — 對齊 ef-core.md 新段）
6. **Feature flag `UseV5Memory` default false 守 fallback**（Trial_v15 驗證後 Christ 拍板才切）
7. **Talent 個人記憶寫回候選 A 起步**（Petra 完成後寫 `last-task-summary` simpler / 候選 B `[REMEMBER]` marker 留升級評估）

---

## 驗收情境

> 對齊 workflow_aria.md 第三節 A 計劃書格式硬規則：本節獨立列驗收場景 + 每個都具體到「怎麼觸發 + 怎麼驗證」。

### 場景 A：TaskMemory + TalentMemory schema 落地 + partial unique index（DB 驗）

**觸發**：Migration 跑完 + dotnet test PetraOrchestratorServiceTests + 新 MemoryRepositoryTests

**驗證**：
- SQL `\d task_memories` + `\d talent_memories` 表 + index 落地對齊設計（partial unique index `WHERE ProjectId IS NOT NULL` + `IS NULL` 兩條）
- Migration apply 0 error / 對齊 Stage 67 紀律（per-row SaveChanges + DbUpdateException catch — 雖然 Stage 69 0 seed 但 schema 設計對齊）
- xUnit test PASS

### 場景 B：MemoryRepository CRUD + compact 純 Mock 驗（Repository 層驗）

**觸發**：xUnit Test 新增 case — InMemory EF Core 注入 + 寫入 100 條 TaskMemory + threshold 觸發 compact

**驗證**：
- `GetTaskMemoriesAsync` 返回 query 結果對齊
- `CompactTaskMemoryAsync(targetCount: 50)` 後 SQL 真實只剩 50 條（newest 50）+ DB 0 orphan / 0 FK constraint violation
- async pattern 對齊（CT 透傳 / Task return）

### 場景 C：v5.5 dispatch 整合 + memory flag=true 真實生效（核心驗）

**觸發**：
- DB flag `Workflow:UseV5Memory=true` + UseTalentSkillSeparation=true + UsePetraOrchestratorV5=true
- /mock framework_pipeline 跑 → Petra v5.5 path dispatch Cody → Vera

**驗證**：
- Bot log `Petra v5.5 dispatch 注入 memory taskMemoryCount=N talentMemoryCount=M`（首次 N=0 / M=0 — baseline 空）
- Bot log `Petra v5.5 dispatch 完成寫回 TaskMemory key=decision/cody-output-summary`（dispatch 後寫回）
- SQL `SELECT COUNT(*) FROM task_memories WHERE TaskGroupId = '<新 group>'` ≥ 2（Cody + Vera 各寫一條）
- SQL `SELECT COUNT(*) FROM talent_memories WHERE TalentId = '<Cody Id>'` ≥ 1（Cody last-task-summary 寫回）

### 場景 D：第二次跑相同 Task 注入 memory（持久記憶生效驗）

**觸發**：場景 C 跑完後第二次 /mock framework_pipeline 同 task type → Petra dispatch

**驗證**：
- Bot log `Petra v5.5 dispatch 注入 memory taskMemoryCount=2+ talentMemoryCount=1+`（第二次 N+M > 0）
- Cody input prompt 含「## Talent 個人記憶 / ## Task 共用 context」段（grep Bot log dispatched prompt content）
- Talent 行為差異對照（首次 vs 第二次 — 業務級驗 / 可能 Mock 看不出來 / 主要靠 Trial_v15 真實任務驗對照組）

### 場景 E：feature flag default false 守 v5.5 既有 path 0 regression（保護驗）

**觸發**：
- DB flag `Workflow:UseV5Memory=false`（default）+ UseTalentSkillSeparation=true + UsePetraOrchestratorV5=true
- /mock framework_pipeline 跑

**驗證**：
- Bot log 0 含「Petra v5.5 dispatch 注入 memory」字樣
- v5.5 path dispatch 行為跟 Stage 68 完全一致（PR 真開 + 0 stale exec_confirm 卡）
- SQL `SELECT COUNT(*) FROM task_memories` 維持 0 / `talent_memories` 維持 0
- dotnet test PetraOrchestratorServiceTests v5.5 case 全 PASS（既有 17 PASS regression baseline）

### 場景 F：v4 既有 production path 0 regression（守護驗）

**觸發**：
- DB flag UsePetraOrchestratorV5=false（v4 path）+ UseTalentSkillSeparation=false + UseV5Memory=false
- /mock framework_kickoff_happy 跑

**驗證**：v4 path 完整跑通 0 regression / dotnet test 178+ PASS

### 場景 G：Token budget compact 真實觸發（threshold 驗）

**觸發**：
- Mock 寫入 100 條 TaskMemory 進同 TaskGroup（透過 xUnit test 或 Internal API helper）
- threshold config = 60% / keep count = 50

**驗證**：
- Petra dispatch 前自動觸發 `CompactTaskMemoryAsync(targetCount: 50)`
- SQL 真實剩 50 條（newest）
- Bot log `Memory compact 觸發 TaskGroupId=... beforeCount=100 afterCount=50`

---

## 技術約束

- 環境細節 source of truth 對齊 workflow_aria.md 第三節 A 第 7 條紀律
- partial unique index 設計對齊 [`docs/conventions/ef-core.md`](docs/conventions/ef-core.md) Stage 68 新段（PostgreSQL NULL ≠ NULL 紀律）
- Migration 紀律：`dotnet ef migrations add Stage69MemorySchema --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext`
- 規模 M-L（2 entity + Migration + MemoryRepository + compact 紀律 + dispatch 整合 + feature flag + 6 case test）
- 預估 Forge context mid 600-800K（對齊 Stage 67 ×0.58 架構級重構新區間 — 預估 Forge 真實 350-450K）

---

---

## 實作紀錄（Forge 結案第一段）

### 實作完成項目（依子項）

| # | 子項 | 落地 commit | 狀態 |
|---|---|---|---|
| 1 | TaskMemory + TalentMemory entity + Migration `Stage69MemorySchema` | `45dc15e`（v1.0）+ pivot 進 `89f807a`（v2.1） | ✅ |
| 2 | MemoryRepository CRUD + Upsert + Compact + ProjectId 隔離 | `45dc15e` + signature pivot `89f807a` | ✅ |
| 3 | Token budget compact 紀律（buffer-above-keep 模型 default 50+60%→trigger 80）| `45dc15e` PetraOrchestratorService L500+ | ✅ |
| 4 | 整合 v5.5 PetraOrchestratorService `DispatchTalentsAsync` 注入 + 寫回 | `45dc15e` + v2.1 移除 taskGroupId gate `89f807a` | ✅ |
| 5 | Feature flag `Workflow:UseV5Memory` default false + 2 compact config | `45dc15e` WorkflowSettings + Resolver + appsettings.json | ✅ |
| 6 | xUnit Test 18-22（5 case）| `45dc15e` + v2.1 setup rename `89f807a` | ✅ |
| 7 | Directory.Build.props v3.58.0 → v3.59.0 | `45dc15e` | ✅ |
| **v2.1 補** | TaskMemory scope pivot `TaskGroupId` → `PetraSessionId` | Migration `Stage69PivotTaskMemoryToSession` + Entity/DbContext/Repository/Orchestrator/Test 全層 pivot `89f807a` | ✅ |

### 關鍵設計決策（含 Forge spike 5 點全規劃書放行通過）

1. **Append = Upsert by Key**：解 Aria Roadmap「Append + unique index」邏輯矛盾。Schema 層由 unique 保護，Repository 內 FirstOrDefault → 更新或 insert。caller 想保留 history → key 自加 round/timestamp 後綴。
2. **Compact threshold = buffer-above-keep 模型**：`count >= KeepCount * (100 + ThresholdPercent) / 100`。Default 50 + 60% → trigger 80 → 削回 50（delete 30 oldest）。對 ThresholdPercent 命名語意自洽。
3. **寫回落點 = DispatchTalentsAsync 每 talent 後立刻 upsert**（同 transaction），不用 FinalizeGitAsync 之後 — 對齊 Stage 66 PetraSessionMessages 寫法 / 失敗 escalate path 也有部分 memory 保留。
4. **Cody 個人記憶寫回 candidate A**：`key=last-task-summary` / content=output 前 500 字元 / upsert 每次蓋舊。Candidate B（`[REMEMBER]` marker parser）留 Phase 2 Step 4 評估。
5. **talentNameToIdMap 一次 batch query**：解 ITalent 不 expose Id 真實議題。v5.5 baseline ProjectId=null 全域 → `Talents.Where(names.Contains(Name) && ProjectId == null).ToDictionary` 一次 query。Phase 3 per-Project Talent 加入時擴展（已加 comment 標明）。

### 驗收後修正（v2.0 → v2.1，2026-05-16）

**Forge 自驗 Phase 2 揭真實 root cause**：
- `PetraOrchestratorService.cs:50` comment「spike forward path 無 TaskGroup」+ `CeoAgentService.cs:104` hardcoded `taskGroupId: null`
- v1.0 `TaskMemory.TaskGroupId = required FK` + `memoryEnabled` gate 守 `taskGroupId is not null`
- → current production CEO flow 永遠 SKIP memory 路徑 = **0 production effect**

**Aria 重新評估後拍板修法 = scope pivot 到 PetraSession**（不對齊 v4 TaskGroup 容器）：
- 對齊 v5.5「每次 CEO 觸發 = 一個 PetraSession = 一個 Task event」設計精神
- task_memories 0 row = 無痛改窗口 / 不用 data migration

**v2.1 修改 6 子項全落地**（commit `89f807a`）：
1. Entity `TaskMemory.TaskGroupId` → `PetraSessionId`（required FK 到 petra_sessions / cascade）
2. AppDbContext `HasOne<PetraSession>()` + index 改 `(PetraSessionId, Key)` unique + `(PetraSessionId, CreatedAt)` 排序
3. 新 Migration `Stage69PivotTaskMemoryToSession`（drop old FK → rename column → rename indexes → add new FK）
4. MemoryRepository 5 method signature 全改 `petraSessionId`
5. PetraOrchestratorService `DispatchTalentsAsync` 簽名改 `Guid sessionId` 非 nullable + 移除 `taskGroupId is not null` gate + caller 傳 `session.Id`
6. Test 19/20/22 setup rename + assertion 對齊（Test 18/21 無關不動）

### Mock 覆蓋情況

| 場景 | Mock 覆蓋狀態 |
|---|---|
| A schema 落地 + partial unique index | ✅ Production schema 真實驗（PostgreSQL `\d task_memories` + `\d talent_memories`）|
| B Repository CRUD + Compact + ProjectId 隔離 | ✅ xUnit Test 19/20/21/22（InMemory provider）|
| E flag default false 守 fallback | ✅ xUnit Test 18 + Production DB 0 row default + Bot 啟動 0 error |
| F v4 既有 production path 0 regression | ✅ dotnet test 全 solution 183 PASS（Bot.Tests 56 + Tests.Generated 127 對齊 baseline 178+5）|
| **Production schema FK / unique / cascade 真實 enforce** | ✅ 自驗用 SQL inject 真實驗（FK violation 爆 / unique duplicate 爆 / cascade delete 自動清 0 orphan）|
| **Flag wire probe v2.1** | ✅ UseV5Memory=true SET → reload-cache → 0 error → reset 乾淨 |
| C v5.5 dispatch + memory inject 真實生效 | ⏸ 留 **Trial_v15**（真實 LLM API cost / 對齊 Stage 63B Test 11 等先例「真實 LLM dispatch path 驗留 Trial_vN」紀律）|
| D 第二次跑同 task 注入 memory | ⏸ 留 Trial_v15 |
| G Compact threshold 真實觸發 | ⏸ 留 Trial_v15 |

### 踩坑紀錄

**v2.1 scope pivot 修根因**（最大踩坑 — Aria 規劃漏掃 v5.5 path source of truth）：
- 原 v1.0 設計 `TaskMemory.TaskGroupId` required FK 抄了 v4 TaskGroup pattern，忽略 v5.5 CEO 入口 hardcoded `taskGroupId: null` 的事實
- Forge 自驗 Phase 2 揭：「整合層 0 production effect」— 場景 C/D/G 用任何方式都無法 fire memory path
- 修根因紀律延伸：規劃前掃 source of truth 對齊（`PetraOrchestratorService.cs:50` comment + `CeoAgentService.cs:104` hardcoded 默認值）

**自驗 SOP「先 push 才 self-verify」紀律**：
- 第一次（v1.0）push 後 Forge 自驗才揭 production gap → escalate → Aria 拍板修法 → v2.1 push 後 Christ 再次觸發自驗
- 兩階段 push + 自驗節奏對齊「Christ 觸發後才進自驗」紀律（2026-05-04 校正）

**0 row 無痛改窗口**：
- v2.1 schema pivot 因 task_memories 真實 0 row → Migration 純 rename column / drop+add FK / rename indexes，不用 data migration
- 「驗收後立即補修」相比「等 production 累積 memory 再改」cost 差距巨大

### 驗收狀態

- ✅ dotnet build / dotnet test 183 PASS（Bot.Tests 56 + Tests.Generated 127）
- ✅ CI/CD self-hosted runner 部署 commit `89f807a` success
- ✅ Migration `Stage69PivotTaskMemoryToSession` apply
- ✅ Production schema FK / unique / cascade 真實 enforce 驗通
- ✅ Forge 自驗物理範疇內全綠
- ⏸ Trial_v15 真實 LLM dispatch fire（場景 C/D/G）— 沿用 Trial_v6-v14 同 prompt 模板 + 第二次跑同 task 對照組

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-16 | 規劃書建立（Aria）— Stage 69 = v5.5 Phase 2 第一步「跨 session 長期持久記憶」基底（vs framework AgentSession short-term session 對話 history 分層架構）。**計劃前 WebSearch 結論**：① framework `AgentSession` + `AIContextProvider` 已內建但是 short-term 角色重疊 PetraSessionRepository / 不 cover 跨 session 長期記憶 ② 業界 2026 standard = Hybrid memory 三層 + per-task per-agent scoping + Mem0/Zep/Letta production frameworks / AiTeam 對齊精神「自己用爽」用 PostgreSQL self-built schema 不引入 dependency。**7 子項**：① TaskMemory + TalentMemory entity + Migration `Stage69MemorySchema`（per-Task 共用 + per-Talent 私有 hybrid 雙層 / 對齊 Stage 67 ProjectId nullable + partial unique index 紀律）② MemoryRepository CRUD + query + compact API（async pattern 對齊 Stage 68 PetraSessionRepository）③ Token budget compact 純 threshold-based delete oldest 起步（60% threshold / keep 50 entries / 不開 LLM summarize 留 Phase 2 Step 4 評估）④ 整合 v5.5 PetraOrchestratorService dispatch（注入 + 寫回 / Talent 個人記憶候選 A `last-task-summary` 起步）⑤ Feature flag `Workflow:UseV5Memory` default false 守 fallback ⑥ xUnit test 4-6 case ⑦ Directory.Build.props v3.59.0 bump。**7 驗收場景**：A schema 落地 + partial unique index / B Repository CRUD + compact Mock / C v5.5 dispatch 整合 memory flag=true 核心驗 / D 第二次跑同 task 注入 memory 持久驗 / E feature flag false 守 v5.5 既有 0 regression / F v4 既有 production path 0 regression / G compact threshold 觸發。**範圍邊界刻意收緊**：不開 vector index / 不引入 Mem0/Zep dependency / 不做 LLM-based summarize / 不開跨 Project shared memory / 不開 WebUI 編輯（全留 Phase 2 Step 4-5 + Phase 3 評估）。**Trial_v15 啟動條件**：Stage 69 Mock 全綠 + Aria gate1 通過 → 沿用 Trial_v6-v14 同 prompt + 第二次跑同 task 對照組驗 memory 行為差異 → 通過 → Christ 拍板切 `Workflow:UseV5Memory` default true = v5.5 Phase 2 Step 3 完整收口 → 進 Step 4 Petra 拆解指令精準度 spike。**規模 M-L 預估 Forge context mid 600-800K**（對齊 Stage 67 ×0.58 架構級重構新區間 → 預估真實 350-450K）。 |
| v2.0 | 2026-05-16 | **Forge 結案第一段** — 實作紀錄章節落地（7 子項 + v2.1 補修 1 子項全完成 / 5 Forge spike 規劃書放行通過 / Mock 覆蓋場景 A/B/E/F + 自驗 SOP 真實 schema enforce 驗 + flag wire probe 全綠 / 場景 C/D/G 真實 LLM dispatch fire 留 Trial_v15）。狀態升級 `規劃中` → `✅ 已完成（2026-05-16）`。CI/CD deploy commit `89f807a` success / dotnet test 183 PASS / Migration `Stage69MemorySchema` + `Stage69PivotTaskMemoryToSession` apply。Aria 接手第二段做 CHANGELOG v3.59.0 + Future_Feature 同步。 |
| v2.1 | 2026-05-16 | **驗收後修正 — Aria 規劃漏掃 v5.5 path source of truth 修根因**。Forge 自驗 Phase 2 揭：`PetraOrchestratorService.cs:50` comment「spike forward path 無 TaskGroup」+ `CeoAgentService.cs:104` hardcoded `taskGroupId: null` — v5.5 path 設計上**刻意不建 v4 workflow 容器**（dynamic orchestrator 取代 hierarchical static）。v1.0 設計 TaskMemory.TaskGroupId = required FK → memoryEnabled gate 守 `taskGroupId is not null`，當前 production CEO flow 永遠 SKIP memory 路徑 = 0 production effect。Aria 重新評估後拍板**正確修法 = scope pivot 到 PetraSession**（對齊 v5.5「每次 CEO 觸發 = 一個 PetraSession = 一個 Task event」設計精神 / task_memories 0 row = 無痛改窗口 / 不用 data migration）。**v2.1 修改清單（Stage 69 內補 1 子項）**：① `TaskMemory.TaskGroupId` → `TaskMemory.PetraSessionId`（required FK 到 petra_sessions / cascade delete）② AppDbContext Fluent config 對應改（HasOne PetraSession / index 改 `(PetraSessionId, Key)` + `(PetraSessionId, CreatedAt)`）③ 新 Migration `Stage69PivotTaskMemoryToSession`（drop old FK + rename column + rename indexes + add new FK 對齊 0 row 無 data 損失）④ MemoryRepository 5 method signature 改 `petraSessionId` ⑤ PetraOrchestratorService `DispatchTalentsAsync` 參數改 `Guid sessionId`（非 nullable）+ 移除 `taskGroupId is not null` gate（改 sessionId 必觸發）+ StartAsync 傳 `session.Id` ⑥ Test 18-22 對應 setup rename + assertion 對齊（Test 21 TalentMemory 無關不動）。**戰略意義**：場景 C/D/G 改用 sessionId 後 production 真實生效 ✅（不再 0 effect）— Trial_v15 真實任務直接看 memory 行為差異對照組。Aria 自省點候選：規劃前掃 source of truth 紀律延伸到 v5.5 path comment + hardcoded 默認值（vs Stage 67 v5.5 既有 entry CEO routing 路線）。 |
