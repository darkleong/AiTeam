using AiTeam.Data;
using AiTeam.Dashboard.Hubs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Records;

/// <summary>
/// F3-B：MCP Records 趨勢圖頁。
/// 3 張 MudChart（ChartType.Line）：
///   1. 過去 7 天每日 token 消耗（input+output 總量）
///   2. 過去 7 天每日 token 消耗 by-model 拆分（sonnet/opus/haiku/其他）
///   3. 過去 30 天每日 active team 新增 + completed task 數量（雙線）
/// 對齊 MonitoringTokens.razor MudChart pattern（ChartSeries + XAxisLabels）。
/// patch1-D：訂閱 RecordsHub /records-hub 收到 RecordsUpdated event → reload 趨勢資料 / 不必 F5。
/// patch1-E：day bucket 用 local date（避免跨日邊界資料被錯歸 UTC 那天的桶）。
/// </summary>
public partial class RecordsTrends : ComponentBase, IAsyncDisposable
{
    [Inject] private AppDbContext             Db            { get; set; } = null!;
    [Inject] private NavigationManager        Nav           { get; set; } = null!;
    [Inject] private IConfiguration           Configuration { get; set; } = null!;
    [Inject] private ILogger<RecordsTrends>   Logger        { get; set; } = null!;

    private string[]          _dailyXAxisLabels        = [];
    private string[]          _monthlyXAxisLabels      = [];
    private List<ChartSeries> _tokenDailyTotalSeries   = [];
    private List<ChartSeries> _tokenDailyByModelSeries = [];
    private List<ChartSeries> _teamTaskMonthlySeries   = [];

    private HubConnection? _hub;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await LoadTokenDaily7DaysAsync();
            await LoadTeamTaskMonthly30DaysAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "RecordsTrends 載入失敗");
        }

        await ConnectHubAsync();
    }

    private async Task ConnectHubAsync()
    {
        try
        {
            // patch1-D：對齊 Records.razor.cs ConnectHubAsync pattern — 用 Dashboard:HubBaseUrl config（docker container localhost:8080）/ fallback to Nav.ToAbsoluteUri（dev mode）
            var hubBaseUrl = Configuration["Dashboard:HubBaseUrl"];
            var hubUrl = string.IsNullOrEmpty(hubBaseUrl)
                ? Nav.ToAbsoluteUri("/records-hub").ToString()
                : $"{hubBaseUrl.TrimEnd('/')}/records-hub";

            _hub = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            _hub.On(RecordsHub.ReceiveRecordsUpdated, async () =>
            {
                try
                {
                    await LoadTokenDaily7DaysAsync();
                    await LoadTeamTaskMonthly30DaysAsync();
                    await InvokeAsync(StateHasChanged);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "RecordsTrends RecordsUpdated handler 例外");
                }
            });

            await _hub.StartAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "RecordsTrends SignalR Hub 連線失敗（非關鍵 / 頁面仍可用）");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null) await _hub.DisposeAsync();
    }

    /// <summary>過去 7 天 token 消耗（總量 + by-model）</summary>
    private async Task LoadTokenDaily7DaysAsync()
    {
        // patch1-E：7 天 window 以 local date 為基準 / query 用 local→UTC boundary（DB 存 UTC）
        var todayLocal = DateTime.Now.Date;
        var startLocal = todayLocal.AddDays(-6);
        var endLocal   = todayLocal.AddDays(1); // exclusive
        var startUtc   = startLocal.ToUniversalTime();
        var endUtc     = endLocal.ToUniversalTime();

        var rows = await Db.AgentTokenUsages
            .AsNoTracking()
            .Where(t => t.CreatedAt >= startUtc && t.CreatedAt < endUtc)
            .Select(t => new { t.CreatedAt, t.InputTokens, t.OutputTokens, t.Model })
            .ToListAsync();

        // 7 個 daily bucket（local date）
        var days = Enumerable.Range(0, 7).Select(i => startLocal.AddDays(i)).ToArray();
        _dailyXAxisLabels = days.Select(d => d.ToString("MM/dd")).ToArray();

        // 總量
        var dailyTotals = days
            .Select(d => rows
                .Where(r => r.CreatedAt.ToLocalTime().Date == d)
                .Sum(r => (double)(r.InputTokens + r.OutputTokens)))
            .ToArray();

        _tokenDailyTotalSeries = dailyTotals.Sum() > 0
            ? [ new ChartSeries { Name = "Total tokens", Data = dailyTotals } ]
            : [];

        // by-model：抓所有出現過的 model bucket（normalize 至 sonnet/opus/haiku/other）
        var modelBuckets = new[] { "sonnet", "opus", "haiku", "other" };
        var byModelSeries = new List<ChartSeries>();
        foreach (var bucket in modelBuckets)
        {
            var data = days
                .Select(d => rows
                    .Where(r => r.CreatedAt.ToLocalTime().Date == d && NormalizeModel(r.Model) == bucket)
                    .Sum(r => (double)(r.InputTokens + r.OutputTokens)))
                .ToArray();
            if (data.Sum() > 0)
                byModelSeries.Add(new ChartSeries { Name = bucket, Data = data });
        }
        _tokenDailyByModelSeries = byModelSeries;
    }

    /// <summary>過去 30 天 active team 新增 + completed task 數量</summary>
    private async Task LoadTeamTaskMonthly30DaysAsync()
    {
        // patch1-E：30 天 window 以 local date 為基準 / query 用 local→UTC boundary（DB 存 UTC）
        var todayLocal = DateTime.Now.Date;
        var startLocal = todayLocal.AddDays(-29);
        var endLocal   = todayLocal.AddDays(1);
        var startUtc   = startLocal.ToUniversalTime();
        var endUtc     = endLocal.ToUniversalTime();

        var teams = await Db.AgentTeams
            .AsNoTracking()
            .Where(t => t.CreatedAt >= startUtc && t.CreatedAt < endUtc)
            .Select(t => new { t.CreatedAt, t.Status })
            .ToListAsync();

        var tasks = await Db.AgentTasks
            .AsNoTracking()
            .Where(t => t.CompletedAt != null && t.CompletedAt >= startUtc && t.CompletedAt < endUtc)
            .Select(t => new { CompletedAt = t.CompletedAt!.Value })
            .ToListAsync();

        var days = Enumerable.Range(0, 30).Select(i => startLocal.AddDays(i)).ToArray();
        _monthlyXAxisLabels = days.Select(d => d.ToString("MM/dd")).ToArray();

        // active team = Status='active' 當日新增（與 spec「active team」一致 / 不是「該日存活」）
        var teamData = days
            .Select(d => (double)teams.Count(t => t.CreatedAt.ToLocalTime().Date == d && t.Status == "active"))
            .ToArray();

        var taskData = days
            .Select(d => (double)tasks.Count(t => t.CompletedAt.ToLocalTime().Date == d))
            .ToArray();

        var series = new List<ChartSeries>();
        if (teamData.Sum() > 0) series.Add(new ChartSeries { Name = "Active Teams (new)", Data = teamData });
        if (taskData.Sum() > 0) series.Add(new ChartSeries { Name = "Completed Tasks",     Data = taskData });
        _teamTaskMonthlySeries = series;
    }

    /// <summary>model name normalize 至 4 bucket（sonnet/opus/haiku/other）</summary>
    private static string NormalizeModel(string? model)
    {
        if (string.IsNullOrEmpty(model)) return "other";
        var lower = model.ToLowerInvariant();
        if (lower.Contains("sonnet")) return "sonnet";
        if (lower.Contains("opus"))   return "opus";
        if (lower.Contains("haiku"))  return "haiku";
        return "other";
    }
}
