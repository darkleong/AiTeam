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
///   - register_teammate       — Lead spawn teammate 時 call、回 teammate_id（Stage 91 自決加 / 對齊運作流程）
///   - record_task             — Task lifecycle 事件（action: create / claim / complete / fail）
///   - record_conversation     — Teammate 對話 message 單筆寫入
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
        [Description("Team name (e.g., 'feature-x-team', 'bug-investigation-team')")] string name,
        [Description("High-level intent the boss gave to this team (optional)")] string? description = null)
    {
        var team = new AgentTeam
        {
            Name = name,
            Description = description,
        };
        db.AgentTeams.Add(team);
        await db.SaveChangesAsync();

        _ = Task.Run(() => notify.SendAsync($"📋 新 Agent Team 開始 — **{name}** (id=`{team.Id.ToString()[..8]}`)\n{description ?? ""}".TrimEnd()));

        return team.Id.ToString();
    }

    [McpServerTool, Description("Register a new teammate (lead or member) in an existing team. Returns the new teammate_id (Guid string).")]
    public static async Task<string> RegisterTeammate(
        AppDbContext db,
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
        return teammate.Id.ToString();
    }

    [McpServerTool, Description("Record a task lifecycle event. Action: 'create' creates new task and returns task_id / 'claim' assigns to teammate / 'complete' marks done / 'fail' marks failed.")]
    public static async Task<string> RecordTask(
        AppDbContext db,
        RecordNotificationService notify,
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
                return task.Id.ToString();

            case "claim":
                if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(teammateId))
                    return "Error: 'claim' requires taskId and teammateId";
                if (!Guid.TryParse(taskId, out var clTaskId) || !Guid.TryParse(teammateId, out var clTmId))
                    return "Error: invalid Guid";
                var t = await db.AgentTasks.FindAsync(clTaskId);
                if (t is null) return $"Error: task {taskId} not found";
                t.TeammateId = clTmId;
                t.Status = "in_progress";
                t.ClaimedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
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
                _ = Task.Run(() => notify.SendAsync($"❌ Task 失敗 — **{tf.Title}** (id=`{tf.Id.ToString()[..8]}`)\nError: {errorMessage ?? "（無訊息）"}"));
                return "failed";

            default:
                return $"Error: unknown action '{action}' (use create | claim | complete | fail)";
        }
    }

    [McpServerTool, Description("Record a single conversation message from a teammate (user / assistant / tool message).")]
    public static async Task<string> RecordConversation(
        AppDbContext db,
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
        return msg.Id.ToString();
    }

    [McpServerTool, Description("Record token usage from a single LLM call. Estimated cost in USD is computed by caller (or null).")]
    public static async Task<string> RecordTokenUsage(
        AppDbContext db,
        [Description("Teammate ID (Guid string)")] string teammateId,
        [Description("Input tokens count")] int inputTokens,
        [Description("Output tokens count")] int outputTokens,
        [Description("Associated task ID (optional)")] string? taskId = null,
        [Description("Cache creation tokens (optional)")] int? cacheCreationTokens = null,
        [Description("Cache read tokens (optional)")] int? cacheReadTokens = null,
        [Description("Model used (optional, e.g., 'sonnet', 'opus')")] string? model = null,
        [Description("Estimated cost in USD (optional, caller computes)")] decimal? estimatedCostUsd = null)
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
            CacheCreationTokens = cacheCreationTokens,
            CacheReadTokens = cacheReadTokens,
            Model = model,
            EstimatedCostUsd = estimatedCostUsd,
        };
        db.AgentTokenUsages.Add(usage);
        await db.SaveChangesAsync();
        return usage.Id.ToString();
    }
}
