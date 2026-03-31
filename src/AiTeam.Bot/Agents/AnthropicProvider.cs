using Anthropic.SDK;
using Anthropic.SDK.Messaging;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Anthropic Claude API 實作，支援 Vision（圖片輸入）。
/// </summary>
public class AnthropicProvider(AnthropicClient client, string model) : ILlmProvider
{
    public async Task<LlmResponse> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        var message = BuildUserMessage(userMessage, images);

        var request = new MessageParameters
        {
            Model     = model,
            MaxTokens = 4096,
            System    = [new SystemMessage(systemPrompt)],
            Messages  = [message]
        };

        var response = await client.Messages.GetClaudeMessageAsync(request, cancellationToken);

        var content      = response.Message.ToString() ?? "";
        var inputTokens  = response.Usage?.InputTokens ?? 0;
        var outputTokens = response.Usage?.OutputTokens ?? 0;

        return new LlmResponse(content, inputTokens, outputTokens);
    }

    // ────────────── Private ──────────────

    private static Message BuildUserMessage(string userMessage, IReadOnlyList<ImageAttachment>? images)
    {
        // 無圖片：使用純文字訊息（與原本行為一致）
        if (images is null || images.Count == 0)
            return new Message(RoleType.User, userMessage);

        // 有圖片：組成多部分 content（圖片在前，文字在後）
        var contentBlocks = new List<ContentBase>();

        foreach (var img in images)
        {
            contentBlocks.Add(new ImageContent
            {
                Source = new ImageSource
                {
                    Type      = SourceType.base64,
                    MediaType = img.MediaType,
                    Data      = img.Base64Data
                }
            });
        }

        contentBlocks.Add(new TextContent { Text = userMessage });

        return new Message { Role = RoleType.User, Content = contentBlocks };
    }
}
