namespace AiTeam.Dashboard.Services;

/// <summary>全域 Toast 通知介面（Phase 3 Dashboard 全域錯誤通知機制）。</summary>
public interface INotificationService
{
    void ShowSuccess(string message);
    void ShowError(string message);
    void ShowWarning(string message);
    void ShowInfo(string message);
}
