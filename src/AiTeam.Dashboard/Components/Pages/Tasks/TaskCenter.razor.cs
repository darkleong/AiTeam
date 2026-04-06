using AiTeam.Data.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
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

    [Inject]
    private ILogger<TaskCenter> Logger { get; set; } = null!;

    #endregion

    #region Private Variables

    private MudTable<TaskItemDto>      _tableRef = null!;
    private TaskItemDto?               _selectedTask;
    private List<TaskLogDto>           _selectedLogs = [];
    private bool                       _isDrawerOpen;
    private string?                    _statusFilter;
    private HubConnection?             _hubConnection;
    private PeriodicTimer?             _elapsedTimer;
    private CancellationTokenSource    _elapsedTimerCts = new();

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
    {
        await ConnectSignalRAsync();
        _ = StartElapsedTimerAsync();
    }

    #endregion

    #region Private Methods

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

    private async Task StartElapsedTimerAsync()
    {
        _elapsedTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await _elapsedTimer.WaitForNextTickAsync(_elapsedTimerCts.Token))
            {
                try
                {
                    await InvokeAsync(StateHasChanged);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "計時器更新 UI 時發生例外");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 元件 Dispose 時正常取消，不需記錄
        }
    }

    private static string FormatDuration(TimeSpan ts) => ts switch
    {
        { TotalSeconds: < 60 } => $"{(int)ts.TotalSeconds}秒",
        { TotalMinutes: < 60 } => $"{ts.Minutes}分{ts.Seconds:D2}秒",
        _                      => $"{(int)ts.TotalHours}小時{ts.Minutes:D2}分"
    };

    private string FormatElapsed(TimeSpan ts) =>
        FormatDuration(ts < TimeSpan.Zero ? TimeSpan.Zero : ts);

    #endregion

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        // 先 Cancel 再 Dispose，確保 WaitForNextTickAsync 能夠正確終止
        await _elapsedTimerCts.CancelAsync();
        _elapsedTimerCts.Dispose();
        _elapsedTimer?.Dispose();
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }

    #endregion
}
