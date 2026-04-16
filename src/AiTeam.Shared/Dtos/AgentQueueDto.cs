namespace AiTeam.Shared.Dtos;

/// <summary>
/// Stage 27b：Agent 佇列狀態 DTO，用於 Dashboard 視覺化。
/// 包含 Agent 的狀態、佇列深度，以及排隊中的任務清單。
/// </summary>
public class AgentQueueDto
{
    public string AgentName { get; set; } = "";

    /// <summary>active / paused / stopping / stopped</summary>
    public string AgentState { get; set; } = "active";

    /// <summary>排隊中（QueueStatus = "queued"）的任務數量。</summary>
    public int QueueDepth { get; set; }

    /// <summary>目前執行中（QueueStatus = "processing"）的任務標題，無則為 null。</summary>
    public string? CurrentTaskTitle { get; set; }

    /// <summary>排隊中的任務列表（依 QueuedAt ASC 排序）。</summary>
    public List<QueuedTaskItemDto> QueuedTasks { get; set; } = [];
}

/// <summary>
/// Stage 27b：排隊中的單一任務摘要，用於 Dashboard 佇列展開清單。
/// </summary>
public class QueuedTaskItemDto
{
    public Guid      TaskId   { get; set; }
    public string    Title    { get; set; } = "";
    public DateTime? QueuedAt { get; set; }
}
