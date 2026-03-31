using System.Text.Json;
using AiTeam.Data;
using AiTeam.Data.Repositories;

namespace AiTeam.Bot.Agents;

/// <summary>
/// CEO Agent 核心邏輯：組建 Prompt、呼叫 LLM、解析 JSON 回應。
/// </summary>
public class CeoAgentService(
    LlmProviderFactory providerFactory,
    TaskRepository taskRepository,
    ILogger<CeoAgentService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 處理使用者輸入，回傳 CEO 的分析結果。
    /// 可選傳入圖片附件（如 Discord 截圖），模型將一併分析圖片內容。
    /// </summary>
    public async Task<CeoResponse> ProcessAsync(
        string userInput,
        string projectName,
        IReadOnlyList<AgentDescriptor> agentList,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        var provider = providerFactory.Create("CEO");

        var systemPrompt = BuildSystemPrompt(agentList, rules);
        var userMessage = await BuildUserMessageAsync(userInput, projectName, cancellationToken);

        // 最多重試一次（回應格式錯誤時）
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var response = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken, images);

            var parsed = TryParseResponse(response.Content);
            if (parsed is not null)
            {
                logger.LogInformation(
                    "CEO 回應解析成功（第 {Attempt} 次），InputTokens={Input} OutputTokens={Output}",
                    attempt, response.InputTokens, response.OutputTokens);
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
            你是 AI 團隊的 CEO，負責接收老闆指令、分析任務、分派給對應的 Agent。

            ## 可用 Agent
            {{agents}}

            ## 規則清單
            {{ruleList}}

            ## 回應格式
            你必須只回傳以下 JSON 格式，不得包含任何其他文字：
            {
              "reply": "給老闆看的回應訊息（繁體中文）",
              "action": "reply | delegate | autonomous",
              "target_agent": "Dev | Ops | QA | Doc | Requirements | null",
              "task": {
                "title": "任務標題",
                "project": "專案名稱",
                "description": "詳細描述",
                "priority": "low | normal | high | critical"
              },
              "require_confirmation": true
            }

            action 說明：
            - reply：僅回覆，不需要 Agent 執行
            - delegate：分派給 target_agent 執行
            - autonomous：你可自主執行的清單中的任務
            """;
    }

    private async Task<string> BuildUserMessageAsync(
        string userInput,
        string projectName,
        CancellationToken cancellationToken)
    {
        var recentTasks = await taskRepository.GetRecentByProjectAsync(projectName, limit: 5, cancellationToken);
        var taskHistory = recentTasks.Count > 0
            ? string.Join("\n", recentTasks.Select(t => $"- [{t.Status}] {t.Title}（{t.AssignedAgent}）"))
            : "（無近期任務紀錄）";

        return $"""
            ## 當前專案
            {projectName}

            ## 近期相關任務紀錄
            {taskHistory}

            ## 老闆指令
            {userInput}
            """;
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
