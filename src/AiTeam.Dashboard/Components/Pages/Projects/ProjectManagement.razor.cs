using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Projects;

public partial class ProjectManagement
{
    #region Dependencies

    [Inject]
    private DashboardProjectService ProjectService { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    [Inject]
    private IDashboardNotificationService NotificationService { get; set; } = null!;

    #endregion

    #region Private Variables

    private List<ProjectDto> _projects = [];
    private ProjectDto?      _selectedProject;
    private bool             _isDrawerOpen;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
        => _projects = await ProjectService.GetAllProjectsAsync();

    #endregion

    #region Private Methods

    private Task OnRowClickAsync(TableRowClickEventArgs<ProjectDto> args)
    {
        _selectedProject = args.Item;
        _isDrawerOpen    = _selectedProject is not null;
        return Task.CompletedTask;
    }

    private void CloseDrawer() => _isDrawerOpen = false;

    private async Task OpenCreateProjectDialogAsync()
    {
        var dialog = await DialogService.ShowAsync<ProjectCreateDialog>("新增專案");
        var result = await dialog.Result;

        if (result is { Canceled: false } && result.Data is ProjectDto created)
        {
            _projects.Insert(0, created);
            await NotificationService.ShowSuccessAsync("已新增專案");
        }
    }

    private async Task ToggleIsActiveAsync(ProjectDto project, bool isActive)
    {
        try
        {
            await ProjectService.ToggleProjectActiveAsync(project.Id, isActive);
            project.IsActive = isActive;
            await NotificationService.ShowSuccessAsync($"專案已{(isActive ? "啟用" : "停用")}");
        }
        catch
        {
            await NotificationService.ShowErrorAsync("切換狀態失敗，請稍後重試");
        }
    }

    #endregion
}
