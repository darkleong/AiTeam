# UI 規格文件：Reviewer Agent 報告問題統計總結行

---

## 確認任務範圍

在輸出規格前，我需要先確認以下幾點以確保規格精準：

> **⚠️ 請 Christ 確認以下問題，再由我完成完整規格：**
>
> 1. **報告顯示位置**：Code Review 報告目前是顯示在哪個頁面？（例如：某個 Task Detail 頁面、獨立的 Review Report 頁面、還是嵌入在 Chat 訊息流中？）
> 2. **報告資料格式**：報告內容目前是純 Markdown 字串、HTML 字串、還是已解析成結構化物件（如有 `Severity` 欄位的 Issue List）？
> 3. **統計來源**：「🔴 嚴重問題」與「🟡 建議」的數量，是從後端 API 直接回傳已統計好的數字，還是前端需要自行 parse 報告內容字串來計算？
> 4. **報告是否為串流輸出**：Vera 產出報告時，是一次性回傳完整報告，還是透過 SignalR 串流逐步推送（Streaming）？這會影響總結行的顯示時機。
> 5. **總結行位置**：總結行是「附加在報告文字內容最後一行」，還是作為一個獨立的 UI 區塊（例如一個 Summary Card）顯示在報告區塊下方？

---

以下規格基於**最常見情境的合理假設**先行輸出，若確認後有出入，我會立即修訂。

**假設前提（請確認）：**
- 報告顯示於某個 Review Report 頁面或 Task Detail 子區塊內
- 報告內容為 Markdown 字串，🔴 / 🟡 是 emoji 前綴標記
- 統計數量由**前端 parse 報告字串**取得（因後端未提供結構化統計欄位）
- 報告為**一次性回傳**，非串流
- 總結行作為**獨立 UI 區塊**顯示在報告內容下方（非混入 Markdown 字串）

---

## 1. 頁面目的

在 Vera 產出的 Code Review 報告末尾，以獨立的統計總結區塊呈現本次審查的嚴重問題與建議數量，讓使用者能快速掌握報告結論，不需逐行閱讀完整報告。

---

## 2. 頁面結構

整體為報告顯示頁面（或 Task Detail 頁內的 Review 子區塊），結構由上至下分為三層：

```
┌─────────────────────────────────────────┐
│  報告標題列                              │
│  （例：Vera Code Review Report / 時間戳） │
├─────────────────────────────────────────┤
│  報告內容區塊                            │
│  （Markdown 渲染，含 🔴 🟡 條列問題）    │
├─────────────────────────────────────────┤
│  問題統計總結列  ← 本次新增              │
│  情境 A：共發現 N 個 🔴 嚴重問題、        │
│          M 個 🟡 建議                    │
│  情境 B：✅ 未發現任何問題               │
└─────────────────────────────────────────┘
```

- 報告內容區塊與統計總結列之間以 `MudDivider` 分隔
- 統計總結列在視覺上需與報告內文做出層次區隔，建議包裹於 `MudPaper` 或 `MudAlert` 內
- 統計總結列僅在報告完整載入後才顯示，載入中顯示 Skeleton 或隱藏

---

## 3. 元件規格

### 3-1. 分隔線

| 項目 | 規格 |
|------|------|
| 元件 | `MudDivider` |
| 位置 | 報告內容區塊與統計總結列之間 |
| Props | `Class="my-4"` |
| 行為 | 純視覺分隔，無互動 |

---

### 3-2. 統計總結列容器

| 項目 | 規格 |
|------|------|
| 元件 | `MudAlert` |
| 條件渲染 | 報告載入完成（`IsLoading == false`）且報告內容不為空時才顯示 |
| Severity（情境 A，有問題） | `Severity.Warning`（若有 🔴 嚴重問題）或 `Severity.Info`（若只有 🟡 建議） |
| Severity（情境 B，無問題） | `Severity.Success` |
| Props | `Dense="true"`、`Variant="Variant.Outlined"` |
| Icon | 情境 A 有嚴重問題：使用 `@Icons.Material.Filled.Error`；情境 A 只有建議：`@Icons.Material.Filled.Info`；情境 B：`@Icons.Material.Filled.CheckCircle` |

> **設計理由**：使用 `MudAlert` 而非純文字，是為了讓總結列在視覺上具備明確的狀態語意（成功/警告），開發者無需額外處理顏色邏輯，直接由 `Severity` 驅動。

---

### 3-3. 統計總結文字內容

| 情境 | 顯示文字格式 |
|------|-------------|
| 有嚴重問題（N > 0） | `共發現 {N} 個 🔴 嚴重問題、{M} 個 🟡 建議` |
| 只有建議（N = 0, M > 0） | `共發現 0 個 🔴 嚴重問題、{M} 個 🟡 建議` |
| 兩者皆為 0 | `✅ 未發現任何問題` |

- 文字元件使用 `MudText`，`Typo="Typo.body1"`
- N、M 為動態數值，以 `<strong>` 或 `MudText Inline="true" Typo="Typo.subtitle2"` 加粗顯示
- Emoji（🔴、🟡、✅）直接以 Unicode 字元嵌入文字，不使用圖示元件替換

---

### 3-4. 載入中狀態（報告尚未回傳時）

| 項目 | 規格 |
|------|------|
| 元件 | `MudSkeleton` |
| 條件 | `IsLoading == true` |
| Props | `SkeletonType="SkeletonType.Rectangle"`、`Height="40px"`、`Width="60%"` |
| 位置 | 佔據統計總結列的位置，載入完成後替換為實際 `MudAlert` |

---

## 4. 資料來源

| 資料項目 | 來源 | 說明 |
|----------|------|------|
| 完整報告字串 | API 回傳 / Component Parameter | 由父層或 API 服務注入，型別為 `string`（Markdown 原文） |
| 嚴重問題數量（N） | **前端計算** | Parse 報告字串，統計以 `🔴` 開頭的行（或以 `🔴` 作為行內前綴的項目）的數量 |
| 建議數量（M） | **前端計算** | Parse 報告字串，統計以 `🟡` 開頭的行的數量 |
| 報告載入狀態 | Component 內部狀態 | `bool IsLoading`，由 API 呼叫的 `async/await` 控制 |

### Parse 規則說明（給 Dev 的行為描述，非程式碼）

- 將報告字串以換行符（`\n`）切割成行陣列
- 對每一行執行 `Contains("🔴")` 判斷，符合者計入嚴重問題計數
- 對每一行執行 `Contains("🟡")` 判斷，符合者計入建議計數
- 上述計算應在報告字串完整回傳後執行一次，結果存入 Component 的 `int CriticalCount` 與 `int SuggestionCount` 狀態欄位

> **⚠️ 注意**：若後端日後改為回傳結構化統計欄位（如 `ReviewSummary.CriticalCount`），前端 parse 邏輯應移除，改由後端數值驅動，以避免計算不一致。此為技術債，建議在 PR 說明中標注。

---

## 5. 互動行為

### 5-1. 報告載入完成觸發統計

| 觸發時機 | 行為 |
|----------|------|
| API 回傳報告字串後 | 執行 Parse → 計算 CriticalCount、SuggestionCount → `IsLoading = false` → `StateHasChanged()` → 統計總結列顯示 |

### 5-2. 統計總結列顯示邏輯（條件渲染）

| 條件 | 顯示內容 |
|------|----------|
| `IsLoading == true` | 顯示 `MudSkeleton` |
| `IsLoading == false && ReportContent 為空或 null` | 隱藏統計總結列與分隔線（整個區塊不渲染） |
| `IsLoading == false && CriticalCount > 0` | 顯示 `MudAlert Severity.Warning`，文字為「共發現 N 個 🔴 嚴重問題、M 個 🟡 建議」 |
| `IsLoading == false && CriticalCount == 0 && SuggestionCount > 0` | 顯示 `MudAlert Severity.Info`，文字為「共發現 0 個 🔴 嚴重問題、M 個 🟡 建議」 |
| `IsLoading == false && CriticalCount == 0 && SuggestionCount == 0` | 顯示 `MudAlert Severity.Success`，文字為「✅ 未發現任何問題」 |

### 5-3. 無互動設計

- 統計總結列為**純唯讀顯示**，無點擊、展開、跳轉等互動行為
- 若未來需要「點擊嚴重問題數量跳至對應問題行」，需另開需求確認範圍

---

## 6. 注意事項

### Blazor Server 特殊考量

| 項目 | 說明 |
|------|------|
| `StateHasChanged()` 呼叫時機 | Parse 計算完成後需明確呼叫 `StateHasChanged()`，若在非 UI 執行緒（如 Task callback）中觸發，需改用 `InvokeAsync(() => StateHasChanged())` |
| 電路隔離 | Parse 邏輯為純字串計算，無資料庫或外部依賴，不存在跨電路資料污染風險 |
| 串流情境（若未來支援） | 若 Vera 改為 SignalR 串流推送報告，Parse 不應在每次 chunk 到達時重複執行，應等 `IsCompleted` 訊號後才觸發統計，避免 UI 閃爍與重複計算 |

### 效能注意

| 項目 | 說明 |
|------|------|
| Parse 執行頻率 | 僅在報告字串首次完整回傳時執行一次，不應放入 `OnParametersSet()` 或其他會頻繁觸發的生命週期 hook，除非確認報告字串有變更 |
| 大型報告字串 | 若報告超過 10,000 行（極端情境），字串切割與遍歷仍為 O(n) 線性複雜度，可接受，無需特殊優化 |

### 可維護性建議

| 項目 | 說明 |
|------|------|
| Parse 邏輯封裝 | 建議將 emoji 計數邏輯封裝為獨立的 private method（如 `CountEmoji(string report, string emoji)`），避免在 Component 中散落重複邏輯 |
| Emoji 字元常數化 | 🔴 與 🟡 的 Unicode 字元建議定義為 `private const string`，避免因複製貼上造成隱形字元差異而計算錯誤 |

---

> 📝 **規格版本**：v1.0 草稿（基於假設前提）
> 若實際報告格式、資料來源或顯示位置與假設不符，請回覆確認，我將針對差異點更新規格。