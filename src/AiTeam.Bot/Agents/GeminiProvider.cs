using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Google Gemini API 實作（FF 四第一階段）：純文字輸入／輸出。
/// Vision / Tool Use 第一階段不支援——images 參數會被忽略並 log warning。
/// API key 走 query string（Gemini 標準做法），token 用量從 response 的 usageMetadata 取得。
/// 錯誤處理：429 rate limit 會拋可識別訊息，其他非 2xx 拋 HttpRequestException。
/// </summary>
public class GeminiProvider(
    HttpClient http,
    string apiKey,
    string model,
    ILogger<GeminiProvider>? logger = null) : ILlmProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<LlmResponse> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        if (images is { Count: > 0 })
            logger?.LogWarning("GeminiProvider 第一階段尚未支援 Vision，已忽略 {Count} 張圖片。", images.Count);

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Gemini API key 未設定（Gemini:ApiKey / AITEAM_GEMINI_KEY）。");

        var request = new GeminiRequest
        {
            SystemInstruction = new GeminiContent
            {
                Parts = [new GeminiPart { Text = systemPrompt }]
            },
            Contents =
            [
                new GeminiContent
                {
                    Role  = "user",
                    Parts = [new GeminiPart { Text = userMessage }]
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                MaxOutputTokens = 4096
            }
        };

        // BaseAddress 末尾有 slash，相對路徑不可開頭加 slash（否則會覆寫路徑）
        var endpoint = $"models/{model}:generateContent?key={apiKey}";

        using var response = await http.PostAsJsonAsync(endpoint, request, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Gemini API rate limit exceeded (429)。免費額度為 15 req/min、1500 req/day。回應：{Truncate(body, 500)}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Gemini API 回傳非成功狀態（{(int)response.StatusCode} {response.StatusCode}）。回應：{Truncate(body, 1000)}");
        }

        GeminiResponse? parsed;
        try
        {
            parsed = await response.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
            throw new InvalidOperationException($"Gemini 回應 JSON 解析失敗：{ex.Message}。原始回應：{Truncate(body, 1000)}", ex);
        }

        if (parsed is null)
            throw new InvalidOperationException("Gemini 回應為 null。");

        var content = parsed.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "";
        var inputTokens  = parsed.UsageMetadata?.PromptTokenCount ?? 0;
        var outputTokens = parsed.UsageMetadata?.CandidatesTokenCount ?? 0;

        return new LlmResponse(content, inputTokens, outputTokens);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";

    // ────────────── 請求 DTO ──────────────

    private sealed class GeminiRequest
    {
        [JsonPropertyName("systemInstruction")]
        public GeminiContent? SystemInstruction { get; set; }

        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = [];

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = [];
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }

    private sealed class GeminiGenerationConfig
    {
        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }
    }

    // ────────────── 回應 DTO ──────────────

    private sealed class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }

        [JsonPropertyName("usageMetadata")]
        public GeminiUsageMetadata? UsageMetadata { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }

    private sealed class GeminiUsageMetadata
    {
        [JsonPropertyName("promptTokenCount")]
        public int PromptTokenCount { get; set; }

        [JsonPropertyName("candidatesTokenCount")]
        public int CandidatesTokenCount { get; set; }

        [JsonPropertyName("totalTokenCount")]
        public int TotalTokenCount { get; set; }
    }
}
