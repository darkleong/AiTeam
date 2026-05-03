using AiTeam.Bot.Workflows.Common;
using AiTeam.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Design;

/// <summary>
/// Stage 52：framework Checkpointing 寫進 task_groups.DesignFrameworkStateJson 的自訂 store（v4 漸進遷移第四步）。
///
/// Stage 54：抽出 base class FrameworkCheckpointStoreBase，本子類只負責 column-specific I/O。
/// </summary>
public sealed class DesignCheckpointStore(
    IServiceScopeFactory scopeFactory,
    ILogger<DesignCheckpointStore> logger)
    : FrameworkCheckpointStoreBase<DesignCheckpointStore>(scopeFactory, logger)
{
    protected override string LogTag => "[DesignCheckpointStore]";
    protected override string DbColumnName => "DesignFrameworkStateJson";

    protected override Task<string?> ReadJsonFromDbAsync(Guid groupId, AppDbContext db, CancellationToken ct)
        => db.TaskGroups
            .Where(g => g.Id == groupId)
            .Select(g => g.DesignFrameworkStateJson)
            .FirstOrDefaultAsync(ct);

    protected override Task<int> WriteJsonToDbAsync(Guid groupId, string json, AppDbContext db, CancellationToken ct)
        => db.TaskGroups
            .Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.DesignFrameworkStateJson, json), ct);
}
