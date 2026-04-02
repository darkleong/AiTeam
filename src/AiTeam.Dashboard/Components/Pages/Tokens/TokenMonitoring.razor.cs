using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Tokens;

public partial class TokenMonitoring
{
    #region Dependencies

    [Inject]
    private DashboardTokenService TokenService { get; set; } = null!;

    [Inject]
    private DashboardAppSettingsService AppSettingsService { get; set; } = null!;

    #endregion

    #region Private Variables

    private bool             _loading = true;
    private string           _period  = "today";
    private TokenSummaryDto  _summary = new();
    private ChartSeries[]    _chartSeries = [];
    private string[]         _chartLabels = [];
    private ChartOptions     _chartOptions = new() { YAxisTicks = 5 };

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
        => await LoadDataAsync();

    #endregion

    #region Private Methods

    private async Task SetPeriodAsync(string period)
    {
        _period = period;
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _loading = true;
        StateHasChanged();

        var now = DateTime.UtcNow;
        var (from, to) = _period switch
        {
            "week"  => (now.Date.AddDays(-(int)now.DayOfWeek), now),
            "month" => (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), now),
            _       => (now.Date.ToUniversalTime(), now)
        };

        // 讀取費率設定
        var inputSetting  = await AppSettingsService.GetAsync("TokenPricing:InputPer1kUsd");
        var outputSetting = await AppSettingsService.GetAsync("TokenPricing:OutputPer1kUsd");
        decimal.TryParse(inputSetting?.Value,  System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var inputRate);
        decimal.TryParse(outputSetting?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var outputRate);
        if (inputRate  == 0) inputRate  = 0.003m;
        if (outputRate == 0) outputRate = 0.015m;

        _summary = await TokenService.GetSummaryAsync(from, to, inputRate, outputRate);

        BuildChart();

        _loading = false;
        StateHasChanged();
    }

    private void BuildChart()
    {
        if (_summary.DailyDataPoints.Count == 0)
        {
            _chartSeries = [];
            _chartLabels = [];
            return;
        }

        // 取得所有日期標籤（X 軸）
        var dates = _summary.DailyDataPoints
            .Select(p => p.Date.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        _chartLabels = dates.Select(d => d.ToString("MM/dd")).ToArray();

        // 每個 Agent 一條線
        var agents = _summary.DailyDataPoints
            .Select(p => p.AgentName)
            .Distinct()
            .OrderBy(a => a)
            .ToList();

        _chartSeries = agents.Select(agent =>
        {
            var dataByDate = _summary.DailyDataPoints
                .Where(p => p.AgentName == agent)
                .ToDictionary(p => p.Date.Date, p => (double)p.TotalTokens);

            return new ChartSeries
            {
                Name = agent,
                Data = dates.Select(d => dataByDate.TryGetValue(d, out var v) ? v : 0.0).ToArray()
            };
        }).ToArray();
    }

    #endregion
}
