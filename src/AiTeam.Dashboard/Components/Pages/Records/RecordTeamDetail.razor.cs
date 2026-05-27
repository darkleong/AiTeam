using AiTeam.Data;
using AiTeam.Data.Records;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Components.Pages.Records;

/// <summary>
/// F3-B：MCP Records 單一 Team drill-down 詳情頁。
/// 路由：/records/team/{TeamId:guid}
/// 顯示：Team Info card + Teammates / Tasks / TokenUsage / Messages 4 個 MudTable。
/// 由於 AgentMessage / AgentTokenUsage 沒有 TeamId 欄位 → 透過該 team 的 Teammate Ids 過濾。
/// </summary>
public partial class RecordTeamDetail : ComponentBase
{
    [Inject] private AppDbContext               Db     { get; set; } = null!;
    [Inject] private NavigationManager          Nav    { get; set; } = null!;
    [Inject] private ILogger<RecordTeamDetail>  Logger { get; set; } = null!;

    [Parameter] public Guid TeamId { get; set; }

    private bool                  _loading     = true;
    private AgentTeam?            _team;
    private List<AgentTeammate>   _teammates   = [];
    private List<AgentTask>       _tasks       = [];
    private List<TokenUsageRow>   _tokenUsages = [];
    private List<MessageRow>      _messages    = [];

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        try
        {
            _team = await Db.AgentTeams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == TeamId);
            if (_team is null) return;

            _teammates = await Db.AgentTeammates.AsNoTracking()
                .Where(x => x.TeamId == TeamId)
                .OrderByDescending(x => x.SpawnedAt)
                .ToListAsync();

            _tasks = await Db.AgentTasks.AsNoTracking()
                .Where(x => x.TeamId == TeamId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var teammateIds   = _teammates.Select(t => t.Id).ToHashSet();
            var teammateNames = _teammates.ToDictionary(t => t.Id, t => t.Name);

            // AgentTokenUsage 沒 TeamId / 用 teammate id 過濾 join name
            var tokenRows = await Db.AgentTokenUsages.AsNoTracking()
                .Where(x => teammateIds.Contains(x.TeammateId))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            _tokenUsages = tokenRows.Select(x => new TokenUsageRow
            {
                TeammateName = teammateNames.GetValueOrDefault(x.TeammateId, x.TeammateId.ToString()[..8]),
                TaskId       = x.TaskId,
                Model        = x.Model,
                InputTokens  = x.InputTokens,
                OutputTokens = x.OutputTokens,
                CreatedAt    = x.CreatedAt,
            }).ToList();

            // AgentMessage 沒 TeamId / 用 teammate id 過濾 + 按 CreatedAt asc timeline
            var msgRows = await Db.AgentMessages.AsNoTracking()
                .Where(x => teammateIds.Contains(x.TeammateId))
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
            _messages = msgRows.Select(x => new MessageRow
            {
                TeammateName = teammateNames.GetValueOrDefault(x.TeammateId, x.TeammateId.ToString()[..8]),
                TaskId       = x.TaskId,
                Role         = x.Role,
                Content      = x.Content,
                CreatedAt    = x.CreatedAt,
            }).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "RecordTeamDetail 載入失敗 TeamId={TeamId}", TeamId);
        }
        finally
        {
            _loading = false;
        }
    }

    public class TokenUsageRow
    {
        public string   TeammateName { get; set; } = "";
        public Guid?    TaskId       { get; set; }
        public string?  Model        { get; set; }
        public int      InputTokens  { get; set; }
        public int      OutputTokens { get; set; }
        public DateTime CreatedAt    { get; set; }
    }

    public class MessageRow
    {
        public string   TeammateName { get; set; } = "";
        public Guid?    TaskId       { get; set; }
        public string   Role         { get; set; } = "";
        public string   Content      { get; set; } = "";
        public DateTime CreatedAt    { get; set; }
    }
}
