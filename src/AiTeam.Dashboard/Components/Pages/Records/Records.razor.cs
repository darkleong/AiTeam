using AiTeam.Data;
using AiTeam.Data.Records;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Components.Pages.Records;

/// <summary>
/// Stage 92：v4-rewrite Dashboard MCP Records 表格頁（minimal scope）。
/// 5 tab × 5 MudTable 顯示 mcp_teams / mcp_teammates / mcp_tasks / mcp_messages / mcp_token_usage。
/// 每表顯示最近 100 筆（CreatedAt desc / SpawnedAt desc）。
/// 無 filter / sort / pagination 進階功能（後續 phase 重評）。
/// </summary>
public partial class Records
{
    [Inject] private AppDbContext Db { get; set; } = null!;

    private List<AgentTeam>       _teams       = new();
    private List<AgentTeammate>   _teammates   = new();
    private List<AgentTask>       _tasks       = new();
    private List<AgentMessage>    _messages    = new();
    private List<AgentTokenUsage> _tokenUsages = new();

    protected override async Task OnInitializedAsync()
    {
        _teams       = await Db.AgentTeams.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
        _teammates   = await Db.AgentTeammates.AsNoTracking().OrderByDescending(x => x.SpawnedAt).Take(100).ToListAsync();
        _tasks       = await Db.AgentTasks.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
        _messages    = await Db.AgentMessages.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
        _tokenUsages = await Db.AgentTokenUsages.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
    }
}
