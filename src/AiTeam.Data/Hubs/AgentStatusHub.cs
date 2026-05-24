using Microsoft.AspNetCore.SignalR;

namespace AiTeam.Data.Hubs;

/// <summary>
/// AgentStatusHub：Bot 推送 Agent 狀態變動，Dashboard 訂閱接收即時更新。
/// Bot 透過 Dashboard HTTP API 間接觸發推送；Dashboard 直接持有 IHubContext。
/// </summary>
public class AgentStatusHub : Hub
{
    /// <summary>Dashboard 訂閱此事件以接收 Agent 狀態更新。</summary>
    public const string ReceiveAgentStatus = nameof(ReceiveAgentStatus);

    /// <summary>Dashboard 訂閱此事件以接收任務狀態變動。</summary>
    public const string ReceiveTaskUpdate = nameof(ReceiveTaskUpdate);

    /// <summary>Dashboard 訂閱此事件以接收 Token 用量更新（每次 LLM 呼叫後觸發）。</summary>
    public const string ReceiveTokenUpdate = nameof(ReceiveTokenUpdate);

    /// <summary>Dashboard 訂閱此事件以接收佇列狀態變動（enqueue / dequeue / cancel / 狀態變更時觸發）。</summary>
    public const string ReceiveQueueUpdate = nameof(ReceiveQueueUpdate);

    /// <summary>Stage 28a：Dashboard 訂閱此事件以接收互動狀態變動（新互動進來 / 回覆後即時更新）。</summary>
    public const string ReceiveInteractionUpdate = nameof(ReceiveInteractionUpdate);

    /// <summary>Stage 85 子項 1：Dashboard 訂閱此事件以接收系統 alert（TokenGuard / dead-letter / paused timeout 三類）— AlertToastSubscriber 收到後 MudSnackbar 彈出。</summary>
    public const string ReceiveAlertEvent = nameof(ReceiveAlertEvent);
}
