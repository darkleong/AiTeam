using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Rules;

public partial class RuleManagement
{
    #region Dependencies

    [Inject]
    private DashboardRuleService RuleService { get; set; } = null!;

    [Inject]
    private DashboardBotService BotService { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    #endregion

    #region Agent Options

    private record AgentOption(string Label, string Value);

    // Stage 86 子項 0：砍 8 個 v4 dead 角色（Dev/Ops/Qa/Doc/Requirements/Reviewer/Release/Designer）
    // + Pm（v5 dispatcher / RulesService 0 caller）— 留全域 + CEO（Stage 85 verify CEO 唯一 caller）/ DB row 變孤兒不刪
    private readonly List<AgentOption> _agentOptions =
    [
        new("全域（所有 Agent）", ""),
        new("CEO",                AgentNames.Ceo),
    ];

    private static Color GetAgentChipColor(string? agentName) => agentName switch
    {
        AgentNames.Ceo => Color.Primary,
        _              => Color.Default,  // 全域 / 孤兒 row fallback
    };

    #endregion

    #region Private Variables

    private List<Rule> _rules    = [];
    private bool       _isReloading;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
        => _rules = await RuleService.GetAllRulesAsync();

    #endregion

    #region Private Methods

    private async Task ReloadCacheAsync()
    {
        _isReloading = true;
        var ok = await BotService.ReloadCacheAsync("rules");
        _isReloading = false;
        Snackbar.Add(ok ? "已套用變更（規則快取已更新）" : "套用失敗，請確認 Bot 服務正常",
            ok ? Severity.Success : Severity.Error);
    }

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
