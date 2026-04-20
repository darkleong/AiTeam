namespace AiTeam.Dashboard.Services;

/// <summary>
/// 呼叫 Bot 內部 API（重啟等管理操作）。
/// </summary>
public class DashboardBotService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<DashboardBotService> logger)
{
    private readonly string _botInternalUrl  = configuration["Bot:InternalUrl"]  ?? "http://aiteam-bot:8080";
    private readonly string _botInternalKey  = configuration["Bot:InternalApiKey"] ?? "";

    /// <summary>呼叫 /internal/reload-cache，清除 Bot 端 Cache，回傳是否成功。</summary>
    public async Task<bool> ReloadCacheAsync(string scope = "all", CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{_botInternalUrl.TrimEnd('/')}/internal/reload-cache?scope={scope}");
            request.Headers.Add("X-Api-Key", _botInternalKey);
            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Bot 快取套用指令已送出（scope={Scope}）", scope);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "送出 Bot 快取套用指令失敗");
            return false;
        }
    }

    /// <summary>呼叫 /internal/tasks/{taskId}/requeue，將失敗 / 取消的任務重新入佇列。</summary>
    public async Task<bool> RequeueTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{_botInternalUrl.TrimEnd('/')}/internal/tasks/{taskId}/requeue");
            request.Headers.Add("X-Api-Key", _botInternalKey);
            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("TaskItem {Id} 重新入佇列指令已送出", taskId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "送出重新入佇列指令失敗（TaskId={Id}）", taskId);
            return false;
        }
    }

    /// <summary>呼叫 /internal/restart，回傳是否成功。</summary>
    public async Task<bool> RestartBotAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_botInternalUrl.TrimEnd('/')}/internal/restart");
            request.Headers.Add("X-Api-Key", _botInternalKey);

            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Bot 重啟指令已送出");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "送出 Bot 重啟指令失敗");
            return false;
        }
    }
}
