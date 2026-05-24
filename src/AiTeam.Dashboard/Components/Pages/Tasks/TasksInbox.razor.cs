using System.Text.Json;
using AiTeam.Data;
using AiTeam.Data.Hubs;
using AiTeam.Data.Repositories;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Tasks;

/// <summary>
/// Stage 87 B0：Tasks 拆 4 sub-page 之 PetraInbox 收件（取代 TaskHub.razor Tab 3）。
/// Stage 87 B1：SignalR 只訂閱 ReceiveQueueUpdate（PetraInbox enqueue/dequeue）。
/// </summary>
public partial class TasksInbox : IAsyncDisposable
{
    #region Dependencies

    [Inject] private IServiceScopeFactory  ScopeFactory  { get; set; } = null!;
    [Inject] private IDialogService        DialogService { get; set; } = null!;
    [Inject] private ISnackbar             Snackbar      { get; set; } = null!;
    [Inject] private NavigationManager     Navigation    { get; set; } = null!;
    [Inject] private IConfiguration        Configuration { get; set; } = null!;
    [Inject] private ILogger<TasksInbox>   Logger        { get; set; } = null!;

    #endregion

    private List<PetraInbox> _inboxRows = [];
    private bool _isLoading = true;
    private HubConnection? _hubConnection;
    private bool _hubConnected;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await ConnectSignalRAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            await using var scope = ScopeFactory.CreateAsyncScope();
            var inboxRepo = scope.ServiceProvider.GetRequiredService<PetraInboxRepository>();
            _inboxRows = await inboxRepo.GetRecentAsync(limit: 50, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "TasksInbox LoadAsync 失敗");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task HandleRequeueInboxAsync(Guid rowId)
    {
        try
        {
            bool success;
            await using (var scope = ScopeFactory.CreateAsyncScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<PetraInboxRepository>();
                var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                success  = await repo.RequeueAsync(rowId, CancellationToken.None);
                if (success) await db.SaveChangesAsync(CancellationToken.None);
            }
            if (success)
            {
                Snackbar.Add("已重新排隊，PetraInboxProcessor 下次 polling tick 將自動接手。", Severity.Success);
                await LoadAsync();
                await InvokeAsync(StateHasChanged);
            }
            else
            {
                Snackbar.Add("重跑失敗：row 不存在或狀態非 failed/dead。", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"重跑時發生錯誤：{ex.Message}", Severity.Error);
        }
    }

    private async Task OpenAttachmentDialogAsync(string? attachmentsJson)
    {
        var parameters = new DialogParameters<AttachmentPreviewDialog>
        {
            { d => d.AttachmentsJson, attachmentsJson },
        };
        await DialogService.ShowAsync<AttachmentPreviewDialog>("附檔預覽", parameters,
            new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true });
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

        _hubConnection.On(AgentStatusHub.ReceiveQueueUpdate, async () =>
        {
            await LoadAsync();
            await InvokeAsync(StateHasChanged);
        });

        _hubConnection.Closed     += _ => { _hubConnected = false; return InvokeAsync(StateHasChanged); };
        _hubConnection.Reconnected += _ => { _hubConnected = true;  return InvokeAsync(StateHasChanged); };

        try
        {
            await _hubConnection.StartAsync();
            _hubConnected = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "TasksInbox SignalR Hub 連線失敗");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }

    private static int CountAttachments(string? attachmentsJson)
    {
        if (string.IsNullOrWhiteSpace(attachmentsJson)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(attachmentsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                return doc.RootElement.GetArrayLength();
        }
        catch { /* malformed JSON tolerated — 0 */ }
        return 0;
    }
}
