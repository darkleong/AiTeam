namespace AiTeam.Bot.Agents;

/// <summary>
/// Stage 58 (FF 五十三)：LLM API 呼叫失敗（餘額不足 / 401 / authentication_error 等）的業務 exception。
///
/// 跨 CLI path（ClaudeCodeService subprocess stdout signal 偵測）+ API path（AnthropicProvider SDK exception catch）
/// 兩條路統一拋此 exception，由 AgentQueueProcessor specific catch（在 generic catch 之前）build
/// `[API_FAILURE]` 前綴 AgentExecutionResult + call HandleAgentCompletedAsync 走正常 callback flow，
/// 4 Pipeline Stage Executor HandleResponseAsync 第一行 marker check → fire agent_api_failure_intervention BossInteraction + yield 等 Christ 真三選（continue / retry / abort）。
///
/// 設計（v1.1 議題 14 拍板）：
///   - ProviderType：LlmProviderType enum 對齊既有 GeminiProvider Stage 37 + 預留擴充
///   - RawError：string capped 500 chars（CLI path stdout 摘要 / API path SDK exception.Message） — 純文字最 flexible 給 BossInteraction.Description Christ 直接判讀
/// </summary>
public sealed class LlmApiFailureException : Exception
{
    public LlmProviderType ProviderType { get; }

    /// <summary>原始錯誤訊息（capped 500 chars）— CLI path 取 stdout result.result 文字 / API path 取 SDK exception.Message。</summary>
    public string RawError { get; }

    public LlmApiFailureException(LlmProviderType providerType, string rawError)
        : base($"LLM API failure ({providerType}): {Truncate(rawError, 500)}")
    {
        ProviderType = providerType;
        RawError = Truncate(rawError, 500);
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? "" : (s.Length > max ? s[..max] : s);
}

/// <summary>Stage 58：LLM provider 類型（Anthropic / Gemini / Unknown） — 對齊既有 GeminiProvider Stage 37 + 預留擴充。</summary>
public enum LlmProviderType
{
    Anthropic,
    Gemini,
    Unknown
}
