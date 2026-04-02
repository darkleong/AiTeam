using System.Text.Json;
using AiTeam.Bot.Discord;
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

            ## action 欄位規則（非常重要）
            - 老闆問問題、閒聊、或只需要你說明 → action = "reply"，target_agent = null
            - 老闆要求執行任何工作（包括修改程式、修 bug、寫文件、測試、需求分析等）→ action = "delegate"，target_agent = 對應 Agent 名稱
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
              "reply": "給老闆看的回應訊息（繁體中文）",
              "action": "reply | delegate | autonomous",
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
            {historyBlock}
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
