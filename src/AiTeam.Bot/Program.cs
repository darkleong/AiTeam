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
using AiTeam.Bot.Orchestration.Petra;
using AiTeam.Bot.Services;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
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

// Stage 63B：Worker IAgentTool multi-registration（v5 動態架構 PoC — Petra Orchestrator 透過 IEnumerable<IAgentTool> DI scan 取所有 7 Worker）
builder.Services.AddScoped<IAgentTool>(sp => sp.GetRequiredService<DevAgentService>());
builder.Services.AddScoped<IAgentTool>(sp => sp.GetRequiredService<ReviewerAgentService>());
builder.Services.AddScoped<IAgentTool>(sp => sp.GetRequiredService<QaAgentService>());
builder.Services.AddScoped<IAgentTool>(sp => sp.GetRequiredService<DocAgentService>());
builder.Services.AddScoped<IAgentTool>(sp => sp.GetRequiredService<RequirementsAgentService>());
builder.Services.AddScoped<IAgentTool>(sp => sp.GetRequiredService<DesignerAgentService>());
builder.Services.AddScoped<IAgentTool>(sp => sp.GetRequiredService<ReleaseAgentService>());

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

// Stage 75：v5.5 Phase 3 — per-Talent serialization lock（Singleton + ConcurrentDictionary<Guid, SemaphoreSlim> / 議題 2 Christ 拍板 SemaphoreSlim 對齊 AgentQueueProcessor v4 既有紀律）
builder.Services.AddSingleton<TalentDispatchLockService>();

// Stage 67：v5.5 Phase 1 Step 2 — Skill registry (code-defined / Singleton) + Talent factory (runtime DB query / Singleton + IServiceScopeFactory)
// Talent register 走 ITalentFactory.GetAllAsync(ct) 取代「DI scan IEnumerable<ITalent>」pattern — 解 app.Build 時 DB 還沒 ready 的時序問題 + Phase 3 dynamic CRUD 自然解
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Petra.Skills.ISkillRegistry, AiTeam.Bot.Orchestration.Petra.Skills.DefaultSkillRegistry>();
builder.Services.AddSingleton<ITalentFactory, DefaultTalentFactory>();

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
builder.Services.AddSingleton<ClaudeCodeService>();
builder.Services.AddSingleton<MockClaudeCodeService>();
builder.Services.AddSingleton<IClaudeCodeService, ClaudeCodeProxy>();
builder.Services.AddControllers();

// Stage 10：CEO Orchestrator（TaskGroupService 管理群組流程）
// Stage 55A：WorkflowEngine 已刪除（v4 漸進遷移第八步 — Pipeline framework 接管全 routing）
// TaskGroupService 所有建構子依賴均為 Singleton，可安全設為 Singleton（供 CommandHandler 直接注入）
// Stage 34：MeetingService 拆解（FF 二十-C）— MeetingCommons 先，Kickoff/Design 後（兩者依賴 Commons）
builder.Services.AddSingleton<MeetingCommons>();
builder.Services.AddSingleton<KickoffMeetingService>();
// Stage 52：DesignSplitProposalEvaluator 抽出（feature flag legacy + framework 共用 SoT，對齊 DesignMeetingService Singleton lifecycle）
builder.Services.AddSingleton<DesignSplitProposalEvaluator>();
builder.Services.AddSingleton<DesignMeetingService>();
// Stage 36：TaskGroupService 拆解（FF 二十-A/B 合併）— 4 子 Orchestration service 先註冊
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Meeting.MeetingOrchestrationService>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Appeal.AppealOrchestrationService>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Qa.QaCoordinationService>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Proposal.ProposalConfirmationService>();
// Stage 59：TaskGroupService 二次拆解（FF 五十四子項 1）— 4 子 service 對齊 Stage 36 既有 Orchestration 子目錄 single-theme pattern（Boss/Epic/Routing 拆 3 子目錄避免 namespace TaskGroup 與 entity 衝突）
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Boss.BossNotificationService>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Boss.BossResponseHandlerService>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Epic.EpicChainService>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Routing.PipelineRoutingService>();

// Stage 49：v4 漸進遷移首發 — MS Agent Framework Appeal Workflow 整合層
// 設計（驗證 B 結論）：framework Executor 不註冊到 DI（factory 模式由 AppealWorkflowFactory 內 new 出 Executor instance）
// AppealCheckpointStore 持有 in-memory checkpoint dict + 同步寫 task_groups.FrameworkAppealStateJson（Singleton 以保留 process 生命週期 cache）
// AppealWorkflowFactory 無 scoped state，可 Singleton；FrameworkAppealRouter 由此 factory 建 Workflow
// FrameworkAppealRouter 注入 AppealOrchestrationService 走 serviceProvider.GetRequiredService（避免循環依賴）
// feature flag 預設 false（Workflow:UseFrameworkAppealLoop AppSettings key），詳見 WorkflowSettingsResolver
builder.Services.AddSingleton<AiTeam.Bot.Workflows.Appeal.AppealCheckpointStore>();
builder.Services.AddSingleton<AiTeam.Bot.Workflows.Appeal.AppealWorkflowFactory>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Appeal.FrameworkAppealRouter>();
// Stage 50：framework Kickoff Workflow（v4 漸進遷移第二步） — A2 fan-out/fan-in 路線
// KickoffCheckpointStore Singleton（process 生命週期 in-memory cache + 同步寫 task_groups.KickoffFrameworkStateJson）
// KickoffWorkflowFactory 無 scoped state，可 Singleton；FrameworkKickoffRouter 對齊 Stage 49 Singleton 慣例（DI 生命週期驗證見 Plan Mode）
// feature flag 預設 false（Workflow:UseFrameworkKickoff AppSettings key），與 Stage 49 UseFrameworkAppealLoop 完全獨立
builder.Services.AddSingleton<AiTeam.Bot.Workflows.Kickoff.KickoffCheckpointStore>();
builder.Services.AddSingleton<AiTeam.Bot.Workflows.Kickoff.KickoffWorkflowFactory>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Meeting.FrameworkKickoffRouter>();
// Stage 51：framework HITL 試點 ↔ BossInteraction 橋接（v4 漸進遷移第三步）
// KickoffMidInterruptTriggerStore：in-memory trigger flag store（Bot Internal API set / MidInterruptCheckExecutor consume）
// FrameworkHitlBridge：對齊 Stage 49/50 router Singleton 慣例（service locator 解 router 循環依賴）
// feature flag 預設 false（Workflow:UseFrameworkKickoffMidInterrupt AppSettings key），需與 UseFrameworkKickoff 雙 flag 同開才有意義
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Hitl.KickoffMidInterruptTriggerStore>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Hitl.FrameworkHitlBridge>();
// Stage 52：framework Design Workflow（v4 漸進遷移第四步） — fan-out/fan-in + 條件式 Demi + needs_adjustment B2 wrapper
// DesignCheckpointStore Singleton（process 生命週期 in-memory cache + 同步寫 task_groups.DesignFrameworkStateJson）
// DesignWorkflowFactory Singleton；FrameworkDesignRouter 對齊 Stage 49/50 Singleton 慣例
// feature flag 預設 false（Workflow:UseFrameworkDesign AppSettings key），與 Stage 49/50/51 三 flag 完全獨立
builder.Services.AddSingleton<AiTeam.Bot.Workflows.Design.DesignCheckpointStore>();
builder.Services.AddSingleton<AiTeam.Bot.Workflows.Design.DesignWorkflowFactory>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Meeting.FrameworkDesignRouter>();
// Stage 53A：framework Pipeline Workflow（v4 漸進遷移第五步） — macro-orchestration 5 Agent stage（Aria 方案 C 拍板：Kickoff/Design 留 legacy，Stage 55 收尾整合）
// PipelineCheckpointStore Singleton（process 生命週期 in-memory cache + 同步寫 task_groups.PipelineFrameworkStateJson）
// PipelineWorkflowFactory Singleton；FrameworkPipelineRouter 子項 7 寫，預留註冊位置
// feature flag 預設 false（Workflow:UseFrameworkPipeline AppSettings key），三 flag 連動：UseFrameworkKickoff + UseFrameworkDesign 都 true 才有意義
builder.Services.AddSingleton<AiTeam.Bot.Workflows.Pipeline.PipelineCheckpointStore>();
builder.Services.AddSingleton<AiTeam.Bot.Workflows.Pipeline.PipelineWorkflowFactory>();
builder.Services.AddSingleton<AiTeam.Bot.Orchestration.Meeting.FrameworkPipelineRouter>();
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
