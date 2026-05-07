using MudBlazor;

namespace AiTeam.Dashboard.Services;

public interface INotificationService
{
    void Success(string message);
    void Warning(string message);
    void Error(string message);
}

public class NotificationService(ISnackbar snackbar) : INotificationService
{
    public void Success(string message) => snackbar.Add(message, Severity.Success);
    public void Warning(string message) => snackbar.Add(message, Severity.Warning);
    public void Error(string message)   => snackbar.Add(message, Severity.Error);
}
