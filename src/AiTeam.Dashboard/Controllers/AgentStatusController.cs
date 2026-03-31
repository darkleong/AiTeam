using AiTeam.Data.Hubs;
using AiTeam.Shared.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
// 明確指定 Route 和 ApiController 使用 MVC 的版本，避免與 Blazor 的 RouteAttribute 衝突
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace AiTeam.Dashboard.Controllers;

/// <summary>
/// Internal API：Bot 透過此端點觸發 SignalR 推送，解決跨 Process 無法共享 IHubContext 的問題。
/// Bot → POST /internal/agent-status → Hub → Dashboard Browser
/// </summary>
[ApiController]
[Route("internal/agent-status")]
public class AgentStatusController(IHubContext<AgentStatusHub> hubContext) : ControllerBase
{
    /// <summary>Bot 呼叫此端點推送 Agent 狀態變動。</summary>
    [HttpPost]
    public async Task<IActionResult> PushAgentStatusAsync([FromBody] AgentStatusViewModel status)
    {
        await hubContext.Clients.All.SendAsync(AgentStatusHub.ReceiveAgentStatus, status);
        return Ok();
    }

    /// <summary>Bot 呼叫此端點推送任務狀態變動。</summary>
    [HttpPost("task")]
    public async Task<IActionResult> PushTaskUpdateAsync([FromBody] TaskUpdateViewModel payload)
    {
        await hubContext.Clients.All.SendAsync(AgentStatusHub.ReceiveTaskUpdate, payload);
        return Ok();
    }

    /// <summary>測試用端點：直接觸發 SignalR 推送，驗證 Hub → Browser 的管道是否正常。</summary>
    [HttpPost("test")]
    public async Task<IActionResult> TestPushAsync()
    {
        await hubContext.Clients.All.SendAsync(AgentStatusHub.ReceiveAgentStatus, new AgentStatusViewModel
        {
            AgentName        = "Dev",
            Status           = "running",
            CurrentTaskTitle = "【測試推送】SignalR 連線正常",
            LastUpdated      = DateTime.UtcNow
        });
        return Ok(new { message = "測試推送成功" });
    }
}
