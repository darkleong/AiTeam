# docs/_archive — 歷史文件歸檔

本資料夾收納**已不再活躍但具歷史脈絡價值**的文件。歸檔而非刪除的理由：

- 早期設計脈絡仍可幫助理解「為什麼現在這樣」
- 部分構想（如 Personal Team / Grand CEO）未來可能重啟
- CHANGELOG / 活檔對歸檔文件有 cross-link reference

> **重要**：歸檔文件**不再維護**。若需要某項資訊的最新狀態，請查詢對應的活躍文件（程式碼 / `architecture/` / `planning/`）。

---

## 子資料夾

### `personal-team/` — Personal Team 構想（暫緩）

> 歸檔日期：2026-04-25
> 原位置：`docs/agents/personal team/`

Christ 早期規劃的個人 AI 管家團隊（Personal CEO `Nora` / Secretary `Seki` / Home `Hana` / Tracker `Tara` / Research `Rhea`）。Stage 7 規劃時決議**暫緩**（聚焦 Software Team），目前無實作計劃。

未來若要重啟，這些文件是設計原點。

### `agents-future/` — 未來才需要的 Agent 構想

> 歸檔日期：2026-04-25

| 檔案 | 觸發條件 |
|---|---|
| `CEO_Grand.md`（Iris 總執行長） | 第二個 Team 出現時 |

Software Team 一直是唯一 Team，目前 Victoria 兼任此角色。

### `agents-v4/software-team-v4/` — v4 Agent 個別設計檔（已過時）

> 歸檔日期：2026-05-21
> 原位置：`docs/agents/software team/`

v4 hierarchical static 時期 12 個 Agent 個別設計檔（CEO/PM/Dev/Reviewer/QA/Doc/Designer/Requirements/Release/Ops/Software_Team_Plan/Agent_Capability_Gaps）。

**Stage 78a 後砍範圍**：Rosa（Requirements）/ Demi（UI Design）/ Rena（Release Publishing）對應 capability 砍 + Maya（Ops）未實作。**v5.5 真實 baseline 只 6 Talent**（Victoria/Petra/Cody/Vera/Quinn/Sage），詳見活檔 [`docs/agents/v5.5_team_plan.md`](../agents/v5.5_team_plan.md)。

歷史價值保留 — Christ 未來如要重啟某 Agent 角色（如 Ops 自動 ALERT / Reporter 自動週報）可參考此 archive 設計原點。

---

## 取消歸檔（如需要）

若任一文件需重新啟用，使用 `git mv` 搬回原位置即可：

```bash
git mv docs/_archive/<subfolder>/<file>.md docs/<original-location>/
```

並更新 `docs/README.md` 對應段落。
