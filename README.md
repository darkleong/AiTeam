# AiTeam

以 AI 驅動的軟體開發團隊管理系統。Christ 擔任老闆角色，透過 Discord 下達自然語言指令，AI 團隊（9 個 Agent）負責執行軟體開發與部署任務，**Stage 10 起全流程自動閉環**：從老闆說需求到通知 merge PR，中間所有推進都不需要手動介入。**Stage 11 起 Dev Agent（Cody）透過 Claude Code CLI 自主開發**：自行探索 repo、寫碼、dotnet build 驗證、修錯，直到 build 通過才 commit 開 PR。**Stage 12 起提案流程全面升級**：Rosa / Demi / Vera / Sage 均可透過 Claude Code 唯讀探索 codebase，老闆附圖也能正確解讀，UI 規格從 GitHub commit 改存 DB 並以 Discord 附件傳送。**Stage 13 起流程全面修正**：Dev → Reviewer → QA → Doc 全串行、一個需求只產生一個 PR（code + tests + docs 三個 commit）、Issues 在 PR merge 後自動關閉。**Stage 14 起 CEO 分類補強**：Victoria 新增技術改善分類、Release/Ops/Doc 直接路由、任務取消能力，老闆只需在 #victoria-ceo 一站式指揮所有 Agent。**Stage 15 起 Victoria 升級為有腦的技術顧問**：透過 Claude Code 自主探索 codebase 回答技術問題、讀寫 docs/ 文件並 git push、Session 對話歷史（DB 持久化，30 分鐘 timeout）、長期記憶（跨 session 記住偏好與決策）。**Stage 16 起加入 PM Agent（Petra）品質審核閘門**：Rosa → Petra → Demi → Petra → 老闆確認 → Cody 計畫書 → Petra → Cody coding → Vera → Petra；Vera 重構為單一 Claude Code session（消滅 false Critical）；QA Agent（Quinn）重構為 Claude Code session（Write 工具直接寫測試檔 + dotnet build 驗證）。**Stage 17 起加入 Mock Mode**：Dashboard 一鍵開關，所有 Claude Code / LLM 呼叫切換為模擬結果，不消耗 API 費用；`/mock` 斜線指令可直接觸發四種工作流程測試（新功能 / 含提案 / Bug 修復 / 技術改善）；Runtime 代理模式（ClaudeCodeProxy）切換，5 分鐘內生效，無需重啟容器。**Stage 18 起 Dashboard 可觀測性升級**：首頁 Agent 狀態卡即時更新（執行中 / 閒置 / 錯誤）；任務中心新增「流程追蹤」Tab，點擊 TaskGroup 展開 Pipeline View（垂直 MudStepper + 懶載入 MudTimeline），流程進行中 Stepper 即時更新步驟狀態，不需手動刷新。**Stage 19 Pt.1 Dashboard UI 全面打磨**：StatusBadge 統一元件、PipelineList 獨立頁面（`/pipeline`）、MudTable 規格統一（FixedHeader、Hover、Breakpoint）、MudSwitch 取代自訂 toggle。**Stage 20 Dashboard 全面換 MudBlazor Layout**：`MainLayout` 改用 `MudLayout` + `MudAppBar` + `MudDrawer Persistent`；`NavMenu` 改用 `MudNavMenu`；Dark Mode 改用 CSS 變數 + JS（`html[data-theme="dark"]` 覆寫 `--mud-palette-*`）；TaskLogDrawer / PipelineList / ProjectManagement 三處 `slide-panel` 全換 `MudDrawer Temporary`；建立 `Routes.razor` 宣告全域 `@rendermode InteractiveServer`，讓 Layout、頁面、MudProviders 共享同一 Circuit，解決 scoped DI 隔離問題。

---

## 系統架構

```
你（老闆）
    ↓ Discord 自然語言（在 #victoria-ceo 說話）
CEO Agent（Victoria）—— 從 DB 動態載入 Agent 清單
    ├── Dev Agent（Cody）        （Claude Code CLI 自主開發、Bug 修復、開 PR）
    ├── PM Agent（Petra）        （品質審核閘門：Rosa/Demi/Dev_plan/Vera 產出皆需過審）
    ├── Ops Agent（Maya）        （部署監控、健康檢查告警）
    ├── QA Agent（Quinn）        （Claude Code 產生測試、dotnet build 驗證）
    ├── Doc Agent（Sage）        （自動產出技術文件、開文件 PR）
    ├── Requirements Agent（Rosa）（需求拆解、建立 GitHub Issues）
    ├── Reviewer Agent（Vera）   （Claude Code session Code Review + 影響範圍分析）
    ├── Release Agent（Rena）    （版本管理、Changelog、建立 GitHub Release）
    └── Designer Agent（Demi）   （需求 → MudBlazor UI 規格文件）
         ↓
    結果回報 Discord + 詳細 log 寫入 PostgreSQL
```

即時狀態透過 **Blazor Dashboard** 可視化（SignalR 推送）。

### 核心工具

| 用途 | 工具 |
|------|------|
| 溝通介面 | Discord（Discord.Net）自然語言對話 |
| 規則管理 | PostgreSQL `rules` 資料表（Dashboard CRUD） |
| 詳細 Log | PostgreSQL（EF Core + Npgsql） |
| 視覺化 | Blazor Server Dashboard（MudBlazor） |
| LLM | Anthropic Claude API |
| 排程 | Quartz.NET |
| 部署 | Docker Compose + GitHub Actions CI/CD |

---

## 專案結構

```
AiTeam.sln
src/
├── AiTeam.AppHost/              ← Aspire 入口（PostgreSQL + Bot + Dashboard 編排）
├── AiTeam.ServiceDefaults/      ← Aspire 共用遙測、健康檢查設定
├── AiTeam.Shared/               ← 共用 DTO、介面、常數（AgentNames 等）
├── AiTeam.Data/                 ← EF Core DbContext、Entities、Repositories、Migrations
├── AiTeam.Bot/                  ← Discord Bot 主程式
│   ├── Agents/                  ← IAgentExecutor、各 AgentService、TokenTrackingProvider、ClaudeCodeService
│   │   ├── IClaudeCodeService.cs   ← Claude Code 介面（5 種模式）
│   │   ├── ClaudeCodeService.cs    ← Claude Code subprocess 封裝（RunAsync/ReadOnly/Review/QA/Victoria 五種模式）
│   │   ├── MockClaudeCodeService.cs ← Mock 實作（30~60 秒延遲 + [MOCK] 預設結果）
│   │   ├── ClaudeCodeProxy.cs      ← Runtime 代理（依 MockMode 旗標切換真/假）
│   │   ├── MockLlmProvider.cs      ← Mock LLM（依 systemPrompt 關鍵字回傳對應格式）
│   │   ├── PmAgentService.cs    ← Petra PM Agent（四個審核方法）
│   │   ├── ReviewerAgentService.cs ← Vera（單一 Claude Code session，patch only）
│   │   └── QaAgentService.cs    ← Quinn（Claude Code session，Write + dotnet build 驗證）
│   ├── Resources/               ← Claude Code 行為約束模板
│   │   ├── CLAUDE_Vera.md       ← Vera：只審 + 行、strict Critical 定義
│   │   ├── CLAUDE_Petra.md      ← Petra：審核標準（approve/revise/escalate）
│   │   └── CLAUDE_QA.md         ← Quinn：寫測試檔 + dotnet build 驗證規範
│   ├── Api/                     ← InternalController（/internal/tokens、/internal/deployment 等）
│   ├── Configuration/           ← DiscordSettings、AgentSettings、GitHubSettings
│   ├── Discord/                 ← DiscordBotService、CommandHandler
│   ├── GitHub/                  ← GitHubService、WebhookController
│   ├── Ops/                     ← OpsAgentService、HealthCheckJob
│   └── Services/                ← AppSettingsService、RulesService、DashboardPushService
├── AiTeam.Dashboard/            ← Blazor Server Dashboard
│   ├── Components/Pages/        ← 首頁、任務中心、部署紀錄、Token 監控、Agent 設定、規則管理、專案管理
│   └── Services/                ← DashboardAgentService、DashboardTokenService、DashboardAppSettingsService
└── AiTeam.Tests.Playwright/     ← Playwright E2E 截圖測試（MSTest + Microsoft.Playwright）
docs/
├── 00_Master_Plan.md
├── 01_Vision_and_Architecture.md
├── 02_Infrastructure.md
├── Stage_1_Design.md            ← ✅ 完成
├── Stage_2_Foundation.md        ← ✅ 完成
├── Stage_3_Agents.md            ← ✅ 完成
├── Stage_4_Dashboard.md         ← ✅ 完成
├── Stage_5_Expansion.md         ← ✅ 完成
├── Stage_6_Roadmap.md           ← ✅ 完成
├── Stage_7_Roadmap.md           ← ✅ 完成
├── Stage_8_Roadmap.md           ← ✅ 完成
├── Stage_9_Roadmap.md           ← ✅ 完成
├── Stage_10_Roadmap.md          ← ✅ 完成（含詳細實作紀錄）
├── Stage_11_Roadmap.md          ← ✅ 完成（含踩坑紀錄）
├── Stage_12_Roadmap.md          ← ✅ 完成（含踩坑紀錄）
├── Stage_13_Roadmap.md          ← ✅ 完成（含踩坑紀錄）
├── Stage_14_Roadmap.md          ← ✅ 完成（含踩坑紀錄）
├── Stage_15_Roadmap.md          ← ✅ 完成（含踩坑紀錄）
├── Stage_16_Roadmap.md          ← ✅ 完成（含踩坑五件組 + 架構決策）
├── Stage_17_Roadmap.md          ← ✅ 完成（含踩坑三件組 + Mock Mode 架構）
├── Stage_18_Roadmap.md          ← ✅ 完成（含踩坑五件組 + Pipeline View 架構）
├── Stage_19_Roadmap.md          ← Pt.1 ✅ 完成（Pt.2 規劃中）
├── Stage_20_Roadmap.md          ← ✅ 完成（含踩坑五件組 + MudBlazor Layout 架構決策）
└── Future_Feature.md            ← 未來功能候選清單
```

---

## Discord 頻道結構

```
📁 Software Team
  # victoria-ceo        ← 主要指令中心，用自然語言跟 CEO 說話
  # cody-dev            ← Dev Agent log，可直接指派任務
  # petra-pm            ← PM Agent log，顯示各審核點的 approve/revise/escalate 結果
  # maya-ops            ← Ops Agent log，可直接指派任務
  # quinn-qa            ← QA Agent log，可直接指派任務
  # sage-doc            ← Doc Agent log，可直接指派任務
  # rosa-requirements   ← Requirements Agent log，可直接指派任務
  # vera-reviewer       ← Reviewer Agent log，可直接指派任務
  # rena-release        ← Release Agent log，可直接指派任務
  # demi-designer       ← Designer Agent log，可直接指派任務

📁 系統
  # 任務動態
  # 警報
  # 每日摘要
```

---

## ## CEO Orchestrator（Stage 10）

Stage 10 起，CEO 從「任務路由器」升級為「任務生命週期全程指揮官」。

**新功能完整流程（無需手動推進）：**
```
老闆說需求（可附截圖）
    ↓
CEO 進入提案模式
Rosa（需求拆解，Claude Code 唯讀探索 codebase）→ 建立 GitHub Issues
    ↓
Demi（UI 規格設計，Claude Code 唯讀探索 .razor 頁面）
    ↓
提案書 Embed（UI 規格以 Discord 附件 ui-spec.md 傳送）
  [✅ 核准] [✏️ 需調整] [❌ 取消]
    ↓ 老闆核准
CEO 自動派 Dev（附帶 Issues + UI 規格全文）
    ↓
Dev 開發 → 開 PR（含 Closes #XX 自動關聯 Issues）
    ↓ CEO 自動觸發（串行）
Vera 審查
  🔴 → Dev 修正推同一 branch → Vera 重審（最多 3 輪）
  ✅ → QA 測試（推到同一 branch）
         ↓
       Doc 文件（推到同一 branch）
         ↓
       CEO 通知老闆：「PR 可以 merge 了（含 code + tests + docs）」
    ↓
老闆 merge（一個 PR，Issues 自動關閉）
```

老闆只需要做兩件事：**核准提案書** + **最後 merge PR**。

**Stage 16 後的完整 NewFeature 流程：**
```
Rosa（需求）→ Petra 審核 → Demi（UI 規格）→ Petra 審核 → 老闆確認
    ↓
Cody Dev_plan（實作計畫書）→ Petra 審核 → Cody coding → Vera review
    ↓
Petra 審核（approve → QA → Doc → 通知 merge）
（若 Vera 有 Critical → Petra 打回 → Cody fix → Vera 重審 → Petra 再審）
```

核心組件：
- `WorkflowEngine`：純靜態流程表，無 LLM，毫秒級路由
- `TaskGroupService`：群組管理 + Petra 審核閘門 + 遞迴 Orchestration
- `PmAgentService`：Petra 品質審核（ReviewRosa/Demi/DevPlan/Vera，Claude Code 唯讀 + LLM fallback）
- `TaskGroup` entity：串聯整批任務（IssueUrls、UiSpecContent、DevPrUrl、LastReviewBody、FixIteration、**DevPlan、DevPlanRevision**）
- `ClaudeCodeService`：封裝 Claude Code CLI subprocess（RunAsync / ReadOnly / Review / QA / Victoria 五種模式）

---

## Victoria CEO 升級（Stage 15）

Stage 15 讓 Victoria 從「有路的分配器」升級為**有腦的技術顧問**：

| 能力 | 說明 |
|------|------|
| **codebase 探索** | 透過 Claude Code 自主 Glob/Grep/Read 整個 repo，回答技術問題時引用實際檔案與行數 |
| **文件讀寫 + git push** | 可讀寫 `docs/` 目錄，更新後自動 `git commit && git push origin main` |
| **Session 對話歷史** | 對話紀錄存 PostgreSQL（`ceo_conversations`），30 分鐘 timeout 自動開新 session |
| **長期記憶** | 跨 session 記憶存 PostgreSQL（`ceo_memories`），session 開始時全量載入（上限 100 筆）|
| **降級機制** | CloneOrPull 失敗或 Claude Code 解析失敗時，自動降級為直接 LLM 呼叫，Discord 顯示降級原因 |

**CLAUDE_Victoria.md 權限邊界**（靠提示詞約束，非系統層隔離）：
- ✅ 允許：`src/` 唯讀（Glob/Grep/Read）、`docs/` 讀寫（Edit/Write）、`git add docs/ && git commit && git push`
- ❌ 禁止：修改 `src/` 程式碼、`dotnet build`、commit 非 docs/ 變更

---

## CEO 智慧分類（Stage 14）

Victoria（CEO）在每次回應前會主動查詢 GitHub 開啟 PR / Issues，對老闆的輸入進行六類分類：

| 分類 | CEO 行為 |
|------|---------|
| **新功能** | 進入提案模式：Rosa + Demi 產出需求 + UI 規格，彙整為提案書 Embed（✅❌ 按鈕）|
| **Bug** | `delegate` → Dev，走 Dev→Reviewer→QA 閉環 |
| **技術改善** | `delegate` → Dev，走 Dev→Reviewer→QA 閉環（不需要 Rosa/Demi）|
| **操作指派** | `delegate` → Release（發版）/ Ops（部署）/ Doc（文件），單次任務直接執行 |
| **取消任務** | 列出進行中任務，確認後取消並 kill subprocess |
| **正常行為／疑問** | 直接回覆說明或回答，不派任何任務 |

---

## Dashboard 可觀測性（Stage 18）

Stage 18 讓 Dashboard 從「看任務清單」升級為「看流水線在動」。

### Agent 狀態卡即時更新

首頁 Agent 卡片即時反映各 Agent 的執行狀態，無需手動刷新：

| 狀態 | 顏色 | 觸發時機 |
|------|------|---------|
| 執行中 | 藍色 | `FireOneStepAsync` 呼叫 executor 前 |
| 閒置 | 灰色 | Agent 執行完成後 |
| 錯誤 | 紅色 | Agent 執行失敗 / Exception |

### Pipeline View

任務中心新增「流程追蹤」Tab，點擊任意 TaskGroup 展開右側 Pipeline 抽屜：

```
CEO ✅ → Rosa ✅ → Petra ✅ → Demi ✅ → Petra ✅ → Dev ✅ → Petra ✅ → Dev ✅ → Reviewer ✅ → Petra ✅ → QA ✅ → Doc ✅
```

- **垂直 MudStepper**（NonLinear）：每步驟顯示 Agent 名稱、狀態色、耗時、[MOCK] 標記
- **MudTimeline 懶載入**：點擊「載入 Log」才讀取該步驟的 TaskLog，不拖慢初始渲染
- **SignalR 即時更新**：流程進行中，Stepper 節點即時變色（執行中→完成），無需手動刷新
- **群組列表同步**：流程完成後 1.5 秒（等 Bot 寫入 DB），列表狀態自動更新為「完成」

---

## Dashboard MudBlazor Layout 架構（Stage 20）

Stage 20 將 Dashboard 的 Layout 基礎設施全面換成 MudBlazor，建立穩定的元件體系。

### 最終架構

```
App.razor（Static SSR HTML shell）
  └── <Routes />（@rendermode InteractiveServer，全局 circuit root）
        └── <CascadingAuthenticationState>
              └── <Router>
                    └── MainLayout（在 Routes 的 circuit 內，不需自己的 @rendermode）
                          ├── <MudProviders />（與 Layout/Pages 共享 circuit）
                          ├── <MudDrawer Persistent>（sidebar，JS toggle）
                          └── <MudMainContent>
                                └── @Body（頁面元件，同一 circuit）
```

### 核心設計決策

| 決策 | 原因 |
|------|------|
| **Layout 不加 `@rendermode`** | Layout 接收 `@Body`（RenderFragment 委派），加了 InteractiveServer 會 HTTP 500（無法序列化） |
| **`Routes.razor` 全域 InteractiveServer** | 讓 Layout、頁面、MudProviders 共享同一 Circuit，解決 scoped DI（`IPopoverService` 等）隔離問題 |
| **onclick 放 HTML 元素而非 Blazor 元件** | Razor 將 Blazor 元件上的 `onclick` 解析為 C# 表達式（build error CS0103）；HTML 元素上的 `onclick` 才是字串屬性 |
| **Dark Mode 用 CSS 變數 + JS** | Layout 無法 Interactive，MudThemeProvider C# binding 無法使用；改用 `html[data-theme="dark"] { --mud-palette-* }` CSS 覆寫 + `localStorage` |
| **Sidebar toggle 操作兩組 CSS class** | MudBlazor Persistent Drawer 需同時切換 `mud-drawer--open`/`mud-drawer--closed`（雙 hyphen）及 `.mud-layout` 的 `mud-drawer-open-persistent-left` |

---

## Token 監控

Dashboard `/tokens` 頁面即時顯示各 Agent 的 Token 用量與 API 費用估算，每次 LLM 呼叫完成後透過 SignalR 自動更新，無需手動重整。

費率可在 Dashboard → Agent 設定 → 系統設定 調整（`TokenPricing:InputPer1kUsd` / `OutputPer1kUsd`）。

---

## 雙層確認機制

```
你對 CEO 說自然語言（在 #victoria-ceo）
    ↓
CEO Agent 解讀意圖 → 回報決策（Embed + ✅❌ 按鈕）  ← 可用 SkipCeoConfirm 略過
    ↓ 你核准
執行 Agent 說明即將操作 → 再次確認（Embed + ✅❌ 按鈕）
    ↓ 你核准
實際執行 → 結果回報 Discord + PostgreSQL
```

> 亦可在各 Agent 專屬頻道直接說話，繞過 CEO 直接指派，CEO 頻道會收到 CC 通知。
>
> **SkipCeoConfirm**：可在 Dashboard → Agent 設定 → 系統設定 開啟，跳過第一層 CEO 確認，直接進入 Agent 執行確認。5 分鐘內自動生效，不需重啟 Bot。

---

## 動態 Agent 框架

新增 Agent 只需四步，**不需修改 CEO 或 Bot 框架任何程式碼**：

1. DB 新增 `AgentConfig` 記錄（`IsActive = false` 預設停用）
2. 實作 `XxxAgentService : IAgentExecutor`
3. `Program.cs` 加 `AddKeyedScoped<IAgentExecutor, XxxAgentService>(AgentNames.Xxx)`
4. Dashboard 切換 `IsActive = true` → CEO 下次呼叫時自動感知

---

## 部署架構（Production）

```
git push origin main
    ↓
GitHub Actions（ubuntu-latest）
  1. dotnet build + test
  2. Docker build → push to ghcr.io
    ↓
Self-hosted Runner（Windows 11 本機）
  3. docker compose pull
  4. docker compose up -d --force-recreate
```

- **Bot Image**：`ghcr.io/darkleong/aiteam-bot:latest`
- **Dashboard**：`http://localhost:5051`（區網可用 `192.168.x.x:5051`）
- **Secrets**：`C:\Users\darkl\aiteam\.env`（不進版控）

---

## Discord 斜線指令

| 指令 | 說明 |
|------|------|
| 自然語言 | 直接在 `#victoria-ceo` 說話，不需格式 |
| `/reload-rules` | 強制重新載入 DB 規則（清除記憶體快取） |
| `/status` | 查詢各 Agent 目前狀態與啟用清單 |
| `/new-session` | 清除目前對話 session（長期記憶不受影響，Victoria 下次回應以全新上下文開始）|
| `/mock` | 【Mock Mode 限定】直接觸發指定工作流程（新功能 / 含提案 / Bug 修復 / 技術改善），不呼叫 LLM，供流程測試用 |

---

## 快速開始

### 環境需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### 設定 User Secrets

**Bot：**

```bash
cd src/AiTeam.Bot

dotnet user-secrets set "Discord:BotToken"               "你的 Discord Bot Token"
dotnet user-secrets set "Discord:GuildId"                "你的 Discord Server ID"
dotnet user-secrets set "Anthropic:ApiKey"               "你的 Anthropic API Key"
dotnet user-secrets set "GitHub:PersonalAccessToken"     "你的 GitHub PAT"
dotnet user-secrets set "GitHub:Owner"                   "你的 GitHub 帳號"
dotnet user-secrets set "GitHub:DefaultRepo"             "預設 Repo 名稱"
dotnet user-secrets set "AgentSettings:InternalApiKey"   "Dashboard 呼叫 Bot 用的 API Key"
```

**Dashboard：**

```bash
cd src/AiTeam.Dashboard

dotnet user-secrets set "BotSettings:InternalApiKey"     "Dashboard 呼叫 Bot 用的 API Key"
dotnet user-secrets set "BotSettings:BaseUrl"            "http://localhost:5050"
```

> Production 環境的 Secret 統一存放於 `C:\Users\darkl\aiteam\.env`，不進版控。

### 啟動（開發模式）

```bash
dotnet run --project src/AiTeam.AppHost
```

Aspire Dashboard 自動開啟，PostgreSQL、Bot、Blazor Dashboard 一併啟動。

### 啟動（Production）

```bash
cd ~/aiteam
docker compose --env-file .env up -d
```

---

## 開發進度

| Stage | 說明 | 狀態 |
|-------|------|------|
| Stage 1 | 設計與決策 | ✅ 完成 |
| Stage 2 | 基礎建設（Discord Bot、CEO Agent、Notion、PostgreSQL） | ✅ 完成 |
| Stage 3 | Dev Agent、Ops Agent、GitHub Webhook | ✅ 完成 |
| Stage 4 | Blazor Server Dashboard、SignalR 即時推送 | ✅ 完成 |
| Stage 5 | 動態 Agent 框架 + QA / Doc / Requirements Agent | ✅ 完成 |
| Stage 6 | Discord Vision、MudBlazor、Requirements 三層確認、E2E 驗收等 12 項強化 | ✅ 完成 |
| Stage 7 | Reviewer / Release / Designer Agent、CI/CD、自然語言對話、Agent 專屬頻道 | ✅ 完成 |
| Stage 8 | 系統可靠性補完、Notion 遷移、動態設定、規則管理、部署紀錄自動化 | ✅ 完成 |
| Stage 9 | Token 監控 Dashboard（即時 SignalR）、CEO 智慧分類 + 提案模式、QA Playwright CI | ✅ 完成 |
| Stage 10 | CEO Orchestrator 全自動流程、提案書 ✏️ 調整按鈕、Dev repo 結構上下文、Review 閉環、Ops Rollback | ✅ 完成 |
| Stage 11 | Dev Agent（Cody）升級為 Claude Code CLI 驅動：自主探索、寫碼、build 驗證、自動修錯 | ✅ 完成 |
| Stage 12 | 提案流程全面升級：Rosa/Demi/Vera/Sage 唯讀探索 codebase、附圖支援、UI 規格存 DB、Discord 附件 | ✅ 完成 |
| Stage 13 | 系統穩定性與流程修正：串行流程（Dev→Reviewer→QA→Doc）、單一 PR（含 Closes #XX）、技術債清償、Dashboard 可觀測性 | ✅ 完成 |
| Stage 14 | CEO 分類補強：技術改善分類、Release/Ops/Doc 直接路由、任務取消能力；Bug fix Orchestrator 修正 | ✅ 完成 |
| Stage 15 | Victoria 升級為技術顧問：Claude Code 探索 codebase + 讀寫 docs/ + Session 對話歷史 + 長期記憶 | ✅ 完成 |
| Stage 16 | PM Agent（Petra）品質審核閘門：Rosa/Demi/Dev_plan/Vera 四個審核點；Vera/QA 重構為 Claude Code session | ✅ 完成 |
| Stage 17 | Mock Mode：IClaudeCodeService 介面 + ClaudeCodeProxy 代理模式 + /mock 指令（4 種流程）；Dashboard 開關 5 分鐘生效 | ✅ 完成 |
| Stage 18 | Dashboard 可觀測性升級：Agent 狀態卡即時更新 + Pipeline View（垂直 MudStepper + MudTimeline 懶載入 + SignalR 即時推進） | ✅ 完成 |
| Stage 19 | Dashboard UI 全面打磨：StatusBadge 統一元件、PipelineList 獨立頁、MudTable 規格統一、MudSwitch 替換（Pt.1 完成；Pt.2 規劃中） | 🔄 進行中 |
| Stage 20 | Dashboard 全面換 MudBlazor Layout：MudLayout + Routes.razor 全域 Circuit + CSS 變數 Dark Mode + MudDrawer Temporary（三處 slide-panel） | ✅ 完成 |

---

## 編程規範

詳見 `docs/conventions/` 資料夾：

- `csharp.md` — C# 命名、結構、非同步規範
- `blazor.md` — Blazor 組件規範
- `ef-core.md` — EF Core 查詢優化、Repository 模式
- `api-design.md` — RESTful API 設計規範
