# AiTeam 專案指引

## 專案背景

AI 團隊執行軟體開發（老闆 + AI Talent 協作）：

- **v5.5 production active 6 Talent**：Victoria CEO / Petra PM / Cody Dev / Vera Code Reviewer / Quinn QA / Sage 收尾歸檔員（完整描述見 [docs/Architecture.md](docs/Architecture.md)）
- **核心工具**：Discord（Discord.Net）+ PostgreSQL + Blazor Dashboard（MudBlazor 8.x / InteractiveServer）
- **LLM 配置**：Petra 走 `LlmProviderFactory.Create("Petra")` → Anthropic Sonnet 4.6 production active default（DB SoT `talents.Provider/Model` Dashboard 動態調 / Stage 87 從 `agent_configs` 遷入）/ Cody/Vera/Quinn/Sage 走 Claude Code CLI subprocess / Victoria flag forward only
- **部署**：Windows 11 本機 Docker Compose（非雲端）

---

## 規劃文件

實作前讀 `docs/planning/Stage_{N}_Roadmap.md`：

```
docs/
  README.md          ← 各子資料夾說明
  Architecture.md    ← v5.5 系統架構全景（三層分工 / HITL / Petra 動態 SubtaskPlan / Worker CLI / 程式碼位置索引）
  planning/          ← Stage Roadmap + Future_Feature
  conventions/       ← 編程規範
```

---

## 編程規範

實作前讀 `docs/conventions/`：

| 檔 | 用途 |
|---|---|
| `csharp.md` | C# 命名 / 結構 / 非同步 / Primary Constructor / ILogger |
| `blazor.md` | Blazor 組件 / @rendermode / SignalR 即時更新 |
| `mudblazor.md` | MudBlazor 8.x（必讀）|
| `ef-core.md` | EF Core 查詢 / PostgreSQL 例外 / Migration 流程 |
| `api-design.md` | RESTful API / Internal API / SignalR Hub |
| `refactor-sop.md` | 服務層大檔案拆解守則 |

UI 元件庫 = **MudBlazor 8.x**。

---

## 專案結構

```
AiTeam.slnx         ← 解決方案檔位於 repo root（注意 .slnx 不是 .sln）
  └── src/
      ├── AiTeam.AppHost          ← Aspire 入口
      ├── AiTeam.ServiceDefaults  ← 共用遙測 / 健康檢查
      ├── AiTeam.Bot              ← Discord Bot 主程式 + Agent 邏輯
      ├── AiTeam.Dashboard        ← Blazor Web App(MudBlazor 8.x / InteractiveServer）
      ├── AiTeam.Data             ← EF Core DbContext / Entities / Repositories / Migrations
      ├── AiTeam.Shared           ← 共用 DTO / 介面 / 常數
      └── AiTeam.Tests.Playwright ← Playwright E2E 截圖測試
```

建置：`dotnet build AiTeam.slnx`（從 repo root 執行）

---

## 部署環境

Windows 11 本機 Docker Compose（非雲端）：
- Bot / Dashboard / PostgreSQL 均本機容器
- Bot 容器內無法執行 host `docker` / `docker compose` 指令
- 涉及容器操作走 GitHub Actions self-hosted runner

**自動部署**：push to main → GitHub Actions self-hosted runner 自動 `docker compose build + up`。驗收步驟不該含「手動部署 / 重啟容器」/ 只需 commit & push 等自動部署完成。

---

## ops 配置改動 SoP

修改 Token / 系統設定 / docker-compose 配置依下表：

| 改動類型 | 路徑 | 生效時間 |
|---|---|---|
| Token limit / 系統設定（全域月限 / per-agent 日月限）| Dashboard 設定頁 → reload-cache 自動觸發 | 5 分鐘內（不重啟）|
| docker-compose.prod.yml / appsettings.json 預設值 | commit + push → CI/CD `docker compose up -d --force-recreate` | push 後 ~5 分鐘 |

⚠️ **不要單獨 `docker restart aiteam-bot`** — restart 不 reload env / 必須 recreate。

### env naming convention

- **Bot 自己** service env：`AgentSettings__*` prefix（如 `AgentSettings__InternalApiKey`）
- **Dashboard 視 Bot 為外部 service** env：`Bot__*` prefix（如 `Bot__InternalApiKey` / `Bot__InternalUrl`）

動 docker-compose env 前必 grep 既有 naming convention verify（避免 Dashboard 端 IOptions 讀 null 等 silent fail）。

### 配置 SoT

| 設定 | SoT | fallback |
|---|---|---|
| Token limit（全域 / per-agent）| DB（`app_settings` + `talents`）| appsettings.json |
| 其他 AgentSettings（RulesCacheTtlMinutes 等）| docker-compose.prod.yml env | appsettings.json |
| Discord / GitHub / DB 連線 | docker-compose.prod.yml env | 無 fallback |

---

## 重要設計原則

- **動態 Agent 清單**：從 DB 載入 / 不寫死
- **Agent 模型可獨立配置**：每個 Agent Provider/Model 經 Dashboard 設定頁管理
- **所有設定**集中在 `appsettings.json` 或動態 `AppSettings` 表 / 不寫死
- **Discord + Dashboard 雙通道**：老闆確認點同時在 Discord 按鈕 + Dashboard 操作中心 / 任一端回覆即鎖（樂觀鎖先到先贏）

---

## 版本號管理（SemVer）

格式 `MAJOR.MINOR.PATCH`：
- **patch**：hotfix / 小 bug 修正
- **minor**：Stage 完成（每 Stage 通常 minor bump）
- **major**：架構層面重大改變

修改：`src/Directory.Build.props` `<Version>` 標籤（Stage 26 起集中管理）。

目前版本 / 最新 Stage 以 [`/CHANGELOG.md`](./CHANGELOG.md) 為準。

---

## EF Core Migration

新 Migration 指令：

```
dotnet ef migrations add {Name} --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
dotnet ef database update --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
```

**AiTeam 特殊紀律**：
- `startup-project` 必須 `src/AiTeam.Dashboard`（含 `Microsoft.EntityFrameworkCore.Design`）/ 用 `AppHost` 找不到 DLL
- 多 DbContext 必加 `--context AppDbContext`
- Bot 容器啟動自動 `MigrateAsync()` AppDbContext（`Bot/Program.cs:184`）/ push 後容器 recreate 自動套用 / 不需手動跑 `dotnet ef database update`

---

## 開發語言

程式碼註解使用繁體中文 / 變數與方法名使用英文。
