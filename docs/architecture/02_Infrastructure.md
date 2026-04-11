# 02 基礎建設規劃

> 所屬專案：AI 團隊實作總規劃  
> 最後更新：2026-04-02

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
| 自然語言 | 直接在 `#victoria-ceo` 說話，不需格式（Stage 7）|
| `/reload-rules` | 強制重新載入 DB 規則快取（Stage 8，原 Notion 版改為 DB 版）|
| `/status` | 查詢目前各 Agent 狀態 |

---

## 二、資料儲存規劃

> **Stage 8 更新（2026-04-02）：Notion 已完全移除，所有資料集中在 PostgreSQL，由 Dashboard 管理。**

### 2.1 PostgreSQL 作為唯一儲存層

| 資料 | 儲存位置 | 管理方式 |
|------|---------|---------|
| 任務 log | `tasks` / `task_logs` 表 | Dashboard 任務中心 |
| Agent 規則 | `rules` 表 | Dashboard 規則管理頁（CRUD）|
| Agent 設定 / 信任等級 | `agent_configs` 表 | Dashboard Agent 設定頁 |
| 系統動態設定 | `app_settings` 表 | Dashboard 系統設定區塊 |
| 專案 | `projects` 表 | Dashboard 專案管理頁 |
| 部署紀錄 | `tasks` 表（Ops Agent 寫入）| Dashboard 部署紀錄頁 |

### 2.2 規則 Cache 機制

Agent 規則從 DB 載入，透過記憶體快取管理：

- **TTL：5 分鐘**（`RulesService` 快取）
- **強制更新：** 下 `/reload-rules` 指令，立即清除記憶體快取並重新從 DB 載入
- 任務執行中途修改規則，當次不生效，下次才套用

### 2.3 PostgreSQL 資料表結構

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

**rules** — Agent 規則（Stage 8 新增，取代 Notion Rules）

| 欄位 | 型別 | 說明 |
|------|------|------|
| id | UUID | 主鍵 |
| agent_name | VARCHAR | 適用 Agent（nullable，null = 全域規則）|
| content | TEXT | 規則內容 |
| created_at | TIMESTAMP | 建立時間 |

**app_settings** — 系統動態設定（Stage 8 新增）

| 欄位 | 型別 | 說明 |
|------|------|------|
| key | VARCHAR | 設定鍵值（PK）|
| value | VARCHAR | 設定值 |

---

## 三、已確認細節

| 項目 | 決定 |
|------|------|
| 多語言支援 | 中文、英文都支援 |
| 部署環境 | 本地伺服器（Docker Compose + GitHub Actions CI/CD）|
| GitHub 整合範圍 | 初期個人 repo，預留公司組織 repo 擴充 |
| Agent 個性設定 | 後期再設定，不影響現有架構 |
| 規則儲存 | PostgreSQL `rules` 表（Stage 8 起，Notion 已移除）|

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
