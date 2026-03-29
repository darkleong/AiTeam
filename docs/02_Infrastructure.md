# 02 基礎建設規劃

> 所屬專案：AI 團隊實作總規劃  
> 最後更新：2026-03-29

---

## 一、Discord 頻道規劃

### 1.1 共用頻道

| 頻道名稱 | 用途 |
|----------|------|
| `#指令中心` | 你下達指令給 CEO 的主要頻道 |
| `#任務動態` | 所有 Agent 的任務狀態更新 |
| `#警報` | 異常、錯誤、需要緊急處理的事項 |
| `#每日摘要` | 每日自動產出的任務摘要報告 |

### 1.2 各專案頻道（動態建立）

每個專案由 CEO 自動建立對應頻道群組，例如：

```
📁 ProjectA
  #projecta-指令
  #projecta-dev-log
  #projecta-ops-log

📁 ProjectB
  #projectb-指令
  #projectb-dev-log
  #projectb-ops-log
```

新增專案時，直接告知 CEO，由 CEO 自動建立並請你確認。

### 1.3 專用指令

| 指令 | 說明 |
|------|------|
| `/task project:ProjectA [描述]` | 指定專案下達任務 |
| `/reload-rules` | 強制重新拉取 Notion 規則（清除 Cache）|
| `/status` | 查詢目前各 Agent 狀態 |

---

## 二、資料儲存規劃

### 2.1 Notion 與 PostgreSQL 分工原則

兩者都會有任務紀錄，但層次不同，互補而不重複：

| | Notion | PostgreSQL |
|---|---|---|
| **定位** | 人類可讀、Agent 學習參考 | 完整 raw 資料、Dashboard 來源 |
| **任務紀錄內容** | 摘要、結論、你的修正備註 | 每個執行步驟的詳細 log |
| **寫入頻率** | 每個任務一筆（任務結束後） | 每個執行步驟即時寫入（高頻） |
| **查詢方式** | 你用眼睛看 | 程式查詢、圖表呈現 |

> **一句話總結：PostgreSQL 記錄「發生了什麼」，Notion 記錄「值得記住什麼」。**

---

### 2.2 寫入權限分區

| 寫入者 | 可寫入的內容 |
|--------|-------------|
| **你** | 規則庫（新增/修改）、Agent 信任等級 |
| **CEO Agent** | 任務摘要（建立/更新）、每日摘要 |
| **Dev Agent** | 任務摘要（執行結果、程式碼摘要） |
| **Ops Agent** | 任務摘要（部署結果、環境狀態） |

**規則庫與信任等級只有你能修改**，Agent 僅能讀取參考，無法自行修改自己的規則。

---

### 2.3 規則 Cache 機制

Agent 不會主動感知 Notion 的變更，規則透過 Cache 機制管理：

- **TTL：1 小時**，到期後下次任務自動重新拉取
- **強制更新：** 下 `/reload-rules` 指令，立即清除 Cache 並重新拉取
- 任務執行中途的修改，當次不生效，下次才套用

**TTL 設定位置：** `appsettings.json`
```json
{
  "AgentSettings": {
    "NotionCacheTtlMinutes": 60
  }
}
```

---

### 2.4 Notion 資料庫結構

**規則庫（Rules）** — 只有你能寫入

| 欄位 | 說明 |
|------|------|
| Agent | 適用的 Agent |
| 規則內容 | 具體規定 |
| 建立日期 | 何時加入 |
| 來源 | 你的指示 / 自動歸納 |

**任務摘要（Task Summary）** — Agent 寫入

| 欄位 | 說明 |
|------|------|
| 任務標題 | 簡短描述 |
| 執行 Agent | 由誰執行 |
| 輸入指令 | 你下達的原始指令 |
| 執行結果 | 成功 / 失敗 / 部分完成 |
| 你的修正 | 若有修正，記錄在此 |
| 日期 | 執行時間 |

**Agent 狀態（Agent Status）** — 只有你能寫入

| 欄位 | 說明 |
|------|------|
| Agent 名稱 | CEO / Dev / Ops / ... |
| 信任等級 | Level 0 ~ 3 |
| 自主決策清單 | 可自主執行的任務類型 |
| 最後更新 | 上次調整時間 |

---

### 2.5 PostgreSQL 資料表結構

**teams** — 團隊主表（預留多團隊）

| 欄位 | 型別 | 說明 |
|------|------|------|
| id | UUID | 主鍵 |
| name | VARCHAR | 團隊名稱（Software / Finance / ...） |
| description | VARCHAR | 團隊描述 |
| is_active | BOOLEAN | 是否啟用 |
| created_at | TIMESTAMP | 建立時間 |

**projects** — 專案主表

| 欄位 | 型別 | 說明 |
|------|------|------|
| id | UUID | 主鍵 |
| team_id | UUID | 所屬團隊 |
| name | VARCHAR | 專案名稱 |
| repo_url | VARCHAR | Git repo 網址 |
| tech_stack | JSONB | 技術棧描述 |
| is_active | BOOLEAN | 是否啟用 |
| created_at | TIMESTAMP | 建立時間 |

**tasks** — 任務主表

| 欄位 | 型別 | 說明 |
|------|------|------|
| id | UUID | 主鍵 |
| team_id | UUID | 所屬團隊 |
| project_id | UUID | 所屬專案（可為空，跨專案任務） |
| title | VARCHAR | 任務標題 |
| triggered_by | VARCHAR | 觸發來源（Discord / GitHub / 排程） |
| assigned_agent | VARCHAR | 負責 Agent |
| status | VARCHAR | 整體狀態 |
| created_at | TIMESTAMP | 建立時間 |
| completed_at | TIMESTAMP | 完成時間 |

**task_logs** — 詳細執行步驟

| 欄位 | 型別 | 說明 |
|------|------|------|
| id | UUID | 主鍵 |
| task_id | UUID | 任務 ID |
| agent | VARCHAR | 執行 Agent |
| step | VARCHAR | 步驟描述 |
| status | VARCHAR | pending / running / done / failed |
| payload | JSONB | 執行細節（raw data） |
| created_at | TIMESTAMP | 時間戳 |

---

## 三、已確認細節

| 項目 | 決定 |
|------|------|
| 多語言支援 | 中文、英文都支援 |
| 部署環境 | 本地伺服器（搭配 Tailscale 跨網路連線） |
| GitHub 整合範圍 | 初期個人 repo，預留公司組織 repo 擴充 |
| Agent 個性設定 | 後期再設定，不影響現有架構 |

### GitHub 多帳號設計（預留）

```
GitHub 整合設定
    ├── 個人帳號（現在）
    │     Token: github_pat_xxx
    │
    └── 公司組織（未來）
          Token: github_pat_yyy
          Org: company-name
```

Dev Agent 操作 repo 時，先查該 repo 屬於哪個帳號，再套用對應的 Token，不需要改動核心邏輯。
