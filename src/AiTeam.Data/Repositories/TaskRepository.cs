using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data.Repositories;

/// <summary>
/// tasks / task_logs 資料存取，遵循 Repository 不呼叫 SaveChangesAsync 原則。
/// </summary>
public class TaskRepository(AppDbContext db)
{
    /// <summary>建立新任務（呼叫方負責 SaveChangesAsync）。</summary>
    public void Add(TaskItem task) => db.Tasks.Add(task);

    /// <summary>新增執行步驟 log（呼叫方負責 SaveChangesAsync）。</summary>
    public void AddLog(TaskLog log) => db.TaskLogs.Add(log);

    /// <summary>依 ID 查詢任務（含最近 20 筆 log）。</summary>
    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await db.Tasks
            .Include(t => t.Logs.OrderByDescending(l => l.CreatedAt).Take(20))
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    /// <summary>查詢指定專案的最近 N 筆任務（CEO 組 Prompt 用）。</summary>
    public async Task<List<TaskItem>> GetRecentByProjectAsync(
        string projectName,
        int limit = 5,
        CancellationToken cancellationToken = default)
        => await db.Tasks
            .Include(t => t.Project)
            .Where(t => t.Project != null && t.Project.Name == projectName)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    /// <summary>更新任務狀態（呼叫方負責 SaveChangesAsync）。</summary>
    public void UpdateStatus(TaskItem task, string status)
    {
        task.Status = status;
        if (status is "done" or "failed")
            task.CompletedAt = DateTime.UtcNow;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
        => await db.SaveChangesAsync(cancellationToken);
}
