# Stage 20 — Dashboard 全面換 MudBlazor Layout

> 版本：v2.0
> 建立日期：2026-04-10
> 狀態：✅ 已完成（2026-04-11）

---

## 背景

Stage 19 Pt.1 實作中發現，Dashboard 目前的 `MainLayout` 為自訂結構，並非 MudBlazor 的 `MudLayout`，導致 `MudDrawer Temporary` 模式的 overlay 無法正確運作。整體呈現「混搭」狀態：部分元件用 MudBlazor，Layout 與 NavMenu 仍是自訂 HTML。

Stage 19 Pt.2 的多項改善（MudDialog 表單、MudDrawer 全面接通、Dark Mode 統一管理）都依賴穩定的 MudLayout 基礎，因此決定先做 Stage 20，將 Layout 全面換成 MudBlazor，再回頭完成 Stage 19 Pt.2。

---

## 目標

將 Dashboard 的 Layout 基礎設施全面換成 MudBlazor，讓後續所有 UI 改善能在穩定的元件體系上進行。

---

## 實作項目

### 一、MainLayout 換成 MudLayout

`MainLayout.razor` 改用 MudBlazor 原生 Layout 骨架：

```razor
@inherits LayoutComponentBase

<MudProviders />   @* InteractiveServer island，見踩坑四 *@

<MudLayout>
    <MudAppBar Elevation="1" Color="Color.Default" Dense="true">
        <span onclick="window.aiteamToggleSidebar()">  @* 見踩坑二/三 *@
            <MudIconButton Icon="@Icons.Material.Filled.Menu" Edge="Edge.Start" />
        </span>
        <MudText Typo="Typo.h6" Class="ml-2">AI Team</MudText>
        <MudSpacer />
    </MudAppBar>
    <MudDrawer Open="true" Variant="DrawerVariant.Persistent"
               Elevation="2" ClipMode="DrawerClipMode.Always">
        <NavMenu />
        <MudSpacer />
        <div class="pa-2">
            <MudStack Row="true" AlignItems="AlignItems.Center">
                <span class="theme-btn-dark" onclick="window.appTheme.setDark(true)">
                    <MudIconButton Icon="@Icons.Material.Filled.DarkMode" title="切換深色模式" />
                </span>
                <span class="theme-btn-light" onclick="window.appTheme.setDark(false)">
                    <MudIconButton Icon="@Icons.Material.Filled.LightMode" title="切換淺色模式" />
                </span>
                <LogoutButton />
            </MudStack>
            <MudText Typo="Typo.caption" Class="d-block text-center">@AppVersion</MudText>
        </div>
    </MudDrawer>
    <MudMainContent>
        <div class="pa-4">
            @Body
        </div>
    </MudMainContent>
</MudLayout>
```

`MainLayout.razor.cs` 移除所有狀態管理，只保留 `AppVersion` property。

### 二、NavMenu 換成 MudNavMenu

```razor
<MudNavMenu>
    <MudNavLink Href="/" Icon="@Icons.Material.Filled.Home" Match="NavLinkMatch.All">首頁</MudNavLink>
    <MudNavLink Href="/tasks" Icon="@Icons.Material.Filled.List">任務列表</MudNavLink>
    <MudNavLink Href="/pipeline" Icon="@Icons.Material.Filled.AccountTree">流程追蹤</MudNavLink>
    <MudNavLink Href="/deployments" Icon="@Icons.Material.Filled.RocketLaunch">部署紀錄</MudNavLink>
    <MudNavLink Href="/projects" Icon="@Icons.Material.Filled.Folder">專案管理</MudNavLink>
    <MudNavLink Href="/agents" Icon="@Icons.Material.Filled.SmartToy">Agent 設定</MudNavLink>
    <MudNavLink Href="/office" Icon="@Icons.Material.Filled.GridView">Team Office</MudNavLink>
    <MudNavLink Href="/rules" Icon="@Icons.Material.Filled.Rule">規則管理</MudNavLink>
    <MudNavLink Href="/tokens" Icon="@Icons.Material.Filled.BarChart">Token 監控</MudNavLink>
</MudNavMenu>
```

`LogoutButton.razor` 改為 `MudIconButton`，與主題切換按鈕並排（`MudStack Row`）。

### 三、Dark Mode — CSS 變數方案

由於 Layout 不能加 `@rendermode`（見踩坑一、二），無法用 MudThemeProvider 的 C# binding 動態切換。

改用純 JS + CSS 方案：

- `window.appTheme.setDark(bool)` 設定 `localStorage` 並切換 `html[data-theme="dark"]`
- `app.css` 用 `html[data-theme="dark"] { --mud-palette-* }` 覆寫 MudBlazor CSS 變數
- **雙圖示 CSS**：`html:not([data-theme="dark"]) .theme-btn-light { display: none }` / `html[data-theme="dark"] .theme-btn-dark { display: none }`

### 四、MudDrawer Temporary（三處 Drawer）

```razor
@* TaskLogDrawer / PipelineList / ProjectManagement *@
<MudDrawer @bind-Open="_open" Anchor="Anchor.End"
           Variant="DrawerVariant.Temporary" OverlayAutoClose="true">
    @* 內容 *@
</MudDrawer>
```

移除原有 `.slide-panel` CSS 和對應的 JS 邏輯。

### 五、App.razor — JS Helper

```javascript
// Sidebar toggle（見踩坑三）
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
    } else {
        drawer.classList.remove('mud-drawer--closed');
        drawer.classList.add('mud-drawer--open');
        layout.classList.add('mud-drawer-open-persistent-left');
    }
};
```

### 六、Routes.razor（全域 InteractiveServer）

**新增** `Routes.razor`（見踩坑四、五）：

```razor
@rendermode @(new InteractiveServerRenderMode(prerender: false))

<CascadingAuthenticationState>
    <Router AppAssembly="@typeof(App).Assembly">
        <Found Context="routeData">
            <AuthorizeRouteView RouteData="@routeData" DefaultLayout="@typeof(Layout.MainLayout)">
                <NotAuthorized>...</NotAuthorized>
            </AuthorizeRouteView>
        </Found>
    </Router>
</CascadingAuthenticationState>
```

`App.razor` 的 `<body>` 改為只有一行 `<Routes />`。

### 七、MudProviders.razor（還原 Interactive rendermode）

```razor
@rendermode @(new InteractiveServerRenderMode(prerender: false))
<MudThemeProvider />
<MudDialogProvider />
<MudSnackbarProvider />
<MudPopoverProvider />
```

### 八、清理 app.css

移除約 300 行自訂 Layout / NavMenu / slide-panel CSS，保留：
- `.status-badge` 系列
- Dark Mode CSS 變數覆寫（`html[data-theme="dark"]`）
- 雙圖示切換 CSS（`.theme-btn-dark` / `.theme-btn-light`）

---

## 踩坑記錄

### 踩坑一：Blazor 元件屬性上的 `onclick` 被視為 C# 表達式

**問題**：`<MudIconButton onclick="window.foo()" />` 導致 build error CS0103（`window` 在 C# context 中不存在）。

**原因**：Razor 將元件屬性上的 `onclick` 解析為 C# 委派表達式，只有原生 HTML 元素上的 `onclick` 才是字串屬性。

**修正**：改用 `<span onclick="window.foo()"><MudIconButton ... /></span>`，onclick 放在 HTML wrapper 上。

---

### 踩坑二：Layout 元件加 @rendermode 導致 HTTP 500

**問題**：嘗試在 `MainLayout.razor` 加 `@rendermode InteractiveServerRenderMode` 讓按鈕可以用 C# OnClick，登入後 HTTP 500。

**錯誤訊息**：
```
System.InvalidOperationException: Cannot pass the parameter 'Body' to component
'MainLayout' with rendermode 'InteractiveServerRenderMode'.
This is because the parameter is of the delegate type
'Microsoft.AspNetCore.Components.RenderFragment',
which is arbitrary code and cannot be serialized.
```

**原因**：Blazor Web App 的 Layout 元件接收 `@Body`（RenderFragment 委派），無法跨 SSR→Interactive 邊界序列化。**Layout 元件只要接收 `@Body` 就絕對不能加 @rendermode InteractiveServer**，這是框架層級的限制。

**教訓**：Layout 互動只能用 JS（onclick 放 HTML elements）或 Interactive "island" leaf components（不接收 RenderFragment 的元件）。

---

### 踩坑三：MudBlazor Drawer toggle 需操作兩組 CSS class

**問題**：用 `mud-drawer-closed`（單 hyphen）沒有任何效果，drawer 不動。

**原因**：MudBlazor Persistent Drawer 使用雙 hyphen class：
- Drawer 本身：`mud-drawer--open` / `mud-drawer--closed`（雙 hyphen）
- `.mud-layout` 容器：`mud-drawer-open-persistent-left`（控制 `.mud-main-content` 的 `margin-left`）

**修正**：同時切換兩組 class，並移除 `mud-drawer--initial`（初始化動畫標記）以啟用滑動動畫。

---

### 踩坑四：MudProviders 在 Static SSR Layout 造成跨 Circuit 服務隔離

**問題**：導航到任何頁面（如 TaskCenter）就 circuit 崩潰，拋出：
```
Missing <MudPopoverProvider />, please add it to your layout.
```

**根本原因**：在 Blazor Web App：
- `MudProviders.razor`（Interactive island in Static SSR Layout）→ 建立 **Circuit A**
- 頁面元件（另一個 Interactive island via Router）→ 建立 **Circuit B**

兩個 circuit 的 scoped `IPopoverService` 是不同實例，Circuit B 中的頁面找不到 Circuit A 裡的 MudPopoverProvider。

**修正**：建立 `Routes.razor` 並宣告全域 `@rendermode InteractiveServer(prerender: false)`，讓 Router、Layout、頁面全部在同一 circuit 執行，scoped services 共享。

---

### 踩坑五：全域 InteractiveServer 讓 Login 白畫面並崩潰

**問題**：Routes.razor 設為 InteractiveServer 後，Login 頁完全白畫面，circuit 立即崩潰。

**雙重原因**：
1. `prerender: false` → 初始 HTTP response 不含任何頁面 HTML，等 WebSocket 連線後才渲染。這是預期行為，稍等就能出現。
2. `Login.razor.cs` 使用 `[CascadingParameter] HttpContext`，在 Interactive 模式下 HttpContext 不作為 Cascading Parameter 提供，為 null → `NullReferenceException` 崩潰 circuit。

**修正**：`Login.razor.cs` 改注入 `NavigationManager`，用 `QueryHelpers.ParseQuery(new Uri(Navigation.Uri).Query)` 解析 query string，移除對 `HttpContext` 的依賴。

**教訓**：全域 InteractiveServer 模式下，任何使用 `HttpContext` 的元件都需改用 Interactive 相容的 API。`HttpContext` 只在 Static SSR 的 request 週期內可用。

---

## 最終架構（關鍵設計決策）

```
App.razor（Static SSR HTML shell）
  └── <Routes />（@rendermode InteractiveServer，全局 circuit root）
        └── <CascadingAuthenticationState>
              └── <Router>
                    └── MainLayout（在 Routes 的 circuit 內，不需自己的 @rendermode）
                          ├── <MudProviders />（與 Layout/Pages 共享 circuit）
                          ├── <MudDrawer Persistent>（sidebar，JS toggle）
                          └── <MudMainContent>
                                └── @Body（頁面元件，同一 circuit）
```

**核心原則**：
- Layout 不加 `@rendermode`（接收 `@Body` 的 Layout 加了會 500）
- 互動靠 `onclick` on HTML elements 或 Interactive "island" leaf components
- 全局 `<Routes @rendermode InteractiveServer>` 讓所有元件共享 circuit，MudBlazor scoped services 正確共享
- Dark Mode 用 CSS 變數 + JS（MudThemeProvider binding 需要 C# 狀態，靜態 Layout 做不到）

---

## 驗收結果

| 項目 | 結果 |
|------|------|
| `dotnet build` 0 errors | ✅ |
| 登入頁正常顯示（全域 Interactive 下 Login 不崩） | ✅ |
| 首頁 MudLayout 正常 | ✅ |
| 漢堡鈕 sidebar toggle（含滑動動畫） | ✅ |
| Dark Mode 切換 + reload 後持久 | ✅ |
| 任務列表頁正常載入（circuit 不崩） | ✅ |
| TaskLogDrawer — MudDrawer Temporary + overlay close | ✅ |
| 流程追蹤 PipelineView Drawer | ✅ |
| 專案管理 Drawer | ✅ |
| 部署紀錄、Agent設定、Team Office、規則管理、Token監控 | ✅ |

---

## Commits

| Commit | 說明 |
|--------|------|
| `8ab65b3` | feat(stage20): Dashboard 全面換 MudBlazor Layout（初版） |
| `cfe00cb` | fix(stage20): 補 MainLayout @rendermode（有誤，導致 HTTP 500） |
| `b9d0293` | fix(stage20): 移除 @rendermode，改用 JS onclick（修正 HTTP 500） |
| `0b10b43` | fix(stage20): 修正漢堡鈕 — 正確 MudBlazor CSS class（雙 hyphen） |
| `e280d1c` | fix(stage20): 還原 MudProviders.razor Interactive rendermode（仍有跨 circuit 問題） |
| `db8f9ef` | fix(stage20): 建立 Routes.razor 全域 InteractiveServer，根治 MudPopoverProvider 問題 |
| `d09218e` | fix(stage20): 修正全域 InteractiveServer 造成 Login 崩潰（HttpContext → NavigationManager） |

---

## 後續

Stage 20 完成後，接續執行 **Stage 19 Pt.2**（首頁重構、Agent 設定改版、表單 MudDialog、多選篩選等），此時 Layout 已穩定，改起來一次到位。

---

## 變更紀錄

| 日期 | 版本 | 內容 |
|------|------|------|
| 2026-04-10 | v1.0 | 初版建立 |
| 2026-04-10 | v1.1 | 實作完成（MainLayout/NavMenu/LogoutButton/三個 Drawer/app.css）；dotnet build 0 errors |
| 2026-04-11 | v2.0 | 驗收完成；補充五項踩坑完整紀錄、最終架構決策、Routes.razor 全域 InteractiveServer 方案、Login NavigationManager 修正 |
