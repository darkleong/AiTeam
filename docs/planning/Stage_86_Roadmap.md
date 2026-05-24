# Stage 86 Roadmap — Dashboard 改造（sidebar + 視覺/UX 一致性 + Theme 機制 + 附檔預覽 + Monitoring + Rules 清理）

> **狀態：📋 規劃中**
> **文件版本：v1.0**
> 對應系統版本：v3.78.0（Stage 86 完成後 / minor bump）
> Stage 規模：**L**（6 子項 catch-all / 純前端 UI/UX 改造為主 + 1 後端 query 改 + 1 dead config 清理 / 0 Migration / 0 燒 AiTeam 餘額 — 純 C# + Dashboard UI / 0 LLM call）
> 觸發來源：2026-05-24 Christ Dashboard 痛點盤點 11 條拆 3 Stage 分組 B 折衷版 / Stage 86 是「改造」性質（讓 UI 變更好 / risk profile 跟 Stage 85「救火」分開）+ 額外加 Rules dead config 清理（chat 內議題 / 同 v4 殘留根因 / 性質併入改造）
> 戰略意義：Dashboard 進入 production 自然累積期前的視覺 + UX 統一改造 / 為 AiTeam dispatch dogfood 後續真實 fire 提供更好操作體驗

---

## 戰略脈絡

Stage 85 把救火做完（修壞掉的 + 系統 alert 補完），但 Christ 真實使用 Dashboard 累積的「UX 不順 + 視覺不一致 + dead config 殘留」沒處理。本 Stage 主題是改造，risk profile 跟救火不同：

- 救火：trade-off 容忍低（修錯就壞）
- 改造：trade-off 容忍高（試錯可以）

6 子項共同根因兩條：
1. **Dashboard 缺 UI 一致性紀律**（各分頁分散時代設計累積 / 視覺、表格、卡片排版各自為政）
2. **v4 角色體系殘留持續 dead config**（Stage 85 cover v4 dead flag / 本 Stage cover Rules 套用對象）

對齊 Stage 85 累積的「v4 dead reference 全 Dashboard 範圍清理」紀律延伸到「v5.5 視覺 + UX 紀律全 Dashboard 範圍對齊」。

---

## 子項清單

### 子項 0：Rules dead config 清理（A 路線）— 規模 XS

UI 移除「Dev / Doc / Ops」套用對象選項，只留「全域」。對齊 Stage 85 已 verify「RulesService.GetRulesAsync() 只有 CEO path 用，Dev/Doc/Ops 套用對象 0 業務 caller」（同 v4 殘留根因 / 同 Stage 84/85 處理 pattern）。

DB row 不刪變孤兒（對齊 0 Migration 紀律）/ 既有 v4 套用對象 row UI 顯示為「全域」或隱藏（Forge plan mode 拍板）。

### 子項 1：全 Dashboard 砍版本/Stage 編號文字 — 規模 XS-S

grep 全 Dashboard 描述文字找「v4」「v5」「v5.5」「Stage 63B」「Stage 80」「Stage 81」「Stage 84+」等開發歷史 reference，改純功能描述。對齊紀律：開發者 SoT（docs/Architecture.md / refactor-sop.md）可保留 reference，**使用者文檔（Dashboard UI / README.md / CLAUDE.md）砍**。

範圍：SettingsHub 各 tab flag 描述、SystemSettings tab description、Monitoring tab tooltip 等。

### 子項 2：Dashboard 視覺/UX 一致性 catch-all — 規模 M

7 項小修打包同子項（catch-all 性質 / 各項獨立但風險同類）：

- 設定中心數值欄位靠右對齊（11 個 flag input）
- TALENTS vs WORKFLOW FLAGS switch 樣式統一
- TALENTS 表 Petra/Victoria Skills column 標「N/A（dispatcher 或 CEO 角色）」明示設計意圖
- SKILLPROMPTS vs TALENTPROMPTS 表格 column 統一（5 欄詳細 / Name + Active Version + Length + UpdatedAt + 動作）
- 首頁 4 張卡片紅字描述移 MudTooltip（卡片只顯示 icon + 標題 + 數字）
- TOKEN 統計下拉砍括號描述（per-Talent / per-PetraSession）
- 全 Dashboard 表格 `FixedHeader="true"` + `Height="calc(100vh - Npx)"` 內置 scrollbar（不用瀏覽器整頁 scroll）

### 子項 3：PetraInbox 附檔預覽 — 規模 S-M

點擊「📎 N 張」開 MudDialog 顯示附檔圖片。Forge Plan Mode 必先 grep + read 既有 `PetraInbox.Attachments` jsonb 真實結構（base64 string 還是 file path / Stage 79 image flow 實作細節 verify）。

如 file path → 可能需新 Internal API endpoint `/internal/attachments/{inboxId}/{filename}` 給 Dashboard 跨 container 讀圖（對齊既有 Internal API X-Api-Key 機制）。

### 子項 4：Monitoring 中心改善 catch-all — 規模 M

TOKEN 統計加時間範圍 filter + 警戒線 + per-Skill 維度（對齊 FF Stage 86+ 既有候選 #6）：

- 時間範圍 selector（MudButtonGroup 或 MudSelect）6 選項：今天 / 這一週 / 最近 7 天 / 這個月 / 最近 30 天 / 全部
- 後端 token_logs 表 query 加 `WHERE CreatedAt >= ...` filter
- MudChart 警戒線 ReferenceLine（全域月限 80% / per-agent 日限 90%）
- per-Skill 維度 chart 切換（既有 per-Talent / per-PetraSession 之外多一維度）

### 子項 5：Sidebar 改造 — 規模 M

砍頂部 MudAppBar，重組 MudDrawer state machine：

- AI Team logo 從 AppBar 移到 Drawer 頂部（展開時顯示）
- Sidebar 收合時 hamburger 按鈕半透明浮左上角
- 滑鼠 hover hamburger → MudDrawer `Variant.Temporary` overlay 模式畫出（不擠壓內容）
- 點擊 hamburger → MudDrawer `Variant.Persistent` pin 模式展開（擠壓內容）
- 兩態切換 state machine（hover / click）+ CSS transition

NavMenu 既有 4 條 link（首頁 / 任務中心 / 設定中心 / 監控中心）結構不動 / 只動 MainLayout 包裝。

### 子項 6：Theme 機制根因修 + Palette 重設計 — 規模 M

修 Stage 83 v4 Bug 9 揭過的既有 Theme 機制 bug（MudThemeProvider 0 binding default light + dark theme 只影響 wwwroot/css/app.css 變數）。整合 FF Stage 86+ 既有候選 #4 + #10：

- MainLayout setDark 改 C# event + MudProviders `IsDarkMode` binding subscribe（即時切換不需重 load 頁面）
- MudTheme palette 重設計：採用 Christ 喜歡的深灰色當 Dark Mode baseline（從目前 broken state 抓 visual）/ 重新定義 Light Mode palette
- 全 Dashboard 視覺 verify dark mode 真實生效（MudPaper / MudCard 兩 layer 對齊）
- IsDarkMode binding 紀律落地 `docs/conventions/mudblazor.md`（FF Stage 86+ #10）

---

## 設計決策

| # | 決策 | 理由 |
|---|---|---|
| 1 | Rules dead config 走 A 路線（UI 移選項 + 全域留 + DB row 變孤兒） | 對齊 Stage 84/85 v4 dead 處理 pattern / 最務實 / 0 Migration |
| 2 | 砍版本/Stage 編號只對使用者文檔，開發者 SoT 不動 | docs/Architecture.md 是開發者 SoT 編號保留 / Dashboard UI 對使用者不該帶歷史脈絡 |
| 3 | 視覺一致性 7 項打包同子項 | catch-all 性質 / 各項風險同類 / 一次 commit 比拆 7 commit 效率高 |
| 4 | PetraInbox 附檔預覽 Forge Plan Mode 必先 verify Attachments 結構 | base64 vs file path 真實結構影響 UI vs 後端 API 選擇 / 不憑印象推測 |
| 5 | 全套用 MudBlazor 8.x 既有元件（MudDrawer / MudTooltip / MudDialog / MudChart / MudButtonGroup / MudThemeProvider） | 0 新依賴 / 對齊 docs/conventions/mudblazor.md |
| 6 | Sidebar 兩態切換 state machine 用 MudDrawer `Variant.Temporary` + `Variant.Persistent` | 既有 API / 0 自造輪子 / hover/click 切換靠 C# event |
| 7 | NavMenu 4 條 link 結構不動 | 只動 MainLayout 包裝 / 避免 cascade 風險 / Stage 85 沒改 nav 結構 |
| 8 | Theme palette Dark Mode baseline 採用 Christ 喜歡的深灰色 | 從 broken state 抓 visual 但走修根因路線（重新定義 palette / 不是保留 bug） |
| 9 | IsDarkMode binding 紀律落地 docs/conventions/mudblazor.md | 對齊 FF Stage 86+ #10 / 防 Stage 83 同類 bug 復發 |
| 10 | Monitoring 時間範圍 filter UI 用 MudButtonGroup 6 選項預設「今天」 | 切換頻繁 / button group 比 dropdown 順手 |
| 11 | 後端 token_logs query 加 WHERE CreatedAt filter / 0 schema 改 | 純 query 改 / 對齊 Stage 85 既有 IDbContextFactory pattern |
| 12 | 6 子項按風險低到高排序 commit chain | Rules → 版本砍 → 視覺一致性 → 附檔預覽 → Monitoring → Sidebar → Theme |

---

## 驗收情境

### A. Rules dead config 清理

1. **Rules 設定頁套用對象 dropdown 只剩「全域」+ 既有 v4 row UI 不顯示** — 觸發：訪問 RULES + PROJECTS 分頁編輯規則。驗證：套用對象下拉只顯示「全域」（無 Dev / Doc / Ops）/ 既有 v4 row 顯示為「全域」或隱藏 / DB row 不刪變孤兒

### B. 全 Dashboard 砍版本/Stage 編號

2. **Dashboard 各分頁無 v4 / v5 / v5.5 / Stage XX 編號** — 觸發：訪問全分頁 + grep 描述文字。驗證：純功能描述 / 0 開發歷史 reference

### C. 視覺一致性

3. **設定中心數值欄位 + switch + Skills column N/A 標示** — 觸發：訪問 WORKFLOW FLAGS 數值上限段 + TALENTS。驗證：11 數值欄位靠右 / switch 樣式 TALENTS = WORKFLOW FLAGS / Petra+Victoria Skills 標「N/A（dispatcher 或 CEO 角色）」
4. **SKILLPROMPTS + TALENTPROMPTS column 統一 + 首頁卡片 tooltip** — 觸發：訪問兩分頁 + 首頁。驗證：5 欄詳細 column 一致 / 首頁 4 卡片紅字進 MudTooltip hover 顯示
5. **全 Dashboard 表格 FixedHeader + TOKEN 統計下拉砍括號** — 觸發：訪問各 list 分頁 + 監控中心 TOKEN 統計。驗證：表格內置 scrollbar header 不動 / 下拉 `per-Talent` 不再帶「(per-AgentName)」

### D. PetraInbox 附檔預覽

6. **點擊「📎 N 張」開 MudDialog 顯示圖片** — 觸發：PetraInbox 收件分頁點擊有附檔 row 的「📎 1 張」chip。驗證：MudDialog 開啟顯示圖片 / 多附檔可切換

### E. Monitoring 中心改善

7. **TOKEN 統計時間範圍 6 選項生效** — 觸發：點擊 MudButtonGroup 各選項（今天 / 這一週 / 最近 7 天 / 這個月 / 最近 30 天 / 全部）。驗證：chart 資料對應 filter 期間
8. **MudChart 警戒線 ReferenceLine** — 觸發：開 TOKEN 統計 chart。驗證：全域月限 80% + per-agent 日限 90% 紅線顯示
9. **per-Skill 維度 chart 切換** — 觸發：維度下拉切「per-Skill」。驗證：chart 按 SkillName 分組顯示

### F. Sidebar 改造

10. **頂部 menu bar 砍 + AI Team logo 移 sidebar 頂** — 觸發：登入 Dashboard。驗證：頂部無 MudAppBar / sidebar 展開時頂部顯示「AI Team」logo
11. **hamburger 半透明浮左上 + hover overlay 模式** — 觸發：sidebar 收合，滑鼠移到 hamburger。驗證：hamburger 半透明 / hover 觸發 sidebar overlay 畫出（不擠壓中央內容）
12. **點擊 hamburger pin 展開模式** — 觸發：sidebar 收合，點擊 hamburger。驗證：sidebar pin 住展開（擠壓中央內容）/ 再點縮回

### G. Theme 機制 + Palette

13. **Theme 切換即時生效**（C# event）— 觸發：點擊 theme toggle。驗證：MudPaper / MudCard 即時切色 / 不需重 load 頁面
14. **Dark / Light palette 重新定義**（Dark = Christ 喜歡的深灰色 baseline）— 觸發：切換 Dark / Light Mode。驗證：兩 Mode 全 Dashboard 視覺 verify（MudPaper / MudCard / wwwroot/css/app.css 兩 layer 對齊）
15. **IsDarkMode binding 紀律落地** — 觸發：`cat docs/conventions/mudblazor.md`。驗證：新增 IsDarkMode binding 紀律段（防 Stage 83 同類 bug 復發）

---

## 技術約束

- MudBlazor 8.x（既有 MudDrawer / MudTooltip / MudDialog / MudChart / MudButtonGroup / MudThemeProvider）/ 0 新依賴
- IDbContextFactory pattern（Stage 85 既有 / 子項 4 後端 query 對齊）
- SignalR Hub 既有 endpoint 不動（本 Stage 純前端 UI + 1 後端 query 改）
- 0 Migration（DB schema 不變 / Rules v4 row 不刪變孤兒）
- 0 LLM call / 0 燒 AiTeam 餘額（純前端 + Dashboard UI / Aria + Forge 走 subscription）
- 對齊 `docs/conventions/csharp.md` + `blazor.md` + `mudblazor.md`
- Internal API X-Api-Key 機制（子項 3 如新加 attachments endpoint 對齊既有）

---

## 風險與緩解

| 風險 | 緩解 |
|---|---|
| Catch-all 規模 L / 6 子項 scope 大 | 每子項獨立 commit / Forge healthy 偏離 plan 紀律 cover / 對齊 Stage 85 5 子項分階段 commit pattern |
| Sidebar hover overlay 跟既有 MudDrawer Variant 衝突 | 對齊 MudBlazor 8.x 既有 Variant API（Temporary / Persistent）+ docs/conventions/mudblazor.md 紀律 |
| Theme palette 重設計影響全 Dashboard 視覺 | 結案前 Chrome MCP 全分頁視覺驗 + Christ 線下 dark/light 雙模式視覺驗收 |
| 全 Dashboard 砍版本/Stage 編號漏 grep 某處 | 砍前 + 砍後雙重 grep verify（對齊 Stage 85 v4 dead flag 整套清 pattern） |
| PetraInbox Attachments jsonb 結構未明 | Forge Plan Mode 必先 grep + read 既有 schema + Stage 79 image flow 實作細節 verify |
| Monitoring 時間範圍 filter 後端 query 改撞 IDbContextFactory pattern | 對齊 Stage 85 既有 5 service 切換 pattern / 0 新 service |
| Rules UI 移選項影響既有 RulesService caller | Stage 85 已 verify 只有 CEO path 用 / Dev/Doc/Ops 套用對象 0 caller / 移除 0 影響 |
| Theme 切換 C# event 對既有 Auth flow / login 流影響 | Stage 83 Auth 體系 0 動紀律延續 / Theme 改僅限 MainLayout / 結案前 login → logout 流程驗 |

---

## 版本歷史

### v1.0 — 2026-05-24（Aria 建立）

- 觸發：Christ 2026-05-24 Dashboard 痛點盤點 11 條 → 拍板 3 Stage 分組 B 折衷版 → Stage 86 = 改造（5 條：#2 + #5 + #9 + #10 + #11）+ 額外加 Rules dead config 清理（chat 內議題 / 同 v4 殘留根因）
- 範圍：6 子項 catch-all / Rules dead config + 版本/Stage 編號砍 + 視覺一致性 7 項 + 附檔預覽 + Monitoring 3 項 + Sidebar 改造 + Theme 機制根因修
- 對應 chat 內 running list：#2 + #5 + #9 + #10 + #11 + 新增 Rules dead config
- 6 維度 ultrathink 自審：① 架構 ✅（MudBlazor 8.x 既有元件 + IDbContextFactory 對齊 Stage 85）② 邏輯 ✅（6 子項獨立可分階段 commit）③ 競態 ✅（client-side state / 0 後端競態）④ 上下文 ⚠️ Attachments jsonb 結構待 Forge verify ⑤ 預留欄位 N/A（0 schema 改）⑥ 關鍵檔案清單 ✅
- 拍板：改造性質 trade-off 容忍高（vs Stage 85 救火低）/ 0 Migration / 0 LLM call / 走 Aria + Forge

### v2.0 — TBD（Forge 結案補實作紀錄）

實作紀錄段（v1.0 不寫，Forge 結案時補）：實際產出檔案 + 行數、SOP 套用對照、踩坑紀錄、健康偏離 plan 紀錄、Mock 覆蓋情況、Attachments jsonb 結構 verify 結論落地、Theme palette 真實 hex 色碼落地。
