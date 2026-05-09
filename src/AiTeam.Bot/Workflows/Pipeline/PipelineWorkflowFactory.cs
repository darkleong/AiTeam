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
    /// Build Pipeline Workflow（Stage 55A：7 Agent stage yield-resume + Start/NotifyMerge + Fallback + Kickoff/Design 整合）。
    ///
    /// Stage 55A 拓撲擴展（議題 G3 解法）：
    ///   - 5→7 RequestPort（加 KickoffCompletion / DesignCompletion）
    ///   - 8→10 stage Executor（加 KickoffStage / DesignStage）
    ///   - PipelineStart 兩出口：parent → KickoffStage / sub-task → DevPlanStage（缺口 2 解法）
    /// </summary>
    public Workflow CreatePipelineWorkflow()
    {
        var start         = new PipelineStartExecutor(_scopeFactory, _loggerFactory.CreateLogger<PipelineStartExecutor>());
        var kickoffStage  = new KickoffStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<KickoffStageExecutor>());     // Stage 55A 新加
        var designStage   = new DesignStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<DesignStageExecutor>());       // Stage 55A 新加
        var devPlanStage  = new DevPlanStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<DevPlanStageExecutor>());
        var devStage      = new DevStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<DevStageExecutor>());
        var reviewerStage = new ReviewerStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<ReviewerStageExecutor>());
        var qaStage       = new QaStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<QaStageExecutor>());
        var docStage      = new DocStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<DocStageExecutor>());
        var devFixStage   = new DevFixStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<DevFixStageExecutor>());  // Stage 53B 新加
        var notifyMerge   = new NotifyMergeStageExecutor(_scopeFactory, _loggerFactory.CreateLogger<NotifyMergeStageExecutor>());
        var fallback      = new PipelineFallbackExecutor(_loggerFactory.CreateLogger<PipelineFallbackExecutor>());

        // 7 個 RequestPort（Stage 55A 新加 KickoffCompletionPortId / DesignCompletionPortId — 5+1+1 entry）
        var kickoffPort  = RequestPort.Create<KickoffCompletionRequest,  KickoffCompletionResponse> (KickoffCompletionPortId);   // Stage 55A
        var designPort   = RequestPort.Create<DesignCompletionRequest,   DesignCompletionResponse>  (DesignCompletionPortId);    // Stage 55A
        var devPlanPort  = RequestPort.Create<DevPlanCompletionRequest,  DevPlanCompletionResponse> (DevPlanCompletionPortId);
        var devPort      = RequestPort.Create<DevCompletionRequest,      DevCompletionResponse>     (DevCompletionPortId);
        var reviewerPort = RequestPort.Create<ReviewerCompletionRequest, ReviewerCompletionResponse>(ReviewerCompletionPortId);
        var qaPort       = RequestPort.Create<QaCompletionRequest,       QaCompletionResponse>      (QaCompletionPortId);
        var docPort      = RequestPort.Create<DocCompletionRequest,      DocCompletionResponse>     (DocCompletionPortId);
        var devFixPort   = RequestPort.Create<DevFixCompletionRequest,   DevFixCompletionResponse>  (DevFixCompletionPortId);  // Stage 53B 新加

        // Stage 55B Session B：5 type-specific intervention HITL RequestPort（議題 2 = 2C Pattern A）
        var devInterventionPort   = RequestPort.Create<DevInterventionRequest,   DevInterventionResponse>  (DevInterventionPortId);
        var qaInterventionPort    = RequestPort.Create<QaInterventionRequest,    QaInterventionResponse>   (QaInterventionPortId);
        var devPlanEscalatePort   = RequestPort.Create<DevPlanEscalateRequest,   DevPlanEscalateResponse>  (DevPlanEscalatePortId);
        var devPlanUnablePort     = RequestPort.Create<DevPlanUnableRequest,     DevPlanUnableResponse>    (DevPlanUnablePortId);
        var splitTaskProposalPort = RequestPort.Create<SplitTaskProposalRequest, SplitTaskProposalResponse>(SplitTaskProposalPortId);

        // Stage 57-FF 五十二：Reviewer fix loop ×3 達 limit RequestPort（第 6 routing）
        var reviewerFixLoopLimitPort = RequestPort.Create<ReviewerFixLoopLimitRequest, ReviewerFixLoopLimitResponse>(ReviewerFixLoopLimitPortId);

        // Stage 58-FF 五十三：Agent API 失敗 per-stage 4 RequestPort（第 7 routing）
        var devAgentApiFailurePort      = RequestPort.Create<DevAgentApiFailureRequest,      DevAgentApiFailureResponse>     (DevAgentApiFailurePortId);
        var reviewerAgentApiFailurePort = RequestPort.Create<ReviewerAgentApiFailureRequest, ReviewerAgentApiFailureResponse>(ReviewerAgentApiFailurePortId);
        var qaAgentApiFailurePort       = RequestPort.Create<QaAgentApiFailureRequest,       QaAgentApiFailureResponse>      (QaAgentApiFailurePortId);
        var docAgentApiFailurePort      = RequestPort.Create<DocAgentApiFailureRequest,      DocAgentApiFailureResponse>     (DocAgentApiFailurePortId);

        return new WorkflowBuilder(start)
            // Stage 55A：Start → KickoffStage（parent group） / DevPlanStage（sub-task）— 兩出口
            .AddEdge(start, kickoffStage)                  // Stage 55A：parent group 入口
            .AddEdge(start, devPlanStage)                  // Stage 55A：sub-task 入口（skip Kickoff/Design）
            // Stage 55A：KickoffStage RequestPort 雙向 + 兩出口
            .AddEdge(kickoffStage, kickoffPort)            // KickoffCompletionRequest → port
            .AddEdge(kickoffPort, kickoffStage)            // KickoffCompletionResponse → KickoffStageExecutor.HandleResponseAsync
            .AddEdge(kickoffStage, designStage)            // DesignStageBridge → DesignStage
            // Stage 55A：DesignStage RequestPort 雙向 + 兩出口
            .AddEdge(designStage, designPort)              // DesignCompletionRequest → port
            .AddEdge(designPort, designStage)              // DesignCompletionResponse → DesignStageExecutor.HandleResponseAsync
            .AddEdge(designStage, devPlanStage)            // DevPlanStageBridge passes through（ConsensusNoSplit / EscalateContinue）
            .AddEdge(designStage, fallback)                // PipelineFallbackBridge（SplitProposalOpened — sub-task chain 接手）
            // DevPlan stage RequestPort 雙向 + 兩出口 + Stage 53B 新加 self-loop（DevPlanRetryBridge）
            .AddEdge(devPlanStage, devPlanPort)            // DevPlanCompletionRequest → port
            .AddEdge(devPlanPort, devPlanStage)            // DevPlanCompletionResponse → DevPlanStageExecutor.HandleResponseAsync
            .AddEdge(devPlanStage, devStage)               // DevStageBridge passes through
            .AddEdge(devPlanStage, fallback)               // PipelineFallbackBridge passes through
            .AddEdge(devPlanStage, devPlanStage)           // Stage 53B：DevPlanRetryBridge self-loop（appeal 重產 Dev_plan）
            // Dev stage RequestPort 雙向 + 兩出口 + Stage 53B 新加 self-loop（DevRetryBridge）
            .AddEdge(devStage, devPort)
            .AddEdge(devPort, devStage)
            .AddEdge(devStage, reviewerStage)              // ReviewerStageBridge
            .AddEdge(devStage, fallback)                   // PipelineFallbackBridge
            .AddEdge(devStage, devStage)                   // Stage 53B：DevRetryBridge self-loop（[BLOCKED] continue 重試 Dev）
            // Reviewer stage RequestPort 雙向 + 三出口（53B 加 DevFixStageBridge fix loop 觸發）
            .AddEdge(reviewerStage, reviewerPort)
            .AddEdge(reviewerPort, reviewerStage)
            .AddEdge(reviewerStage, qaStage)               // QaStageBridge
            .AddEdge(reviewerStage, fallback)              // PipelineFallbackBridge
            .AddEdge(reviewerStage, devFixStage)           // Stage 53B：DevFixStageBridge fix loop 觸發（Petra revise）
            // QA stage RequestPort 雙向 + 三出口（53B 加 DevFixStageBridge QA fix loop）
            .AddEdge(qaStage, qaPort)
            .AddEdge(qaPort, qaStage)
            .AddEdge(qaStage, docStage)                    // DocStageBridge
            .AddEdge(qaStage, fallback)                    // PipelineFallbackBridge
            .AddEdge(qaStage, devFixStage)                 // Stage 53B：DevFixStageBridge QA fix loop（QaFixRound > 0）
            // Doc stage RequestPort 雙向 + 兩出口
            .AddEdge(docStage, docPort)
            .AddEdge(docPort, docStage)
            .AddEdge(docStage, notifyMerge)                // NotifyMergeStageBridge
            .AddEdge(docStage, fallback)                   // PipelineFallbackBridge
            // Stage 53B 新加：DevFix stage RequestPort 雙向 + 一出口（loop back 到 Reviewer）
            .AddEdge(devFixStage, devFixPort)
            .AddEdge(devFixPort, devFixStage)
            .AddEdge(devFixStage, reviewerStage)           // Stage 53B：DevFix passed → ReviewerStageBridge loop back（fix loop 主路徑）
            // Stage 55B Session B：5 type-specific intervention HITL RequestPort 雙向（議題 2 = 2C Pattern A）
            .AddEdge(devStage, devInterventionPort)        // DevInterventionRequest → port
            .AddEdge(devInterventionPort, devStage)        // DevInterventionResponse → DevStageExecutor.HandleDevInterventionResponseAsync
            .AddEdge(qaStage, qaInterventionPort)          // QaInterventionRequest → port
            .AddEdge(qaInterventionPort, qaStage)          // QaInterventionResponse → QaStageExecutor.HandleQaInterventionResponseAsync
            .AddEdge(devPlanStage, devPlanEscalatePort)    // DevPlanEscalateRequest → port
            .AddEdge(devPlanEscalatePort, devPlanStage)    // DevPlanEscalateResponse → DevPlanStageExecutor.HandleDevPlanEscalateResponseAsync
            .AddEdge(devPlanStage, devPlanUnablePort)      // DevPlanUnableRequest → port
            .AddEdge(devPlanUnablePort, devPlanStage)      // DevPlanUnableResponse → DevPlanStageExecutor.HandleDevPlanUnableResponseAsync
            .AddEdge(designStage, splitTaskProposalPort)   // SplitTaskProposalRequest → port
            .AddEdge(splitTaskProposalPort, designStage)   // SplitTaskProposalResponse → DesignStageExecutor.HandleSplitTaskProposalResponseAsync
            // Stage 57-FF 五十二：Reviewer fix loop limit RequestPort 雙向 + 直送 Doc edge（skip_qa case 跳 QA 直接 Doc）
            .AddEdge(reviewerStage, reviewerFixLoopLimitPort)   // ReviewerFixLoopLimitRequest → port
            .AddEdge(reviewerFixLoopLimitPort, reviewerStage)   // ReviewerFixLoopLimitResponse → ReviewerStageExecutor.HandleReviewerFixLoopLimitResponseAsync
            .AddEdge(reviewerStage, docStage)                   // Stage 57：fix_loop_skip_qa case 跳 QA 直接送 Doc
            // Stage 58-FF 五十三：Agent API 失敗 per-stage 4 RequestPort 雙向（第 7 routing — continue 跳下游 edge 共用既有 Dev→Reviewer / Reviewer→Qa / Qa→Doc / Doc→NotifyMerge wiring）
            .AddEdge(devStage,      devAgentApiFailurePort)      // DevAgentApiFailureRequest → port
            .AddEdge(devAgentApiFailurePort,      devStage)      // DevAgentApiFailureResponse → DevStageExecutor.HandleAgentApiFailureResponseAsync
            .AddEdge(reviewerStage, reviewerAgentApiFailurePort) // ReviewerAgentApiFailureRequest → port
            .AddEdge(reviewerAgentApiFailurePort, reviewerStage) // ReviewerAgentApiFailureResponse → ReviewerStageExecutor.HandleAgentApiFailureResponseAsync
            .AddEdge(qaStage,       qaAgentApiFailurePort)       // QaAgentApiFailureRequest → port
            .AddEdge(qaAgentApiFailurePort,       qaStage)       // QaAgentApiFailureResponse → QaStageExecutor.HandleAgentApiFailureResponseAsync
            .AddEdge(docStage,      docAgentApiFailurePort)      // DocAgentApiFailureRequest → port
            .AddEdge(docAgentApiFailurePort,      docStage)      // DocAgentApiFailureResponse → DocStageExecutor.HandleAgentApiFailureResponseAsync
            // 終結 — NotifyMerge（happy path）/ fallback / 55A：含 Kickoff/Design Stage Executor 都可 YieldOutput PipelineLoopResult intervention
            .WithOutputFrom(notifyMerge, fallback, kickoffStage, designStage, devPlanStage, devStage, reviewerStage, qaStage, devFixStage)
            .Build();
    }

    /// <summary>Stage 55A：7 個 Agent 型 stage 各自獨立 RequestPort PortId 常數（55A 加 Kickoff/Design 兩 PortId）。
    /// FrameworkPipelineRouter / ResumeAfterKickoff/DesignAsync 用以從 ExternalRequest.PortInfo.PortId 篩選事件。</summary>
    public const string KickoffCompletionPortId  = "Pipeline-KickoffCompletion";   // Stage 55A
    public const string DesignCompletionPortId   = "Pipeline-DesignCompletion";    // Stage 55A
    public const string DevPlanCompletionPortId  = "Pipeline-DevPlanCompletion";
    public const string DevCompletionPortId      = "Pipeline-DevCompletion";
    public const string ReviewerCompletionPortId = "Pipeline-ReviewerCompletion";
    public const string QaCompletionPortId       = "Pipeline-QaCompletion";
    public const string DocCompletionPortId      = "Pipeline-DocCompletion";
    /// <summary>Stage 53B：DevFix stage RequestPort PortId 常數（K1 拍板 5 → 6 entry）。</summary>
    public const string DevFixCompletionPortId   = "Pipeline-DevFixCompletion";

    // Stage 55B Session B：5 type-specific intervention HITL PortId 常數
    public const string DevInterventionPortId      = "Pipeline-DevIntervention";
    public const string QaInterventionPortId       = "Pipeline-QaIntervention";
    public const string DevPlanEscalatePortId      = "Pipeline-DevPlanEscalate";
    public const string DevPlanUnablePortId        = "Pipeline-DevPlanUnable";
    public const string SplitTaskProposalPortId    = "Pipeline-SplitTaskProposal";

    /// <summary>Stage 57-FF 五十二：Reviewer fix loop ×3 達 limit RequestPort PortId（第 6 routing）。</summary>
    public const string ReviewerFixLoopLimitPortId = "Pipeline-ReviewerFixLoopLimit";

    // Stage 58-FF 五十三：Agent API 失敗 per-stage 4 RequestPort PortId（第 7 routing — Christ 拍板真三選 continue / retry / abort）
    public const string DevAgentApiFailurePortId      = "Pipeline-DevAgentApiFailure";
    public const string ReviewerAgentApiFailurePortId = "Pipeline-ReviewerAgentApiFailure";
    public const string QaAgentApiFailurePortId       = "Pipeline-QaAgentApiFailure";
    public const string DocAgentApiFailurePortId      = "Pipeline-DocAgentApiFailure";

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
