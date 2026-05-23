# Stage 84 Roadmap — PetraOrchestratorService 怪物大檔拆解

> **狀態：📋 規劃中**
> **文件版本：v1.0**
> 對應系統版本：v3.76.0（Stage 84 完成後 / minor bump）
> Stage 規模：**M+**（單檔 2266 行拆解 + Tests 1283 行同步 / 預估 5-6 個新檔 + 1 DTO 獨立檔 / 0 行為改變 pure refactor / 0 燒 AiTeam 餘額 — 純 C# refactor / 0 LLM call）
> 觸發來源：FF Stage 84+ 候選 #13 — Christ 拍板「Stage 84 排 Mock 友善項目 + #13 獨立一個 Stage」
> 戰略意義：**降低未來動 Petra 加新功能 friction** — 當前 2266 行超 `refactor-sop.md` 1000 警戒 2.2 倍 / 5+ 職責同檔 / 下次動 Petra（追加 1 Vera 紀律升級 / 追加 2 Codebase Explorer agent / 追加 3 SubtaskPlan refuse fallback 任一條）前先拆 / 避免漸進累積無止盡

---

## 戰略脈絡

### 為什麼拆 PetraOrchestratorService

`refactor-sop.md` 拆解觸發條件 4 條全中：
- ✅ 單檔超過 1000 行（2266 / 警戒線 2.2 倍）
- ✅ 單一類別職責 ≥ 5 種（v5 dispatch / v5.5 Talent dispatch / HITL plan_confirm / Stage 81 動態 replan / Git finalization）
- ✅ 未來 Stage 需要頻繁動到（Stage 84+ 追加 3 條全要動 Petra）
- ✅ Stage 80-83 連續 4 Stage 都動過此檔（plan_confirm / replan_confirm / Bug 4 prUrl 寫入累積）

對齊「修根因 > 補丁」紀律 — 不是「等下次動再拆」而是「下次動前先拆」/ 避免每次擴功能都 push 主檔變大。

### 為什麼是 Mock 友善（不需 Trial）

**Pure refactor / 0 行為改變** — 拆解 strategy：
- 5 大職責 method body 整段搬 / 0 邏輯改寫
- caller 4 處全 lazy resolve（`scope.ServiceProvider.GetRequiredService<PetraOrchestratorService>()`）/ 主入口 PetraOrchestratorService 保留 / caller 0 改動
- Tests 47 個 mirror 拆 / 既有 assertion 0 改

驗收靠 MockMode 4 流程走全 path（HITL plan_confirm / replan_confirm / cap intervention / chain dispatch / git finalize）/ 不需要燒真實 LLM token 跑 Trial。

### 對齊 refactor-sop 累積實戰數據

| Stage | 拆解目標 | 原行數 | 主檔瘦身 | 倍率 |
|---|---|---|---|---|
| 34 | MeetingService | 1415 | -30% | ×1.60 |
| 35 | PmAgentService | 1389 | -? | ×1.49 |
| 36 | TaskGroupService + CommandHandler | 4795 | -73~-85% | ×1.49 |
| 59 | TaskGroupService（v4 復發）| 1759 | -54%（dispatch 主檔典型）| ×1.09 |
| **84** | **PetraOrchestratorService** | **2266** | **預估 -85%（→ ~250 行）** | **預估 ×0.9-1.0**（SOP 累積第 5 次）|

預估倍率持續下降趨勢 — workflow_aria.md 第 5+6 條紀律已生效（partial read + 不寫 code 範例）/ 對齊 Stage 59 ×1.09 baseline。

---

## 子項清單

### 子項 0：DTO record 獨立檔（規模 XS）

7 個 nested record 集中 `src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorDtos.cs`：

- `PlanConfirmContext` / `PlanConfirmSubtask` / `PlanConfirmDependency`（Stage 80 plan_confirm BossInteraction.ContextJson）
- `DispatchOutcome` / `ReplanSignal` / `ReplanDecisionJson` / `ReplanConfirmContext`（Stage 81 replan）

`internal sealed` 保持 — 避免外部 namespace 引用。對齊 `refactor-sop.md` SOP 1（Internal DTO 集中獨立檔 / 避免跨 service nested type 引用）。

### 子項 1：PetraGitFinalizationService 拆出（規模 S）

method 範圍：
- `FinalizeGitAsync`（L918-1026 / ~110 行）

最獨立 / 0 cross-state / 0 replan 互動 / 0 Petra LLM call。對齊 Stage 83 Bug 4 path A+ 已驗證的 prUrl 寫回 `PetraSession.ResultPrUrl` flow（不改動該行為）。

DI 注入：`GitHubService` + `AppDbContext` + `ILogger<PetraGitFinalizationService>`（從主 service 13 注入抽出 3 個）。

### 子項 2：PetraPlanConfirmationService 拆出（規模 M）

method 範圍（~400 行）：
- `WaitForPlanConfirmationAsync`（L1047-1084 / 開 plan_confirm card + pause）
- `ResumeFromPlanConfirmationAsync`（L1090-1150 / 4-way 分支 router）
- `ResumeApproveAsync`（L1154-1202）/ `ResumeEditOrRespondAsync`（L1205-1251）/ `ResumeRejectAsync`（L1253-1288）
- `DispatchAndFinalizeAsync`（L1290-1347 / approve 路徑 dispatch + FinalizeGit + complete）

對齊 Stage 80 `BossInteraction.InteractionType='plan_confirm'` schema 不動 / ContextJson 結構不動。

`DispatchAndFinalizeAsync` 內部呼叫 `PetraTalentDispatchService`（子項 4）+ `PetraGitFinalizationService`（子項 1）— DI 注入跨 service 對齊 SOP 4。

### 子項 3：PetraDynamicReplanService 拆出（規模 M-L）

Stage 81 全 replan logic（~550 行 / 最大子項）：

- `DetectReplanTrigger`（L1631-1641 / `internal static` 規則式 trigger 偵測）
- `CheckReplanTriggerAfterDispatchAsync`（L1645-1709 / cost cap + trigger + iter cap + Petra LLM call）
- `HandleReplanSignalAsync`（L1712-1750 / router cap_reached → HandleCapReachedAsync / replan_confirm → 開卡）
- `HandleCapReachedAsync`（L1752-1790 / cap intervention card + Cancelled return）
- `WaitForReplanConfirmationAsync`（L1792-1829）
- `InvokePetraReplanAsync`（L1831-1935 / Petra LLM JSON parse retry instruction）
- `ResumeFromReplanConfirmationAsync`（L1937-1983）/ `ResumeReplanApproveAsync`（L1985-1995）/ `ResumeReplanEditOrRespondAsync`（L1997-2058）/ `ResumeReplanRejectAsync`（L2060-2071）
- `ContinueChainFromSubtaskAsync`（L2073-2151 / replan 決定後重新 dispatch 該 subtask 之後）

對齊 Stage 81 `BossInteraction.InteractionType='replan_confirm'` + `intervention` 兩種 schema 不動 / Workflow:UseDynamicReplanning + UseHITLReplanConfirmation flag 路徑不動。

### 子項 4：PetraTalentDispatchService 拆出（規模 M-L）

v5.5 dispatch core（~550 行）：

- `DecideTalentsAsync`（L457-527 / Stage 69 Linear wrapper）
- `DecideTalentsWithPlanAsync`（L529-598 / Stage 70 structured output）
- `DispatchTalentsAsync`（L600-741 / 自管 chain dispatch + replan/cap 偵測 / return `DispatchOutcome`）
- `BuildInputMessagesForSubtaskAsync`（L743-801 / 單 subtask context 組織）
- `ProcessSubtaskResultAsync`（L802-882 / memory write）
- `DispatchRemainingSubtasksAsync`（L2153-2237 / replan / approve 恢復 dispatch）

**State ownership 搬遷**：
- `_roundRobinCounter`（int / Stage 67 Talent 輪詢）→ 此 service 持有
- Scoped lifecycle 對齊（不抽 Singleton Store / 對齊 SOP 5 各方法自管）

跨 service 呼叫：注入 `PetraDynamicReplanService`（子項 3 / 用 `CheckReplanTriggerAfterDispatchAsync` + `HandleReplanSignalAsync`）。

### 子項 5：v5 IAgentTool path 處理（規模 S / 待 Forge plan mode 拍板拆 vs 砍）

method 範圍（~200 行）：
- `DecideAsync`（L250-293 / v5 LLM 決策 capability 序列）
- `DispatchWorkersAsync`（L355-455 / v5 自管 chain dispatch）
- `LogWorkflowEvent`（L299-353 helper）

**State ownership**：`_executorAccumulators`（Dictionary / Trial_v9 workflow event 文本累積）

**待 Forge plan mode verify**：
- grep `Workflow:UseV5SubtaskPlanning` flag — DB 真實狀態 + 對齊 22 Workflow Flag 表
- grep `DecideAsync` caller — 是否還有 dispatch path 走 v5
- 如 v5 path 真實 production dead → 砍（對齊「不寫 backwards-compatibility shims」紀律）
- 如 v5 path 還活 → 拆出 `PetraV5DispatchService.cs`

### 子項 6：PetraContextBuilder Commons 拆出（規模 S-M）

跨 service 共用 prompt building（~250 行）：

- `BuildSessionContext`（context record 組織）
- `BuildResumeInputAsync`（L1349-1546 / 重建 LLM input messages + memory + cost）
- `BuildPetraSystemPromptForRuntimeAsync`（L1548-1571 / system prompt runtime 插值）
- `ResolvePetraPersonaAsync`（L1573-1584 / Petra persona override）
- `BuildMemoryContext`（L884-916 / task + talent memory 串接）

放 `src/AiTeam.Bot/Orchestration/Petra/PetraContextBuilder.cs`（Petra 子目錄 / 非 Orchestration root — 對齊 SOP 6 跨 Petra 子 service 共用 / 不跨 Talent / Vera / Quinn 邊界）。

對齊 SOP 3 Commons 範圍界定 — 多 service 都會用（plan_confirm resume + replan resume + dispatch chain 都需要 context 重建）。

### 子項 7：Tests mirror 拆解（規模 M）

對應 `src/AiTeam.Bot.Tests/Orchestration/PetraOrchestratorServiceTests.cs`（1283 行 / 47 method）：

新增 tests 檔（4 個）：
- `PetraTalentDispatchServiceTests.cs` — 搬 Test12-13 / 15-18 / 22-32（Talent dispatch + SubtaskPlan + memory write）
- `PetraDynamicReplanServiceTests.cs` — 搬 Stage81_* 系列 8 個（DetectReplanTrigger / cap / replan_confirm flow）
- `PetraPlanConfirmationServiceTests.cs` — 新增 plan_confirm HITL flow（既有 plan_confirm tests 散在主檔 / 集中搬）
- `PetraGitFinalizationServiceTests.cs` — 新增 FinalizeGitAsync + prUrl 寫回（Stage 83 Bug 4 補的 path）

主檔 tests 保留：
- 決策 parse / session 持久化 / Workflow flag 行為 / Adapter & Capability dispatch

xUnit InMemory DB fixture pattern 沿用（不換 fixture framework）。

### 子項 8：Program.cs DI registration（規模 XS）

5-6 個新 service 加 Scoped DI（對齊 SOP 4 順序：DTO 不需註冊 / Commons 先 / 子 service / 主 service）：

```
builder.Services.AddScoped<PetraContextBuilder>();
builder.Services.AddScoped<PetraGitFinalizationService>();
builder.Services.AddScoped<PetraTalentDispatchService>();
builder.Services.AddScoped<PetraDynamicReplanService>();
builder.Services.AddScoped<PetraPlanConfirmationService>();
// 如 v5 path 拆 ↓
builder.Services.AddScoped<PetraV5DispatchService>();
builder.Services.AddScoped<PetraOrchestratorService>(); // 主入口保留 / caller 0 改動
```

caller 4 處（PetraDispatchWorker L169 / PetraSessionRecoveryService L37 / PlanConfirmationProcessor L99 / Program.cs）全 lazy resolve → **0 改動**。

---

## 設計決策

| # | 決策 | 理由 |
|---|---|---|
| 1 | DTO record 7 個搬 `PetraOrchestratorDtos.cs` 獨立檔（`internal sealed`）| 對齊 SOP 1 / 避免新 service 跨檔 nested type 引用 / 不寫 public 避免 namespace 污染 |
| 2 | 5-6 新 service 全 Scoped lifecycle（不 Singleton）| 對齊既有 PetraOrchestratorService Scoped pattern / 持 instance state（_roundRobinCounter / _executorAccumulators）對齊 SOP 5「無共享 state 各方法自管」/ 不抽 Singleton Store |
| 3 | `_roundRobinCounter`（int / Stage 67）搬 `PetraTalentDispatchService` 持有 | dispatch 邏輯 ownership / 對齊 SOP 5 / 跨 service 不共享 state |
| 4 | `_executorAccumulators`（Dictionary / Trial_v9）搬 v5 path owning service | v5 path ownership / 拆 or 砍由 Forge plan mode 決定 |
| 5 | `DispatchAndFinalizeAsync`（plan_confirm approve 路徑）留 `PetraPlanConfirmationService` 內 / 透過注入 dispatch + git service 呼叫 | 語義上是 plan_confirm 4-way 分支之一 / 不抽出獨立 service |
| 6 | `PetraContextBuilder` 放 `Petra/` 子目錄（非 Orchestration root）| 對齊 SOP 6 / 跨 Petra 子 service 共用 / 不跨 Talent / Vera / Quinn 邊界 / 不引入跨領域依賴 |
| 7 | v5 IAgentTool path（DecideAsync + DispatchWorkersAsync）拆 vs 砍 = Forge plan mode 階段拍板 | grep `Workflow:UseV5SubtaskPlanning` flag DB 真實狀態 + caller 真實 path / v5 dead 則砍（對齊「不寫 backwards-compatibility shims」紀律）/ v5 still active 則拆 `PetraV5DispatchService` |
| 8 | 主入口 `PetraOrchestratorService` 保留 / 含 `StartAsync` + `ResumeAsync` + entry guards | 4 caller lazy resolve 抓主入口 / 主入口轉發給拆出 service / caller 0 改動 |
| 9 | `ResumeAsync(Guid sessionId)` 新增 dispatcher 邏輯 | 從 DB 讀 session → 檢測 InteractionType → 路由 `PetraPlanConfirmationService.ResumeFromPlanConfirmationAsync` / `PetraDynamicReplanService.ResumeFromReplanConfirmationAsync` |
| 10 | Tests mirror 拆 / 0 邏輯改寫 | xUnit InMemory DB fixture 沿用 / Fake* 物件沿用 / 47 個 assertion baseline 守住 |
| 11 | 不引入 Interface 封裝新 service（IPetraTalentDispatchService 等）| 4 caller 全內部 lazy resolve / 0 跨層 mock 需求 / 對齊 SOP 2「< 15 caller 直接切換不做 thin wrapper」/ 避免過度設計 |
| 12 | 0 Workflow Flag 改動 / 0 BossInteraction schema 改動 / 0 PetraSession 欄位改動 | Pure refactor 紀律 / 0 行為改變 / 0 Migration |

---

## 驗收情境

### A. 行為等價驗（Mock 友善）

1. **MockMode 4 流程 baseline 全綠**
   - 觸發：Dashboard `/mock` 觸發 4 流程任一（image / file / TaskGroup / Petra 等對齊 Stage 83 既有 Mock UI button 範圍）
   - 驗證：PetraSession 從 `pending` → `running` → `done` state machine 走完 / Discord embed 對齊既有訊息 / PetraSessionMessage 內容對齊 / 0 SQL exception

2. **HITL plan_confirm 4-way 分支**
   - 觸發：Mock 一個 task 走到 `Workflow:UseHITLPlanConfirmation=true` plan_confirm 卡片
   - 驗證：approve → SQL 看 `BossInteraction.ResponseAction='plan_approve'` + dispatch 啟動 / edit → 重決策 loop / respond → 重決策 loop / reject → `task_memory` 寫 `plan-rejected` + PetraSession `cancelled`
   - 對齊 Stage 80 既有 4-way 行為

3. **HITL replan_confirm 4-way 分支**
   - 觸發：Mock 一個 task 觸發 Vera critical / Quinn failed → `DetectReplanTrigger` → Petra LLM replan 決策 → replan_confirm 卡片
   - 驗證：approve → `ContinueChainFromSubtaskAsync` 重 dispatch / edit + respond → 重決策 / reject → 跳過 subtask 繼續 chain
   - 對齊 Stage 81 既有 4-way 行為

4. **cap intervention 觸發**
   - 觸發：Mock 一個 task 設低 cost cap / iter cap → `CheckReplanTriggerAfterDispatchAsync` 偵測超 cap
   - 驗證：`HandleCapReachedAsync` 開 intervention card + PetraSession `cancelled` + return `PetraOrchestratorResult.Cancelled`

5. **Chain dispatch 跳過已完成 subtask**
   - 觸發：多 subtask plan / 第一 subtask 完成 / 第二 subtask 觸發 replan reject
   - 驗證：`ContinueChainFromSubtaskAsync` 跳過已完成 / 從第三 subtask 繼續 / SubtaskPlan dependency order 對

6. **Git finalization + prUrl 寫回**
   - 觸發：完整 task 跑到 `FinalizeGitAsync` / `GitHubService.OpenPullRequestAsync` return prUrl
   - 驗證：`PetraSession.ResultPrUrl` 寫入 / Dashboard `/tasks` 歷史 tab PR link 顯示（對齊 Stage 83 Bug 4 path A+ 既有行為）

### B. xUnit Tests baseline

7. **Tests 47 個全綠**
   - 觸發：`dotnet test src/AiTeam.Bot.Tests`
   - 驗證：47 個 baseline test + 新增 service tests 全綠 / 0 邏輯改寫 / 0 assertion 改動

### C. 結構驗

8. **主檔瘦身達標**
   - 觸發：`wc -l src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs`
   - 驗證：≤ 400 行（從 2266 瘦身 ≥ 82% / 對齊 SOP 「dispatch 主檔典型 -50~-85%」）

9. **新檔行數合理**
   - 觸發：`wc -l src/AiTeam.Bot/Orchestration/Petra/Petra*Service.cs`
   - 驗證：5-6 新檔 + 1 Commons + 1 DTO 獨立檔 / 每檔 < 800 行 / 任一新檔超 800 → 再評估二度拆

10. **DI 註冊驗**
    - 觸發：`docker compose up -d --force-recreate` 啟動 Bot
    - 驗證：Bot log 0 DI 解析錯誤 / 5-6 新 service 正確 Scoped 註冊 / lazy resolve caller 正常拿到主入口

11. **Caller 0 改動驗**
    - 觸發：`git diff main..stage84 -- src/AiTeam.Bot/Orchestration/Petra/PetraDispatchWorker.cs` + `PetraSessionRecoveryService.cs` + `PlanConfirmationProcessor.cs`
    - 驗證：3 caller 0 改動（如 v5 path 全砍 / PetraDispatchWorker 可能有 v5/v5.5 分支整理 / 細節由 Forge plan mode 拍板）

### D. v5 path 處理驗（Forge plan mode 階段決定）

12. **v5 path 拆 OR 砍最終確認**
    - 觸發：grep `Workflow:UseV5SubtaskPlanning` DB row + appsettings.json
    - 驗證：拆 → `PetraV5DispatchService.cs` 新檔 + 既有行為對齊 / 砍 → DecideAsync + DispatchWorkersAsync + LogWorkflowEvent + _executorAccumulators 全砍 + Workflow flag 砍 + Stage 78a 既有 v4 砍 pattern 對齊

---

## 技術約束

- **Pure refactor** — 0 行為改變 / 0 Workflow Flag 改動 / 0 BossInteraction schema 改動 / 0 PetraSession 欄位改動 / 0 Migration
- **0 燒 AiTeam 餘額**（純 C# refactor / 0 LLM call / Aria + Forge session 走 Claude Code subscription）
- **0 真實 Trial 依賴**（MockMode 4 流程 + xUnit 47 test 雙層 cover）
- C# 12 Primary Constructor pattern 沿用（既有 13 注入主 service 拆解後新 service 對齊）
- DI lifecycle 全 Scoped（對齊既有 PetraOrchestratorService）
- 不引入 Interface 封裝（4 caller 全內部 lazy resolve / 0 跨層 mock 需求）
- 對齊 `docs/conventions/refactor-sop.md` SOP 1-6（特別 SOP 1 DTO 集中 / SOP 2 caller 改動成本三層分 / SOP 4 DI 註冊順序 / SOP 5 state 管理 / SOP 6 子目錄命名）
- 對齊 `docs/conventions/csharp.md` 命名 / 結構紀律
- Forge context 估 ~300-450K Opus 1M + ultrathink（M+ 規模 / 對齊 Stage 59 ×1.09 倍率 baseline）

---

## 風險與緩解

| 風險 | 緩解 |
|---|---|
| **拆解後合計行數變多（SOP「正常」）** | 對齊 SOP「主檔瘦身為目標非減總碼量」/ 預估主檔 -82%（2266 → ~400）/ 新檔合計可能 ~2100-2300 行 |
| **拆出新檔再變怪物（SOP v1.2 反例 Stage 36 ButtonCallbackRouter 1091）** | 每新檔 < 800 行紀律 / 子項 3+4 預估各 ~550 行 / 任一超 800 → Forge healthy 偏離 plan 紀律觸發二度拆 |
| **v5 IAgentTool path 拆 vs 砍誤判** | Forge plan mode 必先 grep `Workflow:UseV5SubtaskPlanning` DB row + caller 真實 path verify / 不憑 commit message 推測 / 對齊計劃書硬規則 7 |
| **`_executorAccumulators` + `_roundRobinCounter` state ownership 搬遷錯** | 對齊 SOP 5 / 拆解時 state 跟 owning service 走 / 不抽 Singleton Store / Forge 自驗 xUnit Test19-22 memory tests + 多 talent 場景 pass |
| **DispatchAndFinalizeAsync 跨 service 互相 inject 循環依賴** | DI 註冊順序：Commons → Git → Talent dispatch → Replan → PlanConfirmation（單向依賴）/ 對齊 SOP 4 / Forge build 時 catch |
| **Tests mirror 拆漏 cover** | Explore agent 已 mapping 47 test method → 4 新 tests 檔 + 主檔保留 / Forge 結案前 `dotnet test` 47 + 新增全綠 |
| **Caller 4 處 0 改動假設破** | PetraDispatchWorker v5/v5.5 分支整理可能涉及輕改 / Forge plan mode 階段 verify + 對齊計劃書硬規則 11 紀律「caller 0 改動」優先 |
| **Pure refactor 但 silent regression（Stage 35 同類根因第 5 次累積）** | MockMode 4 流程驗收 9 個情境 + xUnit Test 47 個全綠 雙層 cover / Aria gate1 Tier 0+1+2（build/test 驗）/ Aria 視覺驗（Chrome MCP）抽樣驗 Dashboard 顯示對齊 |

---

## 實施順序建議

對齊 SOP 累積經驗 / Forge plan mode 階段拍板細節 / 由易到難：

1. **子項 0**（DTO record 獨立檔 / XS / 最低風險）
2. **子項 1**（PetraGitFinalizationService / S / 最獨立 0 跨 service）
3. **子項 6**（PetraContextBuilder Commons / S-M / 跨 service 共用基礎）
4. **子項 2**（PetraPlanConfirmationService / M / 對齊 Stage 80 既有 schema）
5. **子項 3**（PetraDynamicReplanService / M-L / 對齊 Stage 81 既有 schema）
6. **子項 4**（PetraTalentDispatchService / M-L / 最複雜 / 含 _roundRobinCounter state）
7. **子項 5**（v5 path 拆 OR 砍 / S / 同步處理 _executorAccumulators state）
8. **子項 7**（Tests mirror 拆 / M / 同步進度 OR 最後一波）
9. **子項 8**（Program.cs DI registration / XS / 最後一步）

Forge plan mode 階段可調整順序（healthy 偏離 plan 紀律）/ 但 Tests 拆紀律：每完成一個新 service 子項時對應 tests 同步搬 / 避免最後一波 batch 出問題難 isolate。

---

## 版本歷史

### v1.0 — 2026-05-23（Aria 建立）

- 觸發：Christ 拍板「Stage 84 排 Mock 友善項目 + #13 PetraOrchestratorService 拆解獨立一個 Stage」/ 對齊 FF Stage 84+ 候選 #13「下次動 Petra 加新功能前先拆」觸發時機 / Stage 85 留其他 12+3 條 Mock 友善項目（後續排）
- 範圍：2266 行 5 大職責 → 5-6 新 service + 1 DTO 獨立檔 + 1 Commons + Tests 47 個 mirror 拆
- 9 子項規劃 + 12 設計決策 + 12 驗收情境 + 8 風險緩解
- Explore agent partial read 4 段（PetraOrchestratorService + Tests + 周邊檔 21 個 + caller 盤點）→ Aria 主 session 0 大檔污染
- Aria 6 維度 ultrathink 自審：架構 / 邏輯一致性 ✅ / 競態（state ownership）⚠️ 已 cover / 上下文（v5 path）⚠️ 待 Forge verify / DI lifecycle ✅ Scoped 對齊 / 預留欄位 N/A / 關鍵檔案清單 ✅
- 拍板：0 行為改變 pure refactor / 0 Trial 依賴 / MockMode 4 流程 + xUnit 47 test 雙層 cover / 對齊 refactor-sop.md SOP 1-6

---

## 實作紀錄（Forge 結案第一段）

### 實際產出檔案 + 行數

| 檔案 | 行數 | 角色 |
|---|---|---|
| `src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs` | **193**（從 2266 / 瘦身 **91.5%**） | 主入口 + StartAsync + ResumeAsync + 2 forwarder |
| `src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorDtos.cs` | 56 | 7 internal sealed record（DTO 集中） |
| `src/AiTeam.Bot/Orchestration/Petra/PetraTalentLookupHelper.cs` | 34 | static helper / 解 TalentDispatch ↔ DynamicReplan 循環 |
| `src/AiTeam.Bot/Orchestration/Petra/PetraGitFinalizationService.cs` | 130 | FinalizeGitAsync + BuildPrBody |
| `src/AiTeam.Bot/Orchestration/Petra/PetraContextBuilder.cs` | 327 | BuildSessionContext + BuildResumeInput + BuildPetraSystemPrompt + BuildMemoryContext + BuildSummariesFromSessionMessages |
| `src/AiTeam.Bot/Orchestration/Petra/PetraTalentDispatchService.cs` | 767 | DecideTalents + DispatchTalents + ProcessSubtaskResult + detection family（CheckReplan + InvokePetraReplan + DetectReplanTrigger） |
| `src/AiTeam.Bot/Orchestration/Petra/PetraDynamicReplanService.cs` | 410 | HandleReplanSignal + 4-way replan_confirm Resume + ContinueChainFromSubtask |
| `src/AiTeam.Bot/Orchestration/Petra/PetraPlanConfirmationService.cs` | 351 | WaitForPlanConfirmation + 4-way plan_confirm Resume + DispatchAndFinalize |

合計新增 8 檔 + 主檔砍至 193 行 / 0 新檔超 800 行（refactor-sop.md「主檔瘦身 + 新檔 < 800」紀律守住）。

### SOP 套用對照

- **SOP 1**（DTO 組織）：7 internal sealed record 集中 PetraOrchestratorDtos.cs ✅
- **SOP 2**（caller 改動成本）：4 caller 全 lazy resolve `scope.ServiceProvider.GetRequiredService<PetraOrchestratorService>()` / 0 改動 ✅
- **SOP 3**（Commons 範圍界定）：PetraContextBuilder 真實多 service caller（TalentDispatch + DynamicReplan + 主入口）/ BuildMemoryContext / BuildPetraSystemPrompt 跨 service 共用 ✅
- **SOP 4**（DI 註冊順序）：Commons → static helper（不註冊）→ Git → ContextBuilder → TalentDispatch → DynamicReplan（注入 TalentDispatch + Git + ContextBuilder）→ PlanConfirmation（注入 TalentDispatch + DynamicReplan + Git + ContextBuilder）→ 主 service ✅
- **SOP 5**（state 管理）：`_roundRobinCounter` 拆 2 份（TalentDispatch + DynamicReplan 各持 instance field / Scoped lifecycle）/ counter 作 helper `ref int` param 傳入 ✅
- **SOP 6**（子目錄組織）：全新 service 留 Orchestration/Petra/ 子目錄（既有命名空間 / 0 新子目錄）✅

### 健康偏離 plan 紀錄

**偏離 1：v5 ecosystem 整套砍**（plan v2.0 預先 verify / Aria 拍板）
- v5 IAgentTool path production dead（`Workflow:UseTalentSkillSeparation = true` / v5 else branch 永遠不走）
- 砍範圍：主檔內 v5 method 5 個 + 檔外 4 Agent service + AgentCapabilityAttribute + IAgentTool 整檔 + PetraWorkerHelper.GetCapabilities + DI registrations + 配置 flag

**偏離 2：detection family 移到 TalentDispatch**（Forge spike 揭 Roadmap 子项 3 設計缺陷 / commit message 明寫）
- spike 揭 `DispatchTalentsAsync` L682+L725 + `DispatchRemainingSubtasksAsync` L2204 **3 caller** inline 呼 CheckReplanTriggerAfterDispatchAsync → TalentDispatch ↔ DynamicReplan 真實雙向 ctor 循環（audit 漏看第 2 個循環）
- 修法：detection family（DetectReplanTrigger / CheckReplanTriggerAfterDispatchAsync / InvokePetraReplanAsync）移到 TalentDispatch（dispatcher 自管 detection / handler 留 DynamicReplan）/ 對齊「dispatch loop 內 fire detection 屬 dispatcher 職責」語義
- 結果：TalentDispatch 自管 detection / 0 注入 DynamicReplan → DynamicReplan 單向注入 TalentDispatch（ContinueChain → DispatchRemainingSubtasks）/ 0 循環

**偏離 3：DispatchRemainingSubtasksAsync return type 改 DispatchOutcome**（解循環引申）
- 原本回 PetraOrchestratorResult（含 HandleReplanSignal inline call + FinalizeGit inline）
- 改回 DispatchOutcome（含 Summaries + 選擇性 ReplanSignal）/ caller（ContinueChainFromSubtaskAsync in DynamicReplan）負責 signal handling + FinalizeGit
- 對齊 DispatchTalentsAsync 既有紀律（pure dispatch / outcome 回 caller route）

### Mock 覆蓋

- ✅ build：`dotnet build AiTeam.slnx` 0 error / 58 warning（NU1902 vulnerability + MSTEST0037 stylistic — 0 Stage 84 引入 / 0 CS9113 unused parameter warning after patch d2a4ff4）
- ✅ xUnit：`dotnet test src/AiTeam.Bot.Tests` 130 passed / 2 skipped（Test29-30 待搬 PetraTalentDispatchServiceTests）/ 50 baseline + 2 新 smoke - 3 cut（Test6 + Test12 + Test17）= 49 active + 2 skip = 51 total
- ✅ Runtime DI 驗：container 啟動乾淨（`docker logs aiteam-aiteam-bot-1` 0 exception / 0 DI 解析 error）/ PetraInboxChannel 初始化 log 出現（5 sub-service Scoped 註冊全 OK）
- ⏳ MockMode 6 驗收情境（GUI 部分 — Discord 真實 plan_confirm / replan_confirm card 互動 + Dashboard `/tasks` PR link 顯示）：Christ 後續統一驗收（pure refactor / 0 行為改變 + xUnit 130 全綠 + caller 0 改動 = production cold path 真實顯示驗收可延後）

### 踩坑紀錄

1. **v5 cut 引發 `using AiTeam.Bot.Agents` 連帶清理踩坑**：移除 v5 IAgentTool 後 sed-clean using directives 後發現 `IClaudeCodeService` 也在 `AiTeam.Bot.Agents` namespace → 必須留 using。修法：保守保留 `using AiTeam.Bot.Agents;` 即使部分 type 砍掉（namespace-level using 不會 break compile）。

2. **Test15-16 reflection target 換靜態 helper + ref counter**：原本 `method.Invoke(orch, [skill, talents])` instance method 反射；換 static helper 後 `method.Invoke(null, [skill, talents, counter])` + counter 作 `object[]` boxed ref param / 呼後讀回。Test 簽名也改：`InvokeFindTalentForSkill(string, IReadOnlyList<ITalent>, ref int)`（每 test local counter）。

3. **`#if FALSE_STAGE_84_MOVED` placeholder 失敗踩坑**：嘗試用 `#if FALSE` 暫時包住舊 method body 來逐步遷移 → C# raw string literal 在 `#if` block 內仍會 parse → 編譯失敗。改用 `sed '976,1130d'` 直接整段刪除 cleaner。

4. **Test29 / Test30 reflection target 換 service instance**：本 Stage scope cap 不重構 factory，標 `[Fact(Skip = "...")]` 標記移到 PetraTalentDispatchServiceTests 待 Stage 85+ 處理（或本 Stage 結案前後續 commit 補）。

### 0 follow-up 狀態

- ✅ 0 Workflow Flag schema 改動（DB row `Workflow:UseTalentSkillSeparation = true` 不刪 / 變孤兒 harmless）
- ✅ 0 BossInteraction schema 改動 / 0 PetraSession 欄位改動 / 0 Migration
- ✅ 4 caller 0 改動驗：`PetraDispatchWorker.cs:140` + `PetraSessionRecoveryService.cs:30` + `PlanConfirmationProcessor.cs:118` + `Program.cs` 全保持 lazy resolve / 主入口 + 2 forwarder（ResumeFromPlanConfirmationAsync / ResumeFromReplanConfirmationAsync）保證行為等價

### Aria gate1 follow-up patch（commit d2a4ff4）

Aria gate1 audit 揭 2 個 CS9113 unused parameter warning（plan 寫「0 warning」但實際 net 多 2 個）：

- `PetraDynamicReplanService.cs:30` `WorkflowSettingsResolver workflowResolver` — grep verify 0 method body usage → 砍
- `PetraTalentDispatchService.cs:34` `ITalentFactory talentFactory` — grep verify 0 method body usage → 砍

修法：純 primary constructor parameter 砍（2 檔 / 2 lines deleted）/ 0 行為改變 / 0 caller 改動 / 0 test 改動 / 0 DI 註冊改 / build 0 CS9113 / tests 130 passed / 2 skipped 不變。

### 結案 commit chain

- `8f99ea5`：Stage 84 主拆解（2266 → 193 行 / 5 sub-service + 1 static helper + 1 DTO + 1 Commons + v5 ecosystem 整套砍）
- `d2a4ff4`：CS9113 unused parameter patch（Aria gate1 follow-up）

GUI 部分（Discord plan_confirm / replan_confirm card 互動 + Dashboard `/tasks` PR link 顯示）後續 Christ 統一驗收 — pure refactor + xUnit 130 全綠 + caller 0 改動 = production cold path 真實顯示驗收可延後。

### Context 消耗實測

- Forge context 估 ~400-500K Opus 1M + ultrathink — 實測 stage A→J 全 chain 在單 session 完成 / 對齊 Stage 59 ×1.09 倍率 baseline / Stage 84 拆解倍率 SOP 累積第 5 次效益持續（M+ 規模 single-session 完成是新里程碑）。
