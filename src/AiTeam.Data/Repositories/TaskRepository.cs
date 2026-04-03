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

    /// <summary>查詢所有啟用中的專案名稱（CEO 反問用）。</summary>
    public async Task<List<string>> GetActiveProjectNamesAsync(CancellationToken cancellationToken = default)
        => await db.Projects
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

    /// <summary>依名稱查詢啟用中的專案 ID（Orchestrator 建立 TaskItem 時設定 ProjectId 用）。</summary>
    public async Task<Guid?> GetProjectIdByNameAsync(string projectName, CancellationToken cancellationToken = default)
        => await db.Projects
            .Where(p => p.IsActive && p.Name == projectName)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

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

    /// <summary>
    /// Bot 重啟時，將所有殘留「執行中」與「等待輸入」任務標記為「失敗」。
    /// 回傳清理的任務數量。
    /// </summary>
    public async Task<int> MarkStaleRunningTasksAsync(CancellationToken cancellationToken = default)
    {
        var staleTasks = await db.Tasks
            .Where(t => t.Status == "running" || t.Status == "waiting_input")
            .ToListAsync(cancellationToken);

        foreach (var task in staleTasks)
        {
            task.Status = "failed";
            task.CompletedAt = DateTime.UtcNow;
        }

        if (staleTasks.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        return staleTasks.Count;
    }

    // ---- TaskGroup 相關 ----

    /// <summary>新增任務群組（呼叫方負責 SaveChangesAsync）。</summary>
    public void AddGroup(TaskGroup group) => db.TaskGroups.Add(group);

    /// <summary>依 ID 查詢任務群組（含所有子任務）。</summary>
    public async Task<TaskGroup?> GetGroupByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await db.TaskGroups
            .Include(g => g.Tasks)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    /// <summary>更新任務群組狀態（呼叫方負責 SaveChangesAsync）。</summary>
    public void UpdateGroupStatus(TaskGroup group, string status)
        => group.Status = status;

    /// <summary>查詢指定 PR 且仍在執行中的 Reviewer 任務（Review 閉環用）。</summary>
    public async Task<List<TaskItem>> GetActiveReviewerTasksByPrAsync(
        int prNumber,
        CancellationToken cancellationToken = default)
        => await db.Tasks
            .Where(t => t.AssignedAgent == "Reviewer"
                     && (t.Status == "pending" || t.Status == "running")
                     && t.Description != null
                     && t.Description.Contains($"#{prNumber}"))
            .ToListAsync(cancellationToken);
}
