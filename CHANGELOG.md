# Changelog

本專案所有重要變更紀錄於此檔。

格式參照 [Keep a Changelog 1.1.0](https://keepachangelog.com/zh-TW/1.1.0/)，版本號遵循 [Semantic Versioning](https://semver.org/lang/zh-TW/)。
工作單位為 **Stage**，每完成一個 Stage 通常對應 minor 版本 bump。每條 entry 的細節見對應 Stage Roadmap；更深的實作 commit 訊息見 git log。

未實作功能候選見 [Future_Feature.md](docs/planning/Future_Feature.md)。

---

## [Unreleased]

- **下個動作候選**：**WebUI Stage**（Phase 4 最後一個 / v4 entity drop + Dashboard 重設計為 PetraSession-based / 規模 L+）→ **v5.5 完整收口**。
- **Phase 4 候選路徑**：78a ✅ → 78b ✅ → 78c ✅ → 79 ✅ → Trial_v23 ✅ → 80 ✅ → Trial_v24 ✅ → 81 ✅ → Trial_v25 🟡 → 82 ✅ → Trial_v26 🟢⭐ → **Trial_v27 🟡⭐**（戰略性結案 / LLM alignment safety net 實證）→ **WebUI Stage** → v5.5 完整收口。
- **Trial_v27 戰略結論 ⭐⭐⭐**：跳出 Trial→Fix 迴圈業界共識對齊 — Christ 戰略觀察「Trial→Fix 迴圈永遠不會結束」對齊業界共識（multi-agent LLM system production-ready 不是「Trial 全綠」/ 是「能用 + 可監控 + HITL 兜底 + 已知議題分類完整」）+ AiTeam 累積已達 production-ready（連續 15 Trial 業務級 + Stage 80/81 HITL 雙保險 + Stage 82 修根因 + 業界 supervisor pattern + LLM alignment 雙重 safety net）/ Aria 推薦直接 WebUI Stage 收 Phase 4 / 不再硬模擬 routing wire。詳見 [Trial_v27_Plan.md](docs/experiments/Trial_v27_Plan.md)。
- **Trial_v26 戰略 finding ⭐**：AiTeam Petra LLM + worker CLI + HITL 三層分工對齊業界主流 supervisor pattern（LangGraph / Databricks / Claude Agent SDK 共識）/ WebSearch 3 query 驗證。詳見 [Trial_v26_Plan.md](docs/experiments/Trial_v26_Plan.md)。

---

## [3.74.0] — 2026-05-21 — [Stage 82](docs/planning/Stage_82_Roadmap.md) Quinn outputLen 修根因（路線 A）+ Stage 81 子項 8 砍 + Trial_v25 三 🟡 議題收口（3 子項 / 10 檔變動 net +406 -81 行 / 規模 S→M 邊緣 / Forge context 179K→274K Opus 1M + high）— **子項 1 ⭐ Quinn outputLen 修根因（路線 A stream-json）**：ClaudeCodeService BuildArgs 兩 method 切 `--output-format json` → `stream-json --verbose`（對齊既有 image path RunCoreWithImagesAsync L171 pattern）/ ParseJsonOutput 升級 cover NDJSON line-by-line accumulate `type=assistant.message.content[]` 的 type=text 內容 / `type=result` row 優先取 `result` 欄位（normal text-only turn 對齊既有行為 0 regression）/ 空時 fallback accumulated text（修 Quinn tool-heavy 場景 final turn tool_use → result 空 真實根因）/ TryParseUsage 0 動（既有 4 nested 欄位 + top-level total_cost_usd 完全對齊 stream-json schema — Aria gate1 補強 spike 子項 1.5 跑真實 claude CLI verify）/ 砍 ClaudeCodeChatClientAdapter Stage 81 子項 8 qa_testing prepend + BuildQaSummaryEnforceSection method 整段（Stage 81 假設「無 final text turn」修法錯方向 / Trial_v25 揭真實根因 = Quinn 燒 27K output 但 result 欄位空 vs assumed 0 output）+ **子項 2 PM PetraSessionId 透傳（AsyncLocal scope）**：TokenTrackingProvider 加 internal static `AsyncLocal<Guid?> PetraSessionAmbient` + `BeginPetraSessionScope(sessionId)` IDisposable wrapper + PopScope nested class 對齊 .NET 標準 pattern / TokenLog 寫入加 `PetraSessionId = PetraSessionAmbient.Value`（對齊 Stage 81 議題 #5 worker dispatch path 透傳紀律）/ PetraOrchestratorService 4 LLM call site（DecideAsync L259 / DecideSkillsAsync L466 / DecideSubtaskPlanAsync L537 / InvokePetraReplanAsync L1854）包 `using var _scope = TokenTrackingProvider.BeginPetraSessionScope(ctx.SessionId)` / **範圍刻意收緊**：ILlmProvider 介面 0 動（W1 紀律 / CeoAgentService / DashboardAgentService / AgentQueueProcessor 既有 caller 透明）/ Forge spike 揭真實 PM agent 24 token_logs row 真實有 row 但 PetraSessionId 0/24 全 NULL（Roadmap 假設「0 row」錯 → 修法落點調整為 PetraSessionId 透傳 NULL 而非「包 TokenTrackingProvider」）+ **子項 3 SubtaskPlanParser conversational preamble robust 防呆**：TryParse 順序 trim → StripCodeFence → `StripPreambleAndPostamble` → Deserialize / StripPreambleAndPostamble = first `{` index + last `}` index substring extract（對 LLM 健談行為兜底 / 對齊「修根因 > 補丁」紀律）/ Petra system prompt 純 JSON 紀律維持（既有 Sonnet 4.6 純 JSON OK）+ 底層 parser robust 防呆雙保險 + **新增 14 xUnit test**：ClaudeCodeServiceParseJsonOutputTests 6 case（T1 normal text-only / T2 tool-heavy empty result fallback / T3 result is_error / T4 empty raw / T5 multiline accumulate / T6 stream-json usage nested schema Aria gate1 補強）+ TokenTrackingProviderTests 3 case（T1 no scope null / T2 with scope set / T3 nested scope inner overrides outer + Dispose 恢復）+ SubtaskPlanParserTests 5 case（T1 pure JSON / T2 markdown fence / T3 conversational preamble / T4 preamble + fence 雙 strip / T5 no JSON object error）+ 砍 Stage 81 InlineData 4 = 凈 +10 / build 0 error + dotnet test 136 passed 6 sec / **Aria gate1 Tier 0+1+2 通過**（規模 S→M 邊緣升 Tier 2 #3 build/test 必跑 / ParseJsonOutput 對 v4+v5 4 worker path 都影響 production critical path）/ **連續 18 Stage 0 follow-up + clean delivery 連續第十次** ⭐⭐⭐⭐⭐⭐⭐⭐⭐⭐（Stage 75-78a-78b-78c-79-80-81-82）/ Stage 期間 0 燒 AiTeam 餘額（Forge subscription / spike claude CLI 真實 0 cost API call）/ **3 know-how 升級**：① workflow_aria.md 第三節 A 第 7 條延伸範圍 #13 — 規劃含 token_logs / DB row 既有狀態評估時必 SQL grep 真實狀態（同類根因第 13 次累積） ② workflow_aria_session_lessons.md 自省點 #42 立 — Stage 修根因前先 spike / production 數據 verify 假設真實（Trial_v25 揭 Stage 81 子項 8 假設「無 final text turn」錯方向真實案例 / 修根因前驗證根因假設真實才動） ③ workflow_aria_session_lessons.md #40 補充段 — Aria gate1 階段主動補 micro-spike 紀律首次大規模實踐生效（Stage 82 plan v1.0 → v1.1 揭 TryParseUsage schema verify 議題 → Forge 補 spike 子項 1.5 micro-spike → 實作 0 動 0 follow-up bug 實證）/ **Aria 自審紀律生效驗證連續第二次** ⭐：Stage 82 plan review v1.0 → v1.1 揭 1 🟡 議題（Aria gate1 階段揭 / 對齊 Stage 81 v1.0 → v1.1 自審揭 8 議題首次大規模實踐生效精神延伸到 gate1 階段）/ 戰略意義：**「修根因 > 補丁」紀律深度實踐** — Stage 81 假設方向 vs Trial_v25 真實根因截然不同方向 → Stage 82 砍錯修法 + 修對地方（stream-json accumulate）+ 收口 Trial_v25 三 🟡 議題（SubtaskPlanParser preamble 防呆 + PM PetraSessionId AsyncLocal 透傳 + Quinn outputLen 修對地方）+ Trial_v26 補驗 Stage 81 動態 replan + Stage 82 修法雙驗證 / Petra Provider 切 Sonnet 4.6 為 production active default 紀律延續（Stage 38 Provider/Model DB SoT）

---

## [3.73.0] — 2026-05-20 — [Stage 81](docs/planning/Stage_81_Roadmap.md) B 動態 re-planning + HITL retry gate 配套 + Trial_v24 3 議題收口（10 子項 / 20 檔變動 net +2607 行 -37 行 / Christ 親口要的業務功能 / 規模 L 大規模架構級）— **動態 replan core（5 子項）**：`DetectReplanTrigger` 純規則 Regex（Vera `"critical":[{...}]` 非空 / Quinn `"status":"failed"`）/ `InvokePetraReplanAsync` LangGraph cycles 業界紀律（W8 — Petra LLM 只回 retry instruction string 不回新 plan 結構 / 3 正例 + 1 反例 few-shot / `TryParseReplanDecision` markdown fence 容錯）/ `PetraOrchestratorResult.Replanning(sessionId, currentSubtaskId, retryInstruction, replanReason)` + `Cancelled(sessionId, caps, summary)` 工廠（議題 #3 命名語意收口）/ `ResumeFromReplanConfirmationAsync` 4 decision routing + `ResumeReplanApproveAsync` 從 currentSubtaskId 起重 dispatch with retry instruction prepend（不從頭跑 / 不重 decide plan / 對齊 LangGraph cycles 業界紀律）+ `ResumeReplanEditOrRespondAsync` 重 InvokePetraReplanAsync + override context + 開新 replan_confirm 卡（loop）+ `ResumeReplanRejectAsync` 接受原 output 繼續下個 subtask（不 cancel session / iter 不變 / 議題 1 v1.1 修法）+ `ContinueChainFromSubtaskAsync` 取 plan_confirm ContextJson SoT 還原 plan + Talents（補強 #A 紀律）+ `DispatchRemainingSubtasksAsync` simplified sequential + retry instruction prepend `ChatMessage(System)` 只對第 0 個 subtask + `BuildSummariesFromSessionMessagesAsync` 從 PetraSessionMessages tool rows 還原 / `PetraSession.ReplanIteration` int default 0 + `SessionCostUsd` numeric(18,6) default 0 + `PetraSessionRepository` 3 helper（IncrementReplanIterationAsync / UpdateSessionCostUsdAsync / GetReplanStateAsync）+ `CheckReplanTriggerAfterDispatchAsync` 6 step（update cost / read flag / cap cost / detect trigger / cap iter / Petra LLM call）+ `HandleCapReachedAsync` 開既有 intervention 卡 + 寫 task_memory + `sessionRepo.CancelAsync` + return Cancelled（場景 G/H） + **HITL gate 配套（2 子項 / 重用 Stage 80 既有 infra）**：`PlanConfirmationProcessor` filter 擴 `IN ('plan_confirm', 'replan_confirm')` + dispatch 分支 + `MapActionToDecision` 8 action mapping（4 plan_* + 4 replan_*）/ `InteractionService.ReplanConfirmActionsJson` 4 button + MockMode auto-approve `replan_confirm => replan_approve` / `InteractionCard.razor` `_replanContext` cached field + `ParseReplanContext` + 觸發原因 MudAlert + 進度 + Petra retry instruction MudPaper + 原 output 預覽 MudExpansionPanel + `replan_reject` 二次確認 modal「接受原結果繼續往下跑」+ `InteractionCenter.razor.cs` 4 mapping（Icon=Refresh / Color=Warning / Label=計劃重審 / `replan_reject`=warning 區別 plan_reject error） + **Trial_v24 議題收口（3 子項）**：🟡 #1 Quinn outputLen=0 修根因 — `ClaudeCodeChatClientAdapter` qa_testing capability prepend `BuildQaSummaryEnforceSection` 「QA 報告紀律 — 必須在 final turn 輸出 markdown 摘要」（純 adapter 層 / 不污染 CLAUDE_Quinn.md） 🟡 #2 Petra `NeedsImageContext` 純文字誤判 — `BuildPetraSystemPrompt` few-shot 補 2 反例（純文字 prompt 無 attachment / 含 image 但純後端） 🟡 #3+#8 `Cancelled` 工廠 + reject path 收口 — Stage 80 `ResumeRejectAsync` 改用 `Cancelled` 工廠（vs 既有 `Done` 雜用 caps.Count=4）+ `PlanConfirmationProcessor` log `dispatched={result.DispatchedWorkerCount}` 自動對齊 0（0 code 改動）+ **Aria 二檢補強**：補強 #A `GetUseDynamicReplanningAsync` 雙 flag 綁定 — UseDynamicReplanning=true 但 UseHITLPlanConfirmation=false → effective false + warning log（ContinueChainFromSubtaskAsync 取 plan_confirm ContextJson 是 single source of truth 紀律）/ 補強 #B `TokenLog.PetraSessionId` non-unique non-partial index `IX_token_logs_PetraSessionId`（對齊既有 TaskId FK pattern / UpdateSessionCostUsdAsync 高頻 SumAsync 性能保險） / **AppSetting 3 新加 default 守 baseline**（`Workflow:UseDynamicReplanning=false` + `Workflow:MaxReplanIterations=3` + `Workflow:ReplanCostCapUsd=5`）/ Migration `Stage81PetraSessionReplanFields` 手寫 Up/Down（EF auto-gen 空 body / Stage 80 同類 stale snapshot 雷第 2 次累積）/ 議題 #4 W7 grep verify Stage 57 既有 `IX_boss_interactions_status_pending` partial unique index `(TaskGroupId, InteractionType)` 已 cover plan_confirm vs replan_confirm 不同 type 0 race / 0 新 index / **0 WebSearch 觸發**（Stage 77 既有結論 reference + Stage 80 plan_confirm infra 重用 / 0 third-party framework 真實使用） / Forge self-verify 全 12 場景 PASS（A baseline + B1-B3 trigger detect + C-H Resume routing unit test 簽名 + I QA prepend + J few-shot + K+K2 工廠 shape + L+L2 mapping + ReplanConfirmActionsJson + D defaults / 場景 C-H production 深度整合留 Trial_v25 真實業務驗 v1.1 議題 2 拍板）/ Christ 視覺驗收項目留 Trial_v25（Dashboard replan_confirm UI render + 真實業務 4 decision 端對端） / build：0 error / xUnit 126 pass 0 fail（含 10 新 Stage 81 test 展開 Theory 24 cases + Test29/Test30 既有 fixture 修正 DispatchTalentsAsync 新 ctx param）/ Aria gate1 Tier 0+1+2+Tier 3 #11 通過（規模 L+ 升 tier）/ **連續 17 Stage 0 follow-up + clean delivery 連續第九次** ⭐⭐⭐⭐⭐⭐⭐⭐⭐（Stage 75-78c-79-80-81）/ Stage 期間 0 燒 AiTeam 餘額（Forge subscription / xUnit 0 LLM call）/ **4 踩坑紀錄 / 3 know-how 升級**：① EF stale snapshot 第 2 次（Stage 80 同類延伸 — 即使不用 `--no-build` 仍可能踩 model snapshot 覆蓋 → Migration body 空但 Designer.cs 有正確 snapshot）→ 升級 [docs/conventions/ef-core.md](docs/conventions/ef-core.md) 加「Migration body 空驗證紀律」 ② C# raw string `{{...}}` brace escape 雷 → `$$"""` 修 → 升級 [docs/conventions/csharp.md](docs/conventions/csharp.md) 加「Raw string interpolation 含 JSON template 用 `$$"""`」 ③ DispatchTalentsAsync 簽名擴 ctx → Test29/30 reflection fixture 修正（既有 Stage 67/75 紀律 / 0 新升級） ④ Edit tool 絕對路徑走 main repo 而非 worktree（反而省事對齊 CLAUDE.md push main 紀律）→ 升級 [forge-self-verify skill](.claude/skills/forge-self-verify/SKILL.md) 加「worktree workflow Edit tool 絕對路徑紀律」 / **Aria 自審紀律生效驗證 ⭐**：Roadmap v1.0 → v1.1 自審揭 2 🔴 + 6 🟡 全收口（過去 Stage 79/80 自審 0-1 議題 / Stage 81 自審 8 議題 = aria-review-plan skill 自我自審紀律首次大規模實踐生效）/ 自省點候選留 /aria-end：「Aria 寫完 Roadmap 後系統性 6 維度 ultrathink 自審紀律」 / 戰略意義：**Stage 81 達成 Christ 親口要的動態 re-planning 業務功能** ⭐⭐⭐⭐⭐⭐（業界 LangGraph cycles + max iterations + cost cap + checkpoint replay 業界紀律完整內化到 AiTeam v5.5 orchestrator / HITL retry gate 重用 Stage 80 plan_confirm infra / Trial_v25 啟動條件達成 / Phase 4 接近完整收口前最後動態功能 Stage）

---

## [3.72.0] — 2026-05-20 — [Stage 80](docs/planning/Stage_80_Roadmap.md) A HITL plan confirmation 閘門 + Trial_v23 4 議題收口（9 子項 / 22 檔變動 net +2014 行 / Christ 親口要的業務功能）— HITL 主體（5 子項）：BossInteraction.InteractionType `plan_confirm` 加入 InteractionCenter switch + PlanConfirmActionsJson 4 button 常數 / HITL pause point 插在 [PetraOrchestratorService.cs:111-124](src/AiTeam.Bot/Orchestration/Petra/PetraOrchestratorService.cs#L111)（`DecideTalentsWithPlanAsync` 完成後 + `DispatchTalentsAsync` 前）+ `WaitForPlanConfirmationAsync` 開卡 + `sessionRepo.PauseAsync` + return `PetraOrchestratorResult.Paused` / 4 decision resume routing — `ResumeFromPlanConfirmationAsync` + 3 子 method（Approve / EditOrRespond / Reject）+ `DispatchAndFinalizeAsync` helper（從 StartAsync 抽出收尾段共用 / approve path 從 ContextJson 重建 SubtaskPlan + talentPicks 不重 call Petra LLM cost 節省）/ InteractionCard.razor plan_confirm UI — SubtaskPlan render（subtask 列表 + dependency 圖 + 附圖 chip + talent picks）+ 4 button + ContextJson parse 失敗 fallback alert 0 crash 紀律 / **新建 `PlanConfirmationProcessor` BackgroundService（Forge spike 偏離 plan）** — Roadmap §5 寫「InteractionProcessor 路由擴」對齊 Stage 78c 已砍真實 → 新建 BackgroundService 達成同等設計意圖（3s polling responded plan_confirm + `ProcessedByBot` 原子標 + IServiceScope per row + 對齊 PetraInboxProcessor 紀律）+ Trial_v23 議題收口（4 子項）：🔴 #1 DashboardAppSettingsService ctor → `IDbContextFactory<AppDbContext>` + 3 method `await using var db = await dbFactory.CreateDbContextAsync(ct)` + Program.cs `AddDbContextFactory<AppDbContext>` 並存註冊（修 Blazor InteractiveServer 並行 OnInit 撞 Scoped DbContext 根因 / 5 caller 元件 DI 自動 wire 0 直接影響）🟡 #2 CommandHandler 兩個 v5.5 path 改 `EmptyActionsJson`（0 按鈕純 ack 卡 / 對齊 v5.5 auto dispatch 精神）+ description 純 Christ 任務 + systemNotes Victoria reply / `BuildCeoConfirmDescription` 整 method 砍 0 caller 🟡 #3 **不修紀錄** — grep verify PipelineView 5 handler 真實是 Stage 78c 砍後 placeholder code 0 production risk（Vera review 並非絕對正確 / Aria gate1 反查 production code 真實狀態紀律候選）🟡 #4 `BossInteraction.SystemNotes?` 加欄位 + Migration `Stage80BossInteractionSystemNotes`（AddColumn nullable + AppSetting `Workflow:UseHITLPlanConfirmation` seed default false）+ BossInteractionDto + DashboardTaskService MapToDto + CommandHandler 寫入 `ceoResponse.Reply` + InteractionCard 獨立區塊（`var(--mud-palette-background-grey)` 背景 + `var(--mud-palette-divider)` border / 深色主題友善）/ **AppSetting flag default false 守 v5.5 baseline 0 regression**（Trial_v24 開時切 true → 結案切回 false 對齊 aria-trial-summary skill flag 切回紀律）/ Forge self-verify 全 8 場景 PASS（A flag=false 0 regression + B flag=true 開卡 + C plan_approve + D plan_edit + E plan_reject + F plan_respond + G 🔴 #1 hotfix verify 5 並行 Home GET 0 second operation + H 🟡 #4 SystemNotes 後端 SoT）/ Christ 視覺驗收項目留 Trial_v24（plan_confirm 卡 UI render + 深色主題 SystemNotes 視覺辨識 + 端對端業務體驗）/ build：0 error / xUnit 229 pass（102 Bot + 127 Generated）/ Aria gate1 Tier 0+1+2+Tier 3 #11 通過（升 1 級補 Forge 跳過 Plan Mode 漏層）/ **連續 16 Stage 0 follow-up + clean delivery 連續第八次** ⭐⭐⭐⭐⭐⭐⭐⭐（Stage 75-78c-79-80）/ Stage 期間 0 燒 AiTeam 餘額（Forge subscription / MockMode + MockLlmProvider fallback 0 真實 LLM call）/ **4 踩坑紀錄 / 2 know-how 升級**：① `dotnet ef migrations add --no-build` stale DLL 雷 → 升級 [docs/conventions/ef-core.md](docs/conventions/ef-core.md) 加紀律 ② Roadmap §5 漏掃 Stage 78c 砍範圍 InteractionProcessor（Aria 規劃 grep 紀律候選 / 留 /aria-end workflow_aria.md 第三節 A 第 7 條延伸範圍 #12 升級）③ Internal API JSON body lowercase `text` 同類根因第四次 → 升級 [forge-self-verify skill](.claude/skills/forge-self-verify/SKILL.md) 加 JSON body 欄位名紀律 ④ MockMode auto-approve fallback `ack` 對 plan_confirm 無效（不阻塞 / Future_Feature 候選）/ 自省點 #39 反向校準紀律實證生效 — Effort 給 high baseline + 不慣性推 Extra high / 戰略意義：**Stage 80 達成 Christ 親口要的 HITL plan confirmation 業務功能** ⭐⭐⭐⭐⭐（業界 LangGraph interrupt + 4 decision pattern 業界紀律內化到 AiTeam v5.5 orchestrator / Trial_v24 啟動條件達成 / Phase 4 接近完整收口）

---

## [3.71.0] — 2026-05-19 — [Stage 79](docs/planning/Stage_79_Roadmap.md) v5.5 image flow 補完 — Stage 75 漏接根因修 + 條件性 worker propagation + 半抽象 future-friendly 設計（11 鏈 13 子項 / 20 檔變動 net +1722 行 / 3 議題 Christ 全採納 Forge 推薦）— Stage 75 切 PetraInbox 設計遺漏 image flow 修根因（CeoAgentService.ProcessWithClaudeCodeAsync line 39 method body 0 image 接通 / Trial_v6-v22 連續 17 次純文字 prompt 沒踩 / Stage 80 HITL 視覺 context 前置依賴）+ Christ 2026-05-19 戰略 question 揭 + WebSearch 3 議題業界紀律 incorporated（Claude Code CLI 圖片支援機制無 --image flag / IChatClient multimodal / Multi-agent image propagation 業界 best practice「pass images only to worker agents that need them」）/ 4 議題 Christ 拍板採納（路線 A 只做 Image / 半抽象 Attachments jsonb + Type discriminator / 限制紀律 per task 5 張 per file 5 MB / Dashboard MudFileUpload + API verify + Repository 三層守）+ Forge spike 揭 3 新議題 Christ 全採納（P1 路線 A 補 GeminiProvider multimodal 對齊 AnthropicProvider pattern + Christ 偏好 Gemini 低 cost / P2 路線 A 純字串簽名 Repository 收 attachmentsJson 0 cross-project type 依賴 / P3 Dashboard 4 層 validate 100% 既有 hardcoded → AppSetting 動態 + API/Repository 後備層補強）/ 11 鏈砍實作：§A PetraInbox.Attachments jsonb nullable + Migration `Stage79PetraInboxAttachments` + InsertData seed 2 AppSetting / §B PetraInboxRepository.Enqueue 簽名擴 attachmentsJson 純字串 / §C CeoAgentService 接通 images + 限制紀律守 + workflowResolver ctor dep + camelCase JSON 對齊既有 ImagesJsonOptions pattern / §D PetraDispatchWorker 反序列化 + DeserializeImageAttachments helper + StartAsync 簽名擴 images / §E PetraOrchestratorService 3 LLM call sites（DecideAsync/DecideTalents/DecideTalentsWithPlan）含 images / §F SubtaskPlan.Subtask 加 NeedsImageContext + Parser + Petra prompt 教學 few-shot 3 範例（UI bug case true / 後端 false / docs false）+ BuildInputMessagesForSubtaskAsync 條件性 image AIContent dispatch + DispatchWorkersAsync/DispatchTalentsAsync 簽名擴 images / §G ClaudeCodeChatClientAdapter workspace .tmp/images/ 寫圖檔 + prompt path reference + finally 清理 + try-catch fallback defensive / §H WorkflowSettings + Resolver 加 2 method + CeoCommandController API verify 後備 + Dashboard QuickCommandCard hardcoded → AppSetting 動態 + razor markup 同步動態化 / §I GeminiProvider multimodal（inline_data + base64 + mime_type 對齊 [Gemini API multimodal doc](https://ai.google.dev/gemini-api/docs/vision)）/ §J xUnit Mock fixture 4 處簽名擴 + Test31/32 SubtaskPlanParser NeedsImageContext + Linear default false / §K Directory.Build.props v3.70.0 → v3.71.0 / Forge healthy 偏離 plan 紀律延續第 N+2 次累積（v5 path 簡化紀律 PetraOrchestratorService.cs:344 / dc.MediaType nullable check pattern match / WriteImageContentsToWorkspaceAsync try-catch defensive 比 Plan v1 嚴謹）/ build：0 error / Bot.Tests 100→102 全綠 + Generated 127 全綠 / Wave 2 build verify 3 errors Forge 自抓自修（StubPetraOrchestratorService override 簽名 + 3 處 reflection invoke args propagation + Dashboard razor markup 動態化）/ Aria gate1 Tier 0+1+Tier 2 #3 build 通過（Trial 前最後一個 Stage）/ **連續 15 Stage 0 follow-up + clean delivery 連續第七次** ⭐⭐⭐⭐⭐⭐⭐（Stage 75-78c-79）/ 校準錨真實 **449K** → ratio **×2.49-3.45 mid ×2.97** = Aria 預估範圍上界精準對齊（raw 130-180K × ×1.5-2.5 = 預估 200-450K 真實踩上界 / 大規模架構級重構新區間 ×1.30-4.93 中段下緣 9 資料點累積）+ 自省點 #37 第 9 次累積實證 / 揭新場景變因「M+ 新業務功能 + 1 輪 spike + 半抽象設計 + 議題 P1 補 GeminiProvider scope 略擴 +30%」ratio 中段 / Stage 期間 0 燒 AiTeam 餘額（Forge subscription / 0 API call spike） / Aria 自我反思候選 1 條（議題 P3 揭規劃前對 Dashboard 既有 validate 邏輯認知不全 — 從 0 建 vs 既有 hardcoded → AppSetting 對齊 / workflow_aria.md 第三節 A 第 7 條延伸範圍紀律延伸候選 / 留 /aria-end 統一升級）/ 戰略意義：**v5.5 image flow business-ready** ⭐⭐⭐⭐⭐（Petra 真實看圖 + 條件性 worker propagation + Dashboard 限制三層守 + GeminiProvider multimodal native 支援 + Trial_v23 啟動條件達成）

---

## [3.70.0] — 2026-05-19 — [Stage 78c](docs/planning/Stage_78c_Roadmap.md) v5.5 Phase 4 候選 C 最終收口 — v4 Pipeline framework 整套砍除（**MAJOR refactor / 路線 A 一次過 + 6 議題拍板採納 / 6 鏈 ~37+ 子項 / 108 檔變動 net -22690 行**）— Christ 2026-05-19 拍板路線 A 一次過 + 4 sub-scope 議題拍板（議題 1 路線 2 砍 service 留 entity+Dashboard UI+BossInteraction Repository / 議題 2 整檔砍 WebhookController / 議題 3 砍 `/mock` Discord SlashCommand 留 Mock LLM 層 / 議題 4 全砍其他 Discord SlashCommand）+ Forge 實作期再揭 3 議題拍板（議題 7 MockScenarioService 整套砍 wording 漏分層校正 / 議題 8 Pm/ folder 5 files 整套砍 / 議題 9 agent_queues 表真實**不存在** Migration 全廢除 / 0 不可逆風險）+ Aria 5 條補強紀律 incorporated / 6 鏈砍範圍：鏈 A v4 Pipeline 核心（WorkflowEngine + Workflows/Pipeline/ 16 files + PipelineRoutingService）+ 鏈 B v4 Meeting+Appeal+HITL+Boss+InteractionProcessor+Proposal+Pm+MockScenarioService（Workflows/Kickoff+Design+Appeal/ + Workflows/Common + Orchestration/Meeting+Appeal+Hitl+Boss + Epic + Qa + Proposal + InteractionProcessor + Pm/ 5 files + Services/MockScenarioService）+ 鏈 C v4 Queue+Group（TaskGroupService + AgentQueueService + AgentQueueProcessor + AgentQueueControlService / 0 Migration）+ 鏈 D IAgentExecutor 部分砍（IAgentExecutor.cs → AgentDescriptor.cs 重命名縮檔 / 砍 interface + AgentExecutionResult + AgentResultType / 留 AgentDescriptor record v5.5 active R1 校正）+ 鏈 E Discord routing 重整（ButtonCallbackRouter 713→~120 行 / CommandHandler 614→~280 行 + DetectImageMediaType 移入 + slashRouter/appSettings ctor dep 砍 / SlashCommandRouter 整檔砍 / PendingConfirmationStore + RoutingTypes 縮為純 v5.5 generic）+ 鏈 F 配套（WebhookController 整檔砍 + Program.cs v4 DI 註冊段砍 ~30+ 行 + InternalController v4 endpoints 砍 / mock/scenario + queue control + taskgroup pause-resume + kickoff trigger-mid-interrupt + replay-completion + requeue 整套 + DashboardBotService v4 methods 砍 + DashboardCeoCommandService TriggerKickoffMidInterruptAsync 砍 + Dashboard 4 頁 v4 action methods stub snackbar「v4 已砍 / WebUI Stage 重設計」議題 1 路線 2 邊界守 + Home.razor 砍 MockScenarioCard + GlobalQueueControlCard reference + GlobalQueueControlCard 整檔砍 + Bot.Tests DesignPromptsTests 整檔砍）+ 鏈 G Directory.Build.props v3.69.0 → v3.70.0 / 議題 1 路線 2 邊界：留 TaskGroup + TaskItem + TaskLog + BossInteraction entity + DB tables + DashboardTaskService + 4 Dashboard Pages + BossInteraction Repository + Stage 57 partial unique index / WebUI Stage 預備重設計範圍 / v5.5 path 0 行為改變紀律守 / build：0 error / **0 C# compile warning**（Stage 78b 59 → 78c 0 突破歷史最低 ⭐⭐⭐ 連續 3 Stage 大幅下降 v4 dead code 砍乾淨後 unused warning 全消）/ xUnit Bot.Tests 100 passed（baseline 104 - 4 v4 specific 砍）+ Generated 127 全綠 / Forge 自驗 9 grep verify 全 PASS + production deploy verify / 0 follow-up bug（Wave 2 build verify 12 errors Forge 自抓自修 / 對齊 Stage 54 自驗能力突破紀律）/ Aria gate1 Tier 0+1 通過 / **連續 14 Stage 0 follow-up + clean delivery 連續第六次 ⭐⭐⭐⭐⭐⭐** / 校準錨真實 **521K → ratio ×1.30-2.08 mid ×1.69**（vs Aria prep-session ultrathink 預估 600-800K 偏低 -13% / 對齊大規模架構級重構新區間 ×1.57-4.93 中段下緣）+ 自省點 #37 第 8 次累積實證 + 揭「ultrathink 預估上界偏高」反向校準新發現 / 5 Aria 自我反思候選紀錄：① 議題 3 wording 漏分層 ② agent_queues 表規劃前認知錯誤 ③ Plan v1 沒明列 Forge 7 處 healthy 偏離 ④ Aria ultrathink 預估 vs Forge 真實落點對齊度 ⑤ Roadmap §C 場景 C AgentDescriptor 規格錯誤（Forge R1 校正） / 戰略意義：**v5.5 path single source of truth 完整收口** ⭐⭐⭐⭐⭐⭐（v4 path 全部砍乾淨 / 0 dead code / 0 dead caller / 0 dead routing / v5.5 path 唯一 source）

---

## [3.69.0] — 2026-05-19 — [Stage 78b](docs/planning/Stage_78b_Roadmap.md) v5.5 Phase 4 候選 C 後續 — v4 path dead caller 整套砍除（純 refactor 6 子項 / Christ 拍板路線 C 折衷 — ButtonCallbackRouter v4 routing (exec_yes / escalate_* 5 case + HandleExecYesAsync + ExecuteAgentTaskAsync + 3 helper) + HandleConfirmYesAsync v4 body cascade 砍 (縮為 ~15 行 / 保 Stage 68 短路 + defensive log+ack fallback) + BuildEscalateButtons 0 caller cleanup + BuildAgentPlanEmbed 留 (ShowDirectAgentConfirmAsync 仍 caller / 預期路徑 A) / OpsAgentService IAgentExecutor 實作砍保 HealthCheckJob production active 4 method + Program.cs:68 AddKeyedSingleton 砍 / IAgentExecutor.cs 整檔 0 動 W3 fallback 紀律守 — AgentQueueProcessor:190 still active + AgentExecutionResult/AgentResultType/AgentDescriptor 12+ file 廣用 Stage 78c 預備砍 / `/task` slash command + HandleTaskCommandAsync 整段砍 + ctor 11→6 dep / GitHub Issue webhook HandleIssueOpenedAsync 整段砍 + DispatchEventAsync case issues 砍 + ctor rulesService 砍 / CeoAgentService.ProcessAsync + 4 v4 helper (BuildSystemPrompt / BuildUserMessageAsync / BuildGitHubContextAsync / TryParseResponse) + ctor 8→3 dep 砍縮為純 v5.5 path ~50 行 / Directory.Build.props v3.68.0 → v3.69.0 / 8 檔變動 net -787 行 / build warning 102 → 59 (-43 / -42%) / xUnit Bot.Tests 104 + Generated 127 全綠 / R6 ResolveWorkflowType `\b` 精準匹配紀律守 vs ProposalConfirmationService.ResolveWorkflowTypeInternal 不誤砍 / Forge spike 1 輪 Plan v1 揭 W1 邊界議題 + Christ 拍板路線 C + Plan v2 升級 1 輪 + Aria 二檢通過 + 實作 0 follow-up bug + Forge 自驗 9 grep verify 全 PASS + Aria gate1 Tier 0+1 通過 / 連續 13 Stage 0 follow-up bug fix + clean delivery 連續第五次 ⭐⭐⭐⭐⭐ / 校準錨真實 394K → ratio ×3.03-4.93 突破 Stage 78a baseline ×2.71-4.07 上界 / 大規模架構級重構新區間 ×1.57-4.93 新上界 7 資料點累積 + 自省點 #37 第 7 次累積實證 / Aria 自我反思候選 — Plan v2 §Verification 第 1 條 grep verify 規格過嚴沒對齊路線 C 拍板（候選二檢紀律延伸） / Stage 78c 預留 v4 Pipeline 整套砍 + Stage 79 v5.5 image flow 補完新揭 gap — Christ 2026-05-19 戰略 question 揭 Dashboard 附圖 Petra 看不到 + WebSearch 業界紀律拍板 Petra SubtaskPlan needsImageContext flag 條件性 worker propagation）

---

## [3.68.0] — 2026-05-18 — [Stage 78a](docs/planning/Stage_78_Roadmap.md) v5.5 Phase 4 C — v4 path dead code 整套砍除（10 子項精準範圍 / Forge spike v1+v2+v3 連續三輪揭真實 + Aria 計劃前 grep 紀律補強候選累積第 N+1~3 次延伸 + Christ + Aria 拍板 5 議題 / 砍 3 純 v4 class（Rosa/Demi/Release ~1150 行）+ 4 雙路徑 class v4 method ~2900 行（Doc/Dev/Reviewer/Qa 留 v5.5 IAgentTool）+ LlmProviderFactory 系列全保留（Petra 3 call sites active）+ 1 dead nuget 砍（Microsoft.Agents.AI.Anthropic）+ CeoAgentService v4 fallback + PetraSessionRecoveryService flag check 砍 + 配套 propagation（archive prompt + Adapter capability 7→4 + CLAUDE_Petra.md + xUnit InlineData + csproj + RoutingTypes + ButtonCallbackRouter v4 Requirements + QaReport 搬獨立檔）+ Aria gate1 🟡 修補 DefaultSkillRegistry 6→4 + Test 10/14 + Stage74 T7 + DbSeeder + CLAUDE.md production path 修根因（Petra LlmProviderFactory Gemini default / 非 Claude Code CLI）/ 22+4=26 檔變動 / net -3957 行 / xUnit Bot.Tests 113→104 + Generated 127/127 全綠 / 5 healthy 偏離 commit message 明寫 / Forge spike 揭架構盲點紀律第 N+4 次累積 / Stage 78b 預留 ButtonCallbackRouter 其他 v4 routing + IAgentExecutor + AgentQueueProcessor + OpsAgent + ProcessAsync）

---

## [3.67.0] — 2026-05-18 — [Stage 77](docs/planning/Stage_77_Roadmap.md) v5.5 Phase 3 補強 — fire-and-forget A2 業界推薦完整版（PetraInboxChannel BoundedChannel + PetraDispatchWorker N=3 multi-consumer Task.WhenAll + PetraInboxProcessor 退化 pure producer + Stage 76 retry path 整套搬遷 0 邏輯改變 + dispatch CT 跟 stoppingToken 解耦 + StopAsync graceful shutdown drain 4 階段 30 min timeout + Workflow:MaxConcurrentPetra default 3 範圍 [1,10] + Migration InsertData seed + PetraOrchestratorService.StartAsync virtual + xUnit 15 case 全綠 — 業界 7 議題 WebSearch 完整 incorporated / Aria 二檢 4 點修正 incorporated / Aria gate1 Tier 0+1+Tier 2 #3 build 通過 / 5 層守門全綠 / 連續 11 Stage 0 follow-up bug fix / Trial_v22 啟動條件達成）

---

## [3.66.0] — 2026-05-18 — [Stage 76](docs/planning/Stage_76_Roadmap.md) v5.5 Phase 3 補強 — task retry / resume 機制基礎建設 + Trial_v21 修補類（PetraInbox schema 擴 4 欄 AttemptCount/MaxAttempts/NextRetryAt/DeadAt + Migration + PetraInboxProcessor retry path 3 路分支（Transient retry exponential backoff 30s×2×max3 + ±20% jitter / BusinessRule+Permanent fail-fast）+ PetraErrorClassifier 新檔 3 分類（Transient/BusinessRule/Permanent）+ Dead Letter pattern + queuePosition race condition 修法（🥇 簡化顯示）+ Dashboard 重跑 failed/dead task 按鈕 + xUnit 9 case 全綠 / 連續 10 Stage 0 follow-up bug fix / Forge spike 揭架構盲點修根因 1 處 — Migration MaxAttempts defaultValue 0→3 patch / ef-core.md 升級 Migration AddColumn defaultValue 對齊 entity C# initializer 紀律 / Aria 二檢 3 點修正全 incorporated）

---

## [3.65.0] — 2026-05-17 — [Stage 75](docs/planning/Stage_75_Roadmap.md) v5.5 Phase 3 兩層 queue 配套 — Petra 接收層 + Worker 執行層 per-Talent serialization（PetraInbox table + PetraInboxProcessor BackgroundService + CeoAgentService 寫 inbox + ack + TalentDispatchLockService SemaphoreSlim per-Talent + DispatchTalentsAsync per-Talent lock wire + Forge spike 修根因 talentNameToIdMap unconditional build + 三 method 簽名 non-nullable + Dashboard UX status / 連續 9 Stage 0 follow-up bug fix）

## [3.64.0] — 2026-05-17 — [Stage 74](docs/planning/Stage_74_Roadmap.md) v5.5 Phase 3 Step 8 per-Skill Model + 真並行 dispatch + Skill registry metadata 擴展（TalentSkill schema 擴 Provider/Model + TalentSkillModelResolver 三層 fallback + ClaudeCodeChatClientAdapter 動態 Model 整合 + SubtaskPlanLevelGrouping DAG fan-out 路線 A + SkillDescriptor metadata 對齊 Agent Skills open standard / 連續 8 Stage 0 follow-up bug fix）

## [3.63.0] — 2026-05-17 — [Stage 73](docs/planning/Stage_73_Roadmap.md) v5.5 Phase 3 Step 7 Prompt content 升級 + Petra TalentPrompt persona seed（6 SkillPrompt v1→v2 走 versioning path + 4 拍板特質 persona + 對齊「品質 > 做法」精神 / 連續 7 Stage 0 follow-up bug fix）

## [3.62.0] — 2026-05-17 — [Stage 72](docs/planning/Stage_72_Roadmap.md) v5.5 Phase 2 Step 5 Prompt DB 化 + Talent identity 整合（SkillPrompts + TalentPrompts 兩層 schema + Versioning + rollback / 對齊業界 2026 prompt orchestration 主流 / 連續 6 Stage 0 follow-up bug fix）

## [3.61.0] — 2026-05-16 — [Stage 71](docs/planning/Stage_71_Roadmap.md) v5.5 Phase 2 Step 3+4 production-ready 補強（Trial_v15+v16 揭 2 議題收口 — Petra prompt 線性整包紀律 + memory 空 content guard / 連續 5 Stage 0 follow-up bug fix）

## [3.60.0] — 2026-05-16 — [Stage 70](docs/planning/Stage_70_Roadmap.md) v5.5 Phase 2 Step 4 Petra 拆解指令精準度（hierarchical decomposition + dependency graph / SubtaskPlan + Parser + TopoSort / Backwards-compatible 4 層守護）

## [3.59.0] — 2026-05-16 — [Stage 69](docs/planning/Stage_69_Roadmap.md) v5.5 Phase 2 Step 3 跨 session 長期持久記憶基底（TaskMemory + TalentMemory schema / 整合 v5.5 dispatch / v2.1 scope pivot TaskGroup → PetraSession 修 Aria 漏掃 v5.5 path 根因）

## [3.58.0] — 2026-05-16 — [Stage 68](docs/planning/Stage_68_Roadmap.md) v5.5 Phase 1 完整收口前 production-ready 補強（AppendMessage async + v5 PoC post-confirm 收尾 + ef-core.md nullable unique pattern）

## [3.57.0] — 2026-05-15 — [Stage 67](docs/planning/Stage_67_Roadmap.md) v5.5 升級首發 Phase 1 Step 2 Talent-Skill separation 重構基底（架構級重構 / Trial_v13 啟動條件達成）

## [3.56.0] — 2026-05-14 — [Stage 66](docs/planning/Stage_66_Roadmap.md) v5 動態架構 Trial_v11 揭 3 🟡 議題收口（production-ready 補強第三波 / v5 上線前最後一個工程 Stage / Trial_v12 啟動條件達成）

## [3.55.0] — 2026-05-14 — [Stage 65](docs/planning/Stage_65_Roadmap.md) v5 動態架構 Trial_v10 揭 4 🟡 議題收口（production-ready 補強第二波 + 結案後 merge feature/v5-poc → main）

## [3.54.0] — 2026-05-13 — [Stage 64](docs/planning/Stage_64_Roadmap.md) v5 動態架構 production-ready 收口（Trial_v9 揭 7 議題 + Stage 63A errata）

## [3.53.0] — 2026-05-12 — [Stage 63B](docs/planning/Stage_63B_Roadmap.md) FF 三十六 Phase B PoC spike v5 動態架構 production-ready 實作 + Mock 全綠

## [3.52.0] — 2026-05-11 — [Stage 63A](docs/planning/Stage_63A_Roadmap.md) FF 三十六 Phase B 動態決策 API spike ✅ 硬通過 — 揭 Magentic 命名不存在 + 2 framework limitation（Stage 63B 戰略級早期 derisk）

## [3.51.0] — 2026-05-11 — [Stage 62](docs/planning/Stage_62_Roadmap.md) FF 三十六 Phase B Charter spike — v5 動態架構規劃文件 deliverable

## [3.50.0] — 2026-05-10 — [Stage 61](docs/planning/Stage_61_Roadmap.md) Petra/Cody prompt 對齊群組 + Pipeline UI refresh + Dashboard 補強（Trial_v8 開跑前最後清掃）

## [3.49.0] — 2026-05-10 — [Stage 60](docs/planning/Stage_60_Roadmap.md) FF 五十五 — v4 framework 邊角 user actions legacy 遷移 + meeting subprocess fail-fast 統一（議題 C2/H1 收口 + Trial_v7 反例修根因驗證）

## [3.48.0] — 2026-05-10 — [Stage 59](docs/planning/Stage_59_Roadmap.md) FF 五十四子項 1 — TaskGroupService 怪物大檔拆解 -54% 瘦身（4 子 service / Boss/Epic/Routing 3 子目錄）

## [3.47.0] — 2026-05-10 — [Stage 58](docs/planning/Stage_58_Roadmap.md) v4 framework production-ready 補強第二波 — API 餘額容錯性 ⭐ Trial_v6 揭露 3 🔴 全收口 🎉

## [3.46.0] / [3.46.1] — 2026-05-09 — [Stage 57](docs/planning/Stage_57_Roadmap.md) v4 framework production-ready 補強第一波 — race condition 雙層防 + Vera fix loop HITL routing 第 6 routing

## [3.45.0] — 2026-05-05 — [Stage 56](docs/planning/Stage_56_Roadmap.md) Trial_v6 前置條件統包 — Dashboard MockScenarioCard 補全 33 場景 + FF 四十二/四十三 修 + conventions 補 2 段

## [3.44.0] — 2026-05-05 — [Stage 55B Session B](docs/planning/Stage_55B_Roadmap.md) ⭐ v4 漸進遷移第九步完整結案 — 5 routing types HITL refactor + v4 路線 9/9 達成 🎉

## [3.43.0] — 2026-05-04 — [Stage 55B Session A](docs/planning/Stage_55B_Roadmap.md) v4 漸進遷移第九步（拆 Session A/B 第一段）— PipelineHitlHelper + AppealOrchestrationService 16 處 skip 精簡 + F-α 排除條件移除

## [3.42.0] — 2026-05-04 — [Stage 55A](docs/planning/Stage_55A_Roadmap.md) v4 漸進遷移第八步（拆 55A/55B 第一段）Kickoff/Design 整合到 Pipeline + sub-task 整合 + 6 hooks 移除 + 刪 WorkflowEngine.cs

## [3.41.0] — 2026-05-04 — [Stage 54](docs/planning/Stage_54_Roadmap.md) v4 漸進遷移第七步 Crash Recovery 全切 + 4 CheckpointStore base class + idempotency

## [3.40.0] — 2026-05-03 — [Stage 53B](docs/planning/Stage_53B_Roadmap.md) ⭐ v4 漸進遷移第六步 子流程 + 5 fallback 移除

## [3.39.0] — 2026-05-03 — [Stage 53A](docs/planning/Stage_53A_Roadmap.md) ⭐ v4 漸進遷移第五步 macro pipeline NewFeature happy path

## [3.38.0] — 2026-05-03 — [Stage 52](docs/planning/Stage_52_Roadmap.md) v4 漸進遷移第四步 Design Meeting B3 路線

## [3.37.0] — 2026-05-02 — [Stage 51](docs/planning/Stage_51_Roadmap.md) ⭐ v4 漸進遷移第三步 framework HITL 試點

## [3.36.0] — 2026-05-02 — [Stage 50](docs/planning/Stage_50_Roadmap.md) v4 漸進遷移第二步

## [3.35.0] — 2026-05-02 — [Stage 49](docs/planning/Stage_49_Roadmap.md) ⭐ v4 漸進遷移首發

## [3.34.0] — 2026-05-02 — [Stage 47](docs/planning/Stage_47_Roadmap.md)

## [3.33.0] — 2026-04-29 — [Stage 46](docs/planning/Stage_46_Roadmap.md)

## [3.32.0] — 2026-04-29 — [Stage 45](docs/planning/Stage_45_Roadmap.md)

## [3.31.0] — 2026-04-29 — [Stage 44](docs/planning/Stage_44_Roadmap.md)

## [3.30.0] — 2026-04-29 — [Stage 43](docs/planning/Stage_43_Roadmap.md)

## [3.29.0] — 2026-04-28 — [Stage 42](docs/planning/Stage_42_Roadmap.md)

## [3.28.0] — 2026-04-27 — [Stage 41](docs/planning/Stage_41_Roadmap.md)

## [3.27.0] — 2026-04-26 — [Stage 40](docs/planning/Stage_40_Roadmap.md)

## [3.26.0] — 2026-04-25 — [Stage 39](docs/planning/Stage_39_Roadmap.md)

## [3.25.0] — 2026-04-25 — [Stage 38](docs/planning/Stage_38_Roadmap.md)

## [3.24.0] — 2026-04-25 — [Stage 37](docs/planning/Stage_37_Roadmap.md)

## [3.23.0] — 2026-04-22 — [Stage 36](docs/planning/Stage_36_Roadmap.md)

## [3.22.0] — 2026-04-22 — [Stage 35](docs/planning/Stage_35_Roadmap.md)

## [3.21.0] — 2026-04-22 — [Stage 34](docs/planning/Stage_34_Roadmap.md)

## [3.20.0] — 2026-04-22 — [Stage 33](docs/planning/Stage_33_Roadmap.md)

## [3.19.0] — 2026-04-21 — [Stage 32](docs/planning/Stage_32_Roadmap.md)

## [3.18.0] — 2026-04-20 — [Stage 31](docs/planning/Stage_31_Roadmap.md)

## [3.17.0] — 2026-04-20 — [Stage 30](docs/planning/Stage_30_Roadmap.md)

## [3.16.1] — 2026-04-19 — Hotfix

MockMode 提案核准重複建 TaskGroup bug 修正（Dashboard 路徑補 GroupId 防護對齊 Discord 路徑）

## [3.16.0] — 2026-04-19 — [Stage 29](docs/planning/Stage_29_Roadmap.md)

## [3.15.0] — 2026-04-17 — [Stage 28b](docs/planning/Stage_28b_Roadmap.md)

## [3.14.0] — 2026-04-17 — [Stage 28a](docs/planning/Stage_28a_Roadmap.md)

## [3.13.0] — 2026-04-16 — [Stage 27b](docs/planning/Stage_27b_Roadmap.md)

## [3.12.0] — 2026-04-16 — [Stage 27a](docs/planning/Stage_27a_Roadmap.md)

## [3.11.0] — 2026-04-14 — [Stage 26](docs/planning/Stage_26_Roadmap.md)

## [3.10.0] — 2026-04-14 — [Stage 25b](docs/planning/Stage_25b_Roadmap.md)

## [3.9.0] — 2026-04-14 — [Stage 25a](docs/planning/Stage_25a_Roadmap.md)

## [3.8.0] — 2026-04-13 — [Stage 24](docs/planning/Stage_24_Roadmap.md)

## [3.7.0] — 2026-04-12 — [Stage 23](docs/planning/Stage_23_Roadmap.md)

## [3.6.0] — 2026-04-12 — [Stage 22](docs/planning/Stage_22_Roadmap.md)

## [3.5.0] — 2026-04-11 — [Stage 21](docs/planning/Stage_21_Roadmap.md)

## [3.4.0] — 2026-04-11 — [Stage 20](docs/planning/Stage_20_Roadmap.md)

## [3.3.0] — 2026-04-10 / 04-11 — [Stage 19](docs/planning/Stage_19_Roadmap.md)

## [3.2.0] — 2026-04-09 — [Stage 18](docs/planning/Stage_18_Roadmap.md)

## [3.1.0] — 2026-04-08 — [Stage 17](docs/planning/Stage_17_Roadmap.md)

## [3.0.0] — 2026-04-07 — [Stage 16](docs/planning/Stage_16_Roadmap.md)

## [2.4.0] — 2026-04-06 — [Stage 15](docs/planning/Stage_15_Roadmap.md)

## [2.3.0] — 2026-04-06 — [Stage 14](docs/planning/Stage_14_Roadmap.md)

## [2.2.0] — 2026-04-06 — [Stage 13](docs/planning/Stage_13_Roadmap.md)

## [2.1.0] — 2026-04-06 — [Stage 12](docs/planning/Stage_12_Roadmap.md)

## [2.0.0] — 2026-04-05 — [Stage 11](docs/planning/Stage_11_Roadmap.md)

## [1.4.0] — 2026-04-03 — [Stage 10](docs/planning/Stage_10_Roadmap.md)

## [1.3.1] — 2026-04-04 — Hotfix

Stage 10 驗收後 7 項修正（Race Condition / IssueUrls 重複 / PushStatus / dead code 清理 / EF Index）

## [1.3.0] — 2026-04-03 — [Stage 9](docs/planning/Stage_9_Roadmap.md)

## [1.2.0] — 2026-04-02 — [Stage 8](docs/planning/Stage_8_Roadmap.md)

## [1.1.0] — 2026-04-02 — [Stage 7](docs/planning/Stage_7_Roadmap.md)

## [1.0.0] — 2026-04-01 — [Stage 6](docs/_archive/early-stages/Stage_6_Roadmap.md)

## [0.4.0] — 2026-04-01 — [Stage 5](docs/_archive/early-stages/Stage_5_Expansion.md)

## [0.3.0] — 2026-03-31 — [Stage 4](docs/_archive/early-stages/Stage_4_Dashboard.md)

## [0.2.0] — 2026-03-31 — [Stage 3](docs/_archive/early-stages/Stage_3_Agents.md)

## [0.1.0] — 2026-03-31 — [Stage 1](docs/_archive/early-stages/Stage_1_Design.md) + [Stage 2](docs/_archive/early-stages/Stage_2_Foundation.md)
