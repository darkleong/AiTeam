# Changelog

本專案所有重要變更紀錄於此檔。

格式參照 [Keep a Changelog 1.1.0](https://keepachangelog.com/zh-TW/1.1.0/)，版本號遵循 [Semantic Versioning](https://semver.org/lang/zh-TW/)。
工作單位為 **Stage**，每完成一個 Stage 通常對應 minor 版本 bump。每條 entry 的細節見對應 Stage Roadmap；更深的實作 commit 訊息見 git log。

未實作功能候選見 [Future_Feature.md](docs/planning/Future_Feature.md)。

---

## [Unreleased]

- **下個動作候選**：Stage 79（v5.5 image flow 補完 — PetraInbox schema 擴 Images + SubtaskPlan needsImageContext flag 條件性 worker propagation + Claude Code CLI 圖片支援 spike / 規模 M / Christ 2026-05-19 戰略 question 揭 Dashboard 附圖 Petra 看不到 gap + WebSearch 業界紀律拍板）→ Stage 80（A HITL plan confirmation 閘門 / 業界 LangGraph interrupt + 4 decision pattern / 規模 M）→ Stage 81（B 動態 re-planning / 業界 LangGraph cycles + max iterations / 規模 L）→ WebUI Stage（K WebUI Talent CRUD + Effort 擴展 + E Token monitoring 視覺化 + v4 entity drop + Dashboard 重設計為 PetraSession-based）→ v5.5 完整收口。
- **Phase 4 候選路徑**：78a ✅ → 78b ✅ → 78c ✅ → 79 預留（image flow / M）→ 80 預留（HITL / M）→ 81 預留（動態 replan / L）→ WebUI Stage 預留（v4 entity drop + Dashboard 重設計）→ v5.5 完整收口。

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
