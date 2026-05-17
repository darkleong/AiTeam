using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
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
            Guid? sessionId = null;
            await using (var runScope = serviceProvider.CreateAsyncScope())
            {
                var orchestrator = runScope.ServiceProvider.GetRequiredService<PetraOrchestratorService>();
                var result = await orchestrator.StartAsync(taskGroupId: null, userInput, ct);
                sessionId = result.SessionId;
            }

            await using (var doneScope = serviceProvider.CreateAsyncScope())
            {
                var doneRepo = doneScope.ServiceProvider.GetRequiredService<PetraInboxRepository>();
                var doneDb   = doneScope.ServiceProvider.GetRequiredService<AppDbContext>();
                await doneRepo.MarkCompletedAsync(rowId, sessionId, ct);
                await doneDb.SaveChangesAsync(ct);
            }

            logger.LogInformation(
                "PetraInboxProcessor 完成 row={Id} sessionId={SessionId}",
                rowId, sessionId);
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
