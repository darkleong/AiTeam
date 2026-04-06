using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data.Repositories;

/// <summary>
/// CEO 對話歷史的資料存取。
/// 每位使用者在 #victoria-ceo 頻道維護一個 active session，
/// 閒置超過 30 分鐘後自動開啟新 session。
/// </summary>
public class CeoConversationRepository(AppDbContext db)
{
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 查詢指定使用者的 active session ID。
    /// 若最近一筆訊息在 30 分鐘內，回傳現有 SessionId；否則回傳新 Guid（呼叫方在下次 AddTurnAsync 時帶入）。
    /// </summary>
    public async Task<Guid> GetActiveSessionIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var latest = await db.CeoConversations
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null && DateTime.UtcNow - latest.CreatedAt < SessionTimeout)
            return latest.SessionId;

        return Guid.NewGuid();
    }

    /// <summary>
    /// 載入指定 session 的最近 20 筆對話（按時間升冪），供組裝 Prompt 用。
    /// </summary>
    public async Task<List<CeoConversation>> GetSessionHistoryAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var turns = await db.CeoConversations
            .AsNoTracking()
            .Where(c => c.SessionId == sessionId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        // 重新排為時間升冪，供 Prompt 閱讀
        turns.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return turns;
    }

    /// <summary>新增一筆對話 turn（user 或 assistant），立即寫入 DB。</summary>
    public async Task AddTurnAsync(
        Guid sessionId,
        string userId,
        string role,
        string content,
        CancellationToken cancellationToken = default)
    {
        db.CeoConversations.Add(new CeoConversation
        {
            SessionId = sessionId,
            UserId    = userId,
            Role      = role,
            Content   = content,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>清理指定時間點之前的過期對話記錄（定期維護用）。</summary>
    public async Task<int> DeleteOldSessionsAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default)
    {
        return await db.CeoConversations
            .Where(c => c.CreatedAt < olderThan)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
