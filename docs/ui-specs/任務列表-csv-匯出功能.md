# UI 規格文件：任務列表 CSV 匯出功能

---

## 1. 頁面目的

在任務中心頁面提供 CSV 匯出入口，讓使用者可將任務列表資料（全部或依當前篩選條件）下載為 CSV 檔案，以便進行離線分析或報表製作。

---

## 2. 頁面結構

本功能不新增獨立頁面，而是在**現有任務中心頁面**的工具列（Toolbar）區塊新增匯出控制項。

### 整體佈局調整描述

```
┌─────────────────────────────────────────────────────┐
│  任務中心                                            │
├─────────────────────────────────────────────────────┤
│  [搜尋欄]  [狀態篩選]  [Agent篩選]  ... │ [匯出按鈕▼]│  ← Toolbar 區
├─────────────────────────────────────────────────────┤
│  任務列表（MudDataGrid）                             │
│  ...                                                 │
└─────────────────────────────────────────────────────┘
```

- **匯出按鈕** 置於 Toolbar 最右側，與現有篩選元件同行
- 點擊後展開 **下拉選單**，提供兩個選項：「匯出全部」與「匯出篩選結果」
- 匯出進行中時，按鈕區域顯示 **行內載入指示器**（不使用全頁 Loading）

---

## 3. 元件規格

### 3-1. 匯出觸發按鈕組：`MudButtonGroup` + `MudMenu`

採用 **Split Button 模式**（主按鈕 + 下拉箭頭分離），以明確區分兩種匯出模式。

| 屬性 | 規格 |
|------|------|
| 元件 | `MudButtonGroup`，內含一個 `MudButton` 與一個 `MudMenu`（箭頭觸發） |
| `Variant` | `Variant.Outlined` |
| `Size` | `Size.Medium` |
| 主按鈕文字 | `匯出篩選結果`（預設行為，對應當前篩選狀態） |
| 主按鈕 Icon | `Icons.Material.Filled.FileDownload`，置於文字左側 |
| 下拉箭頭 Icon | `Icons.Material.Filled.ArrowDropDown` |
| `Disabled` 條件 | 匯出作業進行中（`isExporting == true`）時整組 Disabled |

### 3-2. 下拉選單：`MudMenu`（附屬於箭頭按鈕）

| `MudMenuItem` | 說明 |
|---------------|------|
| 匯出篩選結果 | 依當前搜尋／篩選條件查詢並匯出；若無任何篩選條件，行為等同「匯出全部」 |
| 匯出全部 | 忽略所有篩選條件，匯出完整任務資料 |

> **備註**：選單項目前方加上對應小 Icon 增加識別性：
> - 匯出篩選結果：`Icons.Material.Outlined.FilterAlt`
> - 匯出全部：`Icons.Material.Outlined.SelectAll`

### 3-3. 進行中狀態：`MudProgressCircular`

| 屬性 | 規格 |
|------|------|
| 元件 | `MudProgressCircular` |
| `Size` | `Size.Small` |
| `Indeterminate` | `true` |
| 顯示條件 | `isExporting == true` 時，取代主按鈕的 Icon 位置顯示 |
| 伴隨文字 | 主按鈕文字改為「匯出中…」 |

### 3-4. 筆數提示：`MudTooltip`

- 包裹整個 `MudButtonGroup`
- `Tooltip` 內容動態顯示：
  - 有篩選條件時：`目前篩選結果共 {filteredCount} 筆`
  - 無篩選條件時：`共 {totalCount} 筆任務`
- `Placement`：`Placement.Bottom`

### 3-5. 結果回饋：`MudSnackbar`

| 情境 | 訊息內容 | Severity |
|------|----------|----------|
| 匯出成功 | `已成功匯出 {n} 筆任務（{fileName}）` | `Severity.Success` |
| 匯出結果為空 | `篩選結果為空，無可匯出的資料` | `Severity.Warning` |
| 匯出失敗 | `匯出失敗，請稍後再試` | `Severity.Error` |

- 使用現有專案的 `ISnackbar` service 注入，不額外新增元件
- `VisibleStateDuration`：4000ms

---

## 4. 資料來源

### 4-1. 匯出篩選結果

- **來源**：呼叫與當前 `MudDataGrid` 相同的資料查詢服務方法
- **參數**：直接傳入現有頁面 State 中已綁定的篩選參數物件（搜尋關鍵字、狀態、Agent、專案、觸發來源、日期區間等）
- **分頁處理**：匯出時**忽略分頁參數**，取得全量符合條件的資料（`pageSize = int.MaxValue` 或呼叫專用的「不分頁」查詢方法，詳見注意事項）

### 4-2. 匯出全部

- **來源**：同上查詢服務，但傳入**空白（預設）篩選參數**
- **分頁處理**：同上，忽略分頁

### 4-3. CSV 產生

- **責任歸屬**：由後端 Service 或 Blazor Server 端處理，不在瀏覽器端產生
- **欄位對應**：

| CSV 欄位名稱（Header） | 對應資料屬性 | 備註 |
|------------------------|--------------|------|
| 任務標題 | `Task.Title` | |
| 狀態 | `Task.Status` | 輸出人類可讀的中文標籤，如「執行中」、「已完成」 |
| Agent | `Task.AgentName` | |
| 專案 | `Task.ProjectName` | 無專案時輸出空字串 |
| 觸發來源 | `Task.TriggerSource` | |
| 建立時間 | `Task.CreatedAt` | 格式：`yyyy-MM-dd HH:mm:ss` |
| 完成時間 | `Task.CompletedAt` | 未完成時輸出空字串 |

- **檔案命名規則**：
  - 匯出篩選結果：`tasks_filtered_{yyyyMMddHHmm}.csv`
  - 匯出全部：`tasks_all_{yyyyMMddHHmm}.csv`
- **編碼**：UTF-8 with BOM（確保 Excel 開啟時中文不亂碼）

### 4-4. 檔案下載觸發

- 透過 `IJSRuntime` 呼叫 JS interop，將後端產生的 CSV byte array 以 Blob 方式觸發瀏覽器下載
- 不導頁、不開新分頁

---

## 5. 互動行為

### 5-1. 主流程：點擊「匯出篩選結果」

1. 使用者點擊主按鈕（或從下拉選單選擇「匯出篩選結果」）
2. 按鈕組進入 Disabled 狀態，主按鈕 Icon 替換為 `MudProgressCircular`，文字改為「匯出中…」
3. 前端呼叫後端匯出方法，傳入當前篩選參數
4. **後端**查詢資料 → 產生 CSV byte array → 回傳至前端
5. 前端透過 JS Interop 觸發瀏覽器下載
6. 按鈕組恢復正常狀態，顯示成功 Snackbar

### 5-2. 主流程：點擊「匯出全部」

- 流程同 5-1，差異僅在步驟 3 傳入空白篩選參數

### 5-3. 匯出結果為空

- 後端查詢結果筆數為 0 時，**不產生 CSV 檔**，直接回傳空結果訊號
- 前端顯示 Warning Snackbar，按鈕組恢復正常

### 5-4. 匯出失敗（例外處理）

- 後端拋出例外或回傳錯誤時，前端顯示 Error Snackbar
- 按鈕組恢復正常，不殘留 Loading 狀態

### 5-5. 防重複觸發

- `isExporting` flag 為 `true` 期間，整個 `MudButtonGroup` 設為 `Disabled`
- 確保使用者無法在匯出進行中重複觸發

### 5-6. 篩選條件變更時的 Tooltip 更新

- 每次篩選條件異動（`OnFilterChanged` 等事件）後，重新計算 `filteredCount` 並更新 Tooltip 內容
- 此計算可複用現有的 DataGrid 資料計數，**不額外發出查詢請求**

---

## 6. 注意事項

### 6-1. 大量資料效能考量
- 匯出全部情境下，若任務總筆數可能超過 **10,000 筆**，需與後端確認是否採用串流寫入（`IAsyncEnumerable`），避免一次性載入大量資料至記憶體
- 建議後端匯出方法加上**逾時保護**（例如 30 秒），超時時回傳錯誤

### 6-2. Blazor Server 電路（Circuit）考量
- JS Interop 下載呼叫必須在 Blazor 電路存活期間執行；若使用者在匯出完成前關閉頁面，應在後端服務中妥善處理 `OperationCanceledException`
- `isExporting` 狀態變更後必須呼叫 `StateHasChanged()`，確保 UI 即時反映（特別是在 `async` 方法的 `await` 前後）

### 6-3. JS Interop 檔案下載
- 建議封裝為專案共用的 `IFileDownloadService`（或類似命名），而非在頁面元件中直接散落 JS Interop 呼叫
- 若專案中已有類似工具方法，優先複用，不重複建立

### 6-4. 「不分頁查詢」方法設計
- 避免在現有分頁查詢方法上以 `pageSize = int.MaxValue` 方式取得全量資料（可能影響 ORM 生成的 SQL）
- 建議後端提供獨立的 `ExportTasksAsync(FilterParams filter)` 方法，與列表查詢邏輯解耦

### 6-5. 狀態文字本地化
- CSV 中的「狀態」欄位輸出需與 UI 顯示保持一致，建議複用現有的狀態對照表（如 `enum` extension method 或 resource 檔），避免硬編碼字串

### 6-6. 權限控管
- 若任務中心頁面有角色權限控制，匯出按鈕的顯示／隱藏應遵循相同的授權邏輯，建議以 `<AuthorizeView>` 包裹或在 `OnInitializedAsync` 中判斷