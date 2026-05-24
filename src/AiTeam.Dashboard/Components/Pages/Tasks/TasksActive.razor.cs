using AiTeam.Data;
using AiTeam.Data.Hubs;
using AiTeam.Data.Repositories;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Tasks;

/// <summary>
/// Stage 87 B0：Tasks 拆 4 sub-page 之進行中 Session（取代 TaskHub.razor Tab 2）。
/// Stage 87 B1：SignalR 訂閱 ReceiveAgentStatus + ReceiveTaskUpdate（session status + task update）。
/// </summary>
public partial class TasksActive : IAsyncDisposable
{
    #region Dependencies

    [Inject] private PetraSessionRepository SessionRepo   { get; set; } = null!;
    [Inject] private IConfiguration         Configuration { get; set; } = null!;
    [Inject] private NavigationManager      Navigation    { get; set; } = null!;
    [Inject] private ILogger<TasksActive>   Logger        { get; set; } = null!;

    #endregion

    private List<PetraSession> _activeSessions = [];
    private bool          _isLoading = true;
    private bool          _isSessionDrawerOpen;
    private PetraSession? _selectedSession;
    private HubConnection? _hubConnection;
    private bool _hubConnected;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await ConnectSignalRAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            _activeSessions = await SessionRepo.GetActiveAsync(limit: 50);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "TasksActive LoadAsync 失敗");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnSessionRowClick(TableRowClickEventArgs<PetraSession> args)
    {
        var clicked = args.Item;
        if (clicked is null) return;
        _selectedSession = await SessionRepo.GetWithMessagesAsync(clicked.Id);
        _isSessionDrawerOpen = true;
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

        _hubConnection.On(AgentStatusHub.ReceiveAgentStatus, async () =>
        {
            await LoadAsync();
            await InvokeAsync(StateHasChanged);
        });
        _hubConnection.On<object>(AgentStatusHub.ReceiveTaskUpdate, async _ =>
        {
            await LoadAsync();
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
            Logger.LogError(ex, "TasksActive SignalR Hub 連線失敗");
        }
    }

    private static Color GetSessionStatusColor(string status) => status switch
    {
        "running"   => Color.Info,
        "paused"    => Color.Warning,
        "done"      => Color.Success,
        "escalated" => Color.Error,
        "cancelled" => Color.Dark,
        _           => Color.Default,
    };

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }
}
