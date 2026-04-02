# Software Team Agent 規劃

> 建立日期：2026-03-31  
> 最後更新：2026-04-02
> 說明：定義 Software Team 所有 Agent 的角色、職責與上線時程規劃。

---

## 完整 Agent 清單

### 已實作（Stage 2 ~ Stage 8 完成）

| Agent | 人物名 | 職責 | 完成 Stage |
|-------|--------|------|-----------|
| **Aria** | Aria | 策略討論、設計決策、計劃審查、文件維護（Claude.ai 扮演）| 概念運作中 |
| **CEO** | Victoria | 接收指令、分析任務、分派 Agent、追蹤執行 | Stage 2 |
| **Dev** | Cody | 寫程式、解 Bug、重構、操作 repo | Stage 2 |
| **Ops** | Maya | CI/CD 監控、部署、健康檢查告警 | Stage 2 |
| **QA** | Quinn | 自動化測試產出（xUnit + NSubstitute + FluentAssertions）| Stage 5 |
| **Doc** | Sage | 技術文件、API 說明自動產出 | Stage 5 |
| **Requirements** | Rosa | 需求拆解、轉換成 GitHub Issues（三層確認機制）| Stage 5 |
| **Reviewer** | Vera | Code Review，分級審查意見（🔴/🟡/🟢），在 GitHub PR 留評論 | Stage 7 |
| **Release** | Rena | 版本管理、整理 Changelog、建立 GitHub Release tag | Stage 7 |
| **Designer** | Demi | 功能需求 → MudBlazor UI 規格文件 | Stage 7 |

---

### 未來候選名單（視需求評估）

| Agent | 名稱 | 職責 | 備註 |
|-------|------|------|------|
| **Security** | 資安檢查 | 掃描漏洞、敏感資訊洩露、依賴套件風險 | 有對外系統時優先考慮 |
| **Performance** | 效能分析 | 找出效能瓶頸、記憶體洩漏、慢查詢 | 等系統有實際使用者再加 |
| **Reporter** | 進度報告 | 定期產出開發進度報告 | 若每日 Discord 摘要不夠才需要 |
| **Architecture** | 架構顧問 | 評估架構決策、審查設計方案 | 目前由 Aria 暫代，待規模擴大再獨立 |
| **PM** | 專案管理 | 管理任務優先級、追蹤進度、協調資源 | 等多專案並行時再考慮 |

---

## Ops 和 Release 的分工

兩者是接力關係，不重疊：

```
Release Agent
  → 決定版本號（v1.0.0 → v1.1.0）
  → 整理 Changelog
  → 產出 Release Notes
  → 在 GitHub 建立 Release tag
      ↓
Ops Agent
  → 執行部署到伺服器
  → 監控服務狀態
  → 出問題時回滾
```

---

## Designer Agent 工作流程

```
你 → CEO：「我需要 Token 監控功能，圖表化呈現每個 Agent 的狀況」
    ↓
CEO → Designer：「幫我規劃這個功能的畫面」
    ↓
Designer 產出 UI 規格文件：
  - 頁面結構（有哪些區塊）
  - 元件規格（用什麼圖表、顯示什麼資料）
  - 資料來源（從哪裡取得）
  - 互動行為（篩選、切換等）
    ↓
CEO → Dev：「依照這份規格實作」
    ↓
Dev 開始寫程式
```

**Designer 能做到的：**
- 功能需求轉換成具體的畫面規格
- 頁面結構與元件配置建議
- 熟悉 MudBlazor 元件，直接指定用哪個元件
- 低保真線框稿（文字描述或 HTML 原型）

**Designer 做不到的：**
- 視覺設計稿（顏色、字體、精緻排版）
- 使用者研究與訪談
- 品牌設計

---

## 不拆分前後端 Dev 的原因

技術棧是 Blazor，前後端都是 C#，同一套規範，一個 Dev Agent 就能全端處理。

若未來有獨立的行動 App（React Native / MAUI），再考慮新增 App Dev Agent。

---

## UI 元件庫

Stage 6 已全面由 Telerik 替換為 **MudBlazor 8.15.0（MIT 授權）**。所有 Agent 文件中的元件規格一律以 MudBlazor 為準。

---

## 變更紀錄

| 日期 | 內容 |
|------|------|
| 2026-03-31 | 初版建立 |
| 2026-04-01 | Stage 5 完成：QA / Doc / Requirements 狀態更新為已完成 |
| 2026-04-01 | Stage 6 完成：UI 元件庫全面替換為 MudBlazor；Reviewer / Release / Designer 移入第一階段候選；Aria 架構評估職責補充 |
| 2026-04-02 | Stage 7 完成：Reviewer（Vera）/ Release（Rena）/ Designer（Demi）正式上線 |
| 2026-04-02 | Stage 8 完成：整合 Agent 清單為單一表格，補上所有人物名 |
