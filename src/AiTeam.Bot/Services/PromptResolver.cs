using AiTeam.Bot.Configuration;
using AiTeam.Data.Repositories;

namespace AiTeam.Bot.Services;

/// <summary>
/// Stage 72：v5.5 Phase 2 Step 5 — Prompt DB 化 cache 層（Singleton + 5-min TTL + double-check lock）。
///
/// 對齊 AppSettingsService 既有 cache pattern（Singleton lifetime / IServiceScopeFactory 解 Singleton-Scoped 雷 / InvalidateCache 觸發 reload-cache）。
///
/// 設計紀律：
/// - flag=false → 返 null（caller 退 file fallback / 0 DB query hit）
/// - flag=true → 首次 cache miss 批次 reload 全 active SkillPrompts + TalentPrompts 進 in-memory Dictionary
/// - InternalController `all` scope reload-cache 觸發 InvalidateCache（議題 2 路線 A — production rollback 5 分鐘內生效）
///
/// 議題 4 拍板（內容不動）：本 service 只做「搬家工程」— 取 PromptBody 給 caller，不對 content 做任何 transform。
/// </summary>
public class PromptResolver(
    IServiceScopeFactory scopeFactory,
    WorkflowSettingsResolver workflowResolver,
    ILogger<PromptResolver> logger)
{
    private const int CacheTtlMinutes = 5;

    private Dictionary<string, string> _skillCache = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<Guid, string> _talentCache = [];
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// 取指定 capability 對應 SkillPrompt PromptBody。
    /// flag=false → null（caller 退 file fallback）/ flag=true & cache miss → null（SkillName 未 seed）。
    /// </summary>
    public async Task<string?> ResolveCapabilityPromptAsync(string capability, CancellationToken ct = default)
    {
        if (!await workflowResolver.GetUseV5PromptDbAsync(ct)) return null;
        await EnsureCacheAsync(ct);
        return _skillCache.TryGetValue(capability, out var body) ? body : null;
    }

    /// <summary>取 Petra Orchestrator base template（flag=true → DB SkillPrompt `petra_orchestration` PromptBody / flag=false → null）。</summary>
    public Task<string?> ResolvePetraBaseTemplateAsync(CancellationToken ct = default)
        => ResolveCapabilityPromptAsync("petra_orchestration", ct);

    /// <summary>取 Talent persona overlay（flag=true 且 TalentPrompt 存在 → PersonaBody / 其他 → null）。</summary>
    public async Task<string?> ResolveTalentPersonaAsync(Guid talentId, CancellationToken ct = default)
    {
        if (!await workflowResolver.GetUseV5PromptDbAsync(ct)) return null;
        await EnsureCacheAsync(ct);
        return _talentCache.TryGetValue(talentId, out var body) ? body : null;
    }

    /// <summary>強制清除 cache — 下次讀取時重新從 DB 載入（InternalController reload-cache `all` scope 觸發）。</summary>
    public void InvalidateCache() => _cacheExpiry = DateTime.MinValue;

    private async Task EnsureCacheAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow < _cacheExpiry) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (DateTime.UtcNow < _cacheExpiry) return;

            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<PromptRepository>();

            var skills = await repo.ListAllActiveSkillPromptsAsync(ct);
            var talents = await repo.ListAllActiveTalentPromptsAsync(ct);

            _skillCache  = skills.ToDictionary(s => s.SkillName, s => s.PromptBody, StringComparer.OrdinalIgnoreCase);
            _talentCache = talents.ToDictionary(t => t.TalentId, t => t.PersonaBody);

            _cacheExpiry = DateTime.UtcNow.AddMinutes(CacheTtlMinutes);
            logger.LogInformation("PromptResolver cache reloaded: {SkillCount} skills / {TalentCount} talents", skills.Count, talents.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PromptResolver cache reload 失敗，沿用上次 cache");
        }
        finally
        {
            _lock.Release();
        }
    }
}
