using System.Text.Json;
using System.Text.Json.Serialization;
using AiTeam.Bot.Workflows.Pipeline.Executors;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;

namespace AiTeam.Bot.Workflows.Pipeline;

/// <summary>
/// Stage 53A：Build framework Workflow 給 Pipeline macro-orchestration（v4 漸進遷移第五步）。
///
/// Aria 方案 C 拍板（2026-05-03）：53A 範圍縮小 — Pipeline 主 Workflow 從 Dev_plan 階段啟動（Kickoff/Design 留 legacy；
/// Stage 55 收尾統一整合 Kickoff/Design 進 Pipeline framework + sub-task 機制）。
///
/// Workflow 拓撲（7 stage Executor + 1 fallback Executor + 5 RequestPort）：
///
///   PipelineStartExecutor
///        ↓ AddEdge (DevPlanStageBridge)
///   DevPlanStageExecutor (dual handler entry + completion)
///        ├ AddEdge (DevPlanCompletionRequest) → devPlanPort (RequestPort)
///        │     ↓ devPlanPort response (DevPlanCompletionResponse)
///        │   AddEdge (devPlanPort, DevPlanStageExecutor) → HandleResponseAsync
///        ├ AddEdge (DevStageBridge) → DevStageExecutor
///        └ AddEdge (PipelineFallbackBridge) → PipelineFallbackExecutor
///   DevStageExecutor (dual handler) — 同上 RequestPort + 兩出口
///   ReviewerStageExecutor (dual handler + 同步 RunPetraGateAsync C2/I2 整合)
///   QaStageExecutor (dual handler + 同步 HandleQaCompletedAsync C2 整合)
///   DocStageExecutor (dual handler)
///        ↓ AddEdge (NotifyMergeStageBridge)
///   NotifyMergeStageExecutor (同步 NotifyBossMergeAsync + YieldOutputAsync)
///        ↓ PipelineLoopResult (Completed=true)
///   PipelineFallbackExecutor (收 PipelineFallbackBridge → YieldOutputAsync Completed=false)
///        ↓ PipelineLoopResult (Completed=false + FallbackReason)
///
/// type-explicit Bridge record 紀律（Stage 52 fix#2 教訓延續）：
///   - 9 個 Bridge record 各自獨立型別（PipelineStartBridge / DevPlanStageBridge / ... / NotifyMergeStageBridge / PipelineFallbackBridge）
///   - 5 個 Agent 型 stage 各自獨立 RequestPort 型別（DevPlanCompletion + 4 同類）— 避免 type-based dispatch collision
///
/// 設計（對齊 Stage 50 KickoffWorkflowFactory pattern）：
///   - Executor 不註冊 DI（factory 模式，每次 Build 新建 Executor instance）
///   - Executor ctor 注入 IServiceScopeFactory + ILogger（每個 HandleAsync 內 CreateAsyncScope 取 scoped services）
///   - Workflow Factory 本身 Singleton（持有 IServiceScopeFactory + ILoggerFactory + PipelineCheckpointStore）
///   - RequestPort：原生 RequestPort.Create&lt;TReq, TResp&gt;（沿用 Stage 51 試點 know-how）
/// </summary>
public sealed class PipelineWorkflowFactory
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly PipelineCheckpointStore _checkpointStore;

    public PipelineWorkflowFactory(
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        PipelineCheckpointStore checkpointStore)
    {
        _scopeFactory = scopeFactory;
        _loggerFactory = loggerFactory;
        _checkpointStore = checkpointStore;
    }

    /// <summary>
    /// Build Pipeline Workflow（5 Agent stage yield-resume + Start/NotifyMerge + Fallback）。
    /// </summary>
    public Workflow CreatePipelineWorkflow()
    {
        var start         = new PipelineStartExecutor(_scopeFactory, _loggerFactory.CreateLogger<PipelineStartExecutor>());
        var devPlanStage  = new DevPlanStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<DevPlanStageExecutor>());
        var devStage      = new DevStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<DevStageExecutor>());
        var reviewerStage = new ReviewerStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<ReviewerStageExecutor>());
        var qaStage       = new QaStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<QaStageExecutor>());
        var docStage      = new DocStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<DocStageExecutor>());
        var notifyMerge   = new NotifyMergeStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<NotifyMergeStageExecutor>());
        var fallback      = new PipelineFallbackExecutor(_loggerFactory.CreateLogger<PipelineFallbackExecutor>());

        // 5 個 RequestPort（每 Agent 型 stage 獨立 PortId + 獨立 Request/Response 型別）
        var devPlanPort  = RequestPort.Create<DevPlanCompletionRequest,  DevPlanCompletionResponse> (DevPlanCompletionPortId);
        var devPort      = RequestPort.Create<DevCompletionRequest,      DevCompletionResponse>     (DevCompletionPortId);
        var reviewerPort = RequestPort.Create<ReviewerCompletionRequest, ReviewerCompletionResponse>(ReviewerCompletionPortId);
        var qaPort       = RequestPort.Create<QaCompletionRequest,       QaCompletionResponse>      (QaCompletionPortId);
        var docPort      = RequestPort.Create<DocCompletionRequest,      DocCompletionResponse>     (DocCompletionPortId);

        return new WorkflowBuilder(start)
            // Start → DevPlan
            .AddEdge(start, devPlanStage)
            // DevPlan stage RequestPort 雙向 + 兩出口
            .AddEdge(devPlanStage, devPlanPort)            // DevPlanCompletionRequest → port
            .AddEdge(devPlanPort, devPlanStage)            // DevPlanCompletionResponse → DevPlanStageExecutor.HandleResponseAsync
            .AddEdge(devPlanStage, devStage)               // DevStageBridge passes through
            .AddEdge(devPlanStage, fallback)               // PipelineFallbackBridge passes through
            // Dev stage RequestPort 雙向 + 兩出口
            .AddEdge(devStage, devPort)
            .AddEdge(devPort, devStage)
            .AddEdge(devStage, reviewerStage)              // ReviewerStageBridge
            .AddEdge(devStage, fallback)                   // PipelineFallbackBridge
            // Reviewer stage RequestPort 雙向 + 兩出口
            .AddEdge(reviewerStage, reviewerPort)
            .AddEdge(reviewerPort, reviewerStage)
            .AddEdge(reviewerStage, qaStage)               // QaStageBridge
            .AddEdge(reviewerStage, fallback)              // PipelineFallbackBridge
            // QA stage RequestPort 雙向 + 兩出口
            .AddEdge(qaStage, qaPort)
            .AddEdge(qaPort, qaStage)
            .AddEdge(qaStage, docStage)                    // DocStageBridge
            .AddEdge(qaStage, fallback)                    // PipelineFallbackBridge
            // Doc stage RequestPort 雙向 + 兩出口
            .AddEdge(docStage, docPort)
            .AddEdge(docPort, docStage)
            .AddEdge(docStage, notifyMerge)                // NotifyMergeStageBridge
            .AddEdge(docStage, fallback)                   // PipelineFallbackBridge
            // 終結 — NotifyMerge（happy path）和 fallback 都 YieldOutput PipelineLoopResult
            .WithOutputFrom(notifyMerge, fallback)
            .Build();
    }

    /// <summary>5 個 Agent 型 stage 各自獨立 RequestPort PortId 常數（Aria 提醒 1 修正：5 個常數取代單一）。
    /// FrameworkPipelineRouter / ResumeAfterAgentAsync 用以從 ExternalRequest.PortInfo.PortId 篩選事件。</summary>
    public const string DevPlanCompletionPortId  = "Pipeline-DevPlanCompletion";
    public const string DevCompletionPortId      = "Pipeline-DevCompletion";
    public const string ReviewerCompletionPortId = "Pipeline-ReviewerCompletion";
    public const string QaCompletionPortId       = "Pipeline-QaCompletion";
    public const string DocCompletionPortId      = "Pipeline-DocCompletion";

    /// <summary>建立 framework CheckpointManager（綁 PipelineCheckpointStore）。
    /// FrameworkPipelineRouter 用此 manager 跑 InProcessExecution.RunStreamingAsync(...) / ResumeStreamingAsync(...)。
    /// 對齊 Stage 49/50/52 既有 *WorkflowFactory.CreateCheckpointManager pattern。</summary>
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

    public PipelineCheckpointStore CheckpointStore => _checkpointStore;
}
