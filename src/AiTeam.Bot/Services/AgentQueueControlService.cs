using AiTeam.Shared.Constants;

namespace AiTeam.Bot.Services;

/// <summary>
/// Stage 33：佇列控制 shared service，供 Discord 指令（/pause, /resume, /stop-all, /resume-all）
/// 與 Dashboard Internal API 共用。
/// 原本集中在 CommandHandler.Handle{Pause,Resume,StopAll,ResumeAll}CommandAsync，抽離後 Dashboard
/// 卡片也能直接觸發，實現 FF 十五「Dashboard 與 Discord 功能平等 — 佇列控制子項」。
/// 同時為 FF 二十-B（CommandHandler 拆解）積少成多。
/// </summary>
public class AgentQueueControlService(
    AppSettingsService appSettings,
    DashboardPushService dashboardPush,
    ILogger<AgentQueueControlService> logger)
{
    /// <summary>
    /// 可被暫停 / 恢復的 Agent 清單，對齊 CommandHandler 原行為（消費佇列的 8 個 Agent）。
    /// </summary>
    public static readonly string[] QueueExecutorKeys =
    [
        AgentNames.Dev, AgentNames.Reviewer, AgentNames.Qa, AgentNames.Doc,
        AgentNames.Requirements, AgentNames.Designer, AgentNames.Release, AgentNames.Ops
    ];

    /// <summary>暫停指定 Agent 的佇列消費；正在執行的任務不受影響。</summary>
    public async Task<(bool ok, string message)> PauseAgentAsync(string agent, CancellationToken ct = default)
    {
        if (!IsValidAgent(agent))
            return (false, $"❌ 未知的 Agent：`{agent}`。可用 Agent：{string.Join(", ", QueueExecutorKeys)}。");

        await appSettings.SetAsync($"AgentState:{agent}", "paused", ct);
        _ = dashboardPush.PushQueueUpdateAsync();
        logger.LogInformation("AgentState:{Agent} 已設為 paused", agent);

        return (true, $"⏸️ **{agent}** 已暫停佇列消費，正在執行的任務不受影響。\n使用 `/resume agent:{agent}` 恢復。");
    }

    /// <summary>恢復指定 Agent 的佇列消費。</summary>
    public async Task<(bool ok, string message)> ResumeAgentAsync(string agent, CancellationToken ct = default)
    {
        if (!IsValidAgent(agent))
            return (false, $"❌ 未知的 Agent：`{agent}`。可用 Agent：{string.Join(", ", QueueExecutorKeys)}。");

        await appSettings.SetAsync($"AgentState:{agent}", "active", ct);
        _ = dashboardPush.PushQueueUpdateAsync();
        logger.LogInformation("AgentState:{Agent} 已設為 active", agent);

        return (true, $"▶️ **{agent}** 已恢復佇列消費。");
    }

    /// <summary>緊急停止所有 Agent：全部設為 stopping，完成手頭任務後將自動進入 stopped。</summary>
    public async Task<(bool ok, string message)> StopAllAsync(CancellationToken ct = default)
    {
        foreach (var key in QueueExecutorKeys)
            await appSettings.SetAsync($"AgentState:{key}", "stopping", ct);

        _ = dashboardPush.PushQueueUpdateAsync();
        logger.LogInformation("所有 Agent 已進入 stopping 狀態");

        return (true,
            "🛑 所有 Agent 已進入 **Stopping** 狀態，完成手頭任務後將自動停止。\n" +
            "使用 `/resume` 指定個別 Agent 恢復，或 `/resume-all` 全部恢復。");
    }

    /// <summary>恢復所有 Agent 的佇列消費。</summary>
    public async Task<(bool ok, string message)> ResumeAllAsync(CancellationToken ct = default)
    {
        foreach (var key in QueueExecutorKeys)
            await appSettings.SetAsync($"AgentState:{key}", "active", ct);

        _ = dashboardPush.PushQueueUpdateAsync();
        logger.LogInformation("所有 Agent 已恢復 active 狀態");

        return (true, "▶️ 所有 Agent 已恢復佇列消費。");
    }

    private static bool IsValidAgent(string agent)
        => QueueExecutorKeys.Contains(agent);
}
