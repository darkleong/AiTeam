namespace AiTeam.Dashboard.Services;

public interface IDashboardNotificationService
{
    Task ShowSuccessAsync(string message, int durationMs = 3000);
    Task ShowErrorAsync(string message, int durationMs = 5000);
    Task ShowWarningAsync(string message, int durationMs = 4000);
    void ClearAllToasts();
}
