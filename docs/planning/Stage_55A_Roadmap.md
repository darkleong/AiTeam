# Stage 55A：v4 漸進遷移第八步（拆 55A/55B 第一段）— Kickoff/Design 整合到 Pipeline + sub-task 整合 + 移除 6 hooks + 刪 WorkflowEngine.cs

> 對應 Future Feature：v4 漸進遷移 9 Stage 路線第八步（議題 A 拆 Stage 後 Stage 55 進一步拆 55A/55B，v4 路線 8→9 Stage）
> 對應版本：**v3.42.0**（v4 漸進遷移第八個產生版本變動的 Stage）
> 建立日期：2026-05-04
> 狀態：📋 計劃書建立完成，待 Forge 開工
> 文件版本：v1.0

---

## 概述

**戰略背景**：[Stage 49-54](Stage_54_Roadmap.md) 完成 v4 漸進遷移前七步 — **NewFeature 主路徑 + 子流程完整 Pipeline framework 化**達成（Stage 53A/B）+ Crash Recovery 機制全切 + 重構性債務清完（Stage 54）。**Stage 55A 是 v4 漸進遷移收尾第一段**：把 Stage 53A/53B/54 留下的 4 件債務一次清完 — Pipeline framework 從 Kickoff 階段啟動（議題 G3 真正解決）+ sub-task 整合（Stage 46 機制接 Pipeline）+ 移除 J1 既有 6 hooks legacy safety net + 刪除 WorkflowEngine.cs。

**Stage 55 拆 Stage 55A/55B 戰略決策**（沿用 Stage 53A/53B 拆 Stage 戰術成功經驗）：5 件子工作中子工作 3（BossInteraction 切 framework HITL，27 處 caller refactor）規模 vs 其他 4 件合計 ≈ 7:3 — 拆點自然。一次做完 5 件預估 ×1.4-1.7 衝混合型 ×1.4 上界，拆兩 Stage 各落區間中段。**v4 路線 8→9 Stage**。

**Stage 55A 戰略價值**：解決 Stage 53A 議題 G3 假設失誤（inner FrameworkKickoffRouter / FrameworkDesignRouter 的 post-meeting actions 跟 Pipeline 主迴圈推進職責衝突）— 方案 C「Pipeline 從 Dev_plan 階段啟動 + Kickoff/Design 留 legacy」是臨時設計，Stage 55A 真正解決：**inner router 瘦身為「純會議跑通」+ post-meeting actions 由 Pipeline KickoffStage / DesignStage Executor 接管**。

### 既定 TODO（Stage 53A/53B/54 留下，Stage 55A 統一收尾）

4 件 Stage 53A/53B/54 程式碼註解明寫的 TODO：

| TODO | 來源 | 拍板紀錄 |
|---|---|---|
| Pipeline 從 Kickoff 階段啟動（議題 G3 真正解決）| Stage 53A 方案 C 拍板 | 「53A 範圍縮小到 Pipeline 從 Dev_plan 啟動，Kickoff/Design 留 legacy；Stage 55 收尾統一整合 Kickoff/Design」 |
| sub-task 整合到 Pipeline framework | Stage 53A 議題 E 拍板 | 「sub-task 排除（FireOneStepAsync 分流條件保留 ParentGroupId == null），守 Stage 55 收尾統一整合 Kickoff/Design + sub-task 三戰略級工作精神」 |
| 移除 J1 既有 6 hooks legacy safety net | Stage 53B J1 拍板 | 「J1：既有 6 hooks 保留作為 legacy fallback safety net — feature flag false 時走 legacy 6 hooks 機制完整可用」 |
| 刪除 WorkflowEngine.cs | Stage 53A 議題 E 拍板 | 「不動 DesignerAgentService / WorkflowEngine.cs（GetDecision 邏輯由 framework AddSwitch 自然替代，但 cs 檔暫不刪 — Stage 55 收尾移除）」 |

→ **Stage 55A = 一次清完 4 件 Stage 53A/53B/54 留的債務**，讓 v4 漸進遷移真正進入收尾階段。

### Stage 55A 同時做 4 件對齊性工作

1. **Kickoff/Design 整合到 Pipeline framework**（議題 G3 真正解決，inner router 瘦身 + Pipeline Stage Executor 接管 finalize 段 actions）
2. **sub-task 整合到 Pipeline framework**（FireOneStepAsync 排除條件 ParentGroupId == null 移除 + Pipeline 支援 sub-task TaskGroup）
3. **移除 J1 既有 6 hooks legacy safety net**（HandleAgentCompletedAsync line 188-273 共 6 hooks 純刪除）
4. **刪除 WorkflowEngine.cs**（173 行 + 4 處 caller 移除）

### 範圍邊界

- ✅ **Pipeline 從 Kickoff 階段啟動**（FireOneStepAsync line 486 entry guard AgentName 從 `Dev_plan` 改 `Kickoff`，5 條件 entry guard 不變）
- ✅ **Pipeline 主 Workflow 加 KickoffStage / DesignStage Executor**（對齊 Stage 53A 既有 stage Executor pattern + DevFixStageExecutor 命名慣例）
- ✅ **inner FrameworkKickoffRouter / FrameworkDesignRouter 瘦身**：`HandleKickoffMeetingAsync` / `HandleDesignMeetingAsync` 不含 finalize 段 actions（CreateKickoffConfirmationAsync / FinalizeDesignAsync 移到 Pipeline Stage Executor 內接管）
- ✅ **MeetingOrchestrationService.HandleKickoffConfirmedAsync / HandleDesignConfirmedAsync 內 fire next stage 邏輯移除**（由 Pipeline 主迴圈接管）
- ✅ **sub-task 整合**：FireOneStepAsync line 484 排除條件 `group.ParentGroupId == null` 移除 + Stage 46 既有 BuildEpicSubTasksAsync + Sequential 鏈與 Pipeline FinalizePipelineAsync 對齊
- ✅ **HandleAgentCompletedAsync 6 hooks 移除**（Dev_plan / Reviewer / Dev[BLOCKED] / 仲裁後 Dev_fix / QA 修復 / Dev fail intervention）
- ✅ **WorkflowEngine.cs 刪除** + 4 處 caller 移除（TaskGroupService:282/290 + QaCoordinationService 3 處）
- ✅ **F-α 排除條件評估**：Stage 53A 加的「PipelineFrameworkStateJson == null」排除條件保留（55B 後評估移除）

- ❌ **不動**：既有 BossInteraction 10+ type / InteractionService 既有 method 結構（55B 範圍 — BossInteraction 切 framework HITL pattern 27 處 caller refactor）
- ❌ **不動**：FrameworkHitlBridge / `framework_kickoff_mid_interrupt` type（Stage 51 試點，留 55B 推廣）
- ❌ **不動**：Stage 49-54 既有 framework path 主邏輯（除 Pipeline 主 Workflow 拓撲擴展含 Kickoff/Design Stage）
- ❌ **不動**：AppealOrchestrationService 16 處 skip 邏輯（55A 移除 6 hooks 後 AppealOrchestrationService 仍是 Pipeline 唯一 caller — Pipeline DevPlanStage / DevStage 內 call HandleDevPlanCompletedAsync / HandleDevBlockerAsync — 16 處 skip 邏輯保留作為 Pipeline path 的安全網；55B 切 HITL 後可一併評估精簡）

### v4 路線第八步風險預警

- **議題 G3 解法跨層 refactor 規模中-高**：inner router 瘦身 + Pipeline Stage Executor 接管 finalize actions + HandleKickoffConfirmedAsync / HandleDesignConfirmedAsync fire next stage 邏輯移除 — 三處改動需要對齊
- **sub-task 整合 Pipeline 跨 group 鏈拓撲設計**：每個 sub-task 一個獨立 Pipeline 實例（沿用 Stage 46 既有「各 phase 獨立 PR」業務語義）+ Stage 46 既有 Sequential 鏈推進機制與 Pipeline FinalizePipelineAsync 對齊
- **6 hooks 移除影響面**：移除後 Pipeline 必須完全涵蓋全 NewFeature 主路徑 + 子流程（Stage 53A/53B 已驗），但 legacy fallback safety net 也消失 — feature flag UseFrameworkPipeline=true 為唯一 production path
- **WorkflowEngine.cs 刪除影響面低**：QaCoordinationService 3 處 GetDecision call 已被 QaStage Executor 接管 routing（Stage 53B follow-up），可直接移除 caller

→ feature flag UseFrameworkPipeline 已 production 啟用（Christ 2026-05-03 拍板保留 true），Stage 55A 不引入新 flag。

---

## 設計決策（Christ 2026-05-04 拍板）

### 主路線拍板（戰略級）

| 議題 | 拍板 | 替代方案 |
|---|---|---|
| **議題 A：Stage 55 範圍** | **A1：拆 Stage 55A/55B**（沿用 Stage 53A/53B 拆 Stage 戰術成功經驗）— 子工作 3（BossInteraction 切 HITL）規模 vs 其他 4 件合計 7:3 拆點自然 + 守混合型 ×0.73-1.25 區間精神 + 一次做完 5 件預估 ×1.4-1.7 衝上界 | A2 一次做完 5 件 / A3 拆三段（過度拆）|
| **議題 B：55A 範圍** | **B1：子工作 1+2+4+5**（Kickoff/Design 整合 + sub-task 整合 + 移除 6 hooks + 刪 WorkflowEngine.cs）— 子工作 1 為核心戰略級 + 子工作 2/4/5 性質契合（純整合 + 移除 legacy） | 其他組合 |

### Aria 拿捏（已決，純內部實作不對外議題）

| # | 議題 | 決定 |
|---|---|---|
| 1 | Pipeline 從 Kickoff 啟動的入口分流 | FireOneStepAsync line 478-502 既有 entry guard 5 條件不變，AgentName 從 `Dev_plan` 改 `Kickoff`（proposal_approved → ProposalConfirmationService.ProcessProposalApprovedAsync line 191 fire Kickoff step → FireOneStepAsync 分流到 Pipeline framework path） |
| 2 | 議題 G3 解法（inner router 瘦身 + Pipeline 接管 finalize） | inner FrameworkKickoffRouter / FrameworkDesignRouter 提供新 method `HandleKickoffMeetingAsync` / `HandleDesignMeetingAsync` 純會議跑通（不含 finalize 段 actions）— Pipeline KickoffStage / DesignStage Executor 內 call 此 method 同步 await + 跑完後 Pipeline 自己 call **新搬過來的 finalize actions**：① CreateKickoffConfirmationAsync 開 kickoff BossInteraction（沿用既有 type，不切 HITL）② FinalizeDesignAsync 拆 task 提案評估 + 開 design BossInteraction ③ MeetingOrchestrationService.HandleKickoffConfirmedAsync / HandleDesignConfirmedAsync 內既有 `tgs.FireStepsAsync(Design)` / `tgs.FireStepsAsync(Dev_plan)` 邏輯**移除**（由 Pipeline 主迴圈接管） |
| 3 | KickoffStage / DesignStage Executor 設計 | 對齊 Stage 53A/53B 既有 stage Executor pattern — KickoffStage Executor：dual handler（HandleEntryAsync 觸發 inner router 跑會議 + 開 BossInteraction → yield；HandleResponseAsync 接 Christ 回應 → SendMessage DesignStageBridge / FallbackBridge）；DesignStage Executor 同樣 pattern |
| 4 | sub-task 整合方式 | **每個 sub-task 一個獨立 Pipeline 實例**（parent group → 子 group 鏈，每個子 group 跑獨立 Pipeline）— 沿用 Stage 46 既有「各 phase 獨立 PR」業務語義；Pipeline framework 不感知 sub-task 概念，sub-task 推進機制由 Stage 46 既有 BuildEpicSubTasksAsync + Sequential 鏈邏輯處理（Pipeline FinalizePipelineAsync 完成 sub-task → Stage 46 邏輯 fire 下個 phase 子 group → 該子 group 從 proposal_approved 進 Pipeline framework path） |
| 5 | HITL 保留既有機制（55A 不切） | 55A 仍用既有 BossInteraction kickoff/design type — Pipeline KickoffStage Executor 同步 await 等 inner Workflow 跑完 + 自己開 BossInteraction（Stage 54 已加 idempotency check sticks）+ yield 等既有 InteractionProcessor 輪詢觸發 ProcessBossResponseAsync → Pipeline KickoffStage HandleResponseAsync resume；**新加 ResponseAction** "kickoff_continue" / "design_continue" 路由到 Pipeline KickoffStage / DesignStage Executor callback resume（對齊 Stage 53A J1 yield-resume 機制） |
| 6 | F-α 排除條件評估 | Stage 53A F-α 加的「PipelineFrameworkStateJson == null」排除條件**保留**（55B 後評估移除 — 55A 還有 sub-task TaskGroup 可能跨 framework path，保留排除條件較安全）|
| 7 | AppealOrchestrationService 16 處 skip | 55A 移除 6 hooks 後 AppealOrchestrationService 仍是 Pipeline 唯一 caller（Pipeline DevPlanStage / DevStage 內 call）— **16 處 skip 邏輯保留**作為 Pipeline path 的安全網；55B 切 HITL 後再評估精簡 |
| 8 | QaCoordinationService 3 處 GetDecision call 處理 | QaStage Executor 已接管 routing — 直接移除 3 處 GetDecision call（QaStage Executor 內部 routing 邏輯不變）+ QaCoordinationService 對應 method 同步精簡（如 HandleQaCompletedAsync 內邏輯由 QaStage Executor 接管，可整段移除或保留作為 reference） |
| 9 | DB schema | 不加新欄位 — 沿用既有 |
| 10 | 入口分流位置 | 沿用 Stage 53A 既有兩處（FireOneStepAsync line 486 entry guard / HandleAgentCompletedAsync line 173 callback resume 分流）— FireOneStepAsync entry guard AgentName 從 `Dev_plan` 改 `Kickoff` |
| 11 | sub-task 推進機制 | Stage 46 既有 BuildEpicSubTasksAsync + Sequential 鏈不變 — Pipeline 接管後每個 sub-task TaskGroup 跑獨立 Pipeline（從 Kickoff 階段啟動）+ 完成後 fire 下個 phase 機制由 Stage 46 既有邏輯處理（Pipeline FinalizePipelineAsync 完成 → Stage 46 EpicChain 機制觸發下個 phase）|
| 12 | Mock 場景觸發機制 | 對齊 Stage 49-54 `MockClaudeCodeService.FailScenario` static 傳遞 scenario key 慣例 + Stage 53B `/internal/mock/scenario` HTTP API + Stage 54 MockMode auto-approve BossInteraction（含 kickoff/design 兩 type） |
| 13 | Token 計費 | 沿用既有機制（Stage 55A 不引入新 LLM call） |
| 14 | CLAUDE_*.md prompt | 不動（沿用 Stage 49-54 慣例，純內部整合不影響 Agent prompt） |
| 15 | base class 沿用 Stage 54 | KickoffStage / DesignStage 不需新 CheckpointStore（沿用既有 PipelineCheckpointStore + Stage 54 抽出的 FrameworkCheckpointStoreBase） |

### Stage 55A 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **0** | **Spike 第一步：read 對齊範圍**（Forge Plan Mode 第一步）— read inner FrameworkKickoffRouter line 660-750 CreateKickoffConfirmationAsync + FrameworkDesignRouter line 430-575 FinalizeDesignAsync 完整 finalize 段 actions + MeetingOrchestrationService line 627-880 HandleKickoffConfirmedAsync / HandleDesignConfirmedAsync 既有 fire next stage 邏輯 + Stage 46 BuildEpicSubTasksAsync + EpicChain 機制 + ProposalConfirmationService.ProcessProposalApprovedAsync 入口 | XS |
| **1** | inner FrameworkKickoffRouter / FrameworkDesignRouter 瘦身 — 抽出 `HandleKickoffMeetingAsync` / `HandleDesignMeetingAsync` 純會議跑通 method（不含 finalize 段 actions）；既有 entry method 保留作為 legacy path（feature flag false 時走） | M |
| **2** | Pipeline 主 Workflow 加 KickoffStage / DesignStage Executor + 拓撲擴展（5→7 RequestPort + 8→10 stage Executor + 對應 Bridge record 5→7） | M |
| **3** | Pipeline KickoffStage / DesignStage Executor 接管 finalize 段 actions（從 inner router 搬過來）：CreateKickoffConfirmationAsync 開 kickoff BossInteraction + FinalizeDesignAsync 拆 task 提案 + design BossInteraction + 既有 idempotency check 沿用（Stage 54 已加） | M-L |
| **4** | MeetingOrchestrationService.HandleKickoffConfirmedAsync / HandleDesignConfirmedAsync 內 fire next stage 邏輯移除（既有 `tgs.FireStepsAsync(Design)` / `tgs.FireStepsAsync(Dev_plan)`）— 由 Pipeline 主迴圈接管；button id route 改為 KickoffStage / DesignStage Executor callback resume 入口 | S |
| **5** | FireOneStepAsync entry guard AgentName 從 `Dev_plan` 改 `Kickoff` + sub-task 排除條件 `group.ParentGroupId == null` 移除（5 條件變 4 條件，sub-task 也走 Pipeline framework path） | XS |
| **6** | sub-task 整合（Pipeline 接管 sub-task TaskGroup 跑獨立 Pipeline + Stage 46 既有 BuildEpicSubTasksAsync + EpicChain Sequential 鏈與 Pipeline FinalizePipelineAsync 對齊） | M |
| **7** | HandleAgentCompletedAsync 6 hooks 移除（line 188-273 全段刪除 — Pipeline 已涵蓋全路徑）+ 對應 helper 內部精簡（如 NotifyBossDevFailedInterventionAsync 等仍保留供 Pipeline call）| S |
| **8** | WorkflowEngine.cs 刪除 + 4 處 caller 移除（TaskGroupService:282/290 落底 GetDecision 段 + QaCoordinationService 3 處 GetDecision call）| S |
| **9** | Mock 場景擴充（NewFeature 主路徑從 Kickoff 階段啟動驗證 + sub-task 整合驗證 + 6 hooks 移除 regression）| M |
| **10** | Version bump v3.41.0 → v3.42.0 + 結案文件（Roadmap 實作紀錄章節 + CHANGELOG / Future_Feature 同步交給 Aria 結案第二段）| XS |

> **不寫工時估算**（workflow_aria.md 第三節 A 第 4 條）— 各子項規模見上表 XS/S/M/L 範圍描述。

---

## 子項 0：Spike 第一步 — read 對齊範圍

### read 對齊範本

| # | read 對象 | 目的 |
|---|---|---|
| F1 | `src/AiTeam.Bot/Orchestration/Meeting/FrameworkKickoffRouter.cs` line 660-750 CreateKickoffConfirmationAsync 完整內容 | finalize 段 actions 細節（Discord embed + buttons + RegisterKickoffConfirmation + CreateInteractionAsync + Stage 54 idempotency check）— Pipeline KickoffStage Executor 接管的範本 |
| F2 | `src/AiTeam.Bot/Orchestration/Meeting/FrameworkDesignRouter.cs` line 430-575 FinalizeDesignAsync 完整內容 | finalize 段 actions（含拆 task 提案評估 + design BossInteraction + Stage 54 idempotency check）— Pipeline DesignStage Executor 接管的範本 |
| F3 | `src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs` line 627-880 HandleKickoffConfirmedAsync / HandleDesignConfirmedAsync 完整內容 | 既有 fire next stage 邏輯（`tgs.FireStepsAsync(Design)` / `tgs.FireStepsAsync(Dev_plan)`）位置 + button id route 結構 — Stage 55A 移除這段 + 由 Pipeline 接管 |
| F4 | `src/AiTeam.Bot/Orchestration/TaskGroupService.cs` line 188-273 HandleAgentCompletedAsync 6 hooks 完整結構 | 6 hooks 移除範圍 + 對應 helper 是否仍被 Pipeline call（如 NotifyBossDevFailedInterventionAsync） |
| F5 | `src/AiTeam.Bot/Orchestration/TaskGroupService.cs` line 1300-1500 BuildEpicSubTasksAsync + EpicChain 機制（Stage 46） | sub-task 推進機制與 Pipeline FinalizePipelineAsync 對齊位置 |
| F6 | `src/AiTeam.Bot/Orchestration/Proposal/ProposalConfirmationService.cs` line 141-200 ProcessProposalApprovedAsync | proposal_approved → fire Kickoff step 入口流程確認 |
| F7 | `src/AiTeam.Bot/Orchestration/WorkflowEngine.cs` 173 行完整內容 + QaCoordinationService 3 處 GetDecision call 上下文 | WorkflowEngine.cs 刪除 + caller 移除影響面確認 |

### Spike 結案產出（Forge Plan Mode 內含）

- inner router 瘦身範圍 + 抽出新 method signature 列表
- Pipeline KickoffStage / DesignStage Executor lifecycle 設計（HandleEntryAsync 跑會議 + finalize → yield；HandleResponseAsync resume）
- HandleKickoffConfirmedAsync / HandleDesignConfirmedAsync fire next stage 邏輯移除位置
- sub-task 整合 Pipeline FinalizePipelineAsync 對齊 Stage 46 EpicChain 機制位置
- 6 hooks 移除範圍 + helper 保留清單

### Spike 階段失敗條件（極低風險）

Stage 49-54 know-how 全複用，無新 framework 機制需驗。若實作期揭露 inner router 抽出 method 跟既有 caller signature 衝突 → 暫停 + 回報 Christ 評估。

---

## 子項 1-10：實作細節（對齊 Aria 拿捏）

> 詳細實作位置 / 程式碼片段由 Forge Plan Mode 拍板。Aria 計劃書層級提供 scope + 邊界。

### 子項 1：inner router 瘦身（核心戰略級）

新加 method：
- `FrameworkKickoffRouter.HandleKickoffMeetingAsync(group, ct)` — 純會議跑通（內含 KickoffWorkflow Run + WatchStreamAsync 收 KickoffLoopResult）+ **不含** CreateKickoffConfirmationAsync
- `FrameworkDesignRouter.HandleDesignMeetingAsync(group, ct)` — 純會議跑通（內含 DesignWorkflow Run + DesignLoopResult）+ **不含** FinalizeDesignAsync 內 finalize actions（拆 task 提案評估留在 inner，但 BossInteraction 創建移出）

既有 entry method（`RunKickoffMeetingAsync` / `RunDesignMeetingAsync`）保留 — feature flag false 時 legacy path 仍用（內部 call 新瘦身 method + 自己 call finalize actions）。

### 子項 2：Pipeline 主 Workflow 加 KickoffStage / DesignStage Executor

新建：
- `src/AiTeam.Bot/Workflows/Pipeline/Executors/KickoffStageExecutor.cs`
- `src/AiTeam.Bot/Workflows/Pipeline/Executors/DesignStageExecutor.cs`

對齊 Stage 53A/53B 既有 stage Executor pattern：
- dual handler：HandleEntryAsync（KickoffStageBridge / DesignStageBridge → call inner HandleKickoffMeetingAsync / HandleDesignMeetingAsync 同步 await → 跑完後 call 新搬過來的 finalize actions → SendMessageAsync(KickoffCompletionRequest / DesignCompletionRequest) yield 等 Christ 回應）
- HandleResponseAsync（KickoffCompletionResponse / DesignCompletionResponse → SendMessage 下一 stage / FallbackBridge）

新加 PortId：`Pipeline-KickoffCompletion` / `Pipeline-DesignCompletion`

PipelineWorkflowFactory 拓撲擴展：5→7 RequestPort（DevPlan/Dev/Dev_fix/Reviewer/QA/Doc + Kickoff/Design）+ 8→10 stage Executor（PipelineStart → KickoffStage → DesignStage → DevPlanStage → ...）

### 子項 3：Pipeline Stage Executor 接管 finalize actions

KickoffStage Executor 內接管：
- CreateKickoffConfirmationAsync 完整邏輯（Discord embed + 3 buttons + RegisterKickoffConfirmation + CreateInteractionAsync kickoff + Stage 54 idempotency check 沿用）
- escalate 路徑同樣接管

DesignStage Executor 內接管：
- 拆 task 提案評估 call DesignSplitProposalEvaluator（Stage 52 抽出的 helper）
- 開 design BossInteraction（CreateInteractionAsync design + Stage 54 idempotency check）
- escalate 路徑接管

### 子項 4：HandleKickoffConfirmedAsync / HandleDesignConfirmedAsync 內 fire next stage 移除

MeetingOrchestrationService line 627-880 既有：
- HandleKickoffConfirmedAsync 內 `tgs.FireStepsAsync([Design])` 移除（kickoff_continue button → InteractionProcessor 觸發 ProcessBossResponseAsync → 由 Pipeline KickoffStage HandleResponseAsync 接管，SendMessage DesignStageBridge）
- HandleDesignConfirmedAsync 內 `tgs.FireStepsAsync([Dev_plan])` 移除（design_continue button 同樣由 Pipeline DesignStage 接管）

button id route 改為 Pipeline KickoffStage / DesignStage Executor callback resume 入口（對齊 Stage 53A J1 yield-resume 機制）。

### 子項 5：FireOneStepAsync entry guard 修改

line 486 `step.AgentName.Equals("Dev_plan", ...)` → `step.AgentName.Equals(AgentNames.Kickoff, ...)` + line 484 `group.ParentGroupId == null` 排除條件移除（5 條件變 4 條件，sub-task 也走 Pipeline framework path）。

### 子項 6：sub-task 整合

Pipeline framework 不感知 sub-task 概念 — 每個 sub-task TaskGroup 跑獨立 Pipeline 實例（對齊 Stage 46 既有「各 phase 獨立 PR」業務語義）。

整合機制：
- Stage 46 既有 BuildEpicSubTasksAsync + EpicChain Sequential 鏈邏輯不變
- Pipeline FinalizePipelineAsync 完成 sub-task → Stage 46 EpicChain 機制 fire 下個 phase 子 group（既有 `tgs.FireStepsAsync(...)` call 觸發 → 走 FireOneStepAsync entry guard → Pipeline KickoffStage 啟動）
- 子 group 從 proposal_approved 進 Pipeline framework path（沿用 parent group 同樣入口分流）

### 子項 7：HandleAgentCompletedAsync 6 hooks 移除

line 188-273 全段刪除：
- Dev_plan hook → `appealOrchestration.HandleDevPlanCompletedAsync`（line 189-195）
- Reviewer hook → `appealOrchestration.HandleReviewerCompletedAsync`（line 198-217）
- Dev/Dev_fix [BLOCKED] hook → `appealOrchestration.HandleDevBlockerAsync`（line 220-230）
- 仲裁後 Dev_fix hook → `appealOrchestration.RunPetraGateAsync`（line 233-245）
- QA 修復 Dev_fix hook → `FireStepsAsync(QA)`（line 248-256）
- Dev/Dev_fix 失敗 hook → 中止 fix loop + needs_intervention（line 261-273）

對應 helper 保留供 Pipeline call（NotifyBossDevFailedInterventionAsync 等）。Pipeline path callback resume 分流（line 173）保留作為 Pipeline 主入口（不影響 — feature flag false 時無 PipelineFrameworkStateJson，分流 falsy 自然 skip）。

### 子項 8：WorkflowEngine.cs 刪除 + 4 處 caller 移除

- 刪除 `src/AiTeam.Bot/Orchestration/WorkflowEngine.cs`（173 行）
- TaskGroupService.cs:282-290 「落底 WorkflowEngine.GetDecision」段移除（既有 6 hooks 移除後此段也是 dead code）
- QaCoordinationService.cs:109/142/222 三處 GetDecision call 移除（QaStage Executor 已接管 routing）
- DI 註冊 WorkflowEngine 移除
- TaskGroupService / QaCoordinationService 建構子移除 WorkflowEngine 參數

### 子項 9：Mock 場景擴充

新場景：

| scenario key | 行為 |
|---|---|
| `framework_pipeline_kickoff_to_merge_full` | NewFeature 主路徑從 Kickoff 階段啟動跑通完整 pipeline（Kickoff → Design → DevPlan → Dev → Reviewer → QA → Doc → NotifyMerge） |
| `framework_pipeline_subtask_chain` | sub-task 整合驗證（Petra propose 拆 3 phase → Christ 採納 → 3 個獨立 Pipeline 實例 Sequential 跑） |
| `framework_pipeline_kickoff_crash_recovery` | Kickoff 階段 crash → Pipeline ResumeStreamingAsync rehydrate（議題 G3 解決後 Recovery 機制統一） |
| `framework_pipeline_design_crash_recovery_issue_idempotency_v2` | Design 階段 Issue 創建後 crash → Stage 54 idempotency 仍生效（Pipeline DesignStage Executor 接管 idempotency check） |

驗收沿用 Stage 53B/54 Forge 自驗能力（/internal/mock/scenario HTTP API + MockMode auto-approve BossInteraction + docker compose restart 自跑）。

### 子項 10：Version bump v3.42.0 + 結案文件

- `src/Directory.Build.props` v3.41.0 → v3.42.0
- Roadmap 結案紀錄章節（Forge 結案第一段）
- CHANGELOG / Future_Feature 同步交給 Aria 結案第二段

---

## 驗收情境

> Stage 55A 是 v4 漸進遷移第八步（55A/55B 第一段），**驗收必須含 Pipeline 從 Kickoff 啟動完整路徑 + sub-task 整合 + 6 hooks 移除 regression + WorkflowEngine.cs 刪除 regression**。沿用 Stage 49-54 6-8 場景模式擴充。

### 場景 A：feature flag UseFrameworkPipeline=false legacy 行為不變（regression）

**怎麼觸發**：
1. push Stage 55A commit → CI/CD 部署
2. Dashboard SystemSettings 切 `UseFrameworkPipeline = false`
3. 跑 `/mock new_feature_with_proposal` 走完整新功能流程

**怎麼驗證**：
- ❌ **預期失敗 — 這個場景 Stage 55A 會破**：6 hooks 移除後 legacy path 沒有 fallback safety net；feature flag false 時 Pipeline 不啟動 + 6 hooks 不存在 → 流程卡死
- → **Aria 拿捏紀錄**：Stage 55A 移除 6 hooks 等於宣告「feature flag UseFrameworkPipeline 必須 production 啟用，沒有 legacy 退路」。Christ 拍板 production 已啟用 = true，此場景驗收**不再支援 legacy 路徑**
- → **替代驗收**：場景 A 改驗「feature flag UseFrameworkPipeline=true 確認啟用」+ 後續場景 B-H 全部走 Pipeline framework path

### 場景 B：Pipeline 從 Kickoff 階段啟動 NewFeature 主路徑跑通（核心驗證）⭐

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_pipeline_kickoff_to_merge_full`

**怎麼驗證**：
- ✅ ProposalConfirmationService.ProcessProposalApprovedAsync fire Kickoff step → FireOneStepAsync entry guard 從 Pipeline framework path 啟動（**不**走 legacy MeetingOrchestrationService.RunKickoffMeetingAndWaitAsync）
- ✅ Bot log `[Stage55A] Pipeline framework path 從 Kickoff 啟動（Group={Id}）`
- ✅ Pipeline KickoffStage Executor 跑 inner HandleKickoffMeetingAsync 純會議跑通 + Pipeline 接管開 kickoff BossInteraction → MockMode auto-approve → Pipeline KickoffStage HandleResponseAsync resume → DesignStage
- ✅ DesignStage 同樣流程 → DevPlanStage → ... → NotifyMergeStage
- ✅ group.Status = done + PipelineFrameworkStateJson 清空
- ✅ Discord 一張 kickoff 確認卡 + 一張 design 確認卡 + 一張 merge 通知（不重複）

### 場景 C：Kickoff Crash Recovery（議題 G3 真正解決後 Recovery 機制統一）

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_pipeline_kickoff_crash_recovery`
3. Kickoff 階段中段 docker compose restart aiteam-bot

**怎麼驗證**：
- ✅ 重啟前 PipelineFrameworkStateJson != null + state.CurrentStage = "Kickoff"
- ✅ 重啟後 RecoverStuckFrameworkPipelineAsync ResumeStreamingAsync rehydrate（Stage 54 升級的機制）
- ✅ Pipeline KickoffStage Executor 從 yield 點 resume + Kickoff 完成 → DesignStage 推進
- ✅ Bot log 證實「Pipeline framework path 接管 Kickoff Recovery」（**不**走 legacy KickoffFrameworkStateJson Recovery 路徑）
- ✅ KickoffFrameworkStateJson Recovery 路徑保留（feature flag false 時用，但 55A 後不再有 fallback）

### 場景 D：Design Crash Recovery + Issue idempotency

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_pipeline_design_crash_recovery_issue_idempotency_v2`
3. Design 階段 Rosa pre-work 創 GitHub Issue 後（state.LastIssueCreatedRound = 0）但會議還沒結束 docker compose restart

**怎麼驗證**：
- ✅ 重啟前 PipelineFrameworkStateJson != null + state.CurrentStage = "Design" + group.LastIssueCreatedRound = 0
- ✅ 重啟後 ResumeStreamingAsync rehydrate + Pipeline DesignStage Executor 重跑 superstep
- ✅ **Stage 54 idempotency check 觸發**：DesignRosaPreWorkExecutor 偵測 group.LastIssueCreatedRound = 0 → 跳過 CreateIssueAsync → GitHub 上不出現重複 Issue
- ✅ Design 完成 → Pipeline DesignStage 接管開 design BossInteraction（同樣 Stage 54 idempotency check sticks — 不重複開）
- ✅ MockMode auto-approve → DevPlanStage 推進

### 場景 E：sub-task 整合驗證（Stage 46 機制 → Pipeline framework）⭐

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑 `/mock framework_pipeline_subtask_chain`（Petra Design 階段 propose 拆 3 phase）

**怎麼驗證**：
- ✅ Petra 在 Pipeline DesignStage 內 propose 拆 task → split_task_proposal BossInteraction 開卡 → MockMode auto-approve → BuildEpicSubTasksAsync 創 3 個子 group（phase 1/2/3）
- ✅ Phase 1 子 group **走 Pipeline framework path**（FireOneStepAsync entry guard ParentGroupId == null 移除後 sub-task 也走 Pipeline）— Bot log `[Stage55A] Pipeline framework path 從 Kickoff 啟動（Group={Phase1Id}, ParentGroupId={ParentId}）`
- ✅ Phase 1 跑完 → Stage 46 EpicChain 機制 fire Phase 2 → Phase 2 同樣走 Pipeline framework path → Phase 3 同樣
- ✅ Epic done → 3 個獨立 PR（各 phase 獨立業務語義保留）

### 場景 F：6 hooks 移除 regression（Pipeline 涵蓋全路徑）

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. 跑沿用 Stage 53B 6 場景：`framework_pipeline_fix_loop_recover_round1` / `_max_iter` / `_dev_blocker_appeal` / `_qa_no_tests_dynamic` / `_reviewer_fallback_dynamic` / `_fix_loop_crash_recovery`

**怎麼驗證**：
- ✅ 6 場景全部跑通（**6 hooks 移除後 Pipeline 仍涵蓋全路徑** — Dev_plan / Reviewer / Dev[BLOCKED] / 仲裁後 Dev_fix / QA 修復 / Dev fail intervention）
- ✅ Bot log **無**任何「[Stage37] HandleAgentCompletedAsync hook 觸發」相關訊息（6 hooks 移除）
- ✅ 既有 Pipeline 5 stage Executor + 4 子流程 Executor 全部正常運作

### 場景 G：WorkflowEngine.cs 刪除 regression

**怎麼觸發**：
1. UseFrameworkPipeline = true
2. dotnet build 確認 0 Error（WorkflowEngine.cs + 4 處 caller 移除後編譯通過）
3. 跑場景 B + F 全部跑通

**怎麼驗證**：
- ✅ dotnet build 0 Error / 0 新 Warning
- ✅ Bot log 無 `WorkflowEngine.GetDecision` 相關訊息
- ✅ Pipeline 內 routing（含 framework AddSwitch）取代 GetDecision lookup table 行為一致
- ✅ QaCoordinationService 3 處 GetDecision call 移除後 QaStage Executor 接管 routing 行為一致（Stage 53B 既有驗證延伸）

### 場景 H：MockMode auto-approve 含 kickoff/design type（Stage 54 follow-up #2 仍生效）

**怎麼觸發**：
1. MockMode = true
2. 跑場景 B（kickoff/design BossInteraction 創建）

**怎麼驗證**：
- ✅ kickoff BossInteraction 創建後**自動** set status=responded + ResponseAction=kickoff_continue（Stage 54 修法仍生效）
- ✅ design BossInteraction 同樣 auto-approve + ResponseAction=design_continue
- ✅ Pipeline KickoffStage / DesignStage HandleResponseAsync 收到 InteractionProcessor 觸發 → resume → 推進

---

## 風險點 / 注意事項

### 1. 議題 G3 解法跨層 refactor 規模（中-高）

**風險**：inner router 瘦身（抽出 HandleKickoffMeetingAsync / HandleDesignMeetingAsync）+ Pipeline Stage Executor 接管 finalize actions + HandleKickoffConfirmedAsync / HandleDesignConfirmedAsync fire next stage 邏輯移除 — 三處改動需要對齊。

**緩解**：
- 子項 1-4 順序：先抽 method（不破壞既有）→ Pipeline 接管 finalize → 移除 fire next stage 邏輯（最後一步避免中段 broken）
- 場景 B Pipeline 從 Kickoff 啟動完整路徑驗收

### 2. sub-task 整合 Pipeline 跨 group 鏈拓撲（中）

**風險**：每個 sub-task 一個獨立 Pipeline 實例 — Stage 46 既有 EpicChain Sequential 鏈推進機制需要與 Pipeline FinalizePipelineAsync 對齊。

**緩解**：
- 子項 6 sub-task 整合不改 Stage 46 既有機制（BuildEpicSubTasksAsync / EpicChain），只動 FireOneStepAsync entry guard
- 場景 E sub-task 整合驗收 + 確認 3 個獨立 PR（各 phase 業務語義保留）

### 3. 6 hooks 移除影響面（中）

**風險**：移除後 Pipeline 必須完全涵蓋全 NewFeature 主路徑 + 子流程；feature flag false 時無 legacy 退路。

**緩解**：
- Stage 53A/53B 已驗 Pipeline 涵蓋全路徑（6 場景全綠）
- 場景 F regression 確認 6 場景仍跑通
- Christ 已拍 production 保留 UseFrameworkPipeline=true → 無 legacy 路徑需求

### 4. WorkflowEngine.cs 刪除影響面（低）

**風險**：QaCoordinationService 3 處 GetDecision call + TaskGroupService 落底 GetDecision 段移除可能漏 caller。

**緩解**：
- 子項 0 read F7 完整 grep 所有 caller
- dotnet build 0 Error 為 first gate
- 場景 G regression 確認 build + 既有場景跑通

### 5. AppealOrchestrationService 16 處 skip 保留紀律

**Stage 55A 不動 production code**：
- ❌ 既有 BossInteraction 10+ type / InteractionService 既有 method 結構（55B 範圍）
- ❌ FrameworkHitlBridge / framework_kickoff_mid_interrupt type（55B 推廣）
- ❌ AppealOrchestrationService 16 處 skip 邏輯（55B 切 HITL 後評估精簡）
- ❌ Stage 49-54 既有 framework path 主邏輯（除 Pipeline 拓撲擴展）

**Stage 55A 動的 production code**：
- 動：FrameworkKickoffRouter / FrameworkDesignRouter（瘦身 + 抽 method）
- 動：MeetingOrchestrationService.HandleKickoffConfirmedAsync / HandleDesignConfirmedAsync（移除 fire next stage 邏輯）
- 動：TaskGroupService.HandleAgentCompletedAsync（6 hooks 移除）
- 動：TaskGroupService.FireOneStepAsync（entry guard AgentName + ParentGroupId 排除條件）
- 動：QaCoordinationService（3 處 GetDecision call 移除）
- 動：PipelineWorkflowFactory（拓撲擴展 + 2 新 Executor）
- 動：PipelineState（加 KickoffStageBridge / DesignStageBridge + KickoffCompletionRequest/Response + DesignCompletionRequest/Response record）
- 動：FrameworkPipelineRouter.BuildAgentCompletionResponse（mapping 6→8 entries）
- 動：MockScenarioService / MockClaudeCodeService（新場景）
- 動：Directory.Build.props（Version bump）
- 新建：KickoffStageExecutor.cs / DesignStageExecutor.cs
- 刪除：WorkflowEngine.cs（173 行）

### 6. Aria 規劃前期 grep 紀律（自省點 #23 持續守 + 升級候選）

**Stage 53A 議題 G3 在 QA 重演 + Stage 53B 議題 F-1 16 處 skip + Stage 54 子項 4 IsFixLoop 條件廣義化教訓延續**：規劃任何 framework Workflow 同步 await call 既有 service method 時，必須做完整 grep（含 transitive callers + plan 假設條件 vs production 實際 trigger 條件 cross-check）。

Stage 55A 規劃前期 Aria 已 grep：
- inner FrameworkKickoffRouter.CreateKickoffConfirmationAsync 完整內容（line 660-750）+ Stage 54 idempotency check 結構
- FrameworkDesignRouter.FinalizeDesignAsync caller 結構
- MeetingOrchestrationService.HandleKickoffConfirmedAsync / HandleDesignConfirmedAsync 既有 fire next stage 邏輯位置
- HandleAgentCompletedAsync 6 hooks 完整結構（line 188-273）
- FireOneStepAsync entry guard 5 條件（line 478-502）+ sub-task 排除條件
- WorkflowEngine.cs 173 行 + 4 處 caller（TaskGroupService 2 處 + QaCoordinationService 3 處）
- ProposalConfirmationService.ProcessProposalApprovedAsync line 191 fire Kickoff step 入口
- BossInteraction 27 處 caller 散在 14 個 service（55B 範圍 — Stage 55A 不動，僅作 55B 預掃對照）

**Stage 55B 後續預警**：BossInteraction 切 framework HITL pattern 規劃前期必須對 27 處 caller 全部 grep + 對齊 Stage 51 試點 RequestPort + ResumeStreamingAsync know-how。

---

## Model 建議

依 [`workflow_aria_model_effort.md`](../../C:/Users/darkl/.claude/projects/D--Source-Code-AI-Team/memory/workflow_aria_model_effort.md) 四維度：

| 維度 | 評估 |
|---|---|
| **任務複雜度** | 中-高 — 議題 G3 解法跨層 refactor + sub-task 整合 Pipeline 跨 group 鏈拓撲 + 6 hooks 移除 + WorkflowEngine.cs 刪除（4 件規模適中合一） |
| **改動範圍** | M-L — 新建 2 stage Executor + 改既有 8-10 檔 + 移除 1 檔（WorkflowEngine.cs）+ 6 hooks 純刪除 + 新場景 |
| **歷史包袱** | 中 — 議題 G3 解法是 Stage 53A 留的核心戰略級議題；Stage 53A/53B/54 know-how 全複用（Pipeline ResumeStreamingAsync rehydrate + 4 CheckpointStore base class + B2 idempotency） |
| **判斷品質要求** | 中-高 — Pipeline 從 Kickoff 啟動的時序紀律 + 6 hooks 移除後無 legacy 退路 + sub-task 整合 Stage 46 既有機制對齊 |

**建議**：**Opus 1M + medium-high**

理由：
1. **混合型 Stage 第 8 個資料點**（沿用 Stage 49-54 ×0.73-1.25 區間，55A 偏 mid 中段下半 ×0.9-1.2，因規模適中 + 議題 G3 解法核心戰略級 + 4 件子工作合一）
2. **預估 context 450-650K**（vs Stage 53A 562K / 53B 578K / 54 421K — 規模類似 Stage 53B 略低於 53A）
3. **1 session 跑充足**（Opus 1M 45-65% 充裕 + 子項性質連貫）

### Context 預估

依 7 項公式 + 混合型 Stage 校準（×0.73-1.25 區間，55A 偏 mid 中段下半預估）：
- 開場 ~32K
- 工作 raw（新建 2 Executor + 改 8-10 檔 + 6 hooks 移除 + WorkflowEngine 刪除 + sub-task 整合）~150-200K
- Grep / Bash 輸出 ~30-50K（議題 G3 對齊 + 6 hooks caller 結構 + WorkflowEngine caller + dotnet build）
- 對話 turn 成本 ~50-80K（spike read + Plan Mode + 閘門一 + 結案）
- Edit 反覆對齊 ~40-70K（議題 G3 解法跨檔 + Pipeline 拓撲擴展）
- Mock 驗收（4 新場景 + 沿用 6 既有 regression）~60-100K
- follow-up 修正 ~30-80K（議題 G3 跨層 refactor 可能踩坑 + sub-task 整合 Stage 46 既有機制對齊）
- 結案文件寫作 ~10-20K
- **總計約 ~400-630K**（Opus 1M 內 40-63% 負擔，舒適區）

→ 1 session 跑充足，不拆 Session。若 Forge spike + 子項 1-3 結束時 context > 320K，主動跟 Christ 提是否拆 Session B（極低機率）。

---

## 與 v4 路線的關係

**Stage 55A 是 v4 漸進遷移 9 Stage 的第八步**（議題 A 拆 Stage 55 後 Stage 55A/55B，v4 路線 8→9 Stage）：

```
Stage 47 ✅ ops 補丁（v3.34.0）
Stage 48 ✅ spike Phase A（v3.34.0 不變）
Stage 49 ✅ Cody-Vera-Petra Appeal loop 遷移（v3.35.0）
Stage 50 ✅ Kickoff Meeting → Group Chat orchestration（v3.36.0）
Stage 51 ✅ framework HITL pattern 試點（v3.37.0）
Stage 52 ✅ Design Meeting B3 路線（v3.38.0）
Stage 53A ✅ macro pipeline NewFeature 主路徑切 framework（v3.39.0）
Stage 53B ✅ fix loop / appeal / QA fix loop / intervention 子流程切 framework + 5 fallback 移除（v3.40.0）
Stage 54 ✅ Crash Recovery 全切 + 4 CheckpointStore base class + B2 idempotency（v3.41.0）
   ↓
Stage 55A（本 Stage）：Kickoff/Design 整合到 Pipeline + sub-task 整合 + 移除 6 hooks + 刪 WorkflowEngine.cs（v3.42.0）
   ↓
Stage 55B：BossInteraction 切 framework HITL（27 處 caller refactor，Stage 51 試點 know-how 全面 wire） — **v4 路線 9/9 達成**
```

> 註：Stage 55A 完成後 v4 漸進遷移進度 **8/9**。議題 G3 真正解決 + sub-task 整合 + Recovery 機制完整統一 + WorkflowEngine 退場，剩 Stage 55B BossInteraction HITL 推廣作為 v4 路線最後一塊。

**Stage 55A 結案後對 Stage 55B 的影響**：
- Pipeline 已涵蓋全路徑 + 6 hooks 移除 + WorkflowEngine 刪除 → Stage 55B 切 BossInteraction HITL 時 27 處 caller 中 kickoff/design 兩 type 已由 Pipeline 接管（Stage 55A 修法），剩 25 處 caller refactor
- AppealOrchestrationService 16 處 skip 邏輯保留 → Stage 55B 切 HITL 後可一併評估精簡（Pipeline DevPlanStage / DevStage 內 call 路徑 HITL 化後 skip 邏輯也可能簡化）

---

## 實作紀錄

> Forge 結案第一段填（子項完成度對照 / Session 結案 / 關鍵設計決策 / 踩坑紀錄 / 驗收結果 / Aria 校準錨候選 — Aria 第二段填）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-04 | 初版規劃書建立（Aria）—— v4 漸進遷移第八步（拆 55A/55B 第一段）Stage 55A：Kickoff/Design 整合到 Pipeline + sub-task 整合 + 移除 6 hooks + 刪 WorkflowEngine.cs（A1 拆 Stage 55A/55B 沿用 Stage 53A/53B 戰術 + B1 子工作 1+2+4+5 + Aria 拿捏 inner router 瘦身 + Pipeline KickoffStage/DesignStage 接管 finalize actions + sub-task 每子 group 獨立 Pipeline 實例 + 55A 不切 HITL 留 55B + 8 場景驗收含 Pipeline 從 Kickoff 啟動核心驗證）。**規劃前期已 grep**：inner router finalize 段 + HandleKickoffConfirmedAsync/HandleDesignConfirmedAsync 既有 fire next stage 邏輯 + HandleAgentCompletedAsync 6 hooks 完整結構 + WorkflowEngine.cs caller + ProposalConfirmationService 入口 + BossInteraction 27 處 caller（55B 預掃對照）— 對齊自省點 #23 規劃前期 grep 紀律。|
