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
    public DateTime CreatedAt       { get; set; }

    // Stage 26：Kickoff / Design 會議紀錄與計劃書（供 PipelineView 折疊面板顯示）
    public string?  KickoffMeetingLog { get; set; }
    public string?  TaskPlan          { get; set; }
    public int      KickoffRound      { get; set; }
    public string?  DesignMeetingLog  { get; set; }
    public string?  DesignPlan        { get; set; }
    public int      DesignRound       { get; set; }

    // Stage 26 追加：流程文件折疊面板（實作計劃書 / 驗收報告 / 測試報告）
    public string?  DevPlan          { get; set; }  // 實作計劃書
    public string?  LastReviewBody   { get; set; }  // 驗收報告（Vera 最新審查）
    public string?  TestReport       { get; set; }  // 測試報告（Quinn）

    // Stage 29-1：歸檔報告折疊面板
    public string?  ArchiveContent   { get; set; }  // Sage 歸檔報告

    // Stage 31：Appeal 對抗紀錄折疊面板
    public string? ReviewAppealLog     { get; set; }
    public int     ReviewAppealRoundA  { get; set; }
    public string? DevPlanAppealLog    { get; set; }
    public int     DevPlanAppealRoundA { get; set; }
}
