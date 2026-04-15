using System.Collections.Concurrent;
using AiTeam.Data;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Bot.Orchestration;

/// <summary>
/// Stage 27a：Agent 佇列服務。
/// 封裝 TaskItem 的 enqueue / dequeue 操作，以及執行中 CTS 的集中管理。
/// Singleton，供 AgentQueueProcessor 和 TaskGroupService.CancelAsync 共同使用。
/// </summary>
public class AgentQueueService(
    IServiceScopeFactory scopeFactory,
    ILogger<AgentQueueService> logger)
{
    private readonly ManualResetEventSlim _signal = new(false);

    // 執行中 TaskItem 的 CTS，供外部取消時 kill subprocess
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningCts = new();

    // ---- Enqueue ----

    /// <summary>
    /// 將 TaskItem 推入佇列：設定 QueuedAt、QueueStatus = "queued"，存 DB，喚醒 Processor。
    /// TaskItem 必須已存入 DB（Id 已分配）。
    /// </summary>
    public async Task EnqueueAsync(TaskItem task, CancellationToken ct = default)
    {
        await using var scope   = scopeFactory.CreateAsyncScope();
        var db                  = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        task.QueuedAt    = DateTime.UtcNow;
        task.QueueStatus = "queued";

        db.Attach(task);
        db.Entry(task).Property(t => t.QueuedAt).IsModified    = true;
        db.Entry(task).Property(t => t.QueueStatus).IsModified = true;
        await db.SaveChangesAsync(ct);

        logger.LogDebug("AgentQueueService：TaskItem {Id}（{Agent}）已入佇列", task.Id, task.AssignedAgent);
        _signal.Set();
    }

    // ---- Dequeue ----

    /// <summary>
    /// 查詢 assignedAgentNames 中最早（QueuedAt ASC）的 queued TaskItem，
    /// atomically 標記 QueueStatus = "processing"，Status = "running"，回傳該 TaskItem。
    /// 若無待處理任務則回傳 null。
    /// </summary>
    public async Task<TaskItem?> DequeueAsync(string[] assignedAgentNames, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db                = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var task = await db.Set<TaskItem>()
            .Where(t => assignedAgentNames.Contains(t.AssignedAgent) && t.QueueStatus == "queued")
            .OrderBy(t => t.QueuedAt)
            .FirstOrDefaultAsync(ct);

        if (task is null) return null;

        task.QueueStatus = "processing";
        task.Status      = "running";
        await db.SaveChangesAsync(ct);

        logger.LogDebug("AgentQueueService：TaskItem {Id}（{Agent}）已出佇列，開始執行", task.Id, task.AssignedAgent);
        return task;
    }

    // ---- Signal ----

    /// <summary>
    /// 等待新任務 signal 或 timeout。Processor 主迴圈呼叫此方法作為輪詢節奏。
    /// </summary>
    public bool WaitForSignal(int timeoutMs = 3000)
    {
        var triggered = _signal.Wait(timeoutMs);
        _signal.Reset();
        return triggered;
    }

    // ---- CTS 管理 ----

    /// <summary>記錄執行中 TaskItem 的 CTS，供外部取消。</summary>
    public void RegisterCts(Guid taskId, CancellationTokenSource cts)
        => _runningCts[taskId] = cts;

    /// <summary>嘗試取消執行中的 TaskItem（best effort）。</summary>
    public void TryCancel(Guid taskId)
    {
        if (_runningCts.TryRemove(taskId, out var cts))
        {
            try { cts.Cancel(); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AgentQueueService：Cancel CTS 失敗（TaskId={Id}）", taskId);
            }
        }
    }

    /// <summary>移除並回傳指定 TaskItem 的 CTS。</summary>
    public bool TryRemoveCts(Guid taskId, out CancellationTokenSource? cts)
        => _runningCts.TryRemove(taskId, out cts);

    // ---- 佇列狀態清除 ----

    /// <summary>
    /// 執行完畢後清除 QueueStatus（設為 null），代表任務已離開佇列。
    /// </summary>
    public async Task ClearQueueStatusAsync(Guid taskId, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db                = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var task = await db.Set<TaskItem>().FindAsync([taskId], ct);
        if (task is null) return;

        task.QueueStatus = null;
        await db.SaveChangesAsync(ct);
    }

    // ---- 群組取消 ----

    /// <summary>
    /// 取消指定 TaskGroup 中所有「queued 中」的 TaskItem（尚未被 dequeue）：
    /// 直接在 DB 標記 QueueStatus = null、Status = "cancelled"。
    /// 已在執行中的任務須透過 TryCancel(taskId) 處理。
    /// </summary>
    public async Task CancelQueuedTasksForGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db                = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var queuedTasks = await db.Set<TaskItem>()
            .Where(t => t.GroupId == groupId && t.QueueStatus == "queued")
            .ToListAsync(ct);

        foreach (var task in queuedTasks)
        {
            task.QueueStatus = null;
            task.Status      = "cancelled";
        }

        if (queuedTasks.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("AgentQueueService：GroupId={Id} 的 {N} 個 queued 任務已標記 cancelled",
                groupId, queuedTasks.Count);
        }
    }
}
