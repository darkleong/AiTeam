using AiTeam.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiTeam.Data.Extensions;

/// <summary>
/// Bot 和 Dashboard 共用的 Data 層服務註冊擴充方法。
/// 讓兩個專案不需要各自重複 DbContext 和 Repository 的設定。
/// </summary>
public static class DataServiceExtensions
{
    public static IHostApplicationBuilder AddAiTeamData(
        this IHostApplicationBuilder builder,
        string connectionName = "AiTeamDb")
    {
        builder.AddNpgsqlDbContext<AppDbContext>(connectionName);
        builder.Services.AddScoped<TaskRepository>();
        builder.Services.AddScoped<AgentRepository>();
        builder.Services.AddScoped<TokenRepository>();
        return builder;
    }
}
