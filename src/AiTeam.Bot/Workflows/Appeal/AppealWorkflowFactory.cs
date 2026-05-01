using System.Text.Json;
using AiTeam.Bot.Workflows.Appeal.Executors;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Appeal;

/// <summary>
/// Stage 49：Build framework Workflow 給 Cody-Vera-Petra ReviewAppeal / Cody-Petra DevPlanAppeal 用。
///
/// 設計（路線 B 拍板，service 包裝）：
///   - Executor 不註冊 DI（驗證 B 結論：framework factory 模式，每次 Build 新建 Executor instance）
///   - Executor ctor 注入 IServiceScopeFactory + ILogger（每個 HandleAsync 內 CreateAsyncScope 取 scoped services）
///   - Workflow Factory 本身 Scoped DI（持有 IServiceScopeFactory + ILoggerFactory + AppealCheckpointStore）
///
/// Workflow 拓撲：
///
/// **ReviewAppeal**（Cody-Vera-Petra Critical Issue 申訴 loop）：
///   start → CodyReviewAppeal → VeraReviewAppeal → Switch by VeraAppealRoundResult:
///                              ├ Approved == true                          → PetraReviewGate    → output
///                              ├ !Approved && Round >= MaxRounds            → PetraReviewArbitr  → output
///                              └ !Approved && Round <  MaxRounds            → CodyReviewAppeal (loop)
///
/// **DevPlanAppeal**（Cody-Petra Dev_plan 申訴 loop，無 Vera）：
///   start → CodyDevPlanAppeal → PetraDevPlanReassess → Switch by DevPlanAppealRoundResult:
///                                ├ Approved == true                         → DevPlanFinalize  → output
///                                ├ !Approved && Round >= MaxRounds           → DevPlanFinalize  → output（escalate verdict）
///                                └ !Approved && Round <  MaxRounds           → CodyDevPlanAppeal (loop)
/// </summary>
public sealed class AppealWorkflowFactory
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly AppealCheckpointStore _checkpointStore;

    public AppealWorkflowFactory(
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        AppealCheckpointStore checkpointStore)
    {
        _scopeFactory = scopeFactory;
        _loggerFactory = loggerFactory;
        _checkpointStore = checkpointStore;
    }

    /// <summary>
    /// Build ReviewAppeal Workflow（Cody-Vera-Petra Appeal loop）。
    /// </summary>
    public Workflow CreateReviewAppealWorkflow()
    {
        var cody = new CodyReviewAppealExecutor(
            _scopeFactory,
            _loggerFactory.CreateLogger<CodyReviewAppealExecutor>());
        var vera = new VeraReviewAppealExecutor(
            _scopeFactory,
            _loggerFactory.CreateLogger<VeraReviewAppealExecutor>());
        var petraGate = new PetraReviewGateExecutor(
            _scopeFactory,
            _loggerFactory.CreateLogger<PetraReviewGateExecutor>());
        var petraArbitration = new PetraReviewArbitrationExecutor(
            _scopeFactory,
            _loggerFactory.CreateLogger<PetraReviewArbitrationExecutor>());

        return new WorkflowBuilder(cody)
            .AddEdge(cody, vera)
            .AddSwitch(vera, sw => sw
                .AddCase<VeraAppealRoundResult>(
                    v => v?.Approved == true,
                    petraGate)
                .AddCase<VeraAppealRoundResult>(
                    v => v?.Approved == false && v.Round >= v.MaxRounds,
                    petraArbitration)
                .AddCase<VeraAppealRoundResult>(
                    v => v?.Approved == false && v.Round <  v.MaxRounds,
                    cody))
            .WithOutputFrom(petraGate, petraArbitration)
            .Build();
    }

    /// <summary>
    /// Build DevPlanAppeal Workflow（Cody-Petra Dev_plan Appeal loop）。
    /// </summary>
    public Workflow CreateDevPlanAppealWorkflow()
    {
        var cody = new CodyDevPlanAppealExecutor(
            _scopeFactory,
            _loggerFactory.CreateLogger<CodyDevPlanAppealExecutor>());
        var petraReassess = new PetraDevPlanReassessExecutor(
            _scopeFactory,
            _loggerFactory.CreateLogger<PetraDevPlanReassessExecutor>());
        var finalize = new DevPlanAppealFinalizeExecutor(
            _scopeFactory,
            _loggerFactory.CreateLogger<DevPlanAppealFinalizeExecutor>());

        return new WorkflowBuilder(cody)
            .AddEdge(cody, petraReassess)
            .AddSwitch(petraReassess, sw => sw
                .AddCase<DevPlanAppealRoundResult>(
                    r => r?.Approved == true,
                    finalize)
                .AddCase<DevPlanAppealRoundResult>(
                    r => r?.Approved == false && r.Round >= r.MaxRounds,
                    finalize)
                .AddCase<DevPlanAppealRoundResult>(
                    r => r?.Approved == false && r.Round <  r.MaxRounds,
                    cody))
            .WithOutputFrom(finalize)
            .Build();
    }

    /// <summary>
    /// 建立 framework CheckpointManager（綁 AppealCheckpointStore）。
    /// FrameworkAppealRouter 用此 manager 跑 InProcessExecution.RunAsync(...) / ResumeAsync(...)。
    /// </summary>
    public CheckpointManager CreateCheckpointManager()
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // 對 enum 用字串而非數字（state 內 AppealLoopKind 等枚舉）
            Converters =
            {
                new System.Text.Json.Serialization.JsonStringEnumConverter(),
            },
        };
        return CheckpointManager.CreateJson(_checkpointStore, jsonOptions);
    }

    public AppealCheckpointStore CheckpointStore => _checkpointStore;
}
