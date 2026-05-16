using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data.Repositories;

/// <summary>
/// Stage 69（v2.1）：v5.5 Phase 2 Step 3 — 跨 session 長期持久記憶資料存取（per-PetraSession 共用 + per-Talent 私有 hybrid 雙層）。
///
/// Stage 69 v2.1 修根因（Aria 規劃漏掃 v5.5 path source of truth）：
/// - TaskMemory scope = **PetraSession**（不是 v4 TaskGroup）— 對齊 v5.5「每次 CEO 觸發 = 一個 PetraSession = 一個 Task event」設計精神
/// - v5.5 path 刻意不建 v4 workflow 容器（dynamic orchestrator 取代 hierarchical static — PetraOrchestratorService.cs:50 comment「spike forward path 無 TaskGroup」）
///
/// 設計紀律：
/// - Append 語意實作為 Upsert by Key（同 (session, key) 或 (talent, key, projectId) 已存在 → 更新 Content/UpdatedAt；不存在 → insert）。
///   schema 層由 unique partial index 保護（TaskMemory 直接 unique (PetraSessionId, Key) / TalentMemory 拆 NULL / NOT NULL 兩條 partial 對齊 Stage 67 紀律）。
///   想保留 history → caller 在 key 自加 round/timestamp 後綴。
/// - Compact：保留 newest N 條，delete 較舊的（CreatedAt 排序）。
/// - Caller 負責 SaveChangesAsync — 對齊 PetraSessionRepository / BossInteractionRepository pattern。
/// </summary>
public class MemoryRepository(AppDbContext db)
{
    // ─── Task layer（per-PetraSession 共用 — Petra dispatch 多 Talent 共看）─────────────

    /// <summary>取 PetraSession 全部記憶（時序排序 — 注入 prompt 用）。</summary>
    public Task<List<TaskMemory>> GetTaskMemoriesAsync(Guid petraSessionId, CancellationToken ct = default)
        => db.TaskMemories
            .Where(m => m.PetraSessionId == petraSessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

    /// <summary>取 PetraSession 當前記憶數（compact threshold 比對用）。</summary>
    public Task<int> CountTaskMemoriesAsync(Guid petraSessionId, CancellationToken ct = default)
        => db.TaskMemories.CountAsync(m => m.PetraSessionId == petraSessionId, ct);

    /// <summary>Upsert by Key — 同 (petraSessionId, key) 已存在 → 更新 Content/UpdatedAt；不存在 → insert（caller SaveChangesAsync）。</summary>
    public async Task<TaskMemory> UpsertTaskMemoryAsync(
        Guid petraSessionId,
        Guid? projectId,
        string key,
        string content,
        string createdByTalent,
        CancellationToken ct = default)
    {
        var existing = await db.TaskMemories
            .FirstOrDefaultAsync(m => m.PetraSessionId == petraSessionId && m.Key == key, ct);
        if (existing is not null)
        {
            existing.Content = content;
            existing.UpdatedAt = DateTime.UtcNow;
            // ProjectId 補正（caller 後續知道才標）— CreatedByTalent 保留原值（誰先寫就誰留名）
            if (projectId is not null && existing.ProjectId is null) existing.ProjectId = projectId;
            return existing;
        }

        var entity = new TaskMemory
        {
            PetraSessionId  = petraSessionId,
            ProjectId       = projectId,
            Key             = key,
            Content         = content,
            CreatedByTalent = createdByTalent,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow,
        };
        db.TaskMemories.Add(entity);
        return entity;
    }

    /// <summary>
    /// Compact：保留 newest <paramref name="keepCount"/> 條，delete 較舊的（CreatedAt asc 排序前 N-keepCount 條）。
    /// 回傳 delete count（caller SaveChangesAsync 後生效）。
    /// </summary>
    public async Task<int> CompactTaskMemoryAsync(Guid petraSessionId, int keepCount, CancellationToken ct = default)
    {
        if (keepCount < 0) throw new ArgumentOutOfRangeException(nameof(keepCount), "keepCount 不得為負");
        var all = await db.TaskMemories
            .Where(m => m.PetraSessionId == petraSessionId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
        if (all.Count <= keepCount) return 0;

        var toDelete = all.Skip(keepCount).ToList();
        db.TaskMemories.RemoveRange(toDelete);
        return toDelete.Count;
    }

    // ─── Talent layer（per-Talent 私有 — 個人記憶 / 跨 task 累積）─────────────────

    /// <summary>
    /// 取 Talent 記憶（projectId 比對 nullable — null 取全域 / 非 null 取該 project；tagFilter 非 null 時要求 Tags 含所有指定 tag）。
    /// </summary>
    public async Task<List<TalentMemory>> GetTalentMemoriesAsync(
        Guid talentId,
        Guid? projectId,
        string[]? tagFilter,
        CancellationToken ct = default)
    {
        var query = db.TalentMemories.Where(m => m.TalentId == talentId && m.ProjectId == projectId);
        if (tagFilter is { Length: > 0 })
        {
            // PostgreSQL text[] @> 子集合 — Npgsql 翻為 array contains（InMemory provider client-eval fallback）
            foreach (var tag in tagFilter)
            {
                var localTag = tag;
                query = query.Where(m => m.Tags.Contains(localTag));
            }
        }
        return await query.OrderBy(m => m.CreatedAt).ToListAsync(ct);
    }

    public Task<int> CountTalentMemoriesAsync(Guid talentId, Guid? projectId, CancellationToken ct = default)
        => db.TalentMemories.CountAsync(m => m.TalentId == talentId && m.ProjectId == projectId, ct);

    /// <summary>Upsert by (talentId, projectId, key) — caller SaveChangesAsync。</summary>
    public async Task<TalentMemory> UpsertTalentMemoryAsync(
        Guid talentId,
        Guid? projectId,
        string key,
        string content,
        IReadOnlyList<string>? tags,
        CancellationToken ct = default)
    {
        var existing = await db.TalentMemories
            .FirstOrDefaultAsync(m => m.TalentId == talentId && m.ProjectId == projectId && m.Key == key, ct);
        if (existing is not null)
        {
            existing.Content = content;
            existing.UpdatedAt = DateTime.UtcNow;
            if (tags is not null) existing.Tags = tags.ToList();
            return existing;
        }

        var entity = new TalentMemory
        {
            TalentId  = talentId,
            ProjectId = projectId,
            Key       = key,
            Content   = content,
            Tags      = tags?.ToList() ?? [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.TalentMemories.Add(entity);
        return entity;
    }

    /// <summary>Compact：保留 newest keepCount / delete 較舊。</summary>
    public async Task<int> CompactTalentMemoryAsync(
        Guid talentId,
        Guid? projectId,
        int keepCount,
        CancellationToken ct = default)
    {
        if (keepCount < 0) throw new ArgumentOutOfRangeException(nameof(keepCount), "keepCount 不得為負");
        var all = await db.TalentMemories
            .Where(m => m.TalentId == talentId && m.ProjectId == projectId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
        if (all.Count <= keepCount) return 0;

        var toDelete = all.Skip(keepCount).ToList();
        db.TalentMemories.RemoveRange(toDelete);
        return toDelete.Count;
    }
}
