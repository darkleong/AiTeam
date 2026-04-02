using System.Text.Json;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord;
using AiTeam.Bot.GitHub;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Agents;

/// <summary>
/// CEO Agent 核心邏輯：組建 Prompt、呼叫 LLM、解析 JSON 回應。
/// Stage 9：加入智慧分類（Bug / 新功能 / 正常行為 / 疑問）、提案模式（propose action）。
/// </summary>
public class CeoAgentService(
    LlmProviderFactory providerFactory,
    TaskRepository taskRepository,
    GitHubService gitHubService,
    IOptions<GitHubSettings> gitHubSettings,
    ILogger<CeoAgentService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly GitHubSettings _github = gitHubSettings.Value;

    /// <summary>
    /// 處理使用者輸入，回傳 CEO 的分析結果。
    /// 可選傳入圖片附件（如 Discord 截圖）與對話歷史（多輪自然語言對話用）。
    /// </summary>
    public async Task<CeoResponse> ProcessAsync(
        string userInput,
        string projectName,
        IReadOnlyList<AgentDescriptor> agentList,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ImageAttachment>? images = null,
        IReadOnlyList<ConversationTurn>? history = null)
    {
        var provider = providerFactory.Create("CEO");

        var systemPrompt = BuildSystemPrompt(agentList, rules);
        var userMessage  = await BuildUserMessageAsync(userInput, projectName, history, cancellationToken);

        // 最多重試一次（回應格式錯誤時）
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var response = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken, images);

            var parsed = TryParseResponse(response.Content);
            if (parsed is not null)
            {
                logger.LogInformation(
                    "CEO 回應解析成功（第 {Attempt} 次），action={Action} target={Agent} require_confirmation={Confirm} InputTokens={Input} OutputTokens={Output}",
                    attempt, parsed.Action, parsed.TargetAgent, parsed.RequireConfirmation, response.InputTokens, response.OutputTokens);
                return parsed;
            }

            logger.LogWarning("CEO 回應格式錯誤（第 {Attempt} 次），原始內容：{Content}", attempt, response.Content);
        }

        // 兩次都失敗，回傳通知訊息
        return new CeoResponse { Reply = "CEO 回應格式錯誤，請查看 log 或稍後再試。" };
    }

    private static string BuildSystemPrompt(IReadOnlyList<AgentDescriptor> agentList, IReadOnlyList<string> rules)
    {
        var agents = string.Join("\n", agentList.Select(a =>
            string.IsNullOrWhiteSpace(a.Description) ? $"- {a.Name}" : $"- {a.Name}：{a.Description}"));
        var ruleList = rules.Count > 0
            ? string.Join("\n", rules.Select(r => $"- {r}"))
            : "（尚無規則）";

        return $$"""
            你是 AI 團隊的 CEO Victoria，負責接收老闆指令、分析任務、分派給對應的 Agent。
            老闆會用自然語言直接對你說話，不一定使用固定格式。

            ## 可用 Agent
            {{agents}}

            ## 規則清單
            {{ruleList}}

            ## 第一步：智慧分類（每次回應前必須執行）
            在回應之前，先根據老闆的輸入與提供的系統上下文（GitHub PR/Issue 數量、近期任務記錄）進行分類：

            | 分類 | 判斷標準 | 行動 |
            |------|---------|------|
            | 新功能 | 目前沒有相關實作，是全新需求 | action = "propose"（先提案，不直接派工）|
            | Bug | 行為不符合預期，系統有異常 | action = "delegate"，找 Dev 修復 |
            | 正常行為 | 行為符合設計，老闆不了解系統運作 | action = "reply"，解釋清楚 |
            | 疑問 | 老闆在問問題、請求解釋 | action = "reply"，直接回答 |

            在 reply 欄位的開頭**說明分類結果與理由**（一句話），例如：
            「Christ，這是一個新功能需求，因為目前系統中尚未有相關實作。」
            「Christ，這應該是個 Bug，登入流程不應該發生這個錯誤。」
            「Christ，這是正常行為，Vera 找不到 PR 是因為此 Project 目前沒有任何 open PR。」

            ## propose 模式（新功能專用）
            當判斷為新功能時，使用 action = "propose"：
            - 若資訊充足，直接進入提案
            - 若資訊不足，先用 action = "reply" 問一個關鍵問題再繼續

            ## action 欄位規則（非常重要）
            - 老闆問問題、閒聊、或只需要你說明 → action = "reply"，target_agent = null
            - 老闆要求執行 Bug 修復或明確的現有功能維護 → action = "delegate"，target_agent = 對應 Agent 名稱
            - 老闆提出新功能需求 → action = "propose"，target_agent = null
            - 只要你打算派任務給任何 Agent，action 就必須是 "delegate"，不得使用 "reply"
            - 禁止在 reply 欄位描述「已分派給 X 處理」卻把 action 設為 "reply"

            ## 反問機制（非常重要）
            - 當老闆提供的資訊不足以確定要做什麼或針對哪個專案時，使用 action = "reply" 反問
            - 每次只問一個最關鍵的問題，不可以一次問多個問題
            - 提供目前可用的選項供老闆快速回答（例如列出現有專案名稱）
            - 禁止猜測老闆的意圖，寧可反問也不要猜錯

            ## 回應格式
            你必須只回傳以下 JSON 格式，不得包含任何其他文字：
            {
              "reply": "給老闆看的回應訊息（繁體中文，開頭說明分類與理由）",
              "action": "reply | delegate | propose",
              "target_agent": "Dev | Ops | QA | Doc | Requirements | Reviewer | Release | Designer | null",
              "task": {
                "title": "任務標題",
                "project": "專案名稱",
                "description": "詳細描述",
                "priority": "low | normal | high | critical"
              },
              "require_confirmation": true
            }
            """;
    }

    private async Task<string> BuildUserMessageAsync(
        string userInput,
        string projectName,
        IReadOnlyList<ConversationTurn>? history,
        CancellationToken cancellationToken)
    {
        var recentTasks = await taskRepository.GetRecentByProjectAsync(projectName, limit: 5, cancellationToken);
        var taskHistory = recentTasks.Count > 0
            ? string.Join("\n", recentTasks.Select(t => $"- [{t.Status}] {t.Title}（{t.AssignedAgent}）"))
            : "（無近期任務紀錄）";

        // 查詢 GitHub PR / Issue 上下文（供 CEO 分類判斷用）
        var repo = string.IsNullOrWhiteSpace(projectName) ? _github.DefaultRepo : projectName;
        var githubContext = await BuildGitHubContextAsync(_github.Owner, repo, cancellationToken);

        // 若有對話歷史，插入在指令前面讓 CEO 知道上下文
        var historyBlock = "";
        if (history is { Count: > 0 })
        {
            var turns = string.Join("\n", history.Select(t =>
                t.Role == "user" ? $"老闆：{t.Content}" : $"CEO：{t.Content}"));
            historyBlock = $"""

                ## 對話歷史（最近幾輪）
                {turns}

                """;
        }

        return $"""
            ## 當前專案
            {projectName}

            ## 近期相關任務紀錄
            {taskHistory}

            ## GitHub 系統上下文（供分類判斷使用）
            {githubContext}
            {historyBlock}
            ## 老闆指令
            {userInput}
            """;
    }

    private async Task<string> BuildGitHubContextAsync(
        string owner, string repo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            return "（GitHub 設定未完整，無法取得 PR/Issue 資訊）";

        try
        {
            var prsTask    = gitHubService.ListOpenPullRequestsAsync(owner, repo);
            var issuesTask = gitHubService.ListOpenIssuesAsync(owner, repo);
            await Task.WhenAll(prsTask, issuesTask);

            var prs    = prsTask.Result;
            var issues = issuesTask.Result;

            var prList = prs.Count > 0
                ? string.Join("\n", prs.Take(10).Select(p => $"  - PR #{p.Number}：{p.Title}"))
                : "  （無 open PR）";
            var issueList = issues.Count > 0
                ? string.Join("\n", issues.Take(10).Select(i => $"  - Issue #{i.Number}：{i.Title}"))
                : "  （無 open Issue）";

            return $"""
                Repo：{owner}/{repo}
                Open PR（共 {prs.Count} 筆）：
                {prList}
                Open Issue（共 {issues.Count} 筆）：
                {issueList}
                """;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "取得 GitHub 上下文失敗");
            return "（GitHub 上下文取得失敗）";
        }
    }

    private static CeoResponse? TryParseResponse(string content)
    {
        try
        {
            // 嘗試從回應中萃取 JSON（有時 LLM 會在 JSON 前後附加說明文字）
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;

            var json = content[start..(end + 1)];
            return JsonSerializer.Deserialize<CeoResponse>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
