using System.ComponentModel;
using AiTeam.Data;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace AiTeam.Bot.McpTools;

/// <summary>
/// Stage 90：MCP server endpoint 基本健康檢查 tool。
/// 確認 AiTeam MCP server reachable + DB 可連 + Bot 運行時長。
/// 後續 Stage 91 加 register_team / record_task / record_message / record_token_usage 4 個 record tool（record_message v4.0.1 由 record_conversation rename）。
/// v4.0.2 補 close_team / finish_teammate 2 個 lifecycle tool（共 8 個 MCP tool）。
/// </summary>
[McpServerToolType]
public sealed class HealthCheckTool
{
    [McpServerTool, Description("Health check — confirm AiTeam MCP server reachable and DB ready. Returns DB status, Bot uptime, server UTC time.")]
    public static async Task<string> HealthCheck(AppDbContext db, CancellationToken ct)
    {
        var dbOk = await db.Database.CanConnectAsync(ct);
        var uptime = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalMinutes;
        return $"AiTeam MCP server alive. DB={(dbOk ? "OK" : "FAIL")}. Uptime={uptime:F1}min. UTC={DateTime.UtcNow:O}";
    }
}
