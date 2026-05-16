using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AiTeam.Bot.Tests.Orchestration;

/// <summary>
/// Stage 72：v5.5 Phase 2 Step 5 — PromptRepository CRUD + versioning + rollback 單元驗。
///
/// 紀律對齊：xUnit + InMemory DB（對齊 PetraOrchestratorServiceTests 既有 pattern / 不真打 PostgreSQL）。
/// partial unique index 真實 enforced 驗 — InMemory provider 不支援 partial filter，留 production Migration apply 後 manual 驗（場景 A）。
/// </summary>
public class PromptRepositoryTests
{
    private static AppDbContext CreateInMemoryDb(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    // ─── T1：Upsert 累積新版本 + 舊 active 切 false ─────────────────────────
    [Fact]
    public async Task T1_UpsertSkillPromptAsync_NewVersion_FlipsOldActive()
    {
        await using var db = CreateInMemoryDb(nameof(T1_UpsertSkillPromptAsync_NewVersion_FlipsOldActive));
        await db.Database.EnsureCreatedAsync();
        var repo = new PromptRepository(db);

        // baseline v1（模擬 DbSeeder seed）
        await repo.UpsertSkillPromptAsync("code_implementation", "v1 body", createdByUser: null);
        await db.SaveChangesAsync();

        // Upsert v2
        var v2 = await repo.UpsertSkillPromptAsync("code_implementation", "v2 body", createdByUser: "christ");
        await db.SaveChangesAsync();

        // 驗 v1 切 inactive / v2 active
        var all = await db.SkillPrompts
            .Where(s => s.SkillName == "code_implementation")
            .OrderBy(s => s.VersionNumber)
            .ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(1, all[0].VersionNumber);
        Assert.False(all[0].IsActive);
        Assert.Equal(2, all[1].VersionNumber);
        Assert.True(all[1].IsActive);
        Assert.Equal("v2 body", all[1].PromptBody);
        Assert.Equal("christ", all[1].CreatedByUser);
        Assert.Equal(v2.Id, all[1].Id);

        // GetActiveSkillPromptAsync 取最新 active
        var active = await repo.GetActiveSkillPromptAsync("code_implementation");
        Assert.NotNull(active);
        Assert.Equal(2, active!.VersionNumber);
        Assert.Equal("v2 body", active.PromptBody);
    }

    // ─── T2：Rollback 切換 active 到指定版本 + 累積 row 不刪（audit trail 保留）─
    [Fact]
    public async Task T2_RollbackSkillPromptAsync_SwitchesActiveAndKeepsHistory()
    {
        await using var db = CreateInMemoryDb(nameof(T2_RollbackSkillPromptAsync_SwitchesActiveAndKeepsHistory));
        await db.Database.EnsureCreatedAsync();
        var repo = new PromptRepository(db);

        // 累積 3 版本
        await repo.UpsertSkillPromptAsync("petra_orchestration", "v1 body", null);
        await db.SaveChangesAsync();
        await repo.UpsertSkillPromptAsync("petra_orchestration", "v2 body", null);
        await db.SaveChangesAsync();
        await repo.UpsertSkillPromptAsync("petra_orchestration", "v3 body", null);
        await db.SaveChangesAsync();

        // rollback 到 v1
        var rolled = await repo.RollbackSkillPromptAsync("petra_orchestration", targetVersion: 1);
        await db.SaveChangesAsync();

        Assert.NotNull(rolled);
        Assert.Equal(1, rolled!.VersionNumber);
        Assert.True(rolled.IsActive);

        var all = await db.SkillPrompts
            .Where(s => s.SkillName == "petra_orchestration")
            .OrderBy(s => s.VersionNumber)
            .ToListAsync();
        Assert.Equal(3, all.Count);   // 3 版本不刪（audit trail）
        Assert.True(all[0].IsActive);    // v1 = active 後
        Assert.False(all[1].IsActive);
        Assert.False(all[2].IsActive);

        // rollback 不存在的 version → 回 null + 不動 active 狀態
        var notFound = await repo.RollbackSkillPromptAsync("petra_orchestration", targetVersion: 99);
        Assert.Null(notFound);
        // v1 仍 active
        var active = await repo.GetActiveSkillPromptAsync("petra_orchestration");
        Assert.Equal(1, active!.VersionNumber);
    }

    // ─── T3：ListSkillPromptVersionsAsync 返回所有版本（asc by version_number）─
    [Fact]
    public async Task T3_ListSkillPromptVersionsAsync_ReturnsAllOrderedByVersion()
    {
        await using var db = CreateInMemoryDb(nameof(T3_ListSkillPromptVersionsAsync_ReturnsAllOrderedByVersion));
        await db.Database.EnsureCreatedAsync();
        var repo = new PromptRepository(db);

        await repo.UpsertSkillPromptAsync("code_review", "v1", null);
        await db.SaveChangesAsync();
        await repo.UpsertSkillPromptAsync("code_review", "v2", null);
        await db.SaveChangesAsync();
        await repo.UpsertSkillPromptAsync("code_review", "v3", null);
        await db.SaveChangesAsync();

        var versions = await repo.ListSkillPromptVersionsAsync("code_review");
        Assert.Equal(3, versions.Count);
        Assert.Equal(1, versions[0].VersionNumber);
        Assert.Equal(2, versions[1].VersionNumber);
        Assert.Equal(3, versions[2].VersionNumber);
        Assert.Equal("v1", versions[0].PromptBody);
        Assert.Equal("v3", versions[2].PromptBody);

        // 其他 SkillName 不混入
        var other = await repo.ListSkillPromptVersionsAsync("non_existent");
        Assert.Empty(other);
    }

    // ─── T4：TalentPrompt Upsert + GetActive nullable（Phase 3 才補 persona）─
    [Fact]
    public async Task T4_UpsertTalentPromptAsync_NullableActiveResolution()
    {
        await using var db = CreateInMemoryDb(nameof(T4_UpsertTalentPromptAsync_NullableActiveResolution));
        await db.Database.EnsureCreatedAsync();
        var repo = new PromptRepository(db);

        var talentId = Guid.NewGuid();

        // 未 seed → GetActive 回 null（Phase 3 baseline 場景）
        var noneYet = await repo.GetActiveTalentPromptAsync(talentId);
        Assert.Null(noneYet);

        // Upsert v1
        await repo.UpsertTalentPromptAsync(talentId, "Cody persona v1");
        await db.SaveChangesAsync();

        var active = await repo.GetActiveTalentPromptAsync(talentId);
        Assert.NotNull(active);
        Assert.Equal(1, active!.VersionNumber);
        Assert.Equal("Cody persona v1", active.PersonaBody);

        // Upsert v2 — v1 切 inactive
        await repo.UpsertTalentPromptAsync(talentId, "Cody persona v2");
        await db.SaveChangesAsync();

        var all = await db.TalentPrompts
            .Where(t => t.TalentId == talentId)
            .OrderBy(t => t.VersionNumber)
            .ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.False(all[0].IsActive);
        Assert.True(all[1].IsActive);
        Assert.Equal("Cody persona v2", all[1].PersonaBody);
    }
}
