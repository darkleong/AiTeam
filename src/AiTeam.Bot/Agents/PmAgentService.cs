using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.GitHub;
using AiTeam.Data;
using Microsoft.Extensions.Options;

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
/// - 申訴環節（Stage 30）：RunMeetingSessionAsync（新 session，唯讀工具）
/// </summary>
public class PmAgentService(
    LlmProviderFactory providerFactory,
    IClaudeCodeService claudeCodeService,
    IConfiguration configuration,
    GitHubService gitHubService,
    IOptions<GitHubSettings> gitHubSettings,
    ILogger<PmAgentService> logger)
{
    private const string AgentName = "PM";

    private readonly GitHubSettings _gitHub = gitHubSettings.Value;

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

    // ────────────── Stage 30：申訴共用輔助方法 ──────────────

    /// <summary>
    /// Clone/Pull repo 並讀取 model + API key，供 5 個申訴環節共用。
    /// CloneOrPull 失敗時 workingDir 回傳空字串（各方法的 finally 不會清理空路徑）。
    /// </summary>
    private (string workingDir, string model, string apiKey) PrepareClaudeCodeEnv(
        TaskGroup group, string agentConfigKey)
    {
        var model  = configuration[$"Agents:{agentConfigKey}:Model"]
                  ?? configuration["Anthropic:DefaultModel"]
                  ?? "claude-haiku-4-5";
        var apiKey = configuration["Anthropic:ApiKey"] ?? "";

        var owner = _gitHub.Owner;
        var repo  = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;
        var workingDir = "";
        try
        {
            workingDir = gitHubService.CloneOrPull(owner, repo, $"appeal-{group.Id:N}"[..12]);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PrepareClaudeCodeEnv CloneOrPull 失敗，workingDir 為空（group={Id}）", group.Id);
        }
        return (workingDir, model, apiKey);
    }

    /// <summary>
    /// 組建「任務背景脈絡」區塊，供 5 個申訴 prompt 共用。
    /// 含 TaskPlan / DesignPlan / DevPlan / ImplementationNote / PR diff（best-effort）。
    /// Dev_plan Appeal 場景通常尚未建 PR，TryParsePrNumber 會自動返回 false 跳過。
    /// </summary>
    private async Task<string> BuildAppealContextSectionAsync(TaskGroup group)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 任務背景脈絡（供閱讀 codebase 時參考）");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(group.TaskPlan))
        {
            sb.AppendLine("### TaskPlan（Kickoff 會議產出）");
            sb.AppendLine(group.TaskPlan.Length > 2000 ? group.TaskPlan[..2000] + "\n...（截斷）" : group.TaskPlan);
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(group.DesignPlan))
        {
            sb.AppendLine("### DesignPlan（設計規劃書）");
            sb.AppendLine(group.DesignPlan.Length > 2000 ? group.DesignPlan[..2000] + "\n...（截斷）" : group.DesignPlan);
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(group.DevPlan))
        {
            sb.AppendLine("### DevPlan（實作計畫書）");
            sb.AppendLine(group.DevPlan.Length > 2000 ? group.DevPlan[..2000] + "\n...（截斷）" : group.DevPlan);
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(group.ImplementationNote))
        {
            sb.AppendLine("### ImplementationNote（Cody 實作自述）");
            sb.AppendLine(group.ImplementationNote.Length > 2000 ? group.ImplementationNote[..2000] + "\n...（截斷）" : group.ImplementationNote);
            sb.AppendLine();
        }

        if (TryParsePrNumber(group.DevPrUrl, out var prNumber))
        {
            try
            {
                var owner = _gitHub.Owner;
                var repo  = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;
                var files = await gitHubService.GetPullRequestFilesAsync(owner, repo, prNumber);
                if (files.Count > 0)
                {
                    sb.AppendLine($"### PR #{prNumber} 變更摘要（{files.Count} 個檔案）");
                    foreach (var f in files.Take(15))
                    {
                        sb.AppendLine($"**{f.FileName}** (+{f.Additions}/-{f.Deletions})");
                        if (!string.IsNullOrWhiteSpace(f.Patch))
                            sb.AppendLine($"```diff\n{(f.Patch.Length > 400 ? f.Patch[..400] + "\n..." : f.Patch)}\n```");
                    }
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "BuildAppealContextSectionAsync 取得 PR diff 失敗（PR#{N}），略過", prNumber);
            }
        }

        return sb.ToString();
    }

    private static bool TryParsePrNumber(string? prUrl, out int prNumber)
    {
        prNumber = 0;
        if (string.IsNullOrWhiteSpace(prUrl)) return false;
        var parts = prUrl.TrimEnd('/').Split('/');
        return parts.Length > 0 && int.TryParse(parts[^1], out prNumber);
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

        var context        = await BuildAppealContextSectionAsync(group);
        var userPrompt     = BuildCodyAppealPrompt(reviewBody, taskTitle, remainingCriticalIds, priorContext);
        var combinedPrompt = $"[APPEAL:review_cody]\n{BuildCodyAppealSystemPrompt()}\n\n---\n\n{context}\n\n{userPrompt}";
        var sessionId      = Guid.NewGuid().ToString();

        var (workingDir, model, apiKey) = PrepareClaudeCodeEnv(group, "Dev");
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
            if (!string.IsNullOrEmpty(workingDir))
                try { gitHubService.CleanupLocalRepo(workingDir); }
                catch (Exception ex) { logger.LogWarning(ex, "RunCodyAppealAsync cleanup 失敗"); }
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

        var context        = await BuildAppealContextSectionAsync(group);
        var userPrompt     = BuildVeraAppealPrompt(reviewBody, codyAppealJson);
        var combinedPrompt = $"[APPEAL:review_vera]\n{BuildVeraAppealSystemPrompt()}\n\n---\n\n{context}\n\n{userPrompt}";
        var sessionId      = Guid.NewGuid().ToString();

        var (workingDir, model, apiKey) = PrepareClaudeCodeEnv(group, "Reviewer");
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
            if (!string.IsNullOrEmpty(workingDir))
                try { gitHubService.CleanupLocalRepo(workingDir); }
                catch (Exception ex) { logger.LogWarning(ex, "RunVeraAppealAsync cleanup 失敗"); }
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
        var context        = await BuildAppealContextSectionAsync(group);
        var userPrompt     = BuildArbitrationPrompt(reviewBody, appealLog);
        var combinedPrompt = $"[APPEAL:review_arbitration]\n{BuildArbitrationSystemPrompt()}\n\n---\n\n{context}\n\n{userPrompt}";
        var sessionId      = Guid.NewGuid().ToString();

        var (workingDir, model, apiKey) = PrepareClaudeCodeEnv(group, "PM");
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
            if (!string.IsNullOrEmpty(workingDir))
                try { gitHubService.CleanupLocalRepo(workingDir); }
                catch (Exception ex) { logger.LogWarning(ex, "ArbitrateReviewAppealAsync cleanup 失敗"); }
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

    // ────────────── 24-1：QA 失敗評估 ──────────────

    /// <summary>
    /// 評估 QA 測試失敗的原因，決定後續路由。
    /// </summary>
    public async Task<QaFailureDecision> AssessQaFailureAsync(
        TaskGroup group,
        string testReportJson,
        CancellationToken ct = default)
    {
        var prompt       = BuildQaFailureAssessPrompt(group, testReportJson);
        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = BuildQaAssessSystemPrompt();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var response = await provider.CompleteAsync(systemPrompt, prompt, ct);
                var decision = TryParseQaFailureDecision(response.Content);
                if (decision is not null)
                {
                    logger.LogInformation("Petra QA 失敗評估完成（第 {Attempt} 次）：{Routing}", attempt, decision.Routing);
                    return decision;
                }
                logger.LogWarning("Petra QA 失敗評估解析失敗（第 {Attempt} 次）：{Content}", attempt, response.Content);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Petra QA 失敗評估 LLM 呼叫失敗（第 {Attempt} 次）", attempt);
            }
        }

        logger.LogError("Petra QA 失敗評估所有路徑均失敗，fallback env_or_test_issue（不卡流程）");
        return new QaFailureDecision("env_or_test_issue", "QA 評估失敗，略過以免卡住流程");
    }

    /// <summary>
    /// 評估 QA 無適合測試點的理由是否合理，決定是否放行。
    /// </summary>
    public async Task<QaNoTestDecision> AssessNoApplicableTestsAsync(
        TaskGroup group,
        string? noTestReason,
        CancellationToken ct = default)
    {
        var prompt       = BuildNoTestAssessPrompt(group, noTestReason);
        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = BuildQaAssessSystemPrompt();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var response = await provider.CompleteAsync(systemPrompt, prompt, ct);
                var decision = TryParseQaNoTestDecision(response.Content);
                if (decision is not null)
                {
                    logger.LogInformation("Petra QA 無測試評估完成（第 {Attempt} 次）：{Routing}", attempt, decision.Routing);
                    return decision;
                }
                logger.LogWarning("Petra QA 無測試評估解析失敗（第 {Attempt} 次）：{Content}", attempt, response.Content);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Petra QA 無測試評估 LLM 呼叫失敗（第 {Attempt} 次）", attempt);
            }
        }

        logger.LogError("Petra QA 無測試評估所有路徑均失敗，fallback approve（不卡流程）");
        return new QaNoTestDecision("approve", "QA 無測試評估失敗，自動放行");
    }

    private static string BuildQaAssessSystemPrompt() => """
        你是 Petra，專案經理。負責評估 QA 測試結果並決定後續路由。
        偏好放行（approve / env_or_test_issue），只在確認是程式碼 bug 時才觸發修復。
        只輸出 JSON，不加任何說明文字、不加 markdown code block。
        使用繁體中文。
        """;

    private static string BuildQaFailureAssessPrompt(TaskGroup group, string testReportJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 任務背景");
        sb.AppendLine($"Task Group：{group.Title}");
        if (!string.IsNullOrWhiteSpace(group.ImplementationNote))
        {
            sb.AppendLine();
            sb.AppendLine("## Cody 實作說明");
            sb.AppendLine(group.ImplementationNote.Length > 1500
                ? group.ImplementationNote[..1500] + "\n...（截斷）"
                : group.ImplementationNote);
        }
        sb.AppendLine();
        sb.AppendLine("## Quinn 的測試報告（JSON）");
        sb.AppendLine(testReportJson);
        sb.AppendLine();
        sb.AppendLine("## 你的任務");
        sb.AppendLine("評估測試失敗的根本原因，選擇路由：");
        sb.AppendLine("- `code_bug`：確認是程式碼邏輯 bug，**小修正**即可，Dev_fix 後直接重測（跳過 Vera）");
        sb.AppendLine("- `back_to_reviewer`：確認是程式碼 bug，但**需要大幅改動**，Dev_fix 後需回 Vera 正常審查");
        sb.AppendLine("- `env_or_test_issue`：測試本身的問題（錯誤的期望值、環境差異、測試設計不當），視同通過");
        sb.AppendLine("- `escalate_boss`：反覆失敗無法判斷原因，需要老闆介入");
        sb.AppendLine();
        sb.AppendLine("只輸出 JSON：{\"routing\": \"code_bug|back_to_reviewer|env_or_test_issue|escalate_boss\", \"instructions\": \"具體說明\"}");
        return sb.ToString();
    }

    private static string BuildNoTestAssessPrompt(TaskGroup group, string? noTestReason)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 任務背景");
        sb.AppendLine($"Task Group：{group.Title}");
        sb.AppendLine();
        sb.AppendLine("## Quinn 無法測試的理由");
        sb.AppendLine(noTestReason ?? "（未提供理由）");
        sb.AppendLine();
        sb.AppendLine("## 你的任務");
        sb.AppendLine("評估 Quinn 無法測試的理由是否合理：");
        sb.AppendLine("- `approve`：理由合理（如純設定檔/migration 變更），直接放行");
        sb.AppendLine("- `escalate_boss`：理由不充分或有疑慮，需要老闆決策");
        sb.AppendLine();
        sb.AppendLine("只輸出 JSON：{\"routing\": \"approve|escalate_boss\", \"instructions\": \"具體說明\"}");
        return sb.ToString();
    }

    private static QaFailureDecision? TryParseQaFailureDecision(string content)
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
            return new QaFailureDecision(routing, instructions ?? "");
        }
        catch { return null; }
    }

    private static QaNoTestDecision? TryParseQaNoTestDecision(string content)
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
            return new QaNoTestDecision(routing, instructions ?? "");
        }
        catch { return null; }
    }

    // ────────────── 24-2：Dev_plan Appeal ──────────────

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

        var context        = await BuildAppealContextSectionAsync(group);
        var userPrompt     = BuildCodyDevPlanAppealPrompt(group, petraReview, priorContext);
        var combinedPrompt = $"[APPEAL:dev_plan_cody]\n{BuildCodyDevPlanAppealSystemPrompt()}\n\n---\n\n{context}\n\n{userPrompt}";
        var sessionId      = Guid.NewGuid().ToString();

        var (workingDir, model, apiKey) = PrepareClaudeCodeEnv(group, "Dev");
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
            if (!string.IsNullOrEmpty(workingDir))
                try { gitHubService.CleanupLocalRepo(workingDir); }
                catch (Exception ex) { logger.LogWarning(ex, "RunCodyDevPlanAppealAsync cleanup 失敗"); }
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
        var context        = await BuildAppealContextSectionAsync(group);
        var userPrompt     = BuildReassessDevPlanPrompt(group, codyAppeal, previousReview);
        var combinedPrompt = $"[APPEAL:dev_plan_petra]\n{BuildSystemPrompt()}\n\n---\n\n{context}\n\n{userPrompt}";
        var sessionId      = Guid.NewGuid().ToString();

        var (workingDir, model, apiKey) = PrepareClaudeCodeEnv(group, "PM");
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
                    var review = TryParseReview(result.Output);
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
            if (!string.IsNullOrEmpty(workingDir))
                try { gitHubService.CleanupLocalRepo(workingDir); }
                catch (Exception ex) { logger.LogWarning(ex, "ReassessDevPlanAsync cleanup 失敗"); }
        }

        logger.LogError("Petra Dev_plan 重評所有路徑均失敗，fallback approve（不卡流程）");
        return new PetraReview("approve", "重評失敗，自動放行", [], null);
    }

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

// ────────────── 24-1：QA 評估結果 ──────────────

/// <summary>Petra 對 QA 失敗的路由決定。</summary>
public record QaFailureDecision(
    string Routing,        // "code_bug" | "back_to_reviewer" | "env_or_test_issue" | "escalate_boss"
    string Instructions);

/// <summary>Petra 對 QA 無適合測試點的決定。</summary>
public record QaNoTestDecision(
    string Routing,        // "approve" | "escalate_boss"
    string Instructions);

// ────────────── 24-2：Dev_plan Appeal 結果 ──────────────

/// <summary>Cody 針對 Petra Dev_plan 修改意見的反駁（或接受）。</summary>
public record CodyDevPlanAppeal(
    string Position,    // "disagree" | "accept"
    string Reasoning);  // 反駁的技術論點（accept 時可為空）
