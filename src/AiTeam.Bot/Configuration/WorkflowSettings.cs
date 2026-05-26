namespace AiTeam.Bot.Configuration;

/// <summary>v4-rewrite 後僅剩 Alert 限頻設定（6 Talent / HITL / Petra orchestrator 整套砍 / 對應 flag 全砍）。</summary>
public class WorkflowSettings
{
    /// <summary>DiscordAlertService rate-limit window（分鐘）— per event type per window 只發 1 則 + aggregate 描述。
    /// 預設 5（連續失敗洗版防護）/ 範圍守 [1, 60]。
    /// AppSettings 表 key = "Workflow:AlertRateLimitMinutes"，DB 優先。</summary>
    public int AlertRateLimitMinutes { get; set; } = 5;
}
