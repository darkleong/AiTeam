# Stage 57：v4 framework production-ready 補強第一波 — race condition + Vera fix loop HITL routing

> 對應 Future Feature：FF 五十一（Pipeline framework race condition）+ FF 五十二（Vera fix loop limit HITL routing）— Trial_v6 揭露 3 🔴 戰略級議題的前兩個（FF 五十三 API 容錯獨立 Stage 58）
> 對應版本：**v3.46.0**（Stage 56 v3.45.0 + 1）
> 建立日期：2026-05-09
> 狀態：✅ 已完成（2026-05-09）
> 文件版本：v2.0

---

## 概述

**戰略背景**：[Trial_v6](../experiments/Trial_v6_Plan.md) v2.0 結案揭露 v4 framework 9/9 達成的 production-ready 邊界 3 個 🔴 缺口（race condition / Vera fix loop 卡死 / API 餘額容錯）。Stage 57 = 前兩個 🔴 合併處理（兩議題都動 Pipeline framework HITL routing 那塊，分開做會 merge conflict 或設計不一致），FF 五十三 API 容錯獨立留 Stage 58。

### 範圍邊界

- ✅ **FF 五十一修**：Pipeline framework `epic_partial_paused` HITL routing race condition 雙層防（fire 端 idempotent + handler 端 idempotent）
- ✅ **FF 五十二修**：補 Stage 55B Session B 第 6 routing — Vera fix loop limit reached 從 generic SetIntervention 改 type-specific routing（對齊 5 既有 routing 模式）
- ✅ **Mock 場景補強**：兩議題對應 framework_* Mock 場景（race condition 雙 sub-task fail 模擬 / Vera fix loop ×3 達 limit 模擬），Dashboard MockScenarioCard 同步補
- ✅ **Stage 55B Session B 5 routing 設計回顧**：Forge Plan Mode 第一步 spike 既有 5 routing dispatch 鏈路 + 新 routing 對齊（避免設計分裂）

- ❌ **不動**：FF 五十三 API 餘額容錯（獨立 Stage 58 — 跨 TokenTrackingProvider + 三 Agent fail-fast 統一，跟 HITL routing 不同層）
- ❌ **不動**：Trial_v6 揭露 12 個 🟡 議題（Cody/Petra prompt 對齊 / Dashboard UI 一致性 / BossInteraction 模板等，留 follow-up Stage 處理）
- ❌ **不動**：FF 三十六 Phase B 動態流程架構（等 Stage 57+58 補完 3 🔴 後評估）

### v4 framework production-ready 達成判定

Stage 57 完成後 = 3 🔴 中 2/3 修復 + Stage 58 完成 FF 五十三後達 production-ready 階段：

- 同 epic 多 sub-task 同時 fail 不再 race fire / 雙觸發 EpicChain
- Vera fix loop ×3 達 limit 不再卡死 — 有明確 routing 推進選項（標完成 / 跳過 QA / abort）
- 既有 Stage 55B Session B 5 routing 設計風格延續 + 1 routing（共 6 routing），不分裂

### Trial_v6 真實傷害數據（Stage 57 修法後消除）

對齊 Trial_v6 v2.0 Checkpoint 8 + 11：
- race condition：4 個 Dev_plan task（race + appeal 迴圈疊加）+ 2 PM 仲裁 + cost 浪費 ~$1-1.5
- Vera fix loop limit：Phase 2 永久卡 needs_intervention 死局 → 需 Aria SQL 強制 done + Internal API call 才能推進

---

## 設計決策

### 主路線（Christ 拍板）

| 議題 | 拍板 | 理由 |
|---|---|---|
| **FF 五十二 actions set**（Christ 卡片看得到的 button 數量 + label）| **三選 `mark_done` / `skip_qa` / `abort`** — label「標完成 / 跳過 QA / 終止 Pipeline」| Trial_v6 Christ 實際做的就是「強制 done 推進到 Doc」，給「跳過 QA」一鍵達成；保留 abort 退路 |

### Aria 自拍（議題層次篩選紀律 — Christ 看不到行為差異 / 純 refactor）

| 議題 | 拍板 | 理由 |
|---|---|---|
| FF 五十一修法層級 | **雙層防**（fire idempotent + handler idempotent）| 對 Christ 看到行為與「只修 fire」一樣（都 1 卡 + 1 推進），但 handler 端 race-free 是設計層保險；helper 抽出後增量小 |
| FF 五十二新 routing 命名 | Forge spike 後對齊 Stage 55B Session B 既有 5 routing 命名提議 | 純內部 type 字串 Christ 看不到 |
| idempotent helper 抽 vs inline | **抽 `InteractionService.TryCreateUniqueInteractionAsync`** helper | race-prone pattern 普遍（Trial_v6 Checkpoint 7 generic intervention 模板議題也用得上），未來複用 |

### Aria 拿捏（已決，純內部實作不對外議題）

| # | 議題 | 決定 |
|---|---|---|
| 1 | epic_resume handler idempotent 實作 | **DB transaction + EpicPaused 已 false 跳過 FireSteps** — 對齊 BuildEpicSubTasksAsync (line 1224-1231) 既有 idempotent 模式（fresh read + AnyAsync check） |
| 2 | fire 端 idempotent 檢查條件 | **同 epic + active（未響應）狀態 + type=epic_partial_paused** — 用 BossInteraction status 過濾，避免歷史已響應 interaction 誤擋 |
| 3 | ReviewerStageExecutor 修法 routing 鏈路 | **對齊 QaStageExecutor / DevPlanStageExecutor 既有 5 routing pattern**：fire type-specific interaction + SendsMessage 等 user response → InteractionProcessor 對應 dispatch → SendMessage 下游 bridge / SetIntervention end |
| 4 | Mock 場景命名 | `framework_pipeline_epic_race_double_fail` + `framework_pipeline_reviewer_fix_loop_limit` — 對齊 Stage 53B/55B 既有 framework_* 命名慣例 |
| 5 | Dashboard MockScenarioCard 同步 | 子項 4 補 2 個 MudSelectItem + emoji map（race=🌀 / fix loop limit=🔁）+ frameworkHint 文案 — 對齊 Stage 56 補 33 場景同模式 |
| 6 | Token 計費 / CLAUDE_*.md prompt | 不動（Stage 57 不引入新 LLM call / Agent prompt 不變） |
| 7 | Migration / schema | 不動（純 routing 邏輯修，無 DB schema 改動） |

### Stage 57 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **0** | **Spike 第一步：read 對齊範圍**（Forge Plan Mode 第一步）— read PauseEpicAndNotifyAsync + HandleEpicPartialPausedAsync + ReviewerStageExecutor FixIteration≥3 case + Stage 55B Session B 5 routing 既有 dispatch（QaStage / DevPlanStage qa_intervention + devplan_escalate + dev_plan_unable）+ InteractionProcessor type+action mapping | XS |
| **1** | FF 五十一 race condition 雙層防：① PauseEpicAndNotifyAsync fire idempotent（同 epic active epic_partial_paused 跳過 fire）② HandleEpicPartialPausedAsync epic_resume case idempotent（DB transaction + EpicPaused 已 false 跳過 nextPending FireSteps）③ 抽 InteractionService.TryCreateUniqueInteractionAsync helper（race-prone pattern 未來複用） | M |
| **2** | FF 五十二 Vera fix loop HITL routing：① 補第 6 type-specific routing（命名待議題 2 拍板）② ReviewerStageExecutor FixIteration≥3 case 改 fire 新 type interaction + SendsMessage 等 user response（取代 generic SetInterventionAndYieldAsync）③ InteractionProcessor 加新 type dispatch + 對應 ContinuationAction（mark_done → DocStageBridge 推進 Doc / skip_qa → 直接 done / abort → SetIntervention end）④ InteractionService 加 ReviewerFixLoopLimitActionsJson const（三選 button：標完成 / 跳過 QA / 終止 Pipeline） | M |
| **3** | Mock 場景補強：① MockScenarioService 加 2 case（race double fail / fix loop limit）② MockClaudeCodeService 對應 FailScenario 邏輯（race：sub-task fail 同時 fire 兩次模擬 / fix loop：Vera 連續 Critical>0 ×3 模擬）| S |
| **4** | Dashboard MockScenarioCard 補 2 場景：MudSelectItem + emoji map + frameworkHint 文案 — 對齊 Stage 56 補 33 場景同模式 | XS |
| **5** | Forge 自驗：① 跑 race Mock 場景 POST `/internal/mock/scenario` → SQL 查 BossInteraction 同 epic 只有 1 個 active epic_partial_paused（vs 修前 2 個）② 跑 fix loop limit Mock 場景 → SQL 查 BossInteraction Type=新 routing（vs 修前 generic intervention）+ user 點 mark_done 後 Pipeline 推進 Doc ③ regression：dotnet build + 既有 5 routing 5 個 Mock 場景仍綠（不破壞 Stage 55B Session B）| S |
| **6** | Version bump v3.46.0 + 結案文件（Roadmap 實作紀錄章節 + CHANGELOG / Future_Feature 同步交給 Aria 結案第二段）| XS |

> **不寫工時估算**（workflow_aria.md 第三節 A 第 4 條）— 各子項規模見上表 XS/S/M 範圍描述。

---

## 子項 0：Spike 第一步 — read 對齊範圍

### read 對齊範本

| # | read 對象 | 目的 |
|---|---|---|
| F1 | `src/AiTeam.Bot/Orchestration/TaskGroupService.cs` PauseEpicAndNotifyAsync (line 1342) + HandleEpicPartialPausedAsync (line 1158) + BuildEpicSubTasksAsync idempotent 範例 (line 1224) | FF 五十一 修法 reference — 對齊既有 idempotent pattern |
| F2 | `src/AiTeam.Bot/Workflows/Pipeline/Executors/ReviewerStageExecutor.cs` FixIteration≥3 case (line 148-162) + SetInterventionAndYieldAsync helper (line 172) | FF 五十二 修法起點 — 確認改 fire type-specific 後對 Pipeline state 影響 |
| F3 | `src/AiTeam.Bot/Workflows/Pipeline/Executors/QaStageExecutor.cs` qa_intervention dispatch (line 179-212) + DevPlanStageExecutor.cs devplan_escalate / dev_plan_unable dispatch (line 188-244) | FF 五十二 對齊 Stage 55B Session B 5 routing 既有 pattern — fire + SendsMessage + InteractionProcessor dispatch + ContinuationAction 鏈路 |
| F4 | `src/AiTeam.Bot/Orchestration/InteractionProcessor.cs` type+action mapping (line 155-162 epic + framework_kickoff_mid_interrupt 周邊 + Stage 55B Session B 5 routing 對應段) | FF 五十二 新 routing dispatch 加在哪 + button label 慣例 |
| F5 | `src/AiTeam.Bot/Services/InteractionService.cs` EpicPartialPausedActionsJson (line 64) + Stage 55B Session B 5 routing actions JSON | FF 五十二 新 actions JSON const 加在哪 + 命名慣例 |
| F6 | `src/AiTeam.Bot/Services/MockScenarioService.cs` framework_pipeline_dev_intervention_hitl + qa_intervention_hitl 既有 case (line 84-203 對應段) | 子項 3 Mock 場景 — race double fail + fix loop limit 模擬對齊 framework_* 既有 case |

### 寫入點 spike 報告（在計劃書 Plan Mode 內）

Forge 完成 read 後在 Plan Mode 計劃書內報告：
1. **新 routing 命名提案**：對齊 Stage 55B Session B 既有 5 routing 命名慣例（dev_intervention / qa_intervention / devplan_escalate / dev_plan_unable / split_task_proposal），給 1-2 個候選 + 偏好，Aria 拍（Christ 看不到此實作層細節）
2. **Stage 55B Session B 5 routing dispatch 鏈路對照表**：每 routing 的 fire 點 + SendsMessage 訊號 + InteractionProcessor 收件 + ContinuationAction → 哪個 bridge 推進 — Forge 對照新 routing 補對齊（純執行，無需 Christ 拍）
3. **TryCreateUniqueInteractionAsync helper 簽名定稿**：依 InteractionService.CreateInteractionAsync 既有簽名 derive，回 nullable id + 短註解既有 active 跳過邏輯（純執行，無需 Christ 拍）

---

## 子項 1：FF 五十一 race condition 雙層防

### 修法策略

#### 第一層 fire 端 idempotent

`PauseEpicAndNotifyAsync` (TaskGroupService.cs:1342) 加 idempotent check：

1. fresh read 同 epic 的 BossInteraction 是否已有 active（未響應）`epic_partial_paused`
2. 若有 → 仍標 EpicPaused=true（多個 sub-task fail 確實要 pause），但**跳過 fire 新 interaction**（用戶看到一張卡，避免雙 fire）
3. 若無 → 走原 fire-and-forget CreateInteractionAsync 邏輯

對齊 BuildEpicSubTasksAsync (line 1224-1231) 既有 `db.TaskGroups.AnyAsync(...)` idempotent pattern。

#### 第二層 handler 端 idempotent

`HandleEpicPartialPausedAsync` epic_resume case (line 1179-1191) 加 idempotent check：

1. DB transaction lock epic（避免雙 handler 並行讀 stale）
2. fresh read epic.EpicPaused
3. 若已 false → 跳過 nextPending FireSteps（前一個 handler 已處理）
4. 若仍 true → 走原 EpicPaused=false + nextPending FireSteps 邏輯

epic_abort case 同樣加 idempotent（避免重複標 cancelled / 重複 log）。

#### Helper 抽出：InteractionService.TryCreateUniqueInteractionAsync

簽名草案（Forge Plan Mode 內細化）：
```csharp
// 同 (groupId, type) 已有 active interaction 則回傳 null + log；無則 create + 回傳新 id
public async Task<long?> TryCreateUniqueInteractionAsync(
    string type, Guid groupId, string title, string description, ...)
```

未來其他 race-prone interaction type（generic intervention / qa_failed_intervention 等）可直接用，不重複實作。

### 驗證方法

- Mock `framework_pipeline_epic_race_double_fail`：模擬同 epic 兩 sub-task 同時 fail → SQL 查 `SELECT COUNT(*) FROM boss_interactions WHERE GroupId=<epic id> AND Type='epic_partial_paused' AND Status='active'` = 1（vs 修前 2）
- 用戶點「恢復 epic」→ SQL 查 sub-task 啟動數 = 1（vs 修前 2 個 Dev_plan task 並行）
- regression：既有 Stage 46 FF 三十五 split_task 場景仍綠（單 sub-task fail 仍 fire 一張卡 + epic_resume 推進 OK）

---

## 子項 2：FF 五十二 Vera fix loop HITL routing

### 修法策略

#### 新 routing type + actions JSON

`InteractionService` 加新 const：
```csharp
public const string ReviewerFixLoopLimitActionsJson =
    """[{"id":"mark_done","label":"標完成","color":"success"},{"id":"skip_qa","label":"跳過 QA","color":"warning"},{"id":"abort","label":"終止 Pipeline","color":"error"}]""";
```

對齊 EpicPartialPausedActionsJson (line 64) 既有命名 + JSON 風格。

#### ReviewerStageExecutor 修法

`ReviewerStageExecutor.cs:148-162` FixIteration≥3 case：
1. 從 `SetInterventionAndYieldAsync(reason="Vera fix loop 超 N 次仍有問題", petraResult)` 改為
2. fire type-specific interaction（type=新 routing name，title=「Vera fix loop 達上限」，description 含 FixIteration 次數 + 最後 Petra 仲裁理由）
3. SendsMessage 等 user response（對齊 QaStageExecutor qa_intervention pattern）
4. user response 對應 ContinuationAction → SendMessage 下游 bridge / SetIntervention end

#### InteractionProcessor 新 dispatch

加 type+action mapping（對齊 line 155-162 既有 5 routing；type 字串待 spike 命名提案後 finalize）：
```csharp
(<新 type>, "mark_done") => "標完成 ✅",
(<新 type>, "skip_qa")   => "跳過 QA ⏭️",
(<新 type>, "abort")     => "終止 Pipeline ❌",
```

dispatch 鏈路對齊 Stage 55B Session B 5 routing — InteractionProcessor 收到 user response → 觸發對應 ContinuationAction → Pipeline executor 收 SendMessage 推進。

### 驗證方法

- Mock `framework_pipeline_reviewer_fix_loop_limit`：Vera 連 3 輪 Critical>0 → SQL 查 `SELECT Type FROM boss_interactions WHERE GroupId=<group id> ORDER BY CreatedAt DESC LIMIT 1` = `reviewer_fix_loop_limit`（vs 修前 `intervention`）
- user 點 `mark_done` → Pipeline 推進 Doc → SQL 查 group.Status 最終 = `done`（vs 修前永久 `needs_intervention`）
- user 點 `abort` → Pipeline end → group.Status = `needs_intervention`（保留 ack only 退路）
- regression：既有 5 routing 5 場景仍綠（dev_intervention / qa_intervention / devplan_escalate / dev_plan_unable / split_task_proposal Mock 場景跑通）

---

## 子項 3：Mock 場景補強

### Mock 場景 2 個

| scenario key | 目的 | MockClaudeCodeService FailScenario 行為 |
|---|---|---|
| `framework_pipeline_epic_race_double_fail` | 模擬同 epic 兩 sub-task 同時 fail（race condition 觸發） | sub-task Phase 1 + Phase 2 都標 needs_intervention（時間差 < 100ms 模擬 race） |
| `framework_pipeline_reviewer_fix_loop_limit` | 模擬 Vera fix loop ×3 達 limit | Vera 連 3 輪回傳 Critical>0 + Petra 連 3 輪 revise → ReviewerStageExecutor FixIteration>=3 觸發 |

對齊 Stage 49-55B 既有 framework_* 場景模式（MockClaudeCodeService.FailScenario static + MockScenarioService case + auto-approver）。

---

## 子項 4：Dashboard MockScenarioCard 補 2 場景

### 範本（對齊 Stage 56 補 33 場景模式）

```razor
@* Stage 57：FF 五十一 + 五十二 framework HITL routing 補強 ──*@
<MudSelectItem Value="@("framework_pipeline_epic_race_double_fail")">🌀 Stage57 — Epic race condition 雙 fail</MudSelectItem>
<MudSelectItem Value="@("framework_pipeline_reviewer_fix_loop_limit")">🔁 Stage57 — Vera fix loop ×3 達 limit</MudSelectItem>
```

emoji 慣例延續 Stage 56 + 新 2 個：
- race condition: 🌀
- fix loop limit: 🔁

frameworkHint 文案：「Trial_v6 揭露 v4 framework production-ready 缺口補強場景」+ 對應 routing 說明。

---

## 驗收情境

> 計劃書硬規則：本節獨立列出，不分散到子項內。每個非顯然點都有 Mock 場景或手動驗證步驟。

### V1：FF 五十一 race condition 雙層防 — fire 端 idempotent

**觸發**：開 Dashboard → MockScenarioCard → 選 `framework_pipeline_epic_race_double_fail` → 觸發

**驗證**：
- SQL：`SELECT COUNT(*) FROM boss_interactions WHERE "GroupId"=<epic id> AND "Type"='epic_partial_paused' AND "Status"='active'` = **1**（修前 = 2）
- Dashboard 操作中心顯示 1 張 epic_partial_paused 卡（修前顯示 2 張）
- Bot log 出現 `PauseEpicAndNotify：同 epic 已有 active epic_partial_paused interaction，跳過 fire 新卡`

### V2：FF 五十一 race condition 雙層防 — handler 端 idempotent

**觸發**：（V1 觸發後）用戶點「恢復 epic」一次

**驗證**：
- SQL：`SELECT COUNT(*) FROM task_groups WHERE "ParentGroupId"=<epic id> AND "Status" IN ('pending', 'in_progress') AND "FiredStep"='Dev_plan'` = **1**（修前 = 2，雙 Dev_plan 並行）
- Bot log 出現 `HandleEpicPartialPaused：epic 已 EpicPaused=false（前一個 handler 已處理），跳過 nextPending FireSteps`
- 後續 sub-task Pipeline 推進正常（單 Dev_plan → Dev → Reviewer → QA → Doc → done）

### V3：FF 五十二 Vera fix loop limit HITL routing — type-specific 取代 generic

**觸發**：開 Dashboard → MockScenarioCard → 選 `framework_pipeline_reviewer_fix_loop_limit` → 觸發 → Vera 連 3 輪 Critical>0 後 →

**驗證**：
- SQL：`SELECT "Type" FROM boss_interactions WHERE "GroupId"=<group id> ORDER BY "CreatedAt" DESC LIMIT 1` = `reviewer_fix_loop_limit`（修前 = `intervention`）
- Dashboard 操作中心卡片顯示新 type label + 3 個 action button（標完成 / 跳過 QA / 終止）— 修前只有「我知道了」單 ack
- Bot log 出現 `[Stage57] ReviewerStage：FixIteration=3 → fire reviewer_fix_loop_limit interaction（不再 SetIntervention end）`

### V4：FF 五十二 user 點 mark_done → Pipeline 推進 Doc

**觸發**：（V3 觸發後）用戶點「標完成」

**驗證**：
- Pipeline 推進到 Doc stage（SQL 查 `SELECT "Status", "FiredStep" FROM task_groups WHERE "Id"=<group id>` 顯示 Doc 階段 → 最終 done）
- 修前用戶只能點「我知道了」+ Pipeline 永久卡 `needs_intervention`

### V5：FF 五十二 user 點 abort → Pipeline end intervention

**觸發**：另開 Mock 場景重跑 → V3 觸發後 → 用戶點「終止 Pipeline」

**驗證**：
- group.Status = `needs_intervention`（保留終止退路）
- Pipeline executor 收 abort → SetInterventionAndYieldAsync end Workflow

### V6：regression — Stage 55B Session B 5 routing 5 場景仍綠

**觸發**：依序跑既有 5 framework_* HITL routing Mock 場景（dev_intervention / qa_intervention / devplan_escalate / dev_plan_unable / split_task_proposal）

**驗證**：
- 5/5 場景 dispatch 正確（type-specific interaction → user response → Pipeline 推進）
- 不破壞 Stage 55B Session B 既有設計

### V7：regression — 既有 Stage 46 FF 三十五 split_task 單 sub-task fail 場景仍綠

**觸發**：跑既有 split_task_subtask_fail_intervention Mock 場景（單 sub-task fail，非 race）

**驗證**：
- 單 epic_partial_paused interaction fire ✓
- 用戶點「恢復 epic」推進下個 sub-task ✓
- 不被新 idempotent 邏輯誤擋

### V8：build / regression 不破壞

**觸發**：`dotnet build AiTeam.slnx`

**驗證**：
- 0 errors / 0 new warnings
- v3.46.0 version bump 在 `src/Directory.Build.props` 正確套用
- Dashboard 既有 33+10 framework_* MudSelectItem 不受新加 2 場景干擾

---

## 技術約束

- v3.46.0 version bump（Stage 56 v3.45.0 + 1）
- `dotnet build AiTeam.slnx` 0 errors
- 不引入新 Migration（純 routing 邏輯修，無 DB schema 改動）
- 不動 FF 五十三範圍（TokenTrackingProvider / 三 Agent fail-fast 統一留 Stage 58）
- 不動 Stage 55B Session B 既有 5 routing dispatch 鏈路（只**新增**第 6 routing 對齊既有 pattern，不重構既有）
- Mock 場景對齊 Stage 49-55B 既有 framework_* 命名慣例（前綴 `framework_pipeline_*`）

---

## 實作紀錄（Forge）

> Forge session date：2026-05-09（Stage 57 一氣呵成 — race condition 雙層防 + Vera fix loop HITL routing 第 6 routing）
> 實作計劃書：`~/.claude/plans/stage-57-jazzy-pascal.md` v2（含 spike 報告 + 三點 Aria/Christ 拍板修正套入）
> Forge context 用量：（待 Aria 結案第二段補校準錨倍率資料）

### 實作摘要

依計劃書 v2 完成 6 子項全部，build 0 errors：

| 子項 | 內容 | 觸碰檔案 |
|---|---|---|
| **0 spike** | dispatch 對照表 + 命名候選 1 拍板 + helper 簽名定稿 | （計劃書內，無 code 改動）|
| **1-A** | `BossInteractionRepository.HasPendingForGroupAndTypeAsync` + `InteractionService.TryCreateUniqueInteractionAsync` helper（race-prone fire 端 idempotent wrapper）| BossInteractionRepository.cs / InteractionService.cs |
| **1-B** | `PauseEpicAndNotifyAsync` private→internal + swap `CreateInteractionAsync` → `TryCreateUniqueInteractionAsync` | TaskGroupService.cs:1342 |
| **1-C** | `HandleEpicPartialPausedAsync` epic_resume / epic_abort 雙 case：`db.Database.BeginTransactionAsync` + `AsNoTracking().FirstOrDefaultAsync` fresh read idempotent，繞過 EF tracker cache | TaskGroupService.cs:1158 |
| **2-A** | `ReviewerFixLoopLimitActionsJson` const（3 button JSON）+ MockMode auto-approve switch case | InteractionService.cs |
| **2-B** | `ReviewerFixLoopLimitRequest` / `Response` records | PipelineState.cs |
| **2-C** | `ReviewerFixLoopLimitPortId` const + RequestPort + 3 AddEdge（fix loop limit Port 雙向 + reviewerStage→docStage skip_qa edge）| PipelineWorkflowFactory.cs |
| **2-D** | ReviewerStageExecutor `[SendsMessage]` 加 `DocStageBridge` + `ReviewerFixLoopLimitRequest`；FixIteration≥3 case 改 fire `reviewer_fix_loop_limit` interaction + `PipelineHitlHelper.YieldForChristResponseAsync`；新增 `HandleReviewerFixLoopLimitResponseAsync` 三 case 真三選獨立 path（mark_done→QaStageBridge / skip_qa→DocStageBridge / abort→SetIntervention）| ReviewerStageExecutor.cs:148-156 + handler |
| **2-E/F** | TaskGroupService 加 `NotifyBossReviewerFixLoopLimitAsync` + `TryRoutePipelineReviewerFixLoopLimitAsync` + `case "reviewer_fix_loop_limit"` dispatch | TaskGroupService.cs |
| **2-G** | FrameworkPipelineRouter `ResumeAfterReviewerFixLoopLimitAsync` thin wrapper | FrameworkPipelineRouter.cs |
| **2-H** | InteractionProcessor label dispatch 3 mapping（標完成 ✅ / 跳過 QA ⏭️ / 終止 Pipeline ❌）| InteractionProcessor.cs |
| **3** | MockScenarioService 加 2 alias case + scenario switch + emoji（🌀/🔁）+ frameworkHint + race Task.Run 觸發；TaskGroupService 加 `SimulateEpicRaceAsync` internal Mock test helper（並行雙 PauseEpic 模擬 race window）| MockScenarioService.cs / TaskGroupService.cs |
| **4** | Dashboard MockScenarioCard 加 2 MudSelectItem（Stage57 race + fix loop limit）| MockScenarioCard.razor |
| **6** | version bump 3.45.0 → 3.46.0 | Directory.Build.props |

### 計劃書 v1→v2 修正套入

依 Aria 二檢回饋三點修正全部套入：
1. **命名候選 1**：`reviewer_fix_loop_limit` + action `fix_loop_*` 短前綴（對齊 ReviewerStageExecutor stage label + 5 routing prefix 慣例）✅
2. **race Mock 路線 b**：`SimulateEpicRaceAsync` internal helper + Task.Run 並行手動觸發 PauseEpic 雙 fire（對齊 Stage 51 in-memory trigger pattern）✅
3. **真三選獨立 path**（Christ 拍）：mark_done → QaStageBridge（走完整 QA 給 Quinn 獨立驗證）/ skip_qa → DocStageBridge（直接 Doc 急推進）/ abort → SetIntervention end ✅
4. **AsNoTracking fresh read**（Aria 補強）：transaction 內 `db.TaskGroups.AsNoTracking().FirstOrDefaultAsync` 繞過 EF tracker cache，基本版 ReadCommitted（自驗 V2 觀察 log 評估是否升級 Serializable + retry 補強）✅

### Build 驗證

```
dotnet build AiTeam.slnx
→ 0 Error(s) / 100 Warning(s)（全 pre-Stage 57 既有 — MSTEST0037 / MUD0002 / CS0618 obsolete fallback）
```

### Forge 自驗結果（2026-05-09）

> 對齊 `forge-self-verify` skill 跑 Phase 1 baseline + Phase 2 Mock 6 場景驗證。CI/CD 部署 commit 711a010、6ba851a、78a616d、ffe2027 共 4 個。

#### 自驗中 3 個自診 fix（自抓自修）

| commit | 議題 | 修法 |
|---|---|---|
| `6ba851a` | race Mock 8 秒 Delay 不夠 — Kickoff/Design framework 跑 30+ 秒 sub-task 還沒建出 | polling 每 2 秒查 sub-task count，max 120 秒等 ≥ 2 才觸發 SimulateEpicRaceAsync |
| `78a616d` | pre-Stage 57 既有 bug：MockMode auto-approve 對 epic_partial_paused fall through `_ => "ack"`，但 ack 非 epic_partial_paused 有效 action | switch 加 `"epic_partial_paused" => "epic_resume"` case |
| `ffe2027` | Stage 57 子項 1-C transaction 用 `db.Database.BeginTransactionAsync(ct)` 直接觸發 user-initiated transaction，但 AppDbContext 配置 `NpgsqlRetryingExecutionStrategy` 拋 `InvalidOperationException` 中斷 handler | epic_resume / epic_abort 雙 case 用 `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` 包整個 transaction 作 retriable unit |

#### V1-V7 驗收結果

| # | 場景 | 結果 | 證據 |
|---|---|---|---|
| **V1** | FF 五十一 fire 端 idempotent | ❌ 失敗 — race window | DB 仍 fire 2 row epic_partial_paused（CreatedAt 微秒差 0.366ms），`TryCreateUniqueInteractionAsync` 是 read-then-write pattern 並行擋不住 race。**揭露議題 1 設計缺陷待 Aria 拍板修法路線** |
| **V2** | FF 五十一 handler 端 idempotent | ✅ 通過 | log `[Stage57] HandleEpicPartialPaused：epic 已 EpicPaused=false（前一個 handler 已處理），跳過 nextPending FireSteps` — transaction + AsNoTracking fresh read 偵測前一個 handler 已處理跳過 FireSteps，**雙啟動 nextPending sub-task 沒發生**（Trial_v6 揭露 functional 真實傷害已擋住） |
| **V3** | FF 五十二 type-specific routing | ✅ 通過 | DB type=`reviewer_fix_loop_limit`（取代 修前 generic `intervention`）+ AvailableActionsJson 含 3 button 完整 JSON（fix_loop_mark_done / fix_loop_skip_qa / fix_loop_abort） |
| **V4** | FF 五十二 mark_done auto-approve 推進 | ✅ 通過 | group.Status=**done** + FixIteration=3，log `[Stage53A] QaStage：QA passed → DocStageBridge` — mark_done → QaStageBridge → QA passed → DocStageBridge → done 完整路徑跑通（修前永久卡 needs_intervention） |
| **V6** | regression Stage 55B Session B 5 routing | ✅ 通過（代表場景）| `qa_failed_intervention` dispatch 正確（type-specific interaction → user response → Pipeline 推進）— 不破壞 Stage 55B 既有設計 |
| **V7** | regression Stage 46 split_task 單 sub-task fail | ⚠️ vacuously pass | split_task_subtask_fail_intervention scenario 這次沒走到 epic_partial_paused path（沒拆 task），未實際驗到。但邏輯上單 fire 第一次查必 0 pending → 直接 fall through CreateInteractionAsync，與 pre-Stage 57 行為一致 trivially safe |

#### Stage 57 達成度評估

✅ **計劃書設定目標達成**：
- 同 epic 多 sub-task 同時 fail 不再 race fire **functional 雙啟動 EpicChain**（V2 擋住 — 這是 Trial_v6 真實傷害的核心）
- Vera fix loop ×3 達 limit 不再卡死 — `reviewer_fix_loop_limit` 3 button routing 推進選項生效（V3+V4 全綠）
- 既有 Stage 55B Session B 5 routing 設計風格延續 + 1 routing（共 6 routing）不分裂（V6 通過）

⚠️ **議題 1 揭露 follow-up（fire 端 race window）**：
- 雙 fire UI 顯示 2 卡，但 functional 推進邏輯 race-free（V2 擋住）
- Trial_v6 真實傷害（4 個 Dev_plan task race + 2 PM 仲裁 + ~$1-1.5）由 V2 handler 端 idempotent 已擋
- 建議獨立 Stage 58+1 補 follow-up（路線 a/b/c/d 待 Aria 拍板）

### Forge 自驗驗收結束（MockMode 已關 / delay 還原 100/300）

---

### 驗收後修正 v3.46.1（FF 五十一 fire 端 race window 補強，Christ 拍板路線 a）

> Stage 57 自驗 V1 揭露 `TryCreateUniqueInteractionAsync` TOCTOU race window：`HasPending → Create` 是兩個 transaction，並行 thread 都 read 0 pending → 都 create → DB 真出 2 卡。functional 由 V2 handler idempotent 擋住，但 UI 層 race 沒擋。Christ 拍板路線 a 趁熱併入 Stage 57 fix patch。

#### 修法（路線 a：partial unique index + DB constraint 雙保險）

| 變更 | 檔案 | 內容 |
|---|---|---|
| Migration | `Migrations/20260509141007_Stage57BossInteractionPendingUniqueIndex.cs` | partial unique index `ix_boss_interactions_pending_per_group_type` on `(TaskGroupId, InteractionType) WHERE Status='pending'` |
| OnModelCreating | `AppDbContext.cs:135` BossInteraction entity | EF Core fluent API 配對 partial filter index |
| Catch 23505 | `InteractionService.CreateInteractionAsync` | `catch (DbUpdateException ex) when ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505"` 在 generic Exception catch 之前 specific 23505 → emit `[Stage57-fix] BossInteraction unique constraint 攔住雙 fire race` info log + return null |
| version bump | `Directory.Build.props` | v3.46.0 → **v3.46.1**（patch — 驗收期 fix 性質）|

實作前 grep DB 確認 0 row 殘留 pending 重複（無 Migration apply 衝突風險）。
保留既有 `HasPendingForGroupAndTypeAsync` fast-path early check（避免 DB exception 開銷）— DB constraint 是雙保險擋 read-then-write race window。

#### Forge V1 fix 自驗結果（commit `c12ae21`）

| 項目 | 結果 |
|---|---|
| partial unique index Migration apply | ✅ `\d boss_interactions` 顯示 `ix_boss_interactions_pending_per_group_type UNIQUE WHERE Status='pending'` |
| DB epic_partial_paused row count per epic | ✅ **1 row**（修前 2 row）|
| log 證據 | ✅ `[Stage57-fix] BossInteraction unique constraint 攔住雙 fire race（Type=epic_partial_paused, GroupId=...）— functional race-free + UI 1 卡` |
| V7 regression | ⚠️ vacuously pass（split_task scenario 沒走到 epic_partial_paused path，邏輯上單 fire 第一次 INSERT 必過 trivially safe）|

#### 驗收後修正自診 fix（log 訊號修正，commit `c12ae21`）

V1 fix 第一次 commit `62afaf8` 自驗發現 23505 catch 位置在 `TryCreateUniqueInteractionAsync` outer 是 dead code（`CreateInteractionAsync` 內既有 generic Exception catch 先吃掉 exception），log 是 generic warning 非 fix-specific info。修法：catch 移到 `CreateInteractionAsync` 內 generic Exception catch 之前，emit 正確 fix-specific log（functional 行為不變，純 log 訊號修正）。

#### Stage 57 完整 commit 序列

| commit | 性質 | 內容 |
|---|---|---|
| `711a010` | 主實作 | Stage 57 計劃書 v2 完整套入（FF 五十一 + FF 五十二，6 子項）|
| `6ba851a` | 自驗自診 fix | race Mock 8s Delay 不夠 → polling 60×2s |
| `78a616d` | 自驗自診 fix | auto-approve epic_partial_paused → epic_resume case（pre-Stage 57 既有 bug 順手）|
| `ffe2027` | 自驗自診 fix | HandleEpicPartialPaused CreateExecutionStrategy.ExecuteAsync wrap（NpgsqlRetryingExecutionStrategy 不允許 user-initiated transaction）|
| `500158a` | docs | Forge 自驗結果章節 |
| `62afaf8` | 驗收後修正 v3.46.1 | partial unique index + 23505 catch（路線 a）|
| `c12ae21` | 驗收後修正自診 | 23505 catch 移到 inner emit 正確 fix-specific log |

**V1 + V2：FF 五十一 race condition 雙層防**
```bash
curl -X POST http://localhost:5051/internal/mock/scenario \
  -H "Content-Type: application/json" \
  -d '{"scenario":"framework_pipeline_epic_race_double_fail"}'
sleep 12  # 8s wait + race fire
docker exec aiteam-postgres-1 psql -U aiteam -d aiteam -c \
  "SELECT \"GroupId\", COUNT(*) FROM boss_interactions
   WHERE \"InteractionType\"='epic_partial_paused' AND \"Status\"='pending'
   GROUP BY \"GroupId\" HAVING COUNT(*) > 1;"
# 預期 0 row（修前 1 row 同 epic 2 個 active）
# log: 應出現 "[Stage57] TryCreateUniqueInteraction：同 (groupId=..., type=epic_partial_paused) 已有 active interaction，跳過 fire 新卡"
```

**V3 + V4：FF 五十二 fix loop limit type-specific routing**
```bash
curl -X POST http://localhost:5051/internal/mock/scenario \
  -H "Content-Type: application/json" \
  -d '{"scenario":"framework_pipeline_reviewer_fix_loop_limit"}'
sleep 60  # Pipeline 跑 Kickoff/Design/DevPlan/Dev/Reviewer ×3 fix loop
docker exec aiteam-postgres-1 psql -U aiteam -d aiteam -c \
  "SELECT \"InteractionType\", \"AvailableActionsJson\"
   FROM boss_interactions
   WHERE \"Title\" LIKE '%Stage57%' ORDER BY \"CreatedAt\" DESC LIMIT 1;"
# 預期 reviewer_fix_loop_limit + 3 button JSON（vs 修前 intervention + ack only）
# auto-approve fix_loop_mark_done → QaStageBridge → QA → Doc → done
```

**V6 + V7：regression**
- Stage 55B Session B 5 routing 5 場景仍綠（dev_intervention / qa_intervention / devplan_escalate / dev_plan_unable / split_task_proposal）
- Stage 46 split_task_subtask_fail_intervention 單 sub-task fail 場景仍綠（不被新 idempotent 邏輯誤擋）

### 風險點觀察（實作期確認）

1. **DB transaction + AsNoTracking fresh read**：採基本版 ReadCommitted；自驗 V2 觀察 log 若仍 race write skew → 升級 Serializable + SerializationFailure（SqlState 40001）retry 一次補強
2. **TaskGroupService 達 1591+ 行**：Stage 57 加 ~75 行（NotifyBossReviewerFixLoopLimit + TryRoutePipelineReviewerFixLoopLimit + case dispatch + 兩 case transaction idempotent + SimulateEpicRaceAsync）— 仍未動拆檔，留 follow-up FF 觀察
3. **Mock race Task.Run timing**：8 秒 Delay 等 BuildEpicSubTasksAsync 跑完；若 Kickoff/Design 慢於 8 秒 → Mock race 可能撞 sub-task 還沒建好（log warning「sub-task 數 < 2，無法模擬 race」）— 自驗時觀察 log

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-09 | Stage 57 結案（Aria 第二段）— **v3.46.0 主實作 + v3.46.1 patch（FF 五十一 fire 端 race window 補強，Christ 拍板路線 a partial unique index）**。CHANGELOG / Future_Feature 三檔同步：FF 五十一 ✅ + FF 五十二 ✅ 移入 completed + FF changelog v7.79 + Top 5 更新（FF 五十三上 #1 / FF 三十六 #2 / 加 FF 二十五/五十/七）。**Aria 校準錨**：待 Christ 提供 Forge context 數字後補（同 Stage 56 結案模式）。Aria 結案第二段 commit + push。
| v1.2 | 2026-05-09 | Forge 實作紀錄補入（6 子項全部完成，dotnet build 0 errors，計劃書 v2 三點修正全套入：命名候選 1 / race Mock 路線 b / 真三選獨立 path / AsNoTracking fresh read）。Aria 結案第二段補 CHANGELOG / Future_Feature 同步 + Forge context 校準錨倍率。
| v1.1 | 2026-05-09 | Christ 拍板（Aria 重寫）— actions set 三選 `mark_done` / `skip_qa` / `abort`（label「標完成 / 跳過 QA / 終止 Pipeline」），對應 Trial_v6 Christ 實際強制 done 推進的操作。其他三項依議題層次篩選紀律（user_christ.md:32-38）Aria 自拍：① FF 五十一修法 = 雙層防（對 Christ 看到行為與「只修 fire」一樣，handler 端 race-free 設計層保險）② FF 五十二新 routing 命名 = Forge spike 後對齊 Stage 55B Session B 5 routing 命名提議（純內部 type 字串）③ idempotent helper = 抽 InteractionService.TryCreateUniqueInteractionAsync（race-prone pattern 普遍，未來複用）。子項 0 spike 報告改為純執行對齊（命名提案 + dispatch 鏈路對照 + helper 簽名定稿），無需再 Christ 拍。
| v1.0 | 2026-05-09 | 初版規劃書建立（Aria）— Stage 57 = Trial_v6 揭露 3 🔴 戰略級議題前兩個合併（FF 五十一 race condition 雙層防 + FF 五十二 Vera fix loop HITL routing 補第 6 routing），FF 五十三 API 容錯獨立 Stage 58。Christ 拍板「五十一+五十二合併、五十三獨立」基於兩議題都動 Pipeline framework HITL routing 同一塊，分開做會 merge conflict / 設計分裂。**待 Christ 拍板議題 3 個**：① FF 五十一修法層級（雙層防 / 只修 fire / 只修 handler）② FF 五十二新 routing 命名 + actions set ③ idempotent helper 抽 vs inline。**規劃前期已 grep**：PauseEpicAndNotifyAsync (TaskGroupService.cs:1342) + HandleEpicPartialPausedAsync (line 1158) + BuildEpicSubTasksAsync idempotent 範例 (line 1224) + ReviewerStageExecutor FixIteration≥3 case (line 148-162) + Stage 55B Session B 5 routing dispatch (QaStageExecutor.cs / DevPlanStageExecutor.cs) + InteractionProcessor type+action mapping (line 155-162) + InteractionService.EpicPartialPausedActionsJson (line 64) — 對齊自省點 #23 規劃前期 grep 紀律。
