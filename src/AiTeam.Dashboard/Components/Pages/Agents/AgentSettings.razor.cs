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
    private string?                  _saveMessage;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
    {
        _agents = await AgentService.GetAgentConfigsAsync();

        // 載入 Notion 規則
        foreach (var agent in _agents)
        {
            var rules = await TrustLevelService.GetRulesAsync(agent.Name);
            agent.Rules = rules;
            _trustLevels[agent.Id] = agent.TrustLevel;
        }
    }

    #endregion

    #region Private Methods

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
