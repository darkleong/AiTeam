# Stage 9：CEO 升級 + 可觀測性

> 版本：v1.0
> 建立日期：2026-04-03
> 狀態：🔵 規劃中

---

## 目標

三個面向的強化：
1. **CEO 升級**：從「任務路由器」升級為「有判斷力的主管」，能主動分類問題、提案而非直接派工
2. **可觀測性**：Token 用量視覺化，讓老闆掌握 API 費用與各 Agent 的使用狀況
3. **QA 視覺測試**：Playwright E2E 截圖，讓 UI 變更的 PR 可以一眼確認、加速 merge

---

## 一、CEO 智慧分類 + 提案模式

### 背景

目前 CEO 的角色偏向「收到指令就找人執行」，缺少主動判斷的能力。老闆不見得每次都能清楚分辨自己遇到的是 Bug、新功能需求，還是系統的正常行為。

> 「如果 CEO 只是收集資料然後執行，那跟助理就沒差別了。」— Christ

### 1.1 CEO 智慧分類

CEO 在回應之前，先主動查詢必要資料（GitHub API、DB），再對老闆的輸入進行分類並說明理由：

| CEO 的分類 | 回應方式 |
|-----------|---------|
| **新功能** | 「Christ，這屬於新功能開發，因為目前沒有相關實作。我找 Rosa 和 Demi 先做一份提案給你看。」|
| **Bug** | 「Christ，這是個異常，登入流程不應該發生這個錯誤。我請 Dev 來修。」|
| **正常行為** | 「Christ，這是正常的。Vera 失敗是因為這個 Project 目前沒有任何 PR，她找不到東西可以 Review。」|
| **疑問** | 直接回答，不派任何任務。|

### 1.2 提案模式（新功能專用）

當 CEO 判斷為新功能時，不直接進入執行，而是先進入提案模式：

```
你說需求
    ↓
CEO 釐清大方向（對話，一次問一個問題）
    ↓
CEO 協調 Rosa + Demi 並行產出
  Rosa → 功能需求清單、驗收條件
  Demi → UI 規格文件、MudBlazor 元件配置
    ↓
CEO 彙整成提案書，附上兩份文件
    ↓
你審核
    ↓
你核准 → CEO 動員 Agent 執行
```

### 技術重點

| 項目 | 說明 |
|------|------|
| CEO System Prompt 強化 | 新增分類判斷邏輯、說明理由的要求 |
| CEO 主動查資料 | 分類前查詢 GitHub API（PR/Issue）與 DB（任務紀錄）|
| 新增 `action: propose` | 觸發 Rosa + Demi 並行執行，CEO 彙整後回傳提案書 |
| 確認流程調整 | 提案模式的確認對象是提案書，而非個別 Agent 任務 |

---

## 二、Token 監控 Dashboard

### 背景

系統上線後沒有辦法知道每個 Agent 消耗了多少 Token、目前 API 費用大概是多少。需要一個視覺化頁面來觀察用量，才能在有必要時做費用優化決策。

### 目標頁面規格

```
Token 監控頁
  ├── 頂部：時間範圍選擇器（今日 / 本週 / 本月）
  ├── 上方：各 Agent 用量卡片（input / output tokens，預估費用）
  ├── 中間：折線圖（X 軸 = 時間，Y 軸 = Token 用量，每個 Agent 一條線）
  └── 底部：詳細數據表格（可排序）
```

### 技術重點

| 項目 | 說明 |
|------|------|
| 資料記錄 | LLM 呼叫完成後，將 `input_tokens` / `output_tokens` 寫入 `task_logs.payload` |
| 費用估算 | 依模型計費率計算（Claude Sonnet 定價，可設定於 `app_settings`）|
| 資料來源 | 從 `task_logs` 彙總，依時間區間 + Agent 分組 |
| Dashboard 頁面 | 新增「Token 監控」頁，MudChart 折線圖 + MudTable |
| 更新頻率 | 每次任務完成後 SignalR 推送更新 |

---

## 三、QA Agent 視覺截圖測試（Playwright）

### 背景

目前 QA Agent 產出 xUnit 單元測試（純程式碼分析，不開瀏覽器）。Playwright E2E 截圖讓 UI 修正的 PR 可以一眼確認，加速 merge 判斷。

### 目標場景

```
PR 開啟（包含 UI 變更）
    ↓
QA Agent 偵測到 UI 相關檔案變更
    ↓
產出 Playwright 測試（切換 Dark Mode、導航到頁面、截圖）
    ↓
CI/CD pipeline 起臨時容器跑測試
    ↓
截圖附加到 GitHub PR comment
    ↓
老闆看截圖確認 OK → 直接 merge
```

### 技術重點

| 項目 | 說明 |
|------|------|
| 登入驗證 | 方式一：Playwright 直接模擬登入，帳密從 `.env` 讀取 |
| 執行環境 | CI/CD pipeline 起臨時容器，Playwright 打 localhost，測完即銷毀 |
| 觸發條件 | PR 包含 `.razor` / `.css` 檔案變更時才跑 Playwright（純後端變更不跑）|
| QA Agent 邏輯 | 依任務性質選擇 xUnit（邏輯測試）或 Playwright（UI 視覺測試）|
| 所需工作 | Playwright 專案加入 solution、CI/CD 新增 test stage、QA Agent 邏輯擴充 |

---

## 實作順序建議

| 順序 | 項目 | 原因 |
|------|------|------|
| 1 | Token 監控 Dashboard | 純新增功能，不改動現有邏輯，最快上線 |
| 2 | CEO 智慧分類 | Prompt 強化 + 新增 propose 流程，核心能力升級 |
| 3 | CEO 提案模式 | 依賴智慧分類完成後再加入 |
| 4 | QA Playwright 截圖 | 需要改動 CI/CD 與 QA Agent，複雜度最高 |

---

## 驗收標準

| 項目 | 標準 |
|------|------|
| CEO 智慧分類 | Bug / 新功能 / 正常行為 / 疑問，四種情境各測試一次，分類正確且說明理由 |
| CEO 提案模式 | 新功能需求成功觸發 Rosa + Demi 協作，CEO 彙整提案書後老闆確認執行 |
| Token 監控 | Dashboard 正確顯示各 Agent Token 用量與估算費用，折線圖正常 |
| QA Playwright | UI 相關 PR 自動跑截圖測試，截圖附加到 PR comment |

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-03 | 初版建立 |
