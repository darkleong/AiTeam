using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data.Repositories;

/// <summary>
/// agent_configs 資料存取。
/// </summary>
public class AgentRepository(AppDbContext db)
{
    /// <summary>
    /// 查詢所有啟用中的執行 Agent（排除 CEO，CEO 由框架自身管理）。
    /// 結果依名稱排序，供 CEO 系統提示與 CommandHandler 分派使用。
    /// </summary>
    public async Task<List<AgentConfig>> GetActiveExecutorAgentsAsync(
        CancellationToken cancellationToken = default)
        => await db.AgentConfigs
            .AsNoTracking()
            .Where(a => a.IsActive && a.Name != "CEO")
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
}
