using AiTeam.Data;
using AiTeam.Data.Hubs;
using AiTeam.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Components.Pages.Monitoring;

/// <summary>
/// Stage 87 B0：Monitoring 拆 4 sub-page 之部署紀錄（取代 MonitoringHub.razor Tab 3）。
/// Stage 87 B1：SignalR 只訂閱 ReceiveTaskUpdate（部署 = TaskItem AssignedAgent=Ops）。
/// </summary>
public partial class MonitoringDeployments : IAsyncDisposable
{
    #region Dependencies

    [Inject] private IDbContextFactory<AppDbContext> DbFactory     { get; set; } = null!;
    [Inject] private IConfiguration                  Configuration { get; set; } = null!;
    [Inject] private NavigationManager               Navigation    { get; set; } = null!;
    [Inject] private ILogger<MonitoringDeployments>  Logger        { get; set; } = null!;

    #endregion

    private List<TaskItemDto> _recentDeployments = [];
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
            await using var db = await DbFactory.CreateDbContextAsync();
            _recentDeployments = await db.Tasks
                .AsNoTracking()
                .Where(t => t.AssignedAgent == "Ops")
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .Select(t => new TaskItemDto
                {
                    Id            = t.Id,
                    Title         = t.Title,
                    Status        = t.Status,
                    AssignedAgent = t.AssignedAgent,
                    CreatedAt     = t.CreatedAt,
                    CompletedAt   = t.CompletedAt,
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MonitoringDeployments LoadAsync 失敗");
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
            Logger.LogError(ex, "MonitoringDeployments SignalR Hub 連線失敗");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }
}
