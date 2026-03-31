using AiTeam.Data;
using AiTeam.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>專案查詢服務。</summary>
public class DashboardProjectService(AppDbContext db)
{
    #region Public Methods

    /// <summary>取得所有專案列表（含任務數量）。</summary>
    public async Task<List<ProjectDto>> GetAllProjectsAsync(
        CancellationToken cancellationToken = default)
        => await db.Projects
            .AsNoTracking()
            .Include(p => p.Team)
            .OrderBy(p => p.Name)
            .Select(p => new ProjectDto
            {
                Id        = p.Id,
                Name      = p.Name,
                RepoUrl   = p.RepoUrl,
                TechStack = p.TechStack,
                IsActive  = p.IsActive,
                CreatedAt = p.CreatedAt,
                TeamName  = p.Team.Name,
                TaskCount = p.Tasks.Count
            })
            .ToListAsync(cancellationToken);

    #endregion
}
