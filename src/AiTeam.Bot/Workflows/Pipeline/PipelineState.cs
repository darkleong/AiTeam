using System.Text.Json.Serialization;
using AiTeam.Bot.Agents;
using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Pipeline;

/// <summary>
/// Stage 53A：MS Agent Framework Pipeline Workflow 跨 executor 共享 state（v4 漸進遷移第五步 macro-orchestration）。
///
/// framework Checkpointing 序列化單位（透過 ICheckpointStore&lt;JsonElement&gt; 寫進 task_groups.PipelineFrameworkStateJson）。
///
/// 設計約束（對齊 Stage 50 KickoffState / Stage 52 DesignState pattern）：
///   - 純資料，不含 EF entity / DbContext / IClaudeCodeService 等 reference 物件（Round-trip JSON 序列化必須無損）
///   - 對齊既有 TaskGroupService.HandleAgentCompletedAsync + WorkflowEngine.GetDecision 行為（feature flag false 時走 legacy）
///   - LastAgentResult 設計支援 J1 yield-resume：callback 觸發 ResumeStreamingAsync 後 SendResponseAsync 用
///   - FallbackToLegacy + FallbackReason 支援 I2 邊界處理（5 fallback 點）
/// </summary>
public sealed class PipelineState
{
    /// <summary>對應 TaskGroup.Id。Executor 用以從 DB 取真實 entity（不放 entity 本身避免 EF tracking 衝突）。</summary>
    [JsonPropertyName("groupId")]
    public Guid GroupId { get; set; }

    /// <summary>WorkflowType："new_feature"（53A 主路徑限定）/ 留欄位給 53B 擴充 "tech_improvement" / "bug_fix"。</summary>
    [JsonPropertyName("workflowType")]
    public string WorkflowType { get; set; } = "new_feature";

    /// <summary>當前 stage 名稱（"Start" / "Dev_plan" / "Dev" / "Reviewer" / "QA" / "Doc" / "NotifyMerge"），跨 callback resume 用。
    /// Aria 方案 C 拍板（2026-05-03）：53A 範圍縮小，Kickoff/Design 留 legacy（Stage 55 收尾統一整合），Pipeline 從 Dev_plan 階段啟動。</summary>
    [JsonPropertyName("currentStage")]
    public string CurrentStage { get; set; } = "Start";

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = "";

    [JsonPropertyName("repo")]
    public string Repo { get; set; } = "";

    // ── 各 stage 完成 marker（resume 時跳過已完成 stage） ──
    // Aria 方案 C 拍板：Kickoff/Design 留 legacy 不在 Pipeline 範圍，移除 KickoffDone / DesignDone marker

    [JsonPropertyName("devPlanDone")]
    public bool DevPlanDone { get; set; }

    [JsonPropertyName("devDone")]
    public bool DevDone { get; set; }

    [JsonPropertyName("reviewerDone")]
    public bool ReviewerDone { get; set; }

    [JsonPropertyName("qaDone")]
    public bool QaDone { get; set; }

    [JsonPropertyName("docDone")]
    public bool DocDone { get; set; }

    /// <summary>J1 yield-resume：callback 帶入的 last result（resume 時 SendResponseAsync 用）。
    /// 議題 5 修法（Aria 二次檢查）：直接存 record 避免 JSON-in-JSON 雙重序列化（framework state 整體 JSON 序列化已內建）。</summary>
    [JsonPropertyName("lastAgentResult")]
    public AgentExecutionResult? LastAgentResult { get; set; }

    /// <summary>完成的 agent 名稱（用於 ResumeAfterAgentAsync routing 對應 PortId）。</summary>
    [JsonPropertyName("lastAgentName")]
    public string? LastAgentName { get; set; }

    // ── I2 fallback to legacy（53A 反向設計，Stage 55 收尾移除） ──

    /// <summary>5 fallback 點觸發時設 true，主 Workflow 結束流程。</summary>
    [JsonPropertyName("fallbackToLegacy")]
    public bool FallbackToLegacy { get; set; }

    /// <summary>fallback 原因（debug / log 用）：reviewer_critical / dev_plan_failed_escalate / dev_blocker / arbitration_skip_reviewer / qa_fix_loop。</summary>
    [JsonPropertyName("fallbackReason")]
    public string? FallbackReason { get; set; }
}

// ── records（顯式 send/yield 訊息型別，Stage 52 fix#2 type-explicit Bridge record 紀律延續） ──

/// <summary>Stage 53A：PipelineStartExecutor → DevPlanStageExecutor 的初始 bridge。
/// Aria 方案 C 拍板：53A 範圍縮小，Pipeline 從 Dev_plan 階段啟動（Kickoff/Design 留 legacy）。</summary>
public sealed record PipelineStartBridge(
    [property: JsonPropertyName("groupId")] Guid GroupId);

/// <summary>Stage 53A：DevPlanStage 入口 bridge。</summary>
public sealed record DevPlanStageBridge(
    [property: JsonPropertyName("groupId")] Guid GroupId);

/// <summary>Stage 53A：DevStage 入口 bridge。</summary>
public sealed record DevStageBridge(
    [property: JsonPropertyName("groupId")] Guid GroupId);

/// <summary>Stage 53A：ReviewerStage 入口 bridge。</summary>
public sealed record ReviewerStageBridge(
    [property: JsonPropertyName("groupId")] Guid GroupId);

/// <summary>Stage 53A：QaStage 入口 bridge。</summary>
public sealed record QaStageBridge(
    [property: JsonPropertyName("groupId")] Guid GroupId);

/// <summary>Stage 53A：DocStage 入口 bridge。</summary>
public sealed record DocStageBridge(
    [property: JsonPropertyName("groupId")] Guid GroupId);

/// <summary>Stage 53A：NotifyMergeStage 入口 bridge。</summary>
public sealed record NotifyMergeStageBridge(
    [property: JsonPropertyName("groupId")] Guid GroupId);

// ── 5 個 Agent 型 stage 各自獨立 RequestPort + 獨立 Request/Response 型別 ──
//
// 為什麼每 stage 獨立型別（不共用單一 AgentCompletionRequest/Response）：
//   Stage 52 fix#2 教訓 — framework AddEdge 對 message dispatch 只看 type 不看 source
//   若 5 stage 共用同一 record type，emit 時會 routing 到全部 5 個 RequestPort（collision）
//   每 stage 獨立型別 → type-based dispatch 自然分流到正確 port（type-explicit Bridge record 紀律延續）

/// <summary>Stage 53A：DevPlan stage J1 yield-resume RequestPort 請求 payload。</summary>
public sealed record DevPlanCompletionRequest(
    [property: JsonPropertyName("groupId")] Guid GroupId);

/// <summary>Stage 53A：DevPlan stage J1 yield-resume RequestPort 回傳 payload。
/// 議題 5 + Aria 提醒 2：record 直接帶 AgentExecutionResult（System.Text.Json record 序列化支援已驗）。</summary>
public sealed record DevPlanCompletionResponse(
    [property: JsonPropertyName("result")] AgentExecutionResult Result);

/// <summary>Stage 53A：Dev stage J1 yield-resume RequestPort 請求 payload。</summary>
public sealed record DevCompletionRequest(
    [property: JsonPropertyName("groupId")] Guid GroupId);

/// <summary>Stage 53A：Dev stage J1 yield-resume RequestPort 回傳 payload。</summary>
public sealed record DevCompletionResponse(
    [property: JsonPropertyName("result")] AgentExecutionResult Result);

/// <summary>Stage 53A：Reviewer stage J1 yield-resume RequestPort 請求 payload。</summary>
public sealed record ReviewerCompletionRequest(
    [property: JsonPropertyName("groupId")] Guid GroupId);

/// <summary>Stage 53A：Reviewer stage J1 yield-resume RequestPort 回傳 payload。</summary>
public sealed record ReviewerCompletionResponse(
    [property: JsonPropertyName("result")] AgentExecutionResult Result);

/// <summary>Stage 53A：QA stage J1 yield-resume RequestPort 請求 payload。</summary>
public sealed record QaCompletionRequest(
    [property: JsonPropertyName("groupId")] Guid GroupId);

/// <summary>Stage 53A：QA stage J1 yield-resume RequestPort 回傳 payload。</summary>
public sealed record QaCompletionResponse(
    [property: JsonPropertyName("result")] AgentExecutionResult Result);

/// <summary>Stage 53A：Doc stage J1 yield-resume RequestPort 請求 payload。</summary>
public sealed record DocCompletionRequest(
    [property: JsonPropertyName("groupId")] Guid GroupId);

/// <summary>Stage 53A：Doc stage J1 yield-resume RequestPort 回傳 payload。</summary>
public sealed record DocCompletionResponse(
    [property: JsonPropertyName("result")] AgentExecutionResult Result);

/// <summary>Stage 53A：5 fallback 點觸發時送出的 bridge — 由 PipelineFallbackExecutor 收到後 YieldOutputAsync 結束 Workflow。
/// 帶 LastResult 給 FinalizePipelineAsync 主動 call legacy method 接手（議題 9 修法）。</summary>
public sealed record PipelineFallbackBridge(
    [property: JsonPropertyName("groupId")]    Guid                  GroupId,
    [property: JsonPropertyName("reason")]     string                Reason,
    [property: JsonPropertyName("lastResult")] AgentExecutionResult? LastResult);

/// <summary>Stage 53A：Workflow 最終 output（router watch loop 收到後 call FinalizePipelineAsync）。
/// Completed=true → happy path 完成 / Completed=false → fallback to legacy（FallbackReason 不為 null）。</summary>
public sealed class PipelineLoopResult
{
    [JsonPropertyName("groupId")]
    public Guid GroupId { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("fallbackReason")]
    public string? FallbackReason { get; set; }

    [JsonPropertyName("lastResult")]
    public AgentExecutionResult? LastResult { get; set; }
}

// ── state scope helpers（對齊 Stage 50 KickoffStateHelpers / Stage 52 DesignStateHelpers pattern） ──

internal static class PipelineStateKeys
{
    public const string Scope = "PipelineStateScope";
    public const string Key   = "singleton";
}

internal static class PipelineStateHelpers
{
    /// <summary>從 IWorkflowContext 讀 PipelineState。null 時回新實例（首次 superstep 安全）。</summary>
    public static async Task<PipelineState> ReadAsync(IWorkflowContext context)
        => await context.ReadStateAsync<PipelineState>(PipelineStateKeys.Key, scopeName: PipelineStateKeys.Scope)
           ?? new PipelineState();

    /// <summary>把 PipelineState 寫回 IWorkflowContext（framework superstep 結束時自動 flush 到 ICheckpointStore）。</summary>
    public static ValueTask SaveAsync(IWorkflowContext context, PipelineState state)
        => context.QueueStateUpdateAsync(PipelineStateKeys.Key, state, scopeName: PipelineStateKeys.Scope);
}
