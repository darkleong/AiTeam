namespace AiTeam.Bot.Agents;

/// <summary>
/// Stage 17：MockMode 模擬 Claude Code subprocess 呼叫，回傳預設結果，不消耗 API。
/// 各方法的 mock output 格式設計為能通過對應 Agent 的 JSON parser，
/// 無法解析時由 Agent fallback 到 MockLlmProvider。
/// </summary>
public class MockClaudeCodeService(ILogger<MockClaudeCodeService> logger) : IClaudeCodeService
{
    /// <summary>
    /// 模擬 Dev Agent 完整開發（RunAsync）。
    /// Output 包含 /pull/999，讓 DevAgentService.ExtractPrNumberFromText 可解析 PR 編號。
    /// </summary>
    public async Task<ClaudeCodeResult> RunAsync(
        string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default)
    {
        logger.LogInformation("[MockMode] MockClaudeCodeService.RunAsync 回傳模擬結果");
        await Task.Delay(Random.Shared.Next(30000, 60000), ct);
        const string output = "[MOCK] 開發完成，程式碼已實作並通過 build\nhttps://github.com/mock/repo/pull/999";
        return new ClaudeCodeResult(true, output, 0, "");
    }

    /// <summary>
    /// 模擬唯讀探索（RunReadOnlyAsync）。
    /// Output 為 JSON 陣列格式，供 Rosa 的 TryParseIssues 解析。
    /// Petra 的 TryParseReview 因找不到 "decision" 欄位會回傳 null，fallback 到 MockLlmProvider。
    /// Demi / Dev 直接取 Output 字串使用，不影響功能。
    /// </summary>
    public async Task<ClaudeCodeResult> RunReadOnlyAsync(
        string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default)
    {
        logger.LogInformation("[MockMode] MockClaudeCodeService.RunReadOnlyAsync 回傳模擬結果");
        await Task.Delay(Random.Shared.Next(30000, 60000), ct);
        const string output =
            "[MOCK] 探索完成\n" +
            "[{\"title\":\"[MOCK] 模擬需求功能\",\"body\":\"這是 Mock Mode 產生的模擬需求，用於測試流程。\",\"labels\":[\"enhancement\"]}]";
        return new ClaudeCodeResult(true, output, 0, "");
    }

    /// <summary>
    /// 模擬 Victoria CEO 模式（RunVictoriaAsync）。
    /// Output 包含 &lt;ACTION&gt; 區塊，供 CeoAgentService.TryParseActionBlock 解析為 CeoResponse。
    /// </summary>
    public async Task<ClaudeCodeResult> RunVictoriaAsync(
        string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default)
    {
        logger.LogInformation("[MockMode] MockClaudeCodeService.RunVictoriaAsync 回傳模擬結果");
        await Task.Delay(Random.Shared.Next(30000, 60000), ct);
        const string output =
            "[MOCK] Victoria 分析完成\n" +
            "<ACTION>{\"action\":\"reply\",\"reply\":\"[MOCK] Victoria 已完成分析，這是模擬模式回應。\",\"require_confirmation\":false,\"docs_committed\":false}</ACTION>";
        return new ClaudeCodeResult(true, output, 0, "");
    }

    /// <summary>
    /// 模擬 QA 測試產生（RunQaAsync）。
    /// Output 為 QaReport JSON，供 QaAgentService.TryParseQaReport 解析。
    /// </summary>
    public async Task<ClaudeCodeResult> RunQaAsync(
        string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default)
    {
        logger.LogInformation("[MockMode] MockClaudeCodeService.RunQaAsync 回傳模擬結果");
        await Task.Delay(Random.Shared.Next(30000, 60000), ct);
        const string output =
            "[MOCK] QA 完成\n" +
            "{\"generated\":[\"[MOCK] MockFeatureTest.cs\"],\"summary\":\"[MOCK] QA 測試通過，0 個失敗\"}";
        return new ClaudeCodeResult(true, output, 0, "");
    }

    /// <summary>
    /// 模擬 Code Review（RunReviewAsync）。
    /// Output 為 ReviewReport JSON，供 ReviewerAgentService.TryParseReviewReport 解析。
    /// </summary>
    public async Task<ClaudeCodeResult> RunReviewAsync(
        string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default)
    {
        logger.LogInformation("[MockMode] MockClaudeCodeService.RunReviewAsync 回傳模擬結果");
        await Task.Delay(Random.Shared.Next(30000, 60000), ct);
        const string output =
            "[MOCK] 審查完成\n" +
            "{\"critical\":[],\"warning\":[],\"info\":[],\"summary\":\"[MOCK] 模擬審查通過，程式碼品質符合要求\",\"impact\":\"[MOCK] 無影響範圍\"}";
        return new ClaudeCodeResult(true, output, 0, "");
    }
}
