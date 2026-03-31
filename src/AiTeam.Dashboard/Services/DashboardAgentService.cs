using AiTeam.Data;
using AiTeam.Shared.Constants;
using AiTeam.Shared.Dtos;
using AiTeam.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>
/// Agent 狀態查詢服務，初始狀態從 DB 讀取統計，後續由 SignalR 推送更新。
/// </summary>
public class DashboardAgentService(AppDbContext db)
{
    #region Public Methods

    /// <summary>取得所有 Agent 設定 DTO（含信任等級）。</summary>
    public async Task<List<AgentConfigDto>> GetAgentConfigsAsync(
        CancellationToken cancellationToken = default)
        => await db.AgentConfigs
            .AsNoTracking()
            .Include(a => a.Team)
            .Select(a => new AgentConfigDto
            {
                Id       = a.Id,
                Name     = a.Name,
                TrustLevel = a.TrustLevel,
                IsActive = a.IsActive,
                TeamName = a.Team.Name
            })
            .ToListAsync(cancellationToken);

    /// <summary>取得所有 Agent 的初始狀態 ViewModel（首頁用）。</summary>
    public async Task<List<AgentStatusViewModel>> GetAllAgentStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        var configs = await GetAgentConfigsAsync(cancellationToken);
        var today = DateTime.UtcNow.Date;

        var result = new List<AgentStatusViewModel>();
        foreach (var cfg in configs)
        {
            var completedToday = await db.Tasks
                .AsNoTracking()
                .CountAsync(t => t.AssignedAgent == cfg.Name
                              && t.Status == AiTeam.Shared.Constants.TaskStatus.Done
                              && t.CreatedAt >= today,
                    cancellationToken);

            var failedToday = await db.Tasks
                .AsNoTracking()
                .CountAsync(t => t.AssignedAgent == cfg.Name
                              && t.Status == AiTeam.Shared.Constants.TaskStatus.Failed
                              && t.CreatedAt >= today,
                    cancellationToken);

            var running = await db.Tasks
                .AsNoTracking()
                .Where(t => t.AssignedAgent == cfg.Name
                         && t.Status == AiTeam.Shared.Constants.TaskStatus.Running)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => t.Title)
                .FirstOrDefaultAsync(cancellationToken);

            result.Add(new AgentStatusViewModel
            {
                AgentName           = cfg.Name,
                Status              = running != null ? "running" : "idle",
                TrustLevel          = cfg.TrustLevel,
                CurrentTaskTitle    = running,
                TodayCompletedCount = completedToday,
                TodayFailedCount    = failedToday,
                LastUpdated         = DateTime.UtcNow
            });
        }

        return result;
    }

    #endregion
}
