using Anthropic.SDK;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Data;
using AiTeam.Data.Extensions;
using AiTeam.Bot.Discord;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Ops;
using AiTeam.Bot.Orchestration.Petra;
using AiTeam.Bot.Services;
using AiTeam.Data.Repositories;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

// Gemini（Stage 37-1 / FF 四第一階段：API 層 Agent 可選 Google Gemini Flash）
// HttpClient BaseAddress 末尾保留 slash；GeminiProvider 內相對路徑不可開頭加 slash。
builder.Services.AddHttpClient("Gemini", c =>
{
    var baseUrl = (builder.Configuration["Gemini:BaseUrl"]
                   ?? "https://generativelanguage.googleapis.com/v1").TrimEnd('/');
    c.BaseAddress = new Uri(baseUrl + "/");
});

builder.Services.AddScoped<LlmProviderFactory>();

// Rules（取代 Notion，從 PostgreSQL 讀取）
builder.Services.AddSingleton<RulesService>();
// 動態系統設定（TTL cache，免重啟生效）
builder.Services.AddSingleton<AppSettingsService>();
// Stage 38：Agent Provider/Model 動態設定快取（DB 權威，Dashboard 可改，TTL 5 分）
builder.Services.AddSingleton<AgentConfigCache>();

// Agents（v5.5 production active 6 Talent baseline）
// Stage 78a：v4 path 整套砍 — 3 純 v4 class（Rosa/Demi/Release）砍 + 4 雙路徑 class（Doc/Dev/Reviewer/Qa）砍 v4 IAgentExecutor 實作留 v5.5 IAgentTool
// Stage 78b：v4 path dead caller 整套砍 — ButtonCallbackRouter v4 routing + OpsAgent IAgentExecutor 實作 + /task slash command + GitHub Issue webhook + CeoAgentService.ProcessAsync
// Stage 78c：v4 Pipeline framework 整套砍 — WorkflowEngine + Pipeline/Meeting/Appeal/HITL/Queue/Group services + InteractionProcessor + ProposalConfirmationService + ButtonCallbackRouter v4 routing 整套 + SlashCommandRouter + WebhookController + MockScenarioService + IAgentExecutor + AgentExecutionResult + AgentResultType（留 AgentDescriptor v5.5 active）+ Pm folder
builder.Services.AddScoped<CeoAgentService>();
builder.Services.AddScoped<DevAgentService>();
builder.Services.AddSingleton<OpsAgentService>();                          // Singleton：HealthCheckJob 相依（class 仍 Singleton 供 Quartz scheduled job 用）
builder.Services.AddScoped<QaAgentService>();
builder.Services.AddScoped<DocAgentService>();
builder.Services.AddScoped<ReviewerAgentService>();

// Stage 63B：Worker IAgentTool multi-registration（v5 動態架構 — Petra Orchestrator 透過 IEnumerable<IAgentTool> DI scan 取所有 Worker）
// Stage 78a：縮為 4 Worker（Cody/Vera/Quinn/Sage）— 砍 Rosa/Demi/Release（不在 v5.5 6 Talent baseline / Trial_v6-v22 連續 17 次 0 dispatch 累積）
builder.Services.AddScoped<IAgentTool>(sp => sp.GetRequiredService<DevAgentService>());
builder.Services.AddScoped<IAgentTool>(sp => sp.GetRequiredService<ReviewerAgentService>());
builder.Services.AddScoped<IAgentTool>(sp => sp.GetRequiredService<QaAgentService>());
builder.Services.AddScoped<IAgentTool>(sp => sp.GetRequiredService<DocAgentService>());

// Stage 63B：Petra Orchestrator + Session Repository + Recovery hosted service（v5 動態架構 PoC）
builder.Services.AddScoped<PetraSessionRepository>();
builder.Services.AddScoped<PetraOrchestratorService>();
builder.Services.AddHostedService<PetraSessionRecoveryService>();

// Stage 75：v5.5 Phase 3 — PetraInbox 接收層 queue（Layer 1）
// PetraInboxRepository 已由 AddAiTeamData extension 註冊（Bot + Dashboard 共用 Repository pattern）
// Stage 77：v5.5 Phase 3 補強 — fire-and-forget A2 完整版（Channel + multi-consumer + bounded fan-out + graceful shutdown drain）
//   PetraInboxChannel Singleton（process-wide Bounded queue / Capacity=20 / FullMode=Wait）
//   PetraInboxProcessor 退化為 pure producer（poll DB → push channel）
//   PetraDispatchWorker N=3 default consumer loop（multi-consumer Task.WhenAll + Stage 76 retry path 整套搬遷）
builder.Services.AddSingleton<PetraInboxChannel>();
builder.Services.AddHostedService<PetraInboxProcessor>();
builder.Services.AddHostedService<PetraDispatchWorker>();

// Stage 69：v5.5 Phase 2 Step 3 — 跨 session 長期持久記憶 Repository
builder.Services.AddScoped<MemoryRepository>();

// Stage 72：v5.5 Phase 2 Step 5 — Prompt DB 化（SkillPrompt + TalentPrompt 兩層 schema）
// PromptRepository（Scoped — 對齊 MemoryRepository 既有 lifecycle）+ PromptResolver（Singleton + 5-min TTL cache + IServiceScopeFactory 解 Singleton-Scoped 雷 — 對齊 AppSettingsService pattern）
builder.Services.AddScoped<PromptRepository>();
builder.Services.AddSingleton<PromptResolver>();

// Stage 74：v5.5 Phase 3 Step 8 — per-Skill Model 三層 fallback chain（Singleton + 5-min TTL cache + IServiceScopeFactory 對齊 PromptResolver pattern）
builder.Services.AddSingleton<TalentSkillModelResolver>();

// Stage 75：v5.5 Phase 3 — per-Talent serialization lock（Singleton + ConcurrentDictionary<Guid, SemaphoreSlim>）
builder.Services.AddSingleton<TalentDispatchLockService>();

// Stage 67：v5.5 Phase 1 Step 2 — Skill registry (code-defined / Singleton) + Talent factory (runtime DB query / Singleton + IServiceScopeFactory)
// Talent register 走 ITalentFactory.GetAllAsync(ct) 取代「DI scan IEnumerable<ITalent>」pattern — 解 app.Build 時 DB 還沒 ready 的時序問題 + Phase 3 dynamic CRUD 自然解
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Petra.Skills.ISkillRegistry, AiTeam.Bot.Orchestration.Petra.Skills.DefaultSkillRegistry>();
builder.Services.AddSingleton<ITalentFactory, DefaultTalentFactory>();

// Stage 78c 議題 8：Pm/ folder 整套砍（PmAgentCommons / PmReviewService / ReviewAppealService / DevPlanAppealService / PmRoutingService — 100% v4 path / 0 v5.5 caller）

// Discord 警報服務（向 #警報 頻道發送 Token 異常等警報）
builder.Services.AddSingleton<DiscordAlertService>();

// Dashboard 推送（本機 Aspire 用 http+dashboard://，Docker 用 Dashboard:PushUrl 設定）
var dashboardPushUrl = builder.Configuration["Dashboard:PushUrl"] ?? "http+dashboard://aiteam-dashboard";
builder.Services.AddHttpClient("aiteam-dashboard", client =>
    client.BaseAddress = new Uri(dashboardPushUrl));
builder.Services.AddSingleton<DashboardPushService>();
// Stage 56：FF 四十三 修 — TotalCostUsd fallback 估算 helper（CLI / API path 共用）
builder.Services.AddSingleton<TokenCostEstimator>();
// Stage 44：CLI Agent token 寫入共用 helper（內建 try-catch 不阻塞主流程，獨立 scope DbContext）
builder.Services.AddSingleton<TokenLogService>();
// Stage 28a：BossInteraction 寫入與 Discord 回覆同步
builder.Services.AddSingleton<InteractionService>();

// GitHub
builder.Services.AddSingleton<GitHubService>();

// Stage 11：Claude Code subprocess 封裝（供 DevAgentService 使用）
// Stage 17：Proxy pattern — ClaudeCodeProxy 依 MockMode flag 路由到 real 或 mock
// Stage 78c 議題 3：MockClaudeCodeService 留（LLM 層 Mock 對 v5.5 path Cody/Vera/Quinn/Sage 仍有效 / forge-self-verify skill 真實依賴）
builder.Services.AddSingleton<ClaudeCodeService>();
builder.Services.AddSingleton<MockClaudeCodeService>();
builder.Services.AddSingleton<IClaudeCodeService, ClaudeCodeProxy>();
builder.Services.AddControllers();

// Stage 78c：v4 Pipeline framework 整套砍 — TaskGroupService / AgentQueueService / AgentQueueProcessor / WorkflowEngine /
//   MeetingOrchestrationService / AppealOrchestrationService / QaCoordinationService / ProposalConfirmationService /
//   BossNotificationService / BossResponseHandlerService / EpicChainService / PipelineRoutingService /
//   InteractionProcessor / MockScenarioService / AgentQueueControlService /
//   Workflows/Pipeline + Kickoff + Design + Appeal/ folder factory+CheckpointStore + Framework*Router /
//   SlashCommandRouter / WebhookController / Pm/ folder
// 對應 DI 註冊整套砍 — v5.5 path（PetraInboxProcessor + PetraDispatchWorker + PetraOrchestratorService）取代

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
// Stage 36：Discord Routing — Store + ButtonRouter + CommandHandler（Stage 78c 議題 4：SlashCommandRouter 整檔砍）
builder.Services.AddSingleton<AiTeam.Bot.Discord.Routing.PendingConfirmationStore>();
builder.Services.AddSingleton<AiTeam.Bot.Discord.Routing.ButtonCallbackRouter>();
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

    // Stage 38：AgentConfig 的 Provider/Model null 欄位從 appsettings 補 seed（已有值不覆蓋）
    // Guard：只當兩欄位同時為 null 才補（避免半途被 Dashboard 改過的列被 appsettings 回補覆蓋另一半）
    // Runtime 仍允許 per-field null fallback（dbOverride ?? configConfig），保留未來擴充空間
    var agentOpts = scope.ServiceProvider.GetRequiredService<IOptions<AgentSettings>>().Value;
    var agentRows = await db.AgentConfigs.ToListAsync();
    foreach (var row in agentRows)
    {
        if (row.Provider is null && row.Model is null
            && agentOpts.Agents.TryGetValue(row.Name, out var src))
        {
            row.Provider = src.Provider;
            row.Model    = src.Model;
        }
    }
    await db.SaveChangesAsync();

    // Stage 38：預熱 AgentConfigCache，避免第一筆任務觸發 sync DB 載入 block 執行緒
    await scope.ServiceProvider.GetRequiredService<AgentConfigCache>().WarmupAsync();
}

app.Run();
