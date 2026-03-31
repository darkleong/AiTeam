namespace AiTeam.Shared.ViewModels;

/// <summary>
/// 任務狀態變動 ViewModel，用於 Bot → Dashboard SignalR 推送。
/// </summary>
public class TaskUpdateViewModel
{
    public Guid   TaskId    { get; set; }
    public string Title     { get; set; } = "";
    public string Status    { get; set; } = "";
    public string AgentName { get; set; } = "";
}
