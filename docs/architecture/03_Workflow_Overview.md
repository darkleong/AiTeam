# 開發流程全景圖

> 版本：v3.0
> 建立日期：2026-04-16（v1.0）/ 2026-04-26（v2.0）/ **2026-05-07（v3.0 — v4 framework 全切換 + Stage 49-56 演進）**
> 對應系統版本：**v3.45.0（Stage 56）**
>
> 最新狀態以 [`/CHANGELOG.md`](../../CHANGELOG.md) 為準；本檔記錄當前流程設計，後續每幾個 Stage 補一次。

---

## 目錄

1. [系統架構雙層](#系統架構雙層)
2. [Agent 清單與執行方式](#agent-清單與執行方式)
3. [三種工作流程類型](#三種工作流程類型)
4. [NewFeature 完整流程](#newfeature-完整流程)
5. [BugFix 流程](#bugfix-流程)
6. [TechImprovement 流程](#techimprovement-流程)
7. [各階段詳細說明](#各階段詳細說明)
8. [v4 framework 路線（Stage 49-55B）](#v4-framework-路線stage-49-55b)
9. [Crash Recovery（framework Checkpointing）](#crash-recoveryframework-checkpointing)
10. [佇列機制與狀態管理](#佇列機制與狀態管理)
11. [Token 計費（Stage 47/56）](#token-計費stage-4756)
12. [關鍵程式碼位置索引](#關鍵程式碼位置索引)

---

## 系統架構雙層

**Stage 49-55B 完成 v4 漸進遷移 9/9 達成（2026-05-02 ~ 2026-05-05）**：底層執行引擎從 custom workflow engine 全面切換 **Microsoft Agent Framework 1.0**（hierarchical static ARCH，「換引擎不換車身」）。

| 層 | 內容 | 描述 |
|---|---|---|
| **Business 邏輯層** | NewFeature / BugFix / TechImprovement 三種流程 + 8 階段（Kickoff → Design → Dev_plan → Dev → Reviewer → QA → Doc → Notify Merge）+ Agent 角色職責 | **不變**（v3 → v4 切換不影響邏輯層） |
| **Implementation 實作層** | MS Agent Framework Pipeline workflow + 4 framework router + 4 CheckpointStore + 11 Pipeline Stage Executor + framework HITL pattern | **v4 全切換**（feature flag `UseFrameworkPipeline=true` 為 production 唯一 path） |

### Feature Flag（5 個，DB SoT 動態化）

| Flag | Stage | 狀態 | 描述 |
|---|---|---|---|
| `Workflow:UseFrameworkAppealLoop` | 49 | ✅ ON | Cody-Vera-Petra Appeal loop 切 framework |
| `Workflow:UseFrameworkKickoff` | 50 | ✅ ON | Kickoff Meeting 切 framework Group Chat（fan-out/fan-in）|
| `Workflow:UseFrameworkKickoffMidInterrupt` | 51 | ✅ ON | Kickoff 中途介入 framework HITL 試點（雙 flag 連動）|
| `Workflow:UseFrameworkDesign` | 52 | ✅ ON | Design Meeting 切 framework B3 路線 |
| `Workflow:UseFrameworkPipeline` | 53A | ✅ ON ⭐ | Macro Pipeline NewFeature 主路徑切 framework Workflow（**production 唯一 path** — Stage 55A 後 legacy path 為 dead code）|

> Flag 儲存於 `app_settings` 表，DB 優先 + `appsettings.json` fallback，Dashboard 系統設定頁可動態切換不需重啟容器。

---

## Agent 清單與執行方式

系統中 Agent 的執行路徑分為三種：

| 類型 | 說明 |
|------|------|
| **Claude Code CLI** | 透過 `claude -p` subprocess 執行，可存取本機 codebase |
| **LLM API** | 透過 `ILlmProvider.CompleteAsync()` 直接呼叫 Anthropic API（透明包裝 `TokenTrackingProvider`）|
| **純程式邏輯** | 不呼叫任何 LLM，由 C# 程式碼直接執行 |

### Agent 總覽

| Agent | 角色 | 執行方式 | 預設模型 | Claude Code 模式 | 程式碼位置 |
|-------|------|---------|------|-----------------|-----------|
| **Victoria（CEO）** | 任務分類、對話、技術顧問 | Claude Code CLI + LLM API fallback | `claude-sonnet-4-6` | `RunVictoriaAsync`（讀寫 + Git） | `Agents/CeoAgentService.cs` |
| **Cody（Dev）** | 程式碼開發、實作計畫 | Claude Code CLI | `claude-sonnet-4-6` | `RunAsync`（完整開發模式） | `Agents/DevAgentService.cs` |
| **Vera（Reviewer）** | 程式碼審查 | Claude Code CLI | `claude-sonnet-4-6` | `RunReviewAsync`（唯讀 + Bash） | `Agents/ReviewerAgentService.cs` |
| **Quinn（QA）** | 測試撰寫與執行 | Claude Code CLI | `claude-sonnet-4-6` | `RunQaAsync`（可寫測試檔） | `Agents/QaAgentService.cs` |
| **Sage（Doc）** | 收尾歸檔、CHANGELOG | Claude Code CLI | `claude-haiku-4-5` | `RunAsync`（完整開發模式） | `Agents/DocAgentService.cs` |
| **Rosa（Requirements）** | 需求分析、建立 GitHub Issues | Claude Code CLI | `claude-haiku-4-5` | `RunReadOnlyAsync`（唯讀探索） | `Agents/RequirementsAgentService.cs` |
| **Demi（Designer）** | UI/UX 規格設計 | Claude Code CLI | `claude-haiku-4-5` | `RunReadOnlyAsync`（唯讀探索） | `Agents/DesignerAgentService.cs` |
| **Petra（PM）** | 品質審核、流程協調、會議主持 | Claude Code CLI（Meeting / 申訴） + LLM API（仲裁 / 路由） | `claude-haiku-4-5` | `RunMeetingSessionAsync` / `RunReadOnlyAsync` | `Agents/Pm/PmReviewService.cs` 等 5 子服務 |
| **Rena（Release）** | 版本發布 | LLM API | `claude-sonnet-4-6` | — | `Agents/ReleaseAgentService.cs` |
| **Maya（Ops）** | 部署監控、健康檢查 | 純程式邏輯 | — | — | `Ops/OpsAgentService.cs` |

> **Provider/Model 動態化（Stage 38）**：每個 Agent 的 Provider / Model 經 Dashboard `agent_configs` 表設定，DB SoT + 5 分鐘 TTL Cache（`AgentConfigCache.cs`）。

### Claude Code 模式說明

定義在 `Agents/IClaudeCodeService.cs`：

| 模式 | 方法 | 權限 | 使用者 |
|------|------|------|--------|
| `RunAsync` | 完整開發模式 | 讀 + 寫 + Build + Git | Cody、Sage |
| `RunReadOnlyAsync` | 唯讀探索模式 | Glob / Grep / Read only | Rosa、Demi、Petra（review） |
| `RunVictoriaAsync` | CEO 模式 | 讀 + 文件 + Git | Victoria |
| `RunReviewAsync` | 審查模式 | Glob / Grep / Read + Bash | Vera |
| `RunQaAsync` | QA 模式 | 可寫測試檔 + 執行測試 | Quinn |
| `RunMeetingSessionAsync` | 會議模式 | 持久化 Session（`--session-id` / `--resume`） | Kickoff / Design 會議所有 Agent |

### Petra 雙路徑

Petra 依功能分兩種執行路徑：

| 功能 | 執行方式 | 原因 |
|------|---------|------|
| 會議主持（Kickoff / Design） / 審閱 Rosa/Demi 產出 / 審閱 Dev_plan | Claude Code CLI（`RunMeetingSessionAsync` / `RunReadOnlyAsync`） | 需要讀取 codebase 才能評估 |
| 申訴仲裁 / Vera Review 審閱 / QA 失敗路由 / 阻礙報告評估 | LLM API（`ILlmProvider.CompleteAsync`） | 只需分析文字內容，不需存取 codebase |

> **Stage 30 申訴升級**：5 個申訴環節（`RunCodyAppealAsync` / `RunVeraAppealAsync` / `ArbitrateReviewAppealAsync` / `ModifyDevPlanAsync` / `RunPetraDevPlanReassessAsync`）全升級為 Claude Code CLI 新開 session + 唯讀工具，保留 codebase 存取能力（**Stage 49 後**這些申訴已切 framework Appeal Loop workflow）。

---

## 三種工作流程類型

Victoria（CEO）接收 Christ 指令後，分類為三種：

| 類型 | 說明 | 起始步驟 | 典型場景 |
|------|------|---------|---------|
| **NewFeature** | 完整流程（含需求 + 設計階段） | Kickoff 會議 | 新功能開發 |
| **BugFix** | 精簡流程（跳過需求/設計） | Dev（直接開發） | Bug 修復 |
| **TechImprovement** | 中等流程（含計畫書） | Dev_plan（先寫計畫） | 技術改善、重構 |

定義在 `Orchestration/WorkflowEngine.cs`（Stage 55A 後僅保留 `WorkflowType` enum + `WorkflowStep` record 作為跨 23 service fundamental type，**不可移除** — 詳見 [`docs/conventions/csharp.md`](../conventions/csharp.md)「跨 service fundamental type 標記」段）。

實際 routing 邏輯由 framework Pipeline workflow 接管（Stage 53A 起），不再依賴 `WorkflowEngine.GetDecision`（Stage 55A 已移除）。

---

## NewFeature 完整流程

```
Christ 在 Discord #victoria-ceo 頻道下指令
        │
        ▼
┌─────────────────────────────────┐
│  Victoria（CEO）分類與提案      │ ← Claude Code CLI / LLM API
│  判斷為 NewFeature              │
│  產出提案書（標題 + 描述）       │
└───────────────┬─────────────────┘
                │
                ▼
        Christ 確認提案（Discord 按鈕 + Dashboard 操作中心）
                │
                ▼
┌─────────────────────────────────────┐
│  framework Pipeline Workflow 啟動    │ ← Stage 53A 起 production 唯一 path
│  PipelineFrameworkStateJson 寫入     │ ← framework Checkpointing（4 router 共用 base class）
│  ActiveOrchestration = FrameworkPipeline │
└───────────────┬─────────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│  ① Kick-off 會議                │ ← Pipeline KickoffStageExecutor → FrameworkKickoffRouter
│  Petra 主持                     │
│  Rosa / Demi / Cody / Quinn 發言│
│  產出：任務計畫書                │
└───────────────┬─────────────────┘
                │
                ▼
        Christ 確認計畫書（Discord 按鈕 + Dashboard 操作中心）
        〔continue → Pipeline ResumeAfterKickoff〕
        〔modify / restart → 沿用 legacy path〕
                │
                ▼
┌─────────────────────────────────┐
│  ② 設計會議                     │ ← Pipeline DesignStageExecutor → FrameworkDesignRouter
│  Petra 主持（條件式 Demi）       │
│  Rosa 建立 GitHub Issues        │
│  Demi 產出 UI 規格（需要時）     │
│  產出：設計規劃書                │
└───────────────┬─────────────────┘
                │
        〔consensus → Pipeline 直接繼續〕
        〔needs_adjustment → Adjustment 子流程〕
        〔split_task_proposal → 拆 task framework HITL〕
        〔escalate → Christ 確認按鈕〕
                │
                ▼
┌─────────────────────────────────┐
│  ③ Dev_plan（實作計畫書）        │ ← Pipeline DevPlanStageExecutor
│  Cody 根據設計規劃書制定         │
│  Petra 審閱                      │
│  approve → 繼續                  │
│  revise → 申訴迴圈（≤5輪 framework Appeal Loop）│
│  escalate / unable → framework HITL yield-resume │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│  ④ Dev（開發）                   │ ← Pipeline DevStageExecutor
│  Cody Clone repo → branch       │
│  寫程式碼 → dotnet build        │
│  git commit → push → 開 PR      │
│  [BLOCKED] → Petra 評估          │
│  intervention → framework HITL   │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│  ⑤ Reviewer（程式碼審查）        │ ← Pipeline ReviewerStageExecutor
│  Vera 讀取 PR diff + codebase   │
│  產出：Review 報告 + Critical 數 │
└───────────────┬─────────────────┘
                │
          Critical 數 = 0？
           ╱          ╲
         是            否
          │            │
          ▼            ▼
    ┌──────────┐  ┌──────────────────────┐
    │ Petra    │  │ framework Appeal Loop │
    │ 審閱通過 │  │ Cody 反駁 ↔ Vera 再評 │
    └────┬─────┘  └──────────┬───────────┘
         │               仍有 Critical？
         │              ╱          ╲
         │            否            是
         │             │            │
         │             ▼            ▼
         │       ┌──────────┐  ┌──────────────┐
         │       │ Petra    │  │ Petra 仲裁    │
         │       │ 審閱通過 │  │ 判定哪些必修  │
         │       └────┬─────┘  └──────┬───────┘
         │            │               │
         ├────────────┤         必修數 > 0？
         │            │        ╱          ╲
         │            │      是            否
         │            │       │            │
         │            │       ▼            │
         │            │  Pipeline DevFix   │
         │            │  StageExecutor     │
         │            │  Cody 修復         │
         │            │  → Vera 重審       │
         │            │  （≤3輪）          │
         ▼            ▼       ▼            ▼
┌─────────────────────────────────┐
│  ⑥ QA（測試）                    │ ← Pipeline QaStageExecutor
│  Quinn 撰寫測試 → dotnet test    │
│  產出：TestReport JSON          │
└───────────────┬─────────────────┘
                │
          測試結果？
        ╱    │     ╲
     passed  │   failed
       │     │      │
       │  no_tests  ▼
       │     │   ┌──────────────────────┐
       │     ▼   │ Petra QA 路由判斷     │
       │  Petra  │ code_bug → DevFix     │
       │  評估   │ back_to_reviewer      │
       │     │   │ env_or_test → 通過   │
       │     │   │ escalate → framework HITL │
       │     │   └──────────┬───────────┘
       │     │              │
       ▼     ▼              ▼
┌─────────────────────────────────┐
│  ⑦ Doc（收尾歸檔）              │ ← Pipeline DocStageExecutor
│  Sage 產出歸檔報告               │
│  更新 CHANGELOG.md              │
│  git commit → push              │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│  ⑧ NotifyMerge（完成通知）      │ ← Pipeline NotifyMergeStageExecutor
│  Discord 通知 Christ merge PR   │
│  TaskGroup.Status = "done"      │
│  Pipeline framework state 清空  │
└─────────────────────────────────┘
```

---

## BugFix 流程

BugFix 跳過需求和設計階段，Victoria 分類後直接進入 Pipeline Dev 階段。

```
Christ 下指令 → Victoria 分類為 BugFix
        │
        ▼
  Christ 雙層確認（CEO 決策確認 + Agent 執行確認）
        │
        ▼
  Pipeline Workflow 啟動（IsSubTask=false / Skip Kickoff/Design）
        │
        ▼
   ④ Dev → ⑤ Reviewer → ⑥ QA → ⑧ NotifyMerge
                        │           │
                   （同 NewFeature  （同 NewFeature
                    申訴迴圈）       QA 路由）
```

**注意：** BugFix 沒有 Doc 階段 — QA 通過後直接通知 merge。

---

## TechImprovement 流程

TechImprovement 跳過需求和設計階段，但包含 Dev_plan（實作計畫書）。

```
Christ 下指令 → Victoria 分類為 TechImprovement
        │
        ▼
  Christ 雙層確認
        │
        ▼
  Pipeline Workflow 啟動（Skip Kickoff/Design，從 DevPlanStage 啟動）
        │
        ▼
   ③ Dev_plan → ④ Dev → ⑤ Reviewer → ⑥ QA → ⑧ NotifyMerge
                                            │
                                       （無 Doc 階段）
```

---

## 各階段詳細說明

### 0. Victoria 任務分類

**觸發方式：** Christ 在 Discord `#victoria-ceo` 頻道發訊息，或在各 Agent 專屬頻道（如 `#cody-dev`）直接對話。

**Victoria 的回應分四種：**

| Action | 說明 | 後續 |
|--------|------|------|
| `reply` | 純回覆（反問、閒聊、技術討論） | 等待 Christ 下一輪回應 |
| `propose` | 判定為新功能，進入提案模式 | 顯示提案書 → Christ 確認按鈕 |
| `delegate` | 判定為可直接派工的任務 | 雙層確認 → 執行 |
| `cancel` | 取消任務 | 取消指定 TaskGroup |

**雙層確認機制（delegate 路徑）：**
1. **第一層 — CEO 決策確認**：Victoria 說明分類結果與目標 Agent
2. **第二層 — Agent 執行確認**：顯示即將執行的 Agent 與任務內容

可透過 `SkipCeoConfirm` AppSettings 跳過第一層。

**雙通道（Stage 28a/b）：** 確認按鈕同時出現在 Discord + Dashboard 操作中心 (`/interactions`)，**任一端先回覆即鎖**（樂觀鎖 `BossInteractionRepository.ExecuteUpdateAsync WHERE status='pending'`）。Stage 28b 加入文字輸入互動（`MudDialog` 收集修改意見後 submit）。

**程式碼：** `Discord/SlashCommandRouter.cs` + `Discord/ButtonCallbackRouter.cs`（Stage 36 拆解後）+ `Services/InteractionService.cs` + `Dashboard/Pages/InteractionCenter.razor`

---

### 1. Kick-off 會議（僅 NewFeature）

**主持人**：Petra（PM）
**參與者**：Rosa、Demi、Cody、Quinn（四人平行發言）
**執行方式**：Stage 50 後切 framework Group Chat（fan-out/fan-in）— 每輪 4 Agent 平行發言透過 framework `AddFanOutEdge` + `AddFanInBarrierEdge`

**流程**：
1. 每輪（Round）4 Agent 平行發言
2. Petra 收集四人意見，做出判斷：
   - `consensus`：認知對齊，產出任務計畫書
   - `needs_discussion`：再討論（最多 `KickoffMaxRounds` 輪）
   - `escalate`：需要 Christ 介入
3. 結束後 Petra 產出**任務計畫書**，存入 `TaskGroup.TaskPlan`
4. Discord/Dashboard 通知 Christ：繼續 / 修改 / 停止 / 重開

**Stage 51 中途介入試點**：Christ 在 Petra 多輪會議跑期間 Dashboard 點「中途介入」按鈕 → workflow 跑到 RequestPort 點 yield → 開 BossInteraction → Christ 回應後 `InProcessExecution.ResumeStreamingAsync` rehydrate workflow → 從 yield 點繼續跑（feature flag `UseFrameworkKickoffMidInterrupt` 連動）。

**程式碼**：
- `Workflows/Kickoff/KickoffWorkflowFactory.cs`（framework workflow 建構）
- `Orchestration/Meeting/FrameworkKickoffRouter.cs`（router 入口 + Recovery）
- `Orchestration/Meeting/KickoffMeetingService.cs`（legacy path，feature flag false 時用）
- `Orchestration/Hitl/KickoffMidInterruptTriggerStore.cs`（試點 in-memory trigger）

---

### 2. 設計會議（僅 NewFeature）

**主持人**：Petra（PM）
**參與者**：Rosa、Demi（條件式）、Cody、Quinn

**Stage 52 framework Design Meeting B3 路線**：
- Petra 判斷是否需要 Demi（needsDemi=false short-circuit pass-through）
- needs_adjustment B2 子流程（DesignAdjustmentExecutor 兩出口：approved → DesignPlanExecutor / needs_meeting → escalate）
- 拆 task 提案 router 後置（Stage 46 FF 三十五，DesignSplitProposalEvaluator 共用 helper SoT）

**前置作業**：
1. Petra 判斷是否需要 Demi
2. Rosa 分析需求，建立 GitHub Issues
3. Demi 產出 UI/UX 規格（若需要）

**設計輪次**：
1. 4 Agent 平行發言討論設計方向
2. Petra 判斷：consensus / needs_discussion / needs_adjustment / split_task_proposal / escalate

**產出**：
- 設計規劃書（`TaskGroup.DesignPlan`）
- GitHub Issues URL（`TaskGroup.IssueUrls`）
- UI 規格（`TaskGroup.UiSpecContent`）

**Stage 54 idempotency 加固**：
- `TaskGroups.LastIssueCreatedRound` (int?) round-aware marker — DesignRosaPreWork (Round 0) / DesignAdjustment (Round N) 用 round-aware check 防 Recovery 重跑同 round 重複建 GitHub Issue
- Kickoff/Design CreateInteractionAsync 用 `BossInteractionRepository.GetLatestForGroupByTypeAsync` lookup 防重複開 BossInteraction

**程式碼**：
- `Workflows/Design/`（framework workflow factory + 各 stage executor）
- `Orchestration/Meeting/FrameworkDesignRouter.cs`（router 入口 + Recovery）
- `Orchestration/Meeting/DesignMeetingService.cs`（legacy path）
- `Orchestration/Meeting/DesignSplitProposalEvaluator.cs`（拆 task 規則層 SoT）

---

### 3. Dev_plan（實作計畫書）

**執行者**：Cody（Dev Agent）
**Pipeline Stage**：`DevPlanStageExecutor`

Cody 根據前置階段的產出（設計規劃書 + Issues + UI 規格）制定詳細的實作計畫書。此階段只產出計畫，不寫程式碼。

**Petra 審閱閘門**：
- `approve`：通過，進入 Dev 開發階段
- `revise`：要求修改，進入 **framework Appeal Loop**（最多 5 輪）
- `escalate` / `unable`：framework HITL yield-resume（Stage 55B 5 routing HITL refactor — `devplan_escalate` / `devplan_unable` 兩 routing type 區分）

**程式碼**：`Agents/DevAgentService.cs` + `Workflows/Pipeline/Executors/DevPlanStageExecutor.cs` + `Agents/Pm/DevPlanAppealService.cs`

---

### 4. Dev（開發）

**執行者**：Cody（Dev Agent）
**Pipeline Stage**：`DevStageExecutor`

**執行步驟**：
1. Clone repo 到 workspace
2. 建立 feature branch
3. 寫程式碼 → `dotnet build`
4. `git add` / `commit` / `push`
5. `gh pr create` 開 Pull Request

**產出**：GitHub PR URL（`TaskGroup.DevPrUrl`）

**阻礙報告**：若 Cody 遇到無法解決的問題，回傳 `[BLOCKED]` 標記：
- Petra 評估（`HandleDevBlockerAsync` Stage 53B 升級為回傳 `BlockerDecision`）：`continue`（繼續嘗試）/ `escalate_victoria` / `escalate_boss`（framework HITL `dev_failed_intervention` routing type）
- Stage 53B：DevStage [BLOCKED] retry idempotency — `MarkGroupDoneOrInterventionAsync` 廣義化「同 AssignedAgent newer success task 取代」覆蓋 fix loop + dev_blocker 兩場景

**程式碼**：`Agents/DevAgentService.cs` + `Workflows/Pipeline/Executors/DevStageExecutor.cs`

---

### 5. Reviewer（程式碼審查）

**執行者**：Vera（Reviewer Agent）
**Pipeline Stage**：`ReviewerStageExecutor`

**執行步驟**：
1. 從 GitHub 取得 PR diff
2. 讀取 codebase（Glob / Grep / Read）
3. 分析程式碼品質、安全性、最佳實踐
4. 產出結構化 Review 報告
5. 透過 GitHub API 發布 PR Review

**審查範圍（Stage 39 起）**：`.cs` / `.razor` / `.css` 三類副檔名；`CLAUDE_Vera.md` 含 a11y / Blazor / CSS / MudBlazor 判準。

**Skipped 路徑（Stage 39）**：PR 無相關副檔名變更時，回傳 `AgentExecutionResult.Skipped(reason)`，Pipeline 走「跳過 Petra 放行」直進 QA；Dashboard 顯示 teal `#20c997`。

**後續流程（Pipeline ReviewerStageExecutor 處理）**：

```
CriticalReviewCount = 0？
     ╱          ╲
   是            否
    │            │
    ▼            ▼
 Petra         framework Appeal Loop（Stage 49 切 framework）
 審閱 Review   Cody 逐一反駁 Critical
    │          Vera 重新評估
    │            │
    │          仍有 Critical 且輪次用盡？
    │           ╱          ╲
    │         否            是
    │          │            │
    │          ▼            ▼
    │       Petra         Petra 仲裁
    │       審閱通過      判定哪些必修
    │          │            │
    ▼          ▼            ▼
          必修數 > 0 → Pipeline DevFix（≤3輪）
          必修數 = 0 → 通過，進入 QA
```

**Stage 49 framework Appeal Loop**：5 申訴環節（CodyAppeal / VeraAppeal / Arbitrate / ModifyDevPlan / DevPlanReassess）切 framework workflow（max-iter Petra arbitration + framework `AddSwitch` routing）。

**Stage 55B framework HITL routing**：Vera fix loop max iter / Dev failed → `dev_failed_intervention` / `qa_failed_intervention` 等 5 routing type yield-resume HITL（不再 fire-and-forget BossInteraction）。

**程式碼**：`Agents/ReviewerAgentService.cs` + `Workflows/Pipeline/Executors/ReviewerStageExecutor.cs` + `Workflows/Pipeline/Executors/DevFixStageExecutor.cs` + `Workflows/Appeal/`（framework Appeal workflow）

---

### 6. QA（測試）

**執行者**：Quinn（QA Agent）
**Pipeline Stage**：`QaStageExecutor`

**執行步驟**：
1. 讀取 PR diff + codebase
2. 撰寫測試（xUnit / Playwright）
3. 執行 `dotnet test`
4. 修復測試失敗（若有）
5. 產出 TestReport JSON

**TestReport JSON 格式**：
```json
{
  "status": "passed | failed | no_applicable_tests",
  "passed_tests": [...],
  "failed_tests": [...]
}
```

**後續流程（Pipeline QaStageExecutor 處理）**：

| 測試結果 | 處理 |
|---------|------|
| `passed` | 通過 → Doc（NewFeature）/ NotifyMerge（BugFix / TechImprovement） |
| `no_applicable_tests` | Petra 評估（`AssessNoApplicableTestsAsync`，LLM API）：approve 或 escalate |
| `failed` | Petra 路由判斷（`AssessQaFailureAsync`，LLM API），四條路徑 ↓ |

**QA 失敗路由**：

| 路由 | 說明 | 後續 |
|------|------|------|
| `code_bug` | 程式碼 Bug | Pipeline DevFix（跳過 Vera）→ 重新 QA |
| `back_to_reviewer` | 需要 Review 層級的修正 | Pipeline DevFix → Vera 重審 → QA |
| `env_or_test_issue` | 環境或測試本身的問題 | 視為通過 |
| `escalate` | 無法判斷 | framework HITL `qa_failed_intervention` routing type |

**QA 修復迴圈**：`QaFixRound` 計數器限制，超過上限 → escalate（framework HITL）。

**程式碼**：`Agents/QaAgentService.cs` + `Workflows/Pipeline/Executors/QaStageExecutor.cs` + `Orchestration/Qa/QaCoordinationService.cs`

---

### 7. Doc（收尾歸檔）（僅 NewFeature）

**執行者**：Sage（Doc Agent）
**Pipeline Stage**：`DocStageExecutor`

**執行步驟**：
1. 讀取 Cody ImplementationNote + Vera Review + Quinn TestReport
2. 產出歸檔報告（`TaskGroup.ArchiveContent` 寫入 DB，Stage 29-1）
3. 更新 CHANGELOG.md
4. git commit → push

**程式碼**：`Agents/DocAgentService.cs` + `Workflows/Pipeline/Executors/DocStageExecutor.cs`

---

### 8. NotifyMerge（完成通知）

**Pipeline Stage**：`NotifyMergeStageExecutor`

設定 `TaskGroup.Status = "done"` + Discord 通知 Christ merge PR + Pipeline framework state 清空（`PipelineFrameworkStateJson = null`）。

---

## v4 framework 路線（Stage 49-55B）

### 4 framework router

| Router | Stage | 對應 framework workflow | DB framework state column |
|---|---|---|---|
| `Orchestration/Appeal/FrameworkAppealRouter.cs` | 49 | Cody-Vera-Petra Appeal loop（max-iter arbitration） | `AppealFrameworkStateJson` |
| `Orchestration/Meeting/FrameworkKickoffRouter.cs` | 50 | Kickoff Group Chat（fan-out/fan-in 4 Agent + Petra）| `KickoffFrameworkStateJson` |
| `Orchestration/Meeting/FrameworkDesignRouter.cs` | 52 | Design Meeting B3 路線（條件式 Demi + needs_adjustment + 拆 task）| `DesignFrameworkStateJson` |
| `Orchestration/Meeting/FrameworkPipelineRouter.cs` | 53A | Macro Pipeline（11 stage executor 串接 NewFeature 主路徑 + 子流程）| `PipelineFrameworkStateJson` |

### Pipeline workflow 11 Stage Executor（Stage 53A-55B）

`Workflows/Pipeline/Executors/`：

- `PipelineStartExecutor.cs`（兩入口分流：parent → KickoffStage / sub-task → DevPlanStage，Stage 55A）
- `KickoffStageExecutor.cs`（Stage 55A — 整合到 Pipeline）
- `DesignStageExecutor.cs`（Stage 55A + Stage 55B SplitTaskProposal yield-resume）
- `DevPlanStageExecutor.cs`（Stage 53A + Stage 55B escalate vs unable 區分）
- `DevStageExecutor.cs`（Stage 53A + Stage 55B intervention HITL）
- `DevFixStageExecutor.cs`（Stage 53B fix loop loop back）
- `ReviewerStageExecutor.cs`（Stage 53A + 53B fix loop routing）
- `QaStageExecutor.cs`（Stage 53A + 55B intervention HITL）
- `DocStageExecutor.cs`
- `NotifyMergeStageExecutor.cs`
- `PipelineFallbackExecutor.cs`（5 fallback 點 Stage 53B 起逐步移除）

### 4 CheckpointStore（Stage 54 抽 base class）

| CheckpointStore | DB column |
|---|---|
| `Workflows/Appeal/AppealCheckpointStore.cs` | `AppealFrameworkStateJson` |
| `Workflows/Kickoff/KickoffCheckpointStore.cs` | `KickoffFrameworkStateJson` |
| `Workflows/Design/DesignCheckpointStore.cs` | `DesignFrameworkStateJson` |
| `Workflows/Pipeline/PipelineCheckpointStore.cs` | `PipelineFrameworkStateJson` |

**Stage 54**：抽 `Workflows/Common/FrameworkCheckpointStoreBase<TStore>` generic base class — 99% 重複 833 行 → 360 行 -473 淨減；abstract method `ReadJsonFromDbAsync` / `WriteJsonToDbAsync` 子類實作 column-specific 部分，其餘邏輯統一。

### framework HITL pattern（Stage 51 試點 + Stage 55B 全面 wire）

**Stage 51 試點**：`framework_kickoff_mid_interrupt` type — Christ 中途介入按鈕 → workflow yield 在 RequestPort → BossInteraction → Christ 回應 → `InProcessExecution.ResumeStreamingAsync` rehydrate workflow + SendResponseAsync → 從 yield 點繼續跑。

**Stage 55B 全面 wire**：5 routing types 切 framework HITL（`dev_failed_intervention` / `qa_failed_intervention` / `devplan_escalate` / `dev_plan_unable` / `split_task_proposal`）— Pipeline executor 從 SetIntervention end 改 yield-resume + legacy handler 加 Pipeline 分支。

**3 fire-and-forget 保留**（議題 3A 拍板）：`intervention` / `merge_notify` / `ceo_reply` 三 type 是 ack-only 通知性質，切 yield-resume 收益 = 0，繼續用既有 BossInteraction fire-and-forget pattern。

**程式碼**：`Orchestration/Hitl/FrameworkHitlBridge.cs`（Stage 51 試點）+ `Workflows/Pipeline/PipelineHitlHelper.cs`（Stage 55B Session A 共用 helper）+ 5 routing record（DevInterventionRequest/Response 等 in `Workflows/Pipeline/`）

---

## Crash Recovery（framework Checkpointing）

### Stage 54 全切 framework Checkpointing

Bot 啟動時掃 `TaskGroup.ActiveOrchestration != null` 的 group + 對應 `*FrameworkStateJson != null` 的 framework state，依 router 重啟對應 `RecoverStuck*Async` method（4 router 共用 ResumeStreamingAsync rehydrate pattern）：

| Router method | 對應 framework state |
|---|---|
| `FrameworkAppealRouter.RecoverStuckFrameworkAppealsAsync` | `AppealFrameworkStateJson != null` + `ActiveOrchestration = "FrameworkAppeal"` |
| `FrameworkKickoffRouter.RecoverStuckFrameworkKickoffsAsync` | `KickoffFrameworkStateJson != null` + `ActiveOrchestration = "FrameworkKickoff"` |
| `FrameworkDesignRouter.RecoverStuckFrameworkDesignAsync` | `DesignFrameworkStateJson != null` + `ActiveOrchestration = "FrameworkDesign"` |
| `FrameworkPipelineRouter.RecoverStuckFrameworkPipelineAsync` | `PipelineFrameworkStateJson != null` + `ActiveOrchestration = "FrameworkPipeline"` |

**4 router Recovery 共用 pattern**：
1. `LoadFromDbAsync` 從 framework state JSON rehydrate `ICheckpointStore`
2. `GetLatestCheckpoint` 取最後 checkpoint
3. `InProcessExecution.ResumeStreamingAsync` rehydrate workflow
4. `WatchStreamAsync` 等第一個 `RequestInfoEvent` / `WorkflowOutputEvent`
5. Pipeline Recovery 額外 + Agent task requeue helper（Stage 53A follow-up #4 議題 12 升級）

### F-α 排除條件（Stage 53A）

4 個既有 router Recovery 全加 `g.PipelineFrameworkStateJson == null` 排除條件 — 避免 4 marker 共存時 Recovery 篩選優先級 collision（Pipeline 是 macro-orchestration 包含 inner Kickoff/Design/Appeal）。

### Dashboard 重試（Stage 31）

Failed / Cancelled 的 TaskItem 在 PipelineView + TaskCenter 列表頁有 `🔁 重試` 按鈕，呼叫 `AgentQueueService.RequeueTaskAsync` → Bot Internal API 重新 enqueue。

### 既有 ActiveOrchestration 5 種值（v3 legacy，Stage 49+ 改為 framework path 標記）

| 值 | 對應階段 |
|---|---|
| `Kickoff` / `Design` | legacy 會議（feature flag false 時用，現 production 不走） |
| `ReviewAppeal` / `DevPlanAppeal` / `QaRouting` | legacy 申訴/路由（Stage 49+ 切 framework workflow 後極少用） |
| `FrameworkAppeal` / `FrameworkKickoff` / `FrameworkDesign` / `FrameworkPipeline` | **Stage 49-53A 新加 framework path marker**（production 主用）|

---

## 佇列機制與狀態管理

### Per-Agent 佇列（Stage 27a）

所有 Agent 任務透過 DB-as-Queue 機制排隊執行，每個 Agent 一次只執行一個任務。

**核心元件**：
- `AgentQueueService`（Singleton）：Enqueue / Dequeue / CTS 管理 + RequeueTaskAsync
- `AgentQueueProcessor`（BackgroundService）：3 秒輪詢 + Signal 喚醒

**Semaphore 分組**（8 組）：

| Semaphore Key | 對應的 Agent |
|--------------|-------------|
| `Dev` | Dev + Dev_plan（共用） |
| `Reviewer` | Reviewer |
| `QA` | QA |
| `Doc` | Doc |
| `Requirements` | Requirements |
| `Designer` | Designer |
| `Release` | Release |
| `Ops` | Ops |

**PM（Petra）不在佇列中** — Petra 是 framework Pipeline workflow 內的 stage executor 同步 await（不是獨立 BackgroundService）。

### Agent 狀態管理（Stage 27b）

每個 Agent 四種狀態，儲存 `AppSettings` 表（key `AgentState:{executorKey}`）：Active / Paused / Stopping / Stopped。

**Discord 指令**：`/pause` / `/resume` / `/stop-all` / `/resume-all` / `/queue`

**Dashboard 控制（Stage 33）**：Agent 狀態卡內建 pause/resume + `GlobalQueueControlCard` 全域緊急停止 + 佇列深度 Chip + SignalR 即時更新。共用 `AgentQueueControlService`（Discord + Dashboard 先到先贏）。

### TaskGroup 暫停（Stage 45 FF 三十四）

`TaskGroup.PausedAt` + `TaskGroup.PausePoint` 雙欄位 — Christ 可在 Dashboard 暫停 group（被動延遲生效，下個 stage 啟動時 check）+ 跨 BossInteraction 暫停獨立兩機制。

---

## Token 計費（Stage 47/56）

### Token SoT（Stage 47）

DB SoT 統一動態化（`app_settings` + `agent_configs`）：

| 設定類型 | SoT | fallback |
|---|---|---|
| Token limit（全域 / per-agent） | DB（`app_settings` 全域 / `agent_configs.DailyTokenLimitK + MonthlyTokenLimitK` per-agent）| `appsettings.json`（env 已移除）|
| Token pricing（estimated cost）| `app_settings`「TokenPricing:InputPer1kUsd」/ 「TokenPricing:OutputPer1kUsd」（單一 Sonnet 預設費率，client-side estimation）| 同 |

### Token 寫入路徑（Stage 56）

| Path | 寫入點 | TotalCostUsd 來源 | IsEstimated |
|---|---|---|---|
| **Path A — CLI subprocess**（16 caller via TokenLogService）| `Services/TokenLogService.cs` `LogCliUsageAsync` | CLI `--output-format json` 內 `total_cost_usd`（Stage 56 兼容多欄位名 `total_cost_usd` / `cost_usd` / `usage.cost_usd`）；找不到時 fallback `TokenCostEstimator` | false（CLI 真實值）/ true（fallback estimation）|
| **Path B — Anthropic API direct**（Rosa/Demi/Sage/Release/Ops 等走 `ILlmProvider.CompleteAsync`）| `Agents/TokenTrackingProvider.cs` 中央寫入點（line 113-126）| `TokenCostEstimator.Estimate(model, input, output, cacheCreate=0, cacheRead=0)` per-model 4 欄位估算 | true（Anthropic SDK 不回 cost，全估算）|

### TokenCostEstimator（Stage 56）

`Agents/TokenCostEstimator.cs`：hardcoded per-model `Dictionary<string, ModelPricing>` — Opus / Sonnet / Haiku × input / output / cache_creation / cache_read 12 數字（Anthropic 官方公開 pricing）。Model 升級時改 const dict 一處。

### Token 守門

`Agents/TokenTrackingProvider.cs` 四道關卡：單次請求 / per-agent 日 / per-agent 月 / 全域月。任一觸發 → `InvalidOperationException` + Discord alert + 所有 LLM 呼叫暫停。

---

## 各流程類型的階段對照表

| 階段 | NewFeature | BugFix | TechImprovement | 參與 Agent | Pipeline Stage Executor |
|------|:----------:|:------:|:---------------:|-----------|---------|
| Victoria 分類 | ✅ | ✅ | ✅ | Victoria | — |
| Christ 確認 | 提案確認 | 雙層確認 | 雙層確認 | — | — |
| ① Kick-off 會議 | ✅ | — | — | Petra + Rosa + Demi + Cody + Quinn | `KickoffStageExecutor` |
| Christ 確認計畫書 | ✅ | — | — | — | — |
| ② 設計會議 | ✅ | — | — | Petra + Rosa + Demi（條件式）+ Cody + Quinn | `DesignStageExecutor` |
| ③ Dev_plan | ✅ | — | ✅ | Cody → Petra | `DevPlanStageExecutor` |
| ④ Dev | ✅ | ✅ | ✅ | Cody | `DevStageExecutor` |
| ⑤ Reviewer | ✅ | ✅ | ✅ | Vera → Petra | `ReviewerStageExecutor` + `DevFixStageExecutor` |
| 申訴迴圈 | ✅ | ✅ | ✅ | Cody + Vera + Petra（仲裁）| framework Appeal Loop（`Workflows/Appeal/`）|
| ⑥ QA | ✅ | ✅ | ✅ | Quinn → Petra | `QaStageExecutor` |
| ⑦ Doc | ✅ | — | — | Sage | `DocStageExecutor` |
| ⑧ NotifyMerge | ✅ | ✅ | ✅ | — | `NotifyMergeStageExecutor` |

---

## 關鍵程式碼位置索引

### v4 framework 路線（Stage 49-55B）

| 功能 | 檔案 |
|---|---|
| Pipeline framework workflow | `Workflows/Pipeline/PipelineWorkflowFactory.cs` |
| Pipeline 11 stage executor | `Workflows/Pipeline/Executors/{Pipeline,Kickoff,Design,DevPlan,Dev,DevFix,Reviewer,Qa,Doc,NotifyMerge,PipelineFallback}StageExecutor.cs` |
| Pipeline state + checkpoint | `Workflows/Pipeline/PipelineState.cs` + `PipelineCheckpointStore.cs` |
| Pipeline HITL helper | `Workflows/Pipeline/PipelineHitlHelper.cs`（Stage 55B Session A）|
| 4 framework router | `Orchestration/Appeal/FrameworkAppealRouter.cs` + `Orchestration/Meeting/Framework{Kickoff,Design,Pipeline}Router.cs` |
| 4 CheckpointStore base class | `Workflows/Common/FrameworkCheckpointStoreBase.cs`（Stage 54）|
| Kickoff / Design / Appeal workflow | `Workflows/{Kickoff,Design,Appeal}/` 各子資料夾 |
| framework HITL（Stage 51 試點 + Stage 55B 全面 wire）| `Orchestration/Hitl/FrameworkHitlBridge.cs` + `KickoffMidInterruptTriggerStore.cs` |
| Feature flag resolver | `Configuration/WorkflowSettings.cs` + `Configuration/WorkflowSettingsResolver.cs` |

### Business 邏輯層

| 功能 | 檔案 | 說明 |
|------|------|------|
| 流程協調主入口 | `Orchestration/TaskGroupService.cs` | 對外 API + 路由到子 OrchestrationService |
| 會議協調 | `Orchestration/Meeting/MeetingOrchestrationService.cs` | Kickoff / Design 入口分流（feature flag → framework path / legacy path）|
| 申訴協調 | `Orchestration/Appeal/AppealOrchestrationService.cs` | Review Appeal + Dev_plan Appeal 入口分流 |
| QA 協調 | `Orchestration/Qa/QaCoordinationService.cs` | QA 路由判斷 + 修復迴圈 |
| 提案確認協調 | `Orchestration/Proposal/ProposalConfirmationService.cs` | 提案核准 / 修改 / 取消 |
| Workflow type 定義 | `Orchestration/WorkflowEngine.cs` | `WorkflowType` enum + `WorkflowStep` record（Stage 55A 後僅保留 fundamental type）|
| Kickoff / Design legacy service | `Orchestration/Meeting/{Kickoff,Design}MeetingService.cs` | feature flag false 時使用 |
| Petra 子模組 | `Agents/Pm/{PmReviewService,ReviewAppealService,DevPlanAppealService,PmRoutingService,PmAgentCommons}.cs` | Petra 五項職責（Stage 35 拆解後）|

### 通用基礎設施

| 功能 | 檔案 | 說明 |
|------|------|------|
| 佇列處理器 | `Orchestration/AgentQueueProcessor.cs` | Semaphore 分組、輪詢 + Signal、狀態檢查 |
| 佇列服務 | `Orchestration/AgentQueueService.cs` | Enqueue / Dequeue / CTS / RequeueTaskAsync |
| 佇列控制（Stage 33）| `Services/AgentQueueControlService.cs` | Discord + Dashboard 共用 pause/resume/stop |
| Discord 指令分派 | `Discord/SlashCommandRouter.cs` + `ButtonCallbackRouter.cs` + `PendingConfirmationStore.cs` | Stage 36 拆解後 |
| 雙向操作中心 | `Services/InteractionService.cs` + `Dashboard/Pages/InteractionCenter.razor` + `Bot/InteractionProcessor.cs` | BossInteraction 雙通道（樂觀鎖先到先贏）|
| Mock Mode | `Agents/MockClaudeCodeService.cs` + `Services/MockScenarioService.cs` | 動態 Delay + Dashboard `/mock` 卡片（Stage 56 補全 33 framework_* 場景）|
| Provider/Model 動態化 | `Services/AgentConfigCache.cs` + `Configuration/LlmModels.cs` | DB SoT + 5 分鐘 TTL Cache + 常數白名單 |
| Token 計費（Stage 47/56）| `Agents/TokenTrackingProvider.cs` + `Services/TokenLogService.cs` + `Agents/TokenCostEstimator.cs` | 兩 path 中央寫入點 + IsEstimated flag + per-model hardcoded pricing |
| Claude Code 介面 | `Agents/IClaudeCodeService.cs` | 6 種執行模式（含 Stage 25a `RunMeetingSessionAsync`）|
| Agent 設定 | DB `agent_configs` 表 + `appsettings.json` 啟動 seed | Provider / Model / 各項參數 |

---

## 版本歷史

| 日期 | 版本 | 內容 |
|------|------|------|
| 2026-04-16 | v1.0 | 初版建立 — 記錄 v3.13.0 完整流程 |
| 2026-04-26 | v2.0 | 補 Stage 28-39 演進：Dashboard 雙向操作中心 / 申訴迴圈 LLM API → Claude Code CLI / Crash Recovery 全面涵蓋 / Reviewer Skipped 結果型別 / Mock 動態 Delay / Agent 狀態卡 2.0 + Dashboard 佇列控制 / FF 二十大檔案拆解 / Provider/Model 動態化 / Vera 審查擴及 razor/css |
| 2026-05-07 | **v3.0** | **v4 framework 全切換 + Stage 49-56 演進**：v4 漸進遷移 9/9 達成（MS Agent Framework 1.0 切換）+ 5 feature flag（4 ON for production）+ 4 framework router + 4 CheckpointStore + Stage 54 base class + 11 Pipeline Stage Executor + Stage 51/55B framework HITL pattern（試點 + 全面 wire 5 routing types）+ Stage 54 Crash Recovery 全切 framework Checkpointing + Stage 47/56 Token SoT + TokenCostEstimator + IsEstimated flag + Stage 55A WorkflowEngine 精簡。系統架構雙層（business 邏輯不變 + implementation 層全切 v4 framework）|
