using System.Net;
using System.Security.Claims;

namespace AiTeam.Dashboard.Identity;

/// <summary>
/// Localhost Bypass Middleware：Host header 為 localhost 的請求自動以 admin 身份通過授權，
/// 無需登入。適用於 Playwright CI 測試與本機直連操作。
///
/// 安全性說明：
/// - docker-compose.prod.yml 已將 Dashboard port 收緊為 127.0.0.1:5051:8080，
///   外部裝置無法直連 port 5051。
/// - 在容器端，localhost 與 Tailscale Funnel 的請求 RemoteIP 都可能是 Docker bridge gateway，
///   因此改用 Host header 區分：localhost:5051 → bypass，Tailscale domain → 需登入。
/// - 放在 UseAuthentication() 之後、UseAuthorization() 之前執行。
/// - 已認證的用戶（IsAuthenticated == true）直接跳過，不覆蓋現有 Principal。
/// </summary>
public class LocalhostBypassMiddleware(RequestDelegate next, ILogger<LocalhostBypassMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;
        var isLocalhost = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                       || host == "127.0.0.1"
                       || host == "::1";

        if (isLocalhost && context.User.Identity?.IsAuthenticated != true)
        {
            logger.LogDebug("LocalhostBypass：Host={Host}，自動通過授權", host);
            var claims = new[]
            {
                new Claim(ClaimTypes.Name,           "localhost-admin"),
                new Claim(ClaimTypes.NameIdentifier, "localhost-admin"),
                new Claim(ClaimTypes.Role,           "Admin"),
            };
            var identity = new ClaimsIdentity(claims, "LocalhostBypass");
            context.User = new ClaimsPrincipal(identity);
        }

        await next(context);
    }
}
