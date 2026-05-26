using AiTeam.Bot.Configuration;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Services;

/// <summary>
/// 向 Discord #警報 頻道發送警報訊息。
/// 注冊為 Singleton（因為 DiscordSocketClient 是 Singleton）。
/// 發送失敗只記錄 Warning，不影響呼叫方的主要流程。
///
/// Stage 85 子項 1：加 SendThrottledAsync API — per event type per N min 只發 1 則 + aggregate 描述「N 次同類事件」，
/// 同步 fire-and-forget 推 DashboardPushService.PushAlertAsync（SignalR → Dashboard MudSnackbar toast）。
/// </summary>
public class DiscordAlertService(
    DiscordSocketClient client,
    AlertRateLimiter rateLimiter,
    DashboardPushService dashboardPush,
    IOptions<WorkflowSettings> workflowSettings,
    IOptions<DiscordSettings> settings,
    ILogger<DiscordAlertService> logger)
{
    private readonly DiscordSettings _settings = settings.Value;
    private readonly WorkflowSettings _workflow = workflowSettings.Value;

    /// <summary>發送訊息至 #警報 頻道（既有 API / 0 rate-limit / 0 SignalR push — 對齊既有 caller 行為）。</summary>
    public async Task SendAsync(string message)
    {
        try
        {
            if (!ulong.TryParse(_settings.GuildId, out var guildId)) return;

            var channel = client.GetGuild(guildId)
                ?.TextChannels.FirstOrDefault(c => c.Name == _settings.Channels.Alerts);

            if (channel is null)
            {
                logger.LogWarning("找不到 Discord 警報頻道 #{Channel}", _settings.Channels.Alerts);
                return;
            }

            await channel.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "發送 Discord 警報失敗，訊息：{Message}", message);
        }
    }

    /// <summary>Stage 85 子項 1：throttled send — per event type per N min 限頻 + aggregate 文案 + 同步 SignalR push 給 Dashboard toast。
    /// v4-rewrite：直接從 IOptions&lt;WorkflowSettings&gt; 讀 AlertRateLimitMinutes（WorkflowSettingsResolver 砍 / DB 動態調整需求 v4-rewrite 後重評）。</summary>
    public async Task SendThrottledAsync(string eventType, string message)
    {
        var windowMin = _workflow.AlertRateLimitMinutes;
        var window = TimeSpan.FromMinutes(windowMin);

        if (!rateLimiter.TryAcquire(eventType, window, out var suppressed))
        {
            // skip / suppressedCount 已內部累加
            return;
        }

        var finalMsg = suppressed > 0
            ? $"{message}\n\n_（過去 {windowMin} 分鐘內已抑制 {suppressed} 則同類事件）_"
            : message;

        await SendAsync(finalMsg);

        // fire-and-forget SignalR push（顯式 discard 避免 CS4014 / 對齊 refactor-sop v1.3 第 7 條 warning baseline）
        _ = Task.Run(async () =>
        {
            try { await dashboardPush.PushAlertAsync(eventType, "warning", finalMsg); }
            catch (Exception ex) { logger.LogWarning(ex, "PushAlertAsync 失敗（Discord push 已完成 / SignalR 推送 swallow）"); }
        });
    }
}
