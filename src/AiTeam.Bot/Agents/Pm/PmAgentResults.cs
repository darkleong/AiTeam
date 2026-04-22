namespace AiTeam.Bot.Agents.Pm;

/// <summary>Petra 審核結果。</summary>
public record PetraReview(
    string Decision,              // "approve" | "revise" | "escalate"
    string Summary,
    IReadOnlyList<PetraIssue> Issues,
    string? RevisionInstructions);

/// <summary>Petra 發現的單一問題。</summary>
public record PetraIssue(string Severity, string Description);

// ────────────── 23-3：Blocker 評估結果 ──────────────

/// <summary>Petra 對 Dev Blocker 的路由決定。</summary>
public record BlockerDecision(
    string Routing,       // "continue" | "escalate_victoria" | "escalate_boss"
    string Instructions);

// ────────────── 23-1：Review Appeal 結果 ──────────────

/// <summary>Cody 針對 Critical Issues 的逐條回應（Appeal Round A）。</summary>
public record CodyAppeal(IReadOnlyList<CodyAppealItem> Items);

/// <summary>Cody 針對單一 Critical Issue 的回應。</summary>
public record CodyAppealItem(
    int    Id,
    string Response, // "agree" | "disagree"
    string Reason);

/// <summary>Vera 重新評估 Cody 反駁後的結果。</summary>
public record VeraAppealResponse(
    IReadOnlyList<int> AcceptedIds,    // Vera 接受（從 critical 移除）的 IDs
    IReadOnlyList<int> MaintainedIds,  // Vera 維持的 IDs
    string UpdatedSummary);

/// <summary>Petra 仲裁 Cody-Vera 爭議的最終決定。</summary>
public record AppealArbitration(
    string Decision,                   // "support_vera" | "support_cody_partial" | "support_cody_full"
    IReadOnlyList<int> FinalCriticals, // 最終成立的 Critical IDs
    string Reasoning);

// ────────────── 24-1：QA 評估結果 ──────────────

/// <summary>Petra 對 QA 失敗的路由決定。</summary>
public record QaFailureDecision(
    string Routing,        // "code_bug" | "back_to_reviewer" | "env_or_test_issue" | "escalate_boss"
    string Instructions);

/// <summary>Petra 對 QA 無適合測試點的決定。</summary>
public record QaNoTestDecision(
    string Routing,        // "approve" | "escalate_boss"
    string Instructions);

// ────────────── 24-2：Dev_plan Appeal 結果 ──────────────

/// <summary>Cody 針對 Petra Dev_plan 修改意見的反駁（或接受）。</summary>
public record CodyDevPlanAppeal(
    string Position,    // "disagree" | "accept"
    string Reasoning);  // 反駁的技術論點（accept 時可為空）
