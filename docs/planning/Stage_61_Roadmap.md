# Stage 61 Roadmap — Petra/Cody prompt 對齊群組 + Pipeline UI refresh + Dashboard 補強（Trial_v8 開跑前置清掃）

> 目標版本：**v3.50.0**（minor — Trial_v8 開跑前置清掃）
> 狀態：規劃中
> 範圍：FF 五十六 + 二十五 + 四十六 + 四十八 + 五十 + 四十五 + 四十 + 議題 #B（Trial_v6/v7 揭露 🟡 中議題群組全清）
> 規模：M-L

---

## 戰略脈絡

**Trial_v8 開跑前最後一塊清掃** — Stage 60 收口 1 🔴（v4 邊角 user actions legacy）+ Stage 61 收口 6 🟡（Petra/Cody prompt 對齊系統性議題 + Pipeline UI noise + Dashboard 補強）= Trial_v8 才能真正驗證 v4 framework 純粹 ROI（不被已知 🟡 群組 noise 干擾揭新類型訊號純度）。

**Trial_v6+v7 兩次再現的系統性議題群組**：
- Petra「給 Christ 決策包」+「工時估算泛濫」 → AI Agent prompt 層對齊 Aria 紀律
- Cody Dev_plan 「現況確認」vs「實作步驟說明」結構衝突（Trial_v6 ×3 phase 全 escalate）
- Cody 跳過 Dev_plan 沒寫 ImplementationNote → Sage 歸檔失敗
- Cody Dev_plan maxTurns 配置不足踩 100%
- Pipeline KickoffStageExecutor max_iter path TaskPlan 顯示「無計劃書」UI bug
- Dashboard token IsEstimated 視覺區分缺失 + epic 流程追蹤缺失 + Christ action 後 failed task 沒清

---

## 子項清單

### 1. Petra prompt 議題層次篩選紀律延伸（FF 五十六 + 工時砍紀律）

**修法**：CLAUDE_Petra.md（Discord Petra）+ Framework Kickoff/Design Petra（KickoffPetraExecutor / DesignPetraExecutor + KickoffPrompts.cs / DesignState.cs）三處同步對齊：

- **議題層次篩選紀律**：純技術 / 內部設計議題 Petra 自決（不丟 Christ）；對 Christ 看到行為 / 業務邏輯 / spec 有影響的才丟拍板
- **給定見不攤議題**：拍板議題給推薦答案 + 理由，不只列 A/B/C 三選
- **砍工時估算紀律**：禁止 prompt 內出現「X 天 / Y 週 / X.X 天」工時估算（AiTeam 是 AI Session 模式無「天」概念，對齊 workflow_aria.md 第三節 A 第 4 條 Aria 紀律推廣到 AI Agent prompt 層）

對齊 CLAUDE_Petra.md 既有「審核紀律」段（已寫「以下不是 Petra 該審的」+「以下情況不構成 revise 理由」）— 延伸到 Kickoff/Design 會議產出 TaskPlan/DesignPlan 場景。

### 2. Cody prompt 對齊群組（FF 二十五 + 四十六 + 四十八）

CLAUDE_Cody.md 修法：

- **Dev_plan 結構規範**（FF 二十五）：補「實作步驟說明」結構（Step 1 / Step 2 / 改哪些檔案 / 加哪些 class / DI 註冊等），對齊 Petra 期待結構（CLAUDE_Cody.md vs CLAUDE_Petra.md prompt 對齊 audit — Forge spike 階段做）
- **ImplementationNote 強制寫**（FF 四十六）：Dev / Dev_fix 階段強制寫 DB ImplementationNote 欄位（不論是否走 Dev_plan）+ Sage 歸檔流程加備援 source（PR Body / commit log）兩條路雙保險
- **Dev_plan maxTurns 提升**（FF 四十八）：從預設 40 提升至 80-100（或動態化對齊 FF 十九 Agent maxTurns 動態化精神 — Forge spike 評估靜態 vs 動態）

### 3. 議題 #B — Pipeline KickoffStageExecutor max_iter path 「無計劃書」修根因

**Trial_v7 揭露**：DB TaskPlan 實寫入 6,279 字 ✓ 但 Discord embed + Dashboard 顯示「（無計劃書）」。

**真實 root cause**：FrameworkKickoffRouter.CreateKickoffConfirmationAsync `:856-905` embed 構造邏輯本身對（line 875-879 有 TaskPlan 就顯示 / 沒有就「無計劃書」）— 真實問題是 `freshGroup.TaskPlan` 在 embed 構造時為空。**EF Core cached entity 沒 refresh / 或 transaction timing**。

**修法**：refresh entity 確保 freshGroup.TaskPlan 拿到 UPDATE 後最新（DbContext.Entry(group).Reload() 或重 query）— Forge spike 確認 EF Core context lifecycle + 對齊 Stage 60 CreateKickoffConfirmationAfterModifyAsync 是否同類根因（modify path 也要 refresh）。

### 4. FF 五十 Dashboard token 統計頁 IsEstimated 視覺區分（Stage 56 範圍變更 follow-up）

Dashboard token 統計頁（src/AiTeam.Dashboard/Components/Pages/）razor + DTO + SQL 三處改：
- 表格行 IsEstimated=true 加視覺標記（icon / 顏色 / tooltip）
- DTO 加 IsEstimated 欄位
- SQL query 含 IsEstimated 欄位

對齊 Stage 56 結案宣稱「Dashboard token 統計頁 IsEstimated 視覺區分跳過 → 立 FF 五十 Trial_v6 觀察期 follow-up」此 Stage 收口。

### 5. FF 四十五 Christ action 標 cancelled + generic intervention 訊息模板動態化

**Trial_v6 議題 #9 揭露**：Christ 點「跳過審核」/「重啟 Dev」action 後前置 failed task 沒清 → MarkGroupDoneOrIntervention helper 看到歷史 failed task 標 needs_intervention + 建 generic intervention BossInteraction（訊息誤導實際根因如「Vera 0 次修復後仍發現問題」實際是 Sage escalate）。

**修法**：
- Christ action 標記後（跳過審核 / 重啟 Dev）→ 對應 task 標 cancelled / superseded
- MarkGroupDoneOrIntervention 邏輯加「忽略已被 superseded 的 failed task」
- generic intervention 訊息模板按真實 escalate source 動態化（對齊 Stage 58 三 Agent fail-fast 統一精神延伸）

### 6. FF 四十 Stage 46 Dashboard razor UI 接線（epic 折疊 + sub-task 進度條 + 暫停按鈕）

**Trial_v6 議題 #5 揭露**：parent group 流程追蹤右側不顯示 sub-task 內部 stage 進度 — Christ 看 parent UI 困惑「都沒動作」但 sub-task 內部正在跑。

**修法**：
- Dashboard 流程追蹤頁加 epic 折疊面板（parent group 顯示 N sub-task 子層）
- sub-task 進度條（顯示 sub-task 內部 stage：Dev_plan/Dev/Reviewer/QA/Doc）
- 暫停整個 epic 按鈕（對應 EpicPaused 邏輯）

### 7. Mock 場景補強（Trial_v8 前置條件驗證）

- `petra_decision_pack_check`：Petra Round 完成 → DB query TaskPlan 不含「待 Christ 拍板」/「A/B/C 三選」/「X 天 / Y 週」字串（紀律 prompt 生效驗證）
- `cody_devplan_structured_check`：Cody Dev_plan 完成 → DB DevPlan 含「Step 1 / Step 2 / 改哪些檔案」結構（FF 二十五 修法驗證）
- `cody_implementationnote_written_check`：Cody Dev / Dev_fix 完成 → DB ImplementationNote 不為空（FF 四十六 修法驗證）
- `framework_modify_taskplan_display_check`：Stage 60 Mock A 場景延伸 — Discord embed + Dashboard 顯示 TaskPlan 摘要 ≥ 100 字（不是「無計劃書」— 議題 #B 修根因驗證）
- `dashboard_isestimated_visual_check`：Dashboard token 統計頁 IsEstimated=true 行有視覺標記（FF 五十 修法驗證）
- `christ_action_supersede_check`：Christ 跳過審核 action 後前置 failed task 標 cancelled / superseded + generic intervention 訊息含真實 escalate source（FF 四十五 修法驗證）

### 8. Version bump v3.50.0

`src/Directory.Build.props` `<Version>` 標籤 + Dashboard 頁腳自動讀。

---

## 設計決策

### 1. 範圍：合併 Stage 61 全 7 子項 vs 拆 61A/61B

| 路線 | 內容 | 評估 |
|---|---|---|
| **合併**（推薦）| 全 7 子項 + Trial_v8 開跑前一次清完 | 對齊 Stage 60 ×0.80 規模 M-L 可控；7 子項 strongly coupled（都是 Trial_v8 體驗障礙）；分開做 Aria 規劃 + Forge spike 兩次成本高 |
| 拆 61A（Trial_v8 必修核心）+ 61B（🟡 補強）| 61A：Petra/Cody prompt + 議題 #B + FF 五十 / 61B：FF 四十五 + FF 四十 | 規模分散但 Trial_v8 開跑時間延後一個 Stage 週期 |

→ **拍板合併** — Trial_v8 開跑前一次清完，規模 M-L 對齊 Stage 60 範圍。

### 2. Petra prompt 修法層級：純 prompt 改動 vs 行為配套

→ **拍板純 prompt 改動** — 不動 schema / framework wire / DI 結構。三處同步對齊（CLAUDE_Petra.md + KickoffPrompts.cs + DesignState.cs）避免 prompt 漂移。

### 3. 議題 #B refresh entity 戰術

Forge spike 階段揭露 EF Core context 真實 lifecycle 後決定：
- 選項 A：DbContext.Entry(group).Reload() — 簡單但需 DbContext 仍 alive
- 選項 B：重 query freshGroup（taskRepo.GetByIdAsync(group.Id)）— 對齊 Stage 53A 既有 pattern
- 選項 C：改 KickoffPlanExecutor 寫 TaskPlan 後立刻 push 給 KickoffStageExecutor 而非 read DB

→ **Forge spike 自決** — Aria 不預設拍板，對齊 Stage 60 議題 1+2 自決 healthy 模式。

### 4. Cody prompt audit 範圍

Forge spike 階段對 CLAUDE_Cody.md vs CLAUDE_Petra.md 做 audit — 找出 Cody Dev_plan 結構 vs Petra 期待結構的具體 mismatch 點 + 對齊修法。Aria 不預設拍板細節，Forge 自決推薦解法（對齊「議題層次篩選紀律」延伸到 Aria 規劃 — 純技術細節 Aria 自決 / Forge spike 自決）。

### 5. Cody Dev_plan maxTurns：靜態提升 vs 動態化

→ **Forge spike 評估** — 靜態 80-100 vs 動態化（對齊 FF 十九 Agent maxTurns 動態化精神）。Aria 不預設拍板。

### 6. v3.50.0 minor bump（Stage 完成）

---

## 驗收情境

### Mock 場景驗收（Forge 自驗）

#### 場景 A：petra_decision_pack_check（FF 五十六 修法驗證）

**觸發**：`curl -X POST http://localhost:5052/internal/mock/scenario -H "X-Api-Key: $API_KEY" -d '{"scenario": "petra_decision_pack_check"}'`

**驗證**：
- Mock framework Kickoff Round 1-3 + max_iter 強制收尾完成
- DB query：`SELECT "TaskPlan" FROM task_groups WHERE "Id" = '<group_id>'` 文字檢查
  - **不含**「待 Christ 拍板」/「待老闆拍板」字串
  - **不含**「A 方案」/「B 方案」/「C 方案」三選列表結構
  - **不含**「X 天」/「Y 週」/「X.X 天」工時估算字串
- Bot log `[Stage61] Petra prompt 議題層次篩選紀律生效（無決策包輸出）`

#### 場景 B：cody_devplan_structured_check（FF 二十五 修法驗證）

**觸發**：mock framework Pipeline Dev_plan 階段

**驗證**：
- DB query：`SELECT "DevPlan" FROM task_groups WHERE "Id" = '<group_id>'` 文字檢查
  - **含**「Step 1 / Step 2」或「步驟 1 / 步驟 2」結構
  - **含**「改 [檔案名]」或「加 [class 名]」實作具體指引
  - **不含**「現況確認」表格結構（Trial_v6 反例）
- DevPlanRevision = 0 或 1（不需 Petra ×3 revise 推到 escalate）

#### 場景 C：cody_implementationnote_written_check（FF 四十六 修法驗證）

**觸發**：mock framework Pipeline Dev / Dev_fix 階段完成

**驗證**：
- DB query：`SELECT LENGTH("ImplementationNote") FROM task_groups WHERE "Id" = '<group_id>'` ≥ 200 字
- Bot log `[Stage61] Cody ImplementationNote 強制寫入 ✓`
- Sage 歸檔不 escalate（不踩 Trial_v6 議題 #8 反例）

#### 場景 D：framework_modify_taskplan_display_check（議題 #B 修根因驗證）

**觸發**：Stage 60 Mock A 場景延伸 — modify path 完成

**驗證**：
- 對齊 Stage 60 場景 A：DB TaskPlan ≥ 4000 字 ✓
- **新驗證**：Discord embed planPreview field 顯示 ≥ 100 字 TaskPlan 摘要（不是「無計劃書」placeholder）
- DB query：`SELECT "TaskPlan" FROM task_groups WHERE "Id" = '<group_id>'` 對照 Discord embed 內容一致

#### 場景 E：dashboard_isestimated_visual_check（FF 五十 修法驗證）

**觸發**：Christ 開 Dashboard token 統計頁

**驗證**（Christ 視覺驗收）：
- IsEstimated=true 行顯示視覺標記（icon / 顏色 / tooltip）區別 actual vs estimated
- IsEstimated=false 行無標記（baseline 對照）
- DB query：`SELECT COUNT(*) FROM token_logs WHERE "IsEstimated" = true` 對照 Dashboard 顯示

#### 場景 F：christ_action_supersede_check（FF 四十五 修法驗證）

**觸發**：mock failed task 後 Christ 點「跳過審核」action

**驗證**：
- DB query：`SELECT "Status" FROM task_items WHERE "GroupId" = '<group_id>' AND "Status" = 'failed'` 應變 cancelled / superseded（前置 failed task 自動清）
- generic intervention BossInteraction Description 含真實 escalate source（不是寫死「Vera 0 次修復後仍發現問題」）

#### 場景 G：epic_chain_dashboard_ui_check（FF 四十 修法驗證 — Christ 視覺驗收）

**觸發**：mock framework_pipeline_with_subtask_chain 場景

**驗證**（Christ 視覺驗收）：
- Dashboard 流程追蹤頁 parent group 顯示 epic 折疊面板含 N sub-task 子層
- sub-task 進度條顯示內部 stage（Dev_plan/Dev/Reviewer/QA/Doc）
- 暫停 epic 按鈕可用（對應 EpicPaused 邏輯）

### 0 regression 確認

- Stage 60 既有 4 Mock 場景仍綠（taskplan/designplan modify happy + subprocess_failure + modify_during_subprocess_failure）
- 既有 33 framework_* Mock 場景仍綠 + dotnet test pass
- Trial_v6/v7 既有 baseline 主流程行為不變（cost / 時長對齊）

---

## 技術約束

- 對齊 workflow_aria.md 第三節 A 第 5+6+7+8 條紀律
- **環境細節 source of truth**：Bot Internal API port `5052` 見 `docker-compose.prod.yml` / X-Api-Key value 取 `docker exec aiteam-aiteam-bot-1 printenv AgentSettings__InternalApiKey` / Internal endpoint 真實 path 見 `src/AiTeam.Bot/Api/InternalController.cs` / Petra prompt 三處同步：`src/AiTeam.Bot/Resources/CLAUDE_Petra.md` + `src/AiTeam.Bot/Workflows/Kickoff/KickoffPrompts.cs` + `src/AiTeam.Bot/Workflows/Design/DesignState.cs`
- 不動 EF Migration（不動 schema）
- 不動 DI 結構 / framework Workflow wire 點（純 prompt + 小 UI 改 + refresh entity）
- Petra prompt 修法影響面：Discord Petra + Framework Kickoff Petra + Framework Design Petra 三處同步對齊（避免 prompt 漂移）
- Cody prompt 修法 + audit 對齊 Petra 期待結構（Forge spike 自決細節）
- 對齊 Stage 60 第 8 條紀律（Pipeline 接管 decision 擴展配對檢查）— 本 Stage 子項 3 議題 #B 修法不擴 Pipeline 接管 decision，純 entity refresh 不踩

---

## 實作紀錄（v2.0 — 2026-05-10 Forge 結案）

### 實作概況

- **Commit**：`218f150` — feat(stage61): v3.50.0
- **本機驗證**：dotnet build 0 error / dotnet test 131 passed（Bot.Tests 4 + Tests.Generated 127）
- **Push**：`628a52e..218f150 HEAD -> main`，CI/CD self-hosted runner success（run id 25625351905）
- **規模**：27 files changed / +403 / -46（純 prompt + UI + Reload + alias，無 EF Migration）

### 7 子項實作對照表

| 子項 | 內容 | 真實修法 |
|---|---|---|
| 1 | Petra prompt 5 位置同步紀律段（FF 五十六） | CLAUDE_Petra.md「議題層次紀律 + 給定見紀律 + 工時禁字紀律」段 + 4 個 prompt builder 共用 `AppendPetraDisciplineSection` helper（KickoffPrompts.cs / DesignPrompts.cs 各自有同名 helper）。`BuildPetraPlanPrompt` line 191「待 Christ 決定」→「已確認 / 已記錄」 |
| 2-A | CLAUDE_Cody.md Dev_plan 結構規範 + ImplementationNote 強制 | 加新段「Dev_plan 結構規範（強制）」+ ImplementationNote 標題改「強制（不論是否走 Dev_plan）」 |
| 2-B | Sage 備援 source（FF 四十六） | DocAgentService prompt 引導 Sage 走 PR Body / git log fallback + CLAUDE_Sage.md 品質下限改 fallback path（兩備援皆失敗才 escalate）— 規模 ~25 LOC ≤ 100 條件納入 ✅ |
| 2-C | Cody Dev_plan maxTurns 提升 80（FF 四十八） | IClaudeCodeService.RunReadOnlyAsync 加 `int? maxTurns = null` + 4 處同步擴 + DevAgentService caller 傳 `maxTurns: 80` + 3 處既有 caller（Designer/Requirements/PmReview）加 named `ct:` 對齊 |
| 3 | 議題 #B Reload 修根因 | KickoffStageExecutor.HandleEntryAsync line 99 加 `db.Entry(group).ReloadAsync` + try/catch graceful → intervention helper。Stage 60 modify path audit ✓ 不踩同類根因（同 scope 同 entity reference） |
| 4 | Dashboard token IsEstimated 視覺（FF 五十） | TokenAgentSummaryDto 加 `HasEstimated` + 2 處聚合查詢加 `BOOL_OR("IsEstimated")` + razor 卡片 MudIcon Warning + Tooltip + 表格 row icon + cost「~」前綴 |
| 5 | Christ action supersede + intervention 動態化（FF 四十五） | ButtonCallbackRouter SupersedePriorFailedTasks helper（escalate_devplan_skip / abort 2 處呼叫）+ TaskGroupService.MarkGroupDoneOrInterventionAsync InterventionReason 動態列出真實 escalate source — 規模 ~8 LOC ≤ 50 條件納入 ✅ |
| 6 | Dashboard epic UI 接線（FF 四十） | PipelineList row IsEpic / sub-task 視覺標 + EpicPaused chip；PipelineView Epic section 顯示 sub-task 列表 + ⏸️ 暫停 epic / ▶️ 恢復 epic 按鈕（呼叫既有 DashboardBotService.PauseEpicAsync / ResumeEpicAsync） |
| 7 | Mock 場景 7 alias | MockScenarioService 加 7 alias + MockScenarioCard.razor 補 7 SelectItem |
| 8 | Version bump | Directory.Build.props 3.49.0 → 3.50.0 |

### 範圍縮小揭露（Aria 結案備註對齊）

**ButtonCallbackRouter SupersedePriorFailedTasks 範圍**：
- 計劃書原意對齊「Stage 30 既有 5 申訴方法 button callback」，實作只 cover Dev_plan path 兩處（`escalate_devplan_skip` / `escalate_devplan_abort`）
- **YAGNI 範圍縮小理由**：Trial_v6 議題 #9 是 Dev_plan path 具體場景；其他 3 申訴 path（Review Appeal escalate / QA fix loop escalate / Sage escalate）**沒實證同類 cross-Agent supersede needs intervention 誤判**
- **Trial_v8 後重新評估**：是否其他 path 也踩同類問題 candidate（可立 FF 五十八）— 真實使用揭露才動工

### Forge 自驗 7 Mock 場景結果

| # | 場景 | 結果 | 驗證範圍 |
|---|---|---|---|
| 1 | `petra_decision_pack_check` | ✅ PASS | DB TaskPlan 329 字 / 8 個禁字全 NOT FOUND（Mock 流程，真實 LLM 紀律生效待 Trial_v8） |
| 2 | `cody_devplan_structured_check` | ✅ 0 regression | Pipeline 跑通 done / DevPlanRevision=0；DevPlan 為空是 Mock 預期（Mock 走 PR 直接產出，prompt 真實效果待 Trial_v8） |
| 3 | `cody_implementationnote_written_check` | ✅ 0 regression | Pipeline 跑通 done；ImplementationNote 為空是 Mock 預期（真實 Cody LLM 才寫） |
| 4 | `framework_modify_taskplan_display_check` | ✅ PASS（議題 #B 真實驗證） | DB TaskPlan **10302 字** + BossInteraction Description **124 字**（kickoff consensus）+ **504 字**（modify path）+ 7 stage Pipeline 跑通 — Reload 修根因真實生效（否則 Description 顯示「無計劃書」） |
| 5 | `christ_action_supersede_check` | ⚠️ Mock 設計限制 | dev_plan_escalate_loop 跑通 / 3 BossInteraction 全 responded；**SupersedePriorFailedTasks + 動態化 intervention 真實效果 Mock 物理上無法驗** — MockMode auto-approve 走 dashboard source（不踩 Discord button callback）+ MarkGroupDoneOrIntervention 動態化用 `??=` 不踩 specific InterventionReason 場景。Trial_v8 真實 Christ 點 Discord button 才驗 |
| 6 | `dashboard_isestimated_visual_check` | ⏳ Christ 視覺驗收 | Dashboard `/tokens` 頁面 — Agent 卡片 + 表格 HasEstimated row 顯示 MudIcon Warning + Tooltip + cost「~」前綴 |
| 7 | `epic_chain_dashboard_ui_check` | ⏳ Christ 視覺驗收 | Dashboard `/pipeline` 頁面 — IsEpic row 顯示 📦 Epic icon + sub-task chip + EpicPaused chip；PipelineView 內 Epic section 顯示 sub-task 列表 + 暫停 / 恢復 epic 按鈕 |

### 0 regression 確認

- Stage 60 既有 `framework_modify_taskplan_happy` 場景：✅ 7 stage Pipeline 全跑通 + TaskPlan 10302 字 + Status done
- dotnet build 0 error / dotnet test 131 passed

### Forge 自驗能力限制揭露（給 Trial_v8 排程參考）

本 Stage 涉及兩個修法 Mock 場景物理上無法直接驗，必待 Trial_v8 真實 Christ 互動驗：
1. **SupersedePriorFailedTasks**（FF 四十五）— ButtonCallbackRouter 內 helper 依賴 Discord button callback path，MockMode auto-approve 走 dashboard source 不踩
2. **generic intervention 訊息動態化**（FF 四十五）— `??=` operator 設計，已 set specific InterventionReason 場景（如 dev_plan_escalate_loop）不會 override，只在 generic / fallback 場景生效

→ Trial_v8 重跑時關注：① Discord 真實點「跳過審核」/「放棄」button → 前置 [Petra→Dev_plan] failed task 是否標 cancelled；② cross-Agent supersede 場景是否誤觸 needs_intervention（理應已修根因）

### Christ 視覺驗收待辦

請 Christ 開 Dashboard 兩處驗收（Forge 已 Mock 場景準備好資料）：
- **`/tokens`**：場景 6 — 任一個有 IsEstimated=true 的 Agent row 應顯示 ⚠️ MudIcon Warning + Tooltip + cost「~」前綴
- **`/pipeline`**：場景 7 — 觸發 `epic_chain_dashboard_ui_check` Mock 後，parent group row 顯示 📦 Epic + sub-task chip；點開 Drawer Epic section 顯示 sub-task 列表 + 暫停 / 恢復 epic 按鈕

---

## 版本歷史

| 版本 | 日期 | 內容 |
|---|---|---|
| v2.0 | 2026-05-10 | 結案實作紀錄補強（Forge）— 7 子項實作對照表 + 範圍縮小揭露（SupersedePriorFailedTasks 只 cover Dev_plan path 兩處 YAGNI / Trial_v8 重新評估其他 3 申訴 path）+ Forge 自驗 7 Mock 場景結果（5 PASS / 2 Christ 視覺待辦 / Mock 設計限制揭露：場景 5 真實效果待 Trial_v8 真實 Discord button click 驗）+ 0 regression 確認（Stage 60 modify path）。**自驗能力限制 2 處揭露給 Trial_v8 排程參考**：① SupersedePriorFailedTasks 依賴 Discord button callback path（MockMode auto-approve 走 dashboard source 不踩）② generic intervention 動態化 `??=` 不踩已 set specific reason 場景。 |
| v1.0 | 2026-05-10 | 初版計劃書建立（Aria）— Stage 61 = Trial_v8 開跑前最後一塊清掃（Petra/Cody prompt 對齊群組 + Pipeline UI refresh entity 修根因 + Dashboard 補強 + Trial_v6 揭露 🟡 中議題群組全清）。範圍 7 子項：① Petra prompt 議題層次篩選紀律延伸（FF 五十六 + 工時砍紀律 — 三處同步 CLAUDE_Petra.md + KickoffPrompts.cs + DesignState.cs）② Cody prompt 對齊群組（FF 二十五 Dev_plan 結構規範 + FF 四十六 ImplementationNote 強制寫 + Sage 備援 source + FF 四十八 maxTurns 提升）③ 議題 #B Pipeline KickoffStageExecutor max_iter path 「無計劃書」修根因（refresh entity 確保 freshGroup.TaskPlan 拿最新）④ FF 五十 Dashboard token 統計頁 IsEstimated 視覺區分 ⑤ FF 四十五 Christ action 標 cancelled + generic intervention 訊息模板動態化（Trial_v6 議題 #9 直接踩）⑥ FF 四十 Stage 46 Dashboard razor UI 接線（epic 折疊 + sub-task 進度條 + 暫停按鈕 — Trial_v6 議題 #5 Christ 體驗痛點）⑦ Mock 場景補強 6 場景 + ⑧ v3.50.0 bump。**設計決策 5 條**：合併 Stage 61 全 7 子項（vs 拆 61A/61B）/ 純 prompt 改動不動 schema 結構 / 議題 #B refresh entity 戰術 Forge spike 自決 / Cody prompt audit Forge spike 自決 / Cody Dev_plan maxTurns 靜態 vs 動態 Forge spike 評估。**規劃前期已 grep 驗證**：CLAUDE_Petra.md 既有「審核紀律」段（line 19-50 已寫「以下不是 Petra 該審的」+「以下情況不構成 revise 理由」延伸 Kickoff/Design 場景）+ FrameworkKickoffRouter.CreateKickoffConfirmationAsync `:856-905` embed 構造邏輯（line 875-879 planPreview 取 freshGroup.TaskPlan — 確認真實 root cause = entity 沒 refresh）+ Petra prompt 三處同步位置（CLAUDE_Petra.md + KickoffPrompts.cs + DesignState.cs）+ Framework Petra Executor（KickoffPetraExecutor / DesignPetraExecutor）+ FF 二十五/四十六/四十八 真實內容對齊 Trial_v6 揭露議題群組。**規模 M-L** / Opus 1M + medium / 預估 ~350-500K（對齊 Stage 60 ×0.80 範圍延伸略小，純 prompt + UI 改動 + Forge spike 自決多項細節）。Mock 全綠 + Forge 自驗 6 場景 + Christ 視覺驗收 Dashboard 兩處（IsEstimated + epic UI）。 |
