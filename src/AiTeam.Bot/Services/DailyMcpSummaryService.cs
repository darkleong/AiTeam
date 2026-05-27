using AiTeam.Bot.Configuration;
using AiTeam.Data;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;

namespace AiTeam.Bot.Services;

/// <summary>
/// F2：每日早 9 點 Asia/Taipei 觸發 / 查 mcp_* 表彙總昨日 24h + 累積 grand total / Discord push 到「每日摘要」channel。
///
/// 設計紀律：
///   - BackgroundService Singleton / DI 用 IServiceScopeFactory 拿 Scoped AppDbContext
///   - 對齊 RecordNotificationService 既有 Discord push pattern（client.GetGuild(...).TextChannels）
///   - 失敗只 log Warning / 不影響其他服務
///
/// 不混合既有 RecordNotificationService（task complete / team close 即時 push 仍走既有 pattern）。
/// </summary>
public class DailyMcpSummaryService(
    DiscordSocketClient client,
    IServiceScopeFactory scopeFactory,
    IOptions<DiscordSettings> settings,
    ILogger<DailyMcpSummaryService> logger) : BackgroundService
{
    private readonly DiscordSettings _settings = settings.Value;
    private static readonly TimeZoneInfo TaipeiTz = ResolveTaipeiTimeZone();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DailyMcpSummaryService 啟動 — 排程每日 09:00 Asia/Taipei 觸發");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = CalculateDelayUntilNextTrigger();
                logger.LogInformation("DailyMcpSummary 下次觸發於 {NextRunUtc} UTC（等待 {Delay}）",
                    DateTime.UtcNow.Add(delay), delay);

                await Task.Delay(delay, stoppingToken);

                if (stoppingToken.IsCancellationRequested) break;

                await SendDailySummaryAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DailyMcpSummaryService 觸發迴圈例外（非中止）");
                // 防 tight loop：例外後 sleep 5 分鐘再續
                try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>計算距離下一個 Asia/Taipei 09:00 的 TimeSpan。</summary>
    private static TimeSpan CalculateDelayUntilNextTrigger()
    {
        var nowTaipei = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TaipeiTz);
        var todayTrigger = new DateTime(nowTaipei.Year, nowTaipei.Month, nowTaipei.Day, 9, 0, 0, DateTimeKind.Unspecified);
        var nextTrigger = nowTaipei >= todayTrigger ? todayTrigger.AddDays(1) : todayTrigger;
        var nextTriggerUtc = TimeZoneInfo.ConvertTimeToUtc(nextTrigger, TaipeiTz);
        var delay = nextTriggerUtc - DateTime.UtcNow;
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromMinutes(1);
    }

    /// <summary>跨 OS time zone id 容錯（Windows: "Taipei Standard Time" / Linux: "Asia/Taipei"）。</summary>
    private static TimeZoneInfo ResolveTaipeiTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei"); }
        catch (TimeZoneNotFoundException)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"); }
            catch { return TimeZoneInfo.Utc; }
        }
    }

    private async Task SendDailySummaryAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var nowUtc = DateTime.UtcNow;
        var since = nowUtc.AddHours(-24);

        // 昨日 24h 統計
        var dailyInput = await db.AgentTokenUsages
            .Where(x => x.CreatedAt >= since && x.CreatedAt < nowUtc)
            .SumAsync(x => (long)x.InputTokens, cancellationToken);
        var dailyOutput = await db.AgentTokenUsages
            .Where(x => x.CreatedAt >= since && x.CreatedAt < nowUtc)
            .SumAsync(x => (long)x.OutputTokens, cancellationToken);
        var activeTeamCount = await db.AgentTeams
            .Where(x => x.Status == "active")
            .CountAsync(cancellationToken);
        var completedTaskCount = await db.AgentTasks
            .Where(x => x.Status == "completed" && x.CompletedAt >= since && x.CompletedAt < nowUtc)
            .CountAsync(cancellationToken);

        // Per model breakdown（昨日 24h）
        var perModel = await db.AgentTokenUsages
            .Where(x => x.CreatedAt >= since && x.CreatedAt < nowUtc)
            .GroupBy(x => x.Model ?? "（未指定）")
            .Select(g => new
            {
                Model = g.Key,
                Input = g.Sum(x => (long)x.InputTokens),
                Output = g.Sum(x => (long)x.OutputTokens)
            })
            .OrderByDescending(x => x.Input + x.Output)
            .ToListAsync(cancellationToken);

        // 累積 grand total（since v4.0.0 上線 = 全表 SUM）
        var grandInput = await db.AgentTokenUsages.SumAsync(x => (long)x.InputTokens, cancellationToken);
        var grandOutput = await db.AgentTokenUsages.SumAsync(x => (long)x.OutputTokens, cancellationToken);

        var todayTaipei = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TaipeiTz);
        var message = BuildMessage(
            reportDate: todayTaipei,
            dailyInput: dailyInput,
            dailyOutput: dailyOutput,
            activeTeamCount: activeTeamCount,
            completedTaskCount: completedTaskCount,
            perModel: perModel.Select(x => (x.Model, x.Input, x.Output)).ToList(),
            grandInput: grandInput,
            grandOutput: grandOutput);

        await PushToDiscordAsync(message);
    }

    private static string BuildMessage(
        DateTime reportDate,
        long dailyInput,
        long dailyOutput,
        int activeTeamCount,
        int completedTaskCount,
        List<(string Model, long Input, long Output)> perModel,
        long grandInput,
        long grandOutput)
    {
        var sb = new StringBuilder();
        sb.Append("📊 **AiTeam 每日摘要（").Append(reportDate.ToString("yyyy-MM-dd")).AppendLine("）**");
        sb.AppendLine();
        sb.AppendLine("**昨日 24h**");
        sb.Append("- Token：input `").Append(dailyInput.ToString("N0"))
          .Append("` / output `").Append(dailyOutput.ToString("N0"))
          .Append("` / total `").Append((dailyInput + dailyOutput).ToString("N0")).AppendLine("`");
        sb.Append("- Active Team：`").Append(activeTeamCount).AppendLine("`");
        sb.Append("- Completed Task：`").Append(completedTaskCount).AppendLine("`");

        if (perModel.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Per Model（昨日 24h）**");
            foreach (var row in perModel)
            {
                sb.Append("- `").Append(row.Model)
                  .Append("`：input `").Append(row.Input.ToString("N0"))
                  .Append("` / output `").Append(row.Output.ToString("N0")).AppendLine("`");
            }
        }

        sb.AppendLine();
        sb.AppendLine("**累積 Grand Total（since v4.0.0）**");
        sb.Append("- Token：input `").Append(grandInput.ToString("N0"))
          .Append("` / output `").Append(grandOutput.ToString("N0"))
          .Append("` / total `").Append((grandInput + grandOutput).ToString("N0")).AppendLine("`");

        return sb.ToString();
    }

    private async Task PushToDiscordAsync(string message)
    {
        try
        {
            if (!ulong.TryParse(_settings.GuildId, out var guildId))
            {
                logger.LogWarning("DailyMcpSummary skip — Discord GuildId 未設定");
                return;
            }

            var channel = client.GetGuild(guildId)
                ?.TextChannels.FirstOrDefault(c => c.Name == _settings.Channels.DailySummary);

            if (channel is null)
            {
                logger.LogWarning("DailyMcpSummary skip — 找不到 Discord 頻道 #{Channel}", _settings.Channels.DailySummary);
                return;
            }

            await channel.SendMessageAsync(message);
            logger.LogInformation("DailyMcpSummary 已推送至 #{Channel}", _settings.Channels.DailySummary);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DailyMcpSummary 發送失敗");
        }
    }
}
