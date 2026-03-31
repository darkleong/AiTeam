namespace AiTeam.Shared.Dtos;

/// <summary>任務步驟 Log DTO（點擊任務後展開用）。</summary>
public class TaskLogDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string Agent { get; set; } = "";
    public string Step { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Payload { get; set; }
    public DateTime CreatedAt { get; set; }
}
