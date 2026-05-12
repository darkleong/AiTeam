using AiTeam.Bot.Agents;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：Petra Orchestrator — v5 動態架構核心（路線 A 拍板對齊 Stage 63A spike 已驗 path）。
///
/// 設計核心三層：
/// 1. **DecideAsync**（限制 (a) workaround — 自寫不走 framework GroupChatManager loop）：
///    Petra LLM 一次性決策 capability 序列，由 CLAUDE_Petra.md 三 trigger 條件規則
///    （1-on-1 / Design / Kickoff）動態回傳 `|` 分隔 capability 名。
/// 2. **AgentWorkflowBuilder.BuildSequential**（對齊 Stage 63A spike 已驗 path / framework 投資保留）：
///    把 Worker AIAgent 序列包成 sequential workflow，由 InProcessExecution 跑。
/// 3. **ChatClientAgent + ClaudeCodeChatClientAdapter**（限制 (b) workaround — 在 Worker.CreateAgent 內）：
///    確保 Worker 真實被 framework dispatch（base AIAgent subclass 不被 dispatch 的限制已繞）。
///
/// per-task session 持久化：每次 dispatch 寫 PetraSessionMessage，重啟由 PetraSessionRecoveryService rebuild context。
/// </summary>
public class PetraOrchestratorService(
    IEnumerable<IAgentTool> tools,
    PetraSessionRepository sessionRepo,
    AppDbContext db,
    LlmProviderFactory providerFactory,
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    ILogger<PetraOrchestratorService> logger)
{
    private const string PetraAgentName = "PM";   // 對齊既有 appsettings.json BotAgentSettings.Agents.PM 鍵
    private const string DefaultPetraTemplate = "Resources/CLAUDE_Petra.md";

    /// <summary>啟動新 session — Petra 動態決策 + BuildSequential dispatch。taskGroupId 可為 null（spike forward path 無 TaskGroup）。</summary>
    public async Task<PetraOrchestratorResult> StartAsync(
        Guid? taskGroupId,
        string taskInput,
        CancellationToken ct = default)
    {
        var ctx = BuildSessionContext(taskGroupId);
        var session = sessionRepo.Start(taskGroupId);
        await db.SaveChangesAsync(ct);
        var sessionWithCtx = ctx with { SessionId = session.Id };

        sessionRepo.AppendMessage(session.Id, "user", taskInput);
        await db.SaveChangesAsync(ct);

        try
        {
            var toolsList = tools.ToList();
            logger.LogInformation(
                "PetraOrchestrator 啟動 — sessionId={SessionId} taskGroupId={TaskGroupId} toolsCount={Count}",
                session.Id, taskGroupId, toolsList.Count);

            // 1. DecideAsync（限制 (a) workaround — 自寫不走 framework GroupChatManager loop）
            var (decidedCapabilities, picks) = await DecideAsync(taskInput, toolsList, sessionWithCtx, ct);
            if (picks.Count == 0)
            {
                logger.LogWarning("Petra 動態決策回空序列 sessionId={SessionId}", session.Id);
                await sessionRepo.CompleteAsync(session.Id, ct);
                await db.SaveChangesAsync(ct);
                return PetraOrchestratorResult.Empty(session.Id);
            }

            // 2. BuildSequential + InProcessExecution（對齊 Stage 63A spike 已驗 path）
            var workerAgents = picks.Select(t => t.CreateAgent(sessionWithCtx)).ToArray();
            var workflow = AgentWorkflowBuilder.BuildSequential(workerAgents);
            var initial = new ChatMessage(ChatRole.User, taskInput);

            await using var run = await InProcessExecution.RunStreamingAsync(workflow, initial, cancellationToken: ct);
            // Trial_v9 揭：BuildSequential workflow 需要 TurnToken 觸發 first turn 才會 fire executor — 對齊官方 doc Sequential Orchestration 範例（learn.microsoft.com/agent-framework/workflows/orchestrations/sequential）。沒這條 → 0 worker 真實 dispatch + 7 秒 idle 完成。
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
            await foreach (var ev in run.WatchStreamAsync().WithCancellation(ct))
            {
                LogWorkflowEvent(session.Id, ev);
                if (ev is WorkflowOutputEvent) break;
            }
            await db.SaveChangesAsync(ct);

            await sessionRepo.CompleteAsync(session.Id, ct);
            await db.SaveChangesAsync(ct);

            var summary = $"Petra 完成 {picks.Count} worker dispatch（{string.Join(" → ", picks.Select(p => p.Name))}）。";
            return PetraOrchestratorResult.Done(session.Id, decidedCapabilities, summary);
        }
        catch (OperationCanceledException)
        {
            await sessionRepo.EscalateAsync(session.Id, CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PetraOrchestrator 執行失敗 sessionId={SessionId}", session.Id);
            await sessionRepo.EscalateAsync(session.Id, CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
            return PetraOrchestratorResult.Failure(session.Id, Array.Empty<string>(), ex.Message);
        }
    }

    /// <summary>
    /// 重啟 rebuild context（PetraSessionRecoveryService 用）— 紀律：重啟重跑不從 checkpoint resume。
    /// 從 task 原始 input + 已 responded BossInteraction 紀錄重跑 DecideAsync + BuildSequential。
    /// </summary>
    public async Task<PetraOrchestratorResult> ResumeAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await sessionRepo.GetWithMessagesAsync(sessionId, ct);
        if (session is null)
        {
            logger.LogWarning("PetraOrchestrator.ResumeAsync 找不到 sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "session 不存在");
        }

        // 從原始 user message + 已 responded BossInteraction 拼成 task input（重啟重跑不雙重 ask）
        var taskInput = await BuildResumeInputAsync(session, ct);
        logger.LogInformation(
            "PetraOrchestrator resume sessionId={SessionId} 重新從 task input 跑（重啟重跑紀律）",
            sessionId);

        // 直接走 StartAsync 但是用既有 session（不建新 session）
        // PoC 簡化：mark 既有 session done + 開新 session
        await sessionRepo.CompleteAsync(sessionId, ct);
        await db.SaveChangesAsync(ct);
        return await StartAsync(session.TaskGroupId, taskInput, ct);
    }

    /// <summary>
    /// DecideAsync — Petra LLM 動態決策 capability 序列（限制 (a) workaround）。
    /// CLAUDE_Petra.md 三 trigger 條件 prompt + 回傳 List&lt;IAgentTool&gt; picks。
    /// </summary>
    private async Task<(List<string> Capabilities, List<IAgentTool> Picks)> DecideAsync(
        string taskInput,
        IReadOnlyList<IAgentTool> tools,
        PetraSessionContext ctx,
        CancellationToken ct)
    {
        var capabilityRoster = string.Join(", ", tools.SelectMany(t => t.Capabilities).Distinct());
        var systemPrompt = BuildPetraSystemPrompt(capabilityRoster);

        var provider = providerFactory.Create(PetraAgentName);
        var response = await provider.CompleteAsync(systemPrompt, $"任務：{taskInput}", ct);

        sessionRepo.AppendMessage(ctx.SessionId, "assistant", response.Content);

        var raw = response.Content.Trim().Split('\n')[0].Trim();
        var caps = raw.Split('|')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        var picks = new List<IAgentTool>();
        foreach (var cap in caps)
        {
            var tool = tools.FirstOrDefault(t =>
                t.Capabilities.Any(c => string.Equals(c, cap, StringComparison.OrdinalIgnoreCase)));
            if (tool is not null)
            {
                picks.Add(tool);
            }
            else
            {
                logger.LogWarning("Petra 動態決策回未知 capability={Cap}（忽略）", cap);
            }
        }

        logger.LogInformation(
            "Petra DecideAsync 完成 — raw=「{Raw}」picks={Picks}",
            raw, string.Join(" → ", picks.Select(p => p.Name)));

        return (caps, picks);
    }

    // Trial_v9 修：對齊官方 doc Sequential Orchestration 範例 — fire 真實 events 是 AgentResponseUpdateEvent（intermediate worker output）+ ExecutorInvokedEvent + ExecutorCompletedEvent + WorkflowOutputEvent（terminal）
    // accumulator 累積每個 worker streaming update text，executor complete 時 flush 寫 tool message
    private readonly Dictionary<string, System.Text.StringBuilder> _executorAccumulators = new();

    private void LogWorkflowEvent(Guid sessionId, WorkflowEvent ev)
    {
        var typeName = ev.GetType().Name;

        // AgentResponseUpdateEvent: intermediate worker streaming output（對齊官方 doc 範例 evt is AgentResponseUpdateEvent）
        if (ev is AgentResponseUpdateEvent aru)
        {
            var execId = aru.ExecutorId ?? "(unknown)";
            if (!_executorAccumulators.TryGetValue(execId, out var sb))
            {
                sb = new System.Text.StringBuilder();
                _executorAccumulators[execId] = sb;
            }
            sb.Append(aru.Update.Text);
            logger.LogTrace("Workflow event AgentResponseUpdate executor={Exec} delta={Len}", execId, aru.Update.Text?.Length ?? 0);
            return;
        }

        // ExecutorCompletedEvent: flush accumulator 寫 tool message + 清 buffer
        if (ev is ExecutorCompletedEvent ece)
        {
            var execId = ece.ExecutorId ?? "(unknown)";
            string toolText;
            if (_executorAccumulators.TryGetValue(execId, out var sb) && sb.Length > 0)
            {
                toolText = sb.ToString();
                _executorAccumulators.Remove(execId);
            }
            else
            {
                toolText = ece.Data?.ToString() ?? "";
            }

            if (!string.IsNullOrWhiteSpace(toolText))
            {
                sessionRepo.AppendMessage(sessionId, "tool", $"[{execId}] {toolText}");
            }
            logger.LogInformation("Workflow event ExecutorCompleted executor={Exec} outputLen={Len}", execId, toolText.Length);
            return;
        }

        // ExecutorInvokedEvent / WorkflowStartedEvent / WorkflowOutputEvent / 其他 — log only
        if (ev is ExecutorInvokedEvent eie)
        {
            logger.LogInformation("Workflow event ExecutorInvoked executor={Exec}", eie.ExecutorId);
            return;
        }

        logger.LogInformation("Workflow event {Type}", typeName);
    }

    private async Task<string> BuildResumeInputAsync(PetraSession session, CancellationToken ct)
    {
        var firstUserMsg = session.Messages
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefault(m => m.Role == "user")?.Content ?? "";

        // 取已 responded BossInteraction 算 task input（不雙重 ask）
        var responded = session.TaskGroupId is null
            ? new List<BossInteraction>()
            : await db.BossInteractions
                .Where(x => x.TaskGroupId == session.TaskGroupId && x.Status == "responded")
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(ct);

        if (responded.Count == 0) return firstUserMsg;

        var parts = new List<string> { firstUserMsg };
        foreach (var bi in responded)
        {
            parts.Add($"[已 responded] {bi.InteractionType}: {bi.ResponseAction} / {bi.ResponseContent ?? ""}");
        }
        return string.Join("\n", parts);
    }

    private PetraSessionContext BuildSessionContext(Guid? taskGroupId)
    {
        var model = configuration["Agents:Dev:Model"]
                 ?? configuration["Anthropic:DefaultModel"]
                 ?? "claude-opus-4-6";
        var apiKey = configuration["Anthropic:ApiKey"] ?? "";
        // Trial_v9 揭：v4 既有 ClaudeCodeService 真實 config key 是 GitHub:WorkspacePath（docker-compose.prod.yml 設 /tmp/aiteam-workspace）— 不是 GitHub:LocalWorkRoot
        // 沒對齊 → workingDir 空字串 → ClaudeCodeService subprocess 跑容器 /app dir 不是 git repo → "fatal: not in a git directory" → Cody 卡 retry loop
        var workingDir = configuration["GitHub:WorkspacePath"] ?? "";

        return new PetraSessionContext(
            SessionId: Guid.Empty,   // caller 用 with-expression 補
            TaskGroupId: taskGroupId ?? Guid.Empty,
            Round: 0,
            Model: model,
            ApiKey: apiKey,
            WorkingDir: workingDir);
    }

    private static string BuildPetraSystemPrompt(string capabilityRoster) => $$"""
你是 Petra — Multi-Agent Orchestrator（v5 動態架構 PoC）。
依以下 trigger 條件動態決定 Worker capability 序列（用 | 分隔）：

- 1-on-1 trigger（純技術改動 < 50 行 / typo / 文件配置）→ 回「code_implementation」
- Design trigger（跨 3-5 元件 / Issue ≥ 5）→ 回「code_implementation|code_review」
- Kickoff trigger（架構決策 / 跨多領域）→ 回「code_implementation|code_review|code_implementation|code_review」

可選 capability：{{capabilityRoster}}

**只回 capability 序列**（不要解釋 / 不要 markdown / 不要 backtick，例如：code_implementation|code_review）
""";
}
