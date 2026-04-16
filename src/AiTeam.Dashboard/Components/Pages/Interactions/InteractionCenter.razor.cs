using AiTeam.Data.Hubs;
using AiTeam.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Interactions;

public partial class InteractionCenter : IAsyncDisposable
{
    #region Dependencies

    [Inject]
    private DashboardTaskService TaskService { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IConfiguration Configuration { get; set; } = null!;

    [Inject]
    private InteractionRespondService RespondService { get; set; } = null!;

    #endregion

    #region Private Variables

    private List<BossInteractionDto> _pending   = [];
    private List<BossInteractionDto> _responded = [];
    private bool                     _isLoading = true;
    private const int _recentCount = 10;

    #endregion

    #region Private Variables — SignalR

    private HubConnection? _hubConnection;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await ConnectSignalRAsync();
    }

    #endregion

    #region Private Methods — Data

    private async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            var pendingTask   = TaskService.GetPendingInteractionsAsync();
            var respondedTask = TaskService.GetRecentInteractionsAsync(_recentCount);
            await Task.WhenAll(pendingTask, respondedTask);
            _pending   = await pendingTask;
            _responded = await respondedTask;
        }
        finally
        {
            _isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandleResponseAsync(ResponseRequest request)
    {
        try
        {
            var responded = await RespondService.RespondAsync(request.InteractionId, request.Action);
            if (responded)
            {
                Snackbar.Add("回覆成功！", Severity.Success);
                await LoadAsync();
            }
            else
            {
                Snackbar.Add("此互動已被另一通道回覆，已重新整理。", Severity.Warning);
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"回覆時發生錯誤：{ex.Message}", Severity.Error);
        }
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

        _hubConnection.On(AgentStatusHub.ReceiveInteractionUpdate, async () =>
        {
            await InvokeAsync(LoadAsync);
        });

        await _hubConnection.StartAsync();
    }

    #endregion

    #region Static Helpers

    internal static string GetInteractionIcon(string type) => type switch
    {
        "ceo_confirm"      => Icons.Material.Filled.Assignment,
        "exec_confirm"     => Icons.Material.Filled.PlayArrow,
        "proposal"         => Icons.Material.Filled.Description,
        "kickoff"          => Icons.Material.Filled.Groups,
        "design"           => Icons.Material.Filled.DesignServices,
        "devplan_escalate" => Icons.Material.Filled.Warning,
        "merge_notify"     => Icons.Material.Filled.CheckCircle,
        "intervention"     => Icons.Material.Filled.Error,
        _                  => Icons.Material.Filled.Notifications
    };

    internal static Color GetInteractionColor(string type) => type switch
    {
        "devplan_escalate" => Color.Warning,
        "merge_notify"     => Color.Success,
        "intervention"     => Color.Error,
        "proposal"         => Color.Info,
        "kickoff"          => Color.Info,
        "design"           => Color.Info,
        _                  => Color.Default
    };

    internal static string GetInteractionLabel(string type) => type switch
    {
        "ceo_confirm"      => "CEO 決策確認",
        "exec_confirm"     => "Agent 執行確認",
        "proposal"         => "提案確認",
        "kickoff"          => "Kickoff 確認",
        "design"           => "設計確認",
        "devplan_escalate" => "Dev_plan 升級",
        "merge_notify"     => "全流程完成",
        "intervention"     => "需要介入",
        _                  => type
    };

    private static Color GetActionColor(string? action) => action switch
    {
        "confirm_yes" or "exec_yes" or "propose_yes" or "kickoff_continue" or "design_continue" => Color.Success,
        "confirm_no"  or "exec_no"  or "propose_no"  or "kickoff_stop"     or "design_stop"     => Color.Error,
        "kickoff_restart"                                                                         => Color.Warning,
        "devplan_skip"                                                                            => Color.Warning,
        "devplan_abort"                                                                           => Color.Error,
        _                                                                                         => Color.Default
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

/// <summary>InteractionCard → InteractionCenter 的回覆請求。</summary>
public record ResponseRequest(Guid InteractionId, string Action);
