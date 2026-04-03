using System.Text.RegularExpressions;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.ViewModels;

namespace AiTeam.Bot.Agents;

/// <summary>
/// QA Agent：讀取 PR 的變更檔案，依變更性質選擇測試策略：
/// - .cs 變更 → xUnit + NSubstitute + FluentAssertions（邏輯測試）
/// - .razor / .css 變更 → Playwright（UI 視覺截圖測試）
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

            var headRef  = await gitHubService.GetPullRequestHeadRefAsync(owner, repo, prNumber);
            var prFiles  = await gitHubService.GetPullRequestFilesAsync(owner, repo, prNumber);

            // 判斷測試策略
            var hasUiChanges = prFiles.Any(f =>
                f.FileName.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
                f.FileName.EndsWith(".css",   StringComparison.OrdinalIgnoreCase));

            var csFiles = prFiles
                .Where(f => f.FileName.EndsWith(".cs")
                         && !f.FileName.EndsWith("Tests.cs")
                         && !f.FileName.EndsWith("Spec.cs")
                         && !f.FileName.Contains(".Tests/")
                         && !f.FileName.Contains(".Test/"))
                .ToList();

            if (!hasUiChanges && csFiles.Count == 0)
                return new AgentExecutionResult(false, $"PR #{prNumber} 未包含可測試的 .cs / .razor / .css 檔案，略過 QA");

            localPath = gitHubService.CloneOrPull(owner, repo, task.Id.ToString("N")[..8]);
            AddLog(task, "Git Clone/Pull 完成", "done");

            var branchName = $"test/qa-{task.Id.ToString()[..8]}";
            gitHubService.CreateAndCheckoutBranch(localPath, branchName);

            var generatedFiles = new List<string>();

            // xUnit 測試（.cs 變更）
            foreach (var file in csFiles)
            {
                var path = await GenerateAndWriteTestAsync(
                    task, owner, repo, file.FileName, localPath, headRef, cancellationToken);
                if (path is not null) generatedFiles.Add(path);
            }

            // Playwright 測試（UI 變更）
            if (hasUiChanges)
            {
                var uiFiles = prFiles
                    .Where(f => f.FileName.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
                                f.FileName.EndsWith(".css",   StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var path = await GenerateAndWritePlaywrightTestAsync(
                    task, prNumber, uiFiles.Select(f => f.FileName).ToList(),
                    localPath, cancellationToken);
                if (path is not null) generatedFiles.Add(path);
            }

            AddLog(task, $"測試檔案生成完成（共 {generatedFiles.Count} 個）", "done");

            var commitMessage = $"test: QA Agent 自動產生測試（來自 PR #{prNumber}）";
            gitHubService.CommitAll(localPath, commitMessage);
            gitHubService.Push(localPath, branchName);

            var strategyDesc = hasUiChanges
                ? $"xUnit {csFiles.Count} 個 + Playwright 1 個"
                : $"xUnit {csFiles.Count} 個";

            var prBody = $"""
                ## QA Agent 自動測試

                針對 PR #{prNumber} 的變更，自動產生以下測試：
                {string.Join("\n", generatedFiles.Select(f => $"- `{f}`"))}

                ## 測試框架
                {(csFiles.Count > 0 ? "- xUnit + NSubstitute + FluentAssertions（邏輯測試）\n" : "")}{(hasUiChanges ? "- Playwright（UI 視覺截圖測試，PR 合併前 CI 自動執行並附上截圖）" : "")}

                ---
                🤖 由 AiTeam QA Agent 自動產出
                """;

            var prUrl = await gitHubService.OpenPullRequestAsync(
                owner, repo,
                $"test: QA 自動測試（PR #{prNumber}）",
                prBody, branchName);

            AddLog(task, $"PR 已開啟：{prUrl}", "done");
            await taskRepository.SaveAsync(cancellationToken);

            await PushStatus("idle");
            return new AgentExecutionResult(true, $"QA 測試 PR 已開啟（{strategyDesc}）", prUrl);
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

    /// <summary>產生 xUnit 測試並寫入本地路徑，回傳寫入的相對路徑。</summary>
    private async Task<string?> GenerateAndWriteTestAsync(
        TaskItem task,
        string owner,
        string repo,
        string filePath,
        string localPath,
        string headRef,
        CancellationToken cancellationToken)
    {
        var sourceContent = await gitHubService.GetFileContentAsync(owner, repo, filePath, headRef);
        if (string.IsNullOrWhiteSpace(sourceContent)) return null;

        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = BuildTestSystemPrompt();
        var userMessage  = BuildTestUserMessage(filePath, sourceContent);

        var response  = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken);

        var testPath  = BuildTestFilePath(filePath);
        var fullPath  = Path.Combine(localPath, testPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, response.Content, cancellationToken);

        logger.LogInformation("已產生 xUnit 測試：{Path}", testPath);
        AddLog(task, $"已產生 {testPath}", "done");
        return testPath;
    }

    /// <summary>
    /// 產生 Playwright 視覺截圖測試並寫入本地路徑，回傳寫入的相對路徑。
    /// 測試策略：導航到 UI 變更頁面、截圖（light/dark mode）。
    /// </summary>
    private async Task<string?> GenerateAndWritePlaywrightTestAsync(
        TaskItem task,
        int prNumber,
        IReadOnlyList<string> changedUiFiles,
        string localPath,
        CancellationToken cancellationToken)
    {
        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = BuildPlaywrightSystemPrompt();
        var userMessage  = BuildPlaywrightUserMessage(prNumber, changedUiFiles);

        var response = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken);

        var testPath = $"src/AiTeam.Tests.Playwright/Generated/PR{prNumber}/VisualTests.cs";
        var fullPath = Path.Combine(localPath, testPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, response.Content, cancellationToken);

        logger.LogInformation("已產生 Playwright 測試：{Path}", testPath);
        AddLog(task, $"已產生 Playwright 測試：{testPath}", "done");
        return testPath;
    }

    private static int ExtractPrNumber(string text)
    {
        // 先嘗試 PR #123 格式
        var match = Regex.Match(text, @"PR\s*#(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var num)) return num;

        // 再嘗試 GitHub URL /pull/123 格式（Orchestrator 傳入的 PR 連結）
        match = Regex.Match(text, @"/pull/(\d+)", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out num) ? num : 0;
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

    private static string BuildPlaywrightSystemPrompt() => """
        你是 C# QA 工程師，專精 Microsoft.Playwright + MSTest。
        請針對 UI 變更，產生 Playwright E2E 視覺截圖測試。

        規則：
        1. 直接回傳完整 .cs 測試檔案，不加任何說明或 markdown 格式
        2. namespace：AiTeam.Tests.Playwright.Generated
        3. 使用 [TestClass] 和 [TestMethod] attribute
        4. 繼承 PageTest（Microsoft.Playwright.MSTest 提供）
        5. Dashboard URL 從環境變數 DASHBOARD_URL 讀取（預設 http://localhost:5051）
        6. 登入：使用環境變數 DASHBOARD_USER / DASHBOARD_PASS
        7. 每個測試截圖並存到 screenshots/ 資料夾
        8. 同一頁面截兩張：light mode（預設）+ dark mode（點選 DarkMode toggle）
        9. 使用繁體中文命名測試方法
        """;

    private static string BuildPlaywrightUserMessage(int prNumber, IReadOnlyList<string> changedFiles) => $"""
        ## PR 編號
        PR #{prNumber}

        ## UI 變更檔案
        {string.Join("\n", changedFiles.Select(f => $"- {f}"))}

        請針對以上 UI 變更，產生 Playwright 視覺截圖測試。
        根據檔案路徑推斷對應的 Dashboard 頁面 URL，並截圖驗證 UI 是否正常。
        """;

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
