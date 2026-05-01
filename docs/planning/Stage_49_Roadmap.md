# Stage 49：v4 漸進遷移首發 — Cody-Vera-Petra Appeal loop 切 MS Agent Framework + feature flag

> 對應 Future Feature：v4 漸進遷移 6 Stage 路線首發（[Stage 48 spike 報告](../experiments/Spike_v1_MsAgentFramework.md) 節 7）— 不對應特定 active FF（v4 路線進入 Stage 工作模式，按 Stage 走不開新 FF）
> 對應版本：**v3.35.0**（v4 漸進遷移首個產生版本變動的 Stage）
> 建立日期：2026-05-02
> 狀態：📋 規劃中
> 文件版本：v1.0

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
| **3** | `ClaudeCodeAgentExecutor` production 化（從 spike branch 搬 main + 加 production 整合）| M |
| **4** | `AppealWorkflowFactory` production 版（framework Workflow Builder + Checkpointing 整合 DB）| M |
| **5** | Petra 切 framework Anthropic provider（保留 LlmProviderFactory wrapper 維持 TokenLogService）| M |
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

### 3. Petra 切 framework provider 動依賴鏈（中）

**風險**：spike 已驗 framework Anthropic provider 能跑 Petra，但 production Petra 的真實 prompt（CLAUDE_Petra.md）+ 真實任務（DevPlan / Review escalate）可能踩 spike 沒驗到的 corner case。

**緩解**：
- 子項 8 Mock 場景含 max iterations escalate（場景 D）覆蓋 Petra 真實仲裁路徑
- Stage 49 仍透過 LlmProviderFactory wrapper（不切原生 provider）— 行為盡量對齊 legacy
- Stage 54 才完全切原生 provider

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

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-02 | 初版規劃書建立（Aria）—— v4 漸進遷移首發 Stage |
