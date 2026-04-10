using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace AiTeam.Dashboard.Components.Pages.Auth;

public partial class Login
{
    #region Injections

    [Inject] private NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region Private Variables

    private string? _errorMessage;
    private string _returnUrl = "/";

    #endregion

    #region Override Methods

    protected override void OnInitialized()
    {
        // Interactive Server 模式下 HttpContext 不可用，改從 NavigationManager.Uri 解析 query string
        var uri = new Uri(Navigation.Uri);
        var query = QueryHelpers.ParseQuery(uri.Query);

        if (query.ContainsKey("error"))
            _errorMessage = "帳號或密碼錯誤";

        if (query.TryGetValue("ReturnUrl", out var returnUrl) && !string.IsNullOrEmpty(returnUrl))
            _returnUrl = returnUrl!;
    }

    #endregion
}
