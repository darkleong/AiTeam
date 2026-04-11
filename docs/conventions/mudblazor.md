# MudBlazor 使用規範

> 適用版本：MudBlazor 8.x
> 建立日期：2026-04-11

本文件記錄 AiTeam Dashboard 使用 MudBlazor 的標準做法與已驗證的踩坑規則。  
實作前請先閱讀，避免重踏覆轍。

---

## 一、架構前提（必讀）

### Render Mode

本專案採用 **全域 InteractiveServer** 架構：

```
App.razor
  └── <Routes />              ← Routes.razor 宣告 @rendermode InteractiveServer
        └── MainLayout.razor  ← Static SSR（接收 @Body，不可加 rendermode）
              └── 各頁面      ← 共享同一 Circuit，可使用所有 Interactive 功能
```

**關鍵限制：`MainLayout` 永遠是 Static SSR。**

`MainLayout` 因為接收 `@Body`（型別為 `RenderFragment`，本質是 C# delegate），加上 `@rendermode` 會收到：

```
System.InvalidOperationException: Cannot pass the parameter 'Body' to component
'MainLayout' with rendermode 'InteractiveServerRenderMode'.
```

**後果：**
- `MainLayout` 無法持有 C# 狀態
- `MudThemeProvider` 的 `@bind-IsDarkMode` 在 Layout 裡**無法動態更新**
- Dark Mode 改用 CSS 變數 + JS 方案（見第五節）

**各頁面不需要宣告 `@rendermode`**（全域已設定），也不需要加 `prerender: false`。

---

### MudProviders 放置位置

`MudThemeProvider`、`MudDialogProvider`、`MudSnackbarProvider`、`MudPopoverProvider` 統一放在 `MudProviders.razor`：

```razor
@* Components/Layout/MudProviders.razor *@
@rendermode @(new InteractiveServerRenderMode(prerender: false))
<MudThemeProvider />
<MudDialogProvider />
<MudSnackbarProvider />
<MudPopoverProvider />
```

由 `MainLayout.razor` 引用：

```razor
@* MainLayout.razor *@
<MudProviders />   @* Interactive Island，讓 scoped services 在正確 Circuit 中運作 *@
<MudLayout>
    ...
</MudLayout>
```

> ⚠️ **不可直接把 Providers 寫在 MainLayout 裡**。MainLayout 是 Static SSR，直接放 Providers 會導致 `IDialogService` 等 scoped services 在 Interactive 頁面中找不到正確的 Provider 實例。

---

## 二、MudLayout 與 MudDrawer

### Persistent Sidebar（主側邊欄）

主側邊欄使用 `DrawerVariant.Persistent`，固定顯示，不遮蓋主內容：

```razor
<MudLayout>
    <MudAppBar Elevation="1" Color="Color.Default" Dense="true">
        ...
    </MudAppBar>
    <MudDrawer Open="true" Variant="DrawerVariant.Persistent"
               Elevation="2" ClipMode="DrawerClipMode.Always">
        <NavMenu />
    </MudDrawer>
    <MudMainContent>
        <div class="pa-4">@Body</div>
    </MudMainContent>
</MudLayout>
```

**Persistent Drawer 收合：** 因 `MainLayout` 是 Static SSR，無法用 C# `@bind-Open` 切換，改用 JS 直接操作 DOM class：

```js
// MudBlazor Persistent Drawer 使用兩組 class 控制開合：
// - drawer 本身：mud-drawer--open / mud-drawer--closed
// - layout：mud-drawer-open-persistent-left（控制 MudMainContent margin-left）
window.aiteamToggleSidebar = function () {
    var drawer = document.querySelector('.mud-drawer-persistent');
    var layout = document.querySelector('.mud-layout');
    if (!drawer || !layout) return;
    var isOpen = drawer.classList.contains('mud-drawer--open');
    drawer.classList.remove('mud-drawer--initial');
    if (isOpen) {
        drawer.classList.remove('mud-drawer--open');
        drawer.classList.add('mud-drawer--closed');
        layout.classList.remove('mud-drawer-open-persistent-left');
        localStorage.setItem('sidebarOpen', '0');
    } else {
        drawer.classList.remove('mud-drawer--closed');
        drawer.classList.add('mud-drawer--open');
        layout.classList.add('mud-drawer-open-persistent-left');
        localStorage.setItem('sidebarOpen', '1');
    }
};
```

> ⚠️ **兩個 class 都要同時切換**，只切 drawer 本身不切 layout 的話，`MudMainContent` 的 margin-left 不會變，畫面會錯位。

---

### Temporary Drawer（右側 Detail Panel）

各頁面的詳情抽屜使用 `DrawerVariant.Temporary`：

```razor
<MudDrawer @bind-Open="_isDrawerOpen"
           Anchor="Anchor.End"
           Variant="DrawerVariant.Temporary"
           Width="480px"
           OverlayAutoClose="true">
    @if (_selectedItem is not null)
    {
        ...
    }
</MudDrawer>
```

**標準設定：**
- `Anchor="Anchor.End"` — 從右側滑出
- `OverlayAutoClose="true"` — 點擊 overlay 自動關閉
- `Width="480px"` — 統一寬度
- 內容加 null guard（`@if (_selectedItem is not null)`）

---

### Drawer 作為共用子元件（含 EventCallback）

當 Drawer 封裝為共用元件（如 `TaskLogDrawer.razor`），**不可使用 `@bind-Open`**，必須拆開寫：

```razor
@* ❌ 錯誤：@bind-Open 展開後只 mutate 參數，不呼叫父元件的 EventCallback *@
<MudDrawer @bind-Open="IsOpen" ...>

@* ✅ 正確：明確接線 EventCallback *@
<MudDrawer Open="@IsOpen"
           OpenChanged="IsOpenChanged"
           ...>
```

```csharp
// 子元件 .razor.cs
[Parameter] public bool IsOpen { get; set; }
[Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

private async Task CloseAsync()
    => await IsOpenChanged.InvokeAsync(false);
```

**原因：** `@bind-Open="IsOpen"` 展開為 `OpenChanged="(v) => IsOpen = v"`，只直接賦值參數，不會呼叫 `IsOpenChanged` EventCallback，導致父元件狀態不同步。

---

## 三、MudTable

### 標準設定

```razor
<MudTable Items="@_items"
          Dense="true"
          Hover="true"
          Height="600px"
          FixedHeader="true"
          RowsPerPage="10"
          T="MyDto">
    <HeaderContent>
        <MudTh>欄位一</MudTh>
        <MudTh>欄位二</MudTh>
    </HeaderContent>
    <RowTemplate>
        <MudTd>@context.Field1</MudTd>
        <MudTd>@context.Field2</MudTd>
    </RowTemplate>
    <NoRecordsContent>
        <MudText>尚無資料</MudText>
    </NoRecordsContent>
    <PagerContent>
        <MudTablePager PageSizeOptions="new int[]{10, 25, 50}" />
    </PagerContent>
</MudTable>
```

**規則：**
- `Height="600px"` + `FixedHeader="true"` — Scrollbar 限制在元件內，不用瀏覽器 Scrollbar
- `RowsPerPage="10"` — 預設 10 筆
- `PageSizeOptions="new int[]{10, 25, 50}"` — 統一選項

### Server-side 分頁

資料量大或需要即時篩選時使用 `ServerData`：

```razor
<MudTable @ref="_tableRef"
          ServerData="LoadServerDataAsync"
          ...>
```

```csharp
private MudTable<MyDto> _tableRef = null!;

private async Task<TableData<MyDto>> LoadServerDataAsync(TableState state, CancellationToken ct)
{
    var result = await _service.GetPagedAsync(
        page: state.Page,
        pageSize: state.PageSize,
        statusFilters: _statusFilters.ToHashSet(),
        cancellationToken: ct);

    return new TableData<MyDto>
    {
        Items = result.Items,
        TotalItems = result.TotalCount
    };
}

// 篩選條件變更時，呼叫此方法強制重新載入
private async Task OnFilterChangedAsync()
    => await _tableRef.ReloadServerData();
```

### 可點擊列

```razor
<MudTable OnRowClick="OnRowClickAsync"
          RowClass="cursor-pointer"
          ...>
```

---

## 四、MudSelect

### 單選

```razor
<MudSelect T="string" @bind-Value="_selectedValue"
           Label="選項" Dense="true">
    <MudSelectItem Value="@("option1")">選項一</MudSelectItem>
    <MudSelectItem Value="@("option2")">選項二</MudSelectItem>
</MudSelect>
```

### 多選（含觸發 side effect）

```razor
@* ✅ 用 @bind-SelectedValues:after 觸發重載 *@
<MudSelect T="string" MultiSelection="true"
           @bind-SelectedValues="_statusFilters"
           @bind-SelectedValues:after="OnFilterChangedAsync"
           Label="篩選狀態" Dense="true" Style="max-width:300px"
           AdornmentIcon="@Icons.Material.Filled.FilterList">
    <MudSelectItem Value="@("running")">執行中</MudSelectItem>
    <MudSelectItem Value="@("done")">已完成</MudSelectItem>
    ...
</MudSelect>
```

```csharp
// 型別必須是 IEnumerable<string>，不是 List<string>
private IEnumerable<string> _statusFilters = [];

private async Task OnFilterChangedAsync()
    => await _tableRef.ReloadServerData();
    // 注意：@bind-SelectedValues 已自動同步值，這裡只需觸發 side effect
```

> ⚠️ **`@bind-SelectedValues` 不會自動觸發 side effect**（如重新載入資料）。必須加上 `:after` 事件或 `SelectedValuesChanged` 參數。

---

## 五、Dark Mode

本專案 Dark Mode 使用 **CSS 變數 + JS 方案**，而非 `MudThemeProvider` 的 `@bind-IsDarkMode`（因 Layout 限制無法使用 C# binding）。

### 運作方式

```js
// App.razor 的 inline script（頁面載入前執行，避免閃白）
(function(){
    var t = localStorage.getItem('theme') || 'light';
    document.documentElement.dataset.theme = t;
})();

window.appTheme = {
    setDark: function (isDark) {
        var t = isDark ? 'dark' : 'light';
        localStorage.setItem('theme', t);
        document.documentElement.dataset.theme = t;
    }
};
```

```css
/* app.css — 覆寫 MudBlazor CSS 變數 */
html[data-theme="dark"] {
    --mud-palette-background: #121212;
    --mud-palette-surface: #1e1e1e;
    --color-bg-primary: #1e1e1e;
    /* ...其他覆寫 */
}
```

### 切換按鈕

因 Layout 是 Static SSR，切換按鈕用 `onclick` JS 呼叫（不用 `@onclick` C# handler）：

```razor
<span class="theme-btn-dark" onclick="window.appTheme.setDark(true)">
    <MudIconButton Icon="@Icons.Material.Filled.DarkMode" title="切換深色模式" />
</span>
<span class="theme-btn-light" onclick="window.appTheme.setDark(false)">
    <MudIconButton Icon="@Icons.Material.Filled.LightMode" title="切換淺色模式" />
</span>
```

```css
/* 根據當前 theme 只顯示對應按鈕 */
html[data-theme="dark"]  .theme-btn-dark  { display: none; }
html[data-theme="light"] .theme-btn-light { display: none; }
```

---

## 六、MudDialog

### Dialog 元件標準結構

```razor
@* Pages/Rules/RuleFormDialog.razor *@
<MudDialog>
    <DialogContent>
        <MudTextField @bind-Value="_content" Label="內容" Required="true" />
        @if (_error is not null)
        {
            <MudAlert Severity="Severity.Error" Class="mt-2">@_error</MudAlert>
        }
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">取消</MudButton>
        <MudButton Color="Color.Primary" Variant="Variant.Filled"
                   OnClick="SubmitAsync" Disabled="@_isSubmitting">
            @(_isSubmitting ? "儲存中..." : "確認")
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    private void Cancel() => MudDialog.Cancel();

    private async Task SubmitAsync()
    {
        // 驗證 + 呼叫 Service
        MudDialog.Close(DialogResult.Ok(result));
    }
}
```

### 呼叫端（父頁面）

```csharp
[Inject] private IDialogService DialogService { get; set; } = null!;

private async Task OpenCreateDialogAsync()
{
    var parameters = new DialogParameters<RuleFormDialog>
    {
        { d => d.AgentOptions, _agentOptions }
    };
    var dialog = await DialogService.ShowAsync<RuleFormDialog>("新增規則", parameters);
    var result = await dialog.Result;

    // result 可能為 null（視 MudBlazor 版本）；用 pattern matching 安全取值
    if (result is { Canceled: false } && result.Data is Rule created)
        _rules.Add(created);
}
```

> ⚠️ **`await dialog.Result` 回傳 `DialogResult?`**（nullable），直接取 `.Canceled` 或 `.Data` 會有 CS8602 警告。必須用 `result is { Canceled: false }` pattern matching。

---

## 七、MudList（單選清單）

```razor
<MudList T="AgentConfigDto" @bind-SelectedValue="_selectedAgent">
    @foreach (var agent in _agents)
    {
        <MudListItem Value="@agent">
            <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
                <span>@agent.Name</span>
                <StatusBadge Status="@(agent.IsActive ? "idle" : "cancelled")" />
            </MudStack>
        </MudListItem>
    }
</MudList>
```

> ⚠️ **`@bind-SelectedValue` 和 `SelectedValueChanged` 不可共存。**  
> `@bind-SelectedValue` 展開後已自動包含 `SelectedValueChanged`，若再手動加一個 `SelectedValueChanged` 參數，會收到：  
> `The component parameter 'SelectedValueChanged' cannot be set more than once.`  
> 只需要 `@bind-SelectedValue`，不需要額外的 handler 方法。

---

## 八、MudSwitch

```razor
@* ✅ 標準寫法 *@
<MudSwitch T="bool"
           Value="@context.IsActive"
           ValueChanged="@((bool v) => ToggleIsActiveAsync(context, v))"
           Color="Color.Primary"
           Label="@(context.IsActive ? "啟用中" : "已停用")" />
```

> ⚠️ 在 `MudTable RowTemplate` 中使用 MudSwitch 時，若點擊 Switch 也會觸發 `OnRowClick`，需在外層加 `@onclick:stopPropagation="true"`：
>
> ```razor
> <MudTd @onclick:stopPropagation="true">
>     <MudSwitch ... />
> </MudTd>
> ```

---

## 九、MudChip（狀態 Badge）

用於顯示類型、標籤等彩色小徽章：

```razor
<MudChip T="string" Size="Size.Small" Color="@GetColor(context.Type)">
    @context.Type
</MudChip>
```

```csharp
private static Color GetColor(string? type) => type switch
{
    "新功能"  => Color.Primary,
    "Bug Fix" => Color.Warning,
    "技術改善" => Color.Secondary,
    _         => Color.Default
};
```

> 使用 `Color` enum 而非 inline `style="background:..."` 硬編碼色碼，Dark Mode 下自動適配。

---

## 十、MudStack 與 MudGrid

**以 MudBlazor 元件取代 inline flex style：**

```razor
@* ❌ 避免 *@
<div style="display:flex; align-items:center; gap:8px">

@* ✅ 改用 MudStack *@
<MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
```

```razor
@* ❌ 避免 *@
<div style="display:flex; gap:12px">

@* ✅ 改用 MudStack（純水平排列） *@
<MudStack Row="true" Spacing="3">
```

**Grid 佈局：**

```razor
<MudGrid>
    <MudItem xs="12" md="4">
        @* 左欄 *@
    </MudItem>
    <MudItem xs="12" md="8">
        @* 右欄 *@
    </MudItem>
</MudGrid>
```

---

## 十一、CSS 規範

### 使用 MudBlazor CSS 變數

```css
/* ✅ 正確：使用 MudBlazor 調色盤變數，Dark Mode 自動適配 */
color: var(--mud-palette-text-primary);
background: var(--mud-palette-background);
border-color: var(--mud-palette-primary);

/* ✅ 可用：自訂語意變數（在 app.css 同時定義 light/dark 兩版） */
color: var(--color-text-secondary);
background: var(--color-bg-primary);

/* ❌ 避免：硬編碼色碼（Dark Mode 不會自動切換） */
color: #333333;
background: #f59e0b;
```

### 不依賴 MudBlazor 內部 class

MudBlazor 內部 class（如 `.mud-icon-button`、`.mud-button-root`）可能隨版本改名，避免在 `app.css` 直接 target：

```css
/* ❌ 避免 */
.theme-btn-dark:hover .mud-icon-button { ... }

/* ✅ 改用容器層級 */
.theme-btn-dark:hover { opacity: 0.8; }
```

### `:deep()` 穿透元件樣式

若必須覆蓋 MudBlazor 元件內部樣式，在 `.razor.css` 中使用 `:deep()`：

```css
/* MyComponent.razor.css */
:deep(.mud-table-container) {
    border-radius: 8px;
}
```

---

## 十二、Empty State

使用 `MudIcon` 取代 emoji（跨瀏覽器一致）：

```razor
<div class="empty-state">
    <MudIcon Icon="@Icons.Material.Filled.Inbox"
             Size="Size.Large"
             Color="Color.Default" />
    <p class="empty-state-title">尚無資料</p>
    <p class="empty-state-hint">說明文字</p>
</div>
```

---

## 十三、提交前檢查

- [ ] MudDrawer 子元件使用 `Open` + `OpenChanged`，不用 `@bind-Open`
- [ ] MudList 只用 `@bind-SelectedValue`，沒有額外的 `SelectedValueChanged`
- [ ] MudSelect 多選有 `:after` 事件觸發 side effect
- [ ] `DialogResult` 用 pattern matching 取值，不直接存取 `.Data`
- [ ] 所有顏色使用 CSS 變數或 `Color` enum，無硬編碼色碼
- [ ] flex 排版改用 MudStack / MudGrid
- [ ] Empty State 使用 MudIcon，不用 emoji
- [ ] MudTable 有 `FixedHeader` + `Height`，Scrollbar 在元件內
- [ ] MudTable 中的 MudSwitch 有 `stopPropagation`（若有 OnRowClick）
- [ ] Dark Mode 下目視確認顯示正常
