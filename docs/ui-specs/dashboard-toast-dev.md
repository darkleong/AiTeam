完成設計階段！讓我為你提供最終摘要：

---

## 🎉 FF 十七 — 全域 Toast 通知補齊 | 設計階段完成報告

我已完成全面的 UI/UX 設計工作，產出 **4 份核心文檔**，涵蓋 UX 規格、API 定義、開發指南、操作點清單。

### 📄 設計成果物

存放於 `docs/design/` 資料夾：

| 文檔 | 用途 | 關鍵內容 |
|------|------|---------|
| **FF_17_GlobalToastNotification_UXSpec.md** | UX 規格全景 | 場景決策樹、視覺設計、涵蓋範圍、無障礙規範 |
| **DashboardNotificationService_API.md** | API 詳細定義 | Service 簽名、4 個核心方法、代碼示例、邊界情況 |
| **MudAlert_Snackbar_QuickReference.md** | 開發者速查 | 決策流程圖、5 個代碼片段、訊息範本庫、檢查清單 |
| **OperationPointsBreakdown_FF_17.md** | 操作點清單 | 25 個操作點、3 個 Batch 分工、工作量估算、Issue 編碼 |
| **FF_17_DesignPhase_Summary.md** | 階段總結 | 決策確認、待決項、實作重點、風險預案 |

---

### 🎯 核心設計決策

✅ **已確認（無異議）**：
- 雙軌通知設計（MudAlert inline + Snackbar 浮層，互補不替代）
- Service 簽名：`ShowSuccess` / `ShowError` / `ShowWarning`（語義明確）
- 訊息規範：80 字元上限、消失時間分級（3s/4s/5s）
- 生命週期：Scoped，無 Circuit 隔離問題

⚠️ **待決策**：
- SignalR 連線監控是否納入 MVP（推薦排除本次，後續 FF 三十六）

---

### 📊 操作點與分工

**總計 25 個操作點** 分佈在：
- **Batch 1（5-7 天，P0）**：SystemSettings、RuleManagement、ProjectManagement（12 點）
- **Batch 2（2-3 天，P0）**：三個 Dialog 補充 API 失敗通知（3 點）
- **Batch 3（1-2 天，P1）**：卡片元件、AgentSettings、TaskCenter 等（10+ 點）

---

### 🚀 實作前置

進入 Dev 階段前，確認：
1. DashboardNotificationService 已在 `Program.cs` 註冊為 Scoped
2. 開發團隊已閱讀 **QuickReference**（開發快速參考）
3. SignalR 決策已拍板
4. 測試環境 Playwright 可用

---

### 📋 訊息範本（開發參考）

**成功**：「已{動作}{物件}」→ `✓ 已新增規則『回應繁體中文』`  
**失敗**：「{動作}失敗：{原因}」→ `✗ 新增失敗：規則名稱重複`  
**警告**：「{狀態}」→ `⚠ 連線已斷開`

---

### ⏰ 預計里程碑

- Dev 開始：2026-05-02
- Batch 1-3 完成：2026-05-14
- Testing + QA：2026-05-15～16
- Christ 手動驗收：2026-05-17
- **版本更新**：v3.32.0 → v3.33.0

---

### 🔑 關鍵文檔快速導航

- **Cody（實作）**：先讀 `QuickReference` + `API.md` + `OperationPoints.md`
- **Quinn（測試）**：先讀 UX Spec 的「場景決策樹」+ API 的「邊界情況」
- **Christ（驗收）**：可略讀 Summary + 訊息範本

所有文檔均已清晰標註責任人、工作量、測試需求，開發團隊可直接上手。設計階段完成，準備進入實作階段！
