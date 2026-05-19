namespace AiTeam.Dashboard.Services;

/// <summary>
/// 呼叫 Bot 內部 API（重啟、Cache 清除等管理操作）。
///
/// Stage 78c：v4 Pipeline framework 整套砍後 DashboardBotService 縮為 v5.5 essential methods：
///   - ReloadCacheAsync（Bot Cache 重新載入 / Dashboard 改完設定）
///   - RestartBotAsync（Bot 重啟 / Dashboard 用）
///
/// 砍範圍（Stage 78c）：
///   - RequeueTaskAsync（v4 AgentQueueService 砍）
///   - TriggerMockScenarioAsync（v4 MockScenarioService 砍 / 議題 7）
///   - PauseAgentAsync / ResumeAgentAsync / StopAllAsync / ResumeAllAsync（v4 AgentQueueControlService 砍）
///   - PostQueueControlAsync（私 helper / 0 caller after）
///   - PauseTaskGroupAsync / ResumeTaskGroupAsync（v4 TaskGroupService 砍）
///   - PauseEpicAsync / ResumeEpicAsync（v4 TaskGroupService.EpicChainService 砍）
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
