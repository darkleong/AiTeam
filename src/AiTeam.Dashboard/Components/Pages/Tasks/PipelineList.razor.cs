using AiTeam.Data.Hubs;
using AiTeam.Shared.Dtos;
using AiTeam.Shared.ViewModels;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Tasks;

public partial class PipelineList : IAsyncDisposable
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

    private MudTable<TaskGroupDto>  _groupTableRef        = null!;
    private PipelineView            _pipelineViewRef      = null!;
    private TaskGroupDto?           _selectedGroup;
    private bool                    _isPipelineDrawerOpen;
    private IEnumerable<string>     _statusFilters        = [];

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
                await (_groupTableRef?.ReloadServerData() ?? Task.CompletedTask);
                if (_pipelineViewRef is not null)
                    await _pipelineViewRef.HandleTaskUpdateAsync(update);
            }));

        await _hubConnection.StartAsync();
    }

    #endregion

    #region Private Methods

    private async Task<TableData<TaskGroupDto>> LoadGroupServerDataAsync(
        TableState state,
        CancellationToken cancellationToken)
    {
        var result = await TaskService.GetTaskGroupsPagedAsync(
            page: state.Page + 1,
            pageSize: state.PageSize,
            statusFilters: _statusFilters.ToHashSet());

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

    /// <summary>
    /// PipelineView 推算 Group 狀態變化時呼叫。
    /// Bot 寫 DB 需要一點時間，延遲 1.5 秒再刷新群組列表，確保讀到最新狀態。
    /// </summary>
    private void OnGroupStatusChangedAsync(string newStatus)
    {
        if (newStatus is not ("done" or "failed" or "cancelled")) return;

        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            await InvokeAsync(() => _groupTableRef?.ReloadServerData() ?? Task.CompletedTask);
        });
    }

    private async Task OnStatusFilterChangedAsync()
        => await (_groupTableRef?.ReloadServerData() ?? Task.CompletedTask);

    private static string WorkflowTypeLabel(string? workflowType) => workflowType switch
    {
        "new_feature"      => "新功能",
        "bug_fix"          => "Bug Fix",
        "tech_improvement" => "技術改善",
        _                  => workflowType ?? ""
    };

    private static Color WorkflowTypeColor(string? workflowType) => workflowType switch
    {
        "new_feature"      => Color.Primary,
        "bug_fix"          => Color.Warning,
        "tech_improvement" => Color.Secondary,
        _                  => Color.Default
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
