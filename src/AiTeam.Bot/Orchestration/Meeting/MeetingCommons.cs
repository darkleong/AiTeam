using AiTeam.Bot.Agents;
using AiTeam.Bot.Services;

namespace AiTeam.Bot.Orchestration.Meeting;

/// <summary>
/// Stage 34：KickoffMeetingService / DesignMeetingService 共用的會議基礎設施。
/// 提供單輪 Agent 對話封裝（RunAgentTurnAsync）與 session 清理記錄（CloseAllSessionsAsync）。
/// Stage 44：RunAgentTurnAsync 新增 meetingType / round / tokenLogService 三個 optional 參數，
/// 提供時將 token 寫入 token_logs 並歸到 AgentName="Meeting-{type}"（會議當獨立 Agent 計，
/// 避免 Petra 個人 token 看起來爆量）。預設 null = 不寫 token（既有行為相容）。
/// Stage 60 (FF 五十五)：三條 silent failure 路徑全改 throw MeetingSubprocessFailureException
/// — Trial_v7 結案揭露 silent failure 真實 root cause 治本。caller（Pipeline KickoffStageExecutor /
/// DesignStageExecutor / legacy ModifyTaskPlanAsync 等）catch 後 fire 第 7 routing
/// agent_api_failure_intervention（agent="Petra-{Stage}"）。
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

            // Stage 60：fail-fast — 三條 swallow 路徑全改 throw MeetingSubprocessFailureException（治本 Trial_v7 silent failure）
            if (!result.Success)
            {
                logger.LogWarning("[Stage60] MeetingCommons subprocess failure → throw MeetingSubprocessFailureException（Agent={Agent}, sessionId={Id}）", agentDisplayName, sessionId);
                throw new MeetingSubprocessFailureException(agentDisplayName, sessionId,
                    $"subprocess !result.Success（output={(result.Output?.Length ?? 0)} chars）");
            }

            if (string.IsNullOrWhiteSpace(result.Output))
            {
                logger.LogWarning("[Stage60] MeetingCommons 空 output → throw MeetingSubprocessFailureException（Agent={Agent}, sessionId={Id}）", agentDisplayName, sessionId);
                throw new MeetingSubprocessFailureException(agentDisplayName, sessionId,
                    "Agent 無回應（subprocess Success=true 但 output 空）");
            }

            return result.Output;
        }
        catch (MeetingSubprocessFailureException)
        {
            // 已是 Stage 60 業務 exception — 直接 re-throw 給上層（Pipeline KickoffStageExecutor / DesignStageExecutor 接 / legacy modify caller 接）
            throw;
        }
        catch (LlmApiFailureException)
        {
            // Stage 58 既有 marker pattern：保留 type 不誤包，由上層 catch path（AgentQueueProcessor / Pipeline Stage Executor）處理
            throw;
        }
        catch (OperationCanceledException)
        {
            // Cancellation 正常傳播（不視為 subprocess failure）
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Stage60] MeetingCommons：{Agent} session 例外 → wrap 成 MeetingSubprocessFailureException（sessionId={Id}）", agentDisplayName, sessionId);
            throw new MeetingSubprocessFailureException(agentDisplayName, sessionId, ex.Message, ex);
        }
    }
}
