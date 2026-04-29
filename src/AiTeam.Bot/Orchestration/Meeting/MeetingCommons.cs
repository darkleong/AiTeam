using AiTeam.Bot.Agents;
using AiTeam.Bot.Services;

namespace AiTeam.Bot.Orchestration.Meeting;

/// <summary>
/// Stage 34：KickoffMeetingService / DesignMeetingService 共用的會議基礎設施。
/// 提供單輪 Agent 對話封裝（RunAgentTurnAsync）與 session 清理記錄（CloseAllSessionsAsync）。
/// Stage 44：RunAgentTurnAsync 新增 meetingType / round / tokenLogService 三個 optional 參數，
/// 提供時將 token 寫入 token_logs 並歸到 AgentName="Meeting-{type}"（會議當獨立 Agent 計，
/// 避免 Petra 個人 token 看起來爆量）。預設 null = 不寫 token（既有行為相容）。
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

    /// <summary>
    /// 執行單輪 Agent Claude Code session，回傳該輪輸出文字。
    /// Stage 44：meetingType 提供時（"Kickoff" / "Design"），token 整段歸到 AgentName="Meeting-{type}"。
    /// </summary>
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
        int maxTurns = 12,
        string? meetingType = null,
        int? round = null,
        TokenLogService? tokenLogService = null)
    {
        try
        {
            var result = await claudeCode.RunMeetingSessionAsync(
                workingDir, sessionId, prompt, model, apiKey,
                isFirstMessage, maxTurns, allowedTools, ct);

            // Stage 44：會議型 token 整段歸到 Meeting-{type}（個別 Agent 名 agentDisplayName 仍保留 log 用）
            if (meetingType is not null && tokenLogService is not null)
            {
                await tokenLogService.LogCliUsageAsync(
                    $"Meeting-{meetingType}", model, meetingType, round, taskId: null, result.Usage, ct);
            }

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
