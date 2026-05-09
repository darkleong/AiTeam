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
