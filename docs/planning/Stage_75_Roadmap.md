# Stage 75 Roadmap — v5.5 Phase 3 兩層 queue 配套：Petra 接收層 + Worker 執行層 per-Talent 1 task at a time

> 目標版本：**v3.65.0**（minor — v5.5 Phase 3 第三步 / 大規模架構級重構：Petra 接收層 queue + Worker 執行層 per-Talent serialization + UX status 顯示）
> 狀態：📝 規劃中
> 文件版本：v1.0
> 範圍：PetraInbox table + Migration + PetraInboxProcessor BackgroundService + CeoAgentService 寫 inbox path + per-Talent serialization lock + Dashboard UX status + Discord ACK + xUnit + Directory.Build.props bump
> 規模：M+（對齊 Stage 73+74 真實落點 baseline / 大規模架構級重構新類型第 3 資料點候選）
> 對應 v5.5 規劃：[Future_Feature_v5.5.md](Future_Feature_v5.5.md) Phase 3（既有 doc 寫 Step 9 對應 Stage 76 — Christ 2026-05-17 拍板「數字順序執行」/ Step 9 範圍 → Stage 75 / Step 6 範圍 → Stage 76 WebUI 最後做 / 結案第二段順手 update doc）

---

## 戰略脈絡

**Trial_v20 🟡 部分過 + Stage 74 收口拍板閘門通過後 Phase 3 第三步開跑** — 對齊 Christ「Agent 像人類處理事件」精神（真實 PM 接收並行 / 執行管理 / 手上多 task 但同時間深度做 1 個）+ 業界 70% production Orchestrator-Worker pattern 對齊。

### 計劃前 WebSearch 結論（業界 reference — Forge 起草時 reference 用 / 不重複觸發）

**業界 2026 finding 完全支持「兩層 queue + per-agent serialization」設計**：

1. **Message queue scale better than shared state** — 對齊「event-driven decoupled orchestration」精神（[6 Multi-Agent Orchestration Patterns for Production — Beam.ai](https://beam.ai/agentic-insights/multi-agent-orchestration-patterns-production)）
2. **1 session 1 task >> multi-task session**（議題 1 拍板路線）— shared state 業界揭規模化後變 bottleneck（[Multi-Agent Orchestration Patterns — MindStudio](https://www.mindstudio.ai/blog/multi-agent-orchestration-patterns)）
3. **PostgreSQL Advisory Lock 對 worker serialization 是業界經典 pattern** —「`pg_try_advisory_xact_lock` + `SKIP LOCKED` 是 extremely efficient, contention-free job queue」/「in-memory + atomic + cleaner faster than table flags」（[PostgreSQL Advisory Locks explained — flaviodelgrosso](https://flaviodelgrosso.com/blog/postgresql-advisory-locks) + [Advisory Lock Concurrency Control — Medium](https://medium.com/@krsingh081206/postgresql-advisory-lock-concurrency-control-at-application-level-ab7324a2986f)）
4. **40% multi-agent pilots fail within 6 months because teams pick wrong orchestration pattern** — Stage 75 採業界經典 pattern 避雷
5. **單 PostgreSQL instance 場景**：Advisory lock 限制不影響（AiTeam 單 Bot instance + 單 PG / 不需 Redis/etcd distributed lock）

### Christ 戰略 question 點破史（規劃前置）

- **議題 1 拍板路線 🥇**：Petra 多 task 接收 → **1 session 1 task / multi-session 並存**（既有 PetraOrchestratorService Scoped + PetraSession 紀律保留 / Petra Singleton 派工新 Scoped instance 處理每個 task）
- **議題 2 修正 ⚠️**：per-Talent 鎖 → 之前 Aria 推「talents 表加 IsExecuting flag」/ WebSearch 揭業界揭「table row flag 次優 / lock contention 雷」→ **修正為 PostgreSQL Advisory Lock**（不擾 schema / 業界經典 pattern）

### debate / WebUI 撤回紀錄

- ~~3 agent debate 機制~~ — Stage 74 已撤回 / 留 Phase 4 候選
- ~~Cora Talent~~ — Stage 74 已撤回 / Petra 內建職責
- ~~Stage 75 WebUI Talent CRUD~~ — Christ 2026-05-17 拍板「數字順序執行」/ WebUI 改 Stage 76 最後做（對齊既有「核心功能完成且可運作前 WebUI 排最後」紀律）

### 範圍邊界刻意收緊

- ✅ 做：
  - **Layer 1 Petra 接收層 queue**：PetraInbox 新 table + Migration + PetraInboxProcessor BackgroundService（polling pending → invoke PetraOrchestratorService per row）
  - **Layer 2 Worker 執行層 per-Talent serialization**：PostgreSQL Advisory Lock（`pg_try_advisory_xact_lock(hashtext('talent:' || talent_id))` 拿鎖 — 同 talent_id 多 task 等鎖 / 不同 talent_id 平行 OK）
  - CeoAgentService.StartAsync 改成「寫 PetraInbox row + return ack」非「直接 await petraOrchestrator.StartAsync」
  - Dashboard UX status 顯示（InteractionCenter 加「task 排隊狀態」/ TaskCenter 加 queue position）
  - Discord ACK 訊息（接收層 queue 寫入後 immediate ack「task accepted / 排隊位 N」）
  - xUnit 5-7 case
  - Directory.Build.props v3.64.0 → v3.65.0

- ❌ 不做：
  - **task 取消機制**（Phase 4 候選 / 對齊 Christ「自己用爽 / 不過早 over-engineer」精神 — 真實場景 task 一旦送出基本不取消）
  - **queue priority / preemption**（Phase 4 候選 / FIFO 簡單對齊「自己用爽」）
  - **WebUI Talent CRUD**（Stage 76 範圍）
  - **3 agent debate / Cora**（Stage 74 已撤回）
  - **multi-task session 改寫**（議題 1 拍板路線 🥇 1 session 1 task / 既有 PetraSession 紀律保留）
  - **per-Bot instance 分散式鎖**（AiTeam 單 Bot / 不需 Redis/etcd / Advisory Lock 單 PG 足夠）

---

## 子項清單

### 1. PetraInbox table + Migration（Layer 1 接收層 queue）

**新檔** [`Entities.cs`](src/AiTeam.Data/Entities.cs) 加 entity record：

- **`PetraInbox`**（接收層 queue / 對齊 v4 既有 `AgentQueueProcessor` DB-as-Queue pattern）：
  - `Id` (Guid)
  - `UserInput` (text, required) — Christ 送的 task description
  - `Source` (text, required) — 「discord」/「dashboard」(對齊既有 CeoAgentService 來源紀律)
  - `Status` (text, required) — 「pending」/「running」/「completed」/「failed」
  - `PetraSessionId` (Guid?, nullable FK) — 對應 PetraSession（accept 後 Petra 處理時設）
  - `EnqueuedAt` (DateTime, required)
  - `StartedAt` (DateTime?, nullable)
  - `CompletedAt` (DateTime?, nullable)
  - `ErrorMessage` (text?, nullable)

**對齊既有 pattern**：
- Stage 27 v4 既有 `agent_queues` 表 DB-as-Queue 紀律
- Stage 67 talent_skills race-safe seed pattern
- Stage 72 PromptResolver 5-min TTL cache pattern（不適用此表 / 表本身就是 polling 目標）

**index**：`Status + EnqueuedAt`（polling pending 排序用 / FIFO 紀律）

### 2. PetraInboxProcessor BackgroundService（Layer 1 polling 派工）

**新檔** [`PetraInboxProcessor.cs`](src/AiTeam.Bot/Orchestration/Petra/PetraInboxProcessor.cs)（對齊既有 [`AgentQueueProcessor`](src/AiTeam.Bot/Orchestration/AgentQueueProcessor.cs) + [`InteractionProcessor`](src/AiTeam.Bot/Orchestration/InteractionProcessor.cs) BackgroundService pattern）：

**設計**：
- BackgroundService / 每 N 秒 polling `petra_inbox WHERE Status='pending' ORDER BY EnqueuedAt ASC` 第一筆
- 找到 pending row → 切 Status='running' + StartedAt=UtcNow → `IServiceScopeFactory.CreateScope()` 開新 PetraOrchestratorService Scoped instance → `petraOrchestrator.StartAsync(taskGroupId: null, row.UserInput, ct)`
- 完成 → 切 Status='completed' + CompletedAt + 寫 PetraSessionId（從 PetraOrchestratorService 返回）
- 失敗 → 切 Status='failed' + ErrorMessage（取 exception.Message）
- **議題 1 拍板實踐**：每 row 開新 PetraOrchestratorService Scoped instance / 多 task 並存 / multi-session 並行 OK
- polling 間隔：3-5 秒（對齊既有 InteractionProcessor / AgentQueueProcessor 既有 polling 紀律）

**DI 註冊** [`Program.cs`](src/AiTeam.Bot/Program.cs)：`AddHostedService<PetraInboxProcessor>()`

### 3. CeoAgentService 改寫成「寫 inbox + ack」（Layer 1 入口轉接）

**修改** [`CeoAgentService.cs`](src/AiTeam.Bot/Agents/CeoAgentService.cs) v5.5 flag forward path（既有 line 104 `petraOrchestrator.StartAsync(taskGroupId: null, userInput, cancellationToken)`）：

**改前**（直接 await Petra）：
```
flag=true → directly await petraOrchestrator.StartAsync(...) → return petraResult
```

**改後**（寫 inbox + 立即 ack）：
```
flag=true → DbContext.PetraInbox.Add(new PetraInbox { UserInput, Source="dashboard|discord", Status="pending", EnqueuedAt=UtcNow })
         → SaveChangesAsync()
         → 計算當前 queue position（同 Source 內 pending count + 1）
         → return ack message「task 已接收 / 排隊位 N」+ inbox row Id
         → PetraInboxProcessor 背景 polling 接手
```

**Discord ACK 訊息**：對齊既有 [`CeoCommandController.cs:95`](src/AiTeam.Bot/Api/CeoCommandController.cs) 「指令已送達」精神 / 升級為「task 已接收 + 排隊位 N」

### 4. PostgreSQL Advisory Lock per-Talent serialization（Layer 2 執行層鎖）

**修改** [`PetraOrchestratorService.cs`](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs) `DispatchTalentsAsync`（既有 line 532+ Stage 74 DAG fan-out 改寫後）：

**設計**：
- 每個 Worker dispatch（既有 `talentAgents[i].RunAsync(...)` call）前 → 開新 DbContext transaction → `pg_try_advisory_xact_lock(hashtext('talent:' || talentId::text))` 取鎖
- 鎖取到 → 進行 LLM dispatch / 完成後 transaction commit（auto-release 鎖）
- 鎖取不到（同 talent_id 已有 task 在跑） → loop wait（每 N 秒 retry / 對齊「Cody 跑完 task A 才接 task B」精神）
- **per-Talent serialization 紀律**：同 talent_id 多 task 序列化 / 不同 talent_id 平行 OK / 對齊 v5.5 horizontal scaling 未來 Cody-1 + Cody-2 平行設計

**設計決策**：
- transaction-level lock auto-release（不需要 explicit unlock / Talent 跑完 task 自然釋放）
- 對齊 Stage 74 路線 A 紀律（LLM dispatch transaction 跨 10+ 秒 → DB connection pool 評估 — 但業界 advisory lock 設計就是 cover 這場景 / 對齊 [Advisory Lock Concurrency Control](https://medium.com/@krsingh081206/postgresql-advisory-lock-concurrency-control-at-application-level-ab7324a2986f)）

> **議題待 Christ 拍板**：advisory lock transaction 跨 LLM dispatch（10+ 秒）開太久 → DB connection pool 雷評估。3 路線：① 全 advisory lock（業界經典 / 對 connection pool 開銷評估） ② in-memory SemaphoreSlim(1,1) per talent_id（對齊 v4 既有 AgentQueueProcessor pattern / 單 Bot instance 完全夠 / 不擾 PG connection） ③ 混合（atomic check 用 advisory lock / 跨 LLM dispatch 用 in-memory lock）。**Aria 傾向 ②** 在 Forge plan v2 階段拍板 — AiTeam 單 Bot instance 場景 SemaphoreSlim 足夠 + 對齊既有 v4 AgentQueueProcessor pattern + 0 PG connection pool 雷。

### 5. Dashboard UX status 顯示

**修改** Dashboard 兩處：

#### 5a. InteractionCenter UX status 升級

[`InteractionCenter.razor`](src/AiTeam.Dashboard/Components/Pages/Interactions/InteractionCenter.razor) + [`.cs`](src/AiTeam.Dashboard/Components/Pages/Interactions/InteractionCenter.razor.cs)：
- 既有「任務操作中心」段加「PetraInbox 接收狀態」section
- 顯示當前 pending / running queue（最近 5 筆）
- 含 column：UserInput 摘要（前 80 char）/ Source / Status / EnqueuedAt / queue position
- SignalR live update（對齊既有 InteractionCenter SignalR pattern）

#### 5b. TaskCenter queue position 顯示

[`TaskCenter.razor`](src/AiTeam.Dashboard/Components/Pages/Tasks/TaskCenter.razor) + [`.cs`](src/AiTeam.Dashboard/Components/Pages/Tasks/TaskCenter.razor.cs)：
- 既有 PetraSession 列表加「來源 PetraInbox queue position」column
- 顯示「accepted at queue position N」+「started at」+「completed at」

### 6. PetraInbox 對應 SignalR Hub 通知（既有 pattern 對齊）

對齊既有 `BossInteractionHub` SignalR pattern（如有 — 0 grep verify 必修）：
- PetraInboxProcessor 切 Status 時觸發 SignalR notify
- Dashboard 真實 live update queue status

### 7. xUnit test 補強

新立 [`Stage75InboxQueueTests.cs`](src/AiTeam.Bot.Tests/Orchestration/Stage75InboxQueueTests.cs)（對齊 Stage 74 既有 Stage74TalentSkillModelTests baseline pattern）：

| Test | 對應驗收場景 | 驗證點 |
|---|---|---|
| `T1_PetraInbox_Schema_Baseline` | 場景 A | InMemory DB seed 3 PetraInbox row（Status=pending） / verify schema |
| `T2_PetraInboxProcessor_PicksOldestPendingFirst` | 場景 B | seed 3 row 不同 EnqueuedAt → processor polling 取最早 pending → 切 running |
| `T3_PetraInboxProcessor_ConcurrentPolling_NoDoubleProcess` | 場景 C | 模擬兩個 polling tick 同時跑 → 同 row 0 雙重 process（atomic check `pg_try_advisory_lock` 或 SemaphoreSlim 對齊議題 2 拍板）|
| `T4_CeoAgentService_WritesInboxRow_ImmediateAck` | 場景 D | CeoAgentService.StartAsync 0 await Petra / 寫 PetraInbox row + return ack message 含 queue position |
| `T5_PerTalentSerialization_SameTalentSequential` | 場景 E | 模擬 2 task 同 talent_id 並送 → lock 紀律守 sequential（不論 advisory 或 SemaphoreSlim 議題 2 拍板）|
| `T6_PerTalentSerialization_DifferentTalentsParallel` | 場景 F | 模擬 2 task 不同 talent_id 並送 → 兩 Talent 平行 dispatch（per-Talent 鎖不擋）|
| `T7_PetraInbox_FailedStatus_PreservesErrorMessage` | 場景 G | 模擬 Petra 拋 exception → inbox row Status='failed' + ErrorMessage 真實寫入 |

### 8. Directory.Build.props v3.64.0 → v3.65.0

---

## 設計決策

1. **議題 1 拍板路線 🥇 — 1 session 1 task / multi-session 並存**（既有 PetraOrchestratorService Scoped lifetime 保留 / 每個 PetraInbox row 開新 Scoped instance / 業界 finding 完全支持 event-driven decoupled）
2. **議題 2 修正方向 — PostgreSQL Advisory Lock 或 SemaphoreSlim per-Talent**（Forge plan v2 階段拍板 / Aria 傾向 SemaphoreSlim 對齊 v4 既有 AgentQueueProcessor pattern + 單 Bot instance 場景 / 不擾 PG connection pool）
3. **PetraInbox table 採 v4 既有 DB-as-Queue pattern**（對齊 Stage 27 既有 `agent_queues` 紀律 / BackgroundService polling / 0 in-memory queue 風險）
4. **FIFO 紀律**（EnqueuedAt ASC polling / 不引入 priority / preemption 機制 / 對齊「自己用爽 / 不過早 over-engineer」精神）
5. **CeoAgentService 改寫保留 v4 path 0 動**（既有 v4 flag=false fallback path 不走 PetraInbox / 直接走 v4 既有 AgentQueueProcessor）
6. **backwards-compatible 守護 6 層**：v4 既有 path / v5 既有 path / v5.5 既有 hardcoded fallback / Stage 70 SubtaskPlan 既有 schema / Stage 72+73 PromptResolver + Petra persona prepend / Stage 74 per-Skill Model + DAG fan-out — 全 0 動

---

## 驗收情境

### 場景 A：PetraInbox schema + Migration（xUnit + production DB query）

**觸發**：`dotnet ef database update` apply Migration → SQL `\d petra_inbox` query

**驗證**：
- `petra_inbox` 表 9 欄位（Id / UserInput / Source / Status / PetraSessionId / EnqueuedAt / StartedAt / CompletedAt / ErrorMessage）
- index `Status + EnqueuedAt` 真實存在
- xUnit T1 verify schema baseline

### 場景 B：PetraInboxProcessor FIFO polling（xUnit）

**觸發**：xUnit InMemory DB seed 3 PetraInbox row 不同 EnqueuedAt（10:00 / 10:01 / 10:02 / Status=pending） → 跑 PetraInboxProcessor 一次 polling tick

**驗證**：
- T2：最早 EnqueuedAt（10:00）row 被選 + 切 Status='running' + StartedAt=UtcNow
- 其餘 2 row 仍 Status='pending'

### 場景 C：PetraInboxProcessor 0 雙重 process（xUnit）

**觸發**：xUnit 模擬兩個 polling tick 同時跑同 row

**驗證**：
- T3：同 row 0 雙重 process（atomic check 守 / 對齊議題 2 拍板 — advisory `pg_try_advisory_xact_lock` 或 SemaphoreSlim 任一）

### 場景 D：CeoAgentService 寫 inbox + immediate ack（xUnit + Bot log）

**觸發**：xUnit `petraOrchestrator.StartAsync()` mock + CeoAgentService.StartAsync 真實 call / 或 production curl `/internal/ceo/command`

**驗證**：
- T4：CeoAgentService.StartAsync **0 await Petra** / 真實寫 PetraInbox row 進 DB + return ack message 含 queue position
- production：Bot log 含「PetraInbox 已接收 / queue position=N」訊號（新加 log field）
- 對齊既有「指令已送達」精神升級

### 場景 E：per-Talent serialization 同 Talent 序列化（xUnit + Bot log）

**觸發**：xUnit 模擬 2 task 同時送 + 兩 task plan 都 dispatch Cody（同 talent_id） / 或 production 真實兩 prompt 並送

**驗證**：
- T5：第二個 task 對 Cody 的 dispatch **等鎖**（log 含「PetraOrchestrator v5.5 dispatch talent=Cody waiting for lock」訊號 / 或 SemaphoreSlim 排隊）
- 第一個 task 對 Cody dispatch 完成後 → 鎖釋放 → 第二個 task 對 Cody dispatch 接續
- 同 Talent 同時間最多 1 task 真實實證

### 場景 F：per-Talent serialization 不同 Talent 平行（xUnit + Bot log）

**觸發**：xUnit 模擬 2 task 同時送 + task A dispatch Cody / task B dispatch Vera（不同 talent_id）

**驗證**：
- T6：兩個 dispatch 真實平行（log 兩個 dispatch 訊號間隔 < 100ms）
- 不同 talent_id 鎖不擋 / horizontal scaling 設計實證

### 場景 G：PetraInbox failed status 保留 ErrorMessage（xUnit）

**觸發**：xUnit mock PetraOrchestratorService.StartAsync 拋 Exception → PetraInboxProcessor 接

**驗證**：
- T7：PetraInbox row Status='failed' + ErrorMessage 真實寫入 exception.Message
- 異常不擋後續 polling（其他 pending row 仍正常 process）

### 場景 H：Dashboard UX status 真實 live update（手動驗 / Aria gate2 範圍）

**觸發**：production 連續送 3 task → Dashboard InteractionCenter + TaskCenter 真實 live update

**驗證**：
- InteractionCenter 顯示「task A running / B pending / C pending」+ SignalR live update
- TaskCenter 顯示 queue position
- Discord ACK 真實出現「task 已接收 / 排隊位 N」訊息

### 場景 I：Trial_v21 真實業務驗（多 task 並送場景）

**觸發**：Trial_v21 開跑前手動 SQL 切 `Workflow:UsePetraInboxQueue=true`（如有 feature flag） + 連續快速送 2-3 task

**驗證**：
- 全 task 都正常 process + Petra accept 順序對齊 EnqueuedAt FIFO
- per-Talent 鎖紀律真實生效（log 訊號）
- 業務正確性對齊 Trial_v20 baseline

### 場景 J：v4 既有 path 0 regression

**觸發**：SQL 切 `Workflow:UsePetraOrchestratorV5=false`（v4 path） + 跑 Mock task

**驗證**：
- Bot log 0 含「PetraInbox」字樣 / 走 v4 既有 AgentQueueProcessor
- v4 既有 path 0 動 PetraInbox schema（v4 path 不讀 PetraInbox 表）
- v4 path 既有 baseline 行為 0 改變

---

## 技術約束

- 環境細節 source of truth 對齊 [workflow_aria.md 第三節 A 第 7 條紀律](../../memory/workflow_aria.md)
- PetraInboxProcessor BackgroundService 對齊既有 [`AgentQueueProcessor`](src/AiTeam.Bot/Orchestration/AgentQueueProcessor.cs) + [`InteractionProcessor`](src/AiTeam.Bot/Orchestration/InteractionProcessor.cs) + [`PetraSessionRecoveryService`](src/AiTeam.Bot/Orchestration/Petra/PetraSessionRecoveryService.cs) 4 既有 BackgroundService pattern
- PetraInbox table schema 對齊 Stage 27 既有 `agent_queues` DB-as-Queue pattern + Stage 67 既有 race-safe seed 紀律 + ef-core.md Migration 紀律
- per-Talent serialization 對齊 v4 既有 AgentQueueProcessor SemaphoreSlim(1,1) per executor key pattern（議題 2 拍板候選）或業界 PostgreSQL Advisory Lock pattern
- xUnit test 補強對齊 Stage 73+74 既有 Stage73UpgradeTests / Stage74TalentSkillModelTests baseline pattern
- 對齊 [`docs/conventions/csharp.md`](../conventions/csharp.md) / [`ef-core.md`](../conventions/ef-core.md) / [`refactor-sop.md`](../conventions/refactor-sop.md)
- backwards-compatible 守護 6 層延續

---

## ⚠️ Aria 預警（對齊 Stage 73+74 連續兩 Stage raw 預估嚴重低估教訓 — 自省點 #37 第 3 次累積反向防呆 baseline 對照紀律）

**Stage 75 raw 預估評估**（對齊 calibration_anchors Stage 75 預警紀律）：

- Plan 階段 read existing（CeoAgentService + 既有 4 BackgroundService + Dashboard 2 pages + Discord ACK + PetraOrchestratorService DispatchTalentsAsync）~30-40K
- 機制層 code 改動 ~30-40K（新 entity + Migration + BackgroundService + CeoAgentService 改寫 + per-Talent lock + Dashboard UX status + Discord ACK + DI propagation）
- 既有 method body 改寫 + helper 抽 thinking ~15-20K（CeoAgentService.StartAsync 改寫 + DispatchTalentsAsync per-Worker lock 加）
- xUnit case 起草 ~20-30K（7 case + InMemory DB pattern + SignalR mock + Advisory Lock 模擬）
- Aria 二檢 round-trip buffer ~30-50K（議題 2 拍板可能多輪）
- **raw 估算 ~125-180K × 1.6 = 200-288K 總 context** ⭐

**Model 推薦**：
- 🥇 **Opus 200K + high**（200-288K 接近上限 / 風險明顯）
- 🥈 **Opus 1M + ultrathink**（safety buffer 充裕 / cost +$1-2 / 推薦對齊 Stage 72/74 baseline 教訓）
- ❌ Sonnet 200K + high — **不推**（對齊 Stage 73/74 真實落點 295-336K / 嚴重超 Sonnet limit / 即使 Stage 73/74 沒爆是運氣）

**cost 預估**：**$3-5 per cycle**（升級 Future_Feature_v5.5.md 既有 $2-4 預估 / 對齊 Stage 73/74 真實落點 +25-30%）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-17 | 規劃書建立 — v3.65.0 / M+ 規模 / v5.5 Phase 3 兩層 queue 配套（Christ 2026-05-17 拍板「數字順序執行」/ Stage 75 = 兩層 queue / Stage 76 = WebUI 最後做）。**範圍**：Layer 1 PetraInbox table + Migration + PetraInboxProcessor BackgroundService / Layer 2 per-Talent serialization lock（議題 2 拍板候選：PostgreSQL Advisory Lock vs SemaphoreSlim per-Talent — Aria 傾向 SemaphoreSlim 對齊 v4 既有 AgentQueueProcessor pattern / 單 Bot instance 場景 / 0 PG connection pool 雷）/ CeoAgentService 改寫成「寫 inbox + ack」/ Dashboard UX status（InteractionCenter + TaskCenter）/ Discord ACK 「排隊位 N」/ xUnit 7 case + Directory.Build.props bump。**戰略脈絡**：Trial_v20 🟡 部分過 + Stage 74 收口拍板閘門通過 + Christ「Agent 像人類處理事件」精神（PM 接收並行 / 執行管理）+ 業界 70% production Orchestrator-Worker pattern 對齊 + WebSearch 完全支持（議題 1 1 session 1 task 對齊 message queue scale better / 議題 2 PostgreSQL Advisory Lock 業界經典 worker serialization pattern — 修正 Aria 之前推「table 加 IsExecuting flag」次優判斷）。**核心紀律**：FIFO（EnqueuedAt ASC polling / 不引入 priority preemption）+ per-Talent 鎖（非 per-Skill 鎖 / 對齊 v5.5 horizontal scaling 未來 Cody-1 + Cody-2 平行設計）+ 1 session 1 task multi-session 並存（既有 PetraOrchestratorService Scoped 紀律保留）+ backwards-compatible 守護 6 層延續。**校準錨預期**：對齊 Stage 73/74 連續兩 Stage 教訓（自省點 #37 第 3 次累積反向防呆紀律）— **大規模架構級重構新類型第 3 資料點候選**（raw 125-180K × 1.6 = 200-288K 總 context / Opus 200K + high 或 Opus 1M + ultrathink）/ cost $3-5（升級既有 $2-4 預估）。**驗收**：10 場景 — A schema + Migration / B Processor FIFO polling / C 0 雙重 process / D CeoAgentService 寫 inbox + ack / E 同 Talent serialization / F 不同 Talent 平行 / G failed status ErrorMessage / H Dashboard UX status live update（Aria gate2）/ I **Trial_v21 真實業務驗（多 task 並送）** / J v4 path 0 regression。**下一步**：Forge 實作 + 議題 2 拍板（advisory lock vs SemaphoreSlim）+ Aria gate1 Tier 0+1+Tier 2 #3 build + Trial_v21 真實任務驗 → 通過後 Stage 76 開（WebUI Talent CRUD 最後做）= v5.5 完整收口。**Phase 3 完整收口路徑**：73 ✅ → 74 ✅ → 75（兩層 queue 配套 / 本 Stage）→ 76（WebUI Talent CRUD 最後做）→ v5.5 完整收口。 |
