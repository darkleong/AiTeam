using AiTeam.Data;
using AiTeam.Dashboard.Services;
using AiTeam.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Settings;

/// <summary>
/// Stage 87 B0：Settings 拆 6 sub-page 之 Talents（取代 SettingsHub.razor Tab 4）/ 含 Stage 87 A3 擴的 LLM 設定 + Token Limit。
/// 無 SignalR 訂閱（純 DB 操作 + Snackbar feedback）。
/// </summary>
public partial class SettingsTalents
{
    #region Dependencies

    [Inject] private IDbContextFactory<AppDbContext> DbFactory   { get; set; } = null!;
    [Inject] private DashboardAgentService           AgentSvc    { get; set; } = null!;
    [Inject] private DashboardBotService             BotSvc      { get; set; } = null!;
    [Inject] private ISnackbar                       Snackbar    { get; set; } = null!;
    [Inject] private ILogger<SettingsTalents>        Logger      { get; set; } = null!;

    #endregion

    // Stage 78a：4 Final Skill hardcode（對齊 DefaultSkillRegistry）— code-defined 不開放動態加
    private static readonly string[] _allSkillNames =
    [
        "code_implementation",
        "code_review",
        "qa_testing",
        "documentation",
    ];

    private List<TalentRow> _talents = [];

    // Talent CRUD state
    private bool   _newTalentOpen;
    private string _newTalentName        = "";
    private string _newTalentDisplayName = "";
    private string _newTalentDescription = "";
    private TalentRow? _editingTalent;

    // Stage 87 A3：Talent LLM 設定 edit state
    private TalentRow? _editingTalentLlm;
    private string?    _llmEditProvider;
    private string?    _llmEditModel;
    private int?       _llmEditDailyK;
    private int?       _llmEditMonthlyK;
    private bool       _isSavingLlm;

    protected override async Task OnInitializedAsync() => await LoadTalentsAsync();

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
                    Id                 = t.Id,
                    Name               = t.Name,
                    DisplayName        = t.DisplayName,
                    Description        = t.Description,
                    Provider           = t.Provider,
                    Model              = t.Model,
                    DailyTokenLimitK   = t.DailyTokenLimitK,
                    MonthlyTokenLimitK = t.MonthlyTokenLimitK,
                    IsActive           = t.IsActive,
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

    // ─────────────────── Stage 87 A3：LLM 設定編輯 ───────────────────

    private void OpenTalentLlmEdit(TalentRow row)
    {
        _editingTalentLlm = row;
        _llmEditProvider  = row.Provider;
        _llmEditModel     = row.Model;
        _llmEditDailyK    = row.DailyTokenLimitK;
        _llmEditMonthlyK  = row.MonthlyTokenLimitK;
    }

    private void CloseTalentLlmEdit()
    {
        _editingTalentLlm = null;
        _llmEditProvider  = null;
        _llmEditModel     = null;
        _llmEditDailyK    = null;
        _llmEditMonthlyK  = null;
    }

    private void OnLlmProviderChanged(string newProvider)
    {
        _llmEditProvider = newProvider;
        var validModels = LlmModels.GetModelsForProvider(newProvider);
        if (string.IsNullOrEmpty(_llmEditModel) || !validModels.Contains(_llmEditModel))
        {
            _llmEditModel = null;
            Snackbar.Add($"Provider 已改為 {newProvider}，請選擇對應的 Model。", Severity.Warning);
        }
    }

    private async Task SaveTalentLlmAsync()
    {
        if (_editingTalentLlm is null || _isSavingLlm) return;
        if (string.IsNullOrEmpty(_llmEditProvider) || string.IsNullOrEmpty(_llmEditModel))
        {
            Snackbar.Add("Provider / Model 不能為空", Severity.Warning);
            return;
        }

        _isSavingLlm = true;
        try
        {
            var ok1 = await AgentSvc.UpdateTalentProviderModelAsync(
                _editingTalentLlm.Id, _llmEditProvider, _llmEditModel);
            if (!ok1)
            {
                Snackbar.Add($"{_editingTalentLlm.Name} Provider/Model 儲存失敗：查無 Talent", Severity.Error);
                return;
            }

            var ok2 = await AgentSvc.UpdateTalentTokenLimitsAsync(
                _editingTalentLlm.Id, _llmEditDailyK, _llmEditMonthlyK);
            if (!ok2)
            {
                Snackbar.Add($"{_editingTalentLlm.Name} Token Limit 儲存失敗", Severity.Error);
                return;
            }

            await BotSvc.ReloadCacheAsync("agent-config");

            _editingTalentLlm.Provider           = _llmEditProvider;
            _editingTalentLlm.Model              = _llmEditModel;
            _editingTalentLlm.DailyTokenLimitK   = _llmEditDailyK   > 0 ? _llmEditDailyK   : null;
            _editingTalentLlm.MonthlyTokenLimitK = _llmEditMonthlyK > 0 ? _llmEditMonthlyK : null;

            Snackbar.Add(
                $"{_editingTalentLlm.Name}：Provider={_llmEditProvider} / Model={_llmEditModel} / 日限={(_llmEditDailyK > 0 ? $"{_llmEditDailyK}K" : "未設定")} / 月限={(_llmEditMonthlyK > 0 ? $"{_llmEditMonthlyK}K" : "未設定")} 已更新，Bot Cache 已刷新。",
                Severity.Success);

            CloseTalentLlmEdit();
        }
        catch (ArgumentException ex)
        {
            Snackbar.Add($"儲存失敗：{ex.Message}", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"儲存失敗：{ex.Message}", Severity.Error);
        }
        finally
        {
            _isSavingLlm = false;
        }
    }

    public class TalentRow
    {
        public Guid    Id          { get; set; }
        public string  Name        { get; set; } = "";
        public string  DisplayName { get; set; } = "";
        public string  Description { get; set; } = "";
        public string? Provider    { get; set; }
        public string? Model       { get; set; }
        public int?    DailyTokenLimitK   { get; set; }
        public int?    MonthlyTokenLimitK { get; set; }
        public bool    IsActive    { get; set; }
        public List<TalentSkillRow> Skills { get; set; } = [];
    }

    public class TalentSkillRow
    {
        public string SkillName { get; set; } = "";
        public bool   IsPrimary { get; set; }
        public int    Priority  { get; set; }
    }
}
