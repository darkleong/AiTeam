namespace AiTeam.Bot.Configuration;

public class AgentSettings
{
    public int NotionCacheTtlMinutes { get; set; } = 60;
    public int MonthlyTokenLimitK { get; set; } = 1000;
    public string DailyReportCron { get; set; } = "0 9,21 * * *";
    public Dictionary<string, AgentConfig> Agents { get; set; } = [];
}

public class AgentConfig
{
    public string Provider { get; set; } = "Anthropic";
    public string Model { get; set; } = "claude-sonnet-4-6";
    public int DailyTokenLimitK { get; set; } = 10;
    public int MonthlyTokenLimitK { get; set; } = 200;
}
