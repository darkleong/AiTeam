using AiTeam.Data.Hubs;
using AiTeam.Shared.Dtos;
using AiTeam.Shared.ViewModels;
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

    #region Private Variables

    private MudTable<TaskItemDto>  _tableRef         = null!;
    private TaskItemDto?           _selectedTask;
    private List<TaskLogDto>       _selectedLogs     = [];
    private bool                   _isTaskDrawerOpen;
    private IEnumerable<string>    _statusFilters    = [];

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

        _hubConnection.On<TaskUpdateViewModel>(
            AgentStatusHub.ReceiveTaskUpdate,
            async update => await InvokeAsync(async () =>
            {
                await (_tableRef?.ReloadServerData() ?? Task.CompletedTask);
            }));

        await _hubConnection.StartAsync();
    }

    #endregion

    #region Private Methods

    private async Task<TableData<TaskItemDto>> LoadServerDataAsync(
        TableState state,
        CancellationToken cancellationToken)
    {
        var result = await TaskService.GetTasksPagedAsync(
            page: state.Page + 1,
            pageSize: state.PageSize,
            statusFilters: _statusFilters.ToHashSet());

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

        _selectedLogs     = await TaskService.GetTaskLogsAsync(_selectedTask.Id);
        _isTaskDrawerOpen = true;
    }

    private async Task OnStatusFilterChangedAsync()
        => await (_tableRef?.ReloadServerData() ?? Task.CompletedTask);

    private static Color TriggeredByColor(string? triggeredBy) => triggeredBy switch
    {
        "Discord"      => Color.Secondary,
        "Orchestrator" => Color.Default,
        _              => Color.Default
    };

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

    #endregion

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }

    #endregion
}
