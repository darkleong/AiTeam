✅ 完成！我已產出完整的 **UI/UX 規格文件** —— 

## 📄 規格文件位置

`docs/design/UISpec_AgentProviderModelDisplay.md`

## 📋 文件涵蓋範圍

1. **功能概述** — LLM 配置顯示的目標與意義
2. **頁面結構** — 佈局圖示與詳情表單的分區設計
3. **元件清單** — 
   - DTO 新增欄位（`Provider`、`Model`）
   - Service 層讀取邏輯（從 appsettings）
   - UI 元件（MudDivider 分隔、MudText 唯讀顯示、MudStack 排版）
4. **互動說明** — 頁面加載流程、選擇 Agent 行為
5. **MudBlazor 元件選用** — 符合專案規範的選擇與禁止實踐
6. **驗收條件** — 代碼、UI、功能、Playwright、瀏覽器相容性
7. **技術考量** — appsettings 讀取策略、預設值、null 安全
8. **後續擴展點** — FF 四第二階段編輯功能的銜接

## 🎨 核心設計決策

| 項目 | 決策 |
|------|------|
| **UI 位置** | 信任等級下方 + MudDivider 分隔 |
| **排版** | MudStack 水平排列（標籤左、數值右） |
| **唯讀顯示** | MudText + Color.Secondary（灰色） |
| **Provider/Model 來源** | appsettings（短期唯讀）→ DB（長期編輯） |
| **預設值** | `"Anthropic"` / `"claude-sonnet-4-6"` |

**規格已準備妥當，後端（Rosa）和前端（Cody）可直接根據文件實作**。
