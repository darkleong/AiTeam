using AiTeam.Data;
using AiTeam.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>專案查詢服務。
///
/// Stage 85 子項 0（🔴 root cause fix）：ctor 改 IDbContextFactory&lt;AppDbContext&gt; pattern — 修 Blazor InteractiveServer
/// 多元件並行 OnInitializedAsync 撞同 Scoped DbContext 引發「A second operation was started on this context instance」→
/// Circuit terminated 根因（每個 method 開新 short-lived DbContext / await using 自動 dispose）。
/// 對齊 Stage 80 既有 DashboardAppSettingsService / Stage 83 議題 F3 既有 pattern。
/// </summary>
public class DashboardProjectService(IDbContextFactory<AppDbContext> dbFactory)
{
    #region Public Methods

    /// <summary>取得所有專案列表（含任務數量）。</summary>
    public async Task<List<ProjectDto>> GetAllProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Projects
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
    }

    /// <summary>新增專案。</summary>
    public async Task<ProjectDto> CreateProjectAsync(
        string name,
        string? repoUrl,
        string? techStack,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
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
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var project = await db.Projects.FindAsync([projectId], cancellationToken);
        if (project is null) return;
        project.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    #endregion
}
