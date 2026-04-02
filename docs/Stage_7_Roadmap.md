# Stage 7：Software Team 完全體

> 版本：v1.0
> 建立日期：2026-04-02
> 狀態：✅ 已完成（2026-04-02）

---

## 目標

Software Team 三個面向的強化：
1. **Agent 完全體**：新增 Reviewer、Release、Designer 三個 Agent
2. **部署自動化**：GitHub Actions CI/CD，push 即部署
3. **Discord 擬人化**：每個 Agent 獨立頻道 + 自然語言對話

---

## 一、新增三個 Agent

### 1.1 Reviewer Agent

| 項目 | 內容 |
|------|------|
| 職責 | 專門負責 Code Review，比 Dev 兼做更深入 |
| 觸發方式 | CEO 分派 / PR 開啟時自動觸發 |
| 輸出 | PR 審查意見（GitHub Review Comments） |
| 重點 | 程式碼品質、安全性、規範符合度、可維護性 |

**工作流程：**
```
PR 開啟
    ↓
Reviewer 讀取 PR 差異
    ↓
逐檔分析，產出審查意見
    ↓
在 GitHub PR 留下 Review Comments
    ↓
通知老闆與 Dev
```

---

### 1.2 Release Agent

| 項目 | 內容 |
|------|------|
| 職責 | 版本管理、整理 Changelog、產出 Release Notes、建立 GitHub Release tag |
| 觸發方式 | CEO 分派 / 老闆直接下指令 |
| 輸出 | GitHub Release（含 tag、Release Notes） |

**與 Ops 的分工（接力關係）：**
```
Release Agent
  → 決定版本號（v1.0.0 → v1.1.0）
  → 整理 Changelog（從 commit history）
  → 產出 Release Notes
  → 在 GitHub 建立 Release tag
      ↓
Ops Agent
  → 執行部署到伺服器
  → 監控服務狀態
  → 出問題時回滾
```

---

### 1.3 Designer Agent

| 項目 | 內容 |
|------|------|
| 職責 | 把功能需求轉換成 UI 規格文件，規劃頁面結構、MudBlazor 元件配置 |
| 觸發方式 | CEO 分派 |
| 輸出 | UI 規格文件（Markdown），Dev 依此實作 |

**能做到的：**
- 功能需求 → 具體畫面規格
- 頁面結構與元件配置建議
- 熟悉 MudBlazor 元件，直接指定用哪個元件
- 低保真線框稿（文字描述）

**做不到的：**
- 視覺設計稿（顏色、字體、精緻排版）
- 使用者研究與訪談

**工作流程：**
```
你 → CEO：「我需要 Token 監控功能，圖表化呈現每個 Agent 的狀況」
    ↓
CEO → Designer：「幫我規劃這個功能的畫面」
    ↓
Designer 產出 UI 規格文件：
  - 頁面結構（有哪些區塊）
  - 元件規格（用什麼圖表、顯示什麼資料）
  - 資料來源（從哪裡取得）
  - 互動行為（篩選、切換等）
    ↓
CEO → Dev：「依照這份規格實作」
```

---

## 二、GitHub Actions CI/CD 部署

### 背景

目前啟動方式：`dotnet run --project src/AiTeam.AppHost`（Aspire 開發模式）。
目標：push 到 main branch → 自動建立 Docker image → 部署到本機伺服器。

### 架構

```
開發機
  git push → GitHub
      ↓
GitHub Actions
  1. dotnet build + test
  2. docker build（Bot + Dashboard）
  3. push images 到 GitHub Container Registry（ghcr.io）
      ↓
本機伺服器（Windows 11 + Docker Desktop）
  4. Actions SSH 進伺服器
  5. docker compose pull
  6. docker compose up -d --force-recreate
```

### 所需工具

| 工具 | 用途 |
|------|------|
| `aspirate` | 從 Aspire AppHost 產出 docker-compose.yml + Dockerfile |
| GitHub Container Registry（ghcr.io）| 儲存 Docker image，免費 |
| GitHub Actions | CI/CD 流程自動化 |
| Tailscale / 固定 IP | 讓 GitHub Actions 能 SSH 進本機伺服器 |

### docker-compose.yml 結構（預期）

```yaml
services:
  aiteam-bot:
    image: ghcr.io/{username}/aiteam-bot:latest
    restart: always

  aiteam-dashboard:
    image: ghcr.io/{username}/aiteam-dashboard:latest
    restart: always
    ports:
      - "8080:8080"

  postgres:
    image: postgres:latest
    restart: always
    volumes:
      - aiteam-postgres-data:/var/lib/postgresql/data
```

### 更新流程

```bash
# 開發者只需要：
git push origin main

# 剩下 GitHub Actions 自動處理：
# build → push image → SSH 進伺服器 → docker compose up -d
```

---

## 三、自然語言對話

### 背景

目前與 CEO 互動需要使用 Slash Command（`/task project:ProjectA 描述`），格式死板，無法自然對話。

### 目標

移除強制結構，改為在頻道直接輸入自然語言，CEO LLM 自行解讀意圖。

```
現在：/task project:ProjectA 修復登入 Bug

改成：Victoria，登入那邊有個 Bug，幫我看一下
改成：登入壞了，查一下
改成：幫我把首頁加一個 Loading 動畫
```

### CEO 反問機制

當老闆給的資訊不足時，CEO 不會猜測，而是問一個最關鍵的問題（一次只問一個），並提供目前可用的選項供老闆快速回答。

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
| 移除 `/task` Slash Command | 改為監聽頻道的一般訊息 |
| CEO 回傳 `action: reply` | 需要反問時不派任務，只回文字等待回應 |
| 對話 context 管理 | 同一個 Discord thread / 頻道保留最近幾輪對話，讓 CEO 知道上下文 |
| 雙層確認不變 | CEO 理解完畢後，依然走確認流程才執行 |

> 安全保障：就算 CEO 誤解了意圖，雙層確認機制讓老闆在執行前有機會修正。

---

## 四、Discord 頻道重設計

### 3.1 頻道結構

```
📁 Software Team
  # victoria-ceo        ← 主要指令中心，跟 CEO 說話
  # cody-dev            ← Dev 的 log + 可直接下指令
  # maya-ops            ← Ops 的 log + 可直接下指令
  # quinn-qa            ← QA 的 log + 可直接下指令
  # sage-doc            ← Doc 的 log + 可直接下指令
  # rosa-requirements   ← Requirements 的 log + 可直接下指令
  # leo-reviewer        ← Reviewer 的 log + 可直接下指令（新）
  # ryan-release        ← Release 的 log + 可直接下指令（新）
  # dana-designer       ← Designer 的 log + 可直接下指令（新）

📁 系統
  # 任務動態
  # 警報
  # 每日摘要
```

> 注意：Reviewer / Release / Designer 的名字為暫定，待 Agent 個性設定討論後確認。

### 3.2 Bot 路由邏輯

```
訊息進來
    ↓
判斷來源頻道
    ↓
#victoria-ceo   → CEO Agent（現有流程）
#cody-dev       → Dev Agent（直接路由）
#quinn-qa       → QA Agent（直接路由）
...以此類推
```

每個 AgentConfig 記錄新增 `DiscordChannelId` 欄位，Bot 啟動時載入頻道 → Agent 的對應表。

### 3.3 直接對話機制

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

### 3.4 Log 輸出機制

| 情況 | 輸出位置 |
|------|---------|
| Agent 執行中的步驟 log | 自己的頻道 |
| 任務完成摘要 | 自己的頻道 + #任務動態 |
| 錯誤 / 異常 | 自己的頻道 + #警報 |
| 老闆直接指派通知 | #victoria-ceo（CEO 的頻道） |

---

## 實作順序建議

| 順序 | 項目 | 原因 |
|------|------|------|
| 1 | Discord 頻道重設計 + 自然語言對話 | 影響所有後續開發的體驗，一起做最有效率 |
| 2 | GitHub Actions CI/CD | 部署自動化，讓後續開發更流暢 |
| 3 | Reviewer Agent | 需要 GitHub PR 流程，搭配 CI/CD 一起用 |
| 4 | Release Agent | 依賴穩定的部署流程 |
| 5 | Designer Agent | 純輸出型 Agent，相對獨立 |

---

## 驗收標準

| 項目 | 標準 |
|------|------|
| 三個新 Agent | 各自 E2E 測試通過，能透過 Discord 完整走完流程 |
| CI/CD | push 到 main 後，5 分鐘內自動完成部署 |
| Discord 頻道 | 每個 Agent 有獨立頻道，老闆可直接對話，CEO CC 正常，升級機制正常 |
| 自然語言對話 | 不需 `/task`，CEO 能正確解讀自然語言並在資訊不足時反問 |

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-04-02 | 初版建立 |
