# PR #76 技術文件：任務中心執行耗時顯示

## 概覽

本 PR 為任務中心頁面新增「執行耗時」欄位，透過在 `TaskItemDto` 加入計算屬性 `Duration`，並於 `TaskCenter` 元件中新增格式化顯示邏輯，讓使用者可直觀看到每筆任務的執行時間。

---

## 檔案一覽

| 檔案 | 類型 | 說明 |
|------|------|------|
| `src/AiTeam.Shared/Dtos/TaskItemDto.cs` | DTO | 新增 `Duration` 計算屬性 |
| `src/AiTeam.Dashboard/Components/Pages/Tasks/TaskCenter.razor.cs` | Blazor Code-behind | 新增 `FormatDuration()` 格式化方法 |
| `src/AiTeam.Tests.Playwright/Generated/PR76/VisualTests.cs` | E2E 視覺測試 | Playwright 截圖驗證 |
| `tests/Generated/src/AiTeam.Shared/Dtos/TaskItemDtoTests.cs` | 單元測試 | `TaskItemDto` 屬性與 `Duration` 邏輯測試 |
| `tests/Generated/src/AiTeam.Dashboard/Components/Pages/Tasks/TaskCenter.razorTests.cs` | 單元測試 | `FormatDuration()` 邊界值與格式測試 |

---

## 1. `TaskItemDto.cs`

**路徑：** `src/AiTeam.Shared/Dtos/TaskItemDto.cs`  
**命名空間：** `AiTeam.Shared.Dtos`

任務列表顯示用 DTO，不含 Logs（避免資料量過大）。

### 屬性清單

| 屬性 | 型別 | 說明 |
|------|------|------|
| `Id` | `Guid` | 任務唯一識別碼 |
| `Title` | `string` | 任務標題，預設為空字串 |
| `TriggeredBy` | `string` | 觸發者，預設為空字串 |
| `AssignedAgent` | `string` | 指派 Agent 名稱，預設為空字串 |
| `Status` | `string` | 任務狀態，預設為空字串 |
| `CreatedAt` | `DateTime` | 建立時間 |
| `CompletedAt` | `DateTime?` | 完成時間（可為 null） |
| `ProjectName` | `string?` | 所屬專案名稱（可為 null） |
| `TeamName` | `string?` | 所屬團隊名稱（可為 null） |
| `Duration` | `TimeSpan?` | **計算屬性**（PR #76 新增） |

### `Duration` 計算屬性

```csharp
public TimeSpan? Duration =>
    CompletedAt.HasValue ? CompletedAt.Value - CreatedAt : null;
```

- **有值條件：** `CompletedAt` 不為 null
- **計算方式：** `CompletedAt - CreatedAt`
- **未完成任務：** 回傳 `null`
- **精度：** 支援毫秒級差值

---

## 2. `TaskCenter.razor.cs`

**路徑：** `src/AiTeam.Dashboard/Components/Pages/Tasks/TaskCenter.razor.cs`  
**命名空間：** `AiTeam.Dashboard.Components.Pages.Tasks`  
**實作介面：** `IAsyncDisposable`

### 依賴注入

| 屬性 | 型別 | 說明 |
|------|------|------|
| `TaskService` | `DashboardTaskService` | 任務資料存取服務 |
| `Navigation` | `NavigationManager` | Blazor 導覽管理 |
| `Configuration` | `IConfiguration` | 組態讀取 |

### 私有欄位

| 欄位 | 型別 | 說明 |
|------|------|------|
| `_tableRef` | `MudTable<TaskItemDto>` | MudBlazor Table 參考 |
| `_selectedTask` | `TaskItemDto?` | 目前選取的任務 |
| `_selectedLogs` | `List<TaskLogDto>` | 選取任務的 Log 清單 |
| `_isDrawerOpen` | `bool` | 側邊抽屜開關狀態 |
| `_statusFilter` | `string?` | 狀態篩選條件 |
| `_hubConnection` | `HubConnection?` | SignalR 連線 |

### 方法說明

#### `OnInitializedAsync()`
生命週期方法，初始化時建立 SignalR 連線。

#### `ConnectSignalRAsync()`
建立 SignalR Hub 連線：
- Hub URL 優先讀取 `Configuration["Dashboard:HubBaseUrl"]`，若未設定則使用 `NavigationManager` 解析相對路徑 `/hubs/agent-status`
- 啟用自動重連（`WithAutomaticReconnect()`）
- 訂閱 `AgentStatusHub.ReceiveTaskUpdate` 事件，收到更新時自動呼叫 `_tableRef.ReloadServerData()`

#### `LoadServerDataAsync(TableState, CancellationToken)`
MudTable `ServerData` 回呼：
- `state.Page` 為 0-indexed，API 為 1-indexed，故轉換時 `+1`
- 傳入 `_statusFilter` 供伺服器端篩選

#### `OnRowClickAsync(TableRowClickEventArgs<TaskItemDto>)`
點擊資料列時：
1. 設定 `_selectedTask`
2. 呼叫 `TaskService.GetTaskLogsAsync()` 載入 Logs
3. 開啟側邊抽屜（`_isDrawerOpen = true`）

#### `FormatDuration(TimeSpan?)` *(PR #76 新增)*

靜態私有方法，將 `TimeSpan?` 格式化為中文人類易讀字串：

| 條件 | 回傳格式 | 範例 |
|------|----------|------|
| `null` | `"—"` | — |
| `TotalSeconds < 60` | `"N 秒"` | `45 秒` |
| `TotalMinutes < 60`，有秒數 | `"N 分 N 秒"` | `3 分 42 秒` |
| `TotalMinutes < 60`，無秒數 | `"N 分"` | `10 分` |
| `TotalMinutes >= 60`，有分鐘 | `"N 時 N 分"` | `1 時 5 分` |
| `TotalMinutes >= 60`，無分鐘 | `"N 時"` | `2 時` |

> 注意：小時判斷使用 `ts.Minutes`（不含秒），因此 `1:00:30`（1 小時 0 分 30 秒）回傳 `"1 時"` 而非 `"1 時 0 分"`。

#### `DisposeAsync()`
釋放 SignalR 連線資源。

---

## 3. Playwright 視覺截圖測試

**路徑：** `src/AiTeam.Tests.Playwright/Generated/PR76/VisualTests.cs`  
**命名空間：** `AiTeam.Tests.Playwright.Generated`  
**測試框架：** MSTest + Playwright  
**測試類別：** `PR76_TaskCenter視覺截圖測試`

### 環境變數

| 變數 | 預設值 | 說明 |
|------|--------|------|
| `DASHBOARD_URL` | `http://localhost:5051` | Dashboard 根 URL |
| `DASHBOARD_USER` | 空字串 | 登入帳號 |
| `DASHBOARD_PASS` | 空字串 | 登入密碼 |

截圖輸出目錄：`screenshots/`，目標頁面路徑：`/Tasks/TaskCenter`

### 測試案例

| 測試方法 | 截圖檔名 | 說明 |
|----------|----------|------|
| `任務中心頁面_亮色模式_截圖驗證` | `PR76_TaskCenter_light_mode.png` | 整頁亮色截圖 |
| `任務中心頁面_暗色模式_截圖驗證` | `PR76_TaskCenter_dark_mode.png` | 整頁暗色截圖 |
| `任務中心頁面_亮色模式_任務列表區塊截圖驗證` | `PR76_TaskCenter_light_task_list.png` | 任務列表區塊亮色截圖 |
| `任務中心頁面_暗色模式_任務列表區塊截圖驗證` | `PR76_TaskCenter_dark_task_list.png` | 任務列表區塊暗色截圖 |
| `任務中心頁面_亮色模式_頁面標題與頁首截圖驗證` | `PR76_TaskCenter_light_header.png` | 頁首亮色截圖 |
| `任務中心頁面_暗色模式_頁面標題與頁首截圖驗證` | `PR76_TaskCenter_dark_header.png` | 頁首暗色截圖 |
| `任務中心頁面_亮色模式_篩選與操作區塊截圖驗證` | `PR76_TaskCenter_light_filter_bar.png` | 篩選區塊亮色截圖 |
| `任務中心頁面_暗色模式_篩選與操作區塊截圖驗證` | `PR76_TaskCenter_dark_filter_bar.png` | 篩選區塊暗色截圖 |

所有截圖測試驗證：
1. 截圖檔案確實存在
2. 檔案大小大於 0

區塊級截圖採用 fallback 策略：若目標 Locator 不可見，自動改為全頁截圖（檔名加 `_fallback` 後綴）。

### `切換暗色模式()` 輔助方法
優先點擊頁面上的暗色模式切換按鈕；若找不到可見控制項，則透過 JavaScript 直接操作 DOM 設定 `dark` class 與 `data-theme` 屬性。

---

## 4. `TaskItemDtoTests.cs`

**路徑：** `tests/Generated/src/AiTeam.Shared/Dtos/TaskItemDtoTests.cs`  
**命名空間：** `AiTeam.Shared.Tests.Dtos`  
**測試框架：** xUnit + FluentAssertions

### 測試分組

#### 屬性預設值測試
| 測試 | 驗證目標 |
|------|----------|
| `建立新實例_無設定任何屬性_字串屬性應為空字串` | `Title`、`TriggeredBy`、`AssignedAgent`、`Status` 預設為 `""` |
| `建立新實例_無設定任何屬性_可為Null屬性應為Null` | `CompletedAt`、`ProjectName`、`TeamName` 預設為 `null` |
| `建立新實例_無設定任何屬性_Id應為空Guid` | `Id` 預設為 `Guid.Empty` |

#### `Duration` 屬性測試
| 測試 | 情境 | 期望結果 |
|------|------|----------|
| `Duration_CompletedAt有值_應回傳差值` | CreatedAt=10:00, CompletedAt=12:30 | `2.5 小時` |
| `Duration_CompletedAt為Null_應回傳Null` | CompletedAt 未設定 | `null` |
| `Duration_CompletedAt與CreatedAt相同_應回傳零TimeSpan` | 相同時間點 | `TimeSpan.Zero` |
| `Duration_CompletedAt早於CreatedAt_應回傳負值TimeSpan` | 完成時間早於建立時間 | `-2 小時` |
| `Duration_耗時跨越多天_應正確計算差值` | 跨越 3 天 6 小時 | `78 小時` |

#### `Duration` 精確度測試
| 測試 | 說明 |
|------|------|
| `Duration_耗時包含毫秒_應精確計算差值` | 驗證毫秒級精度（1500 ms） |
| `Duration_多次讀取_應回傳相同值` | 計算屬性冪等性驗證 |

#### 屬性設定與讀取測試
| 測試 | 說明 |
|------|------|
| `設定所有屬性_讀取時_應回傳相同值` | 所有屬性完整 round-trip 驗證 |
| `設定ProjectName與TeamName為Null_讀取時_應回傳Null` | nullable 屬性寫入 null 後可讀回 null |
| `設定Status為特定字串_讀取時_應回傳相同字串` | 字串屬性 round-trip |
| `設定Id為新Guid_讀取時_應回傳相同Guid` | Guid 屬性 round-trip |

---

## 5. `TaskCenter.razorTests.cs`

**路徑：** `tests/Generated/src/AiTeam.Dashboard/Components/Pages/Tasks/TaskCenter.razorTests.cs`  
**命名空間：** `AiTeam.Dashboard.Tests.Components.Pages.Tasks`  
**測試框架：** xUnit + FluentAssertions  
**測試類別：** `TaskCenterTests`

> SignalR / Blazor 相關整合行為（`OnInitializedAsync`、`ConnectSignalRAsync` 等）需完整 Blazor 執行環境，故以整合測試處理，此處不涵蓋。

透過 Reflection 呼叫私有靜態方法：

```csharp
private static string InvokeFormatDuration(TimeSpan? duration)
{
    var method = typeof(TaskCenter).GetMethod(
        "FormatDuration",
        BindingFlags.NonPublic | BindingFlags.Static)!;
    return (string)method.Invoke(null, new object?[] { duration })!;
}
```

### `FormatDuration` 測試案例

#### Null 輸入
| 測試 | 輸入 | 期望輸出 |
|------|------|----------|
| `FormatDuration_傳入Null_應回傳破折號` | `null` | `"—"` |

#### 秒數格式（< 60 秒）
| 測試 | 輸入 | 期望輸出 |
|------|------|----------|
| `FormatDuration_傳入小於60秒的時間_應回傳秒格式` | 45 秒 | `"45 秒"` |
| `FormatDuration_傳入零秒_應回傳零秒格式` | 0 秒 | `"0 秒"` |
| `FormatDuration_傳入恰好59秒_應回傳秒格式` | 59 秒 | `"59 秒"` |

#### 分鐘格式（>= 60 秒，< 60 分）
| 測試 | 輸入 | 期望輸出 |
|------|------|----------|
| `FormatDuration_傳入分鐘含秒數_應回傳分秒格式` | 3 分 42 秒 | `"3 分 42 秒"` |
| `FormatDuration_傳入整數分鐘無秒數_應回傳純分格式` | 10 分 | `"10 分"` |
| `FormatDuration_傳入恰好1分鐘_應回傳純分格式` | 1 分 | `"1 分"` |
| `FormatDuration_傳入59分59秒_應回傳分秒格式` | 59 分 59 秒 | `"59 分 59 秒"` |

#### 小時格式（>= 60 分）
| 測試 | 輸入 | 期望輸出 |
|------|------|----------|
| `FormatDuration_傳入小時含分鐘_應回傳時分格式` | 1 時 5 分 | `"1 時 5 分"` |
| `FormatDuration_傳入整數小時無分鐘_應回傳純時格式` | 2 時 | `"2 時"` |
| `FormatDuration_傳入超過24小時_應回傳時格式` | 25 時 30 分 | `"25 時 30 分"` |
| `FormatDuration_傳入恰好1小時_應回傳純時格式` | 1 時 | `"1 時"` |
| `FormatDuration_傳入小時含秒數但無分鐘_分鐘為零應回傳純時格式` | 1:00:30 | `"1 時"` |

#### 邊界值
| 測試 | 輸入 | 期望輸出 | 說明 |
|------|------|----------|------|
| `FormatDuration_傳入恰好60秒_應回傳純分格式而非秒格式` | 60 秒 | `"1 分"` | 邊界切換驗證 |
| `FormatDuration_傳入恰好60分鐘_應回傳純時格式而非分格式` | 60 分 | `"1 時"` | 邊界切換驗證 |

#### 回傳值型別驗證
| 測試 | 說明 |
|------|------|
| `FormatDuration_任意輸入_回傳值不應為Null` | 任意輸入皆不應回傳 null |
| `FormatDuration_任意輸入_回傳值不應為空字串` | 任意輸入皆不應回傳空字串 |