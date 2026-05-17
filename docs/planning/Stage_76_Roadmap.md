# Stage 76 Roadmap — task retry / resume 機制基礎建設 + Trial_v21 修補類項目（v5.5 Phase 3 補強）

> 目標版本：**v3.66.0**（minor — v5.5 Phase 3 補強 / 一般架構級重構：PetraInbox schema 擴 4 欄 + retry path + error classification + DLQ + queuePosition race fix + Dashboard 重跑按鈕）
> 狀態：📋 規劃中
> 文件版本：v1.0
> 範圍：PetraInbox schema 擴 4 欄 + Migration + PetraInboxProcessor retry path（exponential backoff + jitter）+ Error type classification（retryable vs non-retryable）+ Dead Letter pattern + queuePosition race condition 修法 + Dashboard 重跑 failed task 按鈕 + xUnit + version bump
> 規模：M+（對齊一般架構級重構區間 ×0.43-0.60 / 第 5 資料點候選）
> 對應 v5.5 規劃：[Future_Feature_v5.5.md](Future_Feature_v5.5.md) Phase 3 Step 9 補強段（Stage 75 Trial_v21 揭 retry/resume 機制 + queuePosition race）
> 對應前置：Trial_v21 [Trial_v21_Plan.md](../experiments/Trial_v21_Plan.md) v2.0 揭議題

---

## 戰略脈絡

**Trial_v21 🟡 部分過揭 1 🔴 戰略級設計實作落差 + 2 🟡 工程細節 + 3 🟢 觀察留檔 → Christ 拍板 A2 完整版路線 / Stage 76 範圍重排「功能性 + 修補類」/ WebUI Talent CRUD + Effort 擴展 + G Token monitoring 視覺化推 Stage 78+**

**核心戰略議題**（Christ 戰略 question 點破）：「task 意外停止後架構是否能重跑？」— 真實答案「不行 / 必須重下指令」/ 揭 production resilience gap。Trial_v21 Token 守門 fire 場景 + PetraInboxProcessor Status='failed' bug 兩者組合揭真實 retry/resume 機制缺口。

**Stage 76 ≠ Stage 77 fire-and-forget A2 完整版** — 兩 Stage 分工：
- Stage 76：retry / resume 機制基礎建設（schema + retry path + error classification + DLQ + 人工介入）+ 修補類（queuePosition race）
- Stage 77 預留：fire-and-forget A2 完整版（Channel + drain + bounded fan-out / 對齊業界 BackgroundService + Channel 紀律）— 跟 Stage 76 互補 / 不混做

### 計劃前 WebSearch 結論（業界 reference — Forge 起草時 reference 用 / 不重複觸發）

Aria 2026-05-18 計劃前 WebSearch 4 議題（task retry / multi-agent failure recovery / LLM dispatch retry / PostgreSQL job queue retry）結論：

**1. PostgreSQL job queue retry 業界經典 schema 擴展 pattern** — [Implementing a Postgres job queue](https://aminediro.com/posts/pg_job_queue/) + [Reducing batch failures by standardizing retries, backoff, and idempotency](https://us.fitgap.com/stack-guides/reducing-batch-failures-by-standardizing-retries-backoff-and-idempotency) + [Postgres as a Queue](https://www.techplained.com/postgres-as-queue) — 「Track the attempt count and a max_attempts limit on each job. When a job fails, increment the attempt counter and reschedule it with exponential backoff (scheduled_at = NOW() + 2^attempt seconds). Once attempts are exhausted, move the job to a 'dead' status for manual investigation.」對齊 AiTeam PetraInbox PG queue 既有架構 + `SKIP LOCKED` 紀律。

**2. multi-agent 5 failure modes + escalation `Retry → Replan → Decompose`** — [Multi-Agent AI Systems: Why They Fail and How to Fix Coordination Issues (2026)](https://www.augmentcode.com/guides/why-multi-agent-llm-systems-fail-and-how-to-fix-them) + [5 Recovery Strategies for Multi-Agent LLM Failures](https://www.newline.co/@zaoyang/5-recovery-strategies-for-multi-agent-llm-failures--673fe4c4)：
   - mode 1 transient infrastructure（429/5xx/timeout）→ auto retry + exponential backoff
   - mode 2 LLM hallucination / output format invalid → auto retry with adjusted prompt
   - mode 3 tool call failure（git push / file write）→ auto retry + idempotency check
   - mode 4 **business rule rejection（Token 守門 / quota）→ fail-fast 不 retry**（retry 會無限循環）⚠️
   - mode 5 logic dead-end → Replan / Decompose（Phase 4 候選）

   **Trial_v21 Token 守門 fire = mode 4 → fail-fast 紀律對齊**

**3. LLM retry 紀律標準 config** — [AI Agent Retry Patterns - Exponential Backoff Guide 2026](https://fast.io/resources/ai-agent-retry-patterns/) + [Backoff and Retry Strategies for LLM Failures](https://palospublishing.com/backoff-and-retry-strategies-for-llm-failures/) + [Retries, fallbacks, and circuit breakers in LLM apps](https://portkey.ai/blog/retries-fallbacks-and-circuit-breakers-in-llm-apps/)：
   - Initial delay 250-750ms / Backoff factor ×2
   - **Jitter（AWS finding 揭 reduce retry storm 60-80%）**
   - Per-attempt timeout 5-10s match provider SLA
   - Max attempts 3-5 for read / **fewer for writes unless idempotent**
   - Only retry on: 429/5xx/timeouts / respect `Retry-After` headers
   - Idempotency 紀律：billing 不能重複（AiTeam 對應：PR 不能重複開 / commit 不能重複 push — 但 LLM dispatch 是 idempotent for `PetraInbox` re-pickup since each `PetraInboxProcessor` polling 開新 Scoped `PetraOrchestratorService` instance 跑全新 chain）

**4. Polly vs Hangfire vs BackgroundService 選型** — [Background Jobs in .NET: Hangfire, Quartz, Temporal in 2026](https://amarozka.dev/background-jobs-schedulers-dotnet-hangfire-quartz-temporal/) + [Retry resilience strategy - Polly](https://www.pollydocs.org/strategies/retry.html)：
   - Polly = 同 process / 短時間 transient failure / 不適合「跨 session resume」場景
   - Hangfire = dashboards + persistent queue + AutomaticRetryAttribute / 過度重量 / AiTeam PetraInbox 已是 PG queue 不需要
   - **BackgroundService + custom retry = 推薦 / 對齊 AiTeam current path** / 加 4 schema 欄 + retry logic ~80-120 行

**5. 人工介入路徑業界紀律** — [Build AI Agents That Resume from Failure with Pydantic AI](https://www.prefect.io/blog/prefect-pydantic-integration) + [Mastering Retry Logic Agents](https://sparkco.ai/blog/mastering-retry-logic-agents-a-deep-dive-into-2025-best-practices) — 「Anthropic claude-progress.txt + git history 是業界 handoff 紀律 / Dashboard『重跑』按鈕是業界主流 / 人工拍板 + auto retry 的混合模式」

---

## 子項清單

### 1. PetraInbox schema 擴 4 欄 + Migration

**修改** [`Entities.cs:463`](src/AiTeam.Data/Entities.cs#L463) `PetraInbox` 加 4 欄位（對齊 PostgreSQL job queue 業界 retry 紀律）：

- `AttemptCount` (int, default 0) — 已重試次數（每次 retry +1 / 首次 dispatch 為 0）
- `MaxAttempts` (int, default 3) — 上限重試次數（exhausted 後進 Dead Letter）
- `NextRetryAt` (DateTime?, nullable) — 下次重試時間（exponential backoff: `NOW() + 2^attempt 秒`+ jitter）
- `DeadAt` (DateTime?, nullable) — 進 Dead Letter 時間（exhausted attempts 後標）

**index 擴**：既有 `Status + EnqueuedAt` 改 `Status + NextRetryAt`（含 NULL first 對齊 pending 立即 pickup / retry pending 等 backoff）— Migration 加新 index

**新 Status 值**：既有 `pending / running / completed / failed` 加 `dead`（Dead Letter / 等人工介入）

**對齊既有 pattern**：
- Stage 75 既有 schema baseline
- PostgreSQL `SKIP LOCKED` 紀律延續（單 instance OK）

### 2. PetraInboxProcessor retry path（exponential backoff + jitter）

**修改** [`PetraInboxProcessor.cs:55`](src/AiTeam.Bot/Orchestration/Petra/PetraInboxProcessor.cs#L55) `ProcessOnePendingAsync`：

**設計**：
- `GetNextPendingAsync` 改加 `NextRetryAt IS NULL OR NextRetryAt <= NOW()` 條件（守 backoff timing）
- `ProcessOnePendingAsync` 拿到 result 後：
  - `result.Success=true` → `MarkCompletedAsync` 對齊既有路徑
  - `result.Success=false` + retryable error → **`MarkPendingWithRetryAsync`**（新 method）：`AttemptCount++` + `NextRetryAt = NOW() + 2^attempt * baseDelay + jitter` + Status='pending'
  - `result.Success=false` + non-retryable error → `MarkFailedAsync`（既有 / fail-fast）
  - `AttemptCount >= MaxAttempts` → **`MarkDeadAsync`**（新 method）：Status='dead' + `DeadAt = NOW()`

**參數**：
- baseDelay = 30 秒（初始 retry 等 30 秒避免熱失敗循環）
- backoff factor = ×2（30s → 60s → 120s）
- jitter = ±20% 隨機（對齊 AWS finding 60-80% reduce retry storm）
- maxAttempts = 3（exhausted 後進 DLQ）

**對齊既有 pattern**：Stage 27 `AgentQueueProcessor` 既有 retry 紀律（v4 path）

### 3. Error type classification（retryable vs non-retryable）

**新檔** `src/AiTeam.Bot/Orchestration/Petra/PetraErrorClassifier.cs`（service / 對齊既有 PetraOrchestratorService 同 namespace）：

**設計**：static helper class / 接收 `PetraOrchestratorResult` 或 `Exception` / 回傳 `ErrorCategory` enum

**ErrorCategory enum**：
- `Transient` — auto retry（mode 1+2+3：HttpException / TimeoutException / JsonException / 429 / 5xx pattern match）
- `BusinessRule` — fail-fast（mode 4：「守門」/「quota」/「rate limit exceeded」message pattern match）
- `Permanent` — fail-fast（unknown exception type / 標 status='failed' 等人工介入）

**判斷邏輯**：基於 `PetraOrchestratorResult.ErrorMessage` 字串 pattern match + 已知 Exception type heuristic / 對齊既有 [`PetraOrchestratorService.cs:178`](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L178) catch Exception 路徑

**Trial_v21 揭真實 fire pattern reference**：
- Token 守門 message：「Token 守門：全域本月用量 X 超過全域月限 Y。所有 LLM 呼叫已暫停。」→ `BusinessRule`
- 未來其他 quota / rate limit 訊號同類延伸（per-Agent 月限 / 日限 fire）→ `BusinessRule`

### 4. Dead Letter pattern

**新 method** `PetraInboxRepository.MarkDeadAsync(Guid id, string errorMessage, CancellationToken ct)`（對齊既有 `MarkFailedAsync` 簽名 + 額外標 `DeadAt = NOW()` + Status='dead'）

**新 method** `PetraInboxRepository.GetDeadLetterAsync(int limit, CancellationToken ct)`（Dashboard UX 用 / 顯示最近 N 筆 dead row）

**設計決策**：
- `Status='dead'` 不再被 PetraInboxProcessor pickup（GetNextPendingAsync 條件 `Status='pending'` only）
- 等人工介入（Dashboard 重跑按鈕 — 子項 6）

### 5. queuePosition off-by-one race condition 修法

**修改** [`CeoAgentService.cs:100-122`](src/AiTeam.Bot/Agents/CeoAgentService.cs#L100) v5.5 flag forward path：

**Trial_v21 揭**：兩 CeoAgentService 並行寫 inbox 都讀到 `CountPendingBySourceAsync` = 1 → 兩 row 都顯示「排隊位 2」（race condition / SaveChangesAsync 之間 race）

**修法路線**（Forge plan v2 階段拍板候選）：
- 🥇 **簡化顯示**：CeoAgentService 不算精準 queuePosition / boss_interactions Reply 改顯示「task 已接收 / 排隊處理中」+ inbox short id（user 透過 Dashboard PetraInbox section 查精準 position）
- 🥈 **DB SEQUENCE 守 atomic**：用 PostgreSQL `nextval('petra_inbox_position_seq')` 取 monotonic position（schema 加 sequence + position column）
- 🥉 **加 SemaphoreSlim atomic**：CeoAgentService 寫 inbox 前 acquire global SemaphoreSlim / 過度設計

**Aria 傾向 🥇** — 對齊「自己用爽 / 不過早 over-engineer」精神 + Dashboard PetraInbox section 已有 live update（Stage 75 §5a）/ user 真要看精準 position 看 Dashboard

### 6. Dashboard 重跑 failed/dead task 按鈕

**修改** [`InteractionCenter.razor`](src/AiTeam.Dashboard/Components/Pages/Interactions/InteractionCenter.razor) + [`.cs`](src/AiTeam.Dashboard/Components/Pages/Interactions/InteractionCenter.razor.cs)：

**設計**：
- Stage 75 既有「PetraInbox 接收狀態」section 加新 column「動作」
- Status='failed' / Status='dead' row 顯示「重跑」按鈕
- 點按鈕觸發 `PetraInboxRepository.RequeueAsync(rowId, ct)`（新 method）：
  - 確認 Status in ('failed', 'dead') → reset Status='pending' + AttemptCount=0 + NextRetryAt=NULL + StartedAt=NULL + CompletedAt=NULL + DeadAt=NULL + ErrorMessage=NULL
  - 觸發 `DashboardPushService.PushInteractionUpdateAsync()` SignalR live update
- PetraInboxProcessor 下次 polling tick 自動接（不需要重啟 Bot）

**對齊既有 pattern**：[`BossInteractionRepository`](src/AiTeam.Data/Repositories/BossInteractionRepository.cs) 既有 Dashboard 互動紀律

**Discord `/retry-task <short_id>` 指令**：留 Phase 4 候選追蹤（Dashboard 按鈕已 cover 主要 use case / Discord 指令 short_id 手打容易踩錯）

### 7. xUnit test 補強

新立 `src/AiTeam.Bot.Tests/Orchestration/Stage76RetryMechanismTests.cs`（對齊 Stage 75 既有 Stage75InboxQueueTests baseline pattern）：

| Test | 對應驗收場景 | 驗證點 |
|---|---|---|
| `T1_PetraInbox_Schema_Extended_BaselineMigration` | 場景 A | InMemory DB 4 新欄真實落地 + Migration apply |
| `T2_PetraInboxProcessor_TransientError_AutoRetryWithBackoff` | 場景 B | mock result.Success=false + transient → AttemptCount++ + NextRetryAt 對齊 backoff 公式 |
| `T3_PetraInboxProcessor_BusinessRuleError_FailFast_NoRetry` | 場景 C | mock Token 守門 message → MarkFailedAsync（不 retry）|
| `T4_PetraInboxProcessor_ExhaustedAttempts_MarkDead` | 場景 D | AttemptCount=3 fail → Status='dead' + DeadAt set |
| `T5_CeoAgentService_QueuePositionDisplay_SimpleAck` | 場景 E | 並行寫 inbox / 對齊 🥇 簡化顯示 / 不依賴精準 position |
| `T6_DashboardRequeueAsync_ResetFailedRowToPending` | 場景 F | 觸發 RequeueAsync / row 狀態真實 reset / Processor 下次 polling 接 |
| `T7_PetraErrorClassifier_TokenGuardMessage_ClassifyAsBusinessRule` | 場景 C | Trial_v21 真實 Token 守門 message pattern → ErrorCategory.BusinessRule |
| `T8_NextRetryAt_PollingCondition_RespectsBackoff` | 場景 B | seed pending row with NextRetryAt 未來 → Processor 0 pickup |

### 8. Directory.Build.props v3.65.0 → v3.66.0

---

## 設計決策

1. **「修根因 > 補丁」哲學延續** — Trial_v21 揭真實 production resilience gap / 不靠補丁繞過 / 對齊業界經典 PG queue retry pattern 一次到位
2. **AttemptCount + MaxAttempts + NextRetryAt + DeadAt 4 欄組合** — 業界經典 schema 設計（[Postgres as a Queue](https://www.techplained.com/postgres-as-queue) reference）+ 對齊 Hangfire AutomaticRetryAttribute 既有 idiom
3. **business rule rejection（Token 守門）fail-fast 不 retry** — 對齊 multi-agent 5 failure modes mode 4 紀律（retry 業務規則錯誤會無限循環 + 燒 cost）
4. **exponential backoff + jitter 標準 config** — base 30s × 2 × max 3 attempts（30s → 60s → 120s）+ ±20% jitter（對齊 AWS finding 60-80% reduce retry storm）
5. **Dead Letter 等人工介入** — 不 auto recover / Dashboard 重跑按鈕需 Christ 拍板（對齊 Anthropic claude-progress.txt 人工 handoff 紀律 + 業界 mixed mode auto retry + human-in-the-loop）
6. **queuePosition 簡化顯示** 🥇 — 對齊「自己用爽 / 不過早 over-engineer」精神 + Dashboard PetraInbox section 已 cover 精準 position UX
7. **Discord /retry-task 指令推 Phase 4** — Dashboard 按鈕已 cover 主要 use case + short_id 手打容易踩錯
8. **不 cover Stage 75 設計實作落差**（fire-and-forget sequential vs multi-session 並存）— **Stage 77 預留 A2 完整版**（Channel + drain + bounded fan-out）/ 跟 Stage 76 互補不混做
9. **backwards-compatible 守護 7 層延續**：v4 既有 path + v5 既有 path + v5.5 既有 hardcoded fallback + Stage 70 SubtaskPlan 既有 schema + Stage 72+73 PromptResolver + Stage 74 per-Skill Model + DAG + Stage 75 兩層 queue + 本 Stage 76 retry 機制（既有 row Success=true 路徑 0 動）

---

## 驗收情境

### 場景 A：PetraInbox schema 擴 4 欄 + Migration（xUnit + production DB query）

**觸發**：`dotnet ef migrations add Stage76RetrySchema --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext` → apply Migration → SQL `\d petra_inbox` query

**驗證**：
- `petra_inbox` 表 13 欄位（既有 9 + 新 4：AttemptCount + MaxAttempts + NextRetryAt + DeadAt）
- 新 index `ix_petra_inbox_status_next_retry`（守 polling 對齊 backoff timing）
- 既有 row 預設值對齊（AttemptCount=0 / MaxAttempts=3）
- xUnit T1 verify schema baseline

### 場景 B：PetraInboxProcessor transient error auto retry exponential backoff（xUnit + production log）

**觸發**：xUnit mock `PetraOrchestratorService.StartAsync` return `PetraOrchestratorResult.Failure(sessionId, [], "HttpException 5xx")` → `ProcessOnePendingAsync` 接

**驗證**：
- T2：row Status='pending' + AttemptCount=1 + NextRetryAt = StartedAt + 30s × ±20% jitter
- 下次 polling tick（30s 後）NextRetryAt 滿足 → pickup retry
- 第 2 次 fail → AttemptCount=2 + NextRetryAt = +60s
- T8：seed pending row with `NextRetryAt = NOW() + 60s` → 立刻 Processor 0 pickup（守 backoff）

### 場景 C：PetraInboxProcessor business rule fail-fast 不 retry（xUnit）

**觸發**：xUnit mock `PetraOrchestratorService.StartAsync` return `PetraOrchestratorResult.Failure(sessionId, [], "Token 守門：全域本月用量 10,108,845 超過全域月限 10,000,000")`

**驗證**：
- T3 + T7：`PetraErrorClassifier.Classify(result.ErrorMessage)` → `ErrorCategory.BusinessRule`
- row Status='failed'（不是 'pending'）+ AttemptCount=0（首次 fail / 不增 count）+ ErrorMessage 寫清楚

### 場景 D：Dead Letter pattern — exhausted attempts 標 status='dead'（xUnit）

**觸發**：xUnit seed row with AttemptCount=2 + 模擬 transient fail 第 3 次

**驗證**：
- T4：第 3 次 fail → AttemptCount=3 ≥ MaxAttempts=3 → 切 Status='dead' + DeadAt=NOW() + ErrorMessage 寫累積 3 次 fail 訊息
- Processor 後續 polling 0 pickup（GetNextPendingAsync 條件 Status='pending'）
- 等 Dashboard 重跑按鈕觸發

### 場景 E：queuePosition 簡化顯示對齊（xUnit）

**觸發**：xUnit 模擬 2 task 並行寫 inbox（同 source=dashboard）

**驗證**：
- T5：兩 row 都成功寫入 / 0 race exception
- boss_interactions Reply 顯示「task 已接收（inbox=<short_id>）— 請於 Dashboard 操作中心追蹤進度」（不顯示精確 N）
- 用戶看精準 position → 連 Dashboard InteractionCenter PetraInbox section（既有 Stage 75 §5a 已 cover live update）

### 場景 F：Dashboard 重跑 failed/dead task 按鈕（手動驗 / Aria gate2 範圍）

**觸發**：production seed 一個 Status='dead' row → Dashboard InteractionCenter 開 → 點該 row 的「重跑」按鈕

**驗證**：
- T6：`PetraInboxRepository.RequeueAsync` 觸發 / row reset 完整（Status='pending' + AttemptCount=0 + 5 個 timestamp/error 欄全 null）
- Dashboard SignalR live update 即時顯示 row 變 pending
- PetraInboxProcessor 下次 polling tick（3s 後）真實 pickup retry
- Bot log 含「PetraInboxProcessor 接手 row=...（requeued by Dashboard）」訊號

### 場景 G：Trial_v21 揭 PetraInboxProcessor Status='failed' fix 0 regression（xUnit）

**觸發**：xUnit mock `PetraOrchestratorService.StartAsync` return `Failure` result（Trial_v21 中段 commit `9b433a4` 修法生效）

**驗證**：
- result.Success=false 對齊 ErrorCategory classification 路徑（Transient → retry / BusinessRule → fail-fast / Permanent → fail-fast）
- 0 走「直接 MarkCompletedAsync」舊 bug 路徑

### 場景 H：v4 既有 path 0 regression

**觸發**：SQL 切 `Workflow:UsePetraOrchestratorV5=false`（v4 path） + 跑 Mock task

**驗證**：
- Bot log 0 含「PetraInbox」字樣 / 走 v4 既有 AgentQueueProcessor
- v4 既有 path 0 動 PetraInbox schema / retry 邏輯 0 觸發
- v4 path 既有 baseline 行為 0 改變

### 場景 I：Trial_v22 真實業務驗（多 task 並送 + retry path 真實 fire）

**觸發**：Trial_v22 開跑前 SQL 確認 5 v5.5 flag production active + 連送 2-3 task

**驗證**：
- 全 task 都正常 process（Success path 對齊 Trial_v21 baseline）
- 業務正確性對齊 Trial_v19/v20/v21 baseline
- **retry path 真實驗** — Trial_v22 期間若任意 task fail（transient / business rule）→ PetraInboxProcessor 正確分類 + auto retry 或 fail-fast
- **per-Talent lock contention 仍 0 fire**（Stage 76 不修 fire-and-forget — Stage 77 才修）/ 對齊 Stage 76 範圍邊界明示

---

## 技術約束

- 環境細節 source of truth 對齊 [workflow_aria.md 第三節 A 第 7 條紀律](../../memory/workflow_aria.md)
- PetraInbox schema 擴對齊 Stage 27 既有 `agent_queues` DB-as-Queue retry pattern + Stage 67 既有 race-safe seed 紀律 + ef-core.md Migration 紀律（雙 startup-project `src/AiTeam.Dashboard` + `--context AppDbContext`）
- PetraInboxProcessor retry path 對齊 Stage 27 既有 `AgentQueueProcessor` retry 紀律（v4 path reference）
- PetraErrorClassifier 對齊既有 PetraOrchestratorService catch Exception path（[`PetraOrchestratorService.cs:178`](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L178)）
- Dashboard 重跑按鈕對齊既有 [`BossInteractionRepository`](src/AiTeam.Data/Repositories/BossInteractionRepository.cs) + Stage 75 §5a InteractionCenter PetraInbox section live update 紀律
- xUnit test 對齊 Stage 75 既有 Stage75InboxQueueTests baseline pattern
- 對齊 [`docs/conventions/csharp.md`](../conventions/csharp.md) / [`ef-core.md`](../conventions/ef-core.md) / [`refactor-sop.md`](../conventions/refactor-sop.md)
- backwards-compatible 守護 7 層延續

---

## ⚠️ Aria 預警（對齊 Stage 73-75 大規模架構級重構教訓 + 一般架構級重構區間實證）

**Stage 76 raw 預估評估**（對齊 calibration_anchors 一般架構級重構區間 ×0.43-0.60 第 5 資料點候選）：

- Plan 階段 read existing（PetraInbox entity + PetraInboxRepository + PetraInboxProcessor + CeoAgentService + InteractionCenter + PetraOrchestratorService catch path）~20-30K
- 機制層 code 改動 ~25-35K（schema 擴 4 欄 + Migration + Repository 新 3 method + Processor retry path + ErrorClassifier + InteractionCenter 重跑按鈕 + RequeueAsync）
- 既有 method body 改寫 + helper 抽 thinking ~10-15K
- xUnit case 起草 ~20-30K（8 case + InMemory DB pattern + ErrorClassifier pattern matching + RequeueAsync trigger）
- Aria 二檢 round-trip buffer ~20-30K
- **raw 估算 ~95-140K × 0.50 ≈ 50-70K 總 context**

**Model 推薦**：
- 🥇 **Opus 200K + high**（推薦 / safety buffer 充裕 / 對齊 Trial_v21 揭精細業界紀律 trade-off 需深推理）
- 🥈 **Sonnet 200K + high**（也夠 / cost 較低）
- ❌ Opus 1M + ultrathink — 過頭（不是大規模架構級重構 / Stage 73-75 級規模才推 Opus 1M）

**cost 預估**：$3-5 per cycle

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-18 | 規劃書建立 — v3.66.0 / M+ 規模 / v5.5 Phase 3 補強（task retry / resume 機制基礎建設 + Trial_v21 揭修補類項目）。**戰略脈絡**：Trial_v21 🟡 部分過揭 1 🔴 設計實作落差（fire-and-forget 留 Stage 77）+ 2 🟡 工程細節（queuePosition race + Status='failed' bug 已修）+ Christ 戰略 question 點破「task 意外停止後架構是否能重跑？」答案「不行」揭 production resilience gap → Christ 2026-05-18 拍板 A2 完整版路線 + Stage 76 範圍重排「功能性 + 修補類」+ WebUI Talent CRUD + Effort + G Token monitoring 推 Stage 78+。**8 子項**：① PetraInbox schema 擴 4 欄（AttemptCount + MaxAttempts + NextRetryAt + DeadAt）+ Migration ② PetraInboxProcessor retry path（exponential backoff 30s × 2 × max 3 + ±20% jitter）③ PetraErrorClassifier（Transient / BusinessRule / Permanent ErrorCategory enum）④ Dead Letter pattern（Status='dead' + DeadAt + MarkDeadAsync）⑤ queuePosition race 修法（🥇 簡化顯示 / 不算精準 N）⑥ Dashboard 重跑按鈕（InteractionCenter PetraInbox section 加 action + RequeueAsync）⑦ xUnit 8 case ⑧ version bump v3.66.0。**計劃前 WebSearch 結論段 5 議題完整 incorporated**（PG queue retry pattern + multi-agent 5 failure modes + LLM retry 標準 config + Polly/Hangfire/BackgroundService 選型 + 人工介入路徑業界紀律）。**設計決策核心**：business rule rejection fail-fast 不 retry（Token 守門 mode 4 紀律）+ Dead Letter 等人工介入（Anthropic claude-progress.txt + Dashboard 重跑按鈕 hybrid 業界主流）+ Discord /retry-task 推 Phase 4（Dashboard cover 主要 use case）+ Stage 76 不修 fire-and-forget 留 Stage 77 互補。**驗收 9 場景**：A schema + Migration / B transient retry exponential backoff / C business rule fail-fast / D Dead Letter exhausted attempts / E queuePosition 簡化顯示 / F Dashboard 重跑按鈕 / G Trial_v21 Status='failed' fix 0 regression / H v4 path 0 regression / I Trial_v22 真實業務驗（多 task 並送 + retry path 真實 fire / per-Talent lock contention 仍 0 fire 留 Stage 77）。**校準錨預期**：對齊一般架構級重構區間 ×0.43-0.60 第 5 資料點候選 / raw 95-140K × 0.50 ≈ 50-70K 總 context / Opus 200K + high 推薦 / cost $3-5。**Phase 3 完整收口路徑**：73 ✅ → 74 ✅ → 75 ✅ → **76**（retry 機制 + 修補類 / 本 Stage）→ **77 預留**（fire-and-forget A2 完整版 Channel + drain + bounded fan-out）→ **78+ 預留**（WebUI Talent CRUD + Effort 擴展 + G Token monitoring 視覺化）→ v5.5 完整收口。**下一步**：Forge 實作 + Aria gate1 Tier 0+1 + Trial_v22 真實業務驗 → 通過後 Stage 77 開（fire-and-forget A2 完整版）。 |
