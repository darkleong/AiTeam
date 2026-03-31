namespace AiTeam.Dashboard.Components.Pages.Auth;

public partial class Login
{
    #region Parameters

    // Static SSR：從 HTTP Context 讀取 query string（error / ReturnUrl）
    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = null!;

    #endregion

    #region Private Variables

    private string? _errorMessage;
    private string _returnUrl = "/";

    #endregion

    #region Override Methods

    protected override void OnInitialized()
    {
        var query = HttpContext.Request.Query;

        if (query.ContainsKey("error"))
            _errorMessage = "帳號或密碼錯誤";

        var returnUrl = query["ReturnUrl"].FirstOrDefault();
        if (!string.IsNullOrEmpty(returnUrl))
            _returnUrl = returnUrl;
    }

    #endregion
}
