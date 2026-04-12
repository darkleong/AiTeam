using System.Net;
using System.Security.Claims;

namespace AiTeam.Dashboard.Identity;

/// <summary>
/// Localhost Bypass Middleware：來自 loopback 或受信任 IP 的請求自動以 admin 身份通過授權，
/// 無需登入。適用於 Playwright CI 測試與本機直連操作。
///
/// 安全性說明：
/// - docker-compose.prod.yml 已將 Dashboard port 收緊為 127.0.0.1:5051:8080，
///   外部裝置無法直連，Tailscale Funnel 的請求 RemoteIpAddress 為 Tailscale IP（非信任範圍），
///   因此仍需正常登入。
/// - Docker 容器內收到的 RemoteIpAddress 是 Docker bridge gateway（172.19.0.x），
///   屬於 Docker 預設 CIDR 範圍 172.16.0.0/12，會被信任（Port 收緊後外部不可達，安全）。
/// - 放在 UseAuthentication() 之後、UseAuthorization() 之前執行。
/// - 已認證的用戶（IsAuthenticated == true）直接跳過，不覆蓋現有 Principal。
/// </summary>
public class LocalhostBypassMiddleware(RequestDelegate next, ILogger<LocalhostBypassMiddleware> logger)
{
    // Docker 預設 bridge 網段 172.16.0.0/12（涵蓋 172.16.0.0 ~ 172.31.255.255）
    private static readonly IPAddress DockerBridgeNetwork = IPAddress.Parse("172.16.0.0");
    private static readonly IPAddress DockerBridgeMask    = IPAddress.Parse("255.240.0.0");

    public async Task InvokeAsync(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;

        var isTrusted = remoteIp is not null &&
            (IPAddress.IsLoopback(remoteIp) || IsInDockerBridgeRange(remoteIp));

        if (isTrusted && context.User.Identity?.IsAuthenticated != true)
        {
            logger.LogDebug("LocalhostBypass：{RemoteIp} 符合信任條件，自動通過授權", remoteIp);
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

    /// <summary>
    /// 判斷 IP 是否在 Docker bridge 網段 172.16.0.0/12 內。
    /// 此範圍在 port 收緊為 127.0.0.1:5051:8080 後，實際上只有容器自身的 gateway 會出現，安全可信任。
    /// </summary>
    private static bool IsInDockerBridgeRange(IPAddress ip)
    {
        // 只處理 IPv4
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        var ipBytes      = ip.GetAddressBytes();
        var networkBytes = DockerBridgeNetwork.GetAddressBytes();
        var maskBytes    = DockerBridgeMask.GetAddressBytes();

        if (ipBytes.Length != 4) return false;

        for (int i = 0; i < 4; i++)
        {
            if ((ipBytes[i] & maskBytes[i]) != (networkBytes[i] & maskBytes[i]))
                return false;
        }
        return true;
    }
}
