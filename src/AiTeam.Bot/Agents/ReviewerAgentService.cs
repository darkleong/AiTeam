using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Octokit;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Reviewer Agent（Vera）：讀取 PR diff，以單一 Claude Code session 完成
/// 程式碼審查 + 影響範圍分析，透過 GitHub Review API 在 PR 上留下整體審查意見。
/// Stage 16 重構：從「LLM 逐檔呼叫 + 獨立 Claude Code 影響分析」改為
/// 單一 Claude Code session（RunReviewAsync），只帶 patch 不帶完整檔案內容，
/// 根本解決 LLM 混淆 diff 舊/新程式碼造成 Critical 誤判的問題。
/// Stage 23：加入 WorkflowSettings 注入（版本號檢查）。
/// </summary>
public class ReviewerAgentService(
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
    IClaudeCodeService claudeCodeService,
    AppSettingsService appSettings,
    IConfiguration configuration,
    IOptions<WorkflowSettings> workflowSettings,
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
        // Stage 17：MockMode early return — 跳過 CloneOrPull 與 GitHub API 呼叫，回傳模擬審查結果
        // Stage 26：修正狀態時序 — 先推 running，等待延遲後再設 done，確保 Dashboard 可觀察到 running 狀態
        if (await appSettings.GetBoolAsync("MockMode", false, cancellationToken))
        {
            // 強制失敗情境：回傳 Critical Issue，觸發 Review Appeal 流程
            if (MockClaudeCodeService.FailScenario == "review_appeal")
            {
                MockClaudeCodeService.FailScenario = "review_cody_appeal";
                logger.LogInformation("[MockMode/FailReview] Vera 回傳模擬 Critical Issue，觸發 Appeal 流程");
                AddLog(task, "[MOCK-FAIL] Vera 模擬審查中...", "running");
                await taskRepository.SaveAsync(cancellationToken);
                await PushStatus("running", task.Id, task.Title);
                await Task.Delay(Random.Shared.Next(10000, 20000), cancellationToken);
                AddLog(task, "[MOCK-FAIL] Vera 模擬審查完成，發現 1 個 Critical Issue", "revision");
                taskRepository.UpdateStatus(task, "revision");
                await taskRepository.SaveAsync(cancellationToken);
                const string failBody =
                    "## 🔍 PR #999 程式碼審查報告\n\n" +
                    "### 🔴 必須修改（Critical）\n" +
                    "- [#1] **`MockFile.cs`** (line ~42): [MOCK-FAIL] 缺少錯誤處理，可能導致未處理例外\n\n" +
                    "---\n\n**摘要**：[MOCK-FAIL] 發現 1 個 Critical 問題，請修正後重新提交。";
                return new AgentExecutionResult(true, "[MOCK-FAIL] Vera 審查發現 1 個必修問題",
                    ReviewBody: failBody, CriticalReviewCount: 1);
            }

            logger.LogInformation("[MockMode] ReviewerAgentService 跳過 GitHub 操作，回傳模擬結果");
            AddLog(task, "[MOCK] Vera 模擬審查中...", "running");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("running", task.Id, task.Title);
            await Task.Delay(Random.Shared.Next(30000, 60000), cancellationToken);
            AddLog(task, "[MOCK] Vera 模擬審查完成，0 個必修問題", "done");
            taskRepository.UpdateStatus(task, "done");
            await taskRepository.SaveAsync(cancellationToken);
            const string mockBody = "[MOCK] 程式碼審查通過，無 Critical 問題。這是模擬模式產生的審查報告。";
            return new AgentExecutionResult(true, "[MOCK] Vera 審查完成：0 個必修", ReviewBody: mockBody);
        }

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

            AddLog(task, $"PR #{prNumber} 共 {csFiles.Count} 個 .cs 檔，啟動 Claude Code Review session", "running");
            await taskRepository.SaveAsync(cancellationToken);

            // 3. 組建 prompt（只帶 patch，不帶完整檔案內容；Claude Code 自行 Read 需要的檔案）
            var prompt = BuildClaudeCodeReviewPrompt(csFiles, rules);

            // 4. 單一 Claude Code session：程式碼審查 + 影響範圍分析
            var ccResult = await RunClaudeCodeReviewAsync(owner, repo, headRef, prompt, task, cancellationToken);

            // 5. 解析 JSON（triple fallback）
            var report = TryParseReviewReport(ccResult.Output);
            if (report is null && !string.IsNullOrWhiteSpace(ccResult.RawJson))
                report = TryParseReviewReport(ccResult.RawJson);
            if (report is null)
            {
                logger.LogWarning("Vera Claude Code JSON 解析失敗（exitCode={Code}），視為無 Critical", ccResult.ExitCode);
                report = new ReviewReport();
            }

            // 6. 組建 issues 清單
            var allIssues = new List<ReviewIssue>();
            allIssues.AddRange(report.Critical.Select(i => i with { Severity = "critical" }));
            allIssues.AddRange(report.Warning .Select(i => i with { Severity = "warning"  }));
            allIssues.AddRange(report.Info    .Select(i => i with { Severity = "info"     }));

            var criticalCount = allIssues.Count(i => i.Severity == "critical");
            var warningCount  = allIssues.Count(i => i.Severity == "warning");
            var infoCount     = allIssues.Count(i => i.Severity == "info");
            var summary = $"PR #{prNumber} 審查完成：🔴 {criticalCount} 個必修 / 🟡 {warningCount} 個建議 / 🟢 {infoCount} 個優化";

            AddLog(task, summary, "done");
            await taskRepository.SaveAsync(cancellationToken);

            // 7. 組建 Review Body（含影響範圍分析）
            var reviewBody = BuildReviewBody(allIssues, report.Summary, report.Impact, prNumber);

            // 8. 在 GitHub PR 上提交 Review
            AddLog(task, "提交 GitHub Review 中...", "running");
            await taskRepository.SaveAsync(cancellationToken);

            var reviewUrl = await gitHubService.CreatePullRequestReviewAsync(owner, repo, prNumber, reviewBody);

            AddLog(task, $"Review 已提交：{reviewUrl}", "done");
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

    // ────────────── Claude Code Review Session ──────────────

    /// <summary>
    /// Clone repo、checkout PR branch、替換 CLAUDE.md、執行 RunReviewAsync、還原 CLAUDE.md。
    /// </summary>
    private async Task<ClaudeCodeResult> RunClaudeCodeReviewAsync(
        string owner,
        string repo,
        string headRef,
        string prompt,
        TaskItem task,
        CancellationToken cancellationToken)
    {
        var localPath = "";
        try
        {
            localPath = gitHubService.CloneOrPull(owner, repo, $"vera-{task.Id:N}"[..8]);
            // Checkout 到 PR branch，確保 Claude Code 看到最新的 PR 程式碼
            if (!string.IsNullOrWhiteSpace(headRef))
                gitHubService.CreateAndCheckoutBranch(localPath, headRef);

            var claudeMdPath = Path.Combine(localPath, "CLAUDE.md");
            var templatePath = Path.Combine(AppContext.BaseDirectory, "Resources", "CLAUDE_Vera.md");
            var backup = File.Exists(claudeMdPath)
                ? await File.ReadAllTextAsync(claudeMdPath, cancellationToken)
                : null;

            try
            {
                if (File.Exists(templatePath))
                    await File.WriteAllTextAsync(claudeMdPath,
                        await File.ReadAllTextAsync(templatePath, cancellationToken),
                        cancellationToken);
                else
                    logger.LogWarning("CLAUDE_Vera.md 不存在於 {Path}", templatePath);

                var model  = configuration["Agents:Reviewer:Model"]
                          ?? configuration["Anthropic:DefaultModel"]
                          ?? "claude-sonnet-4-6";
                var apiKey = configuration["Anthropic:ApiKey"] ?? "";

                return await claudeCodeService.RunReviewAsync(
                    localPath, prompt, model, apiKey, cancellationToken);
            }
            finally
            {
                // 不論成功或失敗，還原 CLAUDE.md
                if (backup is not null)
                    await File.WriteAllTextAsync(claudeMdPath, backup, CancellationToken.None);
                else if (File.Exists(claudeMdPath))
                    File.Delete(claudeMdPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RunClaudeCodeReviewAsync 失敗（headRef={Ref}）", headRef);
            // 失敗時回傳空結果，讓 triple fallback 產生空 ReviewReport（no Critical）
            return new ClaudeCodeResult(false, "", -1, "");
        }
        finally
        {
            if (!string.IsNullOrEmpty(localPath))
                gitHubService.CleanupLocalRepo(localPath);
        }
    }

    // ────────────── Prompt 建構 ──────────────

    /// <summary>
    /// 只帶 patch（diff），不帶完整檔案內容。
    /// Claude Code 在需要時會自行用 Read 工具讀取完整檔案。
    /// Stage 23：加入版本號檢查指示（若 WorkflowSettings.TargetVersion 有設定）。
    /// </summary>
    private string BuildClaudeCodeReviewPrompt(
        IReadOnlyList<PullRequestFile> csFiles,
        IReadOnlyList<string> rules)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## PR 變更（僅 .cs 檔案的 diff）");
        sb.AppendLine();
        foreach (var file in csFiles)
        {
            sb.AppendLine($"### {file.FileName}");
            sb.AppendLine("```diff");
            sb.AppendLine(file.Patch ?? "(no patch available)");
            sb.AppendLine("```");
            sb.AppendLine();
        }

        if (rules.Count > 0)
        {
            sb.AppendLine("## 專案規則");
            foreach (var r in rules)
                sb.AppendLine($"- {r}");
            sb.AppendLine();
        }

        sb.AppendLine("## 你的任務");
        sb.AppendLine("1. 審查上述 diff，依 CLAUDE.md 的規則產出分級報告");
        sb.AppendLine("2. 可用 Read / Grep 探索 codebase 確認上下文，可用 Bash 執行 git log / dotnet build 等診斷指令");
        sb.AppendLine("3. 探索影響範圍，找出可能受影響的其他模組");
        sb.AppendLine("4. 只輸出 JSON 結果（格式見 CLAUDE.md）");

        // 23-5：若設定了目標版本，追加版本號檢查指示
        var targetVersion = workflowSettings.Value.TargetVersion;
        if (!string.IsNullOrWhiteSpace(targetVersion))
        {
            sb.AppendLine();
            sb.AppendLine($"## 版本號檢查（目標版本：{targetVersion}）");
            sb.AppendLine($"若 PR 修改了任何 .csproj 檔案，確認其 <Version> 是否已更新至 {targetVersion}。");
            sb.AppendLine("未更新時列為 warning（不是 critical）。若 PR 未修改 .csproj 則略過。");
        }

        return sb.ToString();
    }

    // ────────────── Review Body 組建 ──────────────

    private static string BuildReviewBody(
        IReadOnlyList<ReviewIssue> issues,
        string summary,
        string impact,
        int prNumber)
    {
        var lines = new StringBuilder();

        if (issues.Count == 0)
        {
            lines.AppendLine($"## ✅ PR #{prNumber} 程式碼審查通過");
            lines.AppendLine();
            if (!string.IsNullOrWhiteSpace(summary))
                lines.AppendLine(summary);
        }
        else
        {
            lines.AppendLine($"## 🔍 PR #{prNumber} 程式碼審查報告");
            lines.AppendLine();

            var criticals = issues.Where(i => i.Severity == "critical").ToList();
            var warnings  = issues.Where(i => i.Severity == "warning" ).ToList();
            var infos     = issues.Where(i => i.Severity == "info"    ).ToList();

            if (criticals.Count > 0)
            {
                lines.AppendLine("### 🔴 必須修改（Critical）");
                foreach (var i in criticals)
                    lines.AppendLine($"- [#{i.Id}] **`{i.File}`** (line ~{i.Line}): {i.Message}");
                lines.AppendLine();
            }

            if (warnings.Count > 0)
            {
                lines.AppendLine("### 🟡 建議修改（Warning）");
                foreach (var i in warnings)
                    lines.AppendLine($"- [#{i.Id}] **`{i.File}`** (line ~{i.Line}): {i.Message}");
                lines.AppendLine();
            }

            if (infos.Count > 0)
            {
                lines.AppendLine("### 🟢 優化建議（Info）");
                foreach (var i in infos)
                    lines.AppendLine($"- [#{i.Id}] **`{i.File}`** (line ~{i.Line}): {i.Message}");
                lines.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                lines.AppendLine("### 📝 總結");
                lines.AppendLine(summary);
                lines.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(impact))
        {
            lines.AppendLine("---");
            lines.AppendLine();
            lines.AppendLine("## 🔭 影響範圍分析");
            lines.AppendLine();
            lines.AppendLine(impact);
            lines.AppendLine();
        }

        lines.AppendLine("---");
        lines.AppendLine("*由 Vera（Reviewer Agent）自動審查*");

        return lines.ToString();
    }

    // ────────────── 解析 ──────────────

    private ReviewReport? TryParseReviewReport(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
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
}

// ────────────── 資料模型 ──────────────

public class ReviewReport
{
    [JsonPropertyName("critical")] public List<ReviewIssue> Critical { get; set; } = [];
    [JsonPropertyName("warning")]  public List<ReviewIssue> Warning  { get; set; } = [];
    [JsonPropertyName("info")]     public List<ReviewIssue> Info     { get; set; } = [];
    [JsonPropertyName("summary")]  public string Summary             { get; set; } = "";
    [JsonPropertyName("impact")]   public string Impact              { get; set; } = "";
}

public record ReviewIssue
{
    [JsonPropertyName("id")]       public int    Id       { get; init; }   // 23-4：全局唯一編號（1 起遞增）
    [JsonPropertyName("file")]     public string File     { get; init; } = "";
    [JsonPropertyName("line")]     public int    Line     { get; init; }
    [JsonPropertyName("message")]  public string Message  { get; init; } = "";
    public string Severity { get; init; } = "info"; // 由 ReviewerAgentService 填入
}
