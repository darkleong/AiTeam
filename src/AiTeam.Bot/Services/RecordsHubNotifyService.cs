namespace AiTeam.Bot.Services;

/// <summary>
/// F16：MCP record 寫入後 fire-and-forget 通知 Dashboard SignalR Hub。
///
/// 對齊 DashboardPushService 既有 named HttpClient "aiteam-dashboard" pattern
/// （重用 builder.Configuration["Dashboard:PushUrl"] 設定 / 不另開 DashboardSettings class）。
///
/// 設計紀律：
///   - fire-and-forget / 不阻塞 MCP tool 主流程
///   - 失敗只 log Warning / 不 throw（best effort / not critical）
///   - 對齊 RecordNotificationService _ = Task.Run(...) swallow exception pattern
/// </summary>
public class RecordsHubNotifyService(
    IHttpClientFactory httpClientFactory,
    ILogger<RecordsHubNotifyService> logger)
{
    /// <summary>Fire-and-forget 通知 Dashboard Records.razor 整段 reload。</summary>
    public void FireAndForget()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var client = httpClientFactory.CreateClient("aiteam-dashboard");
                await client.PostAsync("/api/internal/records/updated", null);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SignalR records hub notify failed (best effort / not critical)");
            }
        });
    }
}
