using System.Collections.Concurrent;

namespace AiTeam.Bot.Orchestration.Hitl;

/// <summary>
/// Stage 51：In-memory「中途介入」trigger 旗標 store（v4 漸進遷移第三步試點）。
///
/// 設計理由（vs 替代方案）：
///   - 不寫 DB schema（議題 #2 拍板「不加新欄位」）
///   - 不解析 framework state JsonElement 內部結構（避免 framework 版本變動 break）
///   - HITL 等待 phase 不依賴此 trigger（MidInterruptCheckExecutor 已消耗回 false → 寫進 KickoffState.MidInterruptRequestPending）
///   - Bot 重啟丟失「等待按鍵」狀態可接受（Christ 重新點介入按鈕即可，且按下到下個 superstep 邊界本就是時間敏感）
///
/// 流程：
///   - Bot Internal API（POST /internal/kickoff/trigger-mid-interrupt）→ FrameworkHitlBridge.TriggerMidInterruptFlagAsync
///     → KickoffMidInterruptTriggerStore.Set(groupId)
///   - MidInterruptCheckExecutor.HandleVerdictAsync 每個 Petra Round 結束後 → store.TryConsume(groupId)
///     → true 時 emit MidInterruptRequest 給 RequestPort（一次性消耗）
///
/// Singleton — Bot 程序生命週期持有（Bot 重啟 = 全清空）。
/// </summary>
public sealed class KickoffMidInterruptTriggerStore
{
    private readonly ConcurrentDictionary<Guid, byte> _triggered = new();

    /// <summary>設定 groupId 為「待觸發」狀態。Bot Internal API 收到 trigger 請求時呼叫。</summary>
    public void Set(Guid groupId) => _triggered[groupId] = 1;

    /// <summary>嘗試消耗 trigger flag。true = 本次 trigger 成功消耗（caller 應 emit MidInterruptRequest）。
    /// MidInterruptCheckExecutor 每個 Petra Round 結束後呼叫此 method，原子 remove + 回傳是否存在。</summary>
    public bool TryConsume(Guid groupId) => _triggered.TryRemove(groupId, out _);

    /// <summary>檢查 groupId 是否處於「待觸發」狀態（不消耗）。Dashboard UI 可查詢顯示。</summary>
    public bool IsPending(Guid groupId) => _triggered.ContainsKey(groupId);
}
