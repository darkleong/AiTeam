# AiTeam 專案指引

## 專案背景

Christ 老闆 / AI 團隊執行軟體開發：

- **v5.5 production active 6 Talent**：Victoria CEO / Petra PM / Cody Dev / Vera Code Reviewer / Quinn QA / Sage 收尾歸檔員（完整描述見 [docs/agents/v5.5_team_plan.md](docs/agents/v5.5_team_plan.md)）
- **核心工具**：Discord（Discord.Net）+ PostgreSQL + Blazor Dashboard（MudBlazor 8.x / InteractiveServer）
- **LLM 配置**：Petra 走 `LlmProviderFactory.Create("PM")` → Anthropic Sonnet 4.6 production active default（DB SoT `agent_configs.Provider/Model` Dashboard 動態調）/ Cody/Vera/Quinn/Sage 走 Claude Code CLI subprocess / Victoria flag forward only
- **部署**：Windows 11 本機 Docker Compose（非雲端）

---

## 規劃文件

實作前讀 `docs/planning/Stage_{N}_Roadmap.md`：

```
docs/
  README.md          ← 各子資料夾說明
  architecture/      ← 系統流程全景（03_Workflow_Overview / About_Boss）
  planning/          ← Stage Roadmap + Future_Feature
  conventions/       ← 編程規範
  agents/            ← v5.5_team_plan.md（6 Talent SoT）
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
      ├── AiTeam.Dashboard        ← Blazor Web App（MudBlazor 8.x / InteractiveServer）
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
| Token limit（全域 / per-agent）| DB（`app_settings` + `agent_configs`）| appsettings.json |
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

## 自主執行原則

**Christ 只動嘴 / 能自己做的事不要叫 Christ 做。**

實作完畢進驗收前，以下自行完成（不需請 Christ）：

- `dotnet build AiTeam.slnx`（repo root 執行）
- `dotnet test`
- EF Core Migration（有新 Migration 時）：
  ```
  dotnet ef migrations add {Name} --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
  dotnet ef database update --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
  ```
  **注意**：`startup-project` 必須 `src/AiTeam.Dashboard`（含 EntityFrameworkCore.Design）/ 用 AppHost 找不到 DLL / 多 DbContext 必加 `--context AppDbContext`
- git commit + push 到 main（直接執行 / 不詢問「要不要 push」）
- 程式碼靜態分析（無明顯 warning）
- **Playwright 驗收**：可截圖驗證的 UI 變更自行執行 / 不請 Christ 開瀏覽器

**需要請 Christ 操作**：
- 重啟 Docker 容器（`docker compose restart`）
- Discord 執行 `/reload-rules`（規則快取更新）
- Discord 實際測試 Bot 對話流程
- Dashboard 視覺驗收 UI 功能

---

## 實作完成後的結案 SOP

1. 本機驗證：`dotnet build` / `dotnet test` / Playwright 截圖（如適用）
2. 自行 commit（聚焦「為什麼」/ 從近期 commit 觀察風格）
3. 自行 push 到 main（不詢問 / **直接 push**）
4. CI/CD 自動接手：push → GitHub Actions self-hosted runner → `docker compose build + up` → Christ 本機 Win11 Docker
5. 回報「實作完成 + 已 push」+ commit hash / 等 Christ 驗收

> 除非該 commit 涉及破壞性操作（force push / reset --hard 等）或 Christ 明確指示 review，否則一氣呵成完成。

---

## 開發語言

Christ 用繁體中文溝通 / 程式碼註解繁體中文 / 變數與方法名英文。

---

## Session 起手規則

進入「實作 / 修 Bug / 驗收」類 session 時，第一件事：

1. 讀 `docs/planning/Stage_{current}_Roadmap.md`（若 Stage 工作）
2. `git log --oneline -10` 了解最近進度
3. 掃 `docs/planning/Future_Feature.md` 前段（🔴🟡 狀態 bug 區塊）

純諮詢 / 設計討論類 session 不需要。

---

## 回答 Christ 觀察到的異常時

Christ 回報「這合理嗎 / 為什麼 X / 我看到 Y」時，**先查程式碼實證 / 不靠推論解釋**。

- 讀相關檔案 / 確認實際流程
- 比對 Roadmap / 計劃書預期行為
- 確認後再下判斷

不要用「這是既有設計」「不影響正確性」這類結論打發 — 除非已有程式碼實證支撐。Christ 觀察通常基於真實使用感受 / 即使初判「不是 bug」也要**先記錄到 Future_Feature.md 再下結論** / 別直接 dismiss。
