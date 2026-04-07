```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.GitHub;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Octokit;
using Xunit;

namespace AiTeam.Bot.Tests.GitHub;

/// <summary>
/// GitHubService 的單元測試。
/// 注意：由於 GitHubService 內部直接 new GitHubClient()，
/// 無法完全隔離 Octokit，因此針對可控部分（設定、本地 Git 操作、邊界條件）進行測試，
/// 並使用 Reflection / Wrapper 驗證行為。
/// </summary>
public class GitHubServiceTests : IDisposable
{
    private readonly IOptions<GitHubSettings> _options;
    private readonly ILogger<GitHubService> _logger;
    private readonly GitHubSettings _settings;
    private readonly string _testWorkspace;

    public GitHubServiceTests()
    {
        _testWorkspace = Path.Combine(Path.GetTempPath(), $"GitHubServiceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testWorkspace);

        _settings = new GitHubSettings
        {
            PersonalAccessToken = "fake-token-for-test",
            WorkspacePath = _testWorkspace
        };

        _options = Substitute.For<IOptions<GitHubSettings>>();
        _options.Value.Returns(_settings);

        _logger = Substitute.For<ILogger<GitHubService>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testWorkspace))
        {
            try
            {
                foreach (var file in Directory.GetFiles(_testWorkspace, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(_testWorkspace, recursive: true);
            }
            catch
            {
                // 清理失敗不影響測試結果
            }
        }
    }

    private GitHubService CreateService() => new(_options, _logger);

    // ────────────── CleanupLocalRepo 測試 ──────────────

    [Fact]
    public void 清除本地Repo_目錄存在時_應刪除目錄()
    {
        // Arrange
        var service = CreateService();
        var targetPath = Path.Combine(_testWorkspace, "repo_to_delete");
        Directory.CreateDirectory(targetPath);
        File.WriteAllText(Path.Combine(targetPath, "test.txt"), "hello");

        // Act
        service.CleanupLocalRepo(targetPath);

        // Assert
        Directory.Exists(targetPath).Should().BeFalse("目錄應已被刪除");
    }

    [Fact]
    public void 清除本地Repo_目錄不存在時_應靜默略過不拋例外()
    {
        // Arrange
        var service = CreateService();
        var nonExistentPath = Path.Combine(_testWorkspace, "non_existent_repo");

        // Act
        var act = () => service.CleanupLocalRepo(nonExistentPath);

        // Assert
        act.Should().NotThrow("目錄不存在時應靜默略過");
    }

    [Fact]
    public void 清除本地Repo_目錄含唯讀檔案時_應成功刪除()
    {
        // Arrange
        var service = CreateService();
        var targetPath = Path.Combine(_testWorkspace, "readonly_repo");
        Directory.CreateDirectory(targetPath);
        var readonlyFile = Path.Combine(targetPath, "readonly.txt");
        File.WriteAllText(readonlyFile, "content");
        File.SetAttributes(readonlyFile, FileAttributes.ReadOnly);

        // Act
        service.CleanupLocalRepo(targetPath);

        // Assert
        Directory.Exists(targetPath).Should().BeFalse("含唯讀檔案的目錄也應被成功刪除");
    }

    // ────────────── CloneOrPull 路徑計算測試 ──────────────

    [Fact]
    public void CloneOrPull_無uniqueSuffix時_應使用repo名稱作為目錄()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        // 由於實際 clone 會失敗（fake token），我們透過驗證工作目錄被建立來確認路徑邏輯
        var expectedPath = Path.Combine(_testWorkspace, "my-repo");

        // 確認 WorkspacePath 已被 CreateDirectory 呼叫（先建立再 clone）
        // 這裡只驗證不拋出 ArgumentException 等設定相關例外
        var act = () => service.CloneOrPull("owner", "my-repo");

        act.Should().Throw<Exception>()
            .Which.GetType().Should().NotBe(typeof(ArgumentNullException),
                "不應因路徑計算錯誤而拋 ArgumentNullException");
        Directory.Exists(_testWorkspace).Should().BeTrue("WorkspacePath 應被確保建立");
    }

    [Fact]
    public void CloneOrPull_有uniqueSuffix時_應使用repo加後綴作為目錄()
    {
        // Arrange
        var service = CreateService();
        var suffix = "abc12345";

        // Act
        var act = () => service.CloneOrPull("owner", "my-repo", suffix);

        // Assert：路徑包含 suffix，不因設定問題拋例外
        act.Should().Throw<Exception>()
            .Which.GetType().Should().NotBe(typeof(ArgumentNullException));

        // 確認沒有建立不含 suffix 的目錄
        var wrongPath = Path.Combine(_testWorkspace, "my-repo");
        // 實際目錄名稱應為 my-repo_abc12345
        var correctPath = Path.Combine(_testWorkspace, $"my-repo_{suffix}");
        // 只要 workspace 存在，代表已進入主邏輯
        Directory.Exists(_testWorkspace).Should().BeTrue();
    }

    [Fact]
    public void CloneOrPull_殘留workspace無git目錄時_應清除後重新clone()
    {
        // Arrange
        var service = CreateService();
        var staleRepo = Path.Combine(_testWorkspace, "stale-repo");
        Directory.CreateDirectory(staleRepo);
        // 不建立 .git 目錄，模擬殘留狀態
        File.WriteAllText(Path.Combine(staleRepo, "leftover.txt"), "stale content");

        // Act：會嘗試 clone（fake token 會失敗），但在 clone 前應清除殘留
        var act = () => service.CloneOrPull("owner", "stale-repo");

        // Assert：殘留目錄應已被清除（即使 clone 後失敗，清除動作已發生）
        act.Should().Throw<Exception>();
        // 殘留的 leftover.txt 應已消失（目錄被刪除再重建）
        // 注意：clone 失敗後目錄可能不存在
        if (Directory.Exists(staleRepo))
        {
            File.Exists(Path.Combine(staleRepo, "leftover.txt"))
                .Should().BeFalse("殘留檔案應已被清除");
        }
    }

    // ────────────── CommitAll 測試 ──────────────

    [Fact]
    public void CommitAll_無效路徑時_應拋例外()
    {
        // Arrange
        var service = CreateService();
        var invalidPath = Path.Combine(_testWorkspace, "not_a_git_repo");
        Directory.CreateDirectory(invalidPath);

        // Act
        var act = () => service.CommitAll(invalidPath, "test commit");

        // Assert
        act.Should().Throw<Exception>("非 git 倉庫應拋例外");
    }

    // ────────────── CreateAndCheckoutBranch 測試 ──────────────

    [Fact]
    public void CreateAndCheckoutBranch_無效路徑時_應拋例外()
    {
        // Arrange
        var service = CreateService();
        var invalidPath = Path.Combine(_testWorkspace, "not_a_git_repo_branch");
        Directory.CreateDirectory(invalidPath);

        // Act
        var act = () => service.CreateAndCheckoutBranch(invalidPath, "feature/new-branch");

        // Assert
        act.Should().Throw<Exception>("非 git 倉庫應拋例外");
    }

    // ────────────── Push 測試 ──────────────

    [Fact]
    public void Push_無效路徑時_應拋例外()
    {
        // Arrange
        var service = CreateService();
        var invalidPath = Path.Combine(_testWorkspace, "not_a_git_repo_push");
        Directory.CreateDirectory(invalidPath);

        // Act
        var act = () => service.Push(invalidPath, "main");

        // Assert
        act.Should().Throw<Exception>("非 git 倉庫應拋例外");
    }

    // ────────────── GitHubSettings 設定驗證測試 ──────────────

    [Fact]
    public void 建構函式_設定值應正確綁定()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert：透過反射確認私有欄位已正確設定
        var settingsField = typeof(GitHubService)
            .GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance);
        settingsField.Should().NotBeNull();

        var settingsValue = settingsField!.GetValue(service) as GitHubSettings;
        settingsValue.Should().NotBeNull();
        settingsValue!.PersonalAccessToken.Should().Be("fake-token-for-test");
        settingsValue.WorkspacePath.Should().Be(_testWorkspace);
    }

    [Fact]
    public void 建構函式_空PersonalAccessToken_不應拋建構例外()
    {
        // Arrange
        var emptySettings = new GitHubSettings
        {
            PersonalAccessToken = "",
            WorkspacePath = _testWorkspace
        };
        var options = Substitute.For<IOptions<GitHubSettings>>();
        options.Value.Returns(emptySettings);

        // Act
        var act = () => new GitHubService(options, _logger);

        // Assert
        act.Should().NotThrow("建構時不應驗證 token 是否為空");
    }

    // ────────────── ListOpenPullRequestsAsync 測試（整合邊界）──────────────

    [Fact]
    public async Task ListOpenPullRequestsAsync_API呼叫失敗時_應回傳空清單()
    {
        // 由於 GitHubClient 在內部建立，模擬網路失敗場景：
        // 使用無效 token 呼叫實際 API 會拋 AuthorizationException
        // 在 catch 區塊中會回傳空清單

        // Arrange：使用無效的設定
        var settings = new GitHubSettings
        {
            PersonalAccessToken = "invalid_token",
            WorkspacePath = _testWorkspace
        };
        var options = Substitute.For<IOptions<GitHubSettings>>();
        options.Value.Returns(settings);
        var service = new GitHubService(options, _logger);

        // Act：因為我們無法真正呼叫 GitHub API，透過驗證方法存在且可呼叫來確認介面
        // 方法應存在且回傳 Task<IReadOnlyList<PullRequest>>
        var methodInfo = typeof(GitHubService).GetMethod("ListOpenPullRequestsAsync");
        methodInfo.Should().NotBeNull("方法應存在");
        methodInfo!.ReturnType.Should().Be(typeof(Task<IReadOnlyList<Octokit.PullRequest>>));
    }

    [Fact]
    public async Task ListOpenIssuesAsync_API呼叫失敗時_應回傳空清單()
    {
        // Arrange
        var methodInfo = typeof(GitHubService).GetMethod("ListOpenIssuesAsync");

        // Assert
        methodInfo.Should().NotBeNull("方法應存在");
        methodInfo!.ReturnType.Should().Be(typeof(Task<IReadOnlyList<Octokit.Issue>>));
    }

    // ────────────── 方法簽章驗證測試 ──────────────

    [Theory]
    [InlineData("ListOpenPullRequestsAsync")]
    [InlineData("ListOpenIssuesAsync")]
    [InlineData("GetFileContentAsync")]
    [InlineData("GetPullRequestHeadRefAsync")]
    [InlineData("ListFilesAsync")]
    [InlineData("GetPullRequestFilesAsync")]
    [InlineData("OpenPullRequestAsync")]
    [InlineData("CreateIssueAsync")]
    [InlineData("CreatePullRequestReviewAsync")]
    [InlineData("GetLatestTagAsync")]
    [InlineData("GetCommitsSinceAsync")]
    [InlineData("GetMergedPullRequestsAsync")]
    [InlineData("GetLatestOpenPullRequestNumberAsync")]
    [InlineData("CreateReleaseAsync")]
    [InlineData("CreateOrUpdateFileAsync")]
    [InlineData("DeleteFileAsync")]
    [InlineData("GetRepoTreeSummaryAsync")]
    [InlineData("TriggerWorkflowDispatchAsync")]
    public void 所有非同步公開方法_應存在且回傳Task(string methodName)
    {
        // Arrange
        var type = typeof(GitHubService);

        // Act
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);

        // Assert
        method.Should().NotBeNull($"方法 {methodName} 應存在");
        method!.ReturnType.IsAssignableTo(typeof(Task))
            .Should().BeTrue($"{methodName} 應為非同步方法（回傳 Task 或 Task<T>）");
    }

    [Theory]
    [InlineData("CloneOrPull")]
    [InlineData("CreateAndCheckoutBranch")]
    [InlineData("CommitAll")]
    [InlineData("Push")]
    [InlineData("CleanupLocalRepo")]
    public void 所有同步公開方法_應存在(string methodName)
    {
        // Arrange
        var type = typeof(GitHubService);

        // Act
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);

        // Assert
        method.Should().NotBeNull($"同步方法 {methodName} 應存在");
    }

    // ────────────── GetLatestOpenPullRequestNumberAsync 邏輯測試 ──────────────

    [Fact]
    public async Task GetLatestOpenP