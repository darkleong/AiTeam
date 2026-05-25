using AiTeam.Data;
using AiTeam.Data.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Settings;

/// <summary>
/// Stage 87 B0：Settings 拆 6 sub-page 之 TalentPrompts（取代 SettingsHub.razor Tab 6）。
/// Stage 87 follow-up #10：砍「重新載入」button / 改 SignalR ReceiveTaskUpdate 訂閱自動更新。
/// </summary>
public partial class SettingsTalentPrompts : IAsyncDisposable
{
    #region Dependencies

    [Inject] private IDbContextFactory<AppDbContext> DbFactory     { get; set; } = null!;
    [Inject] private ISnackbar                       Snackbar      { get; set; } = null!;
    [Inject] private NavigationManager               Navigation    { get; set; } = null!;
    [Inject] private IConfiguration                  Configuration { get; set; } = null!;
    [Inject] private ILogger<SettingsTalentPrompts>  Logger        { get; set; } = null!;

    private HubConnection? _hubConnection;

    #endregion

    private List<TalentInfoRow>   _talents             = [];
    private List<TalentPromptRow> _talentPromptsActive = [];

    private Guid?  _editingTalentPromptTalentId;
    private string _editingTalentPromptTalentName = "";
    private string _editingPersonaBody = "";

    protected override async Task OnInitializedAsync()
    {
        await LoadAllAsync();
        await ConnectSignalRAsync();
    }

    private async Task ConnectSignalRAsync()
    {
        var hubBaseUrl = Configuration["Dashboard:HubBaseUrl"];
        var hubUrl = string.IsNullOrEmpty(hubBaseUrl)
            ? Navigation.ToAbsoluteUri("/hubs/agent-status").ToString()
            : $"{hubBaseUrl.TrimEnd('/')}/hubs/agent-status";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<object>(AgentStatusHub.ReceiveTaskUpdate, async _ =>
        {
            await LoadAllAsync();
            await InvokeAsync(StateHasChanged);
        });

        try { await _hubConnection.StartAsync(); }
        catch (Exception ex) { Logger.LogError(ex, "SettingsTalentPrompts SignalR Hub 連線失敗"); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }

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
