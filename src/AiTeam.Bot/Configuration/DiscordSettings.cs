namespace AiTeam.Bot.Configuration;

public class DiscordSettings
{
    public string BotToken { get; set; } = "";
    public string GuildId { get; set; } = "";
    public DiscordChannelSettings Channels { get; set; } = new();
}

public class DiscordChannelSettings
{
    public string CommandCenter { get; set; } = "指令中心";
    public string TaskUpdates { get; set; } = "任務動態";
    public string Alerts { get; set; } = "警報";
    public string DailySummary { get; set; } = "每日摘要";
}
