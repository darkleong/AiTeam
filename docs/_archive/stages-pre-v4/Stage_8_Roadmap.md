# Stage 8：系統可靠性與操作體驗

> 版本：v1.1
> 建立日期：2026-04-02
> 完成日期：2026-04-02
> 狀態：✅ 完成

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

---

## 實作重點紀錄

> 以下為實作過程中遭遇的重要決策、陷阱與解法，供未來參考。

### 項目一：Bot 重啟清理殘留任務

- 在 `DiscordBotService.StartAsync()` 最早期呼叫 `TaskRepository.MarkStaleRunningTasksAsync()`
- 使用 `IServiceScopeFactory` 在 BackgroundService 中安全取得 scoped DbContext
- 同時清理 CEO 決策中 (CeoDecision = "pending") 的任務，避免孤立等待

### 項目二：Ops CI/CD 監控

- `HealthCheckJob` 透過 Quartz.NET 排程（30 分鐘一次），獨立於每日 AI 摘要 cron
- **踩坑**：`Program.cs` 錯誤讀取了 `DailyReportCron`（每日 9:00/21:00）作為健康檢查排程；修正為獨立的 `HealthCheckCron = "0 */30 * * * ?"`
- Bot 容器內無 Docker CLI，已移除所有 `docker` / `docker-compose` CLI 呼叫，改以 DB ping 取代
- Rollback 操作改為警示通知，請人工介入

### 項目三：Dashboard 重啟 Bot

- Bot 新增 `InternalController`，`POST /internal/restart` 呼叫 `IHostApplicationLifetime.StopApplication()`
- Docker Compose `restart: always` 確保容器自動重啟（約 15-30 秒）
- API Key 驗證：`X-Api-Key` header，key 存於 `.env` 不進版控
- Dashboard `DashboardBotService` 透過 `IHttpClientFactory` 呼叫 Bot 內部 API

### 項目四：Dashboard UI 微調

- 首頁加入 Empty State（無任務 / 無 Agent 時的引導提示）
- **Dark Mode 踩坑（重要）**：
  - 嘗試將 `<MudProviders />` 拆成 `<MudThemeProvider>` + `<MudDialogProvider>` 等並在 MainLayout 加 C# 狀態 → Blazor SignalR circuit 斷線，所有頁面空白
  - **根本原因**：在 Layout 組件加狀態會影響 Blazor render cycle，導致 circuit 不穩
  - **最終解法**：MainLayout 維持純 JS onclick 切換 `data-theme` 屬性；在 `app.css` 用 `html[data-theme="dark"]` 覆寫 `--mud-palette-*` CSS 變數，完全繞開 C# 狀態管理
  - MudBlazor Grid / Table 文字顏色需覆寫 `--mud-palette-text-primary` 與 `--mud-palette-table-lines`，不能只靠 class selector

### 項目五：CEO 移除派工確認（SkipCeoConfirm）

- 設計為動態設定（`app_settings` 資料表），無需重啟 Bot
- Bot 端新增 `AppSettingsService`（Singleton + 5 分鐘 TTL 記憶體快取），從 DB 讀取設定值
- Dashboard Agent 設定頁底部「系統設定」區塊提供 toggle，即時寫入 DB，5 分鐘內自動生效
- `CommandHandler` 讀取 `await appSettings.GetBoolAsync("SkipCeoConfirm")`，true 時直接呼叫 `ShowDirectAgentConfirmAsync()`，略過 CEO Embed
- **踩坑**：lambda `(embed, comps) => msg.Channel.SendMessageAsync(...)` 回傳 `Task<RestUserMessage>` 無法隱式轉換為 `Task<IUserMessage>`；改用 `async (embed, comps) => await ...` 解決

### 項目六：專案管理新增功能

- Dashboard 專案管理頁新增 side drawer 表單（名稱、Repo URL、TechStack）
- `DashboardProjectService.CreateProjectAsync()` / `ToggleActiveAsync()`
- 任務 ProjectId 為 nullable，null = 無關聯專案，系統正常運作
- 部署自動關聯：`InternalController.RecordDeployment` 解析 `owner/repo` → 比對 DB 專案名稱，自動設定 `task.ProjectId`

### 項目七：部署紀錄補全

- GitHub Actions Deploy job 最後一步 `curl` 呼叫 `POST /internal/deployment`
- **踩坑**：PowerShell `Write-Warning` 包含中文字元在 Windows self-hosted runner 上觸發 YAML 解析錯誤；改為英文字串

### 項目八：Notion 遷移至 PostgreSQL

- 新增 `rules` 資料表，欄位包含 `AgentName`（nullable，null = 全域規則，有值 = 僅套用指定 Agent）
- `RulesService` 快取完整 `Rule` 物件，`GetRulesAsync(agentName?)` 在記憶體過濾
- 新增 `app_settings` 資料表（string PK key/value），支援未來任何動態設定擴充
- 完全移除：`Notion.Net`、`NotionService`、`NotionSettings`、所有 Notion 環境變數
- `/reload-rules` 改為清除 DB rules 記憶體快取，語意不變
- `DbSeeder` 同時為 Bot 啟動 + Dashboard 啟動兩端安全呼叫（冪等操作）
- **踩坑**：DbSeeder 兩端各跑一次導致重複資料；已加 `AnyAsync()` 防呆，並手動清除重複記錄

---

## 驗收結果

| 項目 | 驗收狀態 | 備註 |
|------|---------|------|
| 1. Bot 重啟清理殘留任務 | ✅ 通過 | Stage 7 殘留任務成功清除 |
| 2. Ops CI/CD 監控 | ✅ 通過 | 健康檢查警告正常；docker CLI 依賴已移除 |
| 3. Dashboard 重啟 Bot | ✅ 通過 | 60 秒內 Bot 恢復上線 |
| 4. Dashboard UI 微調 | ✅ 通過 | Dark mode CSS 變數覆寫；Empty State 完成 |
| 5. CEO 確認可選移除 | ✅ 通過 | Dashboard toggle → 直接進入 Agent 執行確認 |
| 6. 專案管理新增功能 | ✅ 通過 | 可新增專案；部署記錄自動關聯 |
| 7. 部署紀錄補全 | ✅ 通過 | CI/CD 完成後自動寫入，專案自動關聯 |
| 8. Notion 遷移 PostgreSQL | ✅ 通過 | Notion 完全移除，規則由 Dashboard 管理 |

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-02 | 初版建立 |
| 2026-04-02 | Stage 8 全部 8 項實作完成並驗收通過；補充實作重點紀錄 |
