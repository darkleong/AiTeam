using System.Text;
using System.Text.RegularExpressions;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.ViewModels;
using Microsoft.Extensions.Configuration;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Documentation Agent（Sage）：收尾歸檔員。
/// Stage 23 重構：不再讀取 .cs 原始碼，改用 Cody 實作說明 + Vera 審查摘要做歸檔。
/// 建立 docs/archive/pr{N}-archive.md + 更新 CHANGELOG.md。
/// </summary>
public class DocAgentService(
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
    IClaudeCodeService claudeCodeService,
    AppSettingsService appSettings,
    IConfiguration configuration,
    ILogger<DocAgentService> logger) : IAgentExecutor
{
    private const string AgentName = "Doc";

    /// <inheritdoc />
    public async Task<AgentExecutionResult> ExecuteTaskAsync(
        TaskItem task,
        string owner,
        string repo,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default)
    {
        // Stage 17：MockMode early return
        // Stage 26：修正狀態時序 — 先推 running，等待延遲後再設 done，確保 Dashboard 可觀察到 running 狀態
        if (await appSettings.GetBoolAsync("MockMode", false, cancellationToken))
        {
            logger.LogInformation("[MockMode] DocAgentService 跳過 GitHub 操作，回傳模擬結果");
            AddLog(task, "[MOCK] Sage 模擬歸檔中...", "running");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("running", task.Title);
            await Task.Delay(Random.Shared.Next(30000, 60000), cancellationToken);
            AddLog(task, "[MOCK] Sage 模擬歸檔完成", "done");
            taskRepository.UpdateStatus(task, "done");
            // Stage 29-1：MockMode 也存入 ArchiveContent，供 Dashboard 折疊面板驗收
            if (task.GroupId is not null)
            {
                var mockGroup = await taskRepository.GetGroupByIdAsync(task.GroupId.Value, cancellationToken);
                if (mockGroup is not null)
                    mockGroup.ArchiveContent = "[MOCK] 模擬歸檔報告（Sage）\n\n此為 MockMode 產生的測試內容。";
            }
            await taskRepository.SaveAsync(cancellationToken);
            return new AgentExecutionResult(true, "[MOCK] 歸檔完成（CHANGELOG + archive）");
        }

        AddLog(task, "Doc Agent 開始執行", "running");
        await taskRepository.SaveAsync(cancellationToken);
        await PushStatus("running", task.Title);

        var writePath = "";

        try
        {
            // 從任務描述解析 PR 編號
            var prNumber = ExtractPrNumber($"{task.Title} {task.Description}");
            if (prNumber <= 0)
                prNumber = await gitHubService.GetLatestOpenPullRequestNumberAsync(owner, repo);

            if (prNumber <= 0)
                return new AgentExecutionResult(true, "找不到 PR 編號，略過歸檔");

            var headRef = await gitHubService.GetPullRequestHeadRefAsync(owner, repo, prNumber);

            // 從 DB 讀取 Cody 實作說明、Vera 審查摘要與 Quinn 測試報告（透過 GroupId）
            var (implementationNote, lastReviewBody, testReport) = await GetGroupContextAsync(task, cancellationToken);

            // Clone PR branch（write mode，Sage 會直接寫入檔案）
            writePath = gitHubService.CloneOrPull(owner, repo, $"saged-{task.Id:N}"[..8]);
            gitHubService.CreateAndCheckoutBranch(writePath, headRef);
            AddLog(task, $"PR #{prNumber} branch checkout 完成，開始歸檔", "running");
            await taskRepository.SaveAsync(cancellationToken);

            // 執行 Claude Code（write mode）讓 Sage 自行寫入 CHANGELOG + archive
            var success = await RunClaudeCodeArchiveAsync(
                task, writePath, prNumber, headRef, implementationNote, lastReviewBody, testReport, cancellationToken);

            if (!success)
                return new AgentExecutionResult(true, $"PR #{prNumber} 歸檔無輸出，略過提交");

            // Stage 29-1：歸檔完成後存入 TaskGroup.ArchiveContent（供 Dashboard 折疊面板顯示）
            if (task.GroupId is not null)
            {
                var archivePath = Path.Combine(writePath, "docs", "archive", $"pr{prNumber}-archive.md");
                if (File.Exists(archivePath))
                {
                    var group = await taskRepository.GetGroupByIdAsync(task.GroupId.Value, cancellationToken);
                    if (group is not null)
                    {
                        group.ArchiveContent = await File.ReadAllTextAsync(archivePath, cancellationToken);
                        await taskRepository.SaveAsync(cancellationToken);
                    }
                }
            }

            gitHubService.CommitAll(writePath, $"docs: Sage 歸檔 PR #{prNumber} 任務");
            gitHubService.Push(writePath, headRef);

            AddLog(task, $"歸檔已推送到 branch {headRef}（PR #{prNumber}）", "done");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("idle");

            return new AgentExecutionResult(true,
                $"歸檔完成（PR #{prNumber}）：CHANGELOG + docs/archive/pr{prNumber}-archive.md");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Doc Agent 執行失敗：{Title}", task.Title);
            AddLog(task, $"執行失敗：{ex.Message}", "failed");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("error");
            return new AgentExecutionResult(false, $"Doc Agent 執行失敗：{ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(writePath)) gitHubService.CleanupLocalRepo(writePath);
        }
    }

    // ────────────── Claude Code 歸檔執行 ──────────────

    private async Task<bool> RunClaudeCodeArchiveAsync(
        TaskItem task,
        string repoLocalPath,
        int prNumber,
        string headRef,
        string? implementationNote,
        string? lastReviewBody,
        string? testReport,
        CancellationToken cancellationToken)
    {
        var claudeMdPath     = Path.Combine(repoLocalPath, "CLAUDE.md");
        var templatePath     = Path.Combine(AppContext.BaseDirectory, "Resources", "CLAUDE_Sage.md");
        var originalClaudeMd = File.Exists(claudeMdPath)
            ? await File.ReadAllTextAsync(claudeMdPath, cancellationToken)
            : null;

        try
        {
            if (File.Exists(templatePath))
                await File.WriteAllTextAsync(claudeMdPath,
                    await File.ReadAllTextAsync(templatePath, cancellationToken), cancellationToken);

            var sb = new StringBuilder();
            sb.AppendLine($"## PR #{prNumber}（branch: {headRef}）");
            sb.AppendLine($"**任務標題**：{task.Title.Replace($"（{AgentName}）", "").Trim()}");
            sb.AppendLine($"**PR 連結**：https://github.com/{headRef.Replace("refs/heads/", "")}（PR #{prNumber}）");
            sb.AppendLine($"**日期**：{DateTime.UtcNow:yyyy-MM-dd}");
            sb.AppendLine();

            sb.AppendLine("## implementation_note（Cody 實作說明）");
            sb.AppendLine(string.IsNullOrWhiteSpace(implementationNote)
                ? "（無實作說明）"
                : implementationNote);
            sb.AppendLine();

            sb.AppendLine("## vera_review_summary（Vera 審查摘要）");
            // 只帶審查報告摘要（截斷至 2000 字避免過長）
            var reviewSummary = string.IsNullOrWhiteSpace(lastReviewBody)
                ? "（無審查摘要）"
                : lastReviewBody.Length > 2000 ? lastReviewBody[..2000] + "\n...（截斷）" : lastReviewBody;
            sb.AppendLine(reviewSummary);
            sb.AppendLine();

            // Stage 24：附上 Quinn 的測試報告供 Sage 歸檔參考
            if (!string.IsNullOrWhiteSpace(testReport))
            {
                sb.AppendLine("## test_report（Quinn 測試報告）");
                sb.AppendLine(testReport);
                sb.AppendLine();
            }

            sb.AppendLine("## 你的任務");
            sb.AppendLine($"1. 更新 CHANGELOG.md（在最頂部插入新條目，保留所有舊內容）");
            sb.AppendLine($"2. 建立 docs/archive/pr{prNumber}-archive.md（若目錄不存在先建立）");
            sb.AppendLine("直接寫入檔案，不需要輸出到 stdout。");

            var model  = configuration["Agents:Doc:Model"]
                      ?? configuration["Anthropic:DefaultModel"]
                      ?? "claude-sonnet-4-6";
            var apiKey = configuration["Anthropic:ApiKey"] ?? "";

            var result = await claudeCodeService.RunAsync(
                repoLocalPath, sb.ToString(), model, apiKey, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning("Sage Claude Code 執行未成功（exitCode={Code}）", result.ExitCode);
                return false;
            }

            // 確認目標檔案是否存在
            var archivePath = Path.Combine(repoLocalPath, "docs", "archive", $"pr{prNumber}-archive.md");
            if (!File.Exists(archivePath))
            {
                logger.LogWarning("Sage 未建立 archive 檔案，嘗試手動建立：{Path}", archivePath);
                return false;
            }

            return true;
        }
        finally
        {
            if (originalClaudeMd is not null)
                await File.WriteAllTextAsync(claudeMdPath, originalClaudeMd, CancellationToken.None);
            else if (File.Exists(claudeMdPath))
                File.Delete(claudeMdPath);
        }
    }

    // ────────────── 從 DB 讀取 TaskGroup 上下文 ──────────────

    private async Task<(string? ImplementationNote, string? LastReviewBody, string? TestReport)> GetGroupContextAsync(
        TaskItem task, CancellationToken cancellationToken)
    {
        if (task.GroupId is null) return (null, null, null);
        try
        {
            var group = await taskRepository.GetGroupByIdAsync(task.GroupId.Value, cancellationToken);
            return (group?.ImplementationNote, group?.LastReviewBody, group?.TestReport);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetGroupContextAsync 讀取失敗（GroupId={Id}）", task.GroupId);
            return (null, null, null);
        }
    }

    // ────────────── 輔助 ──────────────

    private static int ExtractPrNumber(string text)
    {
        var match = Regex.Match(text, @"PR\s*#(\d+)|/pull/(\d+)", RegexOptions.IgnoreCase);
        if (!match.Success) return 0;
        var val = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return int.TryParse(val, out var n) ? n : 0;
    }

    private void AddLog(TaskItem task, string step, string status)
        => taskRepository.AddLog(new TaskLog
        {
            TaskId = task.Id,
            Agent  = AgentName,
            Step   = step,
            Status = status
        });

    private async Task PushStatus(string status, string? taskTitle = null)
        => await dashboardPush.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = AgentName,
            Status           = status,
            CurrentTaskTitle = taskTitle ?? "",
            LastUpdated      = DateTime.UtcNow
        });
}
