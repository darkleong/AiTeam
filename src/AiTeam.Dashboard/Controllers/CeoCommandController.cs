using AiTeam.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace AiTeam.Dashboard.Controllers;

/// <summary>
/// Stage 29-5：Dashboard 前端「快速下達指令」卡片的後端接收端點。
/// 接收 multipart/form-data（文字 + 圖片），驗證後轉發給 DashboardCeoCommandService。
/// </summary>
[ApiController]
public class CeoCommandController(
    DashboardCeoCommandService ceoCommandService,
    ILogger<CeoCommandController> logger) : ControllerBase
{
    private const int MaxImages    = 5;
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5MB

    [HttpPost("/api/ceo/command")]
    [RequestSizeLimit(30 * 1024 * 1024)] // 5 張 × 5MB + 緩衝
    public async Task<IActionResult> SendCommandAsync(
        [FromForm] string text,
        [FromForm] List<IFormFile>? images,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new { error = "指令文字不可為空" });

        if (images is { Count: > MaxImages })
            return BadRequest(new { error = $"圖片最多 {MaxImages} 張" });

        // 驗證並讀取圖片
        var imageDtos = new List<ImageUploadDto>();
        if (images is { Count: > 0 })
        {
            foreach (var file in images)
            {
                if (file.Length > MaxImageBytes)
                    return BadRequest(new { error = $"圖片「{file.FileName}」超過 5MB 限制" });

                if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { error = $"「{file.FileName}」不是有效的圖片格式" });

                using var stream = file.OpenReadStream();
                var bytes = new byte[file.Length];
                await stream.ReadExactlyAsync(bytes, cancellationToken);
                imageDtos.Add(new ImageUploadDto(Convert.ToBase64String(bytes), file.ContentType));
            }
        }

        logger.LogInformation(
            "Dashboard CEO 指令接收（text={Len} 字，images={Count} 張）",
            text.Length, imageDtos.Count);

        var result = await ceoCommandService.SendCommandAsync(
            text,
            imageDtos.Count > 0 ? imageDtos : null,
            cancellationToken);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { success = true, action = result.Action, reply = result.Reply });
    }
}
