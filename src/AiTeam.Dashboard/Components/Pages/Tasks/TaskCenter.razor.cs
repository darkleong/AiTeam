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

    #endregion

    #region Private Variables

    private MudTable<TaskItemDto> _tableRef = null!;
    private TaskItemDto?          _selectedTask;
    private List<TaskLogDto>      _selectedLogs = [];
    private bool                  _isDrawerOpen;
    private string?               _statusFilter;
    private HubConnection?        _hubConnection;

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

        // 收到任務更新時，自動重新整理 Table
        _hubConnection.On<object>(
            AgentStatusHub.ReceiveTaskUpdate,
            async _ => await InvokeAsync(async () =>
                await (_tableRef?.ReloadServerData() ?? Task.CompletedTask)));

        await _hubConnection.StartAsync();
    }

    /// <summary>
    /// MudTable ServerData 回呼：依分頁參數向 Service 取得資料。
    /// state.Page 為 0-indexed，API 使用 1-indexed，故 +1 轉換。
    /// </summary>
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

    private async Task OnRowClickAsync(TableRowClickEventArgs<TaskItemDto> args)
    {
        _selectedTask = args.Item;
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
