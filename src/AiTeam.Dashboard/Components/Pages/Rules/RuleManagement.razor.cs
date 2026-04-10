using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Rules;

public partial class RuleManagement
{
    #region Dependencies

    [Inject]
    private DashboardRuleService RuleService { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

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

    private static Color GetAgentChipColor(string? agentName) => agentName switch
    {
        AgentNames.Ceo          => Color.Primary,
        AgentNames.Dev          => Color.Info,
        AgentNames.Ops          => Color.Tertiary,
        AgentNames.Qa           => Color.Secondary,
        AgentNames.Doc          => Color.Secondary,
        AgentNames.Requirements => Color.Warning,
        AgentNames.Reviewer     => Color.Error,
        AgentNames.Release      => Color.Success,
        AgentNames.Designer     => Color.Warning,
        AgentNames.Pm           => Color.Info,
        _                       => Color.Default,  // 全域
    };

    #endregion

    #region Private Variables

    private List<Rule> _rules = [];

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
        => _rules = await RuleService.GetAllRulesAsync();

    #endregion

    #region Private Methods

    private List<(string Label, string Value)> GetDialogAgentOptions()
        => _agentOptions.Select(o => (o.Label, o.Value)).ToList();

    private async Task OpenCreateRuleDialogAsync()
    {
        var nextSortOrder = _rules.Count > 0 ? _rules.Max(r => r.SortOrder) + 10 : 10;

        var parameters = new DialogParameters<RuleFormDialog>
        {
            { d => d.AgentOptions,   GetDialogAgentOptions() },
            { d => d.NextSortOrder,  nextSortOrder }
        };

        var dialog = await DialogService.ShowAsync<RuleFormDialog>("新增規則", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false } && result.Data is Rule created)
        {
            _rules.Add(created);
            _rules = [.. _rules.OrderBy(r => r.SortOrder).ThenBy(r => r.CreatedAt)];
        }
    }

    private async Task OpenEditRuleDialogAsync(Rule rule)
    {
        var parameters = new DialogParameters<RuleFormDialog>
        {
            { d => d.EditingRule,  rule },
            { d => d.AgentOptions, GetDialogAgentOptions() }
        };

        var dialog = await DialogService.ShowAsync<RuleFormDialog>("編輯規則", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false })
            _rules = [.. _rules.OrderBy(r => r.SortOrder).ThenBy(r => r.CreatedAt)];
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
