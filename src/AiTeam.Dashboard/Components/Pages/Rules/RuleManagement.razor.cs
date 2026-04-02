namespace AiTeam.Dashboard.Components.Pages.Rules;

public partial class RuleManagement
{
    #region Dependencies

    [Inject]
    private DashboardRuleService RuleService { get; set; } = null!;

    #endregion

    #region Agent Options

    private record AgentOption(string Label, string Value);

    private readonly List<AgentOption> _agentOptions =
    [
        new("全域（所有 Agent）", ""),
        new("CEO",          AgentNames.Ceo),
        new("Dev",          AgentNames.Dev),
        new("Ops",          AgentNames.Ops),
        new("QA",           AgentNames.Qa),
        new("Doc",          AgentNames.Doc),
        new("Requirements", AgentNames.Requirements),
        new("Reviewer",     AgentNames.Reviewer),
        new("Release",      AgentNames.Release),
        new("Designer",     AgentNames.Designer),
    ];

    private static string GetAgentColor(string? agentName) => agentName switch
    {
        AgentNames.Ceo          => "#6366f1",
        AgentNames.Dev          => "#0284c7",
        AgentNames.Ops          => "#0891b2",
        AgentNames.Qa           => "#7c3aed",
        AgentNames.Doc          => "#6d28d9",
        AgentNames.Requirements => "#b45309",
        AgentNames.Reviewer     => "#be185d",
        AgentNames.Release      => "#047857",
        AgentNames.Designer     => "#c2410c",
        _                       => "#6c757d",  // 全域
    };

    #endregion

    #region Private Variables

    private List<Rule> _rules = [];

    // 新增表單
    private bool    _showCreateForm;
    private string  _newContent    = "";
    private string  _newAgentName  = "";
    private int     _newSortOrder  = 0;
    private bool    _isCreating;
    private string? _createError;

    // 編輯狀態
    private Guid?  _editingId;
    private string _editContent   = "";
    private string _editAgentName = "";
    private int    _editSortOrder = 0;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
        => _rules = await RuleService.GetAllRulesAsync();

    #endregion

    #region Private Methods

    private void ToggleCreateForm()
    {
        _showCreateForm = !_showCreateForm;
        _createError    = null;
        _newContent     = "";
        _newAgentName   = "";
        _newSortOrder   = _rules.Count > 0 ? _rules.Max(r => r.SortOrder) + 10 : 10;
    }

    private async Task CreateRuleAsync()
    {
        if (string.IsNullOrWhiteSpace(_newContent))
        {
            _createError = "規則內容為必填";
            return;
        }

        _isCreating  = true;
        _createError = null;

        try
        {
            var created = await RuleService.CreateRuleAsync(_newContent, _newAgentName, _newSortOrder);
            _rules.Add(created);
            _rules = [.. _rules.OrderBy(r => r.SortOrder).ThenBy(r => r.CreatedAt)];
            _showCreateForm = false;
            _newContent     = "";
            _newAgentName   = "";
        }
        catch (Exception ex)
        {
            _createError = $"新增失敗：{ex.Message}";
        }
        finally
        {
            _isCreating = false;
        }
    }

    private void StartEdit(Rule rule)
    {
        _editingId    = rule.Id;
        _editContent  = rule.Content;
        _editAgentName = rule.AgentName ?? "";
        _editSortOrder = rule.SortOrder;
    }

    private void CancelEdit() => _editingId = null;

    private async Task SaveEditAsync(Guid id)
    {
        await RuleService.UpdateRuleAsync(id, _editContent, _editAgentName, _editSortOrder);
        var rule = _rules.FirstOrDefault(r => r.Id == id);
        if (rule is not null)
        {
            rule.Content   = _editContent;
            rule.AgentName = string.IsNullOrWhiteSpace(_editAgentName) ? null : _editAgentName;
            rule.SortOrder = _editSortOrder;
            _rules = [.. _rules.OrderBy(r => r.SortOrder).ThenBy(r => r.CreatedAt)];
        }
        _editingId = null;
    }

    private async Task ToggleActiveAsync(Rule rule, bool isActive)
    {
        await RuleService.ToggleRuleActiveAsync(rule.Id, isActive);
        rule.IsActive = isActive;
    }

    private async Task DeleteRuleAsync(Guid id)
    {
        await RuleService.DeleteRuleAsync(id);
        _rules.RemoveAll(r => r.Id == id);
    }

    #endregion
}
