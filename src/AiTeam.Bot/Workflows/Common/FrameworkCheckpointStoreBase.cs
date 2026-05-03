using System.Collections.Concurrent;
using System.Text.Json;
using AiTeam.Data;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Workflows.Common;

/// <summary>
/// Stage 54：framework Checkpointing 4 個 Store 共用 base class（Workflows/Appeal、Kickoff、Design、Pipeline）。
///
/// 抽出共通的 in-memory dict / parent links / latest tracking / Load / Retrieve / Index / Create / JSON serialize 邏輯，
/// 子類只需實作 column-specific 的 ReadJsonFromDbAsync / WriteJsonToDbAsync 兩個 abstract method。
///
/// 設計理由：
///   - Stage 49 起 4 個 CheckpointStore 99% 重複（差異只在 namespace / logger generic / DB column / log prefix）
///   - PipelineCheckpointStore 註解明文「3 次再抽象，第 4 次出現 → Stage 55 評估抽 base class」
///   - Stage 54 提前到第 4 次出現時就抽，符合「3 次再抽象」原則上限（833 行 → ~250 + 4×~50 = ~450 行，淨減 ~380 行）
///
/// generic TStore 用途：
///   - logger generic constraint，每個子類用自己的 type 對齊既有 logger category（不破壞 production logging 設定）
///   - 子類實作如 `: FrameworkCheckpointStoreBase&lt;AppealCheckpointStore&gt;`，內部 logger 仍為 `ILogger&lt;AppealCheckpointStore&gt;`
///
/// 持久化格式（沿用 Stage 49 設計，4 個子類皆相同）：
/// {
///   "checkpoints": { "ckpt-id-1": { ... JsonElement ... } },
///   "latestCheckpointId": "ckpt-id-1",
///   "parentLinks": { "ckpt-id-2": "ckpt-id-1" }
/// }
/// </summary>
public abstract class FrameworkCheckpointStoreBase<TStore> : ICheckpointStore<JsonElement>
    where TStore : class
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TStore> _logger;

    // sessionId → (checkpointId → JsonElement)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, JsonElement>> _store = new();
    // sessionId → (checkpointId → parentCheckpointId)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string?>> _parentLinks = new();
    // sessionId → latestCheckpointId
    private readonly ConcurrentDictionary<string, string> _latest = new();

    protected FrameworkCheckpointStoreBase(
        IServiceScopeFactory scopeFactory,
        ILogger<TStore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>子類實作：從 DB 讀對應 column 的 JSON 字串（null = 無 checkpoint）。</summary>
    protected abstract Task<string?> ReadJsonFromDbAsync(Guid groupId, AppDbContext db, CancellationToken ct);

    /// <summary>子類實作：把 JSON 字串寫進對應 DB column。回傳 affected rows。</summary>
    protected abstract Task<int> WriteJsonToDbAsync(Guid groupId, string json, AppDbContext db, CancellationToken ct);

    /// <summary>子類實作：log prefix（例：`"[AppealCheckpointStore]"`）。</summary>
    protected abstract string LogTag { get; }

    /// <summary>子類實作：log 用 DB column 名（例：`"FrameworkAppealStateJson"`）— 解析失敗訊息用。</summary>
    protected abstract string DbColumnName { get; }

    /// <summary>取得 sessionId 的最新 checkpoint info（重啟 resume 用）。</summary>
    public CheckpointInfo? GetLatestCheckpoint(string sessionId)
    {
        if (!_latest.TryGetValue(sessionId, out var latestId))
            return null;
        return new CheckpointInfo(sessionId, latestId);
    }

    /// <summary>從 DB 對應 column 載回 in-memory（Bot 啟動時 router 呼叫）。</summary>
    public async Task LoadFromDbAsync(Guid groupId, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var json = await ReadJsonFromDbAsync(groupId, db, ct);

        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogInformation("{Tag} Group={Id} 無 checkpoint 可載入（new session）", LogTag, groupId);
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
                "{Tag} Group={Id} 載入 {Count} 個 checkpoint，latest={Latest}",
                LogTag, groupId,
                _store.GetValueOrDefault(sessionId)?.Count ?? 0,
                _latest.GetValueOrDefault(sessionId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{Tag} Group={Id} {Column} 解析失敗，視同無 checkpoint",
                LogTag, groupId, DbColumnName);
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
                $"{LogTag} sessionId={sessionId} checkpointId={key.CheckpointId} 不存在");
        }
        return ValueTask.FromResult(value);
    }

    private async Task PersistToDbAsync(string sessionId, CancellationToken ct)
    {
        if (!Guid.TryParse(sessionId, out var groupId))
        {
            _logger.LogWarning(
                "{Tag} sessionId={SessionId} 不是 Guid，無法寫 DB", LogTag, sessionId);
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

        var rows = await WriteJsonToDbAsync(groupId, json, db, ct);

        if (rows == 0)
        {
            _logger.LogWarning(
                "{Tag} Group={Id} 寫 {Column} 0 rows affected（group 已被刪除？）",
                LogTag, groupId, DbColumnName);
        }
    }
}
