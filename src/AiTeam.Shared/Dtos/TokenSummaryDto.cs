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

    /// <summary>
    /// Stage 61-FF 五十：該 Agent 期間內任一筆 token_log 為 estimated（IsEstimated=true）→ true。
    /// Dashboard token 統計頁據此加視覺標記區分 actual vs estimated（Trial_v6 觀察期 follow-up）。
    /// </summary>
    public bool HasEstimated { get; set; }
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
