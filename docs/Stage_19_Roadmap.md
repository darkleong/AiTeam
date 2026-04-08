# Stage 19 — Dashboard UI 全面打磨

> 版本：v1.0
> 建立日期：2026-04-08
> 狀態：📋 規劃中

---

## 目標

全面修正 Dashboard 的 UI 顯示缺陷與樣式不一致問題，提升使用體驗的一致性和完整性。

---

## 一、狀態 Badge 修正（Critical）

### 1.1 StatusBadge.razor 缺少 `revision` / `reviewing` 狀態

**檔案：** `Components/Shared/StatusBadge.razor`

**現況：** `GetLabel()` switch 只處理 pending / running / done / failed / idle / error，缺少 Stage 16 新增的 `revision` 和 `reviewing` 狀態。

**修正：**
- 新增 `revision` → 橘色 Badge，標籤「修正中」
- 新增 `reviewing` → 藍紫色 Badge，標籤「審核中」
- 新增 `cancelled` → 灰色 Badge，標籤「已取消」
- `app.css` 新增 `.status-revision`、`.status-reviewing`、`.status-cancelled` 樣式

---

## 二、任務詳情顯示方式改善

### 2.1 現況問題

點擊任務後，詳情以自訂 `.slide-panel` 在表格下方或右側滑出，但：
- `.slide-panel`、`.slide-panel-overlay`、`.btn-close` CSS **未定義**，樣式可能不完整
- 不同頁面（TaskCenter / DeploymentHistory / ProjectManagement）各自實作，未統一

### 2.2 改善方案

**統一使用 MudDrawer 或 MudDialog：**

| 方案 | 適合場景 |
|------|---------|
| **MudDrawer**（側邊抽屜） | 快速預覽任務詳情，不離開當前頁面 |
| **MudDialog**（彈窗） | 完整的任務詳情頁，適合 Pipeline View 展開 |

建議採 **MudDrawer**（右側滑出），取代所有自訂 `.slide-panel`。MudBlazor 已安裝，直接使用原生元件。

---

## 三、CSS 缺失修補

### 3.1 缺失的 CSS 變數

| 變數 | 用途 | 修正 |
|------|------|------|
| `--color-status-warning` | 橘色警告狀態 | 新增到 `:root` 和 dark mode |
| `--color-status-revision` | 修正中狀態 | 新增 |
| `--color-status-reviewing` | 審核中狀態 | 新增 |

### 3.2 缺失的 CSS Class

| Class | 使用位置 | 修正 |
|------|---------|------|
| `.slide-panel` / `.slide-panel-overlay` | TaskLogDrawer, ProjectManagement | 改用 MudDrawer 後移除，或補定義 |
| `.btn-close` | 各 Drawer | 改用 MudDrawer 後移除，或補定義 |
| `.active-toggle` / `.toggle-label` | AgentSettings, RuleManagement | 改用 MudSwitch，或補 CSS |
| `.alert-info` | AgentSettings | 改用 MudAlert，或補 CSS |
| `.cursor-pointer` | TaskCenter MudTable row | 補定義 `cursor: pointer` |
| `.text-muted` | AgentSettings | 補定義或用 MudText Typo |

### 3.3 Utility Class 策略

目前混用了自訂 utility class（`.d-flex`、`.justify-space-between`）和 MudBlazor 內建樣式。

**統一方向：** 盡量使用 MudBlazor 的 `Class` 屬性和內建 utility（`d-flex`、`justify-space-between` 等由 MudBlazor 提供），移除 `app.css` 中的重複定義。

---

## 四、Toggle 開關統一化

### 4.1 現況

AgentSettings、RuleManagement 使用自訂 HTML checkbox + `.active-toggle` CSS（未定義），外觀為原生 checkbox。

### 4.2 改善方案

全部改用 **MudSwitch**，與 Stage 17 Mock Mode 開關（如有使用 MudSwitch）保持一致：

```razor
<MudSwitch T="bool" @bind-Value="_skipCeoConfirm"
           Color="Color.Primary"
           Label="跳過 CEO 派工確認" />
```

---

## 五、inline style 清理

### 5.1 現況

多個頁面有硬編碼 `style="..."` 屬性：

| 頁面 | 問題 |
|------|------|
| AgentSettings.razor | `style="color:red"`、`style="display:flex; gap:8px"` |
| Home.razor | `style="font-size:0.8rem; padding:4px 10px;"` |
| RuleManagement.razor | `style="display:flex; align-items:center; gap:16px"` |
| ProjectManagement.razor | 多處 inline style |

### 5.2 改善方案

- 抽取為 CSS class（在 `app.css` 中定義）
- 硬編碼顏色改為 CSS 變數（`color:red` → `color:var(--color-status-failed)`）
- 布局類 inline style 改用 MudBlazor 的 `MudGrid` / `MudStack` / `Class` 屬性

---

## 六、表單卡片樣式統一

### 6.1 現況

AgentSettings、ProjectManagement、RuleManagement 的新增表單都套用 `.agent-setting-card` class，語義不正確。

### 6.2 改善方案

新增 `.form-card` 通用 class，或直接使用 MudBlazor 的 `<MudCard>` + `<MudCardContent>` 取代自訂 CSS。

---

## 七、Empty State 一致性

各頁面的空白狀態（無資料）使用 `.empty-state` class + emoji icon，但 emoji 跨瀏覽器顯示不一致。

**改善：** 改用 MudBlazor 的 `<MudIcon>` 或 Material Design icon，確保一致性。

---

## 八、驗收條件

- [ ] 所有任務狀態（pending / running / done / failed / revision / reviewing / cancelled）Badge 樣式一致
- [ ] 任務詳情使用統一的 MudDrawer 或 MudDialog（不再使用自訂 slide-panel）
- [ ] 所有缺失的 CSS class 已修復或改用 MudBlazor 元件替代
- [ ] Toggle 開關統一使用 MudSwitch
- [ ] 無硬編碼 `style="color:red"` 等 inline 顏色
- [ ] Dashboard 各頁面 Dark Mode 下顯示正常
- [ ] 用 Mock Mode 觸發完整流程，確認 Pipeline View + 狀態 Badge 配合正確
- [ ] `dotnet build` 通過

---

## 附錄：Dashboard 已知 UI 問題清單

> 此清單隨開發過程持續更新。Christ 使用 Dashboard 時發現的問題也記在這裡。

| # | 問題 | 頁面 | 優先級 |
|---|------|------|--------|
| 1 | `revision` 狀態 Badge 無樣式 | 任務中心 | 🔴 高 |
| 2 | 任務詳情顯示在表格下方，操作不直覺 | 任務中心 | 🔴 高 |
| 3 | `.slide-panel` CSS 未定義 | TaskLogDrawer | 🟡 中 |
| 4 | `.active-toggle` CSS 未定義 | Agent 設定 | 🟡 中 |
| 5 | `--color-status-warning` CSS 變數缺失 | Agent 設定 | 🟡 中 |
| 6 | inline `style="color:red"` 硬編碼顏色 | Agent 設定 | 🔵 低 |
| 7 | `.agent-setting-card` 被各頁面混用 | 多頁面 | 🔵 低 |
| 8 | Empty State 使用 emoji（跨瀏覽器不一致） | 多頁面 | 🔵 低 |

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-08 | v1.0 初版建立 |
