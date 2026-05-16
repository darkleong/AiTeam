using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data.Repositories;

/// <summary>
/// Stage 72：v5.5 Phase 2 Step 5 — Prompt DB 化 / 兩層 schema CRUD + versioning + rollback。
///
/// 對齊業界 2026 prompt orchestration 主流：DB-backed + versioning（單表 + version_number + is_active flag）+ rollback 保護 production。
///
/// 設計紀律（對齊 MemoryRepository / PetraSessionRepository 既有 pattern）：
/// - Repository 不呼叫 SaveChangesAsync — caller 控制 transaction boundary（對齊 ef-core.md「SaveChangesAsync 原則」段）
/// - Upsert 語意 = 新版本 row + 舊 active 切 false（同 transaction 內，partial unique index 守一條 active）
/// - Rollback = 切換指定版本 row IsActive=true + 其他 active 切 false（不刪舊版本，留 audit trail）
/// </summary>
public class PromptRepository(AppDbContext db)
{
    // ─── 讀取（PromptResolver cache 批次 reload 用）───────────────────────────

    /// <summary>取指定 Skill 當前 active 版本（partial unique index 守只一條）。</summary>
    public Task<SkillPrompt?> GetActiveSkillPromptAsync(string skillName, CancellationToken ct = default)
        => db.SkillPrompts
            .FirstOrDefaultAsync(s => s.SkillName == skillName && s.IsActive, ct);

    /// <summary>取指定 Talent 當前 active persona（partial unique index 守只一條 / 可能為 null = Phase 3 才補）。</summary>
    public Task<TalentPrompt?> GetActiveTalentPromptAsync(Guid talentId, CancellationToken ct = default)
        => db.TalentPrompts
            .FirstOrDefaultAsync(t => t.TalentId == talentId && t.IsActive, ct);

    /// <summary>列出所有 active SkillPrompts（PromptResolver 批次 cache reload）。</summary>
    public Task<List<SkillPrompt>> ListAllActiveSkillPromptsAsync(CancellationToken ct = default)
        => db.SkillPrompts
            .AsNoTracking()
            .Where(s => s.IsActive)
            .ToListAsync(ct);

    /// <summary>列出所有 active TalentPrompts（PromptResolver 批次 cache reload）。</summary>
    public Task<List<TalentPrompt>> ListAllActiveTalentPromptsAsync(CancellationToken ct = default)
        => db.TalentPrompts
            .AsNoTracking()
            .Where(t => t.IsActive)
            .ToListAsync(ct);

    /// <summary>列出指定 Skill 所有版本（asc by version_number — Phase 3 WebUI 版本列表用）。</summary>
    public Task<List<SkillPrompt>> ListSkillPromptVersionsAsync(string skillName, CancellationToken ct = default)
        => db.SkillPrompts
            .Where(s => s.SkillName == skillName)
            .OrderBy(s => s.VersionNumber)
            .ToListAsync(ct);

    // ─── Upsert（新版本 row + 舊 active 切 false）─────────────────────────────

    /// <summary>
    /// Upsert SkillPrompt — 累積新版本：
    /// 1. 舊 active row（若有）IsActive=false
    /// 2. 新 row 插入 VersionNumber = max(version)+1 / IsActive=true
    /// caller SaveChangesAsync 後生效。
    /// </summary>
    public async Task<SkillPrompt> UpsertSkillPromptAsync(
        string skillName,
        string body,
        string? createdByUser,
        CancellationToken ct = default)
    {
        // 取舊 active 切 false（partial unique index 同 transaction 內由新 row 補位 — PostgreSQL 在 commit 階段檢查 unique）
        var oldActive = await db.SkillPrompts
            .FirstOrDefaultAsync(s => s.SkillName == skillName && s.IsActive, ct);
        if (oldActive is not null)
        {
            oldActive.IsActive = false;
            oldActive.UpdatedAt = DateTime.UtcNow;
        }

        var maxVersion = await db.SkillPrompts
            .Where(s => s.SkillName == skillName)
            .MaxAsync(s => (int?)s.VersionNumber, ct) ?? 0;

        var entity = new SkillPrompt
        {
            SkillName     = skillName,
            PromptBody    = body,
            VersionNumber = maxVersion + 1,
            IsActive      = true,
            CreatedByUser = createdByUser,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        };
        db.SkillPrompts.Add(entity);
        return entity;
    }

    /// <summary>Upsert TalentPrompt — 同 SkillPrompt Upsert 紀律（per-Talent / persona body）。</summary>
    public async Task<TalentPrompt> UpsertTalentPromptAsync(
        Guid talentId,
        string body,
        CancellationToken ct = default)
    {
        var oldActive = await db.TalentPrompts
            .FirstOrDefaultAsync(t => t.TalentId == talentId && t.IsActive, ct);
        if (oldActive is not null)
        {
            oldActive.IsActive = false;
            oldActive.UpdatedAt = DateTime.UtcNow;
        }

        var maxVersion = await db.TalentPrompts
            .Where(t => t.TalentId == talentId)
            .MaxAsync(t => (int?)t.VersionNumber, ct) ?? 0;

        var entity = new TalentPrompt
        {
            TalentId      = talentId,
            PersonaBody   = body,
            VersionNumber = maxVersion + 1,
            IsActive      = true,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        };
        db.TalentPrompts.Add(entity);
        return entity;
    }

    // ─── Rollback（切換 active 到指定版本 / 不刪舊 row）────────────────────────

    /// <summary>
    /// Rollback SkillPrompt — 切換指定 version_number 的 row 為 active：
    /// 1. 舊 active row 切 IsActive=false
    /// 2. target version row 切 IsActive=true
    /// 找不到 target version → 回 null（不動 active 狀態）。
    /// caller SaveChangesAsync 後生效。
    /// </summary>
    public async Task<SkillPrompt?> RollbackSkillPromptAsync(
        string skillName,
        int targetVersion,
        CancellationToken ct = default)
    {
        var target = await db.SkillPrompts
            .FirstOrDefaultAsync(s => s.SkillName == skillName && s.VersionNumber == targetVersion, ct);
        if (target is null) return null;
        if (target.IsActive) return target;   // 已是 active — 無動作

        var oldActive = await db.SkillPrompts
            .FirstOrDefaultAsync(s => s.SkillName == skillName && s.IsActive, ct);
        if (oldActive is not null)
        {
            oldActive.IsActive = false;
            oldActive.UpdatedAt = DateTime.UtcNow;
        }

        target.IsActive = true;
        target.UpdatedAt = DateTime.UtcNow;
        return target;
    }
}
