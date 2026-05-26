using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.McpAuth;
using AiTeam.Bot.McpTools;
using AiTeam.Data;
using AiTeam.Data.Extensions;
using AiTeam.Bot.Discord;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Ops;
using AiTeam.Bot.Services;
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
builder.Services.Configure<GitHubSettings>(builder.Configuration.GetSection("GitHub"));
builder.Services.Configure<OpsSettings>(builder.Configuration.GetSection("OpsSettings"));
builder.Services.Configure<WorkflowSettings>(builder.Configuration.GetSection("WorkflowSettings"));

// 核心 service（dashboard / 通知 / token 記錄 / 維運）
builder.Services.AddSingleton<RulesService>();
builder.Services.AddSingleton<AppSettingsService>();
builder.Services.AddSingleton<AlertRateLimiter>();
builder.Services.AddSingleton<DiscordAlertService>();
builder.Services.AddSingleton<TokenCostEstimator>();
builder.Services.AddSingleton<TokenLogService>();
builder.Services.AddSingleton<GitHubService>();
builder.Services.AddSingleton<OpsAgentService>();

// Dashboard 推送
var dashboardPushUrl = builder.Configuration["Dashboard:PushUrl"] ?? "http+dashboard://aiteam-dashboard";
builder.Services.AddHttpClient("aiteam-dashboard", client =>
    client.BaseAddress = new Uri(dashboardPushUrl));
builder.Services.AddSingleton<DashboardPushService>();

builder.Services.AddControllers();

// Stage 90：MCP server（v4-rewrite 核心）— ModelContextProtocol.AspNetCore 1.3.0
// HTTP transport / Bearer auth middleware（重用 AgentSettings.InternalApiKey）/ /mcp route
// Stage 91 補 register_team / record_task / record_conversation / record_token_usage 4 個 record tool
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<HealthCheckTool>();

// Discord（v4-rewrite 暫保留 GatewayIntents 完整 / Stage 93 Discord notification 改造階段重評）
builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    LogLevel       = Discord.LogSeverity.Info,
    GatewayIntents = Discord.GatewayIntents.Guilds
                   | Discord.GatewayIntents.GuildMessages
                   | Discord.GatewayIntents.MessageContent
}));
builder.Services.AddSingleton<ConversationContextStore>();
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

// Stage 90：MCP Bearer auth middleware（必須在 MapMcp 之前 / 只攔 /mcp* path）
app.UseMiddleware<McpBearerAuthMiddleware>();

app.MapDefaultEndpoints();
app.MapControllers();

// Stage 90：MCP server endpoint 掛在 /mcp（HTTP transport / Claude Code .mcp.json type=http url=http://...:5050/mcp）
app.MapMcp("/mcp");

// 啟動時自動套用 EF Core Migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.Run();
