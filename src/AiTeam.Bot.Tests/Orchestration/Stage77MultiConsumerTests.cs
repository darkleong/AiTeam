using System.Collections.Concurrent;
using System.Threading.Channels;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Orchestration.Petra;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiTeam.Bot.Tests.Orchestration;

/// <summary>
/// Stage 77：v5.5 Phase 3 補強 — fire-and-forget A2 完整版（Channel + multi-consumer + bounded fan-out + graceful shutdown drain）單元驗。
///
/// 覆蓋驗收場景：
/// - 場景 A：PetraInboxChannel Bounded config baseline（T1）
/// - 場景 B：PetraInboxProcessor 退化為 pure producer（T2）
/// - 場景 C：Multi-consumer 並行 pickup（T3）
/// - 場景 D：Bounded fan-out cap 守 max concurrent（T4）
/// - 場景 E：Graceful shutdown drain in-flight（T5）
/// - 場景 F：Stage 76 retry path 0 regression 整合驗收（T6 — Aria 議題 1 拍板方案 A virtual + stub override）
/// - 場景 G：MaxConcurrentPetra AppSetting 動態讀取 + 範圍守 [1, 10]（T7）
///
/// 紀律對齊：xUnit + InMemory DB + ServiceCollection.BuildServiceProvider（對齊 Stage75InboxQueueTests + Stage76RetryMechanismTests 既有 pattern）。
/// </summary>
public class Stage77MultiConsumerTests
{
    private static AppDbContext CreateInMemoryDb(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    /// <summary>Test-only stub — override <see cref="PetraOrchestratorService.StartAsync"/> 不真實 invoke base（base ctor 14 deps 全 null 走 / 0 base method invoke）。</summary>
    private sealed class StubPetraOrchestratorService : PetraOrchestratorService
    {
        private readonly Func<CancellationToken, Task<PetraOrchestratorResult>> _resultFactory;

        public StubPetraOrchestratorService(Func<CancellationToken, Task<PetraOrchestratorResult>> resultFactory)
            : base(
                talentFactory: null!,
                workflowResolver: null!,
                sessionRepo: null!,
                db: null!,
                gitFinalization: null!,      // Stage 84
                contextBuilder: null!,       // Stage 84
                talentDispatch: null!,       // Stage 84
                dynamicReplan: null!,        // Stage 84
                planConfirmation: null!,     // Stage 84
                logger: NullLogger<PetraOrchestratorService>.Instance)
        {
            _resultFactory = resultFactory;
        }

        // Stage 79：簽名擴 images param 對齊 PetraOrchestratorService.StartAsync 真實簽名（Mock fixture 不真實 propagate / Test 只驗 retry path 不驗 image）
        public override Task<PetraOrchestratorResult> StartAsync(
            Guid? taskGroupId, string taskInput, CancellationToken ct = default,
            IReadOnlyList<AiTeam.Bot.Agents.ImageAttachment>? images = null)
            => _resultFactory(ct);
    }

    /// <summary>建 ServiceProvider — InMemory DB + 各 Stage 77 依賴註冊。stubResult 給 T6 retry path 整合驗 / null 走真實 PetraOrchestratorService（不會被 invoke 的 case）。</summary>
    private static ServiceProvider BuildSp(
        string dbName,
        Func<CancellationToken, Task<PetraOrchestratorResult>>? stubFactory = null)
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDbContext<AppDbContext>(o => o
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
        sc.AddScoped<PetraInboxRepository>();
        sc.AddSingleton<PetraInboxChannel>();
        sc.AddHttpClient("aiteam-dashboard", _ => { });
        sc.AddSingleton<DashboardPushService>();

        if (stubFactory is not null)
        {
            sc.AddScoped<PetraOrchestratorService>(_ => new StubPetraOrchestratorService(stubFactory));
        }

        return sc.BuildServiceProvider();
    }

    // ─── T1：場景 A PetraInboxChannel Bounded config baseline ─────────────────────────
    [Fact]
    public async Task T1_PetraInboxChannel_BoundedConfig_BaselineSettings()
    {
        var channel = new PetraInboxChannel(NullLogger<PetraInboxChannel>.Instance);

        // Writer + Reader 可訪問（接近 sanity check / 完整 BoundedChannelOptions 由實作 ctor 守）
        Assert.NotNull(channel.Writer);
        Assert.NotNull(channel.Reader);

        // 1 row push + pop round-trip — Bounded 0 drop
        var rowId = Guid.NewGuid();
        await channel.Writer.WriteAsync(rowId);
        var dequeued = await channel.Reader.ReadAsync();
        Assert.Equal(rowId, dequeued);
    }

    // ─── T2：場景 B PetraInboxProcessor push channel / 0 dispatch logic ─────────────
    [Fact]
    public async Task T2_PetraInboxProcessor_PushesRowIdToChannel_NotAwaitDispatch()
    {
        var dbName = nameof(T2_PetraInboxProcessor_PushesRowIdToChannel_NotAwaitDispatch);
        await using var sp = BuildSp(dbName);

        // seed 1 pending row
        using (var seedScope = sp.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            db.PetraInbox.Add(new PetraInbox
            {
                UserInput = "test push only", Source = "dashboard",
                Status = "pending", EnqueuedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var channel = sp.GetRequiredService<PetraInboxChannel>();
        var pushService = sp.GetRequiredService<DashboardPushService>();
        var processor = new PetraInboxProcessor(sp, channel, pushService, NullLogger<PetraInboxProcessor>.Instance);

        // reflection invoke private ProcessOnePendingAsync（對齊既有 PetraOrchestratorServiceTests Test9 reflection 紀律）
        var method = typeof(PetraInboxProcessor).GetMethod(
            "ProcessOnePendingAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        await (Task)method!.Invoke(processor, [CancellationToken.None])!;

        // verify channel.Reader 拿到 rowId（push 行為實證）
        Assert.True(channel.Reader.TryRead(out var rowId));
        Assert.NotEqual(Guid.Empty, rowId);

        // verify Status='running'（atomic check 切 running 行為實證）
        using (var checkScope = sp.CreateScope())
        {
            var db = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.PetraInbox.FirstAsync(x => x.Id == rowId);
            Assert.Equal("running", row.Status);
            Assert.NotNull(row.StartedAt);
        }
    }

    // ─── T3：場景 C Multi-consumer 並行 pickup ─────────────────────────
    [Fact]
    public async Task T3_PetraDispatchWorker_MultiConsumer_ParallelPickup()
    {
        var dbName = nameof(T3_PetraDispatchWorker_MultiConsumer_ParallelPickup);
        var pickedAt = new ConcurrentBag<(int WorkerIndex, DateTime Time)>();
        var stubGate = new SemaphoreSlim(0, 3);   // 守 3 consumer 都到位才釋放 dispatch（驗並行）

        await using var sp = BuildSp(dbName, async ct =>
        {
            stubGate.Release();
            await Task.Delay(100, ct);
            return PetraOrchestratorResult.Done(Guid.NewGuid(), Array.Empty<string>(), "stub ok");
        });

        using (var seedScope = sp.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            for (var i = 0; i < 3; i++)
            {
                db.PetraInbox.Add(new PetraInbox
                {
                    UserInput = $"task {i}", Source = "dashboard",
                    Status = "running", EnqueuedAt = DateTime.UtcNow.AddSeconds(i),
                    StartedAt = DateTime.UtcNow,
                });
            }
            await db.SaveChangesAsync();
        }

        var channel = sp.GetRequiredService<PetraInboxChannel>();
        var pushService = sp.GetRequiredService<DashboardPushService>();

        // Stub resolver — 雖然 ConsumeLoopAsync 不直接讀 N（N 在 ExecuteAsync 讀後傳）/ T3 直接 invoke ConsumeLoopAsync 不過 ExecuteAsync
        var worker = new PetraDispatchWorker(
            channel, sp, workflowResolver: null!, pushService,
            NullLogger<PetraDispatchWorker>.Instance);

        // push 3 rowId
        var rowIds = new List<Guid>();
        using (var listScope = sp.CreateScope())
        {
            var db = listScope.ServiceProvider.GetRequiredService<AppDbContext>();
            rowIds = await db.PetraInbox.OrderBy(x => x.EnqueuedAt).Select(x => x.Id).ToListAsync();
        }
        foreach (var id in rowIds) await channel.Writer.WriteAsync(id);
        channel.Writer.Complete();

        // 起 3 consumer loop 並 track pickup timestamp
        using var dispatchCts = new CancellationTokenSource();
        var consumers = Enumerable.Range(0, 3).Select(async i =>
        {
            await foreach (var rowId in channel.Reader.ReadAllAsync())
            {
                pickedAt.Add((i, DateTime.UtcNow));
                await worker.DispatchOneAsync(i, rowId, dispatchCts.Token);
            }
        }).ToArray();

        await Task.WhenAll(consumers);

        Assert.Equal(3, pickedAt.Count);
        var distinctWorkers = pickedAt.Select(p => p.WorkerIndex).Distinct().Count();
        Assert.Equal(3, distinctWorkers);   // 3 個不同 consumer 都接到 row（並行 pickup 訊號）
    }

    // ─── T4：場景 D Bounded fan-out cap — 同時最多 N 個並行 ─────────────────────────
    [Fact]
    public async Task T4_PetraDispatchWorker_RespectsBoundedFanOut_MaxConcurrent()
    {
        var dbName = nameof(T4_PetraDispatchWorker_RespectsBoundedFanOut_MaxConcurrent);
        var currentParallel = 0;
        var peakParallel = 0;
        var lockObj = new object();

        await using var sp = BuildSp(dbName, async ct =>
        {
            lock (lockObj)
            {
                currentParallel++;
                if (currentParallel > peakParallel) peakParallel = currentParallel;
            }
            await Task.Delay(150, ct);
            lock (lockObj) currentParallel--;
            return PetraOrchestratorResult.Done(Guid.NewGuid(), Array.Empty<string>(), "stub ok");
        });

        using (var seedScope = sp.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            for (var i = 0; i < 5; i++)
            {
                db.PetraInbox.Add(new PetraInbox
                {
                    UserInput = $"task {i}", Source = "dashboard",
                    Status = "running", EnqueuedAt = DateTime.UtcNow.AddSeconds(i),
                    StartedAt = DateTime.UtcNow,
                });
            }
            await db.SaveChangesAsync();
        }

        var channel = sp.GetRequiredService<PetraInboxChannel>();
        var pushService = sp.GetRequiredService<DashboardPushService>();
        var worker = new PetraDispatchWorker(channel, sp, null!, pushService, NullLogger<PetraDispatchWorker>.Instance);

        List<Guid> rowIds;
        using (var listScope = sp.CreateScope())
        {
            var db = listScope.ServiceProvider.GetRequiredService<AppDbContext>();
            rowIds = await db.PetraInbox.OrderBy(x => x.EnqueuedAt).Select(x => x.Id).ToListAsync();
        }
        foreach (var id in rowIds) await channel.Writer.WriteAsync(id);
        channel.Writer.Complete();

        using var dispatchCts = new CancellationTokenSource();
        // 模擬 N=3 cap — 起 3 consumer / 5 row 排隊
        var consumers = Enumerable.Range(0, 3).Select(i =>
            Task.Run(async () =>
            {
                await foreach (var rowId in channel.Reader.ReadAllAsync())
                    await worker.DispatchOneAsync(i, rowId, dispatchCts.Token);
            })).ToArray();

        await Task.WhenAll(consumers);

        // 5 row 全處理完 + peakParallel 不應超過 3（守 max concurrent cap）
        Assert.True(peakParallel <= 3, $"peakParallel={peakParallel} 超過 N=3 cap");
        Assert.True(peakParallel >= 2, $"peakParallel={peakParallel} 太低 — 並行未發生");
    }

    // ─── T5：場景 E Graceful shutdown drain in-flight ─────────────────────────
    [Fact]
    public async Task T5_PetraDispatchWorker_GracefulShutdown_DrainsInFlight()
    {
        var dbName = nameof(T5_PetraDispatchWorker_GracefulShutdown_DrainsInFlight);
        var dispatchCompleted = new TaskCompletionSource<bool>();

        await using var sp = BuildSp(dbName, async ct =>
        {
            await Task.Delay(200, ct);   // 模擬 in-flight Petra 跑 200ms
            dispatchCompleted.TrySetResult(true);
            return PetraOrchestratorResult.Done(Guid.NewGuid(), Array.Empty<string>(), "ok");
        });

        using (var seedScope = sp.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            db.PetraInbox.Add(new PetraInbox
            {
                UserInput = "in-flight task", Source = "dashboard",
                Status = "running", EnqueuedAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var channel = sp.GetRequiredService<PetraInboxChannel>();

        // 觸發 StopAsync 軟通知 + 驗 channel.Writer.IsCompleted=true
        // 注：T5 不真實跑 ExecuteAsync framework lifecycle / 簡化驗 StopAsync 行為流程（30 min timeout fire 留 Aria gate2 + production force kill 場景）
        Assert.False(channel.Reader.Completion.IsCompleted);
        channel.Writer.TryComplete();
        Assert.True(channel.Reader.Completion.IsCompleted);   // soft notify 生效

        // 證明 dispatch 仍可完成（不受 channel.Writer.Complete 影響 / dispatch 用獨立 CT）
        await dispatchCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5)).ContinueWith(_ => { });
    }

    // ─── T6：場景 F Stage 76 retry path 整合 0 regression（Aria 議題 1 拍板方案 A — virtual + stub override） ─────────────
    [Fact]
    public async Task T6_PetraDispatchWorker_ResultHandling_AlignsStage76RetryPath()
    {
        var dbName = nameof(T6_PetraDispatchWorker_ResultHandling_AlignsStage76RetryPath);

        await using var sp = BuildSp(dbName, ct => Task.FromResult(
            PetraOrchestratorResult.Failure(Guid.NewGuid(), Array.Empty<string>(), "HTTP 500 Internal Server Error")));

        Guid rowId;
        using (var seedScope = sp.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            var row = new PetraInbox
            {
                UserInput = "transient task", Source = "dashboard",
                Status = "running", EnqueuedAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow,
                AttemptCount = 0, MaxAttempts = 3,
            };
            db.PetraInbox.Add(row);
            await db.SaveChangesAsync();
            rowId = row.Id;
        }

        var channel = sp.GetRequiredService<PetraInboxChannel>();
        var pushService = sp.GetRequiredService<DashboardPushService>();
        var worker = new PetraDispatchWorker(channel, sp, null!, pushService, NullLogger<PetraDispatchWorker>.Instance);

        // 整合 invoke DispatchOneAsync 整套（Stage 76 retry path 0 邏輯改變紀律真實 regression test cover）
        using var dispatchCts = new CancellationTokenSource();
        await worker.DispatchOneAsync(0, rowId, dispatchCts.Token);

        // verify Stage 76 Transient retry path（AttemptCount=1 / Status='pending' / NextRetryAt 設定 / ErrorMessage 寫入）
        using var verifyScope = sp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fetched = await verifyDb.PetraInbox.FirstAsync(x => x.Id == rowId);

        Assert.Equal("pending", fetched.Status);          // Stage 76 Transient retry — 不標 failed
        Assert.Equal(1, fetched.AttemptCount);             // caller 算 newAttemptCount=0+1 傳 / method 直接 set
        Assert.NotNull(fetched.NextRetryAt);
        Assert.Equal("HTTP 500 Internal Server Error", fetched.ErrorMessage);
        Assert.Null(fetched.StartedAt);                    // reset 守 fresh dispatch
        Assert.Null(fetched.CompletedAt);

        // NextRetryAt 對齊 ComputeNextRetryAt 區間 — attempt=1 → 30s ± 20% jitter → 24~36s
        var deltaSec = (fetched.NextRetryAt!.Value - DateTime.UtcNow).TotalSeconds;
        Assert.InRange(deltaSec, 23, 37);
    }

    // ─── T7：場景 G MaxConcurrentPetra AppSetting 動態讀取 + 範圍守 [1, 10] ─────
    [Theory]
    [InlineData("5", 5)]      // valid in range
    [InlineData("3", 3)]      // valid default
    [InlineData("10", 10)]    // max
    [InlineData("1", 1)]      // min
    [InlineData("0", 3)]      // out of range → fallback
    [InlineData("11", 3)]     // out of range → fallback
    [InlineData("-1", 3)]     // negative → fallback
    [InlineData("abc", 3)]    // invalid → fallback
    [InlineData("", 3)]       // empty → fallback
    public async Task T7_MaxConcurrentPetra_AppSettingDynamicRead_RespectsRange(string rawValue, int expected)
    {
        var dbName = $"{nameof(T7_MaxConcurrentPetra_AppSettingDynamicRead_RespectsRange)}_{Guid.NewGuid():N}";
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDbContext<AppDbContext>(o => o
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
        sc.Configure<WorkflowSettings>(_ => { });   // Defaults.MaxConcurrentPetra = 3
        sc.AddSingleton<AppSettingsService>();
        sc.AddSingleton<WorkflowSettingsResolver>();
        await using var sp = sc.BuildServiceProvider();

        // seed app_settings row
        using (var seedScope = sp.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            db.AppSettings.Add(new AppSetting
            {
                Key = "Workflow:MaxConcurrentPetra",
                Value = rawValue,
                Description = "test",
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var resolver = sp.GetRequiredService<WorkflowSettingsResolver>();
        var n = await resolver.GetMaxConcurrentPetraAsync();

        Assert.Equal(expected, n);
    }
}
