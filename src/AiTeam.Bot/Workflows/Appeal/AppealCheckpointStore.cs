using AiTeam.Bot.Workflows.Common;
using AiTeam.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Appeal;

/// <summary>
/// Stage 49：framework Checkpointing 寫進 task_groups.FrameworkAppealStateJson 的自訂 store。
///
/// Stage 54：抽出 base class FrameworkCheckpointStoreBase，本子類只負責 column-specific I/O。
///
/// 設計（沿用 Stage 49）：
///   - sessionId = TaskGroup.Id.ToString()（每個 TaskGroup 一條 session，獨立 checkpoint chain）
///   - in-memory dict 持有 process 生命週期內所有 checkpoint
///   - 每次 CreateCheckpointAsync 都序列化整個 dict 寫回 task_groups.FrameworkAppealStateJson
///   - 重啟時：從 DB 讀整個 dict 載回 in-memory（FrameworkAppealRouter 啟動時呼叫 LoadFromDbAsync）
/// </summary>
public sealed class AppealCheckpointStore(
    IServiceScopeFactory scopeFactory,
    ILogger<AppealCheckpointStore> logger)
    : FrameworkCheckpointStoreBase<AppealCheckpointStore>(scopeFactory, logger)
{
    protected override string LogTag => "[AppealCheckpointStore]";
    protected override string DbColumnName => "FrameworkAppealStateJson";

    protected override Task<string?> ReadJsonFromDbAsync(Guid groupId, AppDbContext db, CancellationToken ct)
        => db.TaskGroups
            .Where(g => g.Id == groupId)
            .Select(g => g.FrameworkAppealStateJson)
            .FirstOrDefaultAsync(ct);

    protected override Task<int> WriteJsonToDbAsync(Guid groupId, string json, AppDbContext db, CancellationToken ct)
        => db.TaskGroups
            .Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.FrameworkAppealStateJson, json), ct);
}
