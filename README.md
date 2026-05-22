# AiTeam

以 AI 驅動的軟體開發團隊管理系統。Christ 擔任老闆角色，透過 Discord 下達自然語言指令，AI 團隊（v5.5 Talent-Skill separation 架構）負責執行軟體開發任務——從接需求、設計、實作、Code Review、測試、文件、到通知 merge PR，全流程自動閉環。

> **目前版本與最新狀態見 [CHANGELOG.md](./CHANGELOG.md)。**
> 系統演進歷史 + 各 Stage 細節見 [`docs/planning/`](./docs/planning/)。

---

## 系統架構（v5.5 Talent-Skill separation）

```
你（老闆）
    ↓ Discord 自然語言（在 #victoria-ceo 說話）
    或 Dashboard 操作中心（雙通道，先到先贏）
        │
CEO Talent（Victoria）—— flag forward only（寫 PetraInbox + return ack / v5.5 後不直接 call LLM）
    │
PM Orchestrator（Petra）—— 純 LLM API call 動態拆 SubtaskPlan + 看 Skill 找 Talent pool
    │
    ├── Cody    — code_implementation
    ├── Vera    — code_review
    ├── Quinn   — qa_testing
    ├── Sage    — documentation
    │
    └── 結果回報 Discord + Dashboard + 詳細 log 寫入 PostgreSQL
```

**v5.5 概念**：Talent = 人（Victoria/Petra/Cody/Vera/Quinn/Sage 六位）/ Skill = 4 Final Skill code-defined（code_implementation / code_review / qa_testing / documentation）/ 一個 Talent 可兼多 Skill（baseline 1:1 / Stage 83 後 production 4 row）/ Petra dispatch 看 Skill 找 Talent pool，預備 horizontal scaling（多 instance round-robin）。完整 6 Talent baseline + Talent-Skill separation 見 [docs/Architecture.md](./docs/Architecture.md)。

即時狀態透過 **Blazor Web App Dashboard** 可視化（SignalR 推送）。

### 核心工具

| 用途 | 工具 |
|------|------|
| 溝通介面 | Discord（Discord.Net）自然語言對話 + Dashboard 操作中心（雙通道） |
| 規則管理 | PostgreSQL `rules` 資料表（Dashboard CRUD） |
| 詳細 Log | PostgreSQL（EF Core + Npgsql） |
| 視覺化 | Blazor Web App Dashboard（MudBlazor 8.x，InteractiveServer） |
| LLM | Anthropic Claude API + Google Gemini API（Provider/Model 經 Dashboard 動態配置） |
| Petra（PM）| `LlmProviderFactory.Create("PM")` → 純 LLM API call（Anthropic Sonnet 4.6 production active）|
| Cody / Vera / Quinn / Sage（Worker）| Claude Code CLI subprocess（session-based / workspace 持續）|
| 排程 | Quartz.NET |
| 部署 | Docker Compose + GitHub Actions self-hosted runner |

---

## 專案結構

```
AiTeam.slnx                          ← 解決方案（注意是 .slnx）
src/
├── AiTeam.AppHost/                  ← Aspire 入口（PostgreSQL + Bot + Dashboard 編排）
├── AiTeam.ServiceDefaults/          ← Aspire 共用遙測、健康檢查
├── AiTeam.Shared/                   ← 共用 DTO、介面、常數
├── AiTeam.Data/                     ← EF Core DbContext、Entities、Repositories、Migrations
├── AiTeam.Bot/                      ← Discord Bot 主程式（含各 Agent 邏輯）
│   ├── Agents/                      ← AgentService / ClaudeCodeService / LlmProviderFactory / TokenTrackingProvider
│   ├── Orchestration/Petra/         ← Petra v5.5 動態 orchestrator（PetraOrchestratorService / ClaudeCodeChatClientAdapter / PetraInboxProcessor / PetraDispatchWorker / PlanConfirmationProcessor / SubtaskPlan）
│   ├── Resources/                   ← CLAUDE_*.md（Agent fallback prompt / DB skill_prompts 為主 SoT）
│   ├── Discord/                     ← DiscordBotService、CommandHandler、Routers
│   ├── Api/                         ← Internal API（CeoCommand / InternalController）
│   ├── Configuration/               ← AgentSettings / WorkflowSettings / DiscordSettings 等
│   └── Services/                    ← AppSettingsService / PromptResolver / TalentDispatchLockService / TalentSkillModelResolver 等
├── AiTeam.Dashboard/                ← Blazor Web App Dashboard
├── AiTeam.Bot.Tests/                ← xUnit 單元測試
└── AiTeam.Tests.Playwright/         ← Playwright E2E 截圖測試
tests/
└── AiTeam.Tests.Generated/          ← Quinn 自動產出測試
docs/
├── README.md                        ← 資料夾導覽入口
├── Architecture.md                  ← v5.5 系統架構全景（含 6 Talent baseline）
├── planning/                        ← 各 Stage Roadmap + Future_Feature
├── conventions/                     ← 編程規範（C# / Blazor / MudBlazor / EF Core / API / refactor-sop）
├── experiments/                     ← Self-implement 試驗紀錄
└── _archive/                        ← 歷史歸檔
```

> 建置：`dotnet build AiTeam.slnx`（從 repo root）

---

## Discord 頻道結構

```
📁 Software Team
  # victoria-ceo        ← 主要指令中心，自然語言跟 CEO 說話
  # petra-pm / # cody-dev / # vera-reviewer / # quinn-qa / # sage-doc
  ↑ 各 Talent log + 可直接指派任務（CC 給 CEO）

📁 系統
  # 任務動態 / # 警報 / # 每日摘要
```

> Stage 78a 砍 Rosa（Requirements）/ Demi（UI Design）/ Rena（Release）3 個 v4 Agent + Maya（Ops）未實作 / capability 合進其他 Talent 或 Skill 概念吸收。

---

## HITL（Human-in-the-Loop）兜底機制

v5.5 後雙層確認機制升級為 HITL 閘門（業界 LangGraph interrupt pattern）— Petra 拆完 plan 開 `plan_confirm` 卡 / Vera critical 或 Quinn fail 開 `replan_confirm` 卡 / 你 4 button 拍板（approve / edit / reject / respond）。

詳細 flow + 4 decision routing 見 [docs/Architecture.md](./docs/Architecture.md)「HITL — plan_confirm + replan_confirm」段。

---

## 動態 Talent / Skill 框架（v5.5）

4 Final Skill code-defined（code_implementation / code_review / qa_testing / documentation）+ 6 預設 Talent（DB seed）+ Petra LLM 動態拆 SubtaskPlan JSON dispatch + WebUI Settings 分區 TALENTS / SKILLPROMPTS / TALENTPROMPTS full CRUD（Stage 83 完整收口）。

詳細 Skill registry / Talent registry / Dispatch 機制 / horizontal scaling 見 [docs/Architecture.md](./docs/Architecture.md)「Talent + Skill 分離」段。

---

## 部署架構（Production）

```
git push origin main
    ↓
GitHub Actions（ubuntu-latest）
  1. dotnet build + test
  2. Docker build → push to ghcr.io
    ↓
Self-hosted Runner（Christ 本機 Windows 11）
  3. docker compose pull
  4. docker compose up -d --force-recreate
```

- **Bot Image**：`ghcr.io/darkleong/aiteam-bot:latest`
- **Dashboard**：`http://localhost:5051`（區網 / Tailscale Funnel 對外）
- **Secrets**：`C:\Users\darkl\aiteam\.env`（不進版控）

---

## Discord 互動

- 直接在 `#victoria-ceo` 用**自然語言**說話 / 不需格式
- 斜線指令 Stage 78c 全砍（含 `/mock` / `/pause` / `/queue` / `/status` 等）/ 改走 Dashboard 操作中心
- Dashboard URL：`http://localhost:5051`（區網 / Tailscale Funnel 對外）

---

## 快速開始

### 環境需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Node.js 22+（Claude Code CLI 需要）

### 設定 User Secrets

**Bot：**

```bash
cd src/AiTeam.Bot

dotnet user-secrets set "Discord:BotToken"               "你的 Discord Bot Token"
dotnet user-secrets set "Discord:GuildId"                "你的 Discord Server ID"
dotnet user-secrets set "Anthropic:ApiKey"               "你的 Anthropic API Key"
dotnet user-secrets set "Gemini:ApiKey"                  "你的 Google Gemini API Key（可選）"
dotnet user-secrets set "GitHub:PersonalAccessToken"     "你的 GitHub PAT"
dotnet user-secrets set "GitHub:Owner"                   "你的 GitHub 帳號"
dotnet user-secrets set "GitHub:DefaultRepo"             "預設 Repo 名稱"
dotnet user-secrets set "AgentSettings:InternalApiKey"   "Dashboard 呼叫 Bot 用的 API Key"
```

**Dashboard：**

```bash
cd src/AiTeam.Dashboard

dotnet user-secrets set "BotSettings:InternalApiKey"     "Dashboard 呼叫 Bot 用的 API Key"
dotnet user-secrets set "BotSettings:BaseUrl"            "http://localhost:5052"
```

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

## 編程規範

實作前必讀 [`docs/conventions/`](./docs/conventions/) 內所有規範文件：

| 文件 | 內容 |
|---|---|
| `csharp.md` | C# 命名、結構、非同步、Primary Constructor、ILogger |
| `blazor.md` | Blazor 組件規範、@rendermode、SignalR 即時更新 |
| `mudblazor.md` | MudBlazor 8.x 使用規範、常見陷阱（必讀） |
| `ef-core.md` | EF Core 查詢優化、PostgreSQL 例外處理、Migration 流程 |
| `api-design.md` | RESTful API、Internal API、SignalR Hub 設計規範 |
| `refactor-sop.md` | 服務層大檔案拆解守則（Stage 34-36+59 拆解 SOP 累積） |

---

## 文件導覽

| 想看的東西 | 去哪查 |
|---|---|
| 完整版本變更紀錄 | [CHANGELOG.md](./CHANGELOG.md) |
| Active 功能候選 + Stage 84+ 候選 | [Future_Feature.md](./docs/planning/Future_Feature.md) |
| 系統架構全景 | [docs/Architecture.md](./docs/Architecture.md) |
| 各 Stage 詳細實作 | [docs/planning/Stage_*_Roadmap.md](./docs/planning/) |
| 6 Talent baseline + Talent-Skill separation | [docs/Architecture.md](./docs/Architecture.md) |
| Self-implement 試驗紀錄 | [docs/experiments/](./docs/experiments/) |
