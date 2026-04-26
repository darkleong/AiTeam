# Changelog

本專案所有重要變更紀錄於此檔。

格式參照 [Keep a Changelog 1.1.0](https://keepachangelog.com/zh-TW/1.1.0/)，版本號遵循 [Semantic Versioning](https://semver.org/lang/zh-TW/)。
工作單位為 **Stage**，每完成一個 Stage 通常對應 minor 版本 bump。每條 entry 的細節見對應 Stage Roadmap；更深的實作 commit 訊息見 git log。

未實作功能候選見 [Future_Feature.md](docs/planning/Future_Feature.md)。

---

## [Unreleased]

- **Stage 40（規劃中）**：FF 二十九 `CLAUDE_Vera.md` 判準補強（`rel="noopener"` Critical / `<a>` aria-label Warning / pattern match Warning）+ 順手修 PR #109 兩遺漏 + `Home.razor` 同步 + `ExtractPrNumber` 抽 helper

---

## [3.26.0] — 2026-04-25 — [Stage 39](docs/planning/Stage_39_Roadmap.md)

Vera 審查擴及 `.razor` / `.css`（FF 二十八）；新增 `AgentResultType.Skipped` 結果型別 + Dashboard 全鏈路 teal 配色

## [3.25.0] — 2026-04-25 — [Stage 38](docs/planning/Stage_38_Roadmap.md)

Dashboard Provider/Model 動態化（FF 四第二階段 2-A）：DB SoT + `AgentConfigCache` + `LlmModels.cs` 常數白名單

## [3.24.0] — 2026-04-25 — [Stage 37](docs/planning/Stage_37_Roadmap.md)

GeminiProvider API 層（FF 四第一階段）+ Crash Recovery 全面涵蓋（5 種 `ActiveOrchestration`）

## [3.23.0] — 2026-04-22 — [Stage 36](docs/planning/Stage_36_Roadmap.md)

TaskGroupService + CommandHandler 拆解（FF 二十 A+B 合併）：4795 行 → 1272 行（-73%）；**AiTeam 四怪物級檔案技術債清零** 🎉

## [3.22.0] — 2026-04-22 — [Stage 35](docs/planning/Stage_35_Roadmap.md)

PmAgentService 拆解（FF 二十-D）：1388 行 → 6 個子 service；首次實踐 SOP 6（子資料夾 `Agents/Pm/`）

## [3.21.0] — 2026-04-22 — [Stage 34](docs/planning/Stage_34_Roadmap.md)

MeetingService 拆解（FF 二十-C）：1415 行 → KickoffMeetingService + DesignMeetingService + Commons + Results

## [3.20.0] — 2026-04-22 — [Stage 33](docs/planning/Stage_33_Roadmap.md)

Agent 狀態卡 2.0：佇列控制 Dashboard 化（per-agent pause/resume + 全域 stop-all）+ 待辦清單 expand + 深層連結

## [3.19.0] — 2026-04-21 — [Stage 32](docs/planning/Stage_32_Roadmap.md)

`/mock` Dashboard 化 + Mock Delay / WorkflowSettings 動態化（從 AppSettings 讀，免重啟容器）

## [3.18.0] — 2026-04-20 — [Stage 31](docs/planning/Stage_31_Roadmap.md)

可靠性補強：Dashboard 重試按鈕 + 會議 Crash Recovery + Appeal 對抗紀錄 UI（FF 十七 + 十八）

## [3.17.0] — 2026-04-20 — [Stage 30](docs/planning/Stage_30_Roadmap.md)

申訴迴圈 LLM API → Claude Code CLI 全面升級（5 個環節新開 session + 唯讀工具）

## [3.16.1] — 2026-04-19 — Hotfix

MockMode 提案核准重複建 TaskGroup bug 修正（Dashboard 路徑補 GroupId 防護對齊 Discord 路徑）

## [3.16.0] — 2026-04-19 — [Stage 29](docs/planning/Stage_29_Roadmap.md)

Dashboard 操作性收尾 + CEO 指令通道擴充（Dashboard 直接下指令給 Victoria，含圖片附件）

## [3.15.0] — 2026-04-17 — [Stage 28b](docs/planning/Stage_28b_Roadmap.md)

Dashboard 雙向操作中心 — 文字輸入互動 + 歷史紀錄篩選

## [3.14.0] — 2026-04-17 — [Stage 28a](docs/planning/Stage_28a_Roadmap.md)

Dashboard 雙向操作中心 — 基礎架構 + 8 個確認點按鈕回覆 + 樂觀鎖先到先贏

## [3.13.0] — 2026-04-16 — [Stage 27b](docs/planning/Stage_27b_Roadmap.md)

Agent 任務序列 — 操作性與可觀察性（5 個 Discord 指令 + Dashboard 佇列視覺化 + SignalR）

## [3.12.0] — 2026-04-16 — [Stage 27a](docs/planning/Stage_27a_Roadmap.md)

Agent 任務序列 — 核心佇列機制（DB-as-Queue + AgentQueueService + per-agent SemaphoreSlim + Crash Recovery）

## [3.11.0] — 2026-04-14 — [Stage 26](docs/planning/Stage_26_Roadmap.md)

驗收基礎設施（PipelineView 折疊面板 + MockMode 修正）+ 版本號集中管理（`Directory.Build.props`）

## [3.10.0] — 2026-04-14 — [Stage 25b](docs/planning/Stage_25b_Roadmap.md)

開發流程重構 Phase 1d — 設計規劃階段（5 人設計會議 + 條件式 Christ 確認）

## [3.9.0] — 2026-04-14 — [Stage 25a](docs/planning/Stage_25a_Roadmap.md)

開發流程重構 Phase 1c — Kick-off 會議機制（Claude Code 持續對話 session + 多 Agent 會議）

## [3.8.0] — 2026-04-13 — [Stage 24](docs/planning/Stage_24_Roadmap.md)

開發流程重構 Phase 1b — QA Petra 介入 + Dev_plan 審核強化 + TestReport 結構化存 DB

## [3.7.0] — 2026-04-12 — [Stage 23](docs/planning/Stage_23_Roadmap.md)

開發流程重構 Phase 1a — Review Appeal 迴圈 + Sage 轉型歸檔員 + Git Tag 自動化

## [3.6.0] — 2026-04-12 — [Stage 22](docs/planning/Stage_22_Roadmap.md)

Dashboard 存取分層（localhost bypass）+ Token 守門 4 層攔截 + `#指令中心` 頻道清理

## [3.5.0] — 2026-04-11 — [Stage 21](docs/planning/Stage_21_Roadmap.md)

`docs/` 資料夾重整（architecture / planning 子資料夾）+ SemVer 導入

## [3.4.0] — 2026-04-11 — [Stage 20](docs/planning/Stage_20_Roadmap.md)

Dashboard 全面換 MudBlazor Layout（MainLayout → MudLayout + Dark Mode → MudThemeProvider）

## [3.3.0] — 2026-04-10 / 04-11 — [Stage 19](docs/planning/Stage_19_Roadmap.md)

Dashboard UI 全面打磨（三批 18 項：StatusBadge / MudChip / MudIcon / MudStack / 側邊欄 localStorage 等）

## [3.2.0] — 2026-04-09 — [Stage 18](docs/planning/Stage_18_Roadmap.md)

Dashboard 可觀測性升級：Agent 狀態卡即時更新 + Pipeline View（MudStepper + MudTimeline）

## [3.1.0] — 2026-04-08 — [Stage 17](docs/planning/Stage_17_Roadmap.md)

Mock Mode：`IClaudeCodeService` 代理模式 + Dashboard 開關 + 4 種 `/mock` 流程

## [3.0.0] — 2026-04-07 — [Stage 16](docs/planning/Stage_16_Roadmap.md)

**MAJOR**：PM Agent（Petra）品質審核閘門；Vera / QA 重構為單一 Claude Code session

## [2.4.0] — 2026-04-06 — [Stage 15](docs/planning/Stage_15_Roadmap.md)

Victoria 接上 Claude Code + Session 對話持久化 + 長期記憶

## [2.3.0] — 2026-04-06 — [Stage 14](docs/planning/Stage_14_Roadmap.md)

CEO 分類補強：技術改善分類 + Release / Ops / Doc 直接路由 + 任務取消能力

## [2.2.0] — 2026-04-06 — [Stage 13](docs/planning/Stage_13_Roadmap.md)

系統穩定性與流程修正：Dev → Reviewer → QA → Doc 串行 + 單一 PR + Closes #XX 自動關 Issues

## [2.1.0] — 2026-04-06 — [Stage 12](docs/planning/Stage_12_Roadmap.md)

提案流程全面升級：Rosa / Demi 串行協作 + 唯讀探索 + UI 規格存 DB + Discord 附件

## [2.0.0] — 2026-04-05 — [Stage 11](docs/planning/Stage_11_Roadmap.md)

**MAJOR**：Dev Agent（Cody）驅動 Claude Code CLI 自主開發

## [1.4.0] — 2026-04-03 — [Stage 10](docs/planning/Stage_10_Roadmap.md)

開發流程自動閉環：CEO Orchestrator + WorkflowEngine + Review 閉環 + Ops Rollback

## [1.3.1] — 2026-04-04 — Hotfix

Stage 10 驗收後 7 項修正（Race Condition / IssueUrls 重複 / PushStatus / dead code 清理 / EF Index）

## [1.3.0] — 2026-04-03 — [Stage 9](docs/planning/Stage_9_Roadmap.md)

CEO 升級 + 可觀測性：Token 監控 Dashboard + CEO 智慧分類 + 提案模式 + QA Playwright

## [1.2.0] — 2026-04-02 — [Stage 8](docs/planning/Stage_8_Roadmap.md)

系統可靠性與操作體驗：動態 AppSettings + per-agent Rules + Dark Mode + Notion 移除

## [1.1.0] — 2026-04-02 — [Stage 7](docs/planning/Stage_7_Roadmap.md)

Software Team 完全體：Reviewer / Release / Designer Agent + CI/CD + Discord 重設計 + 自然語言對話

## [1.0.0] — 2026-04-01 — [Stage 6](docs/planning/Stage_6_Roadmap.md)

**MAJOR**：強化、驗收與技術債清償（Discord Vision、MudBlazor、Requirements 三層確認、E2E 驗收等 12 項）

## [0.4.0] — 2026-04-01 — [Stage 5](docs/planning/Stage_5_Expansion.md)

擴充 Agent：QA / Doc / Requirements + 動態 Agent 框架

## [0.3.0] — 2026-03-31 — [Stage 4](docs/planning/Stage_4_Dashboard.md)

Blazor Web App Dashboard（Identity + SignalR + Aspire 基礎）

## [0.2.0] — 2026-03-31 — [Stage 3](docs/planning/Stage_3_Agents.md)

第一批 Agent 上線：CEO / Dev / Ops（Anthropic Claude API）

## [0.1.0] — 2026-03-31 — [Stage 1](docs/planning/Stage_1_Design.md) + [Stage 2](docs/planning/Stage_2_Foundation.md)

基礎建設：系統設計確定 + Discord Bot + Aspire AppHost + PostgreSQL
