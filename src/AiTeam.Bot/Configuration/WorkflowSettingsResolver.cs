using AiTeam.Bot.Services;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Configuration;

/// <summary>
/// 以「AppSettings 優先、appsettings.json fallback」方式讀取 WorkflowSettings flag / 數值。
///
/// v5.5 active flag list（Stage 85 砍 v4 dead flag 11 個後剩餘）：
///   Workflow:UsePetraOrchestratorV5
///   Workflow:UseV5Memory / V5MemoryCompactThresholdPercent / V5MemoryCompactKeepCount
///   Workflow:UseV5SubtaskPlanning
///   Workflow:UseV5PromptDb
///   Workflow:MaxConcurrentPetra
///   Workflow:MaxAttachmentsPerTask / MaxAttachmentSizeMB
///   Workflow:UseHITLPlanConfirmation
///   Workflow:UseDynamicReplanning / MaxReplanIterations / ReplanCostCapUsd
///   Workflow:PausedSessionTimeoutHours（Stage 85 新增）
///
/// AppSettings 無值或解析失敗時 fallback 到 IOptions&lt;WorkflowSettings&gt;（讀 appsettings.json）。
/// </summary>
public class WorkflowSettingsResolver(
    IOptions<WorkflowSettings> options,
    AppSettingsService appSettings,
    ILogger<WorkflowSettingsResolver> logger)
{
    private WorkflowSettings Defaults => options.Value;

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
