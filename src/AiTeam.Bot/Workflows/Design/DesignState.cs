using System.Text.Json.Serialization;
using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Design;

/// <summary>
/// Stage 52：MS Agent Framework Design Meeting Workflow 跨 executor 共享 state（v4 漸進遷移第四步）。
///
/// framework Checkpointing 序列化單位（透過 ICheckpointStore&lt;JsonElement&gt; 寫進 task_groups.DesignFrameworkStateJson）。
///
/// 設計約束（對齊 Stage 50 KickoffState pattern）：
///   - 純資料，不含 EF entity / DbContext / IClaudeCodeService 等 reference 物件（Round-trip JSON 序列化必須無損）
///   - 對齊既有 DesignMeetingService.RunDesignMeetingAsync 行為（feature flag false 時走 legacy）
///   - DemiSessionId / IssueUrls / UiSpecContent / LastPetraOutput / EscalateReason 設 nullable 對齊 legacy DesignSessionState 邊界
/// </summary>
public sealed class DesignState
{
    /// <summary>對應 TaskGroup.Id。Executor 用以從 DB 取真實 entity（不放 entity 本身避免 EF tracking 衝突）。</summary>
    [JsonPropertyName("groupId")]
    public Guid GroupId { get; set; }

    /// <summary>Stage 52 對齊 Stage 51 KickoffTaskId pattern：router 開頭建立的 designTask.Id —
    /// 隨 state 序列化，FrameworkDesignRouter.FinalizeDesignAsync resume 完成後從此撈 task entity mark done。</summary>
    [JsonPropertyName("designTaskId")]
    public Guid DesignTaskId { get; set; }

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = "";

    [JsonPropertyName("repo")]
    public string Repo { get; set; } = "";

    /// <summary>repo clone 工作目錄（router 啟動時 set，跨 superstep 共用）。</summary>
    [JsonPropertyName("workingDir")]
    public string WorkingDir { get; set; } = "";

    /// <summary>從 group.TaskPlan 帶入（會議 input，跨 round 不變）。</summary>
    [JsonPropertyName("taskPlan")]
    public string TaskPlan { get; set; } = "";

    /// <summary>Loop 動態上限（從 WorkflowSettingsResolver.GetDesignMeetingMaxRoundsAsync 讀，避免 framework workflow 內 re-resolve）。</summary>
    [JsonPropertyName("maxRounds")]
    public int MaxRounds { get; set; } = 3;

    /// <summary>當前 round（1-based）。前置作業階段為 0；mainStart 接 PreWorkBridge 時設為 1。</summary>
    [JsonPropertyName("round")]
    public int Round { get; set; }

    /// <summary>Petra session_id — 議題 H1 拍板：跨 framework + legacy 共用，FrameworkDesignRouter finalize 段寫進
    /// PendingConfirmationStore._pendingDesignConfirmations 給 escalate Modify 流程 resume。</summary>
    [JsonPropertyName("petraSessionId")]
    public string PetraSessionId { get; set; } = "";

    [JsonPropertyName("rosaSessionId")]
    public string RosaSessionId { get; set; } = "";

    /// <summary>Demi session_id — nullable：初始 null（Petra judge needsDemi 後才建立）。
    /// short-circuit 條件：null 時 DesignDemiPreWorkExecutor / DesignAgentExecutor[Demi] pass-through 不跑 LLM call。
    /// 邊界：初始 needsDemi=false 但會議揭露需要 UI 規格 → DesignAdjustmentExecutor 動態建立（對齊 legacy line 487-500）。</summary>
    [JsonPropertyName("demiSessionId")]
    public string? DemiSessionId { get; set; }

    [JsonPropertyName("codySessionId")]
    public string CodySessionId { get; set; } = "";

    [JsonPropertyName("quinnSessionId")]
    public string QuinnSessionId { get; set; } = "";

    /// <summary>Petra judge 結果（需 Demi 參與設計），影響條件式拓撲 short-circuit。</summary>
    [JsonPropertyName("needsDemi")]
    public bool NeedsDemi { get; set; }

    /// <summary>Rosa pre-work 產出 Issues JSON Array（"[]" 預設）。供 Cody/Quinn prompt 參考 + DesignSplitProposalEvaluator 規則層判斷。</summary>
    [JsonPropertyName("issuesJson")]
    public string IssuesJson { get; set; } = "[]";

    /// <summary>Rosa pre-work 建立的 GitHub Issue URL JSON string array — nullable。</summary>
    [JsonPropertyName("issueUrls")]
    public string? IssueUrls { get; set; }

    /// <summary>Demi pre-work 產出 — nullable（needsDemi=false 時為 null）。</summary>
    [JsonPropertyName("uiSpecContent")]
    public string? UiSpecContent { get; set; }

    /// <summary>Petra 上一輪整理輸出（第 N 輪 4 Agent 接續用）。null = 第 1 輪。</summary>
    [JsonPropertyName("lastPetraOutput")]
    public string? LastPetraOutput { get; set; }

    /// <summary>會議紀錄累積（Markdown，前置作業段 + 每 round 4 Agent + Petra 各 append）。</summary>
    [JsonPropertyName("meetingLog")]
    public string MeetingLog { get; set; } = "";

    /// <summary>final 設計規劃書（Petra 產出後寫入；router 用此寫進 group.DesignPlan）。null = 尚未產出。</summary>
    [JsonPropertyName("designPlan")]
    public string? DesignPlan { get; set; }

    /// <summary>final decision（"consensus" / "escalate"，預設 "consensus"）。</summary>
    [JsonPropertyName("finalDecision")]
    public string FinalDecision { get; set; } = "consensus";

    /// <summary>escalate 路徑的上呈原因 — nullable。</summary>
    [JsonPropertyName("escalateReason")]
    public string? EscalateReason { get; set; }

    /// <summary>結算用 — 累計輪次（含 needs_adjustment 子流程內推進的 round）。</summary>
    [JsonPropertyName("totalRounds")]
    public int TotalRounds { get; set; }
}

// ── records（顯式 send/yield 訊息型別） ──

/// <summary>Stage 52：前置作業段 Executor 之間傳遞的 bridge record（PetraJudge → RosaPreWork → DemiPreWork → RoundStart）。
/// 線性串接段共用同一型別，下游 Executor 各自從 framework state 讀取需要的欄位。</summary>
public sealed record DesignPreWorkBridge(
    [property: JsonPropertyName("groupId")] Guid GroupId,
    [property: JsonPropertyName("phase")]   string Phase);   // "after_judge" | "after_rosa" | "after_demi"

/// <summary>4 Agent 各自 Executor 回傳給 Aggregator 的單一 Agent 輸出。
/// AgentKey="Demi" + Output="" 表示 short-circuit pass-through（DemiSessionId is null）。</summary>
public sealed record DesignAgentOutput(
    [property: JsonPropertyName("agentKey")] string AgentKey,
    [property: JsonPropertyName("output")]   string Output,
    [property: JsonPropertyName("round")]    int    Round);

/// <summary>Aggregator fan-in 收齊 4 個 DesignAgentOutput 後合併送給 Petra 的 record。
/// Demi 跳過時 Demi 欄位為空字串，Petra prompt 依 state.DemiSessionId is null 判斷是否注入 Demi 段。</summary>
public sealed class DesignRoundCollected
{
    [JsonPropertyName("round")]
    public int Round { get; set; }
    [JsonPropertyName("rosa")]
    public string Rosa { get; set; } = "";
    [JsonPropertyName("demi")]
    public string Demi { get; set; } = "";
    [JsonPropertyName("cody")]
    public string Cody { get; set; } = "";
    [JsonPropertyName("quinn")]
    public string Quinn { get; set; } = "";
}

/// <summary>Petra 整理 + 判斷後輸出（routing source for AddSwitch）。
///
/// 5 個 decision 分支共用同一 verdict 型別（Aria 實作期提醒 #2）：
///   - consensus / needs_discussion / needs_adjustment / escalate / max_iter（needs_discussion + Round >= MaxRounds 衍生）
///   - escalate 路徑 EscalateReason 由 DesignPetraExecutor 解析填，DesignEscalateExecutor / DesignAdjustmentExecutor 收到後寫進 DesignLoopResult.EscalateReason
/// </summary>
public sealed class DesignPetraVerdict
{
    /// <summary>"consensus" | "needs_discussion" | "needs_adjustment" | "escalate"</summary>
    [JsonPropertyName("decision")]
    public string Decision { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    /// <summary>Petra 完整回應（log + 下一輪餵 4 Agent 用 lastPetraOutput）。</summary>
    [JsonPropertyName("petraOutput")]
    public string PetraOutput { get; set; } = "";

    [JsonPropertyName("round")]
    public int Round { get; set; }

    [JsonPropertyName("maxRounds")]
    public int MaxRounds { get; set; }

    /// <summary>needs_adjustment：Petra 指定要修改的角色（"rosa" / "demi"）。</summary>
    [JsonPropertyName("adjustmentTargets")]
    public string[] AdjustmentTargets { get; set; } = [];

    /// <summary>needs_adjustment：Petra 對每個 target 角色的修改指示。</summary>
    [JsonPropertyName("adjustmentInstructions")]
    public Dictionary<string, string> AdjustmentInstructions { get; set; } = new();

    /// <summary>escalate：上呈原因（needs_adjustment → needs_meeting → escalate 邊界也用此欄位）。
    /// 對齊 legacy DesignPetraDecision.EscalateReason + Stage 50 KickoffPetraVerdict 既有 pattern（Aria 實作期提醒 #2）。</summary>
    [JsonPropertyName("escalateReason")]
    public string? EscalateReason { get; set; }
}

/// <summary>Stage 52：DesignAdjustmentExecutor approved 出口 record（Aria 議題 7 + 議題 10）。
///
/// 結構約束：DesignPlan **non-null**，由 Adjustment Executor 內保證已產出（evalDecision.DesignPlan 直接帶 / fallback 走 BuildDesignPetraPlanPrompt 補產）。
/// DesignPlanExecutor.HandleAdjustmentApprovedAsync 收到後直接 wrap DesignLoopResult，不再 call LLM。
/// </summary>
public sealed record DesignAdjustmentApproved(
    [property: JsonPropertyName("round")]           int    Round,
    [property: JsonPropertyName("maxRounds")]       int    MaxRounds,
    [property: JsonPropertyName("designPlan")]      string DesignPlan,
    [property: JsonPropertyName("petraEvalOutput")] string PetraEvalOutput,
    [property: JsonPropertyName("meetingLog")]      string MeetingLog);

/// <summary>Workflow 最終 output（router translate 為 legacy DesignMeetingResult 寫 DB）。</summary>
public sealed class DesignLoopResult
{
    /// <summary>"consensus" / "escalate" / "max_iter"。</summary>
    [JsonPropertyName("decision")]
    public string Decision { get; set; } = "";

    [JsonPropertyName("meetingLog")]
    public string MeetingLog { get; set; } = "";

    [JsonPropertyName("designPlan")]
    public string? DesignPlan { get; set; }

    [JsonPropertyName("issuesJson")]
    public string IssuesJson { get; set; } = "[]";

    [JsonPropertyName("issueUrls")]
    public string? IssueUrls { get; set; }

    [JsonPropertyName("uiSpecContent")]
    public string? UiSpecContent { get; set; }

    [JsonPropertyName("totalRounds")]
    public int TotalRounds { get; set; }

    [JsonPropertyName("petraSessionId")]
    public string PetraSessionId { get; set; } = "";

    [JsonPropertyName("escalateReason")]
    public string? EscalateReason { get; set; }
}

// ── state scope helpers（對齊 Stage 50 KickoffStateHelpers pattern） ──

internal static class DesignStateKeys
{
    public const string Scope = "DesignStateScope";
    public const string Key   = "singleton";
}

internal static class DesignStateHelpers
{
    /// <summary>從 IWorkflowContext 讀 DesignState。null 時回新實例（首次 superstep 安全）。</summary>
    public static async Task<DesignState> ReadAsync(IWorkflowContext context)
        => await context.ReadStateAsync<DesignState>(DesignStateKeys.Key, scopeName: DesignStateKeys.Scope)
           ?? new DesignState();

    /// <summary>把 DesignState 寫回 IWorkflowContext（framework superstep 結束時自動 flush 到 ICheckpointStore）。</summary>
    public static ValueTask SaveAsync(IWorkflowContext context, DesignState state)
        => context.QueueStateUpdateAsync(DesignStateKeys.Key, state, scopeName: DesignStateKeys.Scope);
}
