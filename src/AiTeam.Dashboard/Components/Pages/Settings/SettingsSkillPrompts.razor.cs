using AiTeam.Data;
using AiTeam.Data.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Settings;

/// <summary>
/// Stage 87 B0：Settings 拆 6 sub-page 之 SkillPrompts（取代 SettingsHub.razor Tab 5）。
/// Stage 87 follow-up #10：砍「重新載入」button / 改 SignalR ReceiveTaskUpdate 訂閱自動更新。
/// </summary>
public partial class SettingsSkillPrompts : IAsyncDisposable
{
    #region Dependencies

    [Inject] private IDbContextFactory<AppDbContext> DbFactory     { get; set; } = null!;
    [Inject] private ISnackbar                       Snackbar      { get; set; } = null!;
    [Inject] private NavigationManager               Navigation    { get; set; } = null!;
    [Inject] private IConfiguration                  Configuration { get; set; } = null!;
    [Inject] private ILogger<SettingsSkillPrompts>   Logger        { get; set; } = null!;

    private HubConnection? _hubConnection;

    #endregion

    private List<SkillPromptRow> _skillPromptsActive = [];
    private SkillPromptRow? _editingSkillPrompt;
    private string          _editingPromptBody = "";

    protected override async Task OnInitializedAsync()
    {
        await LoadSkillPromptsAsync();
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
            await LoadSkillPromptsAsync();
            await InvokeAsync(StateHasChanged);
        });

        try { await _hubConnection.StartAsync(); }
        catch (Exception ex) { Logger.LogError(ex, "SettingsSkillPrompts SignalR Hub 連線失敗"); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }

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
