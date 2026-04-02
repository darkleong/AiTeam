namespace AiTeam.Shared.Dtos;

/// <summary>
/// 某段時間內單一 Agent 的 Token 用量彙總，供 Dashboard 顯示。
/// </summary>
public class TokenAgentSummaryDto
{
    public string AgentName { get; set; } = "";
    public string Model { get; set; } = "";
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}

/// <summary>
/// 某段時間內每日每個 Agent 的 Token 用量，供折線圖使用。
/// </summary>
public class TokenDailyDataPointDto
{
    public DateTime Date { get; set; }
    public string AgentName { get; set; } = "";
    public int TotalTokens { get; set; }
}

/// <summary>
/// /internal/tokens 回傳的完整資料，包含 Agent 彙總與每日數據點。
/// </summary>
public class TokenSummaryDto
{
    public List<TokenAgentSummaryDto> AgentSummaries { get; set; } = [];
    public List<TokenDailyDataPointDto> DailyDataPoints { get; set; } = [];
}
