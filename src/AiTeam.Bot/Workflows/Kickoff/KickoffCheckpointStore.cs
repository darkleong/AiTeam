using AiTeam.Bot.Workflows.Common;
using AiTeam.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Kickoff;

/// <summary>
/// Stage 50：framework Checkpointing 寫進 task_groups.KickoffFrameworkStateJson 的自訂 store。
///
/// Stage 54：抽出 base class FrameworkCheckpointStoreBase，本子類只負責 column-specific I/O。
/// </summary>
public sealed class KickoffCheckpointStore(
    IServiceScopeFactory scopeFactory,
    ILogger<KickoffCheckpointStore> logger)
    : FrameworkCheckpointStoreBase<KickoffCheckpointStore>(scopeFactory, logger)
{
    protected override string LogTag => "[KickoffCheckpointStore]";
    protected override string DbColumnName => "KickoffFrameworkStateJson";

    protected override Task<string?> ReadJsonFromDbAsync(Guid groupId, AppDbContext db, CancellationToken ct)
        => db.TaskGroups
            .Where(g => g.Id == groupId)
            .Select(g => g.KickoffFrameworkStateJson)
            .FirstOrDefaultAsync(ct);

    protected override Task<int> WriteJsonToDbAsync(Guid groupId, string json, AppDbContext db, CancellationToken ct)
        => db.TaskGroups
            .Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.KickoffFrameworkStateJson, json), ct);
}
