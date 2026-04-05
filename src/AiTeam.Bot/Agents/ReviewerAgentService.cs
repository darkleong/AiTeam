using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.ViewModels;
using Microsoft.Extensions.Configuration;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Reviewer Agent（Vera）：讀取 PR 差異，呼叫 LLM 產出分級審查報告，
/// 並透過 GitHub Review API 在 PR 上留下整體審查意見。
/// Stage 12：新增 Claude Code 唯讀補強探索（影響範圍分析）。
/// </summary>
public class ReviewerAgentService(
    LlmProviderFactory providerFactory,
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
    ClaudeCodeService claudeCodeService,
    IConfiguration configuration,
    ILogger<ReviewerAgentService> logger) : IAgentExecutor
{
    private const string AgentName = "Reviewer";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public async Task<AgentExecutionResult> ExecuteTaskAsync(
        TaskItem task,
        string owner,
        string repo,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default)
    {
        AddLog(task, "Reviewer Agent 開始執行", "running");
        await taskRepository.SaveAsync(cancellationToken);
        await PushStatus("running", task.Id, task.Title);

        try
        {
            // 1. 從任務描述解析 PR 編號；未指定時自動取最新 open PR
            var prNumber = ExtractPrNumber($"{task.Title} {task.Description}");
            if (prNumber <= 0)
            {
                AddLog(task, "未指定 PR 編號，自動取最新 open PR", "running");
                await taskRepository.SaveAsync(cancellationToken);
                prNumber = await gitHubService.GetLatestOpenPullRequestNumberAsync(owner, repo);
            }
            if (prNumber <= 0)
                return Fail(task, "找不到任何 open PR，請先開一個 PR 或指定格式：PR #123");

            // 2. 取得 PR 的變更檔案（僅審查 .cs 檔）
            var prFiles = await gitHubService.GetPullRequestFilesAsync(owner, repo, prNumber);
            var headRef = await gitHubService.GetPullRequestHeadRefAsync(owner, repo, prNumber);
            var csFiles = prFiles
                .Where(f => f.FileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (csFiles.Count == 0)
                return Fail(task, $"PR #{prNumber} 未包含 .cs 檔案，略過 Reviewer");

            AddLog(task, $"PR #{prNumber} 共 {csFiles.Count} 個 .cs 檔待審查", "running");
            await taskRepository.SaveAsync(cancellationToken);

            // 3. 逐檔讀取內容並呼叫 LLM 審查
            var provider = providerFactory.Create(AgentName);
            var allIssues = new List<ReviewIssue>();
            var fileSummaries = new List<string>();

            foreach (var file in csFiles)
            {
                try
                {
                    var content = await gitHubService.GetFileContentAsync(owner, repo, file.FileName, headRef);
                    if (string.IsNullOrWhiteSpace(content)) continue;

                    var systemPrompt = BuildReviewSystemPrompt(rules);
                    var userMessage  = BuildReviewUserMessage(file.FileName, file.Patch ?? "", content);

                    var response = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken);
                    var report   = TryParseReviewReport(response.Content);

                    if (report is not null)
                    {
                        allIssues.AddRange(report.Critical.Select(i => i with { File = file.FileName, Severity = "critical" }));
                        allIssues.AddRange(report.Warning .Select(i => i with { File = file.FileName, Severity = "warning"  }));
                        allIssues.AddRange(report.Info    .Select(i => i with { File = file.FileName, Severity = "info"     }));
                        if (!string.IsNullOrWhiteSpace(report.Summary))
                            fileSummaries.Add($"**{file.FileName}**：{report.Summary}");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "審查檔案失敗，略過：{File}", file.FileName);
                }
            }

            // 4. 組建整體 Review Body
            var reviewBody = BuildReviewBody(allIssues, fileSummaries, prNumber);

            // 4b. Claude Code 唯讀補強：探索影響範圍（非阻塞，失敗不影響主流程）
            var impactAnalysis = await RunImpactAnalysisAsync(
                owner, repo, headRef, prFiles.Select(f => f.FileName).ToList(), task, cancellationToken);
            if (!string.IsNullOrWhiteSpace(impactAnalysis))
                reviewBody += $"\n\n---\n\n## 🔭 影響範圍分析（Claude Code 探索）\n\n{impactAnalysis}";

            // 5. 在 GitHub PR 上提交 Review
            AddLog(task, "提交 GitHub Review 中...", "running");
            await taskRepository.SaveAsync(cancellationToken);

            var reviewUrl = await gitHubService.CreatePullRequestReviewAsync(owner, repo, prNumber, reviewBody);

            var criticalCount = allIssues.Count(i => i.Severity == "critical");
            var warningCount  = allIssues.Count(i => i.Severity == "warning");
            var infoCount     = allIssues.Count(i => i.Severity == "info");
            var summary = $"PR #{prNumber} 審查完成：🔴 {criticalCount} 個必修 / 🟡 {warningCount} 個建議 / 🟢 {infoCount} 個優化";

            AddLog(task, summary, "done");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("done", task.Id, task.Title);

            return new AgentExecutionResult(true, summary, reviewUrl,
                CriticalReviewCount: criticalCount,
                ReviewBody: reviewBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reviewer Agent 執行失敗（TaskId={Id}）", task.Id);
            AddLog(task, $"執行失敗：{ex.Message}", "failed");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("failed", task.Id, task.Title);
            return Fail(task, ex.Message);
        }
    }

    // ────────────── Prompt 建構 ──────────────

    private static string BuildReviewSystemPrompt(IReadOnlyList<string> rules)
    {
        var ruleList = rules.Count > 0
            ? string.Join("\n", rules.Select(r => $"- {r}"))
            : "（尚無額外規則）";

        return $$"""
            你是資深 C# / .NET / Blazor 程式碼審查工程師 Vera，負責程式碼品質把關。

            ## 審查面向
            - 邏輯正確性：功能是否符合預期、邊界情況是否處理
            - 程式碼品質：是否符合 C# 命名規範、async/await 正確使用
            - 效能：是否有 N+1 查詢、不必要迴圈、記憶體洩漏
            - 安全性：是否有 SQL Injection、敏感資訊洩露、未驗證輸入
            - 可維護性：是否過度複雜、是否需要重構

            ## 專案規則
            {{ruleList}}

            ## 回應格式（JSON，不得包含任何其他文字）
            {
              "critical": [{"file": "路徑", "line": 0, "message": "問題說明（繁體中文）"}],
              "warning":  [{"file": "路徑", "line": 0, "message": "建議說明（繁體中文）"}],
              "info":     [{"file": "路徑", "line": 0, "message": "優化建議（繁體中文）"}],
              "summary":  "這個檔案的整體評語（一句話，繁體中文）"
            }

            - critical：安全漏洞、嚴重 bug、資源洩漏 → 必須修改才能合併
            - warning：效能問題、違反 SOLID、缺少 null 處理 → 建議修改
            - info：命名改善、可讀性提升 → 可選優化
            - 若無問題，對應陣列留空 []
            - line 填原始檔案中大約的行號（不確定時填 0）
            """;
    }

    private static string BuildReviewUserMessage(string filePath, string patch, string content)
        => $"""
            ## 檔案路徑
            {filePath}

            ## diff（PR 變更）
            ```diff
            {patch}
            ```

            ## 完整檔案內容
            ```csharp
            {content}
            ```

            請依照格式審查此檔案。
            """;

    // ────────────── Review Body 組建 ──────────────

    private static string BuildReviewBody(
        IReadOnlyList<ReviewIssue> issues,
        IReadOnlyList<string> fileSummaries,
        int prNumber)
    {
        if (issues.Count == 0)
            return $"## ✅ PR #{prNumber} 程式碼審查通過\n\n未發現任何問題，程式碼品質良好。";

        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"## 🔍 PR #{prNumber} 程式碼審查報告");
        lines.AppendLine();

        var criticals = issues.Where(i => i.Severity == "critical").ToList();
        var warnings  = issues.Where(i => i.Severity == "warning" ).ToList();
        var infos     = issues.Where(i => i.Severity == "info"    ).ToList();

        if (criticals.Count > 0)
        {
            lines.AppendLine("### 🔴 必須修改（Critical）");
            foreach (var i in criticals)
                lines.AppendLine($"- **`{i.File}`** (line ~{i.Line}): {i.Message}");
            lines.AppendLine();
        }

        if (warnings.Count > 0)
        {
            lines.AppendLine("### 🟡 建議修改（Warning）");
            foreach (var i in warnings)
                lines.AppendLine($"- **`{i.File}`** (line ~{i.Line}): {i.Message}");
            lines.AppendLine();
        }

        if (infos.Count > 0)
        {
            lines.AppendLine("### 🟢 優化建議（Info）");
            foreach (var i in infos)
                lines.AppendLine($"- **`{i.File}`** (line ~{i.Line}): {i.Message}");
            lines.AppendLine();
        }

        if (fileSummaries.Count > 0)
        {
            lines.AppendLine("### 📝 各檔案總結");
            foreach (var s in fileSummaries)
                lines.AppendLine($"- {s}");
        }

        lines.AppendLine();
        lines.AppendLine("---");
        lines.AppendLine("*由 Vera（Reviewer Agent）自動審查*");

        return lines.ToString();
    }

    // ────────────── 解析 ──────────────

    private ReviewReport? TryParseReviewReport(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;

            return JsonSerializer.Deserialize<ReviewReport>(content[start..(end + 1)], JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ReviewReport 解析失敗");
            return null;
        }
    }

    private static int ExtractPrNumber(string text)
    {
        var match = Regex.Match(text, @"PR\s*#(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    // ────────────── 輔助方法 ──────────────

    private void AddLog(TaskItem task, string step, string status)
        => task.Logs.Add(new TaskLog
        {
            TaskId    = task.Id,
            Agent     = AgentName,
            Step      = step,
            Status    = status,
            CreatedAt = DateTime.UtcNow
        });

    private async Task PushStatus(string status, Guid taskId, string title)
        => await dashboardPush.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = taskId,
            Title     = title,
            AgentName = AgentName,
            Status    = status
        });

    private static AgentExecutionResult Fail(TaskItem task, string message)
        => new(false, message);

    // ────────────── Claude Code 影響範圍分析 ──────────────

    /// <summary>
    /// 使用 Claude Code 唯讀模式 checkout 到 PR branch，探索影響範圍。
    /// 失敗時靜默忽略，不影響主要審查流程。
    /// </summary>
    private async Task<string> RunImpactAnalysisAsync(
        string owner,
        string repo,
        string headRef,
        IReadOnlyList<string> changedFiles,
        TaskItem task,
        CancellationToken cancellationToken)
    {
        var localPath = "";
        try
        {
            localPath = gitHubService.CloneOrPull(owner, repo, $"vera-{task.Id:N}"[..8]);
            // Checkout 到 PR branch，才能看到 PR 的最新變更
            gitHubService.CreateAndCheckoutBranch(localPath, headRef);

            var claudeMdPath     = Path.Combine(localPath, "CLAUDE.md");
            var templatePath     = Path.Combine(AppContext.BaseDirectory, "Resources", "CLAUDE_Vera.md");
            var originalClaudeMd = File.Exists(claudeMdPath)
                ? await File.ReadAllTextAsync(claudeMdPath, cancellationToken)
                : null;

            try
            {
                if (File.Exists(templatePath))
                    await File.WriteAllTextAsync(claudeMdPath,
                        await File.ReadAllTextAsync(templatePath, cancellationToken), cancellationToken);

                var prompt = BuildImpactPrompt(changedFiles);
                var model  = configuration["Agents:Reviewer:Model"]
                          ?? configuration["Anthropic:DefaultModel"]
                          ?? "claude-sonnet-4-6";
                var apiKey = configuration["Anthropic:ApiKey"] ?? "";

                var result = await claudeCodeService.RunReadOnlyAsync(
                    localPath, prompt, model, apiKey, cancellationToken);

                return result.Success ? result.Output.Trim() : "";
            }
            finally
            {
                if (originalClaudeMd is not null)
                    await File.WriteAllTextAsync(claudeMdPath, originalClaudeMd, CancellationToken.None);
                else if (File.Exists(claudeMdPath))
                    File.Delete(claudeMdPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Vera 影響範圍分析失敗（略過），PR branch={Branch}", headRef);
            return "";
        }
        finally
        {
            if (!string.IsNullOrEmpty(localPath))
                gitHubService.CleanupLocalRepo(localPath);
        }
    }

    private static string BuildImpactPrompt(IReadOnlyList<string> changedFiles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## PR 變更檔案");
        foreach (var f in changedFiles)
            sb.AppendLine($"- {f}");
        sb.AppendLine();
        sb.AppendLine("## 你的任務");
        sb.AppendLine("探索 codebase，找出上述變更檔案的相依關係與影響範圍，輸出影響範圍分析報告（Markdown 格式）。");

        return sb.ToString();
    }
}

// ────────────── 資料模型 ──────────────

public class ReviewReport
{
    [JsonPropertyName("critical")] public List<ReviewIssue> Critical { get; set; } = [];
    [JsonPropertyName("warning")]  public List<ReviewIssue> Warning  { get; set; } = [];
    [JsonPropertyName("info")]     public List<ReviewIssue> Info     { get; set; } = [];
    [JsonPropertyName("summary")]  public string Summary             { get; set; } = "";
}

public record ReviewIssue
{
    [JsonPropertyName("file")]     public string File     { get; init; } = "";
    [JsonPropertyName("line")]     public int    Line     { get; init; }
    [JsonPropertyName("message")]  public string Message  { get; init; } = "";
    public string Severity { get; init; } = "info"; // 由 ReviewerAgentService 填入
}
