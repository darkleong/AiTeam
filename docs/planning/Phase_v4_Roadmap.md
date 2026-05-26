# Phase v4-rewrite Roadmap — AiTeam 純記錄系統改造

> **Phase 性質**：架構級重構（major bump 3.79 → 4.0）
> **執行模式**：Petra 全能模式（大授權 / `bypassPermissions` / Christ 不介入細節）
> **執行分支**：`v4-rewrite`（main 凍結 / 改完一次 merge）
> **規劃日期**：2026-05-26

---

## Phase 目標

AiTeam 從「執行 + 記錄」雙重身份系統、轉成「純記錄 MCP server」：
- **執行端搬到 Christ 本機 Claude Code Agent Team**（v2.1.32+ 內建 experimental feature / Opus 4.6 推出）
- **AiTeam 變成 MCP server** — Bot 容器暴露 HTTP MCP endpoint、Claude Code teammate 透過 MCP tool 寫記錄回來
- **Dashboard 變成記錄檢視介面**（minimal scope / 表格頁 + 既有頁面整理）

---

## 12 項拍板共識

| # | 項目 | 拍板 |
|---|---|---|
| 1 | 大方向 | AiTeam → 純記錄系統、執行端搬到 Claude Code Agent Team |
| 2 | AiTeam 範圍 | 純記錄（不做 teammate SoT 管理） |
| 3 | 6 Talent（Victoria/Petra/Cody/Vera/Quinn/Sage）| 全砍、只剩 Petra 當 default lead persona（本機 subagent definition）|
| 4 | Aria + Forge 工作模式框架 | 全砍 |
| 5 | Discord | 留、純被動通知（MCP 寫進來時觸發）|
| 6 | MCP 技術 | C# 寫 |
| 7 | MCP 部署 | Bot 容器內嵌、HTTP transport |
| 8 | MCP Auth | 統一 API key |
| 9 | Spawn 機制 | natural language spawn（Petra 訊息描述 teammate）|
| 10 | 記錄資料 | Task lifecycle、Teammate 對話 log、Token 消耗 |
| 11 | 過渡策略 | 開 `v4-rewrite` 分支、main 凍結、改完一次切 |
| 12 | Dashboard scope | 新增 1 頁、純表格檢視 records、無其他功能（視覺化/stats 全延後）|

---

## Stage 切分

| Stage | 範圍 | 依賴 | 狀態 |
|---|---|---|---|
| **88** | Spike：C# MCP SDK 驗證、Claude Code remote MCP 相容性驗、開 `v4-rewrite` 分支 | 無 | in_progress |
| **89** | 砍舊架構：Bot 內 6 Talent worker / LlmProviderFactory / HITL / Talent 配置頁 / Dashboard 操作中心。DB schema 改造（drop 舊 table） | 88 | pending |
| **90** | MCP server endpoint：Bot 容器內嵌、C# 寫、HTTP transport、API key auth、minimal health check tool 驗接通 | 88、89 | pending |
| **91** | MCP record tools：`register_team` / `record_task` / `record_message` / `record_token_usage`、DB schema 新表 | 90 | pending |
| **92** | Dashboard 表格頁（minimal、MudTable × 4） | 91 | pending |
| **93** | Discord notification 改造：HITL 雙向 → 純被動 push | 91 | pending |
| **94** | 端到端驗證：寫 `agents/petra-pm.md`、本機跑 `claude --agent petra-pm`、natural language spawn teammate、驗 MCP tool call 寫入 AiTeam | 92、93 | pending |
| **95** | main 切換：merge `v4-rewrite` → main、CI/CD 部署、舊資料處置、SemVer v4.0.0、文件全面更新 | 94 | pending |

---

## 新架構圖

```
┌──────────────────────────────────────────────────────────────┐
│ Christ 本機 (Windows 11)                                       │
│                                                                │
│  ┌───────────────────────────────────────────────────────┐    │
│  │ Claude Code v2.1.32+ (CLAUDE_CODE_EXPERIMENTAL_AGENT_  │    │
│  │ TEAMS=1)                                                │    │
│  │                                                          │    │
│  │  $ claude --agent petra-pm                              │    │
│  │                                                          │    │
│  │  ┌──────────────┐    ┌──────────────┐  ┌────────────┐  │    │
│  │  │ Petra (Lead) │ ─→ │ teammate-1   │  │ teammate-N │  │    │
│  │  │ Opus 4.7     │    │ Sonnet 4.6   │  │ Sonnet 4.6 │  │    │
│  │  └──────────────┘    └──────────────┘  └────────────┘  │    │
│  │         │                   │                  │         │    │
│  │         └───────────────────┴──────────────────┘         │    │
│  │                          │                                │    │
│  │                  MCP tool call (HTTP)                     │    │
│  └──────────────────────────│──────────────────────────────┘    │
└─────────────────────────────│──────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────┐
│ Docker Compose (Windows 11 本機)                              │
│                                                                │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ AiTeam.Bot (純記錄 MCP server)                        │    │
│  │  ├─ MCP server endpoint (HTTP, API key auth)         │    │
│  │  │   ├─ register_team                                 │    │
│  │  │   ├─ record_task                                   │    │
│  │  │   ├─ record_message                                │    │
│  │  │   └─ record_token_usage                            │    │
│  │  ├─ Discord notification (純被動 push)                │    │
│  │  └─ DB layer + EF Migration                          │    │
│  └──────────────────────────────────────────────────────┘    │
│                              │                                │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ PostgreSQL                                            │    │
│  │  ├─ teams / teammates / tasks / messages / token_usage│   │
│  └──────────────────────────────────────────────────────┘    │
│                              │                                │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ AiTeam.Dashboard (Blazor / MudBlazor 8.x)            │    │
│  │  └─ Records 表格頁 (minimal MudTable × 4)             │    │
│  └──────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────┘
```

---

## P0 風險點

| 風險 | 處理時機 | Fallback |
|---|---|---|
| **C# MCP SDK 成熟度** | Stage 88 spike 第一刀 | 沒成熟 SDK → 手寫 HTTP/SSE endpoint 符 MCP spec |
| **Claude Code remote MCP 相容性** | Stage 88 同時驗 | 確認 Claude Code 連 HTTP MCP server、不只支援 stdio。失敗則 phase abort |

---

## 延後拍板（執行中自決）

- **SemVer**：建議 v4.0.0、Stage 95 拍板
- **舊資料處置**：migrate vs drop / Stage 95 自決 + 記 Execution Log
- **Discord 觸發事件 minimal 集合**：Stage 93 自決
- **Dashboard 表格欄位設計**：Stage 92 自決
- **改造期間 Discord 通知**：先全關（避免亂發）、Stage 93 改造好再開

---

## 執行紀律（大授權版）

| 紀律 | 說明 |
|---|---|
| **記錄** | 所有問題與決策寫入 `Phase_v4_Execution_Log.md` |
| **commit/push** | 每 Stage 完成自動 commit + push（CI/CD 自動部署）|
| **escalate** | 技術選型完全卡死 / scope 失控 / Christ 個人偏好相關時停下 |
| **自決** | 實作細節、小卡住、build 失敗修、Migration 細節、自驗 fail 找原因 |
| **subagent 用法** | 大量 spawn cody（單檔實作）/ Explore（codebase search）保 main session lean |
| **Aria-Forge 模式** | 廢止、Petra 全能模式（既規劃又執行 / 不再分工 spawn Forge）|

---

## 結束條件

- Stage 95 完成（merge main + CI/CD 部署 + 文件更新）
- Christ 本機可以跑 `claude --agent petra-pm` 全鏈路通到 AiTeam DB
- Dashboard 看得到記錄
- Discord 收到通知

結束後 Petra 寫一份 Phase 結案報告附在 Execution Log 末段、回報 Christ。
