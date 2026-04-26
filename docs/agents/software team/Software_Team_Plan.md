# Software Team Agent 規劃

> 本檔於 2026-04-26 整理：精簡為「當前 Agent 清單 + 設計原則」。
> 變更歷史詳見 [`/CHANGELOG.md`](../../../CHANGELOG.md)。
> 早期未實作 Agent 構想（Architecture / Performance / Reporter / Secretary / Security / Grand CEO）已歸檔至 [`docs/_archive/agents-future/`](../../_archive/agents-future/)。

---

## 當前 Agent 清單

| Agent | 人物名 | 職責 | 執行模式 |
|-------|--------|------|---------|
| **Strategy Advisor** | **Aria** | 架構諮詢、設計決策、計劃書審查、結案文件同步 | Claude Code（Christ 親自開的 session） |
| **CEO** | Victoria | 接收指令、智慧分類、提案模式、Orchestrator 自動閉環、分派 Agent、追蹤執行、長期記憶 | Claude Code session |
| **PM** | Petra | 品質審核閘門（審 Rosa / Demi / Dev_plan / Vera 四個產出）、申訴仲裁 | Claude Code session |
| **Requirements** | Rosa | 需求拆解、轉換成 GitHub Issues | API call |
| **Designer** | Demi | 需求 → MudBlazor UI 規格（Stage 25b 起進入設計階段） | API call |
| **Dev** | Cody | 寫程式、解 Bug、重構、操作 repo、開 PR | Claude Code session |
| **QA** | Quinn | 自動化測試（xUnit + NSubstitute + FluentAssertions）+ Playwright E2E | Claude Code session |
| **Reviewer** | Vera | Code Review（分級 🔴/🟡/🟢）、影響範圍分析 | Claude Code session |
| **Doc** | Sage | 收尾歸檔員（CHANGELOG / archive 整理） | API call |
| **Release** | Rena | 版本管理、Git Release tag | API call |
| **Ops** | Maya | 部署監控、CI/CD、健康檢查告警、rollback | API call |

---

## 設計原則

### Ops 與 Release 的分工（接力關係，不重疊）

```
Release Agent（Rena）          Ops Agent（Maya）
  → 決定版本號                   → 執行部署
  → 整理 Changelog                → 監控服務狀態
  → 產出 Release Notes            → 出問題時 rollback
  → 建立 GitHub Release tag
```

### 不拆分前後端 Dev

技術棧是 Blazor，前後端都是 C#，同一套規範，一個 Dev Agent 全端處理。
若未來有獨立的行動 App（React Native / MAUI），再考慮新增 App Dev Agent。

### UI 元件庫

**MudBlazor 8.x**（MIT 授權，Stage 6 全面替換 Telerik）。所有 Agent 文件中的元件規格以 MudBlazor 為準。

---

## 個別 Agent 細節

| Agent | 角色 lore | 執行 prompt template |
|---|---|---|
| Aria | [Advisor_Agent.md](../Advisor_Agent.md) | memory: workflow_aria.md（Christ 私人）|
| Victoria | [CEO_Agent.md](./CEO_Agent.md) | `src/AiTeam.Bot/Resources/CLAUDE_Victoria.md` |
| Petra | [PM_Agent.md](./PM_Agent.md) | `src/AiTeam.Bot/Resources/CLAUDE_Petra.md` |
| Cody | [Dev_Agent.md](./Dev_Agent.md) | `src/AiTeam.Bot/Resources/CLAUDE_CODY.md` |
| Vera | [Reviewer_Agent.md](./Reviewer_Agent.md) | `src/AiTeam.Bot/Resources/CLAUDE_Vera.md` |
| Quinn | [QA_Agent.md](./QA_Agent.md) | `src/AiTeam.Bot/Resources/CLAUDE_QA.md` |
| Sage | [Doc_Agent.md](./Doc_Agent.md) | `src/AiTeam.Bot/Resources/CLAUDE_Sage.md` |
| Rosa | [Requirements_Agent.md](./Requirements_Agent.md) | `src/AiTeam.Bot/Resources/CLAUDE_Rosa.md` |
| Demi | [Designer_Agent.md](./Designer_Agent.md) | `src/AiTeam.Bot/Resources/CLAUDE_Demi.md` |
| Rena | [Release_Agent.md](./Release_Agent.md) | API prompt（無 CLI template） |
| Maya | [Ops_Agent.md](./Ops_Agent.md) | API prompt（無 CLI template） |
