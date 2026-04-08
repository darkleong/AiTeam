using AiTeam.Data.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Tasks;

public partial class TaskCenter : IAsyncDisposable
{
    #region Dependencies

    [Inject]
    private DashboardTaskService TaskService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IConfiguration Configuration { get; set; } = null!;

    #endregion

    #region Private Variables — Tab 1（任務列表）

    private MudTable<TaskItemDto> _tableRef  = null!;
    private TaskItemDto?          _selectedTask;
    private List<TaskLogDto>      _selectedLogs      = [];
    private bool                  _isTaskDrawerOpen;
    private string?               _statusFilter;

    #endregion

    #region Private Variables — Tab 2（流程追蹤）

    private MudTable<TaskGroupDto> _groupTableRef         = null!;
    private TaskGroupDto?          _selectedGroup;
    private bool                   _isPipelineDrawerOpen;
    private string?                _groupStatusFilter;

    #endregion

    #region Private Variables — SignalR

    private HubConnection? _hubConnection;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
    {
        await ConnectSignalRAsync();
    }

    #endregion

    #region Private Methods — SignalR

    private async Task ConnectSignalRAsync()
    {
        var hubBaseUrl = Configuration["Dashboard:HubBaseUrl"];
        var hubUrl = string.IsNullOrEmpty(hubBaseUrl)
            ? Navigation.ToAbsoluteUri("/hubs/agent-status").ToString()
            : $"{hubBaseUrl.TrimEnd('/')}/hubs/agent-status";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        // 收到任務更新時，Tab 1 自動重新整理，Tab 2 pipeline view 由元件內部處理
        _hubConnection.On<object>(
            AgentStatusHub.ReceiveTaskUpdate,
            async _ => await InvokeAsync(async () =>
                await (_tableRef?.ReloadServerData() ?? Task.CompletedTask)));

        await _hubConnection.StartAsync();
    }

    #endregion

    #region Private Methods — Tab 1（任務列表）

    private async Task<TableData<TaskItemDto>> LoadServerDataAsync(
        TableState state,
        CancellationToken cancellationToken)
    {
        var result = await TaskService.GetTasksPagedAsync(
            page: state.Page + 1,
            pageSize: state.PageSize,
            statusFilter: _statusFilter);

        return new TableData<TaskItemDto>
        {
            Items      = result.Items,
            TotalItems = result.TotalCount
        };
    }

    private async Task OnTaskRowClickAsync(TableRowClickEventArgs<TaskItemDto> args)
    {
        _selectedTask = args.Item;
        if (_selectedTask is null) return;

        _selectedLogs      = await TaskService.GetTaskLogsAsync(_selectedTask.Id);
        _isTaskDrawerOpen  = true;
    }

    private async Task OnStatusFilterChangedAsync()
        => await (_tableRef?.ReloadServerData() ?? Task.CompletedTask);

    #endregion

    #region Private Methods — Tab 2（流程追蹤）

    private async Task<TableData<TaskGroupDto>> LoadGroupServerDataAsync(
        TableState state,
        CancellationToken cancellationToken)
    {
        var result = await TaskService.GetTaskGroupsPagedAsync(
            page: state.Page + 1,
            pageSize: state.PageSize,
            statusFilter: _groupStatusFilter);

        return new TableData<TaskGroupDto>
        {
            Items      = result.Items,
            TotalItems = result.TotalCount
        };
    }

    private void OnGroupRowClickAsync(TableRowClickEventArgs<TaskGroupDto> args)
    {
        _selectedGroup        = args.Item;
        _isPipelineDrawerOpen = true;
    }

    private async Task OnGroupStatusFilterChangedAsync()
        => await (_groupTableRef?.ReloadServerData() ?? Task.CompletedTask);

    #endregion

    #region Helpers

    /// <summary>將 TimeSpan 格式化為人類易讀字串，如「3 分 42 秒」。</summary>
    private static string FormatDuration(TimeSpan? duration)
    {
        if (duration is null) return "—";

        var ts = duration.Value;

        if (ts.TotalSeconds < 60)
            return $"{(int)ts.TotalSeconds} 秒";

        if (ts.TotalMinutes < 60)
            return ts.Seconds > 0
                ? $"{(int)ts.TotalMinutes} 分 {ts.Seconds} 秒"
                : $"{(int)ts.TotalMinutes} 分";

        return ts.Minutes > 0
            ? $"{(int)ts.TotalHours} 時 {ts.Minutes} 分"
            : $"{(int)ts.TotalHours} 時";
    }

    private static string WorkflowTypeLabel(string? workflowType) => workflowType switch
    {
        "new_feature"      => "新功能",
        "bug_fix"          => "Bug Fix",
        "tech_improvement" => "技術改善",
        _                  => workflowType ?? ""
    };

    #endregion

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }

    #endregion
}
