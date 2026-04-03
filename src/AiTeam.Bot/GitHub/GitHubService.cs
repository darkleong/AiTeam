using AiTeam.Bot.Configuration;
using LibGit2Sharp;
using Microsoft.Extensions.Options;
using Octokit;

namespace AiTeam.Bot.GitHub;

/// <summary>
/// GitHub API 與 Git 操作的底層服務。
/// Dev Agent 透過此服務操作 repo，不直接接觸 Octokit / LibGit2Sharp。
/// </summary>
public class GitHubService(
    IOptions<GitHubSettings> settings,
    ILogger<GitHubService> logger)
{
    private readonly GitHubSettings _settings = settings.Value;

    private GitHubClient CreateClient() => new(new ProductHeaderValue("AiTeamBot"))
    {
        Credentials = new Octokit.Credentials(_settings.PersonalAccessToken)
    };

    // ────────────── CEO 智慧分類用（查詢 PR / Issue 上下文）──────────────

    /// <summary>
    /// 取得目前 open 的 PR 清單（最多 20 筆），供 CEO 判斷是否已有相關 PR。
    /// </summary>
    public async Task<IReadOnlyList<Octokit.PullRequest>> ListOpenPullRequestsAsync(string owner, string repo)
    {
        var client = CreateClient();
        try
        {
            return await client.PullRequest.GetAllForRepository(owner, repo,
                new PullRequestRequest { State = ItemStateFilter.Open },
                new ApiOptions { PageSize = 20, PageCount = 1 });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "取得 open PR 失敗：{Owner}/{Repo}", owner, repo);
            return [];
        }
    }

    /// <summary>
    /// 取得目前 open 的 Issue 清單（最多 20 筆），供 CEO 判斷問題是否已被追蹤。
    /// </summary>
    public async Task<IReadOnlyList<Octokit.Issue>> ListOpenIssuesAsync(string owner, string repo)
    {
        var client = CreateClient();
        try
        {
            return await client.Issue.GetAllForRepository(owner, repo,
                new RepositoryIssueRequest { State = ItemStateFilter.Open },
                new ApiOptions { PageSize = 20, PageCount = 1 });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "取得 open Issue 失敗：{Owner}/{Repo}", owner, repo);
            return [];
        }
    }

    // ────────────── 唯讀操作（Code Review 用）──────────────

    /// <summary>
    /// 取得 repo 的檔案內容（Base64 解碼）。可指定 gitRef（branch / commit SHA），預設讀 default branch。
    /// </summary>
    public async Task<string> GetFileContentAsync(string owner, string repo, string path, string? gitRef = null)
    {
        var client = CreateClient();
        var content = gitRef is null
            ? await client.Repository.Content.GetAllContents(owner, repo, path)
            : await client.Repository.Content.GetAllContentsByRef(owner, repo, path, gitRef);
        return content.FirstOrDefault()?.Content ?? "";
    }

    /// <summary>
    /// 取得 PR 的 head branch 名稱（用於從 head branch 讀取檔案內容）。
    /// </summary>
    public async Task<string> GetPullRequestHeadRefAsync(string owner, string repo, int prNumber)
    {
        var client = CreateClient();
        var pr = await client.PullRequest.Get(owner, repo, prNumber);
        return pr.Head.Ref;
    }

    /// <summary>
    /// 遞迴列出 repo 指定路徑下的所有檔案（含子目錄）。
    /// </summary>
    public async Task<IReadOnlyList<RepositoryContent>> ListFilesAsync(
        string owner, string repo, string path = "")
    {
        // GitHub API 不接受尾巴斜線
        path = path.TrimEnd('/');

        var client = CreateClient();
        var result = new List<RepositoryContent>();
        await CollectFilesAsync(client, owner, repo, path, result);
        return result;
    }

    private static async Task CollectFilesAsync(
        GitHubClient client, string owner, string repo, string path, List<RepositoryContent> result)
    {
        IReadOnlyList<RepositoryContent> items;
        try
        {
            items = string.IsNullOrEmpty(path)
                ? await client.Repository.Content.GetAllContents(owner, repo)
                : await client.Repository.Content.GetAllContents(owner, repo, path);
        }
        catch (Octokit.NotFoundException)
        {
            return; // 路徑不存在時靜默跳過
        }

        foreach (var item in items)
        {
            if (item.Type == ContentType.File)
                result.Add(item);
            else if (item.Type == ContentType.Dir)
                await CollectFilesAsync(client, owner, repo, item.Path, result);
        }
    }

    /// <summary>
    /// 取得 PR 的程式碼變更（diff 檔案清單）。
    /// </summary>
    public async Task<IReadOnlyList<PullRequestFile>> GetPullRequestFilesAsync(
        string owner, string repo, int prNumber)
    {
        var client = CreateClient();
        return await client.PullRequest.Files(owner, repo, prNumber);
    }

    // ────────────── 寫入操作（Clone / Commit / PR）──────────────

    /// <summary>
    /// Clone repo 到本地工作目錄，若已存在則 pull 最新。
    /// 回傳本地路徑。
    /// </summary>
    /// <param name="uniqueSuffix">可選，用於區隔並行 Agent 的工作目錄（建議傳入 taskId 的前 8 碼）。</param>
    public string CloneOrPull(string owner, string repo, string? uniqueSuffix = null)
    {
        var dirName  = string.IsNullOrWhiteSpace(uniqueSuffix) ? repo : $"{repo}_{uniqueSuffix}";
        var localPath = Path.Combine(_settings.WorkspacePath, dirName);
        Directory.CreateDirectory(_settings.WorkspacePath);

        // 防護：目錄存在但沒有 .git（上次清理不完整），強制刪除後重新 clone
        if (Directory.Exists(localPath) && !Directory.Exists(Path.Combine(localPath, ".git")))
        {
            logger.LogWarning("發現殘留 workspace（無 .git），強制清理：{Path}", localPath);
            SetNormalAttributes(localPath);
            Directory.Delete(localPath, recursive: true);
        }

        if (Directory.Exists(Path.Combine(localPath, ".git")))
        {
            logger.LogInformation("Repo {Repo} 已存在，執行 Pull", repo);
            using var gitRepo = new LibGit2Sharp.Repository(localPath);
            var remote = gitRepo.Network.Remotes["origin"];
            var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification);
            Commands.Fetch(gitRepo, remote.Name, refSpecs, new FetchOptions
            {
                CredentialsProvider = (_, _, _) =>
                    new UsernamePasswordCredentials
                    {
                        Username = _settings.PersonalAccessToken,
                        Password = ""
                    }
            }, null);
        }
        else
        {
            logger.LogInformation("Clone repo {Owner}/{Repo} 到 {Path}", owner, repo, localPath);
            LibGit2Sharp.Repository.Clone(
                $"https://{_settings.PersonalAccessToken}@github.com/{owner}/{repo}.git",
                localPath,
                new CloneOptions
                {
                    FetchOptions =
                    {
                        CredentialsProvider = (_, _, _) =>
                            new UsernamePasswordCredentials
                            {
                                Username = _settings.PersonalAccessToken,
                                Password = ""
                            }
                    }
                });
        }

        return localPath;
    }

    /// <summary>
    /// 切換到指定 branch。若 remote 已有同名 branch（fix loop 場景），
    /// 先 checkout remote branch 追蹤；否則從 HEAD 建立新 branch。
    /// </summary>
    public void CreateAndCheckoutBranch(string localPath, string branchName)
    {
        using var gitRepo = new LibGit2Sharp.Repository(localPath);

        // 先嘗試 checkout 已存在的 remote branch（fix loop 修復既有 PR 用）
        var remoteBranch = gitRepo.Branches[$"origin/{branchName}"];
        if (remoteBranch != null)
        {
            var localBranch = gitRepo.Branches[branchName]
                ?? gitRepo.CreateBranch(branchName, remoteBranch.Tip);
            gitRepo.Branches.Update(localBranch,
                b => b.TrackedBranch = remoteBranch.CanonicalName);
            Commands.Checkout(gitRepo, localBranch);
            logger.LogInformation("已切換到既有 remote branch {Branch}", branchName);
        }
        else
        {
            var branch = gitRepo.CreateBranch(branchName);
            Commands.Checkout(gitRepo, branch);
            logger.LogInformation("已建立並切換到新 branch {Branch}", branchName);
        }
    }

    /// <summary>
    /// Commit 所有變更到本地 branch。
    /// </summary>
    public void CommitAll(string localPath, string message)
    {
        using var gitRepo = new LibGit2Sharp.Repository(localPath);
        Commands.Stage(gitRepo, "*");

        var signature = new LibGit2Sharp.Signature("AiTeamBot", "aiteambot@noreply.github.com", DateTimeOffset.UtcNow);
        gitRepo.Commit(message, signature, signature);
        logger.LogInformation("Commit 完成：{Message}", message);
    }

    /// <summary>
    /// Push 本地 branch 到遠端。
    /// </summary>
    public void Push(string localPath, string branchName)
    {
        using var gitRepo = new LibGit2Sharp.Repository(localPath);
        var remote = gitRepo.Network.Remotes["origin"];

        gitRepo.Network.Push(remote, $"refs/heads/{branchName}:refs/heads/{branchName}",
            new PushOptions
            {
                CredentialsProvider = (_, _, _) =>
                    new UsernamePasswordCredentials
                    {
                        Username = _settings.PersonalAccessToken,
                        Password = ""
                    }
            });
        logger.LogInformation("Push 完成：{Branch}", branchName);
    }

    /// <summary>
    /// 開啟 Pull Request，回傳 PR URL。
    /// </summary>
    public async Task<string> OpenPullRequestAsync(
        string owner, string repo,
        string title, string body,
        string head, string baseBranch = "main")
    {
        var client = CreateClient();
        var pr = await client.PullRequest.Create(owner, repo, new NewPullRequest(title, head, baseBranch)
        {
            Body = body
        });
        logger.LogInformation("PR #{Number} 已開啟：{Url}", pr.Number, pr.HtmlUrl);
        return pr.HtmlUrl;
    }

    /// <summary>
    /// 建立 GitHub Issue，回傳 Issue URL。
    /// </summary>
    public async Task<string> CreateIssueAsync(
        string owner, string repo,
        string title, string body,
        IEnumerable<string> labels)
    {
        var client = CreateClient();
        var newIssue = new NewIssue(title) { Body = body };
        foreach (var label in labels)
            newIssue.Labels.Add(label);

        var issue = await client.Issue.Create(owner, repo, newIssue);
        logger.LogInformation("Issue #{Number} 已建立：{Url}", issue.Number, issue.HtmlUrl);
        return issue.HtmlUrl;
    }

    /// <summary>
    /// 清除本地 clone（任務完成後釋放磁碟空間）。
    /// </summary>
    public void CleanupLocalRepo(string localPath)
    {
        if (!Directory.Exists(localPath)) return;
        // 清除 Git 唯讀屬性再刪除
        SetNormalAttributes(localPath);
        Directory.Delete(localPath, recursive: true);
        logger.LogInformation("本地 repo 已清除：{Path}", localPath);
    }

    private static void SetNormalAttributes(string path)
    {
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
    }

    // ────────────── Reviewer Agent 用 ──────────────

    /// <summary>
    /// 在 PR 上提交整體 Review（COMMENT 事件，不影響合併狀態）。
    /// 回傳 Review HTML URL。
    /// </summary>
    public async Task<string> CreatePullRequestReviewAsync(
        string owner, string repo, int prNumber,
        string body)
    {
        var client = CreateClient();
        var review = await client.PullRequest.Review.Create(owner, repo, prNumber,
            new PullRequestReviewCreate
            {
                Body  = body,
                Event = Octokit.PullRequestReviewEvent.Comment
            });
        logger.LogInformation("PR #{Pr} Review 已提交（ReviewId={Id}）", prNumber, review.Id);
        return review.HtmlUrl;
    }

    // ────────────── Release Agent 用 ──────────────

    /// <summary>
    /// 取得最新的 Release tag 名稱，若無任何 Release 則回傳 null。
    /// </summary>
    public async Task<string?> GetLatestTagAsync(string owner, string repo)
    {
        var client = CreateClient();
        try
        {
            var latest = await client.Repository.Release.GetLatest(owner, repo);
            return latest.TagName;
        }
        catch (Octokit.NotFoundException)
        {
            return null; // 尚無任何 Release
        }
    }

    /// <summary>
    /// 取得指定 SHA 之後（不含）到 HEAD 的 commits。
    /// 若 sinceSha 為 null，則取最近 50 筆 commits。
    /// </summary>
    public async Task<IReadOnlyList<GitHubCommit>> GetCommitsSinceAsync(
        string owner, string repo, string? sinceSha)
    {
        var client = CreateClient();

        if (sinceSha is null)
        {
            var commits = await client.Repository.Commit.GetAll(owner, repo,
                new CommitRequest(), new ApiOptions { PageSize = 50, PageCount = 1 });
            return commits;
        }

        // 取得 sinceSha 的日期，以此為起始點查詢
        var refCommit  = await client.Repository.Commit.Get(owner, repo, sinceSha);
        var sinceDate  = refCommit.Commit.Committer.Date;

        var allCommits = await client.Repository.Commit.GetAll(owner, repo,
            new CommitRequest { Since = sinceDate.AddSeconds(1) },
            new ApiOptions { PageSize = 100, PageCount = 1 });

        return allCommits;
    }

    /// <summary>
    /// 取得指定日期之後已合併的 PRs。
    /// </summary>
    public async Task<IReadOnlyList<PullRequest>> GetMergedPullRequestsAsync(
        string owner, string repo, DateTimeOffset since)
    {
        var client = CreateClient();
        var prs = await client.PullRequest.GetAllForRepository(owner, repo,
            new PullRequestRequest
            {
                State     = ItemStateFilter.Closed,
                SortDirection = SortDirection.Descending
            },
            new ApiOptions { PageSize = 50, PageCount = 1 });

        return prs
            .Where(pr => pr.Merged && pr.MergedAt >= since)
            .ToList();
    }

    /// <summary>
    /// 取得最新一個 open PR 的編號，若無則回傳 0。
    /// </summary>
    public async Task<int> GetLatestOpenPullRequestNumberAsync(string owner, string repo)
    {
        var client = CreateClient();
        var prs = await client.PullRequest.GetAllForRepository(owner, repo,
            new PullRequestRequest
            {
                State         = ItemStateFilter.Open,
                SortProperty  = PullRequestSort.Created,
                SortDirection = SortDirection.Descending
            },
            new ApiOptions { PageSize = 1, PageCount = 1 });

        return prs.Count > 0 ? prs[0].Number : 0;
    }

    /// <summary>
    /// 在 GitHub 建立 Release，回傳 Release HTML URL。
    /// </summary>
    public async Task<string> CreateReleaseAsync(
        string owner, string repo,
        string tagName, string releaseName, string body)
    {
        var client  = CreateClient();
        var release = await client.Repository.Release.Create(owner, repo,
            new NewRelease(tagName)
            {
                Name  = releaseName,
                Body  = body,
                Draft = false,
            });
        logger.LogInformation("Release {Tag} 已建立：{Url}", tagName, release.HtmlUrl);
        return release.HtmlUrl;
    }

    /// <summary>
    /// 建立或更新 repo 中的檔案（例如 CHANGELOG.md）。
    /// 若檔案已存在，需傳入現有 SHA（透過 GetAllContents 取得），否則 GitHub API 會拒絕。
    /// </summary>
    public async Task CreateOrUpdateFileAsync(
        string owner, string repo,
        string path, string content, string commitMessage)
    {
        var client = CreateClient();

        // 嘗試取得現有檔案 SHA
        string? existingSha = null;
        try
        {
            var existing = await client.Repository.Content.GetAllContents(owner, repo, path);
            existingSha  = existing.FirstOrDefault()?.Sha;
        }
        catch (Octokit.NotFoundException)
        {
            // 檔案不存在，建立新檔案
        }

        // 直接傳入原始字串，由 Octokit 負責 base64 編碼（convertContentToBase64 預設 true）
        if (existingSha is null)
        {
            await client.Repository.Content.CreateFile(owner, repo, path,
                new CreateFileRequest(commitMessage, content));
            logger.LogInformation("檔案已建立：{Path}", path);
        }
        else
        {
            await client.Repository.Content.UpdateFile(owner, repo, path,
                new UpdateFileRequest(commitMessage, content, existingSha));
            logger.LogInformation("檔案已更新：{Path}", path);
        }
    }

    /// <summary>
    /// 刪除 GitHub 上的檔案（取消提案時清理孤立的 UI 規格文件用）。
    /// 若檔案不存在則靜默略過。
    /// </summary>
    public async Task DeleteFileAsync(string owner, string repo, string path, string commitMessage)
    {
        var client = CreateClient();
        try
        {
            var existing = await client.Repository.Content.GetAllContents(owner, repo, path);
            var sha = existing.FirstOrDefault()?.Sha;
            if (sha is null) return;

            await client.Repository.Content.DeleteFile(owner, repo, path,
                new DeleteFileRequest(commitMessage, sha));
            logger.LogInformation("檔案已刪除：{Path}", path);
        }
        catch (Octokit.NotFoundException)
        {
            // 檔案不存在，不需刪除
        }
    }

    // ────────────── Stage 10：Repo Tree API ──────────────

    /// <summary>
    /// 使用 GitHub Git Trees API 取得 repo 根目錄 + 一層子目錄結構（共兩層），
    /// 不需要 Clone，速度快、不消耗磁碟。
    /// 回傳格式：每行一個路徑，目錄以 / 結尾，最多 200 筆。
    /// </summary>
    public async Task<string> GetRepoTreeSummaryAsync(
        string owner, string repo, string? gitRef = null)
    {
        var client  = CreateClient();
        var treeRef = gitRef ?? "HEAD";

        try
        {
            // 第一層（根目錄）
            var root    = await client.Git.Tree.Get(owner, repo, treeRef);
            var lines   = new List<string>();
            var dirShas = new List<(string Path, string Sha)>();

            foreach (var item in root.Tree)
            {
                if (item.Type.Value == Octokit.TreeType.Blob)
                {
                    lines.Add(item.Path);
                }
                else if (item.Type.Value == Octokit.TreeType.Tree)
                {
                    lines.Add(item.Path + "/");
                    dirShas.Add((item.Path, item.Sha));
                }

                if (lines.Count >= 200) break;
            }

            // 第二層（每個子目錄的直接子項目）
            foreach (var (dirPath, sha) in dirShas)
            {
                if (lines.Count >= 200) break;

                try
                {
                    var sub = await client.Git.Tree.Get(owner, repo, sha);
                    foreach (var item in sub.Tree)
                    {
                        if (lines.Count >= 200) break;
                        var suffix = item.Type.Value == Octokit.TreeType.Tree ? "/" : "";
                        lines.Add($"{dirPath}/{item.Path}{suffix}");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "取得子目錄 {Dir} 的 tree 失敗，略過", dirPath);
                }
            }

            return string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetRepoTreeSummaryAsync 失敗（{Owner}/{Repo}）", owner, repo);
            return "（無法取得 repo 結構）";
        }
    }

    // ────────────── Stage 10：GitHub Actions workflow_dispatch ──────────────

    /// <summary>
    /// 透過 GitHub REST API 觸發 workflow_dispatch 事件。
    /// 用於 Maya 自動觸發 rollback.yml。
    /// </summary>
    public async Task TriggerWorkflowDispatchAsync(
        string owner,
        string repo,
        string workflowFileName,
        string branch,
        Dictionary<string, string> inputs)
    {
        var client = CreateClient();

        // CreateWorkflowDispatch.Inputs 型別為 IDictionary<string, object>
        var inputsAsObject = inputs.ToDictionary(
            kv => kv.Key,
            kv => (object)kv.Value);

        try
        {
            // Octokit.net 3.x+ 支援 CreateDispatch
            await client.Actions.Workflows.CreateDispatch(
                owner, repo, workflowFileName,
                new CreateWorkflowDispatch(branch) { Inputs = inputsAsObject });

            logger.LogInformation(
                "workflow_dispatch 已觸發：{File}（branch={Branch}，inputs={Inputs}）",
                workflowFileName, branch,
                string.Join(", ", inputs.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "TriggerWorkflowDispatchAsync 失敗：{File}（{Owner}/{Repo}）",
                workflowFileName, owner, repo);
            throw;
        }
    }
}
