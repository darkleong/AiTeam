# Stage 19 — Dashboard UI 全面打磨

> 版本：v2.2
> 建立日期：2026-04-08
> 更新日期：2026-04-11
> 狀態：✅ 第一批、第二批均已完成

---

## 執行順序說明

| 批次 | 狀態 | 說明 |
|------|------|------|
| 第一批（🔴 高優先） | ✅ 已完成 | StatusBadge、PipelineList、MudSwitch、表格 FixedHeader 等 |
| 第二批（🟡 中優先） | ✅ 已完成 | 2026-04-11 實作完成 |
| 第三批（🔵 低優先） | ⏸️ 待排 | 第二批完成後接續 |

---

## 目標

全面修正 Dashboard 的 UI 顯示缺陷、樣式不一致與操作體驗問題。依優先級分三批實作，避免一次改太多出 bug。

---

## 第一批：高優先（🔴 視覺明顯 / 功能性問題）✅ 已完成

### 1. StatusBadge 補齊缺少的狀態

**檔案：** `Components/Shared/StatusBadge.razor`、`wwwroot/css/app.css`

`GetLabel()` switch 缺少 Stage 16 新增的狀態：

| 狀態 | 標籤 | 顏色 |
|------|------|------|
| `revision` | 修正中 | 橘色（`--color-status-warning`） |
| `reviewing` | 審核中 | 藍紫色 |
| `cancelled` | 已取消 | 灰色 |

同步新增 `app.css` 對應樣式與缺失的 CSS 變數（`--color-status-warning`、`--color-status-revision`、`--color-status-reviewing`）。

---

### 2. 流程追蹤獨立成頁

**現況：** 流程追蹤藏在任務中心的 Tab 2，需要兩層點擊，且表格過寬造成水平 Scrollbar，Drawer 被藏在右邊。

**改善：**
- 側邊欄新增「流程追蹤」獨立項目，路由 `/pipeline`
- 原「任務中心」改名為「任務列表」，路由維持 `/tasks`
- `TaskCenter.razor` Tab 2 內容移至新的 `PipelineList.razor`（獨立頁面）
- PipelineView Drawer 繼續沿用，不需修改
- 表格欄寬限制在可視區域內，消除水平 Scrollbar

---

### 3. PipelineView Drawer 點擊外部自動關閉

**改善：** MudDrawer 加上 `OverlayAutoClose="true"`，點擊 Drawer 外部即自動關閉，不需點關閉按鈕。

---

### 4. 表格 Scrollbar 改為元件內 + PageSize 預設 10

**適用頁面：** 任務列表、流程追蹤、規則管理、專案管理、部署紀錄

- 所有 MudTable 加上 `Height="600px" FixedHeader="true"`，Scrollbar 限制在表格內
- `RowsPerPage` 預設改為 10，加上 `PageSizeOptions="new int[]{10, 25, 50}"`

---

### 5. CSS 缺失修補

補定義或改用 MudBlazor 原生元件：

| Class | 使用位置 | 處理方式 |
|------|---------|---------|
| `.slide-panel` / `.slide-panel-overlay` | TaskLogDrawer, ProjectManagement | 改用 MudDrawer |
| `.btn-close` | 各 Drawer | 改用 MudDrawer 後移除 |
| `.active-toggle` / `.toggle-label` | AgentSettings, RuleManagement | 改用 MudSwitch |
| `.alert-info` | AgentSettings | 改用 MudAlert |
| `.cursor-pointer` | 各 MudTable 行 | 補定義 `cursor: pointer` |
| `.text-muted` | AgentSettings | 補定義或用 MudText Typo |

---

### 6. Toggle 統一改用 MudSwitch

AgentSettings、RuleManagement 的原生 checkbox 全部改用 MudSwitch：

```razor
<MudSwitch T="bool" @bind-Value="_isEnabled"
           Color="Color.Primary"
           Label="啟用" />
```

---

## 第二批：中優先（🟡 UX 改善）

> **架構前提說明（Stage 20 已完成，實作前請閱讀）**
>
> Stage 20 完成後，Dashboard 的 render mode 架構**維持 Per-page Interactive 不變**：各頁面自行宣告 `@rendermode @(new InteractiveServerRenderMode(prerender: false))`，`MainLayout` 維持 Static SSR。
>
> 這代表 Stage 19 Pt.2 實作時：
> - **`MudThemeProvider` C# binding 仍不適用**：Layout 因接收 `@Body`（RenderFragment）無法加 rendermode，Dark Mode 繼續維持 CSS 變數 + JS 方案（`html[data-theme="dark"]`）
> - **`MudDialogProvider` / `IDialogService` 可正常運作**：頁面元件本身是 Interactive，呼叫 `IDialogService.ShowAsync()` 完全沒問題
> - **MudDialog 表單**（Pt.2 主要改善項）現在有穩定的 MudLayout 基礎，可直接實作
>
> 若未來需要在 Layout 持有 C# 狀態，可考慮升級為全域 Interactive（`App.razor` 的 Router 改用 `<Routes @rendermode="InteractiveServer" />`），但目前收益不足以觸發此變更。

---

### 7. 首頁佈局重構

**現況：** Agent 卡片 + 最近任務垂直疊放，最近任務需 scroll 才看到；且最近任務顯示 TaskItem 細節過多。

**改善：**
- Agent 卡片縮成緊湊小卡（名稱 + 狀態 Badge + 今日完成數），高度約 60px，兩行排完
- 下方「最近任務」改為「**最近流程**」，資料來源改為 TaskGroup
- 最近流程顯示欄位：流程名稱、類型 Badge、狀態 Badge、建立時間、PR 連結
- 預設顯示最近 10 筆

---

### 8. 流程類型 / 觸發來源 改用 Badge

**流程追蹤頁 — 流程類型欄：**

| 值 | Badge 顏色 |
|---|---|
| 新功能 | Color.Primary（藍） |
| Bug Fix | Color.Warning（橘） |
| 技術改善 | Color.Secondary（紫） |

**任務列表頁 — 觸發來源欄：**
- `Orchestrator` → 灰色 Badge
- `Discord` → 藍紫色 Badge

---

### 9. 狀態篩選改為多選

改用 `MudSelect` 的 `MultiSelection="true"` 模式，允許同時勾選多個狀態（例如「執行中 + 失敗」）。任務列表與流程追蹤頁同步更新。

---

### 10. Agent 設定頁面改版

**現況：** 每個 Agent 一張大卡片，往下 scroll 才能看到下一個。

**改善：** 左右雙欄佈局：
- 左側：MudList 顯示所有 Agent 名稱 + 啟用狀態
- 右側：點選 Agent 後顯示詳情（描述、信任等級 Slider、儲存按鈕）

---

### 11. 新增 / 編輯表單改用 MudDialog

**適用：**
- 規則管理 → 新增 / 編輯規則
- Agent 設定 → 新增 Agent
- 專案管理 → 新增專案

---

### 12. Token 監控標題列 Sticky

向下 scroll 查看圖表時，「Token 監控」標題 + 「今日/本週/本月」切換按鈕列加上 `position: sticky; top: 0`，保持可見。Agent 卡片與圖表正常捲動。

---

### 13. 左側欄主題切換按鈕統一樣式

「主題切換（✳️）」按鈕樣式與「登出」按鈕風格不一致，改用統一的 MudIconButton 或補上 hover 效果。

---

### 14. TaskLogDrawer `Open` + `OpenChanged` 正確接線

**檔案：** `Components/Shared/TaskLogDrawer.razor`

**問題：** 目前使用 `@bind-Open="IsOpen"`，展開後等於 `OpenChanged="(v) => IsOpen = v"`，只直接 mutate 參數，**不會呼叫父元件的 `IsOpenChanged` EventCallback**。使用者點 overlay 關閉 Drawer 後，父元件（TaskCenter、DeploymentHistory）的 `_isDrawerOpen` 狀態不同步，下次點擊同一列時 Drawer 不重新載入選中任務。

**修法：**
```razor
<MudDrawer Open="@IsOpen"
           OpenChanged="IsOpenChanged"
           ...>
```

---

### 15. MudProviders.razor 對齊 `prerender: false`

**檔案：** `Components/Layout/MudProviders.razor`

**問題：** 目前使用 `@rendermode InteractiveServer`（預設有 prerender），其他頁面一律使用 `InteractiveServerRenderMode(prerender: false)`。不一致可能造成 providers 初始化閃爍。

**修法：**
```razor
@rendermode @(new InteractiveServerRenderMode(prerender: false))
```

---

## 第三批：低優先（🔵 細節）

- inline `style="color:red"` 等硬編碼顏色，改為 CSS 變數
- `.agent-setting-card` 語義不正確，改用 `.form-card` 或 MudCard
- Empty State emoji 改用 MudBlazor MudIcon（跨瀏覽器一致）
- inline 布局 style 改用 MudGrid / MudStack

---

## 驗收條件

### 第一批
- [ ] `revision` / `reviewing` / `cancelled` 狀態 Badge 樣式正確
- [ ] 側邊欄新增「流程追蹤」獨立項目，點一下即可進入
- [ ] 「任務中心」已改名為「任務列表」
- [ ] 流程追蹤表格欄寬正常，無水平 Scrollbar，Drawer 可從右側滑出
- [ ] PipelineView Drawer 點擊外部自動關閉
- [ ] 所有 MudTable Scrollbar 在元件內，PageSize 預設 10
- [ ] 缺失 CSS class 補齊或改用 MudBlazor 元件
- [ ] Toggle 統一使用 MudSwitch

### 第二批
- [x] 首頁 Agent 卡片改為緊湊小卡
- [x] 首頁「最近任務」改為「最近流程」（TaskGroup），顯示類型 Badge、狀態、PR 連結
- [x] 流程類型、觸發來源欄位以 Badge 顯示
- [x] 狀態篩選支援多選
- [x] Agent 設定改為左右雙欄佈局
- [x] 新增 / 編輯表單改用 MudDialog
- [x] Token 監控標題列 sticky
- [x] 主題切換按鈕樣式統一
- [x] TaskLogDrawer overlay 關閉後父元件狀態正確同步（提前完成，Stage 19 Pt.1 已修）
- [x] MudProviders.razor prerender 對齊其他頁面（提前完成，Stage 20 已修）

### 共同
- [ ] 無硬編碼 `style="color:red"` 等 inline 顏色（第一批修高優先，其餘第二批）
- [ ] Dashboard 各頁面 Dark Mode 下顯示正常
- [ ] 用 Mock Mode 觸發完整流程，確認 Pipeline View + 狀態 Badge 配合正確
- [ ] `dotnet build` 通過

---

## 附錄：已知 UI 問題清單

| # | 問題 | 頁面 | 優先級 | 批次 |
|---|------|------|--------|------|
| 1 | `revision` / `reviewing` / `cancelled` 狀態 Badge 無樣式 | 全域 | 🔴 高 | 第一批 |
| 2 | 流程追蹤是 Tab 而非獨立頁面，導覽層級太深 | 任務中心 | 🔴 高 | 第一批 |
| 3 | 流程追蹤表格過寬，水平 Scrollbar 把 Drawer 藏到右邊 | 流程追蹤 | 🔴 高 | 第一批 |
| 4 | PipelineView Drawer 點擊外部無法關閉 | 流程追蹤 | 🔴 高 | 第一批 |
| 5 | 表格使用瀏覽器 Scrollbar，PageSize 預設 50 | 多頁面 | 🔴 高 | 第一批 |
| 6 | `.slide-panel` / `.active-toggle` 等 CSS 未定義 | 多頁面 | 🔴 高 | 第一批 |
| 7 | Toggle 顯示原生 checkbox（未套用 MudSwitch） | Agent 設定 | 🔴 高 | 第一批 |
| 8 | 首頁佈局：Agent 卡片 + 最近任務垂直疊放，最近任務需 scroll | 首頁 | 🟡 中 | 第二批 |
| 9 | 首頁「最近任務」顯示 TaskItem 細節，資訊過細 | 首頁 | 🟡 中 | 第二批 |
| 10 | 流程類型 / 觸發來源為純文字，辨識度低 | 任務中心 | 🟡 中 | 第二批 |
| 11 | 狀態篩選為單選，無法多選 | 任務中心 | 🟡 中 | 第二批 |
| 12 | Agent 設定大卡片佈局，瀏覽費力 | Agent 設定 | 🟡 中 | 第二批 |
| 13 | 新增表單 inline 展開，擠壓頁面空間 | 多頁面 | 🟡 中 | 第二批 |
| 14 | Token 監控捲動時標題列消失 | Token 監控 | 🟡 中 | 第二批 |
| 15 | 主題切換按鈕樣式與登出按鈕不一致 | 全域 | 🟡 中 | 第二批 |
| 16 | inline `style="color:red"` 硬編碼顏色 | 多頁面 | 🔵 低 | 第三批 |
| 17 | `.agent-setting-card` 語義不正確，被各頁面混用 | 多頁面 | 🔵 低 | 第三批 |
| 18 | Empty State 使用 emoji，跨瀏覽器顯示不一致 | 多頁面 | 🔵 低 | 第三批 |

---

## 第二批：實作紀錄（2026-04-11）

> commit: f2437a1

### 新建檔案

| 檔案 | 說明 |
|------|------|
| `Pages/Agents/Dialogs/AgentCreateDialog.razor` | 新增 Agent Dialog（Name / Description / TrustLevel 欄位） |
| `Pages/Projects/ProjectCreateDialog.razor` | 新增專案 Dialog（Name / RepoUrl / TechStack） |
| `Pages/Rules/RuleFormDialog.razor` | 新增＋編輯合一；`[Parameter] Rule? EditingRule` 決定模式 |

### 關鍵實作模式

#### MudDialog 標準寫法
```razor
@* Dialog 元件內 *@
[CascadingParameter] IMudDialogInstance MudDialog { get; set; } = null!;

@* 確認：回傳資料 *@
MudDialog.Close(DialogResult.Ok(created));

@* 取消 *@
MudDialog.Cancel();
```

```csharp
@* 呼叫端（父頁面）*@
var dialog = await DialogService.ShowAsync<RuleFormDialog>("新增規則", parameters);
var result = await dialog.Result;

// result 可能為 null（視 MudBlazor 版本）；用 pattern matching 安全取值
if (result is { Canceled: false } && result.Data is Rule created)
    _rules.Add(created);
```

#### MudSelect 多選 + 觸發 ReloadServerData
```razor
<MudSelect T="string" MultiSelection="true"
           @bind-SelectedValues="_statusFilters"
           @bind-SelectedValues:after="OnStatusFilterChangedAsync"
           Label="篩選狀態" Dense="true">
    <MudSelectItem Value="@("running")">執行中</MudSelectItem>
    ...
</MudSelect>
```
```csharp
// _statusFilters 型別：IEnumerable<string>（不是 List<string>）
private IEnumerable<string> _statusFilters = [];

private async Task OnStatusFilterChangedAsync()
    => await _tableRef.ReloadServerData();
    // 注意：@bind-SelectedValues 已自動同步值，這裡只需觸發重載，不用再讀 _statusFilters
```

#### Service 層多值篩選
```csharp
// DashboardTaskService：參數改為 IReadOnlyCollection<string>?
public async Task<...> GetTaskGroupsPagedAsync(
    ...,
    IReadOnlyCollection<string>? statusFilters = null,
    ...)
{
    var query = db.TaskGroups.AsNoTracking();
    if (statusFilters is { Count: > 0 })
        query = query.Where(g => statusFilters.Contains(g.Status));
    ...
}
```

#### MudList 左右雙欄（Agent 設定）
```razor
<MudList T="AgentConfigDto" @bind-SelectedValue="_selectedAgent">
    @foreach (var agent in _agents)
    {
        <MudListItem Value="@agent">@agent.Name</MudListItem>
    }
</MudList>
```
> **重點**：`@bind-SelectedValue` 展開後已包含 `SelectedValueChanged`，**不可再手動加 `SelectedValueChanged` 參數**，否則編譯錯誤「參數被設定超過一次」。

#### Token 監控 Sticky 標題列
```razor
<div class="page-header" style="position:sticky; top:0; z-index:10; background:var(--color-bg-primary)">
```

#### 主題切換按鈕 hover CSS
```css
/* 不依賴 MudBlazor 內部 class，避免升版失效 */
.theme-btn-dark:hover,
.theme-btn-light:hover {
    opacity: 0.8;
    border-radius: 50%;
}
```

### 踩坑紀錄

| # | 問題 | 原因 | 修法 |
|---|------|------|------|
| 1 | `CS0103 _showCreateForm` 不存在 | `.razor` 的 empty state 條件 `!_showCreateForm` 忘了一起移除 | 改為 `@if (_rules.Count == 0)` |
| 2 | `CS8602` 可能 null 參考（4 個 warning） | `await dialog.Result` 回傳 `DialogResult?`，直接取 `.Canceled` / `.Data` 會警告 | 改用 `result is { Canceled: false } && result.Data is T val` pattern matching |
| 3 | `@bind-SelectedValues` 不觸發重載 | `@bind-SelectedValues` 只同步值，不觸發 side effect | 加上 `@bind-SelectedValues:after="OnStatusFilterChangedAsync"` |
| 4 | MudList `@bind-SelectedValue` + `SelectedValueChanged` 衝突 | `@bind-SelectedValue` 展開後已含 `SelectedValueChanged`；兩者共存編譯錯誤 | 移除 `SelectedValueChanged` 及對應的 `OnAgentSelected` 方法 |
| 5 | 主題按鈕 hover 靠 `.mud-icon-button` 不穩 | MudBlazor 內部 class 可能隨版本改名 | 改用容器 span 的 `opacity: 0.8`（`--mud-palette-*` 不受影響） |

---

## 驗收結果（第二批，2026-04-11）

| 項目 | 功能 | 結果 |
|------|------|------|
| Item 7 | 首頁緊湊 Agent 小卡 + 最近流程（TaskGroup） | ✅ |
| Item 8 | 流程類型 / 觸發來源 MudChip Badge（橘/藍/紫等彩色） | ✅ |
| Item 9 | 狀態篩選改為 MudSelect 多選 | ✅ |
| Item 10 | Agent 設定左右雙欄（點選 CEO → 右側顯示詳情） | ✅ |
| Item 11 | 新增規則 → MudDialog 彈出表單 | ✅ |
| Item 12 | Token 監控標題列 sticky（捲動時不消失） | ✅ |
| Item 13 | 主題切換按鈕 hover CSS 補齊 | ✅ |

`dotnet build` 通過（0 errors、0 warnings）。

---

## 變更紀錄

| 版本 | 日期 | 內容 |
|------|------|------|
| v1.0 | 2026-04-08 | 初版建立 |
| v1.1 | 2026-04-09 | 新增導覽 UX 重構、表格改善、首頁佈局、MudDialog |
| v2.0 | 2026-04-09 | 全面重整結構，依優先級分三批，整合所有 UI 問題清單（18 項） |
| v2.1 | 2026-04-10 | 第一批驗收完成；第二三批暫緩，待 Stage 20（全面換 MudBlazor Layout）完成後接續 |
| v2.2 | 2026-04-11 | Stage 20 驗收完成；第二批狀態改為進行中；架構說明修正（Per-page Interactive 維持不變） |
| v2.3 | 2026-04-11 | 第二批實作完成（7 項：首頁重構、Badge、多選篩選、Agent 雙欄、MudDialog 表單、Sticky、hover）|
| v3.0 | 2026-04-11 | 補充第二批完整實作紀錄（新建檔案、關鍵模式、五項踩坑、驗收結果）|
