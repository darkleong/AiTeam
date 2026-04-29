using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data.Repositories;

/// <summary>
/// token_logs 資料存取，供 Bot 寫入 Token 用量、Dashboard 彙總費用監控。
/// Stage 44：SUM 公式升級為等效 token（input + output + cache_creation × 1.25 + cache_read × 0.1，
/// 對齊 Anthropic 計費），整數運算 × 5/4 與 ÷ 10 讓 EF Core 可 translate 到 SQL。
/// 回傳型別 int → long 避免高用量月份 overflow。
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

    /// <summary>
    /// 查詢指定 Agent 今日（UTC）已用等效 token。
    /// Stage 44：等效 = input + output + cache_creation × 1.25 + cache_read × 0.1
    /// （以整數運算 × 5/4 / ÷ 10 表達讓 EF Core 可 translate 到 SQL；舊資料 cache 欄位 null 視為 0）。
    /// </summary>
    public async Task<long> GetAgentDailyTotalAsync(
        string agentName,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        return await db.TokenLogs
            .AsNoTracking()
            .Where(t => t.AgentName == agentName && t.CreatedAt >= today && t.CreatedAt < tomorrow)
            .SumAsync(t =>
                (long)t.InputTokens + t.OutputTokens
                + ((long)(t.CacheCreationTokens ?? 0) * 5L) / 4L
                + (long)(t.CacheReadTokens ?? 0) / 10L,
                cancellationToken);
    }

    /// <summary>查詢指定 Agent 本月（UTC）已用等效 token（公式同 daily）。</summary>
    public async Task<long> GetAgentMonthlyTotalAsync(
        string agentName,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        return await db.TokenLogs
            .AsNoTracking()
            .Where(t => t.AgentName == agentName && t.CreatedAt >= monthStart && t.CreatedAt < monthEnd)
            .SumAsync(t =>
                (long)t.InputTokens + t.OutputTokens
                + ((long)(t.CacheCreationTokens ?? 0) * 5L) / 4L
                + (long)(t.CacheReadTokens ?? 0) / 10L,
                cancellationToken);
    }

    /// <summary>查詢所有 Agent 本月（UTC）已用等效 token（全域月限判斷用）。</summary>
    public async Task<long> GetGlobalMonthlyTotalAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        return await db.TokenLogs
            .AsNoTracking()
            .Where(t => t.CreatedAt >= monthStart && t.CreatedAt < monthEnd)
            .SumAsync(t =>
                (long)t.InputTokens + t.OutputTokens
                + ((long)(t.CacheCreationTokens ?? 0) * 5L) / 4L
                + (long)(t.CacheReadTokens ?? 0) / 10L,
                cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
        => await db.SaveChangesAsync(cancellationToken);
}
