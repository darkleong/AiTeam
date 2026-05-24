using AiTeam.Data.Hubs;
using AiTeam.Dashboard.Services;
using AiTeam.Shared.Dtos;
using AiTeam.Shared.ViewModels;
using Microsoft.AspNetCore.SignalR.Client;

namespace AiTeam.Dashboard.Components.Pages.Monitoring;

/// <summary>
/// Stage 87 B0：Monitoring 拆 4 sub-page 之 Agent 狀態（取代 MonitoringHub.razor Tab 2）。
/// Stage 87 B1：SignalR 只訂閱 ReceiveAgentStatus（細粒度）。
/// </summary>
public partial class MonitoringAgents : IAsyncDisposable
{
    #region Dependencies

    [Inject] private DashboardAgentService            AgentService { get; set; } = null!;
    [Inject] private DashboardInteractionQueryService TaskService  { get; set; } = null!;
    [Inject] private IConfiguration                   Configuration { get; set; } = null!;
    [Inject] private NavigationManager                Navigation   { get; set; } = null!;
    [Inject] private ILogger<MonitoringAgents>        Logger       { get; set; } = null!;

    #endregion

    private List<AgentStatusViewModel> _agentStatuses = [];
    private List<AgentQueueDto>        _agentQueues   = [];
    private HubConnection? _hubConnection;
    private bool _hubConnected;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await ConnectSignalRAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _agentStatuses = await AgentService.GetAllAgentStatusesAsync();
            _agentQueues   = await TaskService.GetAgentQueuesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MonitoringAgents LoadAsync 失敗");
        }
    }

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

        // Stage 87 B1：細粒度訂閱 — Agent 狀態只接 ReceiveAgentStatus
        _hubConnection.On<AgentStatusViewModel>(AgentStatusHub.ReceiveAgentStatus, async status =>
        {
            UpdateAgentStatus(status);
            await InvokeAsync(StateHasChanged);
        });

        _hubConnection.Closed     += _ => { _hubConnected = false; return InvokeAsync(StateHasChanged); };
        _hubConnection.Reconnected += _ => { _hubConnected = true;  return InvokeAsync(StateHasChanged); };

        try
        {
            await _hubConnection.StartAsync();
            _hubConnected = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MonitoringAgents SignalR Hub 連線失敗");
        }
    }

    private void UpdateAgentStatus(AgentStatusViewModel updated)
    {
        var idx = _agentStatuses.FindIndex(a => a.AgentName == updated.AgentName);
        if (idx < 0) return;
        _agentStatuses[idx] = updated;
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }
}
