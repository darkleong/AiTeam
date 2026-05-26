using AiTeam.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiTeam.Data.Extensions;

/// <summary>
/// Bot 和 Dashboard 共用的 Data 層服務註冊擴充方法。
/// v4-rewrite：Petra/v5.5 repository（PetraInboxRepository / PetraSessionRepository / MemoryRepository / PromptRepository）整套砍。
/// </summary>
public static class DataServiceExtensions
{
    public static IHostApplicationBuilder AddAiTeamData(
        this IHostApplicationBuilder builder,
        string connectionName = "AiTeamDb")
    {
        builder.AddNpgsqlDbContext<AppDbContext>(connectionName);
        builder.Services.AddScoped<TaskRepository>();
        builder.Services.AddScoped<TokenRepository>();
        builder.Services.AddScoped<CeoConversationRepository>();
        builder.Services.AddScoped<CeoMemoryRepository>();
        builder.Services.AddScoped<BossInteractionRepository>();
        builder.Services.AddScoped<BossCommandLogRepository>();
        return builder;
    }
}
