## v1.3.0 — 2026-04-03

### 新功能
- **Stage 10 — CEO Orchestrator**：任務完成後 CEO 按流程表（in-code WorkflowEngine）自動觸發下一步，不走 LLM，毫秒級路由
  - 新功能流程：提案核准 → Dev → QA / Doc / Vera（並行）→ 通知老闆 merge
  - Bug 修復流程：Dev → Vera → 通知老闆 merge
  - Review 閉環：Vera 有 🔴 → 通知 Dev → Dev 推 commit → PR webhook 自動重派 Vera → 無 🔴 通知老闆
  - 修復迭代防護：`TaskGroup.FixIteration >= 3` 升級給老闆介入
- **Stage 10 — 提案書增強**：
  - ✏️「需要調整」第三個按鈕，老闆說明修改方向後 CEO 重新提案
  - Embed 附上 🎨 UI 規格文件 GitHub 連結（完整版可點擊閱讀）
  - 提案模式一律提交 UI 規格到 `docs/ui-specs/`，不再依關鍵字判斷
- **Stage 10 — 開發上下文補強**：Dev 制定計畫前呼叫 GitHub Tree API 取得 2 層目錄結構（免 Clone），計畫中 `files_to_modify` 準確率提升
- **Stage 10 — Ops Rollback**：部署失敗時 Maya 自動觸發 `rollback.yml` GitHub Actions workflow，回滾到上一個穩定 tag，不再只通知老闆手動處理

### 架構變更
- 新增 `TaskGroup` entity + `task_groups` 資料表（EF Migration `AddTaskGroupAndWaitingInput`）
- `TaskItem.Status` 新增 `waiting_input` 狀態
- `TaskItem.GroupId` FK 關聯 `TaskGroup`
- `AgentExecutionResult` 新增 `IsWaitingInput`、`QuestionType`、`Question`、`CriticalReviewCount`（向後相容）
- 新建 `WorkflowEngine.cs`（純靜態流程表，無 LLM，無 DB）
- 新建 `TaskGroupService.cs`（群組管理 + 並行觸發 + 遞迴 Orchestration）
- `GitHubService` 新增 `GetRepoTreeSummaryAsync`（Git Trees API，2 層，最多 200 筆）
- `GitHubService` 新增 `TriggerWorkflowDispatchAsync`（觸發 `workflow_dispatch`）
- `OpsAgentService` 注入 `GitHubService`，Rollback 改用 GitHub API 而非本機 docker-compose
- 新增 `.github/workflows/rollback.yml`（workflow_dispatch + target_tag input）
- `WebhookController` 新增 `pull_request.synchronize` handler（Review 閉環自動重審）

---

## v1.2.0 — 2026-04-03

### 新功能
- **Stage 9 — CEO 智慧分類**：Victoria 每次回應前自動查 GitHub PR / Issues，判斷輸入為新功能 / Bug / 正常行為 / 疑問並說明理由
- **Stage 9 — CEO 提案模式**：新功能時 CEO 並行呼叫 Rosa + Demi，彙整提案書 Embed（✅❌ 按鈕），核准後才執行
- **Stage 9 — Token 監控 Dashboard**：`/tokens` 頁面顯示各 Agent Token 用量卡片、MudChart 折線圖、費用估算，每次 LLM 呼叫後 SignalR 即時推送更新
- **Stage 9 — QA Playwright CI**：QA Agent 偵測 PR 含 `.razor` / `.css` 變更時自動產出 Playwright 截圖測試，CI/CD pipeline 起臨時容器執行

### 架構變更
- 新增 `token_logs` 資料表（EF Migration `AddTokenLogs`）
- `TokenTrackingProvider` Decorator 包裝所有 LLM 呼叫，AgentService 零改動
- `LlmProviderFactory` 從 Singleton 改 Scoped，支援 Token Repository 注入
- `app_settings` 新增 `TokenPricing:InputPer1kUsd` / `OutputPer1kUsd` 費率設定
- `DesignerAgentService` 新增 `GenerateDraftAsync`（提案模式草稿，不開 PR）
- 新增 `AiTeam.Tests.Playwright` 專案（MSTest + Microsoft.Playwright.MSTest 1.52.0）
- 新增 `.github/workflows/playwright.yml`（觸發條件：PR 含 `.razor` / `.css` 變更）

### Bug 修復
- 修正 Discord CDN 有時將 PNG 回報為 `image/webp`，改從 magic bytes 偵測真實格式

---

## v1.1.0 — 2026-04-02

### 新功能
- **Stage 8 — Bot 重啟清理**：啟動時自動將殘留「執行中」任務標記為失敗，附備註「Bot 重啟，任務中斷」
- **Stage 8 — Ops CI/CD 監控**：Quartz 排程定期查 GitHub Actions，外部故障（docker push/pull）自動重試，程式問題通知老闆
- **Stage 8 — Dashboard 重啟 Bot**：Bot 暴露 `/internal/restart` endpoint，Dashboard 一鍵觸發 Bot 重啟
- **Stage 8 — SkipCeoConfirm 動態設定**：可於 Dashboard 系統設定開啟，5 分鐘內生效，不需重啟 Bot
- **Stage 8 — 專案管理頁面**：Dashboard 新增專案 CRUD（GitHub Repo 連結、優先級、狀態管理）
- **Stage 8 — 部署紀錄自動化**：CI/CD Deploy 成功後呼叫 `/internal/deployment` 寫入 DB，Dashboard 可查歷史部署紀錄
- **Stage 8 — Notion 完全移除**：規則改存 PostgreSQL `rules` 資料表，`app_settings` 資料表取代 Notion AppSettings，移除 Notion.Net 相依

### Bug 修復
- 修正 OpsAgent 移除 docker CLI 依賴，健康檢查改用 DB ping + 記憶體監控
- 修正 per-agent Rules 機制：每個 Agent 有獨立規則集，`/reload-rules` 清除所有快取
- 修正 Dark Mode CSS 覆寫：全域 `.mud-dark` 前綴確保 MudBlazor 顏色主題正確套用

---

## v1.0.0 — 2026-04-02

### 新功能
- **Stage 2**：Discord Bot 基礎建設、CEO Agent、Notion 整合、EF Core 資料層建立
- **Stage 3**：Dev Agent、Ops Agent、GitHub Webhook 串接，`exec_yes` 按鈕觸發 Agent 執行流程
- **Stage 4**：Blazor Dashboard 實作，Bot 串接 SignalR 即時推送任務狀態
- **Stage 5**：動態 Agent 框架完成，新增 QA Agent、Doc Agent、Requirements Agent
- **Stage 6.1**：Discord 圖片輸入支援（Vision 功能），Requirements Agent 第三層確認機制
- **Stage 6.3**：前端 UI 框架由 Telerik UI 全面替換為 MudBlazor 8.15.0
- **Stage 7**：Software Team 完全體，完整多 Agent 協作流程上線
- **Agent 設定頁面**新增「新增 Agent」功能，支援動態擴充 Agent 配置
- Bot 啟動時自動建立缺少的 Discord 頻道
- 任務建立後立即透過 SignalR 推送，任務中心即時顯示 pending 狀態
- Designer Agent 完成後將 UI 規格書以 Markdown 附件形式傳送至 Discord

### Bug 修復
- 修正 Dashboard SignalR 在 Docker 容器內連線失敗及 TrustLevel 顯示錯誤
- 修正 Dashboard 即時推送 URL 在 Docker 環境連到 port 80 而非 8080
- 修正 `BuildCeoDecisionEmbed` 空值欄位導致 Discord Embed 拋出 `ArgumentException`
- 修正 Discord Embed field 超過 1024 字元限制導致錯誤
- 修正 CEO Agent `action=reply` 誤判導致雙層確認流程被跳過
- 修正 `confirm_yes` 建立任務失敗（TeamId FK 違反約束）及無法顯示第二層確認的問題
- 修正 `RequireConfirmation` 判斷邏輯，確保 delegate 永遠顯示確認 Embed
- 修正 `ExecuteAgentTaskAsync` 補齊 running / done / failed 的 SignalR push
- 修正 Requirements Agent 補齊 running / done / failed SignalR push
- 修正 QA Agent 從 PR head branch 讀取原始碼，解決 404 問題
- 修正 QA Agent 排除 PR 中的測試檔案，避免 `GetFileContentAsync` 404
- 修正 Doc Agent `ListFilesAsync` 支援遞迴並修正尾巴斜線 404
- 修正 `DbSeeder` 補入 CEO Agent seed，避免新環境缺少 CEO 設定
- 修正 Dockerfile `libgssapi` 警告、postgres 版本升至 17、docker-compose 納入版本控制
- 修正 `deploy.yml` 加入 `--env-file` 讓 docker compose 正確載入 `.env`
- 修正 Bot Dockerfile 移除 `libgit2` 系統安裝（LibGit2Sharp 0.31+ 已內建 native binary）
- 修正 `MudPopoverProvider` 電路隔離問題，修復側欄收合展開功能
- 修正 `MutationObserver` 防止 Blazor DOM patch 重設側欄收合狀態
- 修正側欄收合時展開按鈕被裁切的問題
- 修正 Agent 設定頁面啟用/停用提示訊息顯示位置錯誤
- 修正 Quartz Cron 格式錯誤導致 Bot 啟動崩潰
- 修正 Bot 啟動崩潰：移除 `appsettings Urls` 衝突、補 Aspire HTTP Endpoint 設定
- 修正 Slash Command 註冊時序問題，新增自動 DB Migration
- 將 `ANTHROPIC_API_KEY` 更名為 `AITEAM_ANTHROPIC_KEY`，避免 Shell 環境變數覆蓋

### 重構
- 抽離 `AiTeam.Data` / `AiTeam.Shared` 共用層，移除 Bot 內的舊 Data 相依

### 文件
- 新增 README.md，包含專案概覽、架構說明與快速啟動指南
- Doc Agent 自動產生 10 個 Markdown 文件檔案
- 更新 Stage 2、3、4、5、6 狀態為已完成，補充實作重點紀錄
- Stage 6 新增 Dashboard UI 微調清單（第十二項）
