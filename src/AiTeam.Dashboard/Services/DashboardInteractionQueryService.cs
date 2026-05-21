using System.Text.Json;
using AiTeam.Data;
using AiTeam.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Dashboard.Services;

/// <summary>
/// Stage 83 子項 7 議題 E1：拆 DashboardTaskService 568 行 →
/// DashboardInteractionQueryService（HITL Interaction query + Agent queue query）+
/// DashboardDeploymentService（Deployment TaskItem + TaskLog query）。
///
/// 對齊新分區語義：
/// - HitlCardCenter (Tasks 分區) + Home 速覽 + InteractionCenter → 走本 service
/// - Monitoring AgentStatus + DashboardSessionService 不需建（Repository 直接 inject）
///
/// 議題 F3：改 IDbContextFactory pattern + 砍 SemaphoreSlim — 對齊 Stage 80 議題 1 修根因紀律
/// （Blazor InteractiveServer Scoped DbContext 並發限制 「A second operation was started」根因 →
/// Factory pattern 開 short-lived DbContext / IDbContextFactory 在 Stage 80 已加 DI 並存）。
/// </summary>
public class DashboardInteractionQueryService(IDbContextFactory<AppDbContext> dbFactory)
{
    /// <summary>取得所有 pending 互動（Tasks 分區 HitlCardCenter / Home 速覽用）。</summary>
    public async Task<List<BossInteractionDto>> GetPendingInteractionsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.BossInteractions
            .AsNoTracking()
            .Where(x => x.Status == "pending")
            .OrderBy(x => x.CreatedAt)
            .Select(x => MapToDto(x))
            .ToListAsync(ct);
    }

    /// <summary>取得最近已處理互動。</summary>
    public async Task<List<BossInteractionDto>> GetRecentInteractionsAsync(int count = 10, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.BossInteractions
            .AsNoTracking()
            .Where(x => x.Status == "responded")
            .OrderByDescending(x => x.RespondedAt)
            .Take(count)
            .Select(x => MapToDto(x))
            .ToListAsync(ct);
    }

    /// <summary>Stage 28b：取得已處理互動歷史，支援篩選與分頁。</summary>
    public async Task<(List<BossInteractionDto> Items, int TotalCount)> GetInteractionHistoryAsync(
        int page, int pageSize, string? typeFilter, string? sourceFilter,
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.BossInteractions.AsNoTracking()
            .Where(x => x.Status == "responded");

        if (!string.IsNullOrEmpty(typeFilter))
            query = query.Where(x => x.InteractionType == typeFilter);
        if (!string.IsNullOrEmpty(sourceFilter))
            query = query.Where(x => x.ResponseSource == sourceFilter);
        if (from.HasValue)
            query = query.Where(x => x.RespondedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(x => x.RespondedAt <= to.Value.AddDays(1));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.RespondedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapToDto(x))
            .ToListAsync(ct);

        return (items, total);
    }

    /// <summary>
    /// 取得所有 Agent 的佇列狀態（Stage 27b）— Home 速覽 + TaskHub + MonitoringHub Agent 狀態 tab 用。
    /// 包含 Agent 狀態（active/paused/stopped）、佇列深度、排隊中的任務清單。
    /// </summary>
    public async Task<List<AgentQueueDto>> GetAgentQueuesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var agentStateSettings = await db.AppSettings
            .AsNoTracking()
            .Where(s => s.Key.StartsWith("AgentState:"))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        var queuedTasks = await db.Tasks
            .AsNoTracking()
            .Where(t => t.QueueStatus == "queued" || t.QueueStatus == "processing")
            .OrderBy(t => t.QueuedAt)
            .Select(t => new { t.Id, t.GroupId, t.Title, t.AssignedAgent, t.QueueStatus, t.QueuedAt })
            .ToListAsync(ct);

        var result = new List<AgentQueueDto>();
        foreach (var (executorKey, assignedAgents) in AgentGroups)
        {
            var groupTasks = queuedTasks
                .Where(t => assignedAgents.Contains(t.AssignedAgent))
                .ToList();

            var state          = agentStateSettings.GetValueOrDefault($"AgentState:{executorKey}", "active");
            var processingTask = groupTasks.FirstOrDefault(t => t.QueueStatus == "processing");
            var waitingTasks   = groupTasks.Where(t => t.QueueStatus == "queued").ToList();

            result.Add(new AgentQueueDto
            {
                AgentName            = executorKey,
                AgentState           = state,
                QueueDepth           = waitingTasks.Count,
                CurrentTaskTitle     = processingTask?.Title,
                CurrentTaskId        = processingTask?.Id,
                CurrentTaskGroupId   = processingTask?.GroupId,
                CurrentTaskQueuedAt  = processingTask?.QueuedAt,
                QueuedTasks          = waitingTasks.Select(t => new QueuedTaskItemDto
                {
                    TaskId   = t.Id,
                    GroupId  = t.GroupId,
                    Title    = t.Title,
                    QueuedAt = t.QueuedAt
                }).ToList()
            });
        }

        return result;
    }

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
            SystemNotes      = x.SystemNotes,
            ContextJson      = x.ContextJson,
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
}
