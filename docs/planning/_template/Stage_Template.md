# Stage 範本（極簡骨架）

> 本範本為 `/aria-plan` skill 唯一範本 SoT / 對齊「對抗 LLM 文檔 inflation + 範本繼承漂移」精神。
> Aria 寫新 Stage Roadmap 時：**只 Read 本範本** / **不 Read 既有 Stage_XX_Roadmap.md** / 避免具體案例寫法繼承累積。

---

## 計劃書硬紀律 10 條（寫前必對齊）

1. **驗收章節獨立一節 `## 驗收情境`**（不分散到子項內）
2. **驗收情境具體到「怎麼觸發 + 怎麼驗證」**（不寫「X 恢復正常」這種模糊）
3. **不寫工時** — AiTeam 沒「天」概念 / 用「子項規模 XS/S/M/L/XL」相對複雜度
4. **不寫整段 code 範例** — code block > 10 行砍 / 用「修法策略 + 對齊 X 既有 pattern（檔案:行號）」
5. **大檔 reference 標精準 line + method 簽名**（> 800 行警戒線 / Forge partial read）
6. **環境細節標 source of truth 不憑印象** — port / env / DB schema / API endpoint / container 名
7. **規劃含 entity / schema / route / endpoint / framework version / DI lifecycle / DB row 既有狀態 reference 必先 grep / SQL verify**
8. **計劃前置 WebSearch** — third-party framework / library / 不確定 API best practice
9. **不寫 cost 欄位 / 預設標「Stage 期間 0 燒 AiTeam 餘額」**
10. **不寫 Forge context 估算 / 倍率 calibration anchor / Opus 1M / Sonnet 200K 規格參數** — Aria 自己 planning 用紀錄在自己 head / 不污染 Forge 看的文件

## 對抗漂移紀律 3 條

- **不繼承既有 Stage Roadmap 寫法** — 此範本是唯一 SoT / 不 Read Stage_XX_Roadmap.md 當參考
- **每段字數建議上限**：戰略脈絡 ~200 字 / 子項描述 ~50 字/子項 / 設計決策表 ≤ 12 條 / 驗收情境每條 ~30 字 / 風險表 ≤ 8 條
- **階段性砍** — 每 5 Stage 後 audit 本範本是否 cascade inflated / 砍累積冗餘段

---

# Stage XX Roadmap — 標題（一行）

> **狀態：📋 規劃中**
> **文件版本：v1.0**
> 對應系統版本：vX.Y.Z（Stage XX 完成後 / minor bump 對齊 Stage 完成 / major 對齊架構重大改變）
> Stage 規模：**XS / S / M / M+ / L / L+ / L++ / L+++**（一行說明：影響範圍 / 0 燒 AiTeam 餘額 OR 預估燒額）
> 觸發來源：FF 候選 #X / Trial_vN 揭議題 / Christ 拍板新方向 / 既有 Stage 後續
> 戰略意義：一行說明為什麼這個 Stage 重要

---

## 戰略脈絡

為什麼動這個 Stage（~200 字 / 不超過）：問題現況 / 對齊精神 / 跟既有架構關係。不複述 docs/Architecture.md / 用 link。

---

## 子項清單

### 子項 0：標題（規模 XS/S/M/L）

一段描述（~50 字）+ 關鍵 method / 檔案 / line 範圍。

### 子項 1：標題（規模）

同上格式。依需要 N 個子項。

---

## 設計決策

| # | 決策 | 理由 |
|---|---|---|
| 1 | 一行決策 | 一行理由（不超過 30 字） |

≤ 12 條 / 超 12 條代表決策太多 / 重新分 Stage 或砍。

---

## 驗收情境

每條格式：「觸發：... / 驗證：...」/ 具體可執行。

1. **驗收項標題** — 觸發：具體動作 / 驗證：SQL query 或 UI 觀察或 build/test 命令

≤ 20 條 / 超 20 條代表 scope 太大 / 重新分 Stage。

---

## 技術約束

bullet list / 框架版本 / DI lifecycle / 不引入新依賴 / 對齊既有 convention（檔案:line）。

---

## 風險與緩解

| 風險 | 緩解 |
|---|---|
| 一行風險 | 一行緩解 |

≤ 8 條 / 超 8 條代表設計不確定性太高 / 先 spike 再規劃。

---

## 版本歷史

### v1.0 — YYYY-MM-DD（Aria 建立）

- 觸發：Christ 拍板 / FF 候選 / Trial 揭議題
- 範圍：一行
- 6 維度 ultrathink 自審：① 架構 ✅/⚠️ ② 邏輯 ③ 競態 ④ 上下文 ⑤ 預留欄位 ⑥ 關鍵檔案

### v2.0 — YYYY-MM-DD（Forge 結案 / 結案後補）

實作紀錄段（v1.0 不寫 / Forge 結案時補）：總覽 + 實作項目 + 關鍵設計決策 + 驗收後修正輪次 + 踩坑紀錄 + Mock 覆蓋情況。
