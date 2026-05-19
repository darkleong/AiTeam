using AiTeam.Dashboard.Services;
using AiTeam.Shared.Dtos;
using AiTeam.Shared.ViewModels;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Home;

/// <summary>
/// Stage 33：Agent 狀態卡元件（抽元件以便管理 per-card state）。
/// - 子項 A：pause/resume 按鈕。
/// - 子項 B：expand 展開看待辦清單（running + queued TaskItem，資料來自 QueueInfo）。
/// </summary>
public partial class AgentStatusCard
{
    [Parameter, EditorRequired]
    public AgentStatusViewModel Agent { get; set; } = null!;

    [Parameter]
    public AgentQueueDto? QueueInfo { get; set; }

    [Inject] private DashboardBotService  BotService { get; set; } = null!;
    [Inject] private ISnackbar            Snackbar   { get; set; } = null!;
    [Inject] private NavigationManager    Navigation { get; set; } = null!;

    private bool _loading;
    private bool _expanded;

    /// <summary>佇列狀態為 paused / stopping / stopped 視為「暫停中」（顯示恢復按鈕 + 淡黃背景）。</summary>
    private bool IsPaused
        => QueueInfo is not null
           && QueueInfo.AgentState is "paused" or "stopping" or "stopped";

    /// <summary>
    /// 只有 QueueExecutorKeys 中的 Agent（Dev/Reviewer/QA/Doc/Requirements/Designer/Release/Ops）
    /// 才消費佇列，才有 pause/resume/expand 可操作；CEO/PM 等不顯示控制按鈕。
    /// </summary>
    private bool IsKnownExecutor
        => QueueInfo is not null;

    /// <summary>是否有任何待辦（running 或 queued）。</summary>
    private bool HasAnyTodo
        => QueueInfo is not null
           && (QueueInfo.CurrentTaskTitle is not null || QueueInfo.QueuedTasks.Count > 0);

    /// <summary>
    /// Stage 33：Status Badge 顯示策略
    /// - active 或無 QueueInfo：永遠顯示（正常運行情境）
    /// - 非 active + running/error：顯示（讓老闆看到「按了停但手頭還在跑」這類重要組合）
    /// - 非 active + idle：隱藏（「已停止+閒置」兩個負面詞疊加屬於冗餘）
    /// </summary>
    private bool ShouldShowStatusBadge
    {
        get
        {
            if (QueueInfo is null || QueueInfo.AgentState == "active") return true;
            return Agent.Status != "idle";
        }
    }

    // Stage 78c：v4 AgentQueueControlService 整套砍 — pause/resume agent 佇列控制 0 backend 支援
    // Dashboard UI 留 button display 但 click 顯示「功能已砍」snackbar / WebUI Stage 預備重設計
    private void PauseAsync()
    {
        Snackbar.Add("⚠️ Stage 78c：v4 佇列控制已砍 / 留待 WebUI Stage 重設計", Severity.Info);
    }

    private void ResumeAsync()
    {
        Snackbar.Add("⚠️ Stage 78c：v4 佇列控制已砍 / 留待 WebUI Stage 重設計", Severity.Info);
    }

    private void NavigateToGroup(Guid? groupId)
    {
        if (groupId is null) return;
        Navigation.NavigateTo($"/pipeline?groupId={groupId}");
    }

    /// <summary>
    /// 將 utc 時間點轉為相對時間字串（例：3:20、45s、1h 2m）。
    /// 用於待辦清單顯示「已跑」/「等候」時長。
    /// </summary>
    private static string FormatDuration(DateTime? utc)
    {
        if (utc is null) return "—";
        var span = DateTime.UtcNow - utc.Value;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;

        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        if (span.TotalMinutes >= 1)
            return $"{span.Minutes}:{span.Seconds:D2}";
        return $"{span.Seconds}s";
    }

    private static string GetAgentStateLabel(string state) => state switch
    {
        "paused"   => "暫停",
        "stopping" => "停止中",
        "stopped"  => "已停止",
        _          => state
    };

    private static Color GetAgentStateColor(string state) => state switch
    {
        "paused"   => Color.Warning,
        "stopping" => Color.Warning,
        "stopped"  => Color.Error,
        _          => Color.Default
    };
}
