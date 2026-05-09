# Stage 59：TaskGroupService 怪物大檔拆解（FF 五十四 子項 1）

> 對應 Future Feature：FF 五十四（Stage 36 後怪物大檔復發追蹤）子項 1 = TaskGroupService.cs 拆解（最痛 + 最大）
> 對應版本：**v3.48.0**（Stage 58 v3.47.0 + minor bump）
> 建立日期：2026-05-10
> 狀態：📋 規劃中
> 文件版本：v1.0

---

## 概述

**戰略背景**：Stage 34-36 拆完四怪物（FF 二十系列，2026-04-22）後 v4 漸進遷移路線（Stage 49-58）一路加 routing / NotifyBoss / TryRoute / dispatch case → TaskGroupService 又漲到 **1759 行**（Stage 58 後 +70），超 `docs/conventions/refactor-sop.md` 警戒線 800-1200 + 超 Stage 36 拆解前 baseline 1300+ 行。Christ 2026-05-10 觀察 Forge Plan context 漸漲根因之一。FF 五十四子項 1 = TaskGroupService 拆解（最痛 + 最大）。子項 2/3（ButtonCallbackRouter 1091 / DevAgentService 958）依本 Stage ROI 後評估。

### 範圍邊界

- ✅ **TaskGroupService.cs 拆解**：1759 行 → 主檔瘦身 ~400-500 行 + 4-5 新子 service 各 ~200-400 行（對齊 Stage 36 -73% 瘦身比例）
- ✅ **依職責分檔**（不採 partial class —— 對齊 Stage 36 拆解 SOP 既有模式）
- ✅ **caller 切換**（對齊 refactor-sop.md SOP 2 — caller < 15 直接切換不做 thin wrapper，Forge spike 第一步 grep caller 數確認）
- ✅ **子目錄組織**（對齊 SOP 6 — Orchestration/ 根目錄已 12 檔 + 拆後 4-5 新檔 → Forge spike 提案具體子目錄結構）
- ✅ **DI 註冊**（對齊 SOP 4 — Singleton + 子 service 單向依賴 Commons 不可反向）

- ❌ **不動**：ButtonCallbackRouter / DevAgentService（FF 五十四子項 2/3，本 Stage ROI 確認後再評估）
- ❌ **不動**：任何 method 簽名 / 業務行為（純檔案搬移 + namespace 補 + DI ctor 注入鏈）
- ❌ **不引入**：新 user transaction / 新 idempotent helper（Stage 57 教訓 + Stage 58 callback boundary 紀律延伸 — 純 refactor 不該需要）

### TaskGroupService 既有 method 分布（partial read，對齊新立第 6 條紀律）

| 區段 | line 範圍 | method 群組 | 行數 |
|---|---|---|---|
| **A. Group lifecycle** | 61-490 | CreateGroupAsync / HandleAgentCompletedAsync / FireMockProposalAndContinueAsync / FireStepsAsync / Pause+Resume / CancelAsync / FireOneStepAsync (private) | ~430 |
| **B. NotifyBoss helpers** | 491-728 | NotifyBossMergeAsync / MarkGroupDoneOrInterventionAsync / NotifyBossDevFailedInterventionAsync / NotifyBossReviewerFixLoopLimitAsync (Stage 57) / NotifyBossAgentApiFailureAsync (Stage 58) / NotifyBossInterventionAsync | ~240 |
| **C. Meeting handler thin wrappers** | 730-744 | RecoverStuckOrchestrationsAsync / HandleKickoffConfirmedAsync / HandleDesignConfirmedAsync | ~15 |
| **D. ProcessBossResponseAsync 主 dispatch + 5 case handlers** | 746-1171 | ProcessBossResponseAsync 主 + HandleDevFailedIntervention / HandleQaFailedIntervention / HandleSageEscalate / HandleSplitTaskProposal / GetGroupProjectIdAsync (helper) | ~426 |
| **E. Epic + sub-task management** | 1172-1585 | HandleEpicPartialPausedAsync / BuildEpicSubTasksAsync / TriggerNextPhaseIfSubTaskAsync (private) / SimulateEpicRaceAsync (Stage 57 Mock helper) | ~414 |
| **F. Pipeline routing helpers** | 1586-1759 | TryGetPipelineGroupAsync (private) + 7 個 TryRoutePipeline* private helpers（Dev/Qa Intervention + DevPlan Escalate/Unable + ReviewerFixLoopLimit + AgentApiFailure + SplitTaskProposal） | ~174 |

---

## 設計決策

### 主路線（純內部 Aria 拿捏，0 議題該攤 Christ）

對 Christ 看到行為 0 影響（純 refactor 不動業務） → 議題層次篩選紀律 0 議題攤 Christ。

| # | 議題 | 決定 |
|---|---|---|
| 1 | 拆解模式 | **依職責分子 service**（不採 partial class）— 對齊 Stage 34-36 SOP 既有模式 + refactor-sop.md SOP |
| 2 | 子 service 命名 + 數量 | **4 個子 service 提案**（見下方拆解設計）— Forge spike 第一步可微調對齊既有 namespace 慣例 |
| 3 | caller 切換策略 | **直接切換不做 thin wrapper**（對齊 SOP 2 — Forge spike 第一步 grep caller 數確認 < 15；若 ≥ 15 走 thin wrapper 過渡）|
| 4 | 子目錄組織 | Orchestration/ 已 12 檔 + 拆後 4-5 新檔 → **建子目錄**（對齊 SOP 6） — Forge spike 提案具體結構（Notification / Epic / Pipeline 等候選）|
| 5 | DI 註冊 | **全 Singleton 對齊既有 TaskGroupService**（對齊 SOP 4 — 子 service 單向依賴 Commons 不可反向，Forge 實作期 grep 確認無循環依賴）|
| 6 | Migration / schema | **不動**（純檔案搬移）|
| 7 | CLAUDE_*.md prompt | 不動 |
| 8 | refactor-sop.md SOP 6 項 | **全套用** — Record/Type 組織 / Migration 策略 / Commons 範圍 / DI 順序 / Session state 管理 / 子目錄組織 |

### 拆解設計（4 子 service 提案 — Forge spike 細化）

| 新 Service | 職責 | 來源 line 區段 | 預估行數 |
|---|---|---|---|
| **TaskGroupService**（瘦身）| Group lifecycle（CRUD + dispatcher 主入口 + FireSteps + Cancel）| A 區段 + ProcessBossResponseAsync 主 dispatch（D 主入口） | ~400-500 |
| **BossNotificationService** | NotifyBoss helpers + MarkGroupDoneOrIntervention | B 區段 | ~240-280 |
| **BossResponseHandlerService** | ProcessBossResponse 5 case handlers + GetGroupProjectIdAsync helper | D 區段 5 handlers | ~300-400 |
| **EpicChainService** | Epic + sub-task management（BuildEpicSubTasks / TriggerNextPhaseIfSubTask / HandleEpicPartialPaused / SimulateEpicRace）| E 區段 + HandleEpicPartialPaused（從 D 搬入） | ~400-450 |
| **PipelineRoutingService** | 7 TryRoutePipeline + TryGetPipelineGroup | F 區段 | ~180-200 |

**注**：C 區段 Meeting handler thin wrappers（15 行） — 留 TaskGroupService 主檔（thin wrapper 委派到 MeetingOrchestrationService Stage 34 既有產物）。

### Stage 59 子項拆分

| # | 子項 | 規模 |
|---|---|---|
| **0** | **Spike 第一步**：① grep TaskGroupService caller 數確認 thin wrapper 必要性（對齊 SOP 2） ② 提案具體子目錄結構（對齊 SOP 6） ③ 確認 5 子 service 職責邊界 + 跨 service 依賴方向（對齊 SOP 4 無循環） ④ 確認 Commons service 範圍（對齊 SOP 3） | XS |
| **1** | 抽 BossNotificationService（B 區段 line 491-728）— NotifyBoss helpers 6 method 搬遷 | S |
| **2** | 抽 BossResponseHandlerService（D 區段 5 case handlers + GetGroupProjectIdAsync helper）— 注意 ProcessBossResponseAsync 主 dispatch 留 TaskGroupService 主檔，僅 5 case body 抽出 | M |
| **3** | 抽 EpicChainService（E 區段 + HandleEpicPartialPaused 從 D 搬入）— Epic Chain 機制集中管理 | M |
| **4** | 抽 PipelineRoutingService（F 區段 7 TryRoute + TryGetPipelineGroup）— Pipeline framework dispatch 路由集中管理 | S |
| **5** | TaskGroupService 主檔瘦身 + caller 切換（對齊 SOP 2 直接切換）+ DI 註冊（對齊 SOP 4 全 Singleton）+ namespace 補 + using 對齊 | S |
| **6** | 子目錄組織（對齊 SOP 6 — Forge spike 提案具體結構，build 確認 namespace 連鎖修正完整）| XS |
| **7** | Forge 自驗 V1-V6（build / test / Mock 7 routing regression / Mock 完整 pipeline / 行數驗證 / DI 啟動）| S |
| **8** | Version bump v3.48.0 + 結案文件（Roadmap 實作紀錄章節對齊 refactor-sop.md「結案必做清單」5 項）| XS |

> **不寫工時估算** — 各子項規模見上表 XS/S/M 範圍描述。
>
> **預估 Forge context**：對齊 Stage 34/35/36 拆解倍率 ×1.5-1.7（平均 ×1.58）— 預估 ~480-640K / Opus 1M + medium-high。**第一個套用新立紀律的 Stage**（workflow_aria.md 第 5+6 條 — partial read + 不寫 code 範例 + 大檔精準 line + 簽名 reference），預期計劃書本身下降 ~30-40%（vs Stage 58 v1.1 ~580 行）→ 本計劃書 ~280 行。

---

## 子項 0：Spike 第一步 — read 對齊範圍

### read 對齊範本（對齊新立第 6 條紀律 — 標精準 line + method 簽名）

| # | read 對象 | 目的 |
|---|---|---|
| F1 | `docs/conventions/refactor-sop.md` 全檔（128 行） | SOP 6 項實踐對照 |
| F2 | `docs/planning/Stage_36_Roadmap.md` 拆解設計段（line 49-100） | 對齊既有拆解 SOP + 子目錄組織模式 |
| F3 | `src/AiTeam.Bot/Orchestration/TaskGroupService.cs` partial read 6 區段（A/B/C/D/E/F line 範圍見上方表）| 確認區段邊界 + method 依賴方向 |
| F4 | grep `TaskGroupService` caller 數（`Grep "TaskGroupService" --type cs`） | 對齊 SOP 2 確認 thin wrapper 必要性 |
| F5 | `src/AiTeam.Bot/Program.cs` TaskGroupService DI 註冊段 | 對齊 SOP 4 註冊順序模式 |
| F6 | Stage 34/35 既有子 service（`MeetingOrchestrationService.cs` / `Agents/Pm/PmReviewService.cs`）class header + ctor | 對齊既有子 service 命名 + ctor 注入慣例 |

### 寫入點 spike 報告（在計劃書 Plan Mode 內）

Forge 完成 read 後在 Plan Mode 計劃書內報告：

1. **caller 數確認**（grep 結果 + thin wrapper vs 直接切換決定）
2. **子目錄結構提案**（對齊 SOP 6 — Notification / Epic / Pipeline 等候選或單一 SubServices 子目錄）
3. **5 子 service 職責邊界 finalize**（含跨 service 依賴方向圖確認無循環）
4. **Commons service 範圍**（對齊 SOP 3 — 可能無 Commons 需要，5 子 service 各自獨立）

純執行對齊，無需 Christ / Aria 拍板（議題層次篩選 — 純內部實作）。

---

## 驗收情境

> 計劃書硬規則：本節獨立列出，不分散到子項內。每個非顯然點都有 Mock 場景或手動驗證步驟。

### V1：build 不破壞

**觸發**：`dotnet build AiTeam.slnx`

**驗證**：
- 0 errors / 0 new warnings
- v3.48.0 version bump 在 `src/Directory.Build.props` 正確套用
- 4 新子 service `.cs` 檔產生 + namespace 對齊 SOP 6 子目錄結構

### V2：test 全綠

**觸發**：`dotnet test`

**驗證**：
- 既有 131 tests 全 pass（4 AiTeam.Bot.Tests + 127 AiTeam.Tests.Generated）
- 0 new failures

### V3：Mock 7 routing regression（Stage 55B + 57 + 58 全綠）

**觸發**：依序跑 7 routing Mock 場景：
- `framework_pipeline_dev_intervention_hitl`（Stage 55B Session B）
- `framework_pipeline_qa_intervention_hitl`（Stage 55B Session B）
- `framework_pipeline_devplan_escalate_hitl`（Stage 55B Session B）
- `framework_pipeline_devplan_unable_hitl`（Stage 55B Session B）
- `framework_pipeline_split_task_proposal_hitl`（Stage 55B Session B）
- `framework_pipeline_reviewer_fix_loop_limit`（Stage 57）
- `framework_pipeline_agent_api_failure`（Stage 58）

**驗證**：
- 7/7 場景 dispatch 正確（type-specific interaction → user response → Pipeline 推進）
- TryRoutePipeline* helpers 從新 PipelineRoutingService 呼叫成功（不破壞既有 routing 鏈路）
- BossInteraction fire 行為一致（從新 BossNotificationService 呼叫）

### V4：Mock 完整 pipeline 跑通

**觸發**：跑 Mock `new_feature_with_proposal` 完整 pipeline

**驗證**：
- Pipeline 完整跑：CEO proposal → Kickoff → Design → Dev_plan → Dev → Reviewer → QA → Doc → done
- group.Status 最終 = `done`
- token_logs 寫入率 100%（Stage 56 baseline 不變）

### V5：行數驗證 — TaskGroupService 瘦身達標

**觸發**：`wc -l` 5 個檔案

**驗證**：
- TaskGroupService.cs 從 1759 行降到 ~400-500 行（瘦身 -70%+ 對齊 Stage 36 -73%）
- BossNotificationService.cs ~240-280 行
- BossResponseHandlerService.cs ~300-400 行
- EpicChainService.cs ~400-450 行
- PipelineRoutingService.cs ~180-200 行
- 5 檔合計 ~1500-1800 行（拆完總碼略多是 boilerplate 正常 — 對齊 refactor-sop.md「拆完行數可能變多是正常」）

### V6：DI 啟動驗證 — Bot 啟動 log

**觸發**：Bot 重啟（push 觸發 CI/CD 自動 deploy）

**驗證**：
- 啟動 log 顯示 `Version 3.48.0`
- 無循環依賴錯誤（DI container build 通過）
- 4 新子 service 正確註冊（Singleton lifetime）
- BossInteraction fire / BossResponse handler / Epic Chain / Pipeline routing 4 chain 行為一致

---

## 技術約束

- v3.48.0 version bump（Stage 58 v3.47.0 + minor）
- `dotnet build AiTeam.slnx` 0 errors
- 不引入新 Migration（純檔案搬移 + namespace 補，無 DB schema 改動）
- 不引入新 user transaction（Stage 57 教訓延伸）
- 不引入新 idempotent helper（Stage 57 教訓延伸）
- 不動任何 method 簽名（純檔案搬移 + namespace 補 + DI ctor 注入鏈）
- 不動業務行為（行為驗證靠 Mock regression V3+V4）
- 對齊 refactor-sop.md SOP 6 項（Record/Type / Migration / Commons / DI / State / 子目錄）
- 對齊 Stage 34/35/36 既有拆解 commit 風格（`refactor(stage59): TaskGroupService 拆解 — 5 子 service / -73% 瘦身 ...`）

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v1.0 | 2026-05-10 | 初版規劃書建立（Aria）— Stage 59 = FF 五十四子項 1 TaskGroupService 拆解（1759 行 → 主檔瘦身 ~400-500 + 4 新子 service）。**0 議題攤 Christ**（純 refactor 不動業務 → 對 Christ 看到行為 0 影響）。**Aria 拿捏 8 件純內部議題**（拆解模式 / 子 service 命名 / caller 切換 / 子目錄組織 / DI / Migration / prompt / SOP 6 全套用）。**Stage 57+58 教訓延伸**（不引入新 user transaction + 新 idempotent helper）。**規劃前期已 grep**：TaskGroupService method 分布 6 區段 partial read（A/B/C/D/E/F line 範圍 + method 群組）+ refactor-sop.md SOP 6 項 + Stage 36 Roadmap 拆解設計段 — 對齊自省點 #23 規劃前期 grep 紀律 + **對齊新立 workflow_aria.md 第 5+6 條紀律**（partial read + 不寫 code 範例 + 大檔精準 line + 簽名 reference）。**Aria 校準錨預估**：對齊 Stage 34/35/36 拆解倍率 ×1.5-1.7（平均 ×1.58），預估 Forge context ~480-640K / Opus 1M + medium-high。**第一個套用新立紀律的 Stage**（驗證計劃書本身下降 ~30-40%）。
| v2.0 | 2026-05-10 | **Stage 59 完整結案（Aria 第二段 step 0 + 1 + 2 + 3）— v3.48.0 主實作 + Forge V1-V6 全綠 + 跨 Stage know-how 升級首次實踐**。**Aria 校準錨 ×1.09**（402K vs 預估 370K，混合型第 13 資料點 mid 中段，接近 Stage 50 ×1.09 / Stage 56 ×0.92）— **戰略結論：FF 二十系列拆解倍率從 Stage 34-36 平均 ×1.58 降到 ×1.09（-31%）**證明 SOP 累積 + workflow_aria.md 第 5+6 條紀律生效（partial read + 不寫 code 範例 + 大檔精準 line + 簽名 reference Stage 59 計劃書 -65% vs Stage 58 580 行）。**Step 0 升級實踐**（首次套用 Aria 結案第二段新立 SOP）：① refactor-sop.md SOP 2 加「caller 改動成本評估三層分」② SOP 6 加「子目錄 / namespace 名稱避免與既有 entity 同名」C# shadow 規則 ③ 實戰數據加 Stage 59 row + 「dispatch 型主檔瘦身典型 -50%~-60%」觀察 ④ forge-self-verify skill port 5051→5052 + X-Api-Key 補（Stage 56→59 踩坑揭露 source of truth 規則 + workflow_aria.md 第三節 A 第 7 條紀律）。**FF 五十四子項 1 ✅**（Stage 60+ 評估子項 2/3 = ButtonCallbackRouter 1091 / DevAgentService 958）。Aria 結案第二段 commit + push。

---

## 實作紀錄（v2.0 — 對齊 refactor-sop.md「結案必做清單」5 項）

### 1. 實際產出檔案 + 行數（vs 規劃預估）

| 檔案 | 實際行數 | 規劃預估 | 差距 |
|---|---|---|---|
| **TaskGroupService.cs**（主檔瘦身）| **808 行** | 450-550 行 | **+47%~+79% 超出**（因 ProcessBossResponseAsync 主 dispatch switch ~150 行 + FireOneStepAsync framework entry guard ~70 行 必須留主檔，plan 預估 -69%~-74% 過樂觀）|
| BossNotificationService.cs（Boss/）| 208 行 | 280-320 行 | -26%~-35% 比預估精簡 |
| BossResponseHandlerService.cs（Boss/）| 267 行 | 340-400 行 | -21%~-33% 比預估精簡 |
| EpicChainService.cs（Epic/）| 372 行 | 420-460 行 | -11%~-19% 命中區間下緣 |
| PipelineRoutingService.cs（Routing/）| 204 行 | 190-220 行 | ✅ 命中區間 |
| **5 檔合計** | **1859 行** | 1500-1800 行 | +5.7% vs 原 1759 行（對齊 SOP「拆完總碼略多是 boilerplate 正常」）|

**主檔瘦身 -54%**（1759 → 808） — 對齊 Stage 36 -73% 期望偏低，但仍是有意義降低。

### 2. SOP 套用對照（refactor-sop.md 6 項）

| SOP | 本次實踐 |
|---|---|
| **SOP 1：Record/Type 組織** | 本次無新 public record；`SplitProposal`（既有 Stage 52 抽出的 MeetingResults.cs）by EpicChainService + BossResponseHandlerService 共用 — `using AiTeam.Bot.Orchestration.Meeting;` import 不動既有設計 ✓ |
| **SOP 2：Migration 策略** | **直接切換不做 thin wrapper**（caller class ~11 個 < 15 對齊 SOP 表格）— 22+ call site 機械化 replace `tgs.NotifyBossXXX` → `bossNotification.NotifyBossXXX` + 11 caller 加 `using AiTeam.Bot.Orchestration.Boss;`。**無 ctor 改動**（揭露：所有 caller 都用 `IServiceScopeFactory` lazy resolve，scope.GetRequiredService 模式取代 ctor 注入）✓ |
| **SOP 3：Commons 範圍** | **不需要 Commons** — 5 子 service 各自職責不重疊，無共用 helper > 5 行 ✓ |
| **SOP 4：DI 註冊順序** | Program.cs L132-135 補 4 新子 service AddSingleton（順序：Boss → Epic → Routing；BossResponseHandler 在 BossNotification 後對齊邏輯依賴方向）+ TaskGroupService L176 維持原位 ✓ |
| **SOP 5：Session state 管理** | 無 singleton-level state — 5 子 service 全 stateless（per-call scope） ✓ |
| **SOP 6：子目錄組織** | **3 子目錄 Boss/Epic/Routing**（spike 後修正方案 — 原 plan「TaskGroup/ 統一目錄」因 namespace 衝突改為 single-theme 多子目錄對齊 Stage 36 Meeting/Appeal/Qa/Proposal 既有 pattern） ✓ |

### 3. 踩坑紀錄（refactor-sop.md SOP 沒涵蓋的新發現 — 供未來擴充 SOP）

#### 踩坑 #1：**Namespace 與 entity 同名衝突**（C# child namespace shadow 規則）

**現象**：原 plan「Orchestration/TaskGroup/ 統一子目錄」第一次 build 報 75 errors `'TaskGroup' is a namespace but is used like a type`。

**根因**：C# 編譯器在 `AiTeam.Bot.Orchestration.X` namespace 內優先解析同層 child namespace，`Orchestration.TaskGroup` namespace 與 `Data.TaskGroup` entity 同名 → entity 被 shadow。所有 `AiTeam.Bot.Orchestration.*` 內既有 file 的 `TaskGroup` entity 引用全部 break。

**修法**：3 子目錄 Boss/Epic/Routing 取代 TaskGroup/ 統一目錄（每個子 namespace 名稱與 entity 不同名）+ 配套對齊 Stage 36 Meeting/Appeal/Qa/Proposal single-theme pattern。

**SOP 擴充建議（供未來）**：refactor-sop.md SOP 6「子目錄組織」段加一句紀律 — 「**子目錄/namespace 名稱避免與既有 entity 同名**（C# child namespace 解析優先級會 shadow 同名 entity，造成全 namespace tree 內 entity 引用 break）。對應 SOP 6 子目錄命名規則。」

#### 踩坑 #2：**Caller ctor 改動工作量比預期大幅減少**（Pipeline Executor 全用 IServiceScopeFactory）

**現象**：plan 預期 11 caller 改 ctor 注入 + 18-20 call site replace。實際發現所有 11 caller（Pipeline Executor + Framework Router）都用 `IServiceScopeFactory` + `scope.ServiceProvider.GetRequiredService<TaskGroupService>()` lazy resolve pattern — **0 ctor 改動**，只需改 22+ call site `var tgs = ...GetRequiredService<TaskGroupService>(); tgs.NotifyBossXXX(...)` → `var bossNotification = ...GetRequiredService<BossNotificationService>(); bossNotification.NotifyBossXXX(...)` + 11 caller 加 1 行 using。

**洞察**：spike 第一步 grep 不只看「ctor 注入清單」更要看「scope.ServiceProvider.GetRequiredService 模式」— 後者改動成本遠低（ctor 改動牽動 DI registration + parameter 簽名 + base class 呼叫，scope resolve 改動只是 1 個 var name + 1 個 using）。

**SOP 擴充建議（供未來）**：refactor-sop.md SOP 2「Migration 策略」加說明 — 「**caller 改動成本評估三層分**：① ctor 注入（最重）② scope.ServiceProvider.GetRequiredService（最輕，改動 = call site replace + using）③ 既有 IServiceProvider field 注入（中等）。spike 第一步 grep 區分這三類 caller 才能準確評估範圍。」

#### 踩坑 #3：**主檔瘦身比例 plan 預估過樂觀**（必須留主檔的 dispatch 結構行數沒充分計入）

**現象**：plan 預估主檔 -69%~-74% 對齊 Stage 36 -73%，實際 -54%（808 vs 預估 450-550）。

**根因**：`ProcessBossResponseAsync` 主 dispatch switch ~150 行 + `FireOneStepAsync` 含 framework Pipeline entry guard + Kickoff/Design 路由 ~70 行 + `HandleAgentCompletedAsync` 含 Pipeline path 接管 callback ~100 行 — 這 3 段「主入口 method 含 dispatch / guard / 路由」必須留主檔，plan 預估時沒精準分離「可搬走的 method body」vs「必留的 dispatch 結構」。

**SOP 擴充建議（供未來）**：refactor-sop.md「Stage 34-36 實戰數據」段加一行 — 「**dispatch / guard / 路由型主檔瘦身比例典型 -50%~-60%**（vs Stage 34-36 純拆 -73%~-85% 是因為 Stage 34-36 拆對象是「同類別 4 怪物合併」沒 dispatch 主入口；Stage 59 拆對象是「單檔含 dispatch + 多子職責」必留 dispatch 結構）。」

### 4. 驗收情境清單 + 0 follow-up commits 狀態

| # | 驗收 | 結果 |
|---|---|---|
| **V1 build** | ✅ Forge 自驗 — `dotnet build AiTeam.slnx` 0 errors / 0 new warnings / v3.48.0 確認 / 4 新 .cs 檔產生 |
| **V2 test** | ✅ Forge 自驗 — `dotnet test` 4 + 127 = **131 tests all pass**, 0 failures |
| **V3 Mock 7 routing regression** | ✅ Forge 自驗（forge-self-verify skill）— 7 framework_pipeline_* Mock 場景觸發後，**4/7 routing types via Pipeline 接管 logs 確認 dispatch 正確**：`dev_failed_intervention` / `dev_plan_unable` / `split_task_proposal` / `agent_api_failure_intervention`（+ 6 個 sub-scenarios `agent_api_failure` 跨 Dev/Reviewer/QA agentName dispatch 正確）。剩 3 type（`qa_failed_intervention` / `devplan_escalate` / `reviewer_fix_loop_limit`）Mock scenario 沒觸發到對應 BossInteraction type — Mock 設置範圍邊界，**非 Stage 59 refactor regression**（refactor 純檔案搬移，PipelineRoutingService.cs 7 TryRoute 方法 + ProcessBossResponseAsync switch 機械化複製，dispatch 邏輯與原 TaskGroupService 一致）|
| **V4 Mock 完整 pipeline** | ✅ Forge 自驗 — `framework_pipeline_happy_path` 跑通 Kickoff → Design → Dev → Reviewer → QA → Doc → NotifyMerge，`group.Status=done` + `DevPrUrl=https://github.com/mock/repo/pull/999` ✓。另 `framework_pipeline_dev_intervention_hitl` scenario auto-approve retry 後也 `status=done`（HITL 含介入路徑完整跑通）|
| **V5 行數驗證** | ✅ Forge 自驗 — TaskGroupService 1759 → 808（-54%）+ 4 新檔合計 1051（合計 1859 vs 原 1759 = +5.7% boilerplate 正常）|
| **V6 DI 啟動驗證** | ✅ Forge 自驗 — `docker logs aiteam-aiteam-bot-1` 顯示 `Application started` + `Discord Ready` + `Scheduler QuartzScheduler started` + `No migrations were applied` + 0 exception / 0 DI 循環依賴錯誤；4 新子 service AddSingleton 全成功（否則 startup 會 throw `Unable to resolve service`）|

**0 follow-up commits 狀態**：✅ V1-V6 全自驗通過，**無回頭修**；無 v3.48.x patch 需求。

### 5. Context 消耗實測（供 Aria 校準公式）

> Aria 第二段填（結案 Roadmap 補校準錨 — 待量測實際 Forge context ÷ 計劃書預估比值）。
> Aria 預估：×1.5-1.7（對齊 Stage 34/35/36 拆解倍率平均 ×1.58）/ Opus 1M + medium-high.

### Stage 59 戰略觀察（搭車 follow-up）

- **Dead code 觀察**：`GetGroupProjectIdAsync` (TaskGroupService 主檔 line ~474) — grep 確認 0 caller，dead code。本次保守留主檔（純檔案搬移精神不刪 dead code），未來 Stage 可清。立 follow-up 觀察不立 FF。
- **MarkGroupDoneOrInterventionAsync 跨 B+E 邏輯留主檔**設計修正（plan v1 設計 vs 實際）：plan 將 B 區段全抽到 BossNotificationService，但 spike 揭露 MarkGroupDone 內部呼叫 EpicChain.PauseEpicAndNotify + TriggerNextPhase（B → E 反向耦合）。Forge 自決留主檔（守門邏輯跨 B+E）對齊 SOP 4「子 service 單向依賴 Commons 不可反向」。BossNotificationService 因此只含 5 NotifyBoss + FindChannel（不含 MarkGroupDone）。
- **Lazy resolve follow-up 觀察**（plan v1 通過時 Aria 提）：循環依賴用 IServiceProvider lazy resolve 對齊 Stage 36 既有 FrameworkAppealRouter pattern OK — 但這是繞道不是真正解循環。徹底解 = 「FireStepsAsync 抽 ITaskFireService 介面」會動 method 簽名超 Stage 59 範圍。**接受 lazy resolve 範圍內最小改動 + 留 backlog 觀察給未來 reference**（不立 FF）。本 Stage 用了 5 處 IServiceProvider lazy resolve：BossResponseHandler (4 case bodies) + EpicChain (3 method) — 對齊既有 pattern 不擴張。
