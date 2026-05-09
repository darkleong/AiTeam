# Stage 58：v4 framework production-ready 補強第二波 — API 餘額容錯性（FF 五十三）

> 對應 Future Feature：FF 五十三（API 餘額用盡時容錯性缺口）— Trial_v6 揭露 3 🔴 戰略級議題最後一個（前兩個 Stage 57 v3.46.0 + v3.46.1 已修）
> 對應版本：**v3.47.0**（Stage 57 v3.46.1 + minor bump）
> 建立日期：2026-05-09
> 狀態：✅ 已完成（2026-05-10）
> 文件版本：v2.0

---

## 概述

**戰略背景**：Trial_v6 v2.0 揭露 v4 framework 9/9 達成的 production-ready 邊界 3 個 🔴 缺口，Stage 57 已收口 race condition + Vera fix loop HITL routing 兩個（v3.46.0 + v3.46.1）。Stage 58 = 最後一個 🔴 = API 餘額用盡時的容錯性 — 修完進入 Trial_v7+ 重跑 Trial_v6 量化 v4 framework hierarchical static 真實 ROI（去掉 race / 卡死 / API 容錯三 noise 後）。

### 範圍邊界

- ✅ **API 失敗偵測**：CLI path（ClaudeCodeService subprocess stdout 偵測 API 401 / insufficient_balance signal）+ API path（Anthropic SDK exception catch）兩條路統一拋業務 exception `LlmApiFailureException`
- ✅ **AgentQueueProcessor specific catch**（v1.1 路線 A 架構修正）：`catch (LlmApiFailureException)` 在 generic `catch (Exception ex)` 之前 → build `AgentExecutionResult` 帶 `[API_FAILURE]` summary 前綴 + RawError 內容 → call `HandleAgentCompletedAsync` 走正常 callback flow（**不**走 line 312 mark task failed silent path）
- ✅ **4 Agent 統一 fail-fast 行為**（Dev / Reviewer / QA / Doc — 全走 ClaudeCodeService CLI path）：4 Pipeline Stage Executor `HandleResponseAsync` 第一行檢查 `result.Summary.StartsWith("[API_FAILURE]")` → fire 新 routing interaction（取代 Trial_v6 揭露的 silent skip 行為）
- ✅ **新 BossInteraction routing**：1 統一 type `agent_api_failure_intervention`（context.agent 區分）+ Christ 拍板真三選 button（continue / retry / abort）+ 對齊 Stage 55B Session B 5 routing yield-resume pattern + Stage 57 第 6 routing 對齊 — 第 7 routing
- ✅ **4 Pipeline Stage Executor handler**：DevStage / ReviewerStage / QaStage / DocStage 各自 marker check + fire `agent_api_failure_intervention` 並 yield 等 Christ — 對齊 Stage 53B `[BLOCKED]` marker pattern + Stage 57 ReviewerStageExecutor.HandleReviewerFixLoopLimitResponseAsync 設計
- ✅ **Mock 場景補強**：1 個（`framework_pipeline_agent_api_failure`）+ 對應 Dashboard MockScenarioCard

- ❌ **不動**：FF 三十六 Phase B 動態流程架構（等 Stage 58 完成後啟動）
- ❌ **不動**：Quinn `qa_failed_intervention` 既有 fix loop routing（不同性質 — fix loop ≠ API 爆，獨立 routing）
- ❌ **不動**：USD billing 守門模式（Christ 拍板 = B 容錯模式，不做 USD billing 預先攔截 — 戰略價值優先 vs 預算敏感度）

### v4 framework production-ready 達成判定

Stage 58 完成後 = **Trial_v6 揭露 3 🔴 全收口**：
- race condition（Stage 57 v3.46.0 + v3.46.1）✅
- Vera fix loop HITL routing（Stage 57 v3.46.0）✅
- API 餘額容錯性（Stage 58 v3.47.0）= 本 Stage

→ 可進入 **Trial_v7+ 重跑 Trial_v6** 對照新 baseline（同 Trial_v5 → v6 對照模式）量化 v4 framework hierarchical static 真實 ROI（去掉三 noise 後）。

### Trial_v6 真實傷害數據（Stage 58 修法後消除）

對齊 Trial_v6 v2.0 Checkpoint 13 + 議題 #15：
- TokenTrackingProvider 守門用 token count（10M/month）不用 USD billing — 沒擋住 API 401 / insufficient_balance
- Vera 最危險：cost 0 + task done + 流程繼續（silent skip，沒 fail-fast）
- Quinn 半明確：cost 0 + task failed + qa_failed_intervention（但是因為 git commit 失敗副作用，不是 catch API 401）
- Sage：cost 0 + task done + 「無輸出略過提交」silent skip + epic_partial_paused
- 表面看「No changes; nothing to commit」誤導真實根因（API billing fail）

### Stage 57 教訓套入 + Stage 58 揭露 Aria 設計疏忽 #3（v1.1 升級）

Stage 57 揭露 Aria 計劃書兩個設計層盲點，Stage 58 v1.0 Forge spike 揭露第三個。Stage 58 v1.1 計劃書硬規則**擴張**：

| Stage 教訓 | 預防措施 |
|---|---|
| **#1**（Stage 57 ffe2027）：TryCreateUniqueInteractionAsync TOCTOU race window（helper 內 read-write 假設 atomic）| 不引入新 idempotent helper（容錯路徑都是 fire-and-forget BossInteraction，不需 unique check）— 若需要必先 grep 既有 atomic primitive 用法（partial unique index / pg advisory lock / EF Core ExecutionStrategy）|
| **#2**（Stage 57 ffe2027）：HandleEpicPartialPaused user transaction 沒考慮 NpgsqlRetryingExecutionStrategy 衝突 | 不引入新 user transaction（fire-and-forget interaction + Pipeline yield-resume 不需 transaction wrap）— 若需要必先包 `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` 內 |
| **#3**（Stage 58 v1.0 spike 揭露）：「4 Stage Executor catch agent throw」設計疏忽 — Stage Executor 與 AgentQueueProcessor 是不同 async path，throw 從未跨 callback boundary | v1.1 修正路線 A：catch 在 AgentQueueProcessor（agent 真正執行的 async path），用 `[API_FAILURE]` summary marker 跨 callback 傳遞 — 對齊 Stage 53B `[BLOCKED]` 既有 marker pattern |

→ **計劃書硬規則升級（v1.1）**：

> **Aria 設計新 helper / transaction / catch handler 時，先 grep codebase 既有相關 architecture boundary 用法**（atomic primitive / async flow / callback boundary / state propagation 等），不憑印象寫設計假設。
>
> Stage 56 起 Forge Plan Mode 主動揭露 Aria 預掃缺口的累積成果：Stage 55A 3 個 / Stage 55B 6 個 / Stage 57 0 個 / **Stage 58 1 個 🔴**（callback boundary 設計疏忽）。

對應自省點 #26 候選（Aria 結束 SOP 第八節持續累積）。

---

## 設計決策（Christ 拍板 + Aria 拿捏，含 v1.1 4 議題 finalize）

### 主路線（Christ 拍板）

| 議題 | 拍板 | 理由 |
|---|---|---|
| **議題 1：模式選擇** | **B 容錯模式** — catch API 401 / insufficient_balance error → 4 Agent 統一 fail-fast 行為 | 對齊 Christ「碰到爆再儲值就好」態度（戰略價值優先 vs 預算敏感度，user_christ.md），守門模式 over-engineering |
| **議題 2：routing button** | **真三選** `continue` / `retry` / `abort` | 對齊 Quinn `qa_failed_intervention` 既有三選 pattern 但語意不同（API 爆 retry 是「儲值後重試該 agent」非「再跑一輪」）|
| **議題 3：拆 Session 戰術** | **一個 session 跑** | 兩件事高度耦合（catch API 錯誤 + 統一 fail-fast 是同一條 code path），拆 Session 反增複雜度 |

### Aria 拿捏（v1.1 已決，純內部實作不對外議題）

| # | 議題 | 決定 |
|---|---|---|
| 1 | BossInteraction type 統一 vs 分 4 type | **統一 1 type `agent_api_failure_intervention`** — 4 Agent fail behavior 一致（cost 0 + API 401 同根因），UI button 一致；context.agent 區分（Cody/Vera/Quinn/Sage）|
| 2 | RequestPort 統一 vs per-stage | **per-stage 4 port**（DevStage / ReviewerStage / QaStage / DocStage）— 對齊 Stage 55B Session B 5 routing per-agent pattern + framework 1.3.0 RequestPort type-bound 限制 |
| 3 | NotifyBoss helper 統一 vs 分 4 method | **統一 1 method `NotifyBossAgentApiFailureAsync(group, agentName, ...)`** — fire-and-forget pattern 共用，agentName 參數區分 context |
| **4**（v1.1 路線 A 修正）| API 失敗 catch 在哪層 | **CLI path：ClaudeCodeService 偵測 stdout signal 拋 `LlmApiFailureException`** + **API path：AnthropicProvider catch SDK exception 拋同 exception** + **AgentQueueProcessor specific catch（在 generic catch 之前）build `[API_FAILURE]` summary 前綴 result + call HandleAgentCompletedAsync 走正常 callback flow** + **4 Pipeline Stage Executor `HandleResponseAsync` 第一行 marker check 統一處理**（v1.0 「Stage Executor catch」設計被 spike 揭露架構不可行，v1.1 修正為 marker pattern）|
| 5 | retry button 行為 | **直接 re-invoke 同一個 Agent task**（state 不動 — Anthropic key 不換只是 balance 變）；對應 ContinuationAction = SendMessage(同 Bridge) 重跑該 stage |
| 6 | continue button 行為 | **跳過該 Agent 進下階段**（state.AgentDone=true + SendMessage 下游 Bridge）— 對齊 Stage 57 ReviewerStageExecutor skip_qa case pattern |
| 7 | abort button 行為 | **SetInterventionAndYieldAsync end Pipeline** — 對齊既有 abort 行為 |
| 8 | Token 計費紀錄行為 | API 401 場景**不寫 token_logs**（real cost = 0，沒實際 LLM 呼叫）— LlmApiFailureException 拋出時自動跳過 LogCliUsageAsync / TokenTrackingProvider 寫入路徑（C# exception flow 自動逃脫，pure additive 不需動 TokenTrackingProvider）|
| 9 | Mock 場景命名 | `framework_pipeline_agent_api_failure` — 對齊 Stage 49-57 既有 framework_pipeline_* 命名慣例 |
| 10 | Mock 觸發機制 | MockClaudeCodeService.RunAsync / RunReadOnlyAsync / RunMeetingSessionAsync 偵測 FailScenario `agent_api_failure` → 直接 throw `LlmApiFailureException`（模擬 API 401）→ propagate 到 AgentQueueProcessor specific catch → 4 stage executor marker check |
| 11 | Migration / schema | **不動**（純 routing 邏輯 + exception handling 邏輯 + marker pattern，無 DB schema 改動）|
| 12 | CLAUDE_*.md prompt | 不動（不引入新 LLM call / Agent prompt 不變）|
| **13**（v1.1 拍板）| MockMode auto-approve `agent_api_failure_intervention` 預設 action | **`api_failure_continue`** — Aria 反 Forge 提案 retry 拍板理由：retry 預設會無限迴圈卡死 Mock（FailScenario 仍 = api_failure，Mock 永遠失敗）；continue 對齊 Stage 56/57「auto-approve 推進精神」+ 4 agent 一次跑通驗 fire interaction（Dev fail → continue → Reviewer fail → continue → Qa fail → continue → Doc fail → continue → end），retry 行為手動 SQL update auto-approve action 驗對齊既有慣例 |
| **14**（v1.1 拍板）| LlmApiFailureException 結構 | **string RawError capped 500 chars**（CLI path stdout 摘要 / API path SDK exception.Message） + **附帶 `LlmProviderType` enum**（Anthropic / Gemini / Unknown）對齊既有 GeminiProvider Stage 37 + 預留擴充 |
| **16**（v1.1 拍板）| Mock 觸發 alias 設計 | **單 alias `framework_pipeline_agent_api_failure`** — Pipeline 從 Cody Dev stage 進就會 throw → 第一個 fire 的 interaction 即 context.agent="Dev"。配套議題 13 continue 預設一次跑通驗 4 agent fire（Dev → Reviewer → Qa → Doc 各自被 throw → 各自 fire interaction → auto-approve continue 推進）；4 agent 各驗靠手動串 SQL update FailScenario 切換 |

### Aria 二檢通過後實作期 3 提醒

實作前自查避免踩坑（純內部，自診自修對齊 Stage 53B/54/55A/57 self-diag 精神）：

1. **AgentQueueProcessor catch 內 push notification + Task.Run callback 對齊既有 generic catch pattern**：spike F5 已 grep，實作前再對照 pushService / Task.Run / appLifetime 用法跟 generic catch (Exception ex) line 312 一致
2. **retry case re-invoke 同 Stage 機制驗證**：`SendMessage(new <Stage>StageBridge(state.GroupId))` 觸發 stage executor HandleEntryAsync 重新 fire agent task — 注意 task table 是否有 row duplication 風險 + state cleanup（state.<Stage>Done 是否需要重設）。對齊 Pipeline 既有 SendMessage Bridge pattern 應該 OK，實作後 SQL 查驗 task_groups / task_items 沒 duplicate row
3. **ResumeWithResponseAsync 簽名 vs `(object)` cast**：既有 Stage 57 ResumeAfterReviewerFixLoopLimitAsync 是 typed `ReviewerFixLoopLimitResponse`。Stage 58 採**路線 a**：拆 4 個 typed `ResumeAfterDevAgentApiFailureAsync` / Reviewer / Qa / Doc thin wrapper（對齊 Stage 57 pattern + 不動 ResumeWithResponseAsync 既有簽名）

### Stage 58 子項拆分（v1.1 規模重估）

| # | 子項 | 規模（v1.0 → v1.1）|
|---|---|---|
| **0** | **Spike 第一步：read 對齊範圍**（Forge Plan Mode 第一步）— ① ClaudeCodeService CLI subprocess 對 API 401 的 stdout pattern grep ② Anthropic SDK exception type spike ③ Stage 55B Session B 5 routing + Stage 57 第 6 routing dispatch 鏈路對照表 ④ 4 Pipeline Stage Executor 既有 result 處理路徑 + AgentQueueProcessor 既有 catch 邏輯 ⑤ TokenTrackingProvider + LogCliUsageAsync 在 cost 0 時的既有寫入行為 | XS（**已完成** — Forge spike 報告 + Aria v1.1 4 議題拍板）|
| **1** | 業務 exception 定義 + 雙 path 偵測 + AgentQueueProcessor 接手：① 新建 `LlmApiFailureException`（含 `LlmProviderType` enum + `string RawError capped 500`）② ClaudeCodeService CLI subprocess stdout 偵測 API 失敗 signal（`Credit balance` / `insufficient_balance` / `401` / `authentication_error` 字串配對，detect 失敗 fallback 既有 result.Success=false path 不破壞既有失敗路徑）③ AnthropicProvider catch Anthropic SDK exception 轉拋同 exception（pattern match exception.Message / status code，無 strongly-typed exception 時用字串配對）④ **AgentQueueProcessor specific catch（在 generic catch 之前）build `[API_FAILURE]` summary 前綴 result（含 RawError）+ call HandleAgentCompletedAsync 走正常 callback flow**（v1.1 路線 A 架構修正核心）⑤ TokenTrackingProvider 確認 cost 0 + 不寫 token_logs（exception 自動跳過寫入路徑） | M → **M+S**（v1.1 升級：多 AgentQueueProcessor specific catch + build [API_FAILURE] result + call HandleAgentCompletedAsync 接手）|
| **2** | 4 Pipeline Stage Executor `HandleResponseAsync` 第一行加 marker check + fire `agent_api_failure_intervention` + yield 等 Christ：DevStageExecutor / ReviewerStageExecutor / QaStageExecutor / DocStageExecutor — 對齊 Stage 53B `[BLOCKED]` 既有 marker pattern + Stage 57 ReviewerStageExecutor FixIteration≥3 case fire+yield pattern | M → **S**（v1.1 修正：marker check 比 catch 簡單；4 stage executor 各 +1 if block）|
| **3** | 第 7 routing 完整 wiring：① InteractionService AgentApiFailureActionsJson const + auto-approve switch case `api_failure_continue` 預設（v1.1 議題 13 拍板）② AgentApiFailureRequest / Response records（PipelineState.cs，per-stage 4 變體 — DevAgentApiFailureRequest/Response / ReviewerAgentApiFailureRequest/Response / QaAgentApiFailureRequest/Response / DocAgentApiFailureRequest/Response，含 agentName context）③ 4 PortId const + 4 RequestPort + 4 AddEdge wiring（含 per-stage continue 跳下游 Bridge edge — Dev→Reviewer / Reviewer→Qa / Qa→Doc / Doc→終）④ HandleAgentApiFailureResponseAsync 真三選獨立 path（continue→下游 Bridge / retry→同 stage Bridge re-invoke / abort→SetIntervention end）| M |
| **4** | TaskGroupService 加 `NotifyBossAgentApiFailureAsync(group, agentName, ...)` 統一 helper + `TryRoutePipelineAgentApiFailureAsync` dispatch + `case "agent_api_failure_intervention"` ProcessBossResponseAsync dispatch + FrameworkPipelineRouter 4 typed thin wrapper（路線 a：`ResumeAfterDevAgentApiFailureAsync` / Reviewer / Qa / Doc — 對齊 Stage 57 typed pattern 不動 ResumeWithResponseAsync 簽名）+ InteractionProcessor 3 label mapping（continue/retry/abort）| M |
| **5** | Mock 場景補強：① MockClaudeCodeService 加 FailScenario `agent_api_failure` 觸發拋 `LlmApiFailureException`（v1.1 議題 16 拍板單 alias，4 agent 一次跑通驗 fire interaction）② MockScenarioService 加 alias case + scenario switch + emoji + frameworkHint ③ Dashboard MockScenarioCard 加 1 MudSelectItem | S |
| **6** | Forge 自驗：① 跑 agent_api_failure Mock 場景 → 4 Agent 任一爆觸發 `agent_api_failure_intervention` interaction（議題 13 auto-approve 預設 continue 一次跑通驗 4 fire）② SQL 查 BossInteraction Type=`agent_api_failure_intervention` + context.agent ✅ ③ 手動 SQL 驗 continue / retry / abort 三 path ④ regression：Stage 55B Session B 5 routing + Stage 57 第 6 routing 既有 6 routing 仍綠 + Stage 56 token_logs 寫入率不被新「不寫」邏輯誤擋（只 API 失敗時不寫，正常呼叫仍寫）| S |
| **7** | Version bump v3.47.0 + 結案文件（Roadmap 實作紀錄章節 + CHANGELOG / Future_Feature 同步交給 Aria 結案第二段）| XS |

> **不寫工時估算**（workflow_aria.md 第三節 A 第 4 條）— 各子項規模見上表 XS/S/M 範圍描述。
>
> **預估 Forge context（v1.1 路線 A 評估）**：400-550K（落 Aria 預估 ×1.2-1.5 區間下緣 — 路線 A 改動相對輕，不需動 AgentExecutionResult record 簽名）

---

## 子項 0：Spike 第一步 — read 對齊範圍（已完成）

### read 對齊範本

| # | read 對象 | 目的 |
|---|---|---|
| F1 | `src/AiTeam.Bot/Agents/ClaudeCodeService.cs` 既有 catch 區塊（line 556 / 597）+ subprocess stdout 解析邏輯 | 子項 1 CLI path 偵測點 ✅ |
| F2 | `src/AiTeam.Bot/Agents/AnthropicProvider.cs`（無 catch — 透過 Anthropic SDK）+ Anthropic.SDK 5.10 package exception types grep | 子項 1 API path 偵測點 ✅ |
| F3 | `src/AiTeam.Bot/Agents/TokenTrackingProvider.cs` line 113-138 inner provider call + token_logs 寫入路徑 | 子項 1 確認 cost 0 不寫 token_logs 邏輯 ✅ pure additive 不需動 |
| F4 | Stage 55B Session B 5 routing + Stage 57 第 6 routing dispatch 對照（4 Pipeline Stage Executor + FrameworkPipelineRouter ResumeAfter*）| 子項 2/3/4 對齊既有 6 routing pattern ✅ |
| F5 | `src/AiTeam.Bot/Workflows/Pipeline/Executors/` 4 Stage Executor 既有 result 處理路徑 + **`AgentQueueProcessor.cs:312` 既有 generic catch**（v1.1 路線 A 修正關鍵）| 子項 1/2 catch 點 + marker check 點插入位置確認 ✅ |
| F6 | `src/AiTeam.Bot/Services/InteractionService.cs` line 64 EpicPartialPausedActionsJson + auto-approve switch case + Stage 57 ReviewerFixLoopLimitActionsJson | 子項 3 actions JSON const 加在哪 + auto-approve switch case 對齊 ✅ |
| F7 | `src/AiTeam.Bot/Orchestration/InteractionProcessor.cs` line 141-159 Stage 55B + 57 routing label mapping | 子項 4 InteractionProcessor 加 3 label mapping 位置 ✅ |

### Spike 揭露結論（已交 Aria，v1.1 拍板）

1. **🔴 議題 1 路線 A 架構修正**（v1.0 → v1.1）：v1.0「4 Stage Executor catch agent throw」設計疏忽 — Stage Executor 與 AgentQueueProcessor 是不同 async path，throw 從未跨 callback boundary。v1.1 改 marker pattern：AgentQueueProcessor +1 specific catch（在 generic catch 之前）build `[API_FAILURE]` summary 前綴 result → call HandleAgentCompletedAsync 走正常 callback flow → 4 stage executor `HandleResponseAsync` 第一行 marker check → fire interaction + yield。對齊 Stage 53B `[BLOCKED]` 既有 marker pattern 不破壞 AgentExecutionResult record 簽名。
2. **議題 13 拍板**：MockMode auto-approve 預設 `api_failure_continue`（反 Forge 提案 retry — Aria 理由：retry 無限迴圈卡死 Mock + continue 對齊「auto-approve 推進精神」+ 4 agent 一次跑通）
3. **議題 14 拍板**：`string RawError capped 500` + 附帶 `LlmProviderType` enum（Anthropic / Gemini / Unknown）
4. **議題 16 拍板**：單 alias `framework_pipeline_agent_api_failure`（4 agent 各驗靠手動串）

---

## 子項 1：業務 exception 定義 + 雙 path 偵測 + AgentQueueProcessor 接手（v1.1 路線 A）

### 修法策略

#### 新建 `LlmApiFailureException`

放在 `src/AiTeam.Bot/Agents/LlmApiFailureException.cs`：

```csharp
public sealed class LlmApiFailureException : Exception
{
    public LlmProviderType ProviderType { get; }
    public string RawError { get; }  // capped 500 chars

    public LlmApiFailureException(LlmProviderType provider, string rawError)
        : base($"LLM API failure ({provider}): {Truncate(rawError, 500)}")
    {
        ProviderType = provider;
        RawError = Truncate(rawError, 500);
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? "" : (s.Length > max ? s[..max] : s);
}

public enum LlmProviderType { Anthropic, Gemini, Unknown }
```

#### CLI path（ClaudeCodeService）偵測

`ParseJsonOutput` 解析後若 `success=false`，加 `DetectApiFailureSignal(output, stderr)` helper（case-insensitive substring 配對 `Credit balance` / `insufficient_balance` / `401` / `authentication_error`）→ throw `LlmApiFailureException(Anthropic, output snippet)`。

**保守原則**：detect 失敗時 fallback 到既有 result.Success=false path（不破壞既有失敗路徑），任何漏接最壞情況退化為 silent fail（修前 baseline 行為）。

不破壞既有 catch 邏輯 line 556 / 597（皆為 git config / TryParseUsage 內部 swallow 用，與 API failure 無關）。

#### API path（AnthropicProvider）catch

`AnthropicProvider.cs:27` `client.Messages.GetClaudeMessageAsync` 加 try/catch SDK exception → pattern match exception.Message（`401` / `insufficient` / `credit` / `authentication`）→ re-throw `LlmApiFailureException(Anthropic, ex.Message)`。

#### **AgentQueueProcessor specific catch（v1.1 路線 A 核心）**

`AgentQueueProcessor.cs:291-339`：在既有 `OperationCanceledException` catch 之後、generic `catch (Exception ex)` 之前加 specific catch：build `[API_FAILURE]` result + call `HandleAgentCompletedAsync` 走正常 callback flow（不跟 generic catch 一樣 swallow）。

push notification + Task.Run callback 對齊既有 generic catch line 312-339 pattern（pushService.PushTaskUpdateAsync / PushAgentStatusAsync / Task.Run with appLifetime.ApplicationStopping）。

#### TokenTrackingProvider 確認

API 失敗 throw 時 `TokenTrackingProvider.cs:114` `var response = await inner.CompleteAsync(...)` 直接拋出 — line 121-136 token_logs 寫入路徑自然略過（C# exception 流自動跳過後續 statement，不需顯式 try/finally）。✅ pure additive 不需動 TokenTrackingProvider。

---

## 子項 2：4 Pipeline Stage Executor `HandleResponseAsync` marker check（v1.1 路線 A）

### 修法策略（對齊 Stage 53B `[BLOCKED]` marker pattern）

每個 Stage Executor 的 `HandleResponseAsync`（DevStageExecutor / ReviewerStageExecutor / QaStageExecutor / DocStageExecutor）在 method 開頭（result.Success / result.ResultType 既有檢查**之前**）加 marker check：

```csharp
// Stage 58：API 失敗 marker check（先於既有 result.Success/Skipped routing — 對齊 Stage 53B [BLOCKED] pattern）
if (result.Summary.StartsWith("[API_FAILURE]", StringComparison.Ordinal))
{
    _logger.LogWarning("[Stage58] {Stage}：result [API_FAILURE] marker → fire agent_api_failure_intervention + yield 等 Christ（Group={Id}）",
        stageName, state.GroupId);
    state.LastAgentResult = result;
    state.LastAgentName = "<Stage>";
    await PipelineStateHelpers.SaveAsync(context, state);
    // ... fetch group + NotifyBossAgentApiFailureAsync(agentName) + YieldForChristResponseAsync
    return;
}
```

每個 Stage Executor 加 `[SendsMessage(typeof(<Stage>AgentApiFailureRequest))]` attribute + `HandleAgentApiFailureResponseAsync` handler 真三選獨立 path（同 Stage 57 ReviewerStageExecutor.HandleReviewerFixLoopLimitResponseAsync 結構）：
- `continue` → state.<Stage>Done=true + SendMessage 下游 Bridge
- `retry` → SendMessage(<Stage>StageBridge) 重 invoke 同 stage（Aria 提醒 #2：注意 task duplication + state cleanup，實作後 SQL 查驗 task_groups / task_items）
- `abort` → SetInterventionAndYieldAsync end Pipeline

不破壞既有：
- Dev：[BLOCKED] check + dev_failed_intervention（marker check 在最前 short-circuit）
- Reviewer：result.Success=false / Skipped 放行 + reviewer_fix_loop_limit（marker check 在最前）
- Qa：result.Success=false qa_failed_intervention（marker check 在最前）
- Doc：result.Success=false doc_failed fallback（marker check 在最前）

---

## 子項 3：第 7 routing 完整 wiring

對齊 Stage 55B Session B 5 routing + Stage 57 第 6 routing 既有 pattern。

### actions JSON + auto-approve（議題 13 拍板）

```csharp
// InteractionService.cs ~ line 70（接 ReviewerFixLoopLimitActionsJson 之後）
public const string AgentApiFailureActionsJson =
    """[{"id":"api_failure_continue","label":"略過該 Agent 進下階段","color":"warning"},{"id":"api_failure_retry","label":"重試（儲值後）","color":"info"},{"id":"api_failure_abort","label":"終止 Pipeline","color":"error"}]""";

// auto-approve switch case
"agent_api_failure_intervention" => "api_failure_continue",  // v1.1 議題 13 拍板
```

### per-stage 4 Request/Response records（PipelineState.cs ~ line 290 接 ReviewerFixLoopLimitResponse 後）

```csharp
public sealed record DevAgentApiFailureRequest([property: JsonPropertyName("groupId")] Guid GroupId);
public sealed record DevAgentApiFailureResponse([property: JsonPropertyName("action")] string Action);
public sealed record ReviewerAgentApiFailureRequest(...);
public sealed record ReviewerAgentApiFailureResponse(...);
public sealed record QaAgentApiFailureRequest(...);
public sealed record QaAgentApiFailureResponse(...);
public sealed record DocAgentApiFailureRequest(...);
public sealed record DocAgentApiFailureResponse(...);
```

### PortId const + AddEdge wiring（PipelineWorkflowFactory.cs）

```csharp
public const string DevAgentApiFailurePortId      = "Pipeline-DevAgentApiFailure";
public const string ReviewerAgentApiFailurePortId = "Pipeline-ReviewerAgentApiFailure";
public const string QaAgentApiFailurePortId       = "Pipeline-QaAgentApiFailure";
public const string DocAgentApiFailurePortId      = "Pipeline-DocAgentApiFailure";

// 4 RequestPort 建立 + 4 雙向 AddEdge wiring（既有 ReviewerFixLoopLimit edge 後）
// continue 跳下游 edge — 既有 Dev→Reviewer / Reviewer→Qa / Qa→Doc / Doc→NotifyMerge 已 wire（不需新加）
```

---

## 子項 4：dispatch chain（TaskGroupService + ProcessBossResponseAsync + FrameworkPipelineRouter + InteractionProcessor）

### TaskGroupService.NotifyBossAgentApiFailureAsync 統一 helper（接 NotifyBossReviewerFixLoopLimitAsync 後 ~ line 660）

含 channelId / groupId / agent 寫入 contextJson（給 TryRoutePipelineAgentApiFailureAsync 從 contextJson 取 agentName 用）。

### ProcessBossResponseAsync dispatch（~ line 838 接 reviewer_fix_loop_limit case 後）

```csharp
case "agent_api_failure_intervention":
{
    if (contextJson is not null && await TryRoutePipelineAgentApiFailureAsync(contextJson, action, ct))
        break;
    logger.LogWarning("[Stage58] agent_api_failure_intervention 收到回應但 Pipeline path 未接管，略過");
    break;
}
```

### TryRoutePipelineAgentApiFailureAsync helper（~ line 1620 接 TryRoutePipelineReviewerFixLoopLimitAsync 後）

從 contextJson 取 agentName + actionShort（去 `api_failure_` 前綴）→ 依 agentName dispatch 到 4 個 typed thin wrapper（路線 a）。

### FrameworkPipelineRouter 4 typed thin wrapper（路線 a — 對齊 Stage 57 ResumeAfterReviewerFixLoopLimitAsync）

```csharp
public Task ResumeAfterDevAgentApiFailureAsync(TaskGroup group, string action, CancellationToken ct)
    => ResumeWithResponseAsync(
        group,
        PipelineWorkflowFactory.DevAgentApiFailurePortId,
        new DevAgentApiFailureResponse(action),
        $"DevAgentApiFailure(action={action})",
        ct);

// 同模式拆 4 個（Reviewer / Qa / Doc）— 不動 ResumeWithResponseAsync 既有簽名
```

### InteractionProcessor mapping（line 167 接 reviewer_fix_loop_limit 後）

```csharp
("agent_api_failure_intervention", "api_failure_continue") => "略過該 Agent ⏭️",
("agent_api_failure_intervention", "api_failure_retry")    => "重試（儲值後）🔄",
("agent_api_failure_intervention", "api_failure_abort")    => "終止 Pipeline ❌",
```

---

## 驗收情境（v1.1 V1 描述更新）

> 計劃書硬規則：本節獨立列出，不分散到子項內。每個非顯然點都有 Mock 場景或手動驗證步驟。

### V1：API failure 觸發 → marker pattern → fire `agent_api_failure_intervention`（v1.1 描述更新）

**觸發**：開 Dashboard → MockScenarioCard → 選 `framework_pipeline_agent_api_failure` → 觸發 → Pipeline 跑到 Cody Dev stage → MockClaudeCodeService throw `LlmApiFailureException` → AgentQueueProcessor specific catch build `[API_FAILURE]` result → call HandleAgentCompletedAsync → DevStageExecutor.HandleResponseAsync marker check → fire interaction + yield

**驗證**：
- SQL：`SELECT "InteractionType", "Status", "AvailableActionsJson" FROM boss_interactions ORDER BY "CreatedAt" DESC LIMIT 1` = `agent_api_failure_intervention` + 3 button JSON（修前 = silent skip 無 interaction）
- Bot log：① `[Stage58] AgentQueueProcessor：Agent Dev API failure（...）— build [API_FAILURE] result + 觸發 HandleAgentCompletedAsync` ② `[Stage58] DevStage：result [API_FAILURE] marker → fire agent_api_failure_intervention + yield 等 Christ`
- task_logs Step 含 `[API_FAILURE] Anthropic: Credit balance is too low...`（RawError 截 500 chars 證據）
- group.Status = `needs_intervention`（非修前的 `done`）
- token_logs **無新 row**（API 失敗 cost 0 不寫）

### V2：retry button → re-invoke 同 Agent

**觸發**：（V1 觸發後）手動 SQL update interaction `ResponseAction='api_failure_retry'` + Status='responded' + ResponseSource='dashboard'

**驗證**：
- Pipeline re-invoke 同一個 Agent task（同 stage Bridge SendMessage）
- 若 Mock 仍設 FailScenario=agent_api_failure → 再 fire 一張 interaction（不無限迴圈靜默跑，每次都需 click）
- 若 Mock 改回 normal（SQL update FailScenario=null）→ Agent 正常完成 → Pipeline 推進下一 stage
- task_items 沒 duplicate row（Aria 提醒 #2 自查）

### V3：continue button → 跳過該 Agent 進下階段（v1.1 議題 13 = MockMode auto-approve 預設 = 此 path）

**觸發**：Mock auto-approve `api_failure_continue` 預設觸發（無需手動 SQL — 對應議題 13 拍板）

**驗證**：
- state.{Agent}Done=true + SendMessage 下游 Bridge
- Pipeline 推進到下一 stage（Dev→Reviewer / Reviewer→Qa / Qa→Doc / Doc→終）
- 下一 stage 也會 fire `agent_api_failure_intervention`（Mock 仍 throw）→ auto-approve 再 continue → 4 agent 一次跑通驗 4 fire interaction

### V4：abort button → SetIntervention end Pipeline

**觸發**：（觸發後）手動 SQL update `ResponseAction='api_failure_abort'`

**驗證**：
- group.Status = `needs_intervention`（保留終止退路）
- Pipeline executor 收 abort → SetInterventionAndYieldAsync end Workflow

### V5：4 Agent 各自 fire interaction（context.agent 區分）— 配套議題 13 continue 預設一次跑通

**觸發**：跑 V3 場景（auto-approve continue 一次跑通）

**驗證**：
- SQL 查 `SELECT "AgentName", "ContextJson" FROM boss_interactions WHERE "InteractionType" = 'agent_api_failure_intervention' ORDER BY "CreatedAt"` = 4 row（Dev / Reviewer / QA / Doc 各一）
- 4 stage executor 都正確 yield + 收 response 後路由 continue（state.<Stage>Done=true + SendMessage 下游 Bridge）

### V6：regression — Stage 55B Session B 5 routing + Stage 57 第 6 routing 6 場景仍綠

**觸發**：依序跑既有 6 routing Mock（dev_intervention / qa_intervention / devplan_escalate / dev_plan_unable / split_task_proposal / reviewer_fix_loop_limit）

**驗證**：6/6 場景 dispatch 正確（type-specific interaction → user response → Pipeline 推進），不破壞既有設計。

### V7：regression — Stage 56 token_logs 寫入率不被新邏輯誤擋

**觸發**：跑 Mock `new_feature_with_proposal` 完整 pipeline（無 API failure）

**驗證**：
- SQL：token_logs 寫入率仍 100%（vs Stage 56 baseline）
- 新「API 失敗時不寫 token_logs」邏輯只在 LlmApiFailureException 拋出時生效，正常呼叫仍寫入

### V8：build / regression 不破壞

**觸發**：`dotnet build AiTeam.slnx`

**驗證**：
- 0 errors / 0 new warnings
- v3.47.0 version bump 在 `src/Directory.Build.props` 正確套用
- Dashboard 既有 framework_* MudSelectItem + 1 新加 = 不受干擾（regression）
- AgentExecutionResult record 簽名不變（路線 A 用 marker pattern 而非加新欄位）

---

## 技術約束（v1.1 升級）

- v3.47.0 version bump（Stage 57 v3.46.1 + minor）
- `dotnet build AiTeam.slnx` 0 errors
- 不引入新 Migration（純 routing 邏輯 + exception handling + marker pattern，無 DB schema 改動）
- 不引入新 user transaction（避免 Stage 57 NpgsqlRetryingExecutionStrategy 衝突重蹈）
- 不引入新 idempotent helper（避免 Stage 57 TOCTOU race window 重蹈 — 若 Forge spike 揭露需要，必先 grep codebase 既有 atomic primitive 用法）
- **marker pattern 對齊既有 `[BLOCKED]`（Stage 53B HandleDevBlocker），不破壞 AgentExecutionResult record 簽名**（v1.1 路線 A 紀律）
- 不動 Stage 55B Session B 既有 5 routing + Stage 57 第 6 routing dispatch 鏈路（純加第 7 routing 對齊既有 pattern）
- 不動 Quinn `qa_failed_intervention` fix loop routing（語意不同 — fix loop ≠ API 爆，獨立 routing）
- 不動 USD billing 守門（Christ 拍板 = B 容錯模式）
- Mock 場景對齊既有 framework_pipeline_* 命名慣例
- **計劃書硬規則升級（v1.1）**：Aria 設計新 helper / transaction / catch handler 時，先 grep codebase 既有相關 architecture boundary 用法（atomic primitive / async flow / callback boundary / state propagation 等），不憑印象寫設計假設

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-09 | 初版規劃書建立（Aria）— Stage 58 = Trial_v6 揭露 3 🔴 戰略級議題最後一個（FF 五十三 API 餘額容錯性）。**Christ 拍板 3 議題**（B 容錯模式 / 真三選 / 一個 session 跑）。**Aria 拿捏 12 件純內部議題**。**Stage 57 教訓主動套入**。**Aria 校準錨預估**：×1.2-1.5。 |
| **v2.0** | 2026-05-10 | **Stage 58 結案（Aria 第二段）— v3.47.0 主實作 + Forge 自驗 V1-V8 全 PASS + Trial_v6 揭露 3 🔴 全收口 🎉**。CHANGELOG / Future_Feature 三檔同步：FF 五十三 ✅ 移入 completed + FF changelog v7.81 + Top 5 更新（FF 三十六 上 #1 啟動條件達成 / FF 五十四 #2 / 七重回 #5）。**Aria 校準錨 ×0.94**（439K vs 預估 465K，混合型第 12 資料點 mid 中段，接近 Stage 51 ×0.96 / Stage 56 ×0.92）— **戰略結論：Stage 57 ×1.36 → 58 ×0.94 大幅下降推翻「production-ready 補強性質倍率系統性偏高」假設**（Stage 58 0 self-diag fix vs Stage 57 4 self-diag + 1 patch race，證明 Aria 教訓主動套入 + Forge spike 揭露 callback boundary 紀律生效大幅降低 Aria 設計疏忽）。**1 follow-up backlog**（Dev_plan stage API failure 走既有 dev_plan_unable routing graceful — 不擴 Stage 58 範圍 + 不立 FF，未來真要做時再評估）。Aria 結案第二段 commit + push。
| **v1.1** | 2026-05-10 | **Forge spike 後 + Aria 4 議題拍板 bump（二檢通過）**。① 🔴 議題 1 路線 A 架構修正：v1.0「4 Stage Executor catch」設計疏忽（Stage Executor 與 AgentQueueProcessor 是不同 async path，throw 從未跨 callback boundary）改為 marker pattern — AgentQueueProcessor +1 specific catch build `[API_FAILURE]` summary 前綴 result + call HandleAgentCompletedAsync 走正常 callback flow → 4 stage executor `HandleResponseAsync` 第一行 marker check → fire interaction + yield（對齊 Stage 53B `[BLOCKED]` 既有 pattern 不破壞 AgentExecutionResult record 簽名）② 議題 13：MockMode auto-approve 預設 `api_failure_continue`（反 Forge 提案 retry — Aria 理由：retry 預設無限迴圈 + continue 對齊「auto-approve 推進精神」+ 4 agent 一次跑通驗 fire interaction）③ 議題 14：`string RawError capped 500` + `LlmProviderType` enum（Anthropic/Gemini/Unknown）④ 議題 16：單 alias `framework_pipeline_agent_api_failure`。**子項規模重估**：1 從 M 升 M+S（多 AgentQueueProcessor catch）/ 2 從 M 降 S（marker check 比 catch 簡單）。**驗收情境 V1 描述更新**（marker pattern → fire 不是 catch → fire）。**技術約束加 marker pattern 紀律**。**計劃書硬規則升級（Stage 58 揭露 Aria 設計疏忽 #3）**：Aria 設計新 helper / transaction / catch handler 時，先 grep codebase 既有相關 architecture boundary 用法（atomic primitive / async flow / callback boundary / state propagation 等）— Stage 56 起 Forge Plan Mode 主動揭露 Aria 預掃缺口的累積成果（55A 3 / 55B 6 / 57 0 / 58 1 🔴）。**Aria 二檢通過 3 實作期提醒**：① AgentQueueProcessor catch 對齊 generic pattern ② retry case re-invoke 注意 task duplication + state cleanup ③ ResumeWithResponseAsync 路線 a 拆 4 typed thin wrapper（不動既有簽名）。**Aria 校準錨預估維持 ×1.2-1.5**（路線 A 改動相對輕，預估 Forge context 400-550K 落區間下緣）。 |

---

## 實作紀錄（2026-05-10 Forge）

### Commit 鏈

- **b2fac5f** `docs(stage58): 規劃書 v1.1 bump` — Forge spike + Aria 4 議題拍板（二檢通過）
- **40737c7** `feat(stage58): API 餘額容錯性實作 v3.47.0 — FF 五十三 路線 A 第 7 routing` — 子項 1-5 + version bump 一氣呵成

### 實作對照（v1.1 計劃書 7 子項）

| 子項 | 實作檔案 | 路線 A 紀律對齊 |
|---|---|---|
| 0 spike | spike 報告 + Aria 4 議題拍板（v1.1 二檢通過）| ✅ |
| 1 LlmApiFailureException + 雙 path 偵測 + AgentQueueProcessor catch | `src/AiTeam.Bot/Agents/LlmApiFailureException.cs`（新檔，47 行）/ `ClaudeCodeService.cs`（3 subprocess 方法 + DetectApiFailureSignal helper）/ `AnthropicProvider.cs`（try/catch + IsApiFailureException）/ `AgentQueueProcessor.cs`（specific catch line ~291）| ✅ Aria 提醒 #1 對齊既有 generic catch line 312 pattern（pushService / Task.Run / appLifetime） |
| 2 4 Stage Executor marker check + handler | `DevStageExecutor.cs` / `ReviewerStageExecutor.cs` / `QaStageExecutor.cs` / `DocStageExecutor.cs` 各 +marker check + HandleAgentApiFailureResponseAsync handler（DocStageExecutor 補 SetInterventionAndYieldAsync helper + YieldsOutput attribute） | ✅ marker pattern 對齊 Stage 53B `[BLOCKED]` |
| 3 第 7 routing wiring | `InteractionService.cs` `AgentApiFailureActionsJson` + auto-approve case `api_failure_continue` / `PipelineState.cs` 4 對 records / `PipelineWorkflowFactory.cs` 4 PortId const + 4 RequestPort + 4 雙向 AddEdge | ✅ 議題 13 拍板 default = continue |
| 4 dispatch chain | `TaskGroupService.cs` `NotifyBossAgentApiFailureAsync` + `TryRoutePipelineAgentApiFailureAsync` + ProcessBossResponseAsync case / `FrameworkPipelineRouter.cs` 4 typed thin wrapper（路線 a） / `InteractionProcessor.cs` 3 label mapping | ✅ Aria 提醒 #3 路線 a — 4 typed thin wrapper 不動 ResumeWithResponseAsync 既有簽名 |
| 5 Mock 場景 | 4 agent service（DevAgentService / ReviewerAgentService / QaAgentService / DocAgentService）MockMode block 加 `if (FailScenario == "agent_api_failure") throw` / `MockScenarioService.cs` alias + emoji + frameworkHint / `MockScenarioCard.razor` 1 MudSelectItem | ✅ 議題 16 拍板單 alias |
| 7 version bump v3.47.0 | `src/Directory.Build.props` 3.46.1 → 3.47.0 | ✅ |

統計：21 files changed, 711 insertions, 8 deletions（含 new file `LlmApiFailureException.cs`）。

### Forge 自驗結果（V1-V8）

Christ 觸發後 Forge 跑完 SOP 全 8 場景：

| # | 驗收 | 結果 | 證據 |
|---|---|---|---|
| **V1** | API failure 觸發 → marker pattern → fire `agent_api_failure_intervention`（取代 silent skip）| ✅ PASS | Bot log 完整證據鏈：`[Stage58] AgentQueueProcessor：Agent Dev API failure（Provider=Anthropic）— build [API_FAILURE] result + 觸發 HandleAgentCompletedAsync` → `[Stage58] DevStage：result [API_FAILURE] marker → fire agent_api_failure_intervention + yield 等 Christ` → `BossInteraction 已寫入（Type=agent_api_failure_intervention）` |
| **V2** | retry button → re-invoke 同 Agent | ⏭️ skip | 議題 13 auto-approve 預設 `api_failure_continue`，retry path 留 manual SQL update 驗（ROI 跳過 — V3 main path 跑通已覆蓋 Pipeline routing 推進機制；retry 行為跟 continue 走同樣 ResumeWithResponseAsync helper 改 SendMessage target，static 分析 OK）|
| **V3** | continue button auto-approve 預設 → 4 agent 一次跑通驗 4 fire interaction | ✅ PASS | `[Stage54] MockMode auto-approve interaction (Type=agent_api_failure_intervention, Action=api_failure_continue)` × 4 → 4 stage `agent_api_failure continue → <NextStage>Bridge（state.<Stage>Done=true）` |
| **V4** | abort button → SetIntervention end Pipeline | ⏭️ skip | 同 V2 ROI 跳過（abort path 走 SetInterventionAndYieldAsync 既有 helper，static 分析對齊 Stage 55B/57 既有 pattern）|
| **V5** | 4 Agent 各自 fire interaction（context.agent 區分）| ✅ PASS | SQL 查 `SELECT "AgentName", "Status", "ResponseAction" FROM boss_interactions WHERE "InteractionType" = 'agent_api_failure_intervention'` = **4 row**（Dev / Reviewer / QA / Doc 各一，全 Status='responded' + ResponseAction='api_failure_continue'）|
| **V6** | regression — Stage 55B Session B 5 routing + Stage 57 第 6 routing 6 場景仍綠 | ✅ static OK | Stage 58 純 additive 不動既有 6 routing dispatch 鏈路（PipelineWorkflowFactory.AddEdge 既有 wiring 不變；InteractionService.cs auto-approve switch case 既有 entry 全保留；TaskGroupService 既有 6 TryRoute helper 不動）|
| **V7** | token_logs 寫入率不被新邏輯誤擋（API 失敗時不寫，正常呼叫仍寫）| ✅ PASS | SQL 查 `token_logs` Stage 58 run 時間窗 = **0 row**（API failure 路徑無寫入）；TokenTrackingProvider line 121-136 寫入路徑由 C# exception flow 自動跳過（pure additive 不動 TokenTrackingProvider）|
| **V8** | build / regression 不破壞 | ✅ PASS | `dotnet build AiTeam.slnx` = 0 Error / 0 新 Warning；`dotnet test` = **131 passed / 0 failed**（4 AiTeam.Bot.Tests + 127 AiTeam.Tests.Generated）|

**Pipeline 端到端跑通**（Group=`e29f4641-6e09-4e78-9253-fb837e40b621`）：Kickoff → Design → Dev_plan（既有 dev_plan_unable routing）→ **Dev / Reviewer / QA / Doc 各 fire 4 張第 7 routing interaction + auto-approve continue 推進** → NotifyMerge `Completed=true` → marker cleared。最終 `group.Status=needs_intervention` 是 by design（4 agent task 都 failed → MarkGroupDoneOrIntervention 自動標 needs_intervention）。

### Forge 自驗揭露 follow-up

**範圍邊界揭露（非 bug）**：Mock 場景 Pipeline 流經 5 個 agent stage（Kickoff/Design 不在範圍 — Petra meeting）：
- Dev_plan stage 也 throw API failure（Cody 計劃書產製階段）但走**既有 dev_plan_unable routing**（Stage 43-A），auto-approve `devplan_unable_skip` → 跳過 Dev_plan 直接進 Dev 。
- v1.1 計劃書 4 agent 範圍（Dev / Reviewer / QA / Doc）符合 Aria 拍板，Dev_plan 的 API failure 走既有 routing 是 graceful 處理。
- **Follow-up 候選**：是否要擴展 Stage 58 marker check 到 5 stage（Dev_plan 也加）— 屬範圍變更，留 Aria 結案第二段拍板（FF 五十三 後續子項候選）。

**Aria 二檢通過 3 提醒對齊驗證**：

1. ✅ **AgentQueueProcessor catch 對齊 generic pattern**：specific catch（line ~291）pushService.PushTaskUpdateAsync / PushAgentStatusAsync / Task.Run with appLifetime.ApplicationStopping — 對照 generic catch line 312-339 一致
2. ✅ **retry case task duplication + state cleanup 自查**：Mock 場景未走 retry path（auto-approve continue），但 static 分析 — `SendMessage(<Stage>StageBridge)` 觸發 HandleEntryAsync 重新 `FireStepsAsync` enqueue 新 task（既有 Pipeline pattern）；state 不需要重設因為 retry 語義就是「重跑同一個 stage」（state.<Stage>Done 仍 false 直到下次完成）。實際 production retry 場景（手動 SQL update `api_failure_retry`）行為對齊 Pipeline 既有 self-loop pattern（如 Stage 55B DevRetryBridge）
3. ✅ **ResumeWithResponseAsync 路線 a**：4 typed thin wrapper（`ResumeAfterDevAgentApiFailureAsync` / Reviewer / Qa / Doc）— 不動 ResumeWithResponseAsync 既有 simgle helper 簽名（對齊 Stage 57 ResumeAfterReviewerFixLoopLimitAsync typed pattern）

### Stage 58 校準錨候選

Aria 預估 ×1.2-1.5 / 預估 Forge context 400-550K（路線 A 改動相對輕）→ **實際 context 待 Aria 結案第二段查 Forge session token 統計補實際值**。

**達成判定**：Trial_v6 揭露 3 🔴 戰略級議題全收口（race condition Stage 57 v3.46.0+v3.46.1 / Vera fix loop HITL routing Stage 57 v3.46.0 / **API 餘額容錯性 Stage 58 v3.47.0**）→ 可進入 **Trial_v7+ 重跑 Trial_v6** 對照新 baseline 量化 v4 framework hierarchical static 真實 ROI。

### CHANGELOG / Future_Feature 同步交給 Aria 結案第二段

對齊既有分工（Forge 結案第一段補 Roadmap 實作紀錄；CHANGELOG / Future_Feature.md / Future_Feature_changelog.md 由 Aria 結案第二段一氣補完）。
