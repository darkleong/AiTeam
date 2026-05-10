# 03 v4 Code Audit — 吸收 / 重寫 / 全保留三類分類 + LoC 量化

> Charter spike deliverable 3/4。對齊 Stage 62 Roadmap 子項 3 表格 + 補 LoC 量化（partial read + `wc -l` + Glob + Grep 工具組合 — Forge spike 自決方法 Aria 通過）。
>
> **量化方法**：`wc -l` 工具於 partial read 紀律下執行（怪物大檔不 full read，只取 LoC 統計與行頭尾 method signature 確認）。

---

## 量化方法

```
wc -l <file_path>           # 單檔行數
find <dir> -name "*.cs" -exec wc -l {} +   # 子資料夾累計
ls <dir>                     # 結構盤點
grep -rn <symbol> --include="*.cs"  # 對齊既有 pattern 確認（不 full read）
```

對齊 [workflow_aria.md 第 6 條 partial read + 大檔 reference 標精準 line + method 簽名](../../../../../C:/Users/darkl/.claude/projects/D--Source-Code-AI-Team/memory/workflow_aria.md)。

---

## 吸收（v5 動態架構不需）— ~16,061 LoC

> 對齊 5 挑戰拍板 #5（重啟重跑 + 不做 Checkpointing）+ Petra orchestrator 全程動態調度（不照固定 pipeline）+ FF 三十六既有「議題 A/B 在動態架構下自動消失」。

### Workflows 全資料夾 — 7864 LoC

```
find src/AiTeam.Bot/Workflows -name "*.cs" -exec wc -l {} +
→ 7864 total
```

| 子資料夾 | 內容 | LoC |
|---|---|---|
| `Workflows/Appeal/` | AppealCheckpointStore + AppealLogHelpers + AppealMessages + AppealState + AppealWorkflowFactory + 5 Executors | 1346 |
| `Workflows/Common/` | FrameworkCheckpointStoreBase | 215 |
| `Workflows/Design/` | DesignCheckpointStore + DesignPrompts + DesignState + DesignWorkflowFactory + 12 Executors | 2192 |
| `Workflows/Kickoff/` | KickoffCheckpointStore + KickoffPrompts + KickoffState + KickoffWorkflowFactory + 7 Executors | 1097 |
| `Workflows/Pipeline/` | PipelineCheckpointStore + PipelineHitlHelper + PipelineState + PipelineWorkflowFactory + WorkflowExceptionHelper + 9 Executors | 3014 |

**v5 動態架構吸收原因**：
- Petra 動態調度不照固定 7-stage pipeline（Stage Executor 全廢）
- 重啟重跑紀律（4 CheckpointStore 全廢）
- Group Chat / Agent-as-Tool 動態切換（Workflow Factory framework Workflow 拓撲全廢）

### WorkflowEngine — 28 LoC

`src/AiTeam.Bot/Orchestration/WorkflowEngine.cs` 28 行 — v5 Petra orchestrator 取代 / 廢棄

### Meeting framework Routers — 2660 LoC

| 檔案 | LoC | 廢棄原因 |
|---|---|---|
| `Orchestration/Meeting/FrameworkKickoffRouter.cs` | 1015 | Petra 動態決定 Kickoff trigger（Hybrid 會議模式）— 不需固定 Router |
| `Orchestration/Meeting/FrameworkDesignRouter.cs` | 849 | 同上 — Design trigger Petra 動態 |
| `Orchestration/Meeting/FrameworkPipelineRouter.cs` | 796 | Pipeline 整段 Petra orchestrate / Group Chat 動態 |

### Meeting Service legacy — 705 LoC

| 檔案 | LoC | 廢棄原因 |
|---|---|---|
| `Orchestration/Meeting/KickoffMeetingService.cs` | 276 | Stage 60 已收口走 framework，v5 全廢棄 |
| `Orchestration/Meeting/DesignMeetingService.cs` | 629 | 同上 |
| `Orchestration/Meeting/MeetingCommons.cs` | 104（部分） | RunAgentTurnAsync 廢（Petra 直接 Tool Call）|
| `Orchestration/Meeting/DesignSplitProposalEvaluator.cs` | 210 | Petra 動態評估（廢評估器）|
| `Orchestration/Meeting/MeetingOrchestrationService.cs` | 1066 | **大部分廢棄**（v5 PetraOrchestratorService 取代）— LoC 計入吸收（部分初始化 boot logic 可能留 — Stage 63 PoC 確認）|
| `Orchestration/Meeting/MeetingResults.cs` | 46 | Result types 廢（Petra Tool Set 結果直接走 IAgentTool）|

> **保守估計吸收**：1066 + 276 + 629 + 104 + 210 + 46 = **2331 LoC**（MeetingOrchestrationService 全納吸收 — Stage 63 PoC 若有部分 boot 留存則減）

### Hitl Bridge — 388 LoC

| 檔案 | LoC | 廢棄原因 |
|---|---|---|
| `Orchestration/Hitl/FrameworkHitlBridge.cs` | 353 | v5 重啟重跑紀律 — 不再用 framework HITL pause-resume |
| `Orchestration/Hitl/KickoffMidInterruptTriggerStore.cs` | 35 | 中途介入 Petra 動態處理 |

### Pm/* 4 services — 1103 LoC

| 檔案 | LoC | 廢棄原因 |
|---|---|---|
| `Agents/Pm/DevPlanAppealService.cs` | 214 | v5 動態架構不需固定 Appeal pipeline |
| `Agents/Pm/PmReviewService.cs` | 250 | Petra Tool Set 直接 review（不需固定 review service）|
| `Agents/Pm/PmRoutingService.cs` | 284 | Petra orchestrator 動態 routing |
| `Agents/Pm/ReviewAppealService.cs` | 355 | 同 DevPlanAppealService |
| `Agents/Pm/PmAgentCommons.cs` | 252 | shared helpers — Petra 直接 Tool Set |
| `Agents/Pm/PmAgentResults.cs` | 60 | Result types 廢 |

> 加 PmAgentCommons + PmAgentResults：1103 + 252 + 60 = **1415 LoC**

### Appeal Orchestration — 1375 LoC

| 檔案 | LoC | 廢棄原因 |
|---|---|---|
| `Orchestration/Appeal/AppealOrchestrationService.cs` | 898 | v5 Petra Tool Set 動態 review/appeal |
| `Orchestration/Appeal/FrameworkAppealRouter.cs` | 477 | 同 framework Routers — Petra 動態 |

### 吸收總計 — ~16,061 LoC

| 子類 | LoC |
|---|---|
| Workflows 全資料夾 | 7864 |
| WorkflowEngine | 28 |
| Meeting framework Routers | 2660 |
| Meeting Service legacy | 2331 |
| Hitl Bridge | 388 |
| Pm/* services | 1415 |
| Appeal Orchestration | 1375 |
| **吸收總計** | **~16,061 LoC** |

> **吸收路徑廢棄風險：高** — v5 PoC 必須 deliver 動態調度路徑才能廢棄（失敗則 fallback v4）。Charter spike 階段不廢棄任何 v4 既有 production code（紀律對齊 Charter 文件 only）。

---

## 重寫（v5 仍需但 prompt 改 / Tool Set 接 wire）— ~3,991 LoC + 925 prompt 行

### Worker Service prompt 配置

| 檔案 | LoC | 重寫範圍 |
|---|---|---|
| `Agents/CeoAgentService.cs` | 544 | prompt 重寫 + Tool Set RouteToPetra（Layer 2）|
| `Agents/DevAgentService.cs` | 1044 | prompt 重寫 + IAgentTool 介面實作 + AgentCapability("code_implementation") attribute |
| `Agents/ReviewerAgentService.cs` | 529 | 同上（capability "code_review"）|
| `Agents/RequirementsAgentService.cs` | 406 | 同上（capability "requirements_extraction"）|
| `Agents/DesignerAgentService.cs` | 407 | 同上（capability "ui_design"）|
| `Agents/QaAgentService.cs` | 385 | 同上（capability "qa_testing"）|
| `Agents/DocAgentService.cs` | 377 | 同上（capability "documentation"）|
| `Agents/ReleaseAgentService.cs` | 299 | 同上（capability "release_publishing"）|
| **小計** | **3991 LoC** | |

### CLAUDE_*.md 8 個 prompt 重寫

| 檔案 | 行數 | 重寫範圍 |
|---|---|---|
| `Resources/CLAUDE_Petra.md` | 221 | **全砍重寫**（質變定位 — Forge spike 自決 Aria 通過）|
| `Resources/CLAUDE_Cody.md` | 201 | partial 重寫（去除「審核 / 申訴 / 固定 pipeline」字樣 + 補「Petra 動態調度 / Tool Set 介面」段）|
| `Resources/CLAUDE_Vera.md` | 149 | 同上 |
| `Resources/CLAUDE_Sage.md` | 92 | 同上 |
| `Resources/CLAUDE_Quinn.md` | 92 | 同上 |
| `Resources/CLAUDE_Victoria.md` | 93 | partial 重寫（Discord 秘書定位 + 移除「業務邏輯 / codebase scan」+ Tool Set 介面）|
| `Resources/CLAUDE_Rosa.md` | 38 | 同上（短 prompt 改動小）|
| `Resources/CLAUDE_Demi.md` | 39 | 同上（短 prompt 改動小）|
| **小計** | **925 行** | 8 個 prompt 重寫（1 全砍 + 7 partial）|

### 重寫總計 — ~3,991 LoC + 925 prompt 行

> **重寫路徑遷移成本：中** — Worker Service 既有 ClaudeCodeService 介面保留 + Tool Set wrapper 包裝即可（不大改 Worker 實作），prompt 是主要工作量。Petra prompt 全砍是質變最大 risk。

---

## 全保留（與 framework 無關基礎設施）— ~32,300+ LoC

### Bot Agent 基礎設施 — 2160 LoC

`src/AiTeam.Bot/Agents/` 中**非 Worker Service** 的基礎設施（不含 *AgentService.cs 8 檔已列重寫）：

| 檔案 | LoC | 保留原因 |
|---|---|---|
| `ClaudeCodeService.cs` | 675 | subprocess 基礎設施 |
| `MockClaudeCodeService.cs` | 545 | Mock fixture（v5 Workers Mock 不變）|
| `AnthropicProvider.cs` | 92 | LLM provider |
| `GeminiProvider.cs` | 164 | Mock Petra 用 |
| `LlmProviderFactory.cs` | 85 | provider factory |
| `TokenTrackingProvider.cs` | 140 | Token 守門 Stage 22 |
| `IClaudeCodeService.cs` | 78 | 介面 |
| `IAgentExecutor.cs` | 84 | 介面 |
| `ILlmProvider.cs` | 27 | 介面 |
| `LlmApiFailureException.cs` | 39 | Stage 58 容錯 |
| `MeetingSubprocessFailureException.cs` | 38 | Stage 60 容錯 |
| `TokenCostEstimator.cs` | 64 | Token 成本估算 |
| `TokenUsage.cs` | 15 | DTO |
| `MockLlmProvider.cs` | 62 | Mock fixture |
| `ClaudeCodeProxy.cs` | 89 | proxy |
| `CeoResponse.cs` | 66 | DTO |

> 部分檔案（CeoResponse.cs DTO）v5 可能不需 — Stage 63 PoC 階段確認

### Bot Queue / Boss / Interaction — 1372 LoC

| 檔案 | LoC | 保留原因 |
|---|---|---|
| `Orchestration/AgentQueueService.cs` | 205 | Stage 27 DB-as-Queue |
| `Orchestration/AgentQueueProcessor.cs` | 510 | Stage 27 + Stage 58 catch path |
| `Orchestration/InteractionProcessor.cs` | 173 | BossInteraction processing |
| `Orchestration/Boss/BossNotificationService.cs` | 208 | Stage 28a 通知（Stage 59 拆解 helpers）|
| `Orchestration/Boss/BossResponseHandlerService.cs` | 267 | Stage 28b 樂觀鎖（Stage 59 拆解 case body）|

### TaskGroupService — 813 LoC

`src/AiTeam.Bot/Orchestration/TaskGroupService.cs` 813 — Stage 59 已拆解 -54%（dispatch / guard / 路由 3 主入口 method 保留）— v5 仍需 Boss notification + dispatch 入口。

### 其他 Bot Orchestration（Routing / Boss / Epic）— ~600 LoC

Stage 59 拆出 3 子目錄：
- `Orchestration/Boss/` — 已列上方 1372
- `Orchestration/Routing/` — Stage 59 拆 PipelineRoutingService 等
- `Orchestration/Epic/` — Stage 59 拆 EpicChainService

### Discord 雙通道 — ~1500+ LoC

| 檔案 | LoC | 保留原因 |
|---|---|---|
| `Discord/Routing/ButtonCallbackRouter.cs` | 1202 | Discord 雙通道（Stage 28a/b 樂觀鎖）— v5 0 改動 |
| 其他 `Discord/**` | ~300 | CommandHandler / Routing helpers / PendingConfirmationStore |

### Configuration / Internal API / etc.

| 子資料夾 | 保留原因 |
|---|---|
| `Configuration/` | settings + WorkflowSettings（v5 PoC 階段用 feature flag 切換 v4/v5）|
| `Api/InternalController.cs` | Bot Internal API（5052 + X-Api-Key）|
| `Persistence/` | EF Core context |

### AiTeam.Dashboard / Data / Shared — 32,296 LoC（總計）

```
find src/AiTeam.Dashboard src/AiTeam.Data src/AiTeam.Shared -name "*.cs" -exec wc -l {} +
→ 32296 total
```

| 專案 | 檔案數 | 保留原因 |
|---|---|---|
| `AiTeam.Dashboard` | 39 .cs + Razor | Dashboard UI 全保留（Operation Center / Pipeline View / Token / 系統設定）— Layer 1 入口 |
| `AiTeam.Data` | 97 | EF Core entities + Migrations + Repositories — 全保留（Stage 63 PoC 加 2 新 entity + 1 Migration）|
| `AiTeam.Shared` | 13 | DTO / ViewModels — 全保留 |

### 全保留總計 — ~32,300+ LoC（含 Dashboard / Data / Shared / Bot 基礎設施）

| 子類 | LoC |
|---|---|
| Bot Agent 基礎設施 | 2,160 |
| Bot Queue / Boss / Interaction | 1,372 |
| TaskGroupService | 813 |
| Bot Orchestration helpers（Routing / Epic 等） | ~600 |
| Discord 雙通道 | ~1,500 |
| AiTeam.Dashboard / Data / Shared | 32,296 |
| **保留總計** | **~38,700+ LoC** |

> **全保留路徑風險：0** — 基礎設施與 framework 解耦，v5 PoC 0 改動既有 baseline 不變。

---

## 三類比例

| 類別 | LoC | 占 v4 全程式碼比例 |
|---|---|---|
| 吸收 | ~16,061 | ~26% |
| 重寫 | ~3,991 LoC + 925 prompt 行 | ~7% LoC |
| 全保留 | ~38,700+ | ~67% |
| **合計** | **~58,752+ LoC** | 100% |

> v4 投資**保留 + 重寫 = 約 73%**（重寫主要是 prompt 工作量 + Tool Set wrapper LoC 不大改 Worker 實作）— 對齊 Christ 拍板「保留 v4 不動 + 漸進遷移過半投資不浪費」精神 + Stage 49-58 漸進遷移投資保留比例健康。

> 對比 Aria 規劃預估（吸收 ~6K）— 實際吸收 ~16K（**+167% 超預估**，Aria 自省揭露補強對齊 — 主因為 Workflows 全資料夾 7864 LoC + Meeting legacy 2331 LoC + Pm/* 1415 LoC + Appeal Orchestration 1375 LoC 累積遠超預期）。

---

## 風險評估

| 路徑 | 風險 | 說明 |
|---|---|---|
| **吸收路徑** | **高** | v5 PoC 必須 deliver 動態調度才能廢棄 v4 既有 4 CheckpointStore + 7+ Stage Executor + Workflow Engine + Meeting Service + Pm/* + Appeal — 失敗則 fallback v4。Charter spike 階段標 deprecated 不刪 — Stage 63 PoC 階段也只在 feature/v5-poc branch 廢棄 / main 仍服務 Christ 日常 |
| **重寫路徑** | **中** | Worker Service code 既有 ClaudeCodeService 介面保留 + Tool Set wrapper 包裝（不大改 Worker 實作）。Petra prompt 全砍是質變最大 risk — 偏見可能殘留（CLAUDE_Petra.md 221 行歷史累積層偏見根因 Trial_v8 揭露）。CLAUDE_*.md 8 個 prompt 重寫工作量集中 |
| **全保留路徑** | **0** | 基礎設施與 framework 解耦 — Discord/Dashboard/EF Core/Token 守門/Mock 模式/Token logs 全保留 0 改動既有 baseline 不變 |

---

## 對齊既有 Stage 投資

| Stage | 投資內容 | v5 處理 |
|---|---|---|
| Stage 22 | Token 守門 / Dashboard 存取分層 | 全保留 |
| Stage 27 | DB-as-Queue（agent_queue + AgentQueueService）| 全保留 |
| Stage 28a/b | 雙向操作中心 + 樂觀鎖 | 全保留（Layer 1 入口）|
| Stage 38 | Provider/Model 動態化 + LlmModels constants | 全保留（v5 Petra Provider 切 Gemini Flash 用）|
| Stage 47 | Token DB SoT + AppSettings | 全保留 |
| Stage 49-58 | v4 漸進遷移（framework Routers + 4 CheckpointStore + 7 Stage Executor + 5+1+1 routing types + ~7+1+1 Mock 場景）| **全吸收**（~14,000+ LoC v4 hierarchical static 投資 — 換動態架構新典範 + 漸進遷移過半投資保留作為「換引擎不換車身」精神範例）|
| Stage 59 | TaskGroupService 拆解 -54% | 全保留（dispatch / guard / 路由 3 主入口 method）|
| Stage 60 | meeting subprocess fail-fast 統一 | 部分吸收（MeetingCommons.RunAgentTurnAsync 廢）|
| Stage 61 | Petra/Cody prompt 對齊群組 + Pipeline UI | prompt 部分重寫（v5 Petra 全砍 + Cody partial）+ UI 全保留 |
