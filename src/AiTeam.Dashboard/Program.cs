using AiTeam.Dashboard.Identity;
using MudBlazor.Services;
using AiTeam.Dashboard.Services;
using AiTeam.Data;
using AiTeam.Data.Extensions;
using AiTeam.Data.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// 業務資料庫（AppDbContext + TaskRepository，共用同一個 PostgreSQL）
builder.AddAiTeamData("AiTeamDb");

// Identity 資料庫（DashboardDbContext，同一個 PostgreSQL，使用 "identity" schema）
builder.AddNpgsqlDbContext<DashboardDbContext>("AiTeamDb");

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

// SignalR（Hub 定義在 AiTeam.Data）
builder.Services.AddSignalR();

// ASP.NET Core Identity（AddIdentity 自動設定 Cookie scheme，包含 DefaultChallengeScheme）
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase       = false;
    options.Password.RequireLowercase       = false;
    options.Password.RequireDigit           = false;
    options.Password.RequiredLength         = 8;
})
.AddEntityFrameworkStores<DashboardDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthorization();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath  = "/login";
    options.LogoutPath = "/logout";
});

// Controllers（AgentStatusController 供 Bot 推送 SignalR；AccountController 處理登入 POST）
// AddControllersWithViews 而非 AddControllers，才能使用 [ValidateAntiForgeryToken]
builder.Services.AddControllersWithViews();

// Dashboard Services
builder.Services.AddScoped<DashboardTaskService>();
builder.Services.AddScoped<DashboardProjectService>();
builder.Services.AddScoped<DashboardAgentService>();
builder.Services.AddScoped<DashboardRuleService>();
builder.Services.AddScoped<DashboardBotService>();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapControllers();
app.MapHub<AgentStatusHub>("/hubs/agent-status");
app.MapRazorComponents<AiTeam.Dashboard.Components.App>()
    .AddInteractiveServerRenderMode();

// 啟動時套用 Identity Migration，並 Seed 基礎 Agent 資料
// （AppDbContext schema migration 由 Bot 負責；Seed 為幂等操作，兩端皆可安全呼叫）
using (var scope = app.Services.CreateScope())
{
    var identityDb = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
    await identityDb.Database.MigrateAsync();

    var appDb  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await DbSeeder.SeedAsync(appDb);
        logger.LogInformation("DbSeeder 完成");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "DbSeeder 執行失敗，Agent 設定頁面將顯示空白");
    }
}

await app.Services.EnsureAdminUserAsync(app.Configuration);

app.Run();
