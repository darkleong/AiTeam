namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：Petra Orchestrator 執行結果（v5 動態架構 PoC）。
/// Stage 80：加 Paused 工廠（HITL plan_confirm 閘門 — Petra 拆完 plan 後等 Christ 拍板 / chain dispatch 0 啟動）。
/// Stage 81：加 Replanning 工廠（動態 replan + HITL retry gate — Vera critical / Quinn failed 觸發後等 Christ 4 decision 拍板）
///            + Cancelled 工廠（reject path 真實 0 dispatch / 議題 #3 命名語意對齊）
///            + PausedAtSubtaskId / RetryInstruction 兩 optional field（Replanning 攜帶 currentSubtaskId + Petra retry instruction）。
/// </summary>
public sealed record PetraOrchestratorResult(
    Guid SessionId,
    bool Success,
    int DispatchedWorkerCount,
    IReadOnlyList<string> DecidedCapabilities,
    string Summary,
    string? ErrorMessage = null,
    int? PausedAtSubtaskId = null,
    string? RetryInstruction = null)
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

    /// <summary>Stage 81：動態 replan + HITL retry gate 觸發 — Vera critical / Quinn failed 後 Petra 建議 retry instruction，
    /// session Status=paused / chain dispatch 暫停 / 等 PlanConfirmationProcessor 拉起 4 decision resume。
    /// 攜帶 currentSubtaskId + retryInstruction 給 ContextJson / caller log 訊息用。</summary>
    public static PetraOrchestratorResult Replanning(
        Guid sessionId, int currentSubtaskId, string retryInstruction, string replanReason) =>
        new(sessionId, true, 0, Array.Empty<string>(),
            $"Petra 觸發 replan（{replanReason}）等 Christ HITL 4 decision 拍板。",
            PausedAtSubtaskId: currentSubtaskId,
            RetryInstruction: retryInstruction);

    /// <summary>Stage 81：reject path / cap-reached 取消 — 真實 0 worker dispatched（vs 既有 Done 雜用 caps.Count / Failure 雜用語意）。
    /// 議題 #3 + #8 修法：DispatchedWorkerCount=0 對齊 PlanConfirmationProcessor log dispatched={Count} 顯示真實 0。</summary>
    public static PetraOrchestratorResult Cancelled(
        Guid sessionId, IReadOnlyList<string> caps, string summary) =>
        new(sessionId, true, 0, caps, summary);
}
