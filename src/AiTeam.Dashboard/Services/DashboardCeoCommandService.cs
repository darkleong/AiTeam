using System.Text.Json;

namespace AiTeam.Dashboard.Services;

/// <summary>
/// Stage 29-5：呼叫 Bot /internal/ceo/command，將 Dashboard 指令轉發給 Victoria。
/// </summary>
public class DashboardCeoCommandService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<DashboardCeoCommandService> logger)
{
    private readonly string _botInternalUrl = configuration["Bot:InternalUrl"]  ?? "http://aiteam-bot:8080";
    private readonly string _botInternalKey = configuration["Bot:InternalApiKey"] ?? "";

    /// <summary>
    /// 發送指令給 Victoria。回傳 (success, action, reply) 三元組；失敗時 success=false，errorMessage 含說明。
    /// </summary>
    public async Task<CeoCommandResult> SendCommandAsync(
        string text,
        IReadOnlyList<ImageUploadDto>? images = null,
        CancellationToken ct = default)
    {
        try
        {
            var body = new
            {
                Text   = text,
                Images = images?.Select(i => new { i.Base64Data, i.MediaType }).ToList()
            };

            var client  = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{_botInternalUrl.TrimEnd('/')}/internal/ceo/command");
            request.Headers.Add("X-Api-Key", _botInternalKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                string? errorMsg = null;
                try
                {
                    using var doc = JsonDocument.Parse(errorBody);
                    errorMsg = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
                }
                catch { /* ignore */ }
                logger.LogWarning("Dashboard 指令發送失敗（{Code}）：{Body}", response.StatusCode, errorBody);
                return new CeoCommandResult(false, null, null, errorMsg ?? "指令發送失敗，請確認設定是否完整。");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var resultDoc = JsonDocument.Parse(json);
            var root   = resultDoc.RootElement;
            var action = root.TryGetProperty("action", out var a) ? a.GetString() : null;
            var reply  = root.TryGetProperty("reply",  out var r) ? r.GetString() : null;

            logger.LogInformation("Dashboard 指令已送達 Victoria（action={Action}）", action);
            return new CeoCommandResult(true, action, reply, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dashboard 指令發送時發生例外");
            return new CeoCommandResult(false, null, null, "連線失敗，請確認 Bot 服務正常。");
        }
    }
}

public record CeoCommandResult(bool Success, string? Action, string? Reply, string? ErrorMessage);

public record ImageUploadDto(string Base64Data, string MediaType);
