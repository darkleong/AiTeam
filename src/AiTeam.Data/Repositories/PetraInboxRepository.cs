using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data.Repositories;

/// <summary>
/// Stage 75：v5.5 Phase 3 — PetraInbox 資料存取（對齊 PetraSessionRepository pattern — caller SaveChangesAsync）。
///
/// W2 trade-off 紀律（Aria gate1）：TryMarkRunningAsync 用「先 read 再 UPDATE」非真正 atomic — 單 Bot instance OK；
/// 未來多 instance 才踩 race / 對齊 AiTeam 單 Bot 真實架構約束。
/// </summary>
public class PetraInboxRepository(AppDbContext db)
{
    /// <summary>建新 PetraInbox row（pending 狀態 — caller SaveChangesAsync 後 Id 產生）。</summary>
    public PetraInbox Enqueue(string userInput, string source)
    {
        var row = new PetraInbox
        {
            UserInput = userInput,
            Source = source,
            Status = "pending",
            EnqueuedAt = DateTime.UtcNow,
        };
        db.PetraInbox.Add(row);
        return row;
    }

    /// <summary>取最早 pending row（FIFO / Status='pending' ORDER BY EnqueuedAt ASC limit 1）。</summary>
    public Task<PetraInbox?> GetNextPendingAsync(CancellationToken ct = default)
        => db.PetraInbox
            .Where(x => x.Status == "pending")
            .OrderBy(x => x.EnqueuedAt)
            .FirstOrDefaultAsync(ct);

    /// <summary>同 Source 內 pending count（queue position 計算）。</summary>
    public Task<int> CountPendingBySourceAsync(string source, CancellationToken ct = default)
        => db.PetraInbox.CountAsync(x => x.Status == "pending" && x.Source == source, ct);

    /// <summary>切 running + StartedAt — W2 trade-off：「先 read 再 UPDATE」非 atomic（單 Bot OK / 多 instance 才踩）。</summary>
    public async Task<bool> TryMarkRunningAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.PetraInbox.FirstOrDefaultAsync(x => x.Id == id && x.Status == "pending", ct);
        if (row is null) return false;
        row.Status = "running";
        row.StartedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>切 completed + CompletedAt + PetraSessionId 寫回。</summary>
    public async Task MarkCompletedAsync(Guid id, Guid? sessionId, CancellationToken ct = default)
    {
        var row = await db.PetraInbox.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return;
        row.Status = "completed";
        row.PetraSessionId = sessionId;
        row.CompletedAt = DateTime.UtcNow;
    }

    /// <summary>切 failed + ErrorMessage + CompletedAt。</summary>
    public async Task MarkFailedAsync(Guid id, string errorMessage, CancellationToken ct = default)
    {
        var row = await db.PetraInbox.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return;
        row.Status = "failed";
        row.ErrorMessage = errorMessage;
        row.CompletedAt = DateTime.UtcNow;
    }

    /// <summary>啟動 Crash Recovery — Bot 重啟時把 Status='running' 重設 pending（對齊 AgentQueueProcessor.RecoverStuckTasksAsync 紀律）。</summary>
    public async Task<int> RecoverStuckRunningAsync(CancellationToken ct = default)
    {
        var stuck = await db.PetraInbox.Where(x => x.Status == "running").ToListAsync(ct);
        foreach (var r in stuck) r.Status = "pending";
        return stuck.Count;
    }

    /// <summary>取最近 N 筆（Dashboard UX status 顯示用 — 對齊 InteractionCenter SignalR 即時拉取 pattern）。</summary>
    public Task<List<PetraInbox>> GetRecentAsync(int limit, CancellationToken ct = default)
        => db.PetraInbox
            .OrderByDescending(x => x.EnqueuedAt)
            .Take(limit)
            .ToListAsync(ct);
}
