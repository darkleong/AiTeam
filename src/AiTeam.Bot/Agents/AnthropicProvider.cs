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

        Anthropic.SDK.Messaging.MessageResponse response;
        try
        {
            response = await client.Messages.GetClaudeMessageAsync(request, cancellationToken);
        }
        catch (Exception ex) when (IsApiFailureException(ex))
        {
            // Stage 58 (FF 五十三)：API path SDK exception → 轉拋業務 exception 給 AgentQueueProcessor specific catch 接手
            throw new LlmApiFailureException(LlmProviderType.Anthropic, ex.Message);
        }

        var content      = response.Message.ToString() ?? "";
        var inputTokens  = response.Usage?.InputTokens ?? 0;
        var outputTokens = response.Usage?.OutputTokens ?? 0;

        return new LlmResponse(content, inputTokens, outputTokens);
    }

    /// <summary>
    /// Stage 58 (FF 五十三)：偵測 Anthropic SDK 拋的 exception 是否為 API 餘額不足 / 401 等失敗（非取消 / 非 timeout）。
    ///
    /// Anthropic.SDK 5.10 無 strongly-typed AnthropicAPIException — 用 case-insensitive Message substring 配對：
    ///   - "Credit balance is too low" / "insufficient_balance" / "401" / "authentication_error"
    ///
    /// 不攔 OperationCanceledException（取消） / TaskCanceledException — 那些是 ct cancel 應該往上傳。
    /// </summary>
    private static bool IsApiFailureException(Exception ex)
    {
        if (ex is OperationCanceledException) return false;

        var msg = (ex.Message ?? "").ToLowerInvariant();
        return msg.Contains("credit balance is too low")
            || msg.Contains("insufficient_balance")
            || msg.Contains("authentication_error")
            || msg.Contains("401");
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
