using AiTeam.Dashboard.Services;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Home;

/// <summary>
/// Stage 32：Mock 情境觸發卡片。透過 /internal/mock/scenario 呼叫 Bot 端 MockScenarioService，
/// 讓老闆在 Dashboard 就能觸發各種測試情境（FF 十五：Discord/Dashboard 功能平等）。
/// </summary>
public partial class MockScenarioCard
{
    [Inject] private DashboardAppSettingsService AppSettingsService { get; set; } = null!;
    [Inject] private DashboardBotService         BotService          { get; set; } = null!;
    [Inject] private ISnackbar                   Snackbar            { get; set; } = null!;

    private bool   _mockModeEnabled;
    private bool   _isSubmitting;
    private string _scenario = "new_feature";
    private string _title    = "";
    private string _project  = "";

    protected override async Task OnInitializedAsync()
    {
        var setting = await AppSettingsService.GetAsync("MockMode");
        _mockModeEnabled = bool.TryParse(setting?.Value, out var v) && v;
    }

    private async Task TriggerAsync()
    {
        if (!_mockModeEnabled || string.IsNullOrWhiteSpace(_scenario)) return;

        _isSubmitting = true;
        var title    = string.IsNullOrWhiteSpace(_title)   ? null : _title.Trim();
        var project  = string.IsNullOrWhiteSpace(_project) ? null : _project.Trim();
        var ok       = await BotService.TriggerMockScenarioAsync(_scenario, title, project);
        _isSubmitting = false;

        if (ok)
        {
            Snackbar.Add($"Mock 情境已送出（{_scenario}），請至任務中心觀察進度", Severity.Success);
            _title = "";
            _project = "";
        }
        else
        {
            Snackbar.Add("觸發失敗，請確認 Bot 服務正常並已啟用 Mock Mode", Severity.Error);
        }
    }
}
