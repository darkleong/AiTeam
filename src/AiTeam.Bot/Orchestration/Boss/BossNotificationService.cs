using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Shared.Constants;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Orchestration.Boss;

/// <summary>
/// Stage 59：BossInteraction fire 集中管理（從 TaskGroupService B 區段拆出）。
///
/// 5 NotifyBoss helpers — 對齊 fire-and-forget pattern，由 Pipeline Stage Executor / Framework Router 直接呼叫：
///   ① NotifyBossMergeAsync                       — merge_notify (Stage 25+)
///   ② NotifyBossDevFailedInterventionAsync       — dev_failed_intervention (Stage 43-B)
///   ③ NotifyBossReviewerFixLoopLimitAsync        — reviewer_fix_loop_limit (Stage 57-FF 五十二)
///   ④ NotifyBossAgentApiFailureAsync             — agent_api_failure_intervention (Stage 58-FF 五十三)
///   ⑤ NotifyBossInterventionAsync                — intervention (generic)
///
/// 不依賴其他子 service — 只用 Discord / DiscordSettings / InteractionService（既有 Stage 28a+ 注入鏈）。
/// MarkGroupDoneOrInterventionAsync 留 TaskGroupService 主檔（守門邏輯跨 B+E，避免 BossNotification → EpicChain 反向耦合）。
/// </summary>
public class BossNotificationService(
    DiscordSocketClient discordClient,
    IOptions<DiscordSettings> discordSettings,
    InteractionService interactionService,
    ILogger<BossNotificationService> logger)
{
    private readonly DiscordSettings _discord = discordSettings.Value;

    public async Task NotifyBossMergeAsync(Data.TaskGroup group, CancellationToken cancellationToken)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null) return;

        var prLink = string.IsNullOrWhiteSpace(group.DevPrUrl)
            ? "（無 PR 連結）"
            : group.DevPrUrl;

        await ceoChannel.SendMessageAsync(
            $"✅ **{group.Title}** — 全流程完成！\n" +
            $"PR：{prLink}（含 code + tests + docs）\n" +
            $"請確認後即可合併 👆");

        logger.LogInformation("TaskGroup {Id} 通知老闆可以 merge PR", group.Id);

        // Stage 55B 議題 3 = 3A 拍板：merge_notify 仍 fire-and-forget — 純通知 ack 性質（Christ 確認 PR 可合併），
        // routing 收益為 0；改 yield-resume 對行為無實質改變但需 NotifyMergeStage dual handler 重構（規模 vs 收益不對等）
        _ = interactionService.CreateInteractionAsync(
            "merge_notify",
            title:                $"全流程完成：{group.Title}",
            description:          $"PR：{prLink}（含 code + tests + docs），請確認後合併。",
            project:              group.Project,
            agentName:            null,
            availableActionsJson: InteractionService.NotifyActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId = ceoChannel.Id.ToString(),
                groupId   = group.Id.ToString(),
                prUrl     = group.DevPrUrl ?? ""
            }),
            taskGroupId: group.Id);
    }

    /// <summary>
    /// Stage 43-B：Dev / Dev_fix 失敗 → 中止 fix loop，通知老闆介入。
    /// 與 NotifyBossInterventionAsync（fix loop 超限走 intervention type）區分用 dev_failed_intervention 細類。
    /// </summary>
    public async Task NotifyBossDevFailedInterventionAsync(
        Data.TaskGroup group, bool isFixLoop, string failSummary, CancellationToken cancellationToken)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null) return;

        var phaseLabel = isFixLoop ? "Dev_fix" : "Dev";
        await ceoChannel.SendMessageAsync(
            $"⚠️ **{group.Title}** — {phaseLabel} 階段失敗，已中止流程。\n" +
            $"原因：{(failSummary.Length > 300 ? failSummary[..300] + "..." : failSummary)}\n" +
            $"PR：{group.DevPrUrl ?? "（無）"}");

        logger.LogWarning("TaskGroup {Id} {Phase} failed，中止 fix loop（Reason={R}）",
            group.Id, phaseLabel, failSummary);

        _ = interactionService.CreateInteractionAsync(
            "dev_failed_intervention",
            title:                $"{phaseLabel} 失敗：{group.Title}",
            description:          $"{phaseLabel} 階段失敗，已中止流程，需要您決定後續處理。原因：{(failSummary.Length > 500 ? failSummary[..500] + "..." : failSummary)}",
            project:              group.Project,
            agentName:            AgentNames.Dev,
            availableActionsJson: InteractionService.DevFailedInterventionActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId = ceoChannel.Id.ToString(),
                groupId   = group.Id.ToString(),
                phase     = phaseLabel,
                isFixLoop
            }),
            taskGroupId: group.Id);
    }

    /// <summary>Stage 57-FF 五十二：Reviewer fix loop ×3 達 limit → Pipeline yield-resume 開 type-specific BossInteraction。
    /// 對齊 NotifyBossDevFailedInterventionAsync 既有 fire-and-forget pattern + Pipeline path 接管 routing（reviewer_fix_loop_limit 第 6 routing）。</summary>
    public async Task NotifyBossReviewerFixLoopLimitAsync(
        Data.TaskGroup group, AgentExecutionResult? petraResult, CancellationToken cancellationToken)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null) return;

        var petraReason = petraResult?.Summary ?? "（無 Petra 仲裁理由）";
        await ceoChannel.SendMessageAsync(
            $"⚠️ **{group.Title}** — Vera 連 {group.FixIteration} 輪 Critical 仍有問題，需要決策：標完成 / 跳過 QA / 終止。\n" +
            $"PR：{group.DevPrUrl ?? "（無）"}\n" +
            $"Petra 最後仲裁：{petraReason}");

        logger.LogWarning("[Stage57] TaskGroup {Id} Reviewer fix loop {Count} 輪達 limit，fire reviewer_fix_loop_limit",
            group.Id, group.FixIteration);

        _ = interactionService.CreateInteractionAsync(
            "reviewer_fix_loop_limit",
            title:                $"Vera fix loop 達上限：{group.Title}",
            description:          $"Vera 連續 {group.FixIteration} 輪審查仍有 Critical 問題。Petra 最後仲裁：{petraReason}",
            project:              group.Project,
            agentName:            null,
            availableActionsJson: InteractionService.ReviewerFixLoopLimitActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId    = ceoChannel.Id.ToString(),
                groupId      = group.Id.ToString(),
                prUrl        = group.DevPrUrl ?? "",
                fixIteration = group.FixIteration
            }),
            taskGroupId: group.Id);
    }

    /// <summary>Stage 58-FF 五十三：Agent API 失敗（餘額不足 / 401）→ Pipeline yield-resume 開 type-specific BossInteraction。
    /// 統一 1 helper（議題 3 拍板）— agentName 參數區分 context（Dev / Reviewer / QA / Doc），4 stage executor marker check 後共用 fire-and-forget pattern。
    /// 對齊 Stage 57 NotifyBossReviewerFixLoopLimitAsync 既有 fire-and-forget pattern + Pipeline path 接管 routing（agent_api_failure_intervention 第 7 routing）。</summary>
    public async Task NotifyBossAgentApiFailureAsync(
        Data.TaskGroup group, string agentName, string failSummary, CancellationToken cancellationToken)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null) return;

        var summarySnippet = failSummary.Length > 300 ? failSummary[..300] + "..." : failSummary;
        await ceoChannel.SendMessageAsync(
            $"⚠️ **{group.Title}** — {agentName} LLM API 呼叫失敗（可能餘額不足 / 401），需要決策：略過 / 重試 / 終止。\n" +
            $"原因摘要：{summarySnippet}");

        logger.LogWarning("[Stage58] TaskGroup {Id} agent={Agent} API failure，fire agent_api_failure_intervention",
            group.Id, agentName);

        _ = interactionService.CreateInteractionAsync(
            "agent_api_failure_intervention",
            title:                $"{agentName} API 失敗：{group.Title}",
            description:          $"{agentName} 的 LLM API 呼叫失敗（可能是餘額不足或 401）。原因：{(failSummary.Length > 500 ? failSummary[..500] + "..." : failSummary)}",
            project:              group.Project,
            agentName:            agentName,
            availableActionsJson: InteractionService.AgentApiFailureActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId = ceoChannel.Id.ToString(),
                groupId   = group.Id.ToString(),
                agent     = agentName,
                prUrl     = group.DevPrUrl ?? ""
            }),
            taskGroupId: group.Id);
    }

    public async Task NotifyBossInterventionAsync(Data.TaskGroup group, CancellationToken cancellationToken)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null) return;

        await ceoChannel.SendMessageAsync(
            $"⚠️ **{group.Title}** — Vera 在 {group.FixIteration} 次修復後仍發現 🔴 問題，需要您介入處理。\n" +
            $"PR：{group.DevPrUrl ?? "（無）"}");

        logger.LogWarning("TaskGroup {Id} 修復次數超限（{Count} 次），升級給老闆", group.Id, group.FixIteration);

        // Stage 55B 議題 3 = 3A 拍板：intervention 仍 fire-and-forget — 純通知 ack 性質，routing 收益為 0；
        // 改 yield-resume 需 dedicated InterventionAckExecutor + 8 stage AddEdge wiring（規模 vs 收益不對等）。
        // SetInterventionAndYieldAsync helper（在 8 個 Pipeline Stage Executor 各自實作）：DB UpdateStatus + call 本 method + YieldOutput Completed=true
        _ = interactionService.CreateInteractionAsync(
            "intervention",
            title:                $"需要介入：{group.Title}",
            description:          $"Vera 在 {group.FixIteration} 次修復後仍發現問題，需要您介入處理。",
            project:              group.Project,
            agentName:            null,
            availableActionsJson: InteractionService.NotifyActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId    = ceoChannel.Id.ToString(),
                groupId      = group.Id.ToString(),
                prUrl        = group.DevPrUrl ?? "",
                fixIteration = group.FixIteration
            }),
            taskGroupId: group.Id);
    }

    private IMessageChannel? FindChannel(string channelName)
    {
        if (!ulong.TryParse(_discord.GuildId, out var guildId)) return null;
        return discordClient.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }
}
