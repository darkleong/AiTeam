using AiTeam.Data;
using AiTeam.Data.Hubs;
using AiTeam.Data.Repositories;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Tasks;

/// <summary>
/// Stage 87 B0：Tasks 拆 4 sub-page 之歷史 Session（取代 TaskHub.razor Tab 4）。
/// Stage 87 B1：SignalR 只訂閱 ReceiveTaskUpdate（完成轉歷史）。
/// </summary>
public partial class TasksHistory : IAsyncDisposable
{
    #region Dependencies

    [Inject] private PetraSessionRepository SessionRepo   { get; set; } = null!;
    [Inject] private IConfiguration         Configuration { get; set; } = null!;
    [Inject] private NavigationManager      Navigation    { get; set; } = null!;
    [Inject] private ILogger<TasksHistory>  Logger        { get; set; } = null!;

    #endregion

    private List<PetraSession> _historySessions = [];
    private bool _isLoading = true;
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
            _historySessions = await SessionRepo.GetHistoryAsync(limit: 50);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "TasksHistory LoadAsync 失敗");
        }
        finally
        {
            _isLoading = false;
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
            Logger.LogError(ex, "TasksHistory SignalR Hub 連線失敗");
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

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 60) return $"{(int)duration.TotalSeconds} 秒";
        if (duration.TotalMinutes < 60) return $"{(int)duration.TotalMinutes} 分";
        if (duration.TotalHours < 24)   return $"{(int)duration.TotalHours} 時 {duration.Minutes} 分";
        return $"{(int)duration.TotalDays} 天 {duration.Hours} 時";
    }

    private static string ExtractPrNumber(string? prUrl)
    {
        if (string.IsNullOrEmpty(prUrl)) return "—";
        var match = System.Text.RegularExpressions.Regex.Match(prUrl, @"/pull/(\d+)");
        return match.Success ? $"PR #{match.Groups[1].Value}" : "PR";
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }
}
