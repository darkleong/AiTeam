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
///   Workflow:UseFrameworkKickoff     (Stage 50 v4 漸進遷移第二步)
///   Workflow:UseFrameworkKickoffMidInterrupt  (Stage 51 v4 漸進遷移第三步 HITL 試點)
///   Workflow:UseFrameworkDesign               (Stage 52 v4 漸進遷移第四步 Design Meeting)
///   Workflow:UseFrameworkPipeline             (Stage 53A v4 漸進遷移第五步 macro-orchestration)
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

    /// <summary>Stage 50：v4 漸進遷移第二步 feature flag。預設 false（走 legacy KickoffMeetingService）。</summary>
    public Task<bool> GetUseFrameworkKickoffAsync(CancellationToken ct = default)
        => GetBoolAsync("Workflow:UseFrameworkKickoff", Defaults.UseFrameworkKickoff, ct);

    /// <summary>Stage 51：v4 漸進遷移第三步 HITL 試點 feature flag。預設 false（不影響 Stage 50 framework Kickoff 行為）。
    /// 雙 flag 連動規則：本 flag 只在 UseFrameworkKickoff = true 時有意義（caller 自行檢查兩 flag）。</summary>
    public Task<bool> GetUseFrameworkKickoffMidInterruptAsync(CancellationToken ct = default)
        => GetBoolAsync("Workflow:UseFrameworkKickoffMidInterrupt", Defaults.UseFrameworkKickoffMidInterrupt, ct);

    /// <summary>Stage 52：v4 漸進遷移第四步 Design Meeting feature flag。預設 false（走 legacy DesignMeetingService）。
    /// 與 Stage 49/50/51 三 flag 完全獨立（pipeline 上 Design 跟 Kickoff 是兩個獨立節點）。</summary>
    public Task<bool> GetUseFrameworkDesignAsync(CancellationToken ct = default)
        => GetBoolAsync("Workflow:UseFrameworkDesign", Defaults.UseFrameworkDesign, ct);

    /// <summary>Stage 53A：v4 漸進遷移第五步 macro-orchestration feature flag。預設 false（走 legacy WorkflowEngine + TaskGroupService 路徑）。
    /// 三 flag 連動規則：本 flag 只在 UseFrameworkKickoff = true AND UseFrameworkDesign = true 時有意義（caller 自行檢查三 flag）。</summary>
    public Task<bool> GetUseFrameworkPipelineAsync(CancellationToken ct = default)
        => GetBoolAsync("Workflow:UseFrameworkPipeline", Defaults.UseFrameworkPipeline, ct);

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
