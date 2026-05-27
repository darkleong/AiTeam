# Stage 7：Software Team 完全體

> 版本：v2.0
> 建立日期：2026-04-02
> 完成日期：2026-04-02
> 狀態：✅ 已完成

---

## 目標

Software Team 三個面向的強化：
1. **Agent 完全體**：新增 Reviewer、Release、Designer 三個 Agent
2. **部署自動化**：GitHub Actions CI/CD，push 即部署
3. **Discord 擬人化**：每個 Agent 獨立頻道 + 自然語言對話

---

## 一、新增三個 Agent

### 1.1 Reviewer Agent（Vera）

| 項目 | 內容 |
|------|------|
| 職責 | 專門負責 Code Review，比 Dev 兼做更深入 |
| 觸發方式 | CEO 分派 / PR 開啟時自動觸發 |
| 輸出 | PR 審查意見（GitHub Review Comments），分級：🔴 critical / 🟡 warning / 🟢 info |
| 備註 | 未指定 PR 號碼時，自動取最新 open PR |

**工作流程：**
```
PR 開啟
    ↓
Reviewer 讀取 PR diff
    ↓
逐檔分析，產出分級審查意見
    ↓
在 GitHub PR 留下 Review Comments
    ↓
通知老闆與 Dev
```

---

### 1.2 Release Agent（Rena）

| 項目 | 內容 |
|------|------|
| 職責 | 版本管理、整理 Changelog、產出 Release Notes、建立 GitHub Release tag |
| 觸發方式 | CEO 分派 / 老闆直接下指令 |
| 輸出 | 更新 CHANGELOG.md + GitHub Release（含 tag、Release Notes） |

**與 Ops 的分工（接力關係）：**
```
Release Agent（Rena）
  → 決定版本號（v1.0.0 → v1.1.0）
  → 彙整 Commits / PRs，整理 Changelog
  → 更新 CHANGELOG.md，建立 GitHub Release tag
      ↓
Ops Agent（Maya）
  → 執行部署到伺服器
  → 監控服務狀態
  → 出問題時回滾
```

---

### 1.3 Designer Agent（Demi）

| 項目 | 內容 |
|------|------|
| 職責 | 把功能需求轉換成 UI 規格文件，規劃頁面結構、MudBlazor 元件配置 |
| 觸發方式 | CEO 分派 |
| 輸出 | UI 規格 Markdown 文件，完成後以 `.md` 附件傳送到 Discord；含「開PR/存入docs」關鍵字時才推 GitHub |

**能做到的：**
- 功能需求 → 具體畫面規格
- 頁面結構與元件配置建議
- 熟悉 MudBlazor 元件，直接指定用哪個元件
- 低保真線框稿（文字描述）

**做不到的：**
- 視覺設計稿（顏色、字體、精緻排版）
- 使用者研究與訪談

---

## 二、GitHub Actions CI/CD 部署

### 架構

```
開發機
  git push → GitHub
      ↓
GitHub Actions（Self-hosted Runner on 本機）
  1. dotnet build + test
  2. docker build（Bot + Dashboard）
  3. push images 到 GitHub Container Registry（ghcr.io）
  4. docker compose pull + up -d --force-recreate
      ↓
Docker（同一台機器）
  aiteam-bot、aiteam-dashboard、postgres 容器自動更新
```

全程約 **3 分鐘**完成。

### 實際設定

| 項目 | 設定值 |
|------|--------|
| Runner 安裝位置 | `C:\actions-runner\` |
| Bot Image | `ghcr.io/darkleong/aiteam-bot:latest` |
| Dashboard Image | `ghcr.io/darkleong/aiteam-dashboard:latest` |
| Compose 檔（CI/CD 用）| `docker-compose.prod.yml`（repo 根目錄） |
| Secrets 檔 | `C:\Users\darkl\aiteam\.env`（不在 repo）|
| Postgres 版本 | `postgres:17`（volume 已用 PG17 初始化，不可降版）|

### docker-compose.prod.yml 結構

```yaml
services:
  aiteam-bot:
    image: ghcr.io/darkleong/aiteam-bot:latest
    restart: always

  aiteam-dashboard:
    image: ghcr.io/darkleong/aiteam-dashboard:latest
    restart: always
    ports:
      - "8080:8080"

  postgres:
    image: postgres:17
    restart: always
    volumes:
      - aiteam-postgres-data:/var/lib/postgresql/data

volumes:
  aiteam-postgres-data:
    external: true
```

### 重開機後自動啟動清單

| 服務 | 啟動方式 |
|------|---------|
| Docker Desktop | 系統服務（自動） |
| postgres / bot / dashboard 容器 | `restart: always`（Docker 啟動後自動）|
| GitHub Actions Runner | 開機捷徑（背景執行，自動）|

開機後約 **1-2 分鐘**系統完全就緒。

---

## 三、自然語言對話

### 目標

移除強制 `/task` 格式，改為在頻道直接輸入自然語言，CEO LLM 自行解讀意圖。

```
現在：/task project:ProjectA 修復登入 Bug

改成：Victoria，登入那邊有個 Bug，幫我看一下
改成：登入壞了，查一下
改成：幫我把首頁加一個 Loading 動畫
```

### CEO 反問機制

當老闆給的資訊不足時，CEO 不猜測，而是問一個最關鍵的問題，並提供目前可用的選項。

**範例對話：**
```
你：「我發現了一個 Bug，系統無法登入」
    ↓
CEO：「了解，是哪個專案的登入？目前有 ProjectA 和 ProjectB。」
    ↓
你：「ProjectA」
    ↓
CEO：「收到，我請 Dev 來處理 ProjectA 的登入 Bug。確認執行？」
    ↓
你確認 → Dev 開始執行
```

### 技術實作

| 項目 | 說明 |
|------|------|
| 移除 `/task` Slash Command | 改為 `MessageReceived` 事件路由 |
| `ConversationContextStore` | Singleton，per-channel 對話歷史，最多 6 輪 |
| CEO 回傳 `action: reply` | 需要反問時不派任務，只回文字等待回應 |
| 雙層確認不變 | CEO 理解完畢後，依然走確認流程才執行 |

---

## 四、Discord 頻道重設計

### 4.1 頻道結構

```
📁 Software Team
  # victoria-ceo        ← 主要指令中心，跟 CEO 說話
  # cody-dev            ← Dev 的 log + 可直接下指令
  # maya-ops            ← Ops 的 log + 可直接下指令
  # quinn-qa            ← QA 的 log + 可直接下指令
  # sage-doc            ← Doc 的 log + 可直接下指令
  # rosa-requirements   ← Requirements 的 log + 可直接下指令
  # vera-reviewer       ← Reviewer 的 log + 可直接下指令
  # rena-release        ← Release 的 log + 可直接下指令
  # demi-designer       ← Designer 的 log + 可直接下指令

📁 系統
  # 任務動態
  # 警報
  # 每日摘要
```

### 4.2 Bot 路由邏輯

```
訊息進來
    ↓
判斷來源頻道（對應 AgentConfig.DiscordChannelId）
    ↓
#victoria-ceo   → CEO Agent（現有流程）
#cody-dev       → Dev Agent（直接路由）
#vera-reviewer  → Reviewer Agent（直接路由）
...以此類推
```

每個 AgentConfig 新增 `DiscordChannelId` 欄位（EF Migration：`AddAgentConfigDiscordChannelId`）。

### 4.3 直接對話機制

**老闆直接找 Agent：**
```
你在 #cody-dev 說：「幫我看一下 BookingService 為什麼會 timeout」
    ↓
Dev Agent 直接處理
    ↓
CEO 在 #victoria-ceo 收到通知：
  「老闆在 #cody-dev 直接指派任務給 Dev，任務：BookingService timeout 問題」
```

**Agent 升級給 CEO：**
```
你在 #cody-dev 說：「幫我把這個功能從開發部署到 production」
    ↓
Dev Agent 評估：這需要 Ops 協同，超出我的範圍
    ↓
Dev 在 #cody-dev 回覆：「這個任務涉及部署，我請 CEO 來協調」
    ↓
CEO 在 #victoria-ceo 收到升級請求，接手走正常分派流程
```

### 4.4 Log 輸出機制

| 情況 | 輸出位置 |
|------|---------|
| Agent 執行中的步驟 log | 自己的頻道 |
| 任務完成摘要 | 自己的頻道 + #任務動態 |
| 錯誤 / 異常 | 自己的頻道 + #警報 |
| 老闆直接指派通知 | #victoria-ceo（CEO 的頻道） |

---

## 陷阱紀錄

| 問題 | 原因 | 解法 |
|------|------|------|
| Bot Dockerfile build 失敗 | `libgit2-1.8` 在 Ubuntu Noble 不存在 | LibGit2Sharp 0.31+ 內建 native binary，移除 apt-get install |
| Deploy job postgres 起不來 | .env 沒載入，`POSTGRES_PASSWORD` 為空 | 加 `--env-file "$env:USERPROFILE\aiteam\.env"` |
| postgres container 一直 unhealthy | volume 是 PG17 初始化，但 compose 用 `postgres:16` | 改用 `postgres:17` |
| `ANTHROPIC_API_KEY` 在容器內為空 | Claude Code 執行環境將此變數設為空字串，Docker Compose 優先使用 Shell 環境變數 | 改名為 `AITEAM_ANTHROPIC_KEY` |
| Dashboard SignalR「未連線」 | `DashboardPushService` 使用 Aspire 的 `http+dashboard://` scheme，Docker 內解析失敗 | 新增 `Dashboard:PushUrl` 設定，Docker 設為 `http://aiteam-dashboard:8080` |
| Designer 規格書看不到 | CommandHandler 完成後只傳 Embed，未讀取 `ui-spec-output` Payload | 完成後額外讀取 Payload，以 `.md` 附件傳送到 Discord |
| Reviewer Agent 失敗（ArgumentException）| 直接頻道指派時 `project=""` → `repo=""` → Octokit 噴錯 | 新增 `GitHub:DefaultRepo` 設定，無 Project 時 fallback |
| Reviewer 未指定 PR 號碼失敗 | `ExtractPrNumber` 找不到格式直接報錯 | 無號碼時自動呼叫 `GetLatestOpenPullRequestNumberAsync` |
| git push rejected（non-fast-forward）| Rena 在 remote 建立了 CHANGELOG commit，本地落後 | `git stash → pull --rebase → stash pop → push` |

---

## 驗收狀態

| 項目 | 狀態 |
|------|------|
| Reviewer Agent E2E | ✅ |
| Release Agent E2E | ✅ |
| Designer Agent E2E | ✅ |
| CI/CD push-to-deploy（約 3 分鐘）| ✅ |
| Discord 九個 Agent 頻道 | ✅ |
| 自然語言對話 + CEO 反問機制 | ✅ |
| 直接對話 CEO CC 通知 | ✅ |
| Agent 升級給 CEO | ✅ |

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-02 | v1.0 初版建立（規劃） |
| 2026-04-02 | v2.0 Stage 7 實作完成，補充實際 Agent 名稱、CI/CD 設定、陷阱紀錄 |
