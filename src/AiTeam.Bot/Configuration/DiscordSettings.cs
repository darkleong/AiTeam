namespace AiTeam.Bot.Configuration;

public class DiscordSettings
{
    public string BotToken { get; set; } = "";
    public string GuildId { get; set; } = "";
    public DiscordChannelSettings Channels { get; set; } = new();
}

public class DiscordChannelSettings
{
    public string TaskUpdates { get; set; } = "任務動態";
    public string Alerts { get; set; } = "警報";
    public string DailySummary { get; set; } = "每日摘要";

    // v5.5 6 Talent baseline 對應頻道（Stage 78a 砍 Rosa/Demi/Rena + Maya 未實作後）
    public string CeoChannel      { get; set; } = "victoria-ceo";
    public string PmChannel       { get; set; } = "petra-pm";
    public string DevChannel      { get; set; } = "cody-dev";
    public string ReviewerChannel { get; set; } = "vera-reviewer";
    public string QaChannel       { get; set; } = "quinn-qa";
    public string DocChannel      { get; set; } = "sage-doc";
}
