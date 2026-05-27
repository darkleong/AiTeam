using AiTeam.Data;
using Microsoft.AspNetCore.Components;
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
/// </summary>
public partial class RecordsTrends : ComponentBase
{
    [Inject] private AppDbContext             Db     { get; set; } = null!;
    [Inject] private ILogger<RecordsTrends>   Logger { get; set; } = null!;

    private string[]          _dailyXAxisLabels        = [];
    private string[]          _monthlyXAxisLabels      = [];
    private List<ChartSeries> _tokenDailyTotalSeries   = [];
    private List<ChartSeries> _tokenDailyByModelSeries = [];
    private List<ChartSeries> _teamTaskMonthlySeries   = [];

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
    }

    /// <summary>過去 7 天 token 消耗（總量 + by-model）</summary>
    private async Task LoadTokenDaily7DaysAsync()
    {
        // 7 天 window：今天往前 6 天起 / 含今天共 7 個 bucket
        var todayUtc = DateTime.UtcNow.Date;
        var startUtc = todayUtc.AddDays(-6);
        var endUtc   = todayUtc.AddDays(1); // exclusive

        var rows = await Db.AgentTokenUsages
            .AsNoTracking()
            .Where(t => t.CreatedAt >= startUtc && t.CreatedAt < endUtc)
            .Select(t => new { t.CreatedAt, t.InputTokens, t.OutputTokens, t.Model })
            .ToListAsync();

        // 7 個 daily bucket（UTC date）
        var days = Enumerable.Range(0, 7).Select(i => startUtc.AddDays(i)).ToArray();
        _dailyXAxisLabels = days.Select(d => d.ToLocalTime().ToString("MM/dd")).ToArray();

        // 總量
        var dailyTotals = days
            .Select(d => rows
                .Where(r => r.CreatedAt.Date == d)
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
                    .Where(r => r.CreatedAt.Date == d && NormalizeModel(r.Model) == bucket)
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
        var todayUtc = DateTime.UtcNow.Date;
        var startUtc = todayUtc.AddDays(-29);
        var endUtc   = todayUtc.AddDays(1);

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

        var days = Enumerable.Range(0, 30).Select(i => startUtc.AddDays(i)).ToArray();
        _monthlyXAxisLabels = days.Select(d => d.ToLocalTime().ToString("MM/dd")).ToArray();

        // active team = Status='active' 當日新增（與 spec「active team」一致 / 不是「該日存活」）
        var teamData = days
            .Select(d => (double)teams.Count(t => t.CreatedAt.Date == d && t.Status == "active"))
            .ToArray();

        var taskData = days
            .Select(d => (double)tasks.Count(t => t.CompletedAt.Date == d))
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
