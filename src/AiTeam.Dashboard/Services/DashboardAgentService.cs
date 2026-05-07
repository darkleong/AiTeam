using AiTeam.Data;
using AiTeam.Shared.Constants;
using AiTeam.Shared.Exceptions;
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

    /// <summary>新增 Agent 設定，回傳新建的 AgentConfigDto。TeamId 自動使用第一個 Team。</summary>
    public async Task<AgentConfigDto> CreateAgentAsync(
        string name,
        string description,
        int trustLevel,
        CancellationToken cancellationToken = default)
    {
        var teamId = await db.Teams
            .AsNoTracking()
            .Select(t => t.Id)
            .FirstAsync(cancellationToken);

        var agent = new AgentConfig
        {
            Name        = name.Trim(),
            Description = description.Trim(),
            TrustLevel  = trustLevel,
            IsActive    = true,
            TeamId      = teamId
        };

        db.AgentConfigs.Add(agent);
        await db.SaveChangesAsync(cancellationToken);

        var team = await db.Teams.FindAsync([teamId], cancellationToken);
        return new AgentConfigDto
        {
            Id               = agent.Id,
            Name             = agent.Name,
            Description      = agent.Description,
            TrustLevel       = agent.TrustLevel,
            IsActive         = agent.IsActive,
            TeamName         = team?.Name ?? "",
            Provider         = agent.Provider,
            Model            = agent.Model,
            DailyTokenLimitK  = agent.DailyTokenLimitK,
            MonthlyTokenLimitK = agent.MonthlyTokenLimitK
        };
    }

    /// <summary>切換 Agent 的啟用狀態，回傳更新後的 IsActive 值。</summary>
    public async Task<bool> UpdateIsActiveAsync(
        Guid agentId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var agent = await db.AgentConfigs.FindAsync([agentId], cancellationToken);
        if (agent is null) throw new AgentConfigurationException();

        agent.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
        return agent.IsActive;
    }

    /// <summary>更新 Agent 信任等級（寫入 PostgreSQL）。</summary>
    public async Task UpdateTrustLevelAsync(
        Guid agentId,
        int trustLevel,
        CancellationToken cancellationToken = default)
    {
        var agent = await db.AgentConfigs.FindAsync([agentId], cancellationToken);
        if (agent is null) throw new AgentConfigurationException();
        agent.TrustLevel = trustLevel;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stage 38：更新 Agent 的 Provider / Model（Dashboard UI 存檔呼叫）。
    /// Server-side 白名單驗證：防 stale 瀏覽器 tab 繞過 MudSelect 約束寫入 garbage 值，
    /// 造成 LlmProviderFactory 在下次任務 runtime 拋 NotSupportedException。
    /// 呼叫方需額外呼叫 DashboardBotService.ReloadCacheAsync("agent-config") 讓 Bot 端快取失效。
    /// </summary>
    public async Task<bool> UpdateProviderModelAsync(
        Guid agentId,
        string provider,
        string model,
        CancellationToken cancellationToken = default)
    {
        if (!LlmModels.AvailableProviders.Contains(provider))
            throw new ArgumentException($"不支援的 Provider：{provider}", nameof(provider));
        if (!LlmModels.GetModelsForProvider(provider).Contains(model))
            throw new ArgumentException($"Provider {provider} 不支援 Model：{model}", nameof(model));

        var agent = await db.AgentConfigs.FindAsync([agentId], cancellationToken);
        if (agent is null) return false;

        agent.Provider = provider;
        agent.Model    = model;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Stage 47：更新 Agent 的 Daily / Monthly Token Limit（0 或負數 → 清為 null，回到 appsettings fallback）。</summary>
    public async Task<bool> UpdateTokenLimitsAsync(
        Guid agentId,
        int? dailyTokenLimitK,
        int? monthlyTokenLimitK,
        CancellationToken cancellationToken = default)
    {
        var agent = await db.AgentConfigs.FindAsync([agentId], cancellationToken);
        if (agent is null) return false;

        agent.DailyTokenLimitK   = dailyTokenLimitK > 0   ? dailyTokenLimitK   : null;
        agent.MonthlyTokenLimitK = monthlyTokenLimitK > 0 ? monthlyTokenLimitK : null;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>取得所有 Agent 設定 DTO（含信任等級）。</summary>
    public async Task<List<AgentConfigDto>> GetAgentConfigsAsync(
        CancellationToken cancellationToken = default)
        => await db.AgentConfigs
            .AsNoTracking()
            .Include(a => a.Team)
            .Select(a => new AgentConfigDto
            {
                Id               = a.Id,
                Name             = a.Name,
                Description      = a.Description,
                TrustLevel       = a.TrustLevel,
                IsActive         = a.IsActive,
                TeamName         = a.Team.Name,
                Provider         = a.Provider,
                Model            = a.Model,
                DailyTokenLimitK  = a.DailyTokenLimitK,
                MonthlyTokenLimitK = a.MonthlyTokenLimitK
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
