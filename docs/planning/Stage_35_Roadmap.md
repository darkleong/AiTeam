# Stage 35：PmAgentService 拆解（FF 二十-D）+ 實踐子資料夾 SOP

> 對應 Future Feature：二十（大檔案拆解技術債合集）子項 D
> 對應版本：v3.22.0
> 建立日期：2026-04-22
> 狀態：🟡 待實作
> 文件版本：v1.0

---

## 概述

**主題**：把 `PmAgentService.cs`（1388 行、12 個 public method）拆成 5 個職責清楚的 service，沿用 Stage 34 的「六項拆解 SOP」，並首次實踐 **SOP 6（子資料夾組織）**—— `Agents/` 目前 20 個檔案已超閾值，建 `Agents/Pm/` 子資料夾收納。

**為什麼現在做 D**：
- Stage 30 剛把 5 個 appeal 方法升級為 Claude Code CLI 讓 PmAgentService 膨脹 50%（~900 → 1388 行），**脈絡還在記憶裡拆最有效率**
- Stage 34 已累積六項拆解 SOP，這次直接套用，實作風險可控
- 同時實踐 SOP 6：`Agents/` 20 檔案 → 建 `Agents/Pm/` 子資料夾，驗證「子資料夾 + namespace 隨移」流程

---

## 現況分析

### PmAgentService 的 12 個 Public Method

| Method | 來源 Stage | 歸屬新 service |
|---|---|---|
| `ReviewRosaAsync`（line 44）| Stage 25a/25b | `PmReviewService` |
| `ReviewDemiAsync`（line 63）| Stage 25a/25b | `PmReviewService` |
| `ReviewDevPlanAsync`（line 83）| Stage 24 | `PmReviewService` |
| `ReviewVeraAsync`（line 114）| Stage 23 | `PmReviewService` |
| `AssessBlockerAsync`（line 499）| Stage 23 | `PmRoutingService` |
| `RunCodyAppealAsync`（line 585）| Stage 30 | `ReviewAppealService` |
| `RunVeraAppealAsync`（line 651）| Stage 30 | `ReviewAppealService` |
| `ArbitrateReviewAppealAsync`（line 711）| Stage 30 | `ReviewAppealService` |
| `AssessQaFailureAsync`（line 952）| Stage 24 | `PmRoutingService` |
| `AssessNoApplicableTestsAsync`（line 987）| Stage 24 | `PmRoutingService` |
| `RunCodyDevPlanAppealAsync`（line 1113）| Stage 30 | `DevPlanAppealService` |
| `ReassessDevPlanAsync`（line 1172）| Stage 30 | `DevPlanAppealService` |

### Private Helper（Stage 30 加的）

- `PrepareClaudeCodeEnv`：準備 Claude Code CLI 工作環境（5 個 appeal 方法共用）
- `BuildAppealContextSectionAsync`：組合 appeal prompt 的 context 段落（帶 TaskPlan / DesignPlan / DevPlan / ImplementationNote / PR diff）

這兩個是 Stage 30 明確抽出的 helper，**歸 `PmAgentCommons`**。

### Callers（3 處，不包含 DI）

1. **`TaskGroupService`**（主 caller）：12 個 method 幾乎都呼叫，分散在多個迴圈方法
2. **`MockClaudeCodeService`**（grep match，需確認實際用途——可能是 mock 某個 PmAgentService 方法的模擬輸出，非直接 `pmService.` 呼叫）
3. **`RequirementsAgentService`**：只有建構子依賴（line 17），**無實際呼叫**（grep match 是 constructor 參數列出）

### Record / DTO

- **Public record / class**（TaskGroupService 會用）：`PetraReview`, `BlockerDecision`, `CodyAppeal`, `VeraAppealResponse`, `AppealArbitration`, `QaFailureDecision`, `QaNoTestDecision`, `CodyDevPlanAppeal`
- **Internal 實作細節**（JSON parse 用）：定義在 line 1309+ 的內部 class，含 `Decision` / `Summary` / `RevisionInstructions` / `Severity` / `Description` 等欄位

---

## 拆解設計

### 五個新 Service + 一個 Record 檔

所有新檔放於 **`src/AiTeam.Bot/Agents/Pm/`**（子資料夾），namespace 改為 `AiTeam.Bot.Agents.Pm`。

#### 1. `PmAgentCommons`（~200 行）

**位置**：`src/AiTeam.Bot/Agents/Pm/PmAgentCommons.cs`

**API**：
```csharp
internal Task<(string workingDir, string model, string apiKey)> PrepareClaudeCodeEnvAsync(...)
internal Task<string> BuildAppealContextSectionAsync(TaskGroup group, CancellationToken ct)
```

**依賴**：`GitHubService`, `IOptions<GitHubSettings>`, `IConfiguration`, `ILogger<PmAgentCommons>`

#### 2. `PmReviewService`（~350 行）

**位置**：`src/AiTeam.Bot/Agents/Pm/PmReviewService.cs`

**Public API**（4 個）：
- `ReviewRosaAsync` / `ReviewDemiAsync` / `ReviewDevPlanAsync` / `ReviewVeraAsync`

**依賴**：`IClaudeCodeService`（若有用）/ `LlmProviderFactory`（純 API 呼叫）/ `ILogger`

#### 3. `ReviewAppealService`（~400 行）

**位置**：`src/AiTeam.Bot/Agents/Pm/ReviewAppealService.cs`

**Public API**（3 個，Stage 30 升級）：
- `RunCodyAppealAsync` / `RunVeraAppealAsync` / `ArbitrateReviewAppealAsync`

**依賴**：`IClaudeCodeService`（用 `RunMeetingSessionAsync`）+ **`PmAgentCommons`**（PrepareClaudeCodeEnv + BuildAppealContextSectionAsync）

#### 4. `DevPlanAppealService`（~250 行）

**位置**：`src/AiTeam.Bot/Agents/Pm/DevPlanAppealService.cs`

**Public API**（2 個，Stage 30 升級）：
- `RunCodyDevPlanAppealAsync` / `ReassessDevPlanAsync`

**依賴**：`IClaudeCodeService` + **`PmAgentCommons`**

#### 5. `PmRoutingService`（~200 行）

**位置**：`src/AiTeam.Bot/Agents/Pm/PmRoutingService.cs`

**Public API**（3 個，純 API 純文字判斷）：
- `AssessBlockerAsync` / `AssessQaFailureAsync` / `AssessNoApplicableTestsAsync`

**依賴**：`LlmProviderFactory` / `ILogger`（可能不需要 Claude Code CLI，都是純文字分析）

#### 6. `PmAgentResults.cs`（Record 獨立檔，~50 行）

**位置**：`src/AiTeam.Bot/Agents/Pm/PmAgentResults.cs`

搬 public record / class：
- `PetraReview` / `BlockerDecision` / `CodyAppeal` / `VeraAppealResponse` / `AppealArbitration` / `QaFailureDecision` / `QaNoTestDecision` / `CodyDevPlanAppeal`

Internal 的 JSON parse DTO 各自留在對應 service 檔。

---

## SOP 套用對照（Stage 34 六項）

| SOP | 本次實踐 |
|---|---|
| 1. Record 組織 | Public record 搬 `PmAgentResults.cs`；internal parse DTO 各自留 service |
| 2. Migration 策略 | Caller 主要是 TaskGroupService（< 15 處），直接切換不做 thin wrapper |
| 3. Commons 範圍 | 只放「多個 service 都用」的（`PrepareClaudeCodeEnv` + `BuildAppealContextSectionAsync`）；`GetApiKey` / `GetModel` 若各 service 都用可考慮放 Commons（但若只 2-3 service 用，保留各自為準）|
| 4. DI 順序 | `PmAgentCommons` 先，再 `PmReviewService` / `ReviewAppealService` / `DevPlanAppealService` / `PmRoutingService` |
| 5. Session state | PmAgentService 無 singleton-level state（appeal 方法用 local session ID），乾淨拆分 |
| 6. **檔案夾組織**（首次實踐） | 建 `Agents/Pm/`，namespace 改 `AiTeam.Bot.Agents.Pm`；所有 caller 的 `using AiTeam.Bot.Agents` 要補 `.Pm` |

---

## Migration 策略：直接切換（沿用 SOP 2）

**步驟**：
1. 建 `Agents/Pm/` 資料夾 + `PmAgentResults.cs`（只有 record）
2. 建 `PmAgentCommons.cs`（先 stable，其他 service 才能依賴）
3. 建 `PmReviewService.cs`（4 method 照搬）
4. 建 `ReviewAppealService.cs`（3 method，Stage 30 升級的）
5. 建 `DevPlanAppealService.cs`（2 method，Stage 30 升級的）
6. 建 `PmRoutingService.cs`（3 method）
7. 改 `TaskGroupService` caller：建構子換 5 個新依賴、call site 逐一指向對應 service
8. 改 `Program.cs` DI：移除 `AddSingleton<PmAgentService>`，新增 5 個 service
9. 改 `RequirementsAgentService` 建構子：若只是依賴但未呼叫，**直接移除 `PmAgentService` 參數**（確認是否真無呼叫）
10. **刪除 `PmAgentService.cs`**
11. `dotnet build` 確認 0 error
12. 補 Roadmap 實作紀錄

**命名空間更新**：
- 所有使用 `PetraReview` 等 public record 的檔案，`using AiTeam.Bot.Agents` 要改加 `using AiTeam.Bot.Agents.Pm`
- 若 TaskGroupService 之類的檔案已 `using AiTeam.Bot.Agents`，補一行 `using AiTeam.Bot.Agents.Pm;` 即可（兩個 namespace 可並存）

---

## 驗收情境

Mock Mode 開啟，必須全跑：

1. **`/mock fail_review`** — 走完整 Review Appeal 流程
   - Cody 反駁（`ReviewAppealService.RunCodyAppealAsync`）
   - Vera 重評（`ReviewAppealService.RunVeraAppealAsync`）
   - Petra 仲裁（`ReviewAppealService.ArbitrateReviewAppealAsync`）
   - 確認 3 個 method 全走 Claude Code CLI（非 LLM API fallback）

2. **`/mock fail_dev_plan`** — 走完整 Dev_plan Appeal 流程
   - Cody 反駁（`DevPlanAppealService.RunCodyDevPlanAppealAsync`）
   - Petra 再評估（`DevPlanAppealService.ReassessDevPlanAsync`）

3. **`/mock fail_qa`** — 走 QA 路由
   - Petra 判斷（`PmRoutingService.AssessQaFailureAsync`）
   - 後續路由依結果走（qa_fix / retest / escalate / dev_revision）

4. **`/mock new_feature_with_proposal`** — 走正向流程，驗 4 個 Review method
   - Rosa 產出 Issues → `PmReviewService.ReviewRosaAsync`
   - Demi 產出 UI 規格 → `PmReviewService.ReviewDemiAsync`
   - Cody 產出 Dev_plan → `PmReviewService.ReviewDevPlanAsync`
   - Vera 產出 Review → `PmReviewService.ReviewVeraAsync`

### 負面驗證

- `dotnet build AiTeam.slnx` 0 error（namespace 改動容易漏 using）
- `PmAgentService.cs` 確實刪除
- `Agents/Pm/` 內 6 個檔案齊全（5 service + 1 record）
- Program.cs DI 正確註冊 5 個 Singleton（順序：Commons 先）
- `RequirementsAgentService` 若真的沒用 PmAgentService，建構子應已移除該參數

---

## 版本

`v3.21.0 → v3.22.0`（minor bump）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議

**推薦：Opus 1M + high**（Christ 2026-04-22 確認）

### Context 預估（按 Stage 34 校準後的 × 1.6 公式）

| 項目 | tokens |
|---|---|
| PmAgentService Read 一次（1388 行）| ~28K |
| TaskGroupService 多處 local Read（caller 多）| ~30K |
| 新寫 5 個 service + 1 record 檔（~1400 行 Write）| ~35K |
| 驗收 buffer（4 個 Mock 情境要跑）| +30K |
| 開場 CLAUDE.md / conventions | +15K |
| Grep / Build / Edit 緩衝 | +20K |
| **粗估** | **~158K** |
| **實際預期（× 1.6）** | **~250K** |

**結論：Sonnet 200K 會爆**，**必須 Opus 1M 或拆兩 Session**。選 Opus 1M 一氣呵成（5 個 service 邏輯耦合，契約一致性勝過省錢）。

### 為何 high effort

- 首次實踐 SOP 6 子資料夾（namespace 跨檔改動 + caller using 補齊）
- 5 個 service 的依賴關係需要跨檔推理
- DI 依賴順序 + callers 改動有陷阱（RequirementsAgentService 建構子是否真沒用？要驗證）

---

## 設計約定

- **檔案夾**：`Agents/Pm/` 子資料夾，namespace `AiTeam.Bot.Agents.Pm`
- **不保留 thin wrapper**（沿用 SOP 2）
- **Record 搬獨立檔**（`PmAgentResults.cs`）
- **DI 生命週期**：5 個新 service 都是 Singleton（對齊舊 PmAgentService）
- **循環依賴**：Review/Appeal/DevPlanAppeal/Routing → Commons（單向）

---

## 預期產出（給 FF 二十 補強 SOP）

Stage 35 完成後，Roadmap 實作紀錄要補記錄 SOP 6 實踐細節，補回 FF 二十：

1. **建子資料夾時的 namespace 更新成本**（實際花多少 Edit？）
2. **caller 的 `using` 補齊踩坑**（容易漏哪些檔案？）
3. **「Agent service 要歸 Agents/ 還是 Orchestration/」的判斷原則**（PmAgentService 拆成 Service 類型，但放 Agents/ 因為是「Petra 的邏輯」——這算 Agent-specific 還是 Orchestration？）

---

## 結案檢查清單（兩段式分工）

- **實作 Session 做**：Stage_35_Roadmap 補「實作紀錄」章節 + 狀態 ✅ + 文件版本 v2.0 + 版本歷史 + commit；記錄 SOP 6 實踐細節（補回 FF 二十）
- **Aria 做**：Master Plan header + 索引 ✅ + changelog；Future_Feature 更新 FF 二十 子項 D 狀態為 ✅；把 SOP 6 實踐細節補進 FF 二十 共通策略；掃 git log 確認驗收期間 commits 補進 Roadmap

---

## 版本歷史

| 版本 | 日期 | 內容 |
|------|------|------|
| v1.0 | 2026-04-22 | 初版規劃書，PmAgentService（1388 行 × 12 method）拆 5 個 service + 1 record 檔（Agents/Pm/ 子資料夾）；Opus 1M + high（粗估 158K 超 Sonnet 200K）；首次實踐 SOP 6 子資料夾組織 |
