using MudBlazor;

namespace AiTeam.Dashboard.Services;

public class DashboardNotificationService(ISnackbar snackbar) : IDashboardNotificationService
{
    private const int MaxMessageLength = 80;

    public Task ShowSuccessAsync(string message, int durationMs = 3000)
    {
        snackbar.Add(Truncate(message), Severity.Success, options => options.VisibleStateDuration = durationMs);
        return Task.CompletedTask;
    }

    public Task ShowErrorAsync(string message, int durationMs = 5000)
    {
        snackbar.Add(Truncate(message), Severity.Error, options => options.VisibleStateDuration = durationMs);
        return Task.CompletedTask;
    }

    public Task ShowWarningAsync(string message, int durationMs = 4000)
    {
        snackbar.Add(Truncate(message), Severity.Warning, options => options.VisibleStateDuration = durationMs);
        return Task.CompletedTask;
    }

    public void ClearAllToasts() => snackbar.Clear();

    private static string Truncate(string message)
        => message.Length > MaxMessageLength ? string.Concat(message.AsSpan(0, MaxMessageLength), "...") : message;
}
