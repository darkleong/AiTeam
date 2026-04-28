using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Discord.Routing;

/// <summary>
/// Stage 36：Discord slash command Router（從 CommandHandler 拆解而來）。
///
/// 承載 10 個 slash command 的定義與處理：
///   /task /reload-rules /status /new-session /mock
///   /pause /resume /stop-all /resume-all /queue
///
/// 注入 ButtonCallbackRouter 以共用 ShowProposalAsync / ShowDirectAgentConfirmAsync
/// / BuildCeoDecisionEmbed / BuildConfirmButtons 等 UI flow methods。
/// </summary>
public class SlashCommandRouter(
    IOptions<DiscordSettings> settings,
    IOptions<GitHubSettings> gitHubSettings,
    IServiceProvider serviceProvider,
    RulesService rulesService,
    AppSettingsService appSettings,
    ConversationContextStore contextStore,
    AgentQueueControlService agentQueueControl,
    InteractionService interactionService,
    PendingConfirmationStore store,
    ButtonCallbackRouter buttonRouter,
    ILogger<SlashCommandRouter> logger)
{
    private readonly DiscordSettings _settings = settings.Value;
    private readonly GitHubSettings _gitHubSettings = gitHubSettings.Value;

    // ========== Command 定義 ==========

    public static ApplicationCommandProperties[] BuildCommandDefinitions()
    {
        return new ApplicationCommandProperties[]
        {
            new SlashCommandBuilder()
                .WithName("task")
                .WithDescription("指派任務給 AI 團隊")
                .AddOption("project", ApplicationCommandOptionType.String, "專案名稱", isRequired: true)
                .AddOption("description", ApplicationCommandOptionType.String, "任務描述", isRequired: true)
                .AddOption("image", ApplicationCommandOptionType.Attachment, "（選用）附圖截圖", isRequired: false)
                .Build(),

            new SlashCommandBuilder()
                .WithName("reload-rules")
                .WithDescription("強制重新載入 Notion 規則（清除 Cache）")
                .Build(),

            new SlashCommandBuilder()
                .WithName("status")
                .WithDescription("查詢各 Agent 目前狀態")
                .Build(),

            new SlashCommandBuilder()
                .WithName("new-session")
                .WithDescription("清除 Victoria 的對話記憶，開始全新 Session（長期記憶不受影響）")
                .Build(),

            new SlashCommandBuilder()
                .WithName("pause")
                .WithDescription("暫停指定 Agent 的佇列消費（不中斷正在執行的任務）")
                .AddOption(BuildAgentChoiceOption("要暫停的 Agent"))
                .Build(),

            new SlashCommandBuilder()
                .WithName("resume")
                .WithDescription("恢復指定 Agent 的佇列消費")
                .AddOption(BuildAgentChoiceOption("要恢復的 Agent"))
                .Build(),

            new SlashCommandBuilder()
                .WithName("stop-all")
                .WithDescription("讓所有 Agent 完成手頭任務後停止，不再接新任務")
                .Build(),

            new SlashCommandBuilder()
                .WithName("resume-all")
                .WithDescription("恢復所有 Agent 的佇列消費（對應 /stop-all）")
                .Build(),

            new SlashCommandBuilder()
                .WithName("queue")
                .WithDescription("顯示 Agent 佇列狀態（排隊中 / 執行中的任務）")
                .AddOption("agent", ApplicationCommandOptionType.String, "（選用）指定 Agent 名稱，省略顯示全部", isRequired: false)
                .Build(),

            new SlashCommandBuilder()
                .WithName("mock")
                .WithDescription("【Mock Mode 限定】直接觸發指定工作流程，不呼叫 LLM，供流程測試使用")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("workflow")
                    .WithDescription("要測試的流程類型")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .AddChoice("新功能（new_feature）", "new_feature")
                    .AddChoice("新功能（含提案）", "new_feature_with_proposal")
                    .AddChoice("Bug 修復（bug_fix）", "bug_fix")
                    .AddChoice("技術改善（tech_improvement）", "tech_improvement")
                    .AddChoice("【失敗測試】Review Appeal（Vera 拒絕 → Cody 反駁）", "fail_review")
                    .AddChoice("【失敗測試】QA 失敗（Quinn 失敗 → Petra 路由）", "fail_qa")
                    .AddChoice("【失敗測試】Dev_plan Appeal（Petra 拒絕 → Cody 反駁）", "fail_dev_plan")
                    .AddChoice("【略過驗收】Vera 略過（無可審檔案 → skipped）", "review_skipped")
                    // Stage 43：FF 三十二 Orchestrator 行為層 4 場景
                    .AddChoice("【失敗測試/Stage43】DevPlan 重產成功（第 1 次失敗 → accept → 第 2 次成功）", "dev_plan_fail_retry")
                    .AddChoice("【失敗測試/Stage43】DevPlan 重產上限（連 2 次失敗 → escalate）", "dev_plan_fail_escalate")
                    .AddChoice("【失敗測試/Stage43】Dev 失敗中止（needs_intervention）", "dev_failed_intervention")
                    .AddChoice("【失敗測試/Stage43】QA fix loop 上限（連 N 輪失敗 → needs_intervention）", "qa_failed_fix_then_intervention"))
                .AddOption("title", ApplicationCommandOptionType.String, "（選用）模擬任務標題", isRequired: false)
                .Build(),
        };
    }

    private static SlashCommandOptionBuilder BuildAgentChoiceOption(string description)
        => new SlashCommandOptionBuilder()
            .WithName("agent")
            .WithDescription(description)
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true)
            .AddChoice("Dev（Cody）",           AgentNames.Dev)
            .AddChoice("Reviewer（Vera）",       AgentNames.Reviewer)
            .AddChoice("QA（Quinn）",            AgentNames.Qa)
            .AddChoice("Doc（Sage）",            AgentNames.Doc)
            .AddChoice("Requirements（Rosa）",   AgentNames.Requirements)
            .AddChoice("Designer（Demi）",       AgentNames.Designer)
            .AddChoice("Release（Rena）",        AgentNames.Release)
            .AddChoice("Ops（Maya）",            AgentNames.Ops);

    // ========== Dispatch ==========

    public async Task RouteAsync(SocketSlashCommand command)
    {
        logger.LogInformation("收到指令 /{CommandName} 來自 {User}", command.CommandName, command.User.Username);
        await command.DeferAsync();

        try
        {
            await (command.CommandName switch
            {
                "task"         => HandleTaskCommandAsync(command),
                "reload-rules" => HandleReloadRulesAsync(command),
                "status"       => HandleStatusAsync(command),
                "new-session"  => HandleNewSessionAsync(command),
                "mock"         => HandleMockCommandAsync(command),
                "pause"        => HandlePauseCommandAsync(command),
                "resume"       => HandleResumeCommandAsync(command),
                "stop-all"     => HandleStopAllCommandAsync(command),
                "resume-all"   => HandleResumeAllCommandAsync(command),
                "queue"        => HandleQueueCommandAsync(command),
                _              => command.FollowupAsync("未知指令")
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "處理指令 /{CommandName} 時發生錯誤", command.CommandName);
            await command.FollowupAsync("處理指令時發生錯誤，請查看 log。");
        }
    }

    // ========== /task ==========

    private async Task HandleTaskCommandAsync(SocketSlashCommand command)
    {
        var project     = command.Data.Options.First(o => o.Name == "project").Value.ToString()!;
        var description = command.Data.Options.First(o => o.Name == "description").Value.ToString()!;

        var images = new List<ImageAttachment>();
        var attachmentOption = command.Data.Options.FirstOrDefault(o => o.Name == "image");
        if (attachmentOption?.Value is IAttachment attachment &&
            attachment.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                using var http = new HttpClient();
                var bytes     = await http.GetByteArrayAsync(attachment.Url);
                var base64    = Convert.ToBase64String(bytes);
                var mediaType = DetectImageMediaType(bytes) ?? attachment.ContentType ?? "image/png";
                images.Add(new ImageAttachment(base64, mediaType));
                logger.LogInformation("附圖已下載並轉為 Base64（{ContentType}，{Bytes} bytes）",
                    mediaType, bytes.Length);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "附圖下載失敗，將忽略圖片繼續處理");
            }
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var ceoService = scope.ServiceProvider.GetRequiredService<CeoAgentService>();
        var agentRepo  = scope.ServiceProvider.GetRequiredService<AgentRepository>();

        var rules        = await rulesService.GetRulesAsync();
        var activeAgents = await agentRepo.GetActiveExecutorAgentsAsync();
        var agentList    = activeAgents
            .Select(a => new AgentDescriptor(a.Name, a.Description))
            .ToList();

        var ceoResponse = await ceoService.ProcessAsync(
            description, project, agentList, rules,
            images: images.Count > 0 ? images : null);

        if (!string.IsNullOrWhiteSpace(ceoResponse.TargetAgent) && ceoResponse.Action == "reply")
        {
            logger.LogWarning(
                "CEO 回傳 action=reply 但 target_agent={Agent}，強制修正為 delegate",
                ceoResponse.TargetAgent);
            ceoResponse.Action = "delegate";
        }

        if (ceoResponse.Action == "propose")
        {
            await command.FollowupAsync(ceoResponse.Reply);
            await buttonRouter.ShowProposalAsync(
                async (embed, comps) => await command.FollowupAsync(embed: embed, components: comps),
                ceoResponse, project, description,
                images: images.Count > 0 ? images : null,
                channelId: command.Channel.Id);
        }
        else if (ceoResponse.Action != "reply")
        {
            if (await appSettings.GetBoolAsync("SkipCeoConfirm"))
            {
                await buttonRouter.ShowDirectAgentConfirmAsync(
                    async (embed, comps) => await command.FollowupAsync(embed: embed, components: comps),
                    ceoResponse, project, description,
                    channelId: command.Channel.Id);
            }
            else
            {
                var confirmMessage = await command.FollowupAsync(
                    embed: ButtonCallbackRouter.BuildCeoDecisionEmbed(ceoResponse, project),
                    components: ButtonCallbackRouter.BuildConfirmButtons());

                store.RegisterConfirmation(confirmMessage.Id,
                    new PendingConfirmation(ceoResponse, project, description));

                // Stage 39 搭車修：補上 Task.Description（與 CommandHandler 兩處對稱）
                var interactionDescription = string.IsNullOrWhiteSpace(ceoResponse.Task?.Description)
                    ? (ceoResponse.Reply ?? description)
                    : $"{ceoResponse.Reply}\n\n---\n\n{ceoResponse.Task.Description}";

                _ = interactionService.CreateInteractionAsync(
                    "ceo_confirm",
                    title:                ceoResponse.Task?.Title ?? description,
                    description:          interactionDescription,
                    project:              project,
                    agentName:            ceoResponse.TargetAgent,
                    availableActionsJson: InteractionService.CeoConfirmActionsJson,
                    contextJson:          JsonSerializer.Serialize(new
                    {
                        channelId       = command.Channel.Id.ToString(),
                        ceoResponseJson = JsonSerializer.Serialize(ceoResponse),
                        project,
                        description
                    }),
                    discordMessageId: (decimal)confirmMessage.Id);
            }
        }
        else
        {
            await command.FollowupAsync(ceoResponse.Reply);
        }
    }

    // ========== /new-session ==========

    private async Task HandleNewSessionAsync(SocketSlashCommand command)
    {
        contextStore.Clear(command.Channel.Id);
        await command.FollowupAsync(
            "✅ Session 已重置。Victoria 的對話語境已清空，下次回應將以全新上下文開始。\n" +
            "（長期記憶不受影響，Victoria 仍記得你過去記錄的設計決策與偏好。）");
    }

    // ========== /mock ==========

    private async Task HandleMockCommandAsync(SocketSlashCommand command)
    {
        var workflowStr = command.Data.Options.First(o => o.Name == "workflow").Value.ToString()!;
        var customTitle = command.Data.Options.FirstOrDefault(o => o.Name == "title")?.Value?.ToString();

        var mockService = serviceProvider.GetRequiredService<MockScenarioService>();
        var (_, message) = await mockService.RunScenarioAsync(workflowStr, customTitle);
        await command.FollowupAsync(message);
    }

    // ========== /reload-rules ==========

    private async Task HandleReloadRulesAsync(SocketSlashCommand command)
    {
        rulesService.InvalidateCache();
        await command.FollowupAsync("規則 Cache 已清除，下次任務將重新從資料庫載入規則。");
    }

    // ========== /status ==========

    private async Task HandleStatusAsync(SocketSlashCommand command)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var agentRepo = scope.ServiceProvider.GetRequiredService<AgentRepository>();
        var agents    = await agentRepo.GetActiveExecutorAgentsAsync();

        var agentLines = agents.Count > 0
            ? string.Join("\n", agents.Select(a => $"• {a.Name} — {(a.IsActive ? "啟用" : "停用")}"))
            : "（尚未設定 Agent）";

        await command.FollowupAsync($"**Agent 狀態**\n{agentLines}");
    }

    // ========== /pause /resume /stop-all /resume-all ==========

    private async Task HandlePauseCommandAsync(SocketSlashCommand command)
    {
        var agent = command.Data.Options.First(o => o.Name == "agent").Value.ToString()!;
        var (_, message) = await agentQueueControl.PauseAgentAsync(agent);
        await command.FollowupAsync(message);
    }

    private async Task HandleResumeCommandAsync(SocketSlashCommand command)
    {
        var agent = command.Data.Options.First(o => o.Name == "agent").Value.ToString()!;
        var (_, message) = await agentQueueControl.ResumeAgentAsync(agent);
        await command.FollowupAsync(message);
    }

    private async Task HandleStopAllCommandAsync(SocketSlashCommand command)
    {
        var (_, message) = await agentQueueControl.StopAllAsync();
        await command.FollowupAsync(message);
    }

    private async Task HandleResumeAllCommandAsync(SocketSlashCommand command)
    {
        var (_, message) = await agentQueueControl.ResumeAllAsync();
        await command.FollowupAsync(message);
    }

    // ========== /queue ==========

    private async Task HandleQueueCommandAsync(SocketSlashCommand command)
    {
        var agentFilter = command.Data.Options.FirstOrDefault(o => o.Name == "agent")?.Value?.ToString();

        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var query = db.Set<TaskItem>()
            .AsNoTracking()
            .Where(t => t.QueueStatus == "queued" || t.QueueStatus == "processing");

        if (!string.IsNullOrWhiteSpace(agentFilter))
        {
            var matchAgents = agentFilter == AgentNames.Dev
                ? new[] { AgentNames.Dev, "Dev_plan" }
                : new[] { agentFilter };
            query = query.Where(t => matchAgents.Contains(t.AssignedAgent));
        }

        var tasks = await query.OrderBy(t => t.QueuedAt).ToListAsync();

        if (tasks.Count == 0)
        {
            var targetLabel = string.IsNullOrWhiteSpace(agentFilter) ? "所有 Agent" : agentFilter;
            await command.FollowupAsync($"✅ {targetLabel} 佇列為空，目前無待執行任務。");
            return;
        }

        var lines = tasks.Select(t =>
        {
            var waitTime = t.QueuedAt.HasValue
                ? $"（等待 {(DateTime.UtcNow - t.QueuedAt.Value).TotalMinutes:F0} 分鐘）"
                : "";
            var statusIcon = t.QueueStatus == "processing" ? "🔄" : "⏳";
            return $"{statusIcon} [{t.AssignedAgent}] {t.Title} {waitTime}";
        });

        var embed = new EmbedBuilder()
            .WithTitle("📋 Agent 佇列狀態")
            .WithDescription(string.Join("\n", lines))
            .WithColor(Color.Blue)
            .WithFooter($"共 {tasks.Count} 個任務（🔄 執行中 / ⏳ 排隊中）")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        await command.FollowupAsync(embed: embed);
    }

    // ========== helpers ==========

    internal static string? DetectImageMediaType(byte[] bytes)
    {
        if (bytes.Length < 4) return null;

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "image/png";

        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
            return "image/gif";

        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return "image/webp";

        return null;
    }
}
