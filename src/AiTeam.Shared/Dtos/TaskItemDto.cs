namespace AiTeam.Shared.Dtos;

/// <summary>任務列表顯示用 DTO（不含 Logs，避免資料量過大）。</summary>
public class TaskItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string TriggeredBy { get; set; } = "";
    public string AssignedAgent { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ProjectName { get; set; }
    public string? TeamName { get; set; }
}
