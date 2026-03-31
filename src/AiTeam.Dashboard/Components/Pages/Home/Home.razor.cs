using System.Net.Http.Json;
using AiTeam.Data.Hubs;
using Microsoft.AspNetCore.SignalR.Client;

namespace AiTeam.Dashboard.Components.Pages.Home;

public partial class Home : IAsyncDisposable
{
    #region Dependencies

    [Inject]
    private DashboardAgentService AgentService { get; set; } = null!;

    [Inject]
    private DashboardTaskService TaskService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IHttpClientFactory HttpClientFactory { get; set; } = null!;

    [Inject]
    private ILogger<Home> Logger { get; set; } = null!;

    #endregion

    #region Private Variables

    private List<AgentStatusViewModel> _agentStatuses = [];
    private List<TaskItemDto> _recentTasks = [];
    private HubConnection? _hubConnection;
    private bool _hubConnected;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
    {
        _agentStatuses = await AgentService.GetAllAgentStatusesAsync();
        _recentTasks   = await TaskService.GetRecentTasksAsync(limit: 10);
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

        _hubConnection.On<AgentStatusViewModel>(
            AgentStatusHub.ReceiveAgentStatus,
            async status =>
            {
                UpdateAgentStatus(status);
                await InvokeAsync(StateHasChanged);
            });

        _hubConnection.On<object>(
            AgentStatusHub.ReceiveTaskUpdate,
            async _ =>
            {
                _recentTasks = await TaskService.GetRecentTasksAsync(limit: 10);
                await InvokeAsync(StateHasChanged);
            });

        _hubConnection.Closed += async _ =>
        {
            _hubConnected = false;
            await InvokeAsync(StateHasChanged);
        };

        _hubConnection.Reconnected += async _ =>
        {
            _hubConnected = true;
            await InvokeAsync(StateHasChanged);
        };

        try
        {
            await _hubConnection.StartAsync();
            _hubConnected = true;
            Logger.LogInformation("SignalR Hub 連線成功：{Url}", Navigation.ToAbsoluteUri("/hubs/agent-status"));
        }
        catch (Exception ex)
        {
            _hubConnected = false;
            Logger.LogError(ex, "SignalR Hub 連線失敗");
        }
    }

    private void UpdateAgentStatus(AgentStatusViewModel updated)
    {
        var idx = _agentStatuses.FindIndex(a => a.AgentName == updated.AgentName);
        if (idx >= 0)
            _agentStatuses[idx] = updated;
        else
            _agentStatuses.Add(updated);
    }

    /// <summary>測試 SignalR 推送管道：POST /internal/agent-status/test → Hub → 頁面更新。</summary>
    private async Task TestSignalRAsync()
    {
        try
        {
            var client = HttpClientFactory.CreateClient();
            client.BaseAddress = new Uri(Navigation.BaseUri);
            var response = await client.PostAsync("/internal/agent-status/test", null);
            Logger.LogInformation("測試推送回應：{StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "測試推送失敗");
        }
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
