namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：Petra Orchestrator 執行結果（v5 動態架構 PoC）。
/// </summary>
public sealed record PetraOrchestratorResult(
    Guid SessionId,
    bool Success,
    int DispatchedWorkerCount,
    IReadOnlyList<string> DecidedCapabilities,
    string Summary,
    string? ErrorMessage = null)
{
    public static PetraOrchestratorResult Empty(Guid sessionId) =>
        new(sessionId, true, 0, Array.Empty<string>(), "Petra 動態決策回空序列（無 worker 派工）。");

    public static PetraOrchestratorResult Done(Guid sessionId, IReadOnlyList<string> caps, string summary) =>
        new(sessionId, true, caps.Count, caps, summary);

    public static PetraOrchestratorResult Failure(Guid sessionId, IReadOnlyList<string> caps, string error) =>
        new(sessionId, false, caps.Count, caps, "Petra 執行失敗 — escalate Christ + Aria。", error);
}
