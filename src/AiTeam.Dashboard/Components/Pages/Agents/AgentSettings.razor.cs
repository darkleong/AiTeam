namespace AiTeam.Dashboard.Components.Pages.Agents;

public partial class AgentSettings
{
    #region Dependencies

    [Inject]
    private DashboardAgentService AgentService { get; set; } = null!;

    [Inject]
    private NotionTrustLevelService TrustLevelService { get; set; } = null!;

    #endregion

    #region Private Variables

    private List<AgentConfigDto>     _agents       = [];
    private Dictionary<Guid, int>    _trustLevels  = [];
    private bool                     _isSaving;
    private bool                     _isTogglingActive;
    private string?                  _saveMessage;
    private string?                  _loadError;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _agents = await AgentService.GetAgentConfigsAsync();

            // 載入 Notion 規則（失敗不阻斷頁面）
            foreach (var agent in _agents)
            {
                var rules = await TrustLevelService.GetRulesAsync(agent.Name);
                agent.Rules = rules;
                _trustLevels[agent.Id] = agent.TrustLevel;
            }
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

    private async Task SaveTrustLevelAsync(AgentConfigDto agent)
    {
        _isSaving = true;
        _saveMessage = null;

        await TrustLevelService.UpdateTrustLevelAsync(
            agent.Name,
            _trustLevels[agent.Id]);

        _saveMessage = $"{agent.Name} 信任等級已儲存為 Lv{_trustLevels[agent.Id]}";
        _isSaving    = false;
    }

    #endregion
}
