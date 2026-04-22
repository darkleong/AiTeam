namespace AiTeam.Bot.Orchestration.Meeting;

/// <summary>Stage 25a：Kick-off 會議執行結果。</summary>
public record MeetingResult(
    bool    Success,
    string  MeetingLog,
    string  TaskPlan,
    int     TotalRounds,
    string? EscalationReason = null);

/// <summary>Stage 25a：Christ 修改計劃書的 Petra 回應結果。</summary>
public record ModifyResult(
    string PetraFullOutput,
    string Impact,        // "small" | "large"
    string RevisedPlan);

/// <summary>Stage 25b：設計會議執行結果。</summary>
public record DesignMeetingResult(
    bool    Success,
    string  MeetingLog,
    string? DesignPlan,
    string? IssueUrls,
    string? UiSpecContent,
    int     TotalRounds,
    string  FinalDecision,   // "consensus" | "escalate"
    string  PetraSessionId,  // 供 escalate 路徑的 modify 流程 resume
    string? EscalateReason);
