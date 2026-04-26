# 01 願景、架構與 Agent 定義

> 所屬專案：AI 團隊實作總規劃  
> 最後更新：2026-03-29

---

## 一、願景與目標

建立一個以軟體開發為核心的 AI 團隊，由你擔任老闆/負責人角色，透過 Discord 下達指令、透過 Notion 追蹤記錄，未來透過 Blazor Dashboard 視覺化管理。

AI 團隊圍繞軟體開發與維護運作，初期專注於開發輔助與部署管理，後續可持續擴充新成員（Agent）。

---

## 二、核心設計原則

### 2.1 雙層確認機制（初期）

所有任務在初期採用雙層確認，確保你對每個決策有完整掌控：

```
你下指令
    ↓
CEO Agent 分析、規劃、提出方案
    ↓ [確認點 1] 回報你審核
你核准
    ↓
執行層 Agent 說明「我要做什麼」
    ↓ [確認點 2] 回報你審核
你核准
    ↓
實際執行（commit / 部署 / 回滾）
    ↓
結果回報 Discord + 寫入 Notion
```

### 2.2 信任等級制度

隨著 Agent 表現穩定，可逐步下放權限：

| 等級 | 說明 |
|------|------|
| Level 0（初期） | 所有決策與執行都需你確認 |
| Level 1 | 例行任務自主執行，重要決策仍問你 |
| Level 2 | 大部分自主，僅高風險操作需確認 |
| Level 3 | 完全自主，結果事後回報 |

**信任等級由你手動調整**，不是 Agent 自動晉升。

### 2.3 Agent 記憶與進步機制

LLM 本身不會自我學習，但透過以下設計實現「持續進步」：

- **規則層**：你的明確修正 → 寫進 System Prompt 規則清單，永遠生效
- **經驗層**：每次任務的決策與結果 → 存進 Notion，下次類似情境撈出參考
- **權限層**：穩定的決策類型 → 加入「可自主執行」清單，不再需詢問

---

## 三、整體架構

### 3.1 多團隊架構（長期目標）

系統採用「多團隊、各自獨立」的設計，每個 Team 有自己的 CEO 與 Agent，上層由總 CEO 統籌：

```
你（老闆）
    ↓
總 CEO（你的直接窗口，跨團隊協調）
    │
    ├─────────────────────────────────────────┐
    ▼                                         ▼
Software Team CEO                      Finance Team CEO（未來）
統籌軟體開發任務                         統籌金融資訊任務
    │                                         │
    ├── Dev Agent                             ├── 股市追蹤 Agent
    └── Ops Agent                            └── 新聞分析 Agent
    │
    └──────────────┬──────────────────────────┘
                   │
        結果回報 Discord
        摘要寫入 Notion
        詳細 log 寫入 PostgreSQL
                   ↓
        （未來）Blazor Dashboard
        視覺化顯示所有團隊狀態
```

**設計原則：**
- 每個 Team 獨立運作，規則與記憶互不干擾
- 新增 Team 只需橫向擴充，不動現有架構
- 你只跟總 CEO 說話，不需要知道底層由哪個 Team 處理

### 3.2 當前實作範圍（Software Team）

初期只實作 Software Team，但資料結構從一開始就預留多團隊支援：

```
你（老闆）
    ↓
總 CEO（兼 Software Team CEO，初期合併）
    │
    ├── Dev Agent（開發助手）
    └── Ops Agent（部署管理）
```

> 待第二個 Team 出現時，總 CEO 與 Software Team CEO 再拆分為獨立角色。

---

## 四、Agent 定義

### 4.1 CEO Agent（總指揮）

| 項目 | 內容 |
|------|------|
| 職責 | 接收指令、分析任務、分派給對應 Agent、追蹤執行結果 |
| 初期決策邊界 | 所有決策回報你確認後才執行 |
| 記憶來源 | Notion 的規則清單 + 歷史任務紀錄 |
| 觸發方式 | Discord 主動下指令 / 事件自動觸發 |
| Agent 分派判斷 | 完全由 CEO 自主判斷（AI 決定），動態從資料庫載入可用 Agent 清單 |

**CEO 的核心 Prompt 結構：**
```
System Prompt
  ├── 角色定義
  ├── 可用 Agent 清單（動態注入，從資料庫載入）
  ├── 規則清單（從 Notion Cache 拉取）
  └── 可自主執行清單（從 Notion Cache 拉取）

User Message
  ├── 當前專案清單（從 PostgreSQL 撈取）
  ├── 近期相關任務紀錄（從 PostgreSQL 撈取）
  └── 使用者輸入
```

**CEO 固定回傳 JSON 格式：**
```json
{
  "reply": "收到，我來處理預約重疊的 Bug",
  "action": "delegate",
  "target_agent": "Dev",
  "task": {
    "title": "修復預約時間重疊 Bug",
    "project": "ProjectA",
    "description": "時間選 12:00 送出後顯示錯誤",
    "priority": "high"
  },
  "require_confirmation": true
}
```

---

### 4.2 開發助手 Agent（Dev）

| 項目 | 內容 |
|------|------|
| 職責 | 寫程式、解 Bug、Code Review、操作 Git repo |
| 初期操作範圍 | 完全自主操作 repo，但執行前需你確認 |
| 支援語言/框架 | C# / Blazor / WPF / EF Core / Aspire / Telerik |
| 觸發方式 | CEO 分派 / GitHub 事件（PR 開啟、Issue 建立） |

**初期確認範例（Discord 訊息）：**
```
[Dev Agent] 我準備執行以下操作：
- 修改 BookingService.cs 的查詢邏輯
- commit 到 feature/fix-booking-overlap branch
- 開啟 PR 請你審核

確認執行？ ✅ / ❌
```

---

### 4.3 部署管理 Agent（Ops）

| 項目 | 內容 |
|------|------|
| 職責 | 監控 CI/CD 狀態、觸發部署、必要時執行回滾 |
| 初期操作範圍 | 觸發與回滾均可自主，但初期執行前需你確認 |
| 監控目標 | Build 狀態、部署結果、服務健康檢查 |
| 觸發方式 | PR Merge 事件 / 排程監控 / 你主動下指令 |

**初期確認範例（Discord 訊息）：**
```
[Ops Agent] 偵測到 main branch 有新 merge
準備部署到 production 環境

確認執行？ ✅ / ❌
```
