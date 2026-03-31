namespace AiTeam.Bot.Agents;

/// <summary>
/// LLM 供應商介面，每個 Agent 可獨立設定不同供應商的模型。
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// 送出 System Prompt + User Message，回傳模型的文字回應。
    /// 可選傳入圖片附件（Base64），支援 Vision 能力的供應商（如 Claude Sonnet）會一併處理。
    /// </summary>
    Task<LlmResponse> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ImageAttachment>? images = null);
}

/// <summary>
/// LLM 回應，包含文字內容與實際用量。
/// </summary>
public record LlmResponse(string Content, int InputTokens, int OutputTokens);

/// <summary>
/// 圖片附件，以 Base64 格式傳遞給支援 Vision 的模型。
/// </summary>
public record ImageAttachment(string Base64Data, string MediaType);
