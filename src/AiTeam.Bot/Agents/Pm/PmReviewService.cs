using System.Text;

namespace AiTeam.Bot.Agents.Pm;

/// <summary>
/// Stage 35：Petra 兩大審核閘門（Stage 16/23/24 累積，Stage 25b 後 Rosa/Demi 改走會議，對應 Review 方法於 Stage 35 清理）。
/// - ReviewDevPlan：Claude Code RunReadOnlyAsync（帶 codebase context），失敗 fallback 到 LLM 直呼叫
/// - ReviewVera：LLM 直呼叫（只看 review 報告，不需 codebase）
/// </summary>
public class PmReviewService(
    LlmProviderFactory providerFactory,
    IClaudeCodeService claudeCodeService,
    IConfiguration configuration,
    ILogger<PmReviewService> logger)
{
    private const string AgentName = "PM";

    // ────────────── 公開審核方法 ──────────────

    /// <summary>
    /// 審核 Cody 產出的實作計畫書（Claude Code + LLM fallback）。
    /// </summary>
    public async Task<PetraReview> ReviewDevPlanAsync(
        string taskTitle,
        string devPlan,
        string? issueUrlsJson,
        string? uiSpecContent,
        string repoLocalPath,
        CancellationToken ct = default)
    {
        // 強制失敗情境：模擬 Petra 拒絕 Dev_plan，觸發 Dev_plan Appeal 流程
        if (MockClaudeCodeService.FailScenario == "dev_plan_appeal")
        {
            MockClaudeCodeService.FailScenario = "dev_plan_cody_appeal";
            logger.LogInformation("[MockMode/FailDevPlan] Petra 模擬拒絕 Dev_plan，觸發 Appeal 流程");
            return new PetraReview("revise", "[MOCK-FAIL] Dev_plan 不夠詳細，缺少錯誤處理章節與回滾計劃。",
                [new PetraIssue("blocking", "[MOCK-FAIL] 缺少錯誤處理與回滾計劃")],
                "[MOCK-FAIL] 請補充：1. 錯誤處理策略 2. 回滾計劃 3. 影響範圍評估");
        }

        var prompt = BuildDevPlanReviewPrompt(taskTitle, devPlan, issueUrlsJson, uiSpecContent);
        var review = await TryRunClaudeCodeAsync(repoLocalPath, prompt, ct);
        if (review is not null)
        {
            logger.LogInformation("Petra Claude Code 審核 DevPlan 完成：{Decision}", review.Decision);
            return review;
        }
        return await RunLlmDirectAsync(prompt, ct);
    }

    /// <summary>
    /// 審核 Vera 產出的 code review 結果（直接 LLM，不需 codebase）。
    /// </summary>
    public async Task<PetraReview> ReviewVeraAsync(
        string taskTitle,
        string reviewBody,
        CancellationToken ct = default)
    {
        var prompt = BuildVeraReviewPrompt(taskTitle, reviewBody);
        return await RunLlmDirectAsync(prompt, ct);
    }

    // ────────────── Claude Code 執行（主路徑）──────────────

    /// <summary>
    /// 以 Claude Code RunReadOnlyAsync 執行 Petra 審核。
    /// 執行前將 CLAUDE.md 替換為 CLAUDE_Petra.md，執行後還原。
    /// 成功解析 JSON 回傳 PetraReview；失敗回傳 null（由呼叫方 fallback 到 LLM）。
    /// </summary>
    private async Task<PetraReview?> TryRunClaudeCodeAsync(
        string repoLocalPath, string prompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(repoLocalPath) || !Directory.Exists(repoLocalPath))
            return null;

        var claudeMdPath     = Path.Combine(repoLocalPath, "CLAUDE.md");
        var templatePath     = Path.Combine(AppContext.BaseDirectory, "Resources", "CLAUDE_Petra.md");
        var originalClaudeMd = File.Exists(claudeMdPath)
            ? await File.ReadAllTextAsync(claudeMdPath, ct)
            : null;

        try
        {
            // 覆蓋 CLAUDE.md 為 Petra 專用（含「只輸出 JSON」指示）
            if (File.Exists(templatePath))
                await File.WriteAllTextAsync(claudeMdPath,
                    await File.ReadAllTextAsync(templatePath, ct), ct);

            var model  = configuration["Agents:PM:Model"]
                      ?? configuration["Anthropic:DefaultModel"]
                      ?? "claude-haiku-4-5";
            var apiKey = configuration["Anthropic:ApiKey"] ?? "";

            var result = await claudeCodeService.RunReadOnlyAsync(repoLocalPath, prompt, model, apiKey, ct);

            if (!result.Success)
            {
                logger.LogWarning("Petra Claude Code 執行未成功（exitCode={Code}）", result.ExitCode);
                return null;
            }

            // 先從 Output（result 欄位摘要）解析
            var review = PmAgentCommons.TryParseReview(result.Output);
            if (review is not null) return review;

            // fallback：從 RawJson 全文搜尋第一個完整 JSON 物件
            review = PmAgentCommons.TryParseReview(result.RawJson);
            if (review is not null)
            {
                logger.LogInformation("Petra 從 RawJson 解析成功");
                return review;
            }

            logger.LogWarning("Petra Claude Code 輸出無法解析為 JSON：{Output}", result.Output);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Petra Claude Code 執行失敗，將 fallback 到 LLM");
            return null;
        }
        finally
        {
            // 還原 CLAUDE.md
            if (originalClaudeMd is not null)
                await File.WriteAllTextAsync(claudeMdPath, originalClaudeMd, CancellationToken.None);
            else if (File.Exists(claudeMdPath))
                File.Delete(claudeMdPath);
        }
    }

    // ────────────── LLM 直呼叫（fallback）──────────────

    private async Task<PetraReview> RunLlmDirectAsync(string prompt, CancellationToken ct)
    {
        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = PmAgentCommons.BuildPetraSystemPrompt();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var response = await provider.CompleteAsync(systemPrompt, prompt, ct);
                var review   = PmAgentCommons.TryParseReview(response.Content);
                if (review is not null)
                {
                    logger.LogInformation("Petra LLM fallback 審核完成（第 {Attempt} 次）：{Decision}", attempt, review.Decision);
                    return review;
                }
                logger.LogWarning("Petra LLM 回應 JSON 解析失敗（第 {Attempt} 次）：{Content}", attempt, response.Content);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Petra LLM 呼叫失敗（第 {Attempt} 次）", attempt);
            }
        }

        // 最終 fallback：回傳 approve 避免卡住流程
        logger.LogError("Petra 所有路徑均失敗，fallback 回傳 approve 避免卡住");
        return new PetraReview("approve", "審核失敗，自動放行", [], null);
    }

    // ────────────── Prompt 組建 ──────────────

    private static string BuildDevPlanReviewPrompt(
        string taskTitle,
        string devPlan,
        string? issueUrlsJson,
        string? uiSpecContent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 審核類型");
        sb.AppendLine("Cody Dev Agent 產出的實作計畫書審核");
        sb.AppendLine();
        sb.AppendLine("## 任務標題");
        sb.AppendLine(taskTitle);
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(issueUrlsJson))
        {
            sb.AppendLine("## GitHub Issues");
            sb.AppendLine(issueUrlsJson);
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(uiSpecContent))
        {
            sb.AppendLine("## Demi UI 規格");
            sb.AppendLine(uiSpecContent);
            sb.AppendLine();
        }
        sb.AppendLine("## Cody 實作計畫書");
        sb.AppendLine(devPlan);
        sb.AppendLine();
        sb.AppendLine("## 你的任務");
        sb.AppendLine("審核實作計畫是否涵蓋所有 Issue 功能點、架構方向是否合理。");
        sb.AppendLine();
        sb.AppendLine("**只審計畫方向，不審實作細節：**");
        sb.AppendLine("- 不要要求 Entity 欄位定義、API 簽名、元件 props 等程式碼層級的細節");
        sb.AppendLine("- 不要要求效能優化方案或大規模場景處理");
        sb.AppendLine("- 不要用 Glob / Grep / Read 驗證檔案是否存在（這是新功能，檔案尚未建立）");
        sb.AppendLine("- approve 標準：所有 Issue 功能點都有對應的修改計畫，且架構方向沒有明顯錯誤");
        sb.AppendLine("輸出 JSON 審核結果。");
        return sb.ToString();
    }

    private static string BuildVeraReviewPrompt(string taskTitle, string reviewBody)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 審核類型");
        sb.AppendLine("Vera Reviewer Agent 產出的 Code Review 結果嚴重度判斷");
        sb.AppendLine();
        sb.AppendLine("## 任務標題");
        sb.AppendLine(taskTitle);
        sb.AppendLine();
        sb.AppendLine("## Vera 審查報告");
        sb.AppendLine(reviewBody);
        sb.AppendLine();
        sb.AppendLine("## 你的任務");
        sb.AppendLine("判斷 Vera 發現的問題嚴重度。");
        sb.AppendLine();
        sb.AppendLine("**blocking（revise）：邏輯錯誤、安全漏洞、build 會失敗的問題**");
        sb.AppendLine("**minor（approve）：命名風格、comment、效能建議、重構建議、測試覆蓋率**");
        sb.AppendLine();
        sb.AppendLine("只有存在 blocking 問題時才 revise，其餘一律 approve。");
        sb.AppendLine("輸出 JSON 審核結果。");
        return sb.ToString();
    }
}
