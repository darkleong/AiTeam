# Blazor 組件規範

## 檔案結構

每個組件包含：

```
UserList.razor          ← UI 標記
UserList.razor.cs       ← 代碼後置（partial class）
UserList.razor.css      ← CSS Isolation（可選）
UserList.razor.js       ← JS Interop（可選）
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
    private List<UserDto>? Users;
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
        Users = await UserService.GetUsersAsync();
    }
    #endregion
}
```

## 生命週期順序

1. `OnInitializedAsync` — 組件初始化（只執行一次）
2. `OnParametersSetAsync` — 參數設置（每次參數改變）
3. `OnAfterRenderAsync(firstRender)` — 渲染後（DOM 操作）

## 組件通信

**Parent → Child：`[Parameter]`**
**Child → Parent：`EventCallback<T>`**
**雙向綁定：`@bind-Value`**

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

// ❌ 直接在組件中 HTTP 呼叫
var response = await _httpClient.GetAsync("api/users");

// ✅ 透過 Service
Users = await UserService.GetUsersAsync();
```

## Blazor Server vs WASM

| 情境 | 選擇 |
|------|------|
| 即時更新、SignalR、企業內網 | Blazor Server |
| 可離線、前端 UI 邏輯為主 | Blazor WASM |

本專案 Dashboard 使用 **Blazor Server**。

## CSS 規則

- 使用 CSS 變數（`var(--bs-primary)`）而非硬編碼色彩
- 組件專屬樣式放在 `.razor.css`，使用 `:deep()` 穿透 Telerik 組件

## 提交前檢查

- [ ] .razor 和 .razor.cs 分離（partial class）
- [ ] 正確使用 #region 組織
- [ ] 所有非同步操作使用 await
- [ ] 視圖中無業務邏輯
- [ ] 使用 Service 進行資料存取
- [ ] CSS 使用變數而非硬編碼色彩
