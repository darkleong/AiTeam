using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 77：v5.5 Phase 3 補強 — fire-and-forget A2 完整版 multi-consumer BackgroundService。
///
/// 對齊業界 7 議題完整 incorporated（BackgroundService + Channel 紀律 / Anthropic rate limit / Graceful shutdown drain）：
/// - N=3 default consumer loop（Task.WhenAll lifecycle / per-Task CreateAsyncScope 紀律）
/// - dispatch CT 跟 stoppingToken 解耦（守 drain 期間 in-flight 不被中斷）
/// - StopAsync override 30 min drain timeout（對齊 Petra chain longest ~13 min × 2x safety buffer）
/// - Stage 76 retry path 3 路分支整套搬遷（0 邏輯改變 / 業界紀律「搬遷 = 等價變換」）
///
/// 設計紀律 8 條：
/// 1. dispatch CT 跟 stoppingToken 解耦 — 守「不 kill in-flight」精神
/// 2. consumer await foreach 用 stoppingToken — channel.Writer.TryComplete 後自然 break
/// 3. per-Task CreateAsyncScope — 對齊 Stage 75 既有紀律
/// 4. 3 scope 分段（load → run → done）— 對齊 Stage 75/76 既有 pattern
/// 5. 外層 catch fallback MarkFailed — Stage 76 既有「外層 catch = fail-fast」精神延續
/// 6. Random.Shared — .NET 6+ thread-safe / 多 consumer 並行 0 race
/// 7. ConsumeLoopAsync + DispatchOneAsync 標 internal — xUnit Test 直接 invoke
/// 8. MaxConcurrentPetra 啟動時讀一次 — SQL UPDATE 需 Bot 重啟生效（對齊「自己用爽 / 不過早 over-engineer」）
/// </summary>
public class PetraDispatchWorker(
    PetraInboxChannel channel,
    IServiceProvider serviceProvider,
    WorkflowSettingsResolver workflowResolver,
    DashboardPushService pushService,
    DiscordAlertService discordAlert,
    ILogger<PetraDispatchWorker> logger) : BackgroundService
{
    private const int StartupDelaySeconds = 10;
    private const int DrainTimeoutMinutes = 30;

    // Stage 76：retry path config（搬遷自 PetraInboxProcessor / 0 邏輯改變）
    private const int RetryBaseDelaySeconds = 30;
    private const double RetryJitterFactor  = 0.2;

    // Stage 77：dispatch CT 跟 stoppingToken 解耦 — 守 drain 期間 in-flight Petra 不被 host stop cancel 中斷
    private readonly CancellationTokenSource _dispatchCts = new();
    private Task[]? _consumers;

    /// <summary>Stage 76：計算下次 retry 時間 — exponential backoff（base × 2^(attempt-1)）+ ±20% jitter（搬遷 0 邏輯改變）。</summary>
    private static DateTime ComputeNextRetryAt(int newAttemptCount)
    {
        var backoffSeconds = RetryBaseDelaySeconds * Math.Pow(2, newAttemptCount - 1);
        var jitter = backoffSeconds * RetryJitterFactor;
        var jitterDelta = (Random.Shared.NextDouble() * 2 - 1) * jitter;
        return DateTime.UtcNow.AddSeconds(backoffSeconds + jitterDelta);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds), stoppingToken); }
        catch (OperationCanceledException) { return; }

        var n = await workflowResolver.GetMaxConcurrentPetraAsync(stoppingToken);
        logger.LogInformation(
            "PetraDispatchWorker 啟動 — consumer count={N} drainTimeout={Mins}min", n, DrainTimeoutMinutes);

        _consumers = Enumerable.Range(0, n)
            .Select(i => ConsumeLoopAsync(i, stoppingToken, _dispatchCts.Token))
            .ToArray();

        try
        {
            await Task.WhenAll(_consumers);
        }
        catch (OperationCanceledException) { }
        logger.LogInformation("PetraDispatchWorker 所有 consumer loop 結束");
    }

    /// <summary>Stage 77：consumer loop — await foreach 拿 rowId / channel.Writer.Complete() 後自然 break。</summary>
    /// <param name="loopCt">stoppingToken：framework cancel 時拋 OCE（force-stop 場景）/ channel.Writer.Complete 後拿完 buffered 自然 break（不拋 OCE）。</param>
    /// <param name="dispatchCt">_dispatchCts.Token：drain 期間不被 cancel / 守 in-flight Petra 跑完 / timeout 30 min 才 force cancel。</param>
    internal async Task ConsumeLoopAsync(int workerIndex, CancellationToken loopCt, CancellationToken dispatchCt)
    {
        logger.LogInformation("PetraDispatchWorker consumer={Index} 啟動", workerIndex);
        try
        {
            await foreach (var rowId in channel.Reader.ReadAllAsync(loopCt))
            {
                try
                {
                    await DispatchOneAsync(workerIndex, rowId, dispatchCt);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "PetraDispatchWorker consumer={Index} dispatch row={Id} 失敗 — 容錯不擋後續 pickup",
                        workerIndex, rowId);
                }
            }
        }
        catch (OperationCanceledException) { }
        logger.LogInformation("PetraDispatchWorker consumer={Index} 結束", workerIndex);
    }

    /// <summary>Stage 77：dispatch 段 — load row + orchestrator.StartAsync + Stage 76 retry path 3 路分支（搬遷 0 邏輯改變）。</summary>
    internal async Task DispatchOneAsync(int workerIndex, Guid rowId, CancellationToken ct)
    {
        // load row userInput + Attachments（PetraInboxProcessor 已切 running + push channel / 此處只 fetch）
        // Stage 79：v5.5 image flow 補完 — 反序列化 Attachments / Type="image" → ImageAttachment list
        string userInput;
        List<ImageAttachment>? images = null;
        await using (var loadScope = serviceProvider.CreateAsyncScope())
        {
            var loadDb = loadScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await loadDb.PetraInbox.FirstOrDefaultAsync(x => x.Id == rowId, ct);
            if (row is null)
            {
                logger.LogWarning("PetraDispatchWorker consumer={Index} row={Id} 不存在 — skip", workerIndex, rowId);
                return;
            }
            userInput = row.UserInput;
            if (!string.IsNullOrEmpty(row.Attachments))
            {
                images = DeserializeImageAttachments(row.Attachments, logger);
            }
        }

        _ = pushService.PushInteractionUpdateAsync();

        logger.LogInformation(
            "PetraDispatchWorker consumer={Index} pickup row={Id} userInputLen={Len} imageCount={ImgCount} — 開新 Scoped PetraOrchestratorService",
            workerIndex, rowId, userInput.Length, images?.Count ?? 0);

        // 處理段 — 開新 Scoped PetraOrchestratorService instance（per-Task / multi-session 並存）
        try
        {
            PetraOrchestratorResult result;
            await using (var runScope = serviceProvider.CreateAsyncScope())
            {
                var orchestrator = runScope.ServiceProvider.GetRequiredService<PetraOrchestratorService>();
                // Stage 79：images 傳 StartAsync（PetraOrchestratorService 內部 propagate 3 LLM call sites + dispatch chain 條件性 worker AIContent）
                result = await orchestrator.StartAsync(taskGroupId: null, userInput, ct, images);
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
                        "PetraDispatchWorker consumer={Index} 完成 row={Id} sessionId={SessionId}",
                        workerIndex, rowId, result.SessionId);
                }
                else
                {
                    var errorMessage = result.ErrorMessage ?? result.Summary;
                    var category = PetraErrorClassifier.Classify(errorMessage);

                    // 取最新 AttemptCount + MaxAttempts（避免 stale snapshot — 對齊 W2 trade-off 紀律延伸）
                    var currentRow = await doneDb.PetraInbox.FirstOrDefaultAsync(x => x.Id == rowId, ct);
                    if (currentRow is null)
                    {
                        logger.LogWarning("PetraDispatchWorker consumer={Index} row={Id} 處理結束時消失 — 忽略", workerIndex, rowId);
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
                            "PetraDispatchWorker consumer={Index} row={Id} transient fail — retry attempt={Attempt}/{Max} nextRetryAt={NextRetryAt} error={Error}",
                            workerIndex, rowId, newAttemptCount, currentRow.MaxAttempts, nextRetryAt, errorMessage);
                    }
                    else if (category == PetraErrorCategory.Transient)
                    {
                        // exhausted attempts → Dead Letter
                        await doneRepo.MarkDeadAsync(rowId, errorMessage, ct);
                        await doneDb.SaveChangesAsync(ct);
                        logger.LogError(
                            "PetraDispatchWorker consumer={Index} row={Id} exhausted attempts={Attempts} → Dead Letter（等 Dashboard 重跑介入） error={Error}",
                            workerIndex, rowId, currentRow.AttemptCount + 1, errorMessage);
                        // Stage 85 子項 1：dead-letter 進 Discord push + SignalR toast（rate-limit 共用 wrapper）
                        await discordAlert.SendThrottledAsync("petra_dead_letter",
                            $"⚠️ **[Stage 85 dead-letter]** PetraInbox row=`{rowId}` 連續 {currentRow.AttemptCount + 1} 次 transient fail / 進 Dead Letter\n錯誤：{errorMessage}");
                    }
                    else
                    {
                        // BusinessRule / Permanent → fail-fast 不 retry
                        await doneRepo.MarkFailedAsync(rowId, errorMessage, ct);
                        await doneDb.SaveChangesAsync(ct);
                        logger.LogWarning(
                            "PetraDispatchWorker consumer={Index} row={Id} fail-fast category={Category} sessionId={SessionId} error={Error}",
                            workerIndex, rowId, category, result.SessionId, errorMessage);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // worst-case 外層 catch — 不走 classifier / 直接標 failed（Stage 76 紀律延續：worst-case catch = fail-fast / 0 retry）
            logger.LogError(ex,
                "PetraDispatchWorker consumer={Index} 處理 row={Id} 失敗 — 標 failed", workerIndex, rowId);
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
                logger.LogError(markEx, "PetraDispatchWorker consumer={Index} 標 failed 失敗 row={Id}", workerIndex, rowId);
            }
        }
        finally
        {
            _ = pushService.PushInteractionUpdateAsync();
        }
    }

    /// <summary>
    /// Stage 77：graceful shutdown drain — 4 階段。
    /// 1. soft notify：channel.Writer.TryComplete() — consumer await foreach 拿完 buffered 自然 break
    /// 2. wait drain：Task.WhenAll(_consumers).WaitAsync(30 min) — in-flight Petra 用 dispatchCt 不被 stoppingToken 影響
    /// 3. timeout fallback：_dispatchCts.Cancel() — force cancel in-flight
    /// 4. base lifecycle：await base.StopAsync(ct) — framework state 一致
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "PetraDispatchWorker StopAsync — channel.Writer.Complete() + drain timeout {Mins} min（不 kill in-flight）",
            DrainTimeoutMinutes);

        // 軟通知 producer：no more rows incoming → consumer await foreach 拿完 buffered 後自然 break
        channel.Writer.TryComplete();

        if (_consumers is not null)
        {
            try
            {
                await Task.WhenAll(_consumers).WaitAsync(TimeSpan.FromMinutes(DrainTimeoutMinutes), cancellationToken);
                logger.LogInformation("PetraDispatchWorker drain 完成 — 所有 in-flight Petra 跑完");
            }
            catch (TimeoutException)
            {
                logger.LogWarning(
                    "PetraDispatchWorker drain timeout {Mins} min — 強制中斷剩餘 in-flight Petra（_dispatchCts.Cancel）",
                    DrainTimeoutMinutes);
                _dispatchCts.Cancel();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("PetraDispatchWorker drain 被 host cancellationToken 中止");
            }
        }

        // 最後 call base — _stoppingCts.Cancel() + await _executeTask（此時 ExecuteAsync 已大致 return / base 立刻返回）
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _dispatchCts.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Stage 79：v5.5 image flow 補完 — 反序列化 PetraInbox.Attachments JSON。
    /// 預期格式：[{ "type": "image", "base64Data": "...", "mediaType": "image/png" }, ...]
    /// 半抽象 future-friendly：未知 type 略過 / 未來擴展 PDF/document 加 type 不擾既有 image dispatch。
    /// 容錯：JSON parse 失敗 log warning + return null（caller 純文字 dispatch 0 fire-stop）。
    /// </summary>
    private static List<ImageAttachment>? DeserializeImageAttachments(string json, ILogger logger)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) return null;

            var list = new List<ImageAttachment>();
            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                if (elem.TryGetProperty("type", out var typeProp)
                    && typeProp.GetString() == "image"
                    && elem.TryGetProperty("base64Data", out var dataProp)
                    && elem.TryGetProperty("mediaType", out var mediaProp))
                {
                    list.Add(new ImageAttachment(dataProp.GetString() ?? "", mediaProp.GetString() ?? ""));
                }
                // 未知 type 略過（半抽象 future-friendly 紀律）
            }
            return list.Count > 0 ? list : null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogWarning(ex, "Stage 79：PetraInbox Attachments 反序列化失敗 jsonLen={Len}", json.Length);
            return null;
        }
    }
}
