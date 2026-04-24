namespace AiTeam.Dashboard.Configuration;

/// <summary>
/// Dashboard 端讀取 Agent Token 限額的設定類別（僅限顯示用）。
/// 對應 appsettings.json 的 AgentSettings section。
/// Bot 端的完整設定（含 Provider / Model / CronStrings 等）定義於 AiTeam.Bot.Configuration.AgentSettings。
/// </summary>
public class AgentTokenLimits
{
    /// <summary>全域月費 Token 上限（千 token）。</summary>
    public int MonthlyTokenLimitK { get; set; } = 1000;

    /// <summary>各 Agent 的日限/月限設定。Key = Agent 名稱（如 "CEO"、"Dev"）。</summary>
    public Dictionary<string, AgentLimit> Agents { get; set; } = [];
}

/// <summary>單一 Agent 的 Token 限額設定。</summary>
public class AgentLimit
{
    /// <summary>日用量上限（千 token）。</summary>
    public int DailyTokenLimitK { get; set; } = 10;

    /// <summary>月用量上限（千 token）。</summary>
    public int MonthlyTokenLimitK { get; set; } = 200;

    /// <summary>LLM 提供者（如 "Anthropic"、"Gemini"）。</summary>
    public string Provider { get; set; } = "Anthropic";

    /// <summary>LLM 模型名稱（如 "claude-sonnet-4-6"、"gemini-2.5-flash"）。</summary>
    public string Model { get; set; } = "claude-sonnet-4-6";
}
