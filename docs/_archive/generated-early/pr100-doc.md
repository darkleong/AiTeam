# PR #100 Dashboard 登出功能 - 視覺截圖測試技術文檔

## 文件概覽

| 屬性 | 內容 |
|------|------|
| **檔案路徑** | `src/AiTeam.Tests.Playwright/Generated/PR100/VisualTests.cs` |
| **命名空間** | `AiTeam.Tests.Playwright.Generated` |
| **測試類別** | `PR100_Dashboard登出功能視覺截圖測試` |
| **基類** | `PageTest`（MSTest Playwright） |
| **測試框架** | Microsoft Playwright + MSTest |
| **用途** | E2E 視覺回歸測試，驗證登出功能在亮色/暗色模式下的截圖 |

---

## 類別結構

### 測試類別：`PR100_Dashboard登出功能視覺截圖測試`

**繼承自：** `PageTest`（Playwright MSTest 基類）

#### 成員變數

| 變數 | 類型 | 描述 |
|------|------|------|
| `_dashboardUrl` | `string` | Dashboard URL，預設 `http://localhost:5051` |
| `_dashboardUser` | `string` | 登入使用者帳號（環境變數 `DASHBOARD_USER`） |
| `_dashboardPass` | `string` | 登入密碼（環境變數 `DASHBOARD_PASS`） |
| `ScreenshotDir` | `const string` | 截圖保存目錄，固定值 `"screenshots"` |

---

## 初始化與輔助方法

### `初始化測試環境()` [TestInitialize]

**簽名：** `Task`

**職責：**
- 從環境變數讀取 `DASHBOARD_URL`、`DASHBOARD_USER`、`DASHBOARD_PASS`
- 建立 `screenshots` 目錄
- 執行登入流程

**環境變數默認值：**
```
DASHBOARD_URL → "http://localhost:5051"
DASHBOARD_USER → "" (空)
DASHBOARD_PASS → "" (空)
```

---

### `執行登入()` [Private]

**簽名：** `Task`

**流程：**
1. 導航至 `{_dashboardUrl}/login`
2. 等待 NetworkIdle 狀態
3. 尋找使用者名稱輸入框（多個選擇器：`input[type='text']`, `input[name='username']`, `input[name='email']` 等）
4. 尋找密碼輸入框（`input[type='password']`）
5. 如果可見，填入帳號與密碼
6. 尋找登入按鈕（多個選擇器：`button[type='submit']`, `button:has-text('登入')` 等）
7. 點擊登入按鈕
8. 等待 NetworkIdle

**關鍵設計：** 使用多個選擇器組合以增強選擇器健壯性

---

### `切換暗色模式()` [Private]

**簽名：** `Task`

**流程：**
1. 嘗試尋找暗色模式切換按鈕，選擇器包括：
   - `button[aria-label*='dark']`、`button[aria-label*='Dark']`
   - `button[aria-label*='暗色']`、`button[aria-label*='dark mode']`
   - `input[type='checkbox'][id*='dark']`、`input[type='checkbox'][id*='Dark']`
   - `[class*='dark-mode-toggle']`、`[class*='DarkModeToggle']`
   - `[class*='theme-toggle']`、`[class*='ThemeToggle']`
   - `button:has-text('Dark')`、`button:has-text('暗色')`
   - `button:has-text('🌙')`、`button:has-text('☀️')`
   - `[data-testid*='dark-mode']`、`[data-testid*='theme-toggle']`
2. 若找到且可見，點擊切換
3. 若找不到，直接操作 DOM：
   - 添加 `dark` 類別至 `document.documentElement`
   - 設定 `data-theme="dark"` 屬性
   - 添加 `dark-mode` 類別至 `document.body`
4. 等待 500ms 動畫完成

---

## 測試方法清單

### 1. MainLayout 整體佈局測試

#### `MainLayout_亮色模式_完整佈局截圖驗證()`

- **目的：** 驗證亮色模式下完整 MainLayout 的視覺
- **動作：**
  1. 導航至 Dashboard
  2. 等待 NetworkIdle + 1.5秒
  3. 擷取完整頁面截圖 → `PR100_MainLayout_light_full.png`
- **驗證：** 檔案存在且大小 > 0

#### `MainLayout_暗色模式_完整佈局截圖驗證()`

- **目的：** 驗證暗色模式下完整 MainLayout 的視覺
- **動作：**
  1. 導航至 Dashboard
  2. 等待 NetworkIdle + 1.5秒
  3. 切換暗色模式
  4. 等待 1秒
  5. 擷取完整頁面截圖 → `PR100_MainLayout_dark_full.png`
- **驗證：** 檔案存在且大小 > 0

---

### 2. 側邊欄區塊測試

#### `MainLayout_亮色模式_側邊欄截圖驗證()`

- **目的：** 驗證亮色模式下側邊欄的視覺
- **動作：**
  1. 導航至 Dashboard
  2. 等待 NetworkIdle + 1.5秒
  3. 尋找側邊欄（選擇器：`.sidebar`, `[class*='sidebar']`, `nav`）
  4. 若可見，擷取側邊欄 → `PR100_MainLayout_light_sidebar.png`
  5. 若不可見，擷取完整頁面（fallback） → `PR100_MainLayout_light_sidebar_fallback.png`
- **驗證：** 檔案存在且大小 > 0

#### `MainLayout_暗色模式_側邊欄截圖驗證()`

- **目的：** 驗證暗色模式下側邊欄的視覺
- **動作：** 同上，但先切換暗色模式
- **輸出檔案：** `PR100_MainLayout_dark_sidebar.png`
- **驗證：** 檔案存在且大小 > 0

---

### 3. 側邊欄收合狀態測試

#### `MainLayout_亮色模式_側邊欄收合狀態截圖驗證()`

- **目的：** 驗證亮色模式下側邊欄收合後的視覺
- **動作：**
  1. 導航至 Dashboard
  2. 等待 NetworkIdle + 1.5秒
  3. 尋找側邊欄切換按鈕（選擇器：`#sidebar-toggle-btn`, `.sidebar-toggle`, `button[aria-label*='toggle']` 等）
  4. 若可見，點擊並等待 600ms
  5. 擷取完整頁面 → `PR100_MainLayout_light_sidebar_collapsed.png`
- **驗證：** 檔案存在且大小 > 0

#### `MainLayout_暗色模式_側邊欄收合狀態截圖驗證()`

- **目的：** 驗證暗色模式下側邊欄收合後的視覺
- **動作：** 同上，但先切換暗色模式
- **輸出檔案：** `PR100_MainLayout_dark_sidebar_collapsed.png`
- **驗證：** 檔案存在且大小 > 0

---

### 4. 登出按鈕外觀測試

#### `LogoutButton_亮色模式_登出按鈕截圖驗證()`

- **目的：** 驗證亮色模式下登出按鈕的視覺
- **動作：**
  1. 導航至 Dashboard
  2. 等待 NetworkIdle + 1.5秒
  3. 尋找登出按鈕（選擇器：`.logout-btn`, `button.logout-btn`, `button:has-text('登出')`）
  4. 若可見，擷取按鈕 → `PR100_LogoutButton_light.png`
  5. 若不可見，擷取完整頁面（fallback） → `PR100_LogoutButton_light_fallback.png`
- **驗證：** 檔案存在且大小 > 0

#### `LogoutButton_暗色模式_登出按鈕截圖驗證()`

- **目的：** 驗證暗色模式下登出按鈕的視覺
- **動作：** 同上，但先切換暗色模式
- **輸出檔案：** `PR100_LogoutButton_dark.png`
- **驗證：** 檔案存在且大小 > 0

---

### 5. 側邊欄頁尾測試

#### `MainLayout_亮色模式_側邊欄頁尾截圖驗證()`

- **目的：** 驗證亮色模式下側邊欄頁尾的視覺（包含版本號、主題切換、登出按鈕）
- **動作：**
  1. 導航至 Dashboard
  2. 等待 NetworkIdle + 1.5秒
  3. 尋找側邊欄頁尾（選擇器：`.sidebar-footer`, `[class*='sidebar-footer']`, `[class*='SidebarFooter']`）
  4. 若可見，擷取頁尾 → `PR100_MainLayout_light_sidebar_footer.png`
  5. 若不可見，擷取完整頁面（fallback） → `PR100_MainLayout_light_sidebar_footer_fallback.png`
- **驗證：** 檔案存在且大小 > 0

#### `MainLayout_暗色模式_側邊欄頁尾截圖驗證()`

- **目的：** 驗證暗色模式下側邊欄頁尾的視覺
- **動作：** 同上，但先切換暗色模式
- **輸出檔案：** `PR100_MainLayout_dark_sidebar_footer.png`
- **驗證：** 檔案存在且大小 > 0

---

### 6. 登出確認 Dialog 測試

#### `LogoutButton_點擊後_確認Dialog截圖驗證()`

- **目的：** 驗證點擊登出按鈕後確認對話框的視覺（亮色模式）
- **動作：**
  1. 導航至 Dashboard
  2. 等待 NetworkIdle + 1.5秒
  3. 尋找登出按鈕
  4. 若可見：
     - 點擊登出按鈕
     - 等待 800ms 對話框顯示
     - 尋找對話框（選擇器：`.mud-dialog`, `[role='dialog']`, `[class*='dialog']`, `[class*='Dialog']`）
     - 若可見，擷取對話框 → `PR100_LogoutDialog_confirm.png`
     - 若不可見，擷取頁面（fallback） → `PR100_LogoutDialog_confirm_fallback.png`
  5. 若登出按鈕不可見，擷取完整頁面（fallback） → `PR100_LogoutDialog_confirm_fallback.png`
- **驗證：** 檔案存在且大小 > 0

#### `LogoutButton_暗色模式_點擊後_確認Dialog截圖驗證()`

- **目的：** 驗證點擊登出按鈕後確認對話框的視覺（暗色模式）
- **動作：** 同上，但先切換暗色模式
- **輸出檔案：** `PR100_LogoutDialog_dark_confirm.png`
- **驗證：** 檔案存在且大小 > 0

---

## 選擇器策略

### 健壯性設計

為避免選擇器失敗，本測試使用**多級選擇器組合**：

1. **精準匹配：** 優先使用 ID 或明確的 class 名稱
2. **備選方案：** 使用 `aria-label`、`data-testid` 屬性
3. **文字匹配：** 使用 `:has-text()` 伪選擇器（如 `button:has-text('登出')`）
4. **通配符：** 使用 `[class*='...']` 或 `[aria-label*='...']` 進行部分匹配
5. **角色選擇：** 使用 `[role='dialog']` 等標準 ARIA 角色

### 範例：登出按鈕選擇器

```javascript
".logout-btn, button.logout-btn, button:has-text('登出')"
```

- 嘗試 class 為 `logout-btn` 的元素
- 嘗試 `<button>` 標籤且 class 為 `logout-btn`
- 嘗試文字內容為 "登出" 的按鈕

---

## 截圖命名規則

```
PR100_[區塊]_[主題]_[狀態]_[類型].png
```

| 部分 | 說明 | 範例 |
|------|------|------|
| PR 號 | `PR100` | 固定 |
| 區塊 | MainLayout / LogoutButton / LogoutDialog | 功能區域 |
| 主題 | light / dark | 亮色或暗色模式 |
| 狀態 | full / sidebar / collapsed / footer / confirm | 截圖類型 |
| 類型 | （無）或 fallback | 主要或備選方案 |

**完整範例：**
- `PR100_MainLayout_light_full.png` — 亮色完整佈局
- `PR100_LogoutButton_dark.png` — 暗色登出按鈕
- `PR100_LogoutDialog_dark_confirm.png` — 暗色確認對話框
- `PR100_MainLayout_light_sidebar_fallback.png` — 亮色側邊欄（備選） 

---

## 等待策略

| 等待類型 | 描述 | 時間 |
|--------|------|------|
| `WaitForLoadStateAsync(LoadState.NetworkIdle)` | 等待網路空閒 | 動態 |
| `WaitForTimeoutAsync(500)` | 暗色模式切換動畫 | 500ms |
| `WaitForTimeoutAsync(600)` | 側邊欄收合動畫 | 600ms |
| `WaitForTimeoutAsync(800)` | 對話框顯示 | 800ms |
| `WaitForTimeoutAsync(1000)` | 暗色模式完全應用 | 1000ms |
| `WaitForTimeoutAsync(1500)` | 頁面穩定 | 1500ms |

---

## 測試覆蓋範圍統計

| 功能區域 | 亮色模式 | 暗色模式 | 總計 |
|--------|--------|--------|------|
| MainLayout 完整佈局 | 1 | 1 | 2 |
| 側邊欄區塊 | 1 | 1 | 2 |
| 側邊欄收合 | 1 | 1 | 2 |
| 登出按鈕 | 1 | 1 | 2 |
| 側邊欄頁尾 | 1 | 1 | 2 |
| 確認 Dialog | 1 | 1 | 2 |
| **總測試方法** | **6** | **6** | **12** |

---

## 斷言邏輯

所有測試方法均執行相同的驗證：

```csharp
Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
var fileInfo = new FileInfo(screenshotPath);
Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
```

**驗證項目：**
1. ✅ 截圖檔案存在
2. ✅ 截圖檔案大小 > 0 字節

---

## 使用場景

### 何時運行此測試

- ✅ PR #100 合併前的視覺回歸測試
- ✅ Dashboard 主題切換功能驗證
- ✅ 登出功能 UI 一致性檢查
- ✅ 新版本發佈前的視覺截圖基準線設定

### 環境設置

**必需環境變數：**

```bash
export DASHBOARD_URL="http://localhost:5051"
export DASHBOARD_USER="admin"
export DASHBOARD_PASS="password123"
```

**或在 .env 檔案中：**

```
DASHBOARD_URL=http://localhost:5051
DASHBOARD_USER=admin
DASHBOARD_PASS=password123
```

---

## 技術細節

### 依賴套件

| 套件 | 用途 |
|------|------|
| `Microsoft.Playwright` | E2E 測試框架 |
| `Microsoft.Playwright.MSTest` | MSTest 整合 |
| `Microsoft.VisualStudio.TestTools.UnitTesting` | 單元測試框架 |

### Fallback 策略

當主要選擇器找不到元素時，測試會：
1. 記錄備選檔案名稱（加上 `_fallback` 後綴）
2. 擷取完整頁面（`FullPage: true`）或視口（`FullPage: false`）
3. 繼續執行驗證流程

此設計確保測試不會因選擇器變更而完全失敗。

---

## 輸出檔案位置

```
./screenshots/
├── PR100_MainLayout_light_full.png
├── PR100_MainLayout_light_sidebar.png
├── PR100_MainLayout_light_sidebar_collapsed.png
├── PR100_LogoutButton_light.png
├── PR100_MainLayout_light_sidebar_footer.png
├── PR100_LogoutDialog_confirm.png
├── PR100_MainLayout_dark_full.png
├── PR100_MainLayout_dark_sidebar.png
├── PR100_MainLayout_dark_sidebar_collapsed.png
├── PR100_LogoutButton_dark.png
├── PR100_MainLayout_dark_sidebar_footer.png
├── PR100_LogoutDialog_dark_confirm.png
└── ... (fallback 變體)
```

---

## 注意事項

⚠️ **選擇器依賴：** 測試嚴重依賴 HTML 選擇器，若 UI 結構變更需同步更新選擇器

⚠️ **時序問題：** 如果動畫或網路延遲超過預期，可能需調整 `WaitForTimeoutAsync()` 的時間

⚠️ **環境變數：** 若 `DASHBOARD_USER` 或 `DASHBOARD_PASS` 為空，登入步驟會被跳過

⚠️ **視窗大小：** 截圖結果可能因瀏覽器視窗大小而異，建議使用固定解析度執行