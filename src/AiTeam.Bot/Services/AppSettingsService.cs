using AiTeam.Data;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Bot.Services;

/// <summary>
/// 動態系統設定服務：從 app_settings 表讀取設定值，使用 TTL 快取。
/// 設定在 Dashboard 修改後，下次 Bot 執行任務時自動生效（不需重啟）。
/// </summary>
public class AppSettingsService(
    IServiceScopeFactory scopeFactory,
    ILogger<AppSettingsService> logger)
{
    private const int CacheTtlMinutes = 5;

    private Dictionary<string, string> _cache = [];
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>取得 bool 設定值，找不到時回傳 defaultValue。</summary>
    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync(key, cancellationToken);
        if (value is null) return defaultValue;
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>取得字串設定值，找不到時回傳 null。</summary>
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await EnsureCacheAsync(cancellationToken);
        return _cache.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>強制清除快取，下次讀取時重新從 DB 載入。</summary>
    public void InvalidateCache() => _cacheExpiry = DateTime.MinValue;

    private async Task EnsureCacheAsync(CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow < _cacheExpiry) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (DateTime.UtcNow < _cacheExpiry) return;

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            _cache = await db.AppSettings
                .AsNoTracking()
                .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

            _cacheExpiry = DateTime.UtcNow.AddMinutes(CacheTtlMinutes);
            logger.LogInformation("AppSettings 已從 DB 載入，共 {Count} 項", _cache.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AppSettings 載入失敗，使用上次快取");
        }
        finally
        {
            _lock.Release();
        }
    }
}
