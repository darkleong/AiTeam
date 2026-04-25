# Stage 39：Vera 審查範圍擴及 .razor / .css + Trial_v2 搭車修三項

> 對應 Future Feature：FF 二十八（主菜）+ Trial_v2 搭車修
> 對應版本：v3.26.0
> 建立日期：2026-04-25
> 狀態：🟡 待實作
> 文件版本：v1.0

---

## 概述

**主菜**：FF 二十八 — Vera 審查範圍擴及 `.razor` / `.css`，對齊 Quinn 已有的 `hasUiChanges` 邏輯。**這是 Self-implement 試驗 v3 的前置條件**——若 Vera 仍只審 `.cs`，純 UI 任務的 Top 1 / Top 2 驗證永遠跑不到。

**搭車修三項**（Trial_v2 試驗發現，[詳細紀錄](../experiments/Trial_v2_RuleManagementUI.md)）：
1. **BossInteraction.Description 缺 Task.Description** — Stage 28a/29-5 設計疏忽，Dashboard UX 不平等
2. **Reviewer 略過時狀態應標 `skipped` 不是 `failed`** — UI 顯示誤導 + 重試按鈕無效
3. **MudSwitch a11y 補 `aria-label`** — PR #108 範圍內的隱患（Vera 該抓但因主菜 bug 略過）

**戰略意義**：這個 Stage 解鎖 self-implement 試驗 v3，順便還清 Trial_v2 觀察累積的三個技術債。

---

## 第一部分（主菜）：FF 二十八 — Vera 審查範圍擴及 .razor / .css

### 現況

[`ReviewerAgentService.cs:108-109`](../../src/AiTeam.Bot/Agents/ReviewerAgentService.cs:108)：
```csharp
if (csFiles.Count == 0)
    return Fail(task, $"PR #{prNumber} 未包含 .cs 檔案，略過 Reviewer");
```

對比 [`QaAgentService.cs:111`](../../src/AiTeam.Bot/Agents/QaAgentService.cs:111)（Quinn 已對齊）：
```csharp
if (!hasUiChanges && csFiles.Count == 0)
    return new AgentExecutionResult(false,
        $"PR #{prNumber} 未包含可測試的 .cs / .razor / .css 檔案，略過 QA");
```

### 實作項目

#### 1. `ReviewerAgentService.cs` 擴充檔案分類

對齊 Quinn 邏輯，提取 `.cs` / `.razor` / `.css` 三類檔案：
- `csFiles`（既有）
- `razorFiles`（新增）
- `cssFiles`（新增）
- `hasUiChanges = razorFiles.Count > 0 || cssFiles.Count > 0`

**新略過條件**：`!hasUiChanges && csFiles.Count == 0`（純文檔/設定 PR 才略過）

#### 2. `BuildReviewPrompt` 擴充

把 `razor` / `css` 變更也加入 review prompt（沿用既有 `## PR 變更（僅 .cs 檔案的 diff）` 段落結構，新增 razor / css 區塊）。

注意現有 prompt 標題：「## PR 變更（僅 .cs 檔案的 diff）」要改名（例如「## PR 變更 diff」），因為不再僅限 .cs。

#### 3. `CLAUDE_Vera.md` 擴充 razor / css / a11y 判準

Vera 接到 razor / css 改動時的審查標準（**這是核心**——光改 service 邏輯沒用，Vera 拿不到判準仍會放行）：

##### 新增 razor / a11y Critical 判準

- **a11y 缺失**列為 **Warning**（不到 Critical 因為不會崩潰）：
  - `<button>` / `MudButton` 沒有可見文字 → 缺 `aria-label`
  - `MudSwitch` / `MudCheckBox` 移除 `Label` 但沒補 `aria-label`
  - `<img>` 缺 `alt` 屬性
  - icon-only 互動元素（`MudIconButton`）缺 Tooltip 或 aria-label

- **Blazor 例外處理**列為 **Critical**（會崩潰）：
  - `@onclick` handler 內未處理可能拋例外的呼叫（如 `await DeleteAsync(...)` 沒 try-catch）
  - `@bind-Value` 拼錯欄位名（編譯錯誤但若 dynamic 綁定 runtime 才炸）

- **Blazor Server Circuit 隔離**列為 **Warning**：
  - 共用 service 注入時忘記考慮 Circuit 範圍

##### 新增 css 判準

- **`!important` 濫用**列為 **Warning**：超過 1-2 個 `!important` → 設計問題
- **寫死顏色不支援 dark mode**列為 **Warning**：`color: #fff` 而非 `var(--mud-palette-text-primary)`

##### 新增 MudBlazor 慣例判準

- 相同類型按鈕應一致（同表格內所有操作按鈕都是 MudButton 或都是 MudIconButton，不混用）
- IconButton 必須有 Tooltip 或 aria-label（icon 沒文字 → 螢幕閱讀器不知道是什麼）

### 細節：CLAUDE_Vera.md 寫作風格

維持既有「**寧可漏報一個 warning，也不可誤報一個 critical**」「**偏好放行**」哲學——**a11y / Blazor 細節都列為 Warning 而非 Critical**（除非真的 runtime 崩潰）。這是刻意保守，避免 v3 試驗時 Vera 過度閉門。

---

## 第二部分（搭車修）：BossInteraction.Description 補 Task.Description

### 位置

[`CommandHandler.cs:195`](../../src/AiTeam.Bot/Discord/CommandHandler.cs:195)：

```csharp
// 前
description: ceoResponse.Reply ?? userInput,

// 後
description: string.IsNullOrWhiteSpace(ceoResponse.Task?.Description)
    ? (ceoResponse.Reply ?? userInput)
    : $"{ceoResponse.Reply}\n\n---\n\n{ceoResponse.Task.Description}",
```

### 影響範圍

- L195（Dashboard 路徑）— 必修
- 檢查 L444（Discord 路徑）— `ProcessBossResponseAsync` 中是否有對應位置（grep 確認）

### 風險

- 既有 BossInteraction.Description 為短摘要設計，可能被 Dashboard 顯示某 column 截斷
- 需驗證 Dashboard 操作中心的 description 顯示是否能容納長 markdown（Task.Description 含 code snippet 可能超過 1000 字）

---

## 第三部分（搭車修）：Reviewer 略過狀態應為 `skipped` 不是 `failed`

### 現況

[`ReviewerAgentService.cs:108-109`](../../src/AiTeam.Bot/Agents/ReviewerAgentService.cs:108) 用 `Fail(task, reason)` → `AgentExecutionResult(success: false, ...)` → `AgentQueueProcessor` 標 `task.Status = "failed"`。

Dashboard 顯示「失敗 + 重試按鈕」，但**重試也救不了**（再跑還是略過），是 UI 誤導。

### 設計方向（實作 Session 決定細節）

兩種策略選一：

**策略 A：新增 `AgentExecutionResult.Skipped(reason)` 工廠方法**
- 在 `AgentQueueProcessor.ExecuteTaskAsync` 處理 result 時，若是 Skipped → `task.Status = "skipped"`（綠色，非紅）
- 流程繼續走下一步（不阻擋 group）
- 影響：`AgentExecutionResult` 結構可能要加 `bool Skipped` 欄位

**策略 B：用既有 success: true + 特殊 reason marker**
- `Fail()` 改為 `Skip()`，回傳 `AgentExecutionResult(success: true, summary: "skipped: ...")`
- Processor 看到「skipped」prefix → 顯示 skipped status
- 影響：較小但語意混淆（success=true 表示 skipped 不夠清楚）

**Aria 推薦策略 A**（語意清楚，符合 Stage 27a 既有狀態枚舉風格）。

### 影響範圍

- `ReviewerAgentService.cs` Skip 邏輯
- `AgentExecutionResult` 結構（如選 A）
- `AgentQueueProcessor.ExecuteTaskAsync` 結果處理
- Dashboard `StatusBadge` / Pipeline View 對 `skipped` 狀態的顯示（綠色 chip + skip icon）

### 順便評估

QA 也有相同「略過」邏輯（[`QaAgentService.cs:111`](../../src/AiTeam.Bot/Agents/QaAgentService.cs:111)），實作策略 A 時**順便讓 QA 也用同一個 `Skipped` API**（一致性）。

---

## 第四部分（搭車修）：MudSwitch a11y aria-label

### 位置

[`RuleManagement.razor` L58-64](../../src/AiTeam.Dashboard/Components/Pages/Rules/RuleManagement.razor)（Stage 38 後 PR #108 改動的位置）：

```razor
// 現況（PR #108 移除 Label 後）
<MudSwitch T="bool"
           Value="@context.IsActive"
           ValueChanged="@((bool v) => ToggleActiveAsync(context, v))"
           Color="Color.Primary" />

// 修後
<MudSwitch T="bool"
           Value="@context.IsActive"
           ValueChanged="@((bool v) => ToggleActiveAsync(context, v))"
           Color="Color.Primary"
           aria-label="@(context.IsActive ? "已啟用" : "已停用，點擊啟用")" />
```

### 影響範圍

- 此處目前只有規則管理頁。若**專案管理頁**或其他頁面也有類似 MudSwitch + 移除 Label 的組合，順便檢查（grep `<MudSwitch` 但無 Label 屬性）。
- 如果 Stage 38 之前其他 PR 也踩過此 a11y 隱患，順便修。

---

## 驗收情境

### A. Vera 審查 .razor PR（核心驗證 FF 二十八）

**Mock Mode**：
1. `/mock` 觸發包含純 `.razor` 改動的 PR
2. 確認 Vera 不再略過、進入 review session
3. Mock 設計讓 Vera 回 dummy review report → 確認 review report 正確被 parse + Petra 接到

**真實流程**（需 Christ 驗收，FF 十的另一個小子項適合）：
- 任務動 razor → Vera 跑 review → 至少抓 1 個 a11y 或 Blazor 議題標 Warning

### B. Vera 抓 a11y 隱患

- 故意製造一個含 a11y 缺陷的 razor PR（如 `MudIconButton` 沒 Tooltip）
- 觸發 Vera review → review report 含此 issue（標 Warning）

### C. Dashboard 操作中心顯示完整 Task.Description

- `/mock new_feature_with_proposal` 或真實 CEO 決策觸發
- Dashboard 操作中心 → 「待處理」CEO 決策卡 → description 顯示 Reply + Task.Description（含 code snippet）

### D. Reviewer 略過時顯示 skipped

- `/mock` 觸發純 `.css` PR（沒 .razor / .cs）— 若 css 改動仍應審，改測純 `.md` 文件 PR
- Vera 略過 → 流程詳情顯示**綠色 skipped chip**（非紅色 failed）+ 無重試按鈕
- 任務繼續走 QA（QA 也略過 → skipped）+ 直到 done

### E. MudSwitch a11y

- 用 Playwright `accessibility audit` 跑 `/rules` 頁面
- MudSwitch 應有 `aria-label` 屬性
- 截圖 light + dark mode 確認視覺不變

---

## 技術約束 & 注意事項

1. **`ReviewerAgentService` 的 Mock 模擬**：[`MockClaudeCodeService`](../../src/AiTeam.Bot/Services/MockClaudeCodeService.cs) 對 Reviewer 的回應目前可能假設 .cs 才會走 review。需檢查 Mock 是否要對應修改（讓 razor PR 觸發 mock review report）。
2. **`AgentExecutionResult` 結構變更**：若採策略 A 加 `Skipped` 欄位/工廠方法，所有 caller 要檢查；既有 record `with` 表達式要相容。
3. **Status 枚舉擴充**：`task.Status = "skipped"` 是新狀態，Dashboard `StatusBadge` 元件要 mapping 到綠色（不是紅色 failed、不是黃色 running）。
4. **CLAUDE_Vera.md 改動 = 行為改動**：CLAUDE_Vera.md 修改後，Bot 容器啟動 seed 邏輯不會自動更新（FF 二十四 fix 的是 csproj COPY，不是運行時更新）。**驗收前需重啟 Bot 容器**讓新 template 生效。
5. **PR review 提示長度**：razor / css diff 加進 review prompt 後，總長度可能變大。Sonnet 4.6 200K 大概率沒問題但需注意。
6. **既有 Stage 37/38 的 `.razor` 改動 PR 不溯及既往**：本 Stage 修完只影響後續 PR；歷史 razor PR 不重審。

---

## 版本

`v3.25.0 → v3.26.0`（minor bump）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議

**推薦：Sonnet 200K + high**

四維度評估：

| 維度 | 評估 |
|------|------|
| **Context 量** | 中（ReviewerAgentService + Mock + CLAUDE_Vera.md + Processor + razor + Dashboard StatusBadge）|
| **邏輯複雜度** | 中（hasUiChanges 對齊 + skipped 狀態設計 + CLAUDE_Vera.md 擴寫）|
| **風險代價** | 中（動 ReviewerAgentService 核心 + 加新狀態枚舉）|
| **範本可用度** | 高（QaAgentService 已有 hasUiChanges 對照、CLAUDE_QA.md 已有 razor 部分可參考）|

**Context 粗估**：~70-90K × 1.6 = ~115-145K（Sonnet 200K 邊界內，但 high effort 處理 CLAUDE_Vera.md 改寫品質要求）

**選 Sonnet 200K + high 理由**：
- 主菜 + 三搭車修的範本可用度高（Quinn 已有 hasUiChanges 邏輯可抄）
- CLAUDE_Vera.md 改寫是核心，需要 high effort 思考新判準的精確度
- Opus 1M 可選但 Sonnet 200K 應該夠

**替代方案**：Opus 1M + medium（若擔心 CLAUDE_Vera.md 寫得不夠精準，Opus 1M 推理品質更穩）

---

## 不在範圍

- ❌ a11y 自動掃描工具整合（如 axe-core）— 用 prompt 指引讓 Vera 用 Read/Grep 探索就夠
- ❌ Visual regression（截圖差異比對）— Quinn 已用 Playwright 截圖負責
- ❌ Vera prompt 「偏好放行」哲學調整（FF 二十七 Top 2 等試驗 v3 結果再決定）
- ❌ Petra 升級為「設計 PM」— 需要更大架構討論
- ❌ 其他頁面的 a11y 全面審查（只修 PR #108 範圍的 MudSwitch；其他搭車到下次 UI 打磨）

---

## 後續關聯

- **Trial_v3 規劃**（FF 二十七）：本 Stage 完成後即可規劃 v3 任務（會動 .cs 的小工程，補 Top 1 / Top 2 驗證）
- **FF 十一 Token 守門限額**：與本 Stage 無關但仍是下一波選項
- **FF 二十二 Agent 命名一致性**：Stage 39 不處理，留作獨立 Stage 或下個 SignalR refactor 搭車

---

## 版本歷史

| 版本 | 日期 | 變更 |
|------|------|------|
| v1.0 | 2026-04-25 | 計劃書建立（Aria）— FF 二十八 主菜 + Trial_v2 搭車修三項 |
| v1.1 | 2026-04-25 | 實作完成 — 實作紀錄章節新增（待 Aria 結案第二段：版本 bump v3.26.0 + Master Plan / Future_Feature 整理） |

---

## 實作紀錄

> 完成日期：2026-04-25
> 模型：Sonnet 4.6 200K（high effort）
> Build：`dotnet build AiTeam.slnx` → 0 Errors / 57 Warnings（皆為既有，含 NU1902 / MSTEST0037 / MUD0002 PipelineView Color 屬性）

### 實作摘要

**Phase A — Skipped 結果型別（基礎建設）**

- [`IAgentExecutor.cs`](../../src/AiTeam.Bot/Agents/IAgentExecutor.cs)：新增 `AgentResultType { Normal, Skipped }` enum + `AgentExecutionResult.Skipped(reason)` 工廠方法。`ResultType` 加在 record 尾端帶 default，所有既有 `with` 表達式相容。Skipped 結果一律 `Success=true`（讓流程繼續、Agent 狀態走 idle）。
- [`AgentQueueProcessor.cs:204-228`](../../src/AiTeam.Bot/Orchestration/AgentQueueProcessor.cs:204)：`finalStatus` 改三分支（skipped / done / failed）。Discord embed 標題與顏色順手改三分支（Skipped 走 ⏭️ + Color.Teal）。
- Dashboard 全鏈路 mapping `skipped`：[`StatusBadge.razor`](../../src/AiTeam.Dashboard/Components/Shared/StatusBadge.razor)（"略過"）、[`app.css`](../../src/AiTeam.Dashboard/wwwroot/css/app.css)（`--color-status-skipped: #20c997`）、[`PipelineView.razor.cs`](../../src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineView.razor.cs)（終態判定 + `IsCompleted` + `GetLogColor` Tertiary）、[`PipelineList.razor.cs`](../../src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineList.razor.cs)（SignalR 終態刷新）、[`TaskCenter.razor`](../../src/AiTeam.Dashboard/Components/Pages/Tasks/TaskCenter.razor) + [`PipelineList.razor`](../../src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineList.razor)（篩選下拉新增）。

**Phase B — 主菜：Vera 審查範圍擴及 .razor / .css**

- [`ReviewerAgentService.cs`](../../src/AiTeam.Bot/Agents/ReviewerAgentService.cs)：對齊 [`QaAgentService.cs:99-113`](../../src/AiTeam.Bot/Agents/QaAgentService.cs:99) 的 `hasUiChanges` 邏輯，提取 `csFiles` / `razorFiles` / `cssFiles`；略過條件改為 `!hasUiChanges && csFiles.Count == 0` → 回 `AgentExecutionResult.Skipped(...)`。
- `BuildClaudeCodeReviewPrompt` 簽名改為三個檔案清單；標題從「PR 變更（僅 .cs 檔案的 diff）」改名為「PR 變更 diff」；新增三段（.cs / .razor / .css）共用 helper `AppendFileDiff`（消除重複）。
- [`CLAUDE_Vera.md`](../../src/AiTeam.Bot/Resources/CLAUDE_Vera.md)：審查範圍擴成「.cs / .razor / .css」；新增「Razor / CSS / a11y / MudBlazor 判準（補充）」段落，**全列為 Warning**（除非 runtime 崩潰），呼應「寧可漏報 warning，不可誤報 critical」「偏好放行」哲學。Critical 仍只限：runtime 崩潰 / 資安 / 資源洩漏 三類。

**Phase C — TaskGroupService Reviewer Skipped 路由**

- [`TaskGroupService.cs:178-200`](../../src/AiTeam.Bot/Orchestration/TaskGroupService.cs:178)：Reviewer 完成判斷改為 `!result.Success || result.ResultType == AgentResultType.Skipped`，兩種情境都走「跳過 Petra 直接放行」路徑（log 訊息分流區別）。Vera 略過時 `CriticalReviewCount = 0`，避免下游 Petra 拿空 review report 異常。
- QA 略過：[`TaskGroupService.cs:156-163`](../../src/AiTeam.Bot/Orchestration/TaskGroupService.cs:156) 的 QA 處理只在 `TestReport != null` 時儲存，QA Skipped 時 `TestReport == null` 自然不進此 if，無需額外處理。

**Phase D — 搭車修三項**

- **D1** [`QaAgentService.cs:111-113`](../../src/AiTeam.Bot/Agents/QaAgentService.cs:111)：略過改用 `AgentExecutionResult.Skipped(...)`（與 Vera 共用同一 API）。
- **D2** BossInteraction.Description 補 Task.Description，三處改動（兩處對稱補完計劃書 L195 沒提到的 SlashCommandRouter）：
  - [`CommandHandler.cs:195`](../../src/AiTeam.Bot/Discord/CommandHandler.cs:195)（Dashboard 路徑）
  - [`CommandHandler.cs:441`](../../src/AiTeam.Bot/Discord/CommandHandler.cs:441)（Discord 路徑）
  - [`SlashCommandRouter.cs:245`](../../src/AiTeam.Bot/Discord/Routing/SlashCommandRouter.cs:245)（slash command `/build` 路徑，計劃書未提，掃描時順手補）
  - 新增 `BuildCeoConfirmDescription` static helper 在 [`CommandHandler.cs:559-571`](../../src/AiTeam.Bot/Discord/CommandHandler.cs:559)（前兩處共用）；SlashCommandRouter 因不同 namespace 採 inline 寫法（不過度抽象）。
- **D3** [`RuleManagement.razor:62`](../../src/AiTeam.Dashboard/Components/Pages/Rules/RuleManagement.razor:62)：MudSwitch 補 `aria-label="@(context.IsActive ? \"已啟用，點擊停用\" : \"已停用，點擊啟用\")"`。全站 grep 確認其他 4 處 MudSwitch 皆有 `Label`，無需改動。

**Phase E — Mock 覆蓋（review_skipped 情境）**

- [`ReviewerAgentService.cs:54-66`](../../src/AiTeam.Bot/Agents/ReviewerAgentService.cs:54)：MockMode early return 新增 `FailScenario == "review_skipped"` 分支，回 `AgentExecutionResult.Skipped(...)`。
- [`MockScenarioService.cs`](../../src/AiTeam.Bot/Services/MockScenarioService.cs)：`RunScenarioAsync` 新增 `review_skipped` 處理（設 FailScenario + 對應 `(NewFeature, "Vera 略過驗收", "Dev")`）。
- [`SlashCommandRouter.cs:114`](../../src/AiTeam.Bot/Discord/Routing/SlashCommandRouter.cs:114)：`/mock` 指令選項新增「【略過驗收】Vera 略過（無可審檔案 → skipped）」。
- [`MockScenarioCard.razor:53`](../../src/AiTeam.Dashboard/Components/Pages/Home/MockScenarioCard.razor:53)：Dashboard /mock 卡片新增「⏭️ 略過驗收 — Vera 略過（無可審檔案）」選項。

### 踩坑記錄

實作過程順利，無重大踩坑。三點注意事項：

1. **TaskGroupService 已有 `using AiTeam.Bot.Agents;`**：`AgentResultType` 不需額外 using。
2. **MudStepper 的 `Skipped` 屬性語義不同**：[`PipelineView.razor:126`](../../src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineView.razor:126) 的 `Skipped="@IsRevision(...)"` 是把 MudStepper 的 Skipped 視覺**借來給 "revision/reviewing"** 用（既有設計）。本 Stage 新增的 `"skipped"` 狀態走 `IsCompleted`（顯示為已完成樣式），由 StatusBadge teal 配色 + "略過" 文字 + 不顯示重試按鈕來呈現語義差異。
3. **C# record `with` 相容性**：`ResultType` 加在 record 尾端帶 default，所有既有 `result with { ... }` 表達式（如 `TaskGroupService.cs` 多處）無需改動。

### 驗收情境完成度

| 情境 | 狀態 | 驗收方式 |
|------|------|----------|
| A. Vera 審 .razor PR | ✅ 程式邏輯就緒 | Mock：`/mock new_feature` 觸發後若 PR 含 .razor 檔，Vera 走 review session（不再略過） |
| B. Vera 抓 a11y | ⏳ 待真實 PR 驗收 | CLAUDE_Vera.md 判準擴充已就位；FF 十子項實作中或 Trial_v3 時驗證 |
| C. BossInteraction 完整描述 | ✅ 程式邏輯就緒 | `/mock new_feature_with_proposal` → Dashboard 操作中心查看 ceo_confirm 卡片 |
| D. Reviewer 略過 skipped | ✅ 程式邏輯就緒 | `/mock review_skipped` → Dashboard 顯示綠色 skipped chip + 流程繼續走 QA |
| E. MudSwitch a11y | ✅ 程式邏輯就緒 | Playwright `/rules` accessibility audit 確認 aria-label 屬性存在 |

### 模型 / Effort 校準

- 預估 Context：~110-140K
- 實際模型：Sonnet 4.6 200K + high effort
- 工作項目：4 個 Phase（A 基礎建設 / B 主菜 / C-D 搭車修 / E Mock 覆蓋），共 17 個檔案改動
- 範本可用度：高（QaAgentService.hasUiChanges 直接抄 + 既有 mock FailScenario 結構抄）
- Build 通過：0 Errors

### 下一步（待 Aria 結案第二段處理）

- ⏳ `src/Directory.Build.props` 版本 bump：v3.25.0 → v3.26.0（minor）
- ⏳ [`docs/architecture/00_Master_Plan.md`](../architecture/00_Master_Plan.md) Stage 39 狀態更新（🟡 → 🟢）+ 版本歷史
- ⏳ [`docs/planning/Future_Feature.md`](Future_Feature.md) FF 二十八 標記完成 + Trial_v2 三項搭車修標記完成
