# MudBlazor 使用規範

> 適用版本：MudBlazor 8.x
> 建立日期：2026-04-11 / 精簡日期：2026-05-10（577 → ~400 行 / -30%）

本文件記錄 AiTeam Dashboard 使用 MudBlazor 的標準做法與已驗證的踩坑規則。

---

## 一、架構前提（必讀）

### Render Mode

本專案採用 **全域 InteractiveServer** 架構：

```
App.razor → Routes.razor 宣告 @rendermode InteractiveServer
  → MainLayout.razor（Static SSR，不可加 rendermode）
    → 各頁面（共享同一 Circuit）
```

⚠️ **`MainLayout` 永遠是 Static SSR**（接收 `@Body` RenderFragment delegate，加 `@rendermode` 會拋 `Cannot pass the parameter 'Body'`）。

**後果**：
- `MainLayout` 無法持有 C# 狀態
- `MudThemeProvider` 的 `@bind-IsDarkMode` 在 Layout 無法動態更新 → Dark Mode 改用 CSS 變數 + JS 方案（見第五節）

各頁面**不需要**宣告 `@rendermode`（全域已設）+ 不需 `prerender: false`。

### MudProviders 放置位置

`MudThemeProvider` / `MudDialogProvider` / `MudSnackbarProvider` / `MudPopoverProvider` 統一放 `Components/Layout/MudProviders.razor`：

```razor
@rendermode @(new InteractiveServerRenderMode(prerender: false))
<MudThemeProvider />
<MudDialogProvider />
<MudSnackbarProvider />
<MudPopoverProvider />
```

`MainLayout.razor` 引用 `<MudProviders />`（Interactive Island）。

⚠️ **不可直接把 Providers 寫在 MainLayout 裡** — Static SSR 直接放 Providers 會導致 `IDialogService` 等 scoped services 在 Interactive 頁面找不到正確 Provider 實例。

---

## 二、MudLayout 與 MudDrawer

### Persistent Sidebar（主側邊欄）

```razor
<MudLayout>
    <MudAppBar Elevation="1" Color="Color.Default" Dense="true">...</MudAppBar>
    <MudDrawer Open="true" Variant="DrawerVariant.Persistent"
               Elevation="2" ClipMode="DrawerClipMode.Always">
        <NavMenu />
    </MudDrawer>
    <MudMainContent><div class="pa-4">@Body</div></MudMainContent>
</MudLayout>
```

**Persistent Drawer 收合**：因 MainLayout Static SSR 無法用 C# `@bind-Open`，改用 JS 直接操作 DOM class：

```js
// 兩組 class 都要切：
// - drawer 本身：mud-drawer--open / mud-drawer--closed
// - layout：mud-drawer-open-persistent-left（控制 MudMainContent margin-left）
window.aiteamToggleSidebar = function () {
    var drawer = document.querySelector('.mud-drawer-persistent');
    var layout = document.querySelector('.mud-layout');
    if (!drawer || !layout) return;
    var isOpen = drawer.classList.contains('mud-drawer--open');
    drawer.classList.remove('mud-drawer--initial');
    drawer.classList.toggle('mud-drawer--open', !isOpen);
    drawer.classList.toggle('mud-drawer--closed', isOpen);
    layout.classList.toggle('mud-drawer-open-persistent-left', !isOpen);
    localStorage.setItem('sidebarOpen', isOpen ? '0' : '1');
};
```

⚠️ **兩個 class 都要同時切換** — 只切 drawer 不切 layout 會讓 MudMainContent margin-left 不變、畫面錯位。

### Temporary Drawer（右側 Detail Panel）

```razor
<MudDrawer @bind-Open="_isDrawerOpen"
           Anchor="Anchor.End" Variant="DrawerVariant.Temporary"
           Width="480px" OverlayAutoClose="true">
    @if (_selectedItem is not null) { ... }
</MudDrawer>
```

**標準設定**：`Anchor.End` 從右側滑出 / `OverlayAutoClose="true"` 點擊 overlay 自動關 / `Width="480px"` 統一寬度 / 內容加 null guard。

### Drawer 作為共用子元件（含 EventCallback）

當 Drawer 封裝為共用元件（如 `TaskLogDrawer.razor`），**不可使用 `@bind-Open`**：

```razor
@* ❌ @bind-Open 展開後只 mutate 參數，不呼叫父元件 EventCallback *@
<MudDrawer @bind-Open="IsOpen" ...>

@* ✅ 明確接線 EventCallback *@
<MudDrawer Open="@IsOpen" OpenChanged="IsOpenChanged" ...>
```

```csharp
[Parameter] public bool IsOpen { get; set; }
[Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

private async Task CloseAsync() => await IsOpenChanged.InvokeAsync(false);
```

⚠️ **原因**：`@bind-Open="IsOpen"` 展開為 `OpenChanged="(v) => IsOpen = v"`，只直接賦值參數，不呼叫 `IsOpenChanged` EventCallback → 父元件狀態不同步。

---

## 三、MudTable

### 標準設定

```razor
<MudTable Items="@_items" Dense="true" Hover="true"
          Height="600px" FixedHeader="true" RowsPerPage="10" T="MyDto">
    <HeaderContent><MudTh>欄位</MudTh>...</HeaderContent>
    <RowTemplate><MudTd>@context.Field</MudTd>...</RowTemplate>
    <NoRecordsContent><MudText>尚無資料</MudText></NoRecordsContent>
    <PagerContent><MudTablePager PageSizeOptions="new int[]{10, 25, 50}" /></PagerContent>
</MudTable>
```

**規則**：`Height="600px"` + `FixedHeader="true"` 讓 Scrollbar 限制元件內 / `RowsPerPage="10"` 預設 / `PageSizeOptions` 統一。

### Server-side 分頁

資料量大或需即時篩選時用 `ServerData`：

```razor
<MudTable @ref="_tableRef" ServerData="LoadServerDataAsync" ...>
```

```csharp
private MudTable<MyDto> _tableRef = null!;

private async Task<TableData<MyDto>> LoadServerDataAsync(TableState state, CancellationToken ct)
{
    var result = await _service.GetPagedAsync(state.Page, state.PageSize, _statusFilters.ToHashSet(), ct);
    return new TableData<MyDto> { Items = result.Items, TotalItems = result.TotalCount };
}

// 篩選變更時呼叫此方法強制重新載入
private async Task OnFilterChangedAsync() => await _tableRef.ReloadServerData();
```

### 可點擊列

```razor
<MudTable OnRowClick="OnRowClickAsync" RowClass="cursor-pointer" ...>
```

---

## 四、MudSelect

### 單選 + 多選（含觸發 side effect）

```razor
@* 單選 *@
<MudSelect T="string" @bind-Value="_selectedValue" Label="選項" Dense="true">
    <MudSelectItem Value="@("option1")">選項一</MudSelectItem>
</MudSelect>

@* 多選 — 用 @bind-SelectedValues:after 觸發重載 *@
<MudSelect T="string" MultiSelection="true"
           @bind-SelectedValues="_statusFilters"
           @bind-SelectedValues:after="OnFilterChangedAsync"
           Label="篩選狀態" Dense="true" Style="max-width:300px"
           AdornmentIcon="@Icons.Material.Filled.FilterList">
    <MudSelectItem Value="@("running")">執行中</MudSelectItem>
</MudSelect>
```

```csharp
// 型別必須 IEnumerable<string>，不是 List<string>
private IEnumerable<string> _statusFilters = [];
```

⚠️ **`@bind-SelectedValues` 不會自動觸發 side effect**（如重新載入資料）→ 必須加 `:after` 事件或 `SelectedValuesChanged` 參數。

---

## 五、Dark Mode

本專案 Dark Mode 使用 **CSS 變數 + JS 方案**（因 Layout Static SSR 限制無法用 `@bind-IsDarkMode`）。

### 運作方式

```js
// App.razor inline script（頁面載入前執行避免閃白）
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
}
```

### 切換按鈕

因 Layout Static SSR，按鈕用 `onclick` JS 呼叫（不用 `@onclick` C# handler）：

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
        @if (_error is not null) { <MudAlert Severity="Severity.Error" Class="mt-2">@_error</MudAlert> }
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
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
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

    // ⚠️ result 可能 null（視 MudBlazor 版本）；用 pattern matching 安全取值
    if (result is { Canceled: false } && result.Data is Rule created)
        _rules.Add(created);
}
```

⚠️ **`await dialog.Result` 回傳 `DialogResult?`**（nullable），直接取 `.Canceled` 或 `.Data` 會 CS8602 警告 → 必須用 `result is { Canceled: false }` pattern matching。

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

⚠️ **`@bind-SelectedValue` 和 `SelectedValueChanged` 不可共存** — `@bind-SelectedValue` 展開後已自動包含 `SelectedValueChanged`，再手動加會收到 `cannot be set more than once` 錯誤。只需要 `@bind-SelectedValue`。

---

## 八、MudSwitch

```razor
<MudSwitch T="bool"
           Value="@context.IsActive"
           ValueChanged="@((bool v) => ToggleIsActiveAsync(context, v))"
           Color="Color.Primary"
           Label="@(context.IsActive ? "啟用中" : "已停用")" />
```

⚠️ 在 `MudTable RowTemplate` 中使用 MudSwitch 時，若點 Switch 也會觸發 `OnRowClick` → 外層加 `@onclick:stopPropagation="true"`：

```razor
<MudTd @onclick:stopPropagation="true">
    <MudSwitch ... />
</MudTd>
```

---

## 九、MudChip（狀態 Badge）

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

> 使用 `Color` enum 而非 inline `style="background:..."` 硬編碼色碼，Dark Mode 自動適配。

---

## 十、MudStack 與 MudGrid

**以 MudBlazor 元件取代 inline flex style**：

```razor
@* ❌ 避免 *@
<div style="display:flex; align-items:center; gap:8px">

@* ✅ 改用 MudStack *@
<MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
```

**Grid 佈局**：

```razor
<MudGrid>
    <MudItem xs="12" md="4">@* 左欄 *@</MudItem>
    <MudItem xs="12" md="8">@* 右欄 *@</MudItem>
</MudGrid>
```

---

## 十一、CSS 規範

### 使用 MudBlazor CSS 變數

```css
/* ✅ 正確：使用 MudBlazor 調色盤變數，Dark Mode 自動適配 */
color: var(--mud-palette-text-primary);
background: var(--mud-palette-background);

/* ✅ 可用：自訂語意變數（在 app.css 同時定義 light/dark 兩版） */
color: var(--color-text-secondary);

/* ❌ 避免：硬編碼色碼（Dark Mode 不會切換） */
color: #333333;
```

### 不依賴 MudBlazor 內部 class

MudBlazor 內部 class（`.mud-icon-button` / `.mud-button-root` 等）可能隨版本改名，避免在 `app.css` 直接 target → 改用容器層級 selector。

### `:deep()` 穿透元件樣式

若必須覆蓋 MudBlazor 元件內部樣式，在 `.razor.css` 中使用 `:deep()`：

```css
/* MyComponent.razor.css */
:deep(.mud-table-container) { border-radius: 8px; }
```

---

## 十二、Empty State

使用 `MudIcon` 取代 emoji（跨瀏覽器一致）：

```razor
<div class="empty-state">
    <MudIcon Icon="@Icons.Material.Filled.Inbox" Size="Size.Large" Color="Color.Default" />
    <p class="empty-state-title">尚無資料</p>
    <p class="empty-state-hint">說明文字</p>
</div>
```

---

## 十三、提交前檢查

- [ ] MudDrawer 子元件用 `Open` + `OpenChanged`，不用 `@bind-Open`
- [ ] MudList 只用 `@bind-SelectedValue`，沒有額外 `SelectedValueChanged`
- [ ] MudSelect 多選有 `:after` 事件觸發 side effect
- [ ] `DialogResult` 用 pattern matching 取值，不直接存取 `.Data`
- [ ] 所有顏色使用 CSS 變數或 `Color` enum，無硬編碼色碼
- [ ] flex 排版改用 MudStack / MudGrid
- [ ] Empty State 使用 MudIcon，不用 emoji
- [ ] MudTable 有 `FixedHeader` + `Height`，Scrollbar 在元件內
- [ ] MudTable 中的 MudSwitch 有 `stopPropagation`（若有 OnRowClick）
- [ ] Dark Mode 下目視確認顯示正常
