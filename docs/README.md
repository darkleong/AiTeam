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

### `Architecture.md` — v5.5 系統架構全景

`docs/Architecture.md` — 系統架構雙層 / v5.5 三層分工（Petra LLM API + Worker CLI + Christ HITL）/ Workflow Feature Flag（22 個）/ Talent + Skill 分離 / 完整流程 — Christ 派指令 → PR / HITL plan_confirm + replan_confirm / Petra 動態 SubtaskPlan / Worker Claude Code CLI subprocess / Crash Recovery + 佇列 / Token 計費 / 關鍵程式碼位置索引。

> 早期 `architecture/` 子資料夾已砍（2026-05-22）— `00_Master_Plan.md` 跟本 README 重疊砍 / `About_Boss.md` 合進 `memory/user_christ.md`（user-local）/ `01_Vision_and_Architecture.md` 歸檔至 [`_archive/early-vision/`](./_archive/early-vision/) / `02_Infrastructure.md` 內容散到 [CLAUDE.md](../CLAUDE.md) + [README.md](../README.md)。

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

> 早期 `agents/` 子資料夾已砍（2026-05-22）— `v5.5_team_plan.md` 內容合進 [`Architecture.md`](Architecture.md)「6 Talent baseline」段 / v4 hierarchical static 12 個 Agent 個別設計檔歸檔至 [`_archive/agents-v4/`](_archive/agents-v4/)。執行細節（行為、工具權限、輸出格式）以 `src/AiTeam.Bot/Resources/CLAUDE_*.md` 為準。

---

### `experiments/` — Self-implement 試驗紀錄

老闆 + Aria 對 AiTeam 系統做 self-implement 試驗的觀察紀錄（`Trial_vN_*.md`）。每次試驗驗證一個假設，是 FF 條目觸發前的真實流程觀察證據。

---

### `_archive/` — 歷史文件歸檔（不再維護）

收納早期設計、暫緩構想、停產自動產出。詳見 [`_archive/README.md`](./_archive/README.md)。

> **`generated/`（Doc Agent 自動產出）與 `ui-specs/`（Demi UI 規格）兩資料夾**：2026-04-25 歸檔 → 2026-05-22 徹底刪除（純歷史快照 / 0 cross-link reference / git history 可查）。前者因 Sage 轉型歸檔員停產，後者因 Stage 12 改 UI 規格存 DB（FF 十七）。
