# Future Feature — 未來功能候選清單

> 版本：v16.0
> 建立日期：2026-04-01
> 最後更新：2026-05-26（v16.0 — Phase v4-rewrite Stage 88-95 結案 / AiTeam 從「執行 + 記錄」轉「純記錄 MCP server」/ 加入 v4-rewrite Followup section）
> 詳細實作紀錄：[CHANGELOG.md](../../CHANGELOG.md) + [Architecture.md](../Architecture.md)

---

## Active 清單

| # | 標題 | 狀態 |
|---|---|---|
| 一 | v5.5 完整收口 + Stage 88+ 候選 | ✅ Phase 1-4 全 deliver + Stage 84 拆解收口 + Stage 85 Dashboard 救火 + Stage 86 Dashboard 改造 + Stage 87 改造收口 + v4 LLM SoT 統一 / Dashboard 改造 3 Stage 大段落結束 |
| 二 | 客戶專案交付流程與驗收閘門 | 🟡 中優先級（AiTeam 開始承接客戶專案時前置必要） |
| 三 | **Phase v4-rewrite Followup**（v4.0.0 結案後候選清單）| ✅ 已彙整於 [Phase_v4_Followup.md](Phase_v4_Followup.md)（F1-F6 / 含舊 entity drop + cascading reference 修 / token milestone push / Dashboard 進階功能 / Channels 整理 / NavMenu 重評 / petra-pm 微調）|

---

## 一、v5.5 完整收口 + Stage 88+ 候選

### Phase 1-4 完整收口（2026-05-15 → 2026-05-21）

| Phase | Stage 範圍 | 主題 |
|---|---|---|
| Phase 1（基礎重構）| [67](Stage_67_Roadmap.md) / [68](Stage_68_Roadmap.md) | Talent-Skill separation |
| Phase 2（核心動態化）| [69](Stage_69_Roadmap.md) / [70](Stage_70_Roadmap.md) / [71](Stage_71_Roadmap.md) / [72](Stage_72_Roadmap.md) | 持久記憶 + Petra 拆解 + Prompt DB 化 |
| Phase 3（進階機制）| [73](Stage_73_Roadmap.md) / [74](Stage_74_Roadmap.md) / [75](Stage_75_Roadmap.md) / [76](Stage_76_Roadmap.md) / [77](Stage_77_Roadmap.md) | Prompt v2 / per-Skill Model + DAG fan-out / 兩層 queue / retry / fire-and-forget A2 |
| Phase 4（v4 砍 + 動態完整 + WebUI）| [78a](Stage_78_Roadmap.md) / [78b](Stage_78b_Roadmap.md) / [78c](Stage_78c_Roadmap.md) / [79](Stage_79_Roadmap.md) / [80](Stage_80_Roadmap.md) / [81](Stage_81_Roadmap.md) / [82](Stage_82_Roadmap.md) / [83](Stage_83_Roadmap.md) | v4 path 全砍 / image flow / HITL plan_confirm + replan_confirm / WebUI 重設計 |

### Trial 真實業務驗（Phase 4 期間）

| Trial | 結果 | 對應 Stage |
|---|---|---|
| [Trial_v23](../experiments/Trial_v23_Plan.md) | 🟡 部分過 | 78a / 78b / 78c / 79 連 4 Stage |
| [Trial_v24](../experiments/Trial_v24_Plan.md) | 🟢 全綠 | 80 HITL plan_confirm |
| [Trial_v25](../experiments/Trial_v25_Plan.md) | 🟡 部分過 | 81 動態 replan / 揭 Quinn outputLen 修根因錯方向 |
| [Trial_v26](../experiments/Trial_v26_Plan.md) | 🟢 | 82 雙修法 + 業界 supervisor pattern 對齊驗證 |
| [Trial_v27](../experiments/Trial_v27_Plan.md) | 🟡 戰略性結案 | LLM alignment 雙重 safety net 實證 / 跳出 Trial→Fix 迴圈 |

### Stage 88+ 候選

從 Stage 83 phased delivery 留的 12 條（#13 已由 [Stage 84](Stage_84_Roadmap.md) deliver / #4 + #10 已由 [Stage 86](Stage_86_Roadmap.md) deliver — Theme 即時切換 C# event 改造 + MudThemeProvider IsDarkMode binding 紀律）。Stage 85 救火 + Stage 86 改造 + Stage 87 收口已 cover Christ 2026-05-24 痛點盤點 11 條 + Dashboard 改造 3 Stage 大段落徹底結束。

| # | 候選 | 規模 |
|---|---|---|
| 1 | ExtractPrNumber DRY refactor（TaskHub reuse PrNumberHelper） | XS |
| 2 | Chrome MCP click stale spike（Aria 視覺驗收環境議題）| S |
| 3 | WorkflowFlags UseHITLPlanConfirmation + UseDynamicReplanning toggle on production 拍板 | XS |
| ~~4~~ | ~~Theme 即時切換 C# event 改造~~ | ✅ Stage 86 deliver |
| 5 | Bug 4 既有 row PR URL retroactive backfill（從 PetraSessionMessages parse）| S |
| 6 | Monitoring 警戒線 MudChart ReferenceLine + per-Skill 維度（Stage 86 已 deliver per-Skill 維度 / per-Skill 維度 stub 仍待補真實 data path）| M |
| 7 | Bot /internal/health Discord 連線真實 check | XS |
| 8 | MockMode 4 流程觸發 UI button | S |
| 9 | env naming convention 紀律升級 → CLAUDE.md ops SoP（已部分完成）| XS |
| ~~10~~ | ~~MudThemeProvider IsDarkMode binding 紀律~~ | ✅ Stage 86 mudblazor.md v1.6 deliver |
| 11 | AppSettingsService 5 分鐘 re-read（21 Workflow:* flag 動態 reload 不需重啟 Bot）| M |
| 12 | DashboardService 進一步重組（Home / TaskHub PetraSession query 直接走 Repository）| S |

**Stage 84 follow-up minor 候選**（Stage 85 sweep dangling comment 但**未**順手清這 6 處 / 仍待處理）：
- 🟢 `WorkflowSettings.cs` + `WorkflowSettingsResolver.cs` 6 處 XML doc 改寫「必須 `UseTalentSkillSeparation=true` 才有意義」失準 dangling comment 清理（flag 已 Stage 84 砍 / doc 殘留）/ Test29-30 真實搬 PetraTalentDispatchServiceTests（Stage 84 Skip 標記）

**Stage 85 follow-up minor 候選**（Aria gate1 audit 揭 / Stage 86+ 真實業務痛點觸發再處理）：
- 🟢 xunit collection-fixture seq lock 修 `ClaudeCodeChatClientAdapterTests` 並發 file lock race（Stage 85 build 自驗首跑 1 fail / 單跑 9/9 pass / 既有 test infra 並發問題跟 Stage 85 0 關係 / commit message 數對齊紀律對未來 Stage 仍有用）

**追加候選**（Trial_v26 戰略 finding）：
- 🟡 Vera review 紀律升級（system prompt / few-shot 更多 critical 反例）
- 🟡 Read-only Codebase Explorer agent（獨立 worker / 給 Petra 第二來源訊號）
- 🟡 SubtaskPlanParser 對 Petra refuse JSON fallback 升級（detect `{"error":"dispatch_rejected"}` → 直接 escalate）

**Dashboard UX 改造候選**（2026-05-24 Christ 真實使用觸發）：
- ✅ **左側 nav 階層化改造**：Stage 87 deliver（3 Hub 拆 14 sub-page + URL 階層 routing + MudNavGroup 二層 + SignalR 細粒度訂閱 / 對應 [Stage 87 Roadmap](Stage_87_Roadmap.md) B 軸）

**Stage 87 follow-up minor 候選**（Stage 87 結案踩坑 + Christ 視覺驗收期觀察 / Forge plan 揭範圍外 / 等真實業務痛點觸發再處理）：
- 🟢 **SignalR 跨 tab 同步推送**：當前 Dashboard CRUD save 後 0 push 對應 SignalR event / SettingsTalents / SettingsSkillPrompts / SettingsTalentPrompts 三 page 已加 HubConnection 訂閱 `ReceiveTaskUpdate` 框架但 Bot 端 / Dashboard service 0 對應 push → 跨 tab 同步不會發生 / 需 Bot Internal API + IHubContext.SendAsync 補完才有真實同步。當前 use case 低（Christ 多 tab 同改 Talent / Skill 配置場景少）/ 等真實業務痛點觸發再升級

**評估時機**：Christ 真實使用累積痛點觸發後動工 / Aria 不主動推（對齊 v5.5 完整收口 + production 自然累積期路線）。

### 不確定性 + 待驗證

1. **戰略決策層的 3 agent 配置** — Trial_v26 揭 Vera review 紀律升級 + Read-only Codebase Explorer 兩條 path 候選
2. **Petra 拆解指令精準度極限** — Stage 81 動態 replan 已實裝 / Trial_v25-v27 驗證業界 supervisor pattern 對齊 / 持續觀察 production fire 結果
3. **Provider 切換 cost-quality trade-off** — Stage 82 Sonnet 4.6 production active default 對齊 reliability > cost / Gemini Flash 留 fallback

---

## 二、客戶專案交付流程與驗收閘門

### 背景

AiTeam 定位不只開發自身系統 / 未來也會替客戶開發。目前流程（merge 後自動部署）對 AiTeam 自身足夠 / 但對客戶專案風險層級不同：

| | AiTeam 自身 | 客戶專案 |
|---|---|---|
| **壞掉代價** | 自己工具壞了 | 客戶系統壞了 |
| **git revert** | 可接受 | 不可接受 |
| **merge 後再測** | OK | ❌ 太晚 |
| **驗收責任** | 自己 | 對客戶負責 |

目前流程產出 GitHub PR / 但 merge 前沒 Preview 環境可人工驗收 / 直接 merge 等於直接上客戶 production。

### 期望行為

```
需求 → ... → PR 開出
       ↓
   Preview 環境自動部署（Staging）
       ↓
   Victoria 通知 Christ：「PR #N 已部署至 staging，請驗收」
       ↓
   Christ（或客戶）在 Staging 實際操作驗收
       ↓
   驗收通過 → Merge → Production 自動部署
   驗收失敗 → Christ 回覆問題 → 修正循環
```

### 需要的兩個東西

1. 每個客戶專案都有一個 Staging 環境（不一定在本機）
2. AiTeam 流程加入正式人工驗收閘門（Victoria 等待 approve 才算 Done）

### 待釐清的子問題

1. **客戶專案的 Staging 環境由誰負責？** — 客戶自己有 staging server？還是 AiTeam 在本機幫每個專案起 container？
2. **AiTeam 是否該管理「部署到客戶環境」？** — 目前 ops 流程只針對 AiTeam 自身
3. **驗收失敗的循環怎麼設計？** — Christ 修改意見要怎麼餵回 Victoria → 再分派給對應 Talent？

### 初步討論結論（2026-04-12）

**部署到 IIS Web Deploy 的能力**：
- 走 **GitHub Actions + Web Deploy** 模式 — 客戶 repo 掛 workflow / push 時自動 `msdeploy` 部署到 IIS
- Talent 不需要直接操作部署 / 只負責 push code / CI/CD 負責部署（與 AiTeam 自身模式一致）
- 每個客戶專案設定一次 workflow 即可

**Git Flow 多環境部署**：
- 可行 / 需調整 Cody 的 PR 目標分支策略
- Cody 的 PR 目標從 `main` 改為 `develop`
- GitHub Actions 依 branch 觸發不同部署目標：feature→開發環境 / develop→測試環境 / master→production
- Victoria 需理解 Git Flow 各階段（merge 到 develop ≠ 上線）
- 人工驗收閘門：feature 部署到開發環境後 / 等 approve 才 merge 到 develop

### 優先級

🟡 中優先級 — AiTeam 開始承接客戶專案時前置必要 / 目前仍以自身開發為主。

---

## 版本歷史

| 版本 | 日期 | 變更 |
|---|---|---|
| v10.0 | 2026-05-21 | Stage 83 結案 / v3.75.0 — v5.5 完整收口進 production 自然累積期 |
| **v11.0** | **2026-05-22** | **v5.5 子檔合進主檔** — Future_Feature_v5.5.md（239 行）整檔砍 / Phase 1-4 audit + Stage 84+ 候選 12+3 條 + 不確定性 3 條合進本檔一、v5.5 完整收口段 / 二、客戶交付段修 Maya（Stage 78a 砍）+ WorkflowEngine（Stage 78c 砍）漂移為「Talent / 分支策略」/ 結構從 95 + 239 = 334 → ~140 行 / 對齊「對冗餘不容忍 / single source of truth / v5.5 已收口不再是規劃 reference」精神 |
| **v12.0** | **2026-05-24** | **Stage 84 結案** — #13 PetraOrchestratorService 拆解 deliver（v3.76.0 / 2266 → 193 行 / 瘦身 91.5%）/ Stage 84+ → Stage 85+ 重命名（12 條留下）/ 加「Stage 84 follow-up minor 候選」段（dangling doc comment 6 處 + Test29-30 真實搬遷）|
| **v12.1** | **2026-05-24** | **加 Dashboard UX 改造候選段** — 左側 nav 階層化改造（規模 L）/ 來源：2026-05-24 Christ 真實業務 dogfood 第一單 AiTeam dispatch 撞 TokenGuard 月限 fire 切換策略後保留 |
| **v13.0** | **2026-05-24** | **Stage 85 結案** — Dashboard 救火 5 子項全 deliver（v3.77.0 / DbContext bug 修 + 三層 alert + v4 dead flag 11 個 + 分頁結構 dup + paused session timeout）/ Stage 85+ → Stage 86+ 重命名 / Active 清單表「Phase 1-4 + Stage 84 + Stage 85」+ Stage 86/87 規劃中描述 / 加「Stage 85 follow-up minor 候選」段（xunit 並發 race）|
| **v14.0** | **2026-05-24** | **Stage 86 結案** — Dashboard 改造 7 子項全 deliver（v3.78.0 / Rules dead config + 砍版本/Stage 編號 + 視覺一致性 catch-all 7 項 + 附檔預覽 + Monitoring 改善 + Sidebar hover/pin + Theme 機制根因修）/ Forge 7 commit + Christ 視覺驗收 22 commit follow-up / Stage 86+ → Stage 87+ 重命名 / Active 清單表「+ Stage 86」 + Dashboard UX 改造候選（nav 階層化）整合進 Stage 87+ 範疇 / 升 refactor-sop v1.6 + mudblazor.md v1.6（5 條紀律補強）|
| **v15.0** | **2026-05-25** | **Stage 87 結案** — Dashboard 改造收口 + v4 LLM 配置 SoT 統一 catch-all L+ 三軸合併（v3.79.0 / A 軸 v4 collapse 最後殘留：agent_configs 表 drop + Petra 配置遷 talents.Petra row + AGENTS 分頁砍 + TALENTS 擴 LLM 設定 UI / B 軸 nav 階層化：3 Hub 拆 14 sub-page + MudNavGroup 二層 + SignalR 細粒度訂閱 / C 軸 Rules v4 9 row DELETE）/ Forge 7 commit + Christ 視覺驗收 13 commit follow-up（14 議題）/ Stage 87+ → Stage 88+ 重命名 / Active 清單表「+ Stage 87」+ Dashboard UX 改造候選（nav 階層化）標 ✅ Stage 87 deliver / Stage 87+ 候選 #4 + #10 標已完成（Stage 86 deliver）/ 加「Stage 87 follow-up minor 候選」段（SignalR 跨 tab 同步推送）/ 升 refactor-sop v1.7 + mudblazor.md v1.7（5 條紀律補強）/ Dashboard 改造 3 Stage 大段落徹底收口 |
