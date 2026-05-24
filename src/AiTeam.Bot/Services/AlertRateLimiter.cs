using System.Collections.Concurrent;

namespace AiTeam.Bot.Services;

/// <summary>
/// Stage 85 子項 1：alert event rate-limiter — per event type per N min 只發 1 則 + aggregate 描述「N 次同類事件」。
///
/// Singleton lifecycle（跨 caller 共用同份 state）/ ConcurrentDictionary 守 thread-safe。
/// API：TryAcquire(eventType, window, out suppressedCount): bool
///   true  → caller 該發訊息（含 suppressedCount 文案 if &gt; 0）
///   false → caller 該 skip（lastSent + window 還沒到 / suppressedCount++ 已累加）
/// </summary>
public class AlertRateLimiter
{
    private sealed class WindowState
    {
        public DateTime LastSentUtc;
        public int SuppressedCount;
    }

    private readonly ConcurrentDictionary<string, WindowState> _windows = new();

    /// <summary>嘗試取得發送 quota — true 代表 caller 該發 / false 代表該 skip + 已累加 suppressedCount。</summary>
    public bool TryAcquire(string eventType, TimeSpan window, out int suppressedCount)
    {
        suppressedCount = 0;
        var now = DateTime.UtcNow;
        var state = _windows.GetOrAdd(eventType, _ => new WindowState { LastSentUtc = DateTime.MinValue });

        lock (state)
        {
            if (state.LastSentUtc == DateTime.MinValue || (now - state.LastSentUtc) >= window)
            {
                suppressedCount = state.SuppressedCount;
                state.LastSentUtc = now;
                state.SuppressedCount = 0;
                return true;
            }
            state.SuppressedCount++;
            return false;
        }
    }
}
