# docs — 文件總覽

本資料夾包含 AiTeam 專案的所有規劃、架構與開發規範文件。

---

## 入口

**想了解整個專案的版本歷史 / 最新狀態？**
→ [`/CHANGELOG.md`](../CHANGELOG.md)（root 層 / 完整版本變更紀錄）

**想看系統架構（v4.1.1）？**
→ [`Architecture.md`](./Architecture.md)

**想看某個 Phase / Stage 的詳細實作？**
→ [`planning/Phase_v4_*.md`](./planning/) — v4-rewrite Phase（Stage 88-95）
→ [`planning/Stage_{N}_Roadmap.md`](./planning/) — v3-v5 + v4 Stage 7-87

---

## 資料夾說明

### `Architecture.md` — v4 系統架構全景

對齊 v4.0.0 後純記錄系統：執行端 = Christ 本機 Claude Code Agent Team（Petra lead + cody worker）/ 記錄端 = AiTeam Bot MCP server endpoint + PostgreSQL + Blazor Dashboard。詳述 MCP 8 個 tool 流程、DB schema（`mcp_*` 5 表）、Discord notification 4 觸發點、關鍵程式碼位置索引。

> **2026-05-22 早期整理**：`architecture/` 子資料夾砍 — `00_Master_Plan.md` 跟 root README 重疊砍 / `About_Boss.md` 合進 `memory/user_christ.md`（user-local）/ `01_Vision_and_Architecture.md` 歸檔至 [`_archive/early-vision/`](./_archive/early-vision/) / `02_Infrastructure.md` 內容散到 [CLAUDE.md](../CLAUDE.md) + [README.md](../README.md)。早期 `agents/` 子資料夾砍 — v5.5 team plan 內容合進 Architecture.md / v4 hierarchical 12 個 Agent 個別設計檔歸檔至 [`_archive/agents-v4/`](./_archive/agents-v4/)。
>
> **2026-05-26 v4-rewrite**：Stage 88-95 砍 v5.5 6 Talent + Aria-Forge 工作模式 / Architecture.md 整篇重寫對齊「純記錄系統」/ 6 Talent baseline 段全砍。

---

### `planning/` — 開發規劃（動態，持續更新）

| 文件 | 說明 |
|---|---|
| `Phase_v4_Roadmap.md` | v4-rewrite Phase 規劃書（Stage 88-95）|
| `Phase_v4_Execution_Log.md` | v4-rewrite 執行紀錄（含每 Stage 自決紀錄）|
| `Phase_v4_Stage94_E2E_Guide.md` | 端到端驗證指南（Christ 本機跑）|
| `Phase_v4_Followup.md` | v4 結案後 follow-up 候選清單 |
| `Future_Feature.md` | active 功能候選 + 客戶交付規劃 |
| ~~`Stage_7_Roadmap.md` ~ `Stage_87_Roadmap.md`~~ | v3-v5 + v4-rewrite 前 Stage 規劃書 / 2026-05-27 v4.1.1 全盤 housekeeping 搬至 [`_archive/stages-pre-v4/`](./_archive/stages-pre-v4/)（早期 Stage 1-6 在 [`_archive/early-stages/`](./_archive/early-stages/)）|

---

### `conventions/` — 編程規範（穩定，必讀）

實作前必須閱讀的技術規範。反映專案累積的最佳實踐（v3-v5 時代由 Aria 維護 / v4-rewrite 後 Petra 接手 / Aria 角色已併入 Petra）。

| 文件 | 說明 |
|---|---|
| `csharp.md` | C# 命名、結構、非同步規範 |
| `blazor.md` | Blazor 組件規範、生命週期、通信 |
| `mudblazor.md` | MudBlazor 8.x 使用規範、常見陷阱 |
| `ef-core.md` | EF Core 查詢優化、Repository 模式、Migration 流程 |
| `api-design.md` | RESTful API、Internal API、SignalR Hub 設計規範 |
| `refactor-sop.md` | 服務層大檔案拆解守則（Stage 34-36 + 59 + 84-87 SOP 累積）|

---

### ~~`experiments/`~~ — Self-implement 試驗紀錄（已歸檔）

v5 時代 Christ 對 AiTeam 系統做 self-implement 試驗的觀察紀錄（`Trial_vN_*.md` × 26 + `Spike_v1_*.md`）。每次試驗驗證一個假設、是 Future_Feature 條目觸發前的真實流程觀察證據。

v4-rewrite 後執行模式換成 Claude Code Agent Team / 不再做 self-implement / 2026-05-27 v4.1.1 全盤 housekeeping 整個資料夾搬至 [`_archive/experiments-pre-v4/`](./_archive/experiments-pre-v4/)。

---

### `_archive/` — 歷史文件歸檔（不再維護）

收納早期設計、暫緩構想、停產自動產出。詳見 [`_archive/README.md`](./_archive/README.md)。

> **`generated/`（Doc Agent 自動產出）與 `ui-specs/`（v4 時代 UI 規格）兩資料夾**：2026-04-25 歸檔 → 2026-05-22 徹底刪除（純歷史快照 / 0 cross-link reference / git history 可查）。前者因 Doc Agent 轉型歸檔員停產、後者因 Stage 12 改 UI 規格存 DB。
