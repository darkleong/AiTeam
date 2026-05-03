using AiTeam.Bot.Workflows.Common;
using AiTeam.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Pipeline;

/// <summary>
/// Stage 53A：framework Checkpointing 寫進 task_groups.PipelineFrameworkStateJson 的自訂 store（v4 漸進遷移第五步 macro-orchestration）。
///
/// Stage 54：抽出 base class FrameworkCheckpointStoreBase，本子類只負責 column-specific I/O。
///
/// framework-in-framework：本 store 寫外層 Pipeline state，與 inner KickoffCheckpointStore / DesignCheckpointStore 各自寫各自的 DB column 互不干擾（雙 CheckpointStore 並存無 collision）。
/// </summary>
public sealed class PipelineCheckpointStore(
    IServiceScopeFactory scopeFactory,
    ILogger<PipelineCheckpointStore> logger)
    : FrameworkCheckpointStoreBase<PipelineCheckpointStore>(scopeFactory, logger)
{
    protected override string LogTag => "[PipelineCheckpointStore]";
    protected override string DbColumnName => "PipelineFrameworkStateJson";

    protected override Task<string?> ReadJsonFromDbAsync(Guid groupId, AppDbContext db, CancellationToken ct)
        => db.TaskGroups
            .Where(g => g.Id == groupId)
            .Select(g => g.PipelineFrameworkStateJson)
            .FirstOrDefaultAsync(ct);

    protected override Task<int> WriteJsonToDbAsync(Guid groupId, string json, AppDbContext db, CancellationToken ct)
        => db.TaskGroups
            .Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.PipelineFrameworkStateJson, json), ct);
}
