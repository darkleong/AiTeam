# AiTeam 專案指引

## 專案背景

這是一個 AI 團隊管理系統。Christ 擔任老闆角色，透過 Discord 下達指令，AI 團隊（Victoria CEO / Cody Dev / Petra PM / Rosa / Demi / Vera / Quinn / Sage / Rena / Maya 等）負責執行軟體開發與部署任務。

**核心工具：**
- 溝通：Discord（Discord.Net）
- 記憶/規則：PostgreSQL `rules` 資料表
- 詳細 log：PostgreSQL（EF Core + Npgsql）
- 視覺化：Blazor Web App Dashboard（MudBlazor 8.x，InteractiveServer）
- LLM：Claude Code CLI（Victoria / Cody / Vera / Quinn / Petra 走 session-based CLI）+ Anthropic API（Rosa / Demi / Sage / Release / Ops 走直接 API call）
- 部署：Docker Compose on Windows 11（本機，非雲端）

---

## 規劃文件

實作前請先閱讀 `docs/` 資料夾內對應的 Stage 文件：

```
docs/
  README.md                  ← 資料夾導覽（各子資料夾說明）
  architecture/
    00_Master_Plan.md          ← 總索引（所有 Stage 狀態與版本歷史、目前版本以此為準）
    01_Vision_and_Architecture.md
    02_Infrastructure.md
    About_Boss.md
  planning/
    Stage_{N}_Roadmap.md       ← 各 Stage 規劃書，完整清單見 Master Plan 索引
    Future_Feature.md          ← 未來功能候選清單 + 待修 Bug 記錄
  conventions/               ← 編程規範（見下方）
  agents/                    ← Agent 角色說明文件
```

---

## 編程規範

實作前請閱讀 `docs/conventions/` 資料夾內的所有規範文件：

```
docs/conventions/
  csharp.md          ← C# 命名、結構、非同步、Primary Constructor、ILogger
  blazor.md          ← Blazor 組件規範、@rendermode、SignalR 即時更新
  mudblazor.md       ← MudBlazor 8.x 使用規範、常見陷阱（必讀）
  ef-core.md         ← EF Core 查詢優化、PostgreSQL 例外處理、Migration 流程
  api-design.md      ← RESTful API、Internal API、SignalR Hub 設計規範
  refactor-sop.md    ← 服務層大檔案拆解守則（Stage 34-36 FF 二十實踐累積）
```

> UI 元件庫為 **MudBlazor 8.x**。

---

## 專案結構

```
AiTeam.slnx   ← 解決方案檔位於 repo root（注意是 .slnx 不是 .sln）
  └── src/
      ├── AiTeam.AppHost              ← Aspire 入口（PostgreSQL + Bot + Dashboard 編排）
      ├── AiTeam.ServiceDefaults      ← 共用遙測、健康檢查設定
      ├── AiTeam.Bot                  ← Discord Bot 主程式（含各 Agent 邏輯）
      ├── AiTeam.Dashboard            ← Blazor Web App Dashboard（MudBlazor 8.x，InteractiveServer）
      ├── AiTeam.Data                 ← EF Core DbContext、Entities、Repositories、Migrations
      ├── AiTeam.Shared               ← 共用 DTO、介面、常數
      └── AiTeam.Tests.Playwright     ← Playwright E2E 截圖測試
```

> 建置指令：`dotnet build AiTeam.slnx`（從 repo root 執行）

---

## 部署環境

系統運行在**本機 Windows 11 的 Docker Compose** 上，非雲端部署。
- Bot / Dashboard / PostgreSQL 均為本機容器
- Bot 容器內無法執行宿主機的 `docker` / `docker compose` 指令
- 涉及容器操作的功能需透過 GitHub Actions self-hosted runner 間接執行
- docker-compose 設定檔：`docker-compose.yml`（開發）、`docker-compose.prod.yml`（正式）

**自動部署：push to main 後，GitHub Actions self-hosted runner 會自動執行 `docker compose build + up`，不需要手動操作。** 因此驗收步驟中不應包含「手動部署到 Docker」或「手動重啟容器」等指示——只需 commit & push，等待自動部署完成即可。

---

## 重要設計原則

- **動態 Agent 清單**：從資料庫載入，不寫死在程式碼
- **Agent 模型可獨立配置**：每個 Agent 的 Provider / Model 經 Dashboard 設定頁管理
- **規則 Cache TTL**：1 小時，可 Discord `/reload-rules` 強制更新
- **所有設定**集中在 `appsettings.json` 或動態 `AppSettings` 資料表，不寫死在程式碼
- **Discord + Dashboard 雙通道**：老闆確認點同時出現在 Discord 按鈕 + Dashboard 操作中心，任一端回覆即鎖（樂觀鎖先到先贏）

---

## 版本號管理（SemVer）

系統版本遵循 **Semantic Versioning**，格式 `MAJOR.MINOR.PATCH`：

| 類型 | 規則 | 範例 |
|------|------|------|
| **patch** | Hotfix、小 bug 修正，不跟 Stage 走 | v3.4.0 → v3.4.1 |
| **minor** | 每個 Stage 完成時遞增 | v3.4.0 → v3.5.0（Stage 21 完成）|
| **major** | 架構層面重大改變（如 Claude Code 引入、PM 閘門等等級）| v2.x → v3.0.0 |

**需要修改的地方：**
- `src/Directory.Build.props` — `<Version>` 標籤（Stage 26 起集中管理，改版只需改此一個檔案）
- Dashboard 頁腳會自動讀取 assembly version 顯示

> 目前版本 / 最新 Stage 以 `docs/architecture/00_Master_Plan.md` 為準（不在本文件寫死，避免過期）。

---

## 自主執行原則

**Christ 是只動嘴的老闆，能自己做的事不要叫他做。**

實作完畢進入驗收前，以下事項應自行完成，不需要請 Christ 操作：

- `dotnet build AiTeam.slnx` — 確認編譯無誤（從 repo root 執行）
- `dotnet test` — 執行所有單元測試
- EF Core Migration — 有新 Migration 時執行：
  ```
  dotnet ef migrations add {Name} --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
  dotnet ef database update --project src/AiTeam.Data --startup-project src/AiTeam.Dashboard --context AppDbContext
  ```
  **注意**：`startup-project` 必須用 `src/AiTeam.Dashboard`（含 `Microsoft.EntityFrameworkCore.Design`），用 `AiTeam.AppHost` 會找不到 DLL；多 DbContext 必加 `--context AppDbContext`
- **git commit + push 到 main** — 實作完成後**直接執行**，不要詢問 Christ「要不要 push」（push 是預設行為；完整結案鏈路詳見下方「結案 SOP」段）
- 程式碼靜態分析 — 確認無明顯 warning
- **Playwright 驗收** — 凡是可以用 Playwright 截圖驗證的 UI 變更，自行執行並確認結果，不需要請 Christ 開瀏覽器驗收

**需要請 Christ 操作的事（Bot / Dashboard 執行中的容器操作）：**
- 重啟 Docker 容器（`docker compose restart`）
- 在 Discord 執行 `/reload-rules`（規則快取更新）
- 在 Discord 實際測試 Bot 對話流程
- 在 Dashboard 驗收 UI 功能

---

## 實作完成後的結案 SOP

驗收條件達標 + 實作完成時，完整結案鏈路：

1. **本機驗證**：`dotnet build AiTeam.slnx` / `dotnet test` / Playwright 截圖（如適用）
2. **自行 commit**：聚焦「為什麼」而非「做了什麼」；遵循近期 commit 訊息風格（從 `git log --oneline -10` 觀察）
3. **自行 push 到 main**：這是預設行為，**不需要詢問 Christ「要不要 push」**——直接 push
4. **CI/CD 自動接手**：
   - push 觸發 GitHub Actions **self-hosted runner**
   - Runner 自動執行 `docker compose build + up`
   - 系統跑在 Christ **本機 Win11 Docker Compose**（非雲端），runner 直接重建本機容器
   - 你**不需要手動 ssh / 執行 docker 指令**——這些 Christ 自己也做不到（runner 才有權限）
5. **回報「實作完成 + 已 push」**：給 commit hash，等 Christ 驗收 Bot / Dashboard 行為

> 這條鏈路（commit → push → CI/CD → 本機 Docker）是預設工作流，**不要把它拆成「先 commit 再問是否 push」**。除非該 commit 涉及破壞性操作（force push / reset --hard 等）或 Christ 明確指示要 review，否則一氣呵成完成。

---

## 開發語言

Christ 使用繁體中文溝通，程式碼註解使用繁體中文，變數與方法名稱使用英文。

---

## Session 起手規則

進入「實作 / 修 Bug / 驗收」類 session 時，第一件事：

1. 讀 `docs/planning/Stage_{current}_Roadmap.md`（若是 Stage 工作）
2. 跑 `git log --oneline -10` 了解最近進度
3. 掃一眼 `docs/planning/Future_Feature.md` 前段（🔴🟡 狀態的 bug 區塊）

純諮詢 / 設計討論類 session 不需要。

---

## 回答 Christ 觀察到的異常時

Christ 回報「這合理嗎 / 為什麼 X / 我看到 Y」時，**先查程式碼實證，不要靠推論解釋**。

- 讀相關檔案、確認實際流程
- 比對 Roadmap / 計劃書的預期行為
- 確認後再下判斷

不要用「這是既有設計」「不影響正確性」這類結論打發——除非已經有程式碼實證支撐。Christ 的觀察通常基於使用時的真實感受，即使初判「不是 bug」也要**先記錄到 Future_Feature.md 再下結論**，別直接 dismiss。
