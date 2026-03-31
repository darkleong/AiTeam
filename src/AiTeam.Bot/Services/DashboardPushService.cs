using System.Net.Http.Json;
using AiTeam.Shared.ViewModels;

namespace AiTeam.Bot.Services;

/// <summary>
/// 推送 Agent 狀態至 Dashboard 的服務。
/// Bot 和 Dashboard 是不同 Process，透過 HTTP API 橋接 SignalR 推送。
/// 推送失敗只記錄 Warning，不影響 Bot 正常運作。
/// </summary>
public class DashboardPushService(
    IHttpClientFactory httpClientFactory,
    ILogger<DashboardPushService> logger)
{
    /// <summary>推送 Agent 狀態變動至 Dashboard，由 Dashboard 轉發至 SignalR Hub。</summary>
    public async Task PushAgentStatusAsync(AgentStatusViewModel status)
    {
        try
        {
            var client = httpClientFactory.CreateClient("aiteam-dashboard");
            var response = await client.PostAsJsonAsync("/internal/agent-status", status);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("推送 Agent 狀態至 Dashboard 回傳 {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            // Dashboard 可能尚未啟動或離線，非關鍵錯誤，只記錄 Warning
            logger.LogWarning(ex, "推送 Agent 狀態至 Dashboard 失敗（非關鍵錯誤）");
        }
    }

    /// <summary>推送任務狀態變動至 Dashboard，由 Dashboard 轉發至 SignalR Hub，觸發任務中心自動重新整理。</summary>
    public async Task PushTaskUpdateAsync(TaskUpdateViewModel taskUpdate)
    {
        try
        {
            var client = httpClientFactory.CreateClient("aiteam-dashboard");
            var response = await client.PostAsJsonAsync("/internal/agent-status/task", taskUpdate);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("推送任務更新至 Dashboard 回傳 {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "推送任務更新至 Dashboard 失敗（非關鍵錯誤）");
        }
    }
}
