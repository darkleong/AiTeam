# Stage 8：系統可靠性與操作體驗

> 版本：v1.0
> 建立日期：2026-04-02
> 狀態：🔵 規劃中

---

## 目標

Stage 7 CI/CD 上線後暴露的可靠性問題 + 操作體驗補完 + 架構簡化。
讓系統跑起來更穩、操作更順、Dashboard 更完整，並移除不必要的外部依賴。

---

## 一、Bot 重啟時自動清理殘留的「執行中」任務

### 背景

Bot 容器重啟（CI/CD Deploy、手動重啟）時，正在執行的任務會被強制中斷。
資料庫中該任務的狀態永遠停在「執行中」，造成任務中心顯示異常（如截圖中的「執行中」Doc 任務）。

### 解法

Bot 啟動時（`IHostedService.StartAsync`），掃描所有狀態為「執行中」的任務，
標記為「失敗」並附上備註：`Bot 重啟，任務中斷`。

---

## 二、Ops Agent 監控 CI/CD 並自動重試

### 背景

GitHub Actions Deploy job 偶爾因外部因素失敗（Docker Hub 502、網路逾時），
目前需要人工收到 Email 後判斷原因並手動重跑（`gh run rerun`）。

### 目標

| 失敗原因 | Ops 的行為 |
|---------|-----------|
| 外部故障（Docker Hub 502、網路逾時）| 自動重試 Deploy |
| 程式問題（Build 失敗、Test 失敗）| 通知老闆並說明原因，不自動重試 |

### 分工

| 職責 | Agent |
|------|-------|
| 監控 Deploy 結果、判斷原因、自動重試 | **Ops Agent（Maya）** |
| 版本號決定、Changelog、建立 Release tag | **Release Agent（Rena）** |

---

## 三、從 Dashboard 重啟 Bot

### 背景

新增 Agent 後必須重啟 Bot 才能生效。目前需要去 terminal 手動操作，有了 Docker Compose 之後應該可以在 Dashboard 直接完成。

### 實作方式

```
Dashboard 按下「重啟 Bot」
    ↓
呼叫 Bot 暴露的 /internal/restart endpoint
    ↓
Bot 呼叫 IHostApplicationLifetime.StopApplication()
    ↓
Docker Compose restart: always 自動重啟容器
```

### 安全考量

`/internal/restart` 限制只接受來自 Dashboard 的內部請求（同網段或 API Key）。

---

## 四、Dashboard UI 微調

| # | 問題描述 | 頁面 / 元件 |
|---|---------|------------|
| 1 | 側欄收合後 emoji 圖示顯示為色塊，字型未載入 | NavMenu（sidebar 收合狀態） |
| 2 | 整體 MudBlazor 色彩主題尚未依品牌色統一設定 | 全域 |
| 3 | 首頁空白時沒有引導提示（Empty State） | 首頁 |

---

## 五、CEO 穩定後移除派工確認

### 背景

目前雙層確認：
1. **CEO 派工確認**：CEO 解讀意圖後，老闆確認「理解是否正確、要派給哪個 Agent」
2. **Agent 執行確認**：Agent 收到後，老闆確認「任務內容與步驟是否正確」

自然語言上線後，若 CEO 判斷準確率夠高，第一層確認可以移除，直接進入執行確認，減少操作步驟。

### 條件

- CEO 連續正確解讀意圖達一定次數後才啟用
- 設計為可開關設定（`AgentSettings__SkipCeoConfirm`），隨時可以關回去

---

## 六、專案管理新增功能

### 背景

Dashboard 專案管理頁目前只有顯示，無法新增專案。所有任務都在「無專案」狀態下執行，任務中心的「專案」欄全部空白。

### 解法

Dashboard 專案管理頁新增表單（類似 Agent 設定頁的新增 UI），支援：
- 新增專案（名稱、Repo URL、技術棧）
- 啟用 / 停用專案

---

## 七、部署紀錄補全

### 背景

Stage 7 改用 GitHub Actions CI/CD 後，部署流程繞過 Ops Agent，沒有任何記錄寫進 DB，部署紀錄頁面空白。

### 解法

GitHub Actions 完成 Deploy job 後，透過 webhook 或呼叫 Bot 的內部 API，寫一筆部署記錄進 DB。

---

## 八、Notion 遷移至 PostgreSQL

### 背景

Notion 目前只有 Rules 在真正發揮作用（Task Summary 空白、Agent Status 已在 AgentConfig）。將 Rules 遷移至 PostgreSQL 後，可完全移除 Notion 外部依賴，所有管理集中在 Dashboard。

### 遷移範圍

| Notion 的東西 | 處理方式 |
|--------------|---------|
| **Rules** | 新增 `rules` 資料表 + Dashboard 規則管理頁（CRUD）|
| **Task Summary** | 廢棄，任務中心已取代 |
| **Agent Status（信任等級）** | 廢棄，`AgentConfig.TrustLevel` 已存在 |

### 一併移除

- `Notion.Net` NuGet 套件
- `NotionService` / `INotionService`
- `NotionCacheTtlMinutes` 設定
- `NOTION_API_KEY`
- `/reload-rules` Discord 指令（改為重新載入 DB 規則快取）

### Notion Task Summary 補充

Notion Task Summary 原本設計為 Agent 寫入任務結果摘要供未來參考，但此功能從未實作。遷移後不再補實作，以 Dashboard 任務中心取代。

---

## 實作順序建議

| 順序 | 項目 | 原因 |
|------|------|------|
| 1 | Bot 重啟清理殘留任務 | 最小改動，立即解決任務中心顯示問題 |
| 2 | Dashboard UI 微調 | 順手，改善日常使用體驗 |
| 3 | 專案管理新增功能 | 讓後續任務可以正確關聯專案 |
| 4 | Notion 遷移至 PostgreSQL | 架構簡化，移除外部依賴 |
| 5 | 部署紀錄補全 | 依賴 Notion 遷移後確認 DB 結構穩定 |
| 6 | 從 Dashboard 重啟 Bot | 需要 Bot 新增 InternalController |
| 7 | Ops 監控 CI/CD 並自動重試 | 需要 GitHub Webhook 或輪詢整合 |
| 8 | CEO 移除派工確認 | 最後做，需要先觀察自然語言穩定度 |

---

## 驗收標準

| 項目 | 標準 |
|------|------|
| Bot 重啟清理 | Deploy 後任務中心不再出現殘留「執行中」任務 |
| Dashboard UI | emoji 正常顯示，首頁有 Empty State |
| 專案管理 | Dashboard 可新增專案，任務關聯專案正常顯示 |
| Notion 遷移 | Notion 完全移除，Rules 改由 Dashboard 管理 |
| 部署紀錄 | CI/CD 完成後自動寫入部署記錄 |
| Dashboard 重啟 Bot | 一鍵重啟，60 秒內 Bot 恢復上線 |
| Ops CI/CD 監控 | 外部失敗自動重試，程式失敗通知老闆 |
| CEO 確認移除 | 自然語言任務直接進入 Agent 執行確認，少一個步驟 |

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-02 | 初版建立 |
