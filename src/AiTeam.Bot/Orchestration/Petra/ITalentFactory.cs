using AiTeam.Bot.Agents;
using AiTeam.Bot.Services;
using AiTeam.Data;
using Microsoft.EntityFrameworkCore;
// AiTeam.Bot.Services.PromptResolver — already covered by `using AiTeam.Bot.Services`

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 67：v5.5 Phase 1 Step 2 — Talent runtime factory（取代 plan 早期 `IEnumerable&lt;ITalent&gt;` DI scan pattern）。
///
/// 設計理由（Forge 實作時 spike）：
/// - DI service collection register 必須在 `app.Build()` 之前完成 — 但 DB migrate / DbSeeder.SeedAsync 在 `app.Build()` 之後跑
/// - 矛盾：要 from DB load Talent 來 register ITalent，但 DB 還沒 ready → 改用 runtime factory pattern
/// - 副作用優勢：Phase 3 dynamic CRUD 自然解（runtime 加 Talent 立刻 pickup / 不需 register hot reload）
///
/// 使用：PetraOrchestratorService ctor 注入 ITalentFactory + StartAsync 內呼叫 `await GetAllAsync(ct)` 取最新 Talent list。
/// </summary>
public interface ITalentFactory
{
    /// <summary>Stage 67：runtime query DB 取所有 active Talent + skill assignment count &gt; 0（Victoria / Petra 0 skill 排除 — orchestrator role）→ 建 GenericAgentTool list。</summary>
    Task<IReadOnlyList<ITalent>> GetAllAsync(CancellationToken ct = default);
}

/// <summary>
/// Stage 67：預設 Talent factory 實作（DI Singleton — 用 IServiceScopeFactory 解 Singleton/Scoped 雷）。
/// 每次 GetAllAsync 開新 scope query DB — 簡單實作（無 cache / Phase 3 加 TTL cache 評估）。
/// Stage 72：注入 PromptResolver 傳給 GenericAgentTool — adapter dispatch 時走 DB SkillPrompt path（feature flag=true 場景）。
/// </summary>
internal sealed class DefaultTalentFactory(
    IServiceScopeFactory scopeFactory,
    IClaudeCodeService claudeCode,
    TokenLogService tokenLogService,
    PromptResolver promptResolver,
    ILoggerFactory loggerFactory) : ITalentFactory
{
    public async Task<IReadOnlyList<ITalent>> GetAllAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var talents = await db.Talents
            .Include(t => t.Skills)
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        // baseline 6 Talent 中 Victoria / Petra 0 skill orchestrator role 排除 — Petra dispatch 只看 Skill 擔任者
        var workerTalents = talents.Where(t => t.Skills.Count > 0).ToList();

        return workerTalents
            .Select(ITalent (t) => new GenericAgentTool(t, claudeCode, tokenLogService, loggerFactory, promptResolver))
            .ToList();
    }
}
