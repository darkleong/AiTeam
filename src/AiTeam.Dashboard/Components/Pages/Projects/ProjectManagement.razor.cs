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

    // 新增表單
    private bool   _showCreateForm;
    private string _newName        = "";
    private string _newRepoUrl     = "";
    private string _newTechStack   = "";
    private bool   _isCreating;
    private string? _createError;

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

    private void ToggleCreateForm()
    {
        _showCreateForm = !_showCreateForm;
        _createError    = null;
        _newName        = "";
        _newRepoUrl     = "";
        _newTechStack   = "";
    }

    private async Task CreateProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(_newName))
        {
            _createError = "專案名稱為必填";
            return;
        }

        _isCreating  = true;
        _createError = null;

        try
        {
            var created = await ProjectService.CreateProjectAsync(_newName, _newRepoUrl, _newTechStack);
            _projects.Insert(0, created);
            _showCreateForm = false;
            _newName = _newRepoUrl = _newTechStack = "";
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

    private async Task ToggleIsActiveAsync(ProjectDto project, bool isActive)
    {
        await ProjectService.ToggleProjectActiveAsync(project.Id, isActive);
        project.IsActive = isActive;
    }

    #endregion
}
