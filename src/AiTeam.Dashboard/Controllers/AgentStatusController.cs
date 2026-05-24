using AiTeam.Dashboard.Services;
using AiTeam.Data.Hubs;
using AiTeam.Shared.Dtos;
using AiTeam.Shared.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
// 明確指定 Route 和 ApiController 使用 MVC 的版本，避免與 Blazor 的 RouteAttribute 衝突
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace AiTeam.Dashboard.Controllers;

/// <summary>
/// Internal API：Bot 透過此端點觸發 SignalR 推送，解決跨 Process 無法共享 IHubContext 的問題。
/// Bot → POST /internal/agent-status → Hub → Dashboard Browser
/// Stage 28a 新增 interaction push 端點 + Dashboard 回覆 API。
/// </summary>
[ApiController]
[Route("internal/agent-status")]
public class AgentStatusController(
    IHubContext<AgentStatusHub> hubContext,
    InteractionRespondService respondService) : ControllerBase
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

    /// <summary>Bot 呼叫此端點通知 Token 用量已更新，觸發 Token 監控頁即時重整。</summary>
    [HttpPost("token")]
    public async Task<IActionResult> PushTokenUpdateAsync()
    {
        await hubContext.Clients.All.SendAsync(AgentStatusHub.ReceiveTokenUpdate);
        return Ok();
    }

    /// <summary>Bot 呼叫此端點通知佇列狀態已變動（enqueue / dequeue / cancel / Agent 狀態變更）。</summary>
    [HttpPost("queue")]
    public async Task<IActionResult> PushQueueUpdateAsync()
    {
        await hubContext.Clients.All.SendAsync(AgentStatusHub.ReceiveQueueUpdate);
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

    // ─── Stage 28a：互動操作 ─────────────────────────────────────────────────

    /// <summary>
    /// Stage 28a：Bot 呼叫此端點通知互動狀態已變動（新互動進來 / 回覆後），觸發操作中心即時重整。
    /// </summary>
    [HttpPost("interaction")]
    public async Task<IActionResult> PushInteractionUpdateAsync()
    {
        await hubContext.Clients.All.SendAsync(AgentStatusHub.ReceiveInteractionUpdate);
        return Ok();
    }

    /// <summary>Stage 85 子項 1：Bot 呼叫此端點推送系統 alert（TokenGuard / dead-letter / paused timeout 三類） — Dashboard AlertToastSubscriber 訂閱後 MudSnackbar 彈出。</summary>
    [HttpPost("alert")]
    public async Task<IActionResult> BroadcastAlertAsync([FromBody] AlertEventDto dto)
    {
        await hubContext.Clients.All.SendAsync(AgentStatusHub.ReceiveAlertEvent, dto);
        return Ok();
    }

    /// <summary>
    /// Stage 28a：Dashboard 前端回覆互動。
    /// 使用樂觀鎖防止先到先贏衝突（另一通道已回覆則回傳 409）。
    /// </summary>
    [HttpPost("/api/interactions/{id}/respond")]
    public async Task<IActionResult> RespondToInteractionAsync(Guid id, [FromBody] InteractionResponseRequest request)
    {
        // Stage 28b：文字輸入類動作需驗證 Content 非空
        string[] textInputActions = ["propose_adjust", "kickoff_modify", "design_modify"];
        if (textInputActions.Contains(request.Action) && string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "此動作需要輸入修改意見" });

        // Delegate 給 InteractionRespondService，回覆邏輯只維護一份
        var responded = await respondService.RespondAsync(id, request.Action, request.Content);
        if (!responded)
            return Conflict(new { message = "此互動已被回覆，請重新整理頁面。" });

        return Ok();
    }
}
