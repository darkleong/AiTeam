using AiTeam.Bot.Configuration;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Services;

/// <summary>
/// Stage 93：v4-rewrite MCP record event Discord notification 服務（純被動 push）。
///
/// 對齊拍板 #5：HITL 雙向砍 / Discord 留純通知。
/// Push 到 TaskUpdates channel（不是 Alerts / 對齊「Task 進度通知」語義）。
/// 發送失敗只 log Warning、不影響 MCP record tool 主流程（fire-and-forget caller 端 swallow）。
/// </summary>
public class RecordNotificationService(
    DiscordSocketClient client,
    IOptions<DiscordSettings> settings,
    ILogger<RecordNotificationService> logger)
{
    private readonly DiscordSettings _settings = settings.Value;

    /// <summary>
    /// Push 訊息到 TaskUpdates channel。caller 應用 _ = Task.Run(...) 包 fire-and-forget。
    /// </summary>
    public async Task SendAsync(string message)
    {
        try
        {
            if (!ulong.TryParse(_settings.GuildId, out var guildId))
            {
                logger.LogDebug("RecordNotification skip — GuildId 未設定");
                return;
            }

            var channel = client.GetGuild(guildId)
                ?.TextChannels.FirstOrDefault(c => c.Name == _settings.Channels.TaskUpdates);

            if (channel is null)
            {
                logger.LogWarning("RecordNotification skip — 找不到 Discord 頻道 #{Channel}", _settings.Channels.TaskUpdates);
                return;
            }

            await channel.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RecordNotification 發送失敗（message={Message}）", message);
        }
    }
}
