using AiTeam.Bot.Services;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Stage 17：MockMode 模擬 LLM 直接呼叫，回傳預設 JSON 結果，不消耗 API。
/// 有意跳過 TokenTrackingProvider 包裝，避免產生假的 Token 統計資料污染 Dashboard 監控頁。
/// 依 systemPrompt 偵測呼叫情境，回傳對應格式的 JSON。
///
/// Stage 32：延遲範圍改由 AppSettings（Mock:DelayMinMs / Mock:DelayMaxMs）動態讀取。
/// </summary>
public class MockLlmProvider(AppSettingsService appSettings) : ILlmProvider
{
    public async Task<LlmResponse> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        await Task.Delay(await appSettings.GetMockDelayMsAsync(cancellationToken), cancellationToken);

        var content = BuildMockResponse(systemPrompt);
        return new LlmResponse(content, InputTokens: 0, OutputTokens: 0);
    }

    /// <summary>
    /// 依 systemPrompt 內容偵測呼叫方並回傳對應格式的 mock JSON。
    /// </summary>
    private static string BuildMockResponse(string systemPrompt)
    {
        // Petra 審核：需要 {"decision","summary","issues"} 格式
        if (systemPrompt.Contains("decision", StringComparison.OrdinalIgnoreCase) ||
            systemPrompt.Contains("approve",  StringComparison.OrdinalIgnoreCase) ||
            systemPrompt.Contains("revise",   StringComparison.OrdinalIgnoreCase) ||
            systemPrompt.Contains("Petra",    StringComparison.OrdinalIgnoreCase) ||
            systemPrompt.Contains("審核",     StringComparison.Ordinal))
        {
            return "{\"decision\":\"approve\",\"summary\":\"[MOCK] 模擬審核通過，無 blocking 問題\",\"issues\":[]}";
        }

        // CEO Victoria 分類：需要 {"action","reply"} 格式
        if (systemPrompt.Contains("Victoria", StringComparison.OrdinalIgnoreCase) ||
            systemPrompt.Contains("CEO",      StringComparison.OrdinalIgnoreCase) ||
            systemPrompt.Contains("delegate", StringComparison.OrdinalIgnoreCase) ||
            systemPrompt.Contains("分類",     StringComparison.Ordinal))
        {
            return "{\"action\":\"reply\",\"reply\":\"[MOCK] Victoria 模擬回應，此為 Mock Mode 測試。\",\"require_confirmation\":false}";
        }

        // Dev plan 產出：需要 DevPlan JSON（含 branch_name、summary 等欄位）
        if (systemPrompt.Contains("Dev",      StringComparison.OrdinalIgnoreCase) ||
            systemPrompt.Contains("branch",   StringComparison.OrdinalIgnoreCase) ||
            systemPrompt.Contains("commit",   StringComparison.OrdinalIgnoreCase) ||
            systemPrompt.Contains("計畫",     StringComparison.Ordinal))
        {
            return "{\"summary\":\"[MOCK] 模擬實作計畫\",\"branch_name\":\"feature/mock-999\",\"commit_message\":\"feat: [MOCK] 模擬功能實作\",\"task_type\":\"feature\"}";
        }

        // 預設：回傳含 [MOCK] 標記的一般文字
        return "[MOCK] 模擬 LLM 回應，此為 Mock Mode 測試，不消耗 API 費用。";
    }
}
