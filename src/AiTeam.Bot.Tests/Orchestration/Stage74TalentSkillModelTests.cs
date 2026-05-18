using AiTeam.Bot.Orchestration.Petra;
using AiTeam.Bot.Orchestration.Petra.Skills;
using AiTeam.Bot.Services;
using AiTeam.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AiTeam.Bot.Tests.Orchestration;

/// <summary>
/// Stage 74：v5.5 Phase 3 Step 8 — per-Skill Model 三層 fallback chain + DAG fan-out level grouping + SkillDescriptor metadata 單元驗。
///
/// 覆蓋驗收場景：
/// - 場景 B：TalentSkillModelResolver 三層 fallback chain（T1-T4）
/// - 場景 D：DAG fan-out 同 level 並行（T6）
/// - 場景 E：線性 chain 0 regression（T5）
/// - 場景 F：SkillDescriptor metadata 擴展（T7）
///
/// 紀律對齊：xUnit + InMemory DB（對齊 PromptRepositoryTests / Stage73UpgradeTests 既有 pattern）。
/// </summary>
public class Stage74TalentSkillModelTests
{
    private static AppDbContext CreateInMemoryDb(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static (TalentSkillModelResolver Resolver, AppDbContext Db, Talent Petra) CreateResolverWithSeed(
        string dbName,
        string? talentModel,
        string? talentSkillModel,
        IDictionary<string, string?>? configValues = null)
    {
        var db = CreateInMemoryDb(dbName);
        db.Database.EnsureCreated();

        // seed Talent + TalentSkill
        var talent = new Talent
        {
            Name        = "Cody",
            DisplayName = "Cody",
            Description = "code_implementation 主",
            ProjectId   = null,
            Provider    = talentModel is null ? null : "Anthropic",
            Model       = talentModel,
            IsActive    = true,
        };
        db.Talents.Add(talent);
        db.SaveChanges();

        var ts = new TalentSkill
        {
            TalentId  = talent.Id,
            SkillName = "code_implementation",
            IsPrimary = true,
            Priority  = 0,
            Provider  = talentSkillModel is null ? null : "Anthropic",
            Model     = talentSkillModel,
        };
        db.TalentSkills.Add(ts);
        db.SaveChanges();

        // build configuration in-memory（場景 B runtime fallback default）
        var configDict = new Dictionary<string, string?>
        {
            ["Agents:Dev:Model"]    = "claude-sonnet-runtime-default",
            ["Agents:Dev:Provider"] = "Anthropic",
        };
        if (configValues is not null)
        {
            foreach (var (k, v) in configValues) configDict[k] = v;
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

        // build IServiceScopeFactory pointing to the same InMemory DB
        var services = new ServiceCollection();
        services.AddSingleton<AppDbContext>(db);
        services.AddSingleton<IConfiguration>(configuration);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var resolver = new TalentSkillModelResolver(
            scopeFactory,
            configuration,
            NullLogger<TalentSkillModelResolver>.Instance);

        return (resolver, db, talent);
    }

    // ─── T1：場景 B per-Skill 優先 ─────────────────────────
    [Fact]
    public async Task T1_TalentSkillModelResolver_PerSkill_OverridesTalentDefault()
    {
        var (resolver, db, petra) = CreateResolverWithSeed(
            nameof(T1_TalentSkillModelResolver_PerSkill_OverridesTalentDefault),
            talentModel: "claude-sonnet-4-6",
            talentSkillModel: "claude-opus-4-7");

        var (_, model) = await resolver.ResolveAsync(petra.Id, "code_implementation");

        Assert.Equal("claude-opus-4-7", model);   // per-Skill 優先
        await db.DisposeAsync();
    }

    // ─── T2：場景 B per-Talent fallback（per-Skill = null）─────────────────────────
    [Fact]
    public async Task T2_TalentSkillModelResolver_PerTalent_FallbackWhenPerSkillNull()
    {
        var (resolver, db, petra) = CreateResolverWithSeed(
            nameof(T2_TalentSkillModelResolver_PerTalent_FallbackWhenPerSkillNull),
            talentModel: "claude-sonnet-4-6",
            talentSkillModel: null);

        var (_, model) = await resolver.ResolveAsync(petra.Id, "code_implementation");

        Assert.Equal("claude-sonnet-4-6", model);   // per-Talent fallback
        await db.DisposeAsync();
    }

    // ─── T3：場景 B runtime fallback（per-Skill + per-Talent 都 null）─────────────────────────
    [Fact]
    public async Task T3_TalentSkillModelResolver_RuntimeFallback_WhenBothNull()
    {
        var (resolver, db, petra) = CreateResolverWithSeed(
            nameof(T3_TalentSkillModelResolver_RuntimeFallback_WhenBothNull),
            talentModel: null,
            talentSkillModel: null);

        var (provider, model) = await resolver.ResolveAsync(petra.Id, "code_implementation");

        Assert.Equal("claude-sonnet-runtime-default", model);   // configuration["Agents:Dev:Model"] runtime fallback
        Assert.Equal("Anthropic", provider);                     // configuration["Agents:Dev:Provider"] runtime fallback
        await db.DisposeAsync();
    }

    // ─── T4：場景 B 補強 — InvalidateCache 刷新 ─────────────────────────
    [Fact]
    public async Task T4_TalentSkillModelResolver_InvalidateCache_RefreshesNextResolve()
    {
        var (resolver, db, petra) = CreateResolverWithSeed(
            nameof(T4_TalentSkillModelResolver_InvalidateCache_RefreshesNextResolve),
            talentModel: null,
            talentSkillModel: "claude-opus-initial");

        var (_, modelFirst) = await resolver.ResolveAsync(petra.Id, "code_implementation");
        Assert.Equal("claude-opus-initial", modelFirst);   // cache 第一次載入

        // 模擬 SQL UPDATE — InMemory DB 直接改 entity
        var ts = await db.TalentSkills.FirstAsync(s => s.TalentId == petra.Id);
        ts.Model = "claude-opus-updated";
        await db.SaveChangesAsync();

        // 不 InvalidateCache → 仍回 cached value
        var (_, modelStale) = await resolver.ResolveAsync(petra.Id, "code_implementation");
        Assert.Equal("claude-opus-initial", modelStale);

        // InvalidateCache → 下次 resolve 返新值
        resolver.InvalidateCache();
        var (_, modelFresh) = await resolver.ResolveAsync(petra.Id, "code_implementation");
        Assert.Equal("claude-opus-updated", modelFresh);

        await db.DisposeAsync();
    }

    // ─── T5：場景 E 線性 chain 0 regression ─────────────────────────
    [Fact]
    public void T5_SubtaskPlanLevelGrouping_LinearChain_AllLevels1Subtask()
    {
        var plan = SubtaskPlan.Linear(new[] { "code_implementation", "code_review", "qa_testing" });
        var levels = SubtaskPlanLevelGrouping.Group(plan);

        Assert.Equal(3, levels.Count);
        Assert.Single(levels[0]); Assert.Equal(1, levels[0][0]);
        Assert.Single(levels[1]); Assert.Equal(2, levels[1][0]);
        Assert.Single(levels[2]); Assert.Equal(3, levels[2][0]);
        // 每 level 1 subtask = caller 自然走 sequential = 0 regression
    }

    // ─── T6：場景 D DAG fan-out 同 level 並行 ─────────────────────────
    [Fact]
    public void T6_SubtaskPlanLevelGrouping_DAG_IndependentSubtasksSameLevel()
    {
        // subtask 2/3 都 dependsOn=[1] / subtask 4 dependsOn=[2,3]
        var plan = new SubtaskPlan(
            new[]
            {
                new Subtask(1, "code_implementation", ""),
                new Subtask(2, "code_review",         ""),
                new Subtask(3, "qa_testing",          ""),
                new Subtask(4, "documentation",       ""),
            },
            new[]
            {
                new DependencyEdge(1, 2, DependencyType.Sequential),
                new DependencyEdge(1, 3, DependencyType.Sequential),
                new DependencyEdge(2, 4, DependencyType.Sequential),
                new DependencyEdge(3, 4, DependencyType.Sequential),
            });

        var levels = SubtaskPlanLevelGrouping.Group(plan);

        Assert.Equal(3, levels.Count);
        Assert.Equal(new[] { 1 },    levels[0]);
        Assert.Equal(new[] { 2, 3 }, levels[1]);   // 同 level 並行
        Assert.Equal(new[] { 4 },    levels[2]);
    }

    // ─── T7：場景 F SkillDescriptor metadata 擴展 ─────────────────────────
    // Stage 78a：v4 path 砍後 Skill registry 縮為 4 Skill baseline（砍 ui_design + release_publishing）
    [Fact]
    public void T7_SkillDescriptor_NewMetadataFields_PopulatedOnAll4Skills()
    {
        var registry = new DefaultSkillRegistry();
        var validTiers = new HashSet<string> { "cost-efficient", "standard", "strategic" };

        Assert.Equal(4, registry.All.Count);

        foreach (var skill in registry.All)
        {
            Assert.Contains(skill.RecommendedModelTier, validTiers);
            Assert.False(string.IsNullOrWhiteSpace(skill.ReturnTypeDescription),
                $"skill={skill.Name} ReturnTypeDescription 必非空");
            // 既有 3 field 0 變動（backwards-compatible）
            Assert.False(string.IsNullOrWhiteSpace(skill.Name));
            Assert.False(string.IsNullOrWhiteSpace(skill.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(skill.Description));
        }

        // 對齊 Plan §E + 議題 2 Christ 拍板的 tier 分類
        // Stage 78a：砍 ui_design + release_publishing tier assert（對應 SkillRegistry 砍 / Rosa/Demi/Release class 整套砍）
        Assert.Equal("standard",       registry.GetByName("code_implementation")!.RecommendedModelTier);
        Assert.Equal("strategic",      registry.GetByName("code_review")!.RecommendedModelTier);
        Assert.Equal("standard",       registry.GetByName("qa_testing")!.RecommendedModelTier);
        Assert.Equal("cost-efficient", registry.GetByName("documentation")!.RecommendedModelTier);
    }
}
