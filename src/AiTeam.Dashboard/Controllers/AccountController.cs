using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace AiTeam.Dashboard.Controllers;

/// <summary>
/// 帳號相關操作 Controller。
/// 登入 POST 由此處理，繞過 Blazor 的 UseAntiforgery 中介軟體，
/// 確保 SignInManager 能正確設定 Cookie。
/// </summary>
[Route("account")]
public class AccountController(SignInManager<IdentityUser> signInManager) : Controller
{
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginAsync(
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string? returnUrl)
    {
        var result = await signInManager.PasswordSignInAsync(
            email, password, isPersistent: true, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return Redirect("/");
        }

        return Redirect("/login?error=1");
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutAsync()
    {
        await signInManager.SignOutAsync();
        return Redirect("/login");
    }
}
