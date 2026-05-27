using AiTeam.Dashboard.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace AiTeam.Dashboard.Controllers;

/// <summary>
/// F16：Bot 寫入 mcp_* 表後透過此端點觸發 SignalR broadcast → Records.razor 整段 reload。
///
/// Bot → POST /api/internal/records/updated → RecordsHub.Clients.All.SendAsync("RecordsUpdated") → Browser
///
/// 對齊既有 AgentStatusController pattern（0 auth attribute / 容器內網 / Dashboard 不對外）。
/// </summary>
[ApiController]
[Route("api/internal/records")]
public class InternalRecordsHubController(IHubContext<RecordsHub> hub) : ControllerBase
{
    /// <summary>Bot 呼叫此端點通知 mcp_* 表已寫入，觸發 Records 頁即時 reload。</summary>
    [HttpPost("updated")]
    public async Task<IActionResult> UpdatedAsync()
    {
        await hub.Clients.All.SendAsync(RecordsHub.ReceiveRecordsUpdated);
        return Ok();
    }
}
