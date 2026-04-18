namespace AiTeam.Bot.Agents;

/// <summary>
/// Stage 17：MockMode 模擬 Claude Code subprocess 呼叫，回傳預設結果，不消耗 API。
/// 各方法的 mock output 格式設計為能通過對應 Agent 的 JSON parser，
/// 無法解析時由 Agent fallback 到 MockLlmProvider。
/// </summary>
public class MockClaudeCodeService(ILogger<MockClaudeCodeService> logger) : IClaudeCodeService
{
    /// <summary>
    /// 強制失敗情境（供 /mock fail_* 指令使用）。
    /// 各 Agent 的 MockMode 區塊會依據此值決定是否回傳失敗結果。
    /// 每次使用後由 Agent 自行推進到下一個值（或清為 null）。
    ///
    /// 狀態機：
    ///  fail_review  → ReviewerAgent 設為 review_cody_appeal，回傳 Critical
    ///  review_cody_appeal → PmAgent.RunCodyAppealAsync 設為 review_vera_appeal，Cody disagree
    ///  review_vera_appeal → PmAgent.RunVeraAppealAsync 設為 null，Vera maintain critical
    ///
    ///  qa_failure → QaAgent 設為 null，回傳 failed 報告
    ///
    ///  dev_plan_appeal → PmAgent.ReviewDevPlanAsync 設為 dev_plan_cody_appeal，回傳 revise
    ///  dev_plan_cody_appeal → PmAgent.RunCodyDevPlanAppealAsync 設為 null，Cody disagree
    /// </summary>
    public static string? FailScenario { get; set; }

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
        string workingDir, string prompt, string model, string anthropicApiKey,
        IReadOnlyList<ImageAttachment>? images = null, CancellationToken ct = default)
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

    /// <summary>
    /// Stage 25a：模擬持續對話 session（RunMeetingSessionAsync）。
    /// 依 sessionId 後綴判斷 Agent 角色，回傳對應的 mock 意見。
    /// Petra 的回應結尾含合法 JSON，確保 MockMode 下自動達成 consensus，不卡流程。
    /// </summary>
    public async Task<ClaudeCodeResult> RunMeetingSessionAsync(
        string workingDir, string sessionId, string prompt, string model, string anthropicApiKey,
        bool isFirstMessage, int maxTurns, string[]? allowedTools = null, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[MockMode] MockClaudeCodeService.RunMeetingSessionAsync（sessionId={Id}，isFirst={IsFirst}）",
            sessionId, isFirstMessage);
        await Task.Delay(Random.Shared.Next(30000, 60000), ct);

        // Stage 26：改用 prompt 內容判斷角色（各 prompt builder 均以「你是 {Name}，」開頭）
        // 原本用 sessionId.Split('-').Last() 無法正確匹配純 UUID 格式的 session ID
        var agentName = prompt.Contains("你是 Petra") ? "petra"
                      : prompt.Contains("你是 Rosa")  ? "rosa"
                      : prompt.Contains("你是 Demi")  ? "demi"
                      : prompt.Contains("你是 Cody")  ? "cody"
                      : prompt.Contains("你是 Quinn") ? "quinn"
                      : "unknown";

        var output = agentName switch
        {
            "petra" =>
                "[MOCK] Petra 整理完成，所有 Agent 意見已彙整，沒有重大分歧。\n" +
                "{\"decision\":\"consensus\",\"summary\":\"[MOCK] 會議順利完成，各角色無重大疑慮。\",\"discussion_points\":[]}",
            "rosa" =>
                "[MOCK] Rosa 需求分析完成。需求描述清晰，無模糊之處。建議在實作前確認 API 設計細節。",
            "demi" =>
                "[MOCK] Demi UI/UX 評估完成。現有 Dashboard 結構可容納此功能，無需大規模 Layout 調整。",
            "cody" =>
                "[MOCK] Cody 技術可行性評估完成。技術上可行，現有架構支援此功能，預計 2 天完成。",
            "quinn" =>
                "[MOCK] Quinn 測試規劃完成。此功能可自動化測試，建議加入 E2E 截圖驗證。",
            _ =>
                "[MOCK] 會議發言完成。"
        };

        return new ClaudeCodeResult(true, output, 0, "");
    }
}
