using AiTeam.Data;
using AiTeam.Data.Hubs;
using AiTeam.Dashboard.Configuration;
using AiTeam.Dashboard.Services;
using AiTeam.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Monitoring;

/// <summary>
/// Stage 87 B0：Monitoring 拆 4 sub-page 之 Token 統計（取代 MonitoringHub.razor Tab 1）。
/// 內容：時間範圍 6 選項 + 維度切換（per-Talent / per-PetraSession / per-Skill stub）+ MudChart + per-Agent table + TokenLogDetail drawer。
/// Stage 87 B1：SignalR 只訂閱 ReceiveTokenUpdate（細粒度 / 取代 MonitoringHub 全 event subscribe）。
/// </summary>
public partial class MonitoringTokens : IAsyncDisposable
{
    #region Dependencies

    [Inject] private DashboardTokenService    TokenService  { get; set; } = null!;
    [Inject] private DashboardAppSettingsService SettingsSvc { get; set; } = null!;
    [Inject] private IDbContextFactory<AppDbContext> DbFactory { get; set; } = null!;
    [Inject] private IConfiguration           Configuration { get; set; } = null!;
    [Inject] private NavigationManager        Navigation    { get; set; } = null!;
    [Inject] private ILogger<MonitoringTokens> Logger        { get; set; } = null!;
    [Inject] private IOptions<AgentTokenLimits> AgentLimitsOptions { get; set; } = null!;
    private AgentTokenLimits AgentLimits => AgentLimitsOptions.Value;

    #endregion

    #region Private State

    private TokenSummaryDto?    _todaySummary;
    private List<TokenLog>      _selectedAgentLogs = [];

    private string _selectedDimension = "per-Talent";
    private bool   _isTokenDrawerOpen;

    private int  _todayTotalKtokens;
    private int  _globalMonthlyLimitK;

    private string _selectedPeriod = "today";
    private readonly List<(string Key, string Label)> _periods =
    [
        ("today",  "今天"),
        ("week",   "這一週"),
        ("last7",  "最近 7 天"),
        ("month",  "這個月"),
        ("last30", "最近 30 天"),
        ("all",    "全部"),
    ];

    private HubConnection? _hubConnection;
    private bool _hubConnected;

    private List<ChartSeries> _perTalentChartSeries = [];
    private string[]          _perTalentXAxisLabels = [];

    #endregion

    protected override async Task OnInitializedAsync()
    {
        await LoadTokensAsync();
        await ConnectSignalRAsync();
    }

    private async Task LoadTokensAsync()
    {
        try
        {
            var (start, end) = ResolvePeriod(_selectedPeriod);
            _todaySummary  = await TokenService.GetSummaryAsync(start, end);

            _todayTotalKtokens = (int)(_todaySummary.AgentSummaries
                .Sum(a => (long)a.TotalInputTokens + a.TotalOutputTokens) / 1000);

            var limitRow = await SettingsSvc.GetAsync("Token:GlobalMonthlyLimitK");
            int.TryParse(limitRow?.Value, out _globalMonthlyLimitK);

            BuildPerTalentChart();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "LoadTokensAsync 失敗");
        }
    }

    private static (DateTime Start, DateTime End) ResolvePeriod(string period)
    {
        var now      = DateTime.UtcNow;
        var todayUtc = now.Date;
        return period switch
        {
            "today"  => (todayUtc,                                                              todayUtc.AddDays(1)),
            "week"   => (StartOfWeek(now),                                                      now.AddMinutes(1)),
            "last7"  => (now.AddDays(-7),                                                       now.AddMinutes(1)),
            "month"  => (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),       now.AddMinutes(1)),
            "last30" => (now.AddDays(-30),                                                      now.AddMinutes(1)),
            "all"    => (DateTime.MinValue.ToUniversalTime(),                                   DateTime.MaxValue.ToUniversalTime()),
            _        => (todayUtc,                                                              todayUtc.AddDays(1)),
        };
    }

    private static DateTime StartOfWeek(DateTime dt)
    {
        var diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
        return dt.Date.AddDays(-diff);
    }

    private async Task SetPeriodAsync(string key)
    {
        if (_selectedPeriod == key) return;
        _selectedPeriod = key;
        await LoadTokensAsync();
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

        // Stage 87 B1：細粒度訂閱 — Token 統計只接 ReceiveTokenUpdate（取代 MonitoringHub 全 event subscribe）
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
            Logger.LogError(ex, "MonitoringTokens SignalR Hub 連線失敗");
        }
    }

    private List<(string AgentName, double Pct, int Total, int LimitTokens)> GetPerAgentDailyAlerts()
    {
        if (_selectedPeriod != "today" || _todaySummary is null) return [];
        var alerts = new List<(string, double, int, int)>();
        foreach (var agent in _todaySummary.AgentSummaries)
        {
            if (!AgentLimits.Agents.TryGetValue(agent.AgentName, out var cfg)) continue;
            if (cfg.DailyTokenLimitK <= 0) continue;
            var limitTokens = cfg.DailyTokenLimitK * 1000;
            var total       = agent.TotalInputTokens + agent.TotalOutputTokens;
            var pct         = (double)total / limitTokens * 100;
            if (pct >= 90) alerts.Add((agent.AgentName, pct, total, limitTokens));
        }
        return alerts;
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }
}
