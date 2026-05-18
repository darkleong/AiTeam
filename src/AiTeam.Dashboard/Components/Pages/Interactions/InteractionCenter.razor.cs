using AiTeam.Data.Hubs;
using AiTeam.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Interactions;

public partial class InteractionCenter : IAsyncDisposable
{
    #region Dependencies

    [Inject]
    private DashboardTaskService TaskService { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IConfiguration Configuration { get; set; } = null!;

    [Inject]
    private InteractionRespondService RespondService { get; set; } = null!;

    #endregion

    #region Private Variables

    private List<BossInteractionDto> _pending      = [];
    private List<BossInteractionDto> _historyItems = [];
    private bool                     _isLoading      = true;
    private bool                     _historyLoading = false;
    private string?                  _loadError;
    private string?                  _historyError;

    // 歷史紀錄篩選條件
    private string?    _typeFilter   = null;
    private string     _sourceFilter = "";
    private DateRange? _dateRange    = null;

    #endregion

    #region Private Variables — SignalR

    private HubConnection? _hubConnection;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await ConnectSignalRAsync();
    }

    #endregion

    #region Private Methods — Data

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadError = null;
        try
        {
            _pending = await TaskService.GetPendingInteractionsAsync();
        }
        catch (Exception ex)
        {
            _loadError = $"載入待處理項目失敗：{ex.Message}";
            Snackbar.Add(_loadError, Severity.Error);
        }
        finally
        {
            _isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        _historyLoading = true;
        _historyError   = null;
        await InvokeAsync(StateHasChanged);
        try
        {
            DateTime? from = _dateRange?.Start?.ToUniversalTime();
            DateTime? to   = _dateRange?.End?.ToUniversalTime();
            var (items, _) = await TaskService.GetInteractionHistoryAsync(
                page: 1, pageSize: 200,
                typeFilter:   _typeFilter,
                sourceFilter: string.IsNullOrEmpty(_sourceFilter) ? null : _sourceFilter,
                from: from, to: to);
            _historyItems = items;
        }
        catch (Exception ex)
        {
            _historyError = $"載入歷史紀錄失敗：{ex.Message}";
            Snackbar.Add(_historyError, Severity.Error);
        }
        finally
        {
            _historyLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OnTypeFilterChanged(string? value)
    {
        _typeFilter = value;
        await LoadHistoryAsync();
    }

    private async Task OnSourceFilterChanged(string? value)
    {
        _sourceFilter = value ?? "";
        await LoadHistoryAsync();
    }

    private async Task OnDateRangeChanged(DateRange? range)
    {
        _dateRange = range;
        await LoadHistoryAsync();
    }

    private async Task HandleResponseAsync(ResponseRequest request)
    {
        try
        {
            var responded = await RespondService.RespondAsync(request.InteractionId, request.Action, request.Content);
            if (responded)
            {
                Snackbar.Add("回覆成功！", Severity.Success);
                await LoadAsync();
            }
            else
            {
                Snackbar.Add("此互動已被另一通道回覆，已重新整理。", Severity.Warning);
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"回覆時發生錯誤：{ex.Message}", Severity.Error);
        }
    }

    #endregion

    #region Private Methods — SignalR

    private async Task ConnectSignalRAsync()
    {
        var hubBaseUrl = Configuration["Dashboard:HubBaseUrl"];
        var hubUrl = string.IsNullOrEmpty(hubBaseUrl)
            ? Navigation.ToAbsoluteUri("/hubs/agent-status").ToString()
            : $"{hubBaseUrl.TrimEnd('/')}/hubs/agent-status";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On(AgentStatusHub.ReceiveInteractionUpdate, async () =>
        {
            await InvokeAsync(LoadAsync);
        });

        await _hubConnection.StartAsync();
    }

    #endregion

    #region Static Helpers

    internal static string GetInteractionIcon(string type) => type switch
    {
        "ceo_confirm"             => Icons.Material.Filled.Assignment,
        "exec_confirm"            => Icons.Material.Filled.PlayArrow,
        "proposal"                => Icons.Material.Filled.Description,
        "kickoff"                 => Icons.Material.Filled.Groups,
        "design"                  => Icons.Material.Filled.DesignServices,
        "devplan_escalate"        => Icons.Material.Filled.Warning,
        "merge_notify"            => Icons.Material.Filled.CheckCircle,
        "intervention"            => Icons.Material.Filled.Error,
        // Stage 43
        "dev_plan_unable"         => Icons.Material.Filled.WarningAmber,
        "dev_failed_intervention" => Icons.Material.Filled.BuildCircle,
        "qa_failed_intervention"  => Icons.Material.Filled.BugReport,
        "sage_escalate"           => Icons.Material.Filled.NoteAlt,
        // Stage 46-FF 三十五
        "split_task_proposal"     => Icons.Material.Filled.AccountTree,
        "epic_partial_paused"     => Icons.Material.Filled.PauseCircle,
        _                         => Icons.Material.Filled.Notifications
    };

    internal static Color GetInteractionColor(string type) => type switch
    {
        "devplan_escalate"        => Color.Warning,
        "merge_notify"            => Color.Success,
        "intervention"            => Color.Error,
        "proposal"                => Color.Info,
        "kickoff"                 => Color.Info,
        "design"                  => Color.Info,
        // Stage 43：介入類用 Warning（amber，與 failed Error 紅色分離）
        "dev_plan_unable"         => Color.Warning,
        "dev_failed_intervention" => Color.Warning,
        "qa_failed_intervention"  => Color.Warning,
        "sage_escalate"           => Color.Warning,
        // Stage 46-FF 三十五
        "split_task_proposal"     => Color.Info,
        "epic_partial_paused"     => Color.Warning,
        _                         => Color.Default
    };

    internal static string GetInteractionLabel(string type) => type switch
    {
        "ceo_confirm"             => "CEO 決策確認",
        "exec_confirm"            => "Agent 執行確認",
        "proposal"                => "提案確認",
        "kickoff"                 => "Kickoff 確認",
        "design"                  => "設計確認",
        "devplan_escalate"        => "Dev_plan 升級",
        "merge_notify"            => "全流程完成",
        "intervention"            => "需要介入",
        // Stage 43
        "dev_plan_unable"         => "DevPlan 重產失敗",
        "dev_failed_intervention" => "Dev 失敗介入",
        "qa_failed_intervention"  => "QA 失敗介入",
        "sage_escalate"           => "Sage 歸檔升級",
        // Stage 46-FF 三十五
        "split_task_proposal"     => "拆任務提案",
        "epic_partial_paused"     => "Epic 部分暫停",
        _                         => type
    };

    private static Color GetActionColor(string? action) => action switch
    {
        "confirm_yes" or "exec_yes" or "propose_yes" or "kickoff_continue" or "design_continue" => Color.Success,
        "confirm_no"  or "exec_no"  or "propose_no"  or "kickoff_stop"     or "design_stop"     => Color.Error,
        "kickoff_restart"                                                                         => Color.Warning,
        "devplan_skip"                                                                            => Color.Warning,
        "devplan_abort"                                                                           => Color.Error,
        "propose_adjust" or "kickoff_modify" or "design_modify"                                  => Color.Info,
        _                                                                                         => Color.Default
    };

    #endregion

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }

    #endregion
}

/// <summary>InteractionCard → InteractionCenter 的回覆請求。Stage 28b 新增 Content（文字輸入類回覆）。</summary>
public record ResponseRequest(Guid InteractionId, string Action, string? Content = null);
