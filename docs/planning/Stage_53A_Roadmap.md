# Stage 53A：v4 漸進遷移第五步 — macro pipeline NewFeature 主路徑切 framework Workflow（happy path 限定）

> 對應 Future Feature：v4 漸進遷移 7 Stage 路線第五步（議題 A 拆 Stage 後 Stage 53 進一步拆 53A/53B，v4 路線 7→8 Stage）— 不對應特定 active FF
> 對應版本：**v3.39.0**（v4 漸進遷移第五個產生版本變動的 Stage）
> 建立日期：2026-05-03
> 狀態：✅ **完成**（2026-05-03，3 session 連跑 + 議題 G3 修正方案 C 拍板 + 驗收期 4 follow-up 修畢 + 6 場景驗收通過）
> 文件版本：v2.1（Forge 結案第一段含驗收期紀錄）

---

## 概述

**戰略背景**：[Stage 48 FF 四十九 spike](Stage_48_Roadmap.md) 結論啟動 v4 漸進遷移路線。**Stage 49（v3.35.0）+ Stage 50（v3.36.0）+ Stage 51（v3.37.0）+ Stage 52（v3.38.0）** 完成前四步（Appeal loop / Kickoff Meeting / HITL 試點 / Design Meeting，均屬「節點內部 framework」）。**Stage 53A 是 v4 漸進遷移第五步 — macro-orchestration 真正啟動**：把整個任務 pipeline（NewFeature 主路徑：proposal_approved → Kickoff → Design → Dev_plan → Dev → Reviewer → QA → Doc → NotifyBossMerge）遷到 framework Workflow，**framework-in-framework**（pipeline 主 Workflow + inner Kickoff/Design Workflow 雙層並存）。

**Aria 重要揭露（規劃前期）**：原以為 Stage 53 範圍 = `WorkflowEngine.cs` 173 行 lookup table 遷移，grep 真實 caller 結構後揭露 **WorkflowEngine 只是「落底」邏輯**，整個 pipeline 控制流散落在多個 service：
- `TaskGroupService.HandleAgentCompletedAsync`（~300 行攔截 hooks + 落底 GetDecision）
- `AppealOrchestrationService`（Dev_plan / Reviewer 完成 hook + Petra 閘門 + Dev 阻礙）
- `QaCoordinationService`（QA 完成 routing：no_tests / env_or_test_issue / passed）
- `MeetingOrchestrationService`（Kickoff / Design 會議入口 + escalate 路徑）
- `WorkflowEngine.GetDecision`（純 lookup table）

**A2 拆 Stage 拍板**：Stage 53 真實範圍超大，原 v4 路線 Stage 53 拆為 **Stage 53A**（NewFeature 主路徑 happy path）+ **Stage 53B**（fix loop + appeal + QA fix loop + intervention 子流程），守混合型 ×0.96-1.25 區間精神。**v4 路線 7→8 Stage**。

**核心 lifecycle 機制（vs Stage 49/50/52）**：
- Stage 49/50/52：framework Workflow 跑「節點內部」（單一會議 / 單一 Appeal loop），同步跑完
- **Stage 53A**：framework Workflow 跑「整個任務 pipeline」，**lifecycle 混合**：
  - **會議型 stage**（Kickoff / Design）：Stage Executor 同步 await `FrameworkKickoffRouter.HandleKickoffMeetingAsync` / `FrameworkDesignRouter.HandleDesignMeetingAsync`（既有 inner Workflow 已完整，G3 拍板）
  - **Agent 型 stage**（Dev_plan / Dev / Reviewer / QA / Doc）：Stage Executor enqueue 後 yield，`HandleAgentCompletedAsync` callback 觸發 `ResumeStreamingAsync` 推進下個 stage（沿用 Stage 51 HITL yield-resume + Checkpointing 跨 process restart know-how）

**範圍邊界（A2 拆 Stage 53A happy path 限定）**：
- ✅ **新建**：`PipelineFrameworkStateJson` DB 欄位 + `Workflows/Pipeline/` 資料夾（`PipelineState` / `PipelineWorkflowFactory` / `PipelineCheckpointStore` / 9 stage Executor）+ `FrameworkPipelineRouter` + feature flag `Workflow:UseFrameworkPipeline`
- ✅ **動既有 framework router**（F-α 拍板首次跨 Stage 修改）：Stage 49/50/52 既有 router 的 `RecoverStuck*Async` 篩選條件**追加** `&& g.PipelineFrameworkStateJson == null`（4 個檔各 +1 行）
- ✅ **fail / intervention 路徑主動 fallback to legacy**（I2 拍板首次「fallback to legacy」反向設計）：Reviewer 🔴 / Dev_plan 失敗 escalate / Dev 阻礙 / 仲裁後 Dev_fix / QA 修復 5 個 fallback 點清 marker → legacy 接管（Stage 53B 動完後 Stage 55 收尾統一移除 fallback）
- ❌ **不動**：fix loop / appeal / QA fix loop / intervention 子流程程式碼（Stage 53B 範圍）
- ❌ **不動**：既有 BossInteraction 10+ type / InteractionService / InteractionRespondService（A3 試點精神延續）
- ❌ **不動**：DesignerAgentService / WorkflowEngine.cs（GetDecision 邏輯由 framework AddSwitch 自然替代，但 cs 檔暫不刪 — Stage 55 收尾移除）

**v4 路線第五步風險預警**：
- **F1 framework-in-framework lifecycle**（高風險，spike 主驗證項）：雙 CheckpointStore + 雙 ActiveOrchestration marker 並存
- **F2 yield-resume routing 整合**（中-高風險，spike 主驗證項）：framework 1.3.0 是否支援 `RequestPort` 以外的 Executor 內 yield 等外部 callback signal
- **F-α 跨 Stage 修改既有 router**（首次出現）：Stage 49/50/52 既有 framework router 各 +1 行排除條件
- **I2 fallback to legacy**（跟 Stage 49/50/52 fallback 拍板**相反**，臨時設計留 Stage 55 收尾移除）
- **macro-orchestration 規模 unknown**：Stage 49-52 都是「節點內部」，53A 是「整個任務調度核心」首次 framework 化，可能突破混合型 ×0.96-1.25 上界

→ feature flag 為主要安全網（`Workflow:UseFrameworkPipeline` 預設 false，三 flag 連動規則 `UseFrameworkKickoff = true` AND `UseFrameworkDesign = true` 才有意義）。

---

## 設計決策（Christ 2026-05-03 拍板）

### 主路線拍板（戰略級）

| 議題 | 拍板 | 替代方案 |
|---|---|---|
| **議題 A：Stage 53 範圍** | **A2 拆 Stage**：Stage 53A = NewFeature 主路徑 happy path / Stage 53B = fix loop + appeal + QA fix loop + intervention（v4 路線 7→8 Stage 守混合型區間） | A1 一個 Stage 全切（必拆 3-4 session 突破 ×1.4 上界）/ A3 分子段遷（路線變太長）|
| **議題 B：fix loop 處理** | **拆到 Stage 53B**（53A 不討論） | — |
| **議題 C：53A 邊界內攔截 hooks** | **C2：QaStage Executor 內 call 既有 `qaCoordination.HandleQaCompletedAsync(...)` 同步 await**（G3 拍板自然延伸，single SoT，QaCoordinationService 內部 routing 邏輯不動） | C1 framework 接管 routing（雙寫漂移）/ C3 抽 routing helper SoT（規模放大，留 Stage 54+ 評估）|
| **議題 D：spike 第一步驗範圍** | **D2：兩項精準驗證** — F1 framework-in-framework lifecycle（雙 CheckpointStore + 雙 ActiveOrchestration marker + 4 marker 共存的 Recovery 篩選優先級） + F2 yield-resume routing 整合（含 fallback `RequestPort` 包裝對齊 Stage 51 試點） | D1 三項全驗（F3 因 G3 隱含解，over-spike）/ D3 一項驗證（過度樂觀）|
| **議題 E：feature flag 顆粒度** | **E1 單一 flag `Workflow:UseFrameworkPipeline` + 三 flag 連動**（沿用 Stage 51 雙 flag 連動 pattern：本 flag 只在 `UseFrameworkKickoff = true` AND `UseFrameworkDesign = true` 時有意義，否則 Dashboard 顯示 disabled 警示）| E2 每 WorkflowType 一個 flag（53A 範圍只 NewFeature 不適用）/ E3 細顆粒度每 stage 一個（過度設計）|
| **議題 F：Crash Recovery 協調** | **F-α 層級隔離**：Stage 49/50/52 既有 router 的 `RecoverStuck*Async` 篩選條件**追加** `&& g.PipelineFrameworkStateJson == null`；Stage 53A `RecoverStuckFrameworkPipelineAsync` 統一處理外層 + 透過 ResumeStreamingAsync 跨層 cascade resume 內層 inner Workflow checkpoint。對齊既有「legacy 排除 framework marker」模式的層級延伸 | F-β 各自獨立跑 + 內部冪等（依賴 framework 1.3.0 冪等實作未驗）/ F-γ 抽 base class + 優先級鏈（規模放大留 Stage 55 評估）|
| **議題 G：跟既有 framework path 整合** | **G3 混合 framework-in-framework**：pipeline 主 Workflow 內 stage Executor 呼叫既有 router 的 `Handle*Async` method（既有 router 內部已是 framework Workflow），等於 framework-in-framework，但每層獨立 Workflow + 獨立 CheckpointStore | G1 subworkflow pattern（framework 1.3.0 支援度未驗）/ G2 節點 framework 替換為 Executor（破壞既有 Stage 49/50/52 既有架構） |
| **議題 H：Mock 場景設計（H-mid 6 場景）** | `framework_pipeline_happy_path` / `framework_pipeline_kickoff_resume` ⭐（F1 lifecycle）/ `framework_pipeline_dev_resume` ⭐（F2 yield-resume）/ `framework_pipeline_qa_no_tests`（C2 整合）/ `framework_pipeline_reviewer_fallback` ⭐（53A→53B fallback）/ `framework_pipeline_kickoff_escalate`（fallback to legacy 邊界）— 對齊 Stage 49-52 6 場景慣例 | H-min 4 場景（漏 framework-in-framework lifecycle）/ H-full 7 場景（over-coverage Trial 跑完整流程已覆蓋） |
| **議題 I：Petra 閘門 routing 整合（53A 邊界 happy path 限定）** | **I2 + fail 路徑主動 fallback to legacy**：Stage Executor 內 call `appealOrchestration.RunPetraGateAsync(...)` 同步 await；passed → SendMessage 推下個 stage / **fail → 清 PipelineFrameworkStateJson + ActiveOrchestration marker → 由 legacy AppealOrchestrationService 接管 fix loop（53B 未動範圍）**。**首次「fallback to legacy」反向設計**（與 Stage 49/50/52 拍板相反）— 因 53B 還沒做必須留 fallback；Stage 55 收尾移除 | I1 framework 接管 Petra 閘門（雙寫漂移）/ I3 抽 Petra 閘門 helper SoT（留 Stage 53B 評估）|
| **議題 J：Stage 53A 主 Workflow 拓撲設計** | **J1 混合 lifecycle 統一在 framework Workflow**：會議型 stage 同步 await + Agent 型 stage yield-resume（沿用 Stage 51 HITL try know-how）— `HandleAgentCompletedAsync` 入口分流：UseFrameworkPipeline=true + PipelineFrameworkStateJson != null → call `FrameworkPipelineRouter.ResumeAfterAgentAsync` 觸發 ResumeStreamingAsync；else legacy 路徑 | J2 兩段獨立 Workflow（兩段 lifecycle 同步麻煩）/ J3 每 stage 獨立 Workflow（失去 macro 戰略價值）|

### Aria 拿捏（已決）

| # | 議題 | 決定 |
|---|---|---|
| 1 | DB schema | **加 1 個欄位** `task_groups.PipelineFrameworkStateJson`（對齊 Stage 49/50/52 pattern，Migration `Stage53ATaskGroupPipelineFrameworkState`）|
| 2 | ActiveOrchestration 雙 marker | 設 `"FrameworkPipeline"`（對齊 Stage 49/50/52 命名慣例 — 與 PipelineFrameworkStateJson 搭配區隔 legacy/framework Crash Recovery）|
| 3 | 入口分流位置 | 兩處：① `HandleAgentCompletedAsync` 開頭加 framework path 分流（UseFrameworkPipeline + PipelineFrameworkStateJson != null → call `FrameworkPipelineRouter.ResumeAfterAgentAsync` + return；else legacy 路徑） ② `RunNewFeatureWorkflowAsync` 入口或 proposal_approved trigger point 加 framework path 分流（call `FrameworkPipelineRouter.HandlePipelineAsync` 啟動主 Workflow） |
| 4 | Crash Recovery hook | `AgentQueueProcessor` 既有 hook 之後（line 85 後）加 `await frameworkPipelineRouter.RecoverStuckFrameworkPipelineAsync(stoppingToken)` — 順序在 Stage 49/50/52 hook 之後 / legacy `RecoverStuckOrchestrationsAsync` 之後（沿用 Stage 50 踩坑 #4 hook 順序紀律：legacy 先跑因排除 framework marker 已 set）|
| 5 | 既有 4 router 排除條件擴充（F-α）| `MeetingOrchestrationService.RecoverStuckOrchestrationsAsync`（legacy）/ `FrameworkAppealRouter.RecoverStuckFrameworkAppealsAsync`（Stage 49）/ `FrameworkKickoffRouter.RecoverStuckFrameworkKickoffsAsync`（Stage 50）/ `FrameworkDesignRouter.RecoverStuckFrameworkDesignAsync`（Stage 52）篩選條件全追加 `&& g.PipelineFrameworkStateJson == null`（避免 collision）|
| 6 | Token 計費 | 沿用既有 `MeetingCommons.RunAgentTurnAsync` + `ClaudeCodeService` 各自既有 tokenLogService 機制（pipeline framework 不引入新 token 計算邏輯，每 stage Executor 內 call 既有 service 自然涵蓋）|
| 7 | CLAUDE_*.md prompt | 不動（沿用 Stage 49-52 慣例，framework 是 orchestration 控制層機制不影響 Agent prompt）|
| 8 | BossInteraction 整合 | A3 試點精神延續 — 沿用既有 `kickoff` / `design` / `merge_notify` BossInteraction type，由 inner FrameworkKickoffRouter / FrameworkDesignRouter 既有 escalate 路徑處理；Stage 53A 主 Workflow 不引入新 BossInteraction type |
| 9 | Christ 確認點 | Kickoff 確認 / Design 確認 / Merge 確認都沿用 Stage 28a `PendingConfirmationStore` 既有機制；pipeline framework yield-resume 自然對映 J1 機制（callback 觸發 ResumeStreamingAsync 推進）|
| 10 | Stage Executor 命名統一 | `Workflows/Pipeline/Executors/`：`PipelineStartExecutor` / `KickoffStageExecutor` / `DesignStageExecutor` / `DevPlanStageExecutor` / `DevStageExecutor` / `ReviewerStageExecutor` / `QaStageExecutor` / `DocStageExecutor` / `NotifyMergeStageExecutor`（具體拓撲設計交 Forge Plan Mode 拍板）|
| 11 | Mock 場景觸發機制 | 對齊 Stage 49-52 `MockClaudeCodeService.FailScenario` static 傳遞 scenario key 慣例；MockScenarioService 加 6 個 `framework_pipeline_*` case |
| 12 | fallback fallback 細節（I2 隱含）| Stage 53A 主 Workflow 內偵測 fail/intervention 路徑時：① 清 `task_groups.PipelineFrameworkStateJson = null` ② 清 `ActiveOrchestration = null` ③ 由 legacy AppealOrchestrationService / WorkflowEngine.GetDecision 既有路徑接管。**5 個 fallback 點**：Reviewer 🔴 / Dev_plan 失敗 escalate / Dev 阻礙 [BLOCKED] / 仲裁後 Dev_fix（SkipReviewerAfterArbitration） / QA 修復 Dev_fix（QaFixRound > 0） |

### Stage 53A 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **0** | **Spike 第一步：D2 兩項驗證**（Forge Plan Mode 第一步）— F1 framework-in-framework lifecycle + F2 yield-resume routing 整合 | S |
| **1** | feature flag `Workflow:UseFrameworkPipeline` + `WorkflowSettings` / `WorkflowSettingsResolver` 擴充（含三 flag 連動 helper） | XS |
| **2** | DB schema：`task_groups.PipelineFrameworkStateJson` 欄位 + Migration `Stage53ATaskGroupPipelineFrameworkState` + `Entities.cs` | XS |
| **3** | F-α 跨 Stage 修改既有 router 排除條件（4 處）| XS |
| **4** | `PipelineState` 設計（含 GroupId / DesignTaskId 等對齊 Stage 51 KickoffTaskId pattern + 各 stage 完成 marker + last result hand-off + 5 個 fallback flag）| S |
| **5** | `PipelineWorkflowFactory` + 9 stage Executor + 拓撲（會議型同步 await + Agent 型 yield-resume）| L |
| **6** | `PipelineCheckpointStore`（對齊 Stage 49/50/52 CheckpointStore pattern，「3 次再抽象」原則第 4 次出現留 Stage 55 評估抽 base class，Stage 53A 不抽） | S |
| **7** | `FrameworkPipelineRouter`（含 `HandlePipelineAsync` 主入口 + `ResumeAfterAgentAsync` callback resume + `RecoverStuckFrameworkPipelineAsync` Crash Recovery + `FinalizePipelineAsync` 收尾）| L |
| **8** | 入口分流：① `HandleAgentCompletedAsync` 加 framework path 分流（callback resume）② `RunNewFeatureWorkflowAsync` 或 proposal_approved trigger point 加 framework path 分流（啟動主 Workflow） | S |
| **9** | Crash Recovery hook + Dashboard SystemSettings 加第五 toggle + Mock 場景擴充 6 個 `framework_pipeline_*` + Forge 自驗 | M |
| **10** | Version bump v3.39.0 + 結案文件（Roadmap 實作紀錄章節）| XS |

> **不寫工時估算**（workflow_aria.md 第三節 A 第 4 條）— 各子項規模見上表 XS/S/M/L 範圍描述。

---

## 子項 0：Spike 第一步 — D2 兩項驗證

### 驗證項目

| # | 驗證題 | 驗證方法 | 影響 |
|---|---|---|---|
| **F1** | framework-in-framework lifecycle | 讀 framework 1.3.0 `Microsoft.Agents.AI.Workflows` xml doc + Stage 49/50/52 既有 router pattern reference；驗 ① 雙 CheckpointStore（Pipeline + Kickoff/Design）各自寫 DB 是否衝突 ② 雙 ActiveOrchestration marker（"FrameworkPipeline" 外 + "FrameworkKickoff"/"FrameworkDesign" 內）並存的 Recovery 篩選優先級 ③ 4 marker 共存（PipelineFrameworkStateJson + KickoffFrameworkStateJson + DesignFrameworkStateJson + FrameworkAppealStateJson）的 collision detection | **影響子項 5/7 設計**：framework-in-framework 可行 → KickoffStageExecutor / DesignStageExecutor 同步 await 既有 router 的 `Handle*Async`；不可行 → fallback 路線拆兩個 Workflow（pipeline 主 Workflow 結束後 router 串接到下個 stage 主 Workflow），規模 +30% |
| **F2** | yield-resume routing 整合 | 讀 framework 1.3.0 docs + Stage 51 RequestPort 既有 pattern；驗 ① framework 是否支援 `RequestPort` 以外的「Executor 內 yield 等任意外部 callback signal」模式 ② fallback：每個 Agent stage 包成 RequestPort（Agent enqueue = 發 request，Agent 完成 callback = 回 response），對齊 Stage 51 既有 mechanism — 是否語意 OK | **影響子項 5/7 設計**：① 原生支援非 RequestPort yield → Agent stage Executor 內直接 yield 等 callback；② 只支援 RequestPort → Stage 53A 每個 Agent stage 都包 RequestPort + Bridge service 內部包 callback → SendResponseAsync 適配層（規模 +20%）|

### Spike 結案產出

- **路線拍板紀錄**寫進 Forge Plan Mode plan 檔最前段
- **2 項驗證證據**（NuGet 文件引用 / GitHub sample 引用 / 必要時建小 spike 程式片段）
- **設計風險升級或降級**：依 spike 結果調整風險點 R1-R8 評估

### Spike 階段失敗條件

若以下任一發生 → 暫停 Stage 53A + 回報 Christ 評估：
- F1 雙層 Workflow lifecycle 衝突 + 兩個獨立 Workflow fallback 對 PipelineFrameworkStateJson 序列化邊界要 fundamental 重新設計
- F2 framework 不支援非 RequestPort yield + RequestPort 包裝 fallback 對 Stage 53A 每個 Agent stage 都需要寫 Bridge service 適配層（規模 +50%）

---

## 子項 1：feature flag 擴充

### 實作項目

**位置**：`src/AiTeam.Bot/Configuration/WorkflowSettings.cs` + `WorkflowSettingsResolver.cs`

**WorkflowSettings 新增屬性**：
- `bool UseFrameworkPipeline { get; set; } = false;`

**WorkflowSettingsResolver 新增 method**：
- `Task<bool> GetUseFrameworkPipelineAsync(CancellationToken ct = default)` — 對齊既有 `GetUseFrameworkKickoffAsync` pattern
- 三 flag 連動 helper（建議）：caller 自行檢查三 flag（本 method 不做檢查），對齊 Stage 51 雙 flag 連動 pattern「caller 自行檢查兩 flag」

**AppSettings key**：`Workflow:UseFrameworkPipeline`，預設 `false`

**三 flag 連動規則**：本 flag 只在 `UseFrameworkKickoff = true` AND `UseFrameworkDesign = true` 時有意義（pipeline framework 內 KickoffStage / DesignStage Executor 呼叫既有 router 的 `Handle*Async`，路徑必須 framework 才通）。Dashboard UI 上顯示 disabled 狀態當任一前置 flag = false。

---

## 子項 2：DB schema

### 實作項目

**Entities.cs 新欄位**：
```
public string? PipelineFrameworkStateJson { get; set; }
```

**Migration 名稱**：`Stage53ATaskGroupPipelineFrameworkState`

**Entity comment**：對齊 Stage 49/50/52 既有 framework state JSON 欄位 comment pattern（含「null = 走 legacy / 有值 = framework Workflow 進行中或已完成 / 走 framework path 時搭配 ActiveOrchestration = "FrameworkPipeline" / 與 Stage 49/50/52 三個 framework state JSON 完全獨立 / legacy 排除條件 必須加 PipelineFrameworkStateJson == null（F-α 緩解）」）。

---

## 子項 3：F-α 跨 Stage 修改既有 router 排除條件

### 實作項目（首次跨 Stage 修改既有 framework 程式碼）

**4 處各 +1 行 `&& g.PipelineFrameworkStateJson == null`**：

| # | 檔案 | Method | 既有排除條件 → Stage 53A 追加 |
|---|---|---|---|
| 1 | `MeetingOrchestrationService.cs` | `RecoverStuckOrchestrationsAsync` | 既有 3 條（FrameworkAppealStateJson + KickoffFrameworkStateJson + DesignFrameworkStateJson）→ 加 PipelineFrameworkStateJson |
| 2 | `FrameworkAppealRouter.cs` | `RecoverStuckFrameworkAppealsAsync` | 既有篩選 `FrameworkAppealStateJson != null && !IsPaused` → 加 `&& PipelineFrameworkStateJson == null` |
| 3 | `FrameworkKickoffRouter.cs` | `RecoverStuckFrameworkKickoffsAsync` | 既有篩選 `KickoffFrameworkStateJson != null && !IsPaused` → 加 `&& PipelineFrameworkStateJson == null` |
| 4 | `FrameworkDesignRouter.cs` | `RecoverStuckFrameworkDesignAsync` | 既有篩選 `DesignFrameworkStateJson != null && !IsPaused` → 加 `&& PipelineFrameworkStateJson == null` |

**Trade-off 紀錄（必須 commit message 寫清楚）**：
- Stage 53A 首次出現「跨 Stage 修改既有 framework 程式碼」
- 4 個既有 router 各 +1 行排除條件，影響面小（純篩選邏輯擴充，不改業務行為）
- Stage 55 收尾時若移除「I2 fallback to legacy」反向設計，這些排除條件可能要重新評估（Stage 53A 不處理）

**設計理由**：對齊既有「legacy 排除 framework marker」模式的層級延伸 — 「外層 framework 排除內層」是同類設計擴展。

---

## 子項 4：PipelineState 設計

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Pipeline/PipelineState.cs`（新檔）

**PipelineState 欄位**（隨 framework state 序列化進 task_groups.PipelineFrameworkStateJson）：

| 欄位 | 型別 | 說明 |
|---|---|---|
| GroupId | Guid | TaskGroup 識別 |
| WorkflowType | string | "new_feature" / "tech_improvement" / "bug_fix"（53A 主路徑只 new_feature，留欄位給 53B 擴充）|
| CurrentStage | string | 當前 stage 名稱（"Kickoff" / "Design" / "Dev_plan" / "Dev" / "Reviewer" / "QA" / "Doc" / "NotifyMerge"），跨 callback resume 用 |
| LastAgentResult | AgentExecutionResult? | callback 帶入的 result（resume 時 SendResponseAsync 用）|
| KickoffDone / DesignDone / DevPlanDone / DevDone / ReviewerDone / QaDone / DocDone | bool | 各 stage 完成 marker |
| FallbackToLegacy | bool | 5 個 fallback 點觸發時設 true，主 Workflow 結束流程 |
| FallbackReason | string? | fallback 原因（debug / log 用，Reviewer 🔴 / Dev_plan 失敗 / Dev 阻礙 / 仲裁後 Dev_fix / QA 修復）|

**設計理由**：
- 全欄位在 framework state 內 → 隨 PipelineCheckpointStore 序列化進 PipelineFrameworkStateJson（不加新 DB column）
- 對齊 Stage 49/50/52 KickoffState / DesignState pattern
- LastAgentResult 設計支援 J1 yield-resume：callback 觸發 ResumeStreamingAsync 後，下個 stage Executor 從 state 讀 LastAgentResult 判斷推進
- FallbackToLegacy + FallbackReason 設計支援 I2「fail/intervention 路徑主動 fallback to legacy」邊界處理

---

## 子項 5：PipelineWorkflowFactory + 9 Stage Executor + 拓撲

### 9 Stage Executor 設計

| Executor | 角色 | lifecycle | 出口 |
|---|---|---|---|
| **PipelineStartExecutor** | StartExecutor，接 router initial PipelineState | 同步 | `KickoffStageBridge`（含 GroupId / WorkflowType）|
| **KickoffStageExecutor** | call `FrameworkKickoffRouter.HandleKickoffMeetingAsync` 同步 await（會議型 stage） | 同步 await | `DesignStageBridge`（Kickoff done 後）|
| **DesignStageExecutor** | call `FrameworkDesignRouter.HandleDesignMeetingAsync` 同步 await（會議型 stage） | 同步 await | `DevPlanStageBridge`（Design consensus 後 + 拆 task 提案沒觸發時）/ FallbackBridge（escalate 路徑 / 拆 task 提案觸發時 fallback to legacy）|
| **DevPlanStageExecutor** | enqueue Dev_plan Agent + yield 等 callback（Agent 型 stage）| **yield-resume**（J1 機制）| `DevStageBridge`（Dev_plan passed Petra 閘門後）/ FallbackBridge（Dev_plan 失敗 escalate fallback）|
| **DevStageExecutor** | enqueue Dev Agent + yield 等 callback | **yield-resume** | `ReviewerStageBridge`（Dev 完成）/ FallbackBridge（Dev 阻礙 [BLOCKED] / 失敗 needs_intervention）|
| **ReviewerStageExecutor** | enqueue Reviewer + yield 等 callback + 內含 Petra 閘門 routing（call `appealOrchestration.RunPetraGateAsync` 同步 await） | **yield-resume + 同步 Petra 閘門** | `QaStageBridge`（Reviewer ✅ + Petra passed）/ FallbackBridge（Reviewer 🔴 fallback to legacy fix loop）|
| **QaStageExecutor** | enqueue QA + yield 等 callback + call `qaCoordination.HandleQaCompletedAsync` 同步 await（C2 拍板，G3 拍板自然延伸） | **yield-resume + 同步 routing** | `DocStageBridge`（QA passed）/ FallbackBridge（QA 修復 Dev_fix 走 53B）|
| **DocStageExecutor** | enqueue Doc + yield 等 callback（NewFeature 主路徑）| **yield-resume** | `NotifyMergeStageBridge` |
| **NotifyMergeStageExecutor** | call `taskGroupService.NotifyBossMergeAsync` 同步 await + YieldOutputAsync(`PipelineLoopResult`) | 同步 await + 結束 | `PipelineLoopResult`（含 final marker）|

### Workflow 拓撲（依 spike F1/F2 結論調整）

```
PipelineStartExecutor
   ↓ AddEdge (KickoffStageBridge)
KickoffStageExecutor (同步 await FrameworkKickoffRouter.HandleKickoffMeetingAsync)
   ↓ AddEdge (DesignStageBridge)
DesignStageExecutor (同步 await FrameworkDesignRouter.HandleDesignMeetingAsync)
   ├ Design consensus + no split → AddEdge (DevPlanStageBridge)
   └ Design escalate / split task proposal → AddEdge (FallbackBridge → 結束 Workflow + fallback to legacy)
DevPlanStageExecutor (yield-resume)
   ├ Dev_plan passed → AddEdge (DevStageBridge)
   └ Dev_plan failed escalate → FallbackBridge
DevStageExecutor (yield-resume)
   ├ Dev 完成 → AddEdge (ReviewerStageBridge)
   └ Dev 阻礙 / 失敗 → FallbackBridge
ReviewerStageExecutor (yield-resume + 內含 Petra 閘門)
   ├ Reviewer ✅ + Petra passed → AddEdge (QaStageBridge)
   └ Reviewer 🔴 → FallbackBridge
QaStageExecutor (yield-resume + 同步 QaCoordinationService.HandleQaCompletedAsync)
   ├ QA passed → AddEdge (DocStageBridge)
   └ QA 修復 → FallbackBridge
DocStageExecutor (yield-resume)
   ↓ AddEdge (NotifyMergeStageBridge)
NotifyMergeStageExecutor (同步 NotifyBossMergeAsync + YieldOutputAsync)
   ↓ PipelineLoopResult → router finalize
```

### 設計約束（沿用 Stage 50 三件套紀律 + Stage 51 雙 handler pattern）

- 所有顯式 SendMessageAsync / YieldOutputAsync 的 Executor 必須三件套：`[SendsMessage(typeof(T))]` 或 `[YieldsOutput(typeof(T))]` + `partial class` + 註解
- Workflow router 一律用 `RunStreamingAsync` + `WatchStreamAsync` foreach（對齊 Stage 50 踩坑 #9）
- yield-resume Stage Executor 必須含「**state.LastAgentResult 寫入 + yield**」+「ResumeStreamingAsync 觸發 + SendResponseAsync(LastAgentResult) → handler 讀 state 推進」雙手順套（對齊 Stage 51 MidInterruptCheckExecutor 既有 pattern + Stage 52 fix#2 教訓「拆 plan executor 解 type filter 不 source-aware」延續 — 各 stage Executor 用 type-explicit 拓撲分流避免 dispatch 衝突）

---

## 子項 6：PipelineCheckpointStore

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Pipeline/PipelineCheckpointStore.cs`（新檔）

**設計**：對齊 Stage 49/50/52 既有 `KickoffCheckpointStore` / `DesignCheckpointStore` pattern — 90% 邏輯相同（從 task_groups.PipelineFrameworkStateJson 載 / 寫 framework state）。

「3 次再抽象」原則第 4 次出現相似 pattern → **Stage 55 收尾時評估抽 base class**（Stage 53A 不抽，避免擴大規模違反議題 A2 拆 Stage 精神，保持現有四個獨立 CheckpointStore class）。

---

## 子項 7：FrameworkPipelineRouter

### 核心 method

| Method | 職責 |
|---|---|
| `HandlePipelineAsync(TaskGroup group, CancellationToken ct)` | 主入口（對齊 Stage 50 `HandleKickoffMeetingAsync`）— ActiveOrchestration 雙 marker + RunStreamingAsync + WatchStreamAsync 收 WorkflowOutputEvent + finalize 段 + 5 個 fallback 點處理（清 marker + legacy 接管）|
| `ResumeAfterAgentAsync(TaskGroup group, string completedAgent, AgentExecutionResult result, CancellationToken ct)` | callback resume（J1 機制核心）— 由 `HandleAgentCompletedAsync` 入口分流呼叫；新 HTTP scope 內 ResumeStreamingAsync from latest checkpoint + SendResponseAsync(result) → 下個 stage Executor 推進 |
| `RecoverStuckFrameworkPipelineAsync(CancellationToken ct)` | Crash Recovery — 篩選 `g.PipelineFrameworkStateJson != null && !g.IsPaused` + 沿用 Stage 49/50/52「降級策略清 marker」拍板（Stage 53A 沒 HITL，純清 marker 重觸發 entry）|
| `FinalizePipelineAsync(...)` | 收尾 — pipeline 完成（NotifyMerge）/ fallback to legacy 兩條路徑分支處理 |

### DI 註冊

對齊 Stage 49/50/52 慣例 Singleton（ctor 注入 IServiceProvider / IServiceScopeFactory / PipelineWorkflowFactory / PipelineCheckpointStore / DiscordSocketClient / IOptions / GitHubService / InteractionService / WorkflowSettingsResolver / TaskGroupService / AppealOrchestrationService / QaCoordinationService / FrameworkKickoffRouter / FrameworkDesignRouter / ILogger，scoped 服務 method 內 CreateAsyncScope 動態取）。

### fallback 拍板（議題 12）

5 個 fallback 點觸發時：
1. 清 `task_groups.PipelineFrameworkStateJson = null`
2. 清 `task_groups.ActiveOrchestration = null`
3. 由 legacy AppealOrchestrationService / WorkflowEngine.GetDecision 既有路徑接管
4. log `[Stage53A-FallbackToLegacy] {reason}`（5 種 reason：reviewer_critical / dev_plan_failed_escalate / dev_blocker / arbitration_skip_reviewer / qa_fix_loop）

---

## 子項 8：入口分流（兩處）

### 8A：`HandleAgentCompletedAsync` 加 framework path 分流（callback resume）

**位置**：`src/AiTeam.Bot/Orchestration/TaskGroupService.cs` 既有 `HandleAgentCompletedAsync` 開頭（line 105 之後，line 117 group 取得後）

**修改方式**（對齊 Stage 50 同 service 既有 Kickoff 分流 pattern）：

```
（既有 line 117 group 取得 + line 124 status 檢查後）
（新增）
if (await workflowResolver.GetUseFrameworkPipelineAsync(ct)
    && group.PipelineFrameworkStateJson != null)
{
    logger.LogInformation("[Stage53A] HandleAgentCompletedAsync framework path 接管（Group={Id}, completedAgent={Agent}）",
        groupId, completedAgent);
    var router = scope.ServiceProvider.GetRequiredService<FrameworkPipelineRouter>();
    await router.ResumeAfterAgentAsync(group, completedAgent, result, cancellationToken);
    return;
}
（既有 legacy 路徑保留不動）
```

### 8B：proposal_approved trigger point 加 framework path 分流（啟動主 Workflow）

**位置**：`src/AiTeam.Bot/Orchestration/TaskGroupService.cs` `RunNewFeatureWorkflowAsync` 入口或對應 trigger 處（Forge Plan Mode 確認位置）

**修改方式**：feature flag UseFrameworkPipeline=true → call `FrameworkPipelineRouter.HandlePipelineAsync(group, ct)` 後 return；else legacy 路徑。

**設計理由**：framework path 內部已處理完整 pipeline lifecycle（含 yield-resume + finalize），legacy path 既有處理保留不動。

---

## 子項 9：Crash Recovery hook + Dashboard + Mock 場景

### Crash Recovery hook

**位置**：`src/AiTeam.Bot/Orchestration/AgentQueueProcessor.cs` 既有 hook 之後（line 85 後加新一段）

**hook 順序**（沿用 Stage 50 踩坑 #4 紀律）：

```
Line 73: RecoverStuckOrchestrationsAsync (legacy，4 marker 排除全)
Line 77: RecoverStuckFrameworkAppealsAsync (Stage 49)
Line 81: RecoverStuckFrameworkKickoffsAsync (Stage 50)
Line 85: RecoverStuckFrameworkDesignAsync (Stage 52)
Line 89（新增）: RecoverStuckFrameworkPipelineAsync (Stage 53A)
```

### Dashboard SystemSettings UI 第五 toggle

**位置**：`src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor` + `.razor.cs`

既有「v4 漸進遷移控制」區塊下方追加第五 toggle：「使用 MS Agent Framework Pipeline（Stage 53A v4 漸進遷移第五步 macro-orchestration）」對應 `Workflow:UseFrameworkPipeline`，警告文字寫明「⚠️ 預設關閉，啟用後 NewFeature 主路徑走 framework Workflow path（macro-orchestration framework-in-framework）；**需先啟用 Stage 50 UseFrameworkKickoff + Stage 52 UseFrameworkDesign**，三 flag 連動」。

### Mock 場景擴充（議題 H-mid 6 場景）

**位置**：`src/AiTeam.Bot/Services/MockScenarioService.cs` + `MockClaudeCodeService.cs`

新增 6 個 `framework_pipeline_*` 系列場景：

| 場景 key | 行為 |
|---|---|
| `framework_pipeline_happy_path` | 完整跑通主路徑（Kickoff consensus → Design consensus 無 split → Dev_plan passed → Dev passed → Reviewer ✅ + Petra passed → QA passed → Doc → NotifyBossMerge）|
| `framework_pipeline_kickoff_resume` ⭐ | Kickoff stage 跑期間 simulate `docker compose restart aiteam-bot` → ResumeStreamingAsync from inner+outer checkpoint（F1 framework-in-framework lifecycle 驗證）|
| `framework_pipeline_dev_resume` ⭐ | Dev stage yield 期間 simulate restart → ResumeStreamingAsync 恢復 → callback 觸發後續 stage（F2 yield-resume 驗證）|
| `framework_pipeline_qa_no_tests` | QA no_tests routing（QaCoordinationService 內部分支驗證 C2 整合）|
| `framework_pipeline_reviewer_fallback` ⭐ | Reviewer 🔴 → 清 marker → fallback to legacy AppealOrchestrationService（議題 I2 邊界 + 53A→53B fallback 驗證）|
| `framework_pipeline_kickoff_escalate` | Kickoff escalate → fallback to legacy（fallback to legacy 邊界場景）|

### Forge 自驗 6 場景

對齊 Stage 49-52 forge-end SOP — Forge 自驗 5 靜態場景（Mock 邏輯審視 + 程式碼路徑審查）+ 1 個 Christ 線下實跑（crash recovery 含 SIGTERM/SIGKILL 兩跑）。

---

## 子項 10：Version bump + 結案文件

### Directory.Build.props

`<Version>3.39.0</Version>`（v4 漸進遷移第五步）

### 結案文件

- Roadmap 實作紀錄章節（Forge 結案第一段填，對齊 Stage 51/52 v2.0 結構）
- CHANGELOG / Future_Feature 同步交給 Aria 結案第二段

---

## 驗收情境

> Stage 53A 是 v4 漸進遷移第五步 macro-orchestration 真正啟動，**驗收必須含 framework-in-framework lifecycle + yield-resume + 53A→53B fallback to legacy 邊界**。沿用 Stage 49/50/51/52 6 場景模式擴充。

### 場景 A：UseFrameworkPipeline = false → Stage 49/50/51/52 + legacy pipeline 行為不變

**怎麼觸發**：
1. push Stage 53A commit → CI/CD 部署
2. Dashboard SystemSettings 確認 `Workflow:UseFrameworkPipeline = false`（預設）
3. 跑 `/mock new_feature_with_proposal` 走完整新功能流程

**怎麼驗證**：
- ✅ pipeline 走 legacy `TaskGroupService.HandleAgentCompletedAsync` 既有路徑（含 Stage 50 framework Kickoff / Stage 52 framework Design 仍依各自 flag 行為）
- ✅ Bot log 沒有 `[Stage53A]` 訊息
- ✅ task_groups.PipelineFrameworkStateJson = null
- ✅ ActiveOrchestration 不會是 "FrameworkPipeline"

### 場景 B：三 flag 都 true + happy path → 完整跑通主路徑

**怎麼觸發**：
1. Dashboard SystemSettings 確認三 flag：`UseFrameworkKickoff = true` + `UseFrameworkDesign = true` + `UseFrameworkPipeline = true`
2. 跑 `/mock framework_pipeline_happy_path`

**怎麼驗證**：
- ✅ Bot log `[Stage53A] HandlePipelineAsync framework path 接管`
- ✅ task_groups.ActiveOrchestration = "FrameworkPipeline"
- ✅ task_groups.PipelineFrameworkStateJson != null（含 PipelineState 序列化）
- ✅ KickoffStage / DesignStage 內 inner FrameworkKickoffRouter / FrameworkDesignRouter 同步 await（內層 KickoffFrameworkStateJson 跑期間並存外層 PipelineFrameworkStateJson）
- ✅ Dev_plan / Dev / Reviewer / QA / Doc 各 Agent 完成後 callback `HandleAgentCompletedAsync` → framework path 觸發 ResumeStreamingAsync 推進下個 stage
- ✅ NotifyMergeStage → call NotifyBossMergeAsync 開 BossInteraction merge_notify type
- ✅ finalize 段清 marker（PipelineFrameworkStateJson + ActiveOrchestration 設 null）

### 場景 C：framework-in-framework lifecycle Crash Recovery（Christ 線下 SIGTERM）⭐

**怎麼觸發**：
1. 三 flag 都 true，跑 `/mock framework_pipeline_kickoff_resume`
2. Kickoff stage 跑期間（4 Agent fan-out 期間）Forge 執行 `docker compose restart aiteam-bot`（**Christ 授權 ops 操作**）

**怎麼驗證**：
- ✅ 重啟前：兩層 framework state JSON 並存（外層 PipelineFrameworkStateJson + 內層 KickoffFrameworkStateJson）
- ✅ Bot 啟動 log `[FrameworkPipelineRouter] 啟動：發現 1 個 stuck framework pipeline`（外層 Recovery 接管）
- ✅ Bot 啟動 log Stage 50 既有 `[FrameworkKickoffRouter]` Recovery 因 F-α 排除條件 PipelineFrameworkStateJson != null **跳過**該 group（雙系統隔離驗證）
- ✅ Recovery 沿用 Stage 49/50/52「降級策略清 marker」拍板（Stage 53A 沒 HITL）
- ✅ 重跑後完整跑通主路徑（依 Mock decision JSON 控制路徑）

### 場景 D：yield-resume 跨 Agent callback 觸發（Christ 線下實跑或 Forge 靜態驗證）⭐

**怎麼觸發**：
1. 三 flag 都 true，跑 `/mock framework_pipeline_dev_resume`
2. Dev stage yield 期間（Dev Agent 跑期間）Forge 執行 `docker compose restart aiteam-bot`

**怎麼驗證**：
- ✅ 重啟前：外層 PipelineFrameworkStateJson != null + ActiveOrchestration = "FrameworkPipeline" + state.CurrentStage = "Dev"
- ✅ 重啟後 Recovery 重觸發 entry（從前置作業重來，沿用降級策略）— 或若 spike F2 揭露 framework 1.3.0 支援 ResumeStreamingAsync 跨 Dev callback resume，則改驗證「不從 entry 重來」
- ✅ Dev Agent 完成後 callback `HandleAgentCompletedAsync` → 觸發 `FrameworkPipelineRouter.ResumeAfterAgentAsync` → 從 latest checkpoint resume + SendResponseAsync(devResult) → ReviewerStage Executor 推進
- ✅ Bot log `[Stage53A] ResumeAfterAgentAsync framework path 觸發 ResumeStreamingAsync`

### 場景 E：QA no_tests routing（C2 整合驗證）

**怎麼觸發**：
1. 三 flag 都 true，跑 `/mock framework_pipeline_qa_no_tests`

**怎麼驗證**：
- ✅ QaStageExecutor 內 call `qaCoordination.HandleQaCompletedAsync(...)` 同步 await
- ✅ QaCoordinationService 既有 no_tests routing 邏輯不動 — 內部 routing 走「approve」分支正常推進 Doc
- ✅ Doc → NotifyBossMerge → BossInteraction merge_notify

### 場景 F：Reviewer 🔴 fallback to legacy（53A→53B 邊界驗證）⭐

**怎麼觸發**：
1. 三 flag 都 true，跑 `/mock framework_pipeline_reviewer_fallback`
2. Reviewer 收到 Critical 評論

**怎麼驗證**：
- ✅ ReviewerStageExecutor 內 call `appealOrchestration.RunPetraGateAsync(...)` 同步 await
- ✅ Petra 閘門判 fail → ReviewerStageExecutor SendMessageAsync(FallbackBridge with reason="reviewer_critical")
- ✅ FrameworkPipelineRouter.FinalizePipelineAsync 走 fallback 分支：清 PipelineFrameworkStateJson + ActiveOrchestration = null + log `[Stage53A-FallbackToLegacy] reviewer_critical`
- ✅ legacy AppealOrchestrationService 接管 fix loop（Reviewer 🔴 → Dev_fix → Reviewer 重審 ... 對齊 Stage 53B 範圍 — 53B 未動由 legacy WorkflowEngine.GetDecision 既有路徑跑）
- ✅ fix loop 完成後（legacy 路徑）pipeline 整體完成（**verify**：legacy WorkflowEngine.GetDecision 走完 Reviewer ✅ → QA → Doc → NotifyBossMerge）

---

## 風險點 / 注意事項

### 1. framework-in-framework lifecycle（高，spike F1 主驗證項）

**風險**：雙 CheckpointStore（Pipeline + Kickoff/Design）+ 雙 ActiveOrchestration marker（"FrameworkPipeline" 外 + "FrameworkKickoff"/"FrameworkDesign" 內）並存 lifecycle 互動未驗。Stage 49-52 都是「節點內部」單層 framework，53A 是首次雙層。

**緩解**：
- spike F1 為 Stage 53A 第一步，不可行 → fallback 兩個獨立 Workflow（規模 +30%）
- F-α 拍板 Recovery 篩選優先級（外層 排除內層）已預先設計

### 2. yield-resume routing 整合（中-高，spike F2 主驗證項）

**風險**：Stage 51 既有 yield-resume 是 RequestPort 模式，Stage 53A 需要 callback 模式 — framework 1.3.0 支援度未驗。

**緩解**：
- spike F2 拍板 yield-resume 路線
- fallback 路線：每個 Agent stage 包 RequestPort + Bridge service 適配層（規模 +20%）
- feature flag 預設 false → 不啟用就 0 影響

### 3. F-α 跨 Stage 修改既有 framework 程式碼（中，首次出現）

**風險**：Stage 49/50/52 既有 framework router 各 +1 行排除條件（純篩選邏輯擴充，不改業務行為），但首次跨 Stage 修改既有 framework 程式碼，Stage 55 收尾時若移除「I2 fallback to legacy」反向設計，這些排除條件要重新評估。

**緩解**：
- commit message 主動揭露為「F-α 配套」+ 理由「避免 4 marker 共存的 Recovery 篩選優先級 collision」
- Stage 53A Roadmap 明寫 trade-off + Stage 55 收尾 checklist

### 4. I2 fallback to legacy 反向設計（中，跟既有拍板相反）

**風險**：Stage 49/50/52 拍板「framework Workflow 跑失敗 → 不 fallback to legacy（避免 session 雙重佔用）」。Stage 53A 因 53B 還沒做必須留 fallback to legacy 路徑（5 個 fallback 點），跟既有設計相反。

**緩解**：
- Roadmap 明寫「I2 fallback 是臨時設計，Stage 55 收尾統一移除」
- 5 個 fallback 點 commit log + DB log 完整紀錄（Forge 自驗 5 場景含 reviewer_fallback 主驗證）
- Stage 55 收尾 checklist 含「移除 5 個 fallback 點 + 拼接 Reviewer 🔴 / Dev 阻礙等 fix loop 路徑到 framework path」

### 5. macro-orchestration 規模 unknown（高，新類型）

**風險**：Stage 49-52 都是「節點內部」混合型 ×0.96-1.25，Stage 53A 是「整個任務調度核心」首次 framework 化，可能突破上界。

**緩解**：
- A2 拆 Stage 53A/53B 守混合型區間精神
- Aria Charter 預估範圍偏寬 500-900K，必拆 session 2-3 段
- Forge Plan Mode 第一步預估 context 超過 280K 時主動跟 Christ 提拆下一 session

### 6. Stage 50 fan-out 拓撲三件套紀律延續（低，已有預警）

對齊 Stage 50 踩坑 #9/#10/#11 給 Stage 53A 三條預警：
- fan-out/fan-in 拓撲 router 一律用 RunStreamingAsync + WatchStreamAsync foreach
- 顯式 SendMessageAsync / YieldOutputAsync 必須三件套（attribute + partial + 註解）
- Stage 52 fix#2 教訓延續 — 各 stage Executor 用 type-explicit 拓撲分流（每個 stage 用獨立 Bridge record type 避免 dispatch 衝突）

### 7. 不踩既有 BossInteraction 邊界（A3 試點精神延續）

**Stage 53A 不動的 production code**：
- ❌ 既有 BossInteraction 10+ type / InteractionService 既有 method / InteractionRespondService / InteractionProcessor
- ❌ DesignerAgentService / WorkflowEngine.cs（GetDecision 邏輯由 framework AddSwitch 自然替代，cs 檔留 Stage 55 收尾移除）
- ❌ AppealOrchestrationService / QaCoordinationService 內部 routing 邏輯（C2 + I2 拍板 — Executor 內 call 既有 method，single SoT）
- ❌ Stage 49/50/51/52 既有 framework path（除了 F-α 排除條件追加）
- ❌ Stage 53B 範圍：fix loop / appeal / QA fix loop / intervention 子流程

**Stage 53A 動的 production code**：
- 動：`src/AiTeam.Bot/Configuration/WorkflowSettings.cs` + `WorkflowSettingsResolver.cs`
- 動：`src/AiTeam.Data/Entities.cs` + Migration `Stage53ATaskGroupPipelineFrameworkState`
- 動：`src/AiTeam.Bot/Orchestration/TaskGroupService.cs`（line 105 之後 + RunNewFeatureWorkflowAsync 入口分流）
- 動：`src/AiTeam.Bot/Orchestration/AgentQueueProcessor.cs`（加 Crash Recovery hook）
- 動：`src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs`（legacy RecoverStuckOrchestrationsAsync 排除條件擴充）
- 動：`src/AiTeam.Bot/Orchestration/Appeal/FrameworkAppealRouter.cs`（F-α 排除條件擴充）
- 動：`src/AiTeam.Bot/Orchestration/Meeting/FrameworkKickoffRouter.cs`（F-α 排除條件擴充）
- 動：`src/AiTeam.Bot/Orchestration/Meeting/FrameworkDesignRouter.cs`（F-α 排除條件擴充）
- 動：`src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor*`（加第五 toggle）
- 動：`src/AiTeam.Bot/Services/MockScenarioService.cs` + `MockClaudeCodeService.cs`（6 個新場景）
- 動：`src/Directory.Build.props`（Version bump）
- 新建：`src/AiTeam.Bot/Workflows/Pipeline/`（資料夾 + PipelineState.cs + PipelineCheckpointStore.cs + PipelineWorkflowFactory.cs + Executors/ 9 檔）
- 新建：`src/AiTeam.Bot/Orchestration/Meeting/FrameworkPipelineRouter.cs`

---

## Model 建議

依 [`workflow_aria_model_effort.md`](../../C:/Users/darkl/.claude/projects/D--Source-Code-AI-Team/memory/workflow_aria_model_effort.md) 四維度：

| 維度 | 評估 |
|---|---|
| **任務複雜度** | **極高** — framework-in-framework 雙層 lifecycle + yield-resume callback 模式 + 5 個 fallback to legacy 點 + macro-orchestration 首次 framework 化 |
| **改動範圍** | **L+** — 新建 1 資料夾（Workflows/Pipeline/）約 12 檔 + 改既有 9-10 檔（含 4 個既有 framework router F-α 修改）+ Migration |
| **歷史包袱** | **高** — Stage 53A 動到整個任務調度核心，Stage 49/50/52 既有 framework router 跨 Stage 修改首次出現；I2 fallback to legacy 反向設計留 Stage 55 移除技術債 |
| **判斷品質要求** | **極高** — v4 漸進遷移最大遷移點之一，影響 Stage 53B / 54 / 55 整體路線設計；framework-in-framework 拓撲設計正確性影響 production 主流程 |

**建議**：**Opus 1M + high**

理由：
1. **混合型 Stage 第 5 個資料點 + macro-orchestration 新類型**（沿用 Stage 49-52 ×0.96-1.25 區間，但 Stage 53A 是 macro 首次可能突破上界，預期偏 mid 帶上半接近 ×1.4）
2. **預估 context 500-900K**（超過 Stage 52 609K），可能拆 session 2-3 段
3. **拆 session 推薦**：
   - Session A：spike + 子項 1-6（feature flag + DB schema + F-α 排除條件 + State + WorkflowFactory + 9 Executor + CheckpointStore）
   - Session B：子項 7（FrameworkPipelineRouter，含 HandlePipelineAsync + ResumeAfterAgentAsync + Recovery + FinalizePipelineAsync + 5 fallback 點）
   - Session C：子項 8-10（入口分流兩處 + Recovery hook + Dashboard + Mock 場景 + Version bump + 結案）
   - 若 spike F1/F2 揭露限制（兩段拓撲 / RequestPort 包裝）→ 規模 +20-30%，可能 Session A/B 拆更細

### Context 預估

依 7 項公式 + 混合型 Stage 校準（macro-orchestration 新類型，預估偏寬）：
- 開場 ~32K
- 工作 raw（新建 12 檔 + 動 9-10 既有檔 + 9 stage Executor 拓撲 + 5 fallback 點）~200-300K
- Grep / Bash 輸出 ~40-60K（讀 Stage 49-52 reference + grep HandleAgentCompletedAsync caller + framework yield-resume docs WebFetch + dotnet build）
- 對話 turn 成本 ~70-120K（spike 第一步 2 項驗證 + Plan Mode + 閘門一兩三輪 + 結案）
- Edit 反覆對齊 ~60-120K（拓撲擴充 + Executor 三件套 + F-α 4 處排除條件對齊 + 5 fallback 點實作對齊）
- Mock 驗收（6 場景）~80-150K
- follow-up 修正 ~50-200K（spike 揭露限制 + framework-in-framework 雙層 lifecycle 整合 unknown + 5 fallback 點邊界）
- 結案文件寫作 ~10-20K
- **總計約 ~540-1000K**（Opus 1M 內 54-100% 負擔，需要拆 session 2-3 段）

→ 拆 session 建議：若 Forge spike + 子項 1-6 結束時 context > 280K，主動跟 Christ 提「拆下一 session 進子項 7+」。

---

## 與 v4 路線的關係

**Stage 53A 是 v4 漸進遷移 8 Stage 的第五步**（議題 A 拆 Stage 後從 7 → 8）：

```
Stage 47 ✅ ops 補丁（v3.34.0）
Stage 48 ✅ spike Phase A（v3.34.0 不變）
Stage 49 ✅ Cody-Vera-Petra Appeal loop 遷移（v3.35.0）
Stage 50 ✅ Kickoff Meeting → Group Chat orchestration（v3.36.0）
Stage 51 ✅ framework HITL pattern 試點（v3.37.0）
Stage 52 ✅ Design Meeting B3 路線（v3.38.0）
   ↓
Stage 53A（本 Stage）：macro pipeline NewFeature 主路徑切 framework Workflow（happy path 限定）（v3.39.0）
   ↓
Stage 53B：fix loop + appeal + QA fix loop + intervention 子流程切 framework + 移除 5 fallback to legacy 點
   ↓
Stage 54：Crash Recovery 全面切 framework Checkpointing（含 4 個 CheckpointStore 抽 base class 評估）
   ↓
Stage 55：收尾 + token middleware + production 切換 + 老 framework code 刪除（含 WorkflowEngine.cs / 5 fallback 點殘留 / framework Executor 從 service 切回直連）+ 真正切既有 BossInteraction 到 framework HITL（Stage 51 試點 know-how 全面 wire）
```

> 註：Stage 53A 完成後 v4 漸進遷移進度 **5/8**。若 Stage 53A 揭露 framework-in-framework lifecycle 重大限制 → 評估是否需要 spike Phase A.6（補做 macro-orchestration 特定模組驗證）。

**Stage 53A 結案後對 Stage 53B 的影響**：
- 5 fallback 點是 53A→53B 邊界紀錄，Stage 53B 動完後逐步移除
- ReviewerStage / DevPlanStage / DevStage / QaStage 的 yield-resume + 同步 Petra 閘門 / QaCoordinationService 整合 pattern 給 Stage 53B fix loop framework 化提供 know-how 基礎

**Stage 53A 對 Stage 55 的鋪路**：
- WorkflowEngine.GetDecision 邏輯由 framework AddSwitch 自然替代，Stage 55 移除 WorkflowEngine.cs
- 4 CheckpointStore class 第 4 次出現相似 pattern，Stage 55 評估抽 base class
- F-α 跨 Stage 修改既有 router 排除條件 + I2 fallback to legacy 反向設計都是 Stage 55 收尾統一處理目標

---

## 實作紀錄

### 子項完成度對照（10/10 ✅）

| # | 子項 | 狀態 | commit |
|---|---|---|---|
| 0 | Spike F1 / F2（讀已存在 framework router code 完成，無需新建 spike 程式片段）| ✅ | plan v1.0/v1.1 內紀錄 |
| 1 | feature flag UseFrameworkPipeline + Resolver | ✅ | 296d44e |
| 2 | DB schema PipelineFrameworkStateJson + Migration `Stage53ATaskGroupPipelineFrameworkState` | ✅ | 296d44e |
| 3 | F-α 4 處 +1 行排除條件（MeetingOrchestrationService / FrameworkAppealRouter / FrameworkKickoffRouter / FrameworkDesignRouter）| ✅ | 296d44e |
| 4 | PipelineState（含 7 Bridge records + 5 stage-distinct request/response types + state helpers）| ✅ | 296d44e |
| 5 | 8 Executor + PipelineWorkflowFactory + DI 註冊（PipelineCheckpointStore / PipelineWorkflowFactory Singleton）| ✅ | b23b760 |
| 6 | PipelineCheckpointStore | ✅ | 296d44e |
| 7 | FrameworkPipelineRouter 4 method（HandlePipelineAsync / ResumeAfterAgentAsync / RecoverStuckFrameworkPipelineAsync ResumeStreamingAsync 議題 12 升級 / FinalizePipelineAsync 5 fallback dispatch 議題 9 修法）+ DI 註冊啟用 | ✅ | 4ec7a35 |
| 8 | 入口分流兩處（FireOneStepAsync line 461 加 Dev_plan 第三條分流 + sub-task ParentGroupId == null 排除 + HandleAgentCompletedAsync 8A line 168 後 callback resume 分流）| ✅ | Session C |
| 9 | AgentQueueProcessor Recovery hook line 85 後 + Dashboard SystemSettings 第五 toggle（三 flag 連動 disabled）+ 6 個 framework_pipeline_* Mock 場景 | ✅ | Session C |
| 10 | Directory.Build.props v3.38.0 → v3.39.0 + Roadmap header v1.0 → v2.0 結案紀錄 | ✅ | Session C |

### Session A/B/C 結案

**Session A（2 commit）**：
- 296d44e — 子項 1/2/3/4/6 完成（揭露議題 G3 假設失誤）
- b23b760 — 子項 5（8 Executor + Factory）

**Session B（1 commit）**：
- 4ec7a35 — 子項 7（FrameworkPipelineRouter 4 method）

**Session C**：本 commit — 子項 8/9/10 + 結案文件

### 關鍵設計決策（跨 Stage 預警價值高的）

1. **Aria 議題 G3 修正方案 C**（2026-05-03 Session A 揭露）：
   - 原 G3「framework-in-framework」假設不成立 — inner FrameworkKickoffRouter.CreateKickoffConfirmationAsync / FrameworkDesignRouter.FinalizeDesignAsync 的 post-meeting actions（Christ confirm BossInteraction / fire Dev_plan / split proposal）跟 Pipeline 主迴圈推進職責衝突
   - **方案 C 拍板**：53A 範圍縮小，Pipeline 主 Workflow 從 Dev_plan 階段啟動（Kickoff/Design 留 legacy；Stage 55 收尾統一整合 Kickoff/Design + sub-task 進 Pipeline framework）— 規模 -40% 守 A2 ×0.96-1.25 區間 + 戰略價值 ~70% 保留
   - **教訓**：framework-in-framework 假設前必須 grep inner router post-meeting actions 完整 caller flow，不能只看 Handle*Async signature

2. **Aria 議題 9 修法（5 fallback 點主動 call legacy）**：
   - 原設計「不主動 call legacy；等下次 callback 落回 legacy 路徑」假設錯誤 — callback 已被 framework path 接管走完，不會二次觸發
   - 修法：FinalizePipelineAsync 主動 call 對應 legacy method（reviewer_critical → FireStepsAsync(Dev, IsFixLoop:true) / dev_plan_failed_escalate → HandleDevPlanCompletedAsync / dev_blocker → HandleDevBlockerAsync / qa_fix_loop → 已由 HandleQaCompletedAsync 內 fire 無需 call / qa_failed/intervention → NotifyBossInterventionAsync / dev_failed/doc_failed → NotifyBoss intervention）
   - **避免遞迴關鍵**：fallback 主動 call legacy 時 PipelineFrameworkStateJson 已 null（Executor ClearMarkerAndFallbackAsync 先清 marker） → HandleAgentCompletedAsync 8A 分流條件失敗 → 走 legacy（不會遞迴回 framework path）

3. **Aria 議題 12 升級 Recovery 採 ResumeStreamingAsync**：
   - Stage 49/50/52 既有「降級策略清 marker 重觸發 entry」對 macro pipeline 不適用 — 重跑會丟失 inner Kickoff/Design 已產出 state（GitHub Issue / DB 寫入 / Christ Discord 確認）+ 重複 LLM token 浪費
   - 修法：沿用 Stage 51 試點 know-how — LoadFromDbAsync + ResumeStreamingAsync rehydrate state（Recovery 階段無 callback signal，不 SendResponseAsync）→ 等下次 Agent callback 自然推進

4. **Aria 議題 3 RequestPort 5 個獨立 PortId + 5 distinct Request/Response 型別**：
   - Stage 52 fix#2 教訓延續 — framework AddEdge type-based dispatch 不 source-aware
   - 5 個 Agent 型 stage 共用同一 record type 會 routing 到全部 5 個 ports（collision）
   - 5 distinct types 自然分流（type-explicit Bridge record 紀律延續）

5. **Aria 時序紀律**：5 fallback 點 Executor 統一 ClearMarkerAndFallbackAsync helper（先 ExecuteUpdateAsync 同步 await 清 marker → 再 SendMessageAsync(PipelineFallbackBridge)）— 避免 Dev_fix / 重產 callback race condition

### 驗收結果（6 場景）

驗收期 Forge 自驗（Christ 給 Forge full 權限 + docker 控制 + mock delay 拉長 60s 自驗 SIGTERM Recovery）：

| # | 場景 | 驗證方式 | 結果 |
|---|---|---|---|
| **A** | UseFrameworkPipeline=false legacy 行為不變 | log query 驗證 4 Recovery hook query `PipelineFrameworkStateJson IS NULL` 排除條件 + UseFrameworkPipeline 預設 fallback false | ✅ |
| **B** | happy_path 完整跑通 | `framework_pipeline_happy_path` Mock：Pipeline 5 stage（DevPlan→Dev→Reviewer→QA→Doc→NotifyMerge）+ J1 yield-resume + 5 PortId routing 全綠；group.Status=done + 1 merge_notify + 1 task/agent | ✅（修 follow-up #1+#2 後二次驗證）|
| **C** | dev_plan_resume Bot restart 自動 Recovery | Mock delay 60s + DevPlanStage yield 後 1 秒 `docker compose restart aiteam-bot` → 自動 Recovery rehydrate + follow-up #4 自動 requeue Dev_plan task → Pipeline 自動跑通至 done | ✅（修 follow-up #4 後二次驗證）|
| **D** | dev_resume Bot restart 自動 Recovery | 同 C 但 timing 在 Dev stage → 自動 Recovery + follow-up #4 自動 requeue Dev task → 自動跑通 | ✅ |
| **E** | qa_no_tests routing（C2 整合）| 程式碼路徑審視（QaStageExecutor 第二 handler call HandleQaCompletedAsync 同步 await + follow-up #1 修法 line 119-135 加 PipelineFrameworkStateJson != null skip fire next 判斷）| ✅ 路徑審視通過（follow-up #3 留 Stage 53B 一併實作 Mock 特殊行為動態驗證）|
| **F** | reviewer_fallback 53A→53B fallback | 程式碼路徑審視（ReviewerStageExecutor 三種 routing：Skipped 放行 / Vera 失敗放行 / 成功觸發 RunPetraGateAsync 同步 await → fail → fallback reviewer_critical → FinalizePipelineAsync FireStepsAsync(Dev, IsFixLoop:true)）| ✅ 路徑審視通過（同 E）|

### 驗收後修正（4 條 follow-up）

#### follow-up #1（commit 7a100e7）：QaCoordinationService Pipeline path skip fire next（議題 G3 同類問題在 QA 重演）

**問題**：場景 B 揭露 Doc task 被 enqueue 2 次 → 第 2 次 callback 走 legacy 開第 2 個 merge_notify。

**根因**：`QaCoordinationService.HandleQaCompletedAsync` line 93-108 happy path（status=passed）內 `tgs.FireStepsAsync(decision.NextSteps)` 自動 fire Doc。同時 Pipeline QaStageExecutor 第二 handler `SendMessageAsync(DocStageBridge)` → DocStageExecutor 又 fire Doc → race。

**根因屬性**：**議題 G3 同類問題在 QA 重演** — Aria 規劃前期 grep 不夠深（既有方案 C 拍板含 inner FrameworkKickoffRouter / FrameworkDesignRouter 衝突，但漏 grep QaCoordinationService.HandleQaCompletedAsync passed 路徑內部 fire 行為）。

**修法**：HandleQaCompletedAsync passed + no_applicable_tests approve 兩處加 `if (group.PipelineFrameworkStateJson != null) → log + return` 跳過 legacy GetDecision/FireStepsAsync/MarkDone（由 Pipeline NotifyMergeStageExecutor 接管）。

#### follow-up #2（commit 7a100e7）：Pipeline NotifyMergeStage 補 MarkGroupDoneOrInterventionAsync

**問題**：原 NotifyMergeStageExecutor 只 call NotifyBossMergeAsync（發 Discord embed + 開 BossInteraction），沒 call MarkGroupDoneOrInterventionAsync。場景 B group.Status=done 是靠 Bug #1 第二次 legacy fall through side effect 完成的。修 #1 後 group.Status 不會 mark Done → 必須補上。

**修法**：NotifyMergeStageExecutor.HandleAsync 改為「MarkGroupDoneOrInterventionAsync → if Status=Done call NotifyBossMergeAsync else NotifyBossInterventionAsync」對齊 legacy QaCoordinationService L99-103 寫法。

#### follow-up #4（commit dc5ff37）：Pipeline Recovery 接管 Bot restart 邊界 failed Agent task requeue

**問題**：場景 C 自驗 docker compose restart aiteam-bot 在 Pipeline yield 期間 → Doc task 跑到一半被 OperationCanceledException 標 `Status="failed"` + Step="The operation was canceled." → AgentQueueProcessor.RecoverStuckTasksAsync 篩選只 requeue `QueueStatus="processing"` → failed task 不被處理 → Pipeline 永遠卡在對應 PortId 等永遠不來的 Agent callback → group.Status 永遠卡在 running + PipelineFrameworkStateJson 永遠 set。

**根因屬性**：**Pipeline framework + AgentQueueService 整合在 Bot restart 邊界 unknown** — Aria 議題 12 升級 Recovery ResumeStreamingAsync rehydrate 已驗 framework state 層完整，但漏掉 Agent task 層 failed→requeue 整合。

**修法**：FrameworkPipelineRouter.RecoverStuckFrameworkPipelineAsync 內 rehydrate 完成偵測 pending PortId 後，依 PortId → AgentName mapping（5 個獨立 PortId 對應 Dev_plan/Dev/Reviewer/QA/Doc）→ 找該 group 內對應 AssignedAgent + Status=failed task → ExecuteUpdateAsync 設 Status=queued/QueueStatus=queued → AgentQueueProcessor 主迴圈自然 pick up 重跑 → 跑完 callback 觸發 ResumeAfterAgentAsync 推進 Pipeline。

新 helper：`RequeueFailedAgentTaskAsync(groupId, agentName, ct)` — Pipeline 自己接管整體 Recovery 完整性（不只 framework state rehydrate，也補 Agent task requeue）。

#### follow-up #3 留 Stage 53B 範圍

`framework_pipeline_qa_no_tests` / `framework_pipeline_reviewer_fallback` 場景 Mock 特殊行為（QA 回 Status=no_applicable_tests / Vera 回 CriticalReviewCount > 0）— MockClaudeCodeService 沒對應 if 分支，預設走 default happy path 不能 dynamic 驗證。Christ 拍板 2 同意延 Stage 53B 一併做（fix loop / appeal / QA fix loop / intervention 子流程一起實作 Mock 特殊行為更乾淨）。

### Mock 覆蓋情況

| 6 場景 | 動態驗證 | 靜態路徑審視 |
|---|---|---|
| A legacy 不變 | ✅ | — |
| B happy_path | ✅（含 follow-up #1+#2 二次驗證）| — |
| C dev_plan_resume | ✅（含 follow-up #4 二次驗證）| — |
| D dev_resume | ✅（含 follow-up #4 mapping 驗）| — |
| E qa_no_tests | ⏸️（Stage 53B follow-up #3）| ✅ |
| F reviewer_fallback | ⏸️（Stage 53B follow-up #3）| ✅ |

**4 場景 dynamic 驗證 + 2 場景靜態審視通過**（超出 plan 預期 5 靜態 + 1 線下 SIGTERM 慣例）。Forge 用 docker compose restart aiteam-bot 直接驗 Recovery（Christ 拍板 Stage 49 放寬 docker process restart 給 Forge）。

### 踩坑紀錄彙整

#### 議題 G3 假設失誤揭露（戰略級，跨 Stage 53B/55 預警）
Forge Session A 子項 5 實作期 grep 真實 inner code 揭露 inner router post-meeting actions 跟 Pipeline 衝突。**Aria 規劃前期 grep 不夠深**（只看 Handle*Async signature 沒看 finalize 段）。Aria 結案第二段已加自省點 — Forge Plan Mode 第一步建議對「inner 同步 await」假設先 grep inner finalize 段確認無 post-meeting side effects。

#### TaskStatus 模糊參考（Stage 53A Session B/C 共 4 處踩到）
`AiTeam.Shared.Constants.TaskStatus` vs `System.Threading.Tasks.TaskStatus` 衝突 — using 兩個 namespace 同時時編譯誤判。修法：specific case 直接用 fully qualified `AiTeam.Shared.Constants.TaskStatus.NeedsIntervention`。Stage 53B+ 預警：寫新 Pipeline / Workflow code 用到 TaskStatus 時主動 fully qualified。

#### InProcessExecution.RunStreamingAsync 5-arg signature
Forge Session B 第一次 build 報 `cannot convert from 'CheckpointManager' to 'string?'` — 實際 signature `(workflow, initialState, manager, sessionId, ct)` 含 sessionId（不是 4 args）。對齊 FrameworkKickoffRouter L409 / FrameworkDesignRouter L382 既有 pattern。

#### 議題 G3 同類問題在 QA 重演（驗收期戰略級揭露 — follow-up #1）
Aria 議題 G3 Session A 修正方案 C 時只 grep inner FrameworkKickoffRouter / FrameworkDesignRouter 的 post-meeting actions，**漏 grep `QaCoordinationService.HandleQaCompletedAsync` passed 路徑內部 `tgs.FireStepsAsync(decision.NextSteps)` 自動 fire Doc** — 場景 B 驗收揭露 Doc 重複 enqueue 2 次 race。**Stage 53B+ 預警**：framework Workflow 同步 await call 既有 service method 時，必須 grep 該 method 內部所有 fire next/MarkDone/NotifyBoss side effects，每處都需 `if (group.PipelineFrameworkStateJson != null) skip` 判斷。Pipeline 主迴圈職責 = 完整接管推進 + mark Done + Notify。

#### Pipeline NotifyMergeStage 沒 mark group.Status=Done（驗收期 follow-up #2）
原 NotifyMergeStageExecutor 只 call NotifyBossMergeAsync（發 Discord embed + 開 BossInteraction），沒 call MarkGroupDoneOrInterventionAsync。場景 B 第一次驗收 group.Status=done 是靠 follow-up #1 race 第二次 legacy fall through side effect 完成 — 修 follow-up #1 後 group.Status 永遠卡 running。修法：對齊 legacy QaCoordinationService L99-103 寫法（MarkDone → if Status=Done NotifyBossMerge else NotifyBossIntervention）。**Stage 53B+ 預警**：Pipeline 終結 Executor（NotifyMergeStage / Fallback）必須完整對齊 legacy「MarkDone + NotifyBoss」整合慣例，不能只做一半。

#### Bot restart 邊界 Agent task failed→requeue（驗收期 follow-up #4）
Pipeline framework + AgentQueueService 在 Bot restart 邊界踩雷：`OperationCanceledException` 標 task `Status="failed"` 不是「跑到一半中斷」，AgentQueueProcessor.RecoverStuckTasksAsync 只 requeue `QueueStatus="processing"` → failed task 不被處理 → Pipeline 卡在 PortId 等永遠不來 callback。修法：Pipeline Recovery 自己接管 Agent task requeue（5 PortId → AgentName mapping helper）。**Stage 53B+ 預警**：framework Workflow 內 enqueue legacy AgentQueueService 整合 idempotency 風險（Aria 提醒 #3 議題 15 警告的反面）— 不只重複 enqueue 風險，也有 failed→requeue 缺口。

#### Mock delay AppSettings cache TTL 邊界（驗收期工具坑）
場景 C/D 自驗時設 `Mock:DelayMinMs=60000` 想拉長 mock delay 給 SIGTERM timing 充裕，但 `/internal/cache/invalidate?scope=agents` reload 後 5 stage 仍在 ~10 秒跑完（cache 未生效或部分生效）。最終靠**精準 timing**（grep DevPlanStage yield log 後 1 秒 docker restart）解決。**Stage 53B+ 預警**：AppSettings cache 動態調整可能有延遲 — 自驗時準備好「精準 timing 等 log 觸發」備案，不要全靠 mock delay 拉長。

### Aria 校準錨候選（Aria 第二段填）

> Forge 預估 ×（待 Aria 校準）— 本次特殊複合：
> 1. **Session A 子項 5 實作期揭露 Aria 規劃前期假設失誤（議題 G3）**+ 即時跨 session 拍板修正方案 C，是混合型 Stage 首次出現「規劃 → 實作 → Aria 拍板修正 → 範圍縮小 -40%」流程
> 2. **驗收期 4 follow-up（戰略級含 #1 議題 G3 同類問題在 QA 重演 + #4 Bot restart 邊界 Pipeline 自接 Recovery 完整性）** — 揭露「framework Workflow 同步 await call 既有 service method」整合 idempotency 議題的兩個維度（fire next 衝突 / failed→requeue 缺口）
> 3. **Forge 自驗超出 plan 預期**（4 dynamic + 2 靜態 vs plan 5 靜態 + 1 線下慣例）— Christ 給 docker compose restart 權限 + 60s mock delay 拉長自驗 SIGTERM Recovery，**首次 Forge 自己跑通 Crash Recovery 完整循環**

3 session 連跑 + 4 follow-up commit + 6 場景驗收 + Build 0 Error。

### Christ 拍板紀錄（forge-end 結案時）

- **拍板 1（production 保留）**：v3.39.0 上線狀態 `Workflow:UseFrameworkPipeline = true` 留著（Pipeline framework path 全自動恢復機制已驗）+ Mock delay 還原 1000-3000ms 預設值。Christ 真實任務 NewFeature 主路徑會走 Pipeline framework path
- **拍板 2（Stage 53B 延 + Aria 補 caveat）**：follow-up #3 場景 E/F Mock 特殊行為動態驗證留 Stage 53B 一併實作（fix loop / appeal / QA fix loop / intervention 子流程一起做更乾淨）— Aria 結案第二段補 caveat 紀錄
- **拍板 3（Aria 處理 CHANGELOG / Future_Feature）**：v3.39.0 CHANGELOG entry + Future_Feature 同步交 Aria 結案第二段

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-03 | 初版規劃書建立（Aria）—— v4 漸進遷移第五步 Stage 53A：macro pipeline NewFeature 主路徑切 framework Workflow（happy path 限定）（A2 拆 Stage 53A/53B + B 拆 53B + C2 QaStage Executor 內 call 既有 service + D2 兩項 spike + E1 三 flag 連動 + F-α 層級隔離跨 Stage 修改既有 router + G3 framework-in-framework + H-mid 6 Mock 場景 + I2 + 5 fallback to legacy + J1 混合 lifecycle yield-resume）|
| v2.0 | 2026-05-03 | Forge 結案第一段（3 session 連跑：Session A 296d44e + b23b760 / Session B 4ec7a35 / Session C 含本 commit）。**議題 G3 修正方案 C 拍板**：53A 範圍縮小到 5 Agent stage（DevPlan/Dev/Reviewer/QA/Doc），Kickoff/Design 留 legacy（Stage 55 收尾整合）；規模 -40% 守 A2 ×0.96-1.25 區間 + 戰略價值 ~70% 保留。**Pipeline 主入口分流位置改 FireOneStepAsync line 461 加 Dev_plan 第三條 single point of entry**（Forge 觀察 6 處 fire Dev_plan 散點全經過 FireOneStepAsync 統一節流）+ sub-task ParentGroupId == null 排除（Stage 46 機制 Stage 55 收尾整合）+ Aria 時序紀律（fallback 5 點先清 marker 再 SendMessage 避免 Dev_fix race）+ 議題 12 升級 ResumeStreamingAsync rehydrate（不採降級重跑） + 議題 9 修法（5 fallback 點主動 call legacy method 接管）+ 議題 3 RequestPort 5 獨立 PortId + 5 distinct Request/Response 型別。Build 0 Error / Migration scaffold 完成（Bot 啟動 MigrateAsync 自動套用）。|
| v2.1 | 2026-05-03 | Forge 結案第一段含驗收期紀錄。**驗收期 4 follow-up commit**：① #1 7a100e7 — QaCoordinationService Pipeline path skip fire next（**議題 G3 同類問題在 QA 重演** — Aria 規劃前期 grep 不夠深，方案 C 修正只覆蓋 Kickoff/Design 漏 QaCoordination passed 路徑 fire Doc 衝突）② #2 7a100e7 — NotifyMergeStage 補 MarkGroupDoneOrInterventionAsync（修 #1 後 group.Status 不會 mark Done 必須補）③ #3 留 Stage 53B（Mock 場景 E/F 特殊行為跟 fix loop / appeal 子流程一起實作）④ #4 dc5ff37 — Pipeline Recovery 接管 Bot restart 邊界 failed Agent task requeue（**Pipeline framework + AgentQueueService 整合 unknown** — Bot restart OperationCanceledException 標 task failed，AgentQueueProcessor.RecoverStuckTasksAsync 只 requeue processing 不 requeue failed → Pipeline 卡 PortId 等永遠不來 callback；修法 5 PortId → AgentName mapping helper requeue）。**6 場景驗收**：A/B/C/D 4 場景 dynamic 驗證（Christ 給 docker restart 權限 + 60s mock delay 拉長自驗 SIGTERM Recovery，**首次 Forge 自跑 Crash Recovery 完整循環**）+ E/F 2 場景靜態路徑審視通過。**Christ 拍板**：① production 保留 UseFrameworkPipeline=true ② Stage 53B 延後處理 follow-up #3 + Aria 補 caveat ③ CHANGELOG / Future_Feature 同步交 Aria 第二段。|
