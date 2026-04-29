using System.Text;
using System.Text.Json;
using AiTeam.Data;

namespace AiTeam.Bot.Agents.Pm;

/// <summary>
/// Stage 30：Review Appeal 三角互動（Cody 反駁 → Vera 重評 → Petra 仲裁）。
/// 三個方法都走 Claude Code CLI RunMeetingSessionAsync（新 session，唯讀工具）。
/// </summary>
public class ReviewAppealService(
    IClaudeCodeService claudeCodeService,
    PmAgentCommons commons,
    Services.TokenLogService tokenLogService,
    ILogger<ReviewAppealService> logger)
{
    /// <summary>
    /// Stage 30：Cody 針對 Vera Critical Issues 的逐條回應（Claude Code CLI，唯讀工具）。
    /// 第二輪起附上累計對話紀錄，讓 Cody 只針對剩餘 criticals 回應。
    /// </summary>
    public async Task<CodyAppeal> RunCodyAppealAsync(
        TaskGroup group,
        string reviewBody,
        string taskTitle,
        IReadOnlyList<int> remainingCriticalIds,
        string? priorContext,
        CancellationToken ct = default)
    {
        // 強制失敗情境：Cody 對 Vera 的 Critical 提出 disagree，推進到 Vera 重評
        if (MockClaudeCodeService.FailScenario == "review_cody_appeal")
        {
            MockClaudeCodeService.FailScenario = "review_vera_appeal";
            logger.LogInformation("[MockMode/FailReview] Cody 模擬 disagree Vera 的 Critical Issue");
            var disagreeItems = remainingCriticalIds
                .Select(id => new CodyAppealItem(id, "disagree", "[MOCK-FAIL] 這個 Critical 判斷有誤，現有架構已有 global error handler 處理。"))
                .ToList();
            return new CodyAppeal(disagreeItems);
        }

        var context        = await commons.BuildAppealContextSectionAsync(group);
        var userPrompt     = BuildCodyAppealPrompt(reviewBody, taskTitle, remainingCriticalIds, priorContext);
        var combinedPrompt = $"[APPEAL:review_cody]\n{BuildCodyAppealSystemPrompt()}\n\n---\n\n{context}\n\n{userPrompt}";
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
                    // Stage 44：寫 token_logs（AgentName=Cody / Stage=ReviewAppeal_cody / Round=ReviewAppealRoundA）
                    await tokenLogService.LogCliUsageAsync(
                        "Cody", model, "ReviewAppeal_cody", group.ReviewAppealRoundA, taskId: null, result.Usage, ct);
                    var appeal = TryParseCodyAppeal(result.Output);
                    if (appeal is not null)
                    {
                        logger.LogInformation("Cody Appeal 解析成功（{Count} 項回應）", appeal.Items.Count);
                        return appeal;
                    }
                    logger.LogWarning("Cody Appeal 回應解析失敗（第 {Attempt} 次）", attempt);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "RunCodyAppealAsync CLI 呼叫失敗（第 {Attempt} 次）", attempt);
                }
            }
        }
        finally
        {
            commons.CleanupAppealRepo(workingDir, "RunCodyAppealAsync");
        }

        logger.LogError("RunCodyAppealAsync 失敗，fallback agree all");
        var fallbackItems = remainingCriticalIds
            .Select(id => new CodyAppealItem(id, "agree", "評估失敗，同意修正"))
            .ToList();
        return new CodyAppeal(fallbackItems);
    }

    /// <summary>
    /// Stage 30：Vera 基於程式碼事實重新評估 Cody 反駁（Claude Code CLI，唯讀工具）。
    /// </summary>
    public async Task<VeraAppealResponse> RunVeraAppealAsync(
        TaskGroup group,
        string reviewBody,
        string codyAppealJson,
        CancellationToken ct = default)
    {
        // 強制失敗情境：Vera 維持 Critical，迫使進入 Petra 仲裁（輪數達上限）
        if (MockClaudeCodeService.FailScenario == "review_vera_appeal")
        {
            MockClaudeCodeService.FailScenario = null;
            logger.LogInformation("[MockMode/FailReview] Vera 模擬維持所有 Critical，不接受 Cody 反駁");
            return new VeraAppealResponse([], [1], "[MOCK-FAIL] Vera 審查架構後確認：此問題不在 global handler 涵蓋範圍內，屬必修項目。");
        }

        var context        = await commons.BuildAppealContextSectionAsync(group);
        var userPrompt     = BuildVeraAppealPrompt(reviewBody, codyAppealJson);
        var combinedPrompt = $"[APPEAL:review_vera]\n{BuildVeraAppealSystemPrompt()}\n\n---\n\n{context}\n\n{userPrompt}";
        var sessionId      = Guid.NewGuid().ToString();

        var (workingDir, model, apiKey) = commons.PrepareClaudeCodeEnv(group, "Reviewer");
        try
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    var result       = await claudeCodeService.RunMeetingSessionAsync(
                        workingDir, sessionId, combinedPrompt, model, apiKey,
                        isFirstMessage: true, maxTurns: 10,
                        allowedTools: ["Glob", "Grep", "Read"], ct);
                    // Stage 44：寫 token_logs（AgentName=Vera / Stage=ReviewAppeal_vera / Round=ReviewAppealRoundA）
                    await tokenLogService.LogCliUsageAsync(
                        "Vera", model, "ReviewAppeal_vera", group.ReviewAppealRoundA, taskId: null, result.Usage, ct);
                    var veraResponse = TryParseVeraAppealResponse(result.Output);
                    if (veraResponse is not null)
                    {
                        logger.LogInformation(
                            "Vera Appeal 重評完成（接受 {AcceptCount} 項，維持 {MaintainCount} 項）",
                            veraResponse.AcceptedIds.Count, veraResponse.MaintainedIds.Count);
                        return veraResponse;
                    }
                    logger.LogWarning("Vera Appeal 回應解析失敗（第 {Attempt} 次）", attempt);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "RunVeraAppealAsync CLI 呼叫失敗（第 {Attempt} 次）", attempt);
                }
            }
        }
        finally
        {
            commons.CleanupAppealRepo(workingDir, "RunVeraAppealAsync");
        }

        logger.LogError("RunVeraAppealAsync 失敗，fallback 維持所有 critical");
        return new VeraAppealResponse([], [], "重評失敗，維持所有 critical");
    }

    /// <summary>
    /// Stage 30：Petra 仲裁 Cody-Vera 爭議（Claude Code CLI，唯讀工具）。
    /// </summary>
    public async Task<AppealArbitration> ArbitrateReviewAppealAsync(
        TaskGroup group,
        string reviewBody,
        string appealLog,
        CancellationToken ct = default)
    {
        var context        = await commons.BuildAppealContextSectionAsync(group);
        var userPrompt     = BuildArbitrationPrompt(reviewBody, appealLog);
        var combinedPrompt = $"[APPEAL:review_arbitration]\n{BuildArbitrationSystemPrompt()}\n\n---\n\n{context}\n\n{userPrompt}";
        var sessionId      = Guid.NewGuid().ToString();

        var (workingDir, model, apiKey) = commons.PrepareClaudeCodeEnv(group, "PM");
        try
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    var result      = await claudeCodeService.RunMeetingSessionAsync(
                        workingDir, sessionId, combinedPrompt, model, apiKey,
                        isFirstMessage: true, maxTurns: 10,
                        allowedTools: ["Glob", "Grep", "Read"], ct);
                    // Stage 44：寫 token_logs（AgentName=Petra / Stage=ReviewAppeal_arbitration / Round=ReviewAppealRoundA）
                    await tokenLogService.LogCliUsageAsync(
                        "Petra", model, "ReviewAppeal_arbitration", group.ReviewAppealRoundA, taskId: null, result.Usage, ct);
                    var arbitration = TryParseArbitration(result.Output);
                    if (arbitration is not null)
                    {
                        logger.LogInformation(
                            "Petra 仲裁完成：{Decision}，最終 {Count} 個 Critical",
                            arbitration.Decision, arbitration.FinalCriticals.Count);
                        return arbitration;
                    }
                    logger.LogWarning("Petra 仲裁回應解析失敗（第 {Attempt} 次）", attempt);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "ArbitrateReviewAppealAsync CLI 呼叫失敗（第 {Attempt} 次）", attempt);
                }
            }
        }
        finally
        {
            commons.CleanupAppealRepo(workingDir, "ArbitrateReviewAppealAsync");
        }

        logger.LogError("ArbitrateReviewAppealAsync 失敗，fallback support_vera");
        return new AppealArbitration("support_vera", [], "仲裁失敗，維持 Vera 判斷");
    }

    // ────────────── Appeal Prompt 組建 ──────────────

    private static string BuildCodyAppealPrompt(
        string reviewBody,
        string taskTitle,
        IReadOnlyList<int> remainingCriticalIds,
        string? priorContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("review_appeal_request: true");
        sb.AppendLine();
        sb.AppendLine("## 任務");
        sb.AppendLine(taskTitle);
        sb.AppendLine();
        sb.AppendLine("## Vera 審查報告");
        sb.AppendLine(reviewBody);
        sb.AppendLine();
        sb.AppendLine("## 需要你回應的 Critical Issue IDs（本輪）");
        sb.AppendLine(string.Join(", ", remainingCriticalIds));
        if (!string.IsNullOrWhiteSpace(priorContext))
        {
            sb.AppendLine();
            sb.AppendLine("## 前幾輪對話紀錄");
            sb.AppendLine(priorContext);
        }
        sb.AppendLine();
        sb.AppendLine("## 你的任務（Cody）");
        sb.AppendLine("針對上述 Critical Issue，逐條回應 agree 或 disagree，並附上具體程式碼事實。");
        sb.AppendLine("disagree 必須有明確的檔案路徑 / 行號 / 邏輯說明，不能只說「我認為沒問題」。");
        sb.AppendLine("只輸出 JSON：{\"items\": [{\"id\": N, \"response\": \"agree|disagree\", \"reason\": \"...\"}]}");
        return sb.ToString();
    }

    private static string BuildVeraAppealPrompt(string reviewBody, string codyAppealJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Vera 原始審查報告");
        sb.AppendLine(reviewBody);
        sb.AppendLine();
        sb.AppendLine("## cody_appeal_json（Cody 的反駁）");
        sb.AppendLine(codyAppealJson);
        sb.AppendLine();
        sb.AppendLine("## 你的任務（Vera）");
        sb.AppendLine("針對 Cody 反駁的每個 disagree 項目重新評估：");
        sb.AppendLine("- 只接受基於程式碼事實的反駁（如：此欄位在 X 處已初始化，不可能為 null）");
        sb.AppendLine("- 不接受主觀判斷");
        sb.AppendLine("只輸出 JSON：{\"accepted_ids\": [1,3], \"maintained_ids\": [2], \"updated_summary\": \"...\"}");
        return sb.ToString();
    }

    private static string BuildArbitrationPrompt(string reviewBody, string appealLog)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Vera 原始審查報告");
        sb.AppendLine(reviewBody);
        sb.AppendLine();
        sb.AppendLine("## 完整 Appeal 對話紀錄");
        sb.AppendLine(appealLog);
        sb.AppendLine();
        sb.AppendLine("## 你的任務（Petra 仲裁）");
        sb.AppendLine("仲裁 Cody 與 Vera 的爭議，決定哪些 Critical Issues 最終成立。");
        sb.AppendLine("偏保守原則：事實不明確時支持 Vera。");
        sb.AppendLine("只輸出 JSON：{\"decision\": \"support_vera|support_cody_partial|support_cody_full\", \"final_criticals\": [1,2], \"reasoning\": \"...\"}");
        return sb.ToString();
    }

    private static string BuildCodyAppealSystemPrompt() => """
        你是 Petra，模擬 Cody 針對 Vera 審查報告的反駁回應。
        只輸出 JSON，不加任何說明文字、不加 markdown code block。
        使用繁體中文。
        """;

    private static string BuildVeraAppealSystemPrompt() => """
        你是 Petra，模擬 Vera 基於程式碼事實重新評估 Cody 的反駁。
        只輸出 JSON，不加任何說明文字、不加 markdown code block。
        使用繁體中文。
        """;

    private static string BuildArbitrationSystemPrompt() => """
        你是 Petra，專案經理，負責仲裁 Cody-Vera 的 Review Appeal 爭議。
        偏保守原則：事實不明確時支持 Vera。
        只輸出 JSON，不加任何說明文字、不加 markdown code block。
        使用繁體中文。
        """;

    // ────────────── Appeal JSON 解析 ──────────────

    private static CodyAppeal? TryParseCodyAppeal(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;
            var json = content[start..(end + 1)];
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var itemsEl)) return null;
            var items = new List<CodyAppealItem>();
            foreach (var el in itemsEl.EnumerateArray())
            {
                var id       = el.TryGetProperty("id",       out var i)  ? i.GetInt32()   : 0;
                var response = el.TryGetProperty("response", out var r)  ? r.GetString()  : null;
                var reason   = el.TryGetProperty("reason",   out var rs) ? rs.GetString() : null;
                if (response is null) continue;
                items.Add(new CodyAppealItem(id, response, reason ?? ""));
            }
            return new CodyAppeal(items);
        }
        catch { return null; }
    }

    private static VeraAppealResponse? TryParseVeraAppealResponse(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;
            var json = content[start..(end + 1)];
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var acceptedIds   = ParseIntArray(root, "accepted_ids");
            var maintainedIds = ParseIntArray(root, "maintained_ids");
            var summary = root.TryGetProperty("updated_summary", out var s) ? s.GetString() ?? "" : "";
            return new VeraAppealResponse(acceptedIds, maintainedIds, summary);
        }
        catch { return null; }
    }

    private static AppealArbitration? TryParseArbitration(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;
            var json = content[start..(end + 1)];
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var decision       = root.TryGetProperty("decision",        out var d) ? d.GetString() ?? "" : "";
            var finalCriticals = ParseIntArray(root, "final_criticals");
            var reasoning      = root.TryGetProperty("reasoning",       out var r) ? r.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(decision)) return null;
            return new AppealArbitration(decision, finalCriticals, reasoning);
        }
        catch { return null; }
    }

    private static IReadOnlyList<int> ParseIntArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var el)) return [];
        var list = new List<int>();
        foreach (var item in el.EnumerateArray())
            if (item.TryGetInt32(out var n)) list.Add(n);
        return list;
    }
}
