# Stage 41：tests/Generated/ 編譯與執行修復 + Quinn 結構性 bug 搭車

> 對應 Future Feature：FF 三十一（測試品質保證迴圈第三層）
> 對應版本：v3.28.0
> 建立日期：2026-04-27
> 狀態：📝 規劃中
> 文件版本：v1.0

---

## 概述

**主菜**：FF 三十一 — `tests/Generated/` 7 個 test 檔從未編譯過、從未執行過。建 xUnit csproj 讓 `dotnet test` 真正跑得到，補完「Vera 審查層 + Petra 閘門層 + Quinn 測試層」三層品質保證迴圈的最後一塊。

**搭車修**（Aria 探索期間 grep 揭露的 Quinn 結構性 bug）：
1. **3 檔開頭含 ```csharp markdown fence**（永遠無法 parse）— Quinn LLM 輸出未剝離 markdown code block
2. **1 檔測試幻覺類別**（`AgentSettings.razorTests.cs` 測「Customer Service Agent / Sales Agent / Technical Support Agent」三個 codebase 中**不存在**的頁面）
3. **CLAUDE_Quinn.md 補強**兩條判準（避免未來繼續產生同類破損）：
   - 「輸出純 C# code，不含 markdown code fence」
   - 「測試標的必須存在於 codebase，先 grep / Read 確認類別 / 方法存在再寫測試」

**戰略意義**：Stage 39（Vera 審查 razor/css）+ Stage 40（Vera/Petra 判準補強）+ **Stage 41（Quinn 測試真正跑）** = 完整品質保證迴圈。Trial_v4 的「Top 3（Quinn 測試品質）」評估線之前一直是空話，Stage 41 完成後才能真正驗證。

---

## 第一部分（主菜）：建立 xUnit test project

### 現況

- `tests/Generated/` 7 檔皆為 xUnit + FluentAssertions（部分加 NSubstitute / Octokit）
- 唯一既有測試專案 [`src/AiTeam.Tests.Playwright/`](../../src/AiTeam.Tests.Playwright/) 採 **MSTest + Microsoft.Playwright.MSTest**（與 Generated 風格不同）
- `find tests -name "*.csproj"` = 0 hits → Generated 完全沒被編譯

### 7 檔 inventory（建議 Forge Plan Mode 第一步逐檔 build 探索）

| 檔案 | 看起來狀態 | 處理 |
|---|---|---|
| [`tests/Generated/AiTeam/Pages/AgentSettings.razorTests.cs`](../../tests/Generated/AiTeam/Pages/AgentSettings.razorTests.cs) | ❌ **幻覺**（測 codebase 不存在的頁面） | **刪除** |
| [`tests/Generated/src/AiTeam.Bot/GitHub/GitHubServiceTests.cs`](../../tests/Generated/src/AiTeam.Bot/GitHub/GitHubServiceTests.cs) | ⚠️ markdown fence + 用 NSubstitute / Octokit | 清 fence + 修破損 |
| [`tests/Generated/src/AiTeam.Dashboard/Components/Layout/MainLayout.razorTests.cs`](../../tests/Generated/src/AiTeam.Dashboard/Components/Layout/MainLayout.razorTests.cs) | ⚠️ 用 protected member subclass 技巧，依賴 MainLayout 內部 API | 修破損（可能要對照 src 變化） |
| [`tests/Generated/src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineListTests.cs`](../../tests/Generated/src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineListTests.cs) | ✅ Stage 40 已修 reflection → `PrNumberHelper` 直呼 | 應可直接 build |
| [`tests/Generated/src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineViewTests.cs`](../../tests/Generated/src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineViewTests.cs) | ✅ Stage 40 已修 | 應可直接 build |
| [`tests/Generated/src/AiTeam.Dashboard/Components/Pages/Tasks/TaskCenter.razorTests.cs`](../../tests/Generated/src/AiTeam.Dashboard/Components/Pages/Tasks/TaskCenter.razorTests.cs) | ⚠️ markdown fence + 反射 private method | 清 fence + 修破損 |
| [`tests/Generated/src/AiTeam.Shared/Dtos/TaskItemDtoTests.cs`](../../tests/Generated/src/AiTeam.Shared/Dtos/TaskItemDtoTests.cs) | ✅ 看起來最乾淨 | 應可直接 build |

**3 檔含 markdown fence 開頭**（不是 valid C#）：
- `AgentSettings.razorTests.cs` — 反正要刪
- `GitHubServiceTests.cs` — 清 fence
- `TaskCenter.razorTests.cs` — 清 fence

### 實作項目

#### 1. 建立 csproj（位置由 Forge 決定）

**csproj 位置候選**（由 Forge Plan Mode 探索選擇）：
- 候選 A：`tests/AiTeam.Tests.Generated/AiTeam.Tests.Generated.csproj`（從 `tests/Generated/` 取出 csproj 到上層獨立資料夾，保持 `tests/Generated/` 純 LLM 產出區） — Aria 推薦
- 候選 B：`tests/Generated/AiTeam.Tests.Generated.csproj`（csproj 直接放 Generated/ 內）
- 候選 C：拆三個 csproj 對應 src 三個專案（Bot / Dashboard / Shared）— 結構工整但維護成本高，**不建議**

**csproj 內容**：
- `TargetFramework`：`net10.0`（對齊主專案）
- `IsTestProject`：`true`
- `IsPackable`：`false`
- 套件：`xunit` + `xunit.runner.visualstudio` + `FluentAssertions` + `NSubstitute` + `Octokit`（Octokit 對應 GitHubServiceTests 需求）+ `Microsoft.NET.Test.Sdk`
- ProjectReference：`AiTeam.Bot` + `AiTeam.Dashboard` + `AiTeam.Shared`（三個 src 專案）
- 注意 `MainLayout.razorTests.cs` 與 `TaskCenter.razorTests.cs` 用了 protected/private 反射技巧，可能需要 `InternalsVisibleTo` 但因用 reflection 不一定需要

#### 2. 清檔案層破損

**A. 刪除幻覺檔案**：
- `tests/Generated/AiTeam/Pages/AgentSettings.razorTests.cs`（測「Customer Service / Sales / Technical Support Agent」codebase 不存在的頁面）
- 順便確認 `tests/Generated/AiTeam/Pages/` 是否還有其他檔；空資料夾可以連帶清掉

**B. 清 markdown code fence**：
- `GitHubServiceTests.cs` 開頭的 ```csharp 與檔尾可能的 ``` 移除
- `TaskCenter.razorTests.cs` 開頭的 ```csharp 與檔尾可能的 ``` 移除

#### 3. 修破損（unknown variable，要逐檔 build）

5 檔保留候選逐檔 `dotnet build` + 修：
- 預期問題類型：`using` namespace 變更、Stage 34/35/36 大檔拆解後的 namespace（例：`AiTeam.Bot.Agents.Pm.*`）、DTO 欄位增刪、API signature 變化
- **修破損的原則**（Forge 驗收前要遵守）：**對照 src 當前真實行為修測試斷言，不要為了讓測試過而改成錯的斷言**。如果一個 test 的「對的行為」已不存在 → 刪除該 test case，不要寫假斷言遷就

#### 4. 整合進 solution

- `AiTeam.slnx` 加入新 csproj
- `dotnet test AiTeam.slnx` 能跑到所有 Generated 測試
- CI/CD self-hosted runner 在下次 push 後自動執行（驗證 GitHub Actions 紀錄）

### 不在範圍

- ❌ **新增測試覆蓋**（本 Stage 只把現有 7 檔修到能跑 + Quinn prompt 補強，不寫新 test）
- ❌ **Quinn 測試品質深度補強**（CLAUDE_Quinn.md 只補本次發現的兩個結構性 bug，「測試品質判準」（如 dummy assertion 防護）等到 Trial_v4 真實觀察後再評估）
- ❌ **Playwright 測試**（既有 `AiTeam.Tests.Playwright` 已 OK；Generated 7 檔皆為 xUnit unit test，無 Playwright UI 測試）
- ❌ **`InternalsVisibleTo` 全面打開**（如果 reflection 已能跑，不為了「更乾淨」加 InternalsVisibleTo）

---

## 第二部分（搭車修）：CLAUDE_Quinn.md 補強兩條結構性 bug 防護

### 現況

[`CLAUDE_Quinn.md`](../../src/AiTeam.Bot/Resources/CLAUDE_Quinn.md) 是 Quinn (QA Agent) 的 prompt 模板。Stage 41 探索揭露 Quinn 有兩個結構性 bug 已長期累積：

1. **輸出含 markdown code fence**（3/7 檔開頭 ```csharp 包裹）—— Quinn LLM 輸出時把 code block 標記也寫進檔案，使檔案不是 valid C#
2. **測試幻覺類別**（`AgentSettings.razorTests.cs` 測根本不存在的 class）—— Quinn 沒驗證測試標的是否存在 codebase

### 實作項目

CLAUDE_Quinn.md 補入：

#### A. 輸出純 C# code 規則

明確指示：「測試檔案輸出**純 C# source code**，**不要**包 ```csharp ```（markdown code fence）。檔案內容必須是 valid C#（從 `using` 或 `namespace` 開始），可以直接被 `dotnet build` 編譯」。

#### B. 測試標的存在性驗證

明確指示：「寫測試前**必須先 grep / Read 確認測試的類別 / 方法存在於 codebase**。不要憑想像或自然語言需求描述就寫『測試 X 類別的 Y 方法』。如果類別 / 方法不存在，先回報 Petra / Christ 而非生成假測試」。

### 寫作風格

對齊 CLAUDE_Vera.md 既有風格（直接條列規則 + 範例），不另起新章節結構。

---

## 驗收情境

### A. csproj 建立 + dotnet build 通過

**驗收方式**：
1. `tests/` 下出現新 csproj（位置由 Forge 決定）
2. `dotnet build AiTeam.slnx` → **0 Errors**（warning 可接受，但不能因 Generated csproj 多出 error）
3. `AiTeam.slnx` 含新 csproj 條目（grep 確認）

### B. 檔案層破損清除

**驗收方式**：
1. `grep -l '^```' tests/Generated -r` → **無結果**（3 檔 fence 已清）
2. `tests/Generated/AiTeam/Pages/AgentSettings.razorTests.cs` 已刪除（`ls` 確認）
3. 全 repo grep 「`Customer Service Agent`」/「`Sales Agent`」/「`Technical Support Agent`」 → **無命中**（確認無殘留幻覺引用）

### C. dotnet test 真正執行 + 結果可觀察

**驗收方式**：
1. `dotnet test AiTeam.slnx` 能執行到 Generated test cases
2. 輸出顯示測試數量（Passed / Failed / Skipped）— 至少有 N 個 test 真的跑了，**不是 0**
3. **可接受少數 test 失敗**（src 演進後測試斷言已過時，但驗收**強制要求**：每個失敗的測試都需在實作紀錄內記錄根因（src 改動 / 測試斷言錯誤 / 兩者擇一）；**不接受刪除測試斷言只為了讓綠燈**

### D. CI/CD self-hosted runner 自動跑

**驗收方式**：
1. push 後 GitHub Actions self-hosted runner trigger
2. 流程紀錄含 Generated test 執行步驟
3. （Christ 視 GitHub Actions UI 確認）

### E. CLAUDE_Quinn.md 補強寫入

**驗收方式**：
1. `git diff` 顯示 CLAUDE_Quinn.md 含新增兩段（markdown fence 禁止 + 測試標的存在性驗證）
2. 重啟 Bot 容器後，啟動 log 不報 template parse 錯
3. **真實生效驗證**：留待後續 Trial 觀察 Quinn 是否還會產生 markdown fence / hallucinated 測試

---

## 技術約束 & 注意事項

1. **csproj 套件版本對齊**：xUnit / FluentAssertions / NSubstitute 版本由 Forge 探索 latest stable 或對齊 .NET 10 相容版。**不要憑空挑版本號**，要 grep 既有 NuGet cache 或 NuGet.org 確認。
2. **Octokit 是 GitHubServiceTests.cs 唯一外部依賴**：除了該檔以外其他檔不需 Octokit。csproj 仍 include Octokit（單一專案統包）。
3. **修破損的紀錄義務**：5 檔候選逐檔 build + 修時，每檔的修法（用 git blame 對照 src 改動 / 找新對應 method / 新斷言邏輯）需在實作紀錄寫清楚，避免「神秘修法」。
4. **CI/CD 階段確認**：`AiTeam.Tests.Playwright` 在 GitHub Actions 流程中是否啟動、用什麼指令？新 csproj 是否會自動含進去？需在實作 Session 探索 `.github/workflows/*.yml` 確認。
5. **CLAUDE_Quinn.md 改動 = 行為改動**：與 Stage 39/40 相同，需 push → CI/CD rebuild Bot 容器才生效。
6. **不要刪 Stage 40 已修的 PipelineList/PipelineViewTests.cs**：這兩檔已是 Stage 40 工作成果（reflection → public helper 直呼），保留不動。

---

## 版本

`v3.27.0 → v3.28.0`（minor bump）

修改位置：`src/Directory.Build.props`

---

## Model / Effort 建議

**推薦：Sonnet 200K + high**

四維度評估：

| 維度 | 評估 |
|------|------|
| **Context 量** | **中**（AiTeam.slnx + Tests.Playwright csproj 對照 + 7 個 test 檔讀 + 部分 src 對照（GitHubService / MainLayout / TaskCenter / TaskItemDto） + 寫 csproj + 多輪 build/test + CLAUDE_Quinn.md 改） |
| **邏輯複雜度** | **中**（修破損是 unknown variable，需要對照 src 演進判斷修哪邊） |
| **風險代價** | **中**（建立新 csproj + 動 slnx；test 修破損可能踩到 src 改動的歷史路徑） |
| **範本可用度** | **中**（既有 AiTeam.Tests.Playwright csproj 結構可參考，但 framework 不同 MSTest vs xUnit） |

**Context 粗估**：~85-110K × 1.6 = ~135-175K（Sonnet 200K 邊界內但要謹慎，high effort 處理破損修法）

**選 Sonnet 200K + high 理由**：
- 主要工作機械化（建 csproj / 清 fence / 刪幻覺）但**修破損是 unknown variable**，high effort 確保推理品質
- 範本可用度中等（要參考 Tests.Playwright 但 framework 不同）

**替代方案**：
- 若驗收期間發現 5 檔破損實際很嚴重（要大改 substantive logic）→ 拆 Session B 用 Sonnet 200K + high 專修破損
- 若 Forge Plan Mode 評估後認為超過 130K → 升 Opus 1M + medium

---

## 不在範圍

- ❌ Trial_v4 試驗（Stage 41 完成後再規劃）
- ❌ 新增測試覆蓋（本 Stage 只修現有 7 檔到能跑）
- ❌ CLAUDE_Quinn.md 全面測試品質判準補強（只補兩個結構性 bug 防護，深度判準等 Trial_v4 觀察後再評估）
- ❌ Playwright 測試結構動（`AiTeam.Tests.Playwright` 不動）
- ❌ FF 三十（tech_improvement ghost Dev task）— 性質不同，獨立 Stage 處理
- ❌ FF 二十二 子項 A / B（Agent 命名一致性）— 獨立 Stage 處理

---

## 後續關聯

- **Trial_v4 規劃**：Stage 41 完成後 = Vera + Petra + Quinn 三層完整迴圈就緒。Trial_v4 任務載體仍待自然合適的需求出現（FF 十六 Dashboard 錯誤處理 UX 是強候選 — a11y 議題密度高）。
- **Quinn 測試品質深度判準**：Stage 41 只補結構性 bug 防護，Trial_v4 真實 PR 跑完後若發現 Quinn 寫的測試品質仍差（例：dummy assertion / coverage 形式主義），再開 FF 評估補 CLAUDE_Quinn.md。
- **FF 三十** ghost Dev task：留待動 Orchestrator 的 Stage 搭車。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|------|------|------|
| v1.0 | 2026-04-27 | 計劃書建立（Aria）— FF 三十一 主菜（建 xUnit csproj + 修 7 檔破損）+ CLAUDE_Quinn.md 兩條結構性 bug 防護搭車 |
