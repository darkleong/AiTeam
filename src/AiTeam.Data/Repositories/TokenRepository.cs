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

    /// <summary>查詢指定 Agent 今日（UTC）已用總 token（input + output）。</summary>
    public async Task<int> GetAgentDailyTotalAsync(
        string agentName,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        return await db.TokenLogs
            .AsNoTracking()
            .Where(t => t.AgentName == agentName && t.CreatedAt >= today && t.CreatedAt < tomorrow)
            .SumAsync(t => t.InputTokens + t.OutputTokens, cancellationToken);
    }

    /// <summary>查詢指定 Agent 本月（UTC）已用總 token（input + output）。</summary>
    public async Task<int> GetAgentMonthlyTotalAsync(
        string agentName,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        return await db.TokenLogs
            .AsNoTracking()
            .Where(t => t.AgentName == agentName && t.CreatedAt >= monthStart && t.CreatedAt < monthEnd)
            .SumAsync(t => t.InputTokens + t.OutputTokens, cancellationToken);
    }

    /// <summary>查詢所有 Agent 本月（UTC）已用總 token（全域月限判斷用）。</summary>
    public async Task<int> GetGlobalMonthlyTotalAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        return await db.TokenLogs
            .AsNoTracking()
            .Where(t => t.CreatedAt >= monthStart && t.CreatedAt < monthEnd)
            .SumAsync(t => t.InputTokens + t.OutputTokens, cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
        => await db.SaveChangesAsync(cancellationToken);
}
