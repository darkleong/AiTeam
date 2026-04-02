using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;

namespace AiTeam.Bot.Agents;

/// <summary>
/// ILlmProvider Decorator：透明地記錄每次 LLM 呼叫的 Token 用量到 token_logs 資料表。
/// 包裝在 LlmProviderFactory.Create() 中，AgentService 無需任何改動。
/// </summary>
public class TokenTrackingProvider(
    ILlmProvider inner,
    TokenRepository tokenRepository,
    DashboardPushService dashboardPush,
    string agentName,
    string model) : ILlmProvider
{
    public async Task<LlmResponse> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        var response = await inner.CompleteAsync(systemPrompt, userMessage, cancellationToken, images);

        tokenRepository.Add(new TokenLog
        {
            AgentName    = agentName,
            Model        = model,
            InputTokens  = response.InputTokens,
            OutputTokens = response.OutputTokens,
            CreatedAt    = DateTime.UtcNow
        });
        await tokenRepository.SaveAsync(cancellationToken);

        // 通知 Dashboard 即時重整 Token 頁面
        await dashboardPush.PushTokenUpdateAsync();

        return response;
    }
}
