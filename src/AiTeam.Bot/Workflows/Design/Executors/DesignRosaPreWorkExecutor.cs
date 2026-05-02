using System.Text.Json;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Design.Executors;

/// <summary>
/// Stage 52：前置作業 — Rosa Issues + GitHub Issue 建立 Executor（v4 漸進遷移第四步）。
///
/// 職責：
///   - 接 DesignPreWorkBridge phase="after_judge"
///   - call MeetingCommons.RunAgentTurnAsync 跑 Rosa pre-work prompt（isFirstMessage:true，新 sessionId，maxTurns:25）
///   - 解析 Issues + 建立 GitHub Issue → 寫進 state.IssuesJson / IssueUrls + append meeting log
///   - SendMessageAsync(DesignPreWorkBridge phase="after_rosa") 推進 DemiPreWork
///
/// 議題 G1 拍板：GitHub Issue 失敗沿用 legacy line 471-480 try-catch + LogWarning 行為（不做冪等保護，立 FF 觀察）。
/// </summary>
[SendsMessage(typeof(DesignPreWorkBridge))]
internal sealed partial class DesignRosaPreWorkExecutor : Executor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DesignRosaPreWorkExecutor> _logger;

    public DesignRosaPreWorkExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<DesignRosaPreWorkExecutor> logger)
        : base("Design-RosaPreWork")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [MessageHandler]
    private async ValueTask HandleAsync(DesignPreWorkBridge bridge, IWorkflowContext context)
    {
        var state = await DesignStateHelpers.ReadAsync(context);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp        = scope.ServiceProvider;
        var commons   = sp.GetRequiredService<MeetingCommons>();
        var tokenLog  = sp.GetRequiredService<TokenLogService>();
        var config    = sp.GetRequiredService<IConfiguration>();
        var ghService = sp.GetRequiredService<GitHubService>();

        var apiKey = config["AITEAM_ANTHROPIC_KEY"] ?? config["Anthropic:ApiKey"] ?? "";
        var model  = config["Agents:Requirements:Model"] ?? config["Anthropic:DefaultModel"] ?? "claude-sonnet-4-6";

        var output = await commons.RunAgentTurnAsync(
            "Rosa", state.RosaSessionId,
            DesignPrompts.BuildDesignRosaPreWorkPrompt(state.TaskPlan),
            model, apiKey,
            isFirstMessage: true,
            workingDir: state.WorkingDir,
            allowedTools: MeetingCommons.ReadOnlyTools,
            ct: default,
            maxTurns: 25,
            meetingType: "Design",
            round: 0,
            tokenLogService: tokenLog);

        // 解析 Issues 並建立 GitHub Issue（議題 G1 沿用 legacy 行為）
        var parsedIssues = DesignPrompts.TryParseDesignIssues(output);
        if (parsedIssues is { Count: > 0 })
        {
            state.IssuesJson = JsonSerializer.Serialize(parsedIssues);
            var issueUrlList = new List<string>();
            foreach (var issue in parsedIssues)
            {
                try
                {
                    var url = await ghService.CreateIssueAsync(state.Owner, state.Repo, issue.Title, issue.Body, issue.Labels);
                    issueUrlList.Add(url);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Stage52] Rosa 建立 GitHub Issue 失敗：{Title}", issue.Title);
                }
            }
            if (issueUrlList.Count > 0)
                state.IssueUrls = JsonSerializer.Serialize(issueUrlList);
        }
        else
        {
            _logger.LogWarning(
                "[Stage52] Rosa Issues 無法解析，跳過 GitHub Issue 建立（GroupId={Id}）",
                state.GroupId);
        }

        state.MeetingLog +=
            "### Rosa — GitHub Issues\n" + output + "\n\n";
        await DesignStateHelpers.SaveAsync(context, state);

        _logger.LogInformation(
            "[Stage52] Rosa pre-work 完成（GroupId={Id}，issues={Count}）",
            state.GroupId, parsedIssues?.Count ?? 0);

        await context.SendMessageAsync(new DesignPreWorkBridge(state.GroupId, "after_rosa"));
    }
}
