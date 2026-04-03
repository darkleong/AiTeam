using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.ViewModels;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Documentation Agent：讀取原始碼，產生 Markdown 文件或補充 XML 註解，開 PR 提交文件。
/// </summary>
public class DocAgentService(
    LlmProviderFactory providerFactory,
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
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
        AddLog(task, "Doc Agent 開始執行", "running");
        await taskRepository.SaveAsync(cancellationToken);
        await PushStatus("running", task.Title);

        var localPath = "";
        try
        {
            var pathPrefix = ExtractPathPrefix(task.Description ?? task.Title);
            var allFiles = await gitHubService.ListFilesAsync(owner, repo, pathPrefix);
            var csFiles = allFiles
                .Where(f => f.Name.EndsWith(".cs") && f.Type == "file")
                .ToList();

            if (csFiles.Count == 0)
            {
                // Orchestrator 觸發時路徑可能無法從描述推斷，略過文件生成（非失敗）
                AddLog(task, $"路徑 '{pathPrefix}' 下無 .cs 檔案，略過文件生成", "done");
                await taskRepository.SaveAsync(cancellationToken);
                await PushStatus("idle");
                return new AgentExecutionResult(true, $"路徑 '{pathPrefix}' 無 .cs 檔案，略過文件生成");
            }

            var xmlMode = IsXmlMode(task.Title);
            localPath = gitHubService.CloneOrPull(owner, repo, task.Id.ToString("N")[..8]);
            AddLog(task, "Git Clone/Pull 完成", "done");

            var branchName = $"docs/auto-{task.Id.ToString()[..8]}";
            gitHubService.CreateAndCheckoutBranch(localPath, branchName);

            var generatedCount = 0;
            foreach (var file in csFiles)
            {
                var written = await GenerateDocAsync(task, owner, repo, file.Path, localPath, xmlMode, cancellationToken);
                if (written) generatedCount++;
            }

            AddLog(task, $"文件生成完成（{generatedCount} 個檔案）", "done");

            var mode = xmlMode ? "XML 註解補充" : "Markdown 文件";
            var commitMessage = $"docs: Doc Agent 自動產生 {mode}（{generatedCount} 個檔案）";
            gitHubService.CommitAll(localPath, commitMessage);
            gitHubService.Push(localPath, branchName);

            var prBody = $"""
                ## Doc Agent 自動文件

                模式：{mode}
                路徑範圍：`{pathPrefix}`
                處理檔案數：{generatedCount}

                ---
                🤖 由 AiTeam Doc Agent 自動產出
                """;

            var prUrl = await gitHubService.OpenPullRequestAsync(owner, repo, $"docs: {mode} 自動產出", prBody, branchName);
            AddLog(task, $"PR 已開啟：{prUrl}", "done");
            await taskRepository.SaveAsync(cancellationToken);

            await PushStatus("idle");
            return new AgentExecutionResult(true, $"文件 PR 已開啟（{generatedCount} 個{mode}）", prUrl);
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
            if (!string.IsNullOrEmpty(localPath))
                gitHubService.CleanupLocalRepo(localPath);
        }
    }

    // ────────────── Private ──────────────

    private async Task<bool> GenerateDocAsync(
        TaskItem task,
        string owner,
        string repo,
        string filePath,
        string localPath,
        bool xmlMode,
        CancellationToken cancellationToken)
    {
        var sourceContent = await gitHubService.GetFileContentAsync(owner, repo, filePath);
        if (string.IsNullOrWhiteSpace(sourceContent)) return false;

        var provider = providerFactory.Create(AgentName);

        string systemPrompt, userMessage, outputPath;
        if (xmlMode)
        {
            systemPrompt = BuildXmlSystemPrompt();
            userMessage = $"## 原始碼路徑\n{filePath}\n\n## 原始碼\n```csharp\n{sourceContent}\n```\n\n請補充 XML 文件註解後回傳完整 .cs 檔案。";
            outputPath = Path.Combine(localPath, filePath.Replace('/', Path.DirectorySeparatorChar));
        }
        else
        {
            systemPrompt = BuildMarkdownSystemPrompt();
            userMessage = $"## 原始碼路徑\n{filePath}\n\n## 原始碼\n```csharp\n{sourceContent}\n```\n\n請產生 Markdown 文件。";
            var mdName = Path.GetFileNameWithoutExtension(filePath) + ".md";
            outputPath = Path.Combine(localPath, "docs", "generated", mdName);
        }

        var response = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, response.Content, cancellationToken);

        logger.LogInformation("已產生文件：{Path}", outputPath);
        AddLog(task, $"已產生 {Path.GetFileName(outputPath)}", "done");
        return true;
    }

    private static string ExtractPathPrefix(string text)
    {
        // 嘗試從文字中找到路徑前綴（如 "src/AiTeam.Bot/Agents"）
        // 分割所有空白與換行，過濾 URL（含 ://）與過長字串（URL 通常很長）
        var segments = text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var pathLike = segments.FirstOrDefault(s =>
            s.Contains('/') &&
            !s.Contains("://") &&   // 排除 http:// https:// 等 URL
            s.Length < 80);         // 排除超長字串
        return (pathLike ?? "src").TrimEnd('/');
    }

    private static bool IsXmlMode(string title)
        => title.Contains("XML", StringComparison.OrdinalIgnoreCase) ||
           title.Contains("xml 註解", StringComparison.OrdinalIgnoreCase);

    private static string BuildMarkdownSystemPrompt() => """
        你是技術文件撰寫專家，使用繁體中文撰寫。
        根據提供的 C# 原始碼，產生清晰的 Markdown 文件，包含：
        1. 類別概覽與用途說明
        2. 所有 public 方法的說明、參數、回傳值
        3. 使用範例（若適用）

        直接回傳 Markdown 內容，不加任何前言或說明。
        """;

    private static string BuildXmlSystemPrompt() => """
        你是 C# 資深工程師，負責補充 XML 文件註解。
        請為所有 public 類別、方法、屬性加上 /// <summary>...</summary> 及 <param>、<returns> 等標籤。
        使用繁體中文撰寫說明。

        直接回傳完整 .cs 檔案（含原有程式碼與新增的 XML 註解），不加任何說明文字。
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
