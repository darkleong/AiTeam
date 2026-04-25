# docs/_archive — 歷史文件歸檔

本資料夾收納**已不再活躍但具歷史脈絡價值**的文件。歸檔而非刪除的理由：

- 早期設計脈絡仍可幫助理解「為什麼現在這樣」
- 部分構想（如 Personal Team / Grand CEO）未來可能重啟
- Doc Agent 早期產出的 class doc 可作為 Sage 角色演進的參照

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

### `generated-early/` — Doc Agent 早期自動產出

> 歸檔日期：2026-04-25
> 原位置：`docs/generated/`

Stage 5-13 期間 Sage（Doc Agent）試產的 class doc / PR doc。Stage 14 起 Sage 從「技術文件撰寫員」轉型為「歸檔員」（FF v5.6 設計變更），這類自動產出文件已停產。

程式碼本身是最權威的 SoT；這些 snapshot 已過時。

### `ui-specs-early/` — Demi 早期 UI 規格產出

> 歸檔日期：2026-04-25
> 原位置：`docs/ui-specs/`

Stage 7-12 期間 Demi（Designer Agent）為 Dev 產出的 UI 規格 Markdown。**Stage 12 起 UI 規格改存 DB**（FF 十七 ✅ 已完成），檔案輸出機制已淘汰。

含多份 Reviewer 報告統計行的重複版（早期試驗殘留）。

---

## 取消歸檔（如需要）

若任一文件需重新啟用，使用 `git mv` 搬回原位置即可：

```bash
git mv docs/_archive/<subfolder>/<file>.md docs/<original-location>/
```

並更新 `docs/README.md` 對應段落。
