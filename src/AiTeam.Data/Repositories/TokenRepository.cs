using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data.Repositories;

/// <summary>
/// token_logs 資料存取，供 Bot 寫入 Token 用量、Dashboard 彙總費用監控。
/// </summary>
public class TokenRepository(AppDbContext db)
{
    /// <summary>新增一筆 Token 用量記錄（呼叫方負責 SaveChangesAsync）。</summary>
    public void Add(TokenLog log) => db.TokenLogs.Add(log);

    /// <summary>依時間區間查詢所有 Token 記錄，供 Dashboard 彙總。</summary>
    public async Task<List<TokenLog>> GetByPeriodAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
        => await db.TokenLogs
            .AsNoTracking()
            .Where(t => t.CreatedAt >= from && t.CreatedAt <= to)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task SaveAsync(CancellationToken cancellationToken = default)
        => await db.SaveChangesAsync(cancellationToken);
}
