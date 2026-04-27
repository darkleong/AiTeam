# PR #122 歸檔 — Dashboard 錯誤處理 UX 打磨：雙軌通知（MudAlert + Toast）

**日期**：2026-04-27
**PR 連結**：https://github.com/feature/110-dashboard-notification-service

## 實作摘要

無實作說明

## 審查摘要

DashboardNotificationService Phase 1 已完成服務定義與 DI 註冊（Program.cs 新增 `AddScoped<DashboardNotificationService>()`），設計清晰、ISnackbar 依存配置正確、Scoped 生命週期無衝突。時間分級策略（Success 3 秒 / Warning 5 秒 / Error 8 秒）已定義，為後續元件遷移奠定基礎。

審查重點建議：ShowError 的 onUndo 回調當前缺乏例外防護，若呼叫端傳入的 onUndo 拋出異常，將透過 MudBlazor 元件事件鏈傳播至 Blazor Server Circuit 造成斷線風險。建議改為 `async _ => { try { await onUndo(); } catch { /* log */ } }`，或在 XML 文件明確要求呼叫端保證例外不外逸。

## 相關資訊

- 任務標題：Dashboard 錯誤處理 UX 打磨：雙軌通知（MudAlert + Toast）
- 完成時間：2026-04-27
- 影響範圍：現有 9 個元件（SystemSettings、AgentSettings、InteractionCenter、RuleManagement、MockScenarioCard、GlobalQueueControlCard、AgentStatusCard、TaskCenter、PipelineView）仍直接注入 ISnackbar，Phase 2 遷移時將統一替換至 DashboardNotificationService
- Phase 2 後續檢查項：確認現有元件無 UI 測試依賴特定 Snackbar 持續時間
