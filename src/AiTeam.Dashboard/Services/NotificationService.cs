using MudBlazor;

namespace AiTeam.Dashboard.Services;

/// <summary>INotificationService 實作，封裝 MudBlazor ISnackbar。</summary>
public class NotificationService(ISnackbar snackbar) : INotificationService
{
    public void ShowSuccess(string message) => snackbar.Add(message, Severity.Success);
    public void ShowError(string message)   => snackbar.Add(message, Severity.Error);
    public void ShowWarning(string message) => snackbar.Add(message, Severity.Warning);
    public void ShowInfo(string message)    => snackbar.Add(message, Severity.Info);
}
