using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.ViewModels;
using Microsoft.Extensions.Configuration;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Requirements Analyst Agent（Rosa）：將原始需求拆解為 GitHub Issues。
/// Stage 12：改用 Claude Code 唯讀模式探索 codebase，產出更精確的 Issues。
/// </summary>
public class RequirementsAgentService(
    LlmProviderFactory providerFactory,
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
    ClaudeCodeService claudeCodeService,
    IConfiguration configuration,
    ILogger<RequirementsAgentService> logger) : IAgentExecutor
{
    private const string AgentName = "Requirements";

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
        AddLog(task, "Requirements Agent 開始執行", "running");
        await taskRepository.SaveAsync(cancellationToken);
        await PushStatus("running", task.Title);

        var localPath = "";
        try
        {
            // Clone repo 供 Claude Code 唯讀探索使用
            localPath = gitHubService.CloneOrPull(owner, repo, $"req-{task.Id:N}"[..8]);
            AddLog(task, "Git Clone/Pull 完成", "running");

            var issues = await AnalyzeOnlyAsync(task, localPath, cancellationToken: cancellationToken);
            if (issues.Count == 0)
                return new AgentExecutionResult(false, "LLM 未能解析出有效的 Issue 清單");

            AddLog(task, $"需求分析完成，共 {issues.Count} 個 Issue", "done");
            return await CreateIssuesFromPreviewAsync(task, owner, repo, issues, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Requirements Agent 執行失敗：{Title}", task.Title);
            AddLog(task, $"執行失敗：{ex.Message}", "failed");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("error");
            return new AgentExecutionResult(false, $"Requirements Agent 執行失敗：{ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(localPath))
                gitHubService.CleanupLocalRepo(localPath);
        }
    }

    /// <summary>
    /// 僅執行需求分析，回傳 Issue 預覽清單（不建立 GitHub Issues）。
    /// Stage 12：使用 Claude Code 唯讀模式探索 codebase。
    /// </summary>
    /// <param name="task">任務資訊</param>
    /// <param name="repoLocalPath">已 clone 的 repo 本地路徑（由 ShowProposalAsync 統一管理）</param>
    /// <param name="images">老闆附的圖片（若有，會先透過 LLM 轉為文字描述再傳入 Claude Code）</param>
    /// <param name="previousIssues">✏️ 調整時帶入第一版 Issues（提示 Claude Code 修改而非重做）</param>
    /// <param name="cancellationToken">CancellationToken</param>
    internal async Task<List<RequirementIssuePreview>> AnalyzeOnlyAsync(
        TaskItem task,
        string? repoLocalPath = null,
        IReadOnlyList<ImageAttachment>? images = null,
        IReadOnlyList<RequirementIssuePreview>? previousIssues = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(repoLocalPath))
        {
            var issues = await RunClaudeCodeAnalysisAsync(
                task, repoLocalPath, images, previousIssues, cancellationToken);
            if (issues.Count > 0) return issues;

            // Claude Code 失敗時 fallback 到 LLM 直呼叫
            logger.LogWarning("Claude Code 唯讀分析失敗，改用 LLM 直接呼叫");
        }

        var raw = await AnalyzeRequirementsAsync(task, images, previousIssues, cancellationToken);
        return raw.Select(i => new RequirementIssuePreview(i.Title, i.Body, i.Labels)).ToList();
    }

    /// <summary>
    /// 根據已確認的預覽清單，實際建立 GitHub Issues。
    /// </summary>
    internal async Task<AgentExecutionResult> CreateIssuesFromPreviewAsync(
        TaskItem task,
        string owner,
        string repo,
        IReadOnlyList<RequirementIssuePreview> issues,
        CancellationToken cancellationToken = default)
    {
        AddLog(task, $"根據確認清單建立 {issues.Count} 個 Issues", "running");
        await taskRepository.SaveAsync(cancellationToken);
        await PushStatus("running", task.Title);

        try
        {
            var createdUrls = new List<string>();
            foreach (var issue in issues)
            {
                var url = await gitHubService.CreateIssueAsync(owner, repo, issue.Title, issue.Body, issue.Labels);
                createdUrls.Add(url);
                AddLog(task, $"Issue 已建立：{issue.Title}", "done");
            }

            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("idle");

            return new AgentExecutionResult(
                true,
                $"已建立 {createdUrls.Count} 個 GitHub Issues",
                createdUrls.FirstOrDefault(),
                OutputUrls: createdUrls);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "建立 Issues 失敗：{Title}", task.Title);
            AddLog(task, $"執行失敗：{ex.Message}", "failed");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("error");
            return new AgentExecutionResult(false, $"建立 Issues 失敗：{ex.Message}");
        }
    }

    // ────────────── Claude Code 唯讀分析 ──────────────

    /// <summary>
    /// 使用 Claude Code 唯讀模式探索 codebase，產出 Issues。
    /// </summary>
    private async Task<List<RequirementIssuePreview>> RunClaudeCodeAnalysisAsync(
        TaskItem task,
        string repoLocalPath,
        IReadOnlyList<ImageAttachment>? images,
        IReadOnlyList<RequirementIssuePreview>? previousIssues,
        CancellationToken cancellationToken)
    {
        var claudeMdPath     = Path.Combine(repoLocalPath, "CLAUDE.md");
        var templatePath     = Path.Combine(AppContext.BaseDirectory, "Resources", "CLAUDE_Rosa.md");
        var originalClaudeMd = File.Exists(claudeMdPath)
            ? await File.ReadAllTextAsync(claudeMdPath, cancellationToken)
            : null;

        try
        {
            // 寫入 Rosa 專用 CLAUDE.md
            if (File.Exists(templatePath))
                await File.WriteAllTextAsync(claudeMdPath,
                    await File.ReadAllTextAsync(templatePath, cancellationToken), cancellationToken);

            var prompt = await BuildClaudeCodePromptAsync(task, images, previousIssues, cancellationToken);
            var model  = configuration["Agents:Requirements:Model"]
                      ?? configuration["Anthropic:DefaultModel"]
                      ?? "claude-sonnet-4-6";
            var apiKey = configuration["Anthropic:ApiKey"] ?? "";

            var result = await claudeCodeService.RunReadOnlyAsync(
                repoLocalPath, prompt, model, apiKey, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning("Rosa Claude Code 執行未成功（exitCode={Code}）", result.ExitCode);
                return [];
            }

            var issues = TryParseIssues(result.Output);
            return issues?.Select(i => new RequirementIssuePreview(i.Title, i.Body, i.Labels)).ToList()
                   ?? [];
        }
        finally
        {
            // 還原 CLAUDE.md
            if (originalClaudeMd is not null)
                await File.WriteAllTextAsync(claudeMdPath, originalClaudeMd, CancellationToken.None);
            else if (File.Exists(claudeMdPath))
                File.Delete(claudeMdPath);
        }
    }

    private async Task<string> BuildClaudeCodePromptAsync(
        TaskItem task,
        IReadOnlyList<ImageAttachment>? images,
        IReadOnlyList<RequirementIssuePreview>? previousIssues,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 任務標題");
        sb.AppendLine(task.Title);
        sb.AppendLine();
        sb.AppendLine("## 功能需求描述");
        sb.AppendLine(task.Description ?? task.Title);
        sb.AppendLine();

        // 圖片描述：Claude Code CLI 不支援 Base64 圖片，先透過 LLM 轉為文字
        if (images?.Count > 0)
        {
            var imageDesc = await DescribeImagesAsync(images, cancellationToken);
            if (!string.IsNullOrWhiteSpace(imageDesc))
            {
                sb.AppendLine("## 老闆附圖說明");
                sb.AppendLine(imageDesc);
                sb.AppendLine();
            }
        }

        // ✏️ 調整模式：帶入第一版，指示修改而非重做
        if (previousIssues?.Count > 0)
        {
            sb.AppendLine("## 第一版 Issues（請依老闆意見修改，不要重做）");
            for (var i = 0; i < previousIssues.Count; i++)
                sb.AppendLine($"{i + 1}. {previousIssues[i].Title}");
            sb.AppendLine();
        }

        sb.AppendLine("## 你的任務");
        if (previousIssues?.Count > 0)
            sb.AppendLine("基於第一版 Issues 和老闆意見進行修改，探索 codebase 後輸出修改後的 JSON Issue 陣列。");
        else
            sb.AppendLine("探索 codebase，理解現有架構，然後輸出 JSON Issue 陣列。只輸出 JSON，不加說明。");

        return sb.ToString();
    }

    // ────────────── LLM 直呼叫（Fallback） ──────────────

    private async Task<List<RequirementIssue>> AnalyzeRequirementsAsync(
        TaskItem task,
        IReadOnlyList<ImageAttachment>? images,
        IReadOnlyList<RequirementIssuePreview>? previousIssues,
        CancellationToken cancellationToken)
    {
        var provider = providerFactory.Create(AgentName);
        var sb = new StringBuilder();
        sb.AppendLine("## 原始需求");
        sb.AppendLine(task.Title);
        sb.AppendLine();
        sb.AppendLine(task.Description ?? "");

        if (previousIssues?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## 第一版 Issues（請依老闆意見修改，不要重做）");
            for (var i = 0; i < previousIssues.Count; i++)
                sb.AppendLine($"{i + 1}. {previousIssues[i].Title}");
        }

        sb.AppendLine();
        sb.AppendLine("請依照格式產出 JSON 陣列，每個 Issue 代表一個可獨立執行的功能或任務。");

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var response = await provider.CompleteAsync(
                BuildSystemPrompt(), sb.ToString(), cancellationToken, images);
            var issues = TryParseIssues(response.Content);
            if (issues is not null)
            {
                logger.LogInformation("需求分析解析成功（第 {Attempt} 次）", attempt);
                return issues;
            }
            logger.LogWarning("需求分析格式錯誤（第 {Attempt} 次）：{Content}", attempt, response.Content);
        }

        return [];
    }

    /// <summary>
    /// 透過 LLM 將圖片轉為文字描述，供 Claude Code prompt 使用。
    /// </summary>
    private async Task<string> DescribeImagesAsync(
        IReadOnlyList<ImageAttachment> images,
        CancellationToken cancellationToken)
    {
        try
        {
            var provider = providerFactory.Create(AgentName);
            var response = await provider.CompleteAsync(
                "你是一位技術文件撰寫員，請簡潔描述圖片中的 UI 結構、功能需求或問題點（100-200 字）。",
                "請描述圖片內容，重點放在功能需求相關的資訊。",
                cancellationToken,
                images);
            return response.Content.Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "圖片描述轉換失敗，略過");
            return "";
        }
    }

    private static List<RequirementIssue>? TryParseIssues(string content)
    {
        try
        {
            var start = content.IndexOf('[');
            var end = content.LastIndexOf(']');
            if (start < 0 || end < 0) return null;
            var json = content[start..(end + 1)];
            return JsonSerializer.Deserialize<List<RequirementIssue>>(json, JsonOptions);
        }
        catch { return null; }
    }

    private static string BuildSystemPrompt() => """
        你是資深需求分析師，負責將原始需求拆解為 GitHub Issues。
        每個 Issue 代表一個可獨立執行的功能或任務，粒度適中（不能太大也不能太小）。

        ## 回應格式（只回傳 JSON Array，不加任何說明）
        [
          {
            "title": "動詞開頭的具體標題（繁體中文）",
            "body": "## 背景\n說明此需求的背景...\n\n## 驗收條件\n- [ ] 條件一\n- [ ] 條件二",
            "labels": ["feature", "P1"]
          }
        ]

        Labels 規則：
        - 類型：feature（功能）/ bug（錯誤）/ chore（維護）
        - 優先度：P0（緊急）/ P1（高）/ P2（一般）/ P3（低）
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

    // ────────────── 內部 DTO ──────────────

    private sealed class RequirementIssue
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("body")]
        public string Body { get; set; } = "";

        [JsonPropertyName("labels")]
        public List<string> Labels { get; set; } = [];
    }
}

/// <summary>
/// Requirements Agent 分析出的 Issue 預覽，供 CommandHandler 雙層確認使用。
/// </summary>
internal sealed record RequirementIssuePreview(
    string Title,
    string Body,
    IReadOnlyList<string> Labels);
