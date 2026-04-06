using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data.Repositories;

/// <summary>
/// CEO 長期記憶的資料存取。
/// Victoria 在對話中判斷值得記住的設計決策或老闆偏好時，
/// 透過回應中的 memories_to_save 欄位觸發，由 CeoAgentService 呼叫此 Repository 持久化。
/// </summary>
public class CeoMemoryRepository(AppDbContext db)
{
    /// <summary>
    /// 載入指定使用者的所有有效記憶，最多 100 筆（按建立時間降冪），
    /// 呼叫方需自行反轉為升冪再組裝 Prompt。
    /// </summary>
    public async Task<List<CeoMemory>> GetActiveMemoriesAsync(
        string userId,
        CancellationToken cancellationToken = default)
        => await db.CeoMemories
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive)
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

    /// <summary>批次新增 Victoria 回應中的 memories_to_save 清單。</summary>
    public async Task SaveMemoriesAsync(
        string userId,
        IReadOnlyList<MemoryToSave> memories,
        CancellationToken cancellationToken = default)
    {
        foreach (var m in memories)
        {
            db.CeoMemories.Add(new CeoMemory
            {
                UserId    = userId,
                Content   = m.Content,
                Category  = m.Category,
                IsActive  = true,
                CreatedAt = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>軟刪除一筆記憶（將 IsActive 設為 false）。</summary>
    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await db.CeoMemories
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsActive, false), cancellationToken);
    }
}

/// <summary>Victoria 回應 JSON 中 memories_to_save 陣列的單一元素 DTO。</summary>
public record MemoryToSave(string Content, string Category);
