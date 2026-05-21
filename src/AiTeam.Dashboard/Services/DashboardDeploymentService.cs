using AiTeam.Data;
using AiTeam.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>
/// Stage 83 子項 7 議題 E1：拆 DashboardTaskService 568 行 →
/// DashboardDeploymentService（v4 entity TaskItem + TaskLog query — 對齊 Roadmap §決策 #1
/// 「v4 entity 留 schema 不 drop / OpsAgent + Internal Deployment 還 active 寫 TaskItem」紀律）。
///
/// 議題 F3：改 IDbContextFactory pattern + 砍 SemaphoreSlim（對齊 Stage 80 修根因紀律）。
///
/// 真實 caller：DeploymentHistory.razor.cs（Monitoring 分區 Deployments tab link to it）。
/// </summary>
public class DashboardDeploymentService(IDbContextFactory<AppDbContext> dbFactory)
{
    /// <summary>取得分頁 TaskItem（DeploymentHistory.razor 篩 AssignedAgent='Ops' 視角 — pageSize=200 既有 caller 傳）。</summary>
    public async Task<PagedResult<TaskItemDto>> GetTasksPagedAsync(
        int page = 1,
        int pageSize = 50,
        IReadOnlyCollection<string>? statusFilters = null,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var query = db.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.Team)
            .Where(t => statusFilters == null || statusFilters.Count == 0 || statusFilters.Contains(t.Status))
            .OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync(ct);
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
            .ToListAsync(ct);

        return new PagedResult<TaskItemDto>(items, total);
    }

    /// <summary>取得任務的所有 Log（DeploymentHistory drawer 點 row 展開用）。</summary>
    public async Task<List<TaskLogDto>> GetTaskLogsAsync(
        Guid taskId,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
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
            .ToListAsync(ct);
    }
}

/// <summary>分頁結果包裝（從 DashboardTaskService 砍時 migrate 過來）。</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
