var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("aiteam-postgres-data")  // 重啟 Aspire 後資料不消失
    .WithPgAdmin()
    .AddDatabase("AiTeamDb");

// Bot 先啟動，負責執行 AppDbContext Migration
// 明確設定 Development 環境，確保 User Secrets 能被 Bot 讀取
var bot = builder.AddProject<Projects.AiTeam_Bot>("aiteam-bot")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpEndpoint(port: 5050, name: "webhook");

// Dashboard 等 Bot 就緒後再啟動（確保 AppDbContext Migration 已完成）
// 明確設定 Development 環境，確保 User Secrets 能被 Dashboard 讀取
var dashboard = builder.AddProject<Projects.AiTeam_Dashboard>("aiteam-dashboard")
    .WithReference(postgres)
    .WithReference(bot)
    .WaitFor(bot)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpEndpoint(port: 5051, name: "dashboard");

// Bot 需要 Dashboard 的 URL 來推送 Agent 狀態（不 WaitFor，避免啟動順序問題）
bot.WithReference(dashboard);

builder.Build().Run();
