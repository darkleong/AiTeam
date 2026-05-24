using AiTeam.Data;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Bot.Services;

/// <summary>
/// Stage 87 A2：Talent 的 Provider / Model / Token Limit 動態設定快取（取代 Stage 38 AgentConfigCache / v4 collapse 最後殘留收口）。
///
/// DB 為唯一權威（talents 表 Stage 67 baseline 6 row + Stage 87 加 DailyTokenLimitK / MonthlyTokenLimitK 兩欄位），
/// appsettings 僅作 fallback 安全網（DB Provider/Model 為 null 時生效）。
///
/// 設計對齊既有 <see cref="AppSettingsService"/> + 原 AgentConfigCache：TTL 5 分、SemaphoreSlim double-check lock、失敗時保留上次快取。
/// LlmProviderFactory.Create() 為同步方法（Petra dispatch 3 site 全 sync），因此 Get 也提供同步 API，使用 sync-over-async 模式
/// （沿用既有 MockMode 的 .GetAwaiter().GetResult() 慣例）。
///
/// Cache key 為 Talent.Name（baseline："Petra" / "Victoria" / "Cody" / "Vera" / "Quinn" / "Sage"）—
/// 取代 Stage 38 既有 cache key AgentName（v4 9 角色 "PM" / "CEO" / "Dev" 等）。
/// </summary>
public class TalentMetaCache(
    IServiceScopeFactory scopeFactory,
    ILogger<TalentMetaCache> logger)
{
    private const int CacheTtlMinutes = 5;

    private Dictionary<string, (string? Provider, string? Model, int? DailyTokenLimitK, int? MonthlyTokenLimitK)> _cache = [];
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// 同步取得指定 Talent.Name 的 Provider / Model / Token Limit override。
    /// 找不到 Talent 或欄位為 null 時回傳 null tuple，呼叫方應 fallback 到 appsettings。
    /// </summary>
    public (string? Provider, string? Model, int? DailyTokenLimitK, int? MonthlyTokenLimitK) Get(string talentName)
    {
        EnsureCacheAsync(CancellationToken.None).GetAwaiter().GetResult();
        return _cache.TryGetValue(talentName, out var v) ? v : (null, null, null, null);
    }

    /// <summary>強制讓快取失效，下次讀取時重新從 DB 載入。Dashboard 改完 Talent 設定後透過 Internal API reload-cache?scope=agent-config 呼叫。</summary>
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

            _cache = await db.Talents
                .AsNoTracking()
                .Where(t => t.IsActive)
                .ToDictionaryAsync(
                    t => t.Name,
                    t => (t.Provider, t.Model, t.DailyTokenLimitK, t.MonthlyTokenLimitK),
                    cancellationToken);

            _cacheExpiry = DateTime.UtcNow.AddMinutes(CacheTtlMinutes);
            logger.LogInformation("TalentMetaCache 已從 DB 載入，共 {Count} 個 Talent", _cache.Count);
        }
        catch (Exception ex)
        {
            // 沿用 AppSettingsService 行為：失敗時保留上次快取（empty → runtime 全 fallback appsettings）
            logger.LogError(ex, "TalentMetaCache 載入失敗，使用上次快取");
        }
        finally
        {
            _lock.Release();
        }
    }
}
