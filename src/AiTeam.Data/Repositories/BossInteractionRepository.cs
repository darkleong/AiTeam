using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data.Repositories;

/// <summary>
/// Stage 28a：boss_interactions 資料存取。
/// </summary>
public class BossInteractionRepository(AppDbContext db)
{
    /// <summary>新增互動記錄（呼叫方負責 SaveChangesAsync）。</summary>
    public void Add(BossInteraction interaction) => db.BossInteractions.Add(interaction);

    /// <summary>查詢所有 pending 互動，按建立時間排序（Dashboard 待處理清單）。</summary>
    public Task<List<BossInteraction>> GetPendingAsync(CancellationToken ct = default)
        => db.BossInteractions
            .Where(x => x.Status == "pending")
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    /// <summary>依 DiscordMessageId 反查互動記錄（Discord 按鈕回覆時使用）。</summary>
    public Task<BossInteraction?> GetByDiscordMessageIdAsync(decimal messageId, CancellationToken ct = default)
        => db.BossInteractions
            .FirstOrDefaultAsync(x => x.DiscordMessageId == messageId, ct);

    /// <summary>查詢 Dashboard 已回覆、尚未被 Bot 消費的記錄（InteractionProcessor 輪詢用）。</summary>
    public Task<List<BossInteraction>> GetDashboardResponsesAsync(CancellationToken ct = default)
        => db.BossInteractions
            .Where(x => x.Status == "responded"
                     && x.ResponseSource == "dashboard"
                     && !x.ProcessedByBot)
            .OrderBy(x => x.RespondedAt)
            .ToListAsync(ct);

    /// <summary>Stage 51：依 TaskGroupId + InteractionType 取最近一筆（FrameworkHitlBridge resume 時用以
    /// 從 ContextJson 取回原 RequestId）。為純讀取，不限定 status。</summary>
    public Task<BossInteraction?> GetLatestForGroupByTypeAsync(
        Guid taskGroupId, string interactionType, CancellationToken ct = default)
        => db.BossInteractions
            .Where(x => x.TaskGroupId == taskGroupId && x.InteractionType == interactionType)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

    /// <summary>Stage 57-FF 五十一：查同 (taskGroupId, type) 是否有 pending（未響應）interaction。
    /// 用於 InteractionService.TryCreateUniqueInteractionAsync 的 idempotent 鍵 — race-prone fire 點防雙 fire。</summary>
    public Task<bool> HasPendingForGroupAndTypeAsync(
        Guid taskGroupId, string interactionType, CancellationToken ct = default)
        => db.BossInteractions.AnyAsync(
            x => x.TaskGroupId == taskGroupId
              && x.InteractionType == interactionType
              && x.Status == "pending",
            ct);

    /// <summary>查詢最近已處理的互動（Dashboard 歷史區）。</summary>
    public Task<List<BossInteraction>> GetRecentRespondedAsync(int count = 10, CancellationToken ct = default)
        => db.BossInteractions
            .Where(x => x.Status == "responded")
            .OrderByDescending(x => x.RespondedAt)
            .Take(count)
            .ToListAsync(ct);

    /// <summary>
    /// 樂觀鎖回覆：WHERE id = @id AND status = 'pending'。
    /// 回傳 true 代表本次回覆成功，false 代表另一通道已先回覆。
    /// </summary>
    public async Task<bool> RespondAsync(Guid id, string action, string source, CancellationToken ct = default)
    {
        var affected = await db.BossInteractions
            .Where(x => x.Id == id && x.Status == "pending")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status,         "responded")
                .SetProperty(x => x.ResponseAction, action)
                .SetProperty(x => x.ResponseSource, source)
                .SetProperty(x => x.RespondedAt,    DateTime.UtcNow), ct);
        return affected > 0;
    }

    /// <summary>標記 Bot 已消費 Dashboard 回覆，防止重複處理。</summary>
    public async Task MarkProcessedByBotAsync(Guid id, CancellationToken ct = default)
    {
        await db.BossInteractions
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ProcessedByBot, true), ct);
    }

    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
