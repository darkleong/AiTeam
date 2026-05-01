using AiTeam.Bot.Agents.Pm;

namespace AiTeam.Bot.Workflows.Appeal;

/// <summary>
/// Stage 49：Vera 重評每輪結果 + framework AddSwitch routing 用的派生 flag。
///
/// 為什麼包一層 vs 直接用 VeraAppealResponse：
///   - framework AddSwitch&lt;T&gt; 的 AddCase predicate 只接 T，不能讀 IWorkflowContext.state
///   - Vera 是否 approved（= MaintainedIds.Count == 0）、是否該 escalate（= Round &gt;= MaxRounds）
///     都需要 routing 看得到
///   - → 用 wrapper record 把 Round / MaxRounds / Approved 派生 flag 含進去
/// </summary>
public sealed record VeraAppealRoundResult(
    VeraAppealResponse Vera,
    int Round,
    int MaxRounds,
    /// <summary>= Vera.MaintainedIds.Count == 0（Vera 接受所有 critical 反駁）。</summary>
    bool Approved,
    /// <summary>剩餘 critical 數（loop 路由判斷用）。</summary>
    int RemainingCriticalCount);

/// <summary>
/// Stage 49：Cody Dev_plan Appeal 每輪結果 + Petra 重評後的最終 decision。
///
/// 為什麼包一層 vs 直接用 PetraReview：
///   - 同 VeraAppealRoundResult 理由：framework AddSwitch predicate 需 Round / MaxRounds 直接可讀
///   - Approved 派生 flag = (Petra.Decision == "approve") OR (Cody.Position == "accept")
/// </summary>
public sealed record DevPlanAppealRoundResult(
    PetraReview Petra,
    CodyDevPlanAppeal LastCodyAppeal,
    int Round,
    int MaxRounds,
    /// <summary>= Cody.Position == "accept" || Petra.Decision == "approve"（Cody 接受意見 OR Petra 重評放行）。</summary>
    bool Approved);

/// <summary>
/// Stage 49：Appeal Workflow 最終輸出（framework WorkflowOutputEvent 帶的 payload）。
/// FrameworkAppealRouter 取此結果寫進既有 DB 欄位（task_groups / tasks / Discord 通知）。
/// </summary>
public sealed record AppealLoopResult(
    /// <summary>"approve" / "revise" / "escalate" / "max_iter_arbitration_approve" / "max_iter_arbitration_reject"。</summary>
    string Verdict,

    /// <summary>最終 Critical IDs（ReviewAppeal 路徑用，escalate 時可能含未解 criticals）。</summary>
    IReadOnlyList<int> FinalCriticalIds,

    /// <summary>Petra 修正指示（revise 路徑用）。</summary>
    string? RevisionInstructions,

    /// <summary>給 Discord 通知的摘要文字。</summary>
    string Summary,

    /// <summary>是否觸發 Petra 仲裁（ReviewAppeal max-iter 路徑）。</summary>
    bool ArbitrationTriggered,

    /// <summary>仲裁結果 detail（含 final_criticals / reasoning），ArbitrationTriggered = false 時 null。</summary>
    AppealArbitration? Arbitration);
