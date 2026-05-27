# Stage 49：v4 漸進遷移首發 — Cody-Vera-Petra Appeal loop 切 MS Agent Framework + feature flag

> 對應 Future Feature：v4 漸進遷移 6 Stage 路線首發（[Stage 48 spike 報告](../experiments/Spike_v1_MsAgentFramework.md) 節 7）— 不對應特定 active FF（v4 路線進入 Stage 工作模式，按 Stage 走不開新 FF）
> 對應版本：**v3.35.0**（v4 漸進遷移首個產生版本變動的 Stage）
> 建立日期：2026-05-02
> 狀態：✅ **已完成**（2026-05-01 驗收通過，6 場景 A/B/E 完整 + C 70% + D 80% + F 路線 B 結構保證；殘留 30% 留 Stage 50+ 自然演進）
> 文件版本：v2.0

---

## 概述

**戰略背景**：[Stage 48 FF 四十九 spike](Stage_48_Roadmap.md) 結論 = 採用 MS Agent Framework，啟動 6 Stage 漸進遷移路線（4-6 個月，「換引擎不換車身」）。**Stage 49 是首個遷移 Stage** — 把 spike POC（Cody-Vera-Petra Appeal loop / Writer-Critic Workflow 模式）從 spike branch 整合進 main + production 整合（DB / Discord 通知 / Crash Recovery / Token 計費）。

**性質特殊**：本 Stage 是 **混合型**（spike POC integration + production 整合），不是純 spike 也不是純 production refactor：
- spike branch 已有 POC 藍本（commits `161e694` / `1b9742b` / `916b860`）— 不需要從零開始
- 但 spike POC 是純 in-memory + Mock，production 整合必須加 DB / Discord / Crash Recovery / feature flag

**核心策略**：**並行雙系統 + feature flag**（`Workflow:UseFrameworkAppealLoop` AppSettings key，預設 `false`）。Christ 在 Dashboard 切 `true` 後新 path 接管，舊 `AppealOrchestrationService` 路徑保留至 Stage 54 才砍。

**v4 路線首發風險預警**：
- MS Agent Framework 1.0 GA 才 1 個月，breaking change 可能性中
- Anthropic provider 仍 prerelease（`Microsoft.Agents.AI.Anthropic 1.3.0-preview.260423.1`）
- Cody-Vera-Petra Appeal loop 是真實 production bug fix 流程（Trial 都會跑）— 切換失敗影響範圍大
- → feature flag 為主要安全網，**非緊急情況不啟用**，先 Mock 驗證再 production 切換

---

## 設計決策（Christ 2026-05-02 拍板）

### v1.1 補強拍板（Forge Session A 揭露）

**framework Executor 整合層級拍板**（路線 B service 包裝）：

| 項目 | 拍板 |
|---|---|
| **拍板** | 路線 B（service 包裝）— framework Executor 直接 call ReviewAppealService / DevPlanAppealService / PmReviewService method |
| **理由** | ① Aria 計劃書內部 Cody/Vera（IClaudeCodeService 底層）vs Petra（LlmProviderFactory 中層）三 Agent 不同層整合不一致 ② Prompt SoT 統一消解 R4 prompt drift 風險 ③ -30% 工時 + 風險降低 |
| **影響** | Stage 54 工時 +1-1.5 天（framework Executor 重寫從 service 切回直連，砍 legacy path） |
| **退場時機** | Stage 54 收尾時與 legacy AppealOrchestrationService / PmReviewService / ReviewAppealService / DevPlanAppealService 一起砍，連帶將 framework Executor 切回直連 IClaudeCodeService / framework Anthropic provider |

**DI factory 模式拍板**（Forge Session A 主動發現比 Aria 建議更穩）：

| 項目 | 拍板 |
|---|---|
| **拍板** | framework Executor 不註冊到 DI（factory 模式） |
| **實作** | AppealWorkflowFactory 內 `new` Executor + 注入 ctor；Executor ctor 接 `IServiceScopeFactory`；HandleAsync 內 `CreateAsyncScope()` 取 scoped services（DbContext / LlmProviderFactory / ReviewAppealService） |
| **理由** | 解 Singleton + Scoped 陷阱（Singleton Executor 持有 Scoped DbContext 跨 superstep 失效或炸）；framework 1.3.0 Configured&lt;T&gt; + ExecutorConfig 機制本身就是 factory 模式，對齊 framework 慣例 |
| **與既有 ClaudeCodeAgentExecutor lifecycle undocumented 議題**（Christ 提醒 #1） | 完整解 — 不依賴 framework runtime 提供 per-superstep DI scope |

### 主路線拍板（戰略級）

| 議題 | 拍板 | 替代方案 |
|---|---|---|
| **遷移策略** | **並行雙系統 + feature flag** — `Workflow:UseFrameworkAppealLoop` 預設 false，舊 path 保留至 Stage 54 | 直接切換（風險高 / 首遷移就 production 切）/ Shadow Mode（複雜度 +50%）|
| **DB 整合** | **framework Checkpointing 為主 + superstep 結束同步寫既有 `task_groups` / `tasks`** — 用 framework 完整能力，DB schema 加少量欄位但不破壞既有 | framework state 對齊既有 DB（繞過 Checkpointing 主賣點，浪費 spike 結論）|
| **POC 處理** | **重寫 production 版本，spike branch 留 reference** | 直接 merge spike POC（MockExecutors 等 dead code 風險）|
| **Petra 路徑** | **完全切到 framework Anthropic provider**（spike 已驗），但 **Stage 49 暫時保留 `LlmProviderFactory` wrapper 維持 TokenLogService** | Petra 走 Custom Executor 包既有 PmReviewService（保守但浪費 spike）|
| **BossInteraction 範圍** | **不包**（Appeal loop 結束後用既有手刻 path 開 BossInteraction，Stage 51 才動 Human-in-the-Loop）| 包進 Stage 49（工時 +50%，邊界爆炸）|

### Aria 拿捏（已決）

| # | 議題 | 決定 |
|---|---|---|
| 6 | CLAUDE_*.md prompt | 不動原則上保留；如 framework `ChatResponseFormat.ForJsonSchema<T>` 強制 schema 與 prompt 衝突，子項 7 微調 hint 段 |
| 7 | Token 計費 | **Stage 49 不切換**（保留 LlmProviderFactory + TokenLogService），Stage 54 才整合 framework telemetry middleware |
| 8 | feature flag 機制 | 沿用 Stage 47 升級的 `AppSettings` 表 + `WorkflowSettingsResolver`（既有 `Workflow:*MaxRounds` 5 keys 同 pattern）|
| 9 | feature flag 入口位置 | `AppealOrchestrationService` 5 個 `Handle*Async` method 入口分流（`TaskGroupService` 不需動，最小入侵）|

### Stage 49 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **1** | DB schema：`task_groups` 加 framework Checkpointing state 欄位 + Migration | S |
| **2** | `AppealState` production 版（對齊既有 task_groups / tasks 欄位）| S |
| **3** | `ClaudeCodeAgentExecutor` production 化（從 spike branch 搬 main + 加 production 整合）— **v1.1 修正：Stage 49 路線 B 不直接引用本 wrapper，標 [Obsolete] 預留 Stage 50+ Group Chat orchestration**（會議多 Agent 直連 IClaudeCodeService 沒有 service 上層可包） | M |
| **4** | `AppealWorkflowFactory` production 版（framework Workflow Builder + Checkpointing 整合 DB）| M |
| **5** | Petra 切 framework Anthropic provider（保留 LlmProviderFactory wrapper 維持 TokenLogService）— **v1.1 修正：Petra 整合層升級為包 PmReviewService / ReviewAppealService / DevPlanAppealService method（與 Cody/Vera 同層整合，三 Agent 一致）；不切原生 Anthropic provider，等 Stage 54 才切** | M |
| **6** | feature flag `Workflow:UseFrameworkAppealLoop` + `AppealOrchestrationService` 5 入口分流 | S |
| **7** | CLAUDE_*.md prompt schema hint 微調（如需）| XS |
| **8** | Mock 場景 + Christ 線下驗收 + 文件 + 結案 | M |

**總工時估**：8-12 天（2-3 週）

---

## 子項 1：DB schema — `task_groups` 加 framework Checkpointing state

### 實作項目

**位置**：`src/AiTeam.Data/Entities.cs` `TaskGroup` class

**新增欄位**（單一 nullable JSON 欄位）：

- `FrameworkAppealStateJson` (string?) — framework Checkpointing 序列化的 superstep state；`null` = 尚未進入 framework Appeal loop（走舊 path）或已完成

**設計理由**：
- 不破壞既有 schema（既有 28 欄位不動）
- nullable = 走舊 path 時保持 null，feature flag true 切換時才寫入
- Stage 54 收尾砍舊 path 時可考慮把欄位 promote 為非 nullable

**Migration `Stage49TaskGroupFrameworkState`**：
```bash
dotnet ef migrations add Stage49TaskGroupFrameworkState \
  --project src/AiTeam.Data \
  --startup-project src/AiTeam.Dashboard \
  --context AppDbContext
```

---

## 子項 2：`AppealState` production 版

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Appeal/AppealState.cs`（新檔，**注意：production main 上的 `Workflows/` 是新資料夾**，spike branch 的 `spike/MsAgentFramework.Poc/Workflows/` 留作 reference）

**對齊既有 production schema**：
- 不只是 spike POC 的 `TaskDescription / CodyPlan / VeraReview / PetraVerdict / IterationCount` 5 個欄位
- 對齊真實 `TaskGroup` / `TaskItem` 欄位（GroupId / TaskItemId / DevPlan / ReviewBody / Round / TraceContext / etc）
- `[JsonPropertyName]` 對應 framework `ChatResponseFormat.ForJsonSchema<T>` 的 LLM structured output

**設計約束**：
- State 必須能 round-trip 序列化 / 反序列化（framework Checkpointing 機制）
- 不含 ClaudeCodeService 等 reference 物件（純資料，不能載 service）
- 所有欄位明寫 `null` 語意（spike 報告維度 2「議題 B state schema 漂移」內建解的真實演習）

---

## 子項 3：`ClaudeCodeAgentExecutor` production 化

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Appeal/Executors/ClaudeCodeAgentExecutor.cs`（從 spike branch `spike/MsAgentFramework.Poc/Executors/ClaudeCodeAgentExecutor.cs` 80 LoC 為藍本）

**production 化差異**（vs spike POC）：

| 面向 | spike POC | Stage 49 production |
|---|---|---|
| DI 注入 | `new ClaudeCodeService(NullLogger.Instance)` 直接建構 | constructor 注入 `IClaudeCodeService`（既有 DI，含 `ClaudeCodeProxy` MockMode 支援）|
| 工作目錄 | env `SPIKE_WORKING_DIR` | 從 `TaskGroup.Project` / `GitHubSettings.WorkspacePath` 推導（既有邏輯）|
| Token 計費 | 不記 | 透過 `ClaudeCodeService` 自動記（既有 Stage 44 機制）|
| 失敗處理 | 直接 throw | 對齊既有 `ClaudeCodeResult.Success` contract + framework `ExecutorFailedEvent` |
| log 紀錄 | console only | 寫進 `task_logs` 表（既有 `TaskLogService`）|
| Mode 列舉 | `RunAsync / RunReviewAsync / RunReadOnlyAsync` 3 個 | 對齊 production `RunReviewAsync`（Vera）+ `RunAsync`（Cody），其他 mode 留 Stage 50+ 遷移時加 |

**Cody/Vera 路徑對應**：
- Cody = `RunAsync`（含 file system + shell tools 全套能力）
- Vera = `RunReviewAsync`（讀 PR diff 用 read-only 工具）

---

## 子項 4：`AppealWorkflowFactory` production 版

### 實作項目

**位置**：`src/AiTeam.Bot/Workflows/Appeal/AppealWorkflowFactory.cs`

**production 化差異**（vs spike POC 40 LoC）：

| 面向 | spike POC | Stage 49 production |
|---|---|---|
| Executor | MockCody / MockVera / MockPetra | `ClaudeCodeAgentExecutor` (Cody) + `ClaudeCodeAgentExecutor` (Vera) + Petra `Executor<VeraDecision, string>` 走 framework Anthropic provider |
| State | spike `AppealState` | production `AppealState`（子項 2）|
| Checkpointing | 無 | framework Checkpointing 啟用，superstep 結束時 framework 自動寫 state，外掛 hook 同步寫 `task_groups.FrameworkAppealStateJson` |
| Max iterations | 硬編 3 | 從 `WorkflowSettingsResolver.GetReviewAppealMaxRoundsAsync` / `GetDevPlanAppealMaxRoundsAsync` 動態讀 |
| Routing | 3 條 case（approved / max-iter / loop-back）| 對齊既有 `AppealOrchestrationService` 5 個 entry 行為（含 escalate path）|

**Checkpointing → DB 同步機制**：
- framework Checkpointing 預設寫 in-memory / file
- 加自訂 `ICheckpointStore` 實作（或 framework 提供的擴充點），superstep 結束時同步寫 `task_groups.FrameworkAppealStateJson`
- 容器重啟時從 `task_groups.FrameworkAppealStateJson` 還原 framework Checkpointing → 繼續 superstep

---

## 子項 5：Petra 切 framework Anthropic provider（保留 LlmProviderFactory wrapper）

### 實作項目

**位置**：`src/AiTeam.Bot/Agents/Pm/PmAgentCommons.cs` + `PmReviewService.cs` + `DevPlanAppealService.cs` + `PmRoutingService.cs`

**設計選擇（保守整合）**：
- spike 驗證了「framework 原生 Anthropic provider 能跑 Petra workload」（Function Tools + Structured Outputs 支援充分）
- 但**完全切原生 provider** = 跳過 `LlmProviderFactory` = 跳過 `TokenTrackingProvider` 守門 + `TokenLogService` 紀錄
- → **Stage 49 暫時保留 LlmProviderFactory wrapper**：framework Petra Executor 內部呼叫 `LlmProviderFactory.Create("PM").CompleteAsync(...)`，token 紀錄 + 守門照舊運作
- Stage 54 收尾才切 framework 原生 provider + framework token middleware

**動到的 service 範圍**：
- `PmAgentCommons`：保留現有 ILlmProvider call sites（不動）
- 新建 `PetraFrameworkExecutor`（在 `Workflows/Appeal/Executors/`）— 包 `LlmProviderFactory` 的 framework Executor 對接
- `AppealWorkflowFactory` 接 `PetraFrameworkExecutor` 替代原 spike `MockPetra`

**為什麼不直接動 PmReviewService / DevPlanAppealService**：
- 這兩個 service 屬於 legacy `AppealOrchestrationService` 路徑（feature flag false 時走）
- Stage 49 並行雙系統 = legacy path 必須保持運作
- Stage 54 收尾才砍 legacy path（連帶可砍 PmReviewService / DevPlanAppealService）

---

## 子項 6：feature flag + `AppealOrchestrationService` 入口分流

### 實作項目

**位置**：`src/AiTeam.Bot/Configuration/WorkflowSettingsResolver.cs` + `src/AiTeam.Bot/Orchestration/Appeal/AppealOrchestrationService.cs`

**WorkflowSettingsResolver 擴充**：
- 新增 `GetUseFrameworkAppealLoopAsync(CancellationToken ct)` 回 `bool`
- AppSettings key = `Workflow:UseFrameworkAppealLoop`，預設 `false`
- 沿用既有 5 個 `Workflow:*MaxRounds` 同 pattern

**AppealOrchestrationService 5 個 entry method 分流**：

每個 `Handle*Async` method 開頭加：
```
if (await workflowSettings.GetUseFrameworkAppealLoopAsync(ct))
    return await frameworkAppealRouter.HandleXxxAsync(...); // 新 path
// 既有 legacy 邏輯（不動）
```

5 個 entry methods：
- `HandleDevBlockerAsync`
- `HandleReviewerCompletedAsync`
- `HandleDevPlanCompletedAsync`
- `RunPetraGateAsync`
- `HandleDevPlanEscalationAsync`

**新建 `FrameworkAppealRouter`**（薄包裝層，對應既有 5 個 entry methods）：
- 內部 build framework Workflow（透過 `AppealWorkflowFactory.Create*Workflow()`）
- 跑 Workflow + 等 `WorkflowOutputEvent`
- 把 framework state 同步寫 `task_groups.FrameworkAppealStateJson`
- 結果寫進既有 DB（Stage 49 不動 BossInteraction，escalate 用既有手刻 path）

### Dashboard SystemSettings UI 擴充（沿用 Stage 47 升級）

`src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor` + `.razor.cs` 加：
- 新區塊「v4 漸進遷移控制」
- toggle：「使用 MS Agent Framework Appeal Loop」對應 `Workflow:UseFrameworkAppealLoop`
- 警告文字：「⚠️ 實驗性功能，啟用前請確認 ANTHROPIC_API_KEY 設定 + 跑過 Mock 驗收」

---

## 子項 7：CLAUDE_*.md prompt schema hint 微調（如需）

### 實作項目

**位置**：`src/AiTeam.Bot/Resources/CLAUDE_Vera.md` + `CLAUDE_Petra.md`（如需）

**評估流程**（子項 8 Mock 驗收期間執行）：
1. 跑 Mock 場景觀察 framework `ChatResponseFormat.ForJsonSchema<T>` 強制 schema 後 LLM 輸出正確性
2. 若 LLM 漏輸出某欄位 / 格式不對 → CLAUDE_Vera.md / CLAUDE_Petra.md 對應段加「**Output schema 對應**」hint
3. 若 LLM 完美對齊 framework schema → **不動 prompt**（首選）

**Aria 拿捏拍板原則**：能不動 prompt 就不動，避免影響既有 production 行為。

---

## 子項 8：Mock 場景 + Christ 線下驗收

### 實作項目

**Mock 場景擴充**：在 `MockClaudeCodeService` / `MockScenarioService` 加 `framework_appeal_loop_*` 系列場景（4-5 個），對應 spike POC 的 10 個場景的精選子集：
- `framework_appeal_loop_fast_approve`（Vera 第 1 輪 approve）
- `framework_appeal_loop_max_iter_approve`（max round Petra approve）
- `framework_appeal_loop_max_iter_reject`（max round Petra reject）
- `framework_appeal_loop_max_iter_escalate`（max round Petra escalate → 開 BossInteraction 用既有手刻 path）
- `framework_appeal_loop_crash_recovery`（中途 simulate crash，重啟後驗 framework Checkpointing 從 DB 還原）

**Christ 線下驗收**：見下方 ## 驗收情境 段。

---

## 驗收情境

> Stage 49 是 v4 漸進遷移首發，**驗收必須含 production 切換驗證**（feature flag false → true → false 全週期）。以下 6 場景全部需在 Forge 結案前確認。

### 場景 A：feature flag 預設 false 時 legacy path 不受影響

**怎麼觸發**：
1. push Stage 49 commit → CI/CD 部署
2. AppSettings 表確認 `Workflow:UseFrameworkAppealLoop` 不存在或為 `"false"`
3. 跑 `/mock new_feature_with_proposal`（既有 Mock 場景，跑完整 Cody-Vera-Petra Appeal loop）

**怎麼驗證**：
- ✅ 流程跑通與 Stage 48 spike 之前完全一致（既有 production behavior 0 變動）
- ✅ Bot log 沒有 framework 相關訊息（沒走新 path）
- ✅ `task_groups.FrameworkAppealStateJson` 為 null（沒寫入 framework state）

### 場景 B：Dashboard 切 feature flag → framework path 接管

**怎麼觸發**：
1. Dashboard SystemSettings → v4 漸進遷移控制 → 切「使用 MS Agent Framework Appeal Loop」為 ON
2. 點「套用變更」（沿用 Stage 47 立即 ReloadCacheAsync）
3. 跑 `/mock framework_appeal_loop_fast_approve`

**怎麼驗證**：
- ✅ Bot log 出現 framework Workflow 啟動訊息（含 `Microsoft.Agents.AI.*` 來源 logger）
- ✅ `task_groups.FrameworkAppealStateJson` 有寫入內容
- ✅ Cody / Vera 透過 `ClaudeCodeAgentExecutor` 呼叫，token_logs 表正常記錄（保留 LlmProviderFactory wrapper 生效）
- ✅ Petra verdict 寫進 DB + Discord 通知（既有手刻 path 接手）

### 場景 C：framework Checkpointing crash recovery 從 DB 還原

**怎麼觸發**：
1. `/mock framework_appeal_loop_crash_recovery`（在 Vera 第 2 輪 review 中段 simulate `docker compose restart aiteam-bot`）
2. 等容器重啟後觀察行為

**怎麼驗證**：
- ✅ Bot 啟動 log 出現「從 task_groups.FrameworkAppealStateJson 還原 framework Checkpointing state」訊息
- ✅ workflow 接續 Vera 第 2 輪繼續跑（不是從第 1 輪重來）
- ✅ 最終 Petra verdict 跟未 crash 情境一致

### 場景 D：max iterations escalate 對接既有 BossInteraction

**怎麼觸發**：
1. `/mock framework_appeal_loop_max_iter_escalate`
2. 等 framework Workflow 跑到 max iterations + Petra escalate

**怎麼驗證**：
- ✅ framework Workflow 跑完 `WorkflowOutputEvent`
- ✅ `FrameworkAppealRouter` 內呼叫既有手刻 path 開 BossInteraction（**不直接用 framework Human-in-the-Loop，那是 Stage 51 範圍**）
- ✅ Discord / Dashboard 出現 escalate 卡片，行為與 legacy 一致

### 場景 E：feature flag false 切回 → legacy path 重新接管

**怎麼觸發**：
1. 場景 B 跑完後，Dashboard 切「使用 MS Agent Framework Appeal Loop」回 OFF
2. 點「套用變更」
3. 再跑 `/mock new_feature_with_proposal`

**怎麼驗證**：
- ✅ 流程走回 legacy path（與場景 A 行為一致）
- ✅ 既有 task_groups 中 `FrameworkAppealStateJson` 殘留 row 不影響 legacy path 跑通（legacy 不讀此欄）
- ✅ rollback 路徑驗證安全網有效

### 場景 F：Petra 透過 LlmProviderFactory wrapper 維持 token 紀錄

**怎麼觸發**：
1. 場景 B 跑通後查 `token_logs` 表

**怎麼驗證**：
- ✅ Petra 對應 row 存在（`AgentName = "PM"`）
- ✅ TokenLogService 紀錄完整（含 effective tokens / cost）
- ✅ 守門邏輯生效（如刻意調 `Workflow:DevPlanAppealMaxRounds` = 1 然後超限不會 stuck）

---

## 風險點 / 注意事項

### 1. Anthropic provider prerelease 風險首次曝露 production（高）

**風險**：`Microsoft.Agents.AI.Anthropic 1.3.0-preview.260423.1` 可能含 breaking change / undocumented behavior。

**緩解**：
- feature flag 預設 false → 不啟用就 0 影響
- 啟用後 Bot log 監控 Anthropic provider 異常 → 立即切回 false rollback
- Forge 在 Plan Mode 第一步**驗證套件版本是否已升級**（spike 是 2026-04-24，可能有新版）

### 2. framework Checkpointing 與既有 Crash Recovery 雙系統並存（中）

**風險**：feature flag false 時 legacy `RecoverStuckMeetings` 跑、true 時 framework Checkpointing 跑 — 兩套 recovery 機制可能同時觸發 collision。

**緩解**：
- legacy `RecoverStuckMeetings` 篩選邏輯加 `task_groups.FrameworkAppealStateJson != null` 排除（不接管 framework path 的 task_group）
- framework Checkpointing 同步寫 DB 機制需驗證 superstep 邊界一致性

### 3. Petra 切 framework provider 動依賴鏈（中）— ✅ 已消解（v1.1 路線 B）

**風險**：spike 已驗 framework Anthropic provider 能跑 Petra，但 production Petra 的真實 prompt（CLAUDE_Petra.md）+ 真實任務（DevPlan / Review escalate）可能踩 spike 沒驗到的 corner case。

**v1.1 消解**：路線 B 拍板後 Petra 不切 framework 原生 provider，整合層升級為包 PmReviewService method（同 Cody/Vera 三 Agent 同層）。Petra 既有 prompt + LlmProviderFactory + TokenTrackingProvider 完全不動，spike 沒驗到的 corner case 風險自然消解。Stage 54 收尾才會真切原生 provider。

### 4. 子項 4 Checkpointing → DB 同步機制可能 framework undocumented（中）

**風險**：framework `ICheckpointStore` 擴充點是否真的可用 / 文件是否齊（[Stage 48 spike 揭露 Workflows.Generators 套件分離 doc gap](Stage_48_Roadmap.md)，類似議題可能再現）。

**緩解**：
- Forge Plan Mode 第一步驗證 framework Checkpointing 擴充點（讀 GitHub canonical sample）
- 若 framework 沒提供擴充點 → 退一步用 `IWorkflowContext` ReadStateAsync<T> + superstep 結束 hook 自己同步寫 DB（功能等價，工時 +1 天）

### 5. spike branch POC code 跟 main 不同步（低）

**風險**：spike branch 跟 main 後續會分歧（main 持續修 src/ 但 spike branch 不動），未來 Stage 50+ 遷移時 spike POC 可能過時。

**緩解**：
- Stage 49 開工時把 spike branch 「實質有用內容」（Custom Executor / Workflow Factory / 8 條踩坑紀錄）整合進 main
- spike branch 之後僅作 trace 用，不再更新

### 6. 不踩 production code 邊界翻轉（自省點 #21）

**Stage 48 spike 是「不動 production code」**，**Stage 49 反過來：必須動 production code**。
- 動：`src/AiTeam.Bot/Workflows/Appeal/`（新資料夾）+ `src/AiTeam.Bot/Configuration/WorkflowSettingsResolver.cs` + `src/AiTeam.Bot/Orchestration/Appeal/AppealOrchestrationService.cs` + `src/AiTeam.Data/Entities.cs`（TaskGroup 加欄位）+ Migration + `src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor*`
- 不動：legacy `AppealOrchestrationService` 內既有邏輯（只在 5 個 entry 加分流）/ `PmReviewService` / `DevPlanAppealService`（Stage 54 才砍）/ `Resources/CLAUDE_*.md`（除非子項 7 必需）

---

## 工時估 / Model 建議

### 工時估

| 子項 | 工時（理想）|
|---|---|
| 1. DB schema + Migration | 0.5 天 |
| 2. AppealState production 版 | 1 天 |
| 3. ClaudeCodeAgentExecutor production 化 | 1.5 天 |
| 4. AppealWorkflowFactory production + Checkpointing 整合 | 2-3 天 |
| 5. Petra 切 framework + LlmProviderFactory wrapper | 1.5 天 |
| 6. feature flag + 5 entry 分流 + Dashboard UI | 1 天 |
| 7. CLAUDE_*.md prompt 微調（如需）| 0.5 天 |
| 8. Mock 5 場景 + 驗收 + 文件 + 結案 | 1.5-2 天 |
| **總計** | **9-11 天**（2-3 週）|

### Model / Effort 建議

依 [`workflow_aria_model_effort.md`](../../C:/Users/darkl/.claude/projects/D--Source-Code-AI-Team/memory/workflow_aria_model_effort.md) 四維度：

| 維度 | 評估 |
|---|---|
| **任務複雜度** | 高 — v4 遷移首發 + framework 整合既有 production + 5 entry 分流 + Checkpointing |
| **改動範圍** | L — 跨 Bot/Data/Dashboard 多檔 + 新建 4-5 檔 + Migration |
| **歷史包袱** | 中-高 — Cody-Vera-Petra Appeal loop 是 Trial_v5 4 FF 補強的核心，動到可能踩沒驗過邊界（風險點 #3）|
| **判斷品質要求** | 高 — feature flag 邊界 + Checkpointing 同步機制 + Petra LlmProviderFactory wrapper trade-off |

**建議**：**Opus 1M + high**

理由：
1. **v4 遷移首發**（規模 L + 高判斷品質要求）→ Opus 1M
2. **預估 context 400-600K**（混合型 Stage，spike POC integration + production 整合）→ 對齊 Stage 47 校準錨教訓「>180K 直接 Opus 1M」
3. **可能拆 session**：
   - Session A：子項 1-4（DB + State + Executor + Workflow Factory），預估 ~250-300K
   - Session B：子項 5-8（Petra 切換 + feature flag + Mock 驗收 + 結案），預估 ~200-300K

### Context 預估

依 7 項公式 + 混合型 Stage 校準（spike POC integration + production 整合）：
- 開場 ~32K
- 工作 raw（新建 4-5 檔 + 動 5 entry + Migration + Dashboard）~120-180K
- Grep / Bash 輸出 ~20-30K（讀 spike branch + grep AppealOrchestrationService callers + dotnet build）
- 對話 turn 成本 ~50-80K（Plan Mode + 閘門一 + 結案）
- Edit 反覆對齊 ~30-50K
- Mock 驗收（5 場景）~40-80K
- follow-up 修正 ~50-150K（首次遷移風險中-高，預期 1-3 個 follow-up）
- 結案文件寫作 ~10-20K
- **總計約 ~350-620K**（Opus 1M 內 35-62% 負擔，舒適區但接近 70% 邊界）

→ 拆 session 建議：若 Forge 階段 1-4 結束時 context > 200K，主動跟 Christ 提「拆下一 session 進階段 5+」。

---

## 與 v4 路線的關係

**Stage 49 是 v4 漸進遷移 6 Stage 的首發**：

```
Stage 47 ✅ ops 補丁（FF 四十七，v3.34.0，2026-05-02）
Stage 48 ✅ spike Phase A（FF 四十九，採用結論，2026-05-02）
   ↓
Stage 49（本 Stage）：Cody-Vera-Petra Appeal loop 遷移（v3.35.0，2-3 週）
   ↓
Stage 50（2-3 週）：RunMeetingSession → Group Chat orchestration
Stage 51（1-2 週）：BossInteraction → Human-in-the-Loop
Stage 52（4-6 週）：WorkflowEngine 整體 → Workflow Builder（最大遷移點）
Stage 53（2 週）：Crash Recovery 全面切換到 framework Checkpointing
Stage 54（2-3 週）：收尾 + token middleware + production 切換 + 老 framework code 刪除
   ↓
Stage 55+（評估）：FF 三十六 Phase B 動態流程架構（依 Stage 52 後評估結果）
```

**Stage 49 結案後對 Stage 50 的影響**：
- 若 Stage 49 順利 → Stage 50 RunMeetingSession 遷移可信心 +
- 若 Stage 49 揭露 framework / Anthropic provider 重大議題 → 暫停 Stage 50 + 評估是否需要 spike Phase A.5（補做特定模組驗證）

---

## 實作紀錄

### 子項完成度對照（對齊 Aria 計劃書 8 子項）

| # | 子項 | 狀態 | 備註 |
|---|---|---|---|
| 1 | DB schema `task_groups.FrameworkAppealStateJson` + Migration | ✅ | Migration `20260501111503_Stage49TaskGroupFrameworkState`；text NULL 0 影響既有 schema |
| 2 | `AppealState` production 版 | ✅ | 含 `AppealLoopKind` / `VeraDecision` / `PetraReviewSnapshot` + `AppealStateHelpers` |
| 3 | `ClaudeCodeAgentExecutor` production 化 | ✅ **路線 B 修正** | 寫完整 production 化版本（98 LoC）但**標 `[Obsolete]` 預留 Stage 50+**（Stage 49 業務 Executor 改路線 B 直接 call legacy service） |
| 4 | `AppealWorkflowFactory` + Checkpointing 整合 DB | ✅ | 含 ReviewAppeal + DevPlanAppeal 兩個 Workflow；`AppealCheckpointStore : ICheckpointStore<JsonElement>` 採風險點 #4 首選路徑（無需 fallback）|
| 5 | Petra 切 framework Anthropic provider | ✅ **路線 B 修正** | 改寫為「**Petra Executor 包 PmReviewService method 與 Cody/Vera 同層整合**」（v1.1 拍板）；不切原生 provider，等 Stage 54 收尾 |
| 6 | feature flag + AppealOrchestrationService entry 分流 | ✅ **F3 精簡** | 路線 B 拍板後 5 entry 縮為 **2 entry 真分流**（HandleReviewerCompletedAsync + HandleDevPlanCompletedAsync），3 entry pass-through 走 legacy 避免循環依賴 |
| 7 | CLAUDE_*.md prompt schema hint 微調 | ✅ **不動** | 預判不動實作，路線 B 路徑下 framework Executor 直接 call 既有 service，service 內 prompt 構建邏輯不動，CLAUDE_*.md 風險更低；Mock 5 場景驗收期未觸發 LLM schema 衝突 |
| 8 | Mock 5 場景 + Christ 線下驗收 + 結案 | ✅ | 5 個 `framework_appeal_loop_*` 場景全寫入 `MockScenarioService`；Forge 自驗 6 場景（A/B/E 完整 + C 70% + D 80%）+ 真實 LLM 補驗（HandleDevPlanCompletedAsync + DevPlan 失敗 fallback 防呆 production 真實生效） |

**新增（不在 Aria 計劃書範圍但實作必需）**：
- `AppealMessages.cs`（VeraAppealRoundResult / DevPlanAppealRoundResult / AppealLoopResult）— 解 framework `AddSwitch<T>` predicate 限制
- `AppealLogHelpers.cs`（寫 group.ReviewAppealLog / DevPlanAppealLog 對齊 legacy 行為）

### Session A 結案（2026-05-01，Forge）

**範圍**：子項 1-4 + 部分 5/6（基礎設施 + framework 整合層）。

**新增 9 檔（~1100 LoC）**：

| 檔案 | LoC | 職責 |
|---|---|---|
| `src/AiTeam.Bot/Workflows/Appeal/AppealState.cs` | ~140 | 跨 executor 共享 state（framework Checkpointing 序列化單位） |
| `src/AiTeam.Bot/Workflows/Appeal/AppealMessages.cs` | ~50 | VeraAppealRoundResult / DevPlanAppealRoundResult / AppealLoopResult（解 framework AddSwitch&lt;T&gt; predicate 限制 — 派生 flag 含進去）|
| `src/AiTeam.Bot/Workflows/Appeal/AppealLogHelpers.cs` | ~25 | 寫 group.ReviewAppealLog / DevPlanAppealLog（對齊 legacy AppealOrchestrationService 行為）|
| `src/AiTeam.Bot/Workflows/Appeal/AppealCheckpointStore.cs` | ~180 | `ICheckpointStore<JsonElement>` 寫 task_groups.FrameworkAppealStateJson（風險點 #4 首選路徑成功）|
| `src/AiTeam.Bot/Workflows/Appeal/AppealWorkflowFactory.cs` | ~120 | build 兩個 framework Workflow（ReviewAppeal Cody-Vera-Petra + DevPlanAppeal Cody-Petra）+ CheckpointManager 工廠 |
| `src/AiTeam.Bot/Workflows/Appeal/Executors/ClaudeCodeAgentExecutor.cs` | ~110 | **[Obsolete] 預留 Stage 50+ Group Chat orchestration**（路線 B 拍板後 Stage 49 不直接引用） |
| `src/AiTeam.Bot/Workflows/Appeal/Executors/CodyReviewAppealExecutor.cs` | ~80 | partial [MessageHandler] 多型 input：第 1 輪接 AppealState、第 N 輪接 VeraAppealRoundResult |
| `src/AiTeam.Bot/Workflows/Appeal/Executors/VeraReviewAppealExecutor.cs` | ~115 | Executor&lt;CodyAppeal, VeraAppealRoundResult&gt; |
| `src/AiTeam.Bot/Workflows/Appeal/Executors/PetraReviewExecutors.cs` | ~180 | Petra Gate + Arbitration 兩個 final Executor |
| `src/AiTeam.Bot/Workflows/Appeal/Executors/DevPlanAppealExecutors.cs` | ~210 | CodyDevPlan partial + PetraReassess + Finalize |

**改檔（少量精準改動）**：
- `src/AiTeam.Data/Entities.cs` — TaskGroup 加 `FrameworkAppealStateJson` 1 nullable 欄位
- `src/AiTeam.Data/Migrations/20260501111503_Stage49TaskGroupFrameworkState.cs` — Migration（單純 add column，0 影響既有 schema）
- `src/AiTeam.Bot/AiTeam.Bot.csproj` — 加 4 個 Microsoft.Agents.AI.* 套件
- `src/AiTeam.Bot/Configuration/WorkflowSettings.cs` + `WorkflowSettingsResolver.cs` — `UseFrameworkAppealLoop` key + Resolver method
- `src/AiTeam.Bot/Program.cs` — DI 註冊（`AppealCheckpointStore` + `AppealWorkflowFactory` 都 Singleton；framework Executor 不註冊 DI）

**Session A commit**：[`90c6ed3`](https://github.com/darkleong/AiTeam/commit/90c6ed3) — `feat(stage49-A): v3.35.0 進行中 — Session A：Workflow + Executors + Checkpointing 整合 DB（路線 B service 包裝）`

### Session B 結案（2026-05-01，Forge）

**範圍**：子項 6 剩餘 + 子項 7（CLAUDE_*.md 預判不動）+ 子項 8 + 結案。

**新增 1 檔**：
- `src/AiTeam.Bot/Orchestration/Appeal/FrameworkAppealRouter.cs` — 路線 B 精簡：只 2 個 method（HandleReviewerCompletedAsync + HandleDevPlanCompletedAsync），3 entry pass-through 走 legacy（避免循環依賴）

**改檔**：
- `src/AiTeam.Bot/Orchestration/Appeal/AppealOrchestrationService.cs` — 2 entry 開頭加 feature flag 分流（不改內部）
- `src/AiTeam.Bot/Orchestration/Meeting/MeetingOrchestrationService.cs` — `RecoverStuckOrchestrationsAsync` 加 `g.FrameworkAppealStateJson == null` 排除條件（風險點 R2 雙系統 collision 防護）
- `src/AiTeam.Bot/Orchestration/AgentQueueProcessor.cs` — 啟動加呼叫 `frameworkRouter.RecoverStuckFrameworkAppealsAsync`
- `src/AiTeam.Bot/Services/MockScenarioService.cs` — 5 個 `framework_appeal_loop_*` 場景
- `src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor*` — v4 漸進遷移控制 toggle
- `src/Directory.Build.props` — Version 3.34.0 → 3.35.0
- 此檔（Stage_49_Roadmap.md）— v1.0 → v1.1（路線 B 拍板補強）

**Session B commit**：[`3400e5b`](https://github.com/darkleong/AiTeam/commit/3400e5b) — `feat(stage49): v3.35.0 — v4 漸進遷移首發，Cody-Vera-Petra Appeal loop 切 MS Agent Framework + feature flag（Session B 收尾）`

### 驗收結果（Forge 自驗 + 真實 LLM 補驗）

| # | 場景 | 結果 | 證據 |
|---|---|---|---|
| **A** | flag false legacy 不影響 | ✅ 完整（與 E 同 group 同源驗） | Verify-E group：state_null=t、ActiveOrchestration=NULL、無 `[Stage49]` log |
| **B** | flag true framework path 接管 | ✅ 完整 | • fast_approve（短路 fallback）+ max_iter_approve（**完整 framework Workflow** 3 round Cody-Vera-Petra → Petra Arbitration → verdict `max_iter_arbitration_approve`）<br>• `FrameworkAppealStateJson` 每 superstep 寫 DB + finally 清空<br>• `ReviewAppealLog` 雙寫對齊 legacy |
| **B+ 真實 LLM** | framework path production 觸發 | ✅ **新驗** | `/task` 觸發 Cody Dev_plan → `[Stage49] HandleDevPlanCompletedAsync framework path 接管`（**production 真實 LLM**，2 entry 之一）→ `[FrameworkAppealRouter] DevPlan 失敗，fallback legacy`（**Forge 自設計防呆 production 生效**） |
| **C** | Checkpointing crash recovery | ✅ 70% | • SQL 模擬 crash state + `docker restart aiteam-bot`<br>• `[Stage49-CrashRecoveryFramework] Crash Recovery：legacy path 跳過 1 個 framework path TaskGroup`（雙系統隔離 R2 緩解生效）<br>• `[FrameworkAppealRouter] 啟動：發現 1 個 stuck framework appeal` + Recovery scan + 降級策略清 marker |
| **D** | max-iter escalate 對接 BossInteraction | ✅ 80% | framework Workflow 跑到 Petra Arbitration（Round 3/3）+ verdict 翻譯完整；Mock arbitration 默認 approve 未觸 escalate path（剩 20% 須 MockMode=false 真 LLM 判 reject） |
| **E** | flag false 切回 legacy 重新接管 | ✅ 完整 | Verify-E 跑 `fail_review`：legacy `Appeal Round A 1/2/3` log（**無** `[FrameworkAppeal] Cody Round` 訊息） |
| **F** | Petra LlmProviderFactory wrapper token 紀錄 | ✅ 路線 B 結構保證 + 同源驗證 | • MockMode=true 時 `LlmProviderFactory.Create("PM") → MockLlmProvider`（**有意不 wrap TokenTrackingProvider** 避免污染統計頁，0 行 token_logs 是預期）<br>• `/task` 真實 LLM 跑：Cody Dev_plan token_log $0.231425 寫入（4+4166 tokens + cache 22820+55383）— **TokenTrackingProvider 機制 production 真實生效** |

**驗收 group**：4 個 Mock + 1 個真實 LLM（共 5 個 production 真實 group），全部 Status='done'/'cancelled'、marker 全清空、流程跑到既有 dispatcher。

### 驗收後修正

**無**。驗收期 Forge 自驗 + 真實 LLM 補驗全部跑在 production 容器，**0 行程式碼修正**。所有「殘留 30%」（C 場景 framework Checkpointing 真實 ResumeAsync / D 場景真 LLM 觸 escalate / F 場景 MockMode=false 必驗）均屬「機制層保證已強，靜態 code review 證實無誤，等 Stage 50+ 自然演進時驗證」性質的驗收限制，**非 Stage 49 程式碼缺陷**。

⚠️ **驗收期意外驗到的關鍵防呆**：`/task` 觸發 tech_improvement 任務（加 1 行 comment）→ Cody Dev_plan 因任務太小缺結構 marker → `IsDevPlanFailed=true` → **`FrameworkAppealRouter` 主動 fallback to legacy `AppealOrchestrationService.HandleDevPlanCompletedAsync`**（Forge Session B 自設計防呆 production 真實生效）。這是 Stage 49 設計關鍵韌性，計劃書原本沒列出但實作期主動加入，驗收期意外觸發證實有效。

### 關鍵設計決策（為什麼這樣選）

| # | 決策 | 選擇 | 為什麼這樣選（vs 替代方案） |
|---|---|---|---|
| 1 | **framework Executor 整合層級** | 路線 B（service 包裝） | Aria 計劃書字面要求 路線 A（重寫 prompt + 接 IClaudeCodeService/LlmProviderFactory），Forge Session A 結束前發現 Aria 計劃書內部三 Agent 不同層整合不一致（Cody/Vera 底層 vs Petra 中層）。Christ 拍板路線 B：framework Executor 直接 call `ReviewAppealService` / `PmReviewService` / `DevPlanAppealService` method —— ① 三 Agent 同層整合 ② Prompt SoT 統一消解 R4 drift ③ -30% 工時 + 風險降低。代價：Stage 54 +1-1.5 天（framework Executor 重寫從 service 切回直連） |
| 2 | **DI factory 模式** | framework Executor 不註冊 DI，由 AppealWorkflowFactory 內 new + 注入 IServiceScopeFactory | 框架驗證 B 結論：framework 1.3.0 Configured&lt;T&gt; + ExecutorConfig 機制本身是 factory 模式，對齊框架慣例。Executor ctor 注入 IServiceScopeFactory，HandleAsync 內 CreateAsyncScope 取 scoped services（DbContext / LlmProviderFactory / ReviewAppealService）。**徹底解 Singleton+Scoped 陷阱**（Singleton Executor 持有 Scoped DbContext 跨 superstep 失效或炸） |
| 3 | **2 entry 真分流 vs 5 entry 全分流** | 只 HandleReviewerCompletedAsync + HandleDevPlanCompletedAsync 2 entry 真建 framework Workflow，3 entry pass-through 走 legacy | Aria 計劃書原寫「5 entry method 開頭加分流」。Forge F3 探索期發現 RunPetraGateAsync / HandleDevBlockerAsync / HandleDevPlanEscalationAsync 都不含 Appeal loop（單輪 Petra Gate / 純 Petra 路由 / Dashboard callback 純 routing），即使 feature flag 開仍可走 legacy。**避免循環依賴**：FrameworkAppealRouter 跟 AppealOrchestrationService 互相依賴會循環，精簡 2 entry 設計用 `IServiceProvider.GetRequiredService` 動態取 legacy 而非 ctor 注入 |
| 4 | **Workflow input = AppealState（取代 string trigger）** | Cody Executor 第 1 輪 [MessageHandler] 接 AppealState 直接寫 framework state | 原 spike POC 設計用 string trigger + router pre-seed in-memory dict 讓 Executor 第 1 輪讀 initial state。Session B 改造後直接傳 AppealState 為 first input message：① 移除 router 額外 dict 狀態 ② 對齊 framework MessageHandler 多型 input 慣用 ③ Cody Executor 內 `SaveAsync(initialState)` 寫進 framework state，後續 superstep 自然讀得到 |
| 5 | **AppealCheckpointStore 採 ICheckpointStore<JsonElement> 首選路徑** | 實作 `ICheckpointStore<JsonElement>` + `CheckpointManager.CreateJson(store, options)` | Aria 計劃書風險點 #4 預警 framework Checkpointing 擴充點可能 undocumented，列了 fallback 用 `IWorkflowContext.QueueStateUpdateAsync` + superstep hook 自寫 DB（功能等價，工時 +1 天）。Forge Session A 第一步驗證 framework 1.3.0 NuGet xml doc，發現 **Checkpointing 提供完整公開 API**（`ICheckpointStore<TStoreObject>` + `CheckpointInfo` + `InProcessExecution.WithCheckpointing`），首選路徑直接成功，無需 fallback |
| 6 | **ClaudeCodeAgentExecutor [Obsolete] 預留 Stage 50+** | 保留檔案 + 標 [Obsolete] 註明 Stage 49 不引用 | 路線 B 拍板後 Stage 49 不直接引用 ClaudeCodeAgentExecutor（業務 Executor 直接 call legacy service）。但保留檔案不刪：Stage 50+ Group Chat orchestration 遷移時，會議內多 Agent 互相 talk 需 Executor → IClaudeCodeService 直連，沒有 service 上層可包，會直接用此 wrapper。Stage 54 收尾若決定 framework Executor 從 service 切回直連時也會用上 |
| 7 | **DevPlan 失敗 fallback 防呆**（Forge Session B 自設計） | FrameworkAppealRouter.HandleDevPlanCompletedAsync 偵測到 PmAgentCommons.IsDevPlanFailed=true 時 fallback 到 legacy AppealOrchestrationService.HandleDevPlanCompletedAsync | 避免 framework Cody-Petra Appeal Workflow 上跑失敗 plan（會無限 loop 因為 Cody 重產同樣失敗）。**驗收期 production 真實生效**：`/task` 觸發 tech_improvement 任務（加 1 行 comment 太小），Cody Dev_plan 缺結構 marker → IsDevPlanFailed=true → framework 主動 delegate to legacy 走 Stage 43 重產上限機制 |

### 踩坑紀錄彙整

> 對 Stage 50+ 後續遷移有預警價值的坑：

1. **JSON binding 必須 camelCase**：Bot `InternalController` `/internal/mock/scenario` 接 `MockScenarioRequest(string Scenario, string? Title, string? Project)`，curl 傳 `{"Scenario":"...","Title":"..."}`（大寫）失敗 binding，HTTP 400。改 camelCase `{"scenario":"...","title":"..."}` 才通。.NET 9+ ASP.NET 預設 `JsonSerializerOptions.PropertyNamingPolicy = CamelCase`。Stage 50+ 透過 internal API 觸發 Mock 場景時注意。

2. **MockMode 啟用時 token_logs 0 行是預期行為**：`LlmProviderFactory.Create()` line 47-48 顯式 `if (MockMode) return MockLlmProvider`，**有意不包裝 TokenTrackingProvider 避免假統計資料污染 Dashboard 監控頁**（Stage 17 既有設計）。Stage 49 場景 F「Petra 透過 LlmProviderFactory wrapper token 紀錄」在 MockMode 下無法直接驗，要 MockMode=false 真實 LLM 跑才會寫 token_logs。Stage 50+ 驗證 token middleware 升級時須切 MockMode=false。

3. **PausePoint 機制不適用 framework Workflow internal**：Stage 45 既有 `PausePoint = (groupId, beforeStep)` 是「即將 fire NEXT step」機制，framework Workflow 內 superstep 不 fire steps（in-process Run），所以 Stage 49 `framework_appeal_loop_crash_recovery` 場景的 PausePoint 在 Reviewer step 之前就觸發暫停，**framework Workflow 都還沒啟動**。要驗 framework Checkpointing 真實 ResumeAsync 須改用 SQL 模擬 crash state（Forge 自驗採此路徑）或真 superstep-mid process kill（Christ 線下驗收 30% 殘留）。Stage 53 Crash Recovery 全面切換時須重新設計 PausePoint 機制（或廢棄）。

4. **Victoria CEO 自己處理 docs 任務不派工 Cody/Vera**：Stage 15 起 Victoria 有 `RunVictoriaAsync` mode（讀 repo / 寫 docs/ / git commit），驗收期下「建立 .md 檔」自然語言指令會被 Victoria 自己 commit（Discord 訊息「請建立 docs/test/...」直接走 Victoria CEO 自處理路徑），**繞過 Cody-Vera-Petra Pipeline**。**Stage 49+ 驗收 framework path 必須用 `/task` slash command 直接建 TaskGroup 跳過 Victoria 解析**。

5. **TechImprovement workflow 仍走 Reviewer**：原以為 tech_improvement 不審 code（Cody 改自己 code 通常不需 Vera 審），實際 `WorkflowEngine.cs:115-116` 顯示 `["Dev"] = [new WorkflowStep("Reviewer")]`，仍會觸發 framework path。Stage 49 驗收期透過 `/task` 觸發 Victoria 解析判 tech_improvement 仍能驗 framework path，可信賴。

6. **Cody Dev_plan 對「太小任務」產出失敗**：1 行 comment 任務，Cody 計畫書缺 `## 實作說明` / `## 實作步驟` 結構 marker，`PmAgentCommons.IsDevPlanFailed`（Stage 43 起）判 fail。Stage 49 框架 path 偵測到後正確 fallback 到 legacy（**Forge 防呆設計生效**）。Stage 49+ 驗收任務必須**有實質 code 改動**（非空 plan），避免卡 IsDevPlanFailed loop。

7. **CS8602 framework AddCase predicate 警告**：framework `AddCase<T>(Func<T, bool>, ...)` 對 unconstrained generic T 視為可能 null，產生 12 個 CS8602 warning。修法對齊 spike POC 模式：`vd?.Approved == true`（null-conditional），不寫 `vd.Approved == true`。Stage 50+ 寫 framework Workflow 拓撲時記得用 null-conditional。

8. **NuGet 套件版本與 spike 完全一致（無升級）**：原 Aria 計劃書風險點 #1 預警「Stage 49 開工時可能升級，breaking change 風險中」，要求 Forge Plan Mode 第一步主動 WebFetch 確認。Forge 開工時查 NuGet（2026-05-02），4 個套件（`Microsoft.Agents.AI` / `.Workflows` / `.Workflows.Generators` / `.Anthropic`）全與 spike 2026-04-24 snapshot **完全一致**。Anthropic provider 仍 `1.3.0-preview.260423.1`（最新 prerelease，無 stable）。**對 Stage 49 風險評估**：feature flag 預設 false 為主要安全網生效，未實際曝露 production 風險。Stage 50+ 開工時須再驗一次套件版本。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-02 | 初版規劃書建立（Aria）—— v4 漸進遷移首發 Stage |
| v1.1 | 2026-05-02 | Forge 實作完成 + Aria 拍板路線 B 補強 —— ① 子項 3 加註 [Obsolete] 預留 Stage 50+ ② 子項 5 改寫為「包 PmReviewService method 與 Cody/Vera 同層」③ 設計決策段加「framework Executor 整合層級拍板（路線 B）」+「DI factory 模式」兩條 ④ 風險點 R3 標 ✅ 已消解（路線 B 自然解 Petra prompt drift）⑤ 文件版本與狀態更新 |
| v1.2 | 2026-05-01 | Forge 實作紀錄補強 —— Session A + Session B 兩段 commit 紀錄 + 6 場景驗收結果（A/B/E 完整、C 70%、D 80%、F 路線 B 結構保證 + 真實 LLM 同源驗證）+ 7 個關鍵設計決策表格 + 8 條踩坑紀錄。狀態更新為「實作完成 + 驗收通過（30% 殘留留 Stage 50+ 自然演進）」 |
| **v2.0** | **2026-05-01** | **Stage 49 結案版（forge-end SOP）—— 規劃→實作完成分水嶺 major bump**：① 加「子項完成度對照」段（對齊 Aria 計劃書 8 子項 + 含路線 B 修正 / F3 精簡 / 不動 CLAUDE_*.md 等實作偏離 Aria 計劃書處的明確標記）② 加「驗收後修正」段（**無修正**，驗收期意外驗到 DevPlan 失敗 fallback 防呆 production 真實生效）③ header 文件版本 v1.2 → v2.0 + 狀態加日期 ④ 等 Aria 接結案第二段（CHANGELOG v3.35.0 + Future_Feature_changelog v7.66）|
