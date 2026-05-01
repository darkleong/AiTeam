using AiTeam.Bot.Services;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Configuration;

/// <summary>
/// Stage 32：以「AppSettings 優先、appsettings.json fallback」方式讀取 WorkflowSettings 輪次上限。
///
/// AppSettings keys：
///   Workflow:ReviewAppealMaxRounds
///   Workflow:QaFixMaxRounds
///   Workflow:DevPlanAppealMaxRounds
///   Workflow:KickoffMaxRounds
///   Workflow:DesignMeetingMaxRounds
///   Workflow:UseFrameworkAppealLoop  (Stage 49 v4 漸進遷移)
///
/// AppSettings 無值或解析失敗 / 非正整數時，fallback 到 IOptions&lt;WorkflowSettings&gt;（讀 appsettings.json）。
/// </summary>
public class WorkflowSettingsResolver(
    IOptions<WorkflowSettings> options,
    AppSettingsService appSettings)
{
    private WorkflowSettings Defaults => options.Value;

    public Task<int> GetReviewAppealMaxRoundsAsync(CancellationToken ct = default)
        => GetIntAsync("Workflow:ReviewAppealMaxRounds", Defaults.ReviewAppealMaxRounds, ct);

    public Task<int> GetQaFixMaxRoundsAsync(CancellationToken ct = default)
        => GetIntAsync("Workflow:QaFixMaxRounds", Defaults.QaFixMaxRounds, ct);

    public Task<int> GetDevPlanAppealMaxRoundsAsync(CancellationToken ct = default)
        => GetIntAsync("Workflow:DevPlanAppealMaxRounds", Defaults.DevPlanAppealMaxRounds, ct);

    public Task<int> GetKickoffMaxRoundsAsync(CancellationToken ct = default)
        => GetIntAsync("Workflow:KickoffMaxRounds", Defaults.KickoffMaxRounds, ct);

    public Task<int> GetDesignMeetingMaxRoundsAsync(CancellationToken ct = default)
        => GetIntAsync("Workflow:DesignMeetingMaxRounds", Defaults.DesignMeetingMaxRounds, ct);

    /// <summary>Stage 49：v4 漸進遷移 feature flag。預設 false（走 legacy AppealOrchestrationService）。</summary>
    public Task<bool> GetUseFrameworkAppealLoopAsync(CancellationToken ct = default)
        => GetBoolAsync("Workflow:UseFrameworkAppealLoop", Defaults.UseFrameworkAppealLoop, ct);

    private async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct)
    {
        var raw = await appSettings.GetAsync(key, ct);
        return int.TryParse(raw, out var v) && v > 0 ? v : fallback;
    }

    private async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct)
    {
        var raw = await appSettings.GetAsync(key, ct);
        return bool.TryParse(raw, out var v) ? v : fallback;
    }
}
