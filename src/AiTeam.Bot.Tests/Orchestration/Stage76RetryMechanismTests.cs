using AiTeam.Bot.Orchestration.Petra;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AiTeam.Bot.Tests.Orchestration;

/// <summary>
/// Stage 76：v5.5 Phase 3 補強 — task retry / resume 機制 + PetraErrorClassifier + Dashboard 重跑單元驗。
///
/// 覆蓋驗收場景：
/// - 場景 A：PetraInbox schema 擴 4 欄 baseline（T1）
/// - 場景 B：MarkPendingWithRetryAsync 直接 set AttemptCount + NextRetryAt（T2）+ GetNextPendingAsync 守 backoff timing（T8）
/// - 場景 C：PetraErrorClassifier 3 路分類（T3 BusinessRule / T4 Transient / T5 Permanent）
/// - 場景 D：MarkDeadAsync 標 Dead Letter（T6）
/// - 場景 F：RequeueAsync 重跑 reset（T7）+ 反向防呆（T9）
///
/// 紀律對齊：xUnit + InMemory DB（對齊 Stage75InboxQueueTests 既有 pattern）。
/// Aria 議題 1 紀律：MarkPendingWithRetryAsync caller 算 newAttemptCount 傳入 / method 內直接 set。
/// </summary>
public class Stage76RetryMechanismTests
{
    private static AppDbContext CreateInMemoryDb(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    // ─── T1：場景 A PetraInbox schema 擴 4 欄 baseline ─────────────────────────
    [Fact]
    public async Task T1_PetraInbox_Schema_Extended_4Fields()
    {
        await using var db = CreateInMemoryDb(nameof(T1_PetraInbox_Schema_Extended_4Fields));
        db.Database.EnsureCreated();

        var row = new PetraInbox
        {
            UserInput = "stage 76 test",
            Source = "dashboard",
            Status = "pending",
            EnqueuedAt = DateTime.UtcNow,
        };
        db.PetraInbox.Add(row);
        await db.SaveChangesAsync();

        var fetched = await db.PetraInbox.FirstAsync();
        Assert.Equal(0, fetched.AttemptCount);        // default 0
        Assert.Equal(3, fetched.MaxAttempts);          // default 3
        Assert.Null(fetched.NextRetryAt);
        Assert.Null(fetched.DeadAt);
    }

    // ─── T2：場景 B MarkPendingWithRetryAsync 直接 set AttemptCount + NextRetryAt ─────
    [Fact]
    public async Task T2_PetraInboxRepository_MarkPendingWithRetry_IncrementsAttemptAndSetsNextRetryAt()
    {
        await using var db = CreateInMemoryDb(nameof(T2_PetraInboxRepository_MarkPendingWithRetry_IncrementsAttemptAndSetsNextRetryAt));
        db.Database.EnsureCreated();
        var repo = new PetraInboxRepository(db);

        var row = new PetraInbox
        {
            UserInput = "transient fail",
            Source = "dashboard",
            Status = "running",
            AttemptCount = 0,
            MaxAttempts = 3,
            StartedAt = DateTime.UtcNow,
        };
        db.PetraInbox.Add(row);
        await db.SaveChangesAsync();

        var nextRetryAt = DateTime.UtcNow.AddSeconds(30);
        await repo.MarkPendingWithRetryAsync(row.Id, newAttemptCount: 1, "transient http 500", nextRetryAt);
        await db.SaveChangesAsync();

        var fetched = await db.PetraInbox.FirstAsync(x => x.Id == row.Id);
        Assert.Equal("pending", fetched.Status);
        Assert.Equal(1, fetched.AttemptCount);              // caller 傳 1 → 直接 set（不 ++）
        Assert.Equal(nextRetryAt, fetched.NextRetryAt);
        Assert.Equal("transient http 500", fetched.ErrorMessage);
        Assert.Null(fetched.StartedAt);                      // reset
        Assert.Null(fetched.CompletedAt);
    }

    // ─── T3：場景 C PetraErrorClassifier Token 守門 → BusinessRule ─────
    [Fact]
    public void T3_PetraErrorClassifier_TokenGuardMessage_ClassifyAsBusinessRule()
    {
        var msg = "Token 守門：全域本月用量 10,108,845 超過全域月限 10,000,000。所有 LLM 呼叫已暫停。";
        Assert.Equal(PetraErrorCategory.BusinessRule, PetraErrorClassifier.Classify(msg));

        // 同類延伸 — 月限 / 日限 / quota / rate limit
        Assert.Equal(PetraErrorCategory.BusinessRule, PetraErrorClassifier.Classify("per-Agent 月限 fire"));
        Assert.Equal(PetraErrorCategory.BusinessRule, PetraErrorClassifier.Classify("per-Agent 日限 exceeded"));
        Assert.Equal(PetraErrorCategory.BusinessRule, PetraErrorClassifier.Classify("Anthropic API quota exceeded"));
        Assert.Equal(PetraErrorCategory.BusinessRule, PetraErrorClassifier.Classify("rate limit 429"));
    }

    // ─── T4：場景 C PetraErrorClassifier transient patterns → Transient ─────
    [Fact]
    public void T4_PetraErrorClassifier_TransientPatterns_ClassifyAsTransient()
    {
        Assert.Equal(PetraErrorCategory.Transient, PetraErrorClassifier.Classify("HTTP 500 Internal Server Error"));
        Assert.Equal(PetraErrorCategory.Transient, PetraErrorClassifier.Classify("HTTP 503 Service Unavailable"));
        Assert.Equal(PetraErrorCategory.Transient, PetraErrorClassifier.Classify("HTTP 502 Bad Gateway"));
        Assert.Equal(PetraErrorCategory.Transient, PetraErrorClassifier.Classify("Request timeout after 30s"));
        Assert.Equal(PetraErrorCategory.Transient, PetraErrorClassifier.Classify("HttpException: connection reset"));
        Assert.Equal(PetraErrorCategory.Transient, PetraErrorClassifier.Classify("System.Text.Json.JsonException: invalid token"));
        Assert.Equal(PetraErrorCategory.Transient, PetraErrorClassifier.Classify("TaskCanceledException"));
        Assert.Equal(PetraErrorCategory.Transient, PetraErrorClassifier.Classify("SocketException: refused"));
    }

    // ─── T5：場景 C PetraErrorClassifier unknown message → Permanent ─────
    [Fact]
    public void T5_PetraErrorClassifier_UnknownMessage_ClassifyAsPermanent()
    {
        Assert.Equal(PetraErrorCategory.Permanent, PetraErrorClassifier.Classify("random 未知錯誤"));
        Assert.Equal(PetraErrorCategory.Permanent, PetraErrorClassifier.Classify("NullReferenceException somewhere"));
        Assert.Equal(PetraErrorCategory.Permanent, PetraErrorClassifier.Classify(""));        // empty
        Assert.Equal(PetraErrorCategory.Permanent, PetraErrorClassifier.Classify(null));      // null
    }

    // ─── T6：場景 D MarkDeadAsync 標 Dead Letter ─────
    [Fact]
    public async Task T6_PetraInboxRepository_MarkDeadAsync_SetsDeadAtAndStatus()
    {
        await using var db = CreateInMemoryDb(nameof(T6_PetraInboxRepository_MarkDeadAsync_SetsDeadAtAndStatus));
        db.Database.EnsureCreated();
        var repo = new PetraInboxRepository(db);

        var row = new PetraInbox
        {
            UserInput = "exhausted task",
            Source = "dashboard",
            Status = "running",
            AttemptCount = 3,
            MaxAttempts = 3,
        };
        db.PetraInbox.Add(row);
        await db.SaveChangesAsync();

        await repo.MarkDeadAsync(row.Id, "exhausted attempts after 3 transient fail");
        await db.SaveChangesAsync();

        var fetched = await db.PetraInbox.FirstAsync(x => x.Id == row.Id);
        Assert.Equal("dead", fetched.Status);
        Assert.NotNull(fetched.DeadAt);
        Assert.NotNull(fetched.CompletedAt);
        Assert.Equal("exhausted attempts after 3 transient fail", fetched.ErrorMessage);
    }

    // ─── T7：場景 F RequeueAsync reset failed/dead → pending ─────
    [Fact]
    public async Task T7_PetraInboxRepository_RequeueAsync_ResetsAllFields()
    {
        await using var db = CreateInMemoryDb(nameof(T7_PetraInboxRepository_RequeueAsync_ResetsAllFields));
        db.Database.EnsureCreated();
        var repo = new PetraInboxRepository(db);

        var row = new PetraInbox
        {
            UserInput = "dead task",
            Source = "dashboard",
            Status = "dead",
            AttemptCount = 3,
            MaxAttempts = 3,
            NextRetryAt = DateTime.UtcNow.AddSeconds(120),
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            CompletedAt = DateTime.UtcNow.AddMinutes(-1),
            DeadAt = DateTime.UtcNow.AddMinutes(-1),
            ErrorMessage = "exhausted",
        };
        db.PetraInbox.Add(row);
        await db.SaveChangesAsync();

        var success = await repo.RequeueAsync(row.Id);
        await db.SaveChangesAsync();
        Assert.True(success);

        var fetched = await db.PetraInbox.FirstAsync(x => x.Id == row.Id);
        Assert.Equal("pending", fetched.Status);
        Assert.Equal(0, fetched.AttemptCount);
        Assert.Null(fetched.NextRetryAt);
        Assert.Null(fetched.StartedAt);
        Assert.Null(fetched.CompletedAt);
        Assert.Null(fetched.DeadAt);
        Assert.Null(fetched.ErrorMessage);
    }

    // ─── T8：場景 B GetNextPendingAsync 守 NextRetryAt backoff timing ─────
    [Fact]
    public async Task T8_GetNextPendingAsync_RespectsNextRetryAt_FuturePending_NoPickup()
    {
        await using var db = CreateInMemoryDb(nameof(T8_GetNextPendingAsync_RespectsNextRetryAt_FuturePending_NoPickup));
        db.Database.EnsureCreated();
        var repo = new PetraInboxRepository(db);

        // seed 一筆 pending row 但 NextRetryAt 在未來 60s（backoff 還沒過）
        var futureRow = new PetraInbox
        {
            UserInput = "future retry",
            Source = "dashboard",
            Status = "pending",
            EnqueuedAt = DateTime.UtcNow.AddSeconds(-30),
            AttemptCount = 1,
            NextRetryAt = DateTime.UtcNow.AddSeconds(60),
        };
        db.PetraInbox.Add(futureRow);
        await db.SaveChangesAsync();

        var next = await repo.GetNextPendingAsync();
        Assert.Null(next);   // backoff 還沒過 → 0 pickup

        // 加一筆「立即可 pickup」row（NextRetryAt=null）— FIFO 紀律保留
        var immediateRow = new PetraInbox
        {
            UserInput = "immediate",
            Source = "dashboard",
            Status = "pending",
            EnqueuedAt = DateTime.UtcNow,
            AttemptCount = 0,
            NextRetryAt = null,
        };
        db.PetraInbox.Add(immediateRow);
        await db.SaveChangesAsync();

        next = await repo.GetNextPendingAsync();
        Assert.NotNull(next);
        Assert.Equal("immediate", next!.UserInput);
    }

    // ─── T9：場景 F RequeueAsync 反向防呆 — completed row 不允許重跑 ─────
    [Fact]
    public async Task T9_RequeueAsync_RejectsNonFailedDeadStatus()
    {
        await using var db = CreateInMemoryDb(nameof(T9_RequeueAsync_RejectsNonFailedDeadStatus));
        db.Database.EnsureCreated();
        var repo = new PetraInboxRepository(db);

        var completedRow = new PetraInbox
        {
            UserInput = "already done",
            Source = "dashboard",
            Status = "completed",
            CompletedAt = DateTime.UtcNow,
        };
        db.PetraInbox.Add(completedRow);
        await db.SaveChangesAsync();

        var success = await repo.RequeueAsync(completedRow.Id);
        Assert.False(success);   // completed 不允許重跑（守業務正確性）

        var fetched = await db.PetraInbox.FirstAsync(x => x.Id == completedRow.Id);
        Assert.Equal("completed", fetched.Status);   // 0 改變
    }
}
