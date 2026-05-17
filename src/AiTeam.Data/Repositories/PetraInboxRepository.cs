using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data.Repositories;

/// <summary>
/// Stage 75：v5.5 Phase 3 — PetraInbox 資料存取（對齊 PetraSessionRepository pattern — caller SaveChangesAsync）。
///
/// W2 trade-off 紀律（Aria gate1）：TryMarkRunningAsync 用「先 read 再 UPDATE」非真正 atomic — 單 Bot instance OK；
/// 未來多 instance 才踩 race / 對齊 AiTeam 單 Bot 真實架構約束。
///
/// Stage 76：v5.5 Phase 3 補強 — retry / resume 機制延伸 4 新 method（MarkPendingWithRetryAsync / MarkDeadAsync / RequeueAsync / GetDeadLetterAsync）+
/// GetNextPendingAsync 加 NextRetryAt 守 backoff timing 條件。
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

    /// <summary>
    /// 取最早 pending row（FIFO / Status='pending' + NextRetryAt 滿足）+ Stage 76 守 retry backoff timing。
    /// 條件：NextRetryAt IS NULL（首次 / 未進 retry path）OR NextRetryAt &lt;= NOW（backoff 已過）。
    /// </summary>
    public Task<PetraInbox?> GetNextPendingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return db.PetraInbox
            .Where(x => x.Status == "pending" && (x.NextRetryAt == null || x.NextRetryAt <= now))
            .OrderBy(x => x.EnqueuedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// 取指定 Source 的 pending row count。
    /// ⚠️ Stage 76 後 CeoAgentService 已不再使用（queuePosition 簡化顯示路線拍板）— 保留 method 給未來 Dashboard metrics / queue depth monitoring 用。
    /// </summary>
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

    /// <summary>
    /// Stage 76：transient error retry path — set AttemptCount + NextRetryAt + Status='pending' + reset 時間欄。
    /// caller 負責算 newAttemptCount 並傳入（method 內直接 set 不 ++ — Aria 議題 1 紀律 / 0 雙處 +1 耦合）。
    /// </summary>
    public async Task MarkPendingWithRetryAsync(
        Guid id, int newAttemptCount, string errorMessage,
        DateTime nextRetryAt, CancellationToken ct = default)
    {
        var row = await db.PetraInbox.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return;
        row.Status        = "pending";
        row.AttemptCount  = newAttemptCount;   // 直接 set / 不 ++ — Aria 議題 1 紀律
        row.NextRetryAt   = nextRetryAt;
        row.ErrorMessage  = errorMessage;       // 寫累積 error message 給 monitoring
        row.StartedAt     = null;               // reset 守 fresh dispatch（Crash Recovery 紀律一致）
        row.CompletedAt   = null;
    }

    /// <summary>Stage 76：Dead Letter — exhausted attempts 後標 Status='dead' + DeadAt + 不再 pickup（等人工介入）。</summary>
    public async Task MarkDeadAsync(Guid id, string errorMessage, CancellationToken ct = default)
    {
        var row = await db.PetraInbox.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return;
        row.Status = "dead";
        row.ErrorMessage = errorMessage;
        row.CompletedAt = DateTime.UtcNow;
        row.DeadAt = DateTime.UtcNow;
    }

    /// <summary>Stage 76：Dashboard 重跑 failed/dead row — reset 5 timestamp/error 欄 + AttemptCount=0 + Status='pending'。回 false 若 row 不存在或 status 非 failed/dead。</summary>
    public async Task<bool> RequeueAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.PetraInbox.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return false;
        if (row.Status != "failed" && row.Status != "dead") return false;   // 只允許 failed/dead 重跑（守業務正確性）
        row.Status        = "pending";
        row.AttemptCount  = 0;
        row.NextRetryAt   = null;
        row.StartedAt     = null;
        row.CompletedAt   = null;
        row.DeadAt        = null;
        row.ErrorMessage  = null;
        return true;
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

    /// <summary>Stage 76：Dashboard UX — 取最近 N 筆 dead row（人工介入 candidate）。</summary>
    public Task<List<PetraInbox>> GetDeadLetterAsync(int limit, CancellationToken ct = default)
        => db.PetraInbox
            .Where(x => x.Status == "dead")
            .OrderByDescending(x => x.DeadAt)
            .Take(limit)
            .ToListAsync(ct);
}
