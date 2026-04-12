using AiTeam.Bot.Configuration;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Services;

/// <summary>
/// 向 Discord #警報 頻道發送警報訊息。
/// 注冊為 Singleton（因為 DiscordSocketClient 是 Singleton）。
/// 發送失敗只記錄 Warning，不影響呼叫方的主要流程。
/// </summary>
public class DiscordAlertService(
    DiscordSocketClient client,
    IOptions<DiscordSettings> settings,
    ILogger<DiscordAlertService> logger)
{
    private readonly DiscordSettings _settings = settings.Value;

    /// <summary>發送訊息至 #警報 頻道。</summary>
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
}
