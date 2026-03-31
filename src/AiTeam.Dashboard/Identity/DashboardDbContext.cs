using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Identity;

/// <summary>
/// Dashboard 專用 Identity DbContext，使用 "identity" schema 避免與業務資料表衝突。
/// </summary>
public class DashboardDbContext(DbContextOptions<DashboardDbContext> options)
    : IdentityDbContext<IdentityUser, IdentityRole, string>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");
    }
}
