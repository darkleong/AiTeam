using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：Bot 啟動時 scan running Petra session → 重啟 rebuild context（v5 動態架構 PoC）。
/// Stage 78a：flag check 砍 / Recovery 永遠 active（v4 path 完整砍 / 對齊 v5.5 production default 紀律 / Stage 77 完整收口 + Trial_v6-v22 累積 17 次 0 v4 caller 紀律延續）。
/// Stage 85：加 paused session timeout cleanup loop（既有 startup recovery 邏輯 0 行為改變 / 包成 RunStartupRecoveryAsync）—
/// paused 超過 PausedSessionTimeoutHours（default 24h）自動 cancel + Discord push 告知。
///
/// 紀律對齊 5 挑戰拍板 #5 — 重啟重跑不從 checkpoint resume：
/// - 從 task 原始 input + 已 responded BossInteraction 紀錄重跑 DecideAsync + BuildSequential
/// - 不雙重 ask Christ
/// </summary>
public class PetraSessionRecoveryService(
    IServiceProvider rootSp,
    ILogger<PetraSessionRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 啟動延遲 — 等其他 hosted service ready
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        // Stage 63B 既有：startup running session resume（0 行為改變 / 包成 method）
        await RunStartupRecoveryAsync(stoppingToken);

        // Stage 85：paused session timeout cleanup loop（每 1 小時跑一次）
        var checkInterval = TimeSpan.FromHours(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(checkInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }

            try
            {
                await RunPausedTimeoutCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "PetraSessionRecoveryService timeout cleanup 失敗（容錯不中斷 loop）");
            }
        }
    }

    /// <summary>Stage 63B 既有：啟動時 scan running session → ResumeAsync 重 build context（0 行為改變）。</summary>
    private async Task RunStartupRecoveryAsync(CancellationToken stoppingToken)
    {
        using var scope = rootSp.CreateScope();

        try
        {
            var sessionRepo = scope.ServiceProvider.GetRequiredService<PetraSessionRepository>();
            var orchestrator = scope.ServiceProvider.GetRequiredService<PetraOrchestratorService>();
            var running = await sessionRepo.GetRunningAsync(stoppingToken);

            logger.LogInformation("PetraSessionRecoveryService 掃描 running session — count={Count}", running.Count);

            foreach (var session in running)
            {
                if (stoppingToken.IsCancellationRequested) break;
                try
                {
                    var result = await orchestrator.ResumeAsync(session.Id, stoppingToken);
                    logger.LogInformation(
                        "PetraSession resume 完成 sessionId={SessionId} success={Success}",
                        session.Id, result.Success);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "PetraSession resume 失敗 sessionId={SessionId}", session.Id);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "PetraSessionRecoveryService 啟動掃描失敗 — v5 PoC 容錯不中斷 Bot");
        }
    }

    /// <summary>Stage 85：paused session timeout cleanup（超過 PausedSessionTimeoutHours 自動 cancel + Discord push 告知）。
    /// caller 負責 SaveChangesAsync 紀律明確化（對齊 PetraSessionRepository class doc）/ 批次寫一次 + 寫完才 Discord push。</summary>
    private async Task RunPausedTimeoutCleanupAsync(CancellationToken ct)
    {
        using var scope = rootSp.CreateScope();
        var sp = scope.ServiceProvider;
        var resolver = sp.GetRequiredService<WorkflowSettingsResolver>();
        var repo = sp.GetRequiredService<PetraSessionRepository>();
        var db = sp.GetRequiredService<AppDbContext>();
        var discordAlert = sp.GetRequiredService<DiscordAlertService>();

        var hours = await resolver.GetPausedSessionTimeoutHoursAsync(ct);
        var cutoff = DateTime.UtcNow.AddHours(-hours);
        var staleSessions = await repo.GetPausedOlderThanAsync(cutoff, ct);
        if (staleSessions.Count == 0) return;

        logger.LogWarning("PetraSessionRecoveryService 偵測 {Count} 筆 paused session 超時 {Hours}h — 批次自動 cancel",
            staleSessions.Count, hours);

        foreach (var s in staleSessions)
        {
            await repo.CancelAsync(s.Id, ct);
        }
        await db.SaveChangesAsync(ct);

        foreach (var s in staleSessions)
        {
            await discordAlert.SendThrottledAsync("paused_timeout",
                $"⚠️ **[Stage 85 timeout]** paused PetraSession `{s.Id}` 超時 {hours}h 自動 cancel（UpdatedAt={s.UpdatedAt:yyyy-MM-dd HH:mm} UTC）");
        }
    }
}
