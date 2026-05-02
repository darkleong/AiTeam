using System.Text.Json;
using System.Text.Json.Serialization;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Workflows.Design.Executors;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;

namespace AiTeam.Bot.Workflows.Design;

/// <summary>
/// Stage 52：Build framework Workflow 給 Design Meeting B3 路線（v4 漸進遷移第四步）。
///
/// 拓撲（spike F1/F2 拍板）：
///
///   （前置段，線性串接）
///   DesignStartExecutor (StartExecutor，接 router initial DesignState)
///      ↓ AddEdge (DesignPreWorkBridge phase="initial")
///   DesignPetraJudgeExecutor (Petra needsDemi 判斷，isFirstMessage:true)
///      ↓ AddEdge (DesignPreWorkBridge phase="after_judge")
///   DesignRosaPreWorkExecutor (Rosa Issues + GitHub Issue 建立)
///      ↓ AddEdge (DesignPreWorkBridge phase="after_rosa")
///   DesignDemiPreWorkExecutor
///      ├ needsDemi=true  → 跑 LLM call → SendMessageAsync(DesignPreWorkBridge phase="after_demi")
///      └ needsDemi=false → short-circuit SendMessageAsync(DesignPreWorkBridge phase="after_demi")
///      ↓ AddEdge (DesignPreWorkBridge)
///   DesignRoundStartExecutor (mainStart，雙 [MessageHandler])
///      ├ HandleAfterPreWorkAsync(DesignPreWorkBridge) → state.Round=1 → SendMessageAsync(DesignState)
///      └ HandleLoopBackAsync(DesignPetraVerdict from needs_discussion 路徑) → state.Round+=1 → SendMessageAsync(DesignState)
///           ↓ AddFanOutEdge (DesignState)
///           ├→ DesignAgentExecutor[Rosa]   ┐
///           ├→ DesignAgentExecutor[Demi]   │ Demi 條件式：DemiSessionId is null → short-circuit
///           ├→ DesignAgentExecutor[Cody]   ├→ AddFanInBarrierEdge → DesignAggregator
///           └→ DesignAgentExecutor[Quinn]  ┘                                ↓ AddEdge (DesignRoundCollected)
///                                                                     DesignPetraExecutor (Petra round 整理 + 解析 decision)
///                                                                               ↓ AddSwitch (DesignPetraVerdict)
///                                                                               ├ consensus              → DesignPlanExecutor.HandleVerdictAsync → output
///                                                                               ├ needs_discussion < max → DesignRoundStartExecutor (loop back)
///                                                                               ├ needs_discussion >= max → DesignPlanExecutor.HandleVerdictAsync (max_iter → output)
///                                                                               ├ needs_adjustment       → DesignAdjustmentExecutor (B2 wrapper)
///                                                                               │                           ├ approved      → DesignPlanExecutor.HandleAdjustmentApprovedAsync → output
///                                                                               │                           └ needs_meeting → AddSwitch 共用既有 case：
///                                                                               │                              · state.Round >= MaxRounds → DesignEscalateExecutor (議題 6 escalate 邊界)
///                                                                               │                              · state.Round < MaxRounds → DesignRoundStartExecutor (loop back round+1)
///                                                                               └ escalate                → DesignEscalateExecutor → output
///
/// 設計：
///   - Executor 不註冊 DI（factory 模式，每次 Build 新建 Executor instance）
///   - Executor ctor 注入 IServiceScopeFactory + ILogger（每個 HandleAsync 內 CreateAsyncScope 取 scoped services）
///   - Workflow Factory 本身 Singleton（持有 IServiceScopeFactory + ILoggerFactory + DesignCheckpointStore）
/// </summary>
public sealed class DesignWorkflowFactory
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly DesignCheckpointStore _checkpointStore;

    public DesignWorkflowFactory(
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        DesignCheckpointStore checkpointStore)
    {
        _scopeFactory = scopeFactory;
        _loggerFactory = loggerFactory;
        _checkpointStore = checkpointStore;
    }

    /// <summary>
    /// Build Design Meeting Workflow（前置段線性 + 主迴圈 fan-out/fan-in + B2 needs_adjustment + escalate）。
    /// </summary>
    public Workflow CreateDesignWorkflow()
    {
        // 前置段
        var start      = new DesignStartExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<DesignStartExecutor>());
        var petraJudge = new DesignPetraJudgeExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<DesignPetraJudgeExecutor>());
        var rosaPre    = new DesignRosaPreWorkExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<DesignRosaPreWorkExecutor>());
        var demiPre    = new DesignDemiPreWorkExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<DesignDemiPreWorkExecutor>());

        // 主迴圈
        var roundStart = new DesignRoundStartExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<DesignRoundStartExecutor>());
        var rosa  = new DesignAgentExecutor(
            "Design-Rosa",  "Rosa",  "Requirements", MeetingCommons.ReadOnlyTools, _scopeFactory, _loggerFactory);
        var demi  = new DesignAgentExecutor(
            "Design-Demi",  "Demi",  "Designer",     MeetingCommons.ReadOnlyTools, _scopeFactory, _loggerFactory);
        // Cody Design 階段允許讀 codebase 確認技術細節，沿用 ReadOnlyTools（對齊 legacy DesignMeetingService.cs:194 allowedTools: null
        // 但 Stage 52 統一用 ReadOnlyTools 對齊 fan-out 紀律 — 若實作期 Mock 觀察到 Cody 需 write tool 再放寬）
        var cody  = new DesignAgentExecutor(
            "Design-Cody",  "Cody",  "Dev",          allowedTools: null, _scopeFactory, _loggerFactory);
        var quinn = new DesignAgentExecutor(
            "Design-Quinn", "Quinn", "QA",           MeetingCommons.ReadOnlyTools, _scopeFactory, _loggerFactory);
        var aggr  = new DesignAggregator(
            _scopeFactory, _loggerFactory.CreateLogger<DesignAggregator>());
        var petra = new DesignPetraExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<DesignPetraExecutor>());
        var adjust = new DesignAdjustmentExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<DesignAdjustmentExecutor>());
        var plan   = new DesignPlanExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<DesignPlanExecutor>());
        // 驗收期 follow-up #2：拆 adjustment_approved 路徑成獨立 Executor，避免 framework AddEdge type-based dispatch
        // 把 adjust needs_meeting 路徑送的 DesignPetraVerdict 誤觸發 plan（造成 plan 跑 LLM + state 同 superstep 衝突）
        var adjPlan = new DesignAdjustmentPlanExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<DesignAdjustmentPlanExecutor>());
        var esc    = new DesignEscalateExecutor(
            _scopeFactory, _loggerFactory.CreateLogger<DesignEscalateExecutor>());

        return new WorkflowBuilder(start)
            // 前置段（線性串接）
            .AddEdge(start, petraJudge)
            .AddEdge(petraJudge, rosaPre)
            .AddEdge(rosaPre, demiPre)
            .AddEdge(demiPre, roundStart)
            // 主迴圈 fan-out / fan-in / Petra
            .AddFanOutEdge(roundStart, [rosa, demi, cody, quinn])
            .AddFanInBarrierEdge([rosa, demi, cody, quinn], aggr)
            .AddEdge(aggr, petra)
            // 主迴圈 routing（5 分支：consensus / needs_discussion < max / needs_discussion >= max / needs_adjustment / escalate）
            .AddSwitch(petra, sw => sw
                .AddCase<DesignPetraVerdict>(
                    v => v?.Decision == "consensus",
                    plan)
                .AddCase<DesignPetraVerdict>(
                    v => v?.Decision == "needs_discussion" && v.Round < v.MaxRounds,
                    roundStart)   // loop back
                .AddCase<DesignPetraVerdict>(
                    v => v?.Decision == "needs_discussion" && v.Round >= v.MaxRounds,
                    plan)         // max_iter 強制結束
                .AddCase<DesignPetraVerdict>(
                    v => v?.Decision == "needs_adjustment",
                    adjust)       // B2 wrapper
                .AddCase<DesignPetraVerdict>(
                    v => v?.Decision == "escalate",
                    esc))
            // adjust 兩出口（議題 6+7 必修；驗收期 follow-up #2：拆 plan 後 type filter 自然分流）
            .AddEdge(adjust, adjPlan)      // approved → DesignAdjustmentApproved → DesignAdjustmentPlanExecutor（type filter 自然分流，不會誤觸發 plan）
            // needs_meeting 路徑：DesignAdjustmentExecutor 送 DesignPetraVerdict
            //   - escalate（state.Round >= MaxRounds 邊界）→ DesignEscalateExecutor
            //   - needs_discussion < max → DesignRoundStartExecutor loop back
            // 驗收期 follow-up #2：plan 已拆只接 DesignPetraVerdict（main loop），但 adjust 也送 DesignPetraVerdict —
            // AddEdge(adjust, adjPlan) 不會誤觸發 plan（adjPlan 沒 DesignPetraVerdict handler），但 AddSwitch case 必須完整路由
            .AddSwitch(adjust, sw => sw
                .AddCase<DesignPetraVerdict>(
                    v => v?.Decision == "escalate",
                    esc)
                .AddCase<DesignPetraVerdict>(
                    v => v?.Decision == "needs_discussion" && v.Round < v.MaxRounds,
                    roundStart))
            .WithOutputFrom(plan, adjPlan, esc)
            .Build();
    }

    /// <summary>
    /// 建立 framework CheckpointManager（綁 DesignCheckpointStore）。
    /// FrameworkDesignRouter 用此 manager 跑 InProcessExecution.RunStreamingAsync(...)。
    /// 對齊 Stage 49/50 既有 CreateCheckpointManager pattern。
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

    public DesignCheckpointStore CheckpointStore => _checkpointStore;
}
