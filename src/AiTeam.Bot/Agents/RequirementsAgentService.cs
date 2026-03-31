using System.Text.Json;
using System.Text.Json.Serialization;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.ViewModels;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Requirements Analyst Agent：將原始需求拆解為 GitHub Issues，逐一建立後回報清單。
/// </summary>
public class RequirementsAgentService(
    LlmProviderFactory providerFactory,
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
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

        try
        {
            var issues = await AnalyzeOnlyAsync(task, cancellationToken);
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
    }

    /// <summary>
    /// 僅執行 LLM 需求分析，回傳 Issue 預覽清單（不建立 GitHub Issues）。
    /// 供 CommandHandler 在第三層確認前使用。
    /// </summary>
    internal async Task<List<RequirementIssuePreview>> AnalyzeOnlyAsync(
        TaskItem task,
        CancellationToken cancellationToken = default)
    {
        var raw = await AnalyzeRequirementsAsync(task, cancellationToken);
        return raw.Select(i => new RequirementIssuePreview(i.Title, i.Body, i.Labels)).ToList();
    }

    /// <summary>
    /// 根據已確認的預覽清單，實際建立 GitHub Issues。
    /// 供 CommandHandler 在第三層 req_yes 後使用。
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
                createdUrls.FirstOrDefault());
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

    // ────────────── Private ──────────────

    private async Task<List<RequirementIssue>> AnalyzeRequirementsAsync(
        TaskItem task,
        CancellationToken cancellationToken)
    {
        var provider = providerFactory.Create(AgentName);
        var userMessage = $"""
            ## 原始需求
            {task.Title}

            {task.Description ?? ""}

            請依照格式產出 JSON 陣列，每個 Issue 代表一個可獨立執行的功能或任務。
            """;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var response = await provider.CompleteAsync(BuildSystemPrompt(), userMessage, cancellationToken);
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
