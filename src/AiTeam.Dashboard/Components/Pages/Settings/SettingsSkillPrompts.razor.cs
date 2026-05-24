using AiTeam.Data;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Settings;

/// <summary>
/// Stage 87 B0：Settings 拆 6 sub-page 之 SkillPrompts（取代 SettingsHub.razor Tab 5）。
/// 無 SignalR 訂閱（純 DB 操作）/ Stage 72 v5.5 Phase 2 SkillPrompt 版本管理（IsActive 切 / 不刪舊版本）。
/// </summary>
public partial class SettingsSkillPrompts
{
    #region Dependencies

    [Inject] private IDbContextFactory<AppDbContext> DbFactory   { get; set; } = null!;
    [Inject] private ISnackbar                       Snackbar    { get; set; } = null!;
    [Inject] private ILogger<SettingsSkillPrompts>   Logger      { get; set; } = null!;

    #endregion

    private List<SkillPromptRow> _skillPromptsActive = [];
    private SkillPromptRow? _editingSkillPrompt;
    private string          _editingPromptBody = "";

    protected override async Task OnInitializedAsync() => await LoadSkillPromptsAsync();

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
            var oldActives = await db.SkillPrompts
                .Where(p => p.SkillName == _editingSkillPrompt.SkillName && p.IsActive)
                .ToListAsync();
            foreach (var old in oldActives)
            {
                old.IsActive  = false;
                old.UpdatedAt = DateTime.UtcNow;
            }
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

    public class SkillPromptRow
    {
        public Guid     Id            { get; set; }
        public string   SkillName     { get; set; } = "";
        public string   PromptBody    { get; set; } = "";
        public int      VersionNumber { get; set; }
        public DateTime UpdatedAt     { get; set; }
    }
}
