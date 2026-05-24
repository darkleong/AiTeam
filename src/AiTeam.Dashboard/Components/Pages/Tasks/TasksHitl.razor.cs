using AiTeam.Data.Hubs;
using AiTeam.Dashboard.Components.Pages.Interactions;
using AiTeam.Dashboard.Services;
using AiTeam.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Tasks;

/// <summary>
/// Stage 87 B0：Tasks 拆 4 sub-page 之 HITL 確認卡片（取代 TaskHub.razor Tab 1）。
/// Stage 87 B1：SignalR 只訂閱 ReceiveInteractionUpdate（細粒度）。
/// </summary>
public partial class TasksHitl : IAsyncDisposable
{
    #region Dependencies

    [Inject] private DashboardInteractionQueryService TaskService    { get; set; } = null!;
    [Inject] private InteractionRespondService        RespondService { get; set; } = null!;
    [Inject] private ISnackbar                        Snackbar       { get; set; } = null!;
    [Inject] private NavigationManager                Navigation     { get; set; } = null!;
    [Inject] private IConfiguration                   Configuration  { get; set; } = null!;
    [Inject] private ILogger<TasksHitl>               Logger         { get; set; } = null!;

    #endregion

    private List<BossInteractionDto> _pendingHitl = [];
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
            _pendingHitl = await TaskService.GetPendingInteractionsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "TasksHitl LoadAsync 失敗");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task HandleHitlResponseAsync(ResponseRequest request)
    {
        try
        {
            var responded = await RespondService.RespondAsync(request.InteractionId, request.Action, request.Content);
            Snackbar.Add(responded ? "回覆成功！" : "此互動已被另一通道回覆，已重新整理。",
                         responded ? Severity.Success : Severity.Warning);
            await LoadAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"回覆時發生錯誤：{ex.Message}", Severity.Error);
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

        _hubConnection.On(AgentStatusHub.ReceiveInteractionUpdate, async () =>
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
            Logger.LogError(ex, "TasksHitl SignalR Hub 連線失敗");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }
}
