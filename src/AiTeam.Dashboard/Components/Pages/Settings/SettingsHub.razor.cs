using AiTeam.Data;
using AiTeam.Dashboard.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Settings;

/// <summary>
/// Stage 83 子項 3：Settings 分區主頁 — MudTabs 8 subtab 整合（補做版）。
///
/// 議題 C1：WorkflowFlags 完整實裝（21 Workflow:* flag）+ Restart Bot 按鈕。21 flag 全 require restart
/// （AppSettingsService startup read-once / 5 分鐘 re-read 未實裝 → FF C2 候選 Stage 84+）。
///
/// Stage 83 補做（Aria gate1 plan ↔ delivery gap 收口）：
/// - Tab 2 TokenGuard + 一般系統設定 → reuse `&lt;SystemSettings /&gt;` page component（inline 整合 / 不是 link out）
/// - Tab 3 Agents → reuse `&lt;AgentSettings /&gt;` component
/// - Tab 4 Talents → full CRUD（Stage 67 v5.5 Phase 1 Talent + TalentSkill 多對多）
/// - Tab 5 SkillPrompts → full CRUD（Stage 72 v5.5 Phase 2 版本管理 / IsActive 切 / 不刪舊版本）
/// - Tab 6 TalentPrompts → full CRUD（per-Talent persona / baseline 0 row / Phase 3 補）
/// - Tab 7 Rules + Projects → reuse `&lt;RuleManagement /&gt; + &lt;ProjectManagement /&gt;` components
/// - Tab 8 MockMode → reuse `&lt;SystemSettings /&gt;`（內含 MockMode toggle + Delay 範圍 section）
///
/// 對齊「component reuse 算 inline 整合 / 不是 link out 既有 page 路徑 button」紀律。
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

    #region Private State — WorkflowFlags（議題 C1）

    private Dictionary<string, string?> _flagValues = new();
    private bool   _pendingRestart;
    private bool   _isRestarting;
    private string? _saveMessage;

    // Stage 85：_v4FrameworkFlags 5 個整段砍（v4 dead / Stage 78b-c 砍 v4 path 後 dead flag 殘留）

    private static readonly string[] _v5Flags =
    [
        "Workflow:UsePetraOrchestratorV5",
        "Workflow:UseV5Memory",
        "Workflow:UseV5SubtaskPlanning",
        "Workflow:UseV5PromptDb",
        "Workflow:UseHITLPlanConfirmation",
        "Workflow:UseDynamicReplanning",
    ];

    private static readonly (string Key, int Min, int Max)[] _numericFlags =
    [
        // Stage 85：5 個 v4 round flag 砍（ReviewAppealMaxRounds / QaFixMaxRounds / DevPlanAppealMaxRounds / KickoffMaxRounds / DesignMeetingMaxRounds — v4 dead caller 砍後）
        ("Workflow:V5MemoryCompactThresholdPercent", 30, 95),
        ("Workflow:V5MemoryCompactKeepCount",       10, 200),
        ("Workflow:MaxConcurrentPetra",             1, 10),
        ("Workflow:MaxAttachmentsPerTask",          1, 20),
        ("Workflow:MaxAttachmentSizeMB",            1, 100),
        ("Workflow:MaxReplanIterations",            1, 10),
    ];

    #endregion

    #region Private State — Talents / SkillPrompts / TalentPrompts CRUD（補做）

    // Stage 78a：4 Final Skill hardcode（對齊 DefaultSkillRegistry）— code-defined 不開放動態加
    private static readonly string[] _allSkillNames =
    [
        "code_implementation",
        "code_review",
        "qa_testing",
        "documentation",
    ];

    private List<TalentRow>        _talents              = [];
    private List<SkillPromptRow>   _skillPromptsActive   = [];
    private List<TalentPromptRow>  _talentPromptsActive  = [];

    // Talent CRUD state
    private bool   _newTalentOpen;
    private string _newTalentName        = "";
    private string _newTalentDisplayName = "";
    private string _newTalentDescription = "";
    private TalentRow? _editingTalent;

    // SkillPrompt edit state
    private SkillPromptRow? _editingSkillPrompt;
    private string          _editingPromptBody = "";

    // TalentPrompt edit state
    private Guid?  _editingTalentPromptTalentId;
    private string _editingTalentPromptTalentName = "";
    private string _editingPersonaBody = "";

    #endregion

    protected override async Task OnInitializedAsync()
    {
        await LoadFlagsAsync();
        await LoadTalentsAsync();
        await LoadSkillPromptsAsync();
        await LoadTalentPromptsAsync();
    }

    private async Task LoadFlagsAsync()
    {
        try
        {
            var allKeys = _v5Flags.Concat(_numericFlags.Select(n => n.Key)).ToArray();
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

    #region Flag 描述

    // Stage 86 子項 1 follow-up：UI 顯示文字砍 v5/v5.5/Stage 編號（純功能描述 / 對齊使用者文檔不該帶開發歷史脈絡紀律）
    private static string GetFlagDescription(string key) => key switch
    {
        "Workflow:UsePetraOrchestratorV5"          => "動態 orchestrator（Petra 拆 SubtaskPlan + 派工）",
        "Workflow:UseV5Memory"                     => "跨 Session 長期記憶",
        "Workflow:UseV5SubtaskPlanning"            => "動態 SubtaskPlan 拆解",
        "Workflow:UseV5PromptDb"                   => "Prompt DB 化（SkillPrompt / TalentPrompt 動態管理）",
        "Workflow:UseHITLPlanConfirmation"         => "plan_confirm HITL gate（老闆審 SubtaskPlan 才執行）",
        "Workflow:UseDynamicReplanning"            => "動態 replan + replan_confirm HITL retry gate",
        "Workflow:V5MemoryCompactThresholdPercent" => "記憶 compact 觸發 % 閾值",
        "Workflow:V5MemoryCompactKeepCount"        => "記憶 compact 保留筆數",
        "Workflow:MaxConcurrentPetra"              => "並行 Petra 消費者數",
        "Workflow:MaxAttachmentsPerTask"           => "per task 附件上限",
        "Workflow:MaxAttachmentSizeMB"             => "per 附件 MB 上限",
        "Workflow:MaxReplanIterations"             => "動態 replan 最大 iteration 輪次",
        _                                          => "（無描述）",
    };

    #endregion

    #region Talent CRUD（Stage 67 v5.5 Phase 1）

    private async Task LoadTalentsAsync()
    {
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            _talents = await db.Talents
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new TalentRow
                {
                    Id          = t.Id,
                    Name        = t.Name,
                    DisplayName = t.DisplayName,
                    Description = t.Description,
                    Provider    = t.Provider,
                    Model       = t.Model,
                    IsActive    = t.IsActive,
                    Skills = db.TalentSkills
                        .Where(ts => ts.TalentId == t.Id)
                        .Select(ts => new TalentSkillRow
                        {
                            SkillName = ts.SkillName,
                            IsPrimary = ts.IsPrimary,
                            Priority  = ts.Priority,
                        })
                        .ToList(),
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "LoadTalentsAsync 失敗");
        }
    }

    private async Task CreateTalentAsync()
    {
        if (string.IsNullOrWhiteSpace(_newTalentName))
        {
            Snackbar.Add("Name 不能為空", Severity.Warning);
            return;
        }
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            var t = new Talent
            {
                Name        = _newTalentName.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(_newTalentDisplayName) ? _newTalentName.Trim() : _newTalentDisplayName.Trim(),
                Description = _newTalentDescription.Trim(),
                IsActive    = true,
            };
            db.Talents.Add(t);
            await db.SaveChangesAsync();
            Snackbar.Add($"Talent「{t.Name}」已新增", Severity.Success);
            _newTalentOpen = false;
            _newTalentName = _newTalentDisplayName = _newTalentDescription = "";
            await LoadTalentsAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"新增 Talent 失敗：{ex.Message}", Severity.Error);
        }
    }

    private async Task ToggleTalentActiveAsync(TalentRow row, bool newValue)
    {
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            var t = await db.Talents.FindAsync(row.Id);
            if (t is null) return;
            t.IsActive  = newValue;
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            row.IsActive = newValue;
            Snackbar.Add($"Talent「{row.Name}」已{(newValue ? "啟用" : "停用")}", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"更新失敗：{ex.Message}", Severity.Error);
        }
    }

    private Task OpenTalentSkillsAsync(TalentRow row)
    {
        _editingTalent = row;
        return Task.CompletedTask;
    }

    private async Task ToggleSkillAssignmentAsync(TalentRow talent, string skillName, bool assign)
    {
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            if (assign)
            {
                var exists = await db.TalentSkills
                    .AnyAsync(ts => ts.TalentId == talent.Id && ts.SkillName == skillName);
                if (!exists)
                {
                    db.TalentSkills.Add(new TalentSkill
                    {
                        TalentId  = talent.Id,
                        SkillName = skillName,
                        IsPrimary = false,
                        Priority  = 0,
                    });
                    await db.SaveChangesAsync();
                    talent.Skills.Add(new TalentSkillRow { SkillName = skillName, IsPrimary = false, Priority = 0 });
                }
            }
            else
            {
                var existing = await db.TalentSkills
                    .FirstOrDefaultAsync(ts => ts.TalentId == talent.Id && ts.SkillName == skillName);
                if (existing is not null)
                {
                    db.TalentSkills.Remove(existing);
                    await db.SaveChangesAsync();
                    talent.Skills.RemoveAll(s => s.SkillName == skillName);
                }
            }
            Snackbar.Add($"{talent.Name} {skillName} 已{(assign ? "指派" : "移除")}", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"更新 Skill assignment 失敗：{ex.Message}", Severity.Error);
        }
    }

    private async Task SetSkillPrimaryAsync(TalentRow talent, string skillName, bool isPrimary)
    {
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            var ts = await db.TalentSkills
                .FirstOrDefaultAsync(x => x.TalentId == talent.Id && x.SkillName == skillName);
            if (ts is null) return;
            ts.IsPrimary = isPrimary;
            await db.SaveChangesAsync();
            var row = talent.Skills.FirstOrDefault(s => s.SkillName == skillName);
            if (row is not null) row.IsPrimary = isPrimary;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"更新 IsPrimary 失敗：{ex.Message}", Severity.Error);
        }
    }

    #endregion

    #region SkillPrompt CRUD（Stage 72 版本管理）

    private async Task LoadSkillPromptsAsync()
    {
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            _skillPromptsActive = await db.SkillPrompts
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.SkillName)
                .Select(p => new SkillPromptRow
                {
                    Id            = p.Id,
                    SkillName     = p.SkillName,
                    PromptBody    = p.PromptBody,
                    VersionNumber = p.VersionNumber,
                    UpdatedAt     = p.UpdatedAt,
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "LoadSkillPromptsAsync 失敗");
        }
    }

    private void OpenSkillPromptEditAsync(SkillPromptRow row)
    {
        _editingSkillPrompt = row;
        _editingPromptBody  = row.PromptBody;
    }

    private async Task SaveNewSkillPromptVersionAsync()
    {
        if (_editingSkillPrompt is null) return;
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            // 1. 找舊 active 切 IsActive=false
            var oldActives = await db.SkillPrompts
                .Where(p => p.SkillName == _editingSkillPrompt.SkillName && p.IsActive)
                .ToListAsync();
            foreach (var old in oldActives)
            {
                old.IsActive  = false;
                old.UpdatedAt = DateTime.UtcNow;
            }
            // 2. 新版本 VersionNumber +1 + IsActive=true
            var newRow = new SkillPrompt
            {
                SkillName     = _editingSkillPrompt.SkillName,
                PromptBody    = _editingPromptBody,
                VersionNumber = _editingSkillPrompt.VersionNumber + 1,
                IsActive      = true,
            };
            db.SkillPrompts.Add(newRow);
            await db.SaveChangesAsync();
            Snackbar.Add($"SkillPrompt「{_editingSkillPrompt.SkillName}」v{newRow.VersionNumber} 已儲存（舊 v{_editingSkillPrompt.VersionNumber} 保留 audit）", Severity.Success);
            _editingSkillPrompt = null;
            _editingPromptBody  = "";
            await LoadSkillPromptsAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"儲存失敗：{ex.Message}", Severity.Error);
        }
    }

    #endregion

    #region TalentPrompt CRUD（Stage 72 per-Talent persona / baseline 0 row）

    private async Task LoadTalentPromptsAsync()
    {
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            _talentPromptsActive = await db.TalentPrompts
                .AsNoTracking()
                .Where(p => p.IsActive)
                .Select(p => new TalentPromptRow
                {
                    Id            = p.Id,
                    TalentId      = p.TalentId,
                    PersonaBody   = p.PersonaBody,
                    VersionNumber = p.VersionNumber,
                    UpdatedAt     = p.UpdatedAt,
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "LoadTalentPromptsAsync 失敗");
        }
    }

    private void OpenTalentPromptEditAsync(Guid talentId, string talentName, TalentPromptRow? existing)
    {
        _editingTalentPromptTalentId   = talentId;
        _editingTalentPromptTalentName = talentName;
        _editingPersonaBody            = existing?.PersonaBody ?? "";
    }

    private async Task SaveNewTalentPromptVersionAsync()
    {
        if (_editingTalentPromptTalentId is null) return;
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            var talentId = _editingTalentPromptTalentId.Value;
            // 1. 切舊 active IsActive=false
            var oldActives = await db.TalentPrompts
                .Where(p => p.TalentId == talentId && p.IsActive)
                .ToListAsync();
            var nextVersion = oldActives.Count == 0 ? 1 : oldActives.Max(p => p.VersionNumber) + 1;
            foreach (var old in oldActives)
            {
                old.IsActive  = false;
                old.UpdatedAt = DateTime.UtcNow;
            }
            // 2. 新版本
            db.TalentPrompts.Add(new TalentPrompt
            {
                TalentId      = talentId,
                PersonaBody   = _editingPersonaBody,
                VersionNumber = nextVersion,
                IsActive      = true,
            });
            await db.SaveChangesAsync();
            Snackbar.Add($"TalentPrompt「{_editingTalentPromptTalentName}」v{nextVersion} 已儲存", Severity.Success);
            _editingTalentPromptTalentId   = null;
            _editingTalentPromptTalentName = "";
            _editingPersonaBody            = "";
            await LoadTalentPromptsAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"儲存失敗：{ex.Message}", Severity.Error);
        }
    }

    #endregion

    #region Row records

    public class TalentRow
    {
        public Guid    Id          { get; set; }
        public string  Name        { get; set; } = "";
        public string  DisplayName { get; set; } = "";
        public string  Description { get; set; } = "";
        public string? Provider    { get; set; }
        public string? Model       { get; set; }
        public bool    IsActive    { get; set; }
        public List<TalentSkillRow> Skills { get; set; } = [];
    }

    public class TalentSkillRow
    {
        public string SkillName { get; set; } = "";
        public bool   IsPrimary { get; set; }
        public int    Priority  { get; set; }
    }

    public class SkillPromptRow
    {
        public Guid     Id            { get; set; }
        public string   SkillName     { get; set; } = "";
        public string   PromptBody    { get; set; } = "";
        public int      VersionNumber { get; set; }
        public DateTime UpdatedAt     { get; set; }
    }

    public class TalentPromptRow
    {
        public Guid     Id            { get; set; }
        public Guid     TalentId      { get; set; }
        public string   PersonaBody   { get; set; } = "";
        public int      VersionNumber { get; set; }
        public DateTime UpdatedAt     { get; set; }
    }

    #endregion
}
