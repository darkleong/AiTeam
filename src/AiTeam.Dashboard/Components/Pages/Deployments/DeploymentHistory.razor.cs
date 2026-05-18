using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Deployments;

public partial class DeploymentHistory
{
    #region Dependencies

    [Inject]
    private DashboardTaskService TaskService { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    #endregion

    #region Private Variables

    private List<TaskItemDto> _deployments = [];
    private TaskItemDto?      _selectedTask;
    private List<TaskLogDto>  _selectedLogs = [];
    private bool              _isDrawerOpen;
    private string?           _loadError;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // 部署紀錄：篩選 Ops Agent 執行的任務
            var result = await TaskService.GetTasksPagedAsync(pageSize: 200);
            _deployments = result.Items
                .Where(t => t.AssignedAgent == AiTeam.Shared.Constants.AgentNames.Ops
                         || t.TriggeredBy == "GitHub")
                .ToList();
        }
        catch (Exception ex)
        {
            _loadError = $"部署紀錄載入失敗：{ex.Message}";
            Snackbar.Add(_loadError, Severity.Error);
        }
    }

    #endregion

    #region Private Methods

    private async Task OnRowClickAsync(TableRowClickEventArgs<TaskItemDto> args)
    {
        _selectedTask = args.Item;
        if (_selectedTask is null) return;

        try
        {
            _selectedLogs = await TaskService.GetTaskLogsAsync(_selectedTask.Id);
            _isDrawerOpen = true;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"載入部署記錄失敗：{ex.Message}", Severity.Error);
        }
    }

    #endregion
}
