# Stage 3 — 第一批 Agent 上線

> 所屬專案：AI 團隊實作總規劃  
> 狀態：✅ 已完成
> 最後更新：2026-03-31

---

## 目標

在 Stage 2 的地基上，讓 Dev Agent 與 Ops Agent 正式運作，接上事件自動觸發機制。

---

## 交付項目

- [x] Dev Agent 實作（GitHub API 整合、程式碼操作）
- [x] Ops Agent 實作（CI/CD 串接、部署監控）
- [x] GitHub Webhook 接收與處理
- [x] 事件自動觸發機制（PR 開啟、Issue 建立、Merge 事件）
- [ ] 多專案支援（CEO 自動建立 Discord 頻道與 Notion 頁面）← 留 Stage 4

---

## 已確認細節

| 項目 | 決定 |
|------|------|
| CI/CD 工具 | GitHub Actions |
| 部署流程 | 直接部署到 production |
| Merge 決策 | 永遠由你手動執行，Agent 不介入 |
| 部署觸發 | PR Merge 到 main 後自動觸發 |

---

## 完整工作流程

```
Issue 建立（GitHub）
    ↓ 自動觸發
CEO Agent 分析、分派給 Dev
    ↓ 你確認
Dev Agent 修改程式碼、commit、開 PR
    ↓ 自動觸發
（未來）測試 Agent 執行測試、產出報告
    ↓
Discord 通知你：PR #xx 已就緒，附測試報告，請審查
    ↓ 你審查程式碼，按下 Merge  ← 你唯一需要做的事
PR Merge 到 main
    ↓ GitHub Actions 自動觸發
編譯 → 測試 → 部署到 production
    ↓ Ops Agent 監控部署結果
成功：Discord 通知你，寫入 Notion + PostgreSQL
失敗：自動回滾 + Discord + Email 通知你
```

---

## GitHub 事件觸發對應

| 事件 | 觸發對象 | 說明 |
|------|---------|------|
| Issue 建立 | CEO Agent | 分析需求，分派給 Dev |
| PR 開啟 | 測試 Agent（未來）/ 通知你 | 現在直接通知你審查 |
| PR Merge 到 main | GitHub Actions + Ops Agent | 自動部署，Ops 監控結果 |

---

## CI/CD 設計（GitHub Actions）

部署設定檔放在 repo 內，由 Dev Agent 產出，你審查後 Merge 生效：

```
.github/workflows/deploy.yml
  → PR Merge 到 main 時觸發
  → 編譯（dotnet build）
  → 測試（dotnet test）
  → 部署到本地伺服器
```

**Ops Agent 的角色：** 不取代 GitHub Actions，而是在部署完成後監控結果，處理成功通知與失敗回滾邏輯。

---

## GitHub Webhook 安全性驗證

採用 GitHub 官方標準：**Secret Token 簽章驗證**

```
GitHub 發送 Webhook 請求
    ↓
附上 X-Hub-Signature-256 標頭（用 Secret 產生的簽章）
    ↓
伺服器驗證簽章是否正確
    ↓
正確 → 處理事件
錯誤 → 拒絕，不處理
```

Secret Token 存放在 `appsettings.json`，不寫進程式碼。

---

## 多專案 Context 切換

**從 Webhook 的 repo 資訊自動判斷，不需要手動指定：**

```
Issue 建立（ProjectA 的 repo）
    ↓
Webhook 帶著 repo 資訊進來
    ↓
系統自動對應到 ProjectA
    ↓
CEO 分派任務時，任務已綁定 ProjectA 的 context
    ↓
Dev Agent 收到任務，直接知道是哪個專案
```

你在 Discord 主動下指令時，用 `/task project:ProjectA` 明確指定專案。

---

## 本地伺服器部署方式

採用 **GitHub Actions Self-hosted Runner**：

```
PR Merge 到 main
    ↓
GitHub Actions 觸發
    ↓
指令發給本地伺服器上的 Runner
    ↓
Runner 在本地直接執行部署腳本
```

優點：Runner 主動連出去，不需要開放任何對外 port，搭配 Tailscale 更安全。

實際安裝與設定交由 Claude Code 協助執行。

---

## Dev Agent 設計細節

### 支援的任務類型

| 任務類型 | 說明 |
|---------|------|
| 修復 Bug | 分析問題、修改程式碼、開 PR |
| 新增功能 | 實作新需求、開 PR |
| 重構 | 改善程式碼結構、開 PR |
| Code Review | 審查現有程式碼，回報問題與建議 |

### 操作程式碼的方式（混合模式）

Dev Agent 根據任務類型自動選擇操作方式，你不需要介入：

| 任務 | 方式 | 原因 |
|------|------|------|
| Code Review | GitHub API（只讀） | 不需要修改檔案 |
| 修復 Bug / 新增功能 / 重構 | Clone repo 到本地 | 需要跨檔案操作 |

```
收到任務
    ↓
判斷任務類型
    ↓
Code Review → GitHub API 讀取程式碼 → 產出審查報告
其他任務   → Clone repo → 修改檔案 → Commit → 開 PR → 清理本地暫存
```

### 確認流程

Dev Agent 直接 commit 到 feature branch，你在 PR 審查時確認：

```
[Dev Agent] 已完成以下操作：
- 修改：BookingService.cs
- Branch：feature/fix-booking-overlap
- PR #42 已開啟，請審查

👉 PR 連結：https://github.com/xxx/ProjectA/pull/42
```

---

## Ops Agent 設計細節

### 監控項目

| 事件 | 處理方式 |
|------|---------|
| Build 失敗 | 立刻通知你 |
| 部署失敗（內層）| 自動回滾，通知你結果 |
| 部署失敗（外層）| 通知你，等你指示 |
| 服務健康檢查異常 | 立刻通知你 |
| CPU / 記憶體超過門檻 | 立刻通知你 |
| Docker 資源不足或異常 | 立刻通知你 |
| 部署成功 | 通知你 |

### 回滾策略

```
【內層失敗】Container 啟不來或立刻 crash
    → 自動回滾到上一個穩定版本
    → 通知你：「已自動回滾到 v1.2」

【外層失敗】Container 啟來了，但健康檢查失敗
    → 通知你，等你決定
    → 你可以回覆「回滾」或「觀察」
```

### 部署腳本

使用 PowerShell，由 Dev Agent 產出，放在 repo 內：

```powershell
# deploy.ps1
docker-compose pull        # 拉取最新 image
docker-compose up -d       # 啟動服務
docker-compose ps          # 確認狀態
```

### 監控設定（appsettings.json）

```json
"OpsSettings": {
  "HealthCheckIntervalMinutes": 30,
  "CpuAlertThresholdPercent": 80,
  "MemoryAlertThresholdPercent": 80
}
```

門檻可隨時調整，不需要改動程式碼。

---

## 開發測試環境設定

### 測試 Repo

初期使用獨立的測試 repo `AiTeam-Test`，與主要 repo 完全隔離：

- Dev Agent 第一次上線期間，所有 branch / commit / PR 都在 `AiTeam-Test` 進行
- 確認 Agent 行為穩定後，再切換到真實 repo
- 降低測試期間對主要 repo 造成污染的風險
- **由 Claude Code 協助在 GitHub 上建立**

### Webhook 內網穿透

本地開發時 GitHub 無法直接打到你的電腦，使用 **Tailscale Funnel** 解決：

```
GitHub Webhook
    ↓
Tailscale Funnel（固定公開 URL）
    ↓
你的本地開發機（Tailscale 節點）
    ↓
Bot 程式接收 Webhook 事件（Port 5050）
```

選擇 Tailscale Funnel 的原因：
- 專案本來就規劃使用 Tailscale，不需要額外安裝工具
- URL 固定，GitHub Webhook 設定一次就不用再改
- 開發與正式環境一致，不需要切換

**Tailscale 尚未安裝，交由 Claude Code 協助安裝與設定 Funnel。**

### 其他設定細節

| 項目 | 決定 |
|------|------|
| Webhook 接收 Port | 5050（不與 Aspire Dashboard 衝突） |
| Dev Agent Clone 路徑 | `D:\AiTeam-Workspace\` |
| GitHub Webhook Secret | 由 Claude Code 產生並設定到 User Secrets |

---

## 待討論事項

- [ ] 測試 Agent 的細節（Stage 5 展開時討論）

---

## 實作重點紀錄

### Dev Agent
- 混合模式：Code Review 用 GitHub API（只讀），其他任務本地 Clone repo 操作
- LibGit2Sharp 與 Octokit 都有 `Signature` 型別，需明確用 `LibGit2Sharp.Signature` 避免編譯錯誤
- GitHub PAT 需有 `repo`、`workflow` 權限
- `GitHub:Owner` 須加到 `appsettings.json`（Dev Agent 呼叫 `BuildPlanAsync` 需要此欄位）
- Clone 路徑：`D:\AiTeam-Workspace\`，由 `GitHubSettings.WorkspacePath` 設定

### Ops Agent
- Discord namespace 衝突（`AiTeam.Bot.Discord` vs `Discord`）：
  WebhookController 與 OpsAgentService 需加 `using DiscordNet = Discord;` alias
- 健康檢查透過 `docker ps` 查詢容器狀態，Quartz.NET 排程定期執行

### Webhook
- GitHub Webhook 用 HMACSHA256 驗簽（`X-Hub-Signature-256` header）
- Tailscale Funnel URL：`https://love-desktop.tailcd0255.ts.net/` → 本地 Port 5050
- Webhook 路由：`[Route("webhook/github")]`，需在 `AppHost` 加 `.WithHttpEndpoint(port: 5050)`

### Discord 頻道自動建立
- Bot 在 `OnReady` 事件呼叫 `EnsureChannelsAsync()`，自動建立缺少的頻道
- 所需頻道：#指令中心、#任務動態、#警報、#每日摘要
- Bot 需要 Discord 伺服器的 **Manage Channels** 權限

### 整合測試結果（2026-03-31）
- 測試 Repo：AiTeam-Test（`github.com/darkleong/AiTeam-Test`）
- 完整流程通過：`/task` → CEO 分析 → `confirm_yes` → `exec_yes` → Dev Agent 執行 → PR #1 建立成功
- PR URL 自動回報到 Discord #任務動態 ✅
