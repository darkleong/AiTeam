using AiTeam.Data;

namespace AiTeam.Bot.Agents;

/// <summary>
/// 所有可被 CEO 分派的執行 Agent 必須實作此介面。
/// CommandHandler 僅依賴此介面，不知道具體 Agent 類型，達成動態分派。
/// </summary>
public interface IAgentExecutor
{
    /// <summary>
    /// 執行 CEO 分派的任務，回傳結果摘要。
    /// 實作方自行管理規劃與執行的內部步驟。
    /// </summary>
    Task<AgentExecutionResult> ExecuteTaskAsync(
        TaskItem task,
        string owner,
        string repo,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Agent 執行結果。
/// Stage 10 新增：IsWaitingInput（暫停等待輸入）、QuestionType、Question（回報問題內容）、
/// CriticalReviewCount（Vera 回傳，用於 Review 閉環判斷）。
/// </summary>
/// <param name="Success">是否成功。</param>
/// <param name="Summary">Discord embed 用的一行摘要。</param>
/// <param name="OutputUrl">PR URL / Issue URL 等輸出連結（可為 null）。</param>
/// <param name="IsWaitingInput">Agent 是否暫停等待老闆或上游補充資訊。</param>
/// <param name="QuestionType">問題類型：requirement / ui_spec / business_decision。</param>
/// <param name="Question">暫停時要問的問題內容。</param>
/// <param name="CriticalReviewCount">Vera 審查出的 critical 問題數量，0 表示通過。</param>
/// <param name="ReviewBody">Vera 的完整審查報告 markdown（fix loop 傳給 Dev 用）。</param>
/// <param name="OutputContent">Demi 產出的 UI 規格 markdown 全文（Stage 12：存入 TaskGroup.UiSpecContent）。</param>
public record AgentExecutionResult(
    bool Success,
    string Summary,
    string? OutputUrl = null,
    bool IsWaitingInput = false,
    string? QuestionType = null,
    string? Question = null,
    int CriticalReviewCount = 0,
    string? ReviewBody = null,
    IReadOnlyList<string>? OutputUrls = null,
    string? OutputContent = null)
{
    /// <summary>建立「暫停並回報問題」的結果（不需 CEO 走 LLM，由 Orchestrator 路由）。</summary>
    public static AgentExecutionResult PauseAndAsk(string questionType, string question)
        => new(false, question, IsWaitingInput: true,
               QuestionType: questionType, Question: question);
}

/// <summary>
/// Agent 描述子，用於 CEO 系統提示與動態分派。
/// </summary>
/// <param name="Name">Agent 名稱（與 DI key 一致）。</param>
/// <param name="Description">Agent 職責描述，供 CEO LLM 判斷分派對象。</param>
public record AgentDescriptor(string Name, string Description);
