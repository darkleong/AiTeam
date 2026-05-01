using System.Text.Json.Serialization;
using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Appeal;

/// <summary>
/// Stage 49：MS Agent Framework Cody-Vera-Petra Appeal Workflow 跨 executor 共享 state。
/// framework Checkpointing 序列化單位（透過 ICheckpointStore&lt;JsonElement&gt; 寫進
/// task_groups.FrameworkAppealStateJson）。
///
/// 設計約束：
///   - 純資料，不含 EF entity / DbContext / IClaudeCodeService 等 reference 物件（Round-trip JSON 序列化必須無損）
///   - 對齊既有 AppealOrchestrationService 行為（feature flag false 時走 legacy）
///   - 對應 Trial_v5 議題 B「ImplementationNote 路徑斷裂」的 framework 內建解（type-safe shared state）
/// </summary>
public sealed class AppealState
{
    /// <summary>對應 TaskGroup.Id。Executor 用以從 DB 取真實 entity（不放 entity 本身避免 EF tracking 衝突）。</summary>
    [JsonPropertyName("groupId")]
    public Guid GroupId { get; set; }

    /// <summary>Loop 類型：ReviewAppeal（Cody-Vera-Petra） / DevPlanAppeal（Cody-Petra）。</summary>
    [JsonPropertyName("kind")]
    public AppealLoopKind Kind { get; set; }

    /// <summary>當前 round（1-based）。</summary>
    [JsonPropertyName("round")]
    public int Round { get; set; } = 1;

    /// <summary>Loop 動態上限（從 WorkflowSettingsResolver 讀，避免 framework workflow 內 re-resolve）。</summary>
    [JsonPropertyName("maxRounds")]
    public int MaxRounds { get; set; } = 3;

    /// <summary>Vera 最近一次 review 全文（從 group.LastReviewBody 帶入；ReviewAppeal 路徑 loop 期間不變）。</summary>
    [JsonPropertyName("lastReviewBody")]
    public string LastReviewBody { get; set; } = "";

    /// <summary>Vera Critical IDs 剩餘清單（ReviewAppeal 路徑用，每輪 Vera 接受後扣減）。</summary>
    [JsonPropertyName("remainingCriticalIds")]
    public List<int> RemainingCriticalIds { get; set; } = [];

    /// <summary>歷次 Cody appeal 完整回應 JSON（逐輪 append；對應既有 ReviewAppealLog 寫入內容）。</summary>
    [JsonPropertyName("codyResponses")]
    public List<string> CodyResponses { get; set; } = [];

    /// <summary>歷次 Vera 重評 / Petra 仲裁決定（依 Round 累積）。</summary>
    [JsonPropertyName("veraDecisions")]
    public List<VeraDecision> VeraDecisions { get; set; } = [];

    /// <summary>DevPlan 全文（DevPlanAppeal 路徑用，從 group.DevPlan 帶入）。</summary>
    [JsonPropertyName("devPlan")]
    public string? DevPlan { get; set; }

    /// <summary>Petra 初審意見（DevPlanAppeal Cody 反駁起點）。</summary>
    [JsonPropertyName("initialPetraReview")]
    public PetraReviewSnapshot? InitialPetraReview { get; set; }

    /// <summary>最終 verdict 摘要（Petra arbitration / gate 完後寫入，FrameworkAppealRouter 取用寫進既有 DB 欄位）。</summary>
    [JsonPropertyName("finalVerdict")]
    public string? FinalVerdict { get; set; }

    /// <summary>最終 Critical IDs 清單（Petra arbitration 後產出，給 FrameworkAppealRouter 寫進 result）。</summary>
    [JsonPropertyName("finalCriticalIds")]
    public List<int> FinalCriticalIds { get; set; } = [];

    /// <summary>Petra 修正指示（gate revise 路徑用）。</summary>
    [JsonPropertyName("revisionInstructions")]
    public string? RevisionInstructions { get; set; }
}

/// <summary>Stage 49：Appeal Workflow 兩種 loop 類型。</summary>
public enum AppealLoopKind
{
    /// <summary>Cody-Vera-Petra Critical Issue 申訴 loop（HandleReviewerCompletedAsync 走的）。</summary>
    ReviewAppeal = 0,

    /// <summary>Cody-Petra Dev_plan 申訴 loop（HandleDevPlanCompletedAsync revise 走的）。</summary>
    DevPlanAppeal = 1,
}

/// <summary>
/// Vera 審查決定（Petra 仲裁的輸入）。
/// 結構直接對應 framework Anthropic provider Structured Output（ChatResponseFormat.ForJsonSchema&lt;T&gt;()）。
/// </summary>
public sealed class VeraDecision
{
    [JsonPropertyName("approved")]
    [JsonPropertyOrder(1)]
    public bool Approved { get; set; }

    [JsonPropertyName("feedback")]
    [JsonPropertyOrder(2)]
    public string Feedback { get; set; } = "";

    /// <summary>Vera 接受的 Critical IDs（同既有 VeraAppealResponse.AcceptedIds）。</summary>
    [JsonPropertyName("acceptedIds")]
    [JsonPropertyOrder(3)]
    public List<int> AcceptedIds { get; set; } = [];

    /// <summary>Vera 維持的 Critical IDs（同既有 VeraAppealResponse.MaintainedIds）。</summary>
    [JsonPropertyName("maintainedIds")]
    [JsonPropertyOrder(4)]
    public List<int> MaintainedIds { get; set; } = [];

    /// <summary>Round 編號（由 Vera 從 AppealState 拷貝，方便 routing 判斷）。</summary>
    [JsonPropertyName("round")]
    [JsonPropertyOrder(5)]
    public int Round { get; set; }
}

/// <summary>Petra 審核 snapshot（純資料 DTO，避免序列化既有 PetraReview record 跨組件相依）。</summary>
public sealed class PetraReviewSnapshot
{
    [JsonPropertyName("decision")]
    public string Decision { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("revisionInstructions")]
    public string? RevisionInstructions { get; set; }
}

/// <summary>跨 executor 共享狀態的 scope + key（與 spike POC 對齊）。</summary>
internal static class AppealStateKeys
{
    public const string Scope = "AppealStateScope";
    public const string Key   = "singleton";
}

internal static class AppealStateHelpers
{
    /// <summary>從 IWorkflowContext 讀 AppealState。null 時回新實例（首次 superstep 安全）。</summary>
    public static async Task<AppealState> ReadAsync(IWorkflowContext context)
        => await context.ReadStateAsync<AppealState>(AppealStateKeys.Key, scopeName: AppealStateKeys.Scope)
           ?? new AppealState();

    /// <summary>把 AppealState 寫回 IWorkflowContext（framework superstep 結束時自動 flush 到 ICheckpointStore）。</summary>
    public static ValueTask SaveAsync(IWorkflowContext context, AppealState state)
        => context.QueueStateUpdateAsync(AppealStateKeys.Key, state, scopeName: AppealStateKeys.Scope);
}
