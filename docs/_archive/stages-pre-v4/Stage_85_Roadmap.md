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

### v2.0 — 2026-05-24（Forge 結案補實作紀錄）

---

## 實作紀錄

### A. commit 清單（4 commit + 1 SQL 操作 / push 到 main）

實際 commit 數 4（plan 寫 6 / 子項 4-A 是純 SQL UPDATE 0 code 改 / 子項 4-B+C 跟子項 1 合併，因 PetraSessionRecoveryService timeout cleanup 需要先建好 SendThrottledAsync 共用 wrapper 才接得上）：

| commit | 子項 | 主要範圍 |
|---|---|---|
| `7d007e2` | 3 | 砍 SettingsHub Tab 8 MOCKMODE + MonitoringHub Discord placeholder 卡（3 張卡 md=4 撐滿） |
| `9dd9596` | 2 | v4 dead flag 11 個整套清（9 檔 / -457 行 +27 行 / grep 0 業務 match verify） |
| `b10a86b` | 0 | 5 個 DashboardServices 切 IDbContextFactory pattern（同類根因第 3 次累積終結） |
| `fe9bd25` | 4-B+C + 1 | paused session timeout cleanup loop + refactor-sop v1.4 + 三層 alert 機制（v3.77.0 bump） |
| _SQL_ | 4-A | `docker exec ... psql ... UPDATE petra_sessions SET Status='cancelled' WHERE Id IN (3 IDs)` — 0 code 改 / 純 DB 操作 |

### B. 實際產出檔（vs plan 預估）

**新增 3 檔**（對齊 plan 預估）：
- `src/AiTeam.Bot/Services/AlertRateLimiter.cs`（41 行 / Singleton + ConcurrentDictionary thread-safe + per type per N min 限頻 + suppressedCount aggregate）
- `src/AiTeam.Shared/Dtos/AlertEventDto.cs`（17 行 / SignalR push payload record）
- `src/AiTeam.Dashboard/Components/Layout/AlertToastSubscriber.razor`（52 行 / 跨 circuit ReceiveAlertEvent 訂閱 + MudSnackbar 5 秒彈出 / 對齊 Home / TaskHub Nav.ToAbsoluteUri pattern）

**修改 21 檔**（含跨層 / 對齊 plan 預估）：
- Bot 端 14 檔：WorkflowSettings.cs / WorkflowSettingsResolver.cs / AgentSettings.cs / appsettings.json / DbSeeder.cs / CommandHandler.cs / DiscordAlertService.cs / DashboardPushService.cs / TokenTrackingProvider.cs / PetraDispatchWorker.cs / PetraSessionRecoveryService.cs / Program.cs / Stage77MultiConsumerTests.cs / PetraSessionRepository.cs（Data 層）
- Dashboard 端 9 檔：AgentStatusController.cs / MudProviders.razor / SettingsHub.razor + razor.cs / SystemSettings.razor + razor.cs / MonitoringHub.razor + razor.cs / InteractionCenter.razor / TaskHub.razor / 5 個 Services（DashboardAgentService / DashboardProjectService / DashboardRuleService / DashboardTokenService / InteractionRespondService）
- Data 層 1 檔：AgentStatusHub.cs（ReceiveAlertEvent const）
- docs 1 檔：refactor-sop.md（v1.4 升級）
- 版本檔 1：Directory.Build.props（3.76.0 → 3.77.0）

### C. v5/v5.5 剩餘 flag audit 落地

**`V5MemoryCompactThresholdPercent` / `V5MemoryCompactKeepCount` 兩 flag 保留**（grep verify 真實業務 caller）：

| Flag | 業務 caller | 狀態 | 動作 |
|---|---|---|---|
| `V5MemoryCompactThresholdPercent` | `src/AiTeam.Bot/Orchestration/Petra/PetraTalentDispatchService.cs:220` `await workflowResolver.GetV5MemoryCompactThresholdPercentAsync(ct)` | **active** | 留 / 在 SettingsHub `_numericFlags` 內 + WorkflowSettings property + Resolver accessor 都不動 |
| `V5MemoryCompactKeepCount` | `src/AiTeam.Bot/Orchestration/Petra/PetraTalentDispatchService.cs:219` `await workflowResolver.GetV5MemoryCompactKeepCountAsync(ct)` | **active** | 留 / 同上 |

對齊 plan 拍板「v5/v5.5 剩餘 flag audit 結果落地」。

### D. SOP 套用對照

| SOP | 本 Stage 怎麼套 |
|---|---|
| refactor-sop v1.3 第 6 條 Dangling reference 清理 | v4 dead flag 11 個 + dangling doc comment（CommandHandler.cs:27 / WorkflowSettings.cs / WorkflowSettingsResolver.cs class doc）+ Dashboard UI dup（MOCKMODE tab / SkipCeo toggle / v4 framework 段 / 流程輪次上限段 / Discord placeholder 卡）全 grep + 砍乾淨 / 砍後 grep verify 0 業務 reference（殘留 4 處全 Stage 85 紀念註解 / `bin/Release/` build artifact 不算） |
| refactor-sop v1.3 第 7 條 Warning baseline 比對 | 砍前 baseline 101 warning / 砍後仍 101 warning / 0 新引入 CS9113 / CS4014 等 / 對齊既有紀律 |
| Stage 80 既有 IDbContextFactory pattern | 5 service 全套切（DashboardAppSettingsService / DashboardDeploymentService / DashboardInteractionQueryService 已對齊 Stage 80）+ Stage 85 新切 5 個（DashboardAgentService / DashboardProjectService / DashboardRuleService / DashboardTokenService / InteractionRespondService） |
| caller 0 改動 verify | 8 Dashboard services 切完 DI 自動解析 IDbContextFactory / 既有 caller 0 改動 |
| 三層 alert 對齊既有 infra | DiscordAlertService（既有 Singleton）+ SignalR `/hubs/agent-status`（既有 endpoint）+ `IHttpClientFactory("aiteam-dashboard")`（既有 HttpClient）+ MudSnackbarProvider（既有 Layout）/ 0 新依賴 |
| 升級到 refactor-sop v1.4 | 加結案必做第 8 條「Test session cleanup」+ v1.4 版本歷史 entry（對齊 Aria 結案第二段 step 1 第 4 次實踐） |

### E. 健康偏離 plan 紀錄

**子項 4-B+C 跟子項 1 合併 commit（plan 寫 6 commit / 實際 4 commit）**：

- 真實 root cause：PetraSessionRecoveryService.RunPausedTimeoutCleanupAsync 內呼 `discordAlert.SendThrottledAsync("paused_timeout", ...)` — `SendThrottledAsync` API 屬子項 1 共用 infra / 子項 4-B 必須晚於子項 1 infra 才接得上
- 折衷拍板：合併兩子項成同 commit（fe9bd25）/ commit message 內小節清楚分段（【三層 alert 機制 — 子項 1】+【paused session timeout — 子項 4-B】+【refactor-sop v1.4 升級 — 子項 4-C】）/ Aria gate1 對照 still ok

對齊 workflow_forge 第三節「healthy 偏離 plan 紀律」+ Aria 預先設計批准範圍。

### F. 踩坑紀錄

#### F1：SystemSettings.razor 大段 Edit old_string match 失敗

砍「v4 漸進遷移控制」整段 5 卡（~110 行）+「流程輪次上限」段時，Edit tool 用一次大 old_string match 失敗（推測 unicode 半形 vs 全形 `,` 跟 `，` 字面有微差異）/ 第一次嘗試只砍掉中間部分 / 留壞掉的 Kickoff 卡內部結構（`<div>` 沒對應 `</div>` 等）。

**修法**：放棄大 Edit / 改用 Write 整檔重寫（從 head + 保留段全寫新檔）/ build verify 通過。同樣紀律套到 SystemSettings.razor.cs（field + load + handler + upsert 砍 6 處）也用 Write 重寫。

**未來 SOP 補強候選**：refactor-sop 加紀律「Razor / 大檔砍多處段時 unicode 全半形 typo 風險高 / 直接 Write 重寫比連續 Edit 安全」— 留 Aria stage-summary 評估是否升 v1.5。

#### F2：Stage77MultiConsumerTests 3 處 ctor 同步漏（refactor-sop v1.3 第 7 條 Warning baseline 跨檢驗）

PetraDispatchWorker ctor 加 `DiscordAlertService discordAlert` 注入後 / 直接呼叫 `new PetraDispatchWorker(...)` 的 test 3 處（L189 / L260 / L354）沒同步 / `dotnet build` 抓出 3 個 CS7036 error。

**修法**：3 處 ctor 加 `discordAlert: null!` named parameter（test 路徑不觸發 dead-letter push）/ build 0 Error。

**對齊 refactor-sop v1.3 第 7 條紀律** — Warning baseline 比對也 cover ctor 簽名變更 test 同步漏網。

### G. Mock 覆蓋情況 + Forge 自驗結果

#### G1：結構驗（A1-A4 全過 — Bash diagnostic）

| # | 項目 | 結果 |
|---|---|---|
| A1 | v4 flag 11 個程式碼 0 業務 reference | ✅ grep 0 match（殘留只在 `bin/Release/` build artifact / 下次 build regenerate） |
| A2 | 5 service IDbContextFactory pattern 完成 | ✅ `grep ^public class.*\(AppDbContext db` 0 match |
| A3 | `dotnet build AiTeam.slnx` 0 Error 0 新 warning | ✅ 0 Error / 101 warning（既有 baseline 不動 / 0 新 CS9113 / CS4014） |
| A4 | xUnit baseline 全綠 | ✅ 130 passed / 2 skipped / 0 failed |

#### G2：行為驗（B5-B6 Forge 自驗 / B7-B9 留 Christ 真實 fire）

| # | 項目 | 結果 |
|---|---|---|
| B5 | paused session 3 筆 Status=cancelled | ✅ DB verify 三筆已 cancelled |
| B6 | DbContext bug 不復發 | ✅ Dashboard log `second operation` 0 match |
| B7 | TokenGuard Discord push fire | ⏸️ 留 Christ 真實 fire（需實際 LLM call / Mock endpoint Stage 78c 已砍 / 不在 Forge 自驗範圍） |
| B8 | paused session timeout fire | ⏸️ 留 Christ 真實 fire（cleanup cycle 1h interval / 等待性驗證） |
| B9 | Rate-limit 防洪水 | ⏸️ 留 Christ 真實 fire（連續 10 次同類 fire 需實際 LLM） |

#### G3：Dashboard UI 視覺驗收（C 段 / 5 條 Claude in Chrome MCP 跑完）

| # | 項目 | Chrome MCP 證據 |
|---|---|---|
| C4 | PetraInbox failed badge 視覺強化 | ✅ failed chip `hasFilled=true`（Variant.Filled）+ `hasIcon=true`（ErrorOutline）/ completed chip 保留既有 Text variant + 無 icon |
| C5 | SettingsHub Tab 列 7 個 + Tab 1 無 v4 panel + 數值上限 6 flag | ✅ Tab list = [Workflow Flags / Token 守門 + 系統設定 / Agents / Talents / SkillPrompts / TalentPrompts / Rules + Projects] / `v4FrameworkFound=false` / 「數值上限（6 flag）」 |
| C6 | SystemSettings 三段結構 | ✅ h3 = [一般設定 / CEO 指令通道 / Token 守門設定] / SkipCeo + v4 + 流程輪次上限段全 false |
| C7 | MonitoringHub 系統健康 3 卡 | ✅ cardTitles = [Bot 容器 / PostgreSQL / SignalR Hub] / `discordCardFound=false` |
| C8 | 3 筆 paused session 已 cancelled | ✅ 任務中心歷史 tab 三 ID（`4b921ffb` / `9627fb6c` / `e13ce693`）Status=cancelled / Completed 05/24 05:49 |

#### G4：剩 Christ 真實 fire 驗收（3 條）

對齊 Christ 拍板「這三條就留著之後再測試吧」：
1. TokenGuard Discord push fire（含 Dashboard MudSnackbar toast 同步彈出）
2. paused session timeout 自動 cancel fire（明天 cleanup cycle 觸發）
3. Rate-limit 防洪水（連 10 次同類 → Discord 1-2 則含 aggregate 文案）

### H. 0 follow-up commits 狀態

本 Stage 0 patch / 0 follow-up commit / 0 build / test regression / 0 既有 Dashboard 分頁 break / 0 既有 caller 改動。版本 `v3.76.0 → v3.77.0` minor bump 完成。

### I. 結案範圍清單

✅ 子項 0：5 DashboardServices IDbContextFactory pattern
✅ 子項 1：三層 alert 機制（Discord push + SignalR + MudSnackbar toast + rate-limit）+ PetraInbox failed badge 視覺強化
✅ 子項 2：v4 dead flag 11 個整套清（9 檔）
✅ 子項 3：Dashboard 分頁結構 dup 清理（MOCKMODE tab + Discord placeholder 卡）
✅ 子項 4-A：3 筆 Stage 80 殘留 paused session SQL UPDATE
✅ 子項 4-B：paused session timeout cleanup loop（24h auto cancel + Discord push）
✅ 子項 4-C：refactor-sop v1.4 升級（結案必做第 8 條 Test session cleanup）
✅ 結構 + 行為 + Dashboard UI 驗收：A1-A4 + B5-B6 + C4-C8 全過 / B7-B9 留 Christ 真實 fire 驗收
