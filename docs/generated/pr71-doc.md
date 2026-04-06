# PR #71 技術文件：任務中心執行時間功能

## 概覽

本 PR 為任務中心頁面新增**執行時間（Duration）**欄位，並為進行中的任務實作**動態計時器**，讓使用者能即時觀察任務耗時。

涉及檔案：

| 檔案 | 專案 | 職責 |
|------|------|------|
| `AiTeam.Shared/Dtos/TaskItemDto.cs` | Shared | 新增 `Duration` 欄位 |
| `AiTeam.Dashboard/Services/DashboardTaskService.cs` | Dashboard | 查詢時計算並填入 `Duration` |
| `AiTeam.Dashboard/Components/Pages/Tasks/TaskCenter.razor.cs` | Dashboard | 計時器邏輯、格式化輸出 |

---

## 1. `TaskItemDto.cs`

**位置：** `src/AiTeam.Shared/Dtos/TaskItemDto.cs`

### 類別定義

```csharp
public class TaskItemDto
```

任務列表顯示用 DTO，不含 Logs，避免查詢資料量過大。

### 屬性清單

| 屬性 | 型別 | 說明 |
|------|------|------|
| `Id` | `Guid` | 任務唯一識別碼 |
| `Title` | `string` | 任務標題 |
| `TriggeredBy` | `string` | 觸發者（Discord 使用者） |
| `AssignedAgent` | `string` | 指派的 AI Agent |
| `Status` | `string` | 任務狀態字串 |
| `CreatedAt` | `DateTime` | 建立時間 |
| `CompletedAt` | `DateTime?` | 完成時間（nullable） |
| `ProjectName` | `string?` | 所屬專案名稱（nullable） |
| `TeamName` | `string?` | 所屬團隊名稱（nullable） |
| `Duration` | `TimeSpan?` | **【新增】** 執行時間；任務未完成時為 `null` |

### 設計決策

- `Duration` 使用 `TimeSpan?`，`null` 代表任務仍在執行中，讓前端能據此決定顯示靜態數值或動態計時器。
- DTO 刻意不含 Logs，依需求另行呼叫 `GetTaskLogsAsync()`。

---

## 2. `DashboardTaskService.cs`

**位置：** `src/AiTeam.Dashboard/Services/DashboardTaskService.cs`

### 類別定義

```csharp
public class DashboardTaskService(AppDbContext db)
```

Primary Constructor 注入 `AppDbContext`，所有查詢皆使用 `AsNoTracking()` 以提升效能。

### 方法

#### `GetTasksPagedAsync`

```csharp
public async Task<PagedResult<TaskItemDto>> GetTasksPagedAsync(
    int page = 1,
    int pageSize = 50,
    string? statusFilter = null,
    CancellationToken cancellationToken = default)
```

- 支援分頁（1-indexed）與狀態篩選。
- 依 `CreatedAt` 降序排列。
- 以 `Include` Eager Loading `Project` 與 `Team` 關聯。
- **Duration 計算邏輯（第 43 行）：**
  ```csharp
  Duration = t.CompletedAt.HasValue ? t.CompletedAt.Value - t.CreatedAt : null
  ```
  在資料庫層完成運算，避免回傳多餘欄位至應用層。
- 回傳 `PagedResult<TaskItemDto>`（含 `TotalCount` 供前端分頁）。

#### `GetRecentTasksAsync`

```csharp
public async Task<List<TaskItemDto>> GetRecentTasksAsync(
    int limit = 10,
    CancellationToken cancellationToken = default)
```

- 首頁快速摘要使用，取最近 N 筆任務。
- 同樣計算 `Duration`，邏輯與 `GetTasksPagedAsync` 一致。

#### `GetTaskLogsAsync`

```csharp
public async Task<List<TaskLogDto>> GetTaskLogsAsync(
    Guid taskId,
    CancellationToken cancellationToken = default)
```

- 取得單一任務的所有 Log，依 `CreatedAt` 升序排列。
- 供使用者點擊任務列後的 Drawer 展開使用。

### 輔助型別

```csharp
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
```

泛型分頁結果包裝，同檔定義。

---

## 3. `TaskCenter.razor.cs`

**位置：** `src/AiTeam.Dashboard/Components/Pages/Tasks/TaskCenter.razor.cs`

### 類別定義

```csharp
public partial class TaskCenter : IAsyncDisposable
```

實作 `IAsyncDisposable` 以正確釋放 SignalR 連線與計時器。

### 相依注入

| 服務 | 用途 |
|------|------|
| `DashboardTaskService` | 查詢任務資料 |
| `NavigationManager` | 建構 SignalR Hub URL |
| `IConfiguration` | 讀取 `Dashboard:HubBaseUrl` 設定 |

### 私有欄位

| 欄位 | 型別 | 用途 |
|------|------|------|
| `_tableRef` | `MudTable<TaskItemDto>` | MudBlazor 表格參考，用於觸發重整 |
| `_selectedTask` | `TaskItemDto?` | 目前選取的任務（Drawer 用） |
| `_selectedLogs` | `List<TaskLogDto>` | 選取任務的 Log 列表 |
| `_isDrawerOpen` | `bool` | 控制 Drawer 開關 |
| `_statusFilter` | `string?` | 狀態篩選值 |
| `_hubConnection` | `HubConnection?` | SignalR 連線 |
| `_elapsedTimer` | `PeriodicTimer?` | **【新增】** 每秒觸發 UI 刷新的計時器 |

### 方法

#### `OnInitializedAsync`

```csharp
protected override async Task OnInitializedAsync()
{
    await ConnectSignalRAsync();
    _ = StartElapsedTimerAsync();
}
```

- 先建立 SignalR 連線，再啟動計時器。
- `StartElapsedTimerAsync` 以 fire-and-forget（`_ =`）方式啟動，不阻塞初始化流程。

#### `ConnectSignalRAsync`

- 優先讀取 `Dashboard:HubBaseUrl` 設定值；若未設定，則使用 `NavigationManager` 組成本機 URL。
- 訂閱 `AgentStatusHub.ReceiveTaskUpdate` 事件：收到通知時，透過 `InvokeAsync` 回到 Blazor 執行緒並呼叫 `_tableRef.ReloadServerData()`。

#### `LoadServerDataAsync`

```csharp
private async Task<TableData<TaskItemDto>> LoadServerDataAsync(
    TableState state,
    CancellationToken cancellationToken)
```

MudTable `ServerData` 回呼。`state.Page` 為 0-indexed，傳給 Service 前加 1 轉為 1-indexed。

#### `OnRowClickAsync`

點擊列後：設定 `_selectedTask`、呼叫 `GetTaskLogsAsync` 載入 Log、開啟 Drawer。

#### `StartElapsedTimerAsync` ★ 新增

```csharp
private async Task StartElapsedTimerAsync()
{
    _elapsedTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
    while (await _elapsedTimer.WaitForNextTickAsync())
    {
        await InvokeAsync(StateHasChanged);
    }
}
```

- 使用 `PeriodicTimer`（.NET 6+ 高效計時器）每秒呼叫一次 `StateHasChanged`。
- Razor 模板針對 `Duration == null` 的進行中任務，每次重繪時以 `DateTime.UtcNow - CreatedAt` 計算即時耗時。

#### `FormatDuration` ★ 新增

```csharp
private static string FormatDuration(TimeSpan ts) => ts switch
{
    { TotalSeconds: < 60 } => $"{(int)ts.TotalSeconds}秒",
    { TotalMinutes: < 60 } => $"{ts.Minutes}分{ts.Seconds:D2}秒",
    _                      => $"{(int)ts.TotalHours}小時{ts.Minutes:D2}分"
};
```

| 條件 | 輸出範例 |
|------|---------|
| < 60 秒 | `42秒` |
| < 60 分 | `5分08秒` |
| ≥ 60 分 | `2小時03分` |

#### `FormatElapsed` ★ 新增

```csharp
private string FormatElapsed(TimeSpan ts) =>
    FormatDuration(ts < TimeSpan.Zero ? TimeSpan.Zero : ts);
```

防禦性包裝：若傳入負值（時鐘偏差）則截斷為 `TimeSpan.Zero`，再交由 `FormatDuration` 格式化。

#### `DisposeAsync`

```csharp
public async ValueTask DisposeAsync()
{
    _elapsedTimer?.Dispose();
    if (_hubConnection is not null)
        await _hubConnection.DisposeAsync();
}
```

依序釋放：先停止計時器，再關閉 SignalR 連線。

---

## 資料流程

```
[MudTable 初次載入 / SignalR 推播]
        │
        ▼
DashboardTaskService.GetTasksPagedAsync()
        │  EF Core 在 DB 層計算 Duration = CompletedAt - CreatedAt
        ▼
List<TaskItemDto>
        │
        ├─ Duration 有值 → 靜態顯示已完成執行時間（FormatDuration）
        │
        └─ Duration == null（進行中）
                │
                ▼
        PeriodicTimer 每秒觸發 StateHasChanged
                │
                ▼
        Razor 模板：DateTime.UtcNow - CreatedAt → FormatElapsed() → 動態顯示
```

---

## 注意事項

- **時區一致性：** `CreatedAt` / `CompletedAt` 儲存於資料庫的值應為 UTC，前端計時器使用 `DateTime.UtcNow`，需確保一致，否則即時耗時計算會有偏差。
- **計時器資源釋放：** `PeriodicTimer.Dispose()` 會使 `WaitForNextTickAsync()` 回傳 `false`，迴圈自然結束，無需額外 CancellationToken。
- **SignalR 重連：** 使用 `WithAutomaticReconnect()`，中斷重連後仍會繼續接收任務更新推播。