using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Discord;
using AiTeam.Bot.Orchestration;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Dtos;
using AiTeam.Shared.ViewModels;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Services;

/// <summary>
/// Stage 32：/mock 情境觸發邏輯的 shared service，供 Discord 指令與 Dashboard Internal API 共用。
/// 原本集中在 CommandHandler.HandleMockCommandAsync / HandleMockProposalFlowAsync，
/// 抽離後同一份邏輯可被 Dashboard 卡片觸發，實現 FF 十五「Discord 與 Dashboard 功能平等」。
/// </summary>
public class MockScenarioService(
    AppSettingsService appSettings,
    TaskGroupService taskGroupService,
    DiscordSocketClient discordClient,
    IOptions<DiscordSettings> discordSettings,
    IOptions<GitHubSettings> gitHubSettings,
    IServiceProvider serviceProvider,
    ILogger<MockScenarioService> logger)
{
    private readonly DiscordSettings _discord   = discordSettings.Value;
    private readonly GitHubSettings  _gitHub    = gitHubSettings.Value;

    /// <summary>
    /// 執行指定情境（new_feature / bug_fix / tech_improvement / new_feature_with_proposal /
    /// fail_review / fail_qa / fail_dev_plan），建立 TaskGroup 並觸發流程。
    /// </summary>
    /// <returns>(ok, message)：ok 表示是否成功啟動；message 供呼叫端顯示（Discord Followup / Dashboard Snackbar）。</returns>
    public async Task<(bool ok, string message)> RunScenarioAsync(
        string scenario,
        string? customTitle = null,
        string? customProject = null,
        CancellationToken ct = default)
    {
        // MockMode 守門
        if (!await appSettings.GetBoolAsync("MockMode", false, ct))
        {
            return (false,
                "⚠️ `/mock` 僅在 **Mock Mode 啟用**時有效。請至 Dashboard → 系統設定 啟用 Mock Mode 後再試。");
        }

        // 失敗測試情境：設定 FailScenario 狀態（與原 CommandHandler 行為一致）
        if (scenario == "fail_review")
            MockClaudeCodeService.FailScenario = "review_appeal";
        else if (scenario == "fail_qa")
            MockClaudeCodeService.FailScenario = "qa_failure";
        else if (scenario == "fail_dev_plan")
            MockClaudeCodeService.FailScenario = "dev_plan_appeal";

        var (workflowType, workflowLabel, initialStep) = scenario switch
        {
            "bug_fix"                   => (WorkflowType.BugFix,          "Bug 修復",              "Dev"),
            "tech_improvement"          => (WorkflowType.TechImprovement, "技術改善",              "Dev_plan"),
            "new_feature_with_proposal" => (WorkflowType.NewFeature,      "新功能（含提案）",       "Dev_plan"),
            "fail_review"               => (WorkflowType.NewFeature,      "失敗測試-ReviewAppeal", "Dev"),
            "fail_qa"                   => (WorkflowType.NewFeature,      "失敗測試-QA失敗",       "Dev"),
            "fail_dev_plan"             => (WorkflowType.NewFeature,      "失敗測試-DevPlanAppeal", "Dev_plan"),
            _                           => (WorkflowType.NewFeature,      "新功能",                "Dev_plan")
        };

        var title   = string.IsNullOrWhiteSpace(customTitle)
            ? $"[MOCK] 模擬{workflowLabel}任務（{DateTime.Now:HH:mm:ss}）"
            : customTitle!;
        var project = string.IsNullOrWhiteSpace(customProject) ? _gitHub.DefaultRepo : customProject!;

        logger.LogInformation("[MockMode] MockScenarioService：scenario={Scenario}，title={Title}", scenario, title);

        if (scenario == "new_feature_with_proposal")
        {
            return await RunProposalFlowAsync(title, project, workflowType, ct);
        }

        // 一般流程：建立 TaskGroup，直接觸發起始步驟
        var group = await taskGroupService.CreateGroupAsync(
            title, project, workflowType,
            issueUrlsJson: "[\"https://github.com/mock/repo/issues/1\"]",
            uiSpecContent: "[MOCK] 模擬 UI 規格，供 Mock Mode 測試使用。",
            cancellationToken: ct);

        _ = Task.Run(() => taskGroupService.FireStepsAsync(group, [new WorkflowStep(initialStep)]));

        var emoji = scenario switch
        {
            "bug_fix"          => "🐛",
            "tech_improvement" => "🔧",
            _                  => "✨"
        };

        return (true,
            $"{emoji} **[MOCK] {workflowLabel}流程已啟動**\n" +
            $"任務：`{title}`\n" +
            $"起始步驟：`{initialStep}` → 後續由 Orchestrator 自動推進\n" +
            $"請至 Dashboard → 任務中心 觀察流程進度，所有輸出將標記 `[MOCK]`。");
    }

    /// <summary>
    /// new_feature_with_proposal 情境：建立 CEO 提案 Embed + 登記 pending confirmation + 建立 BossInteraction。
    /// 與 Discord 觸發完全等價（共用 CommandHandler._pendingConfirmations 字典）。
    /// </summary>
    private async Task<(bool ok, string message)> RunProposalFlowAsync(
        string title,
        string project,
        WorkflowType workflowType,
        CancellationToken ct)
    {
        var ceoChannel = FindCeoChannel();
        if (ceoChannel is null)
            return (false, "❌ 找不到 CEO 頻道，無法發送提案書。請確認 DiscordSettings.Channels.CeoChannel 設定正確。");

        // 1. 建立 TaskGroup（無 issueUrls/uiSpec，設計階段再填入）
        var group = await taskGroupService.CreateGroupAsync(title, project, workflowType, cancellationToken: ct);

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo    = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<DashboardPushService>();

        var projectId = string.IsNullOrWhiteSpace(project)
            ? (Guid?)null
            : await taskRepo.GetProjectIdByNameAsync(project);

        // 2. 建立 CEO 主任務（pending，掛 GroupId）
        var ceoTask = new TaskItem
        {
            Title         = title,
            Description   = "[MOCK] 模擬新功能提案流程",
            TriggeredBy   = "Discord",
            AssignedAgent = "CEO",
            Status        = "pending",
            GroupId       = group.Id,
            ProjectId     = projectId,
        };
        taskRepo.Add(ceoTask);
        await taskRepo.SaveAsync(ct);
        var ceoTaskId = ceoTask.Id;

        await pushService.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = ceoTask.Id,
            GroupId   = group.Id,
            Title     = ceoTask.Title,
            AgentName = ceoTask.AssignedAgent,
            Status    = "pending"
        });

        // 3. 傳送與真實流程相同的提案 Embed + 確認按鈕
        const string description = "[MOCK] 模擬新功能需求，供 Mock Mode 測試流程使用。";
        var proposalEmbed = CommandHandler.BuildProposalEmbed(title, description);
        var proposalMsg   = await ceoChannel.SendMessageAsync(embed: proposalEmbed, components: CommandHandler.BuildProposalConfirmButtons());

        // 4. 登記 _pendingConfirmations（延遲解析 CommandHandler 避免循環依賴）
        var commandHandler = serviceProvider.GetRequiredService<CommandHandler>();
        commandHandler.RegisterProposalConfirmation(proposalMsg.Id, ceoTaskId, project, description);

        // 5. 建立 BossInteraction，讓 Dashboard 操作中心顯示待處理卡片
        await using var interactionScope = serviceProvider.CreateAsyncScope();
        var interactionSvc = interactionScope.ServiceProvider.GetRequiredService<InteractionService>();
        _ = interactionSvc.CreateInteractionAsync(
            "proposal",
            title:                title,
            description:          description,
            project:              project,
            agentName:            null,
            availableActionsJson: InteractionService.ProposalActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId   = ceoChannel.Id.ToString(),
                taskId      = ceoTaskId.ToString(),
                project,
                description
            }),
            discordMessageId: (decimal)proposalMsg.Id,
            taskItemId:       ceoTaskId);

        return (true,
            $"📋 **[MOCK] 新功能（含提案）流程已啟動**\n" +
            $"任務：`{title}`\n" +
            $"請至 <#{ceoChannel.Id}> 或 Dashboard 操作中心點擊確認，流程才會繼續。");
    }

    /// <summary>依 DiscordSettings.Channels.CeoChannel（名稱）從 Guild 取得頻道。</summary>
    private IMessageChannel? FindCeoChannel()
    {
        if (!ulong.TryParse(_discord.GuildId, out var guildId)) return null;
        return discordClient.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == _discord.Channels.CeoChannel);
    }
}
