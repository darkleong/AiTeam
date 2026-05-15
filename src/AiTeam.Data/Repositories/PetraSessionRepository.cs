using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data.Repositories;

/// <summary>
/// Stage 63B：Petra Orchestrator session 資料存取（v5 動態架構 PoC）。
/// Caller 負責 SaveChangesAsync — 對齊 BossInteractionRepository pattern。
/// </summary>
public class PetraSessionRepository(AppDbContext db)
{
    /// <summary>建新 session（caller SaveChangesAsync 後 Id 才產生）。</summary>
    public PetraSession Start(Guid? taskGroupId)
    {
        var session = new PetraSession
        {
            TaskGroupId = taskGroupId,
            Status = "running",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.PetraSessions.Add(session);
        return session;
    }

    /// <summary>寫入一筆 session message（caller SaveChangesAsync）。
    /// Stage 68：簽名改 async + CT 對齊 BossInteractionRepository pattern（FF 二補強清單）。
    /// 當前實作純 EF Add 無 I/O — 回 Task.CompletedTask；CT 為將來 SaveChanges-inline 進化保留。</summary>
    public Task AppendMessageAsync(
        Guid sessionId,
        string role,
        string content,
        string? toolCallId = null,
        CancellationToken ct = default)
    {
        db.PetraSessionMessages.Add(new PetraSessionMessage
        {
            SessionId = sessionId,
            Role = role,
            Content = content,
            ToolCallId = toolCallId,
            CreatedAt = DateTime.UtcNow,
        });
        return Task.CompletedTask;
    }

    /// <summary>取所有 running session（PetraSessionRecoveryService Bot 啟動時掃描用）。</summary>
    public Task<List<PetraSession>> GetRunningAsync(CancellationToken ct = default)
        => db.PetraSessions
            .Where(x => x.Status == "running")
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    /// <summary>取 session 含 messages（時序排序）。</summary>
    public Task<PetraSession?> GetWithMessagesAsync(Guid sessionId, CancellationToken ct = default)
        => db.PetraSessions
            .Include(x => x.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(x => x.Id == sessionId, ct);

    /// <summary>標記 session 完成。</summary>
    public async Task CompleteAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.PetraSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session is null) return;
        session.Status = "done";
        session.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>標記 session escalated（worker 失敗或需老闆介入）。</summary>
    public async Task EscalateAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.PetraSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session is null) return;
        session.Status = "escalated";
        session.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>取 TaskGroup 對應 running session（一個 group 同時間只應有一個 running）。</summary>
    public Task<PetraSession?> GetRunningForGroupAsync(Guid taskGroupId, CancellationToken ct = default)
        => db.PetraSessions
            .Where(x => x.TaskGroupId == taskGroupId && x.Status == "running")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
}
