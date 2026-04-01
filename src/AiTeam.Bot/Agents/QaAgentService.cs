using System.Text.RegularExpressions;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.ViewModels;

namespace AiTeam.Bot.Agents;

/// <summary>
/// QA Agent：讀取 PR 的變更檔案，產生 xUnit + NSubstitute + FluentAssertions 測試，開 PR 提交測試程式碼。
/// </summary>
public class QaAgentService(
    LlmProviderFactory providerFactory,
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
    ILogger<QaAgentService> logger) : IAgentExecutor
{
    private const string AgentName = "QA";

    /// <inheritdoc />
    public async Task<AgentExecutionResult> ExecuteTaskAsync(
        TaskItem task,
        string owner,
        string repo,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default)
    {
        AddLog(task, "QA Agent 開始執行", "running");
        await taskRepository.SaveAsync(cancellationToken);
        await PushStatus("running", task.Title);

        var localPath = "";
        try
        {
            var prNumber = ExtractPrNumber($"{task.Title} {task.Description}");
            if (prNumber <= 0)
                return new AgentExecutionResult(false, "無法從任務描述中取得 PR 編號，格式：PR #123");

            var headRef = await gitHubService.GetPullRequestHeadRefAsync(owner, repo, prNumber);
            var prFiles = await gitHubService.GetPullRequestFilesAsync(owner, repo, prNumber);
            // 只保留 source .cs，排除測試檔案本身
            var csFiles = prFiles
                .Where(f => f.FileName.EndsWith(".cs")
                         && !f.FileName.EndsWith("Tests.cs")
                         && !f.FileName.EndsWith("Spec.cs")
                         && !f.FileName.Contains(".Tests/")
                         && !f.FileName.Contains(".Test/"))
                .ToList();

            if (csFiles.Count == 0)
                return new AgentExecutionResult(false, $"PR #{prNumber} 未包含 .cs 檔案，略過 QA");

            localPath = gitHubService.CloneOrPull(owner, repo);
            AddLog(task, "Git Clone/Pull 完成", "done");

            var branchName = $"test/qa-{task.Id.ToString()[..8]}";
            gitHubService.CreateAndCheckoutBranch(localPath, branchName);

            foreach (var file in csFiles)
            {
                await GenerateAndWriteTestAsync(task, owner, repo, file.FileName, localPath, headRef, cancellationToken);
            }

            AddLog(task, "測試檔案生成完成", "done");

            var commitMessage = $"test: QA Agent 自動產生 {csFiles.Count} 個測試（來自 PR #{prNumber}）";
            gitHubService.CommitAll(localPath, commitMessage);
            gitHubService.Push(localPath, branchName);

            var prBody = $"""
                ## QA Agent 自動測試

                針對 PR #{prNumber} 的變更，自動產生以下測試：
                {string.Join("\n", csFiles.Select(f => $"- `{f.FileName}`"))}

                ## 測試框架
                xUnit + NSubstitute + FluentAssertions

                ---
                🤖 由 AiTeam QA Agent 自動產出
                """;

            var prUrl = await gitHubService.OpenPullRequestAsync(owner, repo, $"test: QA 自動測試（PR #{prNumber}）", prBody, branchName);
            AddLog(task, $"PR 已開啟：{prUrl}", "done");
            await taskRepository.SaveAsync(cancellationToken);

            await PushStatus("idle");
            return new AgentExecutionResult(true, $"QA 測試 PR 已開啟（{csFiles.Count} 個測試檔）", prUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "QA Agent 執行失敗：{Title}", task.Title);
            AddLog(task, $"執行失敗：{ex.Message}", "failed");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("error");
            return new AgentExecutionResult(false, $"QA Agent 執行失敗：{ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(localPath))
                gitHubService.CleanupLocalRepo(localPath);
        }
    }

    // ────────────── Private ──────────────

    private async Task GenerateAndWriteTestAsync(
        TaskItem task,
        string owner,
        string repo,
        string filePath,
        string localPath,
        string headRef,
        CancellationToken cancellationToken)
    {
        var sourceContent = await gitHubService.GetFileContentAsync(owner, repo, filePath, headRef);
        if (string.IsNullOrWhiteSpace(sourceContent)) return;

        var provider = providerFactory.Create(AgentName);
        var systemPrompt = BuildTestSystemPrompt();
        var userMessage = BuildTestUserMessage(filePath, sourceContent);

        var response = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken);

        var testPath = BuildTestFilePath(filePath);
        var fullPath = Path.Combine(localPath, testPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, response.Content, cancellationToken);

        logger.LogInformation("已產生測試檔：{Path}", testPath);
        AddLog(task, $"已產生 {testPath}", "done");
    }

    private static int ExtractPrNumber(string text)
    {
        var match = Regex.Match(text, @"PR\s*#(\d+)", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var num) ? num : 0;
    }

    private static string BuildTestFilePath(string sourcePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        var dir = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? "";
        return $"tests/Generated/{dir}/{fileName}Tests.cs".Replace("//", "/");
    }

    private static string BuildTestSystemPrompt() => """
        你是 C# QA 工程師，專精 xUnit + NSubstitute + FluentAssertions。
        請針對提供的原始碼，產生完整的 .cs 測試檔案。

        規則：
        1. 直接回傳完整 .cs 測試檔案，不加任何說明或 markdown 格式
        2. 測試類別命名：{ClassName}Tests
        3. 每個 public 方法至少 2 個測試（happy path + edge case）
        4. 使用 NSubstitute 的 Substitute.For<T>() 建立 mock
        5. 使用 FluentAssertions 的 .Should() 斷言
        6. 使用繁體中文命名測試方法（中文_條件_期望結果 格式）
        """;

    private static string BuildTestUserMessage(string filePath, string sourceContent) => $"""
        ## 原始碼路徑
        {filePath}

        ## 原始碼
        ```csharp
        {sourceContent}
        ```

        請產生完整的 xUnit 測試 .cs 檔案。
        """;

    private void AddLog(TaskItem task, string step, string status)
        => taskRepository.AddLog(new TaskLog
        {
            TaskId = task.Id,
            Agent = AgentName,
            Step = step,
            Status = status
        });

    private async Task PushStatus(string status, string? taskTitle = null)
        => await dashboardPush.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName = AgentName,
            Status = status,
            CurrentTaskTitle = taskTitle ?? "",
            LastUpdated = DateTime.UtcNow
        });
}
