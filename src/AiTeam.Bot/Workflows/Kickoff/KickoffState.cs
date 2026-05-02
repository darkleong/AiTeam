using System.Text.Json.Serialization;
using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Kickoff;

/// <summary>
/// Stage 50：MS Agent Framework Kickoff Meeting Workflow 跨 executor 共享 state。
/// framework Checkpointing 序列化單位（透過 ICheckpointStore&lt;JsonElement&gt; 寫進
/// task_groups.KickoffFrameworkStateJson）。
///
/// 設計約束：
///   - 純資料，不含 EF entity / DbContext / IClaudeCodeService 等 reference 物件（Round-trip JSON 序列化必須無損）
///   - 對齊既有 KickoffMeetingService.RunKickoffMeetingAsync 行為（feature flag false 時走 legacy）
///   - 對齊 Stage 49 AppealState pattern（純資料 + JsonPropertyName + helper class）
/// </summary>
public sealed class KickoffState
{
    /// <summary>對應 TaskGroup.Id。Executor 用以從 DB 取真實 entity（不放 entity 本身避免 EF tracking 衝突）。</summary>
    [JsonPropertyName("groupId")]
    public Guid GroupId { get; set; }

    /// <summary>當前 round（1-based）。</summary>
    [JsonPropertyName("round")]
    public int Round { get; set; } = 1;

    /// <summary>Loop 動態上限（從 WorkflowSettingsResolver.GetKickoffMaxRoundsAsync 讀，避免 framework workflow 內 re-resolve）。</summary>
    [JsonPropertyName("maxRounds")]
    public int MaxRounds { get; set; } = 3;

    /// <summary>Victoria 提案完整內容（會議 input，跨 round 不變）。</summary>
    [JsonPropertyName("proposalContent")]
    public string ProposalContent { get; set; } = "";

    /// <summary>Petra 上一輪的整理輸出（第 N 輪 4 Agent 接續用）。null = 第 1 輪。</summary>
    [JsonPropertyName("lastPetraOutput")]
    public string? LastPetraOutput { get; set; }

    /// <summary>Petra session_id = group.Id（C2 拍板：沿用 Claude Code --resume 機制給 Modify 流程）。</summary>
    [JsonPropertyName("petraSessionId")]
    public string PetraSessionId { get; set; } = "";

    /// <summary>Rosa session_id（臨時 GUID，會議結束即拋棄）。</summary>
    [JsonPropertyName("rosaSessionId")]
    public string RosaSessionId { get; set; } = "";

    [JsonPropertyName("demiSessionId")]
    public string DemiSessionId { get; set; } = "";

    [JsonPropertyName("codySessionId")]
    public string CodySessionId { get; set; } = "";

    [JsonPropertyName("quinnSessionId")]
    public string QuinnSessionId { get; set; } = "";

    /// <summary>repo clone 工作目錄（router 啟動時 set，跨 superstep 共用）。</summary>
    [JsonPropertyName("workingDir")]
    public string WorkingDir { get; set; } = "";

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = "";

    [JsonPropertyName("repo")]
    public string Repo { get; set; } = "";

    /// <summary>會議紀錄累積（Markdown，每 round 4 Agent + Petra 各 append）。</summary>
    [JsonPropertyName("meetingLog")]
    public string MeetingLog { get; set; } = "";

    /// <summary>final TaskPlan（Petra 產出後寫入；router 用此寫進 group.TaskPlan）。null = 尚未產出。</summary>
    [JsonPropertyName("taskPlan")]
    public string? TaskPlan { get; set; }

    // ── Stage 51：HITL 試點 3 欄位（皆隨 KickoffCheckpointStore 序列化進 KickoffFrameworkStateJson，不加 DB 欄位）──
    //
    // 設計變更紀錄：原計劃含 4 個欄位含 MidInterruptTriggered。實作時改用獨立 in-memory Singleton
    // KickoffMidInterruptTriggerStore（避免在 framework state JSON 上做 mutation 的 brittle 解析），
    // trigger flag 不寫進 KickoffState；其餘 3 欄位仍透過 framework state 序列化。
    // 此變更不影響 Crash Recovery：trigger 在 HITL 等待前就已被 MidInterruptCheckExecutor 消耗，
    // 重啟後讀取 MidInterruptRequestPending=true 即可正確識別「等待人類回應」狀態。

    /// <summary>Stage 51：workflow 已 emit RequestInfoEvent 等待 Christ 回應的旗標。
    /// Recovery 啟動時讀此 flag 區分「等待人類回應」（不算 stuck）vs「真正卡住」。
    /// FrameworkHitlBridge.HandleMidInterruptResponseAsync 開頭讀此 flag 防重入冪等。</summary>
    [JsonPropertyName("midInterruptRequestPending")]
    public bool MidInterruptRequestPending { get; set; }

    /// <summary>Stage 51：Christ 回應的修改指引內容（apply 時為文字 / cancel 時為 null）。
    /// 拍板：持續保留 prompt 注入（對齊 ModifyTaskPlanAsync「Petra 永遠記得」精神）；
    /// Cancel 語意 = 丟棄所有累積指引回到正常對話（每次介入是獨立 trigger-response cycle）。</summary>
    [JsonPropertyName("midInterruptResponse")]
    public string? MidInterruptResponse { get; set; }

    /// <summary>Stage 51：router 開頭建立的 kickoffTask.Id — 隨 state 序列化，
    /// FrameworkHitlBridge.HandleMidInterruptResponseAsync resume 完成後從此撈 task entity mark done。</summary>
    [JsonPropertyName("kickoffTaskId")]
    public Guid KickoffTaskId { get; set; }
}

// ── Stage 51：HITL RequestPort 的 request / response payload record ──

/// <summary>Stage 51：MidInterruptCheckExecutor → RequestPort 送出的請求 payload（Christ 端會看到 Round + Petra 摘要）。</summary>
public sealed record MidInterruptRequest(
    [property: JsonPropertyName("groupId")]      Guid   GroupId,
    [property: JsonPropertyName("round")]        int    Round,
    [property: JsonPropertyName("petraSummary")] string PetraSummary);

/// <summary>Stage 51：Christ 回應後（apply / cancel）由 FrameworkHitlBridge 構造的 response payload，
/// 透過 ExternalResponse 送回 RequestPort，再由 MidInterruptCheckExecutor.HandleResponseAsync 套用到 state。</summary>
public sealed record MidInterruptResponseData(
    [property: JsonPropertyName("apply")]   bool    Apply,
    [property: JsonPropertyName("content")] string? Content);

/// <summary>4 Agent 各自 Executor 回傳給 Aggregator 的單一 Agent 輸出。</summary>
public sealed record KickoffAgentOutput(
    [property: JsonPropertyName("agentKey")] string AgentKey,
    [property: JsonPropertyName("output")]   string Output,
    [property: JsonPropertyName("round")]    int    Round);

/// <summary>Aggregator fan-in 收齊 4 個 KickoffAgentOutput 後合併送給 Petra 的 record。</summary>
public sealed class KickoffRoundCollected
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

/// <summary>Petra 整理 + 判斷後輸出（routing source for AddSwitch）。</summary>
public sealed class KickoffPetraVerdict
{
    /// <summary>consensus / needs_discussion / escalate（對齊既有 KickoffMeetingService.PetraDecision.Decision）。</summary>
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
}

/// <summary>Workflow 最終 output（router translate 為 legacy MeetingResult 寫 DB）。</summary>
public sealed class KickoffLoopResult
{
    /// <summary>consensus / escalate / max_iter。</summary>
    [JsonPropertyName("decision")]
    public string Decision { get; set; } = "";

    [JsonPropertyName("meetingLog")]
    public string MeetingLog { get; set; } = "";

    [JsonPropertyName("taskPlan")]
    public string TaskPlan { get; set; } = "";

    [JsonPropertyName("totalRounds")]
    public int TotalRounds { get; set; }

    [JsonPropertyName("escalateReason")]
    public string? EscalateReason { get; set; }
}

/// <summary>跨 executor 共享狀態的 scope + key（對齊 Stage 49 AppealStateKeys pattern）。</summary>
internal static class KickoffStateKeys
{
    public const string Scope = "KickoffStateScope";
    public const string Key   = "singleton";
}

internal static class KickoffStateHelpers
{
    /// <summary>從 IWorkflowContext 讀 KickoffState。null 時回新實例（首次 superstep 安全）。</summary>
    public static async Task<KickoffState> ReadAsync(IWorkflowContext context)
        => await context.ReadStateAsync<KickoffState>(KickoffStateKeys.Key, scopeName: KickoffStateKeys.Scope)
           ?? new KickoffState();

    /// <summary>把 KickoffState 寫回 IWorkflowContext（framework superstep 結束時自動 flush 到 ICheckpointStore）。</summary>
    public static ValueTask SaveAsync(IWorkflowContext context, KickoffState state)
        => context.QueueStateUpdateAsync(KickoffStateKeys.Key, state, scopeName: KickoffStateKeys.Scope);
}
