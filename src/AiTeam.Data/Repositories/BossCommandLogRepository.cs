namespace AiTeam.Data.Repositories;

/// <summary>Stage 29-5：Dashboard 下達指令記錄的資料存取。</summary>
public class BossCommandLogRepository(AppDbContext db)
{
    public void Add(BossCommandLog log) => db.BossCommandLogs.Add(log);
    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
