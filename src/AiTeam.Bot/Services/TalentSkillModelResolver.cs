using AiTeam.Data;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Bot.Services;

/// <summary>
/// Stage 74：v5.5 Phase 3 Step 8 — per-Skill Model 三層 fallback chain（Singleton + 5-min TTL cache）。
///
/// 對齊 Stage 72 PromptResolver pattern：Singleton lifetime / IServiceScopeFactory 解 Singleton-Scoped 雷 /
/// InternalController reload-cache `all` scope 觸發 InvalidateCache。
///
/// 三層 fallback chain（per ResolveAsync 內邏輯）：
///   1. per-Skill：TalentSkill (TalentId, SkillName).Model / Provider（Stage 74 新欄位）— 最優先
///   2. per-Talent：Talent.Model / Provider（Stage 67 既有 nullable / Stage 74 起 0 consumer → 真實 consumer）
///   3. runtime：configuration["Agents:Dev:Model"] / Agents:Dev:Provider — 末層 fallback
///                對齊 PetraOrchestratorService.BuildSessionContext 既有 chain（Agents:Dev:Model ?? Anthropic:DefaultModel ?? "claude-opus-4-6"）
///
/// cache 內容：(TalentId, SkillName) → (Provider?, Model?) tuple；ResolveAsync 套三層 fallback 後回確定 (Provider, Model) string。
///
/// Provider tuple 保留為未來 Phase 3 真實切 GPT-4o / Gemini 鋪路 — Stage 74 範圍 Adapter 只用 Model
/// （既有 IClaudeCodeService.RunXxxAsync 簽名只吃 model + apiKey 不分 Provider）。
/// </summary>
public class TalentSkillModelResolver(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<TalentSkillModelResolver> logger)
{
    private const int CacheTtlMinutes = 5;

    // (TalentId, SkillName) → (per-Skill Provider, per-Skill Model)
    private Dictionary<(Guid TalentId, string SkillName), (string? Provider, string? Model)> _skillCache
        = new();
    // TalentId → (per-Talent Provider, per-Talent Model)
    private Dictionary<Guid, (string? Provider, string? Model)> _talentCache = new();
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// 三層 fallback chain resolve：per-Skill > per-Talent > runtime。
    /// 結果 (Provider, Model) 必非空字串（runtime fallback 永遠有 default literal 保底）。
    /// </summary>
    public async Task<(string Provider, string Model)> ResolveAsync(
        Guid talentId, string skillName, CancellationToken ct = default)
    {
        await EnsureCacheAsync(ct);

        var perSkill = _skillCache.TryGetValue((talentId, skillName), out var s)
            ? s : (Provider: (string?)null, Model: (string?)null);
        var perTalent = _talentCache.TryGetValue(talentId, out var t)
            ? t : (Provider: (string?)null, Model: (string?)null);

        // 三層 fallback：per-Skill > per-Talent > runtime（null/whitespace 都視為 fallback 條件）
        var provider = FirstNonEmpty(perSkill.Provider, perTalent.Provider, GetRuntimeProvider());
        var model    = FirstNonEmpty(perSkill.Model,    perTalent.Model,    GetRuntimeModel());
        return (provider, model);
    }

    /// <summary>強制清除 cache — 下次 ResolveAsync 重新從 DB 載入（InternalController reload-cache `all` scope 觸發）。</summary>
    public void InvalidateCache() => _cacheExpiry = DateTime.MinValue;

    private string GetRuntimeModel()
        => configuration["Agents:Dev:Model"]
        ?? configuration["Anthropic:DefaultModel"]
        ?? "claude-opus-4-6";

    private string GetRuntimeProvider()
        => configuration["Agents:Dev:Provider"]
        ?? "Anthropic";

    private static string FirstNonEmpty(string? a, string? b, string c)
    {
        if (!string.IsNullOrWhiteSpace(a)) return a;
        if (!string.IsNullOrWhiteSpace(b)) return b;
        return c;
    }

    private async Task EnsureCacheAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow < _cacheExpiry) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (DateTime.UtcNow < _cacheExpiry) return;

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var talents = await db.Talents
                .Where(x => x.IsActive)
                .Select(x => new { x.Id, x.Provider, x.Model })
                .ToListAsync(ct);
            _talentCache = talents.ToDictionary(x => x.Id, x => (x.Provider, x.Model));

            var skills = await db.TalentSkills
                .Select(x => new { x.TalentId, x.SkillName, x.Provider, x.Model })
                .ToListAsync(ct);
            _skillCache = skills.ToDictionary(x => (x.TalentId, x.SkillName), x => (x.Provider, x.Model));

            _cacheExpiry = DateTime.UtcNow.AddMinutes(CacheTtlMinutes);
            logger.LogInformation(
                "TalentSkillModelResolver cache reloaded: {TalentCount} talents / {SkillCount} talent-skill rows",
                talents.Count, skills.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TalentSkillModelResolver cache reload 失敗，沿用上次 cache");
        }
        finally
        {
            _lock.Release();
        }
    }
}
