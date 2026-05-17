using AiTeam.Dashboard.Helpers;
using AiTeam.Dashboard.Services;
using AiTeam.Shared.Constants;
using AiTeam.Shared.Dtos;
using AiTeam.Shared.ViewModels;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Tasks;

public partial class PipelineView : IAsyncDisposable
{
    #region Dependencies

    [Inject]
    private DashboardTaskService TaskService { get; set; } = null!;

    [Inject]
    private DashboardBotService BotService { get; set; } = null!;

    [Inject]
    private DashboardCeoCommandService CeoCommandService { get; set; } = null!;

    [Inject]
    private DashboardAppSettingsService AppSettingsService { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    #endregion

    #region Parameters

    [Parameter]
    public TaskGroupDto? Group { get; set; }

    /// <summary>
    /// Group 狀態推算後發生變化時通知父元件（TaskCenter），讓父元件延遲刷新群組列表。
    /// </summary>
    [Parameter]
    public EventCallback<string> OnGroupStatusChanged { get; set; }

    #endregion

    #region Private State

    private List<PipelineStepViewModel> _steps = [];
    private bool _loading;
    private int  _activeStepIndex;
    private bool _pauseBusy;
    // Stage 51：HITL 中途介入按鈕（v4 漸進遷移第三步試點）
    private bool _useFrameworkKickoffMidInterrupt;
    private bool _midInterruptBusy;

    #endregion

    #region Lifecycle

    protected override async Task OnInitializedAsync()
    {
        // Stage 51：載入 HITL 試點 feature flag（決定是否顯示「中途介入」按鈕）
        var setting = await AppSettingsService.GetAsync("Workflow:UseFrameworkKickoffMidInterrupt");
        _useFrameworkKickoffMidInterrupt = bool.TryParse(setting?.Value, out var v) && v;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Group is not null)
            await LoadStepsAsync();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 由 TaskCenter（父元件）收到 SignalR ReceiveTaskUpdate 後直接呼叫，
    /// 避免雙重訂閱同一 HubConnection 導致 dispatch 不觸發的問題。
    /// </summary>
    public async Task HandleTaskUpdateAsync(TaskUpdateViewModel update)
    {
        if (Group is null) return;
        if (update.GroupId.HasValue && update.GroupId.Value != Group.Id) return;

        var step = _steps.FirstOrDefault(s => s.Task.Id == update.TaskId);
        if (step is not null)
        {
            // 找到對應步驟，直接更新狀態
            step.Task.Status = update.Status;
            // Stage 43：needs_intervention 也算終態（task 階段已停，等待 Christ 介入決定）
            if (update.Status is "done" or "failed" or "cancelled" or "skipped" or "needs_intervention")
            {
                step.Task.CompletedAt = DateTime.UtcNow;
                // 步驟完成時同步更新 Group content 欄位（Agent 可能已寫入歸檔報告等資料）
                await RefreshGroupContentAsync();
            }

            // 更新 ActiveIndex 到目前 running 的步驟
            var runningIdx = _steps.FindIndex(s => s.Task.Status == "running");
            if (runningIdx >= 0)
                _activeStepIndex = runningIdx;
        }
        else
        {
            // 找不到 = 流程新建了 TaskItem（新步驟開始），重新從 DB 載入
            await LoadStepsAsync();
        }

        // 根據步驟狀態推算 Group 整體狀態，即時更新 header 徽章（無需額外 DB 查詢）
        if (_steps.Count > 0)
        {
            var prevStatus = Group.Status;

            // Stage 43：優先順序 — failed > needs_intervention > running > done
            if (_steps.Any(s => s.Task.Status == "failed"))
                Group.Status = "failed";
            else if (_steps.Any(s => s.Task.Status == "needs_intervention"))
                Group.Status = "needs_intervention";
            else if (_steps.All(s => s.Task.Status is "done" or "cancelled" or "skipped"))
                Group.Status = "done";
            else if (_steps.Any(s => s.Task.Status == "running"))
                Group.Status = "running";

            // 狀態變化時通知父元件，讓父元件延遲刷新群組列表（等 Bot 寫完 DB）
            if (Group.Status != prevStatus && OnGroupStatusChanged.HasDelegate)
                await OnGroupStatusChanged.InvokeAsync(Group.Status);
        }

        StateHasChanged();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    #endregion

    #region Private Methods

    private async Task LoadStepsAsync()
    {
        if (Group is null) return;

        _loading = true;
        _steps   = [];

        try
        {
            // 循序載入（AppDbContext 為 Scoped，不支援並發查詢）
            var items      = await TaskService.GetTaskItemsByGroupAsync(Group.Id);
            var freshGroup = await TaskService.GetTaskGroupByIdAsync(Group.Id);
            _steps = items.Select(t => new PipelineStepViewModel { Task = t }).ToList();

            // 同步更新 Group 折疊面板欄位（避免開啟 Drawer 後資料停留在快照）
            ApplyGroupContent(freshGroup);

            // 自動定位到正在執行中的步驟
            var runningIdx = _steps.FindIndex(s => s.Task.Status == "running");
            _activeStepIndex = runningIdx >= 0 ? runningIdx : Math.Max(0, _steps.Count - 1);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Pipeline 步驟載入失敗：{ex.Message}", Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>從 DB 重新載入 Group 的所有 content 欄位（折疊面板資料），不重載步驟清單。</summary>
    private async Task RefreshGroupContentAsync()
    {
        if (Group is null) return;
        try
        {
            var freshGroup = await TaskService.GetTaskGroupByIdAsync(Group.Id);
            ApplyGroupContent(freshGroup);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"群組內容更新失敗：{ex.Message}", Severity.Error);
        }
    }

    /// <summary>將 freshGroup 的 content 欄位同步回 Group Parameter（集中管理，避免漏欄位）。</summary>
    private void ApplyGroupContent(TaskGroupDto? freshGroup)
    {
        if (Group is null || freshGroup is null) return;
        Group.Status             = freshGroup.Status;
        Group.KickoffMeetingLog  = freshGroup.KickoffMeetingLog;
        Group.TaskPlan           = freshGroup.TaskPlan;
        Group.KickoffRound       = freshGroup.KickoffRound;
        Group.DesignMeetingLog   = freshGroup.DesignMeetingLog;
        Group.DesignPlan         = freshGroup.DesignPlan;
        Group.DesignRound        = freshGroup.DesignRound;
        Group.DevPrUrl           = freshGroup.DevPrUrl;
        Group.DevPlan            = freshGroup.DevPlan;
        Group.LastReviewBody     = freshGroup.LastReviewBody;
        Group.TestReport         = freshGroup.TestReport;
        Group.ArchiveContent     = freshGroup.ArchiveContent;
        // Stage 31：Appeal 對抗紀錄
        Group.ReviewAppealLog    = freshGroup.ReviewAppealLog;
        Group.ReviewAppealRoundA = freshGroup.ReviewAppealRoundA;
        Group.DevPlanAppealLog   = freshGroup.DevPlanAppealLog;
        Group.DevPlanAppealRoundA = freshGroup.DevPlanAppealRoundA;
        // Stage 45：TaskGroup 流程暫停
        Group.IsPaused           = freshGroup.IsPaused;
        Group.PausedAt           = freshGroup.PausedAt;
        Group.PausedBy           = freshGroup.PausedBy;
    }

    /// <summary>Stage 45：暫停 TaskGroup（Dashboard 操作）。</summary>
    private async Task HandlePauseClickAsync()
    {
        if (Group is null || _pauseBusy) return;
        _pauseBusy = true;
        try
        {
            var ok = await BotService.PauseTaskGroupAsync(Group.Id);
            Snackbar.Add(ok ? "已暫停下階段啟動，當前階段跑完不會轉下階段" : "暫停指令送出失敗",
                ok ? Severity.Success : Severity.Error);
            if (ok)
            {
                // 樂觀更新（fresh read 由 SignalR / 重新載入觸發）
                Group.IsPaused = true;
                Group.PausedAt = DateTime.UtcNow;
                Group.PausedBy = "Dashboard";
            }
        }
        finally { _pauseBusy = false; }
    }

    /// <summary>Stage 45：恢復暫停的 TaskGroup（Dashboard 操作）。</summary>
    private async Task HandleResumeClickAsync()
    {
        if (Group is null || _pauseBusy) return;
        _pauseBusy = true;
        try
        {
            var ok = await BotService.ResumeTaskGroupAsync(Group.Id);
            Snackbar.Add(ok ? "已送出恢復指令，下階段即將啟動" : "恢復指令送出失敗",
                ok ? Severity.Success : Severity.Error);
            if (ok)
            {
                Group.IsPaused = false;
                Group.PausedAt = null;
                Group.PausedBy = null;
            }
        }
        finally { _pauseBusy = false; }
    }

    /// <summary>Stage 61-FF 四十：暫停整個 epic（Dashboard 操作）— sub-task 不再啟動下個 Phase。</summary>
    private async Task HandlePauseEpicClickAsync()
    {
        if (Group is null || _pauseBusy) return;
        _pauseBusy = true;
        try
        {
            var ok = await BotService.PauseEpicAsync(Group.Id);
            Snackbar.Add(ok ? "Epic 已暫停 — sub-task 不再啟動下個 Phase" : "暫停 Epic 失敗",
                ok ? Severity.Success : Severity.Error);
            if (ok) Group.EpicPaused = true;
        }
        finally { _pauseBusy = false; }
    }

    /// <summary>Stage 61-FF 四十：恢復 epic（Dashboard 操作）— 觸發下個 pending sub-task fire Dev_plan。</summary>
    private async Task HandleResumeEpicClickAsync()
    {
        if (Group is null || _pauseBusy) return;
        _pauseBusy = true;
        try
        {
            var ok = await BotService.ResumeEpicAsync(Group.Id);
            Snackbar.Add(ok ? "Epic 已恢復 — 觸發下個 pending sub-task" : "恢復 Epic 失敗",
                ok ? Severity.Success : Severity.Error);
            if (ok) Group.EpicPaused = false;
        }
        finally { _pauseBusy = false; }
    }

    /// <summary>Stage 51：framework HITL 中途介入按鈕（v4 漸進遷移第三步試點）— Christ 觸發 trigger flag，
    /// 下個 Petra Round 邊界 MidInterruptCheckExecutor emit RequestInfoEvent 開 BossInteraction。</summary>
    private async Task HandleMidInterruptClickAsync()
    {
        if (Group is null || _midInterruptBusy) return;
        _midInterruptBusy = true;
        try
        {
            var (ok, err) = await CeoCommandService.TriggerKickoffMidInterruptAsync(Group.Id);
            Snackbar.Add(
                ok
                    ? "✏️ 中途介入旗標已送，下個 Petra Round 邊界會收到 Discord/Dashboard 介入卡"
                    : $"中途介入觸發失敗：{err ?? "未知錯誤"}",
                ok ? Severity.Success : Severity.Error);
        }
        finally { _midInterruptBusy = false; }
    }

    /// <summary>Stage 51：是否可顯示「中途介入」按鈕（feature flag 開 + Kickoff 步驟 running）。</summary>
    private bool CanShowMidInterruptButton =>
        _useFrameworkKickoffMidInterrupt
        && _steps.Any(s => s.Task.AssignedAgent == AgentNames.Kickoff && s.Task.Status == "running");

    private async Task HandleRequeueAsync(Guid taskId)
    {
        var success = await BotService.RequeueTaskAsync(taskId);
        Snackbar.Add(
            success ? "任務已重新入佇列" : "重新入佇列失敗，請稍後再試",
            success ? Severity.Success : Severity.Error);
    }

    private async Task LoadLogsAsync(PipelineStepViewModel step)
    {
        try
        {
            step.Logs       = await TaskService.GetTaskLogsAsync(step.Task.Id);
            step.LogsLoaded = true;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"任務記錄載入失敗：{ex.Message}", Severity.Error);
        }
        StateHasChanged();
    }

    private string GetStepTitle(PipelineStepViewModel step)
    {
        var agent = step.Task.AssignedAgent;
        // Petra 打回標記
        if (Group is not null && agent == "Petra")
        {
            if (Group.DevPlanRevision > 0)
                return $"Petra（計劃修正 ×{Group.DevPlanRevision}）";
            if (Group.FixIteration > 0)
                return $"Petra（打回 ×{Group.FixIteration}）";
        }
        // Stage 26：Kickoff / Design 步驟顯示中文名
        return agent switch
        {
            AgentNames.Kickoff => "Kick-off 會議",
            AgentNames.Design  => "設計規劃",
            _                  => agent
        };
    }

    private static string FormatDuration(TaskItemDto task)
    {
        if (task.Status == "running")
        {
            var elapsed = DateTime.UtcNow - task.CreatedAt;
            return $"進行中（{(int)elapsed.TotalMinutes} 分）";
        }

        if (task.Duration is null) return "";

        var ts = task.Duration.Value;
        if (ts.TotalSeconds < 60) return $"{(int)ts.TotalSeconds} 秒";
        if (ts.TotalMinutes < 60)
            return ts.Seconds > 0 ? $"{(int)ts.TotalMinutes} 分 {ts.Seconds} 秒" : $"{(int)ts.TotalMinutes} 分";
        return ts.Minutes > 0 ? $"{(int)ts.TotalHours} 時 {ts.Minutes} 分" : $"{(int)ts.TotalHours} 時";
    }

    private static bool IsCompleted(string status)  => status is "done" or "skipped";
    private static bool IsFailed(string status)      => status is "failed" or "error";
    private static bool IsRevision(string status)    => status is "revision" or "reviewing" or "needs_intervention";

    private static Color GetLogColor(string status) => status switch
    {
        "done"               => Color.Success,
        "failed" or "error"  => Color.Error,
        "running"            => Color.Info,
        "revision"           => Color.Warning,
        "reviewing"          => Color.Warning,
        "skipped"            => Color.Tertiary,
        "needs_intervention" => Color.Warning,  // Stage 43：amber
        _                    => Color.Default
    };

    private string WorkflowTypeLabel => Group?.WorkflowType switch
    {
        "new_feature"      => "新功能",
        "bug_fix"          => "Bug Fix",
        "tech_improvement" => "技術改善",
        _                  => Group?.WorkflowType ?? ""
    };

    private string PrNumberText => PrNumberHelper.ExtractPrNumber(Group?.DevPrUrl);

    #endregion
}

/// <summary>Pipeline View 單一步驟 ViewModel（不放 Shared，只在 Dashboard 使用）。</summary>
public class PipelineStepViewModel
{
    public TaskItemDto    Task       { get; set; } = null!;
    public List<TaskLogDto> Logs     { get; set; } = [];
    public bool           LogsLoaded { get; set; }
}
