using Microsoft.AspNetCore.SignalR;

namespace AiTeam.Dashboard.Hubs;

/// <summary>
/// F16：MCP records 寫入後 broadcast 給 Dashboard Records 頁即時整段 reload。
///
/// 設計拍板（不協商）：
///   - C(b)：1 個通用 RecordsUpdated event（不分 5 種 record 類型）
///   - D(b)：前端整段 reload（不做增量 row append）
///
/// 純 Hub class / 不掛 method / broadcast 由 InternalRecordsHubController 透過 IHubContext 觸發。
/// </summary>
public class RecordsHub : Hub
{
    /// <summary>Dashboard Records.razor 訂閱此事件 → 整段 reload。</summary>
    public const string ReceiveRecordsUpdated = "RecordsUpdated";
}
