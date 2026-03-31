using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiTeam.Data;

/// <summary>
/// EF Core CLI 設計時期工廠，供 dotnet ef migrations 指令使用。
/// 執行時期由 Aspire 注入連線字串，此工廠僅用於本機開發時的 Migration 操作。
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=aiteamdb;Username=postgres;Password=postgres");
        return new AppDbContext(optionsBuilder.Options);
    }
}
