using System.Net;
using System.Security.Claims;

namespace AiTeam.Dashboard.Identity;

/// <summary>
/// Localhost Bypass Middleware：來自 loopback 位址的請求自動以 admin 身份通過授權，
/// 無需登入。適用於 Playwright CI 測試與本機直連操作。
///
/// 安全性說明：
/// - docker-compose.prod.yml 已將 Dashboard port 收緊為 127.0.0.1:5051:8080，
///   外部裝置無法直連，Tailscale Funnel 的請求 RemoteIpAddress 為 Tailscale IP（非 loopback），
///   因此仍需正常登入。
/// - 放在 UseAuthentication() 之後、UseAuthorization() 之前執行。
/// - 已認證的用戶（IsAuthenticated == true）直接跳過，不覆蓋現有 Principal。
/// </summary>
public class LocalhostBypassMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        var isLoopback = remoteIp is not null && IPAddress.IsLoopback(remoteIp);

        if (isLoopback && context.User.Identity?.IsAuthenticated != true)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name,              "localhost-admin"),
                new Claim(ClaimTypes.NameIdentifier,    "localhost-admin"),
                new Claim(ClaimTypes.Role,              "Admin"),
            };
            var identity  = new ClaimsIdentity(claims, "LocalhostBypass");
            context.User  = new ClaimsPrincipal(identity);
        }

        await next(context);
    }
}
