using AiTeam.Data;
using AiTeam.Shared.Constants;
using AiTeam.Shared.Dtos;
using AiTeam.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>
/// Agent 狀態查詢服務，初始狀態從 DB 讀取統計，後續由 SignalR 推送更新。
///
/// Stage 85 子項 0：ctor 改 IDbContextFactory&lt;AppDbContext&gt; pattern（對齊 Stage 80 既有 DashboardAppSettingsService）。
/// </summary>
public class DashboardAgentService(IDbContextFactory<AppDbContext> dbFactory)
{
    #region Public Methods

    /// <summary>新增 Agent 設定，回傳新建的 AgentConfigDto。TeamId 自動使用第一個 Team。</summary>
    public async Task<AgentConfigDto> CreateAgentAsync(
        string name,
        string description,
        int trustLevel,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
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
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.AgentConfigs.FindAsync([agentId], cancellationToken);
        if (agent is null) return isActive;

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
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.AgentConfigs.FindAsync([agentId], cancellationToken);
        if (agent is null) return;
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

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
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
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
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
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AgentConfigs
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
    }

    /// <summary>
    /// 取得所有 Agent 的初始狀態 ViewModel（Monitoring AgentStatus tab + Home 速覽用）。
    ///
    /// Stage 83 v3 Bug 5 修根因：改 query `talents` 表（Stage 67 v5.5 baseline 6 Talent — Cody/Vera/Quinn/Sage/Petra/Victoria）
    /// 取代既有 `agent_configs` 表（v4 dead seed 9 Agent — PM/CEO/Doc/Release/Designer/Requirements/Dev/Reviewer/QA + Ops）。
    /// 對齊真實 v5.5 dynamic orchestrator architecture / Stage 78a 砍 v4 class 後 agent_configs row 對齊 v5.5 已 dead。
    /// TrustLevel = 0（Talent entity 無此欄位 / v5.5 沒這個概念 / UI 不顯示 trust）。
    /// </summary>
    public async Task<List<AgentStatusViewModel>> GetAllAgentStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var talents = await db.Talents
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new { t.Name })
            .ToListAsync(cancellationToken);

        var today = DateTime.UtcNow.Date;
        var result = new List<AgentStatusViewModel>();
        foreach (var t in talents)
        {
            var completedToday = await db.Tasks
                .AsNoTracking()
                .CountAsync(x => x.AssignedAgent == t.Name
                              && x.Status == AiTeam.Shared.Constants.TaskStatus.Done
                              && x.CreatedAt >= today,
                    cancellationToken);

            var failedToday = await db.Tasks
                .AsNoTracking()
                .CountAsync(x => x.AssignedAgent == t.Name
                              && x.Status == AiTeam.Shared.Constants.TaskStatus.Failed
                              && x.CreatedAt >= today,
                    cancellationToken);

            var running = await db.Tasks
                .AsNoTracking()
                .Where(x => x.AssignedAgent == t.Name
                         && x.Status == AiTeam.Shared.Constants.TaskStatus.Running)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken);

            result.Add(new AgentStatusViewModel
            {
                AgentName           = t.Name,
                Status              = running != null ? "running" : "idle",
                TrustLevel          = 0,   // Stage 83 v3 Bug 5：Talent entity 無 TrustLevel / v5.5 沒這個概念
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
