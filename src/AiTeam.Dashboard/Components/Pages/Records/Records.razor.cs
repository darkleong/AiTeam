using AiTeam.Data;
using AiTeam.Data.Records;
using AiTeam.Dashboard.Hubs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Records;

/// <summary>
/// Stage 92：v4-rewrite Dashboard MCP Records 表格頁。
/// F13 重構：原 5 MudTab 改成依 route param Section 條件 render 對應 MudTable。
/// F16：訂閱 RecordsHub /records-hub 收到 RecordsUpdated event → 整段 reload 不必 F5 refresh。
/// F3-A：5 表全升級 — Sort（每欄 MudTableSortLabel）/ Paging（MudTablePager 25/50/100/200）/ Filter（每表 2 維度）
/// F3-B：Teams 表加 OnRowClick → drill-down /records/team/{TeamId}
/// 路由：/records（default 顯示 teams）/ /records/{Section} — Section ∈ teams / teammates / tasks / messages / token-usage。
/// 載入策略：全表 ToListAsync()（v4 純記錄系統初期 row 數可控 / 後續若爆量再切 server-side paging）。
/// </summary>
public partial class Records : IAsyncDisposable
{
    [Inject] private AppDbContext      Db            { get; set; } = null!;
    [Inject] private NavigationManager Nav           { get; set; } = null!;
    [Inject] private IConfiguration    Configuration { get; set; } = null!;
    [Inject] private ILogger<Records>  Logger        { get; set; } = null!;

    /// <summary>NavMenu 子項對應的 section key（teams / teammates / tasks / messages / token-usage / null）</summary>
    [Parameter] public string? Section { get; set; }

    private List<AgentTeam>       _teams       = new();
    private List<AgentTeammate>   _teammates   = new();
    private List<AgentTask>       _tasks       = new();
    private List<AgentMessage>    _messages    = new();
    private List<AgentTokenUsage> _tokenUsages = new();

    private HubConnection? _hub;

    #region F3-A filter state

    private string _teamsStatusFilter       = "all";
    private string _teamsNameSearch         = string.Empty;
    private string _teammatesModelFilter    = "all";
    private string _teammatesRoleFilter     = "all";
    private string _tasksStatusFilter       = "all";
    private string _tasksTitleSearch        = string.Empty;
    private string _messagesRoleFilter      = "all";
    private string _messagesContentSearch   = string.Empty;
    private string _tokenUsagesModelFilter  = "all";

    #endregion

    protected override async Task OnInitializedAsync()
    {
        await ReloadAllAsync();
        await ConnectHubAsync();
    }

    private async Task ReloadAllAsync()
    {
        // F3-A：砍 .Take(100) / 全載入（client-side sort + paging + filter）
        _teams       = await Db.AgentTeams.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync();
        _teammates   = await Db.AgentTeammates.AsNoTracking().OrderByDescending(x => x.SpawnedAt).ToListAsync();
        _tasks       = await Db.AgentTasks.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync();
        _messages    = await Db.AgentMessages.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync();
        _tokenUsages = await Db.AgentTokenUsages.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync();
    }

    #region F3-A Filter delegates

    private bool FilterTeam(AgentTeam x)
    {
        if (_teamsStatusFilter != "all" && !string.Equals(x.Status, _teamsStatusFilter, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrWhiteSpace(_teamsNameSearch)
            && x.Name.IndexOf(_teamsNameSearch, StringComparison.OrdinalIgnoreCase) < 0)
            return false;
        return true;
    }

    private bool FilterTeammate(AgentTeammate x)
    {
        if (_teammatesModelFilter != "all"
            && (x.Model is null || x.Model.IndexOf(_teammatesModelFilter, StringComparison.OrdinalIgnoreCase) < 0))
            return false;
        if (_teammatesRoleFilter != "all" && !string.Equals(x.Role, _teammatesRoleFilter, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private bool FilterTask(AgentTask x)
    {
        if (_tasksStatusFilter != "all" && !string.Equals(x.Status, _tasksStatusFilter, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrWhiteSpace(_tasksTitleSearch)
            && x.Title.IndexOf(_tasksTitleSearch, StringComparison.OrdinalIgnoreCase) < 0)
            return false;
        return true;
    }

    private bool FilterMessage(AgentMessage x)
    {
        if (_messagesRoleFilter != "all" && !string.Equals(x.Role, _messagesRoleFilter, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrWhiteSpace(_messagesContentSearch)
            && x.Content.IndexOf(_messagesContentSearch, StringComparison.OrdinalIgnoreCase) < 0)
            return false;
        return true;
    }

    private bool FilterTokenUsage(AgentTokenUsage x)
    {
        if (_tokenUsagesModelFilter != "all"
            && (x.Model is null || x.Model.IndexOf(_tokenUsagesModelFilter, StringComparison.OrdinalIgnoreCase) < 0))
            return false;
        return true;
    }

    #endregion

    /// <summary>F3-B：Teams 列點擊 → drill-down 至 team detail page</summary>
    private void OnTeamRowClick(TableRowClickEventArgs<AgentTeam> args)
    {
        if (args.Item is null) return;
        Nav.NavigateTo($"/records/team/{args.Item.Id}");
    }

    private async Task ConnectHubAsync()
    {
        try
        {
            // F16 fix: 對齊既有 Monitoring 三頁 hub pattern — 用 Dashboard:HubBaseUrl config（docker container localhost:8080）/ fallback to Nav.ToAbsoluteUri（dev mode）
            var hubBaseUrl = Configuration["Dashboard:HubBaseUrl"];
            var hubUrl = string.IsNullOrEmpty(hubBaseUrl)
                ? Nav.ToAbsoluteUri("/records-hub").ToString()
                : $"{hubBaseUrl.TrimEnd('/')}/records-hub";

            _hub = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            _hub.On(RecordsHub.ReceiveRecordsUpdated, async () =>
            {
                try
                {
                    await ReloadAllAsync();
                    await InvokeAsync(StateHasChanged);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Records RecordsUpdated handler 例外");
                }
            });

            await _hub.StartAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Records SignalR Hub 連線失敗（非關鍵 / 頁面仍可用）");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null) await _hub.DisposeAsync();
    }
}
