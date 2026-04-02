using AiTeam.Data;
using AiTeam.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>Token 用量查詢服務，供 Dashboard Token 監控頁使用。</summary>
public class DashboardTokenService(AppDbContext db)
{
    #region Public Methods

    /// <summary>
    /// 查詢指定時間區間的 Token 彙總資料。
    /// inputRate / outputRate 為每千 token 的費用（美金），從 app_settings 傳入。
    /// </summary>
    public async Task<TokenSummaryDto> GetSummaryAsync(
        DateTime from,
        DateTime to,
        decimal inputRatePer1k = 0.003m,
        decimal outputRatePer1k = 0.015m,
        CancellationToken cancellationToken = default)
    {
        var logs = await db.TokenLogs
            .AsNoTracking()
            .Where(t => t.CreatedAt >= from && t.CreatedAt <= to)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var agentSummaries = logs
            .GroupBy(l => l.AgentName)
            .Select(g =>
            {
                var totalInput  = g.Sum(l => l.InputTokens);
                var totalOutput = g.Sum(l => l.OutputTokens);
                return new TokenAgentSummaryDto
                {
                    AgentName         = g.Key,
                    Model             = g.OrderByDescending(l => l.CreatedAt).First().Model,
                    TotalInputTokens  = totalInput,
                    TotalOutputTokens = totalOutput,
                    EstimatedCostUsd  = Math.Round(
                        (totalInput / 1000m) * inputRatePer1k +
                        (totalOutput / 1000m) * outputRatePer1k, 4)
                };
            })
            .OrderBy(s => s.AgentName)
            .ToList();

        var dailyPoints = logs
            .GroupBy(l => (l.CreatedAt.Date, l.AgentName))
            .Select(g => new TokenDailyDataPointDto
            {
                Date        = g.Key.Date,
                AgentName   = g.Key.AgentName,
                TotalTokens = g.Sum(l => l.InputTokens + l.OutputTokens)
            })
            .OrderBy(p => p.Date)
            .ToList();

        return new TokenSummaryDto
        {
            AgentSummaries  = agentSummaries,
            DailyDataPoints = dailyPoints
        };
    }

    #endregion
}
