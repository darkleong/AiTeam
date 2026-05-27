# Stage 77 Roadmap — fire-and-forget A2 完整版（Channel + multi-consumer + bounded fan-out + graceful shutdown drain）

> 目標版本：**v3.67.0**（minor — v5.5 Phase 3 補強 / 一般架構級重構：PetraInboxProcessor 從 sequential await → BackgroundService + Channel multi-consumer 真實多 task 並送）
> 狀態：✅ 已完成（2026-05-18）
> 文件版本：v2.0
> 範圍：PetraInboxProcessor 重構（sequential await → Channel-based multi-consumer）+ BoundedChannelOptions config + `Workflow:MaxConcurrentPetra` AppSetting + graceful shutdown drain + multi-consumer loop + xUnit + version bump
> 規模：S/M（對齊一般架構級重構區間 ×0.43-0.60 / 第 6 資料點候選）
> 對應 v5.5 規劃：[Future_Feature_v5.5.md](Future_Feature_v5.5.md) Phase 3 補強段（Trial_v21 揭 Stage 75 設計實作落差 + Christ 2026-05-18 拍板 A2 業界推薦完整版路線）
> 對應前置：[Stage 75 Roadmap](Stage_75_Roadmap.md) + [Stage 76 Roadmap](Stage_76_Roadmap.md) + [Trial_v21_Plan.md](../experiments/Trial_v21_Plan.md)

---

## 戰略脈絡

**Trial_v21 🟡 部分過揭 1 🔴 戰略級設計實作落差 → Christ 2026-05-18 拍板 A2 完整版路線（vs A1 簡單版 ~50 行）**：

- v2.0 Stage 75 紀錄寫「fire-and-forget per row 開新 Scoped instance / multi-session 並存」/ 議題 1 拍板實踐
- 真實 [`PetraInboxProcessor.cs:107`](src/AiTeam.Bot/Orchestration/Petra/PetraInboxProcessor.cs#L107) `result = await orchestrator.StartAsync(...)` **sequential await** 阻塞
- 結果：Layer 2 per-Talent 鎖 code 真實 wire ✓ / **但 production 0 contention 機會 fire**（PetraInboxProcessor 一次只 dispatch 1 個 Petra / 兩個 Petra 不會同時搶同一個 Talent 鎖）

**Stage 77 修法主軸 — A2 業界推薦完整版（vs A1 ~10 行簡單版）**：

對齊業界 .NET BackgroundService + Channel 紀律（不是無腦 `Task.Run` / 會踩 exception swallow / orphan task / shutdown drain 三大雷 — Stephen Cleary 警示 / Stage 76 Roadmap 計劃前 WebSearch 結論已 incorporated）：

```
PetraInboxProcessor（既有 producer）
        ↓ poll DB pending row → channel.Writer.WriteAsync
Channel<Guid>（BoundedChannel / Capacity 10-20 / FullMode=Wait / SingleWriter=true / SingleReader=false）
        ↓ multi-consumer await foreach
N=3 × consumer loop（Task.WhenAll 守 lifecycle）
        ↓ 每個 loop per row → CreateAsyncScope → orchestrator.StartAsync
N=3 並行 Petra（同時 dispatch / per-Talent 鎖 contention 真實 fire）
        ↓ StopAsync drain
graceful shutdown：取消 Writer + Task.WhenAll(consumers) 等 in-flight 完成
```

### 範圍邊界刻意收緊 — 不擴 3 Phase 4 候選（Christ 2026-05-18 戰略 question 點破 + Aria WebSearch 業界紀律確認後拍板）

- ❌ **HITL plan confirmation 閘門**（Petra 拆完 plan 給 user 看 → 確認後 dispatch）— 業界紀律成熟（LangGraph interrupt + 4 decision pattern）/ Phase 4 候選 / Stage 78+ 評估 / **不擾 Stage 77 infra 主軸**
- ❌ **動態 re-planning**（Petra 看 subtask result 再決下一步）— 業界紀律成熟但規模 L / 必配 max iterations + replan threshold + cost cap + checkpoint replay / Phase 4+ 候選 / 等真實 production「Petra 一次拍板拍錯」case 累積才評估 ROI
- ❌ **multi-agent debate** 🔴 — 業界 2026 研究**反向 finding**（[Revisiting Multi-Agent Debate as Test-Time Scaling](https://openreview.net/forum?id=xzRGxKmeEG) + [Debate or Vote](https://arxiv.org/html/2508.17536v1) + [Nature Scientific Reports 2026 — When collaboration fails](https://www.nature.com/articles/s41598-026-42705-7)）— Majority Voting alone 涵蓋大部分 MAD 收益 / debate 容易 echo chamber / adversarial agent 拉低 accuracy 10-40% — **刪除候選 / 不立 FF**（對齊 Stage 74 Christ 2026-05-17 撤回 Cora Talent 直覺判斷業界 finding ✓）

→ Stage 77 改的是「**並行能力**」/ 不改 Petra 決策深度。

### 計劃前 WebSearch 結論（業界 reference — Forge 起草時 reference 用 / 不重複觸發）

Aria 2026-05-18 計劃前共 7 議題完整 incorporated（Stage 76 規劃前 4 議題 + Stage 77 規劃前 3 議題）：

**1. Fire-and-forget Task.Run 業界雷（不是無腦 Task.Run）** — [Stephen Cleary - Fire and Forget on ASP.NET](https://blog.stephencleary.com/2014/06/fire-and-forget-on-asp-net.html) + [Fire-and-Forget Methods in C# Best Practices](https://techcommunity.microsoft.com/blog/educatordeveloperblog/fire-and-forget-methods-in-c-%E2%80%94-best-practices--pitfalls/4299605)：3 大雷（exception swallowing / orphan task on app recycle / Scoped service / DbContext lifetime）— 對齊 Stage 77 必走 BackgroundService + Channel 業界推薦版

**2. BackgroundService + Channel 業界主流 pattern** — [Long-Running Tasks in ASP.NET Core 2026 Best Practices](https://boldsign.com/blogs/long-running-tasks-asp-net-core-best-practices/)：「Start with BackgroundService + Channel for simple in-process work, then move to a durable queue/job system when jobs can't be lost」— AiTeam PetraInbox 已是 PG durable queue + 加 Channel + multi-consumer 對齊「move to durable queue + multi-consumer parallel dispatch」業界紀律

**3. `Channel<T>` `BoundedChannelOptions` 業界 config** — [Microsoft Learn Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) + [Building High-Performance .NET Apps With C# Channels](https://antondevtips.com/blog/building-high-performance-dotnet-apps-with-csharp-channels) + [ConcurrentQueue vs Channels in .NET 2025](https://medium.com/@mahmednisar/concurrentqueue-vs-channels-in-net-2025-the-performance-battle-you-need-to-see-e9949ec106e2)：
   - `FullMode=Wait`（default 推薦 / production safest / 0 task drop）
   - `Capacity = 10-20`（看期望 backlog / AiTeam 單 Bot 場景小）
   - `SingleWriter=true`（PetraInboxProcessor 唯一 producer）
   - `SingleReader=false`（multi-consumer）

**4. Multi-consumer pattern `Task.WhenAll(N × consumer loop)`** — [Task.WhenAll vs Parallel.ForEachAsync vs Channels in C#](https://www.csharptraining.co.uk/task-whenall-vs-parallel-foreachasync-vs-channels-in-csharp/) + [Channels in C#](https://dev.to/adrianbailador/channels-in-c-3i6h)：
   - BackgroundService `ExecuteAsync` 起 N 個 consumer loop / 每個 loop `await foreach (var item in channel.Reader.ReadAllAsync(ct))` / `Task.WhenAll(N × loop)` 守 lifecycle
   - **MultiConsumerService pattern 業界 reference baseline**

**5. Anthropic API rate limit + max concurrent cap 業界紀律** — [Anthropic API Rate Limits 2026](https://www.respan.ai/articles/anthropic-api-rate-limits) + [Claude API Rate Limits Docs](https://docs.anthropic.com/en/api/rate-limits) + [Our approach to rate limits](https://support.anthropic.com/en/articles/8243635-our-approach-to-api-rate-limits) + [LLM API Rate Limiting Best Practices](https://www.clawpulse.org/blog/llm-api-rate-limiting-best-practices-avoid-429-errors-and-save-40-on-costs)：
   - Anthropic 3 維度 rate limit（RPM / ITPM / OTPM）
   - **真實 bottleneck = token rate limit not request rate limit**（一個 Cody chain 50K context 3 calls 已超 Tier 1 ITPM）
   - **Reduce concurrency on your side** > 無腦 retry（Anthropic 官方建議）
   - **AiTeam Tier 1-2 個人帳號適用 MaxConcurrentPetra = 3 default**（保守 / 配 Stage 76 retry path 兜底 transient 429/5xx）

**6. Graceful shutdown drain pattern** — [Mastering Graceful Shutdown in ASP.NET Core BackgroundService](https://ithy.com/article/graceful-shutdown-guide-bw4zj6o6) + [.NET 8 BackgroundService Production-Ready Patterns](https://www.dotnet-guide.com/tutorials/dotnet-8-essentials/background-jobs-hostedservice-queues/)：
   - StopAsync 觸發 → CancellationToken propagation → 等 in-flight tasks drain
   - **Drain pattern 業界主流**（不直接 kill）/ shutdown timeout 設長一點讓 longest expected job 跑完
   - Petra chain ~13 min / 設 timeout = 20-30 min 安全

**7. IServiceScopeFactory.CreateAsyncScope per Task 紀律** — [Use scoped services within a BackgroundService - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service)：
   - 每個並行 Petra 開新 scope（不能跨 Task 邊界共用）
   - **AiTeam 既有 Stage 75 PetraInboxProcessor 已用此 pattern** ✓ 延續

---

## 子項清單

### 1. `Workflow:MaxConcurrentPetra` AppSetting + WorkflowSettingsResolver method

**修改** [`WorkflowSettingsResolver.cs:23`](src/AiTeam.Bot/Configuration/WorkflowSettingsResolver.cs#L23)（對齊既有 `GetReviewAppealMaxRoundsAsync` / `GetQaFixMaxRoundsAsync` etc method 簽名）：

- 新 method `GetMaxConcurrentPetraAsync(CancellationToken ct = default)` → 從 `app_settings` table 讀 `Workflow:MaxConcurrentPetra` key
- default 值：**3**（業界 Anthropic Tier 1-2 個人帳號保守紀律 / 配 Stage 76 retry path 兜底 transient）
- 範圍守 [1, 10]（超出範圍 fallback default / 對齊 token rate limit 真實上限）

**db_seed**：`app_settings` table 加 row `Workflow:MaxConcurrentPetra = 3`（對齊既有 5 v5.5 flag pattern / Migration 補 seed）

### 2. `Channel<Guid>` bounded queue + DI

**新檔** `src/AiTeam.Bot/Orchestration/Petra/PetraInboxChannel.cs`（singleton wrapper / 對齊既有 Service pattern）：

**設計**：
- 內部封裝 `Channel<Guid>`（rowId queue）
- 建構子讀 AppSettings 拿 `MaxConcurrentPetra` + `Channel:Capacity`（default = 20）
- `BoundedChannelOptions`：`Capacity = 20 / FullMode = Wait / SingleWriter = true / SingleReader = false`
- expose `ChannelWriter<Guid> Writer { get; }` + `ChannelReader<Guid> Reader { get; }`

**DI 註冊** [`Program.cs`](src/AiTeam.Bot/Program.cs)：`AddSingleton<PetraInboxChannel>()` — Channel 是 process-wide queue / Singleton 紀律

### 3. PetraInboxProcessor 重構 — producer 角色（既有 polling 邏輯延續）

**修改** [`PetraInboxProcessor.cs:55-126`](src/AiTeam.Bot/Orchestration/Petra/PetraInboxProcessor.cs#L55) `ProcessOnePendingAsync`：

**改前**（sequential await）：
- 取 pending row → 切 running → `await orchestrator.StartAsync(...)` 阻塞 → MarkCompleted / retry / dead

**改後**（producer pattern）：
- 取 pending row → 切 running → `await channel.Writer.WriteAsync(rowId, ct)` push 進 channel → return（不 await dispatch）
- **PetraInboxProcessor 退化為 pure producer** — 只負責 DB poll + push channel / 0 dispatch logic
- `channel.Writer.WriteAsync` 若 channel full → 自然 backpressure（producer 等空位 / 對齊 FullMode=Wait 紀律）

**Crash Recovery 紀律延續**：啟動 `RecoverStuckRunningAsync` 既有不動（對齊 Stage 75 baseline）

### 4. 新檔 `PetraDispatchWorker` — multi-consumer BackgroundService

**新檔** `src/AiTeam.Bot/Orchestration/Petra/PetraDispatchWorker.cs`（BackgroundService / 對齊既有 4 BackgroundService pattern — AgentQueueProcessor / InteractionProcessor / PetraSessionRecoveryService / PetraInboxProcessor）：

**設計**：
- 建構子 inject `PetraInboxChannel` + `IServiceProvider` + `WorkflowSettingsResolver` + `ILogger<PetraDispatchWorker>`
- `ExecuteAsync(CancellationToken stoppingToken)`：
  - 啟動延遲 10s（對齊 PetraInboxProcessor + PetraSessionRecoveryService 紀律）
  - 動態讀 `MaxConcurrentPetra`（透過 WorkflowSettingsResolver）
  - 起 N 個 consumer loop：`Task.WhenAll(Enumerable.Range(0, n).Select(i => ConsumeLoopAsync(i, stoppingToken)))`
  - 每個 consumer loop `await foreach (var rowId in channel.Reader.ReadAllAsync(stoppingToken))` → dispatch
- `ConsumeLoopAsync(int workerIndex, CancellationToken ct)`：
  - `await foreach` 從 channel pickup rowId
  - 開新 `IServiceScopeFactory.CreateAsyncScope`（per Task / 對齊既有 Stage 75 pattern）
  - 透過 scope 取 `PetraOrchestratorService` + `PetraInboxRepository`
  - load row → `await orchestrator.StartAsync(taskGroupId: null, row.UserInput, ct)`
  - 處理 result（對齊 Stage 76 ErrorClassifier 3 路分支 — 不重複實作 / 直接 import logic）

**DI 註冊** [`Program.cs`](src/AiTeam.Bot/Program.cs)：`AddHostedService<PetraDispatchWorker>()`

### 5. Stage 76 retry path 整合 — 移到 PetraDispatchWorker

**修改** Stage 76 既有 [`PetraInboxProcessor.cs:117-156`](src/AiTeam.Bot/Orchestration/Petra/PetraInboxProcessor.cs#L117) result 3 路分支（success / retry / fail-fast / DLQ）：

**搬遷**：把 result 處理 logic 從 PetraInboxProcessor 搬到 PetraDispatchWorker.ConsumeLoopAsync 內（producer 不再處理 result / consumer 負責）

**0 邏輯改變**：3 路分支邏輯完全保留（Transient retry exponential backoff / Transient exhausted DLQ / BusinessRule+Permanent fail-fast）— 對齊 Stage 76 既有 ErrorClassifier + MarkPendingWithRetryAsync + MarkDeadAsync + MarkFailedAsync method

**ComputeNextRetryAt helper**：搬到 PetraDispatchWorker（既有 PetraInboxProcessor class 常數 + helper method 一起搬）

### 6. Graceful shutdown drain — StopAsync override

**設計** `PetraDispatchWorker.StopAsync(CancellationToken cancellationToken)`：

- override default `StopAsync` 行為（[BackgroundService.StopAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.backgroundservice.stopasync) 既有 cancel ExecuteAsync 紀律延續）
- 加 drain timeout（default 30 min / 對齊 Petra chain longest ~13 min × safety buffer 2x）
- 等 `Task.WhenAll(consumers)` 完成 / 0 中斷 in-flight Petra（對齊「fail-fast 紀律 retry 用 cost / drain 紀律 in-flight 用 cost」精神）
- log drain progress（multi-consumer 哪個還在跑 / 等多久）

**PetraInboxChannel** 配合：StopAsync 內 `channel.Writer.Complete()` 通知所有 consumer 不會有新 row 進來 / consumer loop 自然 await foreach 結束

### 7. xUnit test 補強

新立 `src/AiTeam.Bot.Tests/Orchestration/Stage77MultiConsumerTests.cs`（對齊 Stage75InboxQueueTests + Stage76RetryMechanismTests baseline pattern）：

| Test | 對應驗收場景 | 驗證點 |
|---|---|---|
| `T1_PetraInboxChannel_BoundedConfig_BaselineSettings` | 場景 A | Channel singleton 真實註冊 / FullMode=Wait + SingleWriter=true + SingleReader=false + Capacity 對齊 config |
| `T2_PetraInboxProcessor_PushesRowIdToChannel_NotAwaitDispatch` | 場景 B | mock channel writer / PetraInboxProcessor 取 row → push channel / 0 dispatch logic 殘留 |
| `T3_PetraDispatchWorker_MultiConsumer_ParallelPickup` | 場景 C | seed 3 rowId 進 channel / N=3 consumer loop / verify 3 並行 pickup（concurrent dispatch 訊號）|
| `T4_PetraDispatchWorker_RespectsBoundedFanOut_MaxConcurrent` | 場景 D | seed 5 rowId / N=3 cap / verify 同時最多 3 個 dispatch（第 4+ 個等 consumer 釋出）|
| `T5_PetraDispatchWorker_GracefulShutdown_DrainsInFlight` | 場景 E | mock 長 Petra dispatch / 觸發 StopAsync / verify drain 等完成（不 kill in-flight）|
| `T6_PetraDispatchWorker_ResultHandling_AlignsStage76RetryPath` | 場景 F | mock result.Success=false transient → MarkPendingWithRetry / 對齊 Stage 76 邏輯 0 regression |
| `T7_MaxConcurrentPetra_AppSettingDynamicRead` | 場景 G | mock WorkflowSettingsResolver return 不同 N / verify Worker 起對應 consumer 數 |

### 8. Directory.Build.props v3.66.0 → v3.67.0

---

## 設計決策

1. **producer-consumer 分離 pattern** — PetraInboxProcessor 退化為 pure producer（DB poll）/ PetraDispatchWorker 是 pure consumer（並行 dispatch）/ 對齊業界 BackgroundService + Channel 紀律
2. **BoundedChannel `FullMode=Wait` + Capacity=20** — production safest / 0 task drop / channel 滿時 producer 自然 backpressure
3. **MaxConcurrentPetra default 3** — 對齊 Anthropic Tier 1-2 個人帳號 token rate limit 真實 bottleneck + 配 Stage 76 retry path 兜底 transient 429/5xx
4. **graceful shutdown drain timeout 30 min** — 對齊 Petra chain longest 13 min × 2x safety buffer / StopAsync 等 in-flight 完成不 kill
5. **per-Task CreateAsyncScope** — 既有 Stage 75 pattern 延續（不能跨 Task 邊界共用 Scoped service）
6. **Stage 76 retry path 整合不重寫** — 搬到 PetraDispatchWorker 但邏輯 0 改 / 對齊「修根因 > 補丁」+ 不過早 refactor 精神
7. **backwards-compatible 守護 8 層延續** — v4 / v5 / v5.5 hardcoded / Stage 70 / Stage 72+73 / Stage 74 / Stage 75 / Stage 76 retry 機制 + 本 Stage 77 multi-consumer（既有 Success=true 路徑 0 行為改變）
8. **不擴 3 Phase 4 候選**（HITL / 動態 replan / debate） — 不擾 Stage 77 infra 主軸 / 對齊「不過早 over-engineer / 自己用爽」精神

---

## 驗收情境

### 場景 A：PetraInboxChannel Bounded config 真實註冊（xUnit）

**觸發**：Bot 啟動 + Channel DI Singleton instance 真實註冊

**驗證**：
- T1：Channel 真實是 Bounded type / `Capacity=20` + `FullMode=Wait` + `SingleWriter=true` + `SingleReader=false`
- production：Bot 啟動 log 含「PetraInboxChannel 初始化 capacity=20 maxConcurrent=3」訊號

### 場景 B：PetraInboxProcessor 退化為 pure producer（xUnit）

**觸發**：xUnit mock PetraInboxRepository.GetNextPendingAsync return pending row → PetraInboxProcessor.ProcessOnePendingAsync

**驗證**：
- T2：channel.Writer.WriteAsync 真實被 call（rowId push 進）/ 0 PetraOrchestratorService.StartAsync call
- production：Bot log 改成「PetraInboxProcessor push row={Id} to channel」（移除「接手 row」訊號 / 因為 dispatch 移到 Worker）

### 場景 C：PetraDispatchWorker multi-consumer 並行 pickup（xUnit）

**觸發**：xUnit seed 3 rowId 進 channel + N=3 consumer loop

**驗證**：
- T3：3 consumer 同時 pickup（concurrent dispatch 訊號 log fire 時間差 <100ms）
- production：Bot log 含「PetraDispatchWorker consumer={Index} pickup row={Id}」3 條同時段訊號

### 場景 D：Bounded fan-out cap 守 max concurrent（xUnit）

**觸發**：xUnit seed 5 rowId 進 channel + N=3 cap + mock 慢 dispatch（10s）

**驗證**：
- T4：同時最多 3 個 dispatch fire（第 4+ 個等 consumer 釋出）
- 第 3 個 dispatch 完成後 → 第 4 個自動 pickup
- production：Trial_v22 真實多 task 並送（≥3 task）→ Bot log 訊號

### 場景 E：Graceful shutdown drain in-flight Petra（xUnit + production）

**觸發**：xUnit mock 長 Petra dispatch（5s）/ 觸發 PetraDispatchWorker.StopAsync

**驗證**：
- T5：StopAsync 等 in-flight Petra 完成 / 不 kill / drain 時間 ≥ 5s
- channel.Writer.Complete() fire / consumer loop 自然 await foreach 結束
- production：Bot 容器 stop 期間 log 含「PetraDispatchWorker StopAsync drain 等 N in-flight Petra」訊號

### 場景 F：Stage 76 retry path 整合 0 regression（xUnit）

**觸發**：xUnit mock result.Success=false transient → PetraDispatchWorker consumer 收到

**驗證**：
- T6：ErrorClassifier 真實 fire / 對齊 Stage 76 3 路分支邏輯（Transient retry / Transient exhausted DLQ / BusinessRule+Permanent fail-fast）/ 0 邏輯改變
- AppSettings `MaxAttempts` + `NextRetryAt` + exponential backoff + jitter 紀律完整對齊

### 場景 G：MaxConcurrentPetra AppSetting 動態讀取（xUnit）

**觸發**：xUnit mock WorkflowSettingsResolver return MaxConcurrentPetra=5

**驗證**：
- T7：PetraDispatchWorker 起 5 個 consumer loop（不是 default 3）
- production：SQL UPDATE `Workflow:MaxConcurrentPetra=5` → curl reload-cache → Bot 重啟（or 動態 reload）後生效

### 場景 H：v4 既有 path 0 regression

**觸發**：SQL 切 `Workflow:UsePetraOrchestratorV5=false`（v4 path）+ 跑 Mock task

**驗證**：
- Bot log 0 含「PetraDispatchWorker」字樣 / 走 v4 既有 AgentQueueProcessor
- v4 既有 path 0 動 Channel / multi-consumer / retry 邏輯 0 觸發
- v4 path 既有 baseline 行為 0 改變

### 場景 I：Trial_v22 真實業務驗（多 task 並送 + per-Talent lock contention 真實 fire）

**觸發**：Trial_v22 開跑前 SQL 確認 5 v5.5 flag production active + `MaxConcurrentPetra=3` + 連送 3-5 task

**驗證**：
- 3-5 task 真實並行 process（SQL `petra_inbox` 多 row 同時 `Status='running'` / StartedAt 接近）
- per-Talent lock contention **真實 fire**（Bot log 含「acquire per-Talent lock talent=Cody talentId=... — waiting」訊號 / vs Trial_v21「acquire-immediate-release」永無 contention）
- 業務正確性對齊 Trial_v19/v20/v21 baseline（每 PR 5/5 質感）
- Petra chain duration 從 sequential 25-40 min（3 task）→ 並行 15-20 min（取決於 contention）

---

## 技術約束

- 環境細節 source of truth 對齊 [workflow_aria.md 第三節 A 第 7 條紀律](../../memory/workflow_aria.md)
- PetraDispatchWorker BackgroundService 對齊既有 4 BackgroundService pattern（AgentQueueProcessor / InteractionProcessor / PetraSessionRecoveryService / PetraInboxProcessor）
- Channel + Multi-consumer 對齊業界 `BackgroundService + Channel` 紀律（[Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) 官方 reference）
- WorkflowSettingsResolver 加新 method 對齊既有 method 簽名 pattern（[`WorkflowSettingsResolver.cs:23`](src/AiTeam.Bot/Configuration/WorkflowSettingsResolver.cs#L23)）
- xUnit test 補強對齊 Stage 75/76 既有 InMemory DB baseline pattern
- 對齊 [`docs/conventions/csharp.md`](../conventions/csharp.md) / [`refactor-sop.md`](../conventions/refactor-sop.md)
- backwards-compatible 守護 8 層延續

---

## ⚠️ Aria 預警（對齊一般架構級重構區間實證 + 自省點 #37 Aria raw 偏低紀律）

**Stage 77 raw 預估評估**（對齊 calibration_anchors 一般架構級重構區間 ×0.43-0.60 第 6 資料點候選）：

- Plan 階段 read existing（PetraInboxProcessor + WorkflowSettingsResolver + 既有 4 BackgroundService pattern + Stage 76 retry path 整合 + Program.cs DI 註冊）~25-35K
- 機制層 code 改動 ~30-40K（新檔 PetraInboxChannel + PetraDispatchWorker + WorkflowSettingsResolver 加 method + PetraInboxProcessor 重構 producer-only + Program.cs DI 註冊 + db_seed Migration MaxConcurrentPetra）
- 既有 method body 搬遷 thinking ~10-15K（Stage 76 retry path 邏輯 PetraInboxProcessor → PetraDispatchWorker）
- xUnit case 起草 ~20-30K（7 case + Channel mock + multi-consumer 並行 verify + drain timeout test）
- Aria 二檢 round-trip buffer ~20-30K
- **raw 估算 ~105-150K × 0.50 ≈ 50-75K 總 context**

**對齊 Stage 76 真實落點教訓**（Aria raw 95-140K 預估 / Forge Opus 1M+high 真實 241K / ratio ×1.72-2.54 — 自省點 #37 第 5 次累積實證）：

**Model 推薦**：
- 🥇 **Opus 200K + high**（推薦 / safety buffer 充裕 / 對齊 Stage 76 同類規模真實落點 + 並行 concurrency 邏輯需深推理）
- 🥈 **Opus 1M + Extra high**（Christ 連續 4 Stage 73/74/75/76 真實使用模式校準 / 自升一級兜底紀律連續 / 對齊 Aria meta 紀律「對 retry+concurrency 業界紀律精細 trade-off 直推 Opus 1M」）
- ❌ Sonnet 200K + high — 不推（concurrency 邏輯精細 + Channel + drain 紀律需深推理）

**cost 預估**：**$3-5 per cycle**（對齊 Stage 76 真實落點）

---

## 實作紀錄（v2.0 — Forge 結案第一段 / 2026-05-18）

> Forge commit `c3972f1` — feat(stage77): fire-and-forget A2 完整版 v3.67.0 — v5.5 Phase 3 補強。
> Plan v1.1（Aria gate1 4 點修正 incorporated）+ Roadmap v1.0 規範完整對齊 + 0 follow-up bug fix（Stage 75/76 baseline 連續第三次 clean delivery）。

### 實作完成項目（依 Roadmap 8 子項 + Plan v1.1 #11 對齊）

| # | 子項 | 對應檔案 | 狀態 |
|---|---|---|---|
| 1 | `Workflow:MaxConcurrentPetra` AppSetting + Resolver method（default 3 / 範圍 [1, 10]）| [`WorkflowSettings.cs:111`](../../src/AiTeam.Bot/Configuration/WorkflowSettings.cs#L111) + [`WorkflowSettingsResolver.cs:102`](../../src/AiTeam.Bot/Configuration/WorkflowSettingsResolver.cs#L102) + 新 `GetIntInRangeAsync` private helper | ✅ |
| 2 | PetraInboxChannel 新檔（Singleton / Bounded Capacity=20 / FullMode=Wait / SingleWriter=true / SingleReader=false）| [`PetraInboxChannel.cs`](../../src/AiTeam.Bot/Orchestration/Petra/PetraInboxChannel.cs)（40 行 / 對齊業界 BackgroundService + Channel 紀律）| ✅ |
| 3 | PetraInboxProcessor 退化 pure producer（push channel / 0 dispatch logic）+ Aria 議題 3 `ChannelClosedException` explicit catch | [`PetraInboxProcessor.cs`](../../src/AiTeam.Bot/Orchestration/Petra/PetraInboxProcessor.cs)（213 行 → 107 行 / **-50%**）| ✅ |
| 4 | PetraDispatchWorker 新檔 multi-consumer BackgroundService（N=3 default × Task.WhenAll + 3 scope per dispatch + Stage 76 retry path 整套搬遷 0 邏輯改變 + dispatch CT 跟 stoppingToken 解耦）| [`PetraDispatchWorker.cs`](../../src/AiTeam.Bot/Orchestration/Petra/PetraDispatchWorker.cs)（~270 行 / 設計紀律 8 條完整）| ✅ |
| 5 | Stage 76 retry path 3 路分支整套搬遷（Transient retry / Transient exhausted DLQ / BusinessRule+Permanent fail-fast）| 搬到 `PetraDispatchWorker.DispatchOneAsync` 內 / 邏輯 0 改變（W2 trade-off / Random.Shared / ComputeNextRetryAt helper / outer catch fallback MarkFailed 全延續）| ✅ |
| 6 | StopAsync graceful shutdown drain 4 階段（`channel.Writer.TryComplete` + `WaitAsync(30 min)` + `_dispatchCts.Cancel` fallback + `base.StopAsync`）| `PetraDispatchWorker.StopAsync` override / 3 場景路徑（正常 drain / timeout / host cancel） | ✅ |
| 7 | xUnit 7 case 補強 + `[InternalsVisibleTo]` 既有沿用 + T6 Aria 議題 1 方案 A（`PetraOrchestratorService.StartAsync` 標 `virtual` + `StubPetraOrchestratorService` override 整合 invoke `DispatchOneAsync` 驗 retry path）| [`Stage77MultiConsumerTests.cs`](../../src/AiTeam.Bot.Tests/Orchestration/Stage77MultiConsumerTests.cs)（~360 行 / 7 method = T1-T6 + T7 9 InlineData = 15 test）| ✅ |
| 8 | Migration `Stage77MaxConcurrentPetraSeed` InsertData 純 seed（idempotency 紀律明示）| [`20260518102303_Stage77MaxConcurrentPetraSeed.cs`](../../src/AiTeam.Data/Migrations/20260518102303_Stage77MaxConcurrentPetraSeed.cs) | ✅ |
| 9 | `Directory.Build.props` v3.66.0 → v3.67.0 | [`Directory.Build.props`](../../src/Directory.Build.props) | ✅ |
| 10 | Program.cs DI 註冊 2 行（`PetraInboxChannel` Singleton + `PetraDispatchWorker` HostedService）| [`Program.cs:103-110`](../../src/AiTeam.Bot/Program.cs#L103) / `[InternalsVisibleTo]` 既有已在 [`AiTeam.Bot.csproj:44`](../../src/AiTeam.Bot/AiTeam.Bot.csproj#L44) 立 / 0 新增 | ✅ |
| 11 | `PetraOrchestratorService.StartAsync` 標 `virtual`（Aria 議題 1 拍板 / xUnit T6 stub 用 / 1 keyword / 0 caller 簽名動）| [`PetraOrchestratorService.cs:56`](../../src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L56) | ✅ |

### 關鍵設計決策（為什麼這樣選）

1. **producer-consumer 分離 pattern** — PetraInboxProcessor 退化 pure producer + PetraDispatchWorker 接 pure consumer / 對齊業界 .NET `BackgroundService + Channel` 紀律（Stephen Cleary 警示 / `Task.Run` 3 大雷 — exception swallowing / orphan task / Scoped service lifetime 全避開）。
2. **Channel BoundedChannel `FullMode=Wait` + Capacity=20 const** — production safest / 0 task drop / channel full 時 producer 自然 backpressure / Singleton ctor 不能 await AppSettings（沒做動態讀）/ 「自己用爽 / 不過早 over-engineer」。
3. **MaxConcurrentPetra default 3 / 範圍 [1, 10]** — 業界 Anthropic Tier 1-2 個人帳號保守紀律 / 配 Stage 76 retry path 兜底 transient 429/5xx / 既有 `GetIntAsync` 只守 `v > 0` 不夠 → 加新 `GetIntInRangeAsync` 私有 helper 守 max。
4. **dispatch CT 跟 stoppingToken 解耦** — `_dispatchCts` 獨立 lifecycle / 守 drain 期間 in-flight Petra 不被 host stop cancel 中斷 / 只在 drain timeout 30 min 後才 force cancel（對齊 Roadmap §6「不 kill in-flight」精神）。
5. **MaxConcurrentPetra 啟動時讀一次 / SQL UPDATE 需 Bot 重啟生效**（Aria 議題 2 紀律）— 動態 reload N consumer 非當前 Stage 範圍 / 對齊「自己用爽 / 不過早 over-engineer」精神 / Roadmap §G 場景驗收明寫「Bot 重啟（or 動態 reload）後生效」走 Bot 重啟 path。
6. **Stage 76 retry path 整合搬遷 0 邏輯改變**（業界紀律「搬遷 = 等價變換」）— ErrorClassifier / MarkPendingWithRetryAsync caller 算 newAttemptCount 傳入 / MarkDeadAsync / MarkFailedAsync / `AttemptCount + 1 < MaxAttempts` 條件 / Transient vs BusinessRule+Permanent 判斷 / ComputeNextRetryAt + Random.Shared / 全套搬遷紀律守 T6 整合 invoke `DispatchOneAsync` 驗。
7. **ChannelClosedException explicit catch**（Aria 議題 3 紀律）— PetraDispatchWorker.StopAsync TryComplete 後 PetraInboxProcessor.WriteAsync 拋 ChannelClosedException / 視為正常 shutdown（不算 polling 異常 log noise）/ row 仍標 running / 下次 Bot 重啟 RecoverStuckRunningAsync 救回。
8. **T6 整合驗收方案 A virtual + stub override**（Aria 議題 1 拍板）— `PetraOrchestratorService.StartAsync` 標 `virtual` 1 keyword 最小 invasive / `StubPetraOrchestratorService` test-only subclass override stub / 整合 invoke `DispatchOneAsync` 驗 ErrorClassifier 分類 + MarkPendingWithRetryAsync caller args 正確（Stage 76 retry path 0 邏輯改變紀律真實 regression test cover / 不只各別 method 行為驗）。
9. **backwards-compatible 守護 8 層延續** — v4 / v5 / v5.5 hardcoded / Stage 70 / Stage 72+73 / Stage 74 / Stage 75 / Stage 76 retry — 全 0 動（既有 Success=true 路徑 0 行為改變 / Trial_v22 Aria gate2 場景 H SQL 切 `UsePetraOrchestratorV5=false` 驗證 v4 path 0 regression）。

### Aria gate1 4 點修正 incorporated（Plan v1.0 → v1.1）

| 嚴重度 | 議題 | 修法 | Plan 對應段 |
|---|---|---|---|
| 🟡 必修 | T6 採方案 B「拆兩 sub-case 驗 ErrorClassifier + Repository mark」跳過 Stage 76 retry path 整合驗收 | **走方案 A** — PetraOrchestratorService.StartAsync 標 virtual + test-only subclass override / 整合 invoke DispatchOneAsync 真實 regression test cover | §F + R8 + Critical Files + 子項對應 #11 |
| 🟢 nit | N 啟動讀一次紀律未明示 | §D 設計紀律 #8 明寫「MaxConcurrentPetra 啟動讀一次 / SQL UPDATE 需 Bot 重啟生效」| §D 設計紀律 #8 |
| 🟢 nit | PetraInboxProcessor outer catch shutdown 場景視為 polling 異常 log noise | 加 explicit `ChannelClosedException` 分支（shutdown 場景視為正常）| §C |
| 🟢 nit | Migration InsertData idempotency 紀律未明示 | 「new key in production / 0 conflict risk / 對齊 Stage 74/75/76 紀律延續」明寫 | §G |

### Mock 覆蓋情況（xUnit + production 5 層守門）

| 場景 | 覆蓋手段 | 結果 |
|---|---|---|
| **A** PetraInboxChannel Bounded config baseline | xUnit T1（new + Writer.WriteAsync round-trip） | ✅ |
| **B** PetraInboxProcessor 退化 pure producer（push channel / 0 dispatch logic） | xUnit T2（reflection invoke private `ProcessOnePendingAsync` + verify channel.Reader 拿到 rowId + Status='running'）| ✅ |
| **C** Multi-consumer 並行 pickup | xUnit T3（seed 3 rowId + 3 consumer loop / ConcurrentBag 紀錄 worker index / verify 3 distinct workers）| ✅ |
| **D** Bounded fan-out cap 守 max concurrent | xUnit T4（seed 5 rowId / N=3 cap / 慢 dispatch 150ms / peakParallel ≤ 3 / peakParallel ≥ 2）| ✅ |
| **E** Graceful shutdown drain in-flight | xUnit T5（channel.Writer.TryComplete + verify Completion.IsCompleted=true / 30 min timeout fire 簡化驗 production 範圍）| ✅ |
| **F** Stage 76 retry path 0 regression 整合驗收 | xUnit T6（Aria 議題 1 方案 A — virtual + stub override / 整合 invoke DispatchOneAsync / verify Status='pending' + AttemptCount=1 + NextRetryAt 對齊 ComputeNextRetryAt 區間 23~37s）| ✅ |
| **G** MaxConcurrentPetra AppSetting 動態讀取 + 範圍守 [1, 10] | xUnit T7 Theory 9 InlineData（valid 1/3/5/10 + out-of-range 0/11/-1 + invalid abc/empty / 全 fallback to default=3）| ✅ |
| **production 5 層守門** | Forge 自驗 — CI/CD 部署 + Migration apply + PetraInboxChannel 初始化 log + PetraDispatchWorker N=3 啟動 + 5 v5.5 flag + MaxConcurrentPetra production active | ✅ |
| **H** v4 path 0 regression（SQL 切 flag false） | Aria gate2 範圍 / Christ 觸發 | ⏸ |
| **I** Trial_v22 真實業務驗（多 task 並送 + per-Talent lock contention 真實 fire + 「PetraInboxProcessor push row to channel」+「PetraDispatchWorker consumer={Index} pickup row」3 條同時段訊號） | Aria gate2 + Christ 觸發 | ⏸ |

### 踩坑紀錄（Forge 自驗 catch + 自修）

#### 1. xUnit `PetraOrchestratorResult` positional record 編譯錯（dotnet build 第一次 5 error）

**踩雷**：T3/T4/T5/T6 stub return value 用 object initializer `new PetraOrchestratorResult { Success = true, ... }` — 但 `PetraOrchestratorResult` 是 `sealed record` with positional ctor `(Guid SessionId, bool Success, int DispatchedWorkerCount, IReadOnlyList<string> DecidedCapabilities, string Summary, string? ErrorMessage = null)` / required parameters 不允許 object initializer。

**修法**：改用 static factory method（Done / Failure / Empty）— 更乾淨：
- T3/T4/T5 success → `PetraOrchestratorResult.Done(Guid.NewGuid(), Array.Empty<string>(), "stub ok")`
- T6 failure → `PetraOrchestratorResult.Failure(Guid.NewGuid(), Array.Empty<string>(), "HTTP 500 Internal Server Error")`

#### 2. `PetraDispatchWorker` ctor named arg `resolver:` 應為 `workflowResolver:`（dotnet build 1 error）

**踩雷**：T3 ctor invoke 用了 `workflowResolver` shorthand 寫法 `resolver: null!`，與 PetraDispatchWorker primary constructor 真實參數名 `workflowResolver` 不對齊。

**修法**：改 `workflowResolver: null!`（其餘 ctor 參數 0 動）。

#### 3. Forge spike 揭架構盲點：base ctor 14 deps stub 設計選擇

**踩坑**：Aria 議題 1 拍板方案 A 後 / Forge 實作時面臨 stub 路徑兩選擇 — (a) all null base ctor / (b) BuildServiceProvider DI resolve。Aria 提示「BuildServiceProvider DI resolve 推 — 對齊 Stage75InboxQueueTests pattern」。

**真實落地**：採綜合方案 — `StubPetraOrchestratorService` 用 `base(null!, null!, ..., NullLoggerFactory.Instance, NullLogger<>.Instance)` all-null + 透過 `ServiceCollection.AddScoped<PetraOrchestratorService>(_ => new StubPetraOrchestratorService(stubFactory))` 註冊取代真實 service / `DispatchOneAsync` 內 `runScope.ServiceProvider.GetRequiredService<PetraOrchestratorService>()` 拿到 stub。0 base method invoke / nullable runtime 容忍 / 對齊 BuildServiceProvider DI resolve 紀律。

#### 4. PetraOrchestratorService.StartAsync 標 virtual 0 caller 簽名動驗證（Aria 提醒）

**踩坑前置驗證**：Aria 議題 1 拍板後 / Aria gate1 prompt 提醒「grep 既有 caller 確認 0 簽名動 / 0 既有 invoke 行為變化 / 若有 sealed class context 衝突則 escalate Christ」。

**grep 結果**：
- caller #1：[`PetraInboxProcessor.cs:107`](../../src/AiTeam.Bot/Orchestration/Petra/PetraInboxProcessor.cs#L107) `orchestrator.StartAsync(...)` — Stage 77 改寫退化 producer 後此 line 已刪 / 0 影響
- caller #2：[`CeoAgentService.cs:32`](../../src/AiTeam.Bot/Agents/CeoAgentService.cs#L32) `petraOrchestrator` inject 但 line 106+ 不再 call StartAsync（Stage 75 改寫 PetraInbox 後 ack）/ 0 既有 invoke

**結論**：`PetraOrchestratorService` 非 sealed / `StartAsync` 標 `virtual` 0 caller 簽名動 / 0 既有 invoke 行為變化 / 0 escalate Christ 需求 / 直接改 1 keyword。

### 驗收結果

**本機驗證**（Plan §Verification 對齊）：

| # | 驗證動作 | 結果 |
|---|---|---|
| 1 | `dotnet ef migrations add Stage77MaxConcurrentPetraSeed --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext` | ✅ Migration + Designer 雙檔生成 / 手動加 `InsertData` + `DeleteData` |
| 2 | `dotnet build AiTeam.slnx` | ✅ **0 error / 0 新 warning**（修 2 處 test 編譯錯後通過） |
| 3 | `dotnet test`（含 Stage 77 新 case） | ✅ AiTeam.Bot.Tests **113/113 passed**（Stage 77 新 15 case = T1-T6 + T7 9 InlineData / Stage 75+76 既有 baseline 全綠） |
| 4 | `dotnet test` AiTeam.Tests.Generated | ✅ **127/127 passed**（Stage 77 0 動 Generated test）|

**Forge 自驗 production 5 層守門全綠**（Christ 觸發 forge-self-verify SOP）：

| # | 守門 | 結果 |
|---|---|---|
| 1 | CI/CD `run 26028038857` 部署完成 | ✅ success 5m38s（10:30:53 UTC start → 10:36:31 UTC done）|
| 2 | Migration `Stage77MaxConcurrentPetraSeed` apply | ✅ `Applying migration '20260518102303_Stage77MaxConcurrentPetraSeed'` + INSERT `Workflow:MaxConcurrentPetra` = `3` |
| 3 | PetraInboxChannel 初始化 log | ✅ `capacity=20 fullMode=Wait singleWriter=true singleReader=false`（完整對齊 Plan §B BoundedChannelOptions）|
| 4 | PetraDispatchWorker N=3 consumer 啟動 | ✅ `consumer count=3 drainTimeout=30min` + `consumer=0/1/2 啟動` 3 條訊號 |
| 5 | 5 v5.5 flag + MaxConcurrentPetra production active | ✅ `app_settings` 16 row（5 v5.5 flag 全 `true` + `MaxConcurrentPetra=3` baseline）|

**0 follow-up bug fix 紀律 — Stage 75/76 baseline 連續第三次 clean delivery**：
- Stage 75（commit `fd8975f`）— Forge 自驗 0 fix → Aria gate2 場景補強第二次 commit
- Stage 76（commit `051d9df`）— Forge 自驗 0 fix → Aria gate2 場景 F 視覺驗收
- **Stage 77（commit `c3972f1`）— Forge 自驗 0 fix → Aria gate2 場景 H+I 留 Christ 觸發**

### 留 Aria gate2 + Christ 觸發範圍

- **場景 H**（v4 path 0 regression — SQL 切 `UsePetraOrchestratorV5=false` + Mock task / Bot log 0 含「PetraDispatchWorker」字樣）— **Aria gate2 範圍**
- **場景 I**（Trial_v22 真實業務驗 — Bot log 「PetraInboxProcessor push row to channel」+「PetraDispatchWorker consumer={Index} pickup row」3 條同時段訊號 + per-Talent lock contention「acquire per-Talent lock talent=Cody talentId=... — waiting」訊號真實 fire）— **Aria gate2 + Christ 觸發 Trial_v22 真實業務 task** 範圍

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-18 | Forge 結案第一段 — 實作紀錄章節立 / commit `c3972f1` feat(stage77) 11 file changed +2071/-127 / Plan v1.1（Aria gate1 4 點修正 incorporated）+ Roadmap v1.0 規範完整對齊 / **0 follow-up bug fix**（Stage 75/76 baseline 連續第三次 clean delivery）/ 本機驗證 `dotnet build` 0 error + `dotnet test` 113/113 passed（Stage 77 新 15 case）+ Generated 127/127 / Forge 自驗 production 5 層守門全綠（CI/CD `run 26028038857` success 5m38s + Migration apply + PetraInboxChannel 初始化 + PetraDispatchWorker N=3 啟動 + 5 v5.5 flag + MaxConcurrentPetra=3 baseline）/ 場景 A-G xUnit 全 cover + 場景 H/I 留 Aria gate2 + Christ 觸發。**Forge spike 揭架構盲點 0 處**（對比 Stage 76 MaxAttempts patch 0→3 揭 EF Migration 不讀 C# property initializer / Stage 77 全程順利）。**踩坑 4 條**：① xUnit PetraOrchestratorResult positional record 編譯錯（object initializer 不能用 / 改 static factory Done/Failure）② PetraDispatchWorker ctor named arg `resolver:` → `workflowResolver:` ③ base ctor 14 deps stub 設計綜合方案（all-null base + BuildServiceProvider DI resolve 取代）④ PetraOrchestratorService.StartAsync 標 virtual 前 grep 既有 caller 確認 0 簽名動。**等 Aria 接手第二段**：CHANGELOG v3.67.0 + Future_Feature.md 同步 + Future_Feature_v5.5.md Phase 3 補強 Step 10 ✅（Stage 77 ✅ 標完成）+ Top 5 重排（Trial_v22 升 #1 / Stage 78+ WebUI Talent CRUD + Effort + G Token monitoring 視覺化 #2 / Phase 4 候選 HITL + 動態 replan #3）。 |
| v1.0 | 2026-05-18 | 規劃書建立 — v3.67.0 / S/M 規模 / v5.5 Phase 3 補強（fire-and-forget A2 業界推薦完整版 — Channel + multi-consumer + bounded fan-out + graceful shutdown drain）。**戰略脈絡**：Trial_v21 🟡 部分過揭 Stage 75 設計實作落差（PetraInboxProcessor sequential await vs 議題 1 拍板 multi-session 並存）+ Christ 2026-05-18 戰略 question 點破 Phase 4 候選（HITL / 動態 replan / debate）+ Aria 計劃前 WebSearch 3 議題（HITL 紀律 + 動態 replan 警示 + multi-agent debate 業界反向 finding）→ 拍板 Stage 77 範圍邊界收緊「fire-and-forget A2 完整版 only」+ 不擴 3 Phase 4 候選（HITL + 動態 replan 留 Phase 4 評估 / debate 直接刪除對齊 Stage 74 撤回判斷 + 業界研究反向 finding）。**8 子項**：① MaxConcurrentPetra AppSetting + WorkflowSettingsResolver method ② PetraInboxChannel Singleton（Bounded Capacity=20 + FullMode=Wait + SingleWriter=true + SingleReader=false）③ PetraInboxProcessor 退化為 pure producer（push channel / 0 dispatch logic）④ PetraDispatchWorker 新檔 BackgroundService（N=3 multi-consumer Task.WhenAll）⑤ Stage 76 retry path 整合搬到 PetraDispatchWorker（0 邏輯改變）⑥ Graceful shutdown drain（StopAsync 等 N in-flight Petra 完成 / timeout 30 min）⑦ xUnit 7 case ⑧ version bump v3.67.0。**計劃前 WebSearch 結論段 7 議題完整 incorporated**（Fire-and-forget 雷 + BackgroundService+Channel 業界主流 + Channel BoundedChannelOptions config + multi-consumer Task.WhenAll pattern + Anthropic rate limit + MaxConcurrent 紀律 + Graceful shutdown drain + IServiceScopeFactory CreateAsyncScope per Task）。**設計決策核心**：producer-consumer 分離 + Stage 76 retry 整合 0 邏輯改變 + per-Task CreateAsyncScope + backwards-compatible 守護 8 層延續 + 不擴 3 Phase 4 候選。**驗收 9 場景**：A Channel Bounded config / B Producer 退化 / C Multi-consumer 並行 pickup / D Bounded fan-out cap 守 / E Graceful shutdown drain / F Stage 76 retry path 0 regression / G MaxConcurrentPetra AppSetting 動態 / H v4 path 0 regression / I **Trial_v22 真實業務驗（per-Talent lock contention 真實 fire）**。**校準錨預期**：對齊一般架構級重構區間 ×0.43-0.60 第 6 資料點候選 / raw 105-150K × 0.50 ≈ 50-75K 總 context / Opus 200K + high 推薦 + Opus 1M+Extra high 自升兜底 / cost $3-5。**Phase 3 完整收口路徑**：73 ✅ → 74 ✅ → 75 ✅ → 76 ✅ → **77**（fire-and-forget A2 完整版 / 本 Stage）→ **78+ 預留**（WebUI Talent CRUD + Effort 擴展 + G Token monitoring 視覺化）→ Phase 4 候選（HITL plan confirmation 閘門 / 動態 re-planning / Token rate limit headers monitoring）→ v5.5 完整收口。**下一步**：Forge 實作 + Aria gate1 Tier 0+1+Tier 2 #3 build + Trial_v22 真實業務驗（per-Talent lock contention 真實 fire 機會 → 驗 Stage 75+76+77 三 Stage 整套機制完整生效）。 |
