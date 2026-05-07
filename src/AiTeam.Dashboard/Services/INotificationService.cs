namespace AiTeam.Dashboard.Services;

public interface INotificationService
{
    void Success(string message);
    void Warning(string message);
    void Error(string message, string? details = null);
}
