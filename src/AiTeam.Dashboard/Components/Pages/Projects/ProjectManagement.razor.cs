using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Projects;

public partial class ProjectManagement
{
    #region Dependencies

    [Inject]
    private DashboardProjectService ProjectService { get; set; } = null!;

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

    #endregion
}
