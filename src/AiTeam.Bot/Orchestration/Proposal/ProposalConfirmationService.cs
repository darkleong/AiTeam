using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using AiTeam.Shared.ViewModels;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Orchestration.Proposal;

/// <summary>
/// Stage 36：Dashboard 路徑的 CEO/Exec/Proposal 確認流程（從 TaskGroupService 拆解）。
///
/// 本服務對應 Stage 28a/28b 的 Dashboard 操作中心回覆路徑，
/// 由 InteractionProcessor → TaskGroupService.ProcessBossResponseAsync 分派進來。
/// </summary>
public class ProposalConfirmationService(
    IServiceProvider serviceProvider,
    DiscordSocketClient discordClient,
    IOptions<DiscordSettings> discordSettings,
    AgentQueueService agentQueueService,
    InteractionService interactionService,
    ILogger<ProposalConfirmationService> logger)
{
    private readonly DiscordSettings _discord = discordSettings.Value;

    public async Task ProcessCeoConfirmAsync(string contextJson, CancellationToken ct)
    {
        using var doc         = JsonDocument.Parse(contextJson);
        var root              = doc.RootElement;
        var ceoResponseJson   = root.GetProperty("ceoResponseJson").GetString() ?? "{}";
        var project           = root.TryGetProperty("project",     out var p) ? p.GetString() : null;
        var description       = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        var channelIdStr      = root.TryGetProperty("channelId",   out var c) ? c.GetString() : null;

        var ceoResponse = JsonSerializer.Deserialize<CeoResponse>(ceoResponseJson);
        if (ceoResponse is null)
        {
            logger.LogWarning("ProcessCeoConfirmAsync：無法解析 ceoResponseJson");
            return;
        }

        // Stage 68：v5/v5.5 path 收尾（Trial_v12 揭 stale exec_confirm 卡議題）— Petra 已動態調度完成，
        // 不需建立 TaskItem（無下個 worker）也不 fire exec_confirm 卡。
        if (ceoResponse.Action == CeoResponseActions.PetraV5Dispatched)
        {
            logger.LogInformation("ProcessCeoConfirmAsync：v5 path Petra 已完成（Action={Action}），跳過 exec_confirm fire", ceoResponse.Action);
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var projectId = string.IsNullOrWhiteSpace(project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(project);

        var task = new TaskItem
        {
            Title         = ceoResponse.Task?.Title ?? description,
            Description   = ceoResponse.Task?.Description,
            TriggeredBy   = "Dashboard",
            AssignedAgent = ceoResponse.TargetAgent ?? "CEO",
            Status        = "pending",
            ProjectId     = projectId,
        };
        taskRepo.Add(task);
        await taskRepo.SaveAsync(ct);

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = task.Id,
            Title     = task.Title,
            AgentName = task.AssignedAgent,
            Status    = task.Status
        });

        // Stage 55B 範圍邊界：exec_confirm 仍 fire-and-forget — 對齊 ButtonCallbackRouter exec_confirm 處理路徑（Dashboard CEO confirm 入口）
        _ = interactionService.CreateInteractionAsync(
            "exec_confirm",
            title:                ceoResponse.Task?.Title ?? description,
            description:          $"即將由 {ceoResponse.TargetAgent} 執行",
            project:              project,
            agentName:            ceoResponse.TargetAgent,
            availableActionsJson: InteractionService.ExecConfirmActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId       = channelIdStr,
                ceoResponseJson = ceoResponseJson,
                project         = project,
                description     = description,
                taskId          = task.Id.ToString()
            }),
            taskItemId: task.Id);
    }

    public async Task ProcessExecConfirmAsync(string contextJson, CancellationToken ct)
    {
        using var doc         = JsonDocument.Parse(contextJson);
        var root              = doc.RootElement;
        var ceoResponseJson   = root.GetProperty("ceoResponseJson").GetString() ?? "{}";
        var project           = root.TryGetProperty("project",     out var p) ? p.GetString() ?? "" : "";
        var description       = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        var taskIdStr         = root.TryGetProperty("taskId",      out var t) ? t.GetString() : null;

        var ceoResponse = JsonSerializer.Deserialize<CeoResponse>(ceoResponseJson);
        if (ceoResponse is null)
        {
            logger.LogWarning("ProcessExecConfirmAsync：無法解析 ceoResponseJson");
            return;
        }

        var wfType = ResolveWorkflowTypeInternal(ceoResponse);

        var tgs = serviceProvider.GetRequiredService<TaskGroupService>();

        if (wfType == WorkflowType.TechImprovement)
        {
            var group = await tgs.CreateGroupAsync(
                ceoResponse.Task?.Title ?? description, project, WorkflowType.TechImprovement, cancellationToken: ct);
            await tgs.FireStepsAsync(group, [new WorkflowStep("Dev_plan")], ct);
            return;
        }

        if (!Guid.TryParse(taskIdStr, out var taskId))
        {
            logger.LogWarning("ProcessExecConfirmAsync：無效的 taskId ({Id})", taskIdStr);
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var task              = await taskRepo.GetByIdAsync(taskId);
        if (task is null)
        {
            logger.LogWarning("ProcessExecConfirmAsync：找不到 TaskItem ({Id})", taskId);
            return;
        }

        await agentQueueService.EnqueueAsync(task, ct);
    }

    public async Task ProcessProposalApprovedAsync(string contextJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(contextJson);
        var root      = doc.RootElement;
        var taskIdStr = root.TryGetProperty("taskId",  out var t) ? t.GetString() : null;
        var project   = root.TryGetProperty("project", out var p) ? p.GetString() ?? "" : "";

        if (!Guid.TryParse(taskIdStr, out var taskId))
        {
            logger.LogWarning("ProcessProposalApprovedAsync：無效的 taskId ({Id})", taskIdStr);
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var task = await taskRepo.GetByIdAsync(taskId);
        if (task is null)
        {
            logger.LogWarning("ProcessProposalApprovedAsync：找不到 TaskItem ({Id})", taskId);
            return;
        }

        taskRepo.UpdateStatus(task, "done");
        await taskRepo.SaveAsync(ct);
        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = task.Id,
            Title     = task.Title,
            AgentName = task.AssignedAgent,
            Status    = "done"
        });

        var tgs = serviceProvider.GetRequiredService<TaskGroupService>();

        // Stage 29 hotfix（零-B）：若 task 已有 GroupId，沿用，避免重複建立 TaskGroup
        TaskGroup group;
        if (task.GroupId.HasValue && task.GroupId != Guid.Empty)
        {
            var existingGroup = await taskRepo.GetGroupByIdAsync(task.GroupId.Value, ct);
            if (existingGroup is null)
                throw new InvalidOperationException($"ProcessProposalApprovedAsync：找不到 TaskGroup（Id={task.GroupId}）");
            group = existingGroup;
        }
        else
        {
            group = await tgs.CreateGroupAsync(task.Title, project, WorkflowType.NewFeature, cancellationToken: ct);
        }

        await tgs.FireStepsAsync(group, [new WorkflowStep(AgentNames.Kickoff)], ct);
    }

    /// <summary>Stage 28b：Dashboard 提案調整路徑。</summary>
    public async Task ProcessProposalAdjustAsync(string contextJson, string? adjustmentText, CancellationToken ct)
    {
        using var doc    = JsonDocument.Parse(contextJson);
        var root         = doc.RootElement;
        var taskIdStr    = root.TryGetProperty("taskId",      out var t) ? t.GetString() : null;
        var project      = root.TryGetProperty("project",     out var p) ? p.GetString() ?? "" : "";
        var channelIdStr = root.TryGetProperty("channelId",   out var c) ? c.GetString() : null;

        if (!Guid.TryParse(taskIdStr, out var taskId))
        {
            logger.LogWarning("ProcessProposalAdjustAsync：無效的 taskId ({Id})", taskIdStr);
            return;
        }
        if (!ulong.TryParse(channelIdStr, out var channelId))
        {
            logger.LogWarning("ProcessProposalAdjustAsync：無效的 channelId ({Id})", channelIdStr);
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var task = await taskRepo.GetByIdAsync(taskId, ct);
        if (task is null)
        {
            logger.LogWarning("ProcessProposalAdjustAsync：找不到 TaskItem ({Id})", taskId);
            return;
        }

        var adjustNote  = string.IsNullOrWhiteSpace(adjustmentText) ? "（未填寫修改意見）" : adjustmentText;
        var updatedDesc = string.IsNullOrWhiteSpace(task.Description)
            ? adjustNote
            : $"{task.Description}\n\n【老闆調整意見】{adjustNote}";
        task.Description = updatedDesc;
        await taskRepo.SaveAsync(ct);

        if (!ulong.TryParse(_discord.GuildId, out var guildId))
        {
            logger.LogWarning("ProcessProposalAdjustAsync：無效的 GuildId");
            return;
        }
        var channel = discordClient.GetGuild(guildId)?.GetTextChannel(channelId);
        if (channel is null)
        {
            logger.LogWarning("ProcessProposalAdjustAsync：找不到 Discord 頻道（channelId={Id}）", channelId);
            return;
        }

        var descPreview = updatedDesc.Length > 800 ? updatedDesc[..800] + "\n…（已截斷）" : updatedDesc;
        var embed = new EmbedBuilder()
            .WithTitle($"📋 提案書：{task.Title}")
            .WithColor(Color.Blue)
            .AddField("需求描述", descPreview)
            .WithFooter("✅ 核准後將進入 Kick-off 會議")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        var buttons = new ComponentBuilder()
            .WithButton("✅ 核准，開始開發", InteractionService.ProposeYes,    ButtonStyle.Success)
            .WithButton("✏️ 需要調整",       InteractionService.ProposeAdjust, ButtonStyle.Primary)
            .WithButton("❌ 取消",           InteractionService.ProposeNo,     ButtonStyle.Danger)
            .Build();

        var msg = await channel.SendMessageAsync(embed: embed, components: buttons);

        var cmdHandler = serviceProvider.GetRequiredService<CommandHandler>();
        cmdHandler.RegisterProposalConfirmation(msg.Id, taskId, project, updatedDesc);

        // Stage 55B 議題 1 = 1A 拍板：proposal type 仍 fire-and-forget — Pipeline pre-stage 整合留 Stage 56
        // 理由：ProposalConfirmationService.ProcessProposalApprovedAsync 流程 group 在 proposal 核准後才 CreateGroupAsync，
        // Pipeline ProposalStage pre-stage 整合需重構 group lifecycle（規模超出 Stage 55B / Aria spike F6 揭露）
        _ = interactionService.CreateInteractionAsync(
            "proposal",
            title:                task.Title,
            description:          updatedDesc,
            project:              project,
            agentName:            null,
            availableActionsJson: InteractionService.ProposalActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId   = channelId.ToString(),
                taskId      = taskId.ToString(),
                project,
                description = updatedDesc
            }),
            discordMessageId: (decimal)msg.Id,
            taskItemId:       taskId);

        logger.LogInformation("ProcessProposalAdjustAsync：完成（TaskId={Id}，已發送新提案）", taskId);
    }

    /// <summary>Dashboard exec_no 取消 TaskItem。</summary>
    public async Task CancelTaskItemFromContextAsync(string contextJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(contextJson);
        var taskIdStr = doc.RootElement.TryGetProperty("taskId", out var t) ? t.GetString() : null;
        if (!Guid.TryParse(taskIdStr, out var taskId)) return;

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo          = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var task              = await taskRepo.GetByIdAsync(taskId);
        if (task is null) return;

        taskRepo.UpdateStatus(task, "cancelled");
        await taskRepo.SaveAsync(ct);

        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();
        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = task.Id,
            Title     = task.Title,
            AgentName = task.AssignedAgent,
            Status    = "cancelled"
        });
    }

    private static WorkflowType? ResolveWorkflowTypeInternal(CeoResponse ceoResponse)
    {
        if (ceoResponse.TargetAgent is AgentNames.Release or AgentNames.Ops or AgentNames.Doc)
            return null;

        return ceoResponse.WorkflowType switch
        {
            "tech_improvement" => WorkflowType.TechImprovement,
            _                  => WorkflowType.BugFix
        };
    }
}
