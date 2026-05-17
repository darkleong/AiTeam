namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 76：v5.5 Phase 3 補強 — Petra 執行錯誤分類器（retry 路由用）。
///
/// 對齊業界 multi-agent 5 failure modes：
/// - mode 1+2+3 (transient infrastructure / LLM hallucination / tool call) → Transient → auto retry
/// - mode 4 (business rule rejection — Token 守門 / quota / rate limit) → BusinessRule → fail-fast 不 retry（retry 會無限循環 + 燒 cost）
/// - 其他未知 → Permanent → fail-fast（標 failed 等人工介入）
///
/// Trial_v21 揭真實 fire pattern reference：Token 守門 message「Token 守門：全域本月用量 X 超過全域月限 Y」→ BusinessRule fail-fast。
/// </summary>
public static class PetraErrorClassifier
{
    /// <summary>
    /// 分類錯誤訊息成 ErrorCategory。
    /// 紀律：先比 BusinessRule（明確 quota / 守門 pattern）→ Transient（infra fail signal）→ Permanent（unknown）。
    /// </summary>
    public static PetraErrorCategory Classify(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return PetraErrorCategory.Permanent;

        var msg = errorMessage;

        // mode 4：business rule rejection — Token 守門 / quota / rate limit / 月限 / 日限 fire
        if (msg.Contains("守門", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("月限", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("日限", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("quota", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("已暫停", StringComparison.OrdinalIgnoreCase))
            return PetraErrorCategory.BusinessRule;

        // mode 1+2+3：transient infrastructure / LLM hallucination / tool call fail signal
        if (msg.Contains("timeout", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("HttpException", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("HttpRequestException", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("TaskCanceledException", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("SocketException", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("JsonException", StringComparison.OrdinalIgnoreCase)
         || msg.Contains(" 429", StringComparison.OrdinalIgnoreCase)
         || msg.Contains(" 500", StringComparison.OrdinalIgnoreCase)
         || msg.Contains(" 502", StringComparison.OrdinalIgnoreCase)
         || msg.Contains(" 503", StringComparison.OrdinalIgnoreCase)
         || msg.Contains(" 504", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("transient", StringComparison.OrdinalIgnoreCase))
            return PetraErrorCategory.Transient;

        // unknown → fail-fast 等人工介入
        return PetraErrorCategory.Permanent;
    }
}

/// <summary>Stage 76：Petra 執行錯誤分類（對齊 multi-agent failure mode taxonomy）。</summary>
public enum PetraErrorCategory
{
    /// <summary>暫時性錯誤（429/5xx/timeout/JSON 解析錯誤）— auto retry + exponential backoff。</summary>
    Transient,

    /// <summary>業務規則拒絕（Token 守門 / quota / rate limit / 月限 / 日限）— fail-fast 不 retry。</summary>
    BusinessRule,

    /// <summary>永久錯誤（未知 exception / 邏輯死路）— fail-fast 等人工介入。</summary>
    Permanent,
}
