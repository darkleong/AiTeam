using System.Reflection;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Data.SeedContent;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AiTeam.Bot.Tests.Orchestration;

/// <summary>
/// Stage 73：v5.5 Phase 3 Step 7 — Prompt content 升級 + Petra TalentPrompt persona seed 單元驗。
///
/// 覆蓋驗收場景：
/// - 場景 A：UpgradeSkillPromptsToV2Async 從 v1 baseline 升 v2（petra_orchestration INLINE path / .md fallback path 在 .md 不存在時 skip 不阻擋）
/// - 場景 B：UpgradeSkillPromptsToV2Async 幂等（v2 active 已存在 → skip）
/// - 場景 C：EnsurePetraTalentPromptAsync seed Petra persona + 4 拍板特質關鍵字驗
/// - 場景 D/E（BuildPetraSystemPromptForRuntimeAsync 真實組合）：留 Trial_v19 真實業務 + Aria gate2 範疇（PromptResolver 構造需 IServiceScopeFactory 過重）— 此處改驗 content 結構正確性
///
/// 紀律對齊：xUnit + InMemory DB（對齊 PromptRepositoryTests / PetraOrchestratorServiceTests 既有 pattern）。
/// </summary>
public class Stage73UpgradeTests
{
    private static AppDbContext CreateInMemoryDb(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    // ─── T1：場景 A — UpgradeSkillPromptsToV2Async 從 v1 baseline 升 v2（petra_orchestration INLINE path）─
    [Fact]
    public async Task T1_UpgradeSkillPromptsToV2Async_FromV1Baseline_UpgradesPetraOrchestration()
    {
        await using var db = CreateInMemoryDb(nameof(T1_UpgradeSkillPromptsToV2Async_FromV1Baseline_UpgradesPetraOrchestration));
        await db.Database.EnsureCreatedAsync();

        // 模擬 Stage 72 EnsureSkillPromptsAsync 已 seed v1 baseline（petra_orchestration 用早期版本字串模擬「pre-Stage73」）
        db.SkillPrompts.Add(new SkillPrompt
        {
            SkillName     = "petra_orchestration",
            PromptBody    = "v1 baseline body (pre-Stage 73)",
            VersionNumber = 1,
            IsActive      = true,
            CreatedByUser = null,
        });
        await db.SaveChangesAsync();

        // reflection invoke private static UpgradeSkillPromptsToV2Async（對齊 PetraOrchestratorServiceTests Test9 反射 pattern）
        await InvokeUpgradeSkillPromptsToV2Async(db);

        // 驗 petra_orchestration 從 v1 升 v2 + 舊 v1 切 IsActive=false（audit trail 保留）
        var all = await db.SkillPrompts
            .Where(s => s.SkillName == "petra_orchestration")
            .OrderBy(s => s.VersionNumber)
            .ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(1, all[0].VersionNumber);
        Assert.False(all[0].IsActive);
        Assert.Equal(2, all[1].VersionNumber);
        Assert.True(all[1].IsActive);
        // v2 body = PetraPromptTemplate.Template（INLINE path / Stage 73 升級後 content）
        Assert.Equal(PetraPromptTemplate.Template, all[1].PromptBody);
        Assert.Equal("stage73-upgrade", all[1].CreatedByUser);
    }

    // ─── T2：場景 B — UpgradeSkillPromptsToV2Async 幂等（v2 active 已存在 → skip）─
    [Fact]
    public async Task T2_UpgradeSkillPromptsToV2Async_Idempotent_SkipsExistingV2()
    {
        await using var db = CreateInMemoryDb(nameof(T2_UpgradeSkillPromptsToV2Async_Idempotent_SkipsExistingV2));
        await db.Database.EnsureCreatedAsync();

        // 模擬 v2 active 已存在（Stage 73 已跑過一次）
        db.SkillPrompts.Add(new SkillPrompt
        {
            SkillName     = "petra_orchestration",
            PromptBody    = "v1 baseline",
            VersionNumber = 1,
            IsActive      = false,
            CreatedByUser = null,
        });
        db.SkillPrompts.Add(new SkillPrompt
        {
            SkillName     = "petra_orchestration",
            PromptBody    = PetraPromptTemplate.Template,
            VersionNumber = 2,
            IsActive      = true,
            CreatedByUser = "stage73-upgrade",
        });
        await db.SaveChangesAsync();

        // 再跑一次 UpgradeSkillPromptsToV2Async → 應該 skip（幂等）
        await InvokeUpgradeSkillPromptsToV2Async(db);

        // 驗 row count 沒增加（仍 2 row / 0 v3 row 累積）
        var all = await db.SkillPrompts
            .Where(s => s.SkillName == "petra_orchestration")
            .ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(2, all.Max(s => s.VersionNumber));   // max 仍 v2
    }

    // ─── T3：場景 C — EnsurePetraTalentPromptAsync seed Petra persona ──────────
    [Fact]
    public async Task T3_EnsurePetraTalentPromptAsync_SeedsPersonaWithFourKeywords()
    {
        await using var db = CreateInMemoryDb(nameof(T3_EnsurePetraTalentPromptAsync_SeedsPersonaWithFourKeywords));
        await db.Database.EnsureCreatedAsync();

        // 模擬 EnsureTalentsAsync 已 seed Petra Talent
        var petra = new Talent
        {
            Name        = "Petra",
            DisplayName = "Petra",
            Description = "Orchestrator",
            ProjectId   = null,
            IsActive    = true,
        };
        db.Talents.Add(petra);
        await db.SaveChangesAsync();

        // reflection invoke private static EnsurePetraTalentPromptAsync
        await InvokeEnsurePetraTalentPromptAsync(db);

        // 驗 TalentPrompt row 寫入
        var tp = await db.TalentPrompts
            .FirstOrDefaultAsync(t => t.TalentId == petra.Id && t.IsActive);
        Assert.NotNull(tp);
        Assert.Equal(1, tp!.VersionNumber);
        Assert.True(tp.IsActive);
        Assert.Equal(PetraPersonaSeed.PersonaBody, tp.PersonaBody);

        // 4 拍板特質關鍵字驗（PersonaBody content correctness — 場景 C 驗證點）
        Assert.Contains("謹慎", tp.PersonaBody);
        Assert.Contains("冗餘", tp.PersonaBody);
        Assert.Contains("持續", tp.PersonaBody);
        Assert.Contains("對等", tp.PersonaBody);
    }

    // ─── T4：場景 C 補強 — EnsurePetraTalentPromptAsync 幂等（active TalentPrompt 已存在 → skip）─
    [Fact]
    public async Task T4_EnsurePetraTalentPromptAsync_Idempotent_SkipsWhenActiveExists()
    {
        await using var db = CreateInMemoryDb(nameof(T4_EnsurePetraTalentPromptAsync_Idempotent_SkipsWhenActiveExists));
        await db.Database.EnsureCreatedAsync();

        var petra = new Talent
        {
            Name        = "Petra",
            DisplayName = "Petra",
            Description = "Orchestrator",
            ProjectId   = null,
            IsActive    = true,
        };
        db.Talents.Add(petra);
        await db.SaveChangesAsync();

        // 預先 seed 1 active TalentPrompt（模擬 Stage 73 已跑過）
        db.TalentPrompts.Add(new TalentPrompt
        {
            TalentId      = petra.Id,
            PersonaBody   = "existing persona",
            VersionNumber = 1,
            IsActive      = true,
        });
        await db.SaveChangesAsync();

        // 再跑 EnsurePetraTalentPromptAsync → skip
        await InvokeEnsurePetraTalentPromptAsync(db);

        // 驗 row count 沒增加 / PersonaBody 不被覆蓋
        var all = await db.TalentPrompts
            .Where(t => t.TalentId == petra.Id)
            .ToListAsync();
        Assert.Single(all);
        Assert.Equal("existing persona", all[0].PersonaBody);
    }

    // ─── T5：PetraPromptTemplate.Template Stage 73 升級 content 結構驗（場景 D content 對齊）─
    [Fact]
    public void T5_PetraPromptTemplate_Template_ContainsStage73UpgradedContent()
    {
        var template = PetraPromptTemplate.Template;

        // Stage 73 升級重點關鍵字
        Assert.Contains("v5.5 動態架構 Multi-Agent Orchestrator", template);   // 開頭 v5 → v5.5
        Assert.Contains("派工夥伴", template);                                   // 對等和互相精神
        Assert.Contains("品質目標", template);                                   // 新加段
        // 既有界面契約保留（Test9/47 assertion 對齊）
        Assert.Contains("1-on-1 trigger", template);
        Assert.Contains("Design trigger", template);
        Assert.Contains("Kickoff trigger", template);
        // 3 placeholder 保留（runtime 注入機制）
        Assert.Contains("{{capabilityRoster}}", template);
        Assert.Contains("{{decompositionSection}}", template);
        Assert.Contains("{{outputSection}}", template);
    }

    // ─── 反射 helper ─────────────────────────────────────────────────────────
    private static async Task InvokeUpgradeSkillPromptsToV2Async(AppDbContext db)
    {
        var method = typeof(DbSeeder).GetMethod(
            "UpgradeSkillPromptsToV2Async",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var task = (Task)method!.Invoke(null, new object[] { db })!;
        await task;
    }

    private static async Task InvokeEnsurePetraTalentPromptAsync(AppDbContext db)
    {
        var method = typeof(DbSeeder).GetMethod(
            "EnsurePetraTalentPromptAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var task = (Task)method!.Invoke(null, new object[] { db })!;
        await task;
    }
}
