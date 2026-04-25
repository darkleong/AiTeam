# docs — 文件總覽

本資料夾包含 AiTeam 專案的所有規劃、架構與開發規範文件。

---

## 入口

**想了解整個專案的全貌或查詢某個 Stage？**
→ [`architecture/00_Master_Plan.md`](./architecture/00_Master_Plan.md)

---

## 資料夾說明

### `architecture/` — 系統架構（靜態，少變動）

描述系統的願景、設計原則與基礎建設。這些是整個專案的底層共識，不隨 Stage 推進而頻繁修改。

| 文件 | 說明 |
|------|------|
| `00_Master_Plan.md` | 總索引：所有 Stage 狀態一覽、變更紀錄 |
| `01_Vision_and_Architecture.md` | 願景、核心設計原則、整體架構、Agent 定義 |
| `02_Infrastructure.md` | Discord 頻道、資料儲存、基礎建設細節 |
| `About_Christ.md` | 老闆背景與工作風格（AI 團隊行為依據） |

---

### `planning/` — 開發規劃（動態，持續更新）

每個 Stage 的目標、實作項目與結案記錄。開發進行中時變動最頻繁。

| 文件 | 說明 |
|------|------|
| `Stage_1_Design.md` ~ `Stage_N_Roadmap.md` | 各 Stage 規劃書（依序遞增） |
| `Future_Feature.md` | 尚未排入 Stage 的功能候選清單 |

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

---

### `agents/` — Agent 文件（依需查閱）

各 AI Agent 的能力說明、行為設定與協作架構記錄。執行細節（行為、工具權限、輸出格式）以 `src/AiTeam.Bot/Resources/CLAUDE_*.md` 為準，本資料夾為角色 lore + 設計脈絡。

---

### `experiments/` — Self-implement 試驗紀錄

老闆 + Aria 對 AiTeam 系統做 self-implement 試驗的觀察紀錄（`Trial_vN_*.md`）。每次試驗驗證一個假設，是 FF 條目觸發前的真實流程觀察證據。

---

### `_archive/` — 歷史文件歸檔（不再維護）

收納早期設計、暫緩構想、停產自動產出。詳見 [`_archive/README.md`](./_archive/README.md)。

> **`generated/`（Doc Agent 自動產出）與 `ui-specs/`（Demi UI 規格）兩資料夾已於 2026-04-25 整體歸檔**——前者因 Sage 轉型歸檔員停產，後者因 Stage 12 改 UI 規格存 DB（FF 十七）。
