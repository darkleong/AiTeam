using Anthropic.SDK;
using Microsoft.EntityFrameworkCore;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Data;
using AiTeam.Bot.Discord;
using AiTeam.Bot.Notion;
using Discord.WebSocket;
using Notion.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// PostgreSQL via Aspire
builder.AddNpgsqlDbContext<AppDbContext>("AiTeamDb");

// 設定
builder.Services.Configure<DiscordSettings>(builder.Configuration.GetSection("Discord"));
builder.Services.Configure<AgentSettings>(builder.Configuration.GetSection("AgentSettings"));
builder.Services.Configure<AgentSettings>(o =>
    builder.Configuration.GetSection("Agents").Bind(o.Agents));
builder.Services.Configure<NotionSettings>(builder.Configuration.GetSection("Notion"));

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

// Data
builder.Services.AddScoped<TaskRepository>();

// Agents
builder.Services.AddScoped<CeoAgentService>();

// Discord
builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    LogLevel = Discord.LogSeverity.Info,
    GatewayIntents = Discord.GatewayIntents.Guilds
}));
builder.Services.AddSingleton<CommandHandler>();
builder.Services.AddHostedService<DiscordBotService>();

var host = builder.Build();

// 啟動時自動套用 EF Core Migrations
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();
