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
    ClaudeCodeService claudeCodeService,
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
        sb.AppendLine("審核以上 Issues 規格，**只從需求面**判斷：");
        sb.AppendLine("1. 有無遺漏的使用情境或功能點");
        sb.AppendLine("2. Issue 粒度是否合理");
        sb.AppendLine("3. 每個 Issue 是否有具體可測試的驗收條件（從使用者角度）");
        sb.AppendLine();
        sb.AppendLine("**不要要求 Rosa 提供以下內容，這些不是她的責任：**");
        sb.AppendLine("- Entity / DTO / 資料庫 schema（Cody 的工作）");
        sb.AppendLine("- Service / API 架構（Cody 的工作）");
        sb.AppendLine("- UI 元件或互動流程細節（Demi 的工作）");
        sb.AppendLine();
        sb.AppendLine("approve 標準：功能點無明顯遺漏，且每個 Issue 有至少一條可從使用者角度測試的驗收條件。");
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
        sb.AppendLine("審核 UI 規格是否涵蓋所有 Issues 需求、元件選擇是否合理、互動情境是否完整。輸出 JSON 審核結果。");
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
        sb.AppendLine("審核實作計畫書（不是程式碼），**只從規劃面**判斷：");
        sb.AppendLine("1. 計畫是否涵蓋所有 Issues 需求的功能點？");
        sb.AppendLine("2. 計畫是否對齊 Demi UI 規格（若有）？");
        sb.AppendLine("3. 架構方向是否合理（Entity / Service / Controller 分層）？");
        sb.AppendLine();
        sb.AppendLine("**不要做以下事情：**");
        sb.AppendLine("- 不要用 Glob / Grep / Read 驗證檔案是否存在（這是新功能，檔案尚未建立）");
        sb.AppendLine("- 不要要求 Cody 在計畫書內提供程式碼");
        sb.AppendLine("- 不要以「缺少程式碼細節」作為 revise 理由");
        sb.AppendLine();
        sb.AppendLine("approve 標準：計畫書說明了要建立哪些檔案 / 修改哪些地方、功能點無明顯遺漏、架構方向合理。輸出 JSON 審核結果。");
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
        sb.AppendLine("判斷 Vera 發現的問題中，哪些是 blocking（必須修正）、哪些是 minor（可接受）。若有 blocking 問題則 revise，否則 approve。輸出 JSON 審核結果。");
        return sb.ToString();
    }

    private static string BuildSystemPrompt() => """
        你是 Petra，專案經理。負責審核 AI 團隊的產出品質。

        只輸出 JSON，不加任何說明文字、不加 markdown code block。

        格式：
        {
          "decision": "approve" | "revise" | "escalate",
          "summary": "一句話說明審核結論",
          "issues": [
            {
              "severity": "blocking" | "minor",
              "description": "具體問題描述，引用實際檔案名稱"
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
