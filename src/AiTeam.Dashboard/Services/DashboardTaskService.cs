using AiTeam.Data;
using AiTeam.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>
/// 任務查詢服務，回傳 Dashboard 用的 DTO（不直接回傳 Entity）。
/// </summary>
public class DashboardTaskService(AppDbContext db)
{
    #region Public Methods

    /// <summary>取得分頁任務列表。</summary>
    public async Task<PagedResult<TaskItemDto>> GetTasksPagedAsync(
        int page = 1,
        int pageSize = 50,
        string? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.Team)
            .Where(t => statusFilter == null || t.Status == statusFilter)
            .OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TaskItemDto
            {
                Id            = t.Id,
                Title         = t.Title,
                TriggeredBy   = t.TriggeredBy,
                AssignedAgent = t.AssignedAgent,
                Status        = t.Status,
                CreatedAt     = t.CreatedAt,
                CompletedAt   = t.CompletedAt,
                ProjectName   = t.Project != null ? t.Project.Name : null,
                TeamName      = t.Team    != null ? t.Team.Name    : null
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<TaskItemDto>(items, total);
    }

    /// <summary>取得最近 N 筆任務（首頁快速摘要用）。</summary>
    public async Task<List<TaskItemDto>> GetRecentTasksAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
        => await db.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.Team)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .Select(t => new TaskItemDto
            {
                Id            = t.Id,
                Title         = t.Title,
                TriggeredBy   = t.TriggeredBy,
                AssignedAgent = t.AssignedAgent,
                Status        = t.Status,
                CreatedAt     = t.CreatedAt,
                CompletedAt   = t.CompletedAt,
                ProjectName   = t.Project != null ? t.Project.Name : null,
                TeamName      = t.Team    != null ? t.Team.Name    : null
            })
            .ToListAsync(cancellationToken);

    /// <summary>取得 TaskGroup 分頁列表（流程追蹤 Tab 用）。</summary>
    public async Task<PagedResult<TaskGroupDto>> GetTaskGroupsPagedAsync(
        int page = 1,
        int pageSize = 50,
        string? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.TaskGroups
            .AsNoTracking()
            .Where(g => statusFilter == null || g.Status == statusFilter)
            .OrderByDescending(g => g.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new TaskGroupDto
            {
                Id              = g.Id,
                Title           = g.Title,
                Status          = g.Status,
                WorkflowType    = g.WorkflowType,
                Project         = g.Project,
                FixIteration    = g.FixIteration,
                DevPlanRevision = g.DevPlanRevision,
                DevPrUrl        = g.DevPrUrl,
                CreatedAt       = g.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<TaskGroupDto>(items, total);
    }

    /// <summary>取得 TaskGroup 下所有 TaskItem（Pipeline View 步驟用）。</summary>
    public async Task<List<TaskItemDto>> GetTaskItemsByGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
        => await db.Tasks
            .AsNoTracking()
            .Where(t => t.GroupId == groupId)
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Select(t => new TaskItemDto
            {
                Id            = t.Id,
                GroupId       = t.GroupId,
                Title         = t.Title,
                TriggeredBy   = t.TriggeredBy,
                AssignedAgent = t.AssignedAgent,
                Status        = t.Status,
                CreatedAt     = t.CreatedAt,
                CompletedAt   = t.CompletedAt
            })
            .ToListAsync(cancellationToken);

    /// <summary>取得任務的所有 Log（點擊任務後展開用）。</summary>
    public async Task<List<TaskLogDto>> GetTaskLogsAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
        => await db.TaskLogs
            .AsNoTracking()
            .Where(l => l.TaskId == taskId)
            .OrderBy(l => l.CreatedAt)
            .Select(l => new TaskLogDto
            {
                Id        = l.Id,
                TaskId    = l.TaskId,
                Agent     = l.Agent,
                Step      = l.Step,
                Status    = l.Status,
                Payload   = l.Payload,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync(cancellationToken);

    #endregion
}

/// <summary>分頁結果包裝。</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
