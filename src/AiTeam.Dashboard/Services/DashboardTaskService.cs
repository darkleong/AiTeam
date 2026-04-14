using AiTeam.Data;
using AiTeam.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>
/// 任務查詢服務，回傳 Dashboard 用的 DTO（不直接回傳 Entity）。
/// SemaphoreSlim 確保同一 Blazor circuit（Scoped）下的 DB 操作不並發，
/// 防止 AppDbContext 收到多個並行查詢導致 circuit crash。
/// </summary>
public class DashboardTaskService(AppDbContext db)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    #region Public Methods

    /// <summary>取得分頁任務列表。</summary>
    public async Task<PagedResult<TaskItemDto>> GetTasksPagedAsync(
        int page = 1,
        int pageSize = 50,
        IReadOnlyCollection<string>? statusFilters = null,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var query = db.Tasks
                .AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.Team)
                .Where(t => statusFilters == null || statusFilters.Count == 0 || statusFilters.Contains(t.Status))
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
        finally { _lock.Release(); }
    }

    /// <summary>取得最近 N 筆任務（首頁快速摘要用）。</summary>
    public async Task<List<TaskItemDto>> GetRecentTasksAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await db.Tasks
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
        }
        finally { _lock.Release(); }
    }

    /// <summary>取得最近 N 筆流程（首頁快速摘要用）。</summary>
    public async Task<List<TaskGroupDto>> GetRecentTaskGroupsAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await db.TaskGroups
                .AsNoTracking()
                .OrderByDescending(g => g.CreatedAt)
                .Take(limit)
                .Select(g => new TaskGroupDto
                {
                    Id                = g.Id,
                    Title             = g.Title,
                    Status            = g.Status,
                    WorkflowType      = g.WorkflowType,
                    Project           = g.Project,
                    FixIteration      = g.FixIteration,
                    DevPlanRevision   = g.DevPlanRevision,
                    DevPrUrl          = g.DevPrUrl,
                    CreatedAt         = g.CreatedAt,
                    KickoffMeetingLog = g.KickoffMeetingLog,
                    TaskPlan          = g.TaskPlan,
                    KickoffRound      = g.KickoffRound,
                    DesignMeetingLog  = g.DesignMeetingLog,
                    DesignPlan        = g.DesignPlan,
                    DesignRound       = g.DesignRound,
                    DevPlan           = g.DevPlan,
                    LastReviewBody    = g.LastReviewBody,
                    TestReport        = g.TestReport,
                })
                .ToListAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    /// <summary>取得 TaskGroup 分頁列表（流程追蹤 Tab 用）。</summary>
    public async Task<PagedResult<TaskGroupDto>> GetTaskGroupsPagedAsync(
        int page = 1,
        int pageSize = 50,
        IReadOnlyCollection<string>? statusFilters = null,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var query = db.TaskGroups
                .AsNoTracking()
                .Where(g => statusFilters == null || statusFilters.Count == 0 || statusFilters.Contains(g.Status))
                .OrderByDescending(g => g.CreatedAt);

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(g => new TaskGroupDto
                {
                    Id                = g.Id,
                    Title             = g.Title,
                    Status            = g.Status,
                    WorkflowType      = g.WorkflowType,
                    Project           = g.Project,
                    FixIteration      = g.FixIteration,
                    DevPlanRevision   = g.DevPlanRevision,
                    DevPrUrl          = g.DevPrUrl,
                    CreatedAt         = g.CreatedAt,
                    KickoffMeetingLog = g.KickoffMeetingLog,
                    TaskPlan          = g.TaskPlan,
                    KickoffRound      = g.KickoffRound,
                    DesignMeetingLog  = g.DesignMeetingLog,
                    DesignPlan        = g.DesignPlan,
                    DesignRound       = g.DesignRound,
                    DevPlan           = g.DevPlan,
                    LastReviewBody    = g.LastReviewBody,
                    TestReport        = g.TestReport,
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<TaskGroupDto>(items, total);
        }
        finally { _lock.Release(); }
    }

    /// <summary>取得單一 TaskGroup 的最新快照（Pipeline View 折疊面板即時更新用）。</summary>
    public async Task<TaskGroupDto?> GetTaskGroupByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await db.TaskGroups
                .AsNoTracking()
                .Where(g => g.Id == id)
                .Select(g => new TaskGroupDto
                {
                    Id                = g.Id,
                    Title             = g.Title,
                    Status            = g.Status,
                    WorkflowType      = g.WorkflowType,
                    Project           = g.Project,
                    FixIteration      = g.FixIteration,
                    DevPlanRevision   = g.DevPlanRevision,
                    DevPrUrl          = g.DevPrUrl,
                    CreatedAt         = g.CreatedAt,
                    KickoffMeetingLog = g.KickoffMeetingLog,
                    TaskPlan          = g.TaskPlan,
                    KickoffRound      = g.KickoffRound,
                    DesignMeetingLog  = g.DesignMeetingLog,
                    DesignPlan        = g.DesignPlan,
                    DesignRound       = g.DesignRound,
                    DevPlan           = g.DevPlan,
                    LastReviewBody    = g.LastReviewBody,
                    TestReport        = g.TestReport,
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    /// <summary>取得 TaskGroup 下所有 TaskItem（Pipeline View 步驟用）。</summary>
    public async Task<List<TaskItemDto>> GetTaskItemsByGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await db.Tasks
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
        }
        finally { _lock.Release(); }
    }

    /// <summary>取得任務的所有 Log（點擊任務後展開用）。</summary>
    public async Task<List<TaskLogDto>> GetTaskLogsAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await db.TaskLogs
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
        }
        finally { _lock.Release(); }
    }

    #endregion
}

/// <summary>分頁結果包裝。</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
