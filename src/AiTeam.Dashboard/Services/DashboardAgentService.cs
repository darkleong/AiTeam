using AiTeam.Data;
using AiTeam.Shared.Constants;
using AiTeam.Shared.Dtos;
using AiTeam.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>
/// Stage 87 A3：Agent / Talent 狀態查詢 + Talent 設定編輯服務（v4 collapse 最後殘留收口）。
///
/// 砍範圍（agent_configs 表 A4 DROP TABLE）：
///   - CreateAgentAsync / UpdateIsActiveAsync / UpdateTrustLevelAsync — AgentConfig CRUD 全砍
///   - UpdateProviderModelAsync / UpdateTokenLimitsAsync — 對應改 UpdateTalentProviderModelAsync / UpdateTalentTokenLimitsAsync 走 talents 表
///   - GetAgentConfigsAsync — Stage 87 A3 AGENTS 分頁砍 / 對應改 GetTalentsAsync 給 TALENTS 分頁 Provider/Model + Token Limit edit UI 用
///
/// 留範圍：
///   - GetAllAgentStatusesAsync — Monitoring AgentStatus tab + Home 速覽用（Stage 83 已切 talents 表 / v5.5 baseline 6 Talent）
///
/// 對齊 Stage 85 既有 IDbContextFactory pattern：每 method 短命 context / 不可走 Scoped DbContext 避免並發 bug。
/// </summary>
public class DashboardAgentService(IDbContextFactory<AppDbContext> dbFactory)
{
    #region Talent CRUD（Stage 87 A3 新增 — TALENTS 分頁 Provider/Model + Token Limit edit UI 用）

    /// <summary>Stage 87 A3：取得所有 Talent DTO（含 Provider / Model / Token Limit）— TALENTS 分頁載入用。</summary>
    public async Task<List<TalentDto>> GetTalentsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Talents
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TalentDto
            {
                Id                 = t.Id,
                Name               = t.Name,
                DisplayName        = t.DisplayName,
                Description        = t.Description,
                Provider           = t.Provider,
                Model              = t.Model,
                DailyTokenLimitK   = t.DailyTokenLimitK,
                MonthlyTokenLimitK = t.MonthlyTokenLimitK,
                IsActive           = t.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Stage 87 A3：更新 Talent 的 Provider / Model（取代 Stage 38 UpdateProviderModelAsync）。
    /// Server-side 白名單驗證：防 stale 瀏覽器 tab 繞過 MudSelect 約束寫入 garbage 值，
    /// 造成 LlmProviderFactory 在下次任務 runtime 拋 NotSupportedException。
    /// 呼叫方需額外呼叫 DashboardBotService.ReloadCacheAsync("agent-config") 讓 Bot 端 TalentMetaCache 失效。
    /// </summary>
    public async Task<bool> UpdateTalentProviderModelAsync(
        Guid talentId,
        string provider,
        string model,
        CancellationToken cancellationToken = default)
    {
        if (!LlmModels.AvailableProviders.Contains(provider))
            throw new ArgumentException($"不支援的 Provider：{provider}", nameof(provider));
        if (!LlmModels.GetModelsForProvider(provider).Contains(model))
            throw new ArgumentException($"Provider {provider} 不支援 Model：{model}", nameof(model));

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var talent = await db.Talents.FindAsync([talentId], cancellationToken);
        if (talent is null) return false;

        talent.Provider  = provider;
        talent.Model     = model;
        talent.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Stage 87 A3：更新 Talent 的 Daily / Monthly Token Limit（0 或負數 → 清為 null，回到 appsettings fallback）。
    /// 對齊 Stage 47 既有 AgentConfig.UpdateTokenLimitsAsync 行為。
    /// </summary>
    public async Task<bool> UpdateTalentTokenLimitsAsync(
        Guid talentId,
        int? dailyTokenLimitK,
        int? monthlyTokenLimitK,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var talent = await db.Talents.FindAsync([talentId], cancellationToken);
        if (talent is null) return false;

        talent.DailyTokenLimitK   = dailyTokenLimitK > 0   ? dailyTokenLimitK   : null;
        talent.MonthlyTokenLimitK = monthlyTokenLimitK > 0 ? monthlyTokenLimitK : null;
        talent.UpdatedAt          = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    #endregion

    #region Agent Status（保留 — Stage 83 已切 talents 表）

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
