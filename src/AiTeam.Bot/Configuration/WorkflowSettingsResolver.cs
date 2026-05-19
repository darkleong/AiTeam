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

    /// <summary>Stage 63B：v5 動態架構 PoC feature flag。預設 false（保留 v4 既有 CeoAgentService 入口）。
    /// 切 true 後 CeoAgentService.ProcessWithClaudeCodeAsync 開頭直接 forward 到 PetraOrchestratorService.StartAsync。</summary>
    public Task<bool> GetUsePetraOrchestratorV5Async(CancellationToken ct = default)
        => GetBoolAsync("Workflow:UsePetraOrchestratorV5", Defaults.UsePetraOrchestratorV5, ct);

    /// <summary>Stage 67：v5.5 Phase 1 Step 2 — Talent-Skill separation 重構基底 feature flag。預設 false（保留 v5 既有 IAgentTool + 7 worker class fallback path）。
    /// 切 true 後 PetraOrchestratorService.StartAsync dispatch 走 ITalent + GenericAgentTool path（看 Skill 找 Talent pool / round-robin）。
    /// 必須 UsePetraOrchestratorV5=true 才有意義（v5.5 是 v5 path 上面的演進）。</summary>
    public Task<bool> GetUseTalentSkillSeparationAsync(CancellationToken ct = default)
        => GetBoolAsync("Workflow:UseTalentSkillSeparation", Defaults.UseTalentSkillSeparation, ct);

    /// <summary>Stage 69：v5.5 Phase 2 Step 3 — 跨 session 長期持久記憶 feature flag。預設 false。
    /// 必須 UsePetraOrchestratorV5=true + UseTalentSkillSeparation=true 才有意義（caller 自行檢查三 flag）。</summary>
    public Task<bool> GetUseV5MemoryAsync(CancellationToken ct = default)
        => GetBoolAsync("Workflow:UseV5Memory", Defaults.UseV5Memory, ct);

    /// <summary>Stage 69：compact 觸發閾值百分比（buffer-above-keep）。預設 60。</summary>
    public Task<int> GetV5MemoryCompactThresholdPercentAsync(CancellationToken ct = default)
        => GetIntAsync("Workflow:V5MemoryCompactThresholdPercent", Defaults.V5MemoryCompactThresholdPercent, ct);

    /// <summary>Stage 69：compact 後保留 newest N 條。預設 50。</summary>
    public Task<int> GetV5MemoryCompactKeepCountAsync(CancellationToken ct = default)
        => GetIntAsync("Workflow:V5MemoryCompactKeepCount", Defaults.V5MemoryCompactKeepCount, ct);

    /// <summary>Stage 70：v5.5 Phase 2 Step 4 — hierarchical decomposition + dependency graph 拆解 feature flag。預設 false。
    /// 必須 UsePetraOrchestratorV5=true + UseTalentSkillSeparation=true 才有意義（caller 自行檢查三 flag）。</summary>
    public Task<bool> GetUseV5SubtaskPlanningAsync(CancellationToken ct = default)
        => GetBoolAsync("Workflow:UseV5SubtaskPlanning", Defaults.UseV5SubtaskPlanning, ct);

    /// <summary>Stage 72：v5.5 Phase 2 Step 5 — Prompt DB 化 feature flag。預設 false。
    /// 必須 UsePetraOrchestratorV5=true + UseTalentSkillSeparation=true 才有意義（caller 自行檢查三 flag）。</summary>
    public Task<bool> GetUseV5PromptDbAsync(CancellationToken ct = default)
        => GetBoolAsync("Workflow:UseV5PromptDb", Defaults.UseV5PromptDb, ct);

    /// <summary>Stage 77：v5.5 Phase 3 補強 — PetraDispatchWorker multi-consumer 並行上限（範圍守 [1, 10] / 超出 fallback default）。</summary>
    public Task<int> GetMaxConcurrentPetraAsync(CancellationToken ct = default)
        => GetIntInRangeAsync("Workflow:MaxConcurrentPetra", Defaults.MaxConcurrentPetra, 1, 10, ct);

    /// <summary>Stage 79：v5.5 image flow 補完 — per task max attachment count（範圍守 [1, 20] / 對齊 Claude Code CLI + Claude API 真實上限）。</summary>
    public Task<int> GetMaxAttachmentsPerTaskAsync(CancellationToken ct = default)
        => GetIntInRangeAsync("Workflow:MaxAttachmentsPerTask", Defaults.MaxAttachmentsPerTask, 1, 20, ct);

    /// <summary>Stage 79：v5.5 image flow 補完 — per attachment max size MB（範圍守 [1, 20] / 對齊 Claude Code CLI + Claude API 5 MB per image 真實上限）。</summary>
    public Task<int> GetMaxAttachmentSizeMBAsync(CancellationToken ct = default)
        => GetIntInRangeAsync("Workflow:MaxAttachmentSizeMB", Defaults.MaxAttachmentSizeMB, 1, 20, ct);

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

    /// <summary>Stage 77：範圍守 [min, max] 變體（既有 GetIntAsync 只守 v &gt; 0 不夠 / 避免無腦 N=100）。</summary>
    private async Task<int> GetIntInRangeAsync(string key, int fallback, int min, int max, CancellationToken ct)
    {
        var raw = await appSettings.GetAsync(key, ct);
        if (int.TryParse(raw, out var v) && v >= min && v <= max) return v;
        return fallback;
    }
}
