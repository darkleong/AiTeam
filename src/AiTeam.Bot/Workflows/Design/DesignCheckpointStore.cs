using System.Collections.Concurrent;
using System.Text.Json;
using AiTeam.Data;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Design;

/// <summary>
/// Stage 52：framework Checkpointing 寫進 task_groups.DesignFrameworkStateJson 的自訂 store（v4 漸進遷移第四步）。
///
/// 設計（直接複用 Stage 50 KickoffCheckpointStore pattern，符合「3 次再抽象」原則第 3 次出現相似 pattern）：
///   - sessionId = TaskGroup.Id.ToString()（每個 TaskGroup 一條 session，獨立 checkpoint chain）
///   - in-memory dict 持有 process 生命週期內所有 checkpoint
///   - 每次 CreateCheckpointAsync 都序列化整個 dict 寫回 task_groups.DesignFrameworkStateJson
///   - 重啟時：從 DB 讀整個 dict 載回 in-memory（FrameworkDesignRouter 啟動時呼叫 LoadFromDbAsync）
///
/// 為什麼整個 dict 寫 DB 而非只寫 latest（沿用 Stage 49/50 設計理由）：
///   - framework 可能需要 parent checkpoint chain 做 recovery 邏輯
///   - 每個 TaskGroup 一條 session，dict 大小有上限（典型 ~3 round × 6 superstep ≈ 18 entries），可控
///   - 序列化成本低（JsonElement 已是 JSON tree）
///
/// 持久化格式（task_groups.DesignFrameworkStateJson）：
/// {
///   "checkpoints": {
///     "ckpt-id-1": { ... JsonElement ... },
///     "ckpt-id-2": { ... JsonElement ... }
///   },
///   "latestCheckpointId": "ckpt-id-2",
///   "parentLinks": { "ckpt-id-2": "ckpt-id-1" }
/// }
///
/// 設計差異 vs KickoffCheckpointStore：寫的是 DesignFrameworkStateJson 欄位（不是 KickoffFrameworkStateJson），
/// 其他邏輯 1:1 複用（Stage 50 pattern 已驗 production 跑通）。Stage 53 評估抽 base class（第 3 次出現）。
/// </summary>
public sealed class DesignCheckpointStore : ICheckpointStore<JsonElement>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DesignCheckpointStore> _logger;

    // sessionId → (checkpointId → JsonElement)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, JsonElement>> _store = new();
    // sessionId → (checkpointId → parentCheckpointId)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string?>> _parentLinks = new();
    // sessionId → latestCheckpointId
    private readonly ConcurrentDictionary<string, string> _latest = new();

    public DesignCheckpointStore(
        IServiceScopeFactory scopeFactory,
        ILogger<DesignCheckpointStore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>取得 sessionId 的最新 checkpoint info（重啟 resume 用）。</summary>
    public CheckpointInfo? GetLatestCheckpoint(string sessionId)
    {
        if (!_latest.TryGetValue(sessionId, out var latestId))
            return null;
        return new CheckpointInfo(sessionId, latestId);
    }

    /// <summary>從 DB task_groups.DesignFrameworkStateJson 載回 in-memory（Bot 啟動時 router 呼叫）。</summary>
    public async Task LoadFromDbAsync(Guid groupId, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var json = await db.TaskGroups
            .Where(g => g.Id == groupId)
            .Select(g => g.DesignFrameworkStateJson)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogInformation("[DesignCheckpointStore] Group={Id} 無 checkpoint 可載入（new session）", groupId);
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var sessionId = groupId.ToString();

            if (root.TryGetProperty("checkpoints", out var ckptsEl))
            {
                var dict = new ConcurrentDictionary<string, JsonElement>();
                foreach (var prop in ckptsEl.EnumerateObject())
                    dict[prop.Name] = prop.Value.Clone();
                _store[sessionId] = dict;
            }

            if (root.TryGetProperty("parentLinks", out var linksEl))
            {
                var links = new ConcurrentDictionary<string, string?>();
                foreach (var prop in linksEl.EnumerateObject())
                    links[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.GetString();
                _parentLinks[sessionId] = links;
            }

            if (root.TryGetProperty("latestCheckpointId", out var latestEl) &&
                latestEl.ValueKind == JsonValueKind.String)
            {
                var latestId = latestEl.GetString();
                if (!string.IsNullOrEmpty(latestId))
                    _latest[sessionId] = latestId;
            }

            _logger.LogInformation(
                "[DesignCheckpointStore] Group={Id} 載入 {Count} 個 checkpoint，latest={Latest}",
                groupId,
                _store.GetValueOrDefault(sessionId)?.Count ?? 0,
                _latest.GetValueOrDefault(sessionId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[DesignCheckpointStore] Group={Id} DesignFrameworkStateJson 解析失敗，視同無 checkpoint", groupId);
        }
    }

    public ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(string sessionId, CheckpointInfo? withParent)
    {
        if (!_store.TryGetValue(sessionId, out var dict))
            return ValueTask.FromResult(Enumerable.Empty<CheckpointInfo>());

        if (withParent is null)
        {
            return ValueTask.FromResult(dict.Keys.Select(id => new CheckpointInfo(sessionId, id)));
        }

        if (!_parentLinks.TryGetValue(sessionId, out var links))
            return ValueTask.FromResult(Enumerable.Empty<CheckpointInfo>());

        var children = links
            .Where(kv => kv.Value == withParent.CheckpointId)
            .Select(kv => new CheckpointInfo(sessionId, kv.Key));
        return ValueTask.FromResult(children);
    }

    public async ValueTask<CheckpointInfo> CreateCheckpointAsync(
        string sessionId, JsonElement value, CheckpointInfo? parent)
    {
        var dict  = _store.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, JsonElement>());
        var links = _parentLinks.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, string?>());

        var checkpointId = Guid.NewGuid().ToString();
        dict[checkpointId] = value.Clone();
        links[checkpointId] = parent?.CheckpointId;
        _latest[sessionId] = checkpointId;

        // 同步寫 DB（每個 superstep 結束 framework 都會 call 此 method 一次）
        await PersistToDbAsync(sessionId, default);

        return new CheckpointInfo(sessionId, checkpointId);
    }

    public ValueTask<JsonElement> RetrieveCheckpointAsync(string sessionId, CheckpointInfo key)
    {
        if (!_store.TryGetValue(sessionId, out var dict) ||
            !dict.TryGetValue(key.CheckpointId, out var value))
        {
            throw new KeyNotFoundException(
                $"[DesignCheckpointStore] sessionId={sessionId} checkpointId={key.CheckpointId} 不存在");
        }
        return ValueTask.FromResult(value);
    }

    private async Task PersistToDbAsync(string sessionId, CancellationToken ct)
    {
        if (!Guid.TryParse(sessionId, out var groupId))
        {
            _logger.LogWarning(
                "[DesignCheckpointStore] sessionId={SessionId} 不是 Guid，無法寫 DB", sessionId);
            return;
        }

        var dict  = _store.GetValueOrDefault(sessionId);
        var links = _parentLinks.GetValueOrDefault(sessionId);
        if (dict is null) return;

        var serialized = new
        {
            checkpoints = dict.ToDictionary(
                kv => kv.Key,
                kv => kv.Value),
            parentLinks = links?.ToDictionary(
                kv => kv.Key,
                kv => kv.Value),
            latestCheckpointId = _latest.GetValueOrDefault(sessionId),
        };
        var json = JsonSerializer.Serialize(serialized);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.TaskGroups
            .Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.DesignFrameworkStateJson, json), ct);

        if (rows == 0)
        {
            _logger.LogWarning(
                "[DesignCheckpointStore] Group={Id} 寫 DesignFrameworkStateJson 0 rows affected（group 已被刪除？）",
                groupId);
        }
    }
}
