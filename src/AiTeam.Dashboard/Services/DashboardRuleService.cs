using AiTeam.Data;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>規則 CRUD 服務。</summary>
public class DashboardRuleService(AppDbContext db)
{
    #region Public Methods

    /// <summary>取得所有規則（依 SortOrder, CreatedAt 排序）。</summary>
    public async Task<List<Rule>> GetAllRulesAsync(CancellationToken cancellationToken = default)
        => await db.Rules
            .AsNoTracking()
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <summary>新增規則。</summary>
    public async Task<Rule> CreateRuleAsync(
        string content,
        int sortOrder = 0,
        CancellationToken cancellationToken = default)
    {
        var team = await db.Teams.FirstAsync(cancellationToken);
        var rule = new Rule
        {
            TeamId    = team.Id,
            Content   = content.Trim(),
            IsActive  = true,
            SortOrder = sortOrder
        };
        db.Rules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return rule;
    }

    /// <summary>更新規則內容。</summary>
    public async Task UpdateRuleAsync(
        Guid id,
        string content,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        var rule = await db.Rules.FindAsync([id], cancellationToken);
        if (rule is null) return;
        rule.Content   = content.Trim();
        rule.SortOrder = sortOrder;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>切換規則啟用狀態。</summary>
    public async Task ToggleRuleActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var rule = await db.Rules.FindAsync([id], cancellationToken);
        if (rule is null) return;
        rule.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>刪除規則。</summary>
    public async Task DeleteRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await db.Rules.FindAsync([id], cancellationToken);
        if (rule is null) return;
        db.Rules.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
    }

    #endregion
}
