namespace AiTeam.Bot.Configuration;

public class AgentSettings
{
    public int RulesCacheTtlMinutes { get; set; } = 60;
    public int MonthlyTokenLimitK { get; set; } = 1000;
    /// <summary>單次請求估算 Token 上限（千 token）。超過時拒絕送出並發出警報。</summary>
    public int SingleRequestTokenLimitK { get; set; } = 50;
    public string HealthCheckCron { get; set; } = "0 */30 * * * ?";
    public string InternalApiKey { get; set; } = "";
    // Stage 85：SkipCeoConfirm 砍（v4 Discord ShowDirectAgentConfirmAsync caller Stage 78c 已砍 / dead flag）
    public Dictionary<string, AgentConfig> Agents { get; set; } = [];
}

public class AgentConfig
{
    public string Provider { get; set; } = "Anthropic";
    public string Model { get; set; } = "claude-sonnet-4-6";
    public int DailyTokenLimitK { get; set; } = 10;
    public int MonthlyTokenLimitK { get; set; } = 200;
}
