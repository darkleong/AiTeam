using AiTeam.Bot;
using AiTeam.Bot.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// PostgreSQL via Aspire
builder.AddNpgsqlDbContext<AppDbContext>("AiTeamDb");

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
