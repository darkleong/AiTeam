using System.ComponentModel;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Records;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace AiTeam.Bot.McpTools;

/// <summary>
/// Stage 91：v4-rewrite MCP record tools — 5 個 tool 涵蓋 Claude Code Agent Team 整套 lifecycle 記錄。
///
/// Tool 清單：
///   - register_team           — Claude Code lead 開 team 時 call、回 team_id
///   - close_team              — Lead 收尾整個 team 時 call（v4.0.2 補 / 寫 Team.Status='closed' + ClosedAt）
///   - register_teammate       — Lead spawn teammate 時 call、回 teammate_id（Stage 91 自決加 / 對齊運作流程）
///   - finish_teammate         — Teammate 完成退出時 call（v4.0.2 補 / 寫 Teammate.FinishedAt）
///   - record_task             — Task lifecycle 事件（action: create / claim / complete / fail）
///   - record_message          — Teammate 對話 message 單筆寫入（v4.0.1 由 record_conversation rename / 對齊 AgentMessage entity + mcp_messages table）
///   - record_token_usage      — LLM call 後 token 消耗單筆寫入
///
/// 設計紀律：static method 用 DI inject AppDbContext（EF Core scoped）/ MCP server 自動 scope 處理。
/// </summary>
[McpServerToolType]
public sealed class RecordTools
{
    [McpServerTool, Description("Register a new Claude Code Agent Team execution session. Returns the new team_id (Guid string) to use in subsequent record calls.")]
    public static async Task<string> RegisterTeam(
        AppDbContext db,
        RecordNotificationService notify,
        RecordsHubNotifyService hubNotify,
        [Description("Team name (e.g., 'feature-x-team', 'bug-investigation-team')")] string name,
        [Description("High-level intent the boss gave to this team (optional)")] string? description = null,
        [Description("Project / repo name（caller detect from git remote / repo root basename / nullable）")] string? projectName = null)
    {
        var team = new AgentTeam
        {
            Name = name,
            Description = description,
            ProjectName = projectName,
        };
        db.AgentTeams.Add(team);
        await db.SaveChangesAsync();
        hubNotify.FireAndForget();

        _ = Task.Run(() => notify.SendAsync($"📋 新 Agent Team 開始 — **{name}** (id=`{team.Id.ToString()[..8]}`)\n{description ?? ""}".TrimEnd()));

        return team.Id.ToString();
    }

    [McpServerTool, Description("Close an Agent Team execution session (mark Status='closed' + set ClosedAt). Idempotent — calling on an already-closed team returns 'already closed'.")]
    public static async Task<string> CloseTeam(
        AppDbContext db,
        RecordNotificationService notify,
        RecordsHubNotifyService hubNotify,
        [Description("Team ID (Guid string)")] string teamId)
    {
        if (!Guid.TryParse(teamId, out var parsedTeamId))
            return "Error: invalid teamId Guid";

        var team = await db.AgentTeams.FindAsync(parsedTeamId);
        if (team is null) return $"Error: team {teamId} not found";
        if (team.Status == "closed") return "already closed";

        // cascade auto-finish：team close 時、把該 team 內所有未 FinishedAt 的 teammate 一次補上 FinishedAt
        var now = DateTime.UtcNow;
        var pendingTeammates = await db.AgentTeammates
            .Where(t => t.TeamId == parsedTeamId && t.FinishedAt == null)
            .ToListAsync();
        foreach (var tm in pendingTeammates)
        {
            tm.FinishedAt = now;
        }
        var cascadeCount = pendingTeammates.Count;

        team.Status = "closed";
        team.ClosedAt = now;
        await db.SaveChangesAsync();
        hubNotify.FireAndForget();

        _ = Task.Run(() => notify.SendAsync($"🏁 Agent Team 收尾 — **{team.Name}** (id=`{team.Id.ToString()[..8]}`)"));

        return $"closed (cascade-finished {cascadeCount} teammates)";
    }

    [McpServerTool, Description("Register a new teammate (lead or member) in an existing team. Returns the new teammate_id (Guid string).")]
    public static async Task<string> RegisterTeammate(
        AppDbContext db,
        RecordsHubNotifyService hubNotify,
        [Description("Parent team_id (Guid string from register_team)")] string teamId,
        [Description("Teammate name (e.g., 'petra-pm' for lead, 'cody-1' for member)")] string name,
        [Description("Model used (e.g., 'sonnet', 'opus', 'haiku', or full model id)")] string? model = null,
        [Description("Role: 'lead' or 'member' (default: 'member')")] string role = "member")
    {
        if (!Guid.TryParse(teamId, out var parsedTeamId))
            return "Error: invalid teamId Guid";

        var teammate = new AgentTeammate
        {
            TeamId = parsedTeamId,
            Name = name,
            Model = model,
            Role = role,
        };
        db.AgentTeammates.Add(teammate);
        await db.SaveChangesAsync();
        hubNotify.FireAndForget();
        return teammate.Id.ToString();
    }

    [McpServerTool, Description("Mark a teammate as finished (set FinishedAt). Idempotent — calling on an already-finished teammate returns 'already finished'. Does not push Discord notification (avoid spam).")]
    public static async Task<string> FinishTeammate(
        AppDbContext db,
        RecordsHubNotifyService hubNotify,
        [Description("Teammate ID (Guid string)")] string teammateId)
    {
        if (!Guid.TryParse(teammateId, out var parsedTeammateId))
            return "Error: invalid teammateId Guid";

        var teammate = await db.AgentTeammates.FindAsync(parsedTeammateId);
        if (teammate is null) return $"Error: teammate {teammateId} not found";
        if (teammate.FinishedAt is not null) return "already finished";

        teammate.FinishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        hubNotify.FireAndForget();
        return "finished";
    }

    [McpServerTool, Description("Record a task lifecycle event. Action: 'create' creates new task and returns task_id / 'claim' assigns to teammate (idempotent same teammate / reject cross teammate) / 'complete' marks done / 'fail' marks failed.")]
    public static async Task<string> RecordTask(
        AppDbContext db,
        RecordNotificationService notify,
        RecordsHubNotifyService hubNotify,
        [Description("Action: create | claim | complete | fail")] string action,
        [Description("Team ID (required for create)")] string? teamId = null,
        [Description("Task ID (required for claim/complete/fail)")] string? taskId = null,
        [Description("Teammate ID (required for claim)")] string? teammateId = null,
        [Description("Task title (required for create)")] string? title = null,
        [Description("Task description (optional for create)")] string? description = null,
        [Description("JSON array of dependency task IDs (optional for create)")] string? dependenciesJson = null,
        [Description("Error message (optional for fail)")] string? errorMessage = null)
    {
        switch (action.ToLowerInvariant())
        {
            case "create":
                if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(title))
                    return "Error: 'create' requires teamId and title";
                if (!Guid.TryParse(teamId, out var ctId))
                    return "Error: invalid teamId Guid";
                var task = new AgentTask
                {
                    TeamId = ctId,
                    Title = title,
                    Description = description,
                    DependenciesJson = dependenciesJson,
                };
                db.AgentTasks.Add(task);
                await db.SaveChangesAsync();
                hubNotify.FireAndForget();
                return task.Id.ToString();

            case "claim":
                if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(teammateId))
                    return "Error: 'claim' requires taskId and teammateId";
                if (!Guid.TryParse(taskId, out var clTaskId) || !Guid.TryParse(teammateId, out var clTmId))
                    return "Error: invalid Guid";
                var t = await db.AgentTasks.FindAsync(clTaskId);
                if (t is null) return $"Error: task {taskId} not found";
                // F15: 已 claim 不 overwrite — 同 teammate idempotent / cross teammate reject
                if (t.TeammateId is not null)
                {
                    return t.TeammateId == clTmId
                        ? "already claimed"
                        : $"already claimed by {t.TeammateId}";
                }
                t.TeammateId = clTmId;
                t.Status = "in_progress";
                t.ClaimedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                hubNotify.FireAndForget();
                return "claimed";

            case "complete":
                if (string.IsNullOrEmpty(taskId)) return "Error: 'complete' requires taskId";
                if (!Guid.TryParse(taskId, out var cpTaskId))
                    return "Error: invalid taskId Guid";
                var tc = await db.AgentTasks.FindAsync(cpTaskId);
                if (tc is null) return $"Error: task {taskId} not found";
                tc.Status = "completed";
                tc.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                hubNotify.FireAndForget();
                _ = Task.Run(() => notify.SendAsync($"✅ Task 完成 — **{tc.Title}** (id=`{tc.Id.ToString()[..8]}`)"));
                return "completed";

            case "fail":
                if (string.IsNullOrEmpty(taskId)) return "Error: 'fail' requires taskId";
                if (!Guid.TryParse(taskId, out var fTaskId))
                    return "Error: invalid taskId Guid";
                var tf = await db.AgentTasks.FindAsync(fTaskId);
                if (tf is null) return $"Error: task {taskId} not found";
                tf.Status = "failed";
                tf.ErrorMessage = errorMessage;
                tf.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                hubNotify.FireAndForget();
                _ = Task.Run(() => notify.SendAsync($"❌ Task 失敗 — **{tf.Title}** (id=`{tf.Id.ToString()[..8]}`)\nError: {errorMessage ?? "（無訊息）"}"));
                return "failed";

            default:
                return $"Error: unknown action '{action}' (use create | claim | complete | fail)";
        }
    }

    [McpServerTool, Description("Record a single message from a teammate (user / assistant / tool).")]
    public static async Task<string> RecordMessage(
        AppDbContext db,
        RecordsHubNotifyService hubNotify,
        [Description("Teammate ID (Guid string)")] string teammateId,
        [Description("Role: user | assistant | tool")] string role,
        [Description("Message content (text)")] string content,
        [Description("Associated task ID (optional)")] string? taskId = null,
        [Description("Tool call payload as JSON (only for role=tool)")] string? toolCallJson = null)
    {
        if (!Guid.TryParse(teammateId, out var tmId))
            return "Error: invalid teammateId Guid";

        Guid? parsedTaskId = null;
        if (!string.IsNullOrEmpty(taskId))
        {
            if (!Guid.TryParse(taskId, out var ti)) return "Error: invalid taskId Guid";
            parsedTaskId = ti;
        }

        var msg = new AgentMessage
        {
            TeammateId = tmId,
            TaskId = parsedTaskId,
            Role = role,
            Content = content,
            ToolCallJson = toolCallJson,
        };
        db.AgentMessages.Add(msg);
        await db.SaveChangesAsync();
        hubNotify.FireAndForget();
        return msg.Id.ToString();
    }

    [McpServerTool, Description("Record token usage from a single LLM call (Input / Output tokens only).")]
    public static async Task<string> RecordTokenUsage(
        AppDbContext db,
        RecordsHubNotifyService hubNotify,
        [Description("Teammate ID (Guid string)")] string teammateId,
        [Description("Input tokens count")] int inputTokens,
        [Description("Output tokens count")] int outputTokens,
        [Description("Associated task ID (optional)")] string? taskId = null,
        [Description("Model used (optional, e.g., 'sonnet', 'opus')")] string? model = null)
    {
        if (!Guid.TryParse(teammateId, out var tmId))
            return "Error: invalid teammateId Guid";

        Guid? parsedTaskId = null;
        if (!string.IsNullOrEmpty(taskId))
        {
            if (!Guid.TryParse(taskId, out var ti)) return "Error: invalid taskId Guid";
            parsedTaskId = ti;
        }

        var usage = new AgentTokenUsage
        {
            TeammateId = tmId,
            TaskId = parsedTaskId,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            Model = model,
        };
        db.AgentTokenUsages.Add(usage);
        await db.SaveChangesAsync();
        hubNotify.FireAndForget();
        return usage.Id.ToString();
    }
}
