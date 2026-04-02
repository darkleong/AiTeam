using AiTeam.Data;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>動態系統設定 CRUD 服務。</summary>
public class DashboardAppSettingsService(AppDbContext db)
{
    public async Task<List<AppSetting>> GetAllAsync(CancellationToken cancellationToken = default)
        => await db.AppSettings
            .AsNoTracking()
            .OrderBy(s => s.Key)
            .ToListAsync(cancellationToken);

    public async Task<AppSetting?> GetAsync(string key, CancellationToken cancellationToken = default)
        => await db.AppSettings.FindAsync([key], cancellationToken);

    public async Task UpsertAsync(string key, string value, CancellationToken cancellationToken = default)
    {
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
