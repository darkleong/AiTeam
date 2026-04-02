using AiTeam.Bot.Configuration;
using AiTeam.Data.Repositories;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Discord;

/// <summary>
/// Discord Bot 主服務，負責連線、接收指令、發送訊息。
/// </summary>
public class DiscordBotService(
    DiscordSocketClient client,
    CommandHandler commandHandler,
    IOptions<DiscordSettings> settings,
    IServiceScopeFactory scopeFactory,
    ILogger<DiscordBotService> logger) : BackgroundService
{
    private readonly DiscordSettings _settings = settings.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 清理上次 Bot 非正常關閉留下的殘留「執行中」任務
        await CleanupStaleTasksAsync(stoppingToken);

        client.Log += OnLog;
        client.Ready += OnReady;

        await client.LoginAsync(TokenType.Bot, _settings.BotToken);
        await client.StartAsync();

        // 保持服務運行直到取消
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.StopAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task OnReady()
    {
        logger.LogInformation("Discord Bot 已上線，登入為 {Username}", client.CurrentUser.Username);
        await client.SetStatusAsync(UserStatus.Online);
        await client.SetGameAsync("等待指令...");

        // Ready 後 Guild 快取才完整，此時才能註冊斜線指令
        await commandHandler.RegisterCommandsAsync();
        await EnsureChannelsAsync();
    }

    /// <summary>
    /// 檢查並自動建立缺少的頻道。
    /// </summary>
    private async Task EnsureChannelsAsync()
    {
        if (!ulong.TryParse(_settings.GuildId, out var guildId)) return;
        var guild = client.GetGuild(guildId);
        if (guild is null) return;

        var required = new[]
        {
            _settings.Channels.CommandCenter,
            _settings.Channels.TaskUpdates,
            _settings.Channels.Alerts,
            _settings.Channels.DailySummary,
            // Stage 7：各 Agent 的專屬頻道
            _settings.Channels.CeoChannel,
            _settings.Channels.DevChannel,
            _settings.Channels.OpsChannel,
            _settings.Channels.QaChannel,
            _settings.Channels.DocChannel,
            _settings.Channels.RequirementsChannel,
            _settings.Channels.ReviewerChannel,
            _settings.Channels.ReleaseChannel,
            _settings.Channels.DesignerChannel,
        };

        foreach (var name in required)
        {
            if (guild.TextChannels.Any(c => c.Name == name)) continue;
            await guild.CreateTextChannelAsync(name);
            logger.LogInformation("已建立 Discord 頻道：#{Name}", name);
        }
    }

    private async Task CleanupStaleTasksAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
            var count = await repo.MarkStaleRunningTasksAsync(cancellationToken);
            if (count > 0)
                logger.LogWarning("Bot 重啟清理：已將 {Count} 筆殘留「執行中」任務標記為「失敗」", count);
            else
                logger.LogInformation("Bot 重啟清理：無殘留「執行中」任務");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bot 重啟清理殘留任務失敗");
        }
    }

    private Task OnLog(LogMessage log)
    {
        var level = log.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error    => LogLevel.Error,
            LogSeverity.Warning  => LogLevel.Warning,
            LogSeverity.Info     => LogLevel.Information,
            LogSeverity.Verbose  => LogLevel.Debug,
            LogSeverity.Debug    => LogLevel.Trace,
            _                    => LogLevel.Information
        };
        logger.Log(level, log.Exception, "[Discord] {Message}", log.Message);
        return Task.CompletedTask;
    }
}
