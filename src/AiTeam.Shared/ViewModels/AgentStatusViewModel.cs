namespace AiTeam.Shared.ViewModels;

/// <summary>
/// Agent 狀態 ViewModel，用於首頁狀態卡片與 SignalR 即時推送。
/// </summary>
public class AgentStatusViewModel
{
    public string AgentName { get; set; } = "";
    /// <summary>idle / running / error</summary>
    public string Status { get; set; } = "idle";
    public int TrustLevel { get; set; }
    public string? CurrentTaskTitle { get; set; }
    public int TodayCompletedCount { get; set; }
    public int TodayFailedCount { get; set; }
    public DateTime LastUpdated { get; set; }
}
