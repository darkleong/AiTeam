using AiTeam.Data.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Telerik.Blazor.Components;

namespace AiTeam.Dashboard.Components.Pages.Tasks;

public partial class TaskCenter : IAsyncDisposable
{
    #region Dependencies

    [Inject]
    private DashboardTaskService TaskService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region Private Variables

    private TelerikGrid<TaskItemDto> _gridRef = null!;
    private List<TaskItemDto>        _tasks = [];
    private TaskItemDto?             _selectedTask;
    private List<TaskLogDto>         _selectedLogs = [];
    private bool                     _isDrawerOpen;
    private string?                  _statusFilter;
    private HubConnection?           _hubConnection;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
    {
        await ConnectSignalRAsync();
    }

    #endregion

    #region Private Methods

    private async Task ConnectSignalRAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/hubs/agent-status"))
            .WithAutomaticReconnect()
            .Build();

        // 收到任務更新時，自動重新整理 Grid
        _hubConnection.On<object>(
            AgentStatusHub.ReceiveTaskUpdate,
            async _ => await InvokeAsync(() => _gridRef?.Rebind()));

        await _hubConnection.StartAsync();
    }

    private async Task OnGridReadAsync(GridReadEventArgs args)
    {
        var result = await TaskService.GetTasksPagedAsync(
            page: args.Request.Page,
            pageSize: args.Request.PageSize,
            statusFilter: _statusFilter);
        args.Data  = result.Items;
        args.Total = result.TotalCount;
    }

    private async Task OnRowClickAsync(GridRowClickEventArgs args)
    {
        _selectedTask = args.Item as TaskItemDto;
        if (_selectedTask is null) return;

        _selectedLogs = await TaskService.GetTaskLogsAsync(_selectedTask.Id);
        _isDrawerOpen = true;
    }

    #endregion

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }

    #endregion
}
