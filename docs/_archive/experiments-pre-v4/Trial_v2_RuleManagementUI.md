# Self-Implement 試驗 v2 — 規則管理頁面 UI 微調

> 日期：2026-04-25
> 觸發：[FF 二十七](../planning/Future_Feature.md)
> 任務：規則管理頁面表格 UI 微調（移除狀態文字 + 操作改圖示按鈕）
> WorkflowType：`tech_improvement`
> PR：[#108](https://github.com/darkleong/AiTeam/pull/108)
> 觀察者：Aria（Opus 4.7 1M）+ Christ
> 狀態：✅ 完成（部分驗證 + 多項新發現）

---

## 試驗目的（複習 FF 二十七）

驗證 Stage 37 self-implement 試驗 v1（PR #107）品質低下的三大主因，在 FF 二十四（`CLAUDE_*.md` template COPY 修復，2026-04-25）之後是否回升：

| Top | 主因 | v2 應驗證的點 |
|-----|------|---------------|
| **Top 1** | `CLAUDE_Vera.md` / `CLAUDE_QA.md` 在 production 沒被 COPY → Vera/Quinn 用錯角色 | FF 二十四 fix 後，Vera/Quinn 行為是否回到專業職責 |
| **Top 2** | `CLAUDE_Vera.md` 「偏好放行」+ Petra 對 Warning 寬鬆 | 架構議題（重複邏輯/多份 config）會被歸 Critical 還是 Warning |
| **Top 3** | `CLAUDE_QA.md` 測試品質指引偏「量」不偏「質」 | Quinn 是否寫實質測試 vs dummy 湊數 |

---

## 任務需求（Christ 在 Discord 給 Victoria 的指令）

> 針對 AiTeam 專案中，Dashboard 內『規則管理頁面』的表格做修改：
> 1. 欄位『狀態』中，不要顯示 `啟用文字`
> 2. 欄位『操作』中，`編輯` 和 `刪除` 改成圖示按鈕，現在文字按鈕太佔版面了

附圖：規則管理頁面 screenshot（Stage 29-5 圖片附件功能首次大規模實測）。

**Christ 的設計用意**：用模糊需求 + 附圖測試 Victoria 理解能力，不指定 `MudIconButton` 或 `Icons.Material.Filled.Edit` 這類技術細節（Aria 原本建議精確版，Christ 改擬真版以還原真實使用情境）。

---

## 流程觀察 Checkpoints

### Checkpoint 1：Victoria CEO 決策確認 ⭐⭐⭐⭐⭐

**Victoria 回應**：
- 找到正確檔案路徑：`src/AiTeam.Dashboard/Components/Pages/Rules/RuleManagement.razor`
- 抓對元件 + 行號：狀態欄 L58-64、操作欄 L65-72
- **「啟用文字」歧義精準解讀**：推導出 `MudSwitch.Label` 屬性目前顯示「啟用」/「停用」，**移除整個 Label 屬性**（不只移除字串）
- 圖示常數指定：`Icons.Material.Filled.Edit` + `Delete`
- **保留 `Color.Error`**（刪除按鈕的紅色語意）— 細節敏感
- 智慧分類：`tech_improvement`（Stage 14 CEO 分類能力）

**觀察點**：
- ⚠️ Victoria **已做 Designer 工作**——spec 已到 Cody 級精細度（檔案路徑 + 行號 + 元件 + 圖示常數 + 顏色保留）
- ⚠️ 對「啟用文字」歧義 **沒主動追問** Christ，直接腦補解讀（這次是「精確腦補」推得對，不是「快路徑繞道」）

**評分**：對小工程而言**高品質 8.5/10**。

### Checkpoint 2：Agent 執行確認 + tech_improvement 流程

按下「確認派工」後出現第二個 confirmation：「Agent 執行確認」（雙重確認設計）。

**Aria 重要發現**：`tech_improvement` workflow **跳過 Kickoff + Design 階段**，所以 **Demi 完全不會出現**（無法驗證原本要觀察的「Demi 過度設計」維度——留作下次 `new_feature` 任務試驗）。

新流程對照：
| WorkflowType | 流程路徑 |
|--------------|---------|
| `new_feature` | Kickoff 5 人 → Design（Demi）→ Dev_plan → Dev → Reviewer → QA → Doc |
| **`tech_improvement`**（本任務） | **直接 Dev_plan → Petra → Dev → Reviewer → QA**（跳過 Kickoff、Design、Doc） |

### Checkpoint 3：Cody Dev_plan + Dev

**首頁 Agent 狀態卡 bug**：Christ 觀察到 **Dev chip 顯示「閒置」但 expand 顯示「規則管理頁表... 已跑 40s」running** —— 兩個資料來源邏輯不一致：
- Chip 看 SemaphoreGroups 的 `Dev` 是否被佔用 → Dev_plan 不算 → 閒置
- Expand 看 TaskItem.AssignedAgent → 包含 Dev_plan → 顯示 running

**對應**：[FF 二十二](../planning/Future_Feature.md) 子項 B「Bot PushAgentStatus 名稱語意清理」的具體案例。

### Checkpoint 4：Reviewer 失敗（**設計缺口浮現**）⚠️

**錯誤訊息**：
```
PR #108 未包含 .cs 檔案，略過 Reviewer
```

**根因**（[`ReviewerAgentService.cs:108-109`](../../src/AiTeam.Bot/Agents/ReviewerAgentService.cs:108)）：
```csharp
if (csFiles.Count == 0)
    return Fail(task, $"PR #{prNumber} 未包含 .cs 檔案，略過 Reviewer");
```

對比 [`QaAgentService.cs:111`](../../src/AiTeam.Bot/Agents/QaAgentService.cs:111)：
```csharp
if (!hasUiChanges && csFiles.Count == 0)  // Quinn 有檢查 hasUiChanges（razor/css）
    return new AgentExecutionResult(false,
        $"PR #{prNumber} 未包含可測試的 .cs / .razor / .css 檔案，略過 QA");
```

**Vera 只審 `.cs`，純 `.razor` PR 直接略過。Quinn 設計有檢查 razor/css 但 Vera 沒對齊。**

#### 對 Top 1 / Top 2 驗證的衝擊

- **Top 1（Vera/Quinn 行為品質）**：Vera 沒跑 → **無法驗證 fix 是否有效**
- **Top 2（Petra Warning 寬鬆）**：沒 review report → Petra 沒仲裁 → **無法驗證**

**這是試驗 v2 設計時沒預料到的限制**——任務挑了純 UI（FF 十的微小子項），剛好觸到 Vera 的設計排除範圍。

⚠️ **狀態錯誤分類**：流程詳情顯示「失敗 + 重試」按鈕，但「沒 .cs 略過」應該標 `skipped` 不是 `failed`（重試也救不了）。

### Checkpoint 5：QA (Quinn) ⭐⭐⭐⭐⭐ Top 3 部分驗證

**Quinn 表現**：
- 正確識別「**0 個 .cs + 1 個 UI 檔**」
- 產出 **10 個 Playwright 視覺截圖測試**：完整頁面 / 標題與按鈕區塊 / MudTable 規則表格 / **IconButton 編輯+刪除細節** / 空狀態畫面，每個維度 **light + dark mode 雙截圖**
- `dotnet build` 0 Error
- branch 命名語意化：`feature/rules-ui-tweak-icon-buttons`
- TestClass 中文名稱：`PR108_規則管理頁面視覺截圖測試`
- Dark mode 切換有 **JavaScript fallback**（找不到 toggle 時直接 evaluate `classList.add('dark')`）— 思慮周全

**Top 3 驗證結論**：
- ✅ 測試覆蓋實際變更的 UI 區塊（IconButton 編輯+刪除）不是「公版頁面截圖」
- ✅ 涵蓋邊界場景（空狀態畫面）
- ✅ 是 dummy 湊數測試的反面範例
- **CLAUDE_QA.md 的「測試品質優先於數量」原則確實被遵守**

**無法區分的歸因**：Quinn 一直能跑這樣的 Playwright 測試嗎？還是 FF 二十四 fix 才有？沒對照基線。**至少證明 fix 後 Quinn 仍能維持高品質**。

### Checkpoint 6：PR #108 內容（Aria 代為 Code Review）

#### 改動內容

**改動 1**：MudSwitch 移除整個 `Label` 屬性 ✅（照 spec）
**改動 2**：MudButton → MudIconButton + 保留 `Color.Error` ✅（照 spec）

#### ⭐ 三個超出 spec 的加分項

1. **加 `MudTooltip`**（hover 顯示「編輯」/「刪除」）— **a11y 最佳實踐**（圖示按鈕沒文字 → hover tooltip 補語意）
2. **`MudStack Spacing` 從 `2` 改成 `1`**— 改 IconButton 後元素變小、Spacing 應該也要縮 → 視覺更緊湊
3. **使用 `Icons.Material.Outlined`**（非 spec 指定的 `Filled`）— 輕微偏離 spec，但 Outlined 風格在表格內細按鈕場景視覺更輕盈，可能 grep codebase 後判斷的

#### ⚠️ 一個 a11y 隱患

**MudSwitch 移除 Label 後沒補替代描述**：
- 視障使用者看不到「Switch 的語意」
- 應補 `aria-label="@(context.IsActive ? "已啟用" : "已停用")"` 或類似
- **這就是 Vera 設計缺口（純 razor 略過）的真實代價**——如果 Vera 有跑，這個應該標 Warning

**Cody 表現評分**：⭐⭐⭐⭐ 4/5（兩處 spec 100% 對 + 三個加分項 + 一個 a11y 隱患）

---

## 試驗結果矩陣

| 維度 | 結果 | Top 修復驗證 |
|------|------|------------|
| Victoria CEO 決策 | ⭐⭐⭐⭐⭐ | （非試驗目標） |
| Cody 實作（Dev_plan + Dev） | ⭐⭐⭐⭐ | （非試驗目標） |
| **Vera review** | ❌ **略過**（設計缺口）| **Top 1 ❓ 無法驗證** |
| **Petra 仲裁** | ❌ 沒跑（沒 review 報告） | **Top 2 ❓ 無法驗證** |
| **Quinn test** | ⭐⭐⭐⭐⭐ 高品質 | **Top 3 ✅ 部分通過** |
| Sage Doc | 跳過（tech_improvement 流程） | — |

---

## 試驗發現（需修清單）

| # | 發現 | 嚴重度 | 對應 FF | 修法 |
|---|------|--------|--------|------|
| 1 | **Vera 略過純 .razor PR**（[`ReviewerAgentService.cs:108`](../../src/AiTeam.Bot/Agents/ReviewerAgentService.cs:108)）| 🟡 中（純 UI 任務 review 缺位）| 新 **FF 二十八** | 加 `hasUiChanges` 檢查（對齊 [`QaAgentService.cs:111`](../../src/AiTeam.Bot/Agents/QaAgentService.cs:111) 邏輯）|
| 2 | **BossInteraction.Description 缺 Task.Description**（[`CommandHandler.cs:195`](../../src/AiTeam.Bot/Discord/CommandHandler.cs:195)）| 🟡 中（Dashboard UX 不平等）| 列為下個 Stage 搭車修候選 | 1 行 code：`description: ceoResponse.Task?.Description ?? ceoResponse.Reply ?? userInput` |
| 3 | **Dev/Dev_plan chip vs expand 不一致** | 🔵 低（UI 顯示矛盾）| FF 二十二 子項 B（補具體案例）| Chip / Expand 兩個資料來源對齊 |
| 4 | **MudSwitch 移除 Label 後 a11y 缺替代描述** | 🔵 低（PR #108 範圍內）| 與 #1 連動（Vera 該抓）| `aria-label` 補上，搭車或下個 UI 打磨修 |
| 5 | **Reviewer 略過時狀態錯標 `failed`**（應為 `skipped`）| 🔵 低（UI 顯示誤導）| 與 #1 同 commit | 拋自定義 result 讓 Processor 標 `skipped` |

---

## 三個歸因觀察

### Top 1（CLAUDE_*.md COPY 漏）— ❓ 本次未驗證

PR #108 是純 razor → Vera 直接略過，**沒走到 review 環節**。需要試驗 v3 用會動 .cs 的任務再驗。

### Top 2（Vera 偏好放行 + Petra 寬鬆）— ❓ 本次未驗證

同 Top 1 — Vera 沒跑、Petra 沒仲裁。

### Top 3（CLAUDE_QA.md 偏量不偏質）— ✅ 部分驗證

Quinn 在 FF 二十四 fix 後**確實寫了實質的 Playwright 視覺測試**（10 個截圖涵蓋多維度 + 邊界場景 + dark mode fallback），不是 dummy 湊數。

但**沒有 v1（FF 二十四 fix 前）的對照**，無法 100% 確認是「fix 才有」還是「一直就有」。最多說「fix 後品質仍維持高水準」。

---

## 試驗 v3 規劃建議

### 任務候選

需要**會動 .cs 檔**的小工程，才能補 Top 1 / Top 2 驗證：

| 候選 | 規模 | 為什麼適合 |
|------|------|----------|
| **FF 十：流程追蹤頁面 PR 欄位顯示優化** | XS-S | 會動 razor.cs 處理 link generation（GitHub URL parse → `#999` 短連結）|
| **FF 十：任務列表新增專案/Agent/觸發來源篩選** | S-M | 會動 razor.cs + DTO + service 邏輯 |
| **FF 二十二 子項 A：AgentConfigService 命名守門** | XS | 純 .cs（DashboardAgentService.cs），純驗證 Top 1/2 場景 |

### 觀察重點對照

試驗 v3 需要明確觀察：
- **Vera review report 內容品質**（Critical/Warning 分級準確度）
- **Vera 是否抓到「重複邏輯/硬編碼預設值/多份 config 維護」類議題**（Top 2 預期被 Critical 還是 Warning）
- **Petra 對 Warning 是否有閾值改進**

### 試驗 v3 前置條件（建議）

1. **修 FF 二十八（Vera razor/css 支援）後再試 v3** — 否則純 UI 任務還是會略過
2. **保留 FF 二十五（self-implement prompt 守則）** 作為 v3 任務 prompt 的設計指引

---

## 後續行動清單

立即（試驗結案後）：
- [x] 寫試驗 v2 紀錄（本檔）
- [ ] 更新 FF 二十七：標部分驗證 + 連結到本檔
- [ ] 新增 FF 二十八：Vera 審查範圍擴及 .razor / .css
- [ ] FF 二十二 子項 B 補具體案例（Dev/Dev_plan chip）
- [ ] FF header v7.37 → v7.38 + changelog
- [ ] commit + push

下個 Stage（Stage 39 / 候選）搭車修：
- BossInteraction.Description 缺 Task.Description（CommandHandler.cs:195）
- Reviewer 略過時狀態錯標 failed
- MudSwitch a11y aria-label（搭車 UI 打磨）

未來規劃：
- 試驗 v3 任務選定（建議會動 .cs 的 FF 十子項或 FF 二十二 子項 A）
- 試驗 v3 前置條件：FF 二十八 修完再做

---

## 結論

**試驗 v2 部分成功**：
- ✅ Top 3（Quinn 測試品質）部分驗證為正
- ❓ Top 1 / Top 2 本次未驗證（任務性質繞過 Vera/Petra）
- ⭐ 五個新發現（一個重要設計缺口 + 兩個 UX bug + 一個 a11y 隱患 + 一個錯誤狀態分類）

**對 self-implement 戰略的意義**：
1. **AiTeam 對純 UI 任務的把關不完整**（Vera 不審 razor）— 這是真實設計問題，不是 prompt 問題
2. **Quinn 對 razor/css 變更已能跑高品質視覺測試** — 這是系統優勢
3. **試驗結果不能單看任務 PR merge 與否** — 任務雖然能 merge，但 review 環節缺位 = a11y 等品質問題流入 production

**對下一步試驗的意義**：v3 一定要挑會動 .cs 的任務 + 先修 FF 二十八，才能補完 Top 1 / Top 2 驗證。
