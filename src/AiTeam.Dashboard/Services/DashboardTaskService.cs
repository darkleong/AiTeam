using System.Text.Json;
using AiTeam.Data;
using AiTeam.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>
/// 任務查詢服務，回傳 Dashboard 用的 DTO（不直接回傳 Entity）。
/// SemaphoreSlim 確保同一 Blazor circuit（Scoped）下的 DB 操作不並發，
/// 防止 AppDbContext 收到多個並行查詢導致 circuit crash。
/// </summary>
public class DashboardTaskService(AppDbContext db)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    #region Public Methods

    /// <summary>取得分頁任務列表。</summary>
    public async Task<PagedResult<TaskItemDto>> GetTasksPagedAsync(
        int page = 1,
        int pageSize = 50,
        IReadOnlyCollection<string>? statusFilters = null,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var query = db.Tasks
                .AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.Team)
                .Where(t => statusFilters == null || statusFilters.Count == 0 || statusFilters.Contains(t.Status))
                .OrderByDescending(t => t.CreatedAt);

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TaskItemDto
                {
                    Id            = t.Id,
                    Title         = t.Title,
                    TriggeredBy   = t.TriggeredBy,
                    AssignedAgent = t.AssignedAgent,
                    Status        = t.Status,
                    CreatedAt     = t.CreatedAt,
                    CompletedAt   = t.CompletedAt,
                    ProjectName   = t.Project != null ? t.Project.Name : null,
                    TeamName      = t.Team    != null ? t.Team.Name    : null
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<TaskItemDto>(items, total);
        }
        finally { _lock.Release(); }
    }

    /// <summary>取得最近 N 筆任務（首頁快速摘要用）。</summary>
    public async Task<List<TaskItemDto>> GetRecentTasksAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await db.Tasks
                .AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.Team)
                .OrderByDescending(t => t.CreatedAt)
                .Take(limit)
                .Select(t => new TaskItemDto
                {
                    Id            = t.Id,
                    Title         = t.Title,
                    TriggeredBy   = t.TriggeredBy,
                    AssignedAgent = t.AssignedAgent,
                    Status        = t.Status,
                    CreatedAt     = t.CreatedAt,
                    CompletedAt   = t.CompletedAt,
                    ProjectName   = t.Project != null ? t.Project.Name : null,
                    TeamName      = t.Team    != null ? t.Team.Name    : null
                })
                .ToListAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    /// <summary>取得最近 N 筆流程（首頁快速摘要用）。</summary>
    public async Task<List<TaskGroupDto>> GetRecentTaskGroupsAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await db.TaskGroups
                .AsNoTracking()
                .OrderByDescending(g => g.CreatedAt)
                .Take(limit)
                .Select(g => new TaskGroupDto
                {
                    Id                = g.Id,
                    Title             = g.Title,
                    Status            = g.Status,
                    WorkflowType      = g.WorkflowType,
                    Project           = g.Project,
                    FixIteration      = g.FixIteration,
                    DevPlanRevision   = g.DevPlanRevision,
                    DevPrUrl          = g.DevPrUrl,
                    CreatedAt         = g.CreatedAt,
                    KickoffMeetingLog = g.KickoffMeetingLog,
                    TaskPlan          = g.TaskPlan,
                    KickoffRound      = g.KickoffRound,
                    DesignMeetingLog  = g.DesignMeetingLog,
                    DesignPlan        = g.DesignPlan,
                    DesignRound       = g.DesignRound,
                    DevPlan           = g.DevPlan,
                    LastReviewBody    = g.LastReviewBody,
                    TestReport        = g.TestReport,
                    ArchiveContent    = g.ArchiveContent,
                })
                .ToListAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    /// <summary>取得 TaskGroup 分頁列表（流程追蹤 Tab 用）。</summary>
    public async Task<PagedResult<TaskGroupDto>> GetTaskGroupsPagedAsync(
        int page = 1,
        int pageSize = 50,
        IReadOnlyCollection<string>? statusFilters = null,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var query = db.TaskGroups
                .AsNoTracking()
                .Where(g => statusFilters == null || statusFilters.Count == 0 || statusFilters.Contains(g.Status))
                .OrderByDescending(g => g.CreatedAt);

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(g => new TaskGroupDto
                {
                    Id                = g.Id,
                    Title             = g.Title,
                    Status            = g.Status,
                    WorkflowType      = g.WorkflowType,
                    Project           = g.Project,
                    FixIteration      = g.FixIteration,
                    DevPlanRevision   = g.DevPlanRevision,
                    DevPrUrl          = g.DevPrUrl,
                    CreatedAt         = g.CreatedAt,
                    KickoffMeetingLog = g.KickoffMeetingLog,
                    TaskPlan          = g.TaskPlan,
                    KickoffRound      = g.KickoffRound,
                    DesignMeetingLog  = g.DesignMeetingLog,
                    DesignPlan        = g.DesignPlan,
                    DesignRound       = g.DesignRound,
                    DevPlan           = g.DevPlan,
                    LastReviewBody    = g.LastReviewBody,
                    TestReport        = g.TestReport,
                    ArchiveContent    = g.ArchiveContent,
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<TaskGroupDto>(items, total);
        }
        finally { _lock.Release(); }
    }

    /// <summary>取得單一 TaskGroup 的最新快照（Pipeline View 折疊面板即時更新用）。</summary>
    public async Task<TaskGroupDto?> GetTaskGroupByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await db.TaskGroups
                .AsNoTracking()
                .Where(g => g.Id == id)
                .Select(g => new TaskGroupDto
                {
                    Id                = g.Id,
                    Title             = g.Title,
                    Status            = g.Status,
                    WorkflowType      = g.WorkflowType,
                    Project           = g.Project,
                    FixIteration      = g.FixIteration,
                    DevPlanRevision   = g.DevPlanRevision,
                    DevPrUrl          = g.DevPrUrl,
                    CreatedAt         = g.CreatedAt,
                    KickoffMeetingLog = g.KickoffMeetingLog,
                    TaskPlan          = g.TaskPlan,
                    KickoffRound      = g.KickoffRound,
                    DesignMeetingLog  = g.DesignMeetingLog,
                    DesignPlan        = g.DesignPlan,
                    DesignRound       = g.DesignRound,
                    DevPlan           = g.DevPlan,
                    LastReviewBody    = g.LastReviewBody,
                    TestReport        = g.TestReport,
                    ArchiveContent    = g.ArchiveContent,
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    /// <summary>取得 TaskGroup 下所有 TaskItem（Pipeline View 步驟用）。</summary>
    public async Task<List<TaskItemDto>> GetTaskItemsByGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await db.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId == groupId)
                .OrderBy(t => t.CreatedAt)
                .ThenBy(t => t.Id)
                .Select(t => new TaskItemDto
                {
                    Id            = t.Id,
                    GroupId       = t.GroupId,
                    Title         = t.Title,
                    TriggeredBy   = t.TriggeredBy,
                    AssignedAgent = t.AssignedAgent,
                    Status        = t.Status,
                    CreatedAt     = t.CreatedAt,
                    CompletedAt   = t.CompletedAt
                })
                .ToListAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    /// <summary>取得任務的所有 Log（點擊任務後展開用）。</summary>
    public async Task<List<TaskLogDto>> GetTaskLogsAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await db.TaskLogs
                .AsNoTracking()
                .Where(l => l.TaskId == taskId)
                .OrderBy(l => l.CreatedAt)
                .Select(l => new TaskLogDto
                {
                    Id        = l.Id,
                    TaskId    = l.TaskId,
                    Agent     = l.Agent,
                    Step      = l.Step,
                    Status    = l.Status,
                    Payload   = l.Payload,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// 取得所有 Agent 的佇列狀態（Stage 27b）。
    /// 包含 Agent 狀態（active/paused/stopped）、佇列深度，以及排隊中的任務清單。
    /// </summary>
    public async Task<List<AgentQueueDto>> GetAgentQueuesAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // 讀取 AgentState 設定（app_settings 表）
            var agentStateSettings = await db.AppSettings
                .AsNoTracking()
                .Where(s => s.Key.StartsWith("AgentState:"))
                .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

            // 查詢所有排隊中 / 執行中的任務
            var queuedTasks = await db.Tasks
                .AsNoTracking()
                .Where(t => t.QueueStatus == "queued" || t.QueueStatus == "processing")
                .OrderBy(t => t.QueuedAt)
                .Select(t => new { t.Id, t.Title, t.AssignedAgent, t.QueueStatus, t.QueuedAt })
                .ToListAsync(cancellationToken);

            // Dev group 包含 Dev_plan
            var result = new List<AgentQueueDto>();
            foreach (var (executorKey, assignedAgents) in AgentGroups)
            {
                var groupTasks = queuedTasks
                    .Where(t => assignedAgents.Contains(t.AssignedAgent))
                    .ToList();

                var state        = agentStateSettings.GetValueOrDefault($"AgentState:{executorKey}", "active");
                var processingTask = groupTasks.FirstOrDefault(t => t.QueueStatus == "processing");
                var waitingTasks   = groupTasks.Where(t => t.QueueStatus == "queued").ToList();

                result.Add(new AgentQueueDto
                {
                    AgentName        = executorKey,
                    AgentState       = state,
                    QueueDepth       = waitingTasks.Count,
                    CurrentTaskTitle = processingTask?.Title,
                    QueuedTasks      = waitingTasks.Select(t => new QueuedTaskItemDto
                    {
                        TaskId   = t.Id,
                        Title    = t.Title,
                        QueuedAt = t.QueuedAt
                    }).ToList()
                });
            }

            return result;
        }
        finally { _lock.Release(); }
    }

    /// <summary>Executor key → AssignedAgent 名稱列表（對應 AgentQueueProcessor.SemaphoreGroups）。</summary>
    private static readonly Dictionary<string, string[]> AgentGroups = new()
    {
        ["Dev"]          = ["Dev", "Dev_plan"],
        ["Reviewer"]     = ["Reviewer"],
        ["QA"]           = ["QA"],
        ["Doc"]          = ["Doc"],
        ["Requirements"] = ["Requirements"],
        ["Designer"]     = ["Designer"],
        ["Release"]      = ["Release"],
        ["Ops"]          = ["Ops"],
    };

    // ─── Stage 28a：BossInteraction 互動操作 ────────────────────────────────────

    /// <summary>取得所有 pending 互動（Dashboard 操作中心待處理清單）。</summary>
    public async Task<List<BossInteractionDto>> GetPendingInteractionsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await db.BossInteractions
                .AsNoTracking()
                .Where(x => x.Status == "pending")
                .OrderBy(x => x.CreatedAt)
                .Select(x => MapToDto(x))
                .ToListAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    /// <summary>取得最近已處理互動（Dashboard 歷史區）。</summary>
    public async Task<List<BossInteractionDto>> GetRecentInteractionsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await db.BossInteractions
                .AsNoTracking()
                .Where(x => x.Status == "responded")
                .OrderByDescending(x => x.RespondedAt)
                .Take(count)
                .Select(x => MapToDto(x))
                .ToListAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    /// <summary>Stage 28b：取得已處理互動歷史，支援篩選與分頁。</summary>
    public async Task<(List<BossInteractionDto> Items, int TotalCount)> GetInteractionHistoryAsync(
        int page, int pageSize, string? typeFilter, string? sourceFilter,
        DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var query = db.BossInteractions.AsNoTracking()
                .Where(x => x.Status == "responded");

            if (!string.IsNullOrEmpty(typeFilter))
                query = query.Where(x => x.InteractionType == typeFilter);
            if (!string.IsNullOrEmpty(sourceFilter))
                query = query.Where(x => x.ResponseSource == sourceFilter);
            if (from.HasValue)
                query = query.Where(x => x.RespondedAt >= from.Value);
            if (to.HasValue)
                query = query.Where(x => x.RespondedAt <= to.Value.AddDays(1)); // 包含當天

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.RespondedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => MapToDto(x))
                .ToListAsync(cancellationToken);

            return (items, total);
        }
        finally { _lock.Release(); }
    }

    /// <summary>互動 Entity → DTO 轉換（含 AvailableActionsJson 反序列化）。</summary>
    private static BossInteractionDto MapToDto(BossInteraction x)
    {
        List<InteractionActionDto> actions;
        try
        {
            actions = string.IsNullOrWhiteSpace(x.AvailableActionsJson)
                ? []
                : JsonSerializer.Deserialize<List<InteractionActionDto>>(x.AvailableActionsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch { actions = []; }

        return new BossInteractionDto
        {
            Id               = x.Id,
            TaskGroupId      = x.TaskGroupId,
            TaskItemId       = x.TaskItemId,
            InteractionType  = x.InteractionType,
            Status           = x.Status,
            Title            = x.Title,
            Description      = x.Description,
            Project          = x.Project,
            AgentName        = x.AgentName,
            AvailableActions = actions,
            ResponseAction   = x.ResponseAction,
            ResponseSource   = x.ResponseSource,
            ResponseContent  = x.ResponseContent,
            RespondedAt      = x.RespondedAt,
            CreatedAt        = x.CreatedAt
        };
    }

    #endregion
}

/// <summary>分頁結果包裝。</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
