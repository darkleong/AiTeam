using AiTeam.Data;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>動態系統設定 CRUD 服務。
///
/// Stage 80 議題 1（🔴 root cause fix）：ctor 改 IDbContextFactory&lt;AppDbContext&gt; pattern — 修 Blazor InteractiveServer
/// 多元件並行 OnInitializedAsync 撞同 Scoped DbContext 引發「A second operation was started on this context instance」→
/// Circuit terminated 根因（每個 method 開新 short-lived DbContext / await using 自動 dispose）。
/// 對齊既有 InteractionCenter.razor.cs / DashboardTaskService 等 Dashboard 元件 Scoped DbContext + Factory 並存 pattern。
/// </summary>
public class DashboardAppSettingsService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<AppSetting>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AppSettings
            .AsNoTracking()
            .OrderBy(s => s.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task<AppSetting?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AppSettings.FindAsync([key], cancellationToken);
    }

    public async Task UpsertAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var setting = await db.AppSettings.FindAsync([key], cancellationToken);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            setting.Value     = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
