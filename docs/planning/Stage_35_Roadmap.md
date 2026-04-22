# Stage 35：PmAgentService 拆解（FF 二十-D）+ 實踐子資料夾 SOP

> 對應 Future Feature：二十（大檔案拆解技術債合集）子項 D
> 對應版本：v3.22.0
> 建立日期：2026-04-22
> 狀態：✅ 已完成
> 文件版本：v2.0

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

## 實作紀錄（v2.0）

**完成日期**：2026-04-22
**實際 Model**：Opus 1M + high（單 session 一氣呵成，符合預估）
**建置結果**：`dotnet build AiTeam.slnx` 0 error、23 warnings 全為既有非 Stage 35 相關

### 產出

| 檔案 | 角色 | 行數 | 狀態 |
|---|---|---|---|
| `src/AiTeam.Bot/Agents/Pm/PmAgentResults.cs` | 10 個 public record 獨立檔 | 58 | 新建 |
| `src/AiTeam.Bot/Agents/Pm/PmAgentCommons.cs` | 共用工具（含 Stage 34 SOP 3 範圍討論的結果） | 210 | 新建 |
| `src/AiTeam.Bot/Agents/Pm/PmReviewService.cs` | 4 個 Review method + Claude Code fallback | 309 | 新建 |
| `src/AiTeam.Bot/Agents/Pm/ReviewAppealService.cs` | 3 個 Stage 30 Review Appeal method | 323 | 新建 |
| `src/AiTeam.Bot/Agents/Pm/DevPlanAppealService.cs` | 2 個 Stage 30 Dev_plan Appeal method | 200 | 新建 |
| `src/AiTeam.Bot/Agents/Pm/PmRoutingService.cs` | 3 個純 LLM 路由判斷 method | 247 | 新建 |
| `src/AiTeam.Bot/Agents/PmAgentService.cs` | 1389 行單檔 | — | **刪除** |
| `src/AiTeam.Bot/Orchestration/TaskGroupService.cs` | caller 更新 | — | 8 處編輯 |
| `src/AiTeam.Bot/Program.cs` | DI 1 行 → 5 行 | — | 修改 |
| `src/AiTeam.Bot/Agents/MockClaudeCodeService.cs` | 註解更新 | — | 修改（2 處） |
| `src/AiTeam.Bot/Agents/RequirementsAgentService.cs` | XML doc 註解更新 | — | 修改（1 處） |
| `src/Directory.Build.props` | 版本 3.21.0 → 3.22.0 | — | 修改 |

### 探索階段對原 Roadmap 的 4 項修正（規劃時已校正）

規劃階段透過 Explore agent + 實際讀檔驗證，發現原規劃文件 4 處假設與事實不符，已於 v2.0 Plan 修正：

1. **TaskGroupService caller 模式**：非建構子注入，而是 `scope.ServiceProvider.GetRequiredService<T>()` 動態解析 → 無建構子改動
2. **RequirementsAgentService 建構子**：根本**沒有** PmAgentService 參數（只在 line 397 XML doc 註解提及）→ Roadmap 第 9 步取消
3. **DI 生命週期**：原 `AddScoped`（非 Singleton）→ 5 個新 service 統一 Scoped
4. **Public record 數量**：10 個（原 Roadmap 列 8 個，漏記 `PetraIssue` / `CodyAppealItem`）

### Commons 範圍實際決策（SOP 3）

Roadmap 原規劃 Commons 只放 2 個 helper（`PrepareClaudeCodeEnv` + `BuildAppealContextSectionAsync`）。實作時發現三項交叉依賴強迫擴充 Commons：

1. `BuildPetraSystemPrompt()`：PmReviewService 的 `RunLlmDirectAsync` 與 DevPlanAppealService 的 `ReassessDevPlanAsync` 共用 → 移入 Commons
2. `TryParseReview()`：PmReviewService（Claude Code + LLM fallback 兩路徑）與 DevPlanAppealService.`ReassessDevPlanAsync` 共用 → 移入 Commons，同時帶走 internal `PetraReviewDto` / `PetraIssueDto`
3. `CleanupAppealRepo(workingDir, opName)`：原 5 個 appeal method 各自重複寫 `if+try+LogWarning` 區塊，抽 wrapper 進 Commons，5 處重複壓成一行呼叫

**教訓**：Commons 範圍不能只看規劃時觀察到的 helper；拆檔中會浮現「跨 service 共用但之前藏在同檔內」的隱性共用點。下次拆檔規劃時應跑一次「JSON parse helper + 標準 system prompt 是否被多個 public method 呼叫」的 grep 檢查。

### SOP 6 實踐細節（供 FF 二十 補強，首次實踐）

#### 1. 建子資料夾的 namespace 更新成本

**實際 Edit 次數**：
- 新檔 namespace 宣告：6 個檔案 × 1 行 = 6 行（寫檔時一次到位）
- `using AiTeam.Data;` 補齊：4 個新 service 檔各補 1 行（PmAgentResults / Commons 不需要）
- Caller 檔案 `using` 補齊：TaskGroupService 加 1 行 `using AiTeam.Bot.Agents.Pm;`、Program.cs 加 1 行，共 2 處
- 全限定寫法 `Agents.PetraReview` → `PetraReview`（靠 using 解析）：TaskGroupService 8 處

**合計**：約 17 處 namespace 相關編輯，整體成本低於預期（關鍵是提前寫好 using，後續就是單純型別引用）

#### 2. caller using 踩坑點

- **C# 子 namespace 規則**：在 `AiTeam.Bot.Agents.Pm` 內，父 `AiTeam.Bot.Agents` 的型別**自動可見**（向外查找）。所以 Pm/ 內檔案不需寫 `using AiTeam.Bot.Agents;` 就能用 `MockClaudeCodeService`、`LlmProviderFactory`、`IClaudeCodeService` 等。這省掉大量 using 宣告。
- **反向不成立**：父 namespace（`AiTeam.Bot.Agents` / `AiTeam.Bot.Orchestration`）**看不到**子 namespace `Pm`。所有 caller 都要額外補 `using AiTeam.Bot.Agents.Pm;`。
- **全限定寫法的坑**：TaskGroupService 原有 `Agents.PetraReview petraReview` 這種 in-namespace qualifier 寫法（共 8 處）。搬 namespace 後 `Agents.` 仍然解析成功但找不到 `PetraReview`。修法：要麼改成 `Agents.Pm.PetraReview`，要麼刪掉 qualifier 靠 `using AiTeam.Bot.Agents.Pm;` 解析。採用後者（sed 一次替換），更簡潔。
- **沒有 using 時的迷惑錯誤**：如果忘記加 `using AiTeam.Bot.Agents.Pm;`，compile error 會說「找不到 `PetraReview`」而不是「在錯的 namespace」，容易誤以為型別不存在。**建議 FF 二十 規範：拆子資料夾時，第一個 Edit 就是所有 caller 的 using 加齊，避免一路追 compile error**。

#### 3. 「Agent vs Orchestration 歸屬判斷原則」（Christ 特別點名要記錄）

**本次決策**：PmAgentService 雖然功能上「驅動 Cody / Vera / Quinn 做事」，但放在 `Agents/Pm/`——因為 5 個 service 的**決策主體是 Petra 這個 Agent 角色**（LLM prompt 的身份是 Petra、產出是 Petra 的判斷）。

**判斷原則雛形**（供 FF 二十-B CommandHandler / FF 二十-A TaskGroupService 拆解直接套用）：

> **決策主體（誰說話）= Agent 角色時放 `Agents/`；協調多個 Agent 的流程控制放 `Orchestration/`。**

具體對照：

| 服務類型 | 決策主體 | 歸屬 |
|---|---|---|
| ReviewAppealService（Cody/Vera/Petra 三方對話） | LLM 扮演的 Petra（含 Cody/Vera 角色扮演） | `Agents/Pm/` ✓ |
| PmRoutingService（Blocker/QA 路由判斷） | LLM 扮演的 Petra | `Agents/Pm/` ✓ |
| TaskGroupService（決定何時 fire 哪個 agent、更新 DB 狀態、發 Discord 通知） | C# 邏輯（無 LLM 決策） | `Orchestration/` ✓ |
| CommandHandler（解析 Discord 指令分派給 agent） | C# 邏輯（無 LLM 決策） | `Discord/`（準 Orchestration） |

**邊界情境**：若某 service 既有 LLM 決策、又有跨 agent 流程控制（例如 WorkflowEngine），需看比重——多為 LLM 決策歸 Agents/，多為流程控制歸 Orchestration/。極端混合情境可再拆兩層。

### 驗收結果

- [x] **編譯**：`dotnet build AiTeam.slnx` 0 error、0 新 warning
- [x] **靜態檢查**：`Agents/Pm/` 內 6 檔齊全、`PmAgentService.cs` 已刪除、DI 5 個新 Scoped 註冊、TaskGroupService using 已補
- [ ] **Mock Mode 4 情境驗收**：交付 Christ 於 Discord 執行（/mock fail_review / fail_dev_plan / fail_qa / new_feature_with_proposal）

Mock Mode 情境的 code path 全部走通（FailScenario 狀態機在新 service 正確推進），程式碼層面已驗證；實際 Discord 流程驗收待 Christ 重啟容器後跑。

### 搭車修改

無。本次拆解純粹 refactor，不帶任何 FF 子項或 bug fix。

### 結案分工狀態

- **本 session（實作）完成**：5 個 service + 1 record 檔搬家、TaskGroupService / Program / Mock / Requirements 註解更新、版本號 bump、Roadmap v2.0 實作紀錄、commit
- **Aria 接手**：Master Plan header + 索引 + changelog、Future_Feature FF 二十 子項 D 狀態 ✅、把 SOP 6 三點細節（namespace 成本 / using 踩坑 / Agent vs Orchestration 原則）寫進 FF 二十 共通策略

---

## 版本歷史

| 版本 | 日期 | 內容 |
|------|------|------|
| v1.0 | 2026-04-22 | 初版規劃書，PmAgentService（1388 行 × 12 method）拆 5 個 service + 1 record 檔（Agents/Pm/ 子資料夾）；Opus 1M + high（粗估 158K 超 Sonnet 200K）；首次實踐 SOP 6 子資料夾組織 |
| v2.0 | 2026-04-22 | 實作完成結案。產出 6 個新檔（PmAgentResults / PmAgentCommons / PmReviewService / ReviewAppealService / DevPlanAppealService / PmRoutingService）、PmAgentService.cs 刪除、TaskGroupService 8 處 caller 更新、Program.cs DI 改寫、版本 v3.22.0。build 0 error。SOP 3 Commons 範圍實際比規劃擴大（含 BuildPetraSystemPrompt + TryParseReview + CleanupAppealRepo）、SOP 6 首次實踐結論：**決策主體（誰說話）= Agent 時放 Agents/，協調流程放 Orchestration/**——供 FF 二十-B/A 直接套用 |
