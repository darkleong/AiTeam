using System.Text.Json;
using AiTeam.Bot.Agents.Pm;
using AiTeam.Bot.Orchestration.Meeting;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = AiTeam.Shared.Constants.TaskStatus;

namespace AiTeam.Bot.Orchestration.Epic;

/// <summary>
/// Stage 59：Epic / sub-task chain 機制集中管理（從 TaskGroupService E 區段拆出 + HandleEpicPartialPaused 從 D 搬入）。
///
/// Stage 46-FF 三十五自動拆任務機制（accept → BuildEpicSubTasks 拆 N phase / sub-task done → 啟動下個 phase / sub-task fail → 部分暫停 epic）：
///   ① BuildEpicSubTasksAsync          — split_accept / split_modify path 拆 epic 為 N sub-task + 啟動 Phase 1
///   ② TriggerNextPhaseIfSubTaskAsync  — sub-task done 後啟動下個 Phase or 標 epic 主 group done
///   ③ PauseEpicAndNotifyAsync         — sub-task failed/needs_intervention → epic.EpicPaused=true + 開 epic_partial_paused interaction
///   ④ HandleEpicPartialPausedAsync    — Christ 對 epic_partial_paused 的 epic_resume / epic_abort response 路由
///   ⑤ SimulateEpicRaceAsync           — Stage 57 Mock 專用：並行雙 PauseEpic call 模擬 race condition（驗 FF 五十一 idempotent helper 防線）
///
/// 跨 service 依賴：透過 IServiceProvider lazy resolve TaskGroupService.FireStepsAsync（避免循環依賴 — 對齊 Stage 36 既有 IServiceProvider lazy resolve pattern）。
/// </summary>
public class EpicChainService(
    IServiceProvider serviceProvider,
    InteractionService interactionService,
    ILogger<EpicChainService> logger)
{
    /// <summary>
    /// Stage 46-FF 三十五：epic_partial_paused 卡片分派（恢復 epic / 放棄整個 epic）。
    /// Stage 57-FF 五十一：雙 case transaction + AsNoTracking fresh read idempotent（race condition 雙層防第二層 — handler 端）。
    /// </summary>
    public async Task HandleEpicPartialPausedAsync(
        string contextJson, string action, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(contextJson);
        if (!doc.RootElement.TryGetProperty("epicGroupId", out var g)
            || !Guid.TryParse(g.GetString(), out var epicId))
            return;

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var db       = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        switch (action)
        {
            case "epic_resume":
            {
                // Stage 57-FF 五十一：transaction + AsNoTracking fresh read idempotent
                // 議題 3 修法：NpgsqlRetryingExecutionStrategy 不允許 user-initiated transaction，
                // 必須用 CreateExecutionStrategy().ExecuteAsync 包整個 transaction 作 retriable unit
                var strategyResume = db.Database.CreateExecutionStrategy();
                await strategyResume.ExecuteAsync(async () =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    var freshEpic = await db.TaskGroups.AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == epicId, ct);
                    if (freshEpic is null)
                    {
                        logger.LogWarning("HandleEpicPartialPaused：找不到 epic TaskGroup ({Id})", epicId);
                        await tx.CommitAsync(ct);
                        return;
                    }
                    if (freshEpic.EpicPaused == false)
                    {
                        logger.LogInformation(
                            "[Stage57] HandleEpicPartialPaused：epic 已 EpicPaused=false（前一個 handler 已處理），跳過 nextPending FireSteps（Epic={Id}）",
                            epicId);
                        await tx.CommitAsync(ct);
                        return;
                    }

                    // 仍 EpicPaused=true → 走原邏輯（fresh tracked entity 後寫入 + nextPending fire）
                    var epic = await taskRepo.GetGroupByIdAsync(epicId, ct);
                    if (epic is null)
                    {
                        await tx.CommitAsync(ct);
                        return;
                    }
                    epic.EpicPaused = false;
                    await taskRepo.SaveAsync(ct);

                    var nextPending = await db.TaskGroups
                        .Where(t => t.ParentGroupId == epicId && t.Status == "pending")
                        .OrderBy(t => t.PhaseNumber)
                        .FirstOrDefaultAsync(ct);
                    if (nextPending is not null)
                    {
                        var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
                        await tgs.FireStepsAsync(nextPending, [new WorkflowStep("Dev_plan")], ct);
                    }
                    await tx.CommitAsync(ct);
                });
                break;
            }
            case "epic_abort":
            {
                // Stage 57-FF 五十一：epic_abort 同樣 idempotent（避免重複標 cancelled / 重複 log）
                // 議題 3 修法：CreateExecutionStrategy 包 transaction
                var strategyAbort = db.Database.CreateExecutionStrategy();
                await strategyAbort.ExecuteAsync(async () =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    var freshEpic = await db.TaskGroups.AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == epicId, ct);
                    if (freshEpic is null)
                    {
                        logger.LogWarning("HandleEpicPartialPaused：找不到 epic TaskGroup ({Id})", epicId);
                        await tx.CommitAsync(ct);
                        return;
                    }
                    if (freshEpic.Status == "cancelled")
                    {
                        logger.LogInformation(
                            "[Stage57] HandleEpicPartialPaused：epic 已 cancelled（前一個 handler 已處理），跳過 abort（Epic={Id}）",
                            epicId);
                        await tx.CommitAsync(ct);
                        return;
                    }

                    var epic = await taskRepo.GetGroupByIdAsync(epicId, ct);
                    if (epic is null)
                    {
                        await tx.CommitAsync(ct);
                        return;
                    }
                    taskRepo.UpdateGroupStatus(epic, "cancelled");
                    var subPending = await db.TaskGroups
                        .Where(t => t.ParentGroupId == epicId && t.Status == "pending")
                        .ToListAsync(ct);
                    foreach (var s in subPending)
                        taskRepo.UpdateGroupStatus(s, "cancelled");
                    await taskRepo.SaveAsync(ct);
                    await tx.CommitAsync(ct);
                    logger.LogInformation("epic_abort：epic + {Count} 個 pending sub-task 全標 cancelled（Epic={Id}）",
                        subPending.Count, epicId);
                });
                break;
            }
            default:
                logger.LogWarning("HandleEpicPartialPaused：未識別 action={Action}", action);
                break;
        }
    }

    /// <summary>
    /// Stage 46-FF 三十五：依 SplitProposal phases 建 N 個 sub-task TaskGroup（共享 parent 4 大欄位）+ 啟動 Phase 1。
    /// v1.1 Aria 回饋 #1：idempotent 檢查防 double-click。
    /// v1.1 Aria 回饋 #3：簽名 Guid parentGroupId + 內部 fresh read parent，避免 stale 4 大欄位。
    /// </summary>
    public async Task BuildEpicSubTasksAsync(
        Guid parentGroupId, SplitProposal proposal, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ── v1.1 Aria 回饋 #1：idempotent 檢查（防雙 tab double-click 建重複 sub-task） ──
        if (await db.TaskGroups.AnyAsync(t => t.ParentGroupId == parentGroupId, ct))
        {
            logger.LogInformation(
                "BuildEpicSubTasksAsync：parent {Id} 已有 sub-task，視為重複呼叫，略過",
                parentGroupId);
            return;
        }

        // ── v1.1 Aria 回饋 #3：fresh read parent，避免 4 大欄位複製 stale 資料 ──
        var parent = await db.TaskGroups
            .FirstOrDefaultAsync(g => g.Id == parentGroupId, ct);
        if (parent is null)
        {
            logger.LogWarning("BuildEpicSubTasksAsync：找不到 parent {Id}", parentGroupId);
            return;
        }

        // 1. parent 標 epic 主 group：EpicPaused = false（議題 5）
        parent.EpicPaused = false;

        // 2. 依 phases 建 sub-task TaskGroup（共享 parent Kickoff/Design 4 大欄位）
        foreach (var phase in proposal.Phases)
        {
            var subGroup = new Data.TaskGroup
            {
                Id               = Guid.NewGuid(),
                Title            = $"{parent.Title} - Phase {phase.Phase}: {phase.Description}", // 議題 7 命名
                Project          = parent.Project,
                ProjectId        = parent.ProjectId,
                Status           = "pending",
                WorkflowType     = parent.WorkflowType,
                ParentGroupId    = parent.Id,
                PhaseNumber      = phase.Phase,
                PhaseDescription = phase.Description,

                // sub-task 共享 parent Kickoff/Design 4 大欄位（FF 三十五 細節 2，fresh read 後複製）
                KickoffMeetingLog = parent.KickoffMeetingLog,
                TaskPlan          = parent.TaskPlan,
                DesignMeetingLog  = parent.DesignMeetingLog,
                DesignPlan        = parent.DesignPlan,

                // 共享 Issue 子集 + UI 規格（粗略策略：sub-task 都共享同一份，Cody Dev_plan 階段依 phase.Issues 自行對焦）
                IssueUrls     = FilterIssueUrls(parent.IssueUrls, phase.Issues),
                UiSpecContent = parent.UiSpecContent,
            };
            db.TaskGroups.Add(subGroup);
        }
        await db.SaveChangesAsync(ct);

        // 3. 啟動 Phase 1 sub-task（fire Dev_plan，跳過 Kickoff/Design）
        var phase1 = await db.TaskGroups
            .FirstOrDefaultAsync(g => g.ParentGroupId == parentGroupId && g.PhaseNumber == 1, ct);
        if (phase1 is not null)
        {
            logger.LogInformation(
                "BuildEpicSubTasksAsync：epic {Parent} 拆 {Count} 個 sub-task，啟動 Phase 1（{Phase1Id}）",
                parentGroupId, proposal.Phases.Count, phase1.Id);
            var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
            await tgs.FireStepsAsync(phase1, [new WorkflowStep("Dev_plan")], ct);
        }
        else
        {
            logger.LogWarning(
                "BuildEpicSubTasksAsync：找不到 Phase 1 sub-task（Parent={Id}）",
                parentGroupId);
        }
    }

    /// <summary>
    /// Stage 46-FF 三十五：sub-task done 後 → 啟動下個 Phase or 標 epic 主 group done。
    /// 在 MarkGroupDoneOrInterventionAsync done 路徑被呼叫；epic.EpicPaused=true 時攔下不啟動下個 Phase。
    /// </summary>
    public async Task TriggerNextPhaseIfSubTaskAsync(Data.TaskGroup group, CancellationToken ct)
    {
        if (group.ParentGroupId is null) return; // 不是 sub-task

        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var db       = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var parent = await taskRepo.GetGroupByIdAsync(group.ParentGroupId.Value, ct);
        if (parent is null) return;

        // epic 暫停中 → 不啟動下個 Phase（議題 8 兩機制獨立 + Stage 45 IsPaused 對齊）
        if (parent.EpicPaused == true)
        {
            logger.LogInformation(
                "TriggerNextPhaseIfSubTask：epic {Parent} 暫停中，不啟動下個 Phase（current={Phase}）",
                parent.Id, group.PhaseNumber);
            return;
        }

        // 找下個 PhaseNumber + 1
        var nextPhaseNum = (group.PhaseNumber ?? 0) + 1;
        var nextPhase = await db.TaskGroups
            .FirstOrDefaultAsync(g => g.ParentGroupId == parent.Id && g.PhaseNumber == nextPhaseNum, ct);

        if (nextPhase is not null)
        {
            logger.LogInformation(
                "TriggerNextPhaseIfSubTask：Phase {Done} done → 啟動 Phase {Next}（Epic={Parent}）",
                group.PhaseNumber, nextPhaseNum, parent.Id);
            var tgs = scope.ServiceProvider.GetRequiredService<TaskGroupService>();
            await tgs.FireStepsAsync(nextPhase, [new WorkflowStep("Dev_plan")], ct);
        }
        else
        {
            // 最後一個 Phase done → epic 主 group 標 done
            taskRepo.UpdateGroupStatus(parent, TaskStatus.Done);
            await taskRepo.SaveAsync(ct);
            logger.LogInformation(
                "TriggerNextPhaseIfSubTask：最後 Phase {Done} done → epic {Parent} 標 done",
                group.PhaseNumber, parent.Id);
        }
    }

    /// <summary>Stage 57 Mock 專用：模擬同 epic 兩 sub-task 同時 fail 的 race condition（驗 FF 五十一 idempotent helper 防線）。
    /// 並行對 Phase 1 + Phase 2 sub-task 呼叫 PauseEpicAndNotifyAsync，模擬時間差 < 100ms 雙 fire。
    /// 對齊 Stage 51 KickoffMidInterruptTriggerStore in-memory pattern — 只在 Mock alias case 內呼叫。</summary>
    public async Task SimulateEpicRaceAsync(Guid epicId, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subTasks = await db.TaskGroups.AsNoTracking()
            .Where(t => t.ParentGroupId == epicId)
            .OrderBy(t => t.PhaseNumber)
            .Take(2)
            .ToListAsync(ct);
        if (subTasks.Count < 2)
        {
            logger.LogWarning("[Stage57] SimulateEpicRace：sub-task 數 {Count} < 2，無法模擬 race（Epic={Id}）", subTasks.Count, epicId);
            return;
        }
        logger.LogInformation("[Stage57] SimulateEpicRace：並行雙 PauseEpicAndNotify（Epic={Id}, Phase1={P1}, Phase2={P2}）",
            epicId, subTasks[0].Id, subTasks[1].Id);
        // 並行雙呼叫 — Task.WhenAll 模擬 < 100ms 雙 fire race window
        await Task.WhenAll(
            PauseEpicAndNotifyAsync(subTasks[0], ct),
            PauseEpicAndNotifyAsync(subTasks[1], ct));
    }

    /// <summary>
    /// Stage 46-FF 三十五：sub-task failed/needs_intervention → epic 標 EpicPaused + 建 BossInteraction。
    /// Stage 57：private → internal（讓 SimulateEpicRaceAsync race Mock test helper 在同 namespace 內呼叫）。
    /// Stage 59：搬到 EpicChainService 後 public — 由 TaskGroupService 主檔 MarkGroupDoneOrIntervention 呼叫。
    /// </summary>
    public async Task PauseEpicAndNotifyAsync(Data.TaskGroup subTask, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();
        var parent   = await taskRepo.GetGroupByIdAsync(subTask.ParentGroupId!.Value, ct);
        if (parent is null) return;

        parent.EpicPaused = true;
        await taskRepo.SaveAsync(ct);

        logger.LogWarning(
            "PauseEpicAndNotify：sub-task Phase {Phase} needs_intervention → epic {Parent} EpicPaused=true",
            subTask.PhaseNumber, parent.Id);

        // Stage 55B 範圍邊界：epic_partial_paused 仍 fire-and-forget — Stage 46 Epic Chain 機制跨 framework boundary
        // （parent group 在 Pipeline，sub-task pause 是 epic-level 動作，不在 sub-task 自己的 Pipeline Workflow 內）
        // Stage 57-FF 五十一：CreateInteractionAsync → TryCreateUniqueInteractionAsync 雙 fire 防護（race-prone fire 端 idempotent）
        _ = interactionService.TryCreateUniqueInteractionAsync(
            taskGroupId:          parent.Id,
            interactionType:      "epic_partial_paused",
            title:                $"Epic 部分暫停：{parent.Title}",
            description:          $"Phase {subTask.PhaseNumber}（{subTask.PhaseDescription}）失敗，後續 Phase 已暫停。" +
                                  $"原因：{subTask.InterventionReason ?? "（無）"}",
            project:              parent.Project,
            agentName:            null,
            availableActionsJson: InteractionService.EpicPartialPausedActionsJson,
            contextJson: JsonSerializer.Serialize(new
            {
                epicGroupId       = parent.Id.ToString(),
                failedPhaseId     = subTask.Id.ToString(),
                failedPhaseNumber = subTask.PhaseNumber
            }));
    }

    /// <summary>
    /// Stage 46-FF 三十五：從 parent IssueUrls JSON array 過濾 phase.Issues 對應的 URL 子集。
    /// 失敗（解析錯 / index 越界）→ 回 parent 整份（後續 Cody 階段依 DesignPlan 自行對焦）。
    /// </summary>
    private static string? FilterIssueUrls(string? parentIssueUrls, List<int> phaseIssueIds)
    {
        if (string.IsNullOrWhiteSpace(parentIssueUrls) || phaseIssueIds is { Count: 0 })
            return parentIssueUrls;

        try
        {
            using var doc = JsonDocument.Parse(parentIssueUrls);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return parentIssueUrls;

            var allUrls = doc.RootElement.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            // phaseIssueIds 為 1-based（對應 Rosa 拆解的 Issue 編號）
            var filtered = phaseIssueIds
                .Where(id => id >= 1 && id <= allUrls.Count)
                .Select(id => allUrls[id - 1])
                .ToList();

            return filtered.Count > 0
                ? JsonSerializer.Serialize(filtered)
                : parentIssueUrls;
        }
        catch { return parentIssueUrls; }
    }
}
