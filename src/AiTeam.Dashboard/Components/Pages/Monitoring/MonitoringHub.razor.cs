using System.Net.Http.Json;
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
/// Stage 83 子項 4：Monitoring 分區主頁 — MudTabs 4 subtab（補做版）。
///
/// Stage 83 補做（Aria gate1 plan ↔ delivery gap 收口）：
/// - Tab 1 Token 統計：inline MudChart 趨勢圖 + MudSelect 維度切換（per-Talent / per-PetraSession）+ 警戒線 alert（80% 全域月限）+ click row 開 TokenLogDetail drawer
/// - Tab 4 System Health：query /internal/health endpoint（Stage 83 子項 4 新加 endpoint）+ Discord status 4 個 status card
/// </summary>
public partial class MonitoringHub : IAsyncDisposable
{
    #region Dependencies

    [Inject] private DashboardAgentService    AgentService  { get; set; } = null!;
    [Inject] private DashboardInteractionQueryService TaskService { get; set; } = null!;
    [Inject] private DashboardTokenService    TokenService  { get; set; } = null!;
    [Inject] private DashboardAppSettingsService SettingsSvc { get; set; } = null!;
    [Inject] private IDbContextFactory<AppDbContext> DbFactory { get; set; } = null!;
    [Inject] private IHttpClientFactory       HttpClientFactory { get; set; } = null!;
    [Inject] private IConfiguration           Configuration { get; set; } = null!;
    [Inject] private NavigationManager        Navigation    { get; set; } = null!;
    [Inject] private ILogger<MonitoringHub>   Logger        { get; set; } = null!;

    #endregion

    #region Private State

    private List<AgentStatusViewModel> _agentStatuses     = [];
    private List<AgentQueueDto>        _agentQueues       = [];
    private TokenSummaryDto?           _todaySummary;
    private List<TaskItemDto>          _recentDeployments = [];
    private List<PerSessionRow>        _perSessionRows    = [];
    private List<TokenLog>             _selectedAgentLogs = [];

    private string _selectedDimension = "per-Talent";
    private bool   _isTokenDrawerOpen;

    private int  _todayTotalKtokens;
    private int  _globalMonthlyLimitK;

    private HubConnection? _hubConnection;
    private bool _hubConnected;

    private bool _botHealthy;
    private string _botStatusDetail = "未檢測";
    private bool _dbHealthy;
    private string _dbStatusDetail = "未檢測";
    // Stage 85 子項 3：Discord status field 砍（placeholder 卡砍 / FF #7 真實 check 做完再加回）

    // MudChart data
    private List<ChartSeries> _perTalentChartSeries = [];
    private string[] _perTalentXAxisLabels = [];

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

            await LoadTokensAsync();

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

    private async Task LoadTokensAsync()
    {
        try
        {
            var todayStart = DateTime.UtcNow.Date;
            _todaySummary  = await TokenService.GetSummaryAsync(todayStart, todayStart.AddDays(1));

            // 計算今日 K tokens（警戒線用）
            _todayTotalKtokens = (int)(_todaySummary.AgentSummaries
                .Sum(a => (long)a.TotalInputTokens + a.TotalOutputTokens) / 1000);

            // 載入 global monthly limit（DB SoT）
            var limitRow = await SettingsSvc.GetAsync("Token:GlobalMonthlyLimitK");
            int.TryParse(limitRow?.Value, out _globalMonthlyLimitK);

            // 建 MudChart series（per-Talent input + output stacked bar）
            BuildPerTalentChart();

            // per-PetraSession 維度
            await LoadPerSessionAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "LoadTokensAsync 失敗");
        }
    }

    private void BuildPerTalentChart()
    {
        if (_todaySummary is null || _todaySummary.AgentSummaries.Count == 0)
        {
            _perTalentChartSeries = [];
            _perTalentXAxisLabels = [];
            return;
        }
        _perTalentXAxisLabels = _todaySummary.AgentSummaries.Select(a => a.AgentName).ToArray();
        _perTalentChartSeries =
        [
            new ChartSeries
            {
                Name = "Input (K tokens)",
                Data = _todaySummary.AgentSummaries.Select(a => (double)a.TotalInputTokens / 1000).ToArray(),
            },
            new ChartSeries
            {
                Name = "Output (K tokens)",
                Data = _todaySummary.AgentSummaries.Select(a => (double)a.TotalOutputTokens / 1000).ToArray(),
            },
        ];
    }

    private async Task LoadPerSessionAsync()
    {
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            var todayStart = DateTime.UtcNow.Date;
            _perSessionRows = await db.TokenLogs
                .AsNoTracking()
                .Where(t => t.CreatedAt >= todayStart && t.PetraSessionId != null)
                .GroupBy(t => t.PetraSessionId!.Value)
                .Select(g => new PerSessionRow
                {
                    PetraSessionId   = g.Key,
                    RowCount         = g.Count(),
                    TotalInputTokens = g.Sum(l => l.InputTokens),
                    TotalOutputTokens = g.Sum(l => l.OutputTokens),
                    TotalCostUsd     = g.Sum(l => l.TotalCostUsd ?? 0m),
                })
                .OrderByDescending(r => r.TotalInputTokens + r.TotalOutputTokens)
                .Take(50)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "LoadPerSessionAsync 失敗");
        }
    }

    private async Task OnDimensionChangedAsync(string newValue)
    {
        _selectedDimension = newValue;
        await LoadTokensAsync();
    }

    private async Task OnAgentRowClick(TableRowClickEventArgs<TokenAgentSummaryDto> args)
    {
        var agentName = args.Item?.AgentName;
        if (string.IsNullOrEmpty(agentName)) return;

        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            var todayStart = DateTime.UtcNow.Date;
            _selectedAgentLogs = await db.TokenLogs
                .AsNoTracking()
                .Where(t => t.AgentName == agentName && t.CreatedAt >= todayStart)
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .ToListAsync();
            _isTokenDrawerOpen = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "OnAgentRowClick 失敗");
        }
    }

    private async Task RefreshHealthAsync()
    {
        // 走 /internal/health endpoint（Stage 83 子項 4 新加）
        // Stage 83 v3 Bug 3 修根因：對齊既有 docker-compose.prod.yml Dashboard env naming（`Bot:` prefix / 不是 `AgentSettings:`） —
        // Dashboard 端把 Bot 視為「外部 service」用 Bot:InternalApiKey + Bot:InternalUrl env / 既有 Stage X 已 inject。
        try
        {
            var apiKey = Configuration["Bot:InternalApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _botHealthy = false;
                _botStatusDetail = "Bot:InternalApiKey 未設定（docker-compose Bot__InternalApiKey env）";
                return;
            }

            // Bot Internal API URL — docker-compose Dashboard env `Bot__InternalUrl: "http://aiteam-bot:8080"`（內部 docker network port 8080）
            var botBaseUrl = Configuration["Bot:InternalUrl"] ?? "http://aiteam-bot:8080";

            var client = HttpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync($"{botBaseUrl}/internal/health");
            if (response.IsSuccessStatusCode)
            {
                var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
                if (health is not null)
                {
                    _botHealthy = health.BotProcessUp;
                    _botStatusDetail = health.BotProcessDetail;
                    _dbHealthy = health.DbConnected;
                    _dbStatusDetail = health.DbDetail;
                    // Stage 85 子項 3：Discord status 寫入砍（placeholder 卡砍 / record schema 保留對齊 InternalController deserialization）
                    return;
                }
            }
            _botHealthy = false;
            _botStatusDetail = $"HTTP {(int)response.StatusCode}";
        }
        catch (Exception ex)
        {
            _botHealthy = false;
            _botStatusDetail = ex.Message.Length > 50 ? ex.Message[..50] + "..." : ex.Message;
            // Fallback：DB ping 直接 Dashboard 端做
            try
            {
                await using var db = await DbFactory.CreateDbContextAsync();
                _dbHealthy = await db.Database.CanConnectAsync();
                _dbStatusDetail = _dbHealthy ? "Dashboard 端 DB ping OK" : "Cannot connect";
            }
            catch (Exception dbEx)
            {
                _dbHealthy = false;
                _dbStatusDetail = dbEx.Message;
            }
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

        // Stage 83 子項 6 補做：MonitoringHub subscribe 2 endpoint（ReceiveAgentStatus + ReceiveTokenUpdate）
        // 對齊 Aria 拍板「Monitoring subscribe ReceiveAgentStatus + ReceiveTokenUpdate」 — 砍 ReceiveQueueUpdate（Queue 屬 Tasks 視角 / Home 入口頁有 cover）
        _hubConnection.On<AgentStatusViewModel>(AgentStatusHub.ReceiveAgentStatus, async status =>
        {
            UpdateAgentStatus(status);
            await InvokeAsync(StateHasChanged);
        });
        _hubConnection.On<object>(AgentStatusHub.ReceiveTokenUpdate, async _ =>
        {
            await LoadTokensAsync();
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

    public class PerSessionRow
    {
        public Guid    PetraSessionId    { get; set; }
        public int     RowCount          { get; set; }
        public int     TotalInputTokens  { get; set; }
        public int     TotalOutputTokens { get; set; }
        public decimal TotalCostUsd      { get; set; }
    }

    /// <summary>對齊 InternalController HealthStatusDto schema（避免跨 project DTO reference / 簡化用本地 record）。</summary>
    private record HealthResponse(
        bool BotProcessUp, string BotProcessDetail,
        bool DbConnected, string DbDetail,
        bool DiscordConnected, string DiscordDetail,
        DateTime Timestamp);
}
