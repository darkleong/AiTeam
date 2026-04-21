using AiTeam.Dashboard.Services;
using AiTeam.Shared.Dtos;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Home;

/// <summary>
/// Stage 32：Mock 情境觸發卡片。透過 /internal/mock/scenario 呼叫 Bot 端 MockScenarioService，
/// 讓老闆在 Dashboard 就能觸發各種測試情境（FF 十五：Discord/Dashboard 功能平等）。
/// </summary>
public partial class MockScenarioCard
{
    // DbContext 相關服務透過自建 scope 取得，避免與父組件（Home.razor）並行使用同一個
    // circuit-scoped AppDbContext 觸發 EF Core "A second operation was started" 例外。
    [Inject] private IServiceScopeFactory ScopeFactory { get; set; } = null!;
    [Inject] private DashboardBotService  BotService   { get; set; } = null!;
    [Inject] private ISnackbar            Snackbar     { get; set; } = null!;

    private bool   _mockModeEnabled;
    private bool   _isSubmitting;
    private string _scenario = "new_feature";
    private string _title    = "";
    private string _project  = "";
    private List<ProjectDto> _projects = [];

    protected override async Task OnInitializedAsync()
    {
        await using var scope = ScopeFactory.CreateAsyncScope();
        var appSettings    = scope.ServiceProvider.GetRequiredService<DashboardAppSettingsService>();
        var projectService = scope.ServiceProvider.GetRequiredService<DashboardProjectService>();

        var setting = await appSettings.GetAsync("MockMode");
        _mockModeEnabled = bool.TryParse(setting?.Value, out var v) && v;

        var all = await projectService.GetAllProjectsAsync();
        _projects = all.Where(p => p.IsActive).OrderBy(p => p.Name).ToList();
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
