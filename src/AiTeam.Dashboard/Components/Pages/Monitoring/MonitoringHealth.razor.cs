using System.Net.Http.Json;
using AiTeam.Data;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Components.Pages.Monitoring;

/// <summary>
/// Stage 87 B0：Monitoring 拆 4 sub-page 之系統健康（取代 MonitoringHub.razor Tab 4）。
/// Stage 87 follow-up #SignalR：SignalR Hub 自身健康必檢測（既 B1 拍板「本頁無訂閱」→ Christ 反饋 N/A 灰色不對 — 系統健康頁本來就該檢測 SignalR Hub）。
/// 走 HubConnection.StartAsync 純驗連線狀態 / 不訂閱任何 event / 對齊 health check 純粹性。
/// </summary>
public partial class MonitoringHealth : IAsyncDisposable
{
    #region Dependencies

    [Inject] private IDbContextFactory<AppDbContext> DbFactory         { get; set; } = null!;
    [Inject] private IHttpClientFactory              HttpClientFactory { get; set; } = null!;
    [Inject] private IConfiguration                  Configuration     { get; set; } = null!;
    [Inject] private NavigationManager               Navigation        { get; set; } = null!;
    [Inject] private ILogger<MonitoringHealth>       Logger            { get; set; } = null!;

    #endregion

    private bool   _botHealthy;
    private string _botStatusDetail = "未檢測";
    private bool   _dbHealthy;
    private string _dbStatusDetail  = "未檢測";
    private HubConnection? _hubConnection;
    private bool   _hubConnected;

    protected override async Task OnInitializedAsync()
    {
        await RefreshHealthAsync();
        await ConnectSignalRAsync();
    }

    // Stage 87 follow-up #SignalR：純連線檢測 / 0 event 訂閱
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

        _hubConnection.Closed     += _ => { _hubConnected = false; return InvokeAsync(StateHasChanged); };
        _hubConnection.Reconnected += _ => { _hubConnected = true;  return InvokeAsync(StateHasChanged); };

        try
        {
            await _hubConnection.StartAsync();
            _hubConnected = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MonitoringHealth SignalR Hub 連線失敗");
            _hubConnected = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }

    private async Task RefreshHealthAsync()
    {
        try
        {
            var apiKey = Configuration["Bot:InternalApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _botHealthy = false;
                _botStatusDetail = "Bot:InternalApiKey 未設定（docker-compose Bot__InternalApiKey env）";
                return;
            }

            var botBaseUrl = Configuration["Bot:InternalUrl"] ?? "http://aiteam-bot:8080";

            var client = HttpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync($"{botBaseUrl}/internal/health");
            if (response.IsSuccessStatusCode)
            {
                var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
                if (health is not null)
                {
                    _botHealthy      = health.BotProcessUp;
                    _botStatusDetail = health.BotProcessDetail;
                    _dbHealthy       = health.DbConnected;
                    _dbStatusDetail  = health.DbDetail;
                    return;
                }
            }
            _botHealthy      = false;
            _botStatusDetail = $"HTTP {(int)response.StatusCode}";
        }
        catch (Exception ex)
        {
            _botHealthy      = false;
            _botStatusDetail = ex.Message.Length > 50 ? ex.Message[..50] + "..." : ex.Message;
            // Fallback：DB ping 直接 Dashboard 端做
            try
            {
                await using var db = await DbFactory.CreateDbContextAsync();
                _dbHealthy      = await db.Database.CanConnectAsync();
                _dbStatusDetail = _dbHealthy ? "Dashboard 端 DB ping OK" : "Cannot connect";
            }
            catch (Exception dbEx)
            {
                _dbHealthy      = false;
                _dbStatusDetail = dbEx.Message;
            }
        }
    }

    private record HealthResponse(
        bool BotProcessUp, string BotProcessDetail,
        bool DbConnected, string DbDetail,
        bool DiscordConnected, string DiscordDetail,
        DateTime Timestamp);
}
