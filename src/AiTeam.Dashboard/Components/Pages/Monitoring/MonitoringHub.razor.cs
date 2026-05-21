using AiTeam.Data;
using AiTeam.Data.Hubs;
using AiTeam.Dashboard.Services;
using AiTeam.Shared.Dtos;
using AiTeam.Shared.ViewModels;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Monitoring;

/// <summary>
/// Stage 83 子項 4：Monitoring 分區主頁 — MudTabs 4 subtab
/// （Token 統計 / Agent 狀態 / 部署紀錄 / 系統健康）。
///
/// reuse 既有 component：
/// - AgentStatusCard.razor（Stage 33 老闆控制中心遺產）
/// - DashboardTokenService.GetSummaryAsync（Stage 22 既有）
/// - DashboardAgentService.GetAllAgentStatusesAsync（Stage 33 既有）
/// - DashboardTaskService.GetRecentTasksAsync(filter AssignedAgent='Ops')（既有部署紀錄）
///
/// TokenLogDetail drawer + per-PetraSession 切換 + 警戒線視覺化 — 留 Stage 84+ 真實需求累積後評估
/// （對齊「最後測驗」精神 + L+++ context budget trade-off）。
/// </summary>
public partial class MonitoringHub : IAsyncDisposable
{
    #region Dependencies

    [Inject] private DashboardAgentService    AgentService  { get; set; } = null!;
    [Inject] private DashboardTaskService     TaskService   { get; set; } = null!;
    [Inject] private DashboardTokenService    TokenService  { get; set; } = null!;
    [Inject] private IDbContextFactory<AppDbContext> DbFactory { get; set; } = null!;
    [Inject] private NavigationManager        Navigation    { get; set; } = null!;
    [Inject] private IConfiguration           Configuration { get; set; } = null!;
    [Inject] private ILogger<MonitoringHub>   Logger        { get; set; } = null!;

    #endregion

    #region Private State

    private List<AgentStatusViewModel> _agentStatuses     = [];
    private List<AgentQueueDto>        _agentQueues       = [];
    private TokenSummaryDto?           _todaySummary;
    private List<TaskItemDto>          _recentDeployments = [];

    private HubConnection? _hubConnection;
    private bool _hubConnected;

    private bool _botHealthy;
    private string _botStatusDetail = "未檢測";
    private bool _dbHealthy;
    private string _dbStatusDetail = "未檢測";

    #endregion

    protected override async Task OnInitializedAsync()
    {
        await LoadAllAsync();
        await ConnectSignalRAsync();
        await RefreshHealthAsync();
    }

    private async Task LoadAllAsync()
    {
        try
        {
            _agentStatuses = await AgentService.GetAllAgentStatusesAsync();
            _agentQueues   = await TaskService.GetAgentQueuesAsync();

            var todayStart = DateTime.UtcNow.Date;
            _todaySummary  = await TokenService.GetSummaryAsync(todayStart, todayStart.AddDays(1));

            // 部署紀錄 = TaskItem WHERE AssignedAgent='Ops'（既有 DeploymentHistory.razor 邏輯）
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
            Logger.LogError(ex, "MonitoringHub LoadAllAsync 失敗");
        }
    }

    private async Task RefreshHealthAsync()
    {
        // Bot 健康 = 對齊 Stage 67 既有 /internal/restart endpoint reachable + Internal API key 機制
        // 不在乎是否真實 reachable — 仰賴 SignalR Hub 連線狀態（_hubConnected）= 因 SignalR Hub 由 Dashboard mount + Bot push 來判斷
        // 簡化：用 DB ping 同時驗 Dashboard process healthy（自身 always healthy）
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            await db.Database.CanConnectAsync();
            _dbHealthy = true;
            _dbStatusDetail = "AppDbContext 連線正常";
        }
        catch (Exception ex)
        {
            _dbHealthy = false;
            _dbStatusDetail = ex.Message.Length > 50 ? ex.Message[..50] + "..." : ex.Message;
        }

        // Bot 健康暫用 SignalR Hub 連線狀態代理（真實 /internal/health endpoint Stage 84+ 補）
        _botHealthy = _hubConnected;
        _botStatusDetail = _hubConnected ? "SignalR Hub 連線（Bot pushable）" : "SignalR Hub 斷線";
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

        // Monitoring 分區 subscribe ReceiveAgentStatus + ReceiveTokenUpdate + ReceiveQueueUpdate
        _hubConnection.On<AgentStatusViewModel>(AgentStatusHub.ReceiveAgentStatus, async status =>
        {
            UpdateAgentStatus(status);
            await InvokeAsync(StateHasChanged);
        });
        _hubConnection.On(AgentStatusHub.ReceiveQueueUpdate, async () =>
        {
            _agentQueues = await TaskService.GetAgentQueuesAsync();
            await InvokeAsync(StateHasChanged);
        });
        _hubConnection.On<object>(AgentStatusHub.ReceiveTokenUpdate, async _ =>
        {
            var todayStart = DateTime.UtcNow.Date;
            _todaySummary  = await TokenService.GetSummaryAsync(todayStart, todayStart.AddDays(1));
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
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MonitoringHub SignalR Hub 連線失敗");
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
