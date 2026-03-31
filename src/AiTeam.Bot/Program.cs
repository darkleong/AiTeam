using Anthropic.SDK;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Data.Extensions;
using AiTeam.Bot.Discord;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Notion;
using AiTeam.Bot.Ops;
using AiTeam.Bot.Services;
using AiTeam.Data;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Notion.Client;
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
builder.Services.Configure<NotionSettings>(builder.Configuration.GetSection("Notion"));
builder.Services.Configure<GitHubSettings>(builder.Configuration.GetSection("GitHub"));
builder.Services.Configure<OpsSettings>(builder.Configuration.GetSection("OpsSettings"));

// Anthropic
var anthropicApiKey = builder.Configuration["Anthropic:ApiKey"] ?? "";
builder.Services.AddSingleton(new AnthropicClient(anthropicApiKey));
builder.Services.AddSingleton<LlmProviderFactory>();

// Notion
var notionApiKey = builder.Configuration["Notion:ApiKey"] ?? "";
builder.Services.AddSingleton<INotionClient>(_ => NotionClientFactory.Create(new ClientOptions
{
    AuthToken = notionApiKey
}));
builder.Services.AddSingleton<NotionService>();

// Agents
builder.Services.AddScoped<CeoAgentService>();
builder.Services.AddScoped<DevAgentService>();
builder.Services.AddSingleton<OpsAgentService>();

// Dashboard 推送（透過 Aspire Service Discovery 解析 aiteam-dashboard 的 "dashboard" 端點）
// URL 格式：http+{端點名稱}://{服務名稱}，對應 AppHost 的 WithHttpEndpoint(name: "dashboard")
builder.Services.AddHttpClient("aiteam-dashboard", client =>
    client.BaseAddress = new Uri("http+dashboard://aiteam-dashboard"));
builder.Services.AddSingleton<DashboardPushService>();

// GitHub
builder.Services.AddSingleton<GitHubService>();
builder.Services.AddControllers();

// Discord
builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    LogLevel = Discord.LogSeverity.Info,
    GatewayIntents = Discord.GatewayIntents.Guilds
}));
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
        .WithCronSchedule(builder.Configuration["AgentSettings:DailyReportCron"] ?? "0 */30 * * * ?"));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapControllers();

// 啟動時自動套用 EF Core Migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
