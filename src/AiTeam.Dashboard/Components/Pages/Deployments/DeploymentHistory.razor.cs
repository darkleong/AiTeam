using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Deployments;

public partial class DeploymentHistory
{
    #region Dependencies

    [Inject]
    private DashboardTaskService TaskService { get; set; } = null!;

    #endregion

    #region Private Variables

    private List<TaskItemDto> _deployments = [];
    private TaskItemDto?      _selectedTask;
    private List<TaskLogDto>  _selectedLogs = [];
    private bool              _isDrawerOpen;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
    {
        // 部署紀錄：篩選 Ops Agent 執行的任務
        var result = await TaskService.GetTasksPagedAsync(pageSize: 200);
        _deployments = result.Items
            .Where(t => t.AssignedAgent == AiTeam.Shared.Constants.AgentNames.Ops
                     || t.TriggeredBy == "GitHub")
            .ToList();
    }

    #endregion

    #region Private Methods

    private async Task OnRowClickAsync(TableRowClickEventArgs<TaskItemDto> args)
    {
        _selectedTask = args.Item;
        if (_selectedTask is null) return;

        _selectedLogs = await TaskService.GetTaskLogsAsync(_selectedTask.Id);
        _isDrawerOpen = true;
    }

    #endregion
}
