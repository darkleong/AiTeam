# Blazor 組件規範

## 檔案結構

每個組件包含：

```
UserList.razor          ← UI 標記
UserList.razor.cs       ← 代碼後置（partial class）
UserList.razor.css      ← CSS Isolation（可選）
```

## .razor.cs 基本結構

```csharp
namespace AiTeam.Dashboard.Components.Pages.Users;

public partial class UserList
{
    #region Dependencies
    [Inject]
    private UserService UserService { get; set; } = null!;
    #endregion

    #region Parameters
    [Parameter]
    public int PageSize { get; set; } = 50;
    #endregion

    #region Private Variables
    private List<UserDto>? _users;
    #endregion

    #region Override Methods
    protected override async Task OnInitializedAsync()
    {
        await LoadUsersAsync();
    }
    #endregion

    #region Private Methods
    private async Task LoadUsersAsync()
    {
        _users = await UserService.GetUsersAsync();
    }
    #endregion
}
```

## 生命週期順序

1. `OnInitializedAsync` — 組件初始化（只執行一次）
2. `OnParametersSetAsync` — 參數設置（每次參數改變）
3. `OnAfterRenderAsync(firstRender)` — 渲染後（DOM 操作、JS Interop）

## 渲染模式（@rendermode）

本專案 Dashboard 採用 **Blazor Web App + 全域 InteractiveServer**：

- `Routes.razor` 宣告 `@rendermode @(new InteractiveServerRenderMode(prerender: false))`，所有頁面在同一 SignalR Circuit 執行
- 每個頁面 `.razor` 也顯式宣告 `@rendermode`（增加可讀性，避免日後架構調整時出錯）：

```razor
@page "/rules"
@attribute [Authorize]
@rendermode @(new InteractiveServerRenderMode(prerender: false))
```

> **prerender: false 原因**：prerender 階段 SignalR 尚未建立，`IDialogService` 等 scoped services 不可用；且本機 Docker 部署環境無 SEO 需求。

**`[Inject]` 與 `@inject` 的選擇：**
- `.razor.cs` 代碼後置 → 使用 `[Inject]` 屬性
- `.razor` 檔案頂端（無代碼後置時）→ 使用 `@inject`

## 組件通信

**Parent → Child：`[Parameter]`**
**Child → Parent：`EventCallback<T>`**
**雙向綁定：`@bind-Value`**

> 注意：在子組件的參數上使用 `@bind-Open`，展開後只會在本地賦值，**不會回呼父層的 EventCallback**。
> 需要雙向同步時，明確寫：`Open="@IsOpen" OpenChanged="IsOpenChanged"`

## SignalR 即時更新

需要接收 Push 通知的頁面，透過 `HubConnection` 訂閱。必須實作 `IAsyncDisposable`：

```csharp
public partial class MyPage : IAsyncDisposable
{
    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private HubConnection? _hubConnection;

    protected override async Task OnInitializedAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/hubs/agent-status"))
            .WithAutomaticReconnect()
            .Build();

        // ✅ SignalR callback 在背景執行緒觸發，必須用 InvokeAsync 切回 Blazor 上下文
        _hubConnection.On<SomeViewModel>(
            AgentStatusHub.ReceiveSomething,
            async data => await InvokeAsync(async () =>
            {
                await DoSomethingAsync(data);
                StateHasChanged();
            }));

        await _hubConnection.StartAsync();
    }

    // ✅ 頁面離開時釋放連線，否則連線殘留
    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }
}
```

- **`InvokeAsync` 是必須的**：SignalR callback 在非 UI 執行緒執行，不 `InvokeAsync` 會導致 Blazor 渲染不更新或例外
- **`IAsyncDisposable` 是必須的**：頁面導航離開時，Blazor 會呼叫 `DisposeAsync`，確保 Hub 連線正常關閉

## 避免的錯誤

```csharp
// ❌ .razor 視圖中不放業務邏輯
@if (users.Count > 0 && user.IsActive && DateTime.Now > user.CreatedDate.AddDays(30))

// ✅ 抽成方法
@if (ShouldDisplayUser(user))

// ❌ 死鎖風險
var user = _service.GetUserAsync(id).Result;

// ✅ 正確
var user = await _service.GetUserAsync(id);

// ❌ 直接在組件中操作資料庫
var users = await _dbContext.Users.ToListAsync();

// ✅ 透過 Service
_users = await UserService.GetUsersAsync();
```

## Blazor Web App 渲染架構說明

| 項目 | 說明 |
|------|------|
| 渲染模式 | 全域 InteractiveServer（prerender: false） |
| 入口 | `App.razor` → `<Routes />` |
| 全域宣告 | `Routes.razor` 的 `@rendermode` |
| Scoped Services | 所有頁面共享同一 Circuit，scoped service 為同一實例 |
| Layout 限制 | `MainLayout` 裡的 `@Body` 為 `RenderFragment`，無法序列化，故 Layout 的 C# 狀態不能與子頁面共享 |

## CSS 規則

- 使用 MudBlazor CSS 變數（`--mud-palette-primary`、`--mud-palette-surface`）
- 使用自訂語義變數（定義於 `app.css`）：`--color-text-primary`、`--color-bg-card` 等
- **不使用 Bootstrap 變數**（`--bs-*`）：本專案未引入 Bootstrap
- 組件樣式穿透用 `:deep()`（例如 `:deep(.mud-input-root)`）
- **禁止在 Razor 檔的 `style=` 屬性中硬編碼色彩值**，一律用 CSS 變數

## 提交前檢查

- [ ] .razor 和 .razor.cs 分離（partial class）
- [ ] 頁面頂端已宣告 `@rendermode @(new InteractiveServerRenderMode(prerender: false))`
- [ ] 正確使用 #region 組織
- [ ] 所有非同步操作使用 await
- [ ] 視圖中無業務邏輯
- [ ] 使用 Service 進行資料存取
- [ ] 使用 SignalR 的頁面：實作 `IAsyncDisposable`，且 callback 內使用 `InvokeAsync`
- [ ] CSS 使用變數而非硬編碼色彩
- [ ] 子組件 Drawer/Dialog 的雙向綁定使用明確的 `Open` + `OpenChanged`（不用 `@bind-Open`）
