using Anthropic.SDK;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Data;
using AiTeam.Data.Extensions;
using AiTeam.Bot.Discord;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Ops;
using AiTeam.Bot.Services;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// PostgreSQL via Aspire（AppDbContext + TaskRepository 由 AiTeam.Data 統一管理）
builder.AddAiTeamData("AiTeamDb");

// 設定
builder.Services.Configure<DiscordSettings>(builder.Configuration.GetSection("Discord"));
builder.Services.Configure<AgentSettings>(builder.Configuration.GetSection("AgentSettings"));
builder.Services.Configure<AgentSettings>(o =>
    builder.Configuration.GetSection("Agents").Bind(o.Agents));
builder.Services.Configure<GitHubSettings>(builder.Configuration.GetSection("GitHub"));
builder.Services.Configure<OpsSettings>(builder.Configuration.GetSection("OpsSettings"));

// Anthropic
var anthropicApiKey = builder.Configuration["Anthropic:ApiKey"] ?? "";
builder.Services.AddSingleton(new AnthropicClient(anthropicApiKey));
builder.Services.AddScoped<LlmProviderFactory>();

// Rules（取代 Notion，從 PostgreSQL 讀取）
builder.Services.AddSingleton<RulesService>();
// 動態系統設定（TTL cache，免重啟生效）
builder.Services.AddSingleton<AppSettingsService>();

// Agents（保留具名型別註冊以維持現有相依；同時加上 Keyed 介面，供 CommandHandler 動態分派）
builder.Services.AddScoped<CeoAgentService>();

builder.Services.AddScoped<DevAgentService>();
builder.Services.AddKeyedScoped<IAgentExecutor, DevAgentService>(AgentNames.Dev);

builder.Services.AddSingleton<OpsAgentService>();                          // Singleton：HealthCheckJob 相依
builder.Services.AddKeyedSingleton<IAgentExecutor, OpsAgentService>(AgentNames.Ops);

builder.Services.AddScoped<QaAgentService>();
builder.Services.AddKeyedScoped<IAgentExecutor, QaAgentService>(AgentNames.Qa);

builder.Services.AddScoped<DocAgentService>();
builder.Services.AddKeyedScoped<IAgentExecutor, DocAgentService>(AgentNames.Doc);

builder.Services.AddScoped<RequirementsAgentService>();
builder.Services.AddKeyedScoped<IAgentExecutor, RequirementsAgentService>(AgentNames.Requirements);

builder.Services.AddScoped<ReviewerAgentService>();
builder.Services.AddKeyedScoped<IAgentExecutor, ReviewerAgentService>(AgentNames.Reviewer);

builder.Services.AddScoped<ReleaseAgentService>();
builder.Services.AddKeyedScoped<IAgentExecutor, ReleaseAgentService>(AgentNames.Release);

builder.Services.AddScoped<DesignerAgentService>();
builder.Services.AddKeyedScoped<IAgentExecutor, DesignerAgentService>(AgentNames.Designer);

// Dashboard 推送（本機 Aspire 用 http+dashboard://，Docker 用 Dashboard:PushUrl 設定）
var dashboardPushUrl = builder.Configuration["Dashboard:PushUrl"] ?? "http+dashboard://aiteam-dashboard";
builder.Services.AddHttpClient("aiteam-dashboard", client =>
    client.BaseAddress = new Uri(dashboardPushUrl));
builder.Services.AddSingleton<DashboardPushService>();

// GitHub
builder.Services.AddSingleton<GitHubService>();
builder.Services.AddControllers();

// Discord（Stage 7：加入 GuildMessages + MessageContent 以接收自然語言訊息）
// 注意：MessageContent 是 Privileged Intent，需在 Discord Developer Portal 手動開啟
builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    LogLevel       = Discord.LogSeverity.Info,
    GatewayIntents = Discord.GatewayIntents.Guilds
                   | Discord.GatewayIntents.GuildMessages
                   | Discord.GatewayIntents.MessageContent
}));
builder.Services.AddSingleton<ConversationContextStore>();
builder.Services.AddSingleton<CommandHandler>();
builder.Services.AddHostedService<DiscordBotService>();

// Quartz 健康檢查排程
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("HealthCheck");
    q.AddJob<HealthCheckJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("HealthCheck-trigger")
        .WithCronSchedule(builder.Configuration["AgentSettings:HealthCheckCron"] ?? "0 */30 * * * ?"));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapControllers();

// 啟動時自動套用 EF Core Migrations，並 Seed 初始 AgentConfig 資料
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.Run();
