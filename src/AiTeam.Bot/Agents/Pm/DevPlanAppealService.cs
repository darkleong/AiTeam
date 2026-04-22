using System.Text;
using System.Text.Json;
using AiTeam.Data;

namespace AiTeam.Bot.Agents.Pm;

/// <summary>
/// Stage 30：Dev_plan Appeal 雙角色對話（Cody 反駁 → Petra 重評）。
/// 兩個方法都走 Claude Code CLI RunMeetingSessionAsync（新 session，唯讀工具）。
/// </summary>
public class DevPlanAppealService(
    IClaudeCodeService claudeCodeService,
    PmAgentCommons commons,
    ILogger<DevPlanAppealService> logger)
{
    /// <summary>
    /// Stage 30：Cody 針對 Petra 的 Dev_plan revise 決定發起反駁（Claude Code CLI，唯讀工具）。
    /// </summary>
    public async Task<CodyDevPlanAppeal> RunCodyDevPlanAppealAsync(
        TaskGroup group,
        PetraReview petraReview,
        string? priorContext,
        CancellationToken ct = default)
    {
        // 強制失敗情境：Cody 對 Petra 拒絕 Dev_plan 提出反駁
        if (MockClaudeCodeService.FailScenario == "dev_plan_cody_appeal")
        {
            MockClaudeCodeService.FailScenario = null;
            logger.LogInformation("[MockMode/FailDevPlan] Cody 模擬 disagree Petra 的 Dev_plan 拒絕決定");
            return new CodyDevPlanAppeal("disagree",
                "[MOCK-FAIL] 計劃書中已有錯誤處理章節（見第 3.2 節），回滾計劃透過 DB Transaction 保證，不需額外補充。");
        }

        var context        = await commons.BuildAppealContextSectionAsync(group);
        var userPrompt     = BuildCodyDevPlanAppealPrompt(group, petraReview, priorContext);
        var combinedPrompt = $"[APPEAL:dev_plan_cody]\n{BuildCodyDevPlanAppealSystemPrompt()}\n\n---\n\n{context}\n\n{userPrompt}";
        var sessionId      = Guid.NewGuid().ToString();

        var (workingDir, model, apiKey) = commons.PrepareClaudeCodeEnv(group, "Dev");
        try
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    var result = await claudeCodeService.RunMeetingSessionAsync(
                        workingDir, sessionId, combinedPrompt, model, apiKey,
                        isFirstMessage: true, maxTurns: 10,
                        allowedTools: ["Glob", "Grep", "Read"], ct);
                    var appeal = TryParseCodyDevPlanAppeal(result.Output);
                    if (appeal is not null)
                    {
                        logger.LogInformation("Cody Dev_plan Appeal 完成（第 {Attempt} 次）：{Position}", attempt, appeal.Position);
                        return appeal;
                    }
                    logger.LogWarning("Cody Dev_plan Appeal 解析失敗（第 {Attempt} 次）", attempt);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "RunCodyDevPlanAppealAsync CLI 呼叫失敗（第 {Attempt} 次）", attempt);
                }
            }
        }
        finally
        {
            commons.CleanupAppealRepo(workingDir, "RunCodyDevPlanAppealAsync");
        }

        logger.LogError("Cody Dev_plan Appeal 所有路徑均失敗，fallback accept（不阻礙流程）");
        return new CodyDevPlanAppeal("accept", "");
    }

    /// <summary>
    /// Stage 30：Petra 基於 Cody 的反駁重新評估 Dev_plan（Claude Code CLI，唯讀工具）。
    /// </summary>
    public async Task<PetraReview> ReassessDevPlanAsync(
        TaskGroup group,
        CodyDevPlanAppeal codyAppeal,
        PetraReview previousReview,
        CancellationToken ct = default)
    {
        var context        = await commons.BuildAppealContextSectionAsync(group);
        var userPrompt     = BuildReassessDevPlanPrompt(group, codyAppeal, previousReview);
        var combinedPrompt = $"[APPEAL:dev_plan_petra]\n{PmAgentCommons.BuildPetraSystemPrompt()}\n\n---\n\n{context}\n\n{userPrompt}";
        var sessionId      = Guid.NewGuid().ToString();

        var (workingDir, model, apiKey) = commons.PrepareClaudeCodeEnv(group, "PM");
        try
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    var result = await claudeCodeService.RunMeetingSessionAsync(
                        workingDir, sessionId, combinedPrompt, model, apiKey,
                        isFirstMessage: true, maxTurns: 10,
                        allowedTools: ["Glob", "Grep", "Read"], ct);
                    var review = PmAgentCommons.TryParseReview(result.Output);
                    if (review is not null)
                    {
                        logger.LogInformation("Petra Dev_plan 重評完成（第 {Attempt} 次）：{Decision}", attempt, review.Decision);
                        return review;
                    }
                    logger.LogWarning("Petra Dev_plan 重評解析失敗（第 {Attempt} 次）", attempt);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "ReassessDevPlanAsync CLI 呼叫失敗（第 {Attempt} 次）", attempt);
                }
            }
        }
        finally
        {
            commons.CleanupAppealRepo(workingDir, "ReassessDevPlanAsync");
        }

        logger.LogError("Petra Dev_plan 重評所有路徑均失敗，fallback approve（不卡流程）");
        return new PetraReview("approve", "重評失敗，自動放行", [], null);
    }

    // ────────────── Prompt 組建 ──────────────

    private static string BuildCodyDevPlanAppealSystemPrompt() => """
        你是 Cody，資深後端工程師。Petra 對你的實作計畫書提出了修改意見。
        評估 Petra 的意見是否有技術依據，若有合理理由可以反駁（disagree），否則接受（accept）。
        只輸出 JSON，不加任何說明文字、不加 markdown code block。
        使用繁體中文。
        """;

    private static string BuildCodyDevPlanAppealPrompt(TaskGroup group, PetraReview petraReview, string? priorContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 你的實作計畫書");
        sb.AppendLine(string.IsNullOrWhiteSpace(group.DevPlan) ? "（無計畫書）" :
            group.DevPlan.Length > 2000 ? group.DevPlan[..2000] + "\n...（截斷）" : group.DevPlan);
        sb.AppendLine();
        sb.AppendLine("## Petra 的審核意見");
        sb.AppendLine($"決定：{petraReview.Decision}");
        sb.AppendLine($"摘要：{petraReview.Summary}");
        if (petraReview.Issues.Count > 0)
        {
            sb.AppendLine("問題清單：");
            foreach (var issue in petraReview.Issues)
                sb.AppendLine($"- [{issue.Severity}] {issue.Description}");
        }
        if (!string.IsNullOrWhiteSpace(petraReview.RevisionInstructions))
        {
            sb.AppendLine();
            sb.AppendLine($"修正指示：{petraReview.RevisionInstructions}");
        }
        if (!string.IsNullOrWhiteSpace(priorContext))
        {
            sb.AppendLine();
            sb.AppendLine("## 前輪對話摘要");
            sb.AppendLine(priorContext);
        }
        sb.AppendLine();
        sb.AppendLine("## 你的任務");
        sb.AppendLine("評估 Petra 的意見：");
        sb.AppendLine("- 若有具體技術依據可以反駁，輸出 disagree 並說明理由");
        sb.AppendLine("- 若意見合理或無法反駁，輸出 accept");
        sb.AppendLine();
        sb.AppendLine("只輸出 JSON：{\"position\": \"disagree|accept\", \"reasoning\": \"具體技術論點或接受原因\"}");
        return sb.ToString();
    }

    private static string BuildReassessDevPlanPrompt(TaskGroup group, CodyDevPlanAppeal codyAppeal, PetraReview previousReview)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 實作計畫書");
        sb.AppendLine(string.IsNullOrWhiteSpace(group.DevPlan) ? "（無計畫書）" :
            group.DevPlan.Length > 2000 ? group.DevPlan[..2000] + "\n...（截斷）" : group.DevPlan);
        sb.AppendLine();
        sb.AppendLine("## 你的初次審核意見");
        sb.AppendLine($"摘要：{previousReview.Summary}");
        sb.AppendLine();
        sb.AppendLine("## Cody 的反駁");
        sb.AppendLine($"立場：{codyAppeal.Position}");
        sb.AppendLine($"理由：{codyAppeal.Reasoning}");
        sb.AppendLine();
        sb.AppendLine("## 你的任務");
        sb.AppendLine("基於 Cody 的反駁，重新評估此計畫書：");
        sb.AppendLine("- 若反駁有技術依據，改為 approve");
        sb.AppendLine("- 若維持修改意見，輸出 revise 並更新 summary");
        sb.AppendLine();
        sb.AppendLine("只輸出 JSON：{\"decision\": \"approve|revise\", \"summary\": \"審核摘要\", \"issues\": [], \"revision_instructions\": null}");
        return sb.ToString();
    }

    private static CodyDevPlanAppeal? TryParseCodyDevPlanAppeal(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;
            var json  = content[start..(end + 1)];
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var position  = root.TryGetProperty("position",  out var p) ? p.GetString() : null;
            var reasoning = root.TryGetProperty("reasoning", out var r) ? r.GetString() : null;
            if (string.IsNullOrWhiteSpace(position)) return null;
            return new CodyDevPlanAppeal(position, reasoning ?? "");
        }
        catch { return null; }
    }
}
