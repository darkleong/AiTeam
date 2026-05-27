# Stage 40：CLAUDE_Vera + CLAUDE_Petra 判準補強（Trial_v4 前置條件）

> 對應 Future Feature：FF 二十九（Vera 判準補強）+ FF 二十五子項（Petra 升級規則）
> 對應版本：v3.27.0
> 建立日期：2026-04-26
> 狀態：✅ 已完成（2026-04-26）
> 文件版本：v2.0

---

## 概述

**主菜**：FF 二十九 — `CLAUDE_Vera.md` 補三段判準（`<a>` link a11y / `target="_blank"` 安全 / 業務邏輯 string pattern match），解 Trial_v3 暴露的判準邊界覆蓋漏洞。

**第二菜**：FF 二十五子項 — `CLAUDE_Petra.md` 加一條「Vera Warning 升級為 blocking」規則，讓架構議題即使被 Vera 標 Warning 也能被 Petra 擋下。

**搭車修**（順手清理同類隱患）：
1. PR #109 帶進的兩個遺漏：`PipelineList.razor:65` 缺 `rel="noopener"` + `aria-label`
2. Vera 自己抓到但沒人修：`Home.razor:60-62` 同步顯示 PR 編號（不再寫死「PR」）+ 補 `rel="noopener"` + `aria-label`
3. `ExtractPrNumber` 抽共用 helper（消除 `PipelineList.razor.cs:155` vs `PipelineView.razor.cs:253` 重複）
4. **順手檢出**：`PipelineView.razor:32` + `ProjectManagement.razor:81` 也有 `target="_blank"` 缺 `rel="noopener"` 的 tabnabbing 隱患（同源議題，一起修）

**戰略意義**：這個 Stage 是 **Trial_v4 的前置條件**——FF 二十九讓 Vera 抓得到 a11y/安全議題，FF 二十五讓 Petra 把架構類 Warning 升 blocking，**兩者合一才是「審查層 + 閘門層」的完整閉環**。Stage 40 完成後即可規劃 Trial_v4 任務（涉及 a11y / 安全 / form / auth 的真實需求）驗證閉環效果。

---

## 第一部分（主菜）：FF 二十九 — CLAUDE_Vera.md 補三段判準

### 現況

[`CLAUDE_Vera.md`](../../src/AiTeam.Bot/Resources/CLAUDE_Vera.md) 已有「Razor / CSS / a11y / MudBlazor 判準（補充）」段（Stage 39 建立），目前 a11y 子段列了：button / MudButton / MudSwitch / MudCheckBox / MudIconButton / img — **但漏了 `<a>` link**，且**沒涵蓋安全慣例**與**業務邏輯 pattern match**。

Trial_v3（PR #109）執行後 Vera 沒抓到的議題：
- `<a target="_blank">` 缺 `rel="noopener"`（tabnabbing 安全漏洞）
- `<a>` link 缺 `aria-label`（純圖示 / 短文字 link）
- `Title.Contains("[MOCK]")` 業務邏輯 string pattern match（fragile 設計）

### 實作項目

#### 1. 安全段（**Critical**）

新增「**安全（Critical）**」子段，列為**唯二可能列為 Critical 的 razor 議題之一**（與既有「Blazor 例外處理」並列）：

- `<a target="_blank">` 與 `<MudLink Target="_blank">` **必須同時設定 `rel="noopener"`**（防 tabnabbing 漏洞）
  - 屬於 OWASP 列出的真實安全風險，不是「理論上可能」
  - 因此突破 a11y/CSS/MudBlazor「一律 Warning」的保守設計，列為 **Critical**

#### 2. a11y `<a>` link 段（**Warning**）

在既有 a11y 子段補入：

- `<a>` 與 `<MudLink>` 缺 `aria-label`（純圖示 link / icon-only link / 文字過短難識別 link）→ Warning
  - 例：只顯示「PR」「下載」「More」等短文字、或只有 icon 的 link

#### 3. 業務邏輯 string pattern match 段（**Warning**）

新增「**業務邏輯 pattern match（Warning）**」子段（不在 a11y / CSS / MudBlazor 慣例內，是新類型「設計 smell」）：

- `Title.Contains("[XXX]")` / `Description.StartsWith("Mock:")` / 等用 string pattern 判斷業務狀態 → Warning
  - **建議改法**：DTO 加明確欄位（`IsMock` / `Source = "mock" | "production"`）/ 注入 service 判斷 / 列舉常數
  - **理由**：pattern match 是 fragile 設計，標題文案改動或多語系化會無聲破壞邏輯

### 寫作風格約束

維持 Stage 39 已建立的「**寧可漏報一個 warning，也不可誤報一個 critical**」「**偏好放行**」哲學：
- **僅 `rel="noopener"` 升 Critical**（OWASP 真實安全）
- a11y `<a>` 與 pattern match 維持 Warning（不會崩潰、不洩資料、不洩資源）

### 邊界覆蓋自查（Stage 39 自省點 #8 應用）

寫完判準後，對照「真實 PR 可能出現的 a11y / 安全議題類型」自查覆蓋率：

| 類型 | 是否涵蓋 |
|------|---------|
| 互動元素無文字（button / MudButton / MudSwitch / MudCheckBox / MudIconButton / img） | ✅ Stage 39 已涵蓋 |
| **`<a>` / `<MudLink>` 缺識別**（icon-only / 短文字 link） | ✅ **本 Stage 補入** |
| **`target="_blank"` tabnabbing**（`rel="noopener"`） | ✅ **本 Stage 補入** |
| 業務邏輯 string pattern match | ✅ **本 Stage 補入** |
| Form input 無 label / aria | ⚠️ 留待 Trial_v4 暴露後再評估（form 場景目前少見） |
| Table 無 caption / scope | ⚠️ 留待暴露後再評估 |

目標覆蓋率 **80%+**，剩餘留給 Vera 自由探索。

---

## 第二部分（第二菜）：FF 二十五子項 — CLAUDE_Petra.md 加一條 Warning→blocking 升級規則

### 現況

[`CLAUDE_Petra.md`](../../src/AiTeam.Bot/Resources/CLAUDE_Petra.md) 「### 4. Vera 審查結果判斷」段（L72-90）目前的分類：

- **blocking**：邏輯錯誤 / 安全漏洞 / build 失敗
- **minor**（放行）：命名 / comment / 風格 / 效能可以更好 / 重構建議 / 測試覆蓋率不夠

問題：**Vera 標 Warning 的「架構議題」一律掉進 minor，全部放行**。Trial_v3 的 W01/W02（重複 fallback / 硬編碼字串）就是這樣放過。

### 實作項目

「### 4. Vera 審查結果判斷」段擴充：

#### 新增 blocking 規則：「Warning 升級為 blocking」清單

**Vera 標 Warning 的議題中，以下類型應視為 blocking 不放行：**

- 重複邏輯 / 重複定義（如同樣 helper 在多檔出現）
- 硬編碼預設值應引用既有常數
- 多份 config 分散維護（Bot vs Dashboard 各自一份）
- 業務邏輯用 string pattern match 取代欄位 / 列舉
- `target="_blank"` 缺 `rel="noopener"`（即使 Vera 因任何原因標 Warning 而非 Critical，Petra 也應視為 blocking）

**理由**：這些是「架構債務 / 安全債務」，現在不擋下會持續累積。Petra 的「偏好放行」針對的是命名/格式/效能，不是架構議題。

#### 維持原有 minor 清單（不變）

- 命名不一致、不夠好
- 缺少 comment 或 docstring
- 程式碼風格
- 效能「可以更好」但目前能用
- **單純**重構建議（「這段可以抽成 method 但不重複」）— 注意：「重複定義 → 抽 helper」現在升 blocking
- 測試覆蓋率不夠

### 風險：Petra 改動會影響全流程

這條改動**不只影響 self-implement 試驗**，所有 PR 的 Petra 審核都會用新規則。風險評估：

- ✅ **正面**：架構債務不再無聲累積
- ⚠️ **副作用**：可能擋下「Vera 抓到但 Christ 接受」的議題（例：MockMode 的 [MOCK] pattern match 既有設計）
- 🛡️ **緩解**：升 blocking 不等於 reject，只是 Petra 會 `revise` 給 Cody 修；若 Cody 反駁有理 Petra 仍可 escalate；最差情況 Christ 在 Discord 介入決定

### 邊界覆蓋自查（避免誤擋日常 PR）

寫完判準後思考「正常修 bug PR 會不會被誤擋」：

- 修一個 typo + Vera 標 Warning「函式命名不一致」→ minor 放行 ✅
- 加一個 if 分支 + Vera 標 Warning「這分支可以提取 method」→ minor 放行 ✅
- 改一個 link 加 `target="_blank"` 沒加 `rel="noopener"` → blocking 擋下 ✅（這就是要擋的）
- 兩處重複 `ExtractPrNumber` → blocking 擋下 ✅（這就是要擋的）

---

## 第三部分（搭車修）：PR #109 + 同源 tabnabbing 清掃

### 範圍

把所有 codebase 內的 `target="_blank"` 缺 `rel="noopener"` 一次清掉，作為 **FF 二十九 補強的真實演習場**：

| 位置 | 現況 | 修法 |
|------|------|------|
| [`PipelineList.razor:65`](../../src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineList.razor:65) | `<MudLink Target="_blank">` PR 連結 | 加 `rel="noopener"` + `aria-label`（描述 PR 編號） |
| [`PipelineView.razor:32`](../../src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineView.razor:32) | `<MudLink Target="_blank">` PR 連結 | 同上 |
| [`Home.razor:62`](../../src/AiTeam.Dashboard/Components/Pages/Home/Home.razor:62) | `<MudLink Target="_blank">` 寫死「PR」文字 | 改用 `ExtractPrNumber()` 顯示 #編號 + 加 `rel` + `aria-label` |
| [`ProjectManagement.razor:81`](../../src/AiTeam.Dashboard/Components/Pages/Projects/ProjectManagement.razor:81) | 原生 `<a target="_blank">` Repo URL | 加 `rel="noopener"` + `aria-label` |

### MudLink 加 rel 屬性的技術細節

MudBlazor 8 的 `MudLink` 元件不直接接受 `rel` 參數。實作 Session 需確認可行寫法（兩種候選）：

- 方案 A：`UserAttributes="@(new() { ["rel"] = "noopener" })"`
- 方案 B：直接 inline `rel="noopener"`（MudLink 是否會 forward 未驗證）
- 方案 C：改用原生 `<a>` 標籤（若 MudLink 不支援）

由 Forge 在實作時 grep MudBlazor source 或試 build 驗證。

---

## 第四部分（搭車修）：ExtractPrNumber 抽共用 helper

### 現況

兩處幾乎相同的 `private static string ExtractPrNumber(string? url)`：

- [`PipelineList.razor.cs:155`](../../src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineList.razor.cs:155)
- [`PipelineView.razor.cs:253`](../../src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineView.razor.cs:253)

Vera Trial_v3 W01 抓到的議題，Stage 40 順手解決。

### 實作方向

抽到 Dashboard 共用 helper，候選位置（由 Forge 決定）：
- `src/AiTeam.Dashboard/Helpers/PrNumberHelper.cs`（新建 Helpers/ 資料夾）
- `src/AiTeam.Dashboard/Components/Shared/PrNumberHelper.cs`（沿用既有 Shared/）
- `src/AiTeam.Shared/` 內適當位置（若 Bot 端有共用需求；但 Bot 端 ExtractPrNumber 簽名不同，**本次不跨層共用**）

**注意**：Bot 端的 `ReviewerAgentService.ExtractPrNumber(string text)` / `QaAgentService.ExtractPrNumber(string text)` / `DocAgentService.ExtractPrNumber(string text)` / `DevAgentService.ExtractPrNumberFromText(string text)` 簽名不同（吃 text 不是 URL，回傳 int 不是 string），**不在本次抽 helper 範圍**。

### 既有測試處理

- [`tests/Generated/.../PipelineListTests.cs`](../../tests/Generated/src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineListTests.cs) 用 reflection 呼叫 private method
- [`tests/Generated/.../PipelineViewTests.cs`](../../tests/Generated/src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineViewTests.cs) 同上

抽 helper 後這兩個 test 檔需更新為直接呼叫 public helper（移除 reflection），或改為呼叫 helper 的單一測試檔（合併兩份）。

---

## 驗收情境

### A. CLAUDE_Vera.md 三段判準寫入（FF 二十九）

**驗收方式**（程式邏輯就緒層）：
- `git diff` 顯示 `CLAUDE_Vera.md` 含三段新內容（安全 Critical / `<a>` aria-label Warning / pattern match Warning）
- 重啟 Bot 容器後，啟動 log 不報 template parse 錯
- **真實生效驗證**：留待 Trial_v4 真實 PR 跑（本 Stage 不獨立驗 Vera 行為，因 Vera 跑需真 PR）

### B. CLAUDE_Petra.md Warning→blocking 升級規則寫入（FF 二十五子項）

**驗收方式**（程式邏輯就緒層）：
- `git diff` 顯示 `CLAUDE_Petra.md` 第 4 節含新升級規則清單
- 重啟 Bot 容器後，啟動 log 不報 template parse 錯
- **真實生效驗證**：同樣留待 Trial_v4 真實 PR 跑（觀察 Petra 對 Vera 標 Warning 的「重複定義」是否升 blocking）

### C. PR 連結補完安全 / a11y 屬性

**手動驗證步驟**：
1. `dotnet build AiTeam.slnx` → 0 Errors
2. Playwright 截圖 `/` 首頁 + `/tasks` 流程列表 + 任意 task 的 PipelineView Drawer
3. 開瀏覽器 DevTools → Inspect 對應 link element：
   - `rel` 屬性包含 `noopener`
   - `aria-label` 屬性存在（內容包含 PR 編號或描述）

### D. Home.razor PR 連結同步顯示編號

**手動驗證步驟**：
1. 進入首頁，找一個有 `DevPrUrl` 的 active task
2. PR 連結文字應顯示 **#XXX**（編號），不是寫死「PR」
3. 點擊應正常開新分頁到 GitHub PR

### E. ExtractPrNumber helper 抽出 + 測試通過

**驗收方式**：
1. `dotnet build AiTeam.slnx` → 0 Errors
2. `dotnet test` → 既有 ExtractPrNumber 測試全部通過（不論抽到何處）
3. `PipelineList.razor.cs` 與 `PipelineView.razor.cs` 不再各自定義 `ExtractPrNumber`（grep 確認）

### F. ProjectManagement Repo 連結 a11y / 安全

**手動驗證步驟**：
1. 進入 `/projects` 頁面，選一個有 `RepoUrl` 的 project
2. DevTools Inspect Repo link → `rel="noopener"` + `aria-label` 存在

---

## 技術約束 & 注意事項

1. **CLAUDE_*.md 改動 = 行為改動，需重啟 Bot 容器**：CLAUDE_Vera.md 與 CLAUDE_Petra.md 修改後，Bot 容器啟動 seed 邏輯不會自動更新。**驗收前需 push → CI/CD 重建容器**才能生效。
2. **MudLink rel 屬性 API 確認**：MudBlazor 8 的 `MudLink` 是否 forward 任意 HTML attribute 未驗證。若 inline `rel="noopener"` 不生效，採 `UserAttributes` 字典寫法或回退原生 `<a>`。
3. **Petra 改動影響全流程**：升級規則寫入後，所有 PR 的 Petra 審核都會用新規則。可能造成「以前放行的議題現在 revise」的行為變化。**驗收期間若觀察到誤擋，可立即微調規則文字**（純 prompt 改寫，零成本）。
4. **既有 ExtractPrNumber tests 需同步**：抽 helper 時，`PipelineListTests.cs` 與 `PipelineViewTests.cs` 的 reflection 呼叫要一併改為 public helper 直呼。可考慮合併兩份為單一 `PrNumberHelperTests.cs`。
5. **真實 Vera/Petra 行為驗收延後**：本 Stage 驗收只到「prompt 改完 + UI 屬性補完 + helper 抽完」層次。真實 Vera 抓 a11y / Petra 升 blocking 的行為，**留待 Trial_v4 真實 PR 觀察**。

---

## 版本

`v3.26.0 → v3.27.0`（minor bump）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議

**推薦：Sonnet 200K + medium**

四維度評估：

| 維度 | 評估 |
|------|------|
| **Context 量** | 低（CLAUDE_Vera.md ~120 行 + CLAUDE_Petra.md ~130 行 + 4 個 razor + 2 個 .razor.cs + 1 個新 helper + 2 個 test 檔） |
| **邏輯複雜度** | 低（純 prompt 改寫 + UI 屬性補強 + 簡單 refactor） |
| **風險代價** | 低（純 prompt + 純 UI 屬性 + 一個小 refactor，無 Migration / 無啟動流程） |
| **範本可用度** | 高（Stage 39 剛做過 prompt 擴充 + 既有 a11y 段結構可延續） |

**Context 粗估**：~60-75K × 1.6 = ~96-120K（Sonnet 200K 充裕，邊界遠在 130K 以下）

**選 Sonnet 200K + medium 理由**：
- 性質單純：兩處 prompt 改寫 + UI 屬性補強 + 一個小 refactor
- 範本可用度高（Stage 39 a11y 段結構直接延續）
- 無核心流程改動、無 Migration、無 DI 變更
- 規模最接近 S-M 工程

**不需要拉 high effort 的理由**：本 Stage 沒有跨檔推理 / 新邏輯設計 / 動共用 Service 抽離 / 動啟動流程，medium 足夠。

---

## 不在範圍

- ❌ Vera / Petra **真實行為驗收**（留待 Trial_v4 真實 PR 觀察）
- ❌ Form input / Table 無 label 等其他 a11y 議題（留待 Trial_v4 暴露後再加）
- ❌ Bot 端 `ExtractPrNumber*` 統一（簽名不同，跨層抽 helper 不划算）
- ❌ FF 三十（tech_improvement ghost Dev task）— 動 Orchestrator 性質不同，獨立 Stage 處理
- ❌ FF 二十二 子項 B（PushAgentStatus 命名映射）— M 規模、動 SignalR，獨立 Stage 處理

---

## 後續關聯

- **Trial_v4 規劃**（FF 二十七延伸）：本 Stage 完成後即可規劃 v4 任務（涉及 a11y / 安全 / form / auth 的真實需求）驗證 Vera + Petra 閉環效果
  - 但**任務載體待自然合適的需求出現**，不為了 Trial 強迫造任務
  - 設計 Trial_v4 任務 prompt 時，必讀 [FF 二十五](Future_Feature.md) 整段（self-implement prompt 守則）
- **FF 三十** ghost Dev task：留待動 Orchestrator 的 Stage 搭車
- **FF 二十二** Agent 命名一致性：留待 SignalR refactor 搭車

---

## 實作紀錄

### 實作完成項目

**主菜 — FF 二十九：CLAUDE_Vera.md 三段判準補強**
- **安全 Critical 段**：`<a target="_blank">` / `<MudLink Target="_blank">` 缺 `rel="noopener"` 列為 **Critical**（與 Blazor 例外處理並列唯二 Critical razor 議題）
- **a11y `<a>` link 段**：`<a>` / `<MudLink>` 缺 `aria-label`（icon-only / 短文字）→ Warning；補入既有 a11y 子段末尾
- **業務邏輯 pattern match 段**：`Title.Contains("[XXX]")` 等 string pattern 判斷業務狀態 → Warning；新增獨立子段說明建議改法與理由
- 「關鍵」note 同步更新為「唯二例外：tabnabbing + @onclick 未捕例外」

**第二菜 — FF 二十五子項：CLAUDE_Petra.md Warning→blocking 升級規則**
- 在 blocking 清單後新增「Warning 升 blocking」清單（五條：重複定義 / 硬編碼常數 / config 分散 / pattern match / `rel="noopener"` 缺漏）
- minor 清單「重構建議」條目加「但不重複」限定語，區分 minor vs blocking 的邊界
- revise 標準更新為涵蓋「Warning 升 blocking 清單」

**搭車修 — tabnabbing 全面清掃**
- `PipelineList.razor:65` — `<MudLink>` 補 `rel="noopener"` + `aria-label`（含 PR 編號）
- `PipelineView.razor:32` — 同上
- `Home.razor:62` — 寫死「PR」改為 `PrNumberHelper.ExtractPrNumber(...)` 動態顯示 `#編號` + 補 `rel` + `aria-label`
- `ProjectManagement.razor:81` — 原生 `<a>` 補 `rel="noopener"` + `aria-label`

**搭車修 — ExtractPrNumber 抽共用 helper（Vera Trial_v3 W01）**
- 新建 `src/AiTeam.Dashboard/Helpers/PrNumberHelper.cs`（public static，Helpers/ 新資料夾）
- `_Imports.razor` 加 `@using AiTeam.Dashboard.Helpers` 全域可用
- `PipelineList.razor.cs` / `PipelineView.razor.cs` 移除各自的私有 `ExtractPrNumber`，改呼叫 `PrNumberHelper.ExtractPrNumber`
- 測試檔移除 Reflection 呼叫，改為直接呼叫 `PrNumberHelper.ExtractPrNumber`

### 關鍵設計決策

**MudLink `rel` 屬性寫法**：採 inline `rel="noopener"` 直接寫在 MudLink 元件上（方案 B）。MudBlazor 8 的 MudLink 繼承 MudComponentBase，支援 splatted attributes（`UserAttributes`），inline 未知屬性會 forward 至底層 `<a>`。Build 與驗收均正常，不需改用 `UserAttributes` 字典。

**Helpers/ 資料夾位置**：選 `src/AiTeam.Dashboard/Helpers/`（新建資料夾），而非放入既有 `Components/Shared/`。Dashboard 層的純邏輯 helper 語意上不屬於 Component，獨立 Helpers/ 更清晰。Bot 端的 `ExtractPrNumber*`（簽名不同）不在本次抽 helper 範圍。

**Petra Warning→blocking 升級清單措辭**：明確列出五個類型而非模糊說「架構議題」，避免 LLM 自由裁量範圍過寬誤擋。同時在 minor 清單保留「單純重構建議」，用「但不重複」限定語區分邊界，降低誤擋正常 PR 的風險。

### 驗收後修正

無。首次驗收即通過。

### 踩坑紀錄

**tests/Generated/ 非編譯目標**：`tests/Generated/` 資料夾下的 `PipelineListTests.cs` / `PipelineViewTests.cs` 沒有對應 csproj，不屬於任何已編譯專案，無法透過 `dotnet test` 驗證。這些檔案是文件性質的 reference，Roadmap 中提到的「`dotnet test` ExtractPrNumber 測試通過」實際上無法以此方式驗證。已改以 build 0 Errors + grep 確認方式完成本次 E 項驗收。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|------|------|------|
| v1.0 | 2026-04-26 | 計劃書建立（Aria）— FF 二十九 主菜 + FF 二十五子項 + 同源 tabnabbing 清掃 + ExtractPrNumber 抽 helper |
| v2.0 | 2026-04-26 | 實作完成（Forge）— 全項驗收通過；commit 8454a0f |
