using AiTeam.Bot.Agents;
using AiTeam.Data;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Bot.Services;

/// <summary>
/// Stage 44：CLI Agent token 寫入共用 helper。
/// 硬規則：寫入失敗 catch + log warning，不 throw（不阻塞主流程）。
/// 16 個 CLI caller 都呼叫此 helper，避免重複 try-catch boilerplate。
/// 用獨立 scope + 新 DbContext 寫入：CLI subprocess 跑 5–30 分鐘，主流程的 DbContext
/// 早就被其他寫入污染或處於 Tracking 狀態，token 寫入用獨立 scope 最安全。
/// </summary>
public class TokenLogService(
    IServiceScopeFactory scopeFactory,
    DashboardPushService dashboardPush,
    ILogger<TokenLogService> logger)
{
    /// <summary>
    /// 寫入一筆 CLI token 紀錄。usage 為 null 時 early return（CLI 解析失敗不視為錯）。
    /// 任何異常吞掉並 log warning（硬規則：不阻塞主流程）。
    /// </summary>
    public async Task LogCliUsageAsync(
        string agentName,
        string model,
        string? stage,
        int? round,
        Guid? taskId,
        TokenUsage? usage,
        CancellationToken ct = default)
    {
        if (usage is null) return;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TokenLogs.Add(new TokenLog
            {
                AgentName           = agentName,
                Model               = model,
                InputTokens         = usage.InputTokens,
                OutputTokens        = usage.OutputTokens,
                Stage               = stage,
                Round               = round,
                CacheCreationTokens = usage.CacheCreationTokens,
                CacheReadTokens     = usage.CacheReadTokens,
                TotalCostUsd        = usage.TotalCostUsd,
                TaskId              = taskId,
                CreatedAt           = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);

            try { await dashboardPush.PushTokenUpdateAsync(); }
            catch (Exception pushEx)
            {
                logger.LogDebug(pushEx, "Dashboard token push 通知失敗（不影響主流程）");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "CLI token 寫入失敗（Agent={Agent}，Stage={Stage}），不影響主流程", agentName, stage);
        }
    }
}
