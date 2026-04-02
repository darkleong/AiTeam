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

    // Stage 7：各 Agent 的專屬頻道名稱
    public string CeoChannel         { get; set; } = "victoria-ceo";
    public string DevChannel         { get; set; } = "cody-dev";
    public string OpsChannel         { get; set; } = "maya-ops";
    public string QaChannel          { get; set; } = "quinn-qa";
    public string DocChannel         { get; set; } = "sage-doc";
    public string RequirementsChannel { get; set; } = "rosa-requirements";
    public string ReviewerChannel    { get; set; } = "vera-reviewer";
    public string ReleaseChannel     { get; set; } = "rena-release";
    public string DesignerChannel    { get; set; } = "demi-designer";
}
