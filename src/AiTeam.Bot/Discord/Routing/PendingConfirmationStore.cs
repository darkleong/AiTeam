using System.Collections.Concurrent;
using AiTeam.Data;

namespace AiTeam.Bot.Discord.Routing;

/// <summary>
/// Stage 36：跨 CommandHandler / Router / OrchestrationService 共用的 pending confirmation 暫存 Singleton。
///
/// 原 CommandHandler 6 個 Dictionary（_pendingConfirmations / _pendingAdjustments /
/// _pendingKickoffConfirmations / _pendingKickoffModify / _pendingDesignConfirmations / _pendingDesignModify）
/// 統一搬進本 Store，並全面升級為 ConcurrentDictionary（原為 Dictionary，未加鎖）。
///
/// 升級動機：Store 抽出後，Register 端（OrchestrationService 各處）與 Lookup/Remove 端（ButtonCallbackRouter）
/// 分散於不同 thread（Dashboard task、Discord event loop、WebhookController task 等），
/// 原「都在 Discord event loop 內單 thread」的假設不再成立。
/// </summary>
public sealed class PendingConfirmationStore
{
    private readonly ConcurrentDictionary<ulong, PendingConfirmation> _confirmations = new();
    private readonly ConcurrentDictionary<ulong, PendingConfirmation> _adjustments = new();
    private readonly ConcurrentDictionary<ulong, Guid> _kickoffConfirmations = new();
    private readonly ConcurrentDictionary<ulong, Guid> _kickoffModify = new();
    private readonly ConcurrentDictionary<ulong, (Guid GroupId, string PetraSessionId)> _designConfirmations = new();
    private readonly ConcurrentDictionary<ulong, (Guid GroupId, string PetraSessionId)> _designModify = new();
    private readonly ConcurrentDictionary<ulong, List<TaskGroup>> _cancelSelections = new();

    // ---------- Confirmations（CEO 確認 / 提案 / devplan escalate） ----------
    public void RegisterConfirmation(ulong messageId, PendingConfirmation pending) =>
        _confirmations[messageId] = pending;
    public bool TryGetConfirmation(ulong messageId, out PendingConfirmation pending) =>
        _confirmations.TryGetValue(messageId, out pending!);
    public bool RemoveConfirmation(ulong messageId) =>
        _confirmations.TryRemove(messageId, out _);

    // ---------- Adjustments（Stage 10 ✏️ 修改輸入等待） ----------
    public void RegisterAdjustment(ulong userId, PendingConfirmation pending) =>
        _adjustments[userId] = pending;
    public bool TryGetAdjustment(ulong userId, out PendingConfirmation pending) =>
        _adjustments.TryGetValue(userId, out pending!);
    public bool RemoveAdjustment(ulong userId) =>
        _adjustments.TryRemove(userId, out _);

    // ---------- Kickoff Confirmation（Stage 25a Kick-off 按鈕） ----------
    public void RegisterKickoffConfirmation(ulong messageId, Guid groupId) =>
        _kickoffConfirmations[messageId] = groupId;
    public bool TryGetKickoffConfirmation(ulong messageId, out Guid groupId) =>
        _kickoffConfirmations.TryGetValue(messageId, out groupId);
    public bool RemoveKickoffConfirmation(ulong messageId) =>
        _kickoffConfirmations.TryRemove(messageId, out _);

    // ---------- Kickoff Modify（Stage 25a Christ 修改意見等待） ----------
    public void RegisterKickoffModify(ulong userId, Guid groupId) =>
        _kickoffModify[userId] = groupId;
    public bool TryGetKickoffModify(ulong userId, out Guid groupId) =>
        _kickoffModify.TryGetValue(userId, out groupId);
    public bool RemoveKickoffModify(ulong userId) =>
        _kickoffModify.TryRemove(userId, out _);

    // ---------- Design Confirmation（Stage 25b Design 按鈕） ----------
    public void RegisterDesignConfirmation(ulong messageId, Guid groupId, string petraSessionId) =>
        _designConfirmations[messageId] = (groupId, petraSessionId);
    public bool TryGetDesignConfirmation(ulong messageId, out (Guid GroupId, string PetraSessionId) value) =>
        _designConfirmations.TryGetValue(messageId, out value);
    public bool RemoveDesignConfirmation(ulong messageId) =>
        _designConfirmations.TryRemove(messageId, out _);

    // ---------- Design Modify（Stage 25b Christ 修改意見等待） ----------
    public void RegisterDesignModify(ulong userId, Guid groupId, string petraSessionId) =>
        _designModify[userId] = (groupId, petraSessionId);
    public bool TryGetDesignModify(ulong userId, out (Guid GroupId, string PetraSessionId) value) =>
        _designModify.TryGetValue(userId, out value);
    public bool RemoveDesignModify(ulong userId) =>
        _designModify.TryRemove(userId, out _);

    // ---------- Cancel Selection（Stage 14 /cancel 選擇等待） ----------
    public void RegisterCancelSelection(ulong userId, List<TaskGroup> groups) =>
        _cancelSelections[userId] = groups;
    public bool TryGetCancelSelection(ulong userId, out List<TaskGroup> groups) =>
        _cancelSelections.TryGetValue(userId, out groups!);
    public bool RemoveCancelSelection(ulong userId) =>
        _cancelSelections.TryRemove(userId, out _);

    // ---------- Stage 51：Kickoff Mid-Interrupt Apply（Christ 修改指引文字等待） ----------
    private readonly ConcurrentDictionary<ulong, Guid> _kickoffMidInterruptApply = new();
    public void RegisterKickoffMidInterruptApply(ulong userId, Guid groupId) =>
        _kickoffMidInterruptApply[userId] = groupId;
    public bool TryGetKickoffMidInterruptApply(ulong userId, out Guid groupId) =>
        _kickoffMidInterruptApply.TryGetValue(userId, out groupId);
    public bool RemoveKickoffMidInterruptApply(ulong userId) =>
        _kickoffMidInterruptApply.TryRemove(userId, out _);
}
