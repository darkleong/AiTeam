namespace AiTeam.Bot.Agents;

/// <summary>
/// Stage 44：Claude Code CLI subprocess 結束時抓到的 token 用量。
/// 解析自 --output-format json 的 type=result 物件 usage 欄位 + 頂層 total_cost_usd。
/// 解析失敗或 schema 不符時為 null（呼叫端 LogCliUsageAsync 會 early return，不影響主流程）。
/// Stage 56：加 IsEstimated 欄位，標記 TotalCostUsd 是否為 fallback 估算值。
/// </summary>
public record TokenUsage(
    int InputTokens,
    int OutputTokens,
    int CacheCreationTokens,
    int CacheReadTokens,
    decimal? TotalCostUsd,
    bool IsEstimated = false);
