using AiTeam.Data;
using AiTeam.Dashboard.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Settings;

/// <summary>
/// Stage 83 子項 3：Settings 分區主頁 — MudTabs 8 subtab 整合
/// （WorkflowFlags / TokenGuard / Agents / Talents / SkillPrompts / TalentPrompts / Rules+Projects / MockMode）。
///
/// 議題 C1：WorkflowFlags 完整實裝（21 Workflow:* flag toggle + 數值 + Restart Bot 按鈕）—
/// 21 flag 全 require restart（AppSettingsService startup read-once / 5 分鐘 re-read 未實裝 → FF C2 候選 Stage 84+）。
///
/// Phased delivery 紀律（Forge implementation 階段揭 trade-off）：
/// - WorkflowFlags = 完整新建（核心議題）
/// - 其他 7 tab = 暫保 link to 既有 page button + entity summary 簡單 list（子項 5 整合或保留舊 page）
///   原因：5 既有頁總 1170 行邏輯 / 全 inline migrate 進 SettingsHub 規模超 L+++ context budget /
///   對齊「最後測驗」精神 + 「不重做能用的」紀律延伸（既有 page 留 active / NavMenu 主入口 4 link）
/// </summary>
public partial class SettingsHub
{
    #region Dependencies

    [Inject] private IDbContextFactory<AppDbContext> DbFactory   { get; set; } = null!;
    [Inject] private DashboardAppSettingsService     SettingsSvc { get; set; } = null!;
    [Inject] private DashboardBotService             BotSvc      { get; set; } = null!;
    [Inject] private ISnackbar                       Snackbar    { get; set; } = null!;
    [Inject] private NavigationManager               Nav         { get; set; } = null!;
    [Inject] private ILogger<SettingsHub>            Logger      { get; set; } = null!;

    #endregion

    #region Private State

    private Dictionary<string, string?> _flagValues = new();
    private List<TalentSummary>  _talents       = [];
    private List<PromptSummary>  _skillPrompts  = [];
    private List<PromptSummary>  _talentPrompts = [];
    private bool   _pendingRestart;
    private bool   _isRestarting;
    private string? _saveMessage;

    // 議題 C1：21 flag 分組
    private static readonly string[] _v4FrameworkFlags =
    [
        "Workflow:UseFrameworkAppealLoop",
        "Workflow:UseFrameworkKickoff",
        "Workflow:UseFrameworkKickoffMidInterrupt",
        "Workflow:UseFrameworkDesign",
        "Workflow:UseFrameworkPipeline",
    ];

    private static readonly string[] _v5Flags =
    [
        "Workflow:UsePetraOrchestratorV5",
        "Workflow:UseTalentSkillSeparation",
        "Workflow:UseV5Memory",
        "Workflow:UseV5SubtaskPlanning",
        "Workflow:UseV5PromptDb",
        "Workflow:UseHITLPlanConfirmation",
        "Workflow:UseDynamicReplanning",
    ];

    private static readonly (string Key, int Min, int Max)[] _numericFlags =
    [
        ("Workflow:ReviewAppealMaxRounds",          1, 10),
        ("Workflow:QaFixMaxRounds",                 1, 10),
        ("Workflow:DevPlanAppealMaxRounds",         1, 10),
        ("Workflow:KickoffMaxRounds",               1, 10),
        ("Workflow:DesignMeetingMaxRounds",         1, 10),
        ("Workflow:V5MemoryCompactThresholdPercent", 30, 95),
        ("Workflow:V5MemoryCompactKeepCount",       10, 200),
        ("Workflow:MaxConcurrentPetra",             1, 10),
        ("Workflow:MaxAttachmentsPerTask",          1, 20),
        ("Workflow:MaxAttachmentSizeMB",            1, 100),
        ("Workflow:MaxReplanIterations",            1, 10),
    ];

    #endregion

    protected override async Task OnInitializedAsync()
    {
        await LoadFlagsAsync();
        await LoadSummariesAsync();
    }

    private async Task LoadFlagsAsync()
    {
        try
        {
            var allKeys = _v4FrameworkFlags.Concat(_v5Flags).Concat(_numericFlags.Select(n => n.Key)).ToArray();
            _flagValues = new Dictionary<string, string?>();
            foreach (var k in allKeys)
            {
                var row = await SettingsSvc.GetAsync(k);
                _flagValues[k] = row?.Value;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsHub LoadFlagsAsync 失敗");
        }
    }

    private async Task LoadSummariesAsync()
    {
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();

            _talents = await db.Talents
                .AsNoTracking()
                .Select(t => new TalentSummary
                {
                    Name       = t.Name,
                    IsActive   = t.IsActive,
                    SkillCount = db.TalentSkills.Count(ts => ts.TalentId == t.Id),
                })
                .ToListAsync();

            _skillPrompts = await db.SkillPrompts
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.SkillName)
                .Select(p => new PromptSummary
                {
                    Name          = p.SkillName,
                    Version       = p.VersionNumber,
                    ContentLength = p.PromptBody.Length,
                })
                .ToListAsync();

            _talentPrompts = await db.TalentPrompts
                .AsNoTracking()
                .Where(p => p.IsActive)
                .Join(db.Talents, p => p.TalentId, t => t.Id, (p, t) => new PromptSummary
                {
                    Name          = t.Name,
                    Version       = p.VersionNumber,
                    ContentLength = p.PersonaBody.Length,
                })
                .OrderBy(p => p.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsHub LoadSummariesAsync 失敗");
        }
    }

    #region 議題 C1：Flag Get/Set + Restart Bot

    private bool GetBoolFlag(string key)
        => bool.TryParse(_flagValues.GetValueOrDefault(key), out var v) && v;

    private int GetIntFlag(string key)
        => int.TryParse(_flagValues.GetValueOrDefault(key), out var v) ? v : 0;

    private async Task SetBoolFlagAsync(string key, bool value)
    {
        try
        {
            await SettingsSvc.UpsertAsync(key, value.ToString().ToLowerInvariant());
            _flagValues[key] = value.ToString().ToLowerInvariant();
            _pendingRestart = true;
            _saveMessage = $"{key} = {value}（需重啟 Bot 才生效）";
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Flag 寫入失敗：{ex.Message}", Severity.Error);
        }
    }

    private async Task SetIntFlagAsync(string key, int value)
    {
        try
        {
            await SettingsSvc.UpsertAsync(key, value.ToString());
            _flagValues[key] = value.ToString();
            _pendingRestart = true;
            _saveMessage = $"{key} = {value}（需重啟 Bot 才生效）";
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Flag 寫入失敗：{ex.Message}", Severity.Error);
        }
    }

    private async Task RestartBotAsync()
    {
        _isRestarting = true;
        try
        {
            await BotSvc.RestartBotAsync();
            Snackbar.Add("已觸發 Bot 重啟，CI/CD self-hosted runner 接手部署中（約 30 秒-2 分鐘）。", Severity.Success);
            _pendingRestart = false;
            _saveMessage = null;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Restart Bot 失敗：{ex.Message}", Severity.Error);
        }
        finally
        {
            _isRestarting = false;
        }
    }

    #endregion

    #region Flag 描述（議題 C1 UX）

    private static string GetFlagDescription(string key) => key switch
    {
        "Workflow:UseFrameworkAppealLoop"          => "v4 Cody-Vera-Petra Appeal Loop 走 MS Agent Framework（Stage 49 首發）",
        "Workflow:UseFrameworkKickoff"             => "v4 Kickoff 5 Agent 並行會議走 Framework（Stage 50）",
        "Workflow:UseFrameworkKickoffMidInterrupt" => "v4 Kickoff HITL 中途介入試點（依賴 UseFrameworkKickoff）",
        "Workflow:UseFrameworkDesign"              => "v4 Design Meeting fan-out/fan-in 走 Framework（Stage 52）",
        "Workflow:UseFrameworkPipeline"            => "v4 macro-orchestration Pipeline 走 Framework（Stage 53A）",
        "Workflow:UsePetraOrchestratorV5"          => "v5 PoC 動態 orchestrator 上線（Stage 63B）",
        "Workflow:UseTalentSkillSeparation"        => "v5.5 Phase 1 Talent-Skill 拆分（Stage 67）",
        "Workflow:UseV5Memory"                     => "v5.5 Phase 2 跨 Session 長期記憶（Stage 69）",
        "Workflow:UseV5SubtaskPlanning"            => "v5.5 Phase 2 動態 SubtaskPlan 拆解",
        "Workflow:UseV5PromptDb"                   => "v5.5 Phase 2 Prompt DB 化（Stage 72）",
        "Workflow:UseHITLPlanConfirmation"         => "Stage 80 plan_confirm HITL gate",
        "Workflow:UseDynamicReplanning"            => "Stage 81 動態 replan + replan_confirm HITL retry gate",
        "Workflow:ReviewAppealMaxRounds"           => "Cody 反駁 Vera Review 最大輪次（escalate Petra 仲裁前）",
        "Workflow:QaFixMaxRounds"                  => "Petra 判 code_bug → Dev_fix + QA 重跑最大輪次",
        "Workflow:DevPlanAppealMaxRounds"          => "Cody 反駁 Petra Dev_plan 初審最大輪次",
        "Workflow:KickoffMaxRounds"                => "Kickoff 多 Agent 會議最大輪次",
        "Workflow:DesignMeetingMaxRounds"          => "Design 會議最大輪次",
        "Workflow:V5MemoryCompactThresholdPercent" => "v5 記憶 compact 觸發 % 閾值",
        "Workflow:V5MemoryCompactKeepCount"        => "v5 記憶 compact 保留筆數",
        "Workflow:MaxConcurrentPetra"              => "v5.5 並行 Petra 消費者數",
        "Workflow:MaxAttachmentsPerTask"           => "per task 附件上限（Stage 79）",
        "Workflow:MaxAttachmentSizeMB"             => "per 附件 MB 上限（Stage 79）",
        "Workflow:MaxReplanIterations"             => "動態 replan 最大 iteration 輪次（Stage 81 cap）",
        _                                          => "（無描述）",
    };

    #endregion

    public record TalentSummary
    {
        public string Name { get; init; } = "";
        public bool IsActive { get; init; }
        public int SkillCount { get; init; }
    }

    public record PromptSummary
    {
        public string Name { get; init; } = "";
        public int Version { get; init; }
        public int ContentLength { get; init; }
    }
}
