# docs — 文件總覽

本資料夾包含 AiTeam 專案的所有規劃、架構與開發規範文件。

---

## 入口

**想了解整個專案的版本歷史 / 最新狀態？**
→ [`/CHANGELOG.md`](../CHANGELOG.md)（root 層，完整版本變更紀錄）

**想找某個特定 Stage 的詳細實作？**
→ [`planning/Stage_{N}_Roadmap.md`](./planning/)

---

## 資料夾說明

### `architecture/` — 系統架構（靜態，少變動）

描述系統的願景、設計原則與基礎建設。這些是整個專案的底層共識，不隨 Stage 推進而頻繁修改。

| 文件 | 說明 |
|------|------|
| `00_Master_Plan.md` | 文件導覽 stub（變更紀錄已遷至 [`/CHANGELOG.md`](../CHANGELOG.md)） |
| `03_Workflow_Overview.md` | 開發流程全景圖（v5.5 三層分工 / HITL / Petra 動態 orchestrator） |

> 早期 `01_Vision_and_Architecture.md` 已歸檔至 [`_archive/early-vision/`](./_archive/early-vision/)（保留設計初期願景紀錄）；`02_Infrastructure.md` 已刪除（內容散到 [CLAUDE.md](../CLAUDE.md) + [README.md](../README.md)）。

---

### `planning/` — 開發規劃（動態，持續更新）

每個 Stage 的目標、實作項目與結案記錄。開發進行中時變動最頻繁。

| 文件 | 說明 |
|------|------|
| `Stage_7_Roadmap.md` ~ `Stage_N_Roadmap.md` | 各 Stage 規劃書（早期 Stage 1-6 已歸檔到 [`_archive/early-stages/`](./_archive/early-stages/)） |
| `Future_Feature.md` | active 功能候選清單 |
| `Future_Feature_v5.5.md` ⭐ | v5.5 升級規劃 reference（進行中戰略主軸）|

---

### `conventions/` — 編程規範（穩定，必讀）

實作前必須閱讀的技術規範。由 Aria（架構顧問）維護，反映專案累積的最佳實踐。

| 文件 | 說明 |
|------|------|
| `csharp.md` | C# 命名、結構、非同步規範 |
| `blazor.md` | Blazor 組件規範、生命週期、通信 |
| `mudblazor.md` | MudBlazor 8.x 使用規範、常見陷阱 |
| `ef-core.md` | EF Core 查詢優化、Repository 模式 |
| `api-design.md` | RESTful API 設計規範 |
| `refactor-sop.md` | 服務層大檔案拆解守則（FF 二十實踐累積）|

---

### `agents/` — Agent 文件（依需查閱）

各 AI Agent 的能力說明、行為設定與協作架構記錄。**v5.5 baseline 主檔**：[`agents/v5.5_team_plan.md`](agents/v5.5_team_plan.md)（6 Talent 清單 + Talent-Skill separation + Petra 動態 orchestrator + HITL 兜底 / 對應 v3.75.0 Stage 83 後狀態 / 2026-05-21 大整理）。

v4 hierarchical static 時期 12 個 Agent 個別設計檔已歸檔至 [`_archive/agents-v4/`](_archive/agents-v4/)。執行細節（行為、工具權限、輸出格式）以 `src/AiTeam.Bot/Resources/CLAUDE_*.md` 為準。

---

### `experiments/` — Self-implement 試驗紀錄

老闆 + Aria 對 AiTeam 系統做 self-implement 試驗的觀察紀錄（`Trial_vN_*.md`）。每次試驗驗證一個假設，是 FF 條目觸發前的真實流程觀察證據。

---

### `_archive/` — 歷史文件歸檔（不再維護）

收納早期設計、暫緩構想、停產自動產出。詳見 [`_archive/README.md`](./_archive/README.md)。

> **`generated/`（Doc Agent 自動產出）與 `ui-specs/`（Demi UI 規格）兩資料夾**：2026-04-25 歸檔 → 2026-05-22 徹底刪除（純歷史快照 / 0 cross-link reference / git history 可查）。前者因 Sage 轉型歸檔員停產，後者因 Stage 12 改 UI 規格存 DB（FF 十七）。
