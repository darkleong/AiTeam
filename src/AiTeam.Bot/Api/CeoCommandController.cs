using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord;
using AiTeam.Bot.Services;
using AiTeam.Data.Repositories;
using Discord.WebSocket;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Api;

/// <summary>
/// Stage 29-5：Dashboard 下達指令給 Victoria 的 Bot 端端點。
/// Dashboard → POST /internal/ceo/command → Victoria → BossInteraction
/// </summary>
[ApiController]
[Route("internal/ceo")]
public class CeoCommandController(
    AppSettingsService appSettings,
    CeoAgentService ceoService,
    CommandHandler commandHandler,
    RulesService rulesService,
    IServiceScopeFactory scopeFactory,
    IOptions<AgentSettings> agentSettings,
    ILogger<CeoCommandController> logger) : ControllerBase
{
    private readonly string _apiKey = agentSettings.Value.InternalApiKey;

    /// <summary>
    /// 接收來自 Dashboard 的指令，轉交 Victoria 處理後路由至 Discord + BossInteraction。
    /// Body：{ text, images?: [{base64Data, mediaType}][] }
    /// </summary>
    [HttpPost("command")]
    public async Task<IActionResult> SendCommandAsync(
        [FromBody] DashboardCeoCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized()) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "指令文字不可為空" });

        // 讀 AppSettings：CEO 頻道 ID + Christ Discord User ID
        var channelIdStr  = await appSettings.GetAsync("CeoDefaultChannelId",  cancellationToken);
        var christUserId  = await appSettings.GetAsync("ChristDiscordUserId",   cancellationToken);

        if (string.IsNullOrWhiteSpace(channelIdStr))
            return BadRequest(new { error = "尚未設定 CEO 指令預設頻道，請先至「系統設定」頁面配置。" });
        if (string.IsNullOrWhiteSpace(christUserId))
            return BadRequest(new { error = "尚未設定 Christ Discord User ID，請先至「系統設定」頁面配置。" });
        if (!ulong.TryParse(channelIdStr, out var ceoChannelId))
            return BadRequest(new { error = "CEO 指令預設頻道 ID 格式錯誤，請重新設定。" });

        // 轉換圖片
        var images = request.Images?
            .Select(i => new ImageAttachment(i.Base64Data, i.MediaType))
            .ToList() as IReadOnlyList<ImageAttachment>;

        // 取規則與 Agent 清單（與 Discord 路徑一致）
        await using var scope    = scopeFactory.CreateAsyncScope();
        var agentRepo            = scope.ServiceProvider.GetRequiredService<Data.Repositories.AgentRepository>();
        var taskRepo             = scope.ServiceProvider.GetRequiredService<Data.Repositories.TaskRepository>();
        var logRepo              = scope.ServiceProvider.GetRequiredService<BossCommandLogRepository>();

        var rules            = await rulesService.GetRulesAsync("CEO");
        var activeAgents     = await agentRepo.GetActiveExecutorAgentsAsync();
        var agentList        = activeAgents.Select(a => new AgentDescriptor(a.Name, a.Description)).ToList();
        var availableProjects = await taskRepo.GetActiveProjectNamesAsync();

        // 儲存指令記錄
        var commandLog = new Data.BossCommandLog
        {
            Text   = request.Text,
            Images = request.Images?.Count > 0
                ? JsonSerializer.Serialize(request.Images)
                : null,
            Source = "dashboard"
        };
        logRepo.Add(commandLog);
        await logRepo.SaveAsync(cancellationToken);

        logger.LogInformation(
            "Dashboard 下達指令（logId={Id}，images={Count}）：{Text}",
            commandLog.Id, request.Images?.Count ?? 0, request.Text);

        // 呼叫 Victoria（Claude Code 模式，延續 christUserId 的 session）
        CeoResponse ceoResponse;
        try
        {
            ceoResponse = await ceoService.ProcessWithClaudeCodeAsync(
                request.Text,
                christUserId,
                projectName:       availableProjects.FirstOrDefault() ?? "",
                agentList:         agentList,
                rules:             rules,
                cancellationToken: cancellationToken,
                images:            images?.Count > 0 ? images : null,
                availableProjects: availableProjects);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dashboard 指令：Victoria 處理失敗");
            return StatusCode(500, new { error = "Victoria 處理指令時發生錯誤，請查看 Bot log。" });
        }

        // 持久化 Victoria 原始回應供追溯
        commandLog.CeoResponseRaw = JsonSerializer.Serialize(ceoResponse);
        await logRepo.SaveAsync(cancellationToken);

        // 路由至 Discord + BossInteraction（與 Discord 路徑使用相同邏輯）
        _ = commandHandler.HandleCeoResponseFromDashboardAsync(
            ceoResponse, request.Text, ceoChannelId, images);

        return Ok(new
        {
            success = true,
            action  = ceoResponse.Action,
            reply   = ceoResponse.Reply
        });
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
