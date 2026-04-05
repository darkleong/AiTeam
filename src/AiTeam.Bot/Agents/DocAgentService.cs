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
/// Documentation Agent（Sage）：讀取 PR 變更檔案，產生 Markdown 文件，開 PR 提交文件。
/// Stage 12：改用 Claude Code 唯讀模式直接讀取 PR changed files，刪除 ExtractPathPrefix 猜路徑邏輯。
/// </summary>
public class DocAgentService(
    LlmProviderFactory providerFactory,
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
    ClaudeCodeService claudeCodeService,
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
        AddLog(task, "Doc Agent 開始執行", "running");
        await taskRepository.SaveAsync(cancellationToken);
        await PushStatus("running", task.Title);

        var readPath = "";
        var writePath = "";

        try
        {
            // 從任務描述解析 PR 編號
            var prNumber = ExtractPrNumber($"{task.Title} {task.Description}");
            if (prNumber <= 0)
                prNumber = await gitHubService.GetLatestOpenPullRequestNumberAsync(owner, repo);

            if (prNumber <= 0)
                return new AgentExecutionResult(true, "找不到 PR 編號，略過文件生成");

            var headRef = await gitHubService.GetPullRequestHeadRefAsync(owner, repo, prNumber);
            var prFiles = await gitHubService.GetPullRequestFilesAsync(owner, repo, prNumber);
            var csFiles = prFiles
                .Where(f => f.FileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Select(f => f.FileName)
                .ToList();

            if (csFiles.Count == 0)
            {
                AddLog(task, $"PR #{prNumber} 無 .cs 檔案，略過文件生成", "done");
                await taskRepository.SaveAsync(cancellationToken);
                await PushStatus("idle");
                return new AgentExecutionResult(true, $"PR #{prNumber} 無 .cs 檔案，略過文件生成");
            }

            // 1. Clone PR branch 供 Claude Code 唯讀探索
            readPath = gitHubService.CloneOrPull(owner, repo, $"sager-{task.Id:N}"[..8]);
            gitHubService.CreateAndCheckoutBranch(readPath, headRef);
            AddLog(task, $"PR #{prNumber} branch checkout 完成（{csFiles.Count} 個 .cs 檔）", "done");

            var docContent = await RunClaudeCodeDocAsync(
                task, readPath, csFiles, prNumber, headRef, cancellationToken);

            gitHubService.CleanupLocalRepo(readPath);
            readPath = "";

            if (string.IsNullOrWhiteSpace(docContent))
                return new AgentExecutionResult(true, $"PR #{prNumber} 文件生成無輸出，略過提交");

            // 2. Clone main branch，建立 doc PR
            var docBranch = $"docs/pr{prNumber}-{task.Id.ToString("N")[..6]}";
            writePath = gitHubService.CloneOrPull(owner, repo, $"saged-{task.Id:N}"[..8]);
            gitHubService.CreateAndCheckoutBranch(writePath, docBranch);

            var outputPath = Path.Combine(writePath, "docs", "generated", $"pr{prNumber}-doc.md");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, docContent, cancellationToken);
            AddLog(task, "文件檔案已寫入", "done");

            gitHubService.CommitAll(writePath, $"docs: Sage 自動產生 PR #{prNumber} 技術文件");
            gitHubService.Push(writePath, docBranch);

            var prBody = $"""
                ## Sage 自動技術文件

                來源 PR：#{prNumber}
                文件涵蓋 .cs 檔案：{csFiles.Count} 個

                ---
                🤖 由 AiTeam Doc Agent 自動產出
                """;

            var prUrl = await gitHubService.OpenPullRequestAsync(
                owner, repo, $"docs: PR #{prNumber} 技術文件", prBody, docBranch);
            AddLog(task, $"PR 已開啟：{prUrl}", "done");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("idle");

            return new AgentExecutionResult(true, $"文件 PR 已開啟（PR #{prNumber}，{csFiles.Count} 個檔案）", prUrl);
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
            if (!string.IsNullOrEmpty(readPath))  gitHubService.CleanupLocalRepo(readPath);
            if (!string.IsNullOrEmpty(writePath)) gitHubService.CleanupLocalRepo(writePath);
        }
    }

    // ────────────── Claude Code 唯讀文件生成 ──────────────

    private async Task<string> RunClaudeCodeDocAsync(
        TaskItem task,
        string repoLocalPath,
        IReadOnlyList<string> csFiles,
        int prNumber,
        string headRef,
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
            sb.AppendLine($"## PR #{prNumber}（branch: {headRef}）變更的 .cs 檔案");
            foreach (var f in csFiles)
                sb.AppendLine($"- {f}");
            sb.AppendLine();
            sb.AppendLine("## 你的任務");
            sb.AppendLine("讀取上述每個 .cs 檔案的完整內容，然後輸出一份完整的 Markdown 技術文件（涵蓋所有檔案）。直接輸出 Markdown，不加額外說明。");

            var model  = configuration["Agents:Doc:Model"]
                      ?? configuration["Anthropic:DefaultModel"]
                      ?? "claude-sonnet-4-6";
            var apiKey = configuration["Anthropic:ApiKey"] ?? "";

            var result = await claudeCodeService.RunReadOnlyAsync(
                repoLocalPath, sb.ToString(), model, apiKey, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning("Sage Claude Code 執行未成功，fallback LLM（exitCode={Code}）", result.ExitCode);
                return await GenerateDocWithLlmFallbackAsync(task, csFiles, repoLocalPath, prNumber, cancellationToken);
            }

            return result.Output.Trim();
        }
        finally
        {
            if (originalClaudeMd is not null)
                await File.WriteAllTextAsync(claudeMdPath, originalClaudeMd, CancellationToken.None);
            else if (File.Exists(claudeMdPath))
                File.Delete(claudeMdPath);
        }
    }

    // ────────────── LLM Fallback ──────────────

    private async Task<string> GenerateDocWithLlmFallbackAsync(
        TaskItem task,
        IReadOnlyList<string> csFiles,
        string localPath,
        int prNumber,
        CancellationToken cancellationToken)
    {
        var provider = providerFactory.Create(AgentName);
        var sb = new StringBuilder();

        foreach (var filePath in csFiles.Take(5)) // fallback 最多 5 個檔案
        {
            try
            {
                var fullPath = Path.Combine(localPath, filePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath)) continue;
                var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
                if (string.IsNullOrWhiteSpace(content)) continue;

                sb.AppendLine($"## {filePath}");
                sb.AppendLine("```csharp");
                sb.AppendLine(content.Length > 3000 ? content[..3000] + "\n...(截斷)" : content);
                sb.AppendLine("```");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "讀取檔案失敗：{File}", filePath);
            }
        }

        if (sb.Length == 0) return "";

        var systemPrompt = BuildMarkdownSystemPrompt();
        var userMessage  = $"PR #{prNumber} 的變更檔案如下，請產生 Markdown 技術文件：\n\n{sb}";
        var response     = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken);
        return response.Content.Trim();
    }

    private static string BuildMarkdownSystemPrompt() => """
        你是技術文件撰寫專家，使用繁體中文撰寫。
        根據提供的 C# 原始碼，產生清晰的 Markdown 文件，包含：
        1. 類別概覽與用途說明
        2. 所有 public 方法的說明、參數、回傳值
        3. 使用範例（若適用）

        直接回傳 Markdown 內容，不加任何前言或說明。
        """;

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
