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
/// Stage 78b：/task slash command + HandleTaskCommandAsync 整套砍（v4 path dead caller / CeoAgentService.ProcessAsync v4 唯一 Discord caller 砍後 0 caller）。
/// 承載 9 個 slash command 的定義與處理：
///   /reload-rules /status /new-session /mock
///   /pause /resume /stop-all /resume-all /queue
///
/// 注入 ButtonCallbackRouter 以共用 ShowProposalAsync / ShowDirectAgentConfirmAsync
/// / BuildCeoDecisionEmbed / BuildConfirmButtons 等 UI flow methods。
/// </summary>
public class SlashCommandRouter(
    IOptions<DiscordSettings> settings,
    IServiceProvider serviceProvider,
    RulesService rulesService,
    ConversationContextStore contextStore,
    AgentQueueControlService agentQueueControl,
    ILogger<SlashCommandRouter> logger)
{
    private readonly DiscordSettings _settings = settings.Value;
    // Stage 78b：/task 砍後 ctor 大幅瘦身 — buttonRouter / store / interactionService / appSettings / gitHubSettings 全 0 caller 砍。

    // ========== Command 定義 ==========

    public static ApplicationCommandProperties[] BuildCommandDefinitions()
    {
        // Stage 78b：/task slash command 砍 — v4 path dead caller（CeoAgentService.ProcessAsync 走 v4 直接 LLM mode / production 0 fire）。
        // v5.5 path 入口：Dashboard CEO chat（Victoria → PetraInbox flag forward only）+ Discord @mention chat（CommandHandler）。
        return new ApplicationCommandProperties[]
        {
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

    // Stage 78b：/task slash command + HandleTaskCommandAsync 整套砍 — v4 path dead caller
    //（呼叫 CeoAgentService.ProcessAsync v4 LLM mode / production 0 fire / v5.5 path 走 Dashboard chat + Discord @mention chat 入口）。

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
