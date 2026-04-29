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
    string? EscalateReason,
    /// <summary>Stage 46-FF 三十五：Petra 拆 task 提案（規則層觸發 + Petra 細化拆法）。null = 未觸發或 should_split=false。</summary>
    SplitProposal? SplitProposal = null);

/// <summary>
/// Stage 46-FF 三十五：Petra 在 Design 階段提案的拆 task 結構。
/// 由 Orchestrator 規則層觸發（Issue 數 ≥ 8 / 預估行數 ≥ 500 / 跨多 Phase 標記任一）後，
/// 復用 PetraSessionId resume 問拆法，回傳此結構。should_split=false 代表 Petra 認定不該拆。
/// </summary>
public record SplitProposal(
    bool ShouldSplit,
    string Rationale,
    List<PhaseSpec> Phases);

/// <summary>Stage 46-FF 三十五：拆 task 提案中單一 Phase 的描述。</summary>
public record PhaseSpec(
    int Phase,
    string Description,
    List<int> Issues,
    int EstimatedMinutes);
