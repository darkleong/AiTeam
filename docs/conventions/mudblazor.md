# MudBlazor 使用規範

> 適用版本：MudBlazor 8.x

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

### MudDrawer 自製 overlay-mode 必明設 width（Stage 86 踩坑 F2）

自製 `mud-drawer--overlay-mode` class（hover overlay 不擠壓 main content 用）時，**`position: fixed` 會讓既有 MudDrawer width CSS variable 失效**（露出 80px 預設 mini width）。

✅ 正確 — 明設 width + max-width 不依賴 layout class：
```css
.mud-drawer-pos-left.mud-drawer--overlay-mode {
    position: fixed !important;
    width: 240px !important;
    max-width: 240px !important;
}
```

❌ 錯誤 — 依賴 `mud-drawer-open-persistent-left` 帶 width variable（overlay-mode 已脫離 layout context，width var 不會 apply）。

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

## 五、Dark Mode（Stage 86 重寫）

### 紀律：MudThemeProvider IsDarkMode 必 binding + 跨 component event 同步

```razor
@* MudProviders.razor *@
<MudThemeProvider IsDarkMode="@Theme.IsDarkMode" Theme="_customTheme" />
```

**禁忌寫法**：
```razor
@* ❌ Stage 83 v4 Bug 9 + Stage 86 修根因 — MudThemeProvider 0 binding default light *@
<MudThemeProvider />
@* → MudPaper / MudCard / MudTabs 一直 light / reload 才從 wwwroot/css/app.css `html[data-theme]` 抓 / 兩 layer 永遠對不齊 *@
```

### Theme 切換 button 必走 Interactive component + Scoped Service

`MainLayout` 是 Static SSR（第一節紀律）→ **不可** 用 `@onclick` C# handler / **不可** 持 `_isDarkMode` C# field。Theme toggle button 必抽 Interactive Island + 透過 DI Scoped service 跨 component 共享 state + event。

```csharp
// Services/IThemeService.cs
public interface IThemeService
{
    bool IsDarkMode { get; }
    event Action? OnChanged;
    void SetDarkMode(bool isDark);
}
// Program.cs: builder.Services.AddScoped<IThemeService, ThemeService>();
```

```razor
@* Components/Layout/ThemeToggleButton.razor — Interactive Island *@
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@inject IThemeService Theme
@inject IJSRuntime JS

<MudIconButton Icon="@(Theme.IsDarkMode ? Icons.Material.Filled.LightMode : Icons.Material.Filled.DarkMode)"
               OnClick="ToggleAsync" />

@code {
    protected override async Task OnInitializedAsync()
        => Theme.SetDarkMode(await JS.InvokeAsync<bool>("appTheme.init"));

    private async Task ToggleAsync()
    {
        var newDark = !Theme.IsDarkMode;
        Theme.SetDarkMode(newDark);
        await JS.InvokeVoidAsync("appTheme.setDark", newDark);  // 同步 JS / localStorage / html[data-theme]
    }
}
```

```razor
@* MudProviders.razor — Interactive Island / subscribe event 觸發 StateHasChanged *@
@inject IThemeService Theme
@implements IDisposable

<MudThemeProvider IsDarkMode="@Theme.IsDarkMode" Theme="_customTheme" />

@code {
    protected override void OnInitialized() => Theme.OnChanged += OnThemeChanged;
    private async void OnThemeChanged() => await InvokeAsync(StateHasChanged);
    public void Dispose() => Theme.OnChanged -= OnThemeChanged;
}
```

→ ThemeToggleButton click → IThemeService.SetDarkMode → OnChanged event → MudProviders subscribe → StateHasChanged → MudThemeProvider 重新繪 → MudPaper / MudCard / MudTabs **即時切色（不 reload）**。

### CSS 變數 layer（兩 layer 對齊）

JS `appTheme.setDark` 切 `html[data-theme]` → `wwwroot/css/app.css` `--mud-palette-*` 變數覆寫 → MudBlazor 元件底層 CSS layer 切色。**兩 layer 對齊必要**（MudTheme PaletteDark hex == app.css `html[data-theme="dark"]` 內 `--mud-palette-*` hex）/ Stage 86 對齊深灰色 baseline。

```css
/* app.css — Dark Mode 兩 layer 對齊 MudTheme PaletteDark hex */
html[data-theme="dark"] {
    --mud-palette-background:      #1e1e1e;
    --mud-palette-background-grey: #2d2d2d;
    --mud-palette-surface:         #252525;
    /* ... 其他變數對齊 PaletteDark 完整 hex */
}
```

### JS appTheme API（保留 reload-safe init）

```js
// App.razor inline script（頁面載入前執行避免閃白）
(function(){
    var t = localStorage.getItem('theme') || 'light';
    document.documentElement.dataset.theme = t;
})();

window.appTheme = {
    init: function () {
        var t = localStorage.getItem('theme') || 'light';
        document.documentElement.dataset.theme = t;
        return t === 'dark';
    },
    setDark: function (isDark) {
        var t = isDark ? 'dark' : 'light';
        localStorage.setItem('theme', t);
        document.documentElement.dataset.theme = t;
    }
};
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

### MudBlazor utility class 跟 inline style 不能混搭（Stage 86 踩坑 #5 v2）

MudBlazor utility class（`pa-N` / `m-N` / `d-flex` 等）內部帶 `!important`，**inline `style="..."` 無法 override**。

✅ 正確 — 要 override utility class 的 padding/margin 時，**拿掉 utility class、改全 inline style**：
```razor
<!-- ❌ 不生效（inline 輸給 utility !important） -->
<div class="pa-3" style="padding-left: 56px">...</div>

<!-- ✅ 生效（純 inline 全控） -->
<div style="padding: 12px 12px 12px 56px">...</div>
```

❌ 錯誤 — 混搭依賴「inline 贏 utility」（cascading 邏輯不適用 `!important`）。

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
- [ ] MudDrawer 自製 overlay-mode 明設 `width !important` + `max-width !important`
- [ ] MudBlazor utility class 不跟 inline style 混搭（要 override 拿掉 utility class）
- [ ] MudButtonGroup 子按鈕需自控 Variant 時加 `OverrideStyles="false"`
- [ ] MudTooltip 包大型 block element 用 `Inline="false"` + CSS `.mud-tooltip max-width` limit

---

## 十四、MudButtonGroup

### `OverrideStyles="false"` 才能 child 自控 Variant（Stage 86 踩坑 #7 v2）

`MudButtonGroup` 預設 `OverrideStyles="true"`，會覆寫所有 child MudButton 的 Variant — child 個別設定 `Variant="Variant.Filled"` 不生效。

✅ 正確 — child 自控 Variant（active/inactive 視覺區分 case）必加 `OverrideStyles="false"`：
```razor
<MudButtonGroup Variant="Variant.Outlined" Size="Size.Small" OverrideStyles="false">
    @foreach (var period in _periods)
    {
        <MudButton Variant="@(_selected == period.Key ? Variant.Filled : Variant.Outlined)"
                   Color="@(_selected == period.Key ? Color.Primary : Color.Default)"
                   OnClick="@(() => Select(period.Key))">@period.Label</MudButton>
    }
</MudButtonGroup>
```

❌ 錯誤 — 預設 `true` 會把 child 的 `Variant.Filled` 全 override 成 group 層級的 `Variant.Outlined`。

---

## 十五、MudTooltip

### 包大型 block element 用 `Inline="false"` + popup max-width（Stage 86 踩坑 #1 v2 + #6）

MudTooltip 預設 `Inline="true"` 渲染為 `<span>` — wrap 大型 block element（如 MudPaper 整張卡片）會塌成 inline 寬度，破壞 layout。

✅ 正確雙紀律：
1. **wrap 大型 block** 用 `Inline="false"`（渲染為 `<div>` 撐 100% width）
2. **popup CSS 限寬**（不然 popup 跟 anchor 等寬會超出 viewport）：
```css
.mud-tooltip { max-width: 280px !important; }
```

```razor
<MudTooltip Text="說明文字" Inline="false">
    <MudPaper Elevation="2" Class="pa-4">
        <!-- 大型卡片內容 -->
    </MudPaper>
</MudTooltip>
```

❌ 錯誤 — 預設 `Inline="true"` 包 MudPaper 會塌寬度 / 不設 popup max-width 會超出 viewport。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.6 | 2026-05-24 | Stage 86 結案升級 — 5 條紀律補強：① 五 Dark Mode 段重寫（MudThemeProvider IsDarkMode 必 binding + IThemeService Scoped event 跨 Interactive Island 同步 / Stage 83 v4 Bug 9 修根因）② MudDrawer 自製 overlay-mode 必明設 width（F2 踩坑）③ utility class 不跟 inline style 混搭（#5 v2 踩坑）④ 新十四節 MudButtonGroup OverrideStyles=false 紀律（#7 v2 踩坑）⑤ 新十五節 MudTooltip Inline=false + popup max-width 雙紀律（#1 v2 + #6 踩坑）|
| v1.0-v1.5 | 早期累積 | 既有 13 節紀律（架構前提 / MudLayout+Drawer / MudTable / MudSelect / Dark Mode / MudDialog / MudList / MudSwitch / MudChip / MudStack+MudGrid / CSS 規範 / Empty State / 提交前檢查）|
