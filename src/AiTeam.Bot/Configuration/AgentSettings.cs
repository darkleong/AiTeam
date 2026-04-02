namespace AiTeam.Bot.Configuration;

public class AgentSettings
{
    public int RulesCacheTtlMinutes { get; set; } = 60;
    public int MonthlyTokenLimitK { get; set; } = 1000;
    public string DailyReportCron { get; set; } = "0 9,21 * * *";
    public string InternalApiKey { get; set; } = "";
    /// <summary>跳過 CEO 派工確認，直接進入 Agent 執行確認（準確率穩定後可啟用）。</summary>
    public bool SkipCeoConfirm { get; set; } = false;
    public Dictionary<string, AgentConfig> Agents { get; set; } = [];
}

public class AgentConfig
{
    public string Provider { get; set; } = "Anthropic";
    public string Model { get; set; } = "claude-sonnet-4-6";
    public int DailyTokenLimitK { get; set; } = 10;
    public int MonthlyTokenLimitK { get; set; } = 200;
}
