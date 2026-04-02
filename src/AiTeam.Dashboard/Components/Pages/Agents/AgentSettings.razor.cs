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

    #endregion

    #region Private Variables

    private List<AgentConfigDto>  _agents      = [];
    private Dictionary<Guid, int> _trustLevels = [];
    private bool                  _isSaving;
    private bool                  _isTogglingActive;
    private string?               _saveMessage;
    private string?               _loadError;

    // 重啟 Bot
    private bool  _showRestartConfirm;
    private bool  _isRestarting;

    // 系統設定
    private bool _skipCeoConfirm;

    // 新增 Agent 表單
    private bool    _showCreateForm;
    private string  _newName        = "";
    private string  _newDescription = "";
    private int     _newTrustLevel  = 1;
    private bool    _isCreating;
    private string? _createError;

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

    private async Task CreateAgentAsync()
    {
        _createError = null;
        if (string.IsNullOrWhiteSpace(_newName))
        {
            _createError = "名稱不可為空白。";
            return;
        }
        if (_agents.Any(a => a.Name.Equals(_newName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            _createError = $"Agent「{_newName.Trim()}」已存在。";
            return;
        }

        _isCreating = true;
        var created = await AgentService.CreateAgentAsync(_newName, _newDescription, _newTrustLevel);
        _agents.Add(created);
        _trustLevels[created.Id] = created.TrustLevel;

        _newName        = "";
        _newDescription = "";
        _newTrustLevel  = 1;
        _showCreateForm = false;
        _saveMessage    = $"Agent「{created.Name}」已新增，重啟 Bot 後生效。";
        _isCreating     = false;
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

    private async Task OnSkipCeoConfirmChanged(ChangeEventArgs e)
    {
        _skipCeoConfirm = (bool)e.Value!;
        await AppSettingsService.UpsertAsync("SkipCeoConfirm", _skipCeoConfirm.ToString().ToLower());
        _saveMessage = $"「跳過 CEO 派工確認」已{(_skipCeoConfirm ? "啟用" : "停用")}，5 分鐘內自動生效";
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
