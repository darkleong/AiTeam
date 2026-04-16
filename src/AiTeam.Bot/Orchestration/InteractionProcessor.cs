using System.Text.Json;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data.Repositories;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Orchestration;

/// <summary>
/// Stage 28a：Dashboard 回覆消費器。
/// BackgroundService，每 3 秒輪詢 BossInteraction 表中 Dashboard 已回覆但 Bot 尚未消費的記錄，
/// 呼叫 TaskGroupService.ProcessBossResponseAsync 執行對應的流程動作，並發送 Discord 同步訊息。
/// </summary>
public class InteractionProcessor(
    IServiceProvider serviceProvider,
    TaskGroupService taskGroupService,
    DashboardPushService pushService,
    DiscordSocketClient discordClient,
    IOptions<DiscordSettings> discordSettings,
    ILogger<InteractionProcessor> logger) : BackgroundService
{
    private readonly DiscordSettings _discord = discordSettings.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(3000, stoppingToken);
            await ProcessPendingDashboardResponsesAsync(stoppingToken);
        }
    }

    private async Task ProcessPendingDashboardResponsesAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var repo              = scope.ServiceProvider.GetRequiredService<BossInteractionRepository>();
            var interactions      = await repo.GetDashboardResponsesAsync(ct);

            foreach (var interaction in interactions)
            {
                try
                {
                    logger.LogInformation(
                        "InteractionProcessor：處理 {Type}+{Action}（Id={Id}）",
                        interaction.InteractionType, interaction.ResponseAction, interaction.Id);

                    await taskGroupService.ProcessBossResponseAsync(
                        interaction.InteractionType,
                        interaction.ResponseAction!,
                        interaction.ContextJson,
                        ct);

                    // Discord 同步訊息
                    await SendDiscordSyncMessageAsync(interaction);

                    // 標記已消費
                    await using var markScope = serviceProvider.CreateAsyncScope();
                    var markRepo              = markScope.ServiceProvider.GetRequiredService<BossInteractionRepository>();
                    await markRepo.MarkProcessedByBotAsync(interaction.Id, ct);

                    // 推送 SignalR 更新
                    _ = pushService.PushInteractionUpdateAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "InteractionProcessor：處理 {Type} 失敗（Id={Id}），標記已處理避免無限重試",
                        interaction.InteractionType, interaction.Id);

                    // 標記已處理，避免壞資料（如 ContextJson 格式異常）無限重試
                    try
                    {
                        await using var errScope = serviceProvider.CreateAsyncScope();
                        var errRepo              = errScope.ServiceProvider.GetRequiredService<BossInteractionRepository>();
                        await errRepo.MarkProcessedByBotAsync(interaction.Id, ct);
                    }
                    catch (Exception markEx)
                    {
                        logger.LogError(markEx,
                            "InteractionProcessor：標記 ProcessedByBot 也失敗（Id={Id}）", interaction.Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "InteractionProcessor：輪詢過程中發生例外");
        }
    }

    private async Task SendDiscordSyncMessageAsync(AiTeam.Data.BossInteraction interaction)
    {
        if (interaction.ContextJson is null) return;

        try
        {
            using var doc    = JsonDocument.Parse(interaction.ContextJson);
            var channelIdStr = doc.RootElement.TryGetProperty("channelId", out var c)
                ? c.GetString() : null;

            if (!ulong.TryParse(channelIdStr, out var channelId)) return;
            if (!ulong.TryParse(_discord.GuildId, out var guildId)) return;

            var channel = discordClient.GetGuild(guildId)?.GetTextChannel(channelId);
            if (channel is null) return;

            var actionLabel = GetActionLabel(interaction.InteractionType, interaction.ResponseAction ?? "");
            await channel.SendMessageAsync($"📋 Christ 已在 Dashboard 回覆：{actionLabel}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "InteractionProcessor：Discord 同步訊息發送失敗（Id={Id}）", interaction.Id);
        }
    }

    private static string GetActionLabel(string type, string action) => (type, action) switch
    {
        ("ceo_confirm",      "confirm_yes")      => "確認派工 ✅",
        ("ceo_confirm",      "confirm_no")       => "取消 ❌",
        ("exec_confirm",     "exec_yes")         => "執行 ✅",
        ("exec_confirm",     "exec_no")          => "取消 ❌",
        ("proposal",         "propose_yes")      => "核准提案 ✅",
        ("proposal",         "propose_no")       => "駁回提案 ❌",
        ("kickoff",          "kickoff_continue") => "繼續 Kickoff ▶️",
        ("kickoff",          "kickoff_stop")     => "停止 Kickoff ⏹️",
        ("kickoff",          "kickoff_restart")  => "重開 Kickoff 🔄",
        ("design",           "design_continue")  => "繼續設計 ▶️",
        ("design",           "design_stop")      => "停止設計 ⏹️",
        ("devplan_escalate", "devplan_skip")     => "跳過 Dev_plan，直接開發 ⏭️",
        ("devplan_escalate", "devplan_abort")    => "放棄任務 ❌",
        _                                        => $"{type} → {action}"
    };
}
