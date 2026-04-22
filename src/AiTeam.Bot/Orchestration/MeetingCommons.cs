using AiTeam.Bot.Agents;

namespace AiTeam.Bot.Orchestration;

/// <summary>
/// Stage 34：KickoffMeetingService / DesignMeetingService 共用的會議基礎設施。
/// 提供單輪 Agent 對話封裝（RunAgentTurnAsync）與 session 清理記錄（CloseAllSessionsAsync）。
/// </summary>
public class MeetingCommons(
    IClaudeCodeService claudeCode,
    ILogger<MeetingCommons> logger)
{
    internal static readonly string[] ReadOnlyTools = ["Glob", "Grep", "Read"];

    /// <summary>
    /// Christ 確認（繼續/停止）後呼叫，記錄 Petra session 已完成。
    /// Claude Code session 資料由本機自行管理，不需要主動刪除。
    /// </summary>
    public Task CloseAllSessionsAsync(Guid groupId)
    {
        logger.LogInformation("MeetingCommons：Petra session 關閉（groupId={Id}，sessionId={SessionId}）",
            groupId, groupId.ToString());
        return Task.CompletedTask;
    }

    /// <summary>執行單輪 Agent Claude Code session，回傳該輪輸出文字。</summary>
    internal async Task<string> RunAgentTurnAsync(
        string agentDisplayName,
        string sessionId,
        string prompt,
        string model,
        string apiKey,
        bool isFirstMessage,
        string workingDir,
        string[]? allowedTools,
        CancellationToken ct,
        int maxTurns = 12)
    {
        try
        {
            var result = await claudeCode.RunMeetingSessionAsync(
                workingDir, sessionId, prompt, model, apiKey,
                isFirstMessage, maxTurns, allowedTools, ct);

            if (!result.Success)
                logger.LogWarning("MeetingCommons：{Agent} session 執行失敗（sessionId={Id}）", agentDisplayName, sessionId);

            return string.IsNullOrWhiteSpace(result.Output)
                ? $"（{agentDisplayName} 無回應）"
                : result.Output;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MeetingCommons：{Agent} session 例外（sessionId={Id}）", agentDisplayName, sessionId);
            return $"（{agentDisplayName} 執行失敗：{ex.Message}）";
        }
    }
}
