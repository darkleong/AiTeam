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

    /// <summary>新增專案。</summary>
    public async Task<ProjectDto> CreateProjectAsync(
        string name,
        string? repoUrl,
        string? techStack,
        CancellationToken cancellationToken = default)
    {
        var team = await db.Teams.FirstAsync(cancellationToken);
        var project = new Project
        {
            TeamId    = team.Id,
            Name      = name.Trim(),
            RepoUrl   = string.IsNullOrWhiteSpace(repoUrl) ? null : repoUrl.Trim(),
            TechStack = string.IsNullOrWhiteSpace(techStack) ? null : techStack.Trim(),
            IsActive  = true
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        return new ProjectDto
        {
            Id        = project.Id,
            Name      = project.Name,
            RepoUrl   = project.RepoUrl,
            TechStack = project.TechStack,
            IsActive  = project.IsActive,
            CreatedAt = project.CreatedAt,
            TeamName  = team.Name,
            TaskCount = 0
        };
    }

    /// <summary>切換專案啟用狀態。</summary>
    public async Task ToggleProjectActiveAsync(
        Guid projectId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var project = await db.Projects.FindAsync([projectId], cancellationToken);
        if (project is null) return;
        project.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    #endregion
}
