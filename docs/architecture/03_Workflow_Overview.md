# 開發流程全景圖

> 版本：**v4.1**（2026-05-21 — v5.5 完整收口 + Stage 83 WebUI 重設計後 version bump）
> 對應系統版本：**v3.75.0**（Stage 83 結案後）
>
> 最新狀態以 [`/CHANGELOG.md`](../../CHANGELOG.md) 為準；本檔記錄當前 v5.5 流程設計，後續每幾個 Stage 補一次。
>
> Talent 角色清單 + 設計原則見 [`docs/agents/v5.5_team_plan.md`](../agents/v5.5_team_plan.md)。

---

## 目錄

1. [系統架構雙層](#系統架構雙層)
2. [v5.5 三層分工](#v55-三層分工)
3. [Workflow Feature Flag](#workflow-feature-flag)
4. [Talent + Skill 分離](#talent--skill-分離)
5. [完整流程 — Christ 派指令 → PR](#完整流程--christ-派指令--pr)
6. [HITL — plan_confirm + replan_confirm](#hitl--plan_confirm--replan_confirm)
7. [Petra 動態 Subtask Plan](#petra-動態-subtask-plan)
8. [Worker — Claude Code CLI subprocess](#worker--claude-code-cli-subprocess)
9. [Crash Recovery + 佇列機制](#crash-recovery--佇列機制)
10. [Token 計費 + PetraSessionId 透傳](#token-計費--petrasessionid-透傳)
11. [關鍵程式碼位置索引](#關鍵程式碼位置索引)

---

## 系統架構雙層

| 層 | 內容 | 描述 |
|---|---|---|
| **Business 邏輯層** | 任務派發 → Petra 拆 plan → Worker chain → HITL 拍板 → PR 開啟 | 不變 — 對齊業界 supervisor + worker 三層分工 |
| **Implementation 實作層** | v5 動態 orchestrator（`PetraOrchestratorService`）+ v5.5 Talent-Skill separation + DB 持久化 PetraSession | Stage 64 路線 D 採用 / Stage 67 Talent-Skill separation / Stage 81 動態 replan + HITL retry gate |

**v4 hierarchical static path 演進**：Stage 49-55B 完成 v4 framework 漸進遷移 9/9 → Stage 63B PoC v5 動態 orchestrator → Stage 64 路線 D 採用拍板 → Stage 78a v4 多數 path 砍（含 Rosa/Demi/Release 對應 capability）→ v5.5 為當前唯一 production active path。**OpsAgent + IAgentExecutor + AgentQueueProcessor + ButtonCallbackRouter v4 routing 留 Stage 78b 後續評估**。

---

## v5.5 三層分工

對齊業界 supervisor pattern（LangGraph / Databricks / Claude Agent SDK 共識 — Trial_v26 WebSearch 驗證）：

| 角色 | 用什麼 | 能做什麼 |
|---|---|---|
| **Petra**（指揮腦）| LLM API call（Anthropic Sonnet 4.6 production active）| 看 task 文字 / 拆 SubtaskPlan / 拍 capability + skill / 給 retry instruction |
| **Cody / Vera / Quinn / Sage**（執行手）| Claude Code CLI subprocess | 在 workspace 跑 / 讀寫檔 / 跑 test / commit + PR |
| **Christ**（拍板者）| Dashboard 4 button | 看 Petra 建議 / 拍板 approve / edit / reject / respond |

**業界共識 — 業界共識 supervisor 不該動 codebase**（context overflow + hallucination 風險）/ AiTeam Petra 純 LLM API call 設計對齊。

---

## Workflow Feature Flag

22 個 `Workflow:*` flag（DB SoT `app_settings` 表 / Dashboard 系統設定頁動態切換 / `/internal/reload-cache?scope=all` 立刻生效）：

### v5.5 path active flag（production default）

| Flag | Stage | Default | 描述 |
|---|---|---|---|
| `UsePetraOrchestratorV5` | 63B | `true` | v5 動態 orchestrator path 啟用 |
| `UseTalentSkillSeparation` | 67 | `true` | Talent-Skill 分離 baseline |
| `UseV5SubtaskPlanning` | 64+ | `true` | Petra 拆 SubtaskPlan JSON |
| `UseV5Memory` | 69 | `true` | task_memories + talent_memories 記憶機制 |
| `UseV5PromptDb` | 72 | `true` | SkillPrompt 從 DB 載（fallback Resources/CLAUDE_X.md） |

### HITL flag（default off / Trial 期間切 on 驗證 / 未來 production 切 on 啟用）

| Flag | Stage | Default | 描述 |
|---|---|---|---|
| `UseHITLPlanConfirmation` | 80 | `false` | HITL plan_confirm 閘門 |
| `UseDynamicReplanning` | 81 | `false` | 動態 re-planning + HITL replan_confirm 閘門（必雙 flag 綁定 — 對齊 Stage 81 補強 #A 紀律） |
| `MaxReplanIterations` | 81 | `3` | replan loop 上限 |
| `ReplanCostCapUsd` | 81 | `5` | 單 PetraSession 累積 cost cap |

### v4 framework flag（已隨 Stage 78c v4 path 全砍 / 留 flag 0 caller）

| Flag | Stage | Default | 描述 |
|---|---|---|---|
| `UseFrameworkAppealLoop` / `UseFrameworkKickoff` / `UseFrameworkKickoffMidInterrupt` / `UseFrameworkDesign` / `UseFrameworkPipeline` | 49-53A | `true`（0 caller）| Stage 78c 砍 v4 path 後 0 引用 / 留 DB 0 影響 |

### 其他 workflow 設定

`DesignMeetingMaxRounds` / `DevPlanAppealMaxRounds` / `KickoffMaxRounds` / `ReviewAppealMaxRounds` / `QaFixMaxRounds`（v4 殘留 / 各 3）/ `MaxConcurrentPetra=3` / `MaxAttachmentsPerTask=5` / `MaxAttachmentSizeMB=5`

---

## Talent + Skill 分離

**Talent**（人物 identity）+ **Skill**（能力 capability）兩維度分離（Stage 67 起）：

- **DB `talents` 表**：6 Talent baseline（Victoria / Petra / Cody / Vera / Quinn / Sage）+ `talent_prompts.PersonaBody`
- **DB `skill_prompts` 表**：6 active SkillPrompt（`ceo_orchestration` / `petra_orchestration` / `code_implementation` / `code_review` / `qa_testing` / `documentation`）+ versioning（VersionNumber + IsActive partial unique index）
- **`talent_skills` 表**：Talent ↔ Skill 多對多 mapping（如 Cody = code_implementation + ui_design + release_publishing 兼三 skill）
- **Petra dispatch 時**：依 task 拍 capability + skill → 對齊 `talent_skills` 找到 Talent → 派 worker

詳細 6 Talent 角色 + 設計原則見 [`v5.5_team_plan.md`](../agents/v5.5_team_plan.md)。

---

## 完整流程 — Christ 派指令 → PR

```
Christ（Discord / Dashboard）
  → CommandHandler / CeoAgentService
  → Victoria flag forward 寫 PetraInbox（v5.5 後 Victoria 不直接 call LLM）
  → PetraInboxProcessor 3s polling 撈 pending → push Channel
  → PetraDispatchWorker consume Channel
  → PetraOrchestratorService.StartAsync 起 PetraSession + CloneOrPull workspace
  → Petra LLM call:
      ① DecideTalentsWithPlanAsync（拆 SubtaskPlan JSON + capability sequence）
      ② [若 UseHITLPlanConfirmation=true] 開 plan_confirm 卡 + Paused（等 Christ approve）
      ③ Chain dispatch 每 subtask:
         - 對齊 Talent + Skill mapping
         - 開 ClaudeCodeChatClientAdapter（包 IClaudeCodeService 為 IChatClient）
         - Claude Code CLI subprocess 跑（--session per-talent / workspace 持續）
         - stream-json output accumulate
         - 寫回 task_memories + talent_memories
      ④ [若 UseDynamicReplanning=true] DetectReplanTrigger 偵測 Vera "critical":[{...}] / Quinn "status":"failed"
         → InvokePetraReplanAsync（Petra LLM 給 retry instruction）
         → 開 replan_confirm 卡（等 Christ 4 decision）
      ⑤ Chain 完成 → FinalizeGitAsync（commit + push branch + open PR）
  → PR 開啟 → 完成
```

---

## HITL — plan_confirm + replan_confirm

對齊業界 LangGraph interrupt + Human-in-the-Loop pattern：

### plan_confirm（Stage 80）

- **觸發點**：Petra 拆完 SubtaskPlan 後（dispatch 前）
- **卡 UI render**：SubtaskPlan 內容 + 4 button + Petra 拆 plan 預覽
- **4 decision**：
  - **approve ✅** → `ResumeFromPlanConfirmationAsync` 從 ContextJson 重建 SubtaskPlan + 起 chain dispatch
  - **edit ✏️** → Christ 輸入 override 文字 → Petra 重 decide 開新卡
  - **reject ↩** → 接受任務 cancel 不 dispatch
  - **respond 💬** → Christ 補充 context → Petra 重 decide 開新卡

### replan_confirm（Stage 81）

- **觸發點**：Vera 標 critical 或 Quinn fail 後（`DetectReplanTrigger` Regex pattern match）
- **Petra 收 Vera 結果 → InvokePetraReplanAsync** → 給 retry instruction string（W8 紀律 — Petra LLM 只回 instruction 不回新 plan 結構）
- **卡 UI render**：Petra retry instruction + 原 Vera output 預覽 + 4 button
- **4 decision**：
  - **approve ✅** → 同 subtask 重 dispatch with retry instruction prepend（LangGraph cycles 業界紀律）
  - **edit ✏️** → Petra 重 decide 含 override → 開新卡（loop）
  - **reject ↩** → 接受原 Vera output / 繼續下個 subtask（不 cancel session / iter 不變）
  - **respond 💬** → Petra 重 decide 含 Christ 補充

### Intervention 卡（Stage 81）

- `MaxReplanIterations=3` 達上限 → abort + 開 intervention 卡
- `ReplanCostCapUsd=5` 累積 cost cap → abort + 開 intervention 卡
- 寫 `task_memories key=decision/replan-cap-reached`

---

## Petra 動態 Subtask Plan

**LLM nature 自適應**（Sonnet 4.6 拆 plan 規模對任務複雜度自適應）：

- 簡單 task（純 fix）→ 2-3 subtask
- 中等複雜度（多檔改動 + review）→ 3-4 subtask
- review-fix cycle 需要 → 5+ subtask（含 Cody fix + Vera reverify cycle）

**Subtask 結構**（JSON）：

```json
{
  "subtasks": [
    {"id": 1, "skill": "code_implementation", "description": "...", "needsImageContext": false},
    {"id": 2, "skill": "code_review", "description": "...", "needsImageContext": false}
  ],
  "dependencies": [{"from": 1, "to": 2, "type": "sequential"}]
}
```

**SubtaskPlanParser robust 防呆**（Stage 82 子項 3）：trim → StripCodeFence（markdown ```json fence）→ StripPreambleAndPostamble（first `{` + last `}` substring）→ Deserialize。對 LLM 健談行為兜底（如 Anthropic Haiku 偶爾回 conversational preamble）。

---

## Worker — Claude Code CLI subprocess

**ClaudeCodeChatClientAdapter** 包 `IClaudeCodeService`（Stage 63B PoC + Stage 64 路線 D 採用）：

- **Capability dispatch map**（Stage 78a 縮為 v5.5 4 Worker baseline）：
  - `code_implementation` → `RunAsync` + Cody persona
  - `code_review` → `RunReviewAsync` + Vera persona
  - `qa_testing` → `RunQaAsync` + Quinn persona
  - `documentation` → `RunReadOnlyAsync` + Sage persona
- **stream-json output**（Stage 82 修法）：`--output-format stream-json --verbose` + NDJSON line-by-line accumulate `type=assistant.message.content[].text` + `type=result` row 優先取 result.result（normal text-only turn 0 regression）/ 空時 fallback accumulated text（修 Quinn tool-heavy 場景 final turn tool_use → result 空 議題）
- **Skill Prompt 載入**（Stage 72 起）：DB `skill_prompts` 優先 → fallback `Resources/CLAUDE_<X>.md` file path
- **5xx Transient retry**（Stage 64）：3 次 exponential backoff（1s/2s/4s）/ 不 catch LlmApiFailureException（auth/quota retry 無意義）
- **Session 持久化**：worker 用 `--session <id>` flag + workspace 保留 → 跨 dispatch 脈絡延續

---

## Crash Recovery + 佇列機制

### PetraInbox（v5.5 取代 v4 AgentQueue）

- DB `petra_inbox` 表（Status / AttemptCount / MaxAttempts=3 / NextRetryAt / DeadAt）
- `PetraInboxProcessor` BackgroundService 3s polling pending row + push Channel
- `PetraDispatchWorker` 3 consumer 並行 consume（`Workflow:MaxConcurrentPetra=3`）

### PetraSession 持久化（Stage 15+67）

- DB `petra_sessions` 表（Status: running / paused / done / escalated / cancelled）
- `petra_session_messages` 表（每 LLM call user/assistant/tool messages 持久化 / 跨 LLM call 載入 input 延續脈絡）

### Crash Recovery

- Bot 重啟時 `RecoverStuckTasksAsync` 把 PetraInbox + PetraSession status='running' 重設 pending（對齊 v4 `AgentQueueProcessor.RecoverStuckTasksAsync` 紀律延伸）
- PetraInboxRepository.RequeueAsync 允許 dead/failed row 手動重跑（Dashboard 重跑按鈕 / Stage 76）

---

## Token 計費 + PetraSessionId 透傳

- **DB `token_logs` 表**：AgentName / Model / InputTokens / OutputTokens / TotalCostUsd / **PetraSessionId**（Stage 81+82 透傳）
- **Worker dispatch path**：`ClaudeCodeChatClientAdapter` finally 段寫 token_logs + PetraSessionId 透傳（Stage 65 Vera blind spot 修根因 + Stage 81 議題 #5）
- **Petra LLM call path**：`TokenTrackingProvider` 包 ILlmProvider + AsyncLocal scope（Stage 82 子項 2）— Petra 4 call site 包 `BeginPetraSessionScope(ctx.SessionId)` → TokenLog 寫入透傳 PetraSessionId
- **SessionCostUsd 累積**（Stage 81 子項 5）：`PetraSessionRepository.UpdateSessionCostUsdAsync` SumAsync 對齊 `token_logs WHERE PetraSessionId=...` 累計 / ReplanCostCapUsd 用此值判定

---

## 關鍵程式碼位置索引

| 區塊 | 真實檔案 |
|---|---|
| Petra 動態 orchestrator | `src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs` |
| Worker dispatch adapter | `src/AiTeam.Bot/Orchestration/Petra/ClaudeCodeChatClientAdapter.cs` |
| Claude Code CLI subprocess | `src/AiTeam.Bot/Agents/ClaudeCodeService.cs` |
| LLM Provider Factory | `src/AiTeam.Bot/Agents/LlmProviderFactory.cs` + `AnthropicProvider.cs` + `GeminiProvider.cs` + `TokenTrackingProvider.cs` |
| HITL plan_confirm + replan_confirm | `src/AiTeam.Bot/Orchestration/Petra/PlanConfirmationProcessor.cs` + `InteractionService.cs` |
| SubtaskPlanParser robust | `src/AiTeam.Bot/Orchestration/Petra/SubtaskPlan.cs` |
| PetraInbox + queue | `src/AiTeam.Bot/Orchestration/Petra/PetraInboxProcessor.cs` + `PetraDispatchWorker.cs` |
| DB entities | `src/AiTeam.Data/Entities.cs` |
| Dashboard 操作中心 | `src/AiTeam.Dashboard/Components/Pages/Interactions/InteractionCenter.razor.cs` |
| Dashboard 系統設定（flag）| `src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor.cs` |

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-04-16 | 建立 — v3 hierarchical static 描述 |
| v2.0 | 2026-04-26 | v3 → v4 framework 漸進遷移描述補 |
| v3.0 | 2026-05-07 | v4 framework 全切換 + Stage 49-56 演進描述 |
| **v4.0** | **2026-05-21** | **v5.5 動態架構 + Stage 82 修法後重寫**（取代 v3.0 v4 hierarchical static 描述 / 對應 v3.74.0 Stage 82）。**核心改動**：① 砍 v4 8 階段 Pipeline + 5 Feature Flag 描述（替換 v5.5 三層分工 + 22 flag）② 重寫 Petra 動態 orchestrator + Talent-Skill separation + HITL plan_confirm + replan_confirm 4 decision routing ③ stream-json output Stage 82 修法描述補 ④ token_logs PetraSessionId 透傳機制補 ⑤ 關鍵程式碼位置索引對齊真實 v5.5 src/ 結構 ⑥ Talent 角色清單拆出 `v5.5_team_plan.md`（避免重複）。對齊業界 supervisor pattern WebSearch 驗證（Trial_v26）。從 777 行 → 約 230 行（**-70% 漂移砍**）。 |
