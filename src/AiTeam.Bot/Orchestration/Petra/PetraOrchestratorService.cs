using AiTeam.Bot.Agents;
using AiTeam.Bot.GitHub;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using LibGit2Sharp;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：Petra Orchestrator — v5 動態架構核心（路線 A 拍板對齊 Stage 63A spike 已驗 path）。
///
/// 設計核心三層：
/// 1. **DecideAsync**（不走 framework GroupChatManager loop — Stage 63A spike 揭 base subclass 不啟動 manager loop）：
///    Petra LLM 一次性決策 capability 序列，由三 trigger 條件規則（1-on-1 / Design / Kickoff）動態回傳 `|` 分隔 capability 名。
/// 2. **AgentWorkflowBuilder.BuildSequential**（對齊 Stage 63A spike 已驗 path / framework 投資保留）：
///    把 Worker AIAgent 序列包成 sequential workflow，由 InProcessExecution 跑（需 TurnToken 觸發 first turn — 對齊官方 doc）。
/// 3. **ChatClientAgent + ClaudeCodeChatClientAdapter**（在 Worker.CreateAgent 內）：
///    IClaudeCodeService 是 CLI subprocess pattern 非 IChatClient — adapter 必走 wrap（Stage 64 errata 修正：非「base AIAgent 不被 dispatch」誤判）。
///
/// per-task session 持久化：每次 dispatch 寫 PetraSessionMessage，重啟由 PetraSessionRecoveryService rebuild context。
///
/// Stage 64 補強：
/// - BuildSessionContext 補主動 CloneOrPull wire（Aria 必修 2 — Trial_v9 揭 workspace recreate 不持久 + 不 clone 雙缺口）
/// - StartAsync 收尾段補 git commit/push/PR wire（沿用 v4 既有 GitHubService.CommitAll/Push/OpenPullRequestAsync API）
/// - BuildPetraSystemPrompt 升級三 trigger 具體判準（範例 + 反例 + 紀律 — Trial_v9 揭 Petra prompt 太簡）
/// </summary>
public class PetraOrchestratorService(
    IEnumerable<IAgentTool> tools,
    PetraSessionRepository sessionRepo,
    AppDbContext db,
    LlmProviderFactory providerFactory,
    GitHubService gitHubService,
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    ILogger<PetraOrchestratorService> logger)
{
    private const string PetraAgentName = "PM";   // 對齊既有 appsettings.json BotAgentSettings.Agents.PM 鍵

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
                "PetraOrchestrator 啟動 — sessionId={SessionId} taskGroupId={TaskGroupId} toolsCount={Count} workingDir={Dir}",
                session.Id, taskGroupId, toolsList.Count, sessionWithCtx.WorkingDir);

            // 1. DecideAsync — Petra LLM 動態決策 capability 序列
            var (decidedCapabilities, picks) = await DecideAsync(taskInput, toolsList, sessionWithCtx, ct);
            if (picks.Count == 0)
            {
                logger.LogWarning("Petra 動態決策回空序列 sessionId={SessionId}", session.Id);
                await sessionRepo.CompleteAsync(session.Id, ct);
                await db.SaveChangesAsync(ct);
                return PetraOrchestratorResult.Empty(session.Id);
            }

            // 2. Stage 66：PetraOrchestratorService 自管 chain dispatch（取代 BuildSequential framework chain — 修 Vera 0 work 根因 GitHub #1308）。
            //    framework BuildSequential edge 在 nuget 1.3.0 不會把 first agent output 餵下個 agent，自管 chain 完全 bypass。
            //    BuildSequential 路徑既有 import / LogWorkflowEvent / _executorAccumulators 保留 reference（未來 framework 修 #1308 後評估回切）。
            var workerAgents = picks.Select(t => t.CreateAgent(sessionWithCtx)).ToArray();
            var dispatchSummaries = await DispatchWorkersAsync(session.Id, taskInput, decidedCapabilities, picks, workerAgents, ct);
            await db.SaveChangesAsync(ct);

            // Stage 64 子項 2：Workers 完成後 git commit/push/PR 接通（沿用 v4 GitHubService.CommitAll/Push/OpenPullRequestAsync API）。
            // 無 git diff → 不誤建 PR。Mock 階段 workingDir 不是 git repo → FinalizeGitAsync 內捕例外 + log warning 不擋流程（adapter 跑 Mock 時 workingDir 通常為空）。
            var prUrl = await FinalizeGitAsync(sessionWithCtx, taskInput, decidedCapabilities, picks, ct);

            await sessionRepo.CompleteAsync(session.Id, ct);
            await db.SaveChangesAsync(ct);

            var summary = $"Petra 完成 {picks.Count} worker dispatch（{string.Join(" → ", picks.Select(p => p.Name))}）"
                + (prUrl is null ? "。" : $" + PR {prUrl}。");
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
    /// DecideAsync — Petra LLM 動態決策 capability 序列。
    /// 三 trigger 條件 prompt（升級版見 BuildPetraSystemPrompt）+ 回傳 List&lt;IAgentTool&gt; picks。
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

    /// <summary>
    /// Stage 66：自管 chain dispatch — picks 序列改由 Petra 自己跑（取代 framework BuildSequential，修 GitHub #1308 root cause）。
    /// 同位置同 transaction 寫 PetraSessionMessages tool role（議題 1+2 合併修法位置 — 避免兩段獨立寫入時序漏洞）。
    /// </summary>
    private async Task<List<WorkerDispatchSummary>> DispatchWorkersAsync(
        Guid sessionId,
        string taskInput,
        IReadOnlyList<string> decidedCapabilities,
        IReadOnlyList<IAgentTool> picks,
        AIAgent[] workerAgents,
        CancellationToken ct)
    {
        var summaries = new List<WorkerDispatchSummary>(workerAgents.Length);
        for (var i = 0; i < workerAgents.Length; i++)
        {
            var workerAgent = workerAgents[i];
            var workerName = picks[i].Name;
            // picks 與 decidedCapabilities 同 index 對齊 — DecideAsync 已 filter unknown cap 後保持順序
            var capability = i < decidedCapabilities.Count ? decidedCapabilities[i] : picks[i].Capabilities.FirstOrDefault() ?? "";

            var inputMessages = i == 0
                ? new List<ChatMessage> { new(ChatRole.User, taskInput) }
                : BuildNextWorkerInput(taskInput, summaries);

            logger.LogInformation(
                "PetraOrchestrator 自管 chain dispatch {Index}/{Total} worker={Worker} capability={Cap} inputMsgs={N} sessionId={SessionId}",
                i + 1, workerAgents.Length, workerName, capability, inputMessages.Count, sessionId);

            var response = await workerAgent.RunAsync(inputMessages, session: null, options: null, ct);
            var outputText = response.Text ?? "";

            var toolCallId = Guid.NewGuid().ToString("N");
            var toolMessage = BuildToolMessage(workerName, capability, outputText);
            sessionRepo.AppendMessage(sessionId, "tool", toolMessage, toolCallId);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "PetraOrchestrator 自管 chain dispatch 完成 {Index}/{Total} worker={Worker} outputLen={Len} toolCallId={ToolCallId}",
                i + 1, workerAgents.Length, workerName, outputText.Length, toolCallId);

            summaries.Add(new WorkerDispatchSummary(workerName, capability, outputText, toolCallId));
        }
        return summaries;
    }

    /// <summary>
    /// Stage 66：拼後續 worker input — 原 task input + 前面 worker 已做的 capability + 結果摘要。
    /// 抽 method 留 future prompt DB 化 inject 點（Christ 2026-05-14 拍板）— 未來把 template content 從 method body 換成 DB 讀。
    /// </summary>
    private static List<ChatMessage> BuildNextWorkerInput(
        string taskInput,
        IReadOnlyList<WorkerDispatchSummary> prev)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, taskInput),
        };
        foreach (var s in prev)
        {
            var summaryText = $"[前一個 worker：{s.WorkerName}（capability={s.Capability}）已完成]\n\n{s.Output}";
            messages.Add(new ChatMessage(ChatRole.Assistant, summaryText));
        }
        return messages;
    }

    /// <summary>
    /// Stage 66：產 tool role message content — worker dispatch 結果摘要寫入 PetraSessionMessages。
    /// 抽 method 留 future prompt DB 化 inject 點（同 BuildNextWorkerInput 紀律）。
    /// </summary>
    private static string BuildToolMessage(string workerName, string capability, string output)
    {
        const int maxLen = 2000;
        var truncated = output.Length > maxLen ? output[..maxLen] + "...(truncated)" : output;
        return $"[{workerName}|{capability}|outputLen={output.Length}]\n{truncated}";
    }

    /// <summary>
    /// Stage 64 子項 2：Workers 完成後 git commit/push/PR 接通。
    /// 範圍邊界：最小整合 — 不重做 v4 dev_plan / fix_loop / metadata 機制（留 Stage 65+）。
    /// 無 diff → 不誤建 PR；非 git repo → 捕例外 log warning 不擋流程。
    /// </summary>
    private async Task<string?> FinalizeGitAsync(
        PetraSessionContext ctx,
        string taskInput,
        IReadOnlyList<string> caps,
        IReadOnlyList<IAgentTool> picks,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ctx.WorkingDir) || !Directory.Exists(Path.Combine(ctx.WorkingDir, ".git")))
        {
            logger.LogInformation("Petra FinalizeGitAsync skip — workingDir 非 git repo（Mock 階段或 spike forward path）sessionId={SessionId}", ctx.SessionId);
            return null;
        }

        var owner = configuration["GitHub:Owner"] ?? "";
        var repo = configuration["GitHub:DefaultRepo"] ?? "";
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
        {
            logger.LogWarning("Petra FinalizeGitAsync skip — GitHub:Owner 或 GitHub:DefaultRepo 未設定 sessionId={SessionId}", ctx.SessionId);
            return null;
        }

        try
        {
            // Check diff (LibGit2Sharp)
            using (var gitRepo = new Repository(ctx.WorkingDir))
            {
                var status = gitRepo.RetrieveStatus();
                if (!status.IsDirty)
                {
                    logger.LogInformation("Petra FinalizeGitAsync skip — workingDir 無 git diff（worker 0 變更不誤建 PR）sessionId={SessionId}", ctx.SessionId);
                    return null;
                }
            }

            // 自動產 branch name：petra/{taskGroup-8}-{session-8}-{yyyyMMddHHmm}
            // taskGroupId Empty.Guid → 走 spike- prefix；timestamp 防同 session retry 撞 branch
            var taskGroupShort = ctx.TaskGroupId == Guid.Empty
                ? "spike"
                : ctx.TaskGroupId.ToString("N")[..8];
            var sessionShort = ctx.SessionId.ToString("N")[..8];
            var ts = DateTime.UtcNow.ToString("yyyyMMddHHmm");
            var branchName = $"petra/{taskGroupShort}-{sessionShort}-{ts}";

            gitHubService.CreateAndCheckoutBranch(ctx.WorkingDir, branchName);
            logger.LogInformation("Petra branch 建立 + checkout：{Branch} sessionId={SessionId}", branchName, ctx.SessionId);

            // commit message：Petra dispatch summary + 第一行任務截短
            var taskFirstLine = (taskInput ?? "").Split('\n').FirstOrDefault() ?? "";
            var taskSummary = taskFirstLine.Length > 60 ? taskFirstLine[..60] + "..." : taskFirstLine;
            var commitMessage = $"[Petra] {taskSummary}\n\nDispatch: {string.Join(" → ", caps)}";
            gitHubService.CommitAll(ctx.WorkingDir, commitMessage);
            gitHubService.Push(ctx.WorkingDir, branchName);

            // PR body：Petra 決策 + worker summary（從 PetraSessionMessages tool role 取）
            var workerSummaries = await db.PetraSessionMessages
                .Where(m => m.SessionId == ctx.SessionId && m.Role == "tool")
                .OrderBy(m => m.CreatedAt)
                .Select(m => m.Content)
                .ToListAsync(ct);

            var prBody = BuildPrBody(taskInput, caps, picks, workerSummaries);
            var prTitle = $"[Petra v5] {taskSummary}";

            var prUrl = await gitHubService.OpenPullRequestAsync(owner, repo, prTitle, prBody, branchName);
            logger.LogInformation("Petra PR 開啟：{PrUrl} sessionId={SessionId}", prUrl, ctx.SessionId);
            return prUrl;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Petra FinalizeGitAsync 失敗（不影響 session complete）sessionId={SessionId}", ctx.SessionId);
            return null;
        }
    }

    private static string BuildPrBody(
        string? taskInput,
        IReadOnlyList<string> caps,
        IReadOnlyList<IAgentTool> picks,
        IReadOnlyList<string> workerSummaries)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## 任務");
        sb.AppendLine(taskInput ?? "");
        sb.AppendLine();
        sb.AppendLine("## Petra 動態決策");
        sb.AppendLine($"- Capability 序列：`{string.Join(" | ", caps)}`");
        sb.AppendLine($"- Workers dispatch 順序：{string.Join(" → ", picks.Select(p => p.Name))}");
        sb.AppendLine();
        sb.AppendLine("## Worker 完成 summary");
        if (workerSummaries.Count == 0)
        {
            sb.AppendLine("（無 tool role 紀錄）");
        }
        else
        {
            foreach (var s in workerSummaries)
            {
                sb.AppendLine($"- {s}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("🤖 由 AiTeam Petra Orchestrator（v5 動態架構 PoC）自動產出");
        return sb.ToString();
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

    /// <summary>
    /// Stage 64 子項 4b（Aria 必修 2）：v5 PoC 漏接 CloneOrPull wire — 對齊 v4 DevAgentService.cs:138 既有主動 clone pattern。
    /// 既有 CloneOrPull 防護「dir 存在但無 .git → 清理後 clone」（GitHubService.cs:160-165）— wire 通了就 cover 空 / 缺 .git 兩維度。
    /// v5 PoC 採 single shared clone（uniqueSuffix=null → {WorkspacePath}/AiTeam）— 不走 v4 per-task subfolder。
    /// </summary>
    private PetraSessionContext BuildSessionContext(Guid? taskGroupId)
    {
        var model = configuration["Agents:Dev:Model"]
                 ?? configuration["Anthropic:DefaultModel"]
                 ?? "claude-opus-4-6";
        var apiKey = configuration["Anthropic:ApiKey"] ?? "";

        var owner = configuration["GitHub:Owner"] ?? "";
        var repo = configuration["GitHub:DefaultRepo"] ?? "";

        string workingDir;
        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
        {
            try
            {
                workingDir = gitHubService.CloneOrPull(owner, repo, uniqueSuffix: null);
                logger.LogInformation("Petra BuildSessionContext CloneOrPull 完成 workingDir={Dir}", workingDir);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Petra BuildSessionContext CloneOrPull 失敗 fallback raw WorkspacePath");
                workingDir = configuration["GitHub:WorkspacePath"] ?? "";
            }
        }
        else
        {
            logger.LogWarning("GitHub:Owner 或 GitHub:DefaultRepo 未設定，Petra workingDir fallback 到 raw WorkspacePath（CloneOrPull skip）");
            workingDir = configuration["GitHub:WorkspacePath"] ?? "";
        }

        return new PetraSessionContext(
            SessionId: Guid.Empty,   // caller 用 with-expression 補
            TaskGroupId: taskGroupId ?? Guid.Empty,
            Round: 0,
            Model: model,
            ApiKey: apiKey,
            WorkingDir: workingDir);
    }

    /// <summary>
    /// Stage 64 子項 3：Petra DecideAsync prompt 升級（三 trigger 具體判準 + 範例 + 反例 + 輸出紀律）。
    /// 不對齊 CLAUDE_Petra.md「四審核閘門」段（不同職能 — CLAUDE_Petra.md 仍服務 v4 pm_review path 保留不刪）。
    /// 對 Gemini Flash 友善：聚焦 ~35 行 dispatch 決策核心，避免 221 行整檔 prompt 干擾輕量模型解析。
    /// </summary>
    private static string BuildPetraSystemPrompt(string capabilityRoster) => $$"""
你是 Petra — v5 動態架構 Multi-Agent Orchestrator。
依任務規模 + 三 trigger 條件動態決定 Worker capability 序列。

【可選 capability】{{capabilityRoster}}

【三 trigger 條件具體判斷準則】

★ 1-on-1 trigger（純技術改動 / 配置 / 文件 / typo）
  判準：< 50 行改動 / 單檔範圍 / 無架構決策
  範例：「修 README typo」「調 Gemini BaseUrl 預設值」「rename 一個變數」
  → 回「code_implementation」

★ Design trigger（跨 3-5 元件 / 中型功能 / 需 review）
  判準：Issue ≥ 5 OR 跨多檔 OR 涉及 API/DTO 邊界
  範例：「Dashboard 加 Petra session 列表頁」「新增一個 Agent 設定欄位」
  → 回「code_implementation|code_review」

★ Kickoff trigger（架構決策 / 跨多領域 / 大型功能）
  判準：新 Service 層 / 新 framework wire / 跨 domain 互動
  範例：「v5 動態架構 PoC」「新增 Memory module」
  → 回「code_implementation|code_review|code_implementation|code_review」

【輸出紀律】
- 只回 capability 序列（用 `|` 分隔）
- 不要 markdown 包裹 / 不要 backtick / 不要解釋 / 不要 prefix 「output:」
- 不要回 Worker 名稱（例如「Cody」），只回 capability tag
- 反例：```code_implementation|code_review```（錯：backtick 包裹）
- 反例：「我建議 code_implementation」（錯：解釋）
- 正例：`code_implementation|code_review`
""";
}
