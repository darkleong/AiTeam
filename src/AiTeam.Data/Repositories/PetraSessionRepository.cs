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

    /// <summary>Stage 83：取得 active session count — running + paused 視為「進行中」（Home 速覽 + Tasks 分區 ActiveSessions 用）。</summary>
    public Task<int> CountActiveAsync(CancellationToken ct = default)
        => db.PetraSessions.CountAsync(x => x.Status == "running" || x.Status == "paused", ct);

    /// <summary>Stage 83：取得 active session list — running + paused（Tasks 分區 ActiveSessions 用 / UpdatedAt 倒序）。</summary>
    public Task<List<PetraSession>> GetActiveAsync(int limit, CancellationToken ct = default)
        => db.PetraSessions
            .Where(x => x.Status == "running" || x.Status == "paused")
            .OrderByDescending(x => x.UpdatedAt)
            .Take(limit)
            .ToListAsync(ct);

    /// <summary>Stage 83：取得 history session list — done / escalated / cancelled（Tasks 分區 History 用 / UpdatedAt 倒序）。</summary>
    public Task<List<PetraSession>> GetHistoryAsync(int limit, CancellationToken ct = default)
        => db.PetraSessions
            .Where(x => x.Status == "done" || x.Status == "escalated" || x.Status == "cancelled")
            .OrderByDescending(x => x.UpdatedAt)
            .Take(limit)
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

    /// <summary>Stage 80：標記 session 進入 HITL plan_confirm 等待狀態（Status="paused"）。
    /// 對齊 PetraSessionRecoveryService「重啟重跑」紀律 — Bot 重啟掃 running session resume；paused session 不掃，等 Christ 回覆才被
    /// PlanConfirmationProcessor 拉起繼續 dispatch（Bot 重啟期 plan_confirm BossInteraction 仍在 DB / 0 漏單）。</summary>
    public async Task PauseAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.PetraSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session is null) return;
        session.Status = "paused";
        session.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Stage 80：標記 session 被 Christ HITL plan_confirm reject 取消（Status="cancelled"）。
    /// 對齊 4 decision pattern reject 路徑（task_memory 寫 decision/plan-rejected + chain dispatch 0 啟動）。</summary>
    public async Task CancelAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.PetraSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session is null) return;
        session.Status = "cancelled";
        session.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Stage 81：累計 replan iteration 輪數（approve / edit / respond 觸發 +1 / reject 不算）。</summary>
    public async Task IncrementReplanIterationAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.PetraSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session is null) return;
        session.ReplanIteration += 1;
        session.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Stage 81：從 token_logs WHERE PetraSessionId=... 累計 session 真實 cost 寫回 PetraSession.SessionCostUsd。
    /// 呼叫時機：DispatchTalentsAsync.ProcessSubtaskResultAsync 內 worker dispatch + token_logs 寫入後（再做 cap check）。</summary>
    public async Task UpdateSessionCostUsdAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.PetraSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session is null) return;
        session.SessionCostUsd = await db.TokenLogs
            .Where(l => l.PetraSessionId == sessionId)
            .SumAsync(l => l.TotalCostUsd ?? 0m, ct);
        session.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Stage 81：取得 session 當前 ReplanIteration + SessionCostUsd（cap check 用 / 0 PetraSession row 回 (0, 0)）。</summary>
    public async Task<(int Iter, decimal Cost)> GetReplanStateAsync(Guid sessionId, CancellationToken ct = default)
    {
        var row = await db.PetraSessions
            .Where(x => x.Id == sessionId)
            .Select(x => new { x.ReplanIteration, x.SessionCostUsd })
            .FirstOrDefaultAsync(ct);
        return row is null ? (0, 0m) : (row.ReplanIteration, row.SessionCostUsd);
    }
}
