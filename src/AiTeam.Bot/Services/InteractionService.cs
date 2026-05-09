using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>Stage 43-A：DevPlan 重產上限 — Cody 連續 N 次無法產出可用計畫書，需老闆決策。</summary>
    public const string DevPlanUnableActionsJson =
        """[{"id":"devplan_unable_skip","label":"跳過，直接開發","color":"warning"},{"id":"devplan_unable_abort","label":"放棄任務","color":"error"}]""";

    /// <summary>Stage 43-B：Dev / Dev_fix 失敗 — 中止 fix loop，需老闆介入。</summary>
    public const string DevFailedInterventionActionsJson =
        """[{"id":"dev_intervention_skip","label":"略過進下一階段","color":"warning"},{"id":"dev_intervention_retry","label":"重啟 Dev","color":"info"},{"id":"dev_intervention_abort","label":"放棄任務","color":"error"}]""";

    /// <summary>Stage 43-E：QA 連續失敗上限 — 需人工介入。</summary>
    public const string QaFailedInterventionActionsJson =
        """[{"id":"qa_intervention_continue","label":"再試一輪","color":"warning"},{"id":"qa_intervention_skip","label":"略過 QA 進下一階段","color":"info"},{"id":"qa_intervention_abort","label":"放棄任務","color":"error"}]""";

    /// <summary>Stage 43-F：Sage escalate — 文件無法歸檔，需老闆決策。</summary>
    public const string SageEscalateActionsJson =
        """[{"id":"sage_retry","label":"重跑歸檔","color":"warning"},{"id":"sage_skip","label":"略過歸檔，標完成","color":"info"},{"id":"sage_abort","label":"標需介入","color":"error"}]""";

    /// <summary>Stage 46-FF 三十五：Petra 拆 task 提案卡（4 按鈕：採納 / 修改 / 不拆 / 停止）。</summary>
    public const string SplitTaskProposalActionsJson =
        """[{"id":"split_accept","label":"採納 Petra 方案","color":"success","requiresInput":false},{"id":"split_modify","label":"修改方案 ✏️","color":"info","requiresInput":true},{"id":"split_reject","label":"不拆繼續原樣","color":"warning","requiresInput":false},{"id":"split_abort","label":"停止任務","color":"error","requiresInput":false}]""";

    /// <summary>Stage 46-FF 三十五：epic 部分暫停通知（sub-task failed/needs_intervention → 後續 Phase 不啟動）。</summary>
    public const string EpicPartialPausedActionsJson =
        """[{"id":"epic_resume","label":"恢復 epic","color":"success"},{"id":"epic_abort","label":"放棄整個 epic","color":"error"}]""";

    /// <summary>Stage 57-FF 五十二：Reviewer fix loop ×3 達上限 — Christ 拍板三選 mark_done / skip_qa / abort。</summary>
    public const string ReviewerFixLoopLimitActionsJson =
        """[{"id":"fix_loop_mark_done","label":"標完成","color":"success"},{"id":"fix_loop_skip_qa","label":"跳過 QA","color":"warning"},{"id":"fix_loop_abort","label":"終止 Pipeline","color":"error"}]""";

    /// <summary>Stage 51：framework HITL 中途介入卡（v4 漸進遷移第三步試點）。
    /// midinterrupt_apply 需 modal 收文字（修改指引）；midinterrupt_cancel 直接結束介入。</summary>
    public const string MidInterruptActionsJson =
        """[{"id":"midinterrupt_apply","label":"套用修改 ✏️","color":"info","requiresInput":true},{"id":"midinterrupt_cancel","label":"取消介入","color":"default","requiresInput":false}]""";

    public const string EmptyActionsJson = "[]";

    /// <summary>通知類互動（merge_notify / intervention / ceo_reply）：單一「我知道了」確認按鈕，點擊後標為已處理。</summary>
    public const string NotifyActionsJson =
        """[{"id":"ack","label":"我知道了","color":"default","requiresInput":false}]""";

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

            // Stage 54 follow-up #2：MockMode auto-approve（避免 Christ/Forge 每次手動 DB approve）
            // 對齊 Stage 53B 驗收期 docker exec psql 手動 update 的工具增強
            // 注意：source 必須用 "dashboard" 對齊 InteractionProcessor.GetDashboardResponsesAsync 消費路徑
            // （source="mock" 不會被 InteractionProcessor 輪詢消費，導致 responded interaction 無人處理 → 流程卡死）
            try
            {
                var appSettings = scope.ServiceProvider.GetRequiredService<AppSettingsService>();
                if (await appSettings.GetBoolAsync("MockMode", false))
                {
                    // ResponseAction 對齊 InteractionType 的 Continue / Ack action key
                    // InteractionProcessor 會 ProcessBossResponseAsync(type, action, ...) → 推進流程
                    var autoAction = interactionType switch
                    {
                        "kickoff"             => "kickoff_continue",
                        "design"              => "design_continue",
                        "proposal"            => "propose_yes",
                        // Stage 55A 場景 E 揭露：split_task_proposal 需 split_accept 觸發 BuildEpicSubTasksAsync
                        // （Stage 54 修法漏此 type，Stage 55A sub-task chain 場景才暴露 — 沿用 SplitTaskProposalActionsJson line 60 預設「採納」action）
                        "split_task_proposal" => "split_accept",
                        // Stage 55B Session B：4 個 type-specific intervention HITL 對應的 default approve action
                        // dev/qa intervention 用「再試一輪 / 重啟」維持 Pipeline 推進；devplan escalate/unable 用 skip 直接開發
                        "dev_failed_intervention" => "dev_intervention_retry",
                        "qa_failed_intervention"  => "qa_intervention_continue",
                        "devplan_escalate"        => "devplan_skip",
                        "dev_plan_unable"         => "devplan_unable_skip",
                        // Stage 57-FF 五十二：Reviewer fix loop ×3 達 limit — default 走完整 QA 路徑（mark_done → QaStageBridge）
                        "reviewer_fix_loop_limit" => "fix_loop_mark_done",
                        // Stage 57 自驗揭露 pre-Stage 57 既有 bug：epic_partial_paused 沒有 ack action（buttons: epic_resume / epic_abort）
                        // 預設 epic_resume 觸發 HandleEpicPartialPausedAsync handler idempotent 路徑（驗 FF 五十一第二層防線）
                        "epic_partial_paused"     => "epic_resume",
                        // ack-only 通知類（merge_notify / intervention / ceo_reply 等）
                        _                     => "ack",
                    };
                    var approved = await repo.RespondAsync(interaction.Id, autoAction, "dashboard");
                    if (approved)
                    {
                        logger.LogInformation(
                            "[Stage54] MockMode auto-approve interaction (Id={Id}, Type={Type}, Action={Action})",
                            interaction.Id, interactionType, autoAction);
                        _ = pushService.PushInteractionUpdateAsync();
                    }
                }
            }
            catch (Exception autoEx)
            {
                logger.LogWarning(autoEx, "[Stage54] MockMode auto-approve 失敗，略過（non-critical）");
            }

            return interaction.Id;
        }
        // Stage 57-fix（FF 五十一 fire 端 race window 補強，路線 a Christ 拍板）：
        // 23505-specific catch 必須在 generic Exception catch 之前 — DB partial unique index 攔住雙 fire race
        // 對應 ix_boss_interactions_pending_per_group_type，emit fix-specific log 區別 race 防線生效 vs 真正寫入錯誤
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505")
        {
            logger.LogInformation(
                "[Stage57-fix] BossInteraction unique constraint 攔住雙 fire race（Type={Type}, GroupId={Id}）— functional race-free + UI 1 卡",
                interactionType, taskGroupId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "BossInteraction 寫入失敗（Type={Type}），略過（non-critical）", interactionType);
            return null;
        }
    }

    /// <summary>
    /// Stage 57-FF 五十一：race-prone interaction 防雙 fire 的 idempotent wrapper（修法第一層 + 未來複用）。
    /// 同 (taskGroupId, type) 已有 status="pending" interaction → 回 null + log skip；無則 call CreateInteractionAsync。
    /// 與 CreateInteractionAsync 簽名 1:1 對齊（taskGroupId 拉到第 1 必填參數作 idempotent 鍵）— caller 改 1 行 swap 即可。
    /// pure additive：失敗只 log Warning 回 null（與 CreateInteractionAsync 一致）。
    /// </summary>
    public async Task<Guid?> TryCreateUniqueInteractionAsync(
        Guid     taskGroupId,
        string   interactionType,
        string   title,
        string   description,
        string?  project,
        string?  agentName,
        string   availableActionsJson,
        string?  contextJson      = null,
        decimal? discordMessageId = null,
        Guid?    taskItemId       = null)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<BossInteractionRepository>();
            if (await repo.HasPendingForGroupAndTypeAsync(taskGroupId, interactionType))
            {
                logger.LogInformation(
                    "[Stage57] TryCreateUniqueInteraction：同 (groupId={Id}, type={Type}) 已有 active interaction，跳過 fire 新卡（FF 五十一 idempotent helper）",
                    taskGroupId, interactionType);
                return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "[Stage57] TryCreateUniqueInteraction：idempotent 檢查失敗，fallback to CreateInteractionAsync（Type={Type}）",
                interactionType);
            // 失敗 fallback CreateInteractionAsync — 寧可雙 fire 也不要漏 fire
        }

        // Stage 57-fix（FF 五十一 fire 端 race window 補強，路線 a Christ 拍板）：
        // 23505 DbUpdateException catch 在 CreateInteractionAsync 內處理（generic Exception catch 之前的 specific 23505）—
        // 雙保險：上方 fast-path early check 避免 DB exception 開銷；DB constraint 攔 read-then-write TOCTOU window
        return await CreateInteractionAsync(
            interactionType, title, description, project, agentName, availableActionsJson,
            contextJson, discordMessageId, taskGroupId, taskItemId);
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
