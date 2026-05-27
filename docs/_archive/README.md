# docs/_archive — 歷史文件歸檔

本資料夾收納**已不再活躍但具歷史脈絡價值**的文件。歸檔而非刪除的理由：

- 早期設計脈絡仍可幫助理解「為什麼現在這樣」
- 部分構想（如 Personal Team / Grand CEO）未來可能重啟
- CHANGELOG / 活檔對歸檔文件有 cross-link reference
- git history archaeology 不如 grep markdown 快 50 倍

> **重要**：歸檔文件**不再維護**。若需要某項資訊的最新狀態、請查詢對應活躍文件（`Architecture.md` / `planning/Phase_v4_*.md` / 程式碼）。

---

## 子資料夾（7 個 / 依歸檔時間倒序）

### `stages-pre-v4/` — v3-v5 + v4-rewrite 前 Stage Roadmap（89 檔）

> 歸檔日期：2026-05-27（v4.1.1 全盤 housekeeping）
> 原位置：`docs/planning/Stage_*.md`

Stage 7 ~ Stage 87 規劃書（含 v3-v5 演進全程 + v4-rewrite 前最後一輪 Stage 87 Talent token limits + 模板 `_template/Stage_Template.md`）。v4-rewrite Stage 88-95 細節在 [`planning/Phase_v4_Roadmap.md`](../planning/Phase_v4_Roadmap.md) + [`Phase_v4_Execution_Log.md`](../planning/Phase_v4_Execution_Log.md) / 不在本 archive。

歷史價值：refactor-sop.md 引用「Stage 34-36 / 59 / 84-87 SOP 累積」可 trace 回此 archive 看實際 case study。

### `experiments-pre-v4/` — v5 時代 self-implement 試驗（26 檔）

> 歸檔日期：2026-05-27（v4.1.1 全盤 housekeeping）
> 原位置：`docs/experiments/Trial_*.md` + `Spike_v1_*.md`

Trial v2 ~ Trial v27（含 v15_v16 合一檔）+ Spike v1 MsAgentFramework。v5 時代 Christ 對 AiTeam 系統做 self-implement 試驗的觀察紀錄、是 Future_Feature.md 條目觸發前的真實流程觀察證據。

v4-rewrite 後執行模式換成 Claude Code Agent Team / 不再做 self-implement 試驗。

### `agents-v4/software-team-v4/` — v4 hierarchical Agent 個別設計檔（12 檔）

> 歸檔日期：2026-05-21
> 原位置：`docs/agents/software team/`

v4 hierarchical static 時期 12 個 Agent 個別設計檔（CEO/PM/Dev/Reviewer/QA/Doc/Designer/Requirements/Release/Ops/Software_Team_Plan/Agent_Capability_Gaps）。

**Stage 78a 後砍範圍**：Rosa（Requirements）/ Demi（UI Design）/ Rena（Release Publishing）對應 capability 砍 + Maya（Ops）未實作。**v5.5 真實 baseline 只 6 Talent**（Victoria/Petra/Cody/Vera/Quinn/Sage / 2026-05-26 v4-rewrite 後全砍 / 只剩 Petra + cody）。

歷史價值：未來如要重啟某 Agent 角色（如 Ops 自動 ALERT / Reporter 自動週報）可參考此 archive 設計原點。

### `agents-future/` — 未來才需要的 Agent 構想

> 歸檔日期：2026-04-25

| 檔 | 觸發條件 |
|---|---|
| `CEO_Grand.md`（Iris 總執行長）| 第二個 Team 出現時 |

Software Team 一直是唯一 Team / v4-rewrite 後 Christ 自己 + Petra 即足 / 此構想擱置。

### `personal-team/` — Personal Team 構想（暫緩）

> 歸檔日期：2026-04-25
> 原位置：`docs/agents/personal team/`

Christ 早期規劃的個人 AI 管家團隊（Personal CEO `Nora` / Secretary `Seki` / Home `Hana` / Tracker `Tara` / Research `Rhea`）。Stage 7 規劃時決議**暫緩**（聚焦 Software Team）/ v4-rewrite 後系統定位改為 Claude Code Agent Team 純記錄 / 此構想方向需重評。

未來若要重啟、這些文件是設計原點。

### `early-stages/` — 最早期 Stage Roadmap（6 檔）

> 歸檔日期：2026-04-25 之前
> 原位置：`docs/planning/Stage_1-6_*.md`

Stage 1 ~ Stage 6 規劃書（v3 初創期 / Foundation / Agents / Dashboard / Expansion / Roadmap）。歷史價值：看 AiTeam 怎麼從零開始的最早設計拍板。

### `early-vision/` — 最初願景設計（1 檔）

> 歸檔日期：2026-04-25 之前
> 原位置：`docs/architecture/`

`01_Vision_and_Architecture.md` — v3 之前的最初願景文件。content 散到當前 [`CLAUDE.md`](../../CLAUDE.md) + [`README.md`](../../README.md) + [`Architecture.md`](../Architecture.md) / 但完整原文保留於此。

---

## 取消歸檔（如需要）

若任一文件需重新啟用 / 使用 `git mv` 搬回原位置即可：

```bash
git mv docs/_archive/<subfolder>/<file>.md docs/<original-location>/
```

並更新 [`docs/README.md`](../README.md) 對應段落。
