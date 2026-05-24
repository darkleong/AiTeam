# Stage 85 Roadmap — Dashboard 救火（修 bug + 系統 alert + dead code 清理）

> **狀態：📋 規劃中**
> **文件版本：v1.0**
> 對應系統版本：v3.77.0（Stage 85 完成後 / minor bump）
> Stage 規模：**L**（5 子項 / 含後端 alert 機制 + 前端 bug 修 + 11 個 dead flag 清理 + 分頁結構整理 + dead data 清理 + 後端 timeout 機制 / 0 Migration / 0 燒 AiTeam 餘額 — 純 C# + Dashboard UI / 0 LLM call）
> 觸發來源：2026-05-24 Christ Dashboard 痛點盤點 11 條 / 拍板 3 Stage 分組 B 折衷版 / 本 Stage 是「救火」性質（修壞掉的 + 清 dead code + 修系統 alert 機制 / risk profile 跟「改造」分開）
> 戰略意義：**為後續 AiTeam dispatch dogfood 鋪路** — 系統 alert 機制不修，AiTeam 失誤無法早發現（2026-05-24 Christ 第一單真實業務 dogfood 就因為這個切換策略回 Aria + Forge）

---

## 戰略脈絡

Christ 真實使用 Dashboard 揭出 5 條救火議題，全是「壞掉、誤導、靜默失敗」性質。對齊「修根因 > 補丁」精神，這 5 條共同根因有兩條：

1. **系統 alert 機制缺**（TokenGuard fire + failed task 都靜默失敗 / 老闆看不到）
2. **Stage 78a/78b/78c 砍 v4 path 時 dead code 沒清乾淨**（v4 dead flag 11 個、SystemSettings.razor 含 v4 段、MOCKMODE tab 跟 Tab 2 dup、paused session 殘留、Discord placeholder 卡）

對齊 Stage 84 結案才升的 refactor-sop v1.3「Dangling reference 清理」紀律，本 Stage 是該紀律的全 Dashboard 範圍延伸實踐。

---

## 子項清單

### 子項 0：DbContext 並發 bug 修（同類根因第 3 次累積）— 規模 S

修 `DashboardProjectService.cs:15` `GetAllProjectsAsync()` 撞 Blazor circuit 並發 DbContext 問題（`InvalidOperationException: A second operation was started`），對齊 Stage 80 + Stage 83 議題 F3 既有 `IDbContextFactory<AppDbContext>` pattern。

**audit 紀律**：Forge 砍前必 grep 全 `src/AiTeam.Dashboard/Services/` 看其他 service 是否同類 victim（直接注入 `AppDbContext` 而非 `IDbContextFactory`）。找到的一次全改，不只 ProjectService 一個。

### 子項 1：失敗告知 UX 黑洞（三層 alert 機制）— 規模 M-L

對齊 Christ 紀律「我視線可能在別的地方就完全錯過了」，三層並行通知（不能只一層）：

- **Bot Discord push**：TokenGuard fire + failed task 進 dead-letter + paused session timeout 自動 cancel 三個事件，各自 push 到既有 #警報 channel（含關鍵 ID + 原因）
- **Bot SignalR 即時推送**：既有 SignalR Hub（`/hubs/agent-status`）加 push failed event 給 Dashboard（既有 ReceiveTaskUpdate / ReceiveQueueUpdate endpoint）
- **Dashboard 右下角 toast**：MudBlazor `MudSnackbar` 訂閱 SignalR failed event，即時彈出警告 3-5 秒（對齊 Christ 原話「類似右下角彈出來的那種 toast」）

順帶：PetraInbox 收件分頁 failed status badge 強化視覺（既有顯示但不夠醒目）。

### 子項 2：v4 dead flag 整套清理 — 規模 S

砍 11 個 v4 dead flag（grep verify 0 業務 caller）：

- 5 toggle：`UseFrameworkAppealLoop` / `UseFrameworkKickoff` / `UseFrameworkKickoffMidInterrupt` / `UseFrameworkDesign` / `UseFrameworkPipeline`
- 5 輪次數值：`ReviewAppealMaxRounds` / `QaFixMaxRounds` / `DevPlanAppealMaxRounds` / `KickoffMaxRounds` / `DesignMeetingMaxRounds`
- 1 SkipCeo：`SkipCeoConfirm`

砍範圍：`WorkflowSettings.cs` property + `WorkflowSettingsResolver.cs` accessor method + `AgentSettings.cs`（SkipCeoConfirm）+ `appsettings.json` default + `DbSeeder.cs`（SkipCeoConfirm）+ Dashboard 4 處 dup UI（WORKFLOW FLAGS tab「v4 Framework 控制」collapsed panel + SystemSettings.razor「v4 漸進遷移控制」段 + 「流程輪次上限」段 + 「一般設定」段內 SkipCeoConfirm toggle）。

DB row 不刪（對齊 0 Migration 紀律 / 變孤兒 row harmless / Stage 84 已驗證此模式）。

**附帶 audit**：v5/v5.5 剩餘 flag 中 `V5MemoryCompactThresholdPercent` / `V5MemoryCompactKeepCount` 兩個是否仍有業務 caller — Forge 跑 grep verify，dead 則一併砍 / active 則留。

### 子項 3：Dashboard 分頁結構整理 — 規模 XS

兩個結構性 dup 清理：

- **砍 MOCKMODE tab**（SettingsHub Tab 8 整個砍）— Tab 2「TOKEN 守門 + 系統設定」已含 Mock Mode 完整功能，Tab 8 是 reuse 同個 SystemSettings.razor component 的 dup（程式碼註解自己承認）
- **砍系統健康 Discord placeholder 卡**（MonitoringHub）— Stage 83 留的 placeholder，文字寫「Stage 84+ 補」對使用者無意義。等 FF Stage 85+ 候選 #7「Bot /internal/health Discord 連線真實 check」做完再加回

### 子項 4：paused session dead data 清理 + timeout 預防機制 — 規模 XS + S

**A. 立刻清 3 筆 Stage 80 self-verify 殘留**：

3 筆 paused session（`4b921ffb` / `9627fb6c` / `e13ce693`）卡 4-5 天，是 Stage 80 結案 self-verify scenario B/D/F 測試殘留。SQL UPDATE Status 從 paused 改 cancelled（保留 audit trail）。

**B. timeout 預防機制**：

`PetraSessionRecoveryService` 既有 HostedService 加 timeout cleanup logic：paused 超過 `PausedSessionTimeoutHours`（appsettings.json default = 24h）自動 cancel + Discord push 告知（對齊子項 1 三層 alert）。

**C. self-verify 紀律升級**：

`refactor-sop.md` 結案必做清單第 8 條加「test session cleanup」紀律（Stage 結案 self-verify 跑完，手動或自動清掉測試 PetraSession）。

---

## 設計決策

| # | 決策 | 理由 |
|---|---|---|
| 1 | DbContext bug audit 範圍擴大全 DashboardServices | 同類根因第 3 次，不限 ProjectService 一個 |
| 2 | 失敗告知三層並行（Discord + SignalR + toast）| Christ 視線可能在任何位置，單一通道會錯過 |
| 3 | Toast 用 MudBlazor `MudSnackbar` | 既有元件 0 新依賴，對齊 docs/conventions/mudblazor.md |
| 4 | Discord push 加 rate-limit 防洪水 | 連續失敗會洗版 alert channel |
| 5 | v4 flag 砍範圍含 appsettings.json default + DbSeeder | dead reference 全鏈清，不只 property |
| 6 | v4 DB row 不刪變孤兒（0 Migration） | 對齊 Stage 84 既有模式，harmless |
| 7 | v5/v5.5 剩餘 flag audit 結果落地 plan v2 結案紀錄 | Forge 跑 grep verify 後決定砍/留 |
| 8 | MOCKMODE tab 整個砍（A 路線） | Tab 2 已含 Mock Mode 完整功能，Tab 8 純 dup |
| 9 | Discord placeholder 卡砍（A 路線）+ FF #7 描述更新提醒 | 還沒實作不該佔位誤導 |
| 10 | paused session timeout default 24h（配置在 appsettings.json） | 可後續調整不寫死 |
| 11 | timeout 自動 cancel 走既有 `PetraSessionRecoveryService` | 0 新 HostedService，對齊既有架構 |
| 12 | self-verify cleanup 紀律升級到 `refactor-sop.md` v1.4 | 跨 Stage know-how 升級對齊 Aria 結案第二段 step 1 |

---

## 驗收情境

### A. DbContext bug 修

1. **RULES + PROJECTS 分頁正常開啟** — 觸發：登入 Dashboard，點設定中心 → RULES + PROJECTS 分頁。驗證：頁面正常 load，Dashboard log 0 `InvalidOperationException: A second operation was started`
2. **全 DashboardServices 同類 victim 修完** — 觸發：依序進每個 Dashboard 分頁（首頁、任務中心、設定中心 8 個 tab、監控中心 4 個 tab）。驗證：Dashboard log 全程 0 同類 exception

### B. 失敗告知系統 alert

3. **TokenGuard fire 時 Discord push 警告** — 觸發：SQL 把 TokenGuard 全域月限暫調很低（如 1000），送一個 task → token guard fire。驗證：Discord #警報 channel 收到含「TokenGuard 全域月限觸發 / 所有 LLM 呼叫暫停 / 已用 X / 上限 Y」訊息
4. **failed task 進 dead-letter 時 Discord push** — 觸發：人工讓某 task 連續失敗達 MaxAttempts 進 dead-letter。驗證：Discord #警報 channel 收到含 inboxId + 失敗原因訊息
5. **Dashboard 右下角 toast 即時警告** — 觸發：同 #3 或 #4 觸發失敗。驗證：Dashboard 任何分頁右下角彈出 MudSnackbar toast 訊息，3-5 秒自動消失
6. **Discord push 不洗版**（rate-limit）— 觸發：連續觸發 10 次同類失敗。驗證：Discord 收到 1-2 則 alert（不是 10 則），含「N 次同類事件」aggregate 描述
7. **PetraInbox failed status badge 視覺強化** — 觸發：查看 PetraInbox 收件分頁有 failed row。驗證：status column 紅色 badge 視覺明顯（vs 既有可能不夠醒目）

### C. v4 dead flag 砍

8. **11 個 v4 flag 程式碼 0 reference** — 觸發：`Select-String -Path "src/**/*.cs" -Pattern "UseFrameworkAppealLoop|UseFrameworkKickoff|UseFrameworkKickoffMidInterrupt|UseFrameworkDesign|UseFrameworkPipeline|ReviewAppealMaxRounds|QaFixMaxRounds|DevPlanAppealMaxRounds|KickoffMaxRounds|DesignMeetingMaxRounds|SkipCeoConfirm"`。驗證：0 match（除 git history）
9. **Dashboard 4 處 dup UI 全砍** — 觸發：訪問 SettingsHub WORKFLOW FLAGS tab 與 TOKEN 守門 + 系統設定 tab。驗證：「v4 Framework 控制」collapsed panel 不存在，「v4 漸進遷移控制」段不存在，「流程輪次上限」段不存在，「一般設定」段內 SkipCeoConfirm toggle 不存在
10. **v5/v5.5 audit 結果落地** — 觸發：plan v2 結案紀錄段 Forge 寫 audit 結論（`V5MemoryCompactThresholdPercent` / `V5MemoryCompactKeepCount` 砍或留 + 理由）。驗證：plan v2 結案紀錄含完整 audit 對照表

### D. Dashboard 分頁結構

11. **MOCKMODE tab 砍** — 觸發：訪問 SettingsHub。驗證：Tab 8 MOCKMODE 不存在，Tab 列剩 7 個。Mock Mode 功能仍在 Tab 2「TOKEN 守門 + 系統設定」可用（toggle + delay 範圍兩個 UI 都在）
12. **系統健康 Discord placeholder 卡砍** — 觸發：訪問監控中心 → 系統健康分頁。驗證：Discord 卡不存在，剩 Bot 容器、PostgreSQL、SignalR Hub 三張卡

### E. paused session dead data + timeout

13. **3 筆 Stage 80 殘留 paused session 清掉** — 觸發：`SELECT Id, Status FROM petra_sessions WHERE Id IN ('4b921ffb-47a7-4a91-a01b-c7a4dfb278bb', '9627fb6c-690b-4759-873d-b4207413fdb7', 'e13ce693-dbb1-4444-8d28-e0eea54f888b')`。驗證：3 筆 Status 從 paused 改 cancelled
14. **paused session timeout 機制 fire** — 觸發：人工 INSERT 一筆 test paused petra_session，UpdatedAt = 25 小時前。驗證：PetraSessionRecoveryService 跑 cycle 後自動 cancel（Status = cancelled），同步 Discord #警報 channel 收到 timeout cancel 訊息
15. **timeout config 在 appsettings.json** — 觸發：`cat src/AiTeam.Bot/appsettings.json`。驗證：含 `PausedSessionTimeoutHours = 24`（或對應 key）
16. **refactor-sop v1.4 升級** — 觸發：`cat docs/conventions/refactor-sop.md`。驗證：結案必做清單第 8 條「test session cleanup」存在，版本歷史含 v1.4 entry

---

## 技術約束

- MudBlazor 8.x `MudSnackbar`（既有元件，0 新依賴）
- `IDbContextFactory<AppDbContext>` pattern（對齊 Stage 80 + Stage 83 議題 F3 既有）
- SignalR Hub 既有 endpoint（不新增，加 push 內容）
- Discord push 走既有 InteractionService 或 DiscordBotService 既有 channel push 機制
- 0 Migration（DB schema 不變，arbitrary row 動 OK）
- 0 LLM call（純 C# + UI 改造，Aria + Forge 走 Claude Code subscription）
- 對齊 `docs/conventions/csharp.md` + `docs/conventions/blazor.md` + `docs/conventions/mudblazor.md`

---

## 風險與緩解

| 風險 | 緩解 |
|---|---|
| DbContext bug audit 範圍誤判（漏 grep 某 service） | Forge grep 全 DashboardServices/ + 跨 service caller verify，找到的全改 |
| Discord push 訊息洪水（連續失敗洗版） | rate-limit 機制（per type per N min 限頻）+ aggregate 描述 |
| MudSnackbar 跟既有 UI 衝突 | 對齊 docs/conventions/mudblazor.md，跨分頁全域 service 注入 |
| v4 flag 砍漏（漏 grep 某 reference） | Forge 砍前 + 砍後雙重 grep verify（含 .razor、.cs、appsettings、DbSeeder 全鏈） |
| MOCKMODE tab 砍後 Tab 2 不完整 cover | Forge 砍前 verify Tab 2 含 Mock Mode 完整功能（toggle + delay 範圍兩個 UI） |
| 系統健康 Discord placeholder 砍後 FF #7 真實做時忘記加回 | FF #7 描述更新加「Stage 85 砍 placeholder，真實 check 做完要加回」提醒 |
| paused session timeout cancel 誤殺真實 paused 等老闆的 session | timeout default 24h 比 HITL 老闆回應時間長很多，且 cancel 前 Discord push 提醒老闆有機會手動 resume |
| pure-ish refactor + alert 機制新增 silent regression | MockMode 4 流程 + xUnit 既有 baseline 全綠 + 手動驗收 Stage 85 alert 機制三層 fire 各驗一次 |

---

## 版本歷史

### v1.0 — 2026-05-24（Aria 建立）

- 觸發：Christ 2026-05-24 Dashboard 痛點盤點 11 條 → 拍板 3 Stage 分組 B 折衷版 → Stage 85 = 救火（5 條：#1 + #3 + #4 + #7 + #8）
- 範圍：DbContext bug + 失敗告知三層 alert + v4 dead flag 11 個整套清 + Dashboard 分頁結構 2 處整理 + paused session dead data + timeout
- 對應 running list（chat 內整理）：#1、#3、#4、#7、#8 五條合併為本 Stage
- 6 維度 ultrathink 自審：① 架構 ✅（對齊既有 IDbContextFactory + MudSnackbar + SignalR + Discord push 既有 pattern）② 邏輯 ✅（5 子項獨立可分階段 commit）③ 競態 ⚠️ Discord push 洪水已 cover 緩解 ④ 上下文 ⚠️ v5/v5.5 剩餘 flag audit 結果待 Forge verify ⑤ 預留欄位 N/A（0 schema 改）⑥ 關鍵檔案清單 ✅
- 拍板：pure-ish refactor + alert 機制新增 / 0 Migration / 0 燒 AiTeam 餘額 / 走 Aria + Forge（暫不走 AiTeam dispatch，因 alert 機制本身是這 Stage 要修的東西）

### v2.0 — TBD（Forge 結案補實作紀錄）

實作紀錄段（v1.0 不寫，Forge 結案時補）：實際產出檔案 + 行數、SOP 套用對照、踩坑紀錄、健康偏離 plan 紀錄、Mock 覆蓋情況、v5/v5.5 剩餘 flag audit 結果落地。
