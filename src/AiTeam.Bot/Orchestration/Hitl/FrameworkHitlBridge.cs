using System.Text.Json;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Bot.Workflows.Kickoff;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using Discord;
using Discord.WebSocket;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Orchestration.Hitl;

/// <summary>
/// Stage 51：framework HITL 試點 ↔ 既有 BossInteraction 樂觀鎖機制的橋接 service（v4 漸進遷移第三步）。
///
/// 為什麼是新 service 而非擴充既有 InteractionService（議題 E D2 拍板）：
///   - InteractionService 既有 method 已被 Stage 28a/28b 廣泛使用，避免新加 method 增加表面積
///   - FrameworkHitlBridge 專責 framework HITL ↔ BossInteraction 的橋接邏輯，未來 Stage 54 收尾真正切 HITL 時可獨立替換
///   - 「3 次再抽象」原則：Stage 51 是第 1 次，Stage 54+ 真實 wire 時是第 2-3 次
///
/// 三個 public method：
///   - TriggerMidInterruptFlagAsync：Bot Internal API 收 trigger → 寫 in-memory KickoffMidInterruptTriggerStore
///   - RequestMidInterruptInteractionAsync：router watch 到 RequestInfoEvent → 開 BossInteraction + Discord embed + 2 buttons
///   - HandleMidInterruptResponseAsync：Christ 回應後（透過 TaskGroupService.ProcessBossResponseAsync 或 CommandHandler 路由）
///       → ResumeStreamingAsync from latest checkpoint + SendResponseAsync + 跑到 WorkflowOutputEvent
///
/// Singleton（對齊 Stage 49/50 router 慣例）— ctor 注入 IServiceProvider，scoped service method 內 CreateAsyncScope 取。
///
/// 與 FrameworkKickoffRouter 循環依賴解：
///   - 兩者皆 Singleton，ctor 不互注入
///   - Bridge 需要 router.FinishKickoffAsync → 透過 IServiceProvider.GetRequiredService<FrameworkKickoffRouter>() lazy 取
///   - Router 需要 bridge.RequestMidInterruptInteractionAsync → 同樣 lazy 取
/// </summary>
public sealed class FrameworkHitlBridge(
    IServiceProvider serviceProvider,
    KickoffWorkflowFactory workflowFactory,
    KickoffCheckpointStore checkpointStore,
    KickoffMidInterruptTriggerStore triggerStore,
    DiscordSocketClient discordClient,
    IOptions<DiscordSettings> discordSettings,
    InteractionService interactionService,
    ILogger<FrameworkHitlBridge> logger)
{
    private readonly DiscordSettings _discord = discordSettings.Value;

    // ============================================================
    //  1. Trigger flag（Dashboard / Bot Internal API 入口）
    // ============================================================

    /// <summary>
    /// Stage 51：Bot Internal API（POST /internal/kickoff/trigger-mid-interrupt）入口。
    /// 寫 in-memory trigger store，下個 Petra Round 邊界 MidInterruptCheckExecutor 會看到並 emit RequestInfoEvent。
    ///
    /// 回傳 false：group 不在 framework Kickoff path（KickoffFrameworkStateJson = null），無意義 trigger 拒絕。
    /// </summary>
    public async Task<bool> TriggerMidInterruptFlagAsync(Guid groupId, CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var inFrameworkPath = await db.TaskGroups
            .Where(g => g.Id == groupId && g.KickoffFrameworkStateJson != null)
            .AnyAsync(ct);
        if (!inFrameworkPath)
        {
            logger.LogWarning(
                "[Stage51] TriggerMidInterruptFlagAsync: Group={Id} 不在 framework Kickoff path，trigger 拒絕",
                groupId);
            return false;
        }

        triggerStore.Set(groupId);
        logger.LogInformation("[Stage51] TriggerMidInterruptFlagAsync: Group={Id} trigger flag 已設置", groupId);
        return true;
    }

    // ============================================================
    //  2. RequestInfoEvent → BossInteraction（router 呼叫）
    // ============================================================

    /// <summary>
    /// Stage 51：router watch 到 RequestInfoEvent 時呼叫，開 BossInteraction + Discord embed + 2 buttons。
    /// requestId 寫進 ContextJson，Christ 回應後 HandleMidInterruptResponseAsync 從 BossInteraction 取回。
    /// </summary>
    public async Task<Guid?> RequestMidInterruptInteractionAsync(
        TaskGroup group,
        MidInterruptRequest request,
        ExternalRequest portRequest,
        CancellationToken ct = default)
    {
        var ceoChannel = FindChannel(_discord.Channels.CeoChannel);
        if (ceoChannel is null)
        {
            logger.LogError("[Stage51] RequestMidInterruptInteractionAsync: 找不到 CEO 頻道，無法上呈中途介入");
            return null;
        }

        var summaryPreview = request.PetraSummary.Length > 500
            ? request.PetraSummary[..500] + "...\n（完整內容請查看 Dashboard）"
            : request.PetraSummary;

        var embed = new EmbedBuilder()
            .WithTitle("✏️ Kickoff 中途介入")
            .WithColor(Color.Teal)
            .AddField("任務", group.Title)
            .AddField("當前輪次", $"Round {request.Round}")
            .AddField("Petra 整理", summaryPreview)
            .WithFooter("✏️ 套用修改 = 輸入指引給下輪會議；取消介入 = 不修改繼續會議")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        var buttons = new ComponentBuilder()
            .WithButton("✏️ 套用修改",  $"framework_kickoff_mid_interrupt_apply_{group.Id}",  ButtonStyle.Primary)
            .WithButton("取消介入",      $"framework_kickoff_mid_interrupt_cancel_{group.Id}", ButtonStyle.Secondary)
            .Build();

        var msg = await ceoChannel.SendMessageAsync(embed: embed, components: buttons);

        var interactionId = await interactionService.CreateInteractionAsync(
            "framework_kickoff_mid_interrupt",
            title:                $"Kickoff 中途介入：{group.Title}",
            description:          summaryPreview,
            project:              group.Project,
            agentName:            AgentNames.Pm,
            availableActionsJson: InteractionService.MidInterruptActionsJson,
            contextJson:          JsonSerializer.Serialize(new
            {
                channelId = ceoChannel.Id.ToString(),
                groupId   = group.Id.ToString(),
                requestId = portRequest.RequestId,  // RequestPort.ExternalRequest.RequestId
            }),
            discordMessageId: (decimal)msg.Id,
            taskGroupId:      group.Id);

        logger.LogInformation(
            "[Stage51] RequestMidInterruptInteractionAsync: BossInteraction 已開（Group={Id}，Round={Round}，requestId={Rid}，BiId={Bi}）",
            group.Id, request.Round, portRequest.RequestId, interactionId);

        return interactionId;
    }

    // ============================================================
    //  3. Christ response → ResumeStreamingAsync + SendResponseAsync
    // ============================================================

    /// <summary>
    /// Stage 51：Christ 回應後（TaskGroupService.ProcessBossResponseAsync 路由 case "framework_kickoff_mid_interrupt"）
    /// 觸發 workflow 從 latest checkpoint resume，把 ExternalResponse 送回 RequestPort，跑到 WorkflowOutputEvent。
    ///
    /// 冪等性（Aria 二次檢查 #2 必修）：開頭從 framework state 讀 MidInterruptRequestPending，
    /// false → 早返避免 InteractionProcessor crash 重啟後重複 LLM call。
    /// </summary>
    public async Task HandleMidInterruptResponseAsync(
        TaskGroup group, string action, string? content, CancellationToken ct = default)
    {
        await checkpointStore.LoadFromDbAsync(group.Id, ct);
        var sessionId = group.Id.ToString();
        var latest = checkpointStore.GetLatestCheckpoint(sessionId);
        if (latest is null)
        {
            logger.LogWarning(
                "[Stage51] HandleMidInterruptResponseAsync: latest checkpoint 不存在（Group={Id}），略過",
                group.Id);
            return;
        }

        // 冪等檢查：framework state JSON 內找 KickoffState.MidInterruptRequestPending
        var pendingFlag = await TryReadMidInterruptRequestPendingAsync(sessionId, latest, ct);
        if (!pendingFlag)
        {
            logger.LogWarning(
                "[Stage51] HandleMidInterruptResponseAsync: state.MidInterruptRequestPending=false（已被處理過？），早返避免重入（Group={Id}）",
                group.Id);
            return;
        }

        // 從 BossInteraction.ContextJson 取回 requestId
        await using var scope = serviceProvider.CreateAsyncScope();
        var bossRepo = scope.ServiceProvider.GetRequiredService<BossInteractionRepository>();
        var bi = await bossRepo.GetLatestForGroupByTypeAsync(
            group.Id, "framework_kickoff_mid_interrupt", ct);
        if (bi?.ContextJson is null)
        {
            logger.LogWarning(
                "[Stage51] HandleMidInterruptResponseAsync: BossInteraction 找不到（Group={Id}），略過",
                group.Id);
            return;
        }
        using var ctxDoc = JsonDocument.Parse(bi.ContextJson);
        if (!ctxDoc.RootElement.TryGetProperty("requestId", out var ridEl))
        {
            logger.LogError(
                "[Stage51] HandleMidInterruptResponseAsync: BossInteraction ContextJson 無 requestId（Group={Id}），略過",
                group.Id);
            return;
        }
        var requestId = ridEl.GetString() ?? "";

        var responseData = new MidInterruptResponseData(
            Apply:   action == "midinterrupt_apply",
            Content: action == "midinterrupt_apply" ? content : null);

        // Rehydrate workflow + ResumeStreamingAsync（spike F3 結論：跨 scope 用新 run instance from checkpoint）
        var workflow = workflowFactory.CreateKickoffWorkflow();
        var manager  = workflowFactory.CreateCheckpointManager();
        await using var run = await InProcessExecution.ResumeStreamingAsync(workflow, latest, manager, ct);

        logger.LogInformation(
            "[Stage51] HandleMidInterruptResponseAsync: ResumeStreamingAsync 啟動（Group={Id}，apply={Apply}，requestId={Rid}）",
            group.Id, responseData.Apply, requestId);

        KickoffLoopResult? loopResult = null;
        var sentResponse = false;
        await foreach (var ev in run.WatchStreamAsync().WithCancellation(ct))
        {
            if (ev is RequestInfoEvent requestEvt)
            {
                // ResumeStreamingAsync 啟動時 framework 自動 re-emit pending RequestInfoEvent（XML doc RestoreCheckpointAsync）
                // 第一個 RequestInfoEvent 應對應原 requestId（SendResponseAsync 觸發）
                // 連續介入：跑到 Round N+1 結束又被 trigger → 第二個 RequestInfoEvent 開新 BossInteraction
                if (!sentResponse && requestEvt.Request.RequestId == requestId)
                {
                    var externalResponse = requestEvt.Request.CreateResponse(responseData);
                    await run.SendResponseAsync(externalResponse);
                    sentResponse = true;
                    logger.LogInformation(
                        "[Stage51] SendResponseAsync 完成（Group={Id}，apply={Apply}）",
                        group.Id, responseData.Apply);
                    continue;
                }

                // 連續介入：開新 BossInteraction（後續輪次又 trigger）
                logger.LogInformation(
                    "[Stage51] HandleMidInterruptResponseAsync: 連續介入（Group={Id}，新 requestId={Rid}）",
                    group.Id, requestEvt.Request.RequestId);

                var freshGroup = await GetFreshGroupAsync(group.Id, ct);
                if (freshGroup is not null
                    && requestEvt.Request.TryGetDataAs<MidInterruptRequest>(out var midReq))
                {
                    await RequestMidInterruptInteractionAsync(freshGroup, midReq, requestEvt.Request, ct);
                }
                return;  // 第二次 yield，等下次 Christ 回應觸發本 method
            }
            if (ev is WorkflowOutputEvent outputEvt && outputEvt.Is<KickoffLoopResult>(out var r))
            {
                loopResult = r;
                logger.LogInformation(
                    "[Stage51] WorkflowOutputEvent（Group={Id}，decision={Decision}，rounds={Rounds}）",
                    group.Id, r.Decision, r.TotalRounds);
            }
            else if (ev is WorkflowErrorEvent errEvt)
            {
                logger.LogError(
                    "[Stage51] WorkflowErrorEvent: Group={Id}, exception={Exception}",
                    group.Id, errEvt.Exception?.ToString() ?? "(null)");
            }
            else if (ev is ExecutorFailedEvent failedEvt)
            {
                logger.LogError(
                    "[Stage51] ExecutorFailedEvent: executorId={ExecutorId}, data={Data}",
                    failedEvt.ExecutorId, failedEvt.Data?.ToString() ?? "(null)");
            }
        }

        if (loopResult is null)
        {
            logger.LogWarning(
                "[Stage51] HandleMidInterruptResponseAsync: workflow 未產出 KickoffLoopResult（Group={Id}），略過 Finish",
                group.Id);
            return;
        }

        // 委派 router 完成 DB 寫入 + Discord 確認 embed + cleanup workspace + mark task done
        // 透過 service locator 取 router（避免 ctor 循環依賴）
        var router = serviceProvider.GetRequiredService<AiTeam.Bot.Orchestration.Meeting.FrameworkKickoffRouter>();
        await router.FinishKickoffAsync(group.Id, loopResult, ct);
    }

    // ============================================================
    //  Helpers
    // ============================================================

    private async Task<bool> TryReadMidInterruptRequestPendingAsync(
        string sessionId,
        Microsoft.Agents.AI.Workflows.CheckpointInfo latest,
        CancellationToken ct)
    {
        try
        {
            var ckptValue = await checkpointStore.RetrieveCheckpointAsync(sessionId, latest);
            // framework checkpoint JsonElement 含 ScopeKey → state JSON 字典結構，
            // 我們直接尋找 KickoffStateScope/singleton 對應 KickoffState 序列化內容並讀 midInterruptRequestPending 欄位。
            // 採寬鬆 lookup：在整個 JSON tree 找第一個 "midInterruptRequestPending" 屬性即接受
            // （fail-open 設計：若未找到該欄位視為 false → 早返保護不動）
            return ScanForBoolProperty(ckptValue, "midInterruptRequestPending");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "[Stage51] TryReadMidInterruptRequestPendingAsync 失敗（Group sessionId={Id}），視同 pending=true 繼續嘗試",
                sessionId);
            return true;  // fail-safe：解析失敗時讓主流程嘗試 resume，由後續 framework 行為決定
        }
    }

    private static bool ScanForBoolProperty(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.NameEquals(propertyName)
                        && (prop.Value.ValueKind == JsonValueKind.True
                            || prop.Value.ValueKind == JsonValueKind.False))
                    {
                        return prop.Value.GetBoolean();
                    }
                    if (ScanForBoolProperty(prop.Value, propertyName))
                        return true;
                }
                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (ScanForBoolProperty(item, propertyName))
                        return true;
                }
                return false;
            default:
                return false;
        }
    }

    private async Task<TaskGroup?> GetFreshGroupAsync(Guid groupId, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        return await taskRepo.GetGroupByIdAsync(groupId, ct);
    }

    private IMessageChannel? FindChannel(string channelName)
    {
        if (!ulong.TryParse(_discord.GuildId, out var guildId)) return null;
        return discordClient.GetGuild(guildId)
            ?.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }
}
