using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 75：v5.5 Phase 3 — PetraInbox FIFO polling 派工 BackgroundService。
///
/// 設計（對齊既有 4 BackgroundService pattern）：
/// - 對齊 <see cref="AiTeam.Bot.Orchestration.InteractionProcessor"/> 3 秒 polling 紀律 + IServiceScopeFactory.CreateAsyncScope per row
/// - 對齊 <see cref="PetraSessionRecoveryService"/> 啟動延遲（等其他 hosted service ready）
/// - 對齊 <see cref="AiTeam.Bot.Orchestration.AgentQueueProcessor"/> Crash Recovery 紀律（啟動掃描 running → 重設 pending）
///
/// 議題 1 拍板實踐：每 row 開新 PetraOrchestratorService Scoped instance → multi-session 並存 OK（既有 Scoped lifetime 紀律保留）。
/// FIFO 紀律：EnqueuedAt ASC polling / 不引入 priority / preemption / 對齊「自己用爽 / 不過早 over-engineer」精神。
/// </summary>
public class PetraInboxProcessor(
    IServiceProvider serviceProvider,
    DashboardPushService pushService,
    ILogger<PetraInboxProcessor> logger) : BackgroundService
{
    private const int PollingIntervalMs = 3000;
    private const int StartupDelaySeconds = 10;

    // Stage 76：retry path config — 對齊業界 LLM retry 紀律標準（base 30s × 2 × max 3 attempts + ±20% jitter）
    private const int RetryBaseDelaySeconds = 30;
    private const double RetryJitterFactor  = 0.2;   // ±20% AWS finding reduce retry storm 60-80%
    // 用 Random.Shared（.NET 6+ 內建 thread-safe / 0 instance 管理 — Aria 議題 3 nit）

    /// <summary>Stage 76：計算下次 retry 時間 — exponential backoff（base × 2^(attempt-1)）+ ±20% jitter。</summary>
    private static DateTime ComputeNextRetryAt(int newAttemptCount)
    {
        // exponential backoff：base × 2^(attempt-1) — attempt=1 → 30s / attempt=2 → 60s / attempt=3 → 120s
        var backoffSeconds = RetryBaseDelaySeconds * Math.Pow(2, newAttemptCount - 1);
        // ±20% jitter — 對齊 AWS finding
        var jitter = backoffSeconds * RetryJitterFactor;
        var jitterDelta = (Random.Shared.NextDouble() * 2 - 1) * jitter;   // [-jitter, +jitter] — Random.Shared thread-safe（Aria 議題 3 nit）
        var finalSeconds = backoffSeconds + jitterDelta;
        return DateTime.UtcNow.AddSeconds(finalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 啟動延遲 — 等其他 hosted service ready（對齊 PetraSessionRecoveryService 紀律）
        try { await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds), stoppingToken); }
        catch (OperationCanceledException) { return; }

        // 啟動 Crash Recovery — 對齊 AgentQueueProcessor.RecoverStuckTasksAsync 紀律
        await RecoverStuckRunningAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnePendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // 容錯：單筆 row process 失敗不擋後續 polling（對齊 InteractionProcessor outer try-catch 紀律）
                logger.LogError(ex, "PetraInboxProcessor polling 異常 — 容錯不擋後續 polling");
            }

            try { await Task.Delay(PollingIntervalMs, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessOnePendingAsync(CancellationToken ct)
    {
        // 取一筆 pending row（FIFO）+ atomic check 切 running — 同 scope DbContext
        Guid rowId;
        string userInput;
        await using (var pickScope = serviceProvider.CreateAsyncScope())
        {
            var pickRepo = pickScope.ServiceProvider.GetRequiredService<PetraInboxRepository>();
            var pickDb   = pickScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pending = await pickRepo.GetNextPendingAsync(ct);
            if (pending is null) return;   // 無 pending — 等下 tick

            var marked = await pickRepo.TryMarkRunningAsync(pending.Id, ct);
            if (!marked) return;   // 已被其他 polling cycle 搶（防雙重 process — 場景 C）

            await pickDb.SaveChangesAsync(ct);
            rowId = pending.Id;
            userInput = pending.UserInput;
        }

        _ = pushService.PushInteractionUpdateAsync();

        logger.LogInformation(
            "PetraInboxProcessor 接手 row={Id} userInputLen={Len} — 開新 Scoped PetraOrchestratorService 處理",
            rowId, userInput.Length);

        // 處理段 — 開新 Scoped PetraOrchestratorService instance（議題 1 拍板實踐 / multi-session 並存）
        try
        {
            PetraOrchestratorResult result;
            await using (var runScope = serviceProvider.CreateAsyncScope())
            {
                var orchestrator = runScope.ServiceProvider.GetRequiredService<PetraOrchestratorService>();
                result = await orchestrator.StartAsync(taskGroupId: null, userInput, ct);
            }

            // Trial_v21 揭：PetraOrchestratorService 內部 catch Exception 後 return Failure result 而非拋出，
            // 須 check result.Success 才能對齊 Status='failed' 語意（避免 escalated session 仍標 completed 的事後 monitoring 誤判）。
            // Stage 76 擴：result.Success=false 進 ErrorClassifier 分類 → Transient retry / BusinessRule 立即 failed / Permanent 立即 failed
            await using (var doneScope = serviceProvider.CreateAsyncScope())
            {
                var doneRepo = doneScope.ServiceProvider.GetRequiredService<PetraInboxRepository>();
                var doneDb   = doneScope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (result.Success)
                {
                    await doneRepo.MarkCompletedAsync(rowId, result.SessionId, ct);
                    await doneDb.SaveChangesAsync(ct);
                    logger.LogInformation(
                        "PetraInboxProcessor 完成 row={Id} sessionId={SessionId}",
                        rowId, result.SessionId);
                }
                else
                {
                    var errorMessage = result.ErrorMessage ?? result.Summary;
                    var category = PetraErrorClassifier.Classify(errorMessage);

                    // 取最新 AttemptCount + MaxAttempts（避免 stale snapshot — 對齊 W2 trade-off 紀律延伸）
                    var currentRow = await doneDb.PetraInbox.FirstOrDefaultAsync(x => x.Id == rowId, ct);
                    if (currentRow is null)
                    {
                        logger.LogWarning("PetraInboxProcessor row={Id} 處理結束時消失 — 忽略", rowId);
                        return;
                    }

                    // Stage 76：3 路分支 — Transient retry / Transient exhausted → DLQ / BusinessRule+Permanent fail-fast
                    if (category == PetraErrorCategory.Transient && currentRow.AttemptCount + 1 < currentRow.MaxAttempts)
                    {
                        var newAttemptCount = currentRow.AttemptCount + 1;
                        var nextRetryAt = ComputeNextRetryAt(newAttemptCount);
                        await doneRepo.MarkPendingWithRetryAsync(rowId, newAttemptCount, errorMessage, nextRetryAt, ct);
                        await doneDb.SaveChangesAsync(ct);
                        logger.LogWarning(
                            "PetraInboxProcessor row={Id} transient fail — retry path attempt={Attempt}/{Max} nextRetryAt={NextRetryAt} error={Error}",
                            rowId, newAttemptCount, currentRow.MaxAttempts, nextRetryAt, errorMessage);
                    }
                    else if (category == PetraErrorCategory.Transient)
                    {
                        // exhausted attempts → Dead Letter
                        await doneRepo.MarkDeadAsync(rowId, errorMessage, ct);
                        await doneDb.SaveChangesAsync(ct);
                        logger.LogError(
                            "PetraInboxProcessor row={Id} exhausted attempts={Attempts} → Dead Letter（等 Dashboard 重跑介入） error={Error}",
                            rowId, currentRow.AttemptCount + 1, errorMessage);
                    }
                    else
                    {
                        // BusinessRule / Permanent → fail-fast 不 retry
                        await doneRepo.MarkFailedAsync(rowId, errorMessage, ct);
                        await doneDb.SaveChangesAsync(ct);
                        logger.LogWarning(
                            "PetraInboxProcessor row={Id} fail-fast category={Category} sessionId={SessionId} error={Error}",
                            rowId, category, result.SessionId, errorMessage);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "PetraInboxProcessor 處理 row={Id} 失敗 — 標 failed", rowId);
            try
            {
                await using var failScope = serviceProvider.CreateAsyncScope();
                var failRepo = failScope.ServiceProvider.GetRequiredService<PetraInboxRepository>();
                var failDb   = failScope.ServiceProvider.GetRequiredService<AppDbContext>();
                await failRepo.MarkFailedAsync(rowId, ex.Message, CancellationToken.None);
                await failDb.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception markEx)
            {
                logger.LogError(markEx, "PetraInboxProcessor 標 failed 失敗 row={Id}", rowId);
            }
        }
        finally
        {
            _ = pushService.PushInteractionUpdateAsync();
        }
    }

    private async Task RecoverStuckRunningAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<PetraInboxRepository>();
            var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var count = await repo.RecoverStuckRunningAsync(ct);
            if (count > 0)
            {
                await db.SaveChangesAsync(ct);
                logger.LogWarning("PetraInboxProcessor Crash Recovery：{N} 個 running row 重設為 pending", count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PetraInboxProcessor Crash Recovery 失敗 — 容錯不中斷 Bot");
        }
    }
}
