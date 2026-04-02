using AiTeam.Bot.Configuration;
using AiTeam.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Services;

/// <summary>
/// 從 PostgreSQL rules 表讀取 CEO 規則，取代原 Notion 整合。
/// 使用 TTL 記憶體快取，支援 /reload-rules 強制清除。
/// </summary>
public class RulesService(
    IServiceScopeFactory scopeFactory,
    IOptions<AgentSettings> agentSettings,
    ILogger<RulesService> logger)
{
    private readonly int _cacheTtlMinutes = agentSettings.Value.RulesCacheTtlMinutes;

    private List<string> _cachedRules = [];
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    /// <summary>
    /// 取得啟用的規則清單（TTL Cache，到期自動重新從 DB 讀取）。
    /// </summary>
    public async Task<IReadOnlyList<string>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        if (DateTime.UtcNow < _cacheExpiry)
            return _cachedRules;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // 雙重檢查，避免多執行緒重複載入
            if (DateTime.UtcNow < _cacheExpiry)
                return _cachedRules;

            _cachedRules = await FetchRulesFromDbAsync(cancellationToken);
            _cacheExpiry = DateTime.UtcNow.AddMinutes(_cacheTtlMinutes);
            logger.LogInformation("規則已從 DB 載入，共 {Count} 條，下次更新：{Expiry:HH:mm}", _cachedRules.Count, _cacheExpiry);
        }
        finally
        {
            _cacheLock.Release();
        }

        return _cachedRules;
    }

    /// <summary>
    /// 強制清除 Cache，下次呼叫 GetRulesAsync 時重新從 DB 讀取。
    /// </summary>
    public void InvalidateCache()
    {
        _cacheExpiry = DateTime.MinValue;
        logger.LogInformation("規則 Cache 已清除");
    }

    private async Task<List<string>> FetchRulesFromDbAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            return await db.Rules
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.SortOrder)
                .ThenBy(r => r.CreatedAt)
                .Select(r => r.Content)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "從 DB 載入規則失敗，使用上次 Cache");
            return _cachedRules;
        }
    }
}
