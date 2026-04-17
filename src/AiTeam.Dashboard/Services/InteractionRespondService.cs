using AiTeam.Data;
using AiTeam.Data.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>
/// Stage 28a：Dashboard 側的互動回覆服務。
/// Scoped — 注入 AppDbContext，執行樂觀鎖回覆並廣播 SignalR 更新。
/// </summary>
public class InteractionRespondService(
    AppDbContext db,
    IHubContext<AgentStatusHub> hubContext,
    ILogger<InteractionRespondService> logger)
{
    /// <summary>
    /// 以樂觀鎖回覆互動（無文字內容）。
    /// 回傳 true：回覆成功；false：另一通道已先回覆（競態衝突）。
    /// </summary>
    public Task<bool> RespondAsync(Guid id, string action, CancellationToken ct = default)
        => RespondAsync(id, action, content: null, ct);

    /// <summary>
    /// Stage 28b：以樂觀鎖回覆互動，支援文字輸入內容（修改意見）。
    /// </summary>
    public async Task<bool> RespondAsync(Guid id, string action, string? content, CancellationToken ct = default)
    {
        var affected = await db.BossInteractions
            .Where(x => x.Id == id && x.Status == "pending")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status,           "responded")
                .SetProperty(x => x.ResponseAction,   action)
                .SetProperty(x => x.ResponseContent,  content)
                .SetProperty(x => x.ResponseSource,   "dashboard")
                .SetProperty(x => x.RespondedAt,      DateTime.UtcNow), ct);

        if (affected > 0)
        {
            // 廣播 SignalR 更新，讓所有 Dashboard 客戶端即時重整
            await hubContext.Clients.All.SendAsync(AgentStatusHub.ReceiveInteractionUpdate, ct);
            logger.LogInformation("Dashboard 回覆互動成功（Id={Id}，Action={Action}）", id, action);
            return true;
        }

        logger.LogInformation("Dashboard 回覆衝突：另一通道已先回覆（Id={Id}）", id);
        return false;
    }
}
