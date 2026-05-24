namespace AiTeam.Shared.Dtos;

/// <summary>Stage 85 子項 1：失敗告知三層 alert 機制的 SignalR push payload。
///
/// Bot → DashboardPushService.PushAlertAsync → Dashboard /internal/agent-status/alert → AgentStatusHub.ReceiveAlertEvent
/// → Dashboard 任何 circuit 內 AlertToastSubscriber 訂閱後 MudSnackbar 彈出。
///
/// 三類 EventType（對齊 plan + AlertRateLimiter key）：
///   token_guard       — TokenTrackingProvider 4 Check 觸發
///   petra_dead_letter — PetraDispatchWorker exhausted attempts → MarkDeadAsync
///   paused_timeout    — PetraSessionRecoveryService paused > N hours 自動 cancel
/// </summary>
public record AlertEventDto(
    string EventType,
    string Severity,    // "warning" / "error"
    string Message,
    DateTime Timestamp);
