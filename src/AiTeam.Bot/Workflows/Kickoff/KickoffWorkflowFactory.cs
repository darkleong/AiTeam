using System.Text.Json;
using System.Text.Json.Serialization;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Workflows.Kickoff.Executors;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;

namespace AiTeam.Bot.Workflows.Kickoff;

/// <summary>
/// Stage 50：Build framework Workflow 給 Kickoff Meeting 5 Agent 會議（Rosa/Demi/Cody/Quinn/Petra）。
///
/// 路線拍板（spike 第一步驗證後）：A2 fallback — `WorkflowBuilder` + `AddFanOutEdge` + `AddFanInBarrierEdge` + `AddSwitch` + loop back。
///
/// E1 結論：framework Group Chat custom manager 不支援 multi-speaker per round（star topology + single-speaker turn-by-turn）；
/// E2 結論：ICheckpointStore<JsonElement> 對 fan-out/fan-in 與 Stage 49 WorkflowBuilder pattern 通用；
/// E3 結論：5 個並行 ClaudeCodeService subprocess 並行度 = OS-level（與既有 Task.WhenAll 等價）。
///
/// Workflow 拓撲：
///
///   start (KickoffState) → KickoffStartExecutor (broadcast)
///                            ↓ AddFanOutEdge
///                            ├→ RosaKickoffExecutor   ┐
///                            ├→ DemiKickoffExecutor   │
///                            ├→ CodyKickoffExecutor   ├→ AddFanInBarrierEdge → KickoffAggregator → AddEdge → KickoffPetraExecutor
///                            └→ QuinnKickoffExecutor  ┘
///                                                                                                            ↓ KickoffPetraVerdict
///                                                                                                         AddSwitch:
///                                                                                                          ├ consensus              → KickoffPlanExecutor → output
///                                                                                                          ├ needs_discussion < max → KickoffStartExecutor (loop back)
///                                                                                                          ├ needs_discussion >= max → KickoffPlanExecutor (max_iter)
///                                                                                                          └ escalate                → KickoffEscalateExecutor → output
///
/// 設計：
///   - Executor 不註冊 DI（驗證 B 結論：framework factory 模式，每次 Build 新建 Executor instance）
///   - Executor ctor 注入 IServiceScopeFactory + ILogger（每個 HandleAsync 內 CreateAsyncScope 取 scoped services）
///   - Workflow Factory 本身 Singleton（持有 IServiceScopeFactory + ILoggerFactory + KickoffCheckpointStore）
/// </summary>
public sealed class KickoffWorkflowFactory
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly KickoffCheckpointStore _checkpointStore;

    public KickoffWorkflowFactory(
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        KickoffCheckpointStore checkpointStore)
    {
        _scopeFactory = scopeFactory;
        _loggerFactory = loggerFactory;
        _checkpointStore = checkpointStore;
    }

    /// <summary>
    /// Build Kickoff Meeting Workflow（5 Agent fan-out/fan-in + Petra Switch + loop back）。
    /// </summary>
    public Workflow CreateKickoffWorkflow()
    {
        var start = new KickoffStartExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<KickoffStartExecutor>());
        var rosa  = new KickoffAgentExecutor(
            "Rosa-Kickoff",  "Rosa",  "Requirements", MeetingCommons.ReadOnlyTools, _scopeFactory, _loggerFactory);
        var demi  = new KickoffAgentExecutor(
            "Demi-Kickoff",  "Demi",  "Designer",     MeetingCommons.ReadOnlyTools, _scopeFactory, _loggerFactory);
        // Cody 無限制 tools（深入探 codebase，對齊 legacy KickoffMeetingService.RunKickoffMeetingAsync line 108 allowedTools: null）
        var cody  = new KickoffAgentExecutor(
            "Cody-Kickoff",  "Cody",  "Dev",          allowedTools: null,           _scopeFactory, _loggerFactory);
        var quinn = new KickoffAgentExecutor(
            "Quinn-Kickoff", "Quinn", "QA",           MeetingCommons.ReadOnlyTools, _scopeFactory, _loggerFactory);
        var aggr  = new KickoffAggregator(
            _scopeFactory, _loggerFactory.CreateLogger<KickoffAggregator>());
        var petra = new KickoffPetraExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<KickoffPetraExecutor>());
        var plan  = new KickoffPlanExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<KickoffPlanExecutor>());
        var esc   = new KickoffEscalateExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<KickoffEscalateExecutor>());

        return new WorkflowBuilder(start)
            .AddFanOutEdge(start, [rosa, demi, cody, quinn])
            .AddFanInBarrierEdge([rosa, demi, cody, quinn], aggr)
            .AddEdge(aggr, petra)
            .AddSwitch(petra, sw => sw
                .AddCase<KickoffPetraVerdict>(
                    v => v?.Decision == "consensus",
                    plan)
                .AddCase<KickoffPetraVerdict>(
                    v => v?.Decision == "needs_discussion" && v.Round < v.MaxRounds,
                    start)   // loop back
                .AddCase<KickoffPetraVerdict>(
                    v => v?.Decision == "needs_discussion" && v.Round >= v.MaxRounds,
                    plan)    // max_iter 強制結束
                .AddCase<KickoffPetraVerdict>(
                    v => v?.Decision == "escalate",
                    esc))
            .WithOutputFrom(plan, esc)
            .Build();
    }

    /// <summary>
    /// 建立 framework CheckpointManager（綁 KickoffCheckpointStore）。
    /// FrameworkKickoffRouter 用此 manager 跑 InProcessExecution.RunAsync(...) / ResumeAsync(...)。
    /// 對齊 Stage 49 AppealWorkflowFactory.CreateCheckpointManager pattern。
    /// </summary>
    public CheckpointManager CreateCheckpointManager()
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(),
            },
        };
        return CheckpointManager.CreateJson(_checkpointStore, jsonOptions);
    }

    public KickoffCheckpointStore CheckpointStore => _checkpointStore;
}
