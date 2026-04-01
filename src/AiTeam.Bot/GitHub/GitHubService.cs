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
    public string CloneOrPull(string owner, string repo)
    {
        var localPath = Path.Combine(_settings.WorkspacePath, repo);
        Directory.CreateDirectory(_settings.WorkspacePath);

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
    /// 在本地建立新 branch 並切換。
    /// </summary>
    public void CreateAndCheckoutBranch(string localPath, string branchName)
    {
        using var gitRepo = new LibGit2Sharp.Repository(localPath);
        var branch = gitRepo.CreateBranch(branchName);
        Commands.Checkout(gitRepo, branch);
        logger.LogInformation("已切換到 branch {Branch}", branchName);
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
}
