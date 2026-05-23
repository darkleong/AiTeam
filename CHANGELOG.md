# Changelog

本專案所有重要變更紀錄於此檔。

格式參照 [Keep a Changelog 1.1.0](https://keepachangelog.com/zh-TW/1.1.0/)，版本號遵循 [Semantic Versioning](https://semver.org/lang/zh-TW/)。
工作單位為 **Stage**，每完成一個 Stage 通常對應 minor 版本 bump。每條 entry 的細節見對應 Stage Roadmap；更深的實作 commit 訊息見 git log。

未實作功能候選見 [Future_Feature.md](docs/planning/Future_Feature.md)。

## Entry 紀律

- **新 entry format**：`## [X.Y.Z] — date — [Stage XX](path) 主題` + 換行 + body 段（~100-200 字）
- 細節 link Stage Roadmap 不複述 / 超字數 → 砍重複 / 補 reference 而非展開
- v3.54.0 以下純 link 保留為歷史 format（早期 Sage 自動寫紀律 / 不追溯改）

---

## [Unreleased]

- **v5.5 完整收口 ✅**（Phase 4 全 deliver — 78a → 78b → 78c → 79 → Trial_v23 → 80 → Trial_v24 → 81 → Trial_v25 → 82 → Trial_v26 🟢 → Trial_v27 🟡 → **Stage 83 ✅** → **Stage 84 ✅ PetraOrchestratorService 拆解收口**）
- **下個動作候選**：Stage 85 — Stage 83 phased delivery 留的 12 條 Mock 友善候選（見 [Future_Feature.md](docs/planning/Future_Feature.md) Stage 84+ 候選段）+ 議題 2 minor dangling doc comment patch（`WorkflowSettings.cs` + `Resolver.cs` 6 處 XML doc 失準）

---

## [3.76.0] — 2026-05-24 — [Stage 84](docs/planning/Stage_84_Roadmap.md) PetraOrchestratorService 怪物大檔拆解（pure refactor / 91.5% 瘦身）

`PetraOrchestratorService.cs` 從 2266 → **193 行**（瘦身 91.5%）。新增 8 檔：5 sub-service + 1 static helper（解 TalentDispatch ↔ DynamicReplan 雙向循環）+ 1 DTO 集中 + 1 Commons。v5 IAgentTool ecosystem 整套砍（4 worker class + interface + attribute + flag）對齊 Stage 78a pattern。0 行為改變 / caller 4 處 0 改動 / xUnit 130 passed + 2 skip。SOP 累積第 5 次 single-session 完成 M+ 規模新里程碑 / 升 refactor-sop.md v1.3 3 條 know-how（細節見 [Stage Roadmap](docs/planning/Stage_84_Roadmap.md)）。

---

## [3.75.0] — 2026-05-21 — [Stage 83](docs/planning/Stage_83_Roadmap.md) WebUI 全砍重設計（3 大分區 + Home + Auth）「最後測驗」戰略節點達成

Dashboard 11 頁全砍 → Tasks + Settings + Monitoring 3 大分區 + Home + Auth 獨立 + Office 砍。Migration `Stage83PetraSessionResultPrUrl`。Forge L+++ scope 5 輪修補 + Aria 5 輪 gate1 + Chrome MCP 視覺驗 3 輪 + 9/11 修根因典範 = v5.5 dynamic orchestrator + Dashboard 真實對齊 / v5.5 完整收口進 production 自然累積期。

---

## [3.74.0] — 2026-05-21 — [Stage 82](docs/planning/Stage_82_Roadmap.md) Quinn outputLen 修根因（路線 A stream-json）+ Trial_v25 三 🟡 議題收口

ClaudeCodeService 切 `stream-json --verbose` + ParseJsonOutput NDJSON line-by-line accumulate（解 Quinn tool-heavy 場景 final turn tool_use → result 空 議題）。TokenTrackingProvider AsyncLocal `PetraSessionAmbient` 透傳 PM call site。SubtaskPlanParser 加 StripPreambleAndPostamble robust 防呆。Petra Sonnet 4.6 切 production active default（Stage 38 DB SoT）。

---

## [3.73.0] — 2026-05-20 — [Stage 81](docs/planning/Stage_81_Roadmap.md) B 動態 re-planning + HITL retry gate 配套 + Trial_v24 議題收口

Christ 親口要的業務功能。`DetectReplanTrigger` Regex（Vera critical / Quinn fail）+ `InvokePetraReplanAsync` retry instruction（LangGraph cycles 業界紀律）+ 4 decision routing（approve / edit / reject / respond）+ `replan_confirm` 卡 UI 重用 Stage 80 infra。`ReplanIteration` + `SessionCostUsd` + AppSetting 3 cap default 守 baseline。3 know-how 升級到 conventions/skill。

---

## [3.72.0] — 2026-05-20 — [Stage 80](docs/planning/Stage_80_Roadmap.md) A HITL plan confirmation 閘門 + Trial_v23 議題收口

Christ 親口要的業務功能。BossInteraction `plan_confirm` type + HITL pause point + 4 decision resume routing（approve / edit / reject / respond）+ InteractionCard UI + `PlanConfirmationProcessor` BackgroundService（Stage 78c 砍 InteractionProcessor 後新建 / 對齊既有 PetraInboxProcessor 紀律）+ AppSetting `UseHITLPlanConfirmation` default false 守 baseline。修 Trial_v23 揭 Blazor InteractiveServer 並行 Scoped DbContext 根因（IDbContextFactory 並存註冊）。2 know-how 升級到 conventions/skill。

---

## [3.71.0] — 2026-05-19 — [Stage 79](docs/planning/Stage_79_Roadmap.md) v5.5 image flow 補完

Stage 75 PetraInbox 設計遺漏 image flow 修根因。PetraInbox.Attachments jsonb + Migration + SubtaskPlan.NeedsImageContext 條件性 worker propagation（業界「pass images only to worker agents that need them」）+ ClaudeCodeChatClientAdapter workspace .tmp/images/ 寫圖檔 + GeminiProvider multimodal native + Dashboard 4 層 validate 動態化（AppSetting MaxAttachmentsPerTask=5 + MaxAttachmentSizeMB=5）。Trial_v23 啟動條件達成。

---

## [3.70.0] — 2026-05-19 — [Stage 78c](docs/planning/Stage_78c_Roadmap.md) v5.5 Phase 4 候選 C 最終收口 — v4 Pipeline framework 整套砍除（**MAJOR refactor / 108 檔變動 net -22690 行**）

v4 path 全部砍乾淨（0 dead code / 0 dead caller / 0 dead routing / v5.5 path 唯一 source）。6 鏈砍：Pipeline + Meeting+Appeal+HITL+Boss + Queue+Group + IAgentExecutor 部分 + Discord routing 重整（ButtonCallbackRouter 713→~120 / SlashCommandRouter 整檔砍）+ WebhookController 整檔砍。0 Migration（agent_queues 表真實不存在 / Stage 78c spike 揭）。Build 0 warning（歷史最低）。WebUI Stage 預備重設計範圍。

---

## [3.69.0] — 2026-05-19 — [Stage 78b](docs/planning/Stage_78b_Roadmap.md) v5.5 Phase 4 候選 C 後續 — v4 path dead caller 整套砍除

純 refactor。ButtonCallbackRouter v4 routing 砍 + HandleConfirmYesAsync v4 body cascade 砍 + OpsAgentService IAgentExecutor 實作砍（保 HealthCheckJob production active）+ `/task` slash command 砍 + GitHub Issue webhook handler 砍 + CeoAgentService.ProcessAsync v4 fallback 砍。8 檔 net -787 行 / build warning 102 → 59 (-42%)。Stage 78c 預留 v4 Pipeline 整套砍。

---

## [3.68.0] — 2026-05-18 — [Stage 78a](docs/planning/Stage_78_Roadmap.md) v5.5 Phase 4 C — v4 path dead code 砍除

砍 3 純 v4 class（Rosa/Demi/Release ~1150 行）+ 4 雙路徑 class v4 method ~2900 行（Doc/Dev/Reviewer/Qa 留 v5.5 IAgentTool）+ Adapter capability 7→4 + DefaultSkillRegistry 6→4。LlmProviderFactory 系列全保留（Petra 3 call sites active）。CLAUDE.md production path 修根因（Petra LlmProviderFactory / 非 Claude Code CLI）。Stage 78b 預留 ButtonCallbackRouter v4 routing + IAgentExecutor + AgentQueueProcessor + OpsAgent。

---

## [3.67.0] — 2026-05-18 — [Stage 77](docs/planning/Stage_77_Roadmap.md) v5.5 Phase 3 補強 — fire-and-forget A2 業界推薦完整版

PetraInboxChannel BoundedChannel + PetraDispatchWorker N=3 multi-consumer Task.WhenAll + PetraInboxProcessor 退化 pure producer + StopAsync graceful shutdown drain 4 階段 30 min timeout + `Workflow:MaxConcurrentPetra=3`。Stage 76 retry path 整套搬遷 0 邏輯改變。業界 7 議題 WebSearch 完整 incorporated。Trial_v22 啟動條件達成。

---

## [3.66.0] — 2026-05-18 — [Stage 76](docs/planning/Stage_76_Roadmap.md) v5.5 Phase 3 補強 — task retry / resume 機制

PetraInbox schema 擴 4 欄（AttemptCount/MaxAttempts/NextRetryAt/DeadAt）+ Migration + retry path 3 路分支（Transient retry exponential backoff 30s×2×max3 + ±20% jitter / BusinessRule+Permanent fail-fast）+ PetraErrorClassifier + Dead Letter pattern + Dashboard 重跑按鈕。ef-core.md 升級 Migration AddColumn defaultValue 對齊 entity C# initializer 紀律。

---

## [3.65.0] — 2026-05-17 — [Stage 75](docs/planning/Stage_75_Roadmap.md) v5.5 Phase 3 兩層 queue 配套

Petra 接收層 + Worker 執行層 per-Talent serialization：PetraInbox table + PetraInboxProcessor BackgroundService + CeoAgentService 寫 inbox + TalentDispatchLockService SemaphoreSlim per-Talent + DispatchTalentsAsync per-Talent lock wire。Forge spike 修根因 `talentNameToIdMap` unconditional build。

---

## [3.64.0] — 2026-05-17 — [Stage 74](docs/planning/Stage_74_Roadmap.md) v5.5 Phase 3 per-Skill Model + 真並行 dispatch

TalentSkill schema 擴 Provider/Model + TalentSkillModelResolver 三層 fallback + ClaudeCodeChatClientAdapter 動態 Model 整合 + SubtaskPlanLevelGrouping DAG fan-out 路線 A + SkillDescriptor metadata 對齊 Agent Skills open standard。

---

## [3.63.0] — 2026-05-17 — [Stage 73](docs/planning/Stage_73_Roadmap.md) v5.5 Phase 3 Prompt content 升級 + Petra TalentPrompt persona seed

6 SkillPrompt v1→v2 走 versioning path + Petra 4 拍板特質 persona（謹慎拍板 / 對冗餘不容忍 / 持續迭代 / 對等和互相）。對齊「品質 > 做法」精神。

---

## [3.62.0] — 2026-05-17 — [Stage 72](docs/planning/Stage_72_Roadmap.md) v5.5 Phase 2 Prompt DB 化 + Talent identity 整合

SkillPrompts + TalentPrompts 兩層 schema + Versioning + rollback + PromptRepository CRUD + PromptResolver 5-min TTL cache。對齊業界 2026 prompt orchestration 主流。

---

## [3.61.0] — 2026-05-16 — [Stage 71](docs/planning/Stage_71_Roadmap.md) v5.5 Phase 2 production-ready 補強

Trial_v15+v16 揭 2 議題收口 — Petra prompt 線性整包紀律 + memory 空 content guard。

---

## [3.60.0] — 2026-05-16 — [Stage 70](docs/planning/Stage_70_Roadmap.md) v5.5 Phase 2 Petra 拆解指令精準度

hierarchical decomposition + dependency graph / SubtaskPlan + Parser + TopoSort / Backwards-compatible 4 層守護。

---

## [3.59.0] — 2026-05-16 — [Stage 69](docs/planning/Stage_69_Roadmap.md) v5.5 Phase 2 跨 session 長期持久記憶基底

TaskMemory + TalentMemory schema 整合 v5.5 dispatch。v2.1 scope pivot TaskGroup → PetraSession 修 Aria 漏掃 v5.5 path 根因。

---

## [3.58.0] — 2026-05-16 — [Stage 68](docs/planning/Stage_68_Roadmap.md) v5.5 Phase 1 完整收口前 production-ready 補強

AppendMessage async + v5 PoC post-confirm 收尾 + ef-core.md nullable unique pattern 升級。

---

## [3.57.0] — 2026-05-15 — [Stage 67](docs/planning/Stage_67_Roadmap.md) v5.5 升級首發 Phase 1 Talent-Skill separation 重構基底

架構級重構 / Migration `Stage67TalentSkillSeparation` / Trial_v13 啟動條件達成。

---

## [3.56.0] — 2026-05-14 — [Stage 66](docs/planning/Stage_66_Roadmap.md) v5 動態架構 Trial_v11 議題收口

production-ready 補強第三波 / v5 上線前最後一個工程 Stage / Trial_v12 啟動條件達成。

---

## [3.55.0] — 2026-05-14 — [Stage 65](docs/planning/Stage_65_Roadmap.md) v5 動態架構 Trial_v10 議題收口

production-ready 補強第二波 + 結案後 merge feature/v5-poc → main。

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
