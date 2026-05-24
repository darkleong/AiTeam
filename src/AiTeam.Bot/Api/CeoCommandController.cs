using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord;
using AiTeam.Bot.Services;
using AiTeam.Data.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Api;

/// <summary>
/// Stage 29-5：Dashboard 下達指令給 Victoria 的 Bot 端端點。
/// Dashboard → POST /internal/ceo/command → Victoria → BossInteraction
///
/// Fire-and-forget 模式：驗證 + 存 BossCommandLog 後立即回 202 Accepted；
/// Victoria 處理（耗時數十秒至數分鐘）於背景 Task 中進行，結果透過 BossInteraction + SignalR
/// 推送至 Dashboard 操作中心，避免 Dashboard HttpClient 逾時。
/// </summary>
[ApiController]
[Route("internal/ceo")]
public class CeoCommandController(
    AppSettingsService appSettings,
    CommandHandler commandHandler,
    RulesService rulesService,
    IServiceScopeFactory scopeFactory,
    IOptions<AgentSettings> agentSettings,
    ILogger<CeoCommandController> logger) : ControllerBase
{
    private readonly string _apiKey = agentSettings.Value.InternalApiKey;

    /// <summary>圖片序列化為 jsonb 時使用 camelCase，對齊 BossCommandLog.Images 註解的 {base64Data, mediaType} 格式。</summary>
    private static readonly JsonSerializerOptions ImagesJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [HttpPost("command")]
    public async Task<IActionResult> SendCommandAsync(
        [FromBody] DashboardCeoCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized()) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "指令文字不可為空" });

        // 讀 AppSettings：CEO 頻道 ID + Christ Discord User ID
        var channelIdStr = await appSettings.GetAsync("CeoDefaultChannelId", cancellationToken);
        var christUserId = await appSettings.GetAsync("ChristDiscordUserId", cancellationToken);

        if (string.IsNullOrWhiteSpace(channelIdStr))
            return BadRequest(new { error = "尚未設定 CEO 指令預設頻道，請先至「系統設定」頁面配置。" });
        if (string.IsNullOrWhiteSpace(christUserId))
            return BadRequest(new { error = "尚未設定 Christ Discord User ID，請先至「系統設定」頁面配置。" });
        if (!ulong.TryParse(channelIdStr, out var ceoChannelId))
            return BadRequest(new { error = "CEO 指令預設頻道 ID 格式錯誤，請重新設定。" });

        // Stage 79：v5.5 image flow 補完 — API 端 verify 後備層（Dashboard MudFileUpload UI 阻止 + 此處 API 後備守 + Repository / CeoAgentService 終守 — 三層守）
        if (request.Images is { Count: > 0 })
        {
            var workflowResolver = HttpContext.RequestServices.GetRequiredService<WorkflowSettingsResolver>();
            var maxCount = await workflowResolver.GetMaxAttachmentsPerTaskAsync(cancellationToken);
            var maxSizeMB = await workflowResolver.GetMaxAttachmentSizeMBAsync(cancellationToken);
            var maxSizeBytes = (long)maxSizeMB * 1024 * 1024;

            if (request.Images.Count > maxCount)
                return BadRequest(new { error = $"最多 {maxCount} 張附圖（收 {request.Images.Count} 張）" });

            for (var i = 0; i < request.Images.Count; i++)
            {
                var sizeBytes = (request.Images[i].Base64Data.Length * 3L) / 4;
                if (sizeBytes > maxSizeBytes)
                    return BadRequest(new { error = $"第 {i + 1} 張附圖超 {maxSizeMB} MB 上限（含 {sizeBytes / 1024 / 1024} MB）" });
            }
        }

        // 轉換圖片（stream-json stdin 用）
        var images = request.Images?
            .Select(i => new ImageAttachment(i.Base64Data, i.MediaType))
            .ToList();

        // 儲存 BossCommandLog（同步做完，確認寫入成功後才回 202；失敗讓 Dashboard 能看到錯誤）
        Guid commandLogId;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var logRepo = scope.ServiceProvider.GetRequiredService<BossCommandLogRepository>();
            var commandLog = new Data.BossCommandLog
            {
                Text   = request.Text,
                Images = request.Images?.Count > 0
                    ? JsonSerializer.Serialize(request.Images, ImagesJsonOptions)
                    : null,
                Source = "dashboard"
            };
            logRepo.Add(commandLog);
            await logRepo.SaveAsync(cancellationToken);
            commandLogId = commandLog.Id;
        }

        logger.LogInformation(
            "Dashboard 指令已接收（logId={Id}，images={Count}），背景交給 Victoria 處理",
            commandLogId, images?.Count ?? 0);

        // 背景處理 Victoria 呼叫 + Discord/BossInteraction 路由。
        // 不能用 HTTP 請求的 CancellationToken（response 一回就會被 cancel），改用 None。
        _ = Task.Run(() => ProcessVictoriaInBackgroundAsync(
            request.Text, christUserId, ceoChannelId, images, commandLogId));

        return Accepted(new
        {
            success = true,
            message = "指令已送達，Victoria 的回應將出現在操作中心。"
        });
    }

    /// <summary>
    /// 背景執行 Victoria 分析，完成後將回應透過 CommandHandler 路由至 Discord + BossInteraction。
    /// 例外只 log，不向外拋（Task.Run 無人等候 Task.Wait）。
    /// </summary>
    private async Task ProcessVictoriaInBackgroundAsync(
        string text,
        string christUserId,
        ulong ceoChannelId,
        IReadOnlyList<ImageAttachment>? images,
        Guid commandLogId)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var ceoService    = scope.ServiceProvider.GetRequiredService<CeoAgentService>();
            var taskRepo      = scope.ServiceProvider.GetRequiredService<TaskRepository>();
            var db            = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();

            var rules             = await rulesService.GetRulesAsync("CEO");
            // Stage 87 A2：AgentRepository.GetActiveExecutorAgentsAsync 砍 → 改 inline talents 表 query（對齊 DashboardAgentService.GetAllAgentStatusesAsync Stage 83 修法 / Name != "Victoria" 排除 CEO 自身）
            var activeAgents      = await db.Talents
                .AsNoTracking()
                .Where(t => t.IsActive && t.Name != "Victoria")
                .OrderBy(t => t.Name)
                .Select(t => new { t.Name, t.Description })
                .ToListAsync();
            var agentList         = activeAgents.Select(a => new AgentDescriptor(a.Name, a.Description)).ToList();
            var availableProjects = await taskRepo.GetActiveProjectNamesAsync();

            var ceoResponse = await ceoService.ProcessWithClaudeCodeAsync(
                text,
                christUserId,
                projectName:       availableProjects.FirstOrDefault() ?? "",
                agentList:         agentList,
                rules:             rules,
                cancellationToken: CancellationToken.None,
                images:            images?.Count > 0 ? images : null,
                availableProjects: availableProjects);

            // 更新 BossCommandLog 的 CeoResponseRaw
            var existing = await db.BossCommandLogs.FindAsync(commandLogId);
            if (existing is not null)
            {
                existing.CeoResponseRaw = JsonSerializer.Serialize(ceoResponse);
                await db.SaveChangesAsync();
            }

            // 路由至 Discord + BossInteraction
            await commandHandler.HandleCeoResponseFromDashboardAsync(
                ceoResponse, text, ceoChannelId, images);

            logger.LogInformation(
                "Dashboard 指令背景處理完成（logId={Id}，action={Action}）",
                commandLogId, ceoResponse.Action);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Dashboard 指令背景處理失敗（logId={Id}）", commandLogId);
        }
    }

    private bool IsAuthorized()
    {
        if (string.IsNullOrEmpty(_apiKey)) return false;
        Request.Headers.TryGetValue("X-Api-Key", out var key);
        return key == _apiKey;
    }
}

public record DashboardCeoCommandRequest(
    string Text,
    List<ImageDto>? Images = null);

public record ImageDto(string Base64Data, string MediaType);
