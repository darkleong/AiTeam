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
/// </summary>
/// <param name="Success">是否成功。</param>
/// <param name="Summary">Discord embed 用的一行摘要。</param>
/// <param name="OutputUrl">PR URL / Issue URL 等輸出連結（可為 null）。</param>
public record AgentExecutionResult(bool Success, string Summary, string? OutputUrl = null);

/// <summary>
/// Agent 描述子，用於 CEO 系統提示與動態分派。
/// </summary>
/// <param name="Name">Agent 名稱（與 DI key 一致）。</param>
/// <param name="Description">Agent 職責描述，供 CEO LLM 判斷分派對象。</param>
public record AgentDescriptor(string Name, string Description);
