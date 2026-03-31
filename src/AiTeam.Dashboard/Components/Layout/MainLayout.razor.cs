namespace AiTeam.Dashboard.Components.Layout;

public partial class MainLayout
{
    #region Private Variables

    private string _theme = "light";

    #endregion

    #region Private Methods

    private void ToggleTheme()
        => _theme = _theme == "light" ? "dark" : "light";

    #endregion
}
