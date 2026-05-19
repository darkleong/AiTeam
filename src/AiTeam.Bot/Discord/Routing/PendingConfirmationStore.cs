using System.Collections.Concurrent;

namespace AiTeam.Bot.Discord.Routing;

/// <summary>
/// Stage 36：跨 CommandHandler / Router / OrchestrationService 共用的 pending confirmation 暫存 Singleton。
///
/// Stage 78c：v4 Pipeline framework 整套砍後 Store 縮為 v5.5 generic confirmation 唯一 type：
///   - ceo_confirm BossInteraction Discord button → confirm_yes Stage 68 短路 ack
///
/// 砍範圍（Stage 78c）：
///   - _adjustments + 3 methods（v4 提案調整 path）
///   - _kickoffConfirmations + 3 methods（v4 Kickoff button）
///   - _kickoffModify + 3 methods（v4 Kickoff 修改意見）
///   - _designConfirmations + 3 methods（v4 Design button）
///   - _designModify + 3 methods（v4 Design 修改意見）
///   - _cancelSelections + 3 methods（v4 cancel selection）
///   - _kickoffMidInterruptApply + 3 methods（v4 Stage 51 HITL mid interrupt）
/// </summary>
public sealed class PendingConfirmationStore
{
    private readonly ConcurrentDictionary<ulong, PendingConfirmation> _confirmations = new();

    // ---------- Confirmations（v5.5 ceo_confirm Discord button → confirm_yes Stage 68 短路） ----------
    public void RegisterConfirmation(ulong messageId, PendingConfirmation pending) =>
        _confirmations[messageId] = pending;
    public bool TryGetConfirmation(ulong messageId, out PendingConfirmation pending) =>
        _confirmations.TryGetValue(messageId, out pending!);
    public bool RemoveConfirmation(ulong messageId) =>
        _confirmations.TryRemove(messageId, out _);
}
