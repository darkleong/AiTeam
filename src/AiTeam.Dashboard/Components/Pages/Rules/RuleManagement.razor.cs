namespace AiTeam.Dashboard.Components.Pages.Rules;

public partial class RuleManagement
{
    #region Dependencies

    [Inject]
    private DashboardRuleService RuleService { get; set; } = null!;

    #endregion

    #region Private Variables

    private List<Rule> _rules = [];

    // 新增表單
    private bool   _showCreateForm;
    private string _newContent   = "";
    private int    _newSortOrder = 0;
    private bool   _isCreating;
    private string? _createError;

    // 編輯狀態
    private Guid?  _editingId;
    private string _editContent   = "";
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
            var created = await RuleService.CreateRuleAsync(_newContent, _newSortOrder);
            _rules.Add(created);
            _rules = [.. _rules.OrderBy(r => r.SortOrder).ThenBy(r => r.CreatedAt)];
            _showCreateForm = false;
            _newContent     = "";
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
        _editSortOrder = rule.SortOrder;
    }

    private void CancelEdit() => _editingId = null;

    private async Task SaveEditAsync(Guid id)
    {
        await RuleService.UpdateRuleAsync(id, _editContent, _editSortOrder);
        var rule = _rules.FirstOrDefault(r => r.Id == id);
        if (rule is not null)
        {
            rule.Content   = _editContent;
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
