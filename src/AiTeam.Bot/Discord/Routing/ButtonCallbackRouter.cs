using AiTeam.Bot.Agents;
using AiTeam.Bot.Services;
using AiTeam.Shared.Constants;
using Discord;
using Discord.WebSocket;

namespace AiTeam.Bot.Discord.Routing;

/// <summary>
/// Discord 按鈕回調 Router（v5.5 active routing only）。
///
/// Stage 36 拆出 / Stage 78b 路線 C 折衷 v4 routing 砍 75% / Stage 78c 路線 A 一次過 v4 Pipeline framework 整套砍後 ButtonCallbackRouter 縮為純 v5.5 routing：
///   - confirm_yes / confirm_no 通用 confirmation routing（v5.5 PetraInbox flag forward only 後 confirm_yes Stage 68 短路 ack）
///   - BuildCeoDecisionEmbed / BuildConfirmButtons 共用 UI Flow（CommandHandler caller）
///
/// 砍範圍（Stage 78b + 78c 累積）：
///   - v4 routing prefix：kickoff_ / design_ / framework_kickoff_mid_interrupt_
///   - v4 routing case：propose_yes / propose_no / propose_adjust / cancel_yes
///   - v4 method：HandleKickoffButtonAsync / HandleDesignButtonAsync / HandleFrameworkKickoffMidInterruptAsync /
///                ExecuteProposalApprovedAsync / ShowProposalAsync / ShowDirectAgentConfirmAsync /
///                HandleCancelRequestAsync / HandleCancelSelectionAsync / ExecuteAgentTaskAsync / HandleExecYesAsync
///   - v4 helper：BuildProposalEmbed / BuildProposalConfirmButtons / BuildAgentPlanEmbed / BuildCancelConfirmEmbed /
///                BuildEscalateButtons / ResolveWorkflowType / SupersedePriorFailedTasks / GetAgentChannelName / FindChannel
///   - ctor 大幅瘦身：client / settings / gitHubSettings / serviceProvider / taskGroupService 5 dep 砍
/// </summary>
public class ButtonCallbackRouter(
    InteractionService interactionService,
    PendingConfirmationStore store,
    ILogger<ButtonCallbackRouter> logger)
{
    // ========== Public entry ==========

    public async Task RouteAsync(SocketMessageComponent interaction)
    {
        // Stage 28a：先到先贏 — 嘗試標記 Discord 回覆。若 Dashboard 已先回覆，early return
        var discordMsgId = (decimal)interaction.Message.Id;
        var isFirstToRespond = await interactionService.SyncDiscordResponseAsync(discordMsgId, interaction.Data.CustomId);
        if (!isFirstToRespond)
        {
            await interaction.RespondAsync("✅ 已在 Dashboard 回覆，流程繼續中。", ephemeral: true);
            return;
        }

        if (!store.TryGetConfirmation(interaction.Message.Id, out var pending))
        {
            await interaction.RespondAsync("此確認已過期或不存在。", ephemeral: true);
            return;
        }
        store.RemoveConfirmation(interaction.Message.Id);

        await HandleGenericButtonAsync(interaction, pending);
    }

    // ========== 通用按鈕（v5.5 active：confirm_yes / confirm_no） ==========

    private async Task HandleGenericButtonAsync(SocketMessageComponent interaction, PendingConfirmation pending)
    {
        var id = interaction.Data.CustomId;

        if (id == "confirm_yes")
        {
            await HandleConfirmYesAsync(interaction, pending);
        }
        else // confirm_no
        {
            await interaction.RespondAsync("❌ 已取消。");
        }
    }

    private async Task HandleConfirmYesAsync(SocketMessageComponent interaction, PendingConfirmation pending)
    {
        await interaction.DeferAsync();

        // Stage 68：v5/v5.5 path 收尾 — Petra 已動態調度完成 / 不需建立 TaskItem 或 fire exec_confirm 卡
        if (pending.CeoResponse.Action == CeoResponseActions.PetraV5Dispatched)
        {
            logger.LogInformation(
                "confirm_yes：v5.5 path Petra 已完成（Action={Action}），跳過 TaskItem + exec_confirm fire",
                pending.CeoResponse.Action);
            await interaction.FollowupAsync($"✅ Petra 已動態調度完成 — {pending.Description}");
            return;
        }

        // Stage 78c：v4 path body 整套砍後 defensive log + ack（v4 dead caller / production 0 fire）
        logger.LogWarning(
            "confirm_yes：非 v5.5 path Action={Action}（v4 dead caller path / v4 Pipeline framework Stage 78c 整套砍）— TargetAgent={Agent} Project={Project}",
            pending.CeoResponse.Action, pending.CeoResponse.TargetAgent, pending.Project);
        await interaction.FollowupAsync("⚠️ v4 path 已砍（Stage 78c）— 此操作目前不支援，請改走 Dashboard v5.5 path 重新派任務。");
    }

    // ========== Embed 與按鈕建構（共用 UI Flow / CommandHandler caller） ==========

    internal static string Truncate(string? value, int max = 1024)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        return value.Length <= max ? value : value[..(max - 3)] + "…";
    }

    internal static Embed BuildCeoDecisionEmbed(CeoResponse response, string project)
    {
        var builder = new EmbedBuilder()
            .WithTitle("📋 CEO 決策 — 請確認")
            .WithColor(Color.Blue)
            .AddField("回應", Truncate(response.Reply))
            .AddField("動作", Truncate(response.Action), inline: true)
            .AddField("負責 Agent", response.TargetAgent ?? "—", inline: true)
            .AddField("專案", string.IsNullOrWhiteSpace(project) ? "—" : project, inline: true);

        if (response.Task is not null)
        {
            builder
                .AddField("任務標題", Truncate(response.Task.Title))
                .AddField("優先度", string.IsNullOrWhiteSpace(response.Task.Priority) ? "—" : response.Task.Priority, inline: true)
                .AddField("描述", Truncate(response.Task.Description));
        }

        return builder.Build();
    }

    internal static MessageComponent BuildConfirmButtons(string yesId = "confirm_yes", string noId = "confirm_no")
        => new ComponentBuilder()
            .WithButton("✅ 確認", yesId, ButtonStyle.Success)
            .WithButton("❌ 取消", noId,  ButtonStyle.Danger)
            .Build();
}
