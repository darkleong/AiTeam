using System.Threading.Channels;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 75：v5.5 Phase 3 — PetraInbox FIFO polling 派工 BackgroundService。
///
/// Stage 77：v5.5 Phase 3 補強 — 退化為 pure producer（DB poll → channel.Writer.WriteAsync）。
/// dispatch + retry path 邏輯整套搬到 <see cref="PetraDispatchWorker"/>（multi-consumer / Channel + Task.WhenAll）。
///
/// 紀律延續：
/// - 3 秒 polling 間隔（對齊 InteractionProcessor）
/// - 啟動延遲 10s（對齊 PetraSessionRecoveryService）
/// - Crash Recovery 啟動掃 running → pending（對齊 AgentQueueProcessor）
/// - W2 trade-off：「先 read 再 UPDATE」非真正 atomic（單 Bot OK）
/// </summary>
public class PetraInboxProcessor(
    IServiceProvider serviceProvider,
    PetraInboxChannel channel,
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
            catch (ChannelClosedException)
            {
                // Stage 77 Aria 議題 3：PetraDispatchWorker.StopAsync 已 channel.Writer.TryComplete() →
                // PetraInboxProcessor 後續 WriteAsync 拋 ChannelClosedException — 視為正常 shutdown / 不算「polling 異常」/
                // 該 row 仍標 running / 下次 Bot 重啟 RecoverStuckRunningAsync 救回（對齊 Crash Recovery 紀律）
                logger.LogInformation("PetraInboxProcessor: PetraDispatchWorker shutdown — row 仍標 running / Crash Recovery 救回");
                break;
            }
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
        // Stage 77：取一筆 pending row + atomic check 切 running + push channel — 0 dispatch / 0 retry path（搬 PetraDispatchWorker）
        Guid rowId;
        await using (var pickScope = serviceProvider.CreateAsyncScope())
        {
            var pickRepo = pickScope.ServiceProvider.GetRequiredService<PetraInboxRepository>();
            var pickDb   = pickScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pending = await pickRepo.GetNextPendingAsync(ct);
            if (pending is null) return;   // 無 pending — 等下 tick

            var marked = await pickRepo.TryMarkRunningAsync(pending.Id, ct);
            if (!marked) return;   // 已被其他 polling cycle 搶（防雙重 process）

            await pickDb.SaveChangesAsync(ct);
            rowId = pending.Id;
        }

        _ = pushService.PushInteractionUpdateAsync();

        // Stage 77：push row 進 Channel — 若 channel full 則自然 backpressure（FullMode=Wait / producer 等空位）
        await channel.Writer.WriteAsync(rowId, ct);
        logger.LogInformation(
            "PetraInboxProcessor push row={Id} to channel — PetraDispatchWorker consumer 接手 dispatch", rowId);
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
