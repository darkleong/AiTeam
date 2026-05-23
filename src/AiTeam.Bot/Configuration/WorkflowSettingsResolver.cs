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
    AppSettingsService appSettings,
    ILogger<WorkflowSettingsResolver> logger)
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

    /// <summary>Stage 80：HITL plan confirmation 閘門 feature flag。預設 false（守 v5.5 baseline auto dispatch / 0 行為改變）。
    /// 切 true 後 PetraOrchestratorService.StartAsync 在 DecideTalentsWithPlanAsync 完成後開 BossInteraction plan_confirm 卡 / 等 Christ 4 decision pattern 拍板。
    /// AppSettings 表 key = "Workflow:UseHITLPlanConfirmation"，DB 優先，appsettings.json fallback。</summary>
    public Task<bool> GetUseHITLPlanConfirmationAsync(CancellationToken ct = default)
        => GetBoolAsync("Workflow:UseHITLPlanConfirmation", Defaults.UseHITLPlanConfirmation, ct);

    /// <summary>Stage 81：動態 replan + HITL retry gate feature flag。**真實生效需 UseHITLPlanConfirmation=true 為前置**
    /// （補強 #A 紀律 — ContinueChainFromSubtaskAsync 取 plan_confirm ContextJson 是 single source of truth）。
    /// 若 UseHITLPlanConfirmation=false → effective false（不論 DB value）+ warning log 提示。</summary>
    public async Task<bool> GetUseDynamicReplanningAsync(CancellationToken ct = default)
    {
        var rawDynamic = await GetBoolAsync("Workflow:UseDynamicReplanning", Defaults.UseDynamicReplanning, ct);
        if (!rawDynamic) return false;

        var hitlOn = await GetUseHITLPlanConfirmationAsync(ct);
        if (!hitlOn)
        {
            logger.LogWarning(
                "[Stage81] UseDynamicReplanning=true 但 UseHITLPlanConfirmation=false — dynamic replan 已 disabled 對齊 ContinueChainFromSubtaskAsync 設計依賴（plan_confirm ContextJson 是 single source of truth）");
            return false;
        }
        return true;
    }

    /// <summary>Stage 81：max replan iterations cap（範圍 [1, 10] / 超出 fallback default）。</summary>
    public Task<int> GetMaxReplanIterationsAsync(CancellationToken ct = default)
        => GetIntInRangeAsync("Workflow:MaxReplanIterations", Defaults.MaxReplanIterations, 1, 10, ct);

    /// <summary>Stage 81：replan session cost cap USD（須 > 0 / 超出 fallback default）。</summary>
    public Task<decimal> GetReplanCostCapUsdAsync(CancellationToken ct = default)
        => GetDecimalAsync("Workflow:ReplanCostCapUsd", Defaults.ReplanCostCapUsd, ct);

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

    /// <summary>Stage 81：decimal 值守 > 0（cost cap USD 用 / 對齊 numeric(18,6) 精度）。</summary>
    private async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct)
    {
        var raw = await appSettings.GetAsync(key, ct);
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Number,
                                System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0m
            ? v : fallback;
    }
}
