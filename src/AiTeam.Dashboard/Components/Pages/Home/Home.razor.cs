using AiTeam.Data.Hubs;
using AiTeam.Data.Repositories;
using AiTeam.Dashboard.Services;
using Microsoft.AspNetCore.SignalR.Client;

namespace AiTeam.Dashboard.Components.Pages.Home;

/// <summary>
/// Stage 83 子項 1：Home 入口頁重做 — 4 metric 速覽卡 + 3 分區跳轉 + QuickCommandCard reuse。
///
/// 砍既有 AgentStatus section（搬 Monitoring 分區 / 子項 4）+ RecentGroups section（搬 Tasks 分區 / 子項 2）—
/// Home 純粹當入口頁 / 不堆 mixed scope content。
///
/// SignalR Hub：簡化 3 endpoint reload metric（ReceiveTaskUpdate / ReceiveInteractionUpdate / ReceiveTokenUpdate）—
/// 子項 6 重 wire 完整 5 endpoint subscribe 細分（不同分區 subscribe 對應 endpoint）。
/// </summary>
public partial class Home : IAsyncDisposable
{
    #region Dependencies

    [Inject] private PetraSessionRepository SessionRepo { get; set; } = null!;
    [Inject] private PetraInboxRepository   InboxRepo   { get; set; } = null!;
    [Inject] private DashboardInteractionQueryService TaskService { get; set; } = null!;
    [Inject] private DashboardTokenService  TokenService { get; set; } = null!;
    [Inject] private NavigationManager      Nav { get; set; } = null!;
    [Inject] private IConfiguration         Configuration { get; set; } = null!;
    [Inject] private ILogger<Home>          Logger { get; set; } = null!;

    #endregion

    #region Private Variables

    private int  _activeSessionCount;
    private int  _pendingInboxCount;
    private int  _pendingHitlCount;
    private long _todayTokenTotal;
    private HubConnection? _hubConnection;
    private bool _hubConnected;

    #endregion

    protected override async Task OnInitializedAsync()
    {
        await LoadMetricsAsync();
        await ConnectSignalRAsync();
    }

    private async Task LoadMetricsAsync()
    {
        try
        {
            _activeSessionCount = await SessionRepo.CountActiveAsync();
            _pendingInboxCount  = await InboxRepo.CountPendingTotalAsync();

            var pendingInteractions = await TaskService.GetPendingInteractionsAsync();
            _pendingHitlCount   = pendingInteractions.Count;

            var todayStart = DateTime.UtcNow.Date;
            var summary    = await TokenService.GetSummaryAsync(todayStart, todayStart.AddDays(1));
            _todayTokenTotal = summary.AgentSummaries.Sum(a => (long)a.TotalInputTokens + a.TotalOutputTokens);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Home 速覽 metric 載入失敗");
        }
    }

    private async Task ConnectSignalRAsync()
    {
        // Docker 容器內部用 Dashboard__HubBaseUrl 覆蓋（避免用外部 port 連不到自己）
        var hubBaseUrl = Configuration["Dashboard:HubBaseUrl"];
        var hubUrl = string.IsNullOrEmpty(hubBaseUrl)
            ? Nav.ToAbsoluteUri("/hubs/agent-status").ToString()
            : $"{hubBaseUrl.TrimEnd('/')}/hubs/agent-status";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        // Stage 83 子項 6 補做：Home 速覽 5 endpoint 全 subscribe（從 3 → 5）— 任一觸發 reload 4 metric
        // 對齊 Aria 拍板：Home 入口頁 subscribe 全部 5 endpoint（速覽全分區 metric / 對齊「老闆控制中心」精神）
        _hubConnection.On<object>(AgentStatusHub.ReceiveAgentStatus, async _ =>
        {
            await LoadMetricsAsync();
            await InvokeAsync(StateHasChanged);
        });
        _hubConnection.On<object>(AgentStatusHub.ReceiveTaskUpdate, async _ =>
        {
            await LoadMetricsAsync();
            await InvokeAsync(StateHasChanged);
        });
        _hubConnection.On(AgentStatusHub.ReceiveQueueUpdate, async () =>
        {
            await LoadMetricsAsync();
            await InvokeAsync(StateHasChanged);
        });
        _hubConnection.On<object>(AgentStatusHub.ReceiveInteractionUpdate, async _ =>
        {
            await LoadMetricsAsync();
            await InvokeAsync(StateHasChanged);
        });
        _hubConnection.On<object>(AgentStatusHub.ReceiveTokenUpdate, async _ =>
        {
            await LoadMetricsAsync();
            await InvokeAsync(StateHasChanged);
        });

        _hubConnection.Closed += _ =>
        {
            _hubConnected = false;
            return InvokeAsync(StateHasChanged);
        };
        _hubConnection.Reconnected += _ =>
        {
            _hubConnected = true;
            return InvokeAsync(StateHasChanged);
        };

        try
        {
            await _hubConnection.StartAsync();
            _hubConnected = true;
            Logger.LogInformation("SignalR Hub 連線成功：{Url}", hubUrl);
        }
        catch (Exception ex)
        {
            _hubConnected = false;
            Logger.LogError(ex, "SignalR Hub 連線失敗");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }
}
