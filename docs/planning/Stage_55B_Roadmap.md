# Stage 55B：v4 漸進遷移第九步（拆 55A/55B 第二段）— BossInteraction 切 framework HITL（10 type）+ AppealOrchestrationService 16 處 skip 精簡 + F-α 排除條件移除

> 對應 Future Feature：v4 漸進遷移 9 Stage 路線第九步（v4 路線**最後一塊** — 完成後 9/9 達成）
> 對應版本：**v3.43.0**（v4 漸進遷移第九個產生版本變動的 Stage）
> 建立日期：2026-05-04
> 狀態：📋 計劃書建立完成，待 Forge 開工
> 文件版本：v1.0

---

## 概述

**戰略背景**：[Stage 49-55A](Stage_55A_Roadmap.md) 完成 v4 漸進遷移前八步 — Pipeline framework 完整化（Stage 53A/B）+ Crash Recovery 全切（Stage 54）+ 議題 G3 真正解決 + sub-task 整合 + 6+1 hooks 移除（Stage 55A）。**Stage 55B 是 v4 漸進遷移最後一塊** — Stage 51 試點 framework HITL pattern 全面 wire 到既有 BossInteraction 適合的 type + 清完 v4 路線殘留 legacy（AppealOrchestrationService 16 處 skip + F-α 排除條件）。

**Stage 55B 戰略價值**：
- **Stage 51 試點 know-how 全面 wire**：framework_kickoff_mid_interrupt（1 type）→ 推廣到 10 type，FrameworkHitlBridge pattern 從「試點單例」變「framework HITL 標準範本」
- **v4 路線殘留 legacy 清完**：AppealOrchestrationService 16 處 skip 邏輯（55A 拍板「55B 切 HITL 後可一併評估精簡」）+ F-α 排除條件 4 處（55A 拍板「55B 後評估移除」）
- **Stage 56 = Trial v6 前置條件**鋪路（純機制清完後 Stage 56 純做觀察類 FF 整理）
- **v4 路線 9/9 達成** — Trial_v6 v4 動態架構驗證的前置條件之一

### 既定 TODO（Stage 55A 留下）

| TODO | 來源 | 拍板紀錄 |
|---|---|---|
| BossInteraction 切 framework HITL | Stage 51 試點精神 + Stage 55A 拍板 | 「真正切既有 BossInteraction 到 framework HITL（Stage 51 試點 know-how 全面 wire）」|
| AppealOrchestrationService 16 處 skip 精簡 | Stage 55A 範圍邊界拍板 | 「16 處 skip 邏輯保留作為 Pipeline path 的安全網；55B 切 HITL 後可一併評估精簡」|
| F-α 排除條件移除 | Stage 55A Aria 拿捏 #6 | 「Stage 53A F-α 加的『PipelineFrameworkStateJson == null』排除條件保留（55B 後評估移除）」|

### Stage 55B 同時做 3 件對齊性工作

1. **BossInteraction 切 framework HITL（10 type）** — Stage 51 試點 FrameworkHitlBridge pattern 全面 wire
2. **AppealOrchestrationService 16 處 skip 精簡** — Pipeline path 接管後 skip 邏輯可移除
3. **F-α 排除條件移除** — 4 處 router 的 `PipelineFrameworkStateJson == null` 排除（Stage 53A 加的暫時設計）

### 範圍邊界（grep 確認後拍板）

#### ✅ 切 framework HITL 的 10 type（在 framework Workflow 內等回應）

| type | 既有 caller 數 | 切 HITL 後對應 framework |
|---|---|---|
| `kickoff` | 4 處 | Pipeline KickoffStage Executor RequestPort |
| `design` | 3 處 | Pipeline DesignStage Executor RequestPort |
| `proposal` | 3 處 | Pipeline 啟動前 ProposalStage（新加，或 ProposalConfirmationService 內 yield-resume） |
| `split_task_proposal` | 1 處 | Pipeline DesignStage Executor 內（Petra propose 拆 task） |
| `merge_notify` | 1 處 | Pipeline NotifyMergeStage Executor RequestPort |
| `intervention` | 1 處 | Pipeline 各 stage failed 路徑 |
| `qa_failed_intervention` | 1 處 | Pipeline QaStage Executor failed 路徑 |
| `dev_failed_intervention` | 1 處 | Pipeline DevStage Executor failed 路徑 |
| `devplan_escalate` | 1 處 | Pipeline DevPlanStage Executor escalate 路徑 |
| `dev_plan_unable` | 1 處 | Pipeline DevPlanStage Executor unable 路徑 |

#### ❌ 不切 HITL 仍用 fire-and-forget pattern 的 5 type（不在 framework Workflow 內）

| type | 既有 caller 數 | 不切的理由 |
|---|---|---|
| `ceo_confirm` | 4 處（CommandHandler / SlashCommandRouter）| Discord 命令處理層（CEO 收到命令後請 Christ 確認派工）— 在 ProposalConfirmationService 流程，不在 framework Workflow 內 |
| `ceo_reply` | 1 處（CommandHandler）| 純 Victoria 回覆通知 + ack pattern |
| `exec_confirm` | 3 處（ButtonCallbackRouter / ProposalConfirmationService）| 執行確認（CEO 確認後 → 執行確認），在 Discord button callback 流程 |
| `epic_partial_paused` | 1 處 | Stage 46 Epic Chain 機制，跨 framework boundary（parent group 在 Pipeline，sub-task pause 是 epic-level 動作）|
| `sage_escalate` | 1 處（DocAgentService）| Sage 是收尾類 escalate，escalate 後 Sage agent 已結束，純通知 + ack |

→ **15 unique type 中 10 切 + 5 不切**（含已切的 framework_kickoff_mid_interrupt = 16 unique type 中已 1 + 切 10 = 11 切 / 5 仍 fire-and-forget）

#### ✅ 搭車清完 v4 路線殘留 legacy

- **AppealOrchestrationService 16 處 skip 精簡**（Stage 53B 議題 F-1 加的）：Pipeline path 接管後 skip 邏輯可移除 — 11 處 + 5 處（AppealOrchestration 11 + QaCoordination 5）
- **F-α 排除條件移除**：4 處 router（`MeetingOrchestrationService` / `FrameworkAppealRouter` / `FrameworkKickoffRouter` / `FrameworkDesignRouter`）的 `PipelineFrameworkStateJson == null` 排除條件（Stage 53A 加的暫時設計）

#### ❌ 不動（Stage 56 範圍 / 不對等）

- WorkflowEngine.cs enum + WorkflowStep record 殘留（Stage 55A 已精簡）— 跨 service reference 多，刪除影響面 vs 收益不對等，留 Stage 56 評估
- Stage 49-55A 既有 framework path 主邏輯（除 BossInteraction call 切 HITL）
- 5 type 不切 HITL（Discord 命令層 / 通知 ack 性質，本質不在 framework Workflow 內）
- ❌ Trial v6 前置條件 4 件（Dashboard MockScenarioCard 補 22+ framework_* 場景 / FF 四十二 / FF 四十三 / Stage 48 候選 FF）— Stage 56 範圍

### v4 路線第九步風險預警

- **Stage 51 試點 FrameworkHitlBridge pattern 推廣到 10 type 規模 L**：FrameworkHitlBridge.cs 353 行單 type 邏輯 → 抽 base / shared helper 給 10 type 用，重構成本中等
- **AppealOrchestrationService 16 處 skip 精簡**：Pipeline path 接管後 skip 邏輯可移，但需確認沒 caller 還依賴（grep 完整 caller 結構）
- **F-α 排除條件移除影響面評估**：移除後 4 個既有 router 的 RecoverStuck*Async 篩選會涵蓋 sub-task TaskGroup（55A sub-task 已走 Pipeline framework path）— 需確認沒 race condition
- **legacy 清完後無 fallback**：feature flag UseFrameworkPipeline=true 為唯一 production path（Stage 55A 已宣告），55B 後 BossInteraction HITL 也是唯一 path（5 type fire-and-forget 是設計性保留，非 fallback）

→ feature flag UseFrameworkPipeline 已 production 啟用（Christ 2026-05-03 拍板保留 true），Stage 55B 不引入新 flag。

---

## 設計決策（Christ 2026-05-04 拍板）

### 主路線拍板（戰略級）

| 議題 | 拍板 | 替代方案 |
|---|---|---|
| **議題 A：Stage 55B 範圍** | **A1：HITL 10 type + AppealOrchestrationService 16 處 skip 精簡 + F-α 排除條件移除**（v4 路線殘留 legacy 一次清完 + Stage 51 試點 know-how 全面 wire）| A2 只切 HITL（v4 殘留 legacy 留 Stage 56 / Trial v6 觀察期）|
| **議題 B：Stage 56 範圍** | **B1：Trial v6 前置條件統包**（Dashboard MockScenarioCard 補 22+ framework_* 場景 + FF 四十二 + FF 四十三 + Stage 48 候選 FF + WorkflowEngine.cs enum/record 殘留評估）— 性質觀察類整理，預估 ×0.7-1.0 mid 帶下半 | B2 觀察類 FF 留 Trial v6 觀察期 follow-up（Stage 56 不開）|
| **議題 C：HITL 切 type 篩選** | **C1：切 10 type，5 type 仍 fire-and-forget**（依 grep caller context 判斷 — Discord 命令層 / 通知 ack 性質的 type 本質不在 framework Workflow 內，切 HITL 反而引入跨層複雜度）| C2 全 15 type 切（過度設計）/ C3 只切核心 5-6 type（ceo_confirm/ceo_reply 等是必須切，但本質衝突）|

### Aria 拿捏（已決，純內部實作不對外議題）

| # | 議題 | 決定 |
|---|---|---|
| 1 | FrameworkHitlBridge pattern 推廣方式 | 抽 `FrameworkHitlBridgeBase` 或 helper class — 既有 FrameworkHitlBridge.cs 353 行單 type 邏輯（framework_kickoff_mid_interrupt 試點），10 type 共用 RequestPort + RequestId + ResumeStreamingAsync + SendResponseAsync 機制；type-specific 部分（Discord embed / button id / actions）由子類 / strategy 實作 |
| 2 | Pipeline Stage Executor 內 BossInteraction call 改 HITL pattern | 對齊 Stage 53A/53B/55A 既有 stage Executor pattern — 既有 `await interactionService.CreateInteractionAsync(...)` fire-and-forget 改 `await frameworkHitlBridge.RequestXxxInteractionAsync(state, request, ct)` yield-resume；HandleResponseAsync resume 後 routing 推進 |
| 3 | 5 type 不切 HITL 的明文紀錄 | calling site 加 comment：`// Stage 55B：本 type 仍用 fire-and-forget pattern — 不在 framework Workflow 內等回應（Discord 命令層 / 通知 ack 性質）` |
| 4 | AppealOrchestrationService 16 處 skip 精簡方式 | 評估每處 skip：① 若 Pipeline path 已完全接管該 method 的 caller → skip 邏輯可移除（method body 簡化或刪除）② 若仍有非 Pipeline caller（如 sub-task / legacy testing path）→ skip 邏輯保留；Forge Plan Mode 第一步 grep 每處 caller 結構確認 |
| 5 | F-α 排除條件移除影響面 | 4 處 router（`MeetingOrchestrationService.RecoverStuckOrchestrationsAsync` / `FrameworkAppealRouter.RecoverStuckFrameworkAppealsAsync` / `FrameworkKickoffRouter.RecoverStuckFrameworkKickoffsAsync` / `FrameworkDesignRouter.RecoverStuckFrameworkDesignAsync`）的 `&& g.PipelineFrameworkStateJson == null` 排除條件移除 — 移除後 sub-task TaskGroup 也納入篩選；需確認 sub-task 走 Pipeline path 後 RecoverStuck 不會 race condition |
| 6 | proposal type 切 HITL 整合方式 | proposal type 在 ProposalConfirmationService 流程內（**不在** Pipeline 主 Workflow 內）— 評估：① ProposalConfirmation 流程改用 framework Workflow 包裝（規模放大） ② 把 proposal 視為「Pipeline 啟動前的 pre-stage」加進 Pipeline framework — 由 Forge Plan Mode 拍板，傾向 ② 對齊 Pipeline 主 Workflow 完整化精神 |
| 7 | Mock 場景觸發機制 | 對齊 Stage 49-55A `MockClaudeCodeService.FailScenario` static + `/internal/mock/scenario` HTTP API + MockMode auto-approve BossInteraction（Stage 54 修法 + Stage 55A follow-up 補 split_task_proposal type，55B 後新切 10 type 都要對應 auto-approve switch case 補上）|
| 8 | InteractionProcessor 路由表更新 | `InteractionProcessor.cs:124-153` 既有 (type, action) → display string mapping 加 10 type 對應 framework HITL action（如 `("kickoff", "kickoff_continue")`）— 對齊 Stage 51 試點既有 entry |
| 9 | DB schema | 不加新欄位 — 沿用 Stage 51 試點 `FrameworkHitlStateJson` 機制（如有需要對應 multi-type）/ 或復用既有 BossInteraction 欄位 |
| 10 | Token 計費 | 沿用既有機制（Stage 55B 不引入新 LLM call） |
| 11 | CLAUDE_*.md prompt | 不動（沿用 Stage 49-55A 慣例） |
| 12 | base class 沿用 Stage 54 | FrameworkHitlBridgeBase（如抽出）+ 既有 FrameworkCheckpointStoreBase（Stage 54）對齊紀律 |
| 13 | legacy InteractionService.CreateInteractionAsync 是否保留 | **保留**（5 type 仍用 fire-and-forget pattern 需要既有 method） — 不刪 method，僅 caller refactor |

### Stage 55B 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **0** | **Spike 第一步：read 對齊範圍**（Forge Plan Mode 第一步）— read FrameworkHitlBridge.cs 353 行 + KickoffMidInterruptTriggerStore.cs + Stage 51 試點完整 HITL pattern + AppealOrchestrationService 16 處 skip 完整 caller 結構 + F-α 排除條件 4 處 + 10 type calling site 完整 context | XS-S |
| **1** | FrameworkHitlBridge pattern 推廣抽 base / helper class（FrameworkHitlBridgeBase 或 strategy 設計，10 type 共用機制）| M |
| **2** | 切 10 type 到 framework HITL — 改 caller 從 `interactionService.CreateInteractionAsync` 改 `frameworkHitlBridge.Request*InteractionAsync` + Pipeline Stage Executor 內 yield-resume routing | L |
| **3** | proposal type 整合 — 議題 6 拍板（傾向加進 Pipeline framework 視為 pre-stage） | M |
| **4** | InteractionProcessor 路由表更新 + 10 type framework HITL action mapping + MockMode auto-approve switch 補 10 type | S |
| **5** | AppealOrchestrationService 16 處 skip 精簡（11 + 5）+ method body 簡化評估 | M |
| **6** | F-α 排除條件移除（4 處 router） + sub-task TaskGroup race condition 確認 | XS |
| **7** | Mock 場景擴充（10 type framework HITL 各驗 1 場景 + AppealOrchestrationService 精簡 regression + F-α 移除 regression） | M-L |
| **8** | 5 type fire-and-forget 不切的 calling site comment 補強 + Stage 55B 範圍邊界明文紀錄 | XS |
| **9** | Version bump v3.42.0 → v3.43.0 + 結案文件 | XS |

> **不寫工時估算**（workflow_aria.md 第三節 A 第 4 條）— 各子項規模見上表 XS/S/M/L 範圍描述。

---

## 子項 0：Spike 第一步 — read 對齊範圍

### read 對齊範本

| # | read 對象 | 目的 |
|---|---|---|
| F1 | `src/AiTeam.Bot/Orchestration/Hitl/FrameworkHitlBridge.cs` 353 行 + `KickoffMidInterruptTriggerStore.cs` 35 行 | Stage 51 試點完整 HITL pattern — RequestPort + RequestId + ResumeStreamingAsync + SendResponseAsync 機制；推廣到 10 type 的範本 |
| F2 | 10 type calling site 完整 context（kickoff/design/proposal/split_task_proposal/merge_notify/intervention/qa_failed_intervention/dev_failed_intervention/devplan_escalate/dev_plan_unable）| 確認每個 type 在 Pipeline 哪個 stage / framework Workflow 內等回應位置 |
| F3 | `src/AiTeam.Bot/Orchestration/Appeal/AppealOrchestrationService.cs` 16 處 skip 完整 caller 結構（Stage 53B 議題 F-1 加的 11 處 + Stage 53A QaCoordinationService 對齊 5 處）| 評估每處 skip 是否可移除（Pipeline 已完全接管 vs 仍有非 Pipeline caller）|
| F4 | 4 處 F-α 排除條件（`MeetingOrchestrationService` / `FrameworkAppealRouter` / `FrameworkKickoffRouter` / `FrameworkDesignRouter` 的 RecoverStuck*Async）| sub-task TaskGroup race condition 確認 |
| F5 | `InteractionProcessor.cs:124-153` (type, action) → display string mapping + Stage 54 MockMode auto-approve switch | 路由表更新 + 10 type framework HITL action mapping 補上 |
| F6 | `ProposalConfirmationService.cs` 完整流程結構 | 議題 6 proposal type 整合 Pipeline pre-stage 設計可行性 |
| F7 | 5 type calling site（ceo_confirm/ceo_reply/exec_confirm/epic_partial_paused/sage_escalate）| 確認真的不適合切（已 Aria 預掃確認 + Forge Plan Mode 二次驗證）|

### Spike 結案產出（Forge Plan Mode 內含）

- FrameworkHitlBridge pattern 抽象範圍（base class signature + abstract method）
- 10 type 切 HITL 的 type-specific 設計（每 type Discord embed / button id / actions / Pipeline Stage Executor 對應 RequestPort）
- proposal type 整合方式拍板（議題 6 — Pipeline pre-stage vs 獨立 Workflow）
- AppealOrchestrationService 16 處 skip 精簡範圍（哪幾處可移除 / 哪幾處保留）
- F-α 排除條件移除影響面評估

### Spike 階段失敗條件

若以下任一發生 → 暫停 Stage 55B + 回報 Christ：
- proposal type 整合 Pipeline pre-stage 設計（議題 6 ②）規模超出評估 → 評估退回 ① ProposalConfirmation 流程獨立 Workflow 包裝（規模放大但邊界乾淨）/ 或留 Stage 56
- F-α 排除條件移除引入 sub-task TaskGroup race condition → 評估保留 F-α 留 v4 路線後續評估

---

## 子項 1-9：實作細節（對齊 Aria 拿捏）

> 詳細實作位置 / 程式碼片段由 Forge Plan Mode 拍板。Aria 計劃書層級提供 scope + 邊界。

### 子項 1：FrameworkHitlBridge pattern 推廣

抽 `FrameworkHitlBridgeBase` abstract class（位置 Forge Plan Mode 拍板，可能 `src/AiTeam.Bot/Orchestration/Hitl/`）：

統一機制：
- RequestPort + RequestId 管理
- ResumeStreamingAsync rehydrate + SendResponseAsync routing
- BossInteraction 創建時帶 contextJson 含 requestId

子類 / strategy 實作 type-specific 部分：
- Discord embed builder（title / description / color / fields）
- button id pattern（如 `kickoff_continue_{groupId}` / `design_continue_{groupId}`）
- AvailableActions JSON
- yield-resume routing 後的下個 stage 對應

### 子項 2：切 10 type 到 framework HITL

對每個 type：
1. caller 從 `await interactionService.CreateInteractionAsync(...)` fire-and-forget 改 `await frameworkHitlBridge.RequestXxxInteractionAsync(state, request, ct)` yield-resume（對齊 Stage 51 試點 pattern）
2. Pipeline Stage Executor 對應加 `[RequestPort]` + HandleResponseAsync resume routing

10 type 對應 Pipeline Stage：

| type | Pipeline Stage | RequestPort 命名 |
|---|---|---|
| kickoff | KickoffStageExecutor | `Pipeline-KickoffConfirmation` |
| design | DesignStageExecutor | `Pipeline-DesignConfirmation` |
| proposal | PipelineProposalPreStage（議題 6 ②） | `Pipeline-ProposalConfirmation` |
| split_task_proposal | DesignStageExecutor 內 | `Pipeline-SplitTaskProposal` |
| merge_notify | NotifyMergeStageExecutor | `Pipeline-MergeNotify` |
| intervention | 各 stage failed 路徑 | `Pipeline-Intervention` |
| qa_failed_intervention | QaStageExecutor failed | `Pipeline-QaFailedIntervention` |
| dev_failed_intervention | DevStageExecutor failed | `Pipeline-DevFailedIntervention` |
| devplan_escalate | DevPlanStageExecutor escalate | `Pipeline-DevPlanEscalate` |
| dev_plan_unable | DevPlanStageExecutor unable | `Pipeline-DevPlanUnable` |

具體 RequestPort 命名 + Bridge record / Request-Response record 設計由 Forge Plan Mode 拍板。

### 子項 3：proposal type 整合 Pipeline pre-stage

議題 6 ② 設計：把 proposal 視為 Pipeline 啟動前的 pre-stage（Pipeline 從 ProposalStage 啟動 → KickoffStage → DesignStage → ...）。

ProposalConfirmationService 內既有 `interactionService.CreateInteractionAsync("proposal", ...)` 改為 Pipeline ProposalStage Executor 接管：
- HandleEntryAsync：CEO 收到 proposal → 開 BossInteraction proposal → yield 等 Christ 回應
- HandleResponseAsync：Christ 回應後 routing：
  - approve → SendMessage KickoffStageBridge（推進到 Stage 55A 既有 KickoffStage）
  - reject → YieldOutput Cancelled
  - adjust → 重開 proposal flow（fallback to legacy 或 Pipeline 內 self-loop）

入口分流：FireOneStepAsync entry guard 5 條件 + 兩入口（55A）擴展為三入口：parent group Proposal / parent group Kickoff（如沒 proposal 階段直接進）/ sub-task Dev_plan。Forge Plan Mode 拍板入口分流邏輯。

### 子項 4：InteractionProcessor 路由表更新 + Mock auto-approve

`InteractionProcessor.cs:124-153` 加 10 type framework HITL action mapping：

```
("kickoff",                "kickoff_continue")    => "繼續 Kickoff ▶️"
("kickoff",                "kickoff_stop")        => "停止任務 ❌"
("kickoff",                "kickoff_modify")      => "修改 ✏️"
... 其他 9 type 同樣 pattern
```

Stage 54 MockMode auto-approve switch（含 Stage 55A follow-up #1 補的 split_task_proposal）擴展涵蓋 10 type：每 type 的 default approve action 對齊。

### 子項 5：AppealOrchestrationService 16 處 skip 精簡

子項 0 F3 spike 結果分類每處 skip：
- **可移除**：Pipeline path 已完全接管該 method 的 caller，skip 邏輯成 dead code
- **保留**：仍有非 Pipeline caller（如 sub-task TaskGroup 跑 Pipeline 但 sub-task 內仍 call legacy method 的情況）

精簡後 method body 對應簡化（如某 method 全部 caller 都是 Pipeline → method body 整段 if PipelineFrameworkStateJson != null skip 改為直接 return；若 method 整個變 dead code → 評估 method 刪除）。

### 子項 6：F-α 排除條件移除

4 處 router 的 RecoverStuck*Async 移除 `&& g.PipelineFrameworkStateJson == null`：

```diff
- .Where(g => g.XxxFrameworkStateJson != null && !g.IsPaused
-          && g.PipelineFrameworkStateJson == null)
+ .Where(g => g.XxxFrameworkStateJson != null && !g.IsPaused)
```

sub-task TaskGroup race condition 確認（55A sub-task 已走 Pipeline framework path，55B 移除 F-α 後 sub-task 也納入 4 router 篩選 — 評估是否有 sub-task 既有 Kickoff/Design FrameworkStateJson 的情況）。實際上 sub-task 從 Dev_plan 啟動 skip Kickoff/Design 階段（Stage 55A 兩入口分流 + IsSubTask state 路由）→ sub-task 不會有 Kickoff/Design FrameworkStateJson，race condition 風險低。

### 子項 7：Mock 場景擴充

10 type framework HITL 各驗 1 場景 + 既有 v4 路線 regression 場景沿用：

| scenario key | 驗收重點 |
|---|---|
| `framework_pipeline_kickoff_hitl` | kickoff type 切 HITL — Pipeline KickoffStage yield 等 BossInteraction → MockMode auto-approve → resume 推進 DesignStage |
| `framework_pipeline_design_hitl` | design type 切 HITL — 同 pattern |
| `framework_pipeline_proposal_hitl_pre_stage` ⭐ | proposal type Pipeline pre-stage 整合驗證（議題 6 ②） |
| `framework_pipeline_split_task_proposal_hitl` | split_task_proposal type 切 HITL — Stage 46 拆 task 機制走 framework HITL |
| `framework_pipeline_merge_notify_hitl` | merge_notify type 切 HITL — NotifyMergeStage yield 等 Christ ack |
| `framework_pipeline_intervention_hitl` / `_qa_failed` / `_dev_failed` / `_devplan_escalate` / `_devplan_unable` | 各 escalate / intervention type 切 HITL（合併 5 場景或拆獨立看實作複雜度）|
| `framework_pipeline_appeal_skip_cleanup_regression` | AppealOrchestrationService 16 處 skip 精簡 regression — Pipeline path 跑通既有 Stage 53B 6 場景 + sub-task chain（Stage 55A 場景 E）|
| `framework_pipeline_f_alpha_removed_regression` | F-α 排除條件移除 regression — 4 router Recovery 場景跑通（Stage 49/50/52/53A 既有 Crash Recovery 場景）|

### 子項 8：5 type fire-and-forget 不切的 calling site comment 補強

每個 5 type calling site 加 comment：

```csharp
// Stage 55B 範圍邊界拍板：本 type 仍用 fire-and-forget pattern 不切 framework HITL —
// 不在 framework Workflow 內等回應（ceo_confirm: Discord 命令層 / ceo_reply: 純通知 ack /
// exec_confirm: Discord button callback 流程 / epic_partial_paused: Stage 46 Epic Chain 跨 framework boundary /
// sage_escalate: 收尾類純通知 ack）
_ = interactionService.CreateInteractionAsync(...);
```

對應位置（依子項 0 F7 grep 結果）：
- `CommandHandler.cs:151/193/479/541` ceo_reply / ceo_confirm
- `ButtonCallbackRouter.cs:428/787` exec_confirm
- `ProposalConfirmationService.cs:78` exec_confirm
- `SlashCommandRouter.cs:254` ceo_confirm
- `DocAgentService.cs:310` sage_escalate
- `TaskGroupService.cs:1435` epic_partial_paused

### 子項 9：Version bump v3.43.0 + 結案文件

- `src/Directory.Build.props` v3.42.0 → v3.43.0
- Roadmap 結案紀錄章節（Forge 結案第一段）— 含 Stage 51 試點 know-how 全面 wire 範圍 / proposal Pipeline pre-stage 整合拍板紀錄 / AppealOrchestrationService 16 處 skip 精簡分類紀錄 / F-α 移除影響面實證
- CHANGELOG / Future_Feature 同步交給 Aria 結案第二段

---

## 驗收情境

> Stage 55B 是 v4 漸進遷移**最後一塊**，**驗收必須含 10 type framework HITL 全綠 + AppealOrchestrationService 精簡 regression + F-α 移除 regression**。沿用 Stage 49-55A 6-8 場景模式擴充。

### 場景 A：feature flag UseFrameworkPipeline 啟用確認（55A 已宣告無 legacy 退路）

**怎麼觸發**：
1. push Stage 55B commit → CI/CD 部署
2. 確認 `UseFrameworkPipeline = true`（Christ production 已拍板保留）

**怎麼驗證**：
- ✅ Bot 啟動 log 確認 flag = true
- ✅ Dashboard SystemSettings 確認 flag = true

### 場景 B：kickoff type 切 framework HITL 跑通 ⭐

**怎麼觸發**：
1. 跑 `/mock framework_pipeline_kickoff_hitl`
2. Pipeline KickoffStage yield 等 BossInteraction → MockMode auto-approve（Stage 54 修法 + Stage 55B 補 kickoff_continue mapping）

**怎麼驗證**：
- ✅ Pipeline KickoffStage 走 framework HITL（**不**走既有 fire-and-forget pattern）— Bot log `[Stage55B] kickoff framework HITL request emit (requestId=...)`
- ✅ MockMode auto-approve 觸發 → Pipeline KickoffStage HandleResponseAsync resume → SendMessage DesignStageBridge
- ✅ 完整 Pipeline 跑通到 NotifyMergeStage done

### 場景 C：design type 切 framework HITL 跑通

同場景 B 邏輯，driver 改 `framework_pipeline_design_hitl`。

### 場景 D：proposal type Pipeline pre-stage 整合 ⭐⭐⭐

**怎麼觸發**：
1. 跑 `/mock framework_pipeline_proposal_hitl_pre_stage`
2. Pipeline ProposalStage（新加 pre-stage）yield 等 BossInteraction proposal → MockMode auto-approve

**怎麼驗證**：
- ✅ Pipeline 從 ProposalStage 啟動（**不**走既有 ProposalConfirmationService 流程）— Bot log `[Stage55B] proposal framework HITL pre-stage request emit`
- ✅ Christ approve → ProposalStage HandleResponseAsync resume → SendMessage KickoffStageBridge → 推進 KickoffStage
- ✅ 完整 Pipeline 跑通

### 場景 E：split_task_proposal type 切 framework HITL（Stage 46 拆 task 機制 + Stage 55B HITL）

**怎麼觸發**：
1. 跑 `/mock framework_pipeline_split_task_proposal_hitl`（Petra 在 Pipeline DesignStage propose 拆 3 phase）

**怎麼驗證**：
- ✅ Stage 46 拆 task 機制走 framework HITL — Pipeline DesignStage 內開 split_task_proposal BossInteraction → MockMode auto-approve（Stage 55A follow-up #1 修法 + Stage 55B HITL pattern 對齊）
- ✅ Christ approve → BuildEpicSubTasksAsync 創 3 sub-task → sub-task chain 跑通（Stage 55A 場景 E 對齊）

### 場景 F：merge_notify type 切 framework HITL

**怎麼觸發**：跑 `/mock framework_pipeline_merge_notify_hitl`（Pipeline NotifyMergeStage 等 Christ ack）

**怎麼驗證**：
- ✅ NotifyMergeStage 開 merge_notify BossInteraction → MockMode auto-approve ack → Pipeline 完成 done

### 場景 G：5 種 escalate / intervention type 切 framework HITL（合併或拆獨立看實作複雜度）

**怎麼觸發**：5 個場景或合併（intervention / qa_failed_intervention / dev_failed_intervention / devplan_escalate / dev_plan_unable）

**怎麼驗證**：
- ✅ 各 escalate / intervention type 切 HITL 走 Pipeline path
- ✅ Pipeline 各 stage failed 路徑 yield 等 BossInteraction → MockMode auto-approve → resume 推進 needs_intervention 設定

### 場景 H：AppealOrchestrationService 16 處 skip 精簡 regression

**怎麼觸發**：跑 Stage 53B 6 場景 + Stage 55A 場景 E（sub-task chain）

**怎麼驗證**：
- ✅ 6 場景全綠（Pipeline path 跑通，AppealOrchestrationService skip 精簡後行為一致）
- ✅ sub-task chain 跑通（sub-task 走 Pipeline path + AppealOrchestrationService 精簡無破壞）

### 場景 I：F-α 排除條件移除 regression

**怎麼觸發**：4 router Recovery 場景跑通（Stage 49/50/52/53A 既有 Crash Recovery 場景沿用 + Stage 55A 場景 C/D Pipeline Crash Recovery）

**怎麼驗證**：
- ✅ 4 router Recovery 行為一致（移除 F-α 後 sub-task TaskGroup 也納入篩選但 sub-task 不會有 Kickoff/Design FrameworkStateJson，race condition 0）
- ✅ Pipeline Crash Recovery 跑通（Stage 54 既有 ResumeStreamingAsync rehydrate know-how 沿用）

### 場景 J：5 type fire-and-forget 不切 verify

**怎麼觸發**：Discord 跑 `/ceo` 命令觸發 ceo_confirm + ceo_reply / Discord button click exec_confirm / Stage 46 epic 暫停 epic_partial_paused / Doc agent escalate sage_escalate

**怎麼驗證**：
- ✅ 5 type 仍用既有 `interactionService.CreateInteractionAsync` fire-and-forget pattern
- ✅ 走既有 InteractionProcessor 輪詢 + ProcessBossResponseAsync 路由（**不**走 framework HITL pattern）

---

## 風險點 / 注意事項

### 1. FrameworkHitlBridge pattern 推廣到 10 type 規模 L（中-高）

**風險**：FrameworkHitlBridge.cs 353 行單 type 邏輯，10 type 共用要抽 base / strategy 設計 — Stage 51 試點未驗多 type 共用機制。

**緩解**：
- 子項 0 spike F1 read 完整 Stage 51 試點 pattern + Plan Mode 第一步拍板 base class 抽象範圍
- 對齊 Stage 54 FrameworkCheckpointStoreBase 既有 base class 抽象 know-how（Stage 54 ×0.77 校準錨已驗）

### 2. proposal type 整合 Pipeline pre-stage（中-高，議題 6 拍板）

**風險**：proposal 在 ProposalConfirmationService 流程，**不在** Pipeline 主 Workflow 內 — 整合需要 Pipeline framework 加 ProposalStage pre-stage（規模放大）+ FireOneStepAsync entry guard 三入口分流。

**緩解**：
- 子項 0 spike F6 read ProposalConfirmationService 完整流程 + Plan Mode 第一步拍板整合方式（議題 6 ① 流程包裝 vs ② Pipeline pre-stage）
- 場景 D 專門驗證 proposal pre-stage 整合
- 若實作期揭露 ② 規模超出評估 → 退回 ① 或留 Stage 56

### 3. AppealOrchestrationService 16 處 skip 精簡 caller 結構（中）

**風險**：每處 skip 精簡前需確認沒非 Pipeline caller 仍依賴。

**緩解**：
- 子項 0 spike F3 完整 grep AppealOrchestrationService 16 處 caller 結構
- 子項 5 每處 skip 分類拍板（可移除 vs 保留）
- 場景 H regression 確認

### 4. F-α 排除條件移除 sub-task race condition（低）

**風險**：sub-task TaskGroup 也納入 4 router 篩選 — 是否有 race condition。

**緩解**：
- sub-task 從 Dev_plan 啟動 skip Kickoff/Design 階段 → 不會有 Kickoff/Design FrameworkStateJson → race condition 風險 0
- 場景 I regression 確認

### 5. Aria 規劃前期 grep 紀律（自省點 #23 持續守 + 升級候選）

**Stage 53A 議題 G3 在 QA 重演 + Stage 53B 議題 F-1 16 處 skip + Stage 54 子項 4 IsFixLoop 條件廣義化 + Stage 55A 缺口 2 sub-task first step 教訓延續**：規劃任何 framework Workflow refactor 時，必須做完整 grep（含 transitive callers + plan 假設條件 vs production 實際 trigger 條件 cross-check）。

Stage 55B 規劃前期 Aria 已 grep：
- 27 處 BossInteraction caller 散在 14 service（含 type 分布）
- 5 個「不適合切」type calling site 完整 context（Discord 命令層 / 通知 ack 性質確認）
- FrameworkHitlBridge.cs 353 行 + KickoffMidInterruptTriggerStore.cs 35 行（Stage 51 試點 pattern 範本）
- AppealOrchestrationService 16 處 skip 位置（Stage 53B 議題 F-1 + Stage 53A QaCoordination 對齊）
- F-α 排除條件 4 處（Stage 53A F-α 加的）
- InteractionProcessor.cs:124-153 路由表結構

**Stage 56 後續預警**：Stage 56 = Trial v6 前置條件統包，規劃前期需 grep 觀察類 FF 真實規模 + Dashboard MockScenarioCard 22+ framework_* 場景補齊範圍 + WorkflowEngine.cs enum/record 殘留刪除影響面評估。

---

## Model 建議

依 [`workflow_aria_model_effort.md`](../../C:/Users/darkl/.claude/projects/D--Source-Code-AI-Team/memory/workflow_aria_model_effort.md) 四維度：

| 維度 | 評估 |
|---|---|
| **任務複雜度** | 中-高 — Stage 51 試點 FrameworkHitlBridge pattern 推廣到 10 type（base class 抽象 + type-specific 子類）+ proposal type Pipeline pre-stage 整合 + AppealOrchestrationService 16 處 skip 精簡 + F-α 移除 |
| **改動範圍** | L — FrameworkHitlBridge base class + 10 type calling site refactor + Pipeline 拓撲擴展（含 ProposalStage pre-stage if 議題 6 ②）+ InteractionProcessor 路由表 + Mock 場景擴充 |
| **歷史包袱** | 中 — Stage 51 試點 know-how 全複用 + Stage 54 base class 抽象 know-how 全複用 + Stage 55A 兩入口分流 know-how（sub-task）全複用 |
| **判斷品質要求** | 中-高 — 5 type 不切 HITL 邊界判斷（已 Aria 預掃 + Forge spike 二次驗證）+ AppealOrchestrationService 16 處 skip 分類精簡 + proposal pre-stage 設計選擇 |

**建議**：**Opus 1M + high**

理由：
1. **混合型 Stage 第 9 個資料點**（沿用 Stage 49-55A ×0.73-1.25 區間，55B 偏 mid 中段 ×1.0-1.3，因 v4 路線最後一塊 + 規模 L + 戰略級 BossInteraction 切 HITL）
2. **預估 context 500-700K**（vs Stage 53A 562K / 53B 578K / 55A 482K — 規模類似 53B / 53A）
3. **可能 1 session 跑或拆 session 1-2 段**（Opus 1M 50-70% 充裕，子項性質連貫但 10 type refactor 可能跨 session）

### Context 預估

依 7 項公式 + 混合型 Stage 校準（×0.73-1.25 區間，55B 偏 mid 中段預估）：
- 開場 ~32K
- 工作 raw（FrameworkHitlBridge base class + 10 type refactor + ProposalStage pre-stage + AppealOrchestrationService 16 處 skip 精簡 + F-α 移除）~180-240K
- Grep / Bash 輸出 ~40-60K（27 caller 完整對齊 + 16 skip 分類 + 4 F-α 點 + dotnet build）
- 對話 turn 成本 ~60-90K（spike read + Plan Mode + 閘門一 + 結案）
- Edit 反覆對齊 ~50-80K（10 type calling site 對齊 + base class 抽象設計多輪）
- Mock 驗收（10 type 各 1 場景 + 2 regression + 5 type fire-and-forget verify）~80-120K
- follow-up 修正 ~40-100K（FrameworkHitlBridge pattern 推廣首次 + proposal pre-stage 設計可能踩坑）
- 結案文件寫作 ~10-20K
- **總計約 ~490-740K**（Opus 1M 內 49-74% 負擔，舒適區到接近邊界）

→ 1 session 跑充足，若 Forge spike + 子項 1-3 結束時 context > 380K，主動跟 Christ 提是否拆 Session B（中等機率）。

---

## 與 v4 路線的關係

**Stage 55B 是 v4 漸進遷移 9 Stage 的最後一塊**：

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
Stage 55A ✅ Kickoff/Design 整合到 Pipeline + sub-task 整合 + 6+1 hooks 移除 + WorkflowEngine.cs 精簡（v3.42.0）
   ↓
Stage 55B（本 Stage）：BossInteraction 切 framework HITL（10 type）+ AppealOrchestrationService 16 處 skip 精簡 + F-α 排除條件移除（v3.43.0）— **v4 路線 9/9 達成**
   ↓
Stage 56：Trial v6 前置條件統包（Dashboard MockScenarioCard 補 22+ framework_* 場景 + FF 四十二 + FF 四十三 + Stage 48 候選 FF + WorkflowEngine.cs enum/record 殘留評估）
   ↓
Trial_v6：v4 動態架構驗證（Petra Magentic Orchestration / per-task session 行為驗證）— v4 完成後的試驗
```

> 註：Stage 55B 完成後 **v4 漸進遷移 9/9 達成 🎉**。Stage 51 試點 framework HITL pattern 全面 wire + AppealOrchestrationService 16 處 skip 精簡完成 + F-α 排除條件移除完成 — v4 路線殘留 legacy 清完。

**Stage 55B 結案後對 Stage 56 的影響**：
- v4 路線 9/9 達成 → Stage 56 純做觀察類 FF 整理（不涉及 framework path 主邏輯）
- BossInteraction 切 HITL 後 Dashboard MockScenarioCard 補 22+ framework_* 場景能涵蓋 Stage 49-55B 全部新加的 type
- WorkflowEngine.cs enum/record 殘留評估 — Stage 55B 後 caller 是否減少（影響面評估）

**Stage 55B 結案後對 Trial_v6 的影響**：
- v4 路線 9/9 達成 = Trial_v6 v4 動態架構驗證的前置條件之一
- Stage 56 完成後（FF 四十二 / 四十三修 + Dashboard MockScenarioCard 補 + Stage 48 候選 FF 修）= Trial_v6 觀察期工具完備
- Trial_v6 真實時機取決於 FF 三十六 Phase B 評估結論（Stage 56 後拍板）

---

## 實作紀錄

> Forge 結案第一段填（子項完成度對照 / Session 結案 / 關鍵設計決策 / 踩坑紀錄 / 驗收結果 / Aria 校準錨候選 — Aria 第二段填）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-04 | 初版規劃書建立（Aria）—— v4 漸進遷移第九步（拆 55A/55B 第二段）Stage 55B：BossInteraction 切 framework HITL（10 type）+ AppealOrchestrationService 16 處 skip 精簡 + F-α 排除條件移除（A1 Stage 55B 範圍 = HITL 10 type + v4 殘留 legacy 一次清完 + B1 Stage 56 = Trial v6 前置條件統包 + C1 切 10 type / 5 type 仍 fire-and-forget + Aria 拿捏 FrameworkHitlBridgeBase 抽象 + Pipeline Stage Executor RequestPort 對應 10 type + proposal Pipeline pre-stage 整合議題 6 ② + AppealOrchestration 每處 skip 分類精簡 + F-α 移除 sub-task race condition 0 + Mock 場景擴充含 5 type fire-and-forget verify）。**規劃前期已 grep**：27 處 BossInteraction caller 散在 14 service + 5 個「不適合切」type calling site 完整 context（Discord 命令層 / 通知 ack 性質確認）+ FrameworkHitlBridge.cs 353 行 + Stage 51 試點 pattern + AppealOrchestrationService 16 處 skip 位置 + F-α 排除條件 4 處 + InteractionProcessor 路由表結構 — 對齊自省點 #23 規劃前期 grep 紀律。|
