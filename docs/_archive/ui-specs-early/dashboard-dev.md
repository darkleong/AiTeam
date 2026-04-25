# Dashboard 登出功能 UI 規格文件

## 確認任務範圍

在開始撰寫規格前，我需要確認以下幾點（如有不確定之處，請補充）：

1. **登出後導向頁面**：登出後應導向登入頁面（Login）或首頁？
2. **Session 清理職責**：登出 API 由後端負責清理 Session/Token，還是前端也需配合？
3. **成功通知**：登出成功後是否需要顯示 Toast/Snackbar 通知？
4. **登出按鈕位置**：是否應在頂部導覽列的使用者頭像下拉選單中？
5. **已有登入機制**：專案目前使用什麼認證方案（JWT Token、Session Cookie、Blazor 內建 AuthenticationStateProvider）？

**假設本規格將基於以下預設場景執行**（如有差異請告知）：
- 登出後導向登入頁面（`/login`）
- 後端有登出 API 端點（`POST /api/auth/logout`）負責清理 Session
- 前端透過 MudBlazor 的 `MudButton` 與 `MudDialog` 實現確認機制
- 使用者菜單位於頁面頂部導覽列
- 現有專案使用 Blazor 的 `AuthenticationStateProvider`

---

## 1. 頁面目的

在 Dashboard 頁面頂部導覽列提供使用者菜單，使已登入使用者可安全登出系統，並透過確認對話框防止誤操作。

---

## 2. 頁面結構

### 2.1 整體佈局

```
┌─────────────────────────────────────────────────────────┐
│  Dashboard                          [User Menu] ▼        │  ← 導覽列
├─────────────────────────────────────────────────────────┤
│                                                           │
│  Dashboard 主要內容區域（不受登出功能影響）              │
│                                                           │
└─────────────────────────────────────────────────────────┘
```

### 2.2 使用者菜單結構

```
[User Menu] ▼
    ├─ 👤 [使用者名稱]
    ├─ ─────────────
    ├─ 帳戶設定
    ├─ 個人偏好設定
    ├─ ─────────────
    └─ 登出
```

**位置**：
- 頂部導覽列右側
- 使用 MudMenu 元件包裝
- 預設顯示使用者頭像 + 下拉箭頭

---

## 3. 元件規格

### 3.1 使用者菜單容器

**元件**：`MudMenu`

| 屬性 | 值 | 說明 |
|------|-----|------|
| `AnchorOrigin` | `Origin.BottomRight` | 菜單相對於觸發按鈕的位置（向下展開，靠右對齊） |
| `TransformOrigin` | `Origin.TopRight` | 菜單動畫起點 |
| `Class` | `user-menu` | 自訂 CSS 類別，方便樣式調整 |
| `ActivationButtonContent` | MudButton（見 3.2） | 菜單觸發按鈕內容 |

---

### 3.2 菜單觸發按鈕

**元件**：`MudButton`（作為 MudMenu 的 ActivationButtonContent）

| 屬性 | 值 | 說明 |
|------|-----|------|
| `Variant` | `Variant.Text` | 無背景樣式，融入導覽列 |
| `Color` | `Color.Inherit` | 繼承導覽列顏色 |
| `Size` | `Size.Small` | 緊湊尺寸 |
| `Class` | `d-flex align-center gap-2` | Flexbox 佈局：使用者頭像 + 名稱 + 箭頭 |
| `Content` | 使用者頭像 + 名稱 | 見 3.3 |

---

### 3.3 菜單內容

**菜單項目 1：使用者資訊顯示**

| 屬性 | 值 | 說明 |
|------|-----|------|
| 元件 | `MudText` | 顯示登入的使用者名稱 |
| `Typo` | `Typo.body2` | 字型大小 |
| `Class` | `px-4 py-2` | 間距 |

---

**菜單項目 2：分隔線**

| 屬性 | 值 | 說明 |
|------|-----|------|
| 元件 | `MudDivider` | 視覺分隔 |

---

**菜單項目 3：帳戶設定（可選按鈕）**

| 屬性 | 值 | 說明 |
|------|-----|------|
| 元件 | `MudMenuItem` | 菜單項目 |
| `Text` | `帳戶設定` | 按鈕文本 |
| `OnClick` | 事件處理 | 導向帳戶設定頁面（`/account-settings`） |
| `Icon` | `Icons.Material.Filled.Settings` | 圖示 |

---

**菜單項目 4：個人偏好設定（可選按鈕）**

| 屬性 | 值 | 說明 |
|------|-----|------|
| 元件 | `MudMenuItem` | 菜單項目 |
| `Text` | `個人偏好設定` | 按鈕文本 |
| `OnClick` | 事件處理 | 導向偏好設定頁面（`/preferences`） |
| `Icon` | `Icons.Material.Filled.Tune` | 圖示 |

---

**菜單項目 5：分隔線**

| 屬性 | 值 | 說明 |
|------|-----|------|
| 元件 | `MudDivider` | 視覺分隔 |

---

**菜單項目 6：登出按鈕**

| 屬性 | 值 | 說明 |
|------|-----|------|
| 元件 | `MudMenuItem` | 菜單項目 |
| `Text` | `登出` | 按鈕文本 |
| `OnClick` | `ShowLogoutConfirmationAsync()` | 觸發確認對話框 |
| `Icon` | `Icons.Material.Filled.Logout` | 圖示 |
| `Color` | `Color.Error`（可選） | 視覺提示危險操作 |

---

### 3.4 登出確認對話框

**元件**：`MudDialog`

| 屬性 | 值 | 說明 |
|------|-----|------|
| `MaxWidth` | `MaxWidth.ExtraSmall` | 對話框寬度（約 400px） |
| `Class` | `logout-dialog` | 自訂 CSS 類別 |

#### 對話框標題

**元件**：`DialogTitle`

| 屬性 | 值 | 說明 |
|------|-----|------|
| Content | `確認登出` | 明確的操作意圖 |

---

#### 對話框內容

**元件**：`DialogContent`

| 屬性 | 值 | 說明 |
|------|-----|------|
| Content | `您確定要登出系統嗎？` | 二次確認訊息，語氣友善 |
| 子元件 | `MudText` | 提示文本 |

---

#### 對話框操作按鈕

**操作按鈕 1：取消**

| 屬性 | 值 | 說明 |
|------|-----|------|
| 元件 | `MudButton`（在 DialogActions） | 取消操作 |
| `Text` | `取消` | 按鈕文本 |
| `OnClick` | `CloseLogoutDialog()` | 關閉對話框，不執行登出 |
| `Variant` | `Variant.Text` | 次要操作樣式 |

---

**操作按鈕 2：確認登出**

| 屬性 | 值 | 說明 |
|------|-----|------|
| 元件 | `MudButton`（在 DialogActions） | 確認登出 |
| `Text` | `確認登出` | 按鈕文本 |
| `OnClick` | `ConfirmLogoutAsync()` | 執行登出邏輯 |
| `Variant` | `Variant.Filled` | 主要操作樣式 |
| `Color` | `Color.Error` | 警示顏色 |
| `Disabled` | 登出中時為 `true` | 防止重複點擊 |

---

### 3.5 登出狀態指示器

**元件**：`MudProgressLinear`（可選）

| 屬性 | 值 | 說明 |
|------|-----|------|
| `Visible` | `isLoggingOut` | 登出 API 呼叫中時顯示進度條 |
| `Indeterminate` | `true` | 不確定進度（無明確進度百分比） |
| `Class` | `my-2` | 間距 |

---

## 4. 資料來源

### 4.1 使用者名稱

**來源**：Blazor 的 `AuthenticationStateProvider`

**流程**：
1. 元件生命週期 (`OnInitializedAsync`) 取得 `AuthenticationState`
2. 從 `ClaimsPrincipal` 提取使用者名稱（Claim 型別：`ClaimTypes.Name`）
3. 儲存為元件狀態變數 `CurrentUserName`

**備註**：
- 若無法取得使用者名稱，應顯示預設值「訪客」或「使用者」
- 若未認證（非法登入），應導向登入頁面

---

### 4.2 登出 API 端點

**端點**：`POST /api/auth/logout`

**請求頭**：
- `Authorization: Bearer {token}`（如使用 JWT）或自動帶上 Session Cookie

**請求體**：無（或可選包含額外清理資訊）

**預期回應**：
```json
{
  "success": true,
  "message": "登出成功"
}
```

**HTTP 狀態碼**：
- `200 OK`：登出成功，Session/Token 已清理
- `401 Unauthorized`：Token 無效或已過期
- `500 Internal Server Error`：伺服器錯誤

---

## 5. 互動行為

### 5.1 使用者開啟菜單

**觸發條件**：使用者點擊頂部導覽列的使用者菜單按鈕

**系統回應**：
1. MudMenu 以向下展開動畫顯示菜單項目
2. 菜單項目依序顯示（使用者名稱 → 分隔線 → 設定選項 → 分隔線 → 登出）

---

### 5.2 使用者點擊登出

**觸發條件**：使用者點擊菜單中的「登出」項目

**系統回應**：
1. MudMenu 自動關閉
2. 登出確認對話框 (`MudDialog`) 以模態方式顯示
3. 對話框獲得焦點，使用者菜單背景變暗（灰化）

---

### 5.3 使用者點擊「取消」

**觸發條件**：對話框顯示時，使用者點擊「取消」按鈕

**系統回應**：
1. 對話框關閉
2. 使用者停留在原頁面（Dashboard）
3. 未進行任何後端操作

---

### 5.4 使用者點擊「確認登出」

**觸發條件**：對話框顯示時，使用者點擊「確認登出」按鈕

**系統回應**：

| 步驟 | 動作 | 說明 |
|------|------|------|
| 1 | 按鈕進入 Loading 狀態 | `Disabled = true`，顯示旋轉加載圖示 |
| 2 | 呼叫登出 API | 發送 `POST /api/auth/logout`，攜帶認證令牌 |
| 3 | 等待回應 | SignalR 或標準 HTTP 回應 |
| 4a （成功） | 清理前端狀態 | 清除本地儲存的 Token/Session（若有） |
| 4b （成功） | 導向登入頁面 | `NavigationManager.NavigateTo("/login", forceLoad: true)` |
| 4c （成功） | 顯示成功通知 | 可選：Toast/Snackbar 「已安全登出」（2-3 秒後消失） |
| 5a （失敗） | 顯示錯誤訊息 | 對話框顯示錯誤提示：「登出失敗，請稍後重試」 |
| 5b （失敗） | 按鈕恢復狀態 | 按鈕重新啟用，使用者可重試 |

---

### 5.5 登出後重新登入

**前置條件**：使用者已成功登出並導向登入頁面

**行為**：
- 使用者可在登入頁面重新輸入認證資訊
- 登入成功後返回 Dashboard（或原始請求頁面）
- 新的認證狀態覆蓋舊的 Session/Token

---

## 6. 注意事項

### 6.1 Blazor Server 特殊考量

#### 電路隔離（Circuit Isolation）
- 登出時應清理使用者相關的電路狀態，防止其他標籤頁面仍保持認證
- 建議使用 `forceLoad: true` 在導向登入頁面時強制刷新頁面
