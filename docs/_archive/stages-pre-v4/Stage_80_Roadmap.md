# Stage 80 — A HITL plan confirmation 閘門 + Trial_v23 4 議題收口

> 對應系統版本：v3.71.0 → v3.72.0
> 規模：M+
> 狀態：✅ 已完成（2026-05-20）
> 文件版本：v2.0
> 性質：新業務功能（HITL plan confirmation 閘門 — Petra 拆完 plan 開 BossInteraction + 4 decision pattern + Petra pause/resume）+ Trial_v23 4 議題收口（🔴 #1 DbContext concurrency hotfix + 🟡 #2 v5.5 confirm 按鈕重新定義 + 🟡 #4 InteractionCard 設計 issue）
> Model + Effort 建議：**Opus 1M + high**（M+ 規模 / Christ 自決升 Extra high 兜底空間 / 不慣性推 Extra high 對齊自省點 #39 反向校準）
> Stage 期間餘額影響：**0 燒 AiTeam 餘額**（Aria + Forge session 走 Claude Code subscription）/ Trial_v24 才燒（預估 $1-3）

---

## 戰略脈絡

**Christ 親口要的業務功能** — Petra 拆 plan 後 Christ 看完整 SubtaskPlan + 4 decision pattern 拍板（approve / edit / reject / respond），不無腦 auto dispatch。對齊業界 HITL 紀律（Stage 77 既有 WebSearch 結論「LangGraph interrupt + 4 decision pattern」業界成熟）。

**真實工程性質**：純內部 business logic 設計 — 把既有業界 pattern（Stage 77 已 WebSearch 拍板）內化到 AiTeam 既有 v5.5 orchestrator + UI 改動 + 議題收口。0 third-party framework 真實使用 = **不觸發 WebSearch**（對齊 workflow_aria.md 第三節 A 第 9 條紀律 — 純 v5.5 既有 pattern 對齊）。

**Trial_v23 揭 4 議題評估收口**：
- 🔴 #1 真實存在 → 合進
- 🟡 #2 真實存在 → HITL 設計本身重新定義「v5.5 path 開卡時機」直接修
- 🟡 #3 **false positive**（grep verify 揭真實 PipelineView 5 handler 全是 Stage 78c 砍後 placeholder code 0 production risk / Vera review 看 Cody PR 改動 quality 但真實 production code 0 path 觸發）→ **不修 / 紀錄紀律累積**
- 🟡 #4 真實存在 → 合進

### Phase 4 路徑（Trial_v23 後修正）

```
Stage 78a ✅ → 78b ✅ → 78c ✅ → 79 ✅ → Trial_v23 ✅
            → 80（A HITL plan confirmation + Trial_v23 議題收口 / M+）
            → Trial_v24（驗 HITL 業務體驗 + 🔴 #1 hotfix 驗）
            → 81（B 動態 re-planning / L）
            → WebUI Stage（v4 entity drop + Dashboard 重設計）
            → v5.5 完整收口
```

---

## 計劃前 WebSearch 結論（業界 reference — Forge 起草時 reference 用 / 不重複觸發）

**Stage 77 既有 WebSearch 結論 reference**（[Stage_77_Roadmap.md:39](Stage_77_Roadmap.md#L39)）：「**LangGraph interrupt + 4 decision pattern**」業界紀律成熟 — Phase 4 候選 / Stage 80+ 評估時做 / 不重複觸發 WebSearch。

**4 decision pattern 對齊既有 InteractionType 業界內化做法**：
- `approve` → 繼續 chain dispatch（沿用既有 path）
- `edit` → Christ 給修改意見 → Petra 重 `DecideTalentsWithPlanAsync` 含 edit context
- `reject` → 中斷 + 寫 task_memory + status=cancelled
- `respond` → Christ 給額外指示 → Petra 重 `DecideTalentsWithPlanAsync` 含 respond context

**對齊 AiTeam 既有 InteractionType pattern**（[BossInteraction.cs:13-15](../../src/AiTeam.Data/BossInteraction.cs#L13)）：既有 `ceo_confirm / exec_confirm / kickoff / design / devplan_escalate / split_task_proposal` 等真實 pattern + Stage 80 新加 `plan_confirm` type 對齊既有架構 / 不重寫 InteractionProcessor / InteractionCard 框架。

---

## 子項清單（9 子項 / HITL 主體 5 + Trial_v23 議題收口 4）

### HITL 主體（子項 1-5）

**1. BossInteraction.InteractionType 加 `plan_confirm` type**
   - 既有 schema 0 改動（InteractionType 是 free-form string）/ 對齊 ceo_confirm pattern
   - ContextJson 含 SubtaskPlan JSON（subtask 列表 + dependency 圖 + talent picks）+ PetraSessionId（resume 用）

**2. PetraOrchestratorService 插 HITL pause point**
   - 切位置：[PetraOrchestratorService.cs:95](../../src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L95) `DecideTalentsWithPlanAsync` 完成後 + [line 112](../../src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L112) 自管 chain dispatch 前
   - 新加 `WaitForPlanConfirmationAsync(SubtaskPlan, sessionId, ct)` method — 開 BossInteraction `plan_confirm` 卡 + return null（dispatch chain 不啟動）
   - **flag 守**：`Workflow:UseHITLPlanConfirmation` 為 true 才走 HITL path / false 維持 v5.5 baseline auto dispatch（守 production 0 regression）

**3. PetraOrchestratorService 加 4 decision resume routing**
   - resume 入口沿用既有 [StartAsync:212](../../src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L212) 重入 path（既有 infra / 0 新增 method）
   - 加 `ResumeFromPlanConfirmationAsync(sessionId, decision, contextOverride, ct)` method 路由 4 decision：
     - `approve` → 繼續 chain dispatch（沿用既有 path）
     - `edit` / `respond` → 帶 Christ 文字 → 重 `DecideTalentsWithPlanAsync` 含 override context
     - `reject` → 寫 task_memory `decision/plan-rejected` + petra_session status=cancelled

**4. InteractionCard.razor plan_confirm 卡片 UI**
   - 對齊既有 ceo_confirm / design 卡片設計 pattern（[InteractionCard.razor:32-36](../../src/AiTeam.Dashboard/Components/Pages/Interactions/InteractionCard.razor#L32) MudCardContent + line 86 ConfirmAndRespondAsync + line 103 TextInputAndRespondAsync 既有 4 decision pattern）
   - ContextJson SubtaskPlan 真實 render（subtask 列表 + dependency 圖 + talent picks）
   - 4 個 InteractionActionDto 按鈕：`plan_approve` / `plan_edit`（含 text input）/ `plan_reject` / `plan_respond`（含 text input）

**5. InteractionProcessor 路由擴 plan_confirm 4 decision dispatch**
   - 既有 ceo_confirm / kickoff / design / merge_notify 各自 dispatch / 加 plan_confirm 4 path
   - dispatch 觸發 `PetraOrchestratorService.ResumeFromPlanConfirmationAsync(sessionId, decision, contextOverride, ct)` 重入

### Trial_v23 議題收口（子項 6-9）

**6. 🔴 #1 DashboardAppSettingsService 改 IDbContextFactory.CreateDbContext() pattern**
   - root cause：[DashboardAppSettingsService.cs:7](../../src/AiTeam.Dashboard/Services/DashboardAppSettingsService.cs#L7) ctor primary injection `AppDbContext db` Scoped 共享 instance / Blazor InteractiveServer 多元件並行 OnInitializedAsync 撞同 DbContext → `A second operation was started` → Circuit terminated
   - 修法：ctor signature 改 `IDbContextFactory<AppDbContext> dbFactory` + 3 method（GetAllAsync / GetAsync / UpsertAsync）加 `await using var db = await dbFactory.CreateDbContextAsync(ct)` pattern
   - Program.cs DI registration 加 `AddDbContextFactory<AppDbContext>`（保留既有 `AddDbContext` for 其他 caller）
   - 5 caller 元件 0 直接影響（DI 自動 wire）：QuickCommandCard / PipelineView / SystemSettings / TokenMonitoring / Program.cs Seed

**7. 🟡 #2 v5.5 path 開「確認派工/取消」按鈕重新定義**
   - **HITL 設計本身就修這個**：重新定義「v5.5 path 開卡時機」
     - flag=false：inbox 接收時**不開** confirm 按鈕（純 ack 卡 / 0 按鈕 / 對齊 v5.5 auto dispatch 精神）
     - flag=true：DecideTalentsWithPlanAsync 完成後**才開** plan_confirm 卡（HITL pattern）
   - 修 CommandHandler 或 InteractionCard 的 ack 卡邏輯（grep verify Stage 78c 後 inbox ack 卡開卡點真實位置）

**8. 🟡 #3 PipelineView 缺 catch — false positive 不修**
   - grep verify 揭真實：[PipelineView.razor.cs:205-228](../../src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineView.razor.cs#L205) 5 handler 全是 Stage 78c 砍後 placeholder code（4 行純 `Snackbar.Add("已砍 / 留待 WebUI Stage 重設計")` / `private void` 不是 `async Task` / 0 await / 0 真實 BotService call / 0 production risk）
   - Vera Case A review 揭的 critical 議題是 false positive — Vera 看 Cody PR #386 改動 quality 但真實 production code 0 path 觸發
   - **不修 / 紀錄紀律累積**：「Vera review 並非絕對正確 / Aria gate1 反查 production code 真實狀態紀律」（留 /aria-end 統一升級候選）

**9. 🟡 #4 InteractionCard 深色主題 + ParseDescription string pattern**
   - W1 深色主題硬寫 rgba 視覺失效 → 改 MudBlazor 主題變數（`var(--mud-palette-background-grey)` 背景 + `var(--mud-palette-divider)` border）
   - W2 ParseDescription string pattern 耦合 → BossInteraction 加 `SystemNotes? string` 欄位 + Migration AddColumn nullable / ParseDescription 改讀 SystemNotes（前端 string pattern 耦合移除）+ CommandHandler / Petra 開卡時同步寫入 SystemNotes
   - Migration `Stage80BossInteractionSystemNotes` AddColumn nullable

---

## 設計決策（7 議題拍板）

1. **HITL 插入點切位置** — `DecideTalentsWithPlanAsync` 完成後（vs Step 3 DecideTalents 後 or chain dispatch 中）— 對齊「Christ 看完整 plan + dependency 拍板」業界紀律 + 對齊既有 `DecideTalentsWithPlanAsync` 真實 method 輸出（subtask + dependency + picks 全有）

2. **4 decision pattern 對齊既有 InteractionType pattern** — 不重寫 InteractionProcessor / InteractionCard 框架 / 對齊「修根因 > 補丁」+「自己用爽不重寫」精神

3. **Petra resume 沿用既有 StartAsync re-entry path** — [line 212](../../src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L212) 既有 infra 0 新增 method（Stage 75 PetraInbox 設計時就有重入 path 對齊「session 既有 → 不建新」精神）

4. **AppSetting flag default false 守 v5.5 baseline** — `Workflow:UseHITLPlanConfirmation` 預設 false / Trial_v24 開時切 true → 結案切回 false（對齊 aria-trial-summary skill flag 切回紀律）

5. **🟡 #3 false positive 不修** — 對齊「修根因 > 補丁」精神 / 不為 review noise 動 production code / 紀律累積進 /aria-end

6. **SystemNotes 後端寫入 pattern**（修 #4 W2） — Vera 真實揭的設計議題對齊：後端 source of truth 比前端 string parse 乾淨 / 對齊「修根因 > 補丁」精神

7. **plan_confirm 卡片 UI 對齊既有 ceo_confirm / design 卡片設計 pattern** — 不重寫卡片框架 / 對齊「自己用爽不重寫」+ 既有 4 decision UI helper 真實 fit

---

## 驗收情境

### 場景 A — flag=false / v5.5 baseline 0 regression

1. SQL `UPDATE app_settings SET "Value"='false' WHERE "Key"='Workflow:UseHITLPlanConfirmation'`
2. curl `/internal/reload-cache?scope=all` 帶 X-Api-Key
3. Dashboard 派純文字 prompt（reuse `.tmp/trial_v15_body.json`）
4. **期望**：Petra DecideTalentsWithPlanAsync 完成 → **直接 dispatch chain**（不開 plan_confirm 卡 / Bot log 0 `WaitForPlanConfirmationAsync` fire / 對齊 Trial_v22+v23 baseline）

### 場景 B — flag=true / HITL plan confirmation 開卡

1. SQL `UPDATE app_settings SET "Value"='true' WHERE "Key"='Workflow:UseHITLPlanConfirmation'` + reload-cache
2. Dashboard 派 prompt
3. **期望**：Petra DecideTalentsWithPlanAsync 完成 → **開 BossInteraction `InteractionType=plan_confirm` 卡** / Petra `WaitForPlanConfirmationAsync` fire / chain dispatch 0 啟動 / Dashboard 操作中心顯示卡片含 SubtaskPlan render（subtask 列表 + dependency + talent picks）+ 4 個 InteractionActionDto 按鈕

### 場景 C — Christ 點「核准」(plan_approve)

1. 場景 B 後 / Dashboard 點「核准」
2. **期望**：InteractionProcessor 路由 `plan_approve` → `PetraOrchestratorService.ResumeFromPlanConfirmationAsync(sessionId, "approve", null, ct)` fire / chain dispatch 啟動 / Cody → Vera → Quinn chain 真實跑（對齊 Trial_v22+v23 baseline）

### 場景 D — Christ 點「修改」(plan_edit)

1. 場景 B 後 / Dashboard 點「修改」+ 輸入「Cody 只做 backend，UI 留 Vera 處理」
2. **期望**：InteractionProcessor 路由 `plan_edit` → `ResumeFromPlanConfirmationAsync(sessionId, "edit", "Cody 只做 backend...", ct)` fire / Petra 重 `DecideTalentsWithPlanAsync` 含 edit context / **新 plan_confirm 卡開**等再次確認（loop until approve / reject）

### 場景 E — Christ 點「拒絕」(plan_reject)

1. 場景 B 後 / Dashboard 點「拒絕」
2. **期望**：`ResumeFromPlanConfirmationAsync(sessionId, "reject", null, ct)` fire / task_memory `decision/plan-rejected` 寫入 / petra_session status=cancelled / chain dispatch 0 啟動

### 場景 F — Christ 點「補充」(plan_respond)

1. 場景 B 後 / Dashboard 點「補充」+ 輸入「另外考慮 mobile responsive」
2. **期望**：`ResumeFromPlanConfirmationAsync(sessionId, "respond", "另外考慮 mobile...", ct)` fire / Petra 重 `DecideTalentsWithPlanAsync` 含 respond context / 新 plan_confirm 卡開（loop until approve / reject）

### 場景 G — 🔴 #1 DbContext concurrency hotfix verify

1. Dashboard 打開首頁 `localhost:5051/`
2. **期望**：0 Circuit terminated / 0 `A second operation was started on this context instance` exception / 首頁 Home + QuickCommandCard 並行 init 全綠 / 對齊 Vera Case A review 揭的同類根因紀律

### 場景 H — 🟡 #4 InteractionCard SystemNotes + 深色主題 verify

1. Christ 派任意 prompt → 開 plan_confirm 卡（場景 B）
2. **期望**：
   - SQL `boss_interactions` 新 row 含 `SystemNotes` 真實寫入「[v5.5] Task 已接收 (inbox=...) — Petra 將依 FIFO 順序拆解派工」（或類似系統通知）/ Description 純 Victoria prompt 內容
   - InteractionCard render：SystemNotes 獨立淡灰底 + info icon 區隔 / 深色主題切換時視覺仍可辨識（MudBlazor 主題變數對齊）
   - ParseDescription 不再 string pattern parse（前端 0 `[v` 開頭判斷 / 直接讀 SystemNotes 欄位）

---

## Aria 預警

### W1 — HITL 插入點切位置紀律

`DecideTalentsWithPlanAsync` 完成後是業界 best practice 切點（Christ 看完整 plan + dependency）— **不切過早**（Step 3 DecideTalents 後 / Christ 看不到 dependency）/ **不切過晚**（chain dispatch 中 / Cody 已開工沒法拒絕）。Forge 切位置時對齊本紀律。

### W2 — flag default false 守 v5.5 baseline + Trial_v24 後切回紀律

`Workflow:UseHITLPlanConfirmation` 預設 false（守 production 0 regression）/ Trial_v24 開時切 true → 結案切回 false（對齊 aria-trial-summary skill flag 切回紀律 / workflow_aria.md 第三節 A 第 10 條）。

### W3 — SystemNotes Migration nullable + ParseDescription fallback 紀律

BossInteraction 加 SystemNotes? string nullable column + Migration AddColumn / 既有 boss_interactions row SystemNotes=null 不擾既有 UI 渲染 / ParseDescription 改讀 SystemNotes 後 fallback 對 null 處理（沒 SystemNotes 就純 render Description）。

### W4 — 🟡 #3 PipelineView 5 handler false positive 紀律累積

Vera Case A review 揭 critical 議題 grep verify 後揭真實是 false positive（Stage 78c 砍後 placeholder code 0 production risk）— **不修 / 紀律累積**：「Aria gate1 Tier 0 反查 Vera/Cody review 對 production code 真實狀態紀律」候選 / 留 /aria-end 統一升級。

### W5 — DashboardAppSettingsService IDbContextFactory 改後 caller 影響評估

5 caller 元件 0 直接影響（DI 自動 wire / 元件 inject DashboardAppSettingsService 不變）/ Program.cs DI lifecycle 對齊（AddDbContextFactory 跟既有 AddDbContext 並存 / 兩個 method 都可用）。

---

## 實作紀錄（v2.0 / 2026-05-20）

### 實作完成項目

**HITL 主體（5 子項）**：
1. ✅ **`plan_confirm` InteractionType** + Icon/Color/Label 加入 [InteractionCenter.razor.cs](../../src/AiTeam.Dashboard/Components/Pages/Interactions/InteractionCenter.razor.cs) 三個 switch + `PlanConfirmActionsJson` 4 button 常數加入 [InteractionService.cs](../../src/AiTeam.Bot/Services/InteractionService.cs:84)
2. ✅ **HITL pause point** 插在 [PetraOrchestratorService.cs:111-124](../../src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L111)（`DecideTalentsWithPlanAsync` 完成後 / `DispatchTalentsAsync` 前）+ `WaitForPlanConfirmationAsync` 開 BossInteraction + `sessionRepo.PauseAsync` + return `PetraOrchestratorResult.Paused`
3. ✅ **4 decision resume routing** — `ResumeFromPlanConfirmationAsync(sessionId, decision, contextOverride, ct)` + 3 子 method（`ResumeApproveAsync` / `ResumeEditOrRespondAsync` / `ResumeRejectAsync`）+ `DispatchAndFinalizeAsync` helper（從 StartAsync 抽出收尾段供 approve resume + 既有 StartAsync 共用）
4. ✅ **InteractionCard.razor plan_confirm UI** — SubtaskPlan render（subtask 列表 + dependency 圖 + 附圖 chip + talent picks）+ 4 button（edit/respond 走 TextInputDialog）+ ContextJson 透過 BossInteractionDto 暴露前端 + JsonDocument 淺解避免雙端 type linking
5. ✅ **新建 PlanConfirmationProcessor BackgroundService**（取代 Stage 78c 砍掉的 InteractionProcessor）— 3s polling responded plan_confirm + `ProcessedByBot` 原子標 + IServiceScope per row + ResumeFromPlanConfirmationAsync dispatch

**Trial_v23 議題收口（4 子項）**：
- 🔴 #1 ✅ [DashboardAppSettingsService.cs](../../src/AiTeam.Dashboard/Services/DashboardAppSettingsService.cs) ctor → `IDbContextFactory<AppDbContext>` + 3 method `await using var db = await dbFactory.CreateDbContextAsync(ct)` + Program.cs `AddDbContextFactory<AppDbContext>` 並存註冊
- 🟡 #2 ✅ [CommandHandler.cs](../../src/AiTeam.Bot/Discord/CommandHandler.cs) 兩個 v5.5 path 改 `EmptyActionsJson`（0 按鈕純 ack 卡）+ description = userInput / `BuildCeoConfirmDescription` 整 method 砍（0 caller）
- 🟡 #3 ✅ **不修紀錄** — grep verify [PipelineView.razor.cs:205-228](../../src/AiTeam.Dashboard/Components/Pages/Tasks/PipelineView.razor.cs#L205) 5 handler 真實是 Stage 78c 砍後 placeholder（4 行 `Snackbar.Add`），對齊「Vera review 並非絕對正確 / production code 真實狀態反查」紀律候選
- 🟡 #4 ✅ `BossInteraction.SystemNotes?` 加欄位 + Migration `Stage80BossInteractionSystemNotes`（AddColumn nullable + AppSetting `Workflow:UseHITLPlanConfirmation` seed default false）+ BossInteractionDto + DashboardTaskService MapToDto + CommandHandler 寫入 `ceoResponse.Reply` + InteractionCard 獨立區塊（`var(--mud-palette-background-grey)` 背景 + `var(--mud-palette-divider)` border / 深色主題友善）

**版本 bump**：`src/Directory.Build.props` 3.71.0 → 3.72.0。

### 關鍵設計決策

1. **`PlanConfirmationProcessor` 新 BackgroundService（Forge spike 偏離 plan）** — Roadmap §5 寫「InteractionProcessor 路由擴」但 Stage 78c 已砍 InteractionProcessor。Forge 對齊 Roadmap 設計意圖（4 decision dispatch / 不是要復活 InteractionProcessor 框架）新建專責 BackgroundService。對齊既有 `PetraInboxProcessor` 3s polling + IServiceScope per row 紀律。

2. **`PetraSession.Status="paused"` + 「重啟重跑」紀律對齊** — `PetraSessionRecoveryService` 只掃 `running` session（不掃 paused）→ Bot 重啟期 paused session 不被誤救 + plan_confirm BossInteraction 仍在 DB / 0 漏單 / 等 PlanConfirmationProcessor 拉起 resume。

3. **approve path 從 ContextJson 還原 SubtaskPlan + Talents 而非重 call Petra LLM** — Christ 已 approve 的 plan 必須沿用 / 不能 Petra 重 decide（避免漂移）/ 0 額外 LLM cost。

4. **edit/respond path 同 session 內 redecide（不開新 PetraSession）** — append `[plan_confirm edit/respond]` user message 進 session messages 維持 audit trail / loop until approve/reject 對齊業界 LangGraph interrupt 慣例。

5. **`SystemNotes` 後端 SoT 取代前端 ParseDescription** — Description = 純 Christ 任務 / SystemNotes = 系統提示。前端 0 string pattern 耦合 / 未來擴展系統提示 0 schema migration。

6. **MockMode auto-approve 對 plan_confirm 預設不處理（fallback `ack` 觸發 skip）** — 避免 MockMode 自動干擾 4 decision 手動測試 / production 0 影響（MockMode=false 才是 production）。Follow-up 候選留 Future_Feature.md 純 Mock 自驗工具增強。

### 驗收結果（Forge self-verify 2026-05-20）

| 場景 | 結果 | 重點驗證 |
|---|---|---|
| **G** 🔴 #1 DbContext concurrency | ✅ | 5 並行 Home GET → 0 `second operation` / 0 `Circuit terminated` |
| **A** flag=false baseline | ✅ | 0 `WaitForPlanConfirmationAsync` fire / 0 plan_confirm 卡 / 真實 task 跑通 Cody dispatch → 完成 |
| **B** flag=true HITL 開卡 | ✅ | `Stage 80：HITL plan_confirm 閘門 fire` log + BossInteraction + SystemNotes + session=paused |
| **C** plan_approve | ✅ | ResumeApproveAsync → DispatchAndFinalize → Cody dispatch → session paused→running→done |
| **D** plan_edit | ✅ | redecide 完成 + 新 plan_confirm 卡開（**2 plan_confirms for same session**）/ session 仍 paused |
| **E** plan_reject | ✅ | `task_memory.decision/plan-rejected` 寫入 + session paused→cancelled |
| **F** plan_respond | ✅ | redecide 完成 同 edit path 對齊 |
| **H** 🟡 #4 SystemNotes 後端 | ✅ | `ceo_confirm` row: Description=純 Christ 任務 / SystemNotes="[v5.5] Task 已接收..." 分離 |

**本機驗證**：`dotnet build AiTeam.slnx` 0 Error / `dotnet test`（non-Playwright）229 pass（102 Bot + 127 Generated）。

**Christ 視覺驗收項目（無法 Forge 自驗）**：
- Dashboard 操作中心 plan_confirm 卡 UI render（SubtaskPlan 列表 + dependency 圖 + 4 button）
- 深色主題 SystemNotes 區塊視覺辨識（MudBlazor 主題變數對齊）
- 真實業務 task 端對端流暢度 → 留 **Trial_v24** 真實驗

### Mock 覆蓋情況

**全自驗用 MockMode=true 跑**：MockLlmProvider 對 Petra DecideTalentsWithPlanAsync 回固定 JSON → SubtaskPlanParser 解析失敗 → fallback `Linear[code_implementation]` 1 subtask → 走 Cody dispatch。MockClaudeCodeService 對 Worker dispatch 回 mock 文字。**0 API cost / 0 burn AiTeam 餘額**（對齊 Stage 80 規劃書「0 燒 / Trial_v24 才燒」紀律）。

驗收完成後 flag 切回 baseline：`Workflow:UseHITLPlanConfirmation=false` + `MockMode=false` + `/internal/reload-cache` 套用（對齊 aria-trial-summary skill flag 切回紀律）。

### 踩坑紀錄

1. **`dotnet ef migrations add --no-build` 用 stale DLL** — 第一次跑 EF tools 加 Migration 但 `--no-build` 用了未含 SystemNotes 的 stale DLL → 產出空 Migration + snapshot 也被 stale 覆蓋。**修法**：`git checkout` 還原 Stage79 + 手動寫 Migration .cs + Python 腳本從更新後 `AppDbContextModelSnapshot.cs` 產 Designer.cs（替換 class header + `BuildModel` → `BuildTargetModel`）。**紀律**：`dotnet ef migrations add` 不要用 `--no-build`（避免 stale assembly 干擾 model snapshot）。

2. **Roadmap §5 寫「InteractionProcessor 路由擴」但 Stage 78c 已砍** — 規劃前 Aria 漏掃 Stage 78c 砍範圍（InteractionProcessor 整檔砍）。Forge spike 路線 D 偏離 plan 對齊設計意圖新建 `PlanConfirmationProcessor`。**紀律候選**：規劃前 grep verify 既有 class 真實存在 / Stage 78c 砍範圍對 Stage 80+ 後續 Stage 影響面評估（留 /aria-end 統一升級）。

3. **`Internal API /internal/ceo/command` JSON body 必含 lowercase `text` field** — ASP.NET Core 預設 CamelCase JSON binding / 試 `"text"` 通 / `"Text"` 不通。**紀律延伸**：API 自驗試 JSON body 必確認 binding policy（不憑 record positional param PascalCase 印象寫）。

4. **MockMode auto-approve fallback `ack` 對 plan_confirm 無效** — InteractionService.CreateInteractionAsync auto-approve switch 缺 `plan_confirm` case → 落到 `_ => "ack"` → PlanConfirmationProcessor `MapActionToDecision("ack")` 回 null → log warning + skip dispatch（resilience verified）。自驗用 SQL 手動 UPDATE ResponseAction 覆蓋。production 0 影響 / Trial_v24 0 影響（真實 LLM + Christ 真實點按鈕）。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v1.0 | 2026-05-20 | Stage 80 規劃書建立（Aria 撰寫 / Trial_v23 結案後即進）。**核心結構**：HITL 主體（5 子項 — InteractionType `plan_confirm` + Petra pause/resume + 4 decision routing + InteractionCard UI + InteractionProcessor 路由）+ Trial_v23 議題收口（4 子項 — 🔴 #1 DbContext concurrency hotfix + 🟡 #2 v5.5 confirm 按鈕重新定義 + 🟡 #3 false positive 不修紀錄 + 🟡 #4 InteractionCard 設計 issue）+ 7 設計決策拍板 + 8 驗收情境 + 5 Aria 預警。**Effort baseline Opus 1M + high**（M+ 規模 / 對齊自省點 #39 反向校準紀律不慣性推 Extra high）。**0 WebSearch 觸發**（Stage 77 既有結論 reference + 0 third-party framework 真實使用 / 純內部 business logic 設計）。**規劃前 grep verify 完整**（BossInteraction.cs / PetraOrchestratorService.cs / DashboardAppSettingsService.cs / PipelineView.razor.cs / InteractionCard.razor 真實狀態 verify）。**Migration**：Stage80BossInteractionSystemNotes（AddColumn nullable）。**AppSetting**：Workflow:UseHITLPlanConfirmation default false。 |
| v2.0 | 2026-05-20 | Stage 80 實作完成 + Forge self-verify 全 8 場景 PASS（A/B/C/D/E/F/G + H 後端）+ flag 切回 baseline。**commit `958ad6e`**。**v3.71.0 → v3.72.0**。**Forge spike 偏離 plan 1 點**：Roadmap §5「InteractionProcessor 路由擴」對齊 Stage 78c 已砍真實 → 新建 `PlanConfirmationProcessor` BackgroundService 達成同等設計意圖（4 decision dispatch / 不復活整套框架）。**Migration**：`Stage80BossInteractionSystemNotes` AddColumn nullable + AppSetting `Workflow:UseHITLPlanConfirmation` seed default false。**自驗 0 API cost**（MockMode + MockLlmProvider fallback Linear[code_implementation] / 對齊「0 燒 / Trial_v24 才燒」紀律）。**4 踩坑紀錄**：① `dotnet ef migrations add --no-build` stale DLL → 手動寫 Migration + Python 產 Designer.cs ② Roadmap §5 規劃漏掃 Stage 78c 砍範圍（Aria 紀律候選）③ Internal API JSON body lowercase `text` field ④ MockMode auto-approve fallback `ack` 對 plan_confirm 無效（不阻塞 / Future_Feature 候選）。**Christ 視覺驗收項目** 留 Trial_v24：plan_confirm 卡 UI render + 深色主題 SystemNotes 視覺辨識 + 端對端業務體驗。 |
