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

    private async Task PauseAsync()
    {
        _loading = true;
        var ok = await BotService.PauseAgentAsync(Agent.AgentName);
        _loading = false;

        if (ok)
            Snackbar.Add($"⏸️ {Agent.AgentName} 已暫停佇列消費", Severity.Info);
        else
            Snackbar.Add($"送出暫停指令失敗（{Agent.AgentName}），請確認 Bot 服務正常", Severity.Error);
    }

    private async Task ResumeAsync()
    {
        _loading = true;
        var ok = await BotService.ResumeAgentAsync(Agent.AgentName);
        _loading = false;

        if (ok)
            Snackbar.Add($"▶️ {Agent.AgentName} 已恢復佇列消費", Severity.Success);
        else
            Snackbar.Add($"送出恢復指令失敗（{Agent.AgentName}），請確認 Bot 服務正常", Severity.Error);
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
