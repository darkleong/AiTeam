using AiTeam.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 80：HITL plan_confirm 4 decision dispatch consumer（BackgroundService polling 模式）。
/// Stage 81：擴 routing `replan_confirm`（動態 replan + HITL retry gate） — 同 BackgroundService 兩 InteractionType 共用 polling + dispatch 分支。
///
/// 設計脈絡：Stage 78c 一次過砍了 InteractionProcessor / WorkflowEngine / Pipeline framework 整套
/// — v5.5 baseline 內 BossInteraction 是「Dashboard 純顯示 + Christ ack」單向通道 / 0 Bot 端消費 dispatch。
/// Stage 80 引入 HITL plan_confirm 後需要把 responded `plan_confirm` interaction 拉起餵 Petra resume 4 decision routing
/// — 故新建本 BackgroundService 取代「InteractionProcessor 路由擴」（Roadmap §5 設計意圖：4 decision dispatch / 不是要復活 InteractionProcessor 框架）。
/// Stage 81：對齊 Roadmap §子項 6 — `replan_confirm` 純複用此 BackgroundService（0 新 BackgroundService）。
///
/// 紀律對齊：
/// - 3 秒 polling 間隔（對齊 PetraInboxProcessor / Stage 78c 前 InteractionProcessor 既有紀律）
/// - 啟動延遲 10s（對齊 PetraSessionRecoveryService / PetraInboxProcessor）
/// - 容錯：單筆 row 失敗不擋後續 polling（對齊 PetraInboxProcessor outer try-catch 紀律）
/// - ProcessedByBot 標記防重複處理（既有 BossInteraction schema 欄位 / 對齊 InteractionProcessor pre-Stage 78c 既有 pattern）
/// - PetraOrchestratorService 是 Scoped — per-row 開新 IServiceScope（PetraDispatchWorker 既有 pattern）
/// </summary>
public class PlanConfirmationProcessor(
    IServiceProvider serviceProvider,
    ILogger<PlanConfirmationProcessor> logger) : BackgroundService
{
    private const int PollingIntervalMs = 3000;
    private const int StartupDelaySeconds = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds), stoppingToken); }
        catch (OperationCanceledException) { return; }

        logger.LogInformation("PlanConfirmationProcessor 啟動 — 3s polling responded plan_confirm / replan_confirm interactions (Stage 81)");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOneAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "PlanConfirmationProcessor polling 異常 — 容錯不擋後續 polling");
            }

            try { await Task.Delay(PollingIntervalMs, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        logger.LogInformation("PlanConfirmationProcessor 結束");
    }

    private async Task ProcessOneAsync(CancellationToken ct)
    {
        Guid interactionId;
        Guid sessionId;
        string interactionType;
        string responseAction;
        string? responseContent;

        await using (var pickScope = serviceProvider.CreateAsyncScope())
        {
            var pickDb = pickScope.ServiceProvider.GetRequiredService<AppDbContext>();

            // FIFO：最早 RespondedAt 的 plan_confirm / replan_confirm responded interaction 優先
            // Stage 81：擴 filter 涵蓋 replan_confirm（動態 replan HITL retry gate）
            var pending = await pickDb.BossInteractions
                .Where(x => (x.InteractionType == "plan_confirm" || x.InteractionType == "replan_confirm")
                            && x.Status == "responded"
                            && !x.ProcessedByBot)
                .OrderBy(x => x.RespondedAt)
                .ThenBy(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (pending is null) return;

            // 原子標 ProcessedByBot=true（防多 instance race / Bot 重啟期重複 fire）
            var marked = await pickDb.BossInteractions
                .Where(x => x.Id == pending.Id && !x.ProcessedByBot)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ProcessedByBot, true), ct);
            if (marked == 0) return;

            // 從 ContextJson 還原 sessionId（PetraOrchestratorService.WaitFor*ConfirmationAsync 寫入時 sessionId 是 root field）
            var sid = ExtractSessionId(pending.ContextJson);
            if (sid is null)
            {
                logger.LogWarning(
                    "PlanConfirmationProcessor: ContextJson 缺 sessionId interactionId={Id} type={Type} — skip", pending.Id, pending.InteractionType);
                return;
            }

            interactionId   = pending.Id;
            sessionId       = sid.Value;
            interactionType = pending.InteractionType;
            responseAction  = pending.ResponseAction ?? "";
            responseContent = pending.ResponseContent;
        }

        var decision = MapActionToDecision(responseAction);
        if (decision is null)
        {
            logger.LogWarning(
                "PlanConfirmationProcessor: 未知 ResponseAction={Action} interactionId={Id} type={Type} — skip dispatch",
                responseAction, interactionId, interactionType);
            return;
        }

        logger.LogInformation(
            "PlanConfirmationProcessor pickup interactionId={Id} type={Type} sessionId={SessionId} action={Action} decision={Decision}",
            interactionId, interactionType, sessionId, responseAction, decision);

        await using var runScope = serviceProvider.CreateAsyncScope();
        var orchestrator = runScope.ServiceProvider.GetRequiredService<PetraOrchestratorService>();

        // Stage 81：dispatch 分支 — plan_confirm 走 Stage 80 既有 / replan_confirm 走新 ResumeFromReplanConfirmationAsync
        var result = interactionType switch
        {
            "plan_confirm"   => await orchestrator.ResumeFromPlanConfirmationAsync(sessionId, decision, responseContent, ct),
            "replan_confirm" => await orchestrator.ResumeFromReplanConfirmationAsync(sessionId, decision, responseContent, ct),
            _ => Orchestration.Petra.PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(),
                    $"未知 InteractionType={interactionType}"),
        };

        logger.LogInformation(
            "PlanConfirmationProcessor 完成 interactionId={Id} type={Type} sessionId={SessionId} decision={Decision} success={Success} dispatched={Count}",
            interactionId, interactionType, sessionId, decision, result.Success, result.DispatchedWorkerCount);
    }

    /// <summary>4 decision pattern action → decision string mapping（對齊 InteractionService.PlanConfirmActionsJson + ReplanConfirmActionsJson）。
    /// Stage 81：擴 4 replan_* action 對應同 decision string（dispatch 端用 interactionType 分支走不同 Resume method）。</summary>
    internal static string? MapActionToDecision(string action) => action switch
    {
        "plan_approve" or "replan_approve" => "approve",
        "plan_edit"    or "replan_edit"    => "edit",
        "plan_reject"  or "replan_reject"  => "reject",
        "plan_respond" or "replan_respond" => "respond",
        _                                  => null,
    };

    /// <summary>淺解 JSON 取 sessionId field（避雙端 PlanConfirmContext type 重新 link）— JsonDocument property lookup 0 額外 dep。</summary>
    internal static Guid? ExtractSessionId(string? contextJson)
    {
        if (string.IsNullOrWhiteSpace(contextJson)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(contextJson);
            if (!doc.RootElement.TryGetProperty("sessionId", out var sidProp)) return null;
            var sidStr = sidProp.GetString();
            return Guid.TryParse(sidStr, out var sid) ? sid : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
