using System.Collections.Concurrent;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AiTeam.Bot.Tests.Orchestration;

/// <summary>
/// Stage 75：v5.5 Phase 3 — PetraInbox FIFO queue + TalentDispatchLockService per-Talent serialization 單元驗。
///
/// 覆蓋驗收場景：
/// - 場景 A：PetraInbox schema baseline（T1）
/// - 場景 B：FIFO polling 取最早 pending（T2）
/// - 場景 C：atomic check 防雙重 process（T3）
/// - 場景 D：CountPendingBySource queue position 計算（T7）
/// - 場景 E：同 Talent serialization（T4）
/// - 場景 F：不同 Talent 平行（T5）
/// - 場景 G：MarkFailed 保留 ErrorMessage（T6）
///
/// 紀律對齊：xUnit + InMemory DB（對齊 Stage74TalentSkillModelTests / PromptRepositoryTests 既有 pattern）。
/// 議題 2 Christ 拍板：🥇 SemaphoreSlim per-Talent（in-memory ConcurrentDictionary&lt;Guid, SemaphoreSlim&gt;）。
/// </summary>
public class Stage75InboxQueueTests
{
    private static AppDbContext CreateInMemoryDb(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    // ─── T1：場景 A PetraInbox schema baseline ─────────────────────────
    [Fact]
    public async Task T1_PetraInbox_Schema_Baseline()
    {
        await using var db = CreateInMemoryDb(nameof(T1_PetraInbox_Schema_Baseline));
        db.Database.EnsureCreated();

        var row = new PetraInbox
        {
            UserInput = "test task",
            Source = "dashboard",
            Status = "pending",
            EnqueuedAt = DateTime.UtcNow,
        };
        db.PetraInbox.Add(row);
        await db.SaveChangesAsync();

        var fetched = await db.PetraInbox.FirstAsync();
        Assert.Equal("test task", fetched.UserInput);
        Assert.Equal("dashboard", fetched.Source);
        Assert.Equal("pending", fetched.Status);
        Assert.Null(fetched.PetraSessionId);
        Assert.Null(fetched.StartedAt);
        Assert.Null(fetched.CompletedAt);
        Assert.Null(fetched.ErrorMessage);
    }

    // ─── T2：場景 B FIFO polling 取最早 pending ─────────────────────────
    [Fact]
    public async Task T2_PetraInboxRepository_GetNextPending_OldestFirst()
    {
        await using var db = CreateInMemoryDb(nameof(T2_PetraInboxRepository_GetNextPending_OldestFirst));
        db.Database.EnsureCreated();
        var repo = new PetraInboxRepository(db);

        var baseTime = DateTime.UtcNow;
        db.PetraInbox.AddRange(
            new PetraInbox { UserInput = "second", Source = "dashboard", Status = "pending", EnqueuedAt = baseTime.AddSeconds(1) },
            new PetraInbox { UserInput = "first",  Source = "dashboard", Status = "pending", EnqueuedAt = baseTime },
            new PetraInbox { UserInput = "third",  Source = "dashboard", Status = "pending", EnqueuedAt = baseTime.AddSeconds(2) });
        await db.SaveChangesAsync();

        var next = await repo.GetNextPendingAsync();
        Assert.NotNull(next);
        Assert.Equal("first", next!.UserInput);   // FIFO 取最早 EnqueuedAt
    }

    // ─── T3：場景 C atomic check 防雙重 process ─────────────────────────
    [Fact]
    public async Task T3_PetraInboxRepository_TryMarkRunning_AtomicCheck()
    {
        await using var db = CreateInMemoryDb(nameof(T3_PetraInboxRepository_TryMarkRunning_AtomicCheck));
        db.Database.EnsureCreated();
        var repo = new PetraInboxRepository(db);

        var row = new PetraInbox { UserInput = "only one", Source = "dashboard", Status = "pending" };
        db.PetraInbox.Add(row);
        await db.SaveChangesAsync();

        // 第一次切 running OK
        var first = await repo.TryMarkRunningAsync(row.Id);
        await db.SaveChangesAsync();
        Assert.True(first);

        // 第二次（同 row 已 running）→ Status != pending → false（防雙重 process）
        var second = await repo.TryMarkRunningAsync(row.Id);
        Assert.False(second);

        // verify row 真實切到 running
        var fetched = await db.PetraInbox.FirstAsync(x => x.Id == row.Id);
        Assert.Equal("running", fetched.Status);
        Assert.NotNull(fetched.StartedAt);
    }

    // ─── T4：場景 E 同 Talent serialization ─────────────────────────
    [Fact]
    public async Task T4_TalentDispatchLockService_SameTalent_Sequential()
    {
        var svc = new TalentDispatchLockService();
        var talentId = Guid.NewGuid();
        var order = new ConcurrentQueue<int>();
        var task1Started = new TaskCompletionSource();

        var task1 = Task.Run(async () =>
        {
            using var l = await svc.AcquireAsync(talentId);
            order.Enqueue(1);
            task1Started.SetResult();
            await Task.Delay(200);
            order.Enqueue(2);
        });

        await task1Started.Task;
        var task2 = Task.Run(async () =>
        {
            using var l = await svc.AcquireAsync(talentId);
            order.Enqueue(3);
        });

        await Task.WhenAll(task1, task2);
        Assert.Equal(new[] { 1, 2, 3 }, order.ToArray());   // T1 release 前 T2 等鎖 / 順序保證 1→2→3
    }

    // ─── T5：場景 F 不同 Talent 平行 ─────────────────────────
    [Fact]
    public async Task T5_TalentDispatchLockService_DifferentTalents_Parallel()
    {
        var svc = new TalentDispatchLockService();
        var talentA = Guid.NewGuid();
        var talentB = Guid.NewGuid();
        var bothInside = new TaskCompletionSource();
        var unblockA = new TaskCompletionSource();
        var aInside = new TaskCompletionSource();
        var bInside = new TaskCompletionSource();

        var taskA = Task.Run(async () =>
        {
            using var l = await svc.AcquireAsync(talentA);
            aInside.SetResult();
            await unblockA.Task;   // A 持鎖等 B 也拿到鎖（驗不同 talent 平行）
        });

        var taskB = Task.Run(async () =>
        {
            using var l = await svc.AcquireAsync(talentB);
            bInside.SetResult();
        });

        // A 拿到鎖後 B 也應該能立刻拿到（不同 talent_id 0 互鎖）
        await Task.WhenAll(aInside.Task, bInside.Task).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(aInside.Task.IsCompletedSuccessfully);
        Assert.True(bInside.Task.IsCompletedSuccessfully);

        unblockA.SetResult();   // 釋放 A
        await Task.WhenAll(taskA, taskB);

        Assert.Equal(2, svc.LockCount);   // 兩個獨立 talent_id semaphore 都 alive
    }

    // ─── T6：場景 G MarkFailed 保留 ErrorMessage ─────────────────────────
    [Fact]
    public async Task T6_PetraInboxRepository_MarkFailed_PreservesErrorMessage()
    {
        await using var db = CreateInMemoryDb(nameof(T6_PetraInboxRepository_MarkFailed_PreservesErrorMessage));
        db.Database.EnsureCreated();
        var repo = new PetraInboxRepository(db);

        var row = new PetraInbox { UserInput = "task", Source = "dashboard", Status = "running" };
        db.PetraInbox.Add(row);
        await db.SaveChangesAsync();

        await repo.MarkFailedAsync(row.Id, "test error");
        await db.SaveChangesAsync();

        var fetched = await db.PetraInbox.FirstAsync(x => x.Id == row.Id);
        Assert.Equal("failed", fetched.Status);
        Assert.Equal("test error", fetched.ErrorMessage);
        Assert.NotNull(fetched.CompletedAt);
    }

    // ─── T7：場景 D CountPendingBySource queue position ─────────────────────────
    [Fact]
    public async Task T7_PetraInboxRepository_CountPendingBySource()
    {
        await using var db = CreateInMemoryDb(nameof(T7_PetraInboxRepository_CountPendingBySource));
        db.Database.EnsureCreated();
        var repo = new PetraInboxRepository(db);

        db.PetraInbox.AddRange(
            new PetraInbox { UserInput = "d1", Source = "dashboard", Status = "pending" },
            new PetraInbox { UserInput = "d2", Source = "dashboard", Status = "pending" },
            new PetraInbox { UserInput = "d3", Source = "dashboard", Status = "pending" },
            new PetraInbox { UserInput = "x1", Source = "discord",   Status = "pending" },
            new PetraInbox { UserInput = "d-done", Source = "dashboard", Status = "completed" });   // completed 不算
        await db.SaveChangesAsync();

        Assert.Equal(3, await repo.CountPendingBySourceAsync("dashboard"));
        Assert.Equal(1, await repo.CountPendingBySourceAsync("discord"));
    }
}
