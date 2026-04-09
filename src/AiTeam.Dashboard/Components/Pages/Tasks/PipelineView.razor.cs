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

    #endregion

    #region Parameters

    [Parameter]
    public TaskGroupDto? Group { get; set; }

    #endregion

    #region Private State

    private List<PipelineStepViewModel> _steps = [];
    private bool _loading;
    private int  _activeStepIndex;

    #endregion

    #region Lifecycle

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
            if (update.Status is "done" or "failed" or "cancelled")
                step.Task.CompletedAt = DateTime.UtcNow;

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
            if (_steps.Any(s => s.Task.Status == "failed"))
                Group.Status = "failed";
            else if (_steps.All(s => s.Task.Status is "done" or "cancelled"))
                Group.Status = "done";
            else if (_steps.Any(s => s.Task.Status == "running"))
                Group.Status = "running";
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

        var items = await TaskService.GetTaskItemsByGroupAsync(Group.Id);
        _steps    = items.Select(t => new PipelineStepViewModel { Task = t }).ToList();

        // 自動定位到正在執行中的步驟
        var runningIdx = _steps.FindIndex(s => s.Task.Status == "running");
        _activeStepIndex = runningIdx >= 0 ? runningIdx : Math.Max(0, _steps.Count - 1);

        _loading = false;
    }

    private async Task LoadLogsAsync(PipelineStepViewModel step)
    {
        step.Logs       = await TaskService.GetTaskLogsAsync(step.Task.Id);
        step.LogsLoaded = true;
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
        return agent;
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

    private static bool IsCompleted(string status)  => status == "done";
    private static bool IsFailed(string status)      => status is "failed" or "error";
    private static bool IsRevision(string status)    => status is "revision" or "reviewing";

    private static Color GetLogColor(string status) => status switch
    {
        "done"               => Color.Success,
        "failed" or "error"  => Color.Error,
        "running"            => Color.Info,
        "revision"           => Color.Warning,
        "reviewing"          => Color.Warning,
        _                    => Color.Default
    };

    private string WorkflowTypeLabel => Group?.WorkflowType switch
    {
        "new_feature"      => "新功能",
        "bug_fix"          => "Bug Fix",
        "tech_improvement" => "技術改善",
        _                  => Group?.WorkflowType ?? ""
    };

    #endregion
}

/// <summary>Pipeline View 單一步驟 ViewModel（不放 Shared，只在 Dashboard 使用）。</summary>
public class PipelineStepViewModel
{
    public TaskItemDto    Task       { get; set; } = null!;
    public List<TaskLogDto> Logs     { get; set; } = [];
    public bool           LogsLoaded { get; set; }
}
