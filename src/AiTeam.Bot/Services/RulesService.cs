using AiTeam.Bot.Configuration;
using AiTeam.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Services;

/// <summary>
/// 從 PostgreSQL rules 表讀取規則，取代原 Notion 整合。
/// 快取所有啟用的規則物件，依 agentName 在記憶體中過濾：
///   - AgentName == null  → 全域規則，所有 Agent 都適用
///   - AgentName == agent → 僅套用到該 Agent
/// </summary>
public class RulesService(
    IServiceScopeFactory scopeFactory,
    IOptions<AgentSettings> agentSettings,
    ILogger<RulesService> logger)
{
    private readonly int _cacheTtlMinutes = agentSettings.Value.RulesCacheTtlMinutes;

    private List<Rule> _cachedRules = [];
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    /// <summary>
    /// 取得適用於指定 Agent 的規則內容清單（全域 + 該 Agent 專屬）。
    /// agentName 傳 null 時只回傳全域規則。
    /// </summary>
    public async Task<IReadOnlyList<string>> GetRulesAsync(
        string? agentName = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureCacheAsync(cancellationToken);

        return _cachedRules
            .Where(r => r.AgentName == null || r.AgentName == agentName)
            .Select(r => r.Content)
            .ToList();
    }

    /// <summary>強制清除 Cache，下次呼叫時重新從 DB 讀取。</summary>
    public void InvalidateCache()
    {
        _cacheExpiry = DateTime.MinValue;
        logger.LogInformation("規則 Cache 已清除");
    }

    private async Task EnsureCacheAsync(CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow < _cacheExpiry) return;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (DateTime.UtcNow < _cacheExpiry) return;

            _cachedRules = await FetchRulesFromDbAsync(cancellationToken);
            _cacheExpiry = DateTime.UtcNow.AddMinutes(_cacheTtlMinutes);
            logger.LogInformation("規則已從 DB 載入，共 {Count} 條，下次更新：{Expiry:HH:mm}",
                _cachedRules.Count, _cacheExpiry);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<List<Rule>> FetchRulesFromDbAsync(CancellationToken cancellationToken)
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
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "從 DB 載入規則失敗，使用上次 Cache");
            return _cachedRules;
        }
    }
}
