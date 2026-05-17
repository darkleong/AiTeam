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
    private ISnackbar Snackbar { get; set; } = null!;

    #endregion

    #region Private Variables

    private List<ProjectDto> _projects = [];
    private ProjectDto?      _selectedProject;
    private bool             _isDrawerOpen;
    private string?          _loadError;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _projects = await ProjectService.GetAllProjectsAsync();
        }
        catch (Exception ex)
        {
            _loadError = $"專案清單載入失敗：{ex.Message}";
            Snackbar.Add(_loadError, Severity.Error);
        }
    }

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
            _projects.Insert(0, created);
    }

    private async Task ToggleIsActiveAsync(ProjectDto project, bool isActive)
    {
        try
        {
            await ProjectService.ToggleProjectActiveAsync(project.Id, isActive);
            project.IsActive = isActive;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"狀態切換失敗：{ex.Message}", Severity.Error);
        }
    }

    #endregion
}
