namespace AiTeam.Shared.Constants;

/// <summary>任務狀態常數，統一 Bot 和 Dashboard 使用同一組字串。</summary>
public static class TaskStatus
{
    public const string Pending            = "pending";
    public const string Running            = "running";
    public const string Done               = "done";
    public const string Failed             = "failed";
    /// <summary>Stage 43：需 Christ 介入後可恢復（如 DevPlan 重產上限 / Dev fix failed / QA fix loop 上限 / Sage escalate）。
    /// 與 Failed 區分：Failed = 明確不可挽救；NeedsIntervention = 介入後可恢復。</summary>
    public const string NeedsIntervention  = "needs_intervention";
}
