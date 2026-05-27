# Stage 78b — v4 path dead caller 整套砍除（純 refactor / 接續 Stage 78a 後續）

> 對應系統版本：v3.69.0
> 規模：M
> 狀態：✅ 已完成（2026-05-19）
> 文件版本：v2.0
> 性質：純 refactor / 0 業務變化 / 0 Migration / entry 0 使用
> Model + Effort 建議：**Opus 1M + Extra high**（對齊 Stage 78a 大規模架構級重構新區間 ×1.57-4.07 紀律延續 + 連續 6 Stage 自選 Opus 1M 真實使用模式）
> cost 預估：$3-5 per cycle
> commit：[`a7c763a`](https://github.com/darkleong/AiTeam/commit/a7c763a) feat(stage78b): v4 path dead caller 整套砍除 v3.69.0 — 路線 C 折衷

---

## 戰略脈絡

Stage 78a 砍完 v4 path dead code（3 純 v4 + 4 雙路徑 class v4 method / net -3957 行）後，**v4 path 仍殘留 6 條 dead caller path 未砍**。Christ 2026-05-19 拍板「`/task` slash command + GitHub Issue webhook 都 0 使用」+「Dashboard 主用 / Discord slash command 完全沒用」— 配合 Trial_v6-v22 連續 17 次 v5.5 path 業務級驗證 0 v4 caller 累積，6 條 entry 已是 effective dead code。

**Stage 78b 範圍**：純 caller 砍 / 純 refactor 性質 / 0 業務變化（因 entry 都 0 使用）/ 0 Migration / 低風險。

**Stage 78c 預留範圍**（規模 L / 留下一輪做）：v4 Pipeline framework 整套砍 — TaskGroupService + ProposalConfirmationService + DevStageExecutor 等 Stage Executors + AgentQueueService + AgentQueueProcessor + agent_queues 表 + Migration drop table + 連動 ButtonCallbackRouter propose_yes/cancel_yes/confirm_yes/kickoff_*/design_*/framework_* v5.5 active routing 評估 + WebhookController PR open / PR synchronize / push 事件 handler 評估 + 其他 SlashCommand（reload-rules / status / pause / resume / queue / new-session / mock）評估。

### Phase 4 路徑（Christ 2026-05-19 拍板修正版）

```
Stage 78a ✅ → 78b（本 Stage / 純 refactor 6 條 / M）
            → 78c（v4 Pipeline 整套砍 / 規模 L）
            → 79（A HITL plan confirmation 閘門 / M）
            → 80（B 動態 re-planning / L）
            → WebUI Stage（K WebUI Talent CRUD + Effort 擴展 + E Token monitoring 視覺化）
            → v5.5 完整收口
```

### 範圍邊界刻意收緊

- ❌ v4 Pipeline framework 整套（推 Stage 78c — 規模 L / 涉及 Migration drop agent_queues 表 / 涉及 v4 vs v5.5 routing 邊界 ProposalConfirmationService / TaskGroupService / 多個 Stage Executors 連動）
- ❌ HITL plan confirmation（推 Stage 79 — Christ 親口要的業務功能 / 涉及 framework HITL 整合 + BossInteraction pattern）
- ❌ 動態 re-planning（推 Stage 80 — 規模 L / 需配套 max iterations + replan threshold + cost cap + checkpoint replay）
- ❌ 其他 SlashCommand 評估（reload-rules / status / pause / resume / stop-all / resume-all / queue / new-session / mock — 留 78c 評估 / `/mock` 可能 Forge 自驗用）
- ❌ WebhookController PR open / PR synchronize / push 事件 handler（留 78c 評估 v5.5 path 真實依賴）

---

## 子項清單

### 1. ButtonCallbackRouter v4 routing 砍

**範圍**：`src/AiTeam.Bot/Discord/Routing/ButtonCallbackRouter.cs` 內 v4 dead routing case + 對應 helper。

**v4 routing case**（grep verify 真實 line range）：
- `exec_yes` / `exec_no`（line 267-270 + line 352 else 分支）— v4「執行任務確認」path
- `escalate_devplan_skip`（line 302-328）— v4 Dev_plan 審核跳過 path
- `escalate_devplan_abort`（line 329-351）— v4 Dev_plan 審核放棄 path
- `escalate_skip` / `escalate_abort`（line 293-301）— 已標「已不適用」純 dead

**對應 helper 連動砍**：
- `HandleExecYesAsync` 及其呼叫的 v4 Pipeline helper（grep verify）
- `BuildConfirmButtons("exec_yes", "exec_no")` 兩處 caller（line 426 / line 683 / "exec_confirm" event 對應段）
- `SupersedePriorFailedTasks` 若 0 v5.5 caller → 砍（grep verify）

**保留**（v5.5 active routing）：
- `confirm_yes` / `confirm_no`（Stage 68 收尾）
- `propose_yes` / `propose_no` / `propose_adjust`（留 Stage 78c 評估 / 跟 v4 Pipeline 整套砍一起做）
- `cancel_yes` / `cancel_no`
- `kickoff_*` / `design_*` / `framework_kickoff_mid_interrupt_*`

**規模預估**：原 ~1211 行 → 預期下降 100-200 行（v4 routing case + helper 砍）。

### 2. OpsAgentService IAgentExecutor 實作砍（保留 production active method）

**範圍**：`src/AiTeam.Bot/Ops/OpsAgentService.cs`

- 砍 `ExecuteTaskAsync` method（line 38-47 / 10 行 / IAgentExecutor 實作 / 內容只發 alert 訊息 return result）
- class declaration 改 `: IAgentExecutor` 移除 implements clause

**保留**（production active）：
- `MonitorDeploymentAsync`（line 52-）
- `MonitorCiCdAsync`（line 142-）
- `RunHealthCheckAsync`（line 216-）
- `AlertAsync`（line 256-）
- `HealthCheckJob` IJob 實作（line 389+ / Quartz 定時 job / Program.cs:239 register）
- DI ctor + 其他 dependency 全保留

**DI 註冊連動**：
- `Program.cs:67` `AddSingleton<OpsAgentService>()` 保留（HealthCheckJob 相依）
- `Program.cs:68` `AddKeyedSingleton<IAgentExecutor, OpsAgentService>(AgentNames.Ops)` 砍

### 3. IAgentExecutor interface 砍

**範圍**：`src/AiTeam.Bot/Agents/IAgentExecutor.cs` 砍 + 對應 type（AgentExecutionResult / AgentEvent 等）grep verify 0 caller 砍。

**前置條件**：子項 1 + 2 完成 → 0 implementation + 0 caller。

**對應 caller 預期狀態**（子項 1+2 砍後）：
- `ButtonCallbackRouter.cs:739` `scope.ServiceProvider.GetKeyedService<IAgentExecutor>(...)` — 隨子項 1 v4 routing 砍消失
- `AgentQueueProcessor.cs:190` `scope.ServiceProvider.GetKeyedService<IAgentExecutor>(executorKey)` — 留 Stage 78c 砍（不在本 Stage 範圍 / **本 Stage 留著 AgentQueueProcessor 不動**）

**注意**：若 `AgentQueueProcessor.cs:190` 還有 IAgentExecutor caller → 78b 不能砍 IAgentExecutor interface。**真實砍時機要 grep verify AgentQueueProcessor 是否還在 Stage 78b 範圍動或留 78c** — Forge plan 階段拍板。

**fallback 範圍**：若 IAgentExecutor 在 AgentQueueProcessor 還 active → 子項 3 砍 IAgentExecutor 推 Stage 78c / 子項 3 改為「IAgentExecutor 0 implementation 標記 Stage 78c 預備砍」純文件動作。

### 4. `/task` slash command + HandleTaskCommandAsync 整段砍

**範圍**：`src/AiTeam.Bot/Discord/Routing/SlashCommandRouter.cs`

- 砍 `BuildCommandDefinitions()` 內 `/task` 定義（line 48-54 / 7 行 / `WithName("task")` + 3 option）
- 砍 dispatch 內 `"task" => HandleTaskCommandAsync(command)` line 151
- 砍 `HandleTaskCommandAsync` method body 整段（line 173-...）

**保留**：
- 其他 SlashCommand 定義 + handler（reload-rules / status / pause / resume / stop-all / resume-all / queue / new-session / mock）留 78c 評估

### 5. GitHub Issue webhook handler 砍

**範圍**：`src/AiTeam.Bot/GitHub/WebhookController.cs`

- 砍 `HandleIssueOpenedAsync` method body 整段
- 砍 `DispatchEventAsync` 內 `case "issues" when ...` 段（line 79-81）

**保留**：
- `HandlePrOpenedAsync` / `HandlePrSynchronizedAsync` / `HandlePushAsync` 留 Stage 78c 評估（v5.5 path 真實依賴情況需 grep verify）

### 6. CeoAgentService.ProcessAsync 整段砍 + v4 helper

**範圍**：`src/AiTeam.Bot/Agents/CeoAgentService.cs`

**前置條件**：子項 4 + 5 完成 → ProcessAsync 0 caller。

**砍**：
- `ProcessAsync` method body 整段（line 44-78 / ~35 行）
- `BuildSystemPrompt` helper（line 115-195 / ~80 行 / ProcessAsync line 56 唯一 caller）
- `BuildUserMessageAsync` helper（line 197- / ProcessAsync line 57 唯一 caller）
- `BuildGitHubContextAsync` helper（line 247- / BuildUserMessageAsync 唯一 caller）
- `TryParseResponse` helper（ProcessAsync line 64 唯一 caller）

**ctor dependency 砍**：
- `LlmProviderFactory providerFactory`（ProcessAsync line 54 唯一 caller）
- `TaskRepository taskRepository`（BuildUserMessageAsync 唯一 caller）
- `GitHubService gitHubService`（BuildGitHubContextAsync 唯一 caller）
- `IOptions<GitHubSettings> gitHubSettings` + `_github` field（BuildGitHubContextAsync 唯一 caller）

**保留**（v5.5 active）：
- `ProcessWithClaudeCodeAsync`（line 85-111 / Stage 78a 已改成 v5.5 flag forward only）
- `PetraInboxRepository petraInboxRepository`（Stage 75 / ProcessWithClaudeCodeAsync line 99 用）
- `AppDbContext db`（Stage 75 / ProcessWithClaudeCodeAsync line 100 用）
- `ILogger<CeoAgentService> logger`

**ctor dep grep verify 紀律**：砍 ctor 4 dep 前必 grep 確認真實 0 caller — 對齊 Stage 78a 教訓「砍 ctor dep 前 grep verify 雙資料源」（DI 註冊段 + class 內所有 method）。

### 7. Directory.Build.props v3.68.0 → v3.69.0

---

## 設計決策

### 1. 純 refactor 性質 / 0 業務變化

對齊 entry 全 0 使用紀律：`/task` slash command（Christ 拍板「Discord slash command 完全沒在使用」）+ GitHub Issue webhook（Christ 拍板「完全沒用過」）— 砍掉 0 業務影響。

### 2. backwards-compatible 守護延續（v5.5 path 0 行為改變）

- Dashboard → API `/internal/ceo/command` → CeoAgentService.ProcessWithClaudeCodeAsync → PetraInbox forward → PetraDispatchWorker → Petra → Cody chain — 0 行為改變
- Discord @Christ mention → CommandHandler → ProcessWithClaudeCodeAsync → 同 chain — 0 行為改變
- ButtonCallbackRouter v5.5 routing（kickoff_* / design_* / confirm_yes / propose_yes / cancel_yes 等）— 0 行為改變

### 3. Stage 78c 範圍邊界明確

Stage 78b **不動**：
- v4 Pipeline framework（TaskGroup + ProposalConfirmation + Stage Executors + AgentQueueService + AgentQueueProcessor + agent_queues 表）
- ButtonCallbackRouter v5.5 active routing（propose_yes / cancel_yes / kickoff_* / design_* / framework_* / confirm_yes — 跟 v4 Pipeline 整套砍一起評估）
- WebhookController PR 事件 handler / push 事件 handler（v5.5 path 真實依賴需 grep verify）
- 其他 SlashCommand（reload-rules / status / pause / resume / queue / new-session / mock — `/mock` 可能 Forge 自驗用 / 整體推 78c 評估）

### 4. Forge spike 揭真實 propagation 紀律延續（對齊 Stage 78a 教訓）

Forge plan 階段必 grep verify：
- ButtonCallbackRouter v4 routing case 真實 line range + 對應 helper caller chain
- OpsAgentService IAgentExecutor 砍後 production active method 不受影響（HealthCheckJob 仍 active）
- IAgentExecutor interface 砍前最後 0 caller verify（特別 AgentQueueProcessor.cs:190 — 若還在 Stage 78b 範圍外 active → 子項 3 fallback）
- CeoAgentService ctor 4 dep 砍前真實 0 caller verify（LlmProviderFactory / TaskRepository / GitHubService / GitHubSettings）

### 5. 0 Migration / 0 schema 改動

純 refactor 性質 / 不涉及 agent_queues 表 / 不涉及 PetraInbox schema / 0 Migration。

---

## 驗收情境

### 場景 A：xUnit baseline 0 regression

**觸發**：`dotnet test AiTeam.slnx`
**驗證**：Bot.Tests 104 + Generated 127 全綠（對齊 Stage 78a 結束 baseline）。任何砍動作不能破壞既有 test。

### 場景 B：ButtonCallbackRouter v4 routing 砍 + v5.5 routing 0 行為改變

**觸發**：
- xUnit test：對 v5.5 routing case（confirm_yes / propose_yes / cancel_yes / kickoff_* / design_* / framework_*）的單元測試（若有）
- production 自驗：Dashboard 派 task → v5.5 path 完整 chain 跑通（對齊 Trial_v22 baseline）

**驗證**：
- `ButtonCallbackRouter.cs` 行數下降 100-200 行（grep verify）
- `grep -E '"exec_yes|"exec_no|"escalate_devplan_skip|"escalate_devplan_abort"' ButtonCallbackRouter.cs` 0 match
- v5.5 routing case 邏輯不變（diff 對照）
- production 5 層守門全綠

### 場景 C：OpsAgentService IAgentExecutor 砍 + HealthCheckJob production active

**觸發**：
- xUnit test：OpsAgentService 對應 test 砍 ExecuteTaskAsync case（若有）
- production 自驗：Bot 啟動 + Quartz HealthCheckJob 定時 fire（log 確認 RunHealthCheckAsync + MonitorCiCdAsync 跑）

**驗證**：
- `grep -nE 'ExecuteTaskAsync' OpsAgentService.cs` 0 match
- `grep -nE 'public class OpsAgentService' OpsAgentService.cs` 不含 `: IAgentExecutor`
- Bot 啟動 log 出現 `HealthCheckJob` schedule 紀錄
- `Program.cs` `AddKeyedSingleton<IAgentExecutor, OpsAgentService>(AgentNames.Ops)` 砍 / `AddSingleton<OpsAgentService>()` 保留

### 場景 D：IAgentExecutor interface 砍

**觸發**：`dotnet build AiTeam.slnx` 0 error
**驗證**：
- `IAgentExecutor.cs` 檔案砍 + 對應 type（AgentExecutionResult / AgentEvent 等）grep verify 0 caller 後砍
- 若 `AgentQueueProcessor.cs:190` 還有 IAgentExecutor caller → 子項 3 fallback（IAgentExecutor 0 implementation 標記 78c 預備砍 / 不砍 interface 檔案）
- dotnet build 0 error 0 warning

### 場景 E：`/task` slash command 砍

**觸發**：
- Bot 啟動 + Discord slash command 註冊 sync
- Discord 端嘗試 `/task` 選項

**驗證**：
- `grep -nE '"task"|WithName\("task"\)|HandleTaskCommandAsync' SlashCommandRouter.cs` 0 match
- Discord 端 `/task` 選項不存在（slash command list 不含 task）
- 其他 SlashCommand（reload-rules / status / pause / resume / queue / new-session / mock）正常顯示

### 場景 F：GitHub Issue webhook handler 砍

**觸發**：模擬 GitHub Issue open webhook POST `/webhook/github` 含 `X-GitHub-Event: issues`
**驗證**：
- `grep -nE 'HandleIssueOpenedAsync|case "issues"' WebhookController.cs` 0 match
- POST `/webhook/github` issues event → Bot log 出現「忽略事件：issues」（DispatchEventAsync default case）
- 0 call CeoAgentService.ProcessAsync

### 場景 G：CeoAgentService.ProcessAsync + v4 helper 砍

**觸發**：`dotnet build AiTeam.slnx` 0 error + `grep -nE 'ProcessAsync\(|BuildSystemPrompt\(|BuildUserMessageAsync\(|BuildGitHubContextAsync\(|TryParseResponse\(' src/AiTeam.Bot/Agents/CeoAgentService.cs` 0 match
**驗證**：
- CeoAgentService.cs class body 只剩 `ProcessWithClaudeCodeAsync` + JsonOptions field + logger
- ctor 砍 4 dep（LlmProviderFactory / TaskRepository / GitHubService / GitHubSettings）→ 只剩 petraInboxRepository + db + logger
- dotnet build 0 error 0 warning（grep verify 0 missing reference）

### 場景 H：v5.5 path production 0 regression（Aria gate2 真實業務驗）

**觸發**：Christ 開 Dashboard 派一個簡單 task（對齊 Trial_v22 baseline prompt）

**驗證**：
- API `/internal/ceo/command` 收到指令 → CeoAgentService.ProcessWithClaudeCodeAsync → PetraInbox row 寫入 → return v5.5 ack
- PetraInboxProcessor pickup row → PetraDispatchWorker dispatch → Petra orchestrator → Cody/Vera/etc chain
- PR 真開 + Cody success
- token_logs 真實寫入（PM=gemini / Cody=claude-code）
- Dashboard 操作中心顯示 task 進度
- 對齊連續 13 Trial 業務級成功 baseline

### 場景 I：Bot startup 0 exception（DI registration 完整性）

**觸發**：Bot 啟動 + Application.RunAsync 完整跑通
**驗證**：
- Bot startup log 0 exception（特別 DI ServiceProvider validation）
- 砍 4 ctor dep 後 CeoAgentService Scope register 正常
- 砍 IAgentExecutor 後（若子項 3 整套砍）OpsAgentService keyed register 已移除 0 missing service

---

## 技術約束

- 0 Migration（純 refactor / 對齊 ef-core.md 紀律）
- 0 nuget 砍（Stage 78a 已砍 Microsoft.Agents.AI.Anthropic / Anthropic.SDK 保留 Petra LlmProviderFactory 用）
- 0 archive prompt（Stage 78a 已砍 CLAUDE_Demi.md + CLAUDE_Rosa.md）
- v5.5 path 0 行為改變紀律守
- Forge plan 階段必走 spike grep verify 紀律（對齊 Stage 78a 教訓 — IAgentExecutor 真實 0 caller / OpsAgentService production active method 不受影響 / CeoAgentService ctor 4 dep 真實 0 caller）

---

## ⚠️ Aria 預警（對齊 Stage 78a 教訓 + 連續 6 Stage 自省點 #37 紀律延伸）

### W1：ButtonCallbackRouter v4 vs v5.5 routing 邊界 grep verify

`exec_yes` / `escalate_devplan_*` 是 v4 routing 確定可砍。但 `propose_yes` / `cancel_yes` / `confirm_yes` / `kickoff_*` / `design_*` / `framework_*` 是 v5.5 active routing **不能砍** — 78c 範圍跟 v4 Pipeline 整套砍一起評估。Forge plan 階段必 grep verify routing case 完整列表 + v4 vs v5.5 邊界精準分辨。

### W2：OpsAgentService 砍 IAgentExecutor 後 HealthCheckJob 不受影響

HealthCheckJob 透過 `OpsAgentService` Singleton 直接呼叫 production active method（RunHealthCheckAsync + MonitorCiCdAsync）— 砍 IAgentExecutor 實作 + keyed register 不影響 HealthCheckJob。但 Bot startup `q.AddJob<HealthCheckJob>(...)` Quartz 註冊段必驗證仍正常。

### W3：IAgentExecutor interface 砍前 AgentQueueProcessor:190 active 風險

`AgentQueueProcessor.cs:190` `scope.ServiceProvider.GetKeyedService<IAgentExecutor>(executorKey)` 仍 active（Stage 78c 才砍 AgentQueueProcessor）— 78b 子項 3 砍 IAgentExecutor interface 前必 grep verify 是否 0 caller。若 AgentQueueProcessor 還在 → 子項 3 fallback（純標記 78c 預備砍 / interface 檔案不動）。

### W4：CeoAgentService ctor dep 砍 grep verify

砍 LlmProviderFactory / TaskRepository / GitHubService / GitHubSettings 4 dep 前必 grep：
- LlmProviderFactory `providerFactory.Create` 全 caller（class 內）
- TaskRepository `taskRepository.` 全 caller
- GitHubService `gitHubService.` 全 caller
- _github / gitHubSettings 全使用點

對齊 workflow_aria.md 第三節 A 第 7 條延伸範圍 #7「規劃 capability 砍時必 grep 雙資料源」紀律延伸到 ctor dep 砍領域。

### W5：BuildSystemPrompt / BuildUserMessageAsync / BuildGitHubContextAsync / TryParseResponse 砍順序

cascading dependency：BuildGitHubContextAsync ← BuildUserMessageAsync ← ProcessAsync / BuildSystemPrompt ← ProcessAsync / TryParseResponse ← ProcessAsync。建議砍順序：先砍 ProcessAsync method body → 然後 BuildSystemPrompt / BuildUserMessageAsync / TryParseResponse → 最後 BuildGitHubContextAsync。或對齊 Forge spike 揭真實後重排。

### W6：子項 1+2+3 vs 子項 4+5+6 內部依賴鏈

- 子項 1+2 → 子項 3（IAgentExecutor 0 caller verify）
- 子項 4+5 → 子項 6（ProcessAsync 0 caller verify）

兩條獨立依賴鏈 — Forge 可分兩 commit 或一 commit 拍板。

### 校準錨預估（自省點 #37 雙因子三步法）

- raw 預估：80-130K（範圍精準 entry 0 使用驗證後純 refactor 性質 / 對齊 Stage 78a 後續純 caller 砍）
- ratio 預估：×1.5-2.5（對齊大規模架構級重構新區間 ×1.57-4.07 下界 — 純 refactor 性質 / 0 Migration / spike 多輪可能性低 / Aria gate1 守門 1 輪）
- 真實 context 預估：~150-280K（Plan + 實作 + 驗收 + 結案 累積）
- safety：Opus 1M = 15-28% 利用率（充裕）
- Forge spike 揭真實風險：中等（W1-W4 四條主要 grep verify 預警 / 對齊 Stage 78a 教訓多輪 spike 風險）

### Forge Plan Mode 起手紀律

1. 對齊 workflow_forge.md + forge-self-verify skill 紀律
2. 先 grep verify W1-W4 真實狀態 → 拍板子項依賴鏈 + fallback 策略
3. spike 揭真實後 escalate Christ 拍板（對齊 Stage 78a 連續 3 輪 spike 經驗 / 範圍變動必 escalate）
4. v5.5 path 0 行為改變紀律守
5. 配套 propagation 範圍精準（不擴 Stage 78c 範圍 / propose_yes / cancel_yes / kickoff_* / design_* / framework_* / AgentQueueProcessor / TaskGroupService / ProposalConfirmationService 等 v5.5 active 或 78c 範圍**完全不動**）

---

## 實作紀錄（v2.0 / 2026-05-19）

### 實作完成項目

**鏈 A — v4 routing dead caller 整套砍**：

- ✅ **§A 子項 1 ButtonCallbackRouter v4 routing 砍**（路線 C 升級）
  - 砍 5 routing case：`exec_yes` (line 267-270) / `escalate_skip` + `escalate_abort` (line 293-301) / `escalate_devplan_skip` (line 302-328) / `escalate_devplan_abort` (line 329-351)
  - 砍 5 method：`HandleExecYesAsync` (line 457-505) / `ExecuteAgentTaskAsync` (line 707-857) / `ResolveWorkflowType` (line 932-942) / `SupersedePriorFailedTasks` (line 989-1007) / `GetAgentChannelName` (line 1056-1067)
  - **路線 C 升級加砍**：`HandleConfirmYesAsync` v4 body line 393-454（保留 Stage 68 短路 line 386-391 + defensive log+ack fallback / method 縮為 ~15 行）
  - **路線 C 升級 grep verify cleanup**：`BuildEscalateButtons` (0 caller after escalate_skip/abort routing 砍 → 砍) / `BuildAgentPlanEmbed` 留（ShowDirectAgentConfirmAsync line 682 仍 caller — 路線 C 預期路徑 A 確認）
  - 連動 ctor param 砍：`rulesService` 0 caller after → 砍
  - 變動：-362 行 (1075 → 713)

- ✅ **§B 子項 2 OpsAgentService IAgentExecutor 實作砍**
  - class declaration 移除 `: IAgentExecutor`
  - 砍 `ExecuteTaskAsync` method (line 38-47)
  - 砍 `using AiTeam.Bot.Agents;` (IAgentExecutor / AgentExecutionResult import 0 caller after)
  - **保留 production active**: `MonitorDeploymentAsync` / `MonitorCiCdAsync` / `RunHealthCheckAsync` / `AlertAsync` / `HealthCheckJob` Quartz job
  - Program.cs:68 `AddKeyedSingleton<IAgentExecutor, OpsAgentService>(AgentNames.Ops)` 砍 / line 67 `AddSingleton<OpsAgentService>()` 留（HealthCheckJob ctor inject 用）
  - 變動：-10 行 + Program.cs 註解 update

- ✅ **§C 子項 3 IAgentExecutor interface fallback 純文件標記 78c**
  - IAgentExecutor.cs 整檔 0 動（W3 fallback 紀律守）
  - XML doc summary 加 6 行 fallback 紀律說明：AgentQueueProcessor.cs:190 `GetKeyedService<IAgentExecutor>(executorKey)` 仍 active + `AgentExecutionResult`/`AgentResultType`/`AgentDescriptor` 廣泛使用於 PipelineState/Executors/FrameworkAppealRouter/AppealOrchestrationService/QaCoordinationService/MeetingOrchestrationService/FrameworkPipelineRouter/TaskGroupService 12+ file → Stage 78c 預備砍

**鏈 B — v4 LLM-direct caller 整套砍**：

- ✅ **§D 子項 4 SlashCommandRouter /task 砍**
  - slash command 定義砍 (line 48-54)
  - dispatch case "task" 砍 (line 151)
  - `HandleTaskCommandAsync` 整 method 砍 (line 173-275 / ~103 行)
  - ctor 從 11 dep → 6 dep（砍 5 dep：`buttonRouter` / `store` / `interactionService` / `appSettings` / `gitHubSettings`）
  - 砍 2 unused using：`System.Text.Json` / `AiTeam.Bot.Agents`
  - **保留 9 個其他 slash command**：`/reload-rules` / `/status` / `/new-session` / `/mock` / `/pause` / `/resume` / `/stop-all` / `/resume-all` / `/queue`
  - 變動：-125 行

- ✅ **§E 子項 5 WebhookController HandleIssueOpenedAsync 砍**
  - dispatch case "issues" 砍 (line 79-81)
  - `HandleIssueOpenedAsync` 整 method 砍 (line 105-147 / ~43 行)
  - ctor param `rulesService` 0 caller after → 砍
  - 砍 1 unused using：`AiTeam.Bot.Agents`
  - **保留 3 個其他 event handler**：`HandlePrOpenedAsync` / `HandlePushAsync` / `HandlePrSynchronizedAsync`
  - 變動：-52 行

- ✅ **§F 子項 6 CeoAgentService.ProcessAsync + 4 v4 helper + ctor 4 dep 砍**
  - `ProcessAsync` 整 method 砍 (line 44-78 / ~35 行)
  - 4 v4 helper 砍：`BuildSystemPrompt` / `BuildUserMessageAsync` / `BuildGitHubContextAsync` / `TryParseResponse` (~191 行)
  - ctor 從 8 dep → 3 dep（砍 4 dep：`providerFactory` / `taskRepository` / `gitHubService` / `gitHubSettings`）
  - 砍 `_github` private field + `JsonOptions` static field + 7 unused using statements
  - class 縮為純 v5.5 path（ProcessWithClaudeCodeAsync 寫 PetraInbox + return ack / ~50 行）
  - 變動：-229 行

**子項 7**：
- ✅ **§G 子項 7 Directory.Build.props v3.68.0 → v3.69.0**（3 處 `<Version>` / `<AssemblyVersion>` / `<FileVersion>`）

### 關鍵設計決策

#### 1. W1 邊界議題 Christ 拍板路線 C 折衷（2026-05-19）

Plan v1 階段 Forge spike 揭真實揭 1 邊界議題：`HandleConfirmYesAsync` v4 body line 393-454 在砍 `exec_yes` routing 後仍 fire 但會 post `exec_yes`/`exec_no` 按鈕造成 broken UI（用戶點按鈕 router 落入 else 顯示「已取消」）。

Plan v1 列 3 路線給 Christ 拍板：
- 路線 A 嚴格 Roadmap scope（不砍 v4 body / codebase incoherent / production 0 影響因 0 fire 假設）
- 路線 B 擴範圍 cascade 砍整套含 CommandHandler / scope creep 翻倍
- 路線 C 折衷（砍 v4 body + BuildAgentPlanEmbed grep verify 後決定 / ShowDirectAgentConfirmAsync + HandleDirectAgentChannelMessageAsync 留 Stage 78c）

**Christ 拍板路線 C** — 理由：「對齊『修根因 > 補丁』+『對冗餘不容忍』+ 不擴 scope 到 CommandHandler 保 Stage 規模可控」。

實作：HandleConfirmYesAsync method body 縮為 ~15 行（Stage 68 短路 + defensive log+ack fallback）/ BuildAgentPlanEmbed grep verify 後 ShowDirectAgentConfirmAsync 仍 caller → BuildAgentPlanEmbed 留 / 額外 grep verify BuildEscalateButtons 0 caller → cleanup 砍。

#### 2. W3 fallback IAgentExecutor interface 整檔 0 動

Forge spike grep verify：`AgentQueueProcessor.cs:190` `GetKeyedService<IAgentExecutor>(executorKey)` 仍 active + `AgentExecutionResult` / `AgentResultType` / `AgentDescriptor` record/enum 廣泛使用於 PipelineState / Executors（5 個 Stage Executor）/ FrameworkAppealRouter / AppealOrchestrationService / QaCoordinationService / MeetingOrchestrationService / FrameworkPipelineRouter / TaskGroupService **12+ file**。

結論：子項 3 必 fallback — IAgentExecutor.cs 整檔 0 動 / XML doc 加 fallback 註解標記 Stage 78c 預備砍。

#### 3. R6 ResolveWorkflowType 精準匹配紀律（Christ 補強 #3）

`ButtonCallbackRouter.cs:932 ResolveWorkflowType` (v4 砍) vs `ProposalConfirmationService.cs:320 ResolveWorkflowTypeInternal` (v5.5 留) — 兩 method 同源命名不同檔不同實作。

實作時用 word boundary `\b` 精準匹配（非 substring match）— Forge grep verify pass / ProposalConfirmationService.ResolveWorkflowTypeInternal 不誤砍。

#### 4. ctor unused dep cleanup 紀律延伸

砍 v4 method 後 ctor 對應 unused dep 一併砍（對齊「對冗餘不容忍」+ CS9113 warning cleanup）：
- CeoAgentService：8 dep → 3 dep（砍 4 dep）
- SlashCommandRouter：11 dep → 6 dep（砍 5 dep）
- WebhookController：7 dep → 6 dep（砍 `rulesService`）
- ButtonCallbackRouter：9 dep → 8 dep（砍 `rulesService`）

Build warning 從 baseline 102 ↓ 59（-43 warnings / -42%）— v4 dead caller 砍掉的 unused ctor param + 砍掉的 method body warning 大幅清除。

### Mock 覆蓋情況

**N/A** — Stage 78b 為純 refactor / v4 dead caller 砍除 / 0 entry 點改動 / 0 新業務邏輯。Mock scenarios 不適用（v4 path 已砍 / v5.5 path 0 行為改變紀律守）。

Forge 自驗範圍走 **grep verify 9 條件 + production deploy verify**（對齊 Plan v2 §Verification gate1 範圍）— 場景 E/F/G/H/I 留 Aria gate2 範圍 production 真實 task 驗證。

### 驗收後修正

**0 follow-up bug 揭露** — 一次性 commit 成功 / 0 自診修補需要 / Forge 自驗 0 issue。

Forge 自驗結果（Christ 觸發 forge-self-verify skill）：
- ✅ Plan v2 §Verification 9 grep verify 全 PASS（註解 markers 不算 code reference / 真實 code reference 唯一 `GetKeyedService<IAgentExecutor>` 1 hit at AgentQueueProcessor.cs:190 — W3 fallback 紀律守 ✓）
- ✅ Bot startup verify：`Application started. Press Ctrl+C to shut down.` / `Hosting environment: Production` / `Scheduler QuartzScheduler_$_NON_CLUSTERED started` (HealthCheckJob 子項 2 W2 ✓) / `No migrations were applied. The database is already up to date.` / 0 fatal/error in startup log
- ✅ v5.5 path production active：PetraInbox + PetraDispatchWorker polling running（v5.5 dispatch 0 影響）
- ✅ CI/CD 自動部署：commit 09:52:38 → DLL build 09:54 → container recreate 09:57:54（~5 min 從 commit 到 production）

### 踩坑紀錄

**過程相對順利 — 0 踩坑**。對齊 Stage 78a 累積經驗紀律生效成熟：

- Plan Mode 一輪 spike W1-W6 grep verify 完整（沒有像 Stage 78a 連續 3 輪 spike 揭重大議題的累積）
- Christ 拍板 W1 邊界議題路線 C 後 Plan v2 升級 1 輪即進實作
- 實作期 0 follow-up bug / 0 self-diag fix
- 自驗 0 issue

對齊「修根因 > 補丁」紀律延伸成熟實踐：
- ctor unused dep cleanup 一併砍（避免 CS9113 warning 累積成 Stage 78c 範圍補丁）
- grep verify 紀律守（W1-W6 spike + 路線 C grep verify BuildAgentPlanEmbed caller 預期路徑 A 驗證）
- ResolveWorkflowType `\b` 精準匹配避免誤砍 ProposalConfirmationService.ResolveWorkflowTypeInternal

### 校準錨數據點

| 項目 | 數據 |
|---|---|
| **砍量** | -849 行刪 / +62 行新增 / **net -787 行** |
| **檔案數** | 8 files changed |
| **build warning** | 102 (baseline) → 59 (**-43 / -42%**) |
| **xUnit baseline** | Bot.Tests 104/104 ✓ + Generated 127/127 ✓ 全綠 |
| **commit** | `a7c763a`（單 commit / 對齊 §R5 + Stage 78a baseline） |
| **CI/CD deploy** | ~5 min（commit 09:52:38 → DLL 09:54 → container recreate 09:57:54） |
| **規模分類** | 架構級重構區間 ×0.58 第 8 資料點候選（Stage 66/67/78a 連續 4 次同倍率累積） |
| **raw 預估 vs 真實 ratio** | raw 80-130K / ratio ×1.5-2.5 預估 / 真實落點待 Aria 自省點 #37 結案對照 |

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v2.0 | 2026-05-19 | **實作完成 + Forge 自驗 PASS**。commit `a7c763a` 單 commit / -849/+62 = net -787 行 / 8 files changed / build warning 102 → 59 (-43 / -42%) / xUnit baseline 104+127 全綠 / CI/CD ~5 min deploy。**W1 邊界議題 Christ 拍板路線 C 折衷實作**：HandleConfirmYesAsync v4 body line 393-454 cascade 砍 (method 縮為 ~15 行 / 保 Stage 68 短路 + defensive log+ack fallback) + BuildEscalateButtons grep verify 0 caller cleanup 砍 + BuildAgentPlanEmbed 留 (ShowDirectAgentConfirmAsync 仍 caller / 預期路徑 A)。**W3 fallback 紀律守**：IAgentExecutor.cs 整檔 0 動 / XML doc 加 fallback 註解標記 Stage 78c 預備砍。**R6 ResolveWorkflowType `\b` 精準匹配紀律守**：v4 ButtonCallbackRouter 版砍 / v5.5 ProposalConfirmationService.ResolveWorkflowTypeInternal 不誤砍。**ctor unused dep cleanup**：CeoAgentService 8→3 dep / SlashCommandRouter 11→6 dep / WebhookController + ButtonCallbackRouter 各砍 rulesService。**Forge 自驗 grep verify 9 條件全 PASS + production deploy verify**（Bot startup 0 exception / Quartz Scheduler started / v5.5 PetraInbox polling active / Migration empty）。**0 踩坑 / 0 follow-up bug** — 對齊 Stage 78a 累積經驗紀律生效成熟（Plan 1 輪 spike + Christ 拍板 1 輪即進實作 + 自驗 0 issue）。 |
| v1.0 | 2026-05-19 | 規劃書建立 — v3.69.0 / M 規模 / v5.5 Phase 4 候選 C 後續（Stage 78a v4 path dead code 整套砍完 → 78b v4 path dead caller 整套砍 → 78c v4 Pipeline 整套砍）。**戰略脈絡**：Christ 2026-05-19 拍板「Discord slash command 完全沒在使用 + GitHub Issue webhook 完全沒用過」+ Trial_v6-v22 連續 17 次 v5.5 path 業務級驗證 0 v4 caller 累積 → 6 條 entry effective dead code 拍板砍。**6 子項**：① ButtonCallbackRouter v4 routing 砍（exec_yes / escalate_devplan_skip / escalate_devplan_abort / escalate_skip / escalate_abort + 對應 helper）② OpsAgentService IAgentExecutor 實作砍（保 HealthCheckJob production active）③ IAgentExecutor interface 砍（0 implementation + 0 caller 後 / W3 fallback 評估）④ `/task` slash command + HandleTaskCommandAsync 整段砍 ⑤ GitHub Issue webhook HandleIssueOpenedAsync 砍 ⑥ CeoAgentService.ProcessAsync 整段砍 + v4 helper（BuildSystemPrompt / BuildUserMessageAsync / BuildGitHubContextAsync / TryParseResponse）+ ctor 4 dep 砍（LlmProviderFactory / TaskRepository / GitHubService / GitHubSettings）⑦ Directory.Build.props v3.68.0 → v3.69.0。**設計決策核心**：純 refactor 性質 / 0 業務變化（entry 全 0 使用）/ 0 Migration / backwards-compatible 守護延續 v5.5 path 0 行為改變 / Stage 78c 範圍邊界明確（v4 Pipeline framework 整套留 78c）。**驗收 9 場景**：A xUnit baseline 0 regression / B ButtonCallbackRouter v4 routing 砍 v5.5 routing 0 行為改變 / C OpsAgentService IAgentExecutor 砍 HealthCheckJob production active / D IAgentExecutor interface 砍（W3 fallback 評估）/ E `/task` slash command 砍 / F GitHub Issue webhook 砍 / G CeoAgentService.ProcessAsync + v4 helper 砍 / H v5.5 path production 0 regression（Aria gate2）/ I Bot startup 0 exception。**Aria 預警 W1-W6**：ButtonCallbackRouter v4 vs v5.5 routing 邊界 grep verify / OpsAgentService HealthCheckJob 不受影響 / IAgentExecutor interface 砍前 AgentQueueProcessor:190 active 風險 + fallback / CeoAgentService ctor 4 dep grep verify / cascading helper 砍順序 / 子項依賴鏈兩條獨立。**校準錨預估**：對齊大規模架構級重構新區間 ×1.57-4.07 下界 / raw 80-130K × ratio ×1.5-2.5 = 真實 ~150-280K / Opus 1M + Extra high safety 15-28% / cost $3-5。**Phase 4 路徑**：78a ✅ → **78b**（本 / 純 refactor 6 條）→ 78c（v4 Pipeline 整套砍 / L）→ 79（HITL / M）→ 80（動態 replan / L）→ WebUI Stage → v5.5 完整收口。**下一步**：Forge 實作 + Aria gate1 Tier 0+1+Tier 2 #3 build + Aria gate2 production 0 regression 驗 → 通過後 Stage 78c 開（v4 Pipeline 整套砍）。 |
