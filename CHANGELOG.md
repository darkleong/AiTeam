# Changelog

本專案所有重要變更紀錄於此檔。

格式參照 [Keep a Changelog 1.1.0](https://keepachangelog.com/zh-TW/1.1.0/)，版本號遵循 [Semantic Versioning](https://semver.org/lang/zh-TW/)。
工作單位為 **Stage**，每完成一個 Stage 通常對應 minor 版本 bump。每條 entry 的細節見對應 Stage Roadmap；更深的實作 commit 訊息見 git log。

未實作功能候選見 [Future_Feature.md](docs/planning/Future_Feature.md)。

---

## [Unreleased]

- **下個動作候選**：Stage 74 開（v5.5 Phase 3 Step 8 — 真並行 dispatch + 戰略決策層 3 agent debate / 預估 cost $3-5 per cycle）。Trial_v19 🟢 全綠驗收通過 + v5.5 Phase 3 Step 7 正式完整收口（Stage 73 升級後 prompt content 真實生效 + Petra TalentPrompt persona 4 拍板特質 production 真實實證 + 連續 9 Trial 業務級成功 v10-v19 + 連續 7 Stage 0 follow-up 67-73）。**Phase 3 完整收口路徑**：73 ✅ → 74 → 76（兩層 queue 配套）→ 75（WebUI Talent CRUD 最後做）→ v5.5 完整收口。

---

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
