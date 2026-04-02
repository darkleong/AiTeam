using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.ViewModels;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Release Agent（Rena）：彙整 commits 與 merged PRs，
/// 產出 Changelog 與 Release Notes，建立 GitHub Release tag，更新 CHANGELOG.md。
/// </summary>
public class ReleaseAgentService(
    LlmProviderFactory providerFactory,
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
    ILogger<ReleaseAgentService> logger) : IAgentExecutor
{
    private const string AgentName = "Release";

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
        AddLog(task, "Release Agent 開始執行", "running");
        await taskRepository.SaveAsync(cancellationToken);
        await PushStatus("running", task.Title);

        try
        {
            // 1. 取得最新 tag（作為「上一版」基準）
            var latestTag = await gitHubService.GetLatestTagAsync(owner, repo);
            AddLog(task, $"目前最新版本：{latestTag ?? "（無）"}", "running");
            await taskRepository.SaveAsync(cancellationToken);

            // 2. 取得自上一個 tag 以來的 commits
            var commits = await gitHubService.GetCommitsSinceAsync(owner, repo,
                latestTag is null ? null : await GetTagShaAsync(owner, repo, latestTag));

            // 3. 取得自上一個 Release 以來合併的 PRs
            var sinceDate = latestTag is null
                ? DateTimeOffset.UtcNow.AddDays(-30)
                : (await GetTagDateAsync(owner, repo, latestTag));

            var mergedPrs = await gitHubService.GetMergedPullRequestsAsync(owner, repo, sinceDate);

            if (commits.Count == 0 && mergedPrs.Count == 0)
                return Fail(task, "自上次發版以來沒有新的 commits 或合併的 PR，無需建立新版本。");

            AddLog(task, $"收集到 {commits.Count} 個 commits、{mergedPrs.Count} 個 merged PRs", "running");
            await taskRepository.SaveAsync(cancellationToken);

            // 4. 呼叫 LLM 產出版本號建議與 Release Notes
            var provider = providerFactory.Create(AgentName);
            var systemPrompt = BuildReleaseSystemPrompt();
            var userMessage  = BuildReleaseUserMessage(latestTag, commits, mergedPrs);

            var response     = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken);
            var releaseNotes = TryParseReleaseNotes(response.Content);

            if (releaseNotes is null)
                return Fail(task, "LLM 回應格式錯誤，無法解析版本資訊。");

            // 版本號防護：若 LLM 未填或格式錯誤，自動計算
            var version = NormalizeVersion(releaseNotes.Version, latestTag, releaseNotes.BumpType);

            AddLog(task, $"建議版本：{version}（{releaseNotes.BumpType}）", "running");
            await taskRepository.SaveAsync(cancellationToken);

            // 5. 更新 CHANGELOG.md
            var changelogEntry = BuildChangelogEntry(version, releaseNotes.Changelog);
            await AppendToChangelogAsync(owner, repo, version, changelogEntry);

            // 6. 建立 GitHub Release
            var releaseUrl = await gitHubService.CreateReleaseAsync(
                owner, repo,
                tagName:     version,
                releaseName: $"Release {version}",
                body:        releaseNotes.ReleaseNotesMd);

            var summary = $"{version} Release 已建立（{mergedPrs.Count} 個 PR / {commits.Count} 個 commit）";

            AddLog(task, summary, "done");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("done", task.Title);

            return new AgentExecutionResult(true, summary, releaseUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Release Agent 執行失敗（TaskId={Id}）", task.Id);
            AddLog(task, $"執行失敗：{ex.Message}", "failed");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("failed", task.Title);
            return Fail(task, ex.Message);
        }
    }

    // ────────────── Prompt 建構 ──────────────

    private static string BuildReleaseSystemPrompt() => """
        你是發版管理專家 Rena，負責整理版本變更並產出標準格式的 Release Notes。

        ## Semantic Versioning 規則
        - major（1.0.0 → 2.0.0）：重大變更、不相容的 API 修改
        - minor（1.0.0 → 1.1.0）：新增功能，向下相容
        - patch（1.0.0 → 1.0.1）：Bug 修復、維護性修改、文件更新

        ## 回應格式（JSON，不得包含任何其他文字）
        {
          "version": "v1.2.0",
          "bump_type": "minor",
          "changelog": "Markdown 格式的 Changelog（分類：新功能 / Bug 修復 / 重構 / 文件）",
          "release_notes_md": "面向使用者的 Release Notes（Markdown），說明重點功能"
        }

        - version 必須以 v 開頭，格式 vX.Y.Z
        - 使用繁體中文撰寫
        """;

    private static string BuildReleaseUserMessage(
        string? latestTag,
        IReadOnlyList<Octokit.GitHubCommit> commits,
        IReadOnlyList<Octokit.PullRequest> mergedPrs)
    {
        var commitLines = commits.Count > 0
            ? string.Join("\n", commits.Take(50).Select(c =>
                $"- [{c.Sha[..7]}] {c.Commit.Message.Split('\n').FirstOrDefault()}"))
            : "（無新 commit）";

        var prLines = mergedPrs.Count > 0
            ? string.Join("\n", mergedPrs.Take(30).Select(pr =>
                $"- PR #{pr.Number}: {pr.Title}（by {pr.User.Login}）"))
            : "（無合併 PR）";

        return $"""
            ## 上一個版本
            {latestTag ?? "（首次發版）"}

            ## 自上次發版以來的 Commits
            {commitLines}

            ## 已合併的 Pull Requests
            {prLines}

            請依據以上變更，產出版本號建議與 Release Notes。
            """;
    }

    // ────────────── 輔助 GitHub 操作 ──────────────

    private async Task<string?> GetTagShaAsync(string owner, string repo, string tag)
    {
        try
        {
            // 使用 Commits API 取得 tag 對應的 commit SHA
            var commits = await gitHubService.GetCommitsSinceAsync(owner, repo, null);
            // 簡化做法：回傳 null 讓 GetCommitsSinceAsync 取最近 50 筆
            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<DateTimeOffset> GetTagDateAsync(string owner, string repo, string tag)
    {
        // 取得最近 commits 中的最舊一筆日期作為近似值
        var commits = await gitHubService.GetCommitsSinceAsync(owner, repo, null);
        var oldest  = commits.LastOrDefault()?.Commit?.Committer?.Date;
        return oldest ?? DateTimeOffset.UtcNow.AddDays(-30);
    }

    private async Task AppendToChangelogAsync(string owner, string repo, string version, string entry)
    {
        try
        {
            // 嘗試讀取現有 CHANGELOG.md
            string existingContent;
            try
            {
                existingContent = await gitHubService.GetFileContentAsync(owner, repo, "CHANGELOG.md");
            }
            catch
            {
                existingContent = "# Changelog\n\n";
            }

            // 新增到頂部（最新版在最前面）
            var newContent = $"{entry}\n\n{existingContent}";
            await gitHubService.CreateOrUpdateFileAsync(
                owner, repo, "CHANGELOG.md", newContent,
                $"docs: update CHANGELOG for {version}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "更新 CHANGELOG.md 失敗，略過");
        }
    }

    private static string BuildChangelogEntry(string version, string changelog)
        => $"## {version} — {DateTime.UtcNow:yyyy-MM-dd}\n\n{changelog}";

    // ────────────── 版本號工具 ──────────────

    private static string NormalizeVersion(string llmVersion, string? latestTag, string bumpType)
    {
        // 若 LLM 給的版本號格式正確就直接用
        if (Regex.IsMatch(llmVersion, @"^v\d+\.\d+\.\d+$"))
            return llmVersion;

        // 否則從 latestTag 計算
        return SuggestNextVersion(latestTag ?? "v0.0.0", bumpType);
    }

    private static string SuggestNextVersion(string currentTag, string bumpType)
    {
        var match = Regex.Match(currentTag, @"v?(\d+)\.(\d+)\.(\d+)");
        if (!match.Success) return "v0.1.0";

        var major = int.Parse(match.Groups[1].Value);
        var minor = int.Parse(match.Groups[2].Value);
        var patch = int.Parse(match.Groups[3].Value);

        return bumpType switch
        {
            "major" => $"v{major + 1}.0.0",
            "minor" => $"v{major}.{minor + 1}.0",
            _       => $"v{major}.{minor}.{patch + 1}"
        };
    }

    // ────────────── 解析 ──────────────

    private ReleaseNotesDto? TryParseReleaseNotes(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;

            return JsonSerializer.Deserialize<ReleaseNotesDto>(content[start..(end + 1)], JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ReleaseNotes 解析失敗");
            return null;
        }
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

    private async Task PushStatus(string status, string title)
        => await dashboardPush.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = Guid.Empty,
            Title     = title,
            AgentName = AgentName,
            Status    = status
        });

    private static AgentExecutionResult Fail(TaskItem task, string message)
        => new(false, message);
}

// ────────────── 資料模型 ──────────────

internal class ReleaseNotesDto
{
    [JsonPropertyName("version")]          public string Version         { get; set; } = "";
    [JsonPropertyName("bump_type")]        public string BumpType        { get; set; } = "patch";
    [JsonPropertyName("changelog")]        public string Changelog       { get; set; } = "";
    [JsonPropertyName("release_notes_md")] public string ReleaseNotesMd { get; set; } = "";
}
