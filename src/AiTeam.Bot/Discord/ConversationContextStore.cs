namespace AiTeam.Bot.Discord;

/// <summary>
/// 按 channelId 保存各頻道的對話歷史，供 CEO 多輪對話使用。
/// 以 Singleton 生命週期注入，Bot 重啟後清空（可接受，確認機制短期內有效）。
/// </summary>
public class ConversationContextStore
{
    private const int MaxTurns = 6; // 保留最近 6 輪（約 3 問 3 答）

    private readonly Dictionary<ulong, List<ConversationTurn>> _store = [];
    private readonly Lock _lock = new();

    /// <summary>取得指定頻道的對話歷史（空清單表示無歷史）。</summary>
    public IReadOnlyList<ConversationTurn> GetHistory(ulong channelId)
    {
        lock (_lock)
        {
            return _store.TryGetValue(channelId, out var turns)
                ? turns.AsReadOnly()
                : [];
        }
    }

    /// <summary>新增一輪對話（超過 MaxTurns 時移除最舊的）。</summary>
    public void AddTurn(ulong channelId, string role, string content)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(channelId, out var turns))
                _store[channelId] = turns = [];

            turns.Add(new ConversationTurn(role, content));

            if (turns.Count > MaxTurns)
                turns.RemoveAt(0);
        }
    }

    /// <summary>清除指定頻道的對話歷史（任務確認後呼叫）。</summary>
    public void Clear(ulong channelId)
    {
        lock (_lock)
            _store.Remove(channelId);
    }
}

/// <summary>一輪對話的角色與內容。</summary>
public record ConversationTurn(string Role, string Content);
