using AiTeam.Data;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Bot.Services;

/// <summary>
/// Stage 38：Agent 的 Provider / Model 動態設定快取（DB 為唯一權威，appsettings 僅作啟動 seed）。
/// Stage 47：擴充含 DailyTokenLimitK / MonthlyTokenLimitK（null = DB 未設定，runtime fallback appsettings）。
/// 設計對齊 <see cref="AppSettingsService"/>：TTL 5 分、SemaphoreSlim double-check lock、失敗時保留上次快取。
/// LlmProviderFactory.Create() 為同步方法（12 callers），因此 Get 也提供同步 API，使用 sync-over-async
/// 模式（沿用既有 MockMode 的 .GetAwaiter().GetResult() 慣例）。
/// </summary>
public class AgentConfigCache(
    IServiceScopeFactory scopeFactory,
    ILogger<AgentConfigCache> logger)
{
    private const int CacheTtlMinutes = 5;

    private Dictionary<string, (string? Provider, string? Model, int? DailyTokenLimitK, int? MonthlyTokenLimitK)> _cache = [];
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// 同步取得指定 Agent 的 Provider / Model / Token Limit override。
    /// 找不到 Agent 或欄位為 null 時回傳 null，呼叫方應 fallback 到 appsettings。
    /// </summary>
    public (string? Provider, string? Model, int? DailyTokenLimitK, int? MonthlyTokenLimitK) Get(string agentName)
    {
        EnsureCacheAsync(CancellationToken.None).GetAwaiter().GetResult();
        return _cache.TryGetValue(agentName, out var v) ? v : (null, null, null, null);
    }

    /// <summary>強制讓快取失效，下次讀取時重新從 DB 載入。Dashboard 改完 AgentConfig 後透過 Internal API 呼叫。</summary>
    public void InvalidateCache() => _cacheExpiry = DateTime.MinValue;

    /// <summary>啟動時預熱快取，避免第一筆任務觸發 sync DB 載入 block 執行緒。</summary>
    public Task WarmupAsync(CancellationToken cancellationToken = default)
        => EnsureCacheAsync(cancellationToken);

    private async Task EnsureCacheAsync(CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow < _cacheExpiry) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (DateTime.UtcNow < _cacheExpiry) return;

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            _cache = await db.AgentConfigs
                .AsNoTracking()
                .ToDictionaryAsync(
                    a => a.Name,
                    a => (a.Provider, a.Model, a.DailyTokenLimitK, a.MonthlyTokenLimitK),
                    cancellationToken);

            _cacheExpiry = DateTime.UtcNow.AddMinutes(CacheTtlMinutes);
            logger.LogInformation("AgentConfigCache 已從 DB 載入，共 {Count} 個 Agent", _cache.Count);
        }
        catch (Exception ex)
        {
            // 沿用 AppSettingsService 行為：失敗時保留上次快取（empty → runtime 全 fallback appsettings）
            logger.LogError(ex, "AgentConfigCache 載入失敗，使用上次快取");
        }
        finally
        {
            _lock.Release();
        }
    }
}
