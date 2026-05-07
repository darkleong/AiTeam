using MudBlazor;

namespace AiTeam.Dashboard.Services;

public class NotificationService(ISnackbar snackbar) : INotificationService
{
    public void Success(string message)
        => snackbar.Add(message, Severity.Success);

    public void Warning(string message)
        => snackbar.Add(message, Severity.Warning, config => config.VisibleStateDuration = 5000);

    public void Error(string message, string? details = null)
        => snackbar.Add(details is null ? message : $"{message}：{details}", Severity.Error,
            config => config.VisibleStateDuration = 5000);
}
