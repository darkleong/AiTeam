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
}
