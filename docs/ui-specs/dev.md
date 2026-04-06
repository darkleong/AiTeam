現在我對現有程式碼有清楚的了解，可以針對實際程式碼結構修正規格文件。以下是需要修正的關鍵點：

1. **`IAsyncDisposable`**：現有類別實作的是 `IAsyncDisposable`（非 `IDisposable`），`DisposeAsync()` 已存在
2. **SignalR 行為**：目前 SignalR 觸發的是整張表重新載入（`ReloadServerData()`），非單筆更新
3. **`_statusFilter` 型別**：現有欄位為 `string?`，篩選使用原生 `<select>` 非 MudBlazor 元件

---

# UI 規格文件：任務中心頁面顯示任務執行時間

---

## 一、需求摘要

**功能目標：** 在任務中心頁面（`TaskCenter.razor`）的任務列表中，新增「執行時間」欄位，讓使用者一眼看出每個任務耗費了多少時間。

**背景說明：**
- `TaskItem` 實體已有 `CreatedAt`（UTC）與 `CompletedAt`（UTC, nullable）兩個時間欄位
- 執行時間 = `CompletedAt - CreatedAt`，屬於衍生值，不需新增資料庫欄位
- 僅 `done` / `failed` 狀態的任務有完整的執行時間；`running` 狀態應顯示動態已耗時；`pending` / `waiting_input` 狀態顯示「—」

---

## 二、資料層變更

### 2-1. DTO 新增欄位

**檔案：** `src/AiTeam.Shared/Dtos/TaskItemDto.cs`

在現有 9 個屬性後新增：

```csharp
/// <summary>任務執行時間（CompletedAt - CreatedAt）；尚未完成時為 null。</summary>
public TimeSpan? Duration { get; set; }
```

### 2-2. Service 層計算

**檔案：** `src/AiTeam.Dashboard/Services/DashboardTaskService.cs`

在 `GetTasksPagedAsync()` 與 `GetRecentTasksAsync()` 兩處的 `Select` 投影中，新增 `Duration` 的計算：

```csharp
Duration = t.CompletedAt.HasValue
    ? t.CompletedAt.Value - t.CreatedAt
    : null,
```

> `running` 狀態下 `CompletedAt` 為 null，故 `Duration` 為 null，前端以 `CreatedAt` 為基準自行計算已耗時。

---

## 三、UI 規格

### 3-1. 欄位位置

在現有欄位順序中，於「完成時間」欄右側新增「執行時間」欄：

| 狀態 | 任務標題 | Agent | 專案 | 觸發來源 | 建立時間 | 完成時間 | **執行時間** |
|------|---------|-------|------|---------|---------|---------|------------|

### 3-2. 表頭

使用 `<MudTh>執行時間</MudTh>`，無排序功能（衍生值排序意義不大）。

### 3-3. 儲存格顯示規則

| 任務狀態 | `Duration` 值 | 顯示內容 | 樣式 |
|---------|--------------|---------|------|
| `pending` / `waiting_input` | null | `—` | `color: var(--mud-palette-text-secondary)` |
| `running` | null（動態） | `▶ HH:mm:ss`（持續更新） | `color: var(--mud-palette-warning)` |
| `done` | 有值 | 格式化後的時間字串 | 預設文字色 |
| `failed` | 有值 | 格式化後的時間字串 | 預設文字色 |

### 3-4. 時間格式化規則

依執行時間長短自動選擇最易讀的格式：

| 條件 | 格式 | 範例 |
|------|------|------|
| 不足 60 秒 | `ss秒` | `42秒` |
| 60 秒 ～ 未滿 60 分 | `mm分ss秒` | `3分27秒` |
| 60 分以上 | `H小時mm分` | `1小時25分` |

### 3-5. Running 狀態動態計時器

- 頁面上有 `running` 任務時，需每秒更新顯示已耗時
- 使用單一 `PeriodicTimer`（共用，避免每筆任務各建一個 Timer）每秒觸發 `StateHasChanged()`
- 現有 SignalR 已於收到任務更新通知時自動呼叫 `_tableRef.ReloadServerData()`，重新載入後 `running` 任務若已變為 `done`/`failed`，`Duration` 將由 Service 層填入，前端自動改顯示靜態時間，無需額外處理狀態切換
- Timer 僅在頁面存在 `running` 任務時啟動；頁面 `DisposeAsync()` 時確保 Timer 釋放，避免記憶體洩漏

### 3-6. MudBlazor 元件規格

**表頭（`HeaderContent` 區塊，加在 `<MudTh>完成時間</MudTh>` 之後）：**
```razor
<MudTh>執行時間</MudTh>
```

**資料列（`RowTemplate` 區塊，加在完成時間 `<MudTd>` 之後）：**
```razor
<MudTd>
    @if (context.Status == "running")
    {
        <span style="color: var(--mud-palette-warning);">
            ▶ @FormatElapsed(DateTime.UtcNow - context.CreatedAt)
        </span>
    }
    else if (context.Duration.HasValue)
    {
        @FormatDuration(context.Duration.Value)
    }
    else
    {
        <span style="color: var(--mud-palette-text-secondary);">—</span>
    }
</MudTd>
```

**格式化輔助方法（定義於 `TaskCenter.razor.cs`，加入現有 `#region Private Methods` 區塊）：**
```csharp
private static string FormatDuration(TimeSpan ts) => ts switch
{
    { TotalSeconds: < 60 } => $"{(int)ts.TotalSeconds}秒",
    { TotalMinutes: < 60 } => $"{ts.Minutes}分{ts.Seconds:D2}秒",
    _                      => $"{(int)ts.TotalHours}小時{ts.Minutes:D2}分"
};

private string FormatElapsed(TimeSpan ts) =>
    FormatDuration(ts < TimeSpan.Zero ? TimeSpan.Zero : ts);
```

---

## 四、Timer 實作規格

### 4-1. 私有欄位（加入 `#region Private Variables`）

```csharp
private PeriodicTimer? _elapsedTimer;
```

### 4-2. 啟動 Timer（於 `OnInitializedAsync` 中，在 `ConnectSignalRAsync()` 之後呼叫）

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

> `StartElapsedTimerAsync()` 使用 `_ = StartElapsedTimerAsync()` 或 `Task.Run` 非同步啟動，不等待其完成，避免阻塞 `OnInitializedAsync`。

### 4-3. 釋放 Timer（加入現有 `DisposeAsync()`）

```csharp
public async ValueTask DisposeAsync()
{
    _elapsedTimer?.Dispose();           // 新增：停止計時器
    if (_hubConnection is not null)
        await _hubConnection.DisposeAsync();
}
```

---

## 五、互動行為

| 情境 | 行為 |
|------|------|
| 點擊任務列（開啟 TaskLogDrawer） | 不受影響，現有行為不變 |
| 執行中任務計時顯示 | 每秒更新，不影響分頁或篩選操作 |
| 任務狀態切換（running → done） | SignalR 推播後觸發 `ReloadServerData()`，重新載入後 `Duration` 由 Service 填入，自動改顯示靜態時間 |
| 篩選狀態（Status Filter） | 執行時間欄依新狀態規則正常顯示，不需額外處理 |

---

## 六、邊界條件與注意事項

1. **時區：** `CreatedAt` 與 `CompletedAt` 均儲存為 UTC，差值計算不涉及時區轉換，可直接相減
2. **資料遷移：** 僅新增 DTO 欄位與 Service 計算邏輯，不變更資料庫 Schema，**不需要 EF Migration**
3. **數值異常：** 若 `CompletedAt < CreatedAt`（系統時鐘漂移），`Duration` 為負值；`FormatDuration` 以 `TimeSpan.Zero` 兜底，顯示 `0秒`
4. **大量 running 任務：** 單一 `PeriodicTimer` 統一每秒觸發 `StateHasChanged()`，`running` 儲存格直接以 `DateTime.UtcNow - context.CreatedAt` 計算，不需為每筆任務維護個別計時器
5. **頁面切換：** 離開任務中心頁面時，`DisposeAsync()` 呼叫 `_elapsedTimer?.Dispose()` 停止 Timer，SignalR 連線也同步釋放
6. **`GetRecentTasksAsync`：** 首頁快速摘要同樣呼叫此 Service，`Duration` 欄位一併填入，首頁若有需要可直接使用，**不需額外修改**

---

## 七、驗收標準（QA Checklist）

- [ ] `done` 任務：執行時間欄正確顯示計算後的時間字串（秒 / 分秒 / 小時分格式）
- [ ] `failed` 任務：執行時間欄正確顯示（與 `done` 相同邏輯）
- [ ] `pending` / `waiting_input` 任務：顯示灰色 `—`
- [ ] `running` 任務：顯示橘色 `▶ XX秒`（或分秒），且每秒遞增
- [ ] `running` → `done`：SignalR 推播後表格重新載入，動態計時停止，顯示靜態最終時間
- [ ] 執行時間不足 60 秒：格式為 `N秒`
- [ ] 執行時間 1～59 分鐘：格式為 `N分NN秒`
- [ ] 執行時間超過 1 小時：格式為 `N小時NN分`
- [ ] 開啟/關閉 TaskLogDrawer：執行時間欄不受影響，計時繼續
- [ ] 切換篩選狀態：重新載入後各狀態任務執行時間顯示正確
- [ ] 離開頁面再回來：無 Timer 洩漏（瀏覽器記憶體無異常增長）
- [ ] 現有欄位（建立時間、完成時間）格式不受影響
- [ ] `CompletedAt < CreatedAt` 邊界情況：顯示 `0秒` 而非負數或例外

---

以上規格已依據現有程式碼修正了三處主要差異：

| 修正項目 | 第一版 | 修正後 |
|---------|--------|--------|
| Dispose 介面 | `IDisposable.Dispose()` | `IAsyncDisposable.DisposeAsync()`（與現有類別一致） |
| SignalR 觸發機制 | 個別追蹤狀態切換 | 沿用現有 `ReloadServerData()`，自然覆蓋 running → done 轉換 |
| Timer 生命週期 | 未說明何時啟動 | 明確以 fire-and-forget 方式在 `OnInitializedAsync` 啟動，不阻塞初始化 |
