namespace AiTeam.Bot.Agents;

/// <summary>
/// Stage 60 (FF 五十五)：Meeting subprocess（Kickoff / Design / 任何 MeetingCommons.RunAgentTurnAsync 呼叫站）
/// 不可恢復失敗的業務 exception。對齊 Stage 58 LlmApiFailureException 設計精神。
///
/// 治本範圍 — Trial_v7 結案揭露 silent failure 真實 root cause：
/// MeetingCommons.RunAgentTurnAsync 三條 swallow 路徑（subprocess !result.Success / 空 output placeholder /
/// catch Exception 包成「（Agent 無回應）」/「（Agent 執行失敗：...）」placeholder string）統一改 throw 此 exception。
/// 上層 catch 路徑：
///   - Pipeline KickoffStageExecutor / DesignStageExecutor 在 sync await router 外圍 catch → fire
///     agent_api_failure_intervention BossInteraction（agent="Petra-Kickoff" / "Petra-Design"）+ SendMessage
///     Kickoff/DesignAgentApiFailureRequest yield 等 Christ 真三選（continue / retry / abort）
///   - Legacy KickoffMeetingService.ModifyTaskPlanAsync / DesignMeetingService.ModifyDesignPlanAsync re-throw 給上層
///
/// LlmApiFailureException 不被本 exception 吞 — MeetingCommons catch 內先檢測 LlmApiFailureException 直接 re-throw
/// 保留 Stage 58 既有 marker pattern 不誤包（兩 exception 同走「Agent 不可恢復失敗」第 7 routing）。
/// </summary>
public sealed class MeetingSubprocessFailureException : Exception
{
    public string AgentDisplayName { get; }

    public string SessionId { get; }

    /// <summary>原始錯誤訊息（capped 500 chars）— subprocess !result.Success 時取 result 文字 / 空 output 時為 placeholder 描述 / catch Exception 時取 inner.Message。</summary>
    public string RawError { get; }

    public MeetingSubprocessFailureException(string agentDisplayName, string sessionId, string rawError, Exception? innerException = null)
        : base($"Meeting subprocess failure ({agentDisplayName}, sessionId={sessionId}): {Truncate(rawError, 500)}", innerException)
    {
        AgentDisplayName = agentDisplayName;
        SessionId = sessionId;
        RawError = Truncate(rawError, 500);
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? "" : (s.Length > max ? s[..max] : s);
}
