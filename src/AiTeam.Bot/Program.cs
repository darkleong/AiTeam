using Anthropic.SDK;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Bot.Configuration;
using AiTeam.Data;
using AiTeam.Data.Extensions;
using AiTeam.Bot.Discord;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Ops;
using AiTeam.Bot.Orchestration;
using AiTeam.Bot.Orchestration.Meeting;
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
builder.Services.Configure<WorkflowSettings>(builder.Configuration.GetSection("WorkflowSettings"));
builder.Services.AddSingleton<WorkflowSettingsResolver>();

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

// Stage 35：PmAgentService 拆解為 5 個 service + 1 record 檔（Agents/Pm/）
builder.Services.AddScoped<PmAgentCommons>();
builder.Services.AddScoped<PmReviewService>();
builder.Services.AddScoped<ReviewAppealService>();
builder.Services.AddScoped<DevPlanAppealService>();
builder.Services.AddScoped<PmRoutingService>();

// Discord 警報服務（向 #警報 頻道發送 Token 異常等警報）
builder.Services.AddSingleton<DiscordAlertService>();

// Dashboard 推送（本機 Aspire 用 http+dashboard://，Docker 用 Dashboard:PushUrl 設定）
var dashboardPushUrl = builder.Configuration["Dashboard:PushUrl"] ?? "http+dashboard://aiteam-dashboard";
builder.Services.AddHttpClient("aiteam-dashboard", client =>
    client.BaseAddress = new Uri(dashboardPushUrl));
builder.Services.AddSingleton<DashboardPushService>();
// Stage 28a：BossInteraction 寫入與 Discord 回覆同步
builder.Services.AddSingleton<InteractionService>();

// GitHub
builder.Services.AddSingleton<GitHubService>();

// Stage 11：Claude Code subprocess 封裝（供 DevAgentService 使用）
// Stage 17：Proxy pattern — ClaudeCodeProxy 依 MockMode flag 路由到 real 或 mock
builder.Services.AddSingleton<ClaudeCodeService>();
builder.Services.AddSingleton<MockClaudeCodeService>();
builder.Services.AddSingleton<IClaudeCodeService, ClaudeCodeProxy>();
builder.Services.AddControllers();

// Stage 10：CEO Orchestrator（WorkflowEngine 無狀態，TaskGroupService 管理群組流程）
// TaskGroupService 所有建構子依賴均為 Singleton，可安全設為 Singleton（供 CommandHandler 直接注入）
builder.Services.AddSingleton<WorkflowEngine>();
// Stage 34：MeetingService 拆解（FF 二十-C）— MeetingCommons 先，Kickoff/Design 後（兩者依賴 Commons）
builder.Services.AddSingleton<MeetingCommons>();
builder.Services.AddSingleton<KickoffMeetingService>();
builder.Services.AddSingleton<DesignMeetingService>();
// Stage 36：TaskGroupService 拆解（FF 二十-A/B 合併）— 4 子 Orchestration service 先註冊
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Meeting.MeetingOrchestrationService>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Appeal.AppealOrchestrationService>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Qa.QaCoordinationService>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Proposal.ProposalConfirmationService>();
// Stage 27a：Agent 佇列機制（AgentQueueProcessor 同時以 Singleton + HostedService 兩種方式註冊，共用同一實例）
builder.Services.AddSingleton<AgentQueueService>();
builder.Services.AddSingleton<AgentQueueProcessor>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentQueueProcessor>());
builder.Services.AddSingleton<TaskGroupService>();
builder.Services.AddSingleton<MockScenarioService>();
// Stage 33：佇列控制 shared service（Discord 指令 + Dashboard Internal API 共用）
builder.Services.AddSingleton<AgentQueueControlService>();

// Stage 28a：Dashboard 回覆消費器
builder.Services.AddSingleton<InteractionProcessor>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<InteractionProcessor>());

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
// Stage 36：Discord Routing 拆解（FF 二十-A/B 合併）— Store 先 → ButtonRouter → SlashRouter → CommandHandler
builder.Services.AddSingleton<AiTeam.Bot.Discord.Routing.PendingConfirmationStore>();
builder.Services.AddSingleton<AiTeam.Bot.Discord.Routing.ButtonCallbackRouter>();
builder.Services.AddSingleton<AiTeam.Bot.Discord.Routing.SlashCommandRouter>();
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
