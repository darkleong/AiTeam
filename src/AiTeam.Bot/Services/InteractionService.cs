using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AiTeam.Bot.Services;

/// <summary>
/// Stage 28a：BossInteraction 寫入與 Discord 回覆同步服務。
/// Singleton — 內部每次操作均建立 Scope 取得 Scoped Repository，避免 captive dependency。
/// 所有例外均只 log Warning，不影響主流程（pure additive）。
/// </summary>
public class InteractionService(
    IServiceProvider serviceProvider,
    DashboardPushService pushService,
    ILogger<InteractionService> logger)
{
    // ─── AvailableActionsJson 常數 ───────────────────────────────────────────

    public const string CeoConfirmActionsJson =
        """[{"id":"confirm_yes","label":"確認派工","color":"success"},{"id":"confirm_no","label":"取消","color":"error"}]""";

    public const string ExecConfirmActionsJson =
        """[{"id":"exec_yes","label":"執行","color":"success"},{"id":"exec_no","label":"取消","color":"error"}]""";

    // Stage 28b：提案 Discord 按鈕 CustomId 常數（TaskGroupService 建立 ComponentBuilder 時共用）
    public const string ProposeYes    = "propose_yes";
    public const string ProposeAdjust = "propose_adjust";
    public const string ProposeNo     = "propose_no";

    public const string ProposalActionsJson =
        """[{"id":"propose_yes","label":"核准提案","color":"success","requiresInput":false},{"id":"propose_adjust","label":"需要調整 ✏️","color":"info","requiresInput":true},{"id":"propose_no","label":"駁回","color":"error","requiresInput":false}]""";

    public const string KickoffActionsJson =
        """[{"id":"kickoff_continue","label":"繼續","color":"success","requiresInput":false},{"id":"kickoff_modify","label":"需要修改 ✏️","color":"info","requiresInput":true},{"id":"kickoff_stop","label":"停止","color":"error","requiresInput":false},{"id":"kickoff_restart","label":"重開會議","color":"warning","requiresInput":false}]""";

    public const string DesignActionsJson =
        """[{"id":"design_continue","label":"繼續","color":"success","requiresInput":false},{"id":"design_modify","label":"需要修改 ✏️","color":"info","requiresInput":true},{"id":"design_stop","label":"停止","color":"error","requiresInput":false}]""";

    public const string DevPlanEscalateActionsJson =
        """[{"id":"devplan_skip","label":"跳過審閱，直接開發","color":"warning"},{"id":"devplan_abort","label":"放棄任務","color":"error"}]""";

    public const string EmptyActionsJson = "[]";

    // ─── 建立 BossInteraction ─────────────────────────────────────────────────

    /// <summary>
    /// 建立 BossInteraction 並推送 SignalR 更新。
    /// 失敗只 log Warning，回傳 null 代表未寫入（不影響主流程）。
    /// </summary>
    public async Task<Guid?> CreateInteractionAsync(
        string   interactionType,
        string   title,
        string   description,
        string?  project,
        string?  agentName,
        string   availableActionsJson,
        string?  contextJson          = null,
        decimal? discordMessageId     = null,
        Guid?    taskGroupId          = null,
        Guid?    taskItemId           = null)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<BossInteractionRepository>();

            var interaction = new BossInteraction
            {
                InteractionType      = interactionType,
                Title                = title.Length > 200 ? title[..200] : title,
                Description          = description.Length > 2000 ? description[..2000] : description,
                Project              = project,
                AgentName            = agentName,
                AvailableActionsJson = availableActionsJson,
                ContextJson          = contextJson,
                DiscordMessageId     = discordMessageId,
                TaskGroupId          = taskGroupId,
                TaskItemId           = taskItemId,
            };
            repo.Add(interaction);
            await repo.SaveAsync();

            // fire-and-forget SignalR 推送（失敗不影響主流程）
            _ = pushService.PushInteractionUpdateAsync();

            logger.LogInformation("BossInteraction 已寫入（Id={Id}，Type={Type}）", interaction.Id, interactionType);
            return interaction.Id;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "BossInteraction 寫入失敗（Type={Type}），略過（non-critical）", interactionType);
            return null;
        }
    }

    // ─── Discord 回覆時同步更新 ───────────────────────────────────────────────

    /// <summary>
    /// Discord 按鈕被點擊時，嘗試將對應 BossInteraction 標記為 discord 已回覆。
    /// 回傳 true：本次 Discord 為先到方（可繼續現有流程）。
    /// 回傳 false：Dashboard 已先回覆（Discord 端應 early return）。
    /// 若查無記錄或發生例外，一律回傳 true 讓 Discord 流程正常繼續。
    /// </summary>
    public async Task<bool> SyncDiscordResponseAsync(decimal discordMessageId, string action)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<BossInteractionRepository>();

            var interaction = await repo.GetByDiscordMessageIdAsync(discordMessageId);

            // 無記錄（寫入失敗或舊訊息）：讓 Discord 流程正常繼續
            if (interaction is null) return true;

            // 已被回覆（Dashboard 先到）
            if (interaction.Status != "pending") return false;

            // 嘗試樂觀鎖標記 discord 回覆
            var succeeded = await repo.RespondAsync(interaction.Id, action, "discord");

            if (succeeded)
            {
                _ = pushService.PushInteractionUpdateAsync();
                logger.LogInformation("BossInteraction Discord 回覆（Id={Id}，Action={Action}）", interaction.Id, action);
            }
            else
            {
                logger.LogInformation("BossInteraction 先到先贏：Dashboard 已先回覆（Id={Id}）", interaction.Id);
            }

            return succeeded;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SyncDiscordResponseAsync 失敗，略過（non-critical）");
            // 發生例外時讓 Discord 流程繼續
            return true;
        }
    }
}
