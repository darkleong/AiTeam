using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiTeam.Data;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Stage 16：PM Agent（Petra）品質審核閘門。
/// 負責在 Rosa / Demi / Dev_plan / Vera 完成後審核產出，
/// 回傳 approve / revise / escalate 決定。
///
/// 執行策略：
/// - ReviewRosa / ReviewDemi / ReviewDevPlan：Claude Code RunReadOnlyAsync（帶 codebase context）
///   若 Claude Code 失敗，fallback 到 LLM 直呼叫
/// - ReviewVera：LLM 直呼叫（只看 review 報告，不需 codebase）
/// </summary>
public class PmAgentService(
    LlmProviderFactory providerFactory,
    IClaudeCodeService claudeCodeService,
    IConfiguration configuration,
    ILogger<PmAgentService> logger)
{
    private const string AgentName = "PM";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ────────────── 公開審核方法 ──────────────

    /// <summary>
    /// 審核 Rosa 產出的 Issues 規格（Claude Code + LLM fallback）。
    /// </summary>
    public async Task<PetraReview> ReviewRosaAsync(
        TaskItem ceoTask,
        IReadOnlyList<RequirementIssuePreview> issues,
        string repoLocalPath,
        CancellationToken ct = default)
    {
        var prompt = BuildRosaReviewPrompt(ceoTask, issues);
        var review = await TryRunClaudeCodeAsync(repoLocalPath, prompt, ct);
        if (review is not null)
        {
            logger.LogInformation("Petra Claude Code 審核 Rosa 完成：{Decision}", review.Decision);
            return review;
        }
        return await RunLlmDirectAsync(prompt, ct);
    }

    /// <summary>
    /// 審核 Demi 產出的 UI 規格（Claude Code + LLM fallback）。
    /// </summary>
    public async Task<PetraReview> ReviewDemiAsync(
        TaskItem ceoTask,
        IReadOnlyList<RequirementIssuePreview> issues,
        string uiSpec,
        string repoLocalPath,
        CancellationToken ct = default)
    {
        var prompt = BuildDemiReviewPrompt(ceoTask, issues, uiSpec);
        var review = await TryRunClaudeCodeAsync(repoLocalPath, prompt, ct);
        if (review is not null)
        {
            logger.LogInformation("Petra Claude Code 審核 Demi 完成：{Decision}", review.Decision);
            return review;
        }
        return await RunLlmDirectAsync(prompt, ct);
    }

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
            var review = TryParseReview(result.Output);
            if (review is not null) return review;

            // fallback：從 RawJson 全文搜尋第一個完整 JSON 物件
            review = TryParseReview(result.RawJson);
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
        var systemPrompt = BuildSystemPrompt();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var response = await provider.CompleteAsync(systemPrompt, prompt, ct);
                var review   = TryParseReview(response.Content);
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

    private static string BuildRosaReviewPrompt(
        TaskItem ceoTask,
        IReadOnlyList<RequirementIssuePreview> issues)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 審核類型");
        sb.AppendLine("Rosa Requirements Agent 產出的 Issues 規格審核");
        sb.AppendLine();
        sb.AppendLine("## 原始需求");
        sb.AppendLine(ceoTask.Title);
        if (!string.IsNullOrWhiteSpace(ceoTask.Description) && ceoTask.Description != ceoTask.Title)
        {
            sb.AppendLine();
            sb.AppendLine(ceoTask.Description);
        }
        sb.AppendLine();
        sb.AppendLine("## Rosa 產出的 Issues 清單");
        for (var i = 0; i < issues.Count; i++)
        {
            sb.AppendLine($"### Issue {i + 1}：{issues[i].Title}");
            sb.AppendLine(issues[i].Body);
            sb.AppendLine($"Labels: {string.Join(", ", issues[i].Labels)}");
            sb.AppendLine();
        }
        sb.AppendLine("## 你的任務");
        sb.AppendLine("只從需求面審核：");
        sb.AppendLine("1. 原始需求中的功能點是否都有對應的 Issue？");
        sb.AppendLine("2. 每個 Issue 是否有至少一條可從使用者角度測試的驗收條件？");
        sb.AppendLine();
        sb.AppendLine("**不要要求 Rosa 提供以下內容，這些不是她的責任：**");
        sb.AppendLine("- Entity / DTO / 資料庫 schema（Cody 的工作）");
        sb.AppendLine("- Service / API 架構（Cody 的工作）");
        sb.AppendLine("- UI 元件或互動流程細節（Demi 的工作）");
        sb.AppendLine("- 權限驗證、安全性、跨裝置 / 效能場景（非需求面）");
        sb.AppendLine();
        sb.AppendLine("**以下情況不構成 revise 理由：**");
        sb.AppendLine("- 驗收條件「可以更精確」但已經能測試 → approve");
        sb.AppendLine("- 文案用詞「可以更好」 → approve");
        sb.AppendLine("- Issue 粒度「可以再拆」但目前可實作 → approve");
        sb.AppendLine("- 「沒提到 XX 場景」但該場景非原始需求所述 → approve");
        sb.AppendLine();
        sb.AppendLine("**revise 的唯一理由：原始需求中明確提到的功能點，在 Issues 中完全找不到對應。**");
        sb.AppendLine("輸出 JSON 審核結果。");
        return sb.ToString();
    }

    private static string BuildDemiReviewPrompt(
        TaskItem ceoTask,
        IReadOnlyList<RequirementIssuePreview> issues,
        string uiSpec)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 審核類型");
        sb.AppendLine("Demi Designer Agent 產出的 UI 規格審核");
        sb.AppendLine();
        sb.AppendLine("## 原始需求");
        sb.AppendLine(ceoTask.Title);
        sb.AppendLine();
        sb.AppendLine("## Rosa Issues 清單（UI 規格必須涵蓋所有 Issue）");
        foreach (var issue in issues)
            sb.AppendLine($"- {issue.Title}");
        sb.AppendLine();
        sb.AppendLine("## Demi 產出的 UI 規格");
        sb.AppendLine(uiSpec);
        sb.AppendLine();
        sb.AppendLine("## 你的任務");
        sb.AppendLine("只審核覆蓋率：每個 Issue 的功能點是否都有對應的 UI 設計？");
        sb.AppendLine();
        sb.AppendLine("**不要審以下內容：**");
        sb.AppendLine("- 元件內部 props / event 設計（Cody 的工作）");
        sb.AppendLine("- CSS 細節、responsive、accessibility（除非原始需求要求）");
        sb.AppendLine("- 設計美感或文案用詞的主觀判斷");
        sb.AppendLine();
        sb.AppendLine("**revise 的唯一理由：某個 Issue 的功能點在 UI 規格中完全沒有對應的畫面設計。**");
        sb.AppendLine("輸出 JSON 審核結果。");
        return sb.ToString();
    }

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

    private static string BuildSystemPrompt() => """
        你是 Petra，專案經理。負責審核 AI 團隊的產出品質。

        重要原則：偏好 approve。你的職責是擋住「會出事」的問題，不是追求完美。如果產出能用，就放行。

        只輸出 JSON，不加任何說明文字、不加 markdown code block。

        格式：
        {
          "decision": "approve" | "revise" | "escalate",
          "summary": "一句話說明審核結論",
          "issues": [
            {
              "severity": "blocking" | "minor",
              "description": "具體問題描述"
            }
          ],
          "revision_instructions": "打回修正時給 Agent 的具體修改指示（approve 時為 null）"
        }

        decision 說明：
        - approve：無 blocking 問題，放行
        - revise：有 blocking 問題，打回修正（提供 revision_instructions）
        - escalate：情況複雜或 2 次修正後仍不通過，需老闆決定

        使用繁體中文，程式碼與專有名詞保留英文。
        """;

    // ────────────── 23-3：Blocker 評估 ──────────────

    /// <summary>
    /// 評估 Cody 回報的開發阻礙，決定路由：continue / escalate_victoria / escalate_boss。
    /// </summary>
    public async Task<BlockerDecision> AssessBlockerAsync(
        string blockerJson,
        string taskTitle,
        CancellationToken ct = default)
    {
        var prompt = BuildBlockerAssessPrompt(blockerJson, taskTitle);
        return await RunBlockerLlmAsync(prompt, ct);
    }

    private async Task<BlockerDecision> RunBlockerLlmAsync(string prompt, CancellationToken ct)
    {
        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = BuildBlockerSystemPrompt();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var response = await provider.CompleteAsync(systemPrompt, prompt, ct);
                var decision = TryParseBlockerDecision(response.Content);
                if (decision is not null)
                {
                    logger.LogInformation("Petra Blocker 評估完成（第 {Attempt} 次）：{Routing}", attempt, decision.Routing);
                    return decision;
                }
                logger.LogWarning("Petra Blocker 回應解析失敗（第 {Attempt} 次）：{Content}", attempt, response.Content);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Petra Blocker LLM 呼叫失敗（第 {Attempt} 次）", attempt);
            }
        }

        logger.LogError("Petra Blocker 評估所有路徑均失敗，fallback escalate_boss");
        return new BlockerDecision("escalate_boss", "Blocker 評估失敗，升級給老闆");
    }

    private static string BuildBlockerAssessPrompt(string blockerJson, string taskTitle)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 任務");
        sb.AppendLine(taskTitle);
        sb.AppendLine();
        sb.AppendLine("## Cody 回報的阻礙");
        sb.AppendLine(blockerJson);
        sb.AppendLine();
        sb.AppendLine("## 你的任務");
        sb.AppendLine("評估此阻礙是否可解決：");
        sb.AppendLine("- `continue`：阻礙描述不明確或可以讓 Cody 重試");
        sb.AppendLine("- `escalate_victoria`：需要 CEO Victoria 提供決策或資訊");
        sb.AppendLine("- `escalate_boss`：需要老闆介入（外部系統、API Key、根本性問題）");
        sb.AppendLine();
        sb.AppendLine("只輸出 JSON：{\"routing\": \"continue|escalate_victoria|escalate_boss\", \"instructions\": \"具體說明\"}");
        return sb.ToString();
    }

    private static string BuildBlockerSystemPrompt() => """
        你是 Petra，專案經理。負責評估 Cody 回報的開發阻礙。
        只輸出 JSON，不加任何說明文字、不加 markdown code block。
        使用繁體中文。
        """;

    private static BlockerDecision? TryParseBlockerDecision(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;
            var json  = content[start..(end + 1)];
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var routing      = root.TryGetProperty("routing",      out var r) ? r.GetString() : null;
            var instructions = root.TryGetProperty("instructions", out var i) ? i.GetString() : null;
            if (string.IsNullOrWhiteSpace(routing)) return null;
            return new BlockerDecision(routing, instructions ?? "");
        }
        catch { return null; }
    }

    // ────────────── 23-1：Review Appeal ──────────────

    /// <summary>
    /// 模擬 Cody 針對 Vera Critical Issues 的逐條回應（agree / disagree + 具體理由）。
    /// 第二輪起附上累計對話紀錄，讓 Cody 只針對剩餘 criticals 回應。
    /// </summary>
    public async Task<CodyAppeal> RunCodyAppealAsync(
        string reviewBody,
        string taskTitle,
        IReadOnlyList<int> remainingCriticalIds,
        string? priorContext,
        CancellationToken ct = default)
    {
        var prompt       = BuildCodyAppealPrompt(reviewBody, taskTitle, remainingCriticalIds, priorContext);
        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = BuildCodyAppealSystemPrompt();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var response = await provider.CompleteAsync(systemPrompt, prompt, ct);
                var appeal   = TryParseCodyAppeal(response.Content);
                if (appeal is not null)
                {
                    logger.LogInformation("Cody Appeal 解析成功（{Count} 項回應）", appeal.Items.Count);
                    return appeal;
                }
                logger.LogWarning("Cody Appeal 回應解析失敗（第 {Attempt} 次）", attempt);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RunCodyAppealAsync LLM 呼叫失敗（第 {Attempt} 次）", attempt);
            }
        }

        logger.LogError("RunCodyAppealAsync 失敗，fallback agree all");
        var fallbackItems = remainingCriticalIds
            .Select(id => new CodyAppealItem(id, "agree", "評估失敗，同意修正"))
            .ToList();
        return new CodyAppeal(fallbackItems);
    }

    /// <summary>
    /// 模擬 Vera 基於程式碼事實重新評估 Cody 反駁的 disagree 項目。
    /// </summary>
    public async Task<VeraAppealResponse> RunVeraAppealAsync(
        string reviewBody,
        string codyAppealJson,
        CancellationToken ct = default)
    {
        var prompt       = BuildVeraAppealPrompt(reviewBody, codyAppealJson);
        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = BuildVeraAppealSystemPrompt();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var response     = await provider.CompleteAsync(systemPrompt, prompt, ct);
                var veraResponse = TryParseVeraAppealResponse(response.Content);
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
                logger.LogWarning(ex, "RunVeraAppealAsync LLM 呼叫失敗（第 {Attempt} 次）", attempt);
            }
        }

        logger.LogError("RunVeraAppealAsync 失敗，fallback 維持所有 critical");
        return new VeraAppealResponse([], [], "重評失敗，維持所有 critical");
    }

    /// <summary>
    /// Petra 仲裁 Cody-Vera 爭議，決定最終 Critical Issues 清單。
    /// </summary>
    public async Task<AppealArbitration> ArbitrateReviewAppealAsync(
        string reviewBody,
        string appealLog,
        CancellationToken ct = default)
    {
        var prompt       = BuildArbitrationPrompt(reviewBody, appealLog);
        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = BuildArbitrationSystemPrompt();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var response    = await provider.CompleteAsync(systemPrompt, prompt, ct);
                var arbitration = TryParseArbitration(response.Content);
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
                logger.LogWarning(ex, "ArbitrateReviewAppealAsync LLM 呼叫失敗（第 {Attempt} 次）", attempt);
            }
        }

        logger.LogError("ArbitrateReviewAppealAsync 失敗，fallback support_vera");
        return new AppealArbitration("support_vera", [], "仲裁失敗，維持 Vera 判斷");
    }

    // ── Appeal Prompt 組建 ──

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

    // ── Appeal JSON 解析 ──

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

    // ────────────── JSON 解析 ──────────────

    private static PetraReview? TryParseReview(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;
            var json  = content[start..(end + 1)];
            var dto   = JsonSerializer.Deserialize<PetraReviewDto>(json, JsonOptions);
            if (dto is null || string.IsNullOrWhiteSpace(dto.Decision)) return null;

            var decision = dto.Decision.ToLowerInvariant() switch
            {
                "approve"  => "approve",
                "revise"   => "revise",
                "escalate" => "escalate",
                _          => "approve" // 未知值 fallback 放行
            };

            var issues = dto.Issues?.Select(i => new PetraIssue(
                i.Severity ?? "minor",
                i.Description ?? "")).ToList() ?? [];

            return new PetraReview(decision, dto.Summary ?? "", issues, dto.RevisionInstructions);
        }
        catch { return null; }
    }

    // ────────────── 內部 DTO ──────────────

    private sealed class PetraReviewDto
    {
        [JsonPropertyName("decision")]
        public string? Decision { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("issues")]
        public List<PetraIssueDto>? Issues { get; set; }

        [JsonPropertyName("revision_instructions")]
        public string? RevisionInstructions { get; set; }
    }

    private sealed class PetraIssueDto
    {
        [JsonPropertyName("severity")]
        public string? Severity { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}

/// <summary>Petra 審核結果。</summary>
public record PetraReview(
    string Decision,              // "approve" | "revise" | "escalate"
    string Summary,
    IReadOnlyList<PetraIssue> Issues,
    string? RevisionInstructions);

/// <summary>Petra 發現的單一問題。</summary>
public record PetraIssue(string Severity, string Description);

// ────────────── 23-3：Blocker 評估結果 ──────────────

/// <summary>Petra 對 Dev Blocker 的路由決定。</summary>
public record BlockerDecision(
    string Routing,       // "continue" | "escalate_victoria" | "escalate_boss"
    string Instructions); // 說明

// ────────────── 23-1：Review Appeal 結果 ──────────────

/// <summary>Cody 針對 Critical Issues 的逐條回應（Appeal Round A）。</summary>
public record CodyAppeal(IReadOnlyList<CodyAppealItem> Items);

/// <summary>Cody 針對單一 Critical Issue 的回應。</summary>
public record CodyAppealItem(
    int    Id,
    string Response, // "agree" | "disagree"
    string Reason);

/// <summary>Vera 重新評估 Cody 反駁後的結果。</summary>
public record VeraAppealResponse(
    IReadOnlyList<int> AcceptedIds,    // Vera 接受（從 critical 移除）的 IDs
    IReadOnlyList<int> MaintainedIds,  // Vera 維持的 IDs
    string UpdatedSummary);

/// <summary>Petra 仲裁 Cody-Vera 爭議的最終決定。</summary>
public record AppealArbitration(
    string Decision,                   // "support_vera" | "support_cody_partial" | "support_cody_full"
    IReadOnlyList<int> FinalCriticals, // 最終成立的 Critical IDs
    string Reasoning);
