using Anthropic.SDK;
using Anthropic.SDK.Messaging;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Anthropic Claude API 實作。
/// </summary>
public class AnthropicProvider(AnthropicClient client, string model) : ILlmProvider
{
    public async Task<LlmResponse> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var request = new MessageParameters
        {
            Model = model,
            MaxTokens = 4096,
            System = [new SystemMessage(systemPrompt)],
            Messages = [new Message(RoleType.User, userMessage)]
        };

        var response = await client.Messages.GetClaudeMessageAsync(request, cancellationToken);

        var content = response.Message.ToString() ?? "";
        var inputTokens = response.Usage?.InputTokens ?? 0;
        var outputTokens = response.Usage?.OutputTokens ?? 0;

        return new LlmResponse(content, inputTokens, outputTokens);
    }
}
