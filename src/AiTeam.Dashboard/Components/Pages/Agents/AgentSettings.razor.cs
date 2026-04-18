using AiTeam.Dashboard.Components.Pages.Agents.Dialogs;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Agents;

public partial class AgentSettings
{
    #region Dependencies

    [Inject]
    private DashboardAgentService AgentService { get; set; } = null!;

    [Inject]
    private DashboardBotService BotService { get; set; } = null!;

    [Inject]
    private DashboardAppSettingsService AppSettingsService { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    #endregion

    #region Private Variables

    private List<AgentConfigDto>  _agents         = [];
    private Dictionary<Guid, int> _trustLevels    = [];
    private AgentConfigDto?       _selectedAgent;
    private bool                  _isSaving;
    private bool                  _isTogglingActive;
    private string?               _saveMessage;
    private string?               _loadError;

    // 重啟 Bot
    private bool  _showRestartConfirm;
    private bool  _isRestarting;

    // 套用變更
    private bool  _isReloading;

    // 系統設定
    private bool _skipCeoConfirm;
    private bool _mockMode;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _agents = await AgentService.GetAgentConfigsAsync();
            foreach (var agent in _agents)
                _trustLevels[agent.Id] = agent.TrustLevel;

            var skipSetting = await AppSettingsService.GetAsync("SkipCeoConfirm");
            _skipCeoConfirm = bool.TryParse(skipSetting?.Value, out var v) && v;

            var mockSetting = await AppSettingsService.GetAsync("MockMode");
            _mockMode = bool.TryParse(mockSetting?.Value, out var mv) && mv;
        }
        catch (Exception ex)
        {
            _loadError = ex.Message;
        }
    }

    #endregion

    #region Private Methods

    private async Task ToggleIsActiveAsync(AgentConfigDto agent, bool newValue)
    {
        if (_isTogglingActive) return;
        _isTogglingActive = true;

        agent.IsActive = await AgentService.UpdateIsActiveAsync(agent.Id, newValue);
        _saveMessage = $"{agent.Name} 已{(agent.IsActive ? "啟用" : "停用")}";

        _isTogglingActive = false;
    }

    private async Task OpenCreateAgentDialogAsync()
    {
        var dialog = await DialogService.ShowAsync<AgentCreateDialog>("新增 Agent");
        var result = await dialog.Result;
        if (result is { Canceled: false } && result.Data is AgentConfigDto created)
        {
            _agents.Add(created);
            _trustLevels[created.Id] = created.TrustLevel;
            _saveMessage = $"Agent「{created.Name}」已新增，重啟 Bot 後生效。";
        }
    }

    private async Task SaveTrustLevelAsync(AgentConfigDto agent)
    {
        _isSaving    = true;
        _saveMessage = null;

        await AgentService.UpdateTrustLevelAsync(agent.Id, _trustLevels[agent.Id]);
        agent.TrustLevel = _trustLevels[agent.Id];

        _saveMessage = $"{agent.Name} 信任等級已儲存為 Lv{_trustLevels[agent.Id]}";
        _isSaving    = false;
    }

    private async Task OnSkipCeoConfirmChanged(bool newValue)
    {
        _skipCeoConfirm = newValue;
        await AppSettingsService.UpsertAsync("SkipCeoConfirm", _skipCeoConfirm.ToString().ToLower());
        _saveMessage = $"「跳過 CEO 派工確認」已{(_skipCeoConfirm ? "啟用" : "停用")}，5 分鐘內自動生效";
    }

    private async Task OnMockModeChanged(bool newValue)
    {
        _mockMode = newValue;
        await AppSettingsService.UpsertAsync("MockMode", _mockMode.ToString().ToLower());
        _saveMessage = $"「Mock Mode」已{(_mockMode ? "啟用" : "停用")}，5 分鐘內自動生效";
    }

    private async Task ReloadCacheAsync()
    {
        _isReloading = true;
        var ok = await BotService.ReloadCacheAsync("all");
        _isReloading = false;
        Snackbar.Add(ok ? "已套用變更（規則與 Agent 快取已更新）" : "套用失敗，請確認 Bot 服務正常",
            ok ? Severity.Success : Severity.Error);
    }

    private async Task RestartBotAsync()
    {
        _isRestarting = true;
        var success = await BotService.RestartBotAsync();
        _showRestartConfirm = false;
        _saveMessage = success ? "Bot 重啟指令已送出，請稍候約 30 秒後確認上線狀態" : "重啟失敗，請確認 Bot 服務設定";
        _isRestarting = false;
    }

    #endregion
}
