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

### v2.0 — 2026-05-24（Forge 結案）

#### A. commit 清單（7 commit / plan 預期 7 / 實際對齊）

| commit | 範圍 |
|---|---|
| `6ad2cbb` | 子項 0：Rules dropdown 砍 8 個 v4 dead 角色（全域 + CEO 留 / DB row 變孤兒）|
| `06810ca` | 子項 1：全 Dashboard UI 顯示文字砍版本/Stage 編號（含 SettingsHub 視覺一致性合併）|
| `c18f7d2` | 子項 2 剩餘 + 子項 3：視覺一致性 + PetraInbox 附檔預覽 MudDialog |
| `eba5b0e` | 子項 4：Monitoring Tab 1 時間範圍 filter（6 選項）+ 90% per-agent 警戒擴 + per-Skill 維度 |
| `9e344cd` | 子項 5 + 子項 6：Sidebar 兩態切換 + Theme 機制根因修 + mudblazor.md 紀律落地 |
| `96618b6` | chore：version bump 3.77.0 → 3.78.0 |
| `304d077` | fix：視覺驗收 follow-up 2 處（GetFlagDescription UI 文字砍 + sidebar overlay width 修） |

#### B. 實際產出檔（3 新 + 12 修 + 1 docs）

**新增 3 檔**：
- `src/AiTeam.Dashboard/Services/IThemeService.cs`（36 行 / Scoped DI）
- `src/AiTeam.Dashboard/Components/Layout/ThemeToggleButton.razor`（32 行 / Interactive Island）
- `src/AiTeam.Dashboard/Components/Pages/Tasks/AttachmentPreviewDialog.razor`（80 行 / base64 圖片渲染 + MudCarousel 多附檔）

**修改 12 檔**：
- `MainLayout.razor`（砍 AppBar / logo 移 Drawer 頂 / hamburger 浮 / ThemeToggleButton 接入）
- `MudProviders.razor`（IThemeService subscribe + IDisposable + MudTheme palette 重設計）
- `App.razor`（inline script 砍 aiteamToggleSidebar + 改寫 window.aiteamSidebar 兩態 API）
- `RuleManagement.razor` + `.cs`（_agentOptions 砍 8 entry + GetAgentChipColor + isLegacy fallback chip）
- `SettingsHub.razor` + `.cs`（UI 文字砍 5+ 處 / TALENTS Skills N/A / TALENTPROMPTS 擴 5 欄 / GetFlagDescription 12 case 砍版本編號 / switch Color 統一 / 數值靠右）
- `Home.razor`（4 卡 tooltip 包裝 / 紅字描述移 hover）
- `MonitoringHub.razor` + `.cs`（時間範圍 6 selector + per-Skill 維度 stub + 90% per-agent 警戒 + UI 文字砍 3 處）
- `TaskHub.razor` + `.cs`（MudChip 改 MudButton OpenAttachmentDialogAsync + IDialogService 注入 + FixedHeader 3 表）
- `InteractionCenter.razor`（歷史回覆表 FixedHeader）
- `TokenMonitoring.razor`（FixedHeader + UI 文字砍 Stage 56）
- `Program.cs`（AddScoped<IThemeService, ThemeService>）
- `wwwroot/css/app.css`（hamburger 浮 + overlay 模式 + dark hex 對齊 PaletteDark + 砍 dead .theme-btn-dark/light）

**docs 修改**：
- `docs/conventions/mudblazor.md`（第五節 Dark Mode 完整重寫 / IsDarkMode binding 紀律落地 + ThemeToggleButton Interactive component pattern + IThemeService Scoped event + 兩 layer 對齊 CSS 變數）
- `src/Directory.Build.props`（v3.77.0 → v3.78.0）

#### C. 5 議題 verify 結論落地

| 議題 | 結論 |
|---|---|
| PetraInbox.Attachments 結構 | **base64 內嵌**（Stage 79 schema `[{"type":"image","base64Data":"...","mediaType":"..."}]`）→ 子項 3 0 新 Internal API endpoint / MudDialog 直接 `<img src="data:...">` 渲染（驗收實證圖片正常顯示）|
| 全 Dashboard 砍版本/Stage 編號 grep baseline | 63 處 across 25 files → 10+ 處 UI 文字砍 / razor `@*` comment + C# `//` comment + XML doc 保留 / **子項 1 漏抓 razor.cs GetFlagDescription 12 case**（follow-up commit `304d077` 修）|
| Theme 機制衝突 | Aria Roadmap 子項 6「MainLayout setDark 改 C# event」跟 mudblazor.md 第一節 MainLayout Static SSR 紀律衝突 → 改 ThemeToggleButton 抽 Interactive component + IThemeService Scoped event（即時切色 0 reload ✓ Chrome MCP 視覺實證）|
| Rules dropdown 砍範圍 | Christ 拍板「全域 + CEO 留」/ 砍 8 v4 + Pm 共 9 entry → 留 2 entry / DB v4 殘留 row 顯示「全域（v4 殘留：xxx）」fallback chip（驗收實證 row 15「Dev」row 25「Doc」正確顯示）|
| NavMenu 結構 | 0 動 / 對齊 Roadmap 設計決策 #7 |

#### D. SOP 套用對照

- ✅ **mudblazor.md 第一節「MainLayout Static SSR」紀律**：plan mode 揭跟 Aria Roadmap 子項 6 設計衝突 / 走 Interactive Island + Scoped service event 修法（healthy 偏離 / Aria 採納率高）
- ✅ **mudblazor.md 第三節「MudTable FixedHeader」紀律**：子項 2.7 全 Dashboard 11 個 MudTable 補 FixedHeader + Height（既有 RuleManagement / ProjectManagement / DeploymentHistory 已對齊）
- ✅ **Stage 85 IDbContextFactory pattern**：子項 4 後端 query 改對齊（DashboardTokenService 已切 Stage 85）
- ✅ **refactor-sop v1.5「Razor 大檔砍多處段工具選擇」**：本 Stage 0 大段 Razor 砍 / 全用小段 Edit / 0 踩 unicode typo 坑
- ✅ **IsDarkMode binding 紀律**：mudblazor.md 第五節重寫落地（IThemeService Scoped + Interactive component + 兩 layer 對齊 CSS 變數）

#### E. Healthy 偏離 plan 紀錄

1. **commit 7 → 7（含 follow-up 1）+ 子項合併**：
   - 子項 0 / 1 各獨立 commit ✓
   - 子項 2 跟子項 3 因 TaskHub.razor 同檔重疊 → 合 commit `c18f7d2`
   - 子項 5 跟子項 6 因 MainLayout.razor / App.razor / app.css 三檔重疊 → 合 commit `9e344cd`
   - SettingsHub.razor 視覺一致性 5 處改動跟 UI 文字砍 5 處改動同檔 → 合進子項 1 commit `06810ca`（commit message 沒明寫 / healthy 偏離）
   - 子項 4 獨立 commit ✓
   - 視覺驗收 follow-up 修 1 commit `304d077`（GetFlagDescription + sidebar overlay width）

2. **子項 3 路線簡化**：Plan 寫「如 file path → 可能需新 Internal API endpoint」/ plan mode verify Attachments 是 base64 內嵌 → 0 新 API / 直接 MudDialog 渲染（Aria gate0 verify 結論）

3. **子項 4.3 MudChart ReferenceLine 退路**：MudBlazor 8.x MudChart `ChartOptions` 不直接支援 ReferenceLine API → 走既有 MudAlert path 擴（80% 全域月限 + 90% per-agent 日限 / `GetPerAgentDailyAlerts` helper / today period 才算）

4. **子項 4.4 per-Skill 維度 stub**：TokenLog **0 SkillName column** verify → MudAlert stub「per-Skill 維度待 SkillName column 補完才有資料」+ Stage 86+ FF 候選紀錄

5. **Theme palette 真實 hex 落地**：plan 預設 VS Code Dark+ baseline → Chrome MCP 視覺驗收 Christ 看了 ✓ 接受（待 Christ 反饋是否需微調）
   - PaletteDark：`#1e1e1e` background / `#2d2d2d` BackgroundGray + Drawer/Appbar / `#252525` Surface / rgba(255,255,255,0.87) TextPrimary / `#818cf8` Primary（沿用）
   - app.css `html[data-theme="dark"]` `--mud-palette-*` 完整對齊（兩 layer 不再對不齊）

#### F. 踩坑紀錄

**F1：子項 1 grep filter 漏抓 razor.cs C# return string**

- 砍前 grep 用 `--include="*.razor"` / 只抓 razor 檔
- razor.cs C# code 內 method return string 也含 UI 顯示文字（`GetFlagDescription` 12 case 每個 description 都會 render 到 Dashboard）
- Chrome MCP 視覺驗收訪 SettingsHub 第一眼揪「v5 PoC 動態 orchestrator 上線（Stage 63B）」等 12 處殘留
- 修法：follow-up commit `304d077` 砍 12 case description 開發歷史 reference
- **SOP 升級候選**：砍 UI 文字時 grep 範圍應含 `*.razor` + `*.razor.cs` + `*.cs`（特別 method return string 模式 `=> "..."` / chip text / tooltip text）

**F2：子項 5 Sidebar overlay 模式 width 截斷**

- 加 `mud-drawer--overlay-mode` class 含 `position: fixed !important` / 但未明設 width
- MudDrawer 預設 width 透過 layout class `mud-drawer-open-persistent-left` 帶 CSS variable / overlay 模式不加 layout class（避免擠 main）→ width 變數沒套用 → 露出 ~80px
- 修法：follow-up commit `304d077` 加 `width: 260px !important` + `max-width: 260px !important` 對齊 MudDrawer 預設
- **SOP 升級候選**：MudDrawer overlay-mode 自製 CSS 必明設 width（不能依賴既有 layout class）

#### G. 自驗結果

**結構驗（Forge 自驗）**：
- ✅ A1：UI 顯示文字砍版本/Stage 編號 — grep filter razor comment + C# comment 後 0 match（除 RuleManagement.razor:56 `(v4 殘留)` 功能性 fallback 文字）
- ✅ A2：Rules dropdown 2 entry（全域 + CEO）verify
- ✅ A3：`dotnet build AiTeam.slnx` 0 Error / 25 Warning（NuGet vulnerability + 1 既有 CS0649 / 0 新增）
- ✅ A4：`dotnet test src/AiTeam.Bot.Tests` 130 passed / 0 failed / 2 skipped（Stage 85 baseline 不動）

**Chrome MCP UI 視覺驗（Forge 自驗）**：
- ✅ 首頁 4 卡乾淨（icon + 標題 + 數字 / MudTooltip hover 顯示描述）
- ✅ Sidebar 兩態：click hamburger pin 模式（擠壓 main）/ hover overlay 模式（不擠 main / 修 width 前露 80px / 修後完整）
- ✅ AI Team logo 在 Drawer 頂部 / hamburger 半透明浮左上 / v3.78.0 顯示
- ✅ Theme 切換即時生效（0 reload / dark↔light 切換 MudPaper / MudCard / 整體背景 + 按鈕變色全套）
- ✅ Dark Mode palette = Christ 深灰色 baseline（#1e1e1e 主背景 / #252525 卡片 / #2d2d2d sidebar+appbar）
- ✅ 設定中心 WORKFLOW FLAGS tab「動態架構（6 flag）」+「數值上限（6 flag）」/ UI 文字乾淨
- ✅ TALENTS Petra / Victoria Skills column = 「N/A（dispatcher / CEO 角色）」/ 其他 4 個顯示真實 Skill
- ✅ TALENTPROMPTS 5 欄統一（Talent / Active Version / Length / UpdatedAt / 動作）
- ✅ Rules tab：既有 row「全域」+ v4 殘留 row「全域（v4 殘留：Dev/Doc 等）」fallback chip
- ✅ 監控中心時間範圍 6 button（今天/這一週/最近 7 天/這個月/最近 30 天/全部）
- ✅ 維度 3 entry（per-Talent / per-PetraSession / per-Skill）/ per-Skill stub MudAlert 顯示「待 SkillName column 補完」
- ✅ PetraInbox 點「📎 1 張」開 AttachmentPreviewDialog / base64 圖片正常渲染

#### H. 0 真實 Trial 依賴 / 0 LLM call

純前端 + 1 後端 query 改造 / 0 燒 AiTeam 餘額 / 對齊 plan 「走 Aria + Forge subscription」紀律。

#### I. Christ 視覺驗收期 follow-up（15 議題 / 18 commit）

Christ 視覺驗收期逐張截圖反饋 / Forge 即時自修 + push / Chrome MCP 重 verify cycle 累積 follow-up 全紀錄：

| # | commit | 議題 | 修法 |
|---|---|---|---|
| 1 v1 | `0ac1c1a` | 首頁 4 卡 width 偏窄（Active 任務縮 80px / 右側空格）| MudTooltip 加 `Style="display:block; width:100%"` |
| 2+3 | `c504978` | NavMenu active icon 沒套 primary color + Drawer 沒延伸到 viewport top | CSS `.mud-nav-link.active .mud-nav-link-icon { color: primary }` + MudDrawer ClipMode.Always → Never + CSS `top:0 height:100vh` |
| 4 | `51bed95` | 主內容上方 ~64px 空白（AppBar 預留位）| CSS `.mud-main-content { padding-top:0 }` |
| 5 v1 | `588dd32` | AI Team logo 跟 hamburger 重疊 | drawer-logo class + `Style="padding-left:56px"` |
| 1 v2 | `ec8c23d` | 首頁卡 width v1 沒生效 | MudTooltip Style 改 `Inline="false"` prop（MudBlazor 8.x API）|
| 6 | `3ffb300` | MudTooltip popup 超寬出 viewport | CSS `.mud-tooltip { max-width: 280px }` |
| 5 v2 | `071372d` | logo padding v1 沒生效 — `pa-3` utility class `!important` 覆寫 inline style | 拿掉 pa-3 / 改全 inline `style="padding:12px 12px 12px 56px"` |
| 7 v1 | `3eb2702` | 監控中心時間範圍 active button highlight 不明顯 | 每 button 加 `Variant="@(active ? Filled : Outlined)"` |
| 8 | `6f2232f` | 監控中心維度 MudSelect 高度比旁邊小按鈕高 | MudSelect 加 `Margin.Dense + Dense=true` |
| 9 | `49b7f9e` | NavMenu 順序：監控中心 應在 設定中心 前 | NavMenu + Home 跳轉按鈕同步交換順序 |
| 10 | `30d61de` | 全 Dashboard MudNumericField 高度太高 | 8 個 MudNumericField 統一加 `Margin.Dense` |
| 7 v2 | `b6447fa` | 時間範圍 v1 button Variant 沒生效 — MudButtonGroup `OverrideStyles=true` 覆寫 child | MudButtonGroup 加 `OverrideStyles="false"` 讓 child 自己控 Variant |
| 11 v1 | `7a00273` | 監控中心表數字欄位 + 設定中心數值 input 右對齊 | MudTh/MudTd 加 `Style="text-align:right"` + 數值 input 改 flex 結構 |
| 12 | `c4dce70` | MudTextField + MudSelect 全 Dashboard 縮高度 | 10+ 處加 `Margin.Dense`（textarea Lines>=4 不動 / 視覺空間保留）|
| 13 | `35733d5` | TALENTS / SKILLPROMPTS / TALENTPROMPTS 編輯按鈕統一 | 3 處 MudButton + 文字 → MudTooltip + MudIconButton + Outlined.Edit icon |
| 11 v2 | `a510664` | 數值 input v1 沒貼右 — d-flex utility class 在 MudExpansionPanel 環境失效 | 純 inline `display:flex width:100%` + input `margin-left:auto` 雙保險 |
| 14 | `c6a1238` | PetraInbox 重跑按鈕統一 MudIconButton | TaskHub + InteractionCenter 2 處 MudButton → MudTooltip + MudIconButton + Outlined.Replay |
| 15 | `866054f` | RuleFormDialog 編輯排版混亂（dialog 太窄 / 元素孤立）| DialogOptions MaxWidth.Small + FullWidth / textarea Lines 3→4 Outlined / 套用對象+排序 inline flex 同行 |

#### J. SOP 升級候選（Aria 第二段 evaluate）

本 Stage 累積的 SOP 升級候選 — 4 條：

1. **refactor-sop v1.6 候選**：「砍 UI 文字 grep 範圍含 razor.cs C# return string 模式」
   - 觸發：F1 踩坑（子項 1 用 `--include="*.razor"` 漏抓 GetFlagDescription 12 case `=> "..."` description）
   - 紀律：UI 文字砍時 grep `*.razor + *.razor.cs + *.cs`（特別 `method() => "..."` chip text / tooltip text / select label 模式）

2. **mudblazor.md 候選**：「MudDrawer overlay-mode 自製 CSS 必明設 width」
   - 觸發：F2 踩坑（mud-drawer--overlay-mode 加 `position: fixed` 後 MudDrawer width CSS variable 失效 / 露 80px）
   - 紀律：MudDrawer 自製 mode class 必 explicit `width: Npx !important; max-width: Npx !important`（不能依賴 layout class 帶 width variable）

3. **mudblazor.md 候選**：「MudButtonGroup `OverrideStyles=false` 才能 child 自控 Variant」
   - 觸發：#7 v2 議題（Group 預設 true 覆寫所有 child Variant / 個別設定無效）
   - 紀律：MudButtonGroup 需 child 自決 Variant 時必加 `OverrideStyles="false"`（active/inactive 視覺區分 case）

4. **mudblazor.md 候選**：「`pa-N/m-N` utility class 帶 `!important` / inline style 無法 override」
   - 觸發：#5 v2 議題（drawer-logo `class="pa-3" + style="padding-left:56px"` v1 沒生效）
   - 紀律：要 override MudBlazor utility class padding/margin 時必拿掉 utility class / 改全 inline style（不能混搭依賴 inline 贏 utility）

5. **mudblazor.md 候選**：「MudTooltip `Inline="false"` + popup `max-width` 紀律」
   - 觸發：#1 v2 + #6 議題（MudTooltip wrap card 需 `Inline="false"` 撐 100% width / 但 popup 跟著繼承 anchor width 超出 viewport / 需 CSS limit `.mud-tooltip max-width`）
   - 紀律：MudTooltip 包大型 block element 時 `Inline="false"` 配 `.mud-tooltip max-width` CSS limit 雙紀律

5 條 SOP 升級候選交 Aria 第二段（`/aria-stage-summary`）evaluate 採納哪幾條 / 寫進 mudblazor.md + refactor-sop.md 對應紀律段。

**Christ 線下視覺驗收**：✅ 完成（Christ 第一段確認「完美 / 可結案」/ 視覺對齊預期）。
