# Stage 20 — Dashboard 全面換 MudBlazor Layout

> 版本：v1.1
> 建立日期：2026-04-10
> 狀態：🔄 實作完成，待驗收

---

## 背景

Stage 19 Pt.1 實作中發現，Dashboard 目前的 `MainLayout` 為自訂結構，並非 MudBlazor 的 `MudLayout`，導致 `MudDrawer Temporary` 模式的 overlay 無法正確運作。整體呈現「混搭」狀態：部分元件用 MudBlazor，Layout 與 NavMenu 仍是自訂 HTML。

Stage 19 Pt.2 的多項改善（MudDialog 表單、MudDrawer 全面接通、Dark Mode 統一管理）都依賴穩定的 MudLayout 基礎，因此決定先做 Stage 20，將 Layout 全面換成 MudBlazor，再回頭完成 Stage 19 Pt.2。

---

## 目標

將 Dashboard 的 Layout 基礎設施全面換成 MudBlazor，讓後續所有 UI 改善能在穩定的元件體系上進行。

---

## 一、MainLayout 換成 MudLayout

**現況：** `MainLayout.razor` 使用自訂 HTML 結構（`<div class="app-container">` 等）。

**改善：**
```razor
<MudThemeProvider @ref="_themeProvider" Theme="_theme" @bind-IsDarkMode="_isDarkMode" />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="1">
        @* 頂部 AppBar（可選）*@
    </MudAppBar>
    <MudDrawer @bind-Open="_drawerOpen" Variant="DrawerVariant.Persistent" Elevation="2">
        <NavMenu />
    </MudDrawer>
    <MudMainContent>
        @Body
    </MudMainContent>
</MudLayout>
```

---

## 二、NavMenu 換成 MudNavMenu

**現況：** 自訂 `<nav>` + `<NavLink>` + emoji icon，`.nav-item` / `.nav-text` CSS。

**改善：** 換成 MudBlazor 原生元件：
```razor
<MudNavMenu>
    <MudNavLink Href="/" Icon="@Icons.Material.Filled.Home" Match="NavLinkMatch.All">首頁</MudNavLink>
    <MudNavLink Href="/tasks" Icon="@Icons.Material.Filled.List">任務列表</MudNavLink>
    <MudNavLink Href="/pipeline" Icon="@Icons.Material.Filled.AccountTree">流程追蹤</MudNavLink>
    <MudNavLink Href="/deployments" Icon="@Icons.Material.Filled.RocketLaunch">部署紀錄</MudNavLink>
    <MudNavLink Href="/projects" Icon="@Icons.Material.Filled.Folder">專案管理</MudNavLink>
    <MudNavLink Href="/agents" Icon="@Icons.Material.Filled.SmartToy">Agent 設定</MudNavLink>
    <MudNavLink Href="/rules" Icon="@Icons.Material.Filled.Rule">規則管理</MudNavLink>
    <MudNavLink Href="/tokens" Icon="@Icons.Material.Filled.BarChart">Token 監控</MudNavLink>
</MudNavMenu>
```

sidebar 底部的「主題切換」和「登出」也改用 MudBlazor 元件，風格統一。

---

## 三、Dark Mode 改由 MudThemeProvider 管理

**現況：** Dark Mode 靠 CSS class（`class="dark-mode"`）切換，`app.css` 用 `.dark-mode` selector 覆寫變數。

**改善：**
- `MudThemeProvider` 的 `@bind-IsDarkMode` 統一管理
- 移除 `app.css` 中的 `.dark-mode` CSS 覆寫區段
- 保留 `--color-*` 自訂變數供非 MudBlazor 元件使用，但顏色值由 Theme 決定

---

## 四、MudDrawer 全面接通

Layout 換成 MudLayout 後：

- **TaskLogDrawer** — 移除 `.slide-panel` CSS，改用 `MudDrawer Variant="DrawerVariant.Temporary" Anchor="Anchor.End" OverlayAutoClose="true"`
- **PipelineView Drawer**（PipelineList.razor）— 同上
- **ProjectManagement 詳情**（Stage 19 Pt.1 遺留）— 同上

---

## 五、MudDialogProvider 接通

`MainLayout` 加上 `<MudDialogProvider />` 後，Stage 19 Pt.2 的表單 MudDialog 才能正常運作。

---

## 六、清理自訂 CSS

完成 MudLayout 後，可以移除或簡化 `app.css` 中大量的自訂 Layout CSS：

- `.app-container`、`.sidebar`、`.main-content` 等結構 class
- `.nav-item`、`.nav-text`、`.nav-icon` 等 NavMenu 樣式
- `.dark-mode` 覆寫區段

保留：
- 自訂 `.status-badge` 系列（MudBlazor 沒有對應元件）
- 頁面專屬的少量自訂樣式

---

## 執行順序

1. 確認 MudThemeProvider / MudDialogProvider / MudSnackbarProvider 設定
2. MainLayout → MudLayout（包含 MudDrawer Persistent 側邊欄）
3. NavMenu → MudNavMenu（包含底部主題切換 + 登出統一樣式）
4. Dark Mode → MudThemeProvider 管理
5. TaskLogDrawer / PipelineView Drawer → MudDrawer Temporary（overlay 正確）
6. ProjectManagement 詳情 Drawer → MudDrawer
7. 清理 app.css 自訂 Layout CSS
8. `dotnet build` + 全頁面目測驗收

---

## 風險評估

| 風險 | 影響 | 緩解措施 |
|------|------|---------|
| MainLayout 換掉後所有頁面排版跑掉 | 高 | 逐步換，先確認基本 Layout 正確再換 NavMenu |
| Dark Mode 切換行為改變 | 中 | 測試 Light / Dark 兩種主題下每個頁面 |
| MudDrawer Persistent 側邊欄寬度影響內容區 | 中 | 調整 MudMainContent padding / margin |
| CSS 清理過度，刪掉仍在使用的 class | 中 | 先用 grep 確認每個 class 的使用位置再刪 |

---

## 驗收條件

- [ ] `dotnet build` 通過，無 error
- [ ] 首頁、任務列表、流程追蹤、Agent 設定、規則管理、Token 監控 — 所有頁面排版正常
- [ ] Dark Mode / Light Mode 切換正常，所有頁面顯示一致
- [ ] 側邊欄收合（MudDrawer Persistent）正常運作
- [ ] TaskLogDrawer 點擊外部自動關閉（MudDrawer Temporary overlay）
- [ ] PipelineView Drawer 點擊外部自動關閉
- [ ] MudDialog 可正常彈出（為 Stage 19 Pt.2 準備）
- [ ] NavMenu 樣式與「登出」、「主題切換」風格一致
- [ ] 用 Mock Mode 跑一次完整流程，確認功能正常

---

## 後續

Stage 20 完成後，接續執行 **Stage 19 Pt.2**（首頁重構、Agent 設定改版、表單 MudDialog、多選篩選等），此時 Layout 已穩定，改起來一次到位。

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-10 | v1.0 初版建立 |
| 2026-04-10 | v1.1 實作完成（MainLayout/NavMenu/LogoutButton/三個 Drawer/app.css）；dotnet build 0 errors |
