using AiTeam.Data;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Settings;

/// <summary>
/// Stage 87 B0：Settings 拆 6 sub-page 之 TalentPrompts（取代 SettingsHub.razor Tab 6）。
/// 無 SignalR 訂閱（純 DB 操作）/ Stage 72 v5.5 Phase 2 per-Talent persona / baseline 0 row。
/// </summary>
public partial class SettingsTalentPrompts
{
    #region Dependencies

    [Inject] private IDbContextFactory<AppDbContext> DbFactory   { get; set; } = null!;
    [Inject] private ISnackbar                       Snackbar    { get; set; } = null!;
    [Inject] private ILogger<SettingsTalentPrompts>  Logger      { get; set; } = null!;

    #endregion

    private List<TalentInfoRow>   _talents             = [];
    private List<TalentPromptRow> _talentPromptsActive = [];

    private Guid?  _editingTalentPromptTalentId;
    private string _editingTalentPromptTalentName = "";
    private string _editingPersonaBody = "";

    protected override async Task OnInitializedAsync() => await LoadAllAsync();

    private async Task LoadAllAsync()
    {
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            _talents = await db.Talents
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new TalentInfoRow
                {
                    Id   = t.Id,
                    Name = t.Name,
                })
                .ToListAsync();

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
            Logger.LogError(ex, "LoadAllAsync 失敗");
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
            var oldActives = await db.TalentPrompts
                .Where(p => p.TalentId == talentId && p.IsActive)
                .ToListAsync();
            var nextVersion = oldActives.Count == 0 ? 1 : oldActives.Max(p => p.VersionNumber) + 1;
            foreach (var old in oldActives)
            {
                old.IsActive  = false;
                old.UpdatedAt = DateTime.UtcNow;
            }
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
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"儲存失敗：{ex.Message}", Severity.Error);
        }
    }

    public class TalentInfoRow
    {
        public Guid   Id   { get; set; }
        public string Name { get; set; } = "";
    }

    public class TalentPromptRow
    {
        public Guid     Id            { get; set; }
        public Guid     TalentId      { get; set; }
        public string   PersonaBody   { get; set; } = "";
        public int      VersionNumber { get; set; }
        public DateTime UpdatedAt     { get; set; }
    }
}
