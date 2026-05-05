namespace AiTeam.Bot.Agents;

/// <summary>
/// Stage 56：FF 四十三 修法（議題 spike-2 選項 B）—
/// 當 LLM provider 未直接回傳 total_cost_usd 時，從 token 數 × 各 model 官方費率估算。
///
/// 兩個用途：
///   ① CLI path（TokenLogService）— ClaudeCodeService.TryParseUsage 找不到 cost 欄位時 fallback 估算
///   ② API path（TokenTrackingProvider）— Stage 44 設計缺口（TotalCostUsd 連寫都不寫），統一接此 estimator
///
/// 公式：input × input_price + output × output_price + cache_creation × cache_creation_price + cache_read × cache_read_price
/// 費率資料來源：Anthropic 官方公開 pricing（USD per 1M tokens）— Model 升級時改 const dict 一處。
/// 未匹配 model（罕見）→ fallback 走 Sonnet 費率，避免 NullReference。
/// </summary>
public class TokenCostEstimator
{
    private record ModelPricing(decimal InputPer1M, decimal OutputPer1M, decimal CacheCreatePer1M, decimal CacheReadPer1M);

    // Anthropic 官方 pricing（USD per 1M tokens）— 2026-05 維護點。
    // cache_creation = input × 1.25；cache_read = input × 0.1（對齊 Anthropic 公開倍率）。
    private static readonly Dictionary<string, ModelPricing> Prices = new(StringComparer.OrdinalIgnoreCase)
    {
        // Claude 4.x 家族
        ["claude-opus-4-7"]      = new(15.00m, 75.00m, 18.75m, 1.50m),
        ["claude-opus-4-6"]      = new(15.00m, 75.00m, 18.75m, 1.50m),
        ["claude-opus-4"]        = new(15.00m, 75.00m, 18.75m, 1.50m),
        ["claude-sonnet-4-6"]    = new( 3.00m, 15.00m,  3.75m, 0.30m),
        ["claude-sonnet-4-5"]    = new( 3.00m, 15.00m,  3.75m, 0.30m),
        ["claude-sonnet-4"]      = new( 3.00m, 15.00m,  3.75m, 0.30m),
        ["claude-haiku-4-5"]     = new( 1.00m,  5.00m,  1.25m, 0.10m),
        ["claude-haiku-4-5-20251001"] = new(1.00m, 5.00m, 1.25m, 0.10m),
    };

    private static readonly ModelPricing FallbackSonnet = new(3.00m, 15.00m, 3.75m, 0.30m);

    /// <summary>
    /// 估算總成本（USD）。回傳 (cost, isEstimated=true)；isEstimated 永遠 true（呼叫端決定要不要寫入）。
    /// </summary>
    public (decimal cost, bool isEstimated) Estimate(
        string model,
        int inputTokens,
        int outputTokens,
        int cacheCreate = 0,
        int cacheRead = 0)
    {
        var p = ResolvePricing(model);
        var cost =
              (decimal)inputTokens   * p.InputPer1M       / 1_000_000m
            + (decimal)outputTokens  * p.OutputPer1M      / 1_000_000m
            + (decimal)cacheCreate   * p.CacheCreatePer1M / 1_000_000m
            + (decimal)cacheRead     * p.CacheReadPer1M   / 1_000_000m;
        return (decimal.Round(cost, 6, MidpointRounding.AwayFromZero), true);
    }

    private static ModelPricing ResolvePricing(string model)
    {
        if (string.IsNullOrWhiteSpace(model)) return FallbackSonnet;
        if (Prices.TryGetValue(model, out var exact)) return exact;
        // prefix 匹配（如 claude-sonnet-4-6-20260101 → claude-sonnet-4-6）
        foreach (var (key, p) in Prices)
            if (model.StartsWith(key, StringComparison.OrdinalIgnoreCase)) return p;
        return FallbackSonnet;
    }
}
