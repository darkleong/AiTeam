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

    /// <summary>Stage 33：執行中任務的 TaskItem Id（供待辦清單導航用）。</summary>
    public Guid? CurrentTaskId { get; set; }

    /// <summary>Stage 33：執行中任務所屬的 TaskGroup Id（用於點擊後導航至 PipelineView）。</summary>
    public Guid? CurrentTaskGroupId { get; set; }

    /// <summary>Stage 33：執行中任務進入佇列的時間（近似「開始執行」時間，用於顯示已跑時長）。</summary>
    public DateTime? CurrentTaskQueuedAt { get; set; }

    /// <summary>排隊中的任務列表（依 QueuedAt ASC 排序）。</summary>
    public List<QueuedTaskItemDto> QueuedTasks { get; set; } = [];
}

/// <summary>
/// Stage 27b：排隊中的單一任務摘要，用於 Dashboard 佇列展開清單。
/// Stage 33 加入 GroupId 供待辦清單導航至 PipelineView。
/// </summary>
public class QueuedTaskItemDto
{
    public Guid      TaskId   { get; set; }
    public Guid?     GroupId  { get; set; }   // Stage 33：點擊跳轉至 /pipeline?groupId=...
    public string    Title    { get; set; } = "";
    public DateTime? QueuedAt { get; set; }
}
