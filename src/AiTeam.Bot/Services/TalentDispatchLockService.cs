using System.Collections.Concurrent;

namespace AiTeam.Bot.Services;

/// <summary>
/// Stage 75：v5.5 Phase 3 — per-Talent serialization lock（Singleton + ConcurrentDictionary&lt;Guid, SemaphoreSlim&gt;）。
///
/// 紀律：
/// - 同 talent_id 多 task dispatch 序列化（同時間 1 task / Cody 跑完才接下個）
/// - 不同 talent_id 平行 OK（對齊 v5.5 horizontal scaling 未來 Cody-1 + Cody-2 平行設計）
/// - 對齊 v4 既有 <see cref="AiTeam.Bot.Orchestration.AgentQueueProcessor"/> SemaphoreSlim(1,1) per executor key pattern production-active 紀律
/// - 單 Bot instance 場景足夠（業界 2026 reference architecture for single-instance async concurrency）
/// - 議題 2 SemaphoreSlim 路線（Christ 拍板 2026-05-17 vs Advisory Lock）— WebSearch 對齊 + 0 PG connection pool 雷
///
/// W6 trade-off 紀律（Aria gate1）：SemaphoreSlim cleanup — talent 數量有限（baseline 6 / horizontal scaling 場景 &lt; 100）/ 不擋 plan / 未來 100+ Talent 才需評估。
///
/// 使用 IDisposable AcquireAsync(talentId, ct) → using 自動 release（caller 0 finally 顯式 release）。
/// </summary>
public class TalentDispatchLockService
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    /// <summary>取 talent 鎖 — 同 talent_id 序列化等待 / 不同 talent_id 平行 OK。回 IDisposable using 自動 release。</summary>
    public async Task<IDisposable> AcquireAsync(Guid talentId, CancellationToken ct = default)
    {
        var sem = _locks.GetOrAdd(talentId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        return new Releaser(sem);
    }

    /// <summary>診斷用：取當前 lock count（test / log 用 — 不影響 production logic）。</summary>
    public int LockCount => _locks.Count;

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}
