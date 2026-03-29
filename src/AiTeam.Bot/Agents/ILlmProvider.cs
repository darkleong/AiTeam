namespace AiTeam.Bot.Agents;

/// <summary>
/// LLM 供應商介面，每個 Agent 可獨立設定不同供應商的模型。
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// 送出 System Prompt + User Message，回傳模型的文字回應。
    /// </summary>
    Task<LlmResponse> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// LLM 回應，包含文字內容與實際用量。
/// </summary>
public record LlmResponse(string Content, int InputTokens, int OutputTokens);
