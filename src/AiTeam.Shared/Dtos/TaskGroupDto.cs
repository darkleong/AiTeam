namespace AiTeam.Shared.Dtos;

/// <summary>TaskGroup 列表顯示用 DTO（流程追蹤 Tab 用）。</summary>
public class TaskGroupDto
{
    public Guid     Id              { get; set; }
    public string   Title           { get; set; } = "";
    public string   Status          { get; set; } = "";
    public string?  WorkflowType    { get; set; }
    public string?  Project         { get; set; }
    public int      FixIteration    { get; set; }
    public int      DevPlanRevision { get; set; }
    public string?  DevPrUrl        { get; set; }
    public DateTime CreatedAt { get; set; }
}
