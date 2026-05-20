namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：Petra Orchestrator 執行結果（v5 動態架構 PoC）。
/// Stage 80：加 Paused 工廠（HITL plan_confirm 閘門 — Petra 拆完 plan 後等 Christ 拍板 / chain dispatch 0 啟動）。
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

    /// <summary>Stage 80：HITL plan_confirm 等待 — session Status=paused / chain dispatch 0 啟動 / 等 PlanConfirmationProcessor 拉起 resume。
    /// PetraDispatchWorker.DispatchOneAsync 視 Paused 為 success path 完成 row（PetraInbox 標 completed / session 自己 track 真實 work）。</summary>
    public static PetraOrchestratorResult Paused(Guid sessionId, IReadOnlyList<string> caps, string summary) =>
        new(sessionId, true, 0, caps, summary);
}
