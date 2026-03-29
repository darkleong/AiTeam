# AiTeam 專案指引

## 專案背景

這是一個 AI 團隊管理系統。Christ 擔任老闆角色，透過 Discord 下達指令，AI 團隊（CEO / Dev / Ops Agent）負責執行軟體開發與部署任務。

**核心工具：**
- 溝通：Discord（Discord.Net）
- 記憶/規則：Notion（Notion.Net）
- 詳細 log：PostgreSQL（EF Core + Npgsql）
- 視覺化：未來 Blazor Server Dashboard
- LLM：Anthropic Claude API（多供應商介面設計）
- 部署：Docker Compose on Windows 11

---

## 規劃文件

實作前請先閱讀 `docs/` 資料夾內對應的 Stage 文件：

```
docs/
  00_Master_Plan.md          ← 總索引
  01_Vision_and_Architecture.md
  02_Infrastructure.md
  Stage_1_Design.md          ← 已完成
  Stage_2_Foundation.md      ← 目前實作中
  Stage_3_Agents.md
  Stage_4_Dashboard.md
  Stage_5_Expansion.md
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
  telerik.md         ← Telerik 組件使用規範
```

---

## 專案結構

```
AiTeam.sln
  ├── AiTeam.AppHost              ← Aspire 入口
  ├── AiTeam.ServiceDefaults      ← 共用設定
  ├── AiTeam.Bot                  ← Discord Bot 主程式
  ├── AiTeam.Dashboard            ← Blazor Server Dashboard（Stage 4）
  ├── AiTeam.Agents               ← 各 Agent Prompt 與邏輯
  ├── AiTeam.Data                 ← EF Core，Bot 與 Dashboard 共用
  └── AiTeam.Shared               ← 共用 DTO、介面、常數
```

---

## 重要設計原則

- **雙層確認機制**：CEO 決策問你確認，Agent 執行前也問你確認
- **動態 Agent 清單**：從資料庫載入，不寫死在程式碼
- **ILlmProvider 介面**：每個 Agent 可獨立設定不同供應商的模型
- **Notion Cache TTL**：1 小時，可 `/reload-rules` 強制更新
- **所有設定**集中在 `appsettings.json`，不寫死在程式碼

---

## 開發語言

Christ 使用繁體中文溝通，程式碼註解使用繁體中文，變數與方法名稱使用英文。
