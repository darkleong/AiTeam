# AiTeam 專案指引

## 專案背景

這是一個 AI 團隊管理系統。Christ 擔任老闆角色，透過 Discord 下達指令，AI 團隊（CEO / Dev / Ops Agent）負責執行軟體開發與部署任務。

**核心工具：**
- 溝通：Discord（Discord.Net）
- 記憶/規則：PostgreSQL `rules` 資料表（Stage 8 起，Notion 已完全移除）
- 詳細 log：PostgreSQL（EF Core + Npgsql）
- 視覺化：Blazor Server Dashboard
- LLM：Anthropic Claude API（多供應商介面設計）
- 部署：Docker Compose on Windows 11（本機，非雲端）

---

## 規劃文件

實作前請先閱讀 `docs/` 資料夾內對應的 Stage 文件：

```
docs/
  00_Master_Plan.md          ← 總索引（含所有 Stage 狀態）
  01_Vision_and_Architecture.md
  02_Infrastructure.md
  Stage_1_Design.md ~ Stage_10_Roadmap.md  ← 全部已完成
  Future_Feature.md          ← 未來功能候選清單
```

---

## 編程規範

實作前請閱讀 `docs/conventions/` 資料夾內的所有規範文件：

```
docs/conventions/
  csharp.md          ← C# 命名、結構、非同步規範
  blazor.md          ← Blazor 組件規範、生命週期、通信
  ef-core.md         ← EF Core 查詢優化、Repository 模式
  api-design.md      ← RESTful API 設計規範
```

> 注意：UI 元件庫為 **MudBlazor 8.x**（Stage 6 起從 Telerik 全面替換）。

---

## 專案結構

```
AiTeam.sln
  ├── AiTeam.AppHost              ← Aspire 入口（PostgreSQL + Bot + Dashboard 編排）
  ├── AiTeam.ServiceDefaults      ← 共用遙測、健康檢查設定
  ├── AiTeam.Bot                  ← Discord Bot 主程式（含各 Agent 邏輯）
  ├── AiTeam.Dashboard            ← Blazor Server Dashboard（MudBlazor）
  ├── AiTeam.Data                 ← EF Core DbContext、Entities、Repositories、Migrations
  ├── AiTeam.Shared               ← 共用 DTO、介面、常數
  └── AiTeam.Tests.Playwright     ← Playwright E2E 截圖測試
```

---

## 部署環境

系統運行在**本機 Windows 11 的 Docker Compose** 上，非雲端部署。
- Bot / Dashboard / PostgreSQL 均為本機容器
- Bot 容器內無法執行宿主機的 `docker` / `docker compose` 指令
- 涉及容器操作的功能需透過 GitHub Actions self-hosted runner 間接執行
- docker-compose 設定檔：`docker-compose.yml`（開發）、`docker-compose.prod.yml`（正式）

---

## 重要設計原則

- **雙層確認機制**：CEO 決策問你確認，Agent 執行前也問你確認
- **動態 Agent 清單**：從資料庫載入，不寫死在程式碼
- **ILlmProvider 介面**：每個 Agent 可獨立設定不同供應商的模型
- **規則 Cache TTL**：1 小時，可 `/reload-rules` 強制更新（Notion 已移除，規則存於 PostgreSQL）
- **所有設定**集中在 `appsettings.json`，不寫死在程式碼

---

## 自主執行原則

**Christ 是只動嘴的老闆，能自己做的事不要叫他做。**

實作完畢進入驗收前，以下事項應自行完成，不需要請 Christ 操作：

- `dotnet build` — 確認編譯無誤
- `dotnet test` — 執行所有單元測試
- EF Core Migration — 有新 Migration 時執行 `dotnet ef database update`
- git commit / push / 開 PR — 實作完成後自行提交
- 程式碼靜態分析 — 確認無明顯 warning

**需要請 Christ 操作的事（Bot / Dashboard 執行中的容器操作）：**
- 重啟 Docker 容器（`docker compose restart`）
- 在 Discord 執行 `/reload-rules`（規則快取更新）
- 在 Discord 實際測試 Bot 對話流程
- 在 Dashboard 驗收 UI 功能

---

## 開發語言

Christ 使用繁體中文溝通，程式碼註解使用繁體中文，變數與方法名稱使用英文。
