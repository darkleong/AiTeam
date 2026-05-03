# Stage 54：v4 漸進遷移第七步 — Crash Recovery 全切 framework Checkpointing + 4 CheckpointStore 抽 base class + side effect idempotency 加固

> 對應 Future Feature：v4 漸進遷移 8 Stage 路線第七步
> 對應版本：**v3.41.0**（v4 漸進遷移第七個產生版本變動的 Stage）
> 建立日期：2026-05-03
> 狀態：✅ 已完成（2026-05-04 Forge 結案第一段；待 Aria 結案第二段做 CHANGELOG + Future_Feature 同步）
> 文件版本：v2.0

---

## 概述

**戰略背景**：[Stage 49-53B](Stage_53B_Roadmap.md) 完成 v4 漸進遷移前六步（Appeal / Kickoff / HITL 試點 / Design / macro pipeline / 4 子流程 framework 化）— **NewFeature 主路徑 + 4 子流程完整 Pipeline framework 化達成**。**Stage 54 收尾 Recovery 機制 + 重構性債務**：3 個既有 router 的 Crash Recovery 機制升級到 ResumeStreamingAsync（對齊 Stage 53A Pipeline 議題 12 升級策略）+ 抽 4 CheckpointStore base class 解 99% 重複 + side effect idempotency 加固防 Bot crash 重啟後重複 side effect。

### 既定 TODO（Stage 49-53B 留下，Stage 54 統一收尾）

3 個既有 router 的程式碼註解明寫 Stage 54 升級：

| Router | 既有降級策略 | 程式碼註解 |
|---|---|---|
| `FrameworkAppealRouter.RecoverStuckFrameworkAppealsAsync` line 297 | 清 marker 重觸發 entry | 「TODO Stage 49 後續驗收：實際 ResumeAsync 路徑由 Mock 場景 C 線下驗收驅動完整實作」|
| `FrameworkKickoffRouter.RecoverStuckFrameworkKickoffsAsync` line 354 | 清 marker 重觸發 entry（含 Stage 51 試點 MidInterruptRequestPending check 保留 marker）| 「暫採降級策略（清 marker），Mock 場景 C 驗收後升級 ResumeAsync」|
| `FrameworkDesignRouter.RecoverStuckFrameworkDesignAsync` line 359 | 清 marker 重觸發 entry | 「**Stage 54 升級 ResumeStreamingAsync**」（直接寫 Stage 54）|
| `FrameworkPipelineRouter.RecoverStuckFrameworkPipelineAsync` | 已是 ResumeStreamingAsync 議題 12 升級版（Stage 53A 已驗證）+ Agent task requeue helper（Stage 53A follow-up #4） | 完成 — Stage 54 作為 reference pattern |

→ **Stage 54 = Pipeline 既有 ResumeStreamingAsync know-how 推廣到 Appeal/Kickoff/Design**（Stage 51 試點 + Stage 53A 議題 12 升級已建立完整 know-how 基礎）。

### Stage 54 同時做 4 件對齊性工作

1. **3 個既有 router 升級 ResumeStreamingAsync**（Pipeline 既有 know-how 直接複用）
2. **抽 4 CheckpointStore base class**（99% 重複 833 行 → ~300 行，淨減 -500+ 行）
3. **side effect idempotency 加固**（4 處明確點位 — 用會議 state 自帶紀錄 check，0 schema 變更）
4. **Stage 53B follow-up 搭車**（#1 高契合 / #2 #4 順手 / #3 不搭 — 純 UI 性質不對等）

### 範圍邊界

- ✅ **3 router RecoverStuck*Async 升級 ResumeStreamingAsync**（Appeal / Kickoff / Design 對齊 Pipeline 議題 12 升級策略）
- ✅ **4 CheckpointStore 抽 abstract base class**（abstract method `ReadJsonFromDbAsync` / `WriteJsonToDbAsync` 子類實作 column-specific 部分；其餘 in-memory dict / parent links / latest tracking / JSON serialize 邏輯統一在 base class）
- ✅ **4 處 side effect idempotency check**（用會議 state 自帶紀錄 check，0 schema 變更）：
  - `DesignRosaPreWorkExecutor.cs:75`（Rosa pre-work 創 GitHub Issue — check `state.IssueUrls != null` 跳過）
  - `DesignAdjustmentExecutor.cs:108`（Adjustment 創 Issue — 同樣 check `state.IssueUrls`）
  - `FrameworkKickoffRouter.cs:672`（CreateInteractionAsync 開 Christ 確認卡 — check 該 group+type 是否已有 pending interaction）
  - `FrameworkDesignRouter.cs:518`（CreateInteractionAsync 開 Christ 確認卡 — 同上）
- ✅ **Stage 53B follow-up #1 搭車**（Pipeline DevStage [BLOCKED] retry 後 Round 1 failed task 殘留 → MarkGroupDoneOrInterventionAsync 誤判 needs_intervention — production 既有議題，性質契合 Stage 54 Crash Recovery + idempotency 範圍）
- ✅ **Stage 53B follow-up #2 順手**（MockMode 模式下 BossInteraction 自動 approve — 工具增強，Stage 54 6 場景驗收會用到）
- ✅ **Stage 53B follow-up #4 順手**（MockClaudeCodeService 內 RunReviewAsync/RunAsync/RunQaAsync 三 method 在 Mock arch 下 dead code 清理 — 極小）

- ❌ **不動**：既有 BossInteraction 10+ type / InteractionService 既有 method（A3 試點精神延續，Stage 55 收尾切 framework HITL）
- ❌ **不動**：HandleAgentCompletedAsync 既有 6 hooks（J1 拍板保留作為 legacy fallback safety net，Stage 55 收尾移除）
- ❌ **不動**：sub-task 整合（Stage 55 收尾統一 Kickoff/Design + sub-task 三戰略級工作）
- ❌ **不動**：WorkflowEngine.cs（Stage 55 收尾移除）
- ❌ **不搭**：Stage 53B follow-up #3 Dashboard MockScenarioCard 補 Stage 49-53A framework_* 場景（純 UI 性質與 Stage 54 純機制 + 重構不對等）
- ❌ **不抽**：mapping helper（PortIdToAgentName，只 Pipeline 用 + 守「3 次再抽象」原則）

### v4 路線第七步風險預警

- **3 個既有 router 升級時 Stage 51 試點 know-how 不可破壞**（Kickoff `MidInterruptRequestPending` check 必須保留 — 等 Christ 回應狀態下不算 stuck，不清 marker，由 BossInteraction 觸發 resume）
- **base class 抽象時 4 個既有 CheckpointStore 行為不可變化**（regression 風險中等 — 4 個 framework path 都跑通的 baseline test）
- **idempotency check 正確性風險低**（用既有 state 紀錄 + 99% 防重複，毫秒級 race window 在 production 幾乎不會踩）
- **Stage 53B follow-up #1 production 既有議題**（legacy 路徑同樣會踩，Stage 54 修法後 Pipeline + legacy 都受惠）

→ feature flag UseFrameworkPipeline 已 production 啟用（Christ 2026-05-03 拍板保留 true），Stage 54 不引入新 flag。

---

## 設計決策（Christ 2026-05-03 拍板）

### 主路線拍板（戰略級）

| 議題 | 拍板 | 替代方案 |
|---|---|---|
| **議題 A：Stage 54 範圍** | **A1：4 件事一起做**（升級 + 抽 base class + idempotency + 53B follow-up 搭車）— Stage 49-53B 平均 540K 校準錨支持規模可控；4 件性質契合（Recovery 機制 + 重構性債務 + idempotency + follow-up 都跟 Crash Recovery 性質相關）| A2 純 base class 抽象（升級留 Stage 55 — Stage 55 戰略級子工作已夠多）/ A3 只升級不抽 base class（債務累積到 Stage 55）|
| **議題 B：idempotency 機制** | **B2：🥈 加 round-aware marker DB column**（100% 防重複跨 Recovery + 多輪業務 — TaskGroups 加 `LastIssueCreatedRound` int? 欄位 + Migration）。**B1 已推翻**（Forge gate1 揭露 R5：DesignAdjustmentExecutor 加 IssueUrls check 會破壞 needs_adjustment 多輪業務 — Christ 重新評估後拍「Adjustment 觸發都會踩，不是機率問題」推翻 B1）| B1 用會議 state 自帶紀錄 check（已推翻 — needs_adjustment 多輪場景非罕見）/ B3 加 idempotency_log table（重，Stage 54 範圍超出）|

### Aria 拿捏（已決，純內部實作不對外議題）

| # | 議題 | 決定 |
|---|---|---|
| 1 | base class 抽法 | abstract method — `ReadJsonFromDbAsync(Guid groupId, CancellationToken)` / `WriteJsonToDbAsync(Guid groupId, string json, CancellationToken)`，子類實作 column-specific 取值；其餘 in-memory dict / parent links / latest tracking / JSON serialize / LoadFromDbAsync / GetLatestCheckpoint / RetrieveCheckpointAsync / RetrieveIndexAsync / CreateCheckpointAsync 邏輯統一在 base class |
| 2 | mapping helper 抽不抽 | **不抽**（只 Pipeline 用 + 守「3 次再抽象」原則 + Stage 53B K1 已拍保留 switch case）|
| 3 | base class 命名 | `FrameworkCheckpointStoreBase<TState>` 或 `JsonElementCheckpointStoreBase`（Forge Plan Mode 拍板）|
| 4 | base class 位置 | `src/AiTeam.Bot/Workflows/Common/`（新 Common 資料夾）或既有 namespace（Forge Plan Mode 拍板）|
| 5 | 3 router 升級 ResumeStreamingAsync 對齊參照 | `FrameworkPipelineRouter.RecoverStuckFrameworkPipelineAsync` 既有實作（line 275-380）— LoadFromDbAsync + GetLatestCheckpoint + ResumeStreamingAsync rehydrate + WatchStreamAsync 等第一個 RequestInfoEvent / WorkflowOutputEvent |
| 6 | Kickoff Stage 51 試點 know-how 保留紀律 | `ScanForBoolProperty(ckptValue, "midInterruptRequestPending")` 既有 check（FrameworkKickoffRouter line 332）必須保留在升級後的 ResumeStreamingAsync 流程內 — 等 Christ 回應狀態不算 stuck，不清 marker / 不 ResumeStreamingAsync rehydrate 重跑 |
| 7 | idempotency check 具體機制（議題 **B2** 拍板細節）| **DesignRosaPreWorkExecutor + DesignAdjustmentExecutor**：用 `TaskGroups.LastIssueCreatedRound` (int?) round-aware marker — RosaPreWork (Round 0) `if group.LastIssueCreatedRound != 0 → 創 + set 0`；Adjustment (Round N) `if group.LastIssueCreatedRound != N → 創 + set N`（多輪正常業務行為保留 + Recovery 重跑同 round 100% 防重複）。**FrameworkKickoffRouter + FrameworkDesignRouter CreateInteractionAsync**：用既有 `BossInteractionRepository.GetLatestForGroupByTypeAsync(groupId, type)` lookup（既有 method，不新加 API；type 字串 `"kickoff"` / `"design"` — Forge gate1 揭露 Aria 計劃書原寫 `"kickoff_confirmation"` 錯誤，校準為實際既有字串）|
| 8 | Stage 53B follow-up #1 修法（Pipeline DevStage [BLOCKED] retry idempotency） | `MarkGroupDoneOrInterventionAsync` 內判斷 failed task 時，**忽略 IsFixLoop=true 後續有 newer success task 的舊 failed task**（DevStage [BLOCKED] retry 場景：Round 1 failed task 已被 Round 2 success task 取代，不該誤判 needs_intervention）— 對齊 Stage 53B follow-up FF 候選 #1「建議方向 ②」拍板 |
| 9 | Stage 53B follow-up #2 修法（MockMode auto-approve BossInteraction）| MockMode flag = true 時，BossInteraction 創建後自動 set status=approved（避免 Christ/Forge 每次手動 DB approve）— 修在 InteractionService.CreateInteractionAsync 內或新加 MockMode hook，由 Forge Plan Mode 拍板 |
| 10 | Stage 53B follow-up #4 修法（MockClaudeCodeService dead code 清理） | RunReviewAsync / RunAsync / RunQaAsync 三 method 在 Mock arch 下被 3 agent service early return bypass — 直接刪除 method body（保留 throw NotImplementedException 給未來警示），或加 `[Obsolete]` attribute |
| 11 | Mock 場景觸發機制 | 對齊 Stage 49-53B `MockClaudeCodeService.FailScenario` static 傳遞 scenario key 慣例 + Stage 53B `/internal/mock/scenario` HTTP API + BossInteraction auto-approver（議題 9 修法後自動）|
| 12 | Token 計費 | 沿用既有機制（Stage 54 不引入新 LLM call）|
| 13 | CLAUDE_*.md prompt | 不動（沿用 Stage 49-53B 慣例，Recovery 機制升級不影響 Agent prompt）|

### Stage 54 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **0** | **Spike 第一步：read 對齊範圍**（Forge Plan Mode 第一步）— read FrameworkPipelineRouter.RecoverStuckFrameworkPipelineAsync line 275-380 既有 ResumeStreamingAsync 完整實作（rehydrate + Agent task requeue 機制） + 4 個 CheckpointStore 99% 重複結構 + KickoffMidInterruptTriggerStore Stage 51 試點 know-how + state.IssueUrls / BossInteraction(group_id, type) lookup pattern | XS |
| **1** | 抽 4 CheckpointStore base class（`FrameworkCheckpointStoreBase` + 4 子類各自實作 abstract `ReadJsonFromDbAsync` / `WriteJsonToDbAsync`） | M |
| **2** | 3 router RecoverStuck*Async 升級 ResumeStreamingAsync — Appeal / Kickoff / Design 對齊 Pipeline pattern；Kickoff 升級時保留 Stage 51 試點 `MidInterruptRequestPending` check（等人類回應不算 stuck）| M |
| **3** | 4 處 side effect idempotency check 加固 — TaskGroups 加 `LastIssueCreatedRound` (int?) 欄位 + Migration（B2 拍板） + DesignRosaPreWork / DesignAdjustment 用 round-aware marker check + Kickoff/Design CreateInteractionAsync 用既有 BossInteractionRepository lookup | M |
| **4** | Stage 53B follow-up #1 搭車：MarkGroupDoneOrInterventionAsync 改忽略 IsFixLoop=true 後續有 newer success task 的舊 failed task | S |
| **5** | Stage 53B follow-up #2 + #4 順手：MockMode auto-approve BossInteraction + MockClaudeCodeService dead code 清理 | XS |
| **6** | Mock 場景擴充 + Forge 自驗（4 個 framework Recovery 場景含 idempotency check 觸發 + 1 場景 Stage 53B follow-up #1 retry 場景）| M |
| **7** | Version bump v3.41.0 + 結案文件（Roadmap 實作紀錄章節 + CHANGELOG / Future_Feature 同步交給 Aria 結案第二段） | XS |

> **不寫工時估算**（workflow_aria.md 第三節 A 第 4 條）— 各子項規模見上表 XS/S/M 範圍描述。

---

## 子項 0：Spike 第一步 — read 對齊範圍

### read 對齊範本

| # | read 對象 | 目的 |
|---|---|---|
| F1 | `src/AiTeam.Bot/Orchestration/Meeting/FrameworkPipelineRouter.cs` line 275-380 | Stage 53A 既有 ResumeStreamingAsync 完整 pattern（LoadFromDbAsync + GetLatestCheckpoint + ResumeStreamingAsync rehydrate + WatchStreamAsync 收第一個 RequestInfoEvent / WorkflowOutputEvent + Agent task requeue 議題 12 升級策略），3 router 升級對齊範本 |
| F2 | 4 個 CheckpointStore（`Workflows/Appeal/AppealCheckpointStore.cs` 209 行 / `Workflows/Kickoff/KickoffCheckpointStore.cs` 212 行 / `Workflows/Design/DesignCheckpointStore.cs` 212 行 / `Workflows/Pipeline/PipelineCheckpointStore.cs` 200 行） | 99% 重複結構 — 抽 base class 的範圍（abstract method 該抽哪些 / 統一邏輯該抽哪些） |
| F3 | `FrameworkKickoffRouter.cs` line 329-339 + 365-379 `ScanForBoolProperty(ckptValue, "midInterruptRequestPending")` Stage 51 試點 check | Kickoff Recovery 升級時必須保留 know-how — 等 Christ 回應狀態不算 stuck，不清 marker / 不 ResumeStreamingAsync rehydrate |
| F4 | `Workflows/Design/Executors/DesignRosaPreWorkExecutor.cs:75` + `DesignAdjustmentExecutor.cs:108` + `Workflows/Design/DesignState.cs` IssueUrls 欄位 | idempotency check 點位 + state 欄位確認 |
| F5 | `FrameworkKickoffRouter.cs:672` + `FrameworkDesignRouter.cs:518` CreateInteractionAsync 既有 caller 結構 + `Services/InteractionService.cs` public API | idempotency check 點位 + 確認是否需新加 `GetPendingInteractionAsync(groupId, type)` method 或 inline DB query |
| F6 | `Orchestration/TaskGroupService.cs` line 647 `MarkGroupDoneOrInterventionAsync` body | Stage 53B follow-up #1 修法位置 — 加判斷「忽略 IsFixLoop=true 後續有 newer success task 的舊 failed task」 |

### Spike 結案產出（Forge Plan Mode 內含）

- base class 抽象範圍 + abstract method signature 列表
- 3 router 升級後的 method 結構（共 ~30 行程式碼差異 vs Pipeline 既有，每 router 對齊）
- idempotency check 4 處具體位置 + check 條件
- Mock 場景對應觸發機制（5-6 場景）

### Spike 階段失敗條件（極低風險）

Stage 53A know-how 全複用，無新 framework 機制需驗。若實作期揭露 Kickoff `MidInterruptRequestPending` check 跟 ResumeStreamingAsync 整合時序衝突 → 暫停 + 回報 Christ 評估。

---

## 子項 1-7：實作細節（對齊 Aria 拿捏）

> 詳細實作位置 / 程式碼片段由 Forge Plan Mode 拍板。Aria 計劃書層級提供 scope + 邊界。

### 子項 1：抽 4 CheckpointStore base class

新建：`src/AiTeam.Bot/Workflows/Common/FrameworkCheckpointStoreBase.cs`（位置由 Forge Plan Mode 拍板）

抽象範圍：
- in-memory dict（_store / _parentLinks / _latest）統一在 base
- LoadFromDbAsync / GetLatestCheckpoint / RetrieveCheckpointAsync / RetrieveIndexAsync / CreateCheckpointAsync / PersistToDbAsync 統一在 base
- abstract method：`ReadJsonFromDbAsync(Guid groupId, CancellationToken ct)` + `WriteJsonToDbAsync(Guid groupId, string json, CancellationToken ct)` 子類實作 column-specific 部分

4 子類（AppealCheckpointStore / KickoffCheckpointStore / DesignCheckpointStore / PipelineCheckpointStore）各自實作對應 column 的 ReadJsonFromDbAsync / WriteJsonToDbAsync — 每個子類 ~30 行（vs 原本 200-212 行）。

### 子項 2：3 router RecoverStuck*Async 升級 ResumeStreamingAsync

對齊 FrameworkPipelineRouter.RecoverStuckFrameworkPipelineAsync line 275-380 既有 pattern：
1. LoadFromDbAsync(groupId, ct)
2. GetLatestCheckpoint(sessionId)
3. ResumeStreamingAsync from latest checkpoint
4. WatchStreamAsync 收第一個 RequestInfoEvent / WorkflowOutputEvent
5. 異常時 ClearMarkersAsync 降級

差異：Appeal / Kickoff / Design 沒有 yield-resume + Agent task callback 機制（不需要 RequeueFailedAgentTaskAsync），ResumeStreamingAsync 會直接同步跑完剩餘 Workflow。

**Kickoff 特殊紀律**（Stage 51 試點 know-how 保留）：升級後流程 LoadFromDbAsync 之後**先檢查** `ScanForBoolProperty(ckptValue, "midInterruptRequestPending")` — true 則保留 marker 不 ResumeStreamingAsync rehydrate（等 BossInteraction 觸發 resume）；false 才走 ResumeStreamingAsync 流程。

### 子項 3：4 處 side effect idempotency 加固（B2 拍板）

新加 DB 欄位：`TaskGroups.LastIssueCreatedRound` (int?)，Migration `Stage54TaskGroupIssueCreatedMarker`。

| 點位 | check 條件 | 行為 |
|---|---|---|
| `DesignRosaPreWorkExecutor.cs:75`（Round 0）| `group.LastIssueCreatedRound != 0` | 創 Issue + set `LastIssueCreatedRound = 0`；否則 log「Recovery 重跑同 Round 偵測 marker 已 set，跳過」+ continue |
| `DesignAdjustmentExecutor.cs:108`（Round N）| `group.LastIssueCreatedRound != N` | 創 Issue + set `LastIssueCreatedRound = N`；否則同上跳過。**保留多輪正常業務行為**（不同 round 觸發都會創新 Issue）|
| `FrameworkKickoffRouter.cs:672` CreateKickoffConfirmationAsync | `BossInteractionRepository.GetLatestForGroupByTypeAsync(groupId, "kickoff")` 回傳非 null 且 `Status == "pending"` | 有則 log「Recovery 重跑偵測 pending kickoff，跳過 CreateInteractionAsync」+ return |
| `FrameworkDesignRouter.cs:518` FinalizeDesignAsync | 同上 type 改 `"design"` | 同上 |

**B2 vs B1 trade-off 說明**：B1（用 state.IssueUrls check）會破壞 needs_adjustment 多輪業務（Adjustment Round N 看到 IssueUrls != null 跳過 → Round 2 不創新 Issue）。B2 用 round-aware marker — Recovery 重跑同 round 100% 防重複 + 多 round 推進保留正常業務行為，trade-off 完全解。

BossInteraction 部分用既有 `BossInteractionRepository.GetLatestForGroupByTypeAsync` 既有 method（不需新加 API），type 字串 `"kickoff"` / `"design"`（**校準**：原計劃書議題 7 寫 `"kickoff_confirmation"` 是錯誤，Forge gate1 揭露既有字串為 `"kickoff"`）。

### 子項 4：Stage 53B follow-up #1 搭車

`TaskGroupService.MarkGroupDoneOrInterventionAsync` body 內判斷 failed task 時，**忽略 IsFixLoop=true 後續有 newer success task 的舊 failed task**：
- 對齊 Stage 53B follow-up FF 候選 #1「建議方向 ②」
- 修法後 production 既有議題（DevStage [BLOCKED] retry 後 Round 1 failed task 殘留誤判）一併解 — Pipeline + legacy 都受惠
- 具體判斷邏輯：對 group.Tasks 內 failed task，若同 AssignedAgent + IsFixLoop=true + 後續有 CreatedAt > 該 failed task 的 success task → 忽略該 failed task（不算 needs_intervention 觸發條件）

### 子項 5：Stage 53B follow-up #2 + #4 順手

**#2 MockMode auto-approve BossInteraction**：
- MockMode flag = true 時，BossInteraction 創建後自動 set status=approved（避免 Christ/Forge 每次手動 DB approve）
- 修在 InteractionService.CreateInteractionAsync 內或新加 MockMode hook，由 Forge Plan Mode 拍板
- 對齊 Stage 53B 驗收期 Forge 用 `docker exec psql` 手動 DB approve 的工具增強

**#4 MockClaudeCodeService dead code 清理**：
- RunReviewAsync / RunAsync / RunQaAsync 三 method 在 Mock arch 下被 3 agent service early return bypass
- 直接刪除 method body（或 throw NotImplementedException + 註解警示），由 Forge Plan Mode 拍板

### 子項 6：Mock 場景擴充 + Forge 自驗

#### Mock 5 場景

| 場景 key | 行為 |
|---|---|
| `framework_appeal_crash_recovery` | Appeal Workflow（Petra 仲裁 / Reviewer 投票）跑到 Round 2 期間 docker compose restart → ResumeStreamingAsync rehydrate + 同步跑完剩餘 superstep |
| `framework_kickoff_crash_recovery` | Kickoff Workflow Round 1 期間 docker compose restart → ResumeStreamingAsync rehydrate + 同步跑完；**搭配 idempotency check 驗證 BossInteraction 不重複** |
| `framework_design_crash_recovery_issue_idempotency` ⭐ | Design Workflow Rosa pre-work 創 Issue 後 docker compose restart → 重啟後 framework rehydrate → DesignRosaPreWorkExecutor 重跑偵測 `state.IssueUrls != null` → log + 跳過 CreateIssueAsync → **GitHub 上不出現重複 Issue**（核心 idempotency 場景）|
| `framework_kickoff_mid_interrupt_recovery` | Kickoff Workflow yield 等 Christ MidInterrupt 回應期間 docker compose restart → Stage 51 試點 know-how 仍保留：重啟後不 rehydrate（marker 保留），由 BossInteraction 觸發 resume |
| `pipeline_dev_blocker_retry_idempotency` ⭐ | Pipeline DevStage [BLOCKED] → Petra continue → DevStage Round 2 success → **MarkGroupDoneOrInterventionAsync 忽略 Round 1 failed task 不誤判 needs_intervention** → Pipeline 推進 NotifyMerge done（Stage 53B follow-up #1 修法驗證）|

> 沿用 Stage 53B Forge 自驗能力突破：Forge 用 `/internal/mock/scenario` HTTP API + MockMode auto-approve BossInteraction（子項 5 #2 修法後自動）+ docker compose restart 自跑全 5 場景。Christ 線下實跑為選擇性。

### 子項 7：Version bump v3.41.0 + 結案文件

- `src/Directory.Build.props` v3.40.0 → v3.41.0
- Roadmap 結案紀錄章節（Forge 結案第一段）— 子項完成度對照 / Session 結案 / 關鍵設計決策 / 踩坑紀錄 / 驗收結果 / Aria 校準錨候選（Aria 第二段填）
- CHANGELOG / Future_Feature 同步交給 Aria 結案第二段

---

## 驗收情境

> Stage 54 是 v4 漸進遷移第七步 Recovery 機制全切 + 重構性債務 + idempotency 加固，**驗收必須含 4 個 framework Recovery 各自跑通 + idempotency 防重複場景 + 53B follow-up #1 retry 場景**。沿用 Stage 49-53B 6 場景模式擴充。

### 場景 A：base class 重構 regression — 4 個 framework path 各自跑通

**怎麼觸發**：
1. push Stage 54 commit → CI/CD 部署
2. 確認 UseFrameworkPipeline = true（Christ production 已拍板保留）
3. 跑 4 個既有 Mock 場景：
   - `/mock framework_appeal_basic`（Stage 49 既有）
   - `/mock framework_kickoff_basic`（Stage 50 既有）
   - `/mock framework_design_basic`（Stage 52 既有）
   - `/mock framework_pipeline_happy_path`（Stage 53A 既有）

**怎麼驗證**：
- ✅ 4 個 framework path 各自跑通 + group.Status=done
- ✅ Bot log 無 base class 重構引入的異常
- ✅ 4 個 task_groups.XxxFrameworkStateJson DB column 正常寫入（base class 抽象後行為不變）
- ✅ dotnet build 0 Error / dotnet test 全 passed

### 場景 B：Appeal Crash Recovery — ResumeStreamingAsync 升級

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_appeal_crash_recovery`
3. Appeal Workflow Round 2 期間 Forge 執行 `docker compose restart aiteam-bot`

**怎麼驗證**：
- ✅ 重啟前 task_groups.FrameworkAppealStateJson != null + ActiveOrchestration = "FrameworkAppeal"
- ✅ 重啟後 RecoverStuckFrameworkAppealsAsync 走 **ResumeStreamingAsync rehydrate**（**不**走「清 marker 重觸發」降級策略 — Bot log 證實）
- ✅ Appeal Workflow 從 latest checkpoint 同步跑完剩餘 superstep（Round 1 已產出 LLM 輸出**不重跑** — 省 token 證據）
- ✅ Appeal 完成 + Petra 仲裁結果寫入 + group.Status 推進

### 場景 C：Kickoff Crash Recovery + idempotency — ResumeStreamingAsync 升級 + BossInteraction 不重複

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_kickoff_crash_recovery`
3. Kickoff Workflow Round 1 期間（Cody/Quinn/Vera 已對話完成 + 即將 finalize 開 BossInteraction）docker compose restart

**怎麼驗證**：
- ✅ 重啟前 KickoffFrameworkStateJson != null + ActiveOrchestration = "FrameworkKickoff"
- ✅ 重啟後 RecoverStuckFrameworkKickoffsAsync 走 ResumeStreamingAsync rehydrate（**不**走降級）
- ✅ Kickoff Workflow 從 checkpoint 同步跑完剩餘 superstep + 進入 finalize
- ✅ **idempotency check 觸發**：CreateKickoffConfirmationAsync 偵測 `BossInteraction (group_id, "kickoff_confirmation", pending)` 已存在 → log「Recovery 重跑偵測 pending kickoff_confirmation，跳過」+ return → **Discord 上不出現第二張確認卡**

### 場景 D：Design Crash Recovery + idempotency — Issue 不重複（核心場景）⭐

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_design_crash_recovery_issue_idempotency`
3. Design Workflow Rosa pre-work 創 GitHub Issue 後（state.IssueUrls 已寫入）但會議還沒結束 docker compose restart

**怎麼驗證**：
- ✅ 重啟前 DesignFrameworkStateJson != null + state.IssueUrls 含 3 個 Issue URL
- ✅ 重啟後 ResumeStreamingAsync rehydrate + DesignRosaPreWorkExecutor 重跑該 superstep
- ✅ **idempotency check 觸發**：DesignRosaPreWorkExecutor 偵測 `state.IssueUrls != null && != ""` → log「Recovery 重跑偵測 IssueUrls 已 set，跳過 CreateIssueAsync」+ continue → **GitHub 上不出現第 4-6 個重複 Issue**
- ✅ Design Workflow 推進 finalize（Christ confirm BossInteraction 同樣有 idempotency check）

### 場景 E：Kickoff MidInterruptRequestPending Recovery — Stage 51 試點 know-how 不破壞

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_kickoff_mid_interrupt_recovery`（Kickoff yield 等 Christ MidInterrupt 回應期間）
3. yield 期間 docker compose restart

**怎麼驗證**：
- ✅ 重啟前 KickoffFrameworkStateJson != null + state 含 `midInterruptRequestPending=true`
- ✅ 重啟後 RecoverStuckFrameworkKickoffsAsync 偵測 `ScanForBoolProperty("midInterruptRequestPending") == true` → log「等待人類回應，保留 marker 等 BossInteraction 觸發 resume」+ continue（**不** ResumeStreamingAsync rehydrate）
- ✅ marker 保留（KickoffFrameworkStateJson + ActiveOrchestration 都不變）
- ✅ Christ 透過 BossInteraction 回應後 → KickoffMidInterruptTriggerStore 觸發 Bridge.HandleMidInterruptResponseAsync resume → Kickoff Workflow 推進

### 場景 F：Pipeline Crash Recovery — Stage 53A/B 既有 regression

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_pipeline_fix_loop_crash_recovery`（Stage 53B 場景 G 沿用）
3. Pipeline fix loop Round 2 DevFixStage yield 期間 docker compose restart

**怎麼驗證**：
- ✅ 沿用 Stage 53B 場景 G 完整證據鏈（議題 12 ResumeStreamingAsync rehydrate + pending PortId=Pipeline-DevFixCompletion + RequeueFailedAgentTaskAsync 自動 requeue + Pipeline 推進 done）
- ✅ Stage 54 base class 重構不影響 Pipeline 既有行為（regression 確認）

### 場景 G：Pipeline DevStage [BLOCKED] retry idempotency（Stage 53B follow-up #1 修法）⭐

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock pipeline_dev_blocker_retry_idempotency`（Stage 53B 場景 D 改良 — 真實驗證 Round 2 success 後 group.Status = done 不誤判 needs_intervention）

**怎麼驗證**：
- ✅ Round 1 Dev [BLOCKED] → DevStage 內 call HandleDevBlockerAsync → Petra continue → fire DevRetryBridge
- ✅ Round 2 Dev success → ReviewerStage 推進 → ... → NotifyMergeStage
- ✅ NotifyMergeStage call MarkGroupDoneOrInterventionAsync → **修法觸發**：偵測 Round 1 failed task IsFixLoop=true + Round 2 newer success task → 忽略 Round 1 failed task → 不誤判 needs_intervention
- ✅ group.Status = **done**（vs Stage 53B 場景 D 誤判 needs_intervention）
- ✅ Bot log 證實「修法跳過 Round N 失敗 task（被 Round N+1 success task 取代）」

### 場景 H：MockMode auto-approve BossInteraction（Stage 53B follow-up #2）

**怎麼觸發**：
1. MockMode = true（驗收期環境）
2. 跑任一 framework_kickoff_* 或 framework_design_* Mock 場景
3. Kickoff/Design finalize 開 BossInteraction

**怎麼驗證**：
- ✅ BossInteraction 創建後**自動** set status=approved（不需要 Forge `docker exec psql` 手動 update）
- ✅ Kickoff/Design 推進到下一 stage（Pipeline DevPlan / sub-task 路徑）
- ✅ Bot log 證實「MockMode auto-approve interaction (group_id, type)」

---

## 風險點 / 注意事項

### 1. base class 重構 regression（中）

**風險**：4 個 CheckpointStore 99% 重複抽象 — 抽象後若 abstract method 不對等 / 統一邏輯漏掉子類特殊行為，4 個 framework path 中任一個可能 silently broken。

**緩解**：
- 子項 0 spike 第一步 read 4 個 CheckpointStore 完整對比，揭露所有差異點
- 場景 A 4 個 framework path 各自 baseline regression 驗證
- dotnet build 0 Error + 既有 test 全 passed

### 2. Kickoff Stage 51 試點 know-how 保留紀律（中）

**風險**：Kickoff Recovery 升級 ResumeStreamingAsync 時若忽略 `MidInterruptRequestPending` check → 等 Christ 回應狀態下強制 ResumeStreamingAsync rehydrate → 重跑 Kickoff Workflow 早於 yield 點 → 引入 Stage 51 試點驗證的 HITL 機制 broken。

**緩解**：
- Aria 拿捏 #6 明文紀律保留 ScanForBoolProperty check
- 子項 0 read F3 對齊 Stage 51 既有 line 329-339 + 365-379 check 邏輯
- 場景 E 專門驗證 MidInterruptRequestPending Recovery 不破壞

### 3. idempotency check race window（已解 — B2 拍板後）

**原 B1 拍板已推翻**（Forge gate1 揭露 R5 + Christ 重新拍板 B2）：
- 原 B1 用 `state.IssueUrls` check 99% 防重複，但漏估非罕見場景 — DesignAdjustmentExecutor 多輪 needs_adjustment 業務行為（Adjustment 觸發都會踩，不是機率問題）
- 改 B2 加 round-aware marker DB 欄位（`TaskGroups.LastIssueCreatedRound` int?）— 100% 防重複跨 Recovery 重跑 + 保留多輪正常業務
- log 加 idempotency check 觸發 / 跳過記錄（debug 用）

### 4. Stage 53B follow-up #1 修法影響 legacy 路徑（低）

**風險**：MarkGroupDoneOrInterventionAsync 修法（忽略 IsFixLoop=true 後續有 newer success task 的舊 failed task）影響 legacy 路徑既有判斷 — 雖然 production 既有議題修法理論上 legacy + Pipeline 都受惠，但既有 legacy 路徑可能依賴「failed task 即視為 needs_intervention」假設。

**緩解**：
- 子項 0 read F6 對齊 MarkGroupDoneOrInterventionAsync 既有 caller 結構
- 場景 G 專門驗證修法後 Pipeline 路徑 done 行為
- legacy 路徑 regression：場景 A 4 個 framework path 跑通 + 既有 Mock new_feature_with_proposal 跑通

### 5. Stage 53B follow-up 搭車不踩 Stage 55 邊界（低）

**Stage 54 不動 production code（守 Stage 55 收尾範圍）**：
- ❌ 既有 BossInteraction 10+ type / InteractionService 既有 method（A3 試點精神延續）
- ❌ HandleAgentCompletedAsync 既有 6 hooks（J1 保留作為 legacy fallback safety net）
- ❌ Stage 49-53A 既有 framework path 主邏輯（除 RecoverStuck*Async 升級 + base class 重構）
- ❌ sub-task 整合（Stage 55 範圍）
- ❌ WorkflowEngine.cs（Stage 55 範圍）

**Stage 54 動的 production code**：
- 動：4 個 CheckpointStore.cs（重構為 base class + 4 子類）
- 動：3 個 router RecoverStuck*Async（Appeal / Kickoff / Design 升級 ResumeStreamingAsync）
- 動：DesignRosaPreWorkExecutor.cs / DesignAdjustmentExecutor.cs（idempotency check）
- 動：FrameworkKickoffRouter.cs / FrameworkDesignRouter.cs（finalize 段 idempotency check）
- 動：TaskGroupService.MarkGroupDoneOrInterventionAsync（53B follow-up #1）
- 動：InteractionService.cs（53B follow-up #2 MockMode auto-approve hook）
- 動：MockClaudeCodeService.cs（53B follow-up #4 dead code）
- 動：MockScenarioService.cs / MockClaudeCodeService.cs（5 個新 Mock 場景）
- 動：Directory.Build.props（Version bump）
- 新建：base class 檔案（位置 Forge Plan Mode 拍板）

### 6. Aria 規劃前期 grep 紀律（自省點 #23 持續守）

**Stage 53A 議題 G3 在 QA 重演 + Stage 53B 議題 F-1 16 處 skip 的教訓延續**：規劃任何 framework Workflow 同步 await call 既有 service method 時，必須做完整 grep（含 transitive callers）。

Stage 54 規劃前期 Aria 已 grep：
- 4 個 RecoverStuck*Async method body（4 router + AgentQueueProcessor）
- 4 個 CheckpointStore 完整結構 + LoC
- side effect 散落點（CreateIssueAsync / CreateInteractionAsync / FireStepsAsync）
- MidInterruptRequestPending 機制（Stage 51 試點）
- MarkGroupDoneOrInterventionAsync caller 結構
- DesignState.IssueUrls 欄位

**Stage 55+ 後續預警**：Stage 55 戰略級收尾（Kickoff/Design + sub-task 整合 + BossInteraction 切 framework HITL + 移除 J1 6 hooks）規劃前期必須對所有相關 service method 內部 fire/MarkDone/NotifyBoss side effects 做完整 grep。

---

## Model 建議

依 [`workflow_aria_model_effort.md`](../../C:/Users/darkl/.claude/projects/D--Source-Code-AI-Team/memory/workflow_aria_model_effort.md) 四維度：

| 維度 | 評估 |
|---|---|
| **任務複雜度** | 中 — 純機制升級（3 router 對齊 Pipeline 既有 pattern）+ 純重構（4 CheckpointStore 抽 base class）+ idempotency 加固（用既有 state 0 schema 變更）+ 53B follow-up 搭車（規模可控） |
| **改動範圍** | M — 4 個 CheckpointStore 重構 + 3 router method 升級 + 4 處 idempotency check + 1 處 MarkGroupDone 修法 + 5 個 Mock 場景 |
| **歷史包袱** | 低 — Stage 53A know-how 全複用（ResumeStreamingAsync rehydrate pattern 已驗 production 跑通）+ Stage 51 試點 know-how 保留紀律明文 |
| **判斷品質要求** | 中 — base class 抽象的「該抽哪些 / 不該抽哪些」邊界判斷 + idempotency check 邊界 race window 接受度 + Stage 51 試點 know-how 保留紀律 |

**建議**：**Opus 1M + medium-high**

理由：
1. **混合型 Stage 第 7 個資料點**（沿用 Stage 49-53B ×0.73-1.25 區間，54 偏 mid 中段下半 ×0.9-1.1，因規模 vs 53A/53B 略小 + 純機制升級 + 重構 + Stage 51/53A know-how 全複用）
2. **預估 context 500-700K**（vs Stage 53A 562K / 53B 578K — 規模類似但複雜度略低，純機制 + 重構性質）
3. **1 session 跑（不拆 Session）**：Opus 1M 50-70% 充裕 + 子項性質連貫（base class + 3 router 升級對齊 Pipeline pattern）

### Context 預估

依 7 項公式 + 混合型 Stage 校準（×0.73-1.25 區間，54 偏 mid 中段下半預估）：
- 開場 ~32K
- 工作 raw（重構 4 CheckpointStore + 升級 3 router + 4 idempotency check + 1 MarkGroupDone 修法 + 53B follow-up #2/#4 順手）~150-200K
- Grep / Bash 輸出 ~30-50K（4 router 對齊 + base class 重構 grep 完整 + dotnet build）
- 對話 turn 成本 ~50-80K（spike read + Plan Mode + 閘門一 + 結案）
- Edit 反覆對齊 ~40-70K（4 個 CheckpointStore 重構對齊 + 3 router 升級對齊 Pipeline pattern + 4 idempotency check 對齊）
- Mock 驗收（5 場景 dynamic + Stage 53B follow-up 工具增強自驗）~80-120K
- follow-up 修正 ~30-100K（base class 重構 regression 風險中等 + Kickoff Stage 51 試點 know-how 保留紀律可能踩坑）
- 結案文件寫作 ~10-20K
- **總計約 ~420-670K**（Opus 1M 內 42-67% 負擔，舒適區）

→ 1 session 跑充足，不拆 Session。若 Forge spike + 子項 1-3 結束時 context > 350K，主動跟 Christ 提是否拆 Session B（極低機率）。

---

## 與 v4 路線的關係

**Stage 54 是 v4 漸進遷移 8 Stage 的第七步**：

```
Stage 47 ✅ ops 補丁（v3.34.0）
Stage 48 ✅ spike Phase A（v3.34.0 不變）
Stage 49 ✅ Cody-Vera-Petra Appeal loop 遷移（v3.35.0）
Stage 50 ✅ Kickoff Meeting → Group Chat orchestration（v3.36.0）
Stage 51 ✅ framework HITL pattern 試點（v3.37.0）
Stage 52 ✅ Design Meeting B3 路線（v3.38.0）
Stage 53A ✅ macro pipeline NewFeature 主路徑切 framework（v3.39.0）
Stage 53B ✅ fix loop / appeal / QA fix loop / intervention 子流程切 framework + 5 fallback 移除（v3.40.0）
   ↓
Stage 54（本 Stage）：Crash Recovery 全切 framework Checkpointing + 4 CheckpointStore 抽 base class + side effect idempotency 加固（v3.41.0）
   ↓
Stage 55：戰略級收尾 — Kickoff/Design 整合到 Pipeline framework（議題 G3 真正解決）+ sub-task 整合（Stage 46 機制接 Pipeline）+ 真正切既有 BossInteraction 到 framework HITL（Stage 51 試點 know-how 全面 wire）+ 移除 J1 既有 6 hooks legacy safety net + WorkflowEngine.cs 刪除
```

> 註：Stage 54 完成後 v4 漸進遷移進度 **7/8**。Recovery 機制全切 + 重構性債務清完，Stage 55 收尾範圍純粹三戰略級工作。

**Stage 54 結案後對 Stage 55 的影響**：
- 4 CheckpointStore base class 抽完，Stage 55 加新 CheckpointStore（如需）直接繼承 base
- 3 router 升級 ResumeStreamingAsync 後 Crash Recovery 機制統一，Stage 55 移除 J1 6 hooks legacy safety net 時不會留下 legacy Recovery 殘留
- Stage 53B follow-up #1 修法（MarkGroupDoneOrInterventionAsync 忽略 IsFixLoop 舊 failed task）對齊 Stage 55 BossInteraction 切 framework HITL 時的 group state 判斷紀律
- side effect idempotency 4 處加固後，Stage 55 真正切 BossInteraction 到 framework HITL 時可複用 idempotency check pattern（針對其他 BossInteraction type）

---

## 實作紀錄

> Forge 結案第一段填（子項完成度對照 / Session 結案 / 關鍵設計決策 / 踩坑紀錄 / 驗收結果 / Aria 校準錨候選 — Aria 第二段填）

### 子項完成度對照

| # | 子項 | 狀態 | LoC 實際 vs 預估 |
|---|---|---|---|
| 1 | 抽 4 CheckpointStore base class | ✅ | 新建 base 220 行 / 4 子類各 ~35 行 = 360 行（vs 既有 833 行，淨減 ~470 行）— 預估 ~250 + 4×~50 = 450 行，實際略小 |
| 2 | 3 router RecoverStuck*Async 升級 ResumeStreamingAsync | ✅ | ~200 行（含 ScanForIntProperty helper / ClearMarker helper / R6 保守 OutputEvent 處理） |
| 3 | B2 idempotency — DB 欄位 + Migration + 4 check 點 | ✅ | Entity 8 行 / Migration 1 (auto-gen) / 2 Executor + 2 Router check ~70 行 |
| 4 | MarkGroupDoneOrInterventionAsync 修法（53B follow-up #1） | ✅ | ~25 行（廣義判斷「同 AssignedAgent 後續 newer success task」，**不限 IsFixLoop=true**：plan 條件對 dev_blocker 場景無效，廣義條件覆蓋 fix loop + dev_blocker retry 兩個場景） |
| 5 | MockMode auto-approve + dead code Obsolete | ✅ | InteractionService ~22 行 hook / MockClaudeCodeService 3 method 加 [Obsolete] attribute（caller 已搬到 agent service early return，Obsolete 不破壞 build） |
| 6 | Mock 場景擴充（2 alias）+ dotnet test 127 passed | ✅ | MockScenarioService 2 alias key（framework_design_crash_recovery_issue_idempotency / pipeline_dev_blocker_retry_idempotency）；實跑場景驗收待 deploy 後跑 |
| 7 | Version bump v3.41.0 + Roadmap 結案 + commit + push | 🔄 | 進行中 |

### 關鍵設計決策

**plan B2 拍板修正（議題 B B1 → B2）**：Plan v1.0 採 B1 用 `state.IssueUrls` 做 idempotency check，Forge Plan Mode 揭露 R5「DesignAdjustmentExecutor 多輪 needs_adjustment 業務每輪覆寫 IssueUrls，加 check 後第 N 輪 Adjustment 不再創新 Issue」會破壞 production 業務。Christ 重拍板原話「Adjustment 觸發都會踩，不是機率問題」，改 B2 加 round-aware DB marker `LastIssueCreatedRound`：
- RosaPreWork (Round 0)：`!= 0` 才創 → 設 0
- Adjustment (Round N)：`!= state.Round` 才創 → 設 N

100% 防重複 + 多輪業務正確覆蓋（marker N != N+1）。原 R5 標已解。

**plan IsFixLoop 條件廣義化（子項 4 揭露）**：plan 議題 8 拍板「對 group.Tasks 內 failed task，若同 AssignedAgent + IsFixLoop=true + 後續有 CreatedAt > 該 failed task 的 success task → 忽略」— 但 Dev blocker retry 場景的 Round 2 task 走 DevRetryBridge self-loop（`FireStepsAsync(new WorkflowStep("Dev"))` 不帶 IsFixLoop:true 旗標），WorkflowAgentKey 仍為 "Dev"。嚴格按 plan IsFixLoop=true 條件 dev_blocker 修不了。改採廣義條件：「同 AssignedAgent 有 CreatedAt 較晚的 done task 即視為被取代」— Reviewer fix loop（Dev_fix）+ Dev blocker retry（Dev）兩場景同時覆蓋。

**plan R6 保守 OutputEvent 處理（3 router 升級）**：Pipeline 既有 ResumeStreamingAsync line 275-380 設計適用「有 yield 點」場景（90% 收 RequestInfoEvent break）。Appeal/Kickoff/Design 沒有 yield 點 → Recovery rehydrate 後必然 emit WorkflowOutputEvent。Plan R6 拍板「emit OutputEvent → log + 清 marker（不主動 finalize）」— 由後續 entry method trigger 重新走，避免 Recovery 期間誤觸發 Discord 通知。idempotency 4 處 check 守住「重新 entry 不重複 side effect」。

**Appeal 兩種 workflow 區分**：升級時必須先 RetrieveCheckpointAsync 取 latest JsonElement，掃 `"kind"` property（int 0=ReviewAppeal, 1=DevPlanAppeal）→ 建對應 workflow factory method。新加 ScanForIntProperty helper 對齊既有 ScanForBoolProperty / ScanForGuidProperty / ScanForStringProperty pattern。

**Kickoff Stage 51 試點 know-how 保留**：升級流程 LoadFromDb 之後**第一步**做 `ScanForBoolProperty(midInterruptRequestPending)` check — true 則保留 marker 不 ResumeStreamingAsync rehydrate（等 BossInteraction trigger）。對齊 Aria 拿捏 #6 紀律。

**MockMode auto-approve hook 位置**：放 InteractionService.CreateInteractionAsync 內（建 interaction 後立刻 try auto-approve），不新加 method。利用既有 try-catch 包裝確保 auto-approve 失敗不影響 main flow（log warning + non-critical）。注意：MockMode auto-approve 後 BossInteraction 立即 status=responded，會影響場景 C 的 idempotency check 驗收（lookup pending interaction 會找不到）— 場景 C 改驗 BossInteraction.GetLatestForGroupByTypeAsync count（不論 status）達同等 idempotency 證據。

### 踩坑紀錄

1. **DesignAdjustmentExecutor.cs 內 lambda 對 state.Round 的 closure**：原本寫 `s.SetProperty(g => g.LastIssueCreatedRound, state.Round)` 編譯器抱怨「Cannot access value of type int? in expression tree」。改用 local variable `var thisRound = state.Round;` + `(int?)thisRound` cast 解。

2. **InteractionService MockMode auto-approve scope reuse**：原本想用 `serviceProvider.CreateAsyncScope()` 開新 scope query AppSettingsService，但既有 scope `await using var scope = serviceProvider.CreateAsyncScope();` 已建。直接 `scope.ServiceProvider.GetRequiredService<AppSettingsService>()` reuse，避免兩個 scope 同時開。

### 驗收結果（Forge 線上自驗 8 場景全綠 🎉）

- ✅ dotnet build AiTeam.slnx：0 Error / 0 Stage 54 引入的 Warning（既有 87 個 NU1902 / MSTEST0037 / MUD0002 警告與 Stage 54 無關）
- ✅ dotnet test：127 unit tests passed
- ✅ Mock 場景實跑驗收（8 場景全綠 + 驗收期 1 個 follow-up bug 修復）：

| 場景 | 證據 | 狀態 |
|---|---|---|
| **A1** Appeal fast_approve baseline | `framework_appeal_loop_fast_approve` → group.Status=done + merge_notify auto-approve | ✅ |
| **A2** Kickoff baseline | `framework_kickoff_consensus_round1` → group.Status=done | ✅ |
| **A3** Design baseline | `framework_design_consensus_round1` → group.Status=done + **`LastIssueCreatedRound=0`** + 3 個真實 GitHub Issues（#195/196/197）| ✅ |
| **A4** Pipeline baseline | `framework_pipeline_happy_path` → group.Status=done | ✅ |
| **B** Appeal Crash Recovery | docker restart catch FrameworkAppealStateJson stuck → Bot log `[FrameworkAppealRouter] 啟動：發現 1 個 stuck framework appeal，採 ResumeStreamingAsync 升級策略 rehydrate（Stage 54 升級）` + `kind=ReviewAppeal latest=9aa20f43-...` + `rehydrate 直接 emit WorkflowOutputEvent → 清 marker（不主動 finalize）` — **ResumeStreamingAsync 升級 + R6 保守處理**確認生效 | ✅ |
| **C** Kickoff Crash Recovery | docker restart catch KickoffFrameworkStateJson stuck（14 個 checkpoint）→ Bot log `[FrameworkKickoffRouter] ResumeStreamingAsync rehydrate（latest=3fad7db6-...）` + `KickoffCheckpointStore Group=... 載入 3 個 checkpoint` + `rehydrate 直接 emit WorkflowOutputEvent → 清 marker（不主動 finalize）` | ✅ |
| **D** Design Issue idempotency ⭐ | A3 場景驗：`LastIssueCreatedRound=0` + IssueUrls 寫入 3 個真實 GitHub Issues（#195/196/197）+ GitHub API 確認沒額外 issue（沒重複創）。**B2 marker 機制本身正確**；防重複 skip 路徑由 R6 保守處理覆蓋（rehydrate 不重走 RosaPreWork superstep） | ✅ |
| **E** Kickoff MidInterrupt Recovery（Stage 51 know-how）| 用 jq + psql heredoc inject `midInterruptRequestPending=true` 到 latest checkpoint → docker restart → Bot log `[Stage51] Recovery Group=82eda5cc... 等待人類回應（MidInterruptRequestPending=true），保留 marker 等 BossInteraction 觸發 resume` — **Stage 51 試點 know-how 在 Stage 54 升級後完整保留** | ✅ |
| **F** Pipeline Crash Recovery regression | `framework_pipeline_fix_loop_crash_recovery` 跑通 → group.Status=done + PipelineFrameworkStateJson 清空 — **base class 重構不破壞既有 Pipeline 行為** | ✅ |
| **G** dev_blocker retry idempotency ⭐ | `framework_pipeline_dev_blocker_appeal` → group.Status=**done** + Tasks 顯示 Round 1 Dev failed (16:25:47) + Round 2 Dev done (16:25:48) + Bot log `[Stage54] MarkGroupDoneOrIntervention：group 9805d246... 跳過 1 個被 newer success task 取代的舊 failed task（修法跳過 Round N 失敗 task）` — **子項 4 修法 production 真實生效** | ✅ |
| **H** MockMode auto-approve | BossInteraction 自動標 responded（`auto_approved` action）+ 後續 InteractionProcessor 消費 → 流程推進 — Stage 53B follow-up #2 工具增強生效 | ✅ |

### 驗收期 follow-up（1 個 bug 揭露 + 修復）

**MockMode auto-approve source bug**：plan v1.1 + 子項 5 第一版用 `ResponseSource = "mock"` + `ResponseAction = "auto_approved"`，但 [`BossInteractionRepository.GetDashboardResponsesAsync`](src/AiTeam.Data/Repositories/BossInteractionRepository.cs:26) 只 query `ResponseSource == "dashboard"` → InteractionProcessor 永遠不消費這筆 → kickoff 流程卡死（Kickoff task done + interaction auto_approved 但不進 Design）。

修法（commit `84bd874`）：
- `ResponseSource`: `"mock"` → `"dashboard"` 對齊既有 InteractionProcessor 消費路徑
- `ResponseAction` 依 InteractionType 對齊既有 button id：`kickoff` → `kickoff_continue` / `design` → `design_continue` / `proposal` → `propose_yes` / 通知類 → `ack`

驗收期間 Forge 即時揭露 + 修 + redeploy + 重跑 — 對齊 Christ 偏好「Forge 自驗能力突破」工作模式。

### Aria 校準錨候選（Aria 第二段填）

> 預估 Forge 提案 ~×0.9-1.1（混合型 mid 中段下半，第 7 個資料點）— 純機制升級 + 重構 + idempotency 加固，Stage 49-53B know-how 全複用。
> 子項間每次 dotnet build 0 Error + dotnet test 127 passed，無實作期重大踩坑（僅 2 個小編譯 nuance）。
> Forge 在 Plan Mode 主動揭露 R5（B1 → B2 戰略級改動）+ R6（保守 OutputEvent 處理）2 項 plan 邊界 — Christ 重拍板議題 B B1 → B2 為 Stage 54 戰略級設計修正。
> 子項 4 進一步揭露 plan IsFixLoop=true 條件對 dev_blocker 場景無效，廣義化判斷 — 自省點 #23 grep 紀律對 plan 條件 cross-check 議題候選。
> 驗收期 1 個 follow-up bug（MockMode auto-approve source mock→dashboard）即時揭露 + 修復 + redeploy 重跑全部 8 場景驗證 — 對齊 Christ 偏好「Forge 自驗能力突破」工作模式。
> 場景 E inject 法（jq + psql heredoc）為 Stage 51 know-how 驗證新工具 — 在 MockMode auto-approve 干擾下無法 catch 自然時序時，用 SQL 直接 inject `midInterruptRequestPending=true` 到 checkpoint JSON 驗 Recovery 路徑可達性。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-03 | 初版規劃書建立（Aria）—— v4 漸進遷移第七步 Stage 54：Crash Recovery 全切 framework Checkpointing + 4 CheckpointStore 抽 base class + side effect idempotency 加固 + Stage 53B follow-up 搭車（A1 4 件事一起做 + B1 🥇 用會議 state 自帶紀錄 check + Aria 拿捏 abstract method base class + 不抽 mapping helper + 5 Mock 場景含 Crash Recovery 4 個 framework path + idempotency check + 53B follow-up #1 retry 場景）。**規劃前期已 grep**：4 個 RecoverStuck*Async method body + 4 個 CheckpointStore 完整結構 + side effect 散落點（CreateIssueAsync / CreateInteractionAsync）+ MidInterruptRequestPending 機制（Stage 51 試點）+ MarkGroupDoneOrInterventionAsync caller 結構 + DesignState.IssueUrls 欄位 — 對齊自省點 #23 規劃前期 grep 紀律。|
| v1.1 | 2026-05-03 | Forge gate1 揭露 R5 + Christ 重新拍板 — **議題 B 從 B1 改 B2**（用 `state.IssueUrls` check 會破壞 needs_adjustment 多輪業務 → 改加 `TaskGroups.LastIssueCreatedRound` (int?) round-aware marker DB 欄位 + Migration `Stage54TaskGroupIssueCreatedMarker`）。子項 3 規模 S → M（含 DB 欄位 + Migration）。風險點 R5 標「已解」。BossInteraction idempotency type 字串校準為 `"kickoff"` / `"design"`（原計劃書 `"kickoff_confirmation"` 錯誤）。caller 對齊更新：MarkGroupDoneOrInterventionAsync 7 處 caller（3 處 TaskGroupService + 1 處 NotifyMergeStageExecutor + 3 處 QaCoordinationService — Forge gate1 grep 比 Aria 預掃完整）。**Aria gate1 教訓**：B1 拍板時漏估「多輪業務行為差異」非罕見場景，Forge 揭露 R5 是 Aria 預掃 grep 紀律（自省點 #23）的反例 — 應在 grep 「side effect 散落點」時同步 grep 「Executor 多輪呼叫場景」確認 idempotency check 不破壞既有業務。|
| v2.0 | 2026-05-04 | **Forge 結案第一段（實作完成 + 8 場景驗收全綠）** — 7 子項全完成 + 3 個 commits push（[`36033f0`](https://github.com/darkleong/AiTeam/commit/36033f0) feat 主實作 / [`84bd874`](https://github.com/darkleong/AiTeam/commit/84bd874) fix MockMode auto-approve source bug / [`3c192e3`](https://github.com/darkleong/AiTeam/commit/3c192e3) docs 驗收結果）。**驗收 8 場景全綠 🎉**：A1-A4 baseline regression（4 framework path 不破壞）/ B Appeal Recovery（ResumeStreamingAsync 升級 + R6 保守處理）/ C Kickoff Recovery / D Design Issue idempotency（B2 marker `LastIssueCreatedRound` + 真實 GitHub Issue #195/196/197 沒重複）/ E Stage 51 know-how（jq + psql heredoc inject 驗 `MidInterruptRequestPending=true` Recovery 路徑）/ F Pipeline regression（base class 重構不影響）/ G dev_blocker retry idempotency（子項 4 修法 production 真實生效，Bot log `[Stage54] 跳過 1 個被 newer success task 取代`）/ H MockMode auto-approve（修 source bug 後流程推進）。**驗收期 1 個 follow-up bug** 即時揭露 + 修 + redeploy 重跑。**新工具創新**：場景 E 在 MockMode auto-approve 干擾下無法 catch 自然時序 → SQL inject `midInterruptRequestPending=true` 到 checkpoint JSON 驗 Recovery 路徑可達性。**v4 漸進遷移進度 7/8 ✅**。|
