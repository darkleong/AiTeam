using AiTeam.Bot.Configuration;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.McpAuth;

/// <summary>
/// Stage 90：MCP HTTP endpoint Bearer token 驗證 middleware。
///
/// 對 /mcp* path 強制 Authorization: Bearer {AgentSettings.InternalApiKey}。
/// 通過 → 進入 MCP server pipeline。失敗 → 401 不繼續。
///
/// 重用既有 AgentSettings.InternalApiKey（Dashboard / GitHub Actions / docker-compose 已配 / 不另開新 env var）。
/// </summary>
public class McpBearerAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _apiKey;
    private readonly ILogger<McpBearerAuthMiddleware> _logger;

    public McpBearerAuthMiddleware(
        RequestDelegate next,
        IOptions<AgentSettings> agentSettings,
        ILogger<McpBearerAuthMiddleware> logger)
    {
        _next = next;
        _apiKey = agentSettings.Value.InternalApiKey;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 只對 /mcp* path 套用 / 其他 endpoint 跳過
        if (!context.Request.Path.StartsWithSegments("/mcp"))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogError("MCP endpoint 未配置 API key（AgentSettings__InternalApiKey 空）— 拒絕所有請求");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("MCP server API key not configured");
            return;
        }

        var auth = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Missing or malformed Authorization header (expect: Bearer <token>)");
            return;
        }

        var token = auth["Bearer ".Length..].Trim();
        if (token != _apiKey)
        {
            _logger.LogWarning("MCP endpoint 401 — Bearer token mismatch (path={Path})", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid Bearer token");
            return;
        }

        await _next(context);
    }
}
