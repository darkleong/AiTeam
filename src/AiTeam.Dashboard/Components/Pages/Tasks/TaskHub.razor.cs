using System.Text.Json;
using AiTeam.Data;
using AiTeam.Data.Hubs;
using AiTeam.Data.Repositories;
using AiTeam.Dashboard.Components.Pages.Interactions;
using AiTeam.Dashboard.Services;
using AiTeam.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Tasks;

/// <summary>
/// Stage 83 子項 2：Tasks 分區主頁 — MudTabs 4 tab 整合
/// （HITL 確認卡片 / 進行中 Session / PetraInbox 收件 / 歷史）。
///
/// 議題 A1：HitlCardCenter 完整 reuse 既有 InteractionCard component（cover plan_confirm + replan_confirm 2 類完整 / 其他 3 類 generic fallback）
/// 議題 B1：ActiveSessions 點 row 開 drawer 顯示 PetraSessionMessages snippet + cost + replan iteration
/// 議題 D：接管既有 /tasks route（TaskCenter.razor 砍 / 對齊 Roadmap §子項 5 redirect 表）
///
/// SignalR 3 endpoint subscribe（ReceiveTaskUpdate / ReceiveQueueUpdate / ReceiveInteractionUpdate）—
/// 子項 6 重 wire 細分 subscribe 拓撲。
/// </summary>
public partial class TaskHub : IAsyncDisposable
{
    #region Dependencies

    [Inject] private DashboardInteractionQueryService TaskService     { get; set; } = null!;
    [Inject] private PetraSessionRepository    SessionRepo     { get; set; } = null!;
    [Inject] private InteractionRespondService RespondService  { get; set; } = null!;
    [Inject] private IServiceScopeFactory      ScopeFactory    { get; set; } = null!;
    [Inject] private ISnackbar                 Snackbar        { get; set; } = null!;
    [Inject] private NavigationManager         Navigation      { get; set; } = null!;
    [Inject] private IConfiguration            Configuration   { get; set; } = null!;
    [Inject] private ILogger<TaskHub>          Logger          { get; set; } = null!;

    #endregion

    #region Private State

    private List<BossInteractionDto> _pendingHitl     = [];
    private List<PetraSession>       _activeSessions  = [];
    private List<PetraSession>       _historySessions = [];
    private List<PetraInbox>         _inboxRows       = [];

    private bool          _isLoading = true;
    private bool          _isSessionDrawerOpen;
    private PetraSession? _selectedSession;

    private HubConnection? _hubConnection;
    private bool _hubConnected;

    #endregion

    protected override async Task OnInitializedAsync()
    {
        await LoadAllAsync();
        await ConnectSignalRAsync();
    }

    private async Task LoadAllAsync()
    {
        _isLoading = true;
        try
        {
            _pendingHitl     = await TaskService.GetPendingInteractionsAsync();
            _activeSessions  = await SessionRepo.GetActiveAsync(limit: 50);
            _historySessions = await SessionRepo.GetHistoryAsync(limit: 50);

            await using var scope = ScopeFactory.CreateAsyncScope();
            var inboxRepo = scope.ServiceProvider.GetRequiredService<PetraInboxRepository>();
            _inboxRows = await inboxRepo.GetRecentAsync(limit: 50, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "TaskHub LoadAllAsync 失敗");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnSessionRowClick(TableRowClickEventArgs<PetraSession> args)
    {
        var clicked = args.Item;
        if (clicked is null) return;
        _selectedSession = await SessionRepo.GetWithMessagesAsync(clicked.Id);
        _isSessionDrawerOpen = true;
    }

    private async Task HandleHitlResponseAsync(ResponseRequest request)
    {
        try
        {
            var responded = await RespondService.RespondAsync(request.InteractionId, request.Action, request.Content);
            if (responded)
            {
                Snackbar.Add("回覆成功！", Severity.Success);
            }
            else
            {
                Snackbar.Add("此互動已被另一通道回覆，已重新整理。", Severity.Warning);
            }
            await LoadAllAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"回覆時發生錯誤：{ex.Message}", Severity.Error);
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
                await LoadAllAsync();
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

        // Tasks 分區 subscribe 3 endpoint — 子項 6 重 wire 完整 5 endpoint 細分
        _hubConnection.On<object>(AgentStatusHub.ReceiveTaskUpdate, async _ =>
        {
            await LoadAllAsync();
            await InvokeAsync(StateHasChanged);
        });
        _hubConnection.On(AgentStatusHub.ReceiveQueueUpdate, async () =>
        {
            await LoadAllAsync();
            await InvokeAsync(StateHasChanged);
        });
        _hubConnection.On(AgentStatusHub.ReceiveInteractionUpdate, async () =>
        {
            await LoadAllAsync();
            await InvokeAsync(StateHasChanged);
        });

        _hubConnection.Closed += _ =>
        {
            _hubConnected = false;
            return InvokeAsync(StateHasChanged);
        };
        _hubConnection.Reconnected += _ =>
        {
            _hubConnected = true;
            return InvokeAsync(StateHasChanged);
        };

        try
        {
            await _hubConnection.StartAsync();
            _hubConnected = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "TaskHub SignalR Hub 連線失敗");
        }
    }

    private static Color GetSessionStatusColor(string status) => status switch
    {
        "running"   => Color.Info,
        "paused"    => Color.Warning,
        "done"      => Color.Success,
        "escalated" => Color.Error,
        "cancelled" => Color.Dark,
        _           => Color.Default,
    };

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

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 60) return $"{(int)duration.TotalSeconds} 秒";
        if (duration.TotalMinutes < 60) return $"{(int)duration.TotalMinutes} 分";
        if (duration.TotalHours < 24)   return $"{(int)duration.TotalHours} 時 {duration.Minutes} 分";
        return $"{(int)duration.TotalDays} 天 {duration.Hours} 時";
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }
}
