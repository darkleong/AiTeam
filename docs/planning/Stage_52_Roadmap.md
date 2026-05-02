# Stage 52：v4 漸進遷移第四步 — Design Meeting 切 framework Workflow（fan-out/fan-in + 條件式 Demi + needs_adjustment 子流程）

> 對應 Future Feature：v4 漸進遷移 7 Stage 路線第四步（議題 A 拆 Stage 後從 6 Stage 路線擴為 7 Stage）— 不對應特定 active FF（v4 路線進入 Stage 工作模式，按 Stage 走不開新 FF）
> 對應版本：**v3.38.0**（v4 漸進遷移第四個產生版本變動的 Stage）
> 建立日期：2026-05-02
> 狀態：📋 計劃書建立完成，待 Forge 開工
> 文件版本：v1.0

---

## 概述

**戰略背景**：[Stage 48 FF 四十九 spike](Stage_48_Roadmap.md) 結論啟動 v4 漸進遷移路線。**Stage 49 v3.35.0**（Cody-Vera-Petra Appeal loop）+ **Stage 50 v3.36.0**（Kickoff Meeting fan-out/fan-in）+ **Stage 51 v3.37.0**（HITL pattern 試點）完成前三步。**Stage 52 是 v4 漸進遷移第四步** — Design Meeting 切 framework Workflow，引入 Design 特有的「條件式 Demi」+「needs_adjustment 子流程」+「拆 task 提案後置」三層 Stage 50 沒踩過的拓撲擴展。

**範圍拆解（議題 A 拍板）**：原 v4 路線 Stage 52 預計含「Design Meeting + WorkflowEngine 整體 hardcoded pipeline → Workflow Builder（最大遷移點）」一氣呵成，Aria 規劃時拆 Stage：
- **Stage 52**（本 Stage）：Design Meeting B3 路線 only（micro-orchestration，單一 Meeting 內部）
- **Stage 53**（後續）：WorkflowEngine pipeline → framework Workflow（macro-orchestration，整個任務 8 階段調度表）
- **Stage 54**：Crash Recovery 全面切 framework Checkpointing
- **Stage 55**：收尾 + token middleware + production 切換 + 真正切 BossInteraction 到 framework HITL（Stage 51 試點 know-how 全面 wire）
- v4 路線從 6 Stage 變 7 Stage（拆 Stage 後守混合型 Stage 倍率穩定區間 ×0.96-1.25）

**Design Meeting vs Kickoff Meeting 拓撲差異**：

| 階段 | Kickoff（Stage 50 已驗）| Design Meeting（Stage 52 unknown）|
|---|---|---|
| 前置作業 | 無 | **線性串聯 + 條件式 Demi**（Petra judge → Rosa parallel + Demi 條件式 → 進入 round loop）|
| 主迴圈 | fan-out 4 Agent → Petra 整理 → AddSwitch loop（3 出口：consensus / needs_discussion loop / escalate）| fan-out 4 Agent（Demi 條件式）→ Petra 整理 → AddSwitch loop（**4 出口：consensus / needs_discussion loop / needs_adjustment B2 子 Executor / escalate**）|
| 後置 | router 跑 confirmation embed | router 跑 **split proposal 評估**（C2 拍板）+ confirmation embed |
| 副作用 | 無 | **GitHub Issue 建立**（Rosa pre-work + needs_adjustment Rosa 調整）|

**v4 路線第四步風險預警**：
- **條件式 fan-out 拓撲**（needsDemi=false 時 Demi Executor 怎麼跳過）— framework 1.3.0 拓撲表達待 spike F1 驗證
- **前置作業 → 主迴圈 round loop 串接 pattern**（兩段拓撲是否同一 WorkflowBuilder 內串、StartExecutor 怎麼設、前置 state 怎麼帶到 round loop）— spike F2 驗證
- **needs_adjustment B2 子 Executor 兩出口模式**（approved → produce plan / needs_meeting → loop back round+1）— 對齊 Stage 50 KickoffPlanExecutor / KickoffEscalateExecutor 兩出口慣例
- **拆 task 提案 router 後置處理**（C2 抽 helper SoT 給 framework + legacy 共用）— Stage 46-FF 三十五 戰略級機制不能漂移
- **Stage 50 fan-out 拓撲三件套紀律延續**（partial class + [SendsMessage] + RunStreamingAsync + WatchStreamAsync foreach）

→ feature flag 為主要安全網（`Workflow:UseFrameworkDesign` 預設 false，對齊 Stage 49/50/51 慣例）。

---

## 設計決策（Christ 2026-05-02 拍板）

### 主路線拍板（戰略級）

| 議題 | 拍板 | 替代方案 |
|---|---|---|
| **議題 A：Stage 52 範圍** | **A2 拆 Stage**：Stage 52 = Design Meeting B3 only（micro-orchestration），WorkflowEngine pipeline 拆到 Stage 53 獨立做 | A1 合一 Stage（×1.4-1.7 + 必拆 2-3 session + 兩層耦合 spike 難獨立驗證）/ A3 反序先 macro 後 micro（Design 在 macro 中變 wrapper node，事後重做風險）|
| **議題 B：needs_adjustment 子流程整合** | **B2：Single-Executor wrapper（兩出口 fan-out）** — 新建 `DesignAdjustmentExecutor`，內部跑抽出的 helper（Rosa adjust + Demi adjust + Petra eval 三 LLM call + GitHub I/O 都在 Executor 內），出口分 approved（產 plan）/ needs_meeting（loop back round+1）對齊 Stage 50 KickoffPlanExecutor / KickoffEscalateExecutor 兩出口慣例 | B1 Subworkflow pattern（framework 1.3.0 subworkflow 支援度未驗 + 與 Stage 50 慣例不同）/ B3 拆 3 Executor（規模放大違反議題 A 拆 Stage 守 ×0.96-1.25 區間精神）/ B4 legacy 留底（混合架構 Stage 53 還要再動）|
| **議題 C：Stage 46 拆 task 提案後置整合方式** | **C2：Workflow 結束後 router 跑** — framework workflow 跑到 consensus → 產 designPlan → WorkflowOutputEvent → router 收 result 後 call 抽出的 helper（同 Petra session）；helper 給 framework + legacy 共用 SoT（避免 Stage 46-FF 三十五 戰略級機制雙寫漂移） | C1 workflow 內最後一個 node（拓撲多一層 + escalate 路徑要 AddSwitch 跳過 + Mock 場景多一層覆蓋）/ C3 直接 call 既有 method 不抽（不抽 SoT 雙寫漂移風險）|
| **議題 D：spike 第一步驗證範圍** | **D2：兩項精準驗證** — F1 framework 條件式 fan-out 表達（needsDemi=false 時 Demi Executor 怎麼跳過：AddSwitch 兩條分支 vs Executor 內 short-circuit `IsActive` flag vs 動態建構不同 workflow instance）+ F2 前置作業 → 主迴圈 round loop 串接 pattern（兩段拓撲同一 WorkflowBuilder 內串 / StartExecutor 怎麼設 / 前置 state 怎麼帶到 round loop） | D1 三項全驗（B2/C2 都 0 新機制 → over-spike）/ D3 一項驗證 F1 only（Stage 50 教訓「拓撲假設可推導」會踩坑，過度樂觀）|
| **議題 E：feature flag 顆粒度** | **E1 獨立 flag `Workflow:UseFrameworkDesign`**（對齊 Stage 49/50/51 慣例，預設 false，與 Stage 49/50/51 三 flag 完全獨立 — Design 跟 Kickoff 在 pipeline 上是兩個獨立節點，不繼承 Stage 51 雙 flag 連動設計）| E2 與 Stage 50 連動 / E3 細顆粒度（前置 / 主迴圈 / split proposal 各一個 flag，過度設計） |
| **議題 F：Mock 場景設計（F-mid 6 場景）** | `framework_design_consensus_round1` / `framework_design_consensus_round2` / `framework_design_needs_adjustment_approved` ⭐ / `framework_design_needs_adjustment_needs_meeting` ⭐ / `framework_design_no_demi` ⭐ / `framework_design_crash_recovery_during_round` — Design 特有 3 條 ⭐ 全覆蓋；max_iter / escalate 由 Forge 改 prompt 參數代驗（機制簡單，不獨立 Mock）；crash_recovery_during_adjustment 跟議題 G 互鎖暫不開 | F-min 5 場景（漏 Crash Recovery）/ F-full 9 場景（over-coverage，Trial 會跑完整流程） |
| **議題 G：GitHub Issue 重複建立的冪等性** | **G1：沿用 legacy 行為（重複建立不做冪等保護）+ 立 FF 觀察** — Stage 52 Roadmap 明寫 trade-off + 立新 FF「Design Meeting Crash Recovery Issue 重複建立」未來獨立 Stage / 搭車修；Crash 頻率不高（Christ 觀察）+ Trial 真實踩到再升級優先級 | G2 framework state 寫已建 Issue URL list（規模 +50-100 行 + Mock 場景 + 違反議題 A 拆 Stage 精神）/ G3 Rosa Executor GitHub API title 去重（戰略級「修根因 > 打補丁」但 +100-200 行 + helper 抽出 + framework/legacy 雙端 wire，不該擠進 Stage 52）|
| **議題 H：ModifyDesignPlanAsync legacy 流程接點** | **H1：沿用 Stage 50 C2 + Stage 51 A3 試點精神** — DesignState 加 `PetraSessionId` 欄位（隨 framework state 序列化，對齊 Stage 51 KickoffTaskId pattern）+ framework Design router 結束時從 framework state 取 PetraSessionId → 寫進**既有** `PendingConfirmationStore._pendingDesignConfirmations`（legacy + framework 雙路徑共用同一個 store）+ `ButtonCallbackRouter` 既有 Modify 按鈕 callback 不變 → 走 legacy `ModifyDesignPlanAsync` 不變 | H1b 把 PendingConfirmationStore 改 DB 持久化（修 Bot 重啟丟失既有 bug，FF 級工作）/ H2 Stage 52 同時遷 Modify 流程（違反 Stage 51 A3 試點「不切既有 BossInteraction 10+ type」拍板）|

### Aria 拿捏（已決）

| # | 議題 | 決定 |
|---|---|---|
| 1 | DB schema | **加 1 個欄位** `task_groups.DesignFrameworkStateJson`（對齊 Stage 50 `KickoffFrameworkStateJson` pattern，Migration `Stage52TaskGroupDesignFrameworkState`）；PetraSessionId 寫進 DesignState 隨 framework state 序列化（不加額外 column）|
| 2 | ActiveOrchestration 雙 marker | 設 `"FrameworkDesign"`（對齊 Stage 50 `"FrameworkKickoff"` pattern，與 KickoffFrameworkStateJson 搭配區隔 legacy/framework Crash Recovery）|
| 3 | 入口分流位置 | `MeetingOrchestrationService.RunDesignPhaseAsync` line 301 既有 `await designMeetingService.RunDesignMeetingAsync(...)` 呼叫處改為 if (UseFrameworkDesign) call FrameworkDesignRouter else legacy（對齊 Stage 50 同 service line 60 Kickoff 分流 pattern）|
| 4 | Crash Recovery hook | `AgentQueueProcessor` line 81 之後加 `await frameworkDesignRouter.RecoverStuckFrameworkDesignAsync(stoppingToken)` — 順序在 Stage 49/50 hook 之後 / legacy `RecoverStuckOrchestrationsAsync` 之前（沿用 Stage 50 踩坑紀錄 #4 hook 順序紀律）|
| 5 | legacy `RecoverStuckOrchestrationsAsync` 排除條件 | 加 `&& g.DesignFrameworkStateJson == null`（對齊 Stage 50 既有排除條件 KickoffFrameworkStateJson）|
| 6 | Token 計費 | 沿用 `MeetingCommons.RunAgentTurnAsync` 既有機制 — `meetingType="Design"` + `round=group.DesignRound` + `tokenLogService` 三參數對齊 Stage 44 既有，Executor 內呼叫 RunAgentTurnAsync 時帶完整三參數，token_logs Round 欄位對齊既有行為 |
| 7 | CLAUDE_*.md prompt | 不動（沿用 Stage 49/50/51 慣例 — framework 是 orchestration 控制層機制，不影響 Agent prompt 或 CLAUDE.md 行為規範）|
| 8 | DesignerAgentService 不動 | DesignerAgentService 跟 DesignMeetingService 是兩個獨立 service（DesignerAgentService 是 Demi 個別 Agent 的 Claude Code wrapper，不在 Design Meeting 流程內），Stage 52 不動 |
| 9 | DesignAdjustmentExecutor session 邊界 | DesignAdjustmentExecutor 內部跑 Rosa adjust + Demi adjust（含 DemiSessionId 動態建立邊界 — 初始 needsDemi=false 但會議揭露需要 UI 規格）+ Petra eval 三 LLM call。session_id 透過 framework state（DesignState）傳遞，DemiSessionId nullable 處理對齊 legacy line 487-500 邊界 |
| 10 | Mock 場景觸發機制 | 對齊 Stage 50 `MockClaudeCodeService.FailScenario` static 傳遞 scenario key，agent prompt 識別判別角色 + decision JSON 回應 mock 化；MockScenarioService 加 6 個 `framework_design_*` case 對應 F-mid 6 場景 |
| 11 | 抽 helper 位置 | `DesignMeetingService.EvaluateAndProposeSplitAsync` 抽成 public method（或抽到獨立 `DesignSplitProposalEvaluator` service 對齊 Stage 50 `KickoffPrompts` 抽出 SoT 慣例 — 具體位置 Forge Plan Mode 拍板，但必須 framework + legacy 共用 SoT）|
| 12 | 拓撲收尾 fallback | framework Workflow 跑失敗 → 不 fallback to legacy（避免 Petra session 雙重佔用）+ 改發 Discord error embed + 標 group failed，由 Christ 線下決定 retry — 對齊 Stage 50 既有 fallback 拍板 |

### Stage 52 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **0** | **Spike 第一步：D2 兩項驗證**（Forge Plan Mode 第一步） — F1 條件式拓撲表達 + F2 前置 → 主迴圈串接 pattern | XS-S |
| **1** | feature flag `Workflow:UseFrameworkDesign` + `WorkflowSettings` / `WorkflowSettingsResolver` 擴充 | XS |
| **2** | DB schema：`task_groups.DesignFrameworkStateJson` 欄位 + Migration `Stage52TaskGroupDesignFrameworkState` + `Entities.cs` + `legacy RecoverStuckOrchestrationsAsync` 排除條件 | XS |
| **3** | DesignState 設計（含 PetraSessionId / DemiSessionId nullable / 3 IssuesJson 相關欄位 / 3 Petra round state 欄位 / DesignTaskId）+ 隨 framework state 序列化 pattern | S |
| **4** | `KickoffPrompts` 抽出 SoT 模式對齊 — 從 `DesignMeetingService` 抽出 `DesignPrompts.cs`（Petra judge / Rosa pre-work / Demi pre-work / 4 Agent meeting prompt / Petra round / Petra plan / Petra adjustment eval / 8 個 builder 方法）給 framework + legacy 雙路徑共用 SoT | M |
| **5** | 抽 `EvaluateAndProposeSplitAsync` 成共用 helper（議題 11，具體放哪檔 Forge Plan Mode 拍板） | XS |
| **6** | `DesignWorkflowFactory` + 8 個 Executor（DesignStartExecutor / DesignPetraJudgeExecutor / DesignRosaPreWorkExecutor / DesignDemiPreWorkExecutor / DesignAgentExecutor[Rosa/Demi/Cody/Quinn] 4 個 + DesignAggregator + DesignPetraExecutor + DesignAdjustmentExecutor + DesignPlanExecutor + DesignEscalateExecutor）+ Workflow 拓撲（兩段：前置 + 主迴圈 round loop）| L |
| **7** | `DesignCheckpointStore`（對齊 Stage 49/50 KickoffCheckpointStore pattern，符合「3 次再抽象」原則第 3 次出現相似 pattern → Stage 53 評估抽 base class）| S |
| **8** | `FrameworkDesignRouter`（含 `HandleDesignMeetingAsync` 主入口 + `RecoverStuckFrameworkDesignAsync` Crash Recovery + 後置 split proposal helper call + Discord embed/buttons + `RegisterDesignConfirmation` + `InteractionService.CreateInteractionAsync` 對齊 legacy）| L |
| **9** | 入口分流：`MeetingOrchestrationService.RunDesignPhaseAsync` 加 `if (UseFrameworkDesign) → FrameworkDesignRouter` else legacy 路徑 | XS |
| **10** | Crash Recovery 整合：`AgentQueueProcessor` 加 hook + Dashboard SystemSettings 加第四 toggle + Mock 場景擴充 6 個 `framework_design_*` + Forge 自驗 6 場景 | M |
| **11** | Version bump v3.38.0 + 結案文件（Roadmap 實作紀錄章節 + CHANGELOG / Future_Feature 同步交給 Aria 結案第二段）| XS |

> **不寫工時估算**（workflow_aria.md 第三節 A 第 4 條）— 各子項規模見上表 XS/S/M/L 範圍描述。

---

## 子項 0：Spike 第一步 — D2 兩項驗證

### 驗證項目

| # | 驗證題 | 驗證方法 | 影響 |
|---|---|---|---|
| **F1** | framework 條件式 fan-out 表達 | 讀 `Microsoft.Agents.AI.Workflows` 1.3.0 xml doc + Microsoft Learn `agent-framework/workflows/conditional-routing` C# sample + GitHub `microsoft/agent-framework` 對應 sample；驗 needsDemi=false 時 Demi Executor 怎麼跳過：① AddSwitch 兩條拓撲分支 ② Executor 內 short-circuit `IsActive` flag pass-through ③ 動態建構不同 workflow instance | **影響子項 6 拓撲設計**：① framework 原生支援條件 fan-out → 用 AddSwitch 分支；② 不支援 → fallback Executor 內 short-circuit（needsDemi=false 時 Demi Executor 收 message 直接 SendMessageAsync(empty 字串 / 空 verdict) pass-through 不跑 LLM call，等同 no-op）|
| **F2** | 前置作業 → 主迴圈 round loop 串接 pattern | 讀 framework 1.3.0 docs + Stage 50 KickoffWorkflowFactory pattern reference；驗兩段拓撲是否同一 WorkflowBuilder 內串 / StartExecutor 怎麼設 / 前置 state 怎麼帶到 round loop（DesignState 持有 issuesJson / uiSpecContent / DemiSessionId 等前置作業產出，這些 state 怎麼從 DesignRosaPreWorkExecutor 流到 main loop start）| **影響子項 6 Workflow 拓撲架構**：① 同一 Builder 內串接 → 前置 Executors AddEdge 串接到 mainStart node（簡單），② 兩個獨立 Workflow → 前置作業 Workflow 跑完後 router 串接到主迴圈 Workflow（規模 +30%，DesignFrameworkStateJson 序列化邊界要重設計）|

### Spike 結案產出

- **路線拍板紀錄**寫進 Forge Plan Mode plan 檔最前段
- **2 項驗證證據**（NuGet 文件引用 / Microsoft Learn / GitHub sample 引用 / 必要時建小 spike 程式片段）
- **設計風險升級或降級**：依 spike 結果調整風險點 R1-R6 評估

### Spike 階段失敗條件

若以下任一發生 → 暫停 Stage 52 + 回報 Christ 評估：
- F1 framework 不支援條件式拓撲 + Executor 內 short-circuit fallback 也踩到 framework type validation 限制（如 SendMessageAsync(null) 不被允許）
- F2 兩段拓撲在同一 WorkflowBuilder 內無法串接 + 兩個獨立 Workflow fallback 對 DesignFrameworkStateJson 序列化邊界要 fundamental 重新設計

---

## 子項 1：feature flag 擴充

### 實作項目

**位置**：`src/AiTeam.Bot/Configuration/WorkflowSettings.cs` + `WorkflowSettingsResolver.cs`

**WorkflowSettings 新增屬性**：
- `bool UseFrameworkDesign { get; set; } = false;`

**WorkflowSettingsResolver 新增 method**：
- `Task<bool> GetUseFrameworkDesignAsync(CancellationToken ct = default)` — 對齊既有 `GetUseFrameworkKickoffAsync` pattern

**AppSettings key**：`Workflow:UseFrameworkDesign`，預設 `false`

**獨立性說明**：與 Stage 49 UseFrameworkAppealLoop / Stage 50 UseFrameworkKickoff / Stage 51 UseFrameworkKickoffMidInterrupt 完全獨立（pipeline 上 Design 跟 Kickoff 是兩個獨立節點，不繼承 Stage 51 雙 flag 連動設計理由）。

---

## 子項 2：DB schema

### 實作項目

**Entities.cs 新欄位**：
```
public string? DesignFrameworkStateJson { get; set; }
```

**Migration 名稱**：`Stage52TaskGroupDesignFrameworkState`

**legacy 排除條件擴充**：`RecoverStuckOrchestrationsAsync` 篩選條件加 `&& g.DesignFrameworkStateJson == null`（避免 legacy/framework Design 雙系統 collision）。

---

## 子項 3：DesignState 設計

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Design/DesignState.cs`（新檔）

**DesignState 欄位**（隨 framework state 序列化進 task_groups.DesignFrameworkStateJson）：

| 欄位 | 型別 | 說明 |
|---|---|---|
| GroupId | Guid | TaskGroup 識別 |
| DesignTaskId | Guid | designTask.Id（router 結束時跨 scope 取用，對齊 Stage 51 KickoffTaskId pattern） |
| Owner / Repo | string | GitHub repo 識別 |
| WorkingDir | string | clone 目錄路徑 |
| TaskPlan | string | 從 group.TaskPlan 帶入 |
| MaxRounds | int | designMaxRounds（從 AppSettings 載入） |
| Round | int | 當前輪次（從 1 開始） |
| PetraSessionId | string | Guid.NewGuid().ToString() — 議題 H1 跨 framework + legacy 共用 |
| RosaSessionId | string | Guid.NewGuid().ToString() |
| DemiSessionId | string? | nullable — 初始 null（needsDemi 判斷後才建立） |
| CodySessionId | string | Guid.NewGuid().ToString() |
| QuinnSessionId | string | Guid.NewGuid().ToString() |
| NeedsDemi | bool | Petra judge 結果，影響條件式拓撲 |
| IssuesJson | string | Rosa pre-work 產出（"[]" 預設） |
| IssueUrls | string? | nullable — JSON string array |
| UiSpecContent | string? | nullable — Demi pre-work 產出 |
| LastPetraOutput | string? | round 間傳遞用 |
| MeetingLog | StringBuilder（序列化轉 string）| 累積會議紀錄 |
| FinalDecision | string | "consensus" / "escalate"（預設 "consensus"） |
| EscalateReason | string? | nullable |
| TotalRounds | int | 結算用 |

**設計理由**：
- 全欄位在 framework state 內 → 隨 DesignCheckpointStore 序列化進 DesignFrameworkStateJson（不加新 DB column）
- 對齊 Stage 50 KickoffState 既有 13 欄位 pattern + Stage 51 KickoffTaskId pattern
- DemiSessionId / IssueUrls / UiSpecContent / LastPetraOutput / EscalateReason 設 nullable 對齊 legacy DesignSessionState

---

## 子項 4：DesignPrompts.cs 抽出 SoT

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Design/DesignPrompts.cs`（新檔，對齊 Stage 50 `Workflows/Kickoff/KickoffPrompts.cs` 慣例）

**從 DesignMeetingService 抽出 8 個 prompt builder static method**：
- `BuildDesignPetraJudgePrompt` / `BuildDesignRosaPreWorkPrompt` / `BuildDesignDemiPreWorkPrompt`
- `BuildDesignRosaMeetingPrompt` / `BuildDesignDemiMeetingPrompt` / `BuildDesignCodyPrompt` / `BuildDesignQuinnPrompt`
- `BuildDesignPetraRoundPrompt` / `BuildDesignPetraPlanPrompt`

**設計約束**：
- 對齊 Stage 50 KickoffPrompts pattern — legacy DesignMeetingService 全刪 wrapper 改 inline call DesignPrompts.cs（兩條路徑共用同 SoT，避免雙寫漂移）
- record `DesignPetraDecision` / `DesignAdjustmentEvaluation` / `ModifyDecision` 也搬進 DesignPrompts.cs（注意 Stage 50 踩坑 #2 — 跨 service 用 internal record 必須 grep 全 callers 補 using）
- 純機械化重構，prompt 文字 0 變動

---

## 子項 5：抽 EvaluateAndProposeSplitAsync 成共用 helper

### 實作項目

**動機**：議題 C2 拍板 — Stage 46-FF 三十五 戰略級拆 task 提案機制不能漂移，framework + legacy 共用 SoT。

**實作方式**（Forge Plan Mode 拍板具體位置）：
- 選項 a：抽到獨立 `DesignSplitProposalEvaluator` service（對齊 Stage 50 KickoffPrompts 抽出 SoT 慣例）
- 選項 b：`DesignMeetingService.EvaluateAndProposeSplitAsync` 改 public method，FrameworkDesignRouter 在 finalize 段直接 call

**Helper 簽名**（兩選項共用）：
```
Task<SplitProposal?> EvaluateAndProposeSplitAsync(
    string petraSessionId,
    string designPlan,
    string issuesJson,
    string workingDir,
    string apiKey,
    int totalRounds,
    CancellationToken ct);
```

**設計原則**：抽出後 legacy DesignMeetingService 內部 call helper（內部 call 不變行為），FrameworkDesignRouter 跑到 WorkflowOutputEvent 後在 finalize 段 call helper（同 Petra session）。

---

## 子項 6：DesignWorkflowFactory + Executors + Workflow 拓撲

### Workflow 拓撲（依 spike F1/F2 結論調整）

```
（前置作業段）
DesignStartExecutor
   ├ AddEdge → DesignPetraJudgeExecutor (Petra judge needsDemi)
   │            ↓ DesignPetraJudgeVerdict (含 needsDemi flag)
   │          DesignRosaPreWorkExecutor (Rosa 產 Issues + 建 GitHub Issue)
   │            ↓ DesignRosaPreWorkVerdict (含 IssuesJson + IssueUrls)
   │          DesignDemiPreWorkExecutor (條件式 — needsDemi=false 時 short-circuit pass-through)
   │            ↓ DesignDemiPreWorkVerdict (含 UiSpecContent or null)
   │
（主迴圈 round loop 段）
   └→ mainStart (DesignRoundStart Executor，loop back 點)
        ↓ AddFanOutEdge
        ├→ DesignAgentExecutor[Rosa]   ┐
        ├→ DesignAgentExecutor[Demi]   │ (條件式 — sessions.DemiSessionId is null 時 short-circuit pass-through)
        ├→ DesignAgentExecutor[Cody]   ├→ AddFanInBarrierEdge → DesignAggregator → AddEdge → DesignPetraExecutor
        └→ DesignAgentExecutor[Quinn]  ┘                                                            ↓ DesignPetraVerdict
                                                                                              AddSwitch:
                                                                                               ├ consensus              → DesignPlanExecutor → output
                                                                                               ├ needs_discussion < max → mainStart (loop back)
                                                                                               ├ needs_discussion >= max → DesignPlanExecutor (max_iter)
                                                                                               ├ needs_adjustment       → DesignAdjustmentExecutor (B2 single-Executor wrapper)
                                                                                               │                            ├ approved      → DesignPlanExecutor → output
                                                                                               │                            └ needs_meeting → mainStart (loop back round+1)
                                                                                               └ escalate                → DesignEscalateExecutor → output
```

### Executor 設計

| Executor | 角色 | 出口型別 | 設計約束 |
|---|---|---|---|
| **DesignStartExecutor** | 接 initial DesignState + ActiveOrchestration 設 "FrameworkDesign" + clone repo | DesignState | partial class + [SendsMessage(typeof(DesignState))] |
| **DesignPetraJudgeExecutor** | Petra judge needsDemi（isFirstMessage: true） | DesignPetraJudgeVerdict | partial class + [SendsMessage] |
| **DesignRosaPreWorkExecutor** | Rosa 產 Issues + 建 GitHub Issue（議題 G1：失敗 catch + LogWarning，沿用 legacy 行為） | DesignRosaPreWorkVerdict | partial class + [SendsMessage] |
| **DesignDemiPreWorkExecutor** | Demi 產 UI 規格（needsDemi=true 時 isFirstMessage: true 跑 LLM；needsDemi=false 時 short-circuit pass-through） | DesignDemiPreWorkVerdict | partial class + [SendsMessage]，spike F1 結論影響 short-circuit 實作 |
| **DesignAgentExecutor**（4 instance：Rosa/Demi/Cody/Quinn）| 主迴圈每 round 跑各 Agent | string output（meetingLog 用） | 對齊 Stage 50 KickoffAgentExecutor pattern；Demi instance 加條件式 short-circuit（DemiSessionId is null 時 pass-through） |
| **DesignAggregator** | fan-in barrier 收 4 Agent output | DesignRoundCollected | partial class + [SendsMessage]，對齊 Stage 50 KickoffAggregator Dictionary 序列化 deliver pattern |
| **DesignPetraExecutor** | Petra 整理（resume session）+ 解析 decision | DesignPetraVerdict | partial class + [SendsMessage] |
| **DesignAdjustmentExecutor** ⭐ | B2 single-Executor wrapper：內部跑 Rosa adjust + Demi adjust（含 DemiSessionId 動態建立邊界）+ Petra eval 三 LLM call + GitHub I/O | 兩出口：DesignPetraVerdict（needs_meeting → loop back）/ KickoffLoopResult-equivalent（approved → produce plan） | partial class + [SendsMessage(typeof(DesignPetraVerdict))] + [SendsMessage(typeof(DesignAdjustmentApproved))] 兩個 attribute；對齊 Stage 50 踩坑 #10 三件套紀律 |
| **DesignPlanExecutor** | consensus / max_iter / adjustment_approved 三入口共用 — 產設計規劃書（call DesignPrompts.BuildDesignPetraPlanPrompt） | DesignLoopResult（含 designPlan / sessionIds / issueUrls / uiSpec / totalRounds / finalDecision） | partial class + [YieldsOutput(typeof(DesignLoopResult))] |
| **DesignEscalateExecutor** | escalate 路徑收尾 | DesignLoopResult（finalDecision="escalate" + escalateReason） | partial class + [YieldsOutput] |

### 設計約束（沿用 Stage 50 三件套紀律）

- 所有顯式 SendMessageAsync / YieldOutputAsync 的 Executor 必須三件套：① `[SendsMessage(typeof(T))]` 或 `[YieldsOutput(typeof(T))]` ② `partial class` ③ 註解清楚說明為何用顯式而非 generic return
- `DesignAdjustmentExecutor` 兩出口必須兩個 [SendsMessage] attribute（對齊 Stage 51 MidInterruptCheckExecutor 兩 [MessageHandler] partial class pattern）
- Workflow router 一律用 `RunStreamingAsync` + `WatchStreamAsync` foreach（對齊 Stage 50 踩坑 #9 fan-out 拓撲必須 streaming）

---

## 子項 7：DesignCheckpointStore

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Design/DesignCheckpointStore.cs`（新檔）

**設計**：對齊 Stage 49/50 既有 `KickoffCheckpointStore` pattern — 90% 邏輯相同（從 task_groups.DesignFrameworkStateJson 載 / 寫 framework state），符合「3 次再抽象」原則第 3 次出現相似 pattern → **Stage 53 評估抽 base class**（Stage 52 不抽，避免擴大規模違反議題 A 拆 Stage 精神）。

---

## 子項 8：FrameworkDesignRouter

### 實作項目

**位置**：`src/AiTeam.Bot/Orchestration/Meeting/FrameworkDesignRouter.cs`（新檔）

**核心 method**：

| Method | 職責 |
|---|---|
| `HandleDesignMeetingAsync(TaskGroup group, CancellationToken ct)` | 主入口（對齊 Stage 50 `HandleKickoffMeetingAsync`）— ActiveOrchestration 雙 marker + 建 designTask + Dashboard push + RunStreamingAsync + WatchStreamAsync 收 WorkflowOutputEvent + 寫 task_groups.DesignMeetingLog/DesignPlan/DesignRound/IssueUrls/UiSpecContent + finalize 段 call split proposal helper（C2 後置處理）+ Discord embed/buttons + RegisterDesignConfirmation + InteractionService.CreateInteractionAsync |
| `RecoverStuckFrameworkDesignAsync(CancellationToken ct)` | Crash Recovery（對齊 Stage 50 `RecoverStuckFrameworkKickoffsAsync`）— 篩選 `g.DesignFrameworkStateJson != null && !g.IsPaused` + 沿用 Stage 50 「降級策略清 marker」拍板（Stage 52 沒 HITL，無「等待人類回應」狀態，純清 marker 即可）|
| `FinalizeDesignAsync(...)` 內部 helper | 跟議題 C2 split proposal 後置處理 + Discord notification 串接 |

**DI 註冊**：對齊 Stage 49/50 慣例 Singleton（ctor 注入 IServiceProvider / IServiceScopeFactory / DesignWorkflowFactory / DesignCheckpointStore / DiscordSocketClient / IOptions / GitHubService / InteractionService / WorkflowSettingsResolver / ILogger，scoped 服務 method 內 CreateAsyncScope 動態取）

**fallback 拍板**（議題 12）：framework Workflow 跑失敗 → 不 fallback to legacy（避免 Petra session 雙重佔用）+ 改發 Discord error embed + 標 group failed，由 Christ 線下決定 retry — 對齊 Stage 50 既有 fallback 拍板。

---

## 子項 9：入口分流

### 實作項目

**位置**：`src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs` line 301（既有 `RunDesignPhaseAsync` 內部）

**修改方式**（對齊同 service line 60 既有 Kickoff 分流 pattern）：

```
（既有）var designResult = await designMeetingService.RunDesignMeetingAsync(group, owner, repo, ct);
（改）
if (await workflowResolver.GetUseFrameworkDesignAsync(ct))
{
    await frameworkDesignRouter.HandleDesignMeetingAsync(group, ct);
    return;  // framework path 內部已處理 finalize（split proposal / Discord embed / fire Dev_plan / 等）
}
var designResult = await designMeetingService.RunDesignMeetingAsync(group, owner, repo, ct);
// ... legacy path 既有處理 ...
```

**設計理由**：framework path 內部已處理完整 finalize（對齊 Stage 50 同 pattern），legacy path 既有處理保留不動。

---

## 子項 10：Crash Recovery + Dashboard + Mock 場景

### Crash Recovery hook

**位置**：`src/AiTeam.Bot/Orchestration/AgentQueueProcessor.cs` line 81 之後

**修改方式**：
```
await frameworkRouter.RecoverStuckFrameworkAppealsAsync(stoppingToken);          // Stage 49
await frameworkKickoffRouter.RecoverStuckFrameworkKickoffsAsync(stoppingToken);  // Stage 50
await frameworkDesignRouter.RecoverStuckFrameworkDesignAsync(stoppingToken);     // Stage 52 新增
// legacy RecoverStuckOrchestrationsAsync 在三 hook 之後（沿用 Stage 50 踩坑 #4 hook 順序紀律）
```

### Dashboard SystemSettings UI 第四 toggle

**位置**：`src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor` + `.razor.cs`

既有「v4 漸進遷移控制」區塊（Stage 49/50/51 建立）下方追加第四 toggle：「使用 MS Agent Framework Design Meeting（Stage 52 v4 漸進遷移）」對應 `Workflow:UseFrameworkDesign`，警告文字寫明「⚠️ 預設關閉，啟用後新功能流程的 Design 階段走 framework Workflow path（fan-out/fan-in + needs_adjustment + 條件式 Demi）」。

### Mock 場景擴充（議題 F-mid 6 場景）

**位置**：`src/AiTeam.Bot/Services/MockScenarioService.cs` + `MockClaudeCodeService.cs`

新增 6 個 `framework_design_*` 系列場景：

| 場景 key | 行為 |
|---|---|
| `framework_design_consensus_round1` | 前置作業：needsDemi=true + Rosa 3 Issue + Demi UI 規格；Round 1 Petra → consensus；產設計規劃書 |
| `framework_design_consensus_round2` | Round 1 Petra → needs_discussion；Round 2 Petra → consensus；產設計規劃書（驗證主迴圈 loop back）|
| `framework_design_needs_adjustment_approved` ⭐ | Round 1 Petra → needs_adjustment（targets=[rosa, demi], instructions={...}）→ DesignAdjustmentExecutor Rosa adjust + Demi adjust + Petra eval=approved → 產設計規劃書（B2 Executor 主路徑驗證）|
| `framework_design_needs_adjustment_needs_meeting` ⭐ | Round 1 Petra → needs_adjustment → DesignAdjustmentExecutor Rosa adjust + Petra eval=needs_meeting → loop back Round 2 → consensus（B2 Executor needs_meeting 出口 + 外層 loop back 機制驗證）|
| `framework_design_no_demi` ⭐ | 前置作業：Petra judge needsDemi=false → DemiPreWorkExecutor short-circuit pass-through → main loop 4 Agent 但 Demi 也 short-circuit → Round 1 consensus（D2 spike F1 條件式拓撲驗證對應 Mock）|
| `framework_design_crash_recovery_during_round` | Round 1 跑期間 simulate `docker compose restart aiteam-bot` → Recovery 載 latest checkpoint resume → Round 1 完成 → consensus（對齊 Stage 50 crash_recovery_during_round 場景）|

### Forge 自驗 6 場景

對齊 Stage 50/51 forge-end SOP — Forge 自驗 5 靜態場景（Mock 邏輯審視 + 程式碼路徑審查）+ 1 個 Christ 線下實跑（crash_recovery）。

---

## 子項 11：Version bump + 結案文件

### Directory.Build.props

`<Version>3.38.0</Version>`（v4 漸進遷移第四步）

### 結案文件

- Roadmap 實作紀錄章節（Forge 結案第一段填）
- CHANGELOG / Future_Feature 同步交給 Aria 結案第二段
- 立新 FF（議題 G1）「Design Meeting Crash Recovery Issue 重複建立」🟡 觀察類

---

## 驗收情境

> Stage 52 是 v4 漸進遷移第四步，**驗收必須含條件式拓撲（needsDemi=false）+ needs_adjustment 兩出口路徑 + Crash Recovery 完整性**。沿用 Stage 49/50/51 6 場景模式擴充。

### 場景 A：UseFrameworkDesign = false → Stage 49/50/51 + legacy Design 行為不變

**怎麼觸發**：
1. push Stage 52 commit → CI/CD 部署
2. Dashboard SystemSettings 確認 `Workflow:UseFrameworkDesign = false`（預設）
3. 跑 `/mock new_feature_with_proposal` 走完整新功能流程

**怎麼驗證**：
- ✅ Design 階段走 legacy `DesignMeetingService.RunDesignMeetingAsync`（Stage 25b 既有行為）
- ✅ Bot log 沒有 `[Stage52]` 訊息
- ✅ task_groups.DesignFrameworkStateJson = null
- ✅ ActiveOrchestration 仍是 "Design"（legacy 既有值，非 "FrameworkDesign"）

### 場景 B：UseFrameworkDesign = true + Round 1 consensus → 產設計規劃書

**怎麼觸發**：
1. Dashboard SystemSettings 切 `UseFrameworkDesign = true`
2. 跑 `/mock framework_design_consensus_round1`

**怎麼驗證**：
- ✅ Bot log `[Stage52] HandleDesignMeetingAsync framework path 接管`
- ✅ task_groups.ActiveOrchestration = "FrameworkDesign"
- ✅ task_groups.DesignFrameworkStateJson != null（含 DesignState 序列化）
- ✅ 前置作業：needsDemi=true → Rosa 建 GitHub Issue + Demi 產 UI 規格
- ✅ Round 1 Petra → consensus → DesignPlanExecutor 產設計規劃書
- ✅ WorkflowOutputEvent → router finalize 段 call split proposal helper（議題 C2）→ should_split=false → fall through fire Dev_plan
- ✅ task_groups.DesignMeetingLog / DesignPlan / DesignRound / IssueUrls / UiSpecContent 全寫入
- ✅ Dashboard 流程追蹤頁 Design step 顯示 done

### 場景 C：needs_adjustment → approved 路徑 ⭐（B2 Executor 主路徑驗證）

**怎麼觸發**：
1. UseFrameworkDesign = true
2. 跑 `/mock framework_design_needs_adjustment_approved`

**怎麼驗證**：
- ✅ Round 1 Petra 回 needs_adjustment（targets=[rosa, demi], instructions=...）
- ✅ AddSwitch 路由到 DesignAdjustmentExecutor
- ✅ DesignAdjustmentExecutor 內部跑 Rosa adjust（建 GitHub Issue 第二批）+ Demi adjust + Petra eval
- ✅ Petra eval = approved → DesignAdjustmentExecutor 出口 DesignAdjustmentApproved → 產設計規劃書
- ✅ `dotnet test` 內 DesignAdjustmentExecutor 兩 [SendsMessage] attribute 對應出口型別 + partial class 三件套通過 framework type validation

### 場景 D：needs_adjustment → needs_meeting 路徑 ⭐（外層 loop back 機制驗證）

**怎麼觸發**：
1. UseFrameworkDesign = true
2. 跑 `/mock framework_design_needs_adjustment_needs_meeting`

**怎麼驗證**：
- ✅ Round 1 Petra 回 needs_adjustment
- ✅ DesignAdjustmentExecutor 內部跑 Rosa adjust + Petra eval
- ✅ Petra eval = needs_meeting → DesignAdjustmentExecutor 出口 DesignPetraVerdict（loop back signal）
- ✅ AddSwitch 路由到 mainStart loop back Round 2（外層 round+1 推進）
- ✅ Round 2 Petra → consensus → 產設計規劃書

### 場景 E：條件式 Demi 跳過 ⭐（D2 spike F1 條件式拓撲驗證對應 Mock）

**怎麼觸發**：
1. UseFrameworkDesign = true
2. 跑 `/mock framework_design_no_demi`

**怎麼驗證**：
- ✅ 前置作業：Petra judge needsDemi=false
- ✅ DesignDemiPreWorkExecutor short-circuit pass-through（不跑 LLM call，等同 no-op）
- ✅ task_groups.UiSpecContent 仍為 null（Demi 沒產出）
- ✅ Main loop 4 Agent fan-out 時 DesignAgentExecutor[Demi] 同樣 short-circuit pass-through（DemiSessionId is null 時不跑 LLM）
- ✅ Round 1 Petra → consensus → 產設計規劃書（不含 UI 規格段）

### 場景 F：Crash Recovery during round（**Christ 線下實跑**）

**怎麼觸發**：
1. UseFrameworkDesign = true
2. 跑 `/mock framework_design_crash_recovery_during_round`
3. Round 1 跑期間 Forge 執行 `docker compose restart aiteam-bot`（**Christ 授權的 ops 操作**）
4. 等容器重啟

**怎麼驗證**：
- ✅ Bot 啟動 log `[FrameworkDesignRouter] 啟動：發現 N 個 stuck framework design`
- ✅ DB `task_groups.DesignFrameworkStateJson` 仍含 latest checkpoint
- ✅ Recovery 沿用 Stage 50「降級策略清 marker」拍板（Stage 52 沒 HITL，無「等待人類回應」狀態）
- ✅ 重啟後完整跑通流程到 consensus → 產設計規劃書

---

## 風險點 / 注意事項

### 1. framework 條件式 fan-out 拓撲表達（高，spike F1 主驗證項）

**風險**：framework 1.3.0 是否原生支援條件式拓撲？needsDemi=false 時 Demi Executor 怎麼跳過？AddSwitch 兩條拓撲分支 vs Executor 內 short-circuit fallback 路線取決於 spike F1 結論。

**緩解**：
- spike F1 為 Stage 52 第一步，不可行則 fallback 到 Executor 內 short-circuit pass-through（功能等價但拓撲變平）
- feature flag 預設 false → 不啟用就 0 影響

### 2. 前置作業 → 主迴圈 round loop 串接（中-高，spike F2 主驗證項）

**風險**：Stage 50 純單段拓撲，Stage 52 是兩段串接（前置 + round loop）。framework 1.3.0 是否支援同一 WorkflowBuilder 內串兩段拓撲？前置 state 怎麼帶到 round loop？

**緩解**：
- spike F2 拍板串接 pattern
- 若必須拆兩個 Workflow → DesignFrameworkStateJson 序列化邊界要重設計（規模 +30%）

### 3. needs_adjustment B2 Executor 兩出口 type validation（中，Stage 50 踩坑 #10 延續）

**風險**：DesignAdjustmentExecutor 兩出口（DesignPetraVerdict for loop back / DesignAdjustmentApproved for produce plan）必須兩個 [SendsMessage] attribute + partial class，framework 1.3.0 type validation 嚴格。

**緩解**：
- 對齊 Stage 51 MidInterruptCheckExecutor 既有 pattern（兩 [MessageHandler] partial class）
- Mock 場景 C/D 含兩出口路徑驗證

### 4. GitHub Issue 重複建立的冪等性（低-中，議題 G1 拍板沿用 legacy）

**風險**：Crash 在前置作業 Rosa Issues 建立期間 / needs_adjustment Rosa 調整期間 → Recovery 重跑會建重複 Issue（既有 Stage 25b silent bug）。

**緩解**：
- 議題 G1 拍板沿用 legacy 重複建立行為 + 立 FF 觀察
- Crash 頻率不高（Christ 觀察）+ Trial 真實踩到再升級優先級
- Stage 52 Roadmap 明寫 trade-off

### 5. 既有 PendingConfirmationStore in-memory 邊界（低，議題 H1 拍板沿用）

**風險**：framework Design router 結束時把 PetraSessionId 寫進 in-memory `_pendingDesignConfirmations`，Bot 重啟丟失。

**緩解**：
- 議題 H1 拍板沿用 legacy 既有行為（Stage 25b 起既有 + 從未踩過）
- Modify 流程留 Stage 54 真正切 framework HITL 時統一處理

### 6. Stage 50 fan-out 拓撲三件套紀律延續（低，已有預警）

**Stage 50 踩坑紀錄 #9/#10/#11 給 Stage 52 三條預警**：
- fan-out/fan-in 拓撲 router 一律用 RunStreamingAsync + WatchStreamAsync foreach
- 顯式 SendMessageAsync / YieldOutputAsync 必須三件套（attribute + partial + 註解）
- 抽 prompt builders 共用後 Mock 角色識別字串覆蓋全 prompt 變體（Stage 50 踩坑 #11）

**緩解**：Forge Plan Mode 第一步主動 grep 對照 Stage 50 踩坑紀錄。

### 7. 不踩既有 BossInteraction 邊界（A3 試點精神延續）

**Stage 52 不動的 production code**：
- ❌ 既有 BossInteraction 10+ type 任何 type 行為（沿用 Stage 51 A3 試點精神）
- ❌ InteractionService 既有 method（CreateInteractionAsync 是 add-only）
- ❌ InteractionRespondService / InteractionProcessor 主流程
- ❌ DesignMeetingService.ModifyDesignPlanAsync legacy「Workflow 結束後修改」流程（議題 H1 沿用）
- ❌ Stage 49/50/51 既有 framework path（沿用既有 hook 順序）
- ❌ DesignerAgentService（議題 8 — Demi 個別 Agent Claude Code wrapper，不在 Design Meeting 流程內）
- ❌ WorkflowEngine.cs（Stage 53 才動）

**Stage 52 動的 production code**：
- 動：`src/AiTeam.Bot/Configuration/WorkflowSettings.cs` + `WorkflowSettingsResolver.cs`
- 動：`src/AiTeam.Data/Entities.cs` + Migration `Stage52TaskGroupDesignFrameworkState`
- 動：`src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs`（line 301 入口分流）
- 動：`src/AiTeam.Bot/Orchestration/Meeting/DesignMeetingService.cs`（抽 DesignPrompts.cs + 抽 EvaluateAndProposeSplitAsync helper）
- 動：`src/AiTeam.Bot/Orchestration/AgentQueueProcessor.cs`（加 Crash Recovery hook）
- 動：legacy `RecoverStuckOrchestrationsAsync` 排除條件加 DesignFrameworkStateJson
- 動：`src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor*`（加第四 toggle）
- 動：`src/AiTeam.Bot/Services/MockScenarioService.cs` + `MockClaudeCodeService.cs`（6 個新場景）
- 動：`src/Directory.Build.props`（Version bump）
- 新建：`src/AiTeam.Bot/Workflows/Design/`（資料夾 + DesignState.cs + DesignPrompts.cs + DesignCheckpointStore.cs + DesignWorkflowFactory.cs + Executors/ 8-10 檔）
- 新建：`src/AiTeam.Bot/Orchestration/Meeting/FrameworkDesignRouter.cs`
- 新建：可能的 `DesignSplitProposalEvaluator.cs`（議題 11，Forge Plan Mode 拍板位置）

---

## Model 建議

依 [`workflow_aria_model_effort.md`](../../C:/Users/darkl/.claude/projects/D--Source-Code-AI-Team/memory/workflow_aria_model_effort.md) 四維度：

| 維度 | 評估 |
|---|---|
| **任務複雜度** | 高 — 條件式 fan-out 拓撲 + 兩段拓撲串接 + needs_adjustment B2 兩出口 + 拆 task 提案 helper 抽出 SoT |
| **改動範圍** | L — 跨 Bot/Dashboard 多檔 + 新建 1 個資料夾（Workflows/Design/）約 10-12 檔 + 改既有 6-8 檔 + Migration |
| **歷史包袱** | 中-高 — Stage 25b 既有 GitHub Issue 重複建立 silent bug（議題 G1 沿用，立 FF 觀察）+ Stage 25b TryParseDesignIssues 既有 bug（FF 四十二 觀察類，Stage 52 不動）|
| **判斷品質要求** | 高 — Design Meeting 是 v4 漸進遷移最大遷移點（議題 A 拆 Stage 後仍是最複雜的單一 Meeting 遷移），影響 Stage 53 macro pipeline 設計 |

**建議**：**Opus 1M + high**

理由：
1. **混合型 Stage 第 4 個資料點**（沿用 Stage 49 ×1.25 + Stage 50 ×1.09 + Stage 51 ×0.96 三資料點 mid 帶下半至 mid 上半區間）
2. **Stage 52 預期偏 mid 帶上半接近 ×1.4 上界**（Design 特有 3 條子路徑 + 兩段拓撲串接 + B2 Executor 兩出口 + Stage 25b 歷史包袱觸發風險）
3. **可能拆 session（依 Forge Plan Mode 第一步觀察）**：
   - Session A：spike + 子項 1-7（feature flag + DB schema + State + Prompts 抽出 + Split helper 抽出 + WorkflowFactory + Executors + CheckpointStore）
   - Session B：子項 8-11（Router + 入口分流 + Crash Recovery + Dashboard + Mock 場景 + Version bump + 結案）

### Context 預估

依 7 項公式 + 混合型 Stage 校準（對齊 Stage 49/50/51 三資料點 mid 帶 ×0.96-1.25 區間）：
- 開場 ~32K
- 工作 raw（新建 10-12 檔 + 動 6-8 既有檔 + DesignPrompts 抽出 + WorkflowFactory + Executor 拓撲）~150-220K
- Grep / Bash 輸出 ~30-40K（讀 Stage 50 reference + grep DesignMeetingService caller + framework 條件式拓撲 docs WebFetch + dotnet build）
- 對話 turn 成本 ~50-90K（spike 第一步 2 項驗證 + Plan Mode + 閘門一 + 結案）
- Edit 反覆對齊 ~40-80K（拓撲擴充 + Executor 三件套 + DesignPrompts 抽出對齊）
- Mock 驗收（6 場景）~60-120K
- follow-up 修正 ~40-150K（spike 揭露 framework 條件式拓撲限制 / Executor type validation 揭露 / Stage 25b 歷史包袱觸發 — 中-高風險）
- 結案文件寫作 ~10-20K
- **總計約 ~410-750K**（Opus 1M 內 41-75% 負擔，舒適區）

→ 拆 session 建議：若 Forge spike + 子項 1-7 結束時 context > 280K，主動跟 Christ 提「拆下一 session 進子項 8+」。

---

## 與 v4 路線的關係

**Stage 52 是 v4 漸進遷移 7 Stage 的第四步**（議題 A 拆 Stage 後從 6 → 7）：

```
Stage 47 ✅ ops 補丁（FF 四十七，v3.34.0，2026-05-02）
Stage 48 ✅ spike Phase A（FF 四十九，採用結論，2026-05-02）
Stage 49 ✅ Cody-Vera-Petra Appeal loop 遷移（v3.35.0，2026-05-02，混合型 ×1.25）
Stage 50 ✅ Kickoff Meeting → Group Chat orchestration（v3.36.0，2026-05-02，混合型 ×1.09）
Stage 51 ✅ framework HITL pattern 試點（v3.37.0，2026-05-02，混合型 ×0.96）
   ↓
Stage 52（本 Stage）：Design Meeting B3 路線 — fan-out/fan-in + 條件式 Demi + needs_adjustment 子流程（v3.38.0）
   ↓
Stage 53：WorkflowEngine pipeline → framework Workflow（macro-orchestration，最大遷移點 — 整個任務 8 階段調度表）
   ↓
Stage 54：Crash Recovery 全面切 framework Checkpointing
   ↓
Stage 55：收尾 + token middleware + production 切換 + 老 framework code 刪除 + framework Executor 從 service 切回直連 + 真正切既有 BossInteraction 到 framework HITL（Stage 51 試點 know-how 全面 wire）
   ↓
Stage 56+（評估）：FF 三十六 Phase B 動態流程架構（依 Stage 53/54/55 結果評估）
```

> 註：Stage 52 完成後 v4 漸進遷移進度 **4/7**。若 Stage 52 揭露 framework 條件式拓撲重大限制 → 評估是否需要 spike Phase A.5（補做 framework 動態拓撲驗證）。

**Stage 52 結案後對 Stage 53 的影響**：
- Stage 50 fan-out + Stage 52 條件式拓撲 + Stage 52 兩段拓撲串接三層 know-how 累積，Stage 53 macro pipeline（8 階段調度表 → framework Workflow）有完整拓撲表達基礎
- DesignCheckpointStore 是「3 次再抽象」原則第 3 次出現（Appeal / Kickoff / Design），Stage 53 評估抽 base class

**Stage 52 對 Stage 55 的鋪路**：
- DesignAdjustmentExecutor B2 兩出口 pattern 給 Stage 55 真正切既有 BossInteraction 到 framework HITL 時複用（Modify 流程是 framework HITL 真正切的核心場景）
- DesignSplitProposalEvaluator helper 抽出 SoT 模式給 Stage 55+ 收尾時雙路徑共用 SoT 範本

---

## 實作紀錄

> 由 Forge 結案第一段填（Roadmap 章節對齊 Stage 51 v1.2 結構：子項完成度對照 / Session A B 結案 / 驗收結果 / 驗收後修正 / 關鍵設計決策 / 踩坑紀錄彙整 / Aria 校準錨候選）

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-02 | 初版規劃書建立（Aria）—— v4 漸進遷移第四步 Stage：Design Meeting B3 路線（A2 拆 Stage + B2 single-Executor wrapper + C2 router 後置 + D2 兩項 spike + E1 獨立 flag + F-mid 6 Mock 場景 + G1 沿用 legacy + H1 沿用 PendingConfirmationStore）|
