using AiTeam.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：Bot 啟動時 scan running Petra session → 重啟 rebuild context（v5 動態架構 PoC）。
/// Stage 78a：flag check 砍 / Recovery 永遠 active（v4 path 完整砍 / 對齊 v5.5 production default 紀律 / Stage 77 完整收口 + Trial_v6-v22 累積 17 次 0 v4 caller 紀律延續）。
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
}
