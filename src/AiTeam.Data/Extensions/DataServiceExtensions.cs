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
        builder.Services.AddScoped<CeoConversationRepository>();
        builder.Services.AddScoped<CeoMemoryRepository>();
        builder.Services.AddScoped<BossInteractionRepository>();
        builder.Services.AddScoped<BossCommandLogRepository>();
        // Stage 75：v5.5 Phase 3 — Petra 接收層 queue（Dashboard InteractionCenter + TaskCenter 用）
        builder.Services.AddScoped<PetraInboxRepository>();
        return builder;
    }
}
