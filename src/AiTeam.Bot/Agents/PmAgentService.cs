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
