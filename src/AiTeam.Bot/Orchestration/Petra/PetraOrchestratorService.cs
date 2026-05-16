using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
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
    ITalentFactory talentFactory,
    WorkflowSettingsResolver workflowResolver,
    PetraSessionRepository sessionRepo,
    MemoryRepository memoryRepo,
    AppDbContext db,
    LlmProviderFactory providerFactory,
    GitHubService gitHubService,
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    ILogger<PetraOrchestratorService> logger)
{
    private const string PetraAgentName = "PM";   // 對齊既有 appsettings.json BotAgentSettings.Agents.PM 鍵

    // Stage 67：v5.5 path round-robin counter（PetraOrchestratorService 是 Scoped — session 級無需 thread-safe）。
    private int _roundRobinCounter;

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

        await sessionRepo.AppendMessageAsync(session.Id, "user", taskInput, ct: ct);
        await db.SaveChangesAsync(ct);

        try
        {
            // Stage 67：v5.5 path 切換 — runtime flag 讀（DB app_settings 優先 / appsettings.json fallback）。
            // flag=true 走 ITalent + GenericAgentTool path（Talent pool / round-robin）/ flag=false 走 v5 既有 IAgentTool path（守 fallback）。
            var useTalentSkillSeparation = await workflowResolver.GetUseTalentSkillSeparationAsync(ct);

            List<string> decidedCapabilities;
            List<string> dispatchNames;   // worker name (v5) or talent name (v5.5) — picks 抽 Name 統一傳 FinalizeGitAsync / summary
            if (useTalentSkillSeparation)
            {
                var talentsList = await talentFactory.GetAllAsync(ct);
                // Stage 70：v5.5 Phase 2 Step 4 — UseV5SubtaskPlanning 三 flag 連動（v5 + v5.5 + SubtaskPlanning 同 true 才走 JSON SubtaskPlan path）
                var useV5SubtaskPlanning = await workflowResolver.GetUseV5SubtaskPlanningAsync(ct);
                logger.LogInformation(
                    "PetraOrchestrator 啟動 (v5.5 Talent-Skill path) — sessionId={SessionId} taskGroupId={TaskGroupId} talentsCount={Count} useV5SubtaskPlanning={SubtaskPlanning} workingDir={Dir}",
                    session.Id, taskGroupId, talentsList.Count, useV5SubtaskPlanning, sessionWithCtx.WorkingDir);

                // 1. Decide — flag=true 走 JSON SubtaskPlan / flag=false 走 Stage 69 既有 Skill 序列線性 chain（Linear 包成 SubtaskPlan 統一介面）
                SubtaskPlan plan;
                List<ITalent> talentPicks;
                if (useV5SubtaskPlanning)
                {
                    (plan, talentPicks) = await DecideTalentsWithPlanAsync(taskInput, talentsList, sessionWithCtx, ct);
                }
                else
                {
                    var (skills, picks) = await DecideTalentsAsync(taskInput, talentsList, sessionWithCtx, ct);
                    plan = SubtaskPlan.Linear(skills);
                    talentPicks = picks;
                }
                if (talentPicks.Count == 0)
                {
                    logger.LogWarning("Petra v5.5 動態決策回空序列 sessionId={SessionId}", session.Id);
                    await sessionRepo.CompleteAsync(session.Id, ct);
                    await db.SaveChangesAsync(ct);
                    return PetraOrchestratorResult.Empty(session.Id);
                }
                decidedCapabilities = plan.Subtasks.Select(s => s.SkillName).ToList();

                // 2. v5.5 自管 chain dispatch — 對齊 v5 既有 DispatchWorkersAsync pattern 但用 ITalent
                // Stage 70：plan.Subtasks index 與 talentPicks index 對齊（CreateAgent 用 plan.Subtasks[i].SkillName 動態傳）
                var talentAgents = new AIAgent[talentPicks.Count];
                for (var i = 0; i < talentPicks.Count; i++)
                {
                    talentAgents[i] = talentPicks[i].CreateAgent(sessionWithCtx, plan.Subtasks[i].SkillName);
                }

                // Stage 69 v2.1：v5.5 Phase 2 Step 3 — v5Memory flag 三 flag 連動（必須 v5 + v5.5 + memory 同時 true 才生效）
                // scope = PetraSession（不是 v4 TaskGroup）— session.Id 100% 有值，移除 taskGroupId gate
                var useV5Memory = await workflowResolver.GetUseV5MemoryAsync(ct);
                // talentName → Talent.Id lookup map（v5.5 baseline 全 ProjectId=null 全域 Talent / Phase 3 per-Project Talent 加入時需擴展此 query）
                Dictionary<string, Guid>? talentNameToIdMap = null;
                if (useV5Memory)
                {
                    var names = talentPicks.Select(t => t.Name).Distinct().ToList();
                    talentNameToIdMap = await db.Talents
                        .Where(t => names.Contains(t.Name) && t.ProjectId == null)
                        .ToDictionaryAsync(t => t.Name, t => t.Id, ct);
                }

                await DispatchTalentsAsync(
                    session.Id, taskInput, plan, talentPicks, talentAgents,
                    useV5Memory, talentNameToIdMap, ct);
                await db.SaveChangesAsync(ct);

                dispatchNames = talentPicks.Select(t => t.Name).ToList();
            }
            else
            {
                var toolsList = tools.ToList();
                logger.LogInformation(
                    "PetraOrchestrator 啟動 (v5 既有 IAgentTool path) — sessionId={SessionId} taskGroupId={TaskGroupId} toolsCount={Count} workingDir={Dir}",
                    session.Id, taskGroupId, toolsList.Count, sessionWithCtx.WorkingDir);

                // 1. DecideAsync — Petra LLM 動態決策 capability 序列
                var (caps, picks) = await DecideAsync(taskInput, toolsList, sessionWithCtx, ct);
                if (picks.Count == 0)
                {
                    logger.LogWarning("Petra 動態決策回空序列 sessionId={SessionId}", session.Id);
                    await sessionRepo.CompleteAsync(session.Id, ct);
                    await db.SaveChangesAsync(ct);
                    return PetraOrchestratorResult.Empty(session.Id);
                }
                decidedCapabilities = caps;

                // 2. Stage 66：PetraOrchestratorService 自管 chain dispatch（取代 BuildSequential framework chain — 修 Vera 0 work 根因 GitHub #1308）。
                //    framework BuildSequential edge 在 nuget 1.3.0 不會把 first agent output 餵下個 agent，自管 chain 完全 bypass。
                //    BuildSequential 路徑既有 import / LogWorkflowEvent / _executorAccumulators 保留 reference（未來 framework 修 #1308 後評估回切）。
                var workerAgents = picks.Select(t => t.CreateAgent(sessionWithCtx)).ToArray();
                await DispatchWorkersAsync(session.Id, taskInput, caps, picks, workerAgents, ct);
                await db.SaveChangesAsync(ct);

                dispatchNames = picks.Select(p => p.Name).ToList();
            }

            // Stage 64 子項 2：Workers 完成後 git commit/push/PR 接通（沿用 v4 GitHubService.CommitAll/Push/OpenPullRequestAsync API）。
            // 無 git diff → 不誤建 PR。Mock 階段 workingDir 不是 git repo → FinalizeGitAsync 內捕例外 + log warning 不擋流程（adapter 跑 Mock 時 workingDir 通常為空）。
            var prUrl = await FinalizeGitAsync(sessionWithCtx, taskInput, decidedCapabilities, dispatchNames, ct);

            await sessionRepo.CompleteAsync(session.Id, ct);
            await db.SaveChangesAsync(ct);

            var summary = $"Petra 完成 {dispatchNames.Count} dispatch（{string.Join(" → ", dispatchNames)}）"
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

        await sessionRepo.AppendMessageAsync(ctx.SessionId, "assistant", response.Content, ct: ct);

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
                // LogWorkflowEvent 是 framework callback（非 async signature）— fire-and-forget 同步 enqueue 即可（純 EF Add 無 I/O）
                _ = sessionRepo.AppendMessageAsync(sessionId, "tool", $"[{execId}] {toolText}");
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
            await sessionRepo.AppendMessageAsync(sessionId, "tool", toolMessage, toolCallId, ct);
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
    /// Stage 67：v5.5 Phase 1 Step 2 — Petra LLM 動態決策 Skill 序列 + lookup Talent pool（看 Skill 找 Talent / round-robin）。
    /// 對齊既有 DecideAsync 邏輯但 lookup 改用 Talent pool — talentPool 由 talents.Where(t.Skills.Contains(skill)) + IsPrimary desc + Priority asc 排序。
    /// baseline 1 instance 場景 pool.Count == 1 round-robin 無感 / future horizontal scaling 多 instance 自然分流。
    /// </summary>
    private async Task<(List<string> Skills, List<ITalent> TalentPicks)> DecideTalentsAsync(
        string taskInput,
        IReadOnlyList<ITalent> talents,
        PetraSessionContext ctx,
        CancellationToken ct)
    {
        // skill roster 從 talent.Skills 取（vs 既有 DecideAsync 取 tools.SelectMany(Capabilities)）— 對 LLM 等價
        var skillRoster = string.Join(", ", talents.SelectMany(t => t.Skills).Distinct(StringComparer.OrdinalIgnoreCase));
        var systemPrompt = BuildPetraSystemPrompt(skillRoster);

        var provider = providerFactory.Create(PetraAgentName);
        var response = await provider.CompleteAsync(systemPrompt, $"任務：{taskInput}", ct);

        await sessionRepo.AppendMessageAsync(ctx.SessionId, "assistant", response.Content, ct: ct);

        var raw = response.Content.Trim().Split('\n')[0].Trim();
        var skills = raw.Split('|')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        var talentPicks = new List<ITalent>();
        var validSkills = new List<string>();
        foreach (var skill in skills)
        {
            var pick = FindTalentForSkill(skill, talents);
            if (pick is not null)
            {
                talentPicks.Add(pick);
                validSkills.Add(skill);
            }
            else
            {
                logger.LogWarning("Petra v5.5 動態決策回未知 skill={Skill} 或 0 Talent 擔任（忽略）", skill);
            }
        }

        logger.LogInformation(
            "Petra v5.5 DecideTalentsAsync 完成 — raw=「{Raw}」picks={Picks}",
            raw, string.Join(" → ", talentPicks.Select(p => $"{p.Name}({validSkills[talentPicks.IndexOf(p)]})")));

        return (validSkills, talentPicks);
    }

    /// <summary>
    /// Stage 67：v5.5 — 看 Skill 找 Talent pool（IsPrimary desc + Priority asc 排序） + round-robin pick。
    /// baseline 簡單實作（避 fancy load balancing — Roadmap 子項 4 拍板）：pool[counter++ % pool.Count]。
    /// 找不到任何 Talent 擔任該 Skill → return null。
    /// </summary>
    private ITalent? FindTalentForSkill(string skill, IReadOnlyList<ITalent> talents)
    {
        var pool = talents
            .Where(t => t.Skills.Any(s => string.Equals(s, skill, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (pool.Count == 0) return null;
        if (pool.Count == 1) return pool[0];

        // multi-instance — round-robin baseline（future 加 IsPrimary / Priority 排序由 TalentSkill 對齊複雜化留 Phase 2/3）
        var index = _roundRobinCounter % pool.Count;
        _roundRobinCounter++;
        return pool[index];
    }

    /// <summary>
    /// Stage 70：v5.5 Phase 2 Step 4 — Petra LLM 動態拆任務 + dependency graph + lookup Talent pool。
    /// Prompt 含 hierarchical decomposition + few-shot 範例 — LLM 回 JSON SubtaskPlan。
    /// 容錯紀律：JSON 解析失敗 fallback 為 Linear[code_implementation] 單一 subtask（0-crash 保 dispatch 不中斷）。
    /// </summary>
    private async Task<(SubtaskPlan Plan, List<ITalent> TalentPicks)> DecideTalentsWithPlanAsync(
        string taskInput,
        IReadOnlyList<ITalent> talents,
        PetraSessionContext ctx,
        CancellationToken ct)
    {
        var skillRoster = string.Join(", ", talents.SelectMany(t => t.Skills).Distinct(StringComparer.OrdinalIgnoreCase));
        var systemPrompt = BuildPetraSystemPrompt(skillRoster, useSubtaskPlanning: true);

        var provider = providerFactory.Create(PetraAgentName);
        var response = await provider.CompleteAsync(systemPrompt, $"任務：{taskInput}", ct);

        await sessionRepo.AppendMessageAsync(ctx.SessionId, "assistant", response.Content, ct: ct);

        SubtaskPlan plan;
        if (!SubtaskPlanParser.TryParse(response.Content, out plan, out var parseError))
        {
            var rawPreview = response.Content.Length > 200 ? response.Content[..200] : response.Content;
            logger.LogWarning(
                "Petra v5.5 Step 4 SubtaskPlan JSON 解析失敗 fallback Linear[code_implementation] error={Error} raw=「{Raw}」",
                parseError, rawPreview);
            plan = SubtaskPlan.Linear(new[] { "code_implementation" });
        }

        // 每 subtask 用 FindTalentForSkill 找 Talent — 找不到的整 subtask 連同對應 dependency edges filter 掉
        var validSubtasks = new List<Subtask>();
        var validTalents = new List<ITalent>();
        foreach (var sub in plan.Subtasks)
        {
            var pick = FindTalentForSkill(sub.SkillName, talents);
            if (pick is not null)
            {
                validSubtasks.Add(sub);
                validTalents.Add(pick);
            }
            else
            {
                logger.LogWarning(
                    "Petra v5.5 Step 4 SubtaskPlan 回未知 skill={Skill} 或 0 Talent 擔任 subtaskId={Id}（忽略 subtask + 對應 edges）",
                    sub.SkillName, sub.Id);
            }
        }

        var validIds = validSubtasks.Select(s => s.Id).ToHashSet();
        var validEdges = plan.Dependencies
            .Where(d => validIds.Contains(d.FromId) && validIds.Contains(d.ToId))
            .ToList();
        var finalPlan = new SubtaskPlan(validSubtasks, validEdges);

        logger.LogInformation(
            "Petra v5.5 Step 4 DecideTalentsWithPlanAsync 完成 — subtasks={N} dependencies={E} picks={Picks}",
            finalPlan.Subtasks.Count, finalPlan.Dependencies.Count,
            string.Join(" → ", validTalents.Select((t, i) => $"{t.Name}({finalPlan.Subtasks[i].SkillName})")));

        return (finalPlan, validTalents);
    }

    /// <summary>
    /// Stage 67：v5.5 — 對齊 v5 DispatchWorkersAsync L238-277 自管 chain pattern 但用 ITalent + skill 動態傳。
    /// 同位置同 transaction 寫 PetraSessionMessages tool role — 對齊 Stage 66 修法位置。
    ///
    /// Stage 69 v2.1（v5.5 Phase 2 Step 3）：useV5Memory=true 時 dispatch 前注入 TaskMemory + TalentMemory + dispatch 後 upsert 寫回。
    /// scope = sessionId（PetraSession.Id 100% 有值 — 對齊 v5.5「每次 CEO 觸發 = 一個 Task event」設計精神，
    /// 不對齊 v4 TaskGroup 容器 — Aria v2.1 規劃漏掃修根因）。
    /// Talent 個人記憶寫回起步 = candidate A（key=`last-task-summary` / content = output 前 500 字元）。
    /// useV5Memory=false → skip memory 段保 v5.5 既有行為 0 regression。
    /// </summary>
    private async Task<List<WorkerDispatchSummary>> DispatchTalentsAsync(
        Guid sessionId,
        string taskInput,
        SubtaskPlan plan,
        IReadOnlyList<ITalent> talentPicks,
        AIAgent[] talentAgents,
        bool useV5Memory,
        IReadOnlyDictionary<string, Guid>? talentNameToIdMap,
        CancellationToken ct)
    {
        // 三 flag 連動 + lookup map 必須備好（caller useV5Memory=true 時應已 build / 缺則退為 false 保險）
        var memoryEnabled = useV5Memory && talentNameToIdMap is not null;
        var compactKeep = memoryEnabled ? await workflowResolver.GetV5MemoryCompactKeepCountAsync(ct) : 0;
        var compactThresholdPct = memoryEnabled ? await workflowResolver.GetV5MemoryCompactThresholdPercentAsync(ct) : 0;

        // Stage 70：topological sort — Linear plan (0 deps) 自然回 Id 升序 = 既有 dispatch 順序 = 0 regression
        var orderedIds = SubtaskPlanTopologicalSort.Sort(plan);
        // subtask Id → plan.Subtasks index（caller talentPicks / talentAgents 與 plan.Subtasks 同 index 對齊）
        var idToIndex = new Dictionary<int, int>(plan.Subtasks.Count);
        for (var i = 0; i < plan.Subtasks.Count; i++) idToIndex[plan.Subtasks[i].Id] = i;
        // dependsOn 觀察用 lookup（per subtask 直接 depends 的 from Id 集合）
        var dependsOnLookup = plan.Subtasks.ToDictionary(s => s.Id, _ => new List<int>());
        foreach (var edge in plan.Dependencies)
        {
            if (dependsOnLookup.ContainsKey(edge.ToId)) dependsOnLookup[edge.ToId].Add(edge.FromId);
        }

        var summaries = new List<WorkerDispatchSummary>(talentAgents.Length);
        for (var dispatchIndex = 0; dispatchIndex < orderedIds.Count; dispatchIndex++)
        {
            var subtaskId = orderedIds[dispatchIndex];
            var i = idToIndex[subtaskId];
            var talentAgent = talentAgents[i];
            var talentName = talentPicks[i].Name;
            var skill = plan.Subtasks[i].SkillName;

            // 1. 組 input messages（base chain + 可選 memory inject）— 對齊既有「prior summaries 全餵下個」紀律
            var baseInput = dispatchIndex == 0
                ? new List<ChatMessage> { new(ChatRole.User, taskInput) }
                : BuildNextWorkerInput(taskInput, summaries);

            List<ChatMessage> inputMessages;
            if (memoryEnabled && talentNameToIdMap!.TryGetValue(talentName, out var talentId))
            {
                var taskMems = await memoryRepo.GetTaskMemoriesAsync(sessionId, ct);
                var talentMems = await memoryRepo.GetTalentMemoriesAsync(talentId, projectId: null, tagFilter: null, ct);
                var memoryContext = BuildMemoryContext(taskMems, talentMems);
                if (!string.IsNullOrEmpty(memoryContext))
                {
                    inputMessages = new List<ChatMessage>(baseInput.Count + 1)
                    {
                        new(ChatRole.System, memoryContext),
                    };
                    inputMessages.AddRange(baseInput);
                    logger.LogInformation(
                        "Petra v5.5 dispatch 注入 memory talent={Talent} taskMemoryCount={TaskN} talentMemoryCount={TalentN}",
                        talentName, taskMems.Count, talentMems.Count);
                }
                else
                {
                    inputMessages = baseInput;
                }
            }
            else
            {
                inputMessages = baseInput;
            }

            // Stage 70：dispatch log 含 subtaskId + dependsOn — Linear plan dependsOn=[] 對齊既有 dispatch 觀察點
            var dependsOnIds = dependsOnLookup[subtaskId];
            logger.LogInformation(
                "PetraOrchestrator v5.5 自管 chain dispatch {DispatchIndex}/{Total} subtaskId={SubtaskId} talent={Talent} skill={Skill} dependsOn=[{DependsOn}] inputMsgs={N} sessionId={SessionId}",
                dispatchIndex + 1, orderedIds.Count, subtaskId, talentName, skill,
                string.Join(",", dependsOnIds), inputMessages.Count, sessionId);

            var response = await talentAgent.RunAsync(inputMessages, session: null, options: null, ct);
            var outputText = response.Text ?? "";

            var toolCallId = Guid.NewGuid().ToString("N");
            var toolMessage = BuildToolMessage(talentName, skill, outputText);
            await sessionRepo.AppendMessageAsync(sessionId, "tool", toolMessage, toolCallId, ct);

            // 2. memory 寫回（useV5Memory=true 時 dispatch 後 upsert TaskMemory + TalentMemory）
            if (memoryEnabled && talentNameToIdMap!.TryGetValue(talentName, out var talentIdForWrite))
            {
                var truncated = outputText.Length > 500 ? outputText[..500] : outputText;
                await memoryRepo.UpsertTaskMemoryAsync(
                    sessionId, projectId: null,
                    key: $"decision/{talentName}-output-summary",
                    content: truncated,
                    createdByTalent: talentName,
                    ct);
                await memoryRepo.UpsertTalentMemoryAsync(
                    talentIdForWrite, projectId: null,
                    key: "last-task-summary",
                    content: truncated,
                    tags: null,
                    ct);
                logger.LogInformation(
                    "Petra v5.5 dispatch 完成寫回 TaskMemory key=decision/{Talent}-output-summary + TalentMemory key=last-task-summary",
                    talentName);
            }

            await db.SaveChangesAsync(ct);

            // 3. compact threshold 檢查（buffer-above-keep 模型：count >= keep * (100 + thresholdPct) / 100）
            if (memoryEnabled)
            {
                var taskMemCount = await memoryRepo.CountTaskMemoriesAsync(sessionId, ct);
                var triggerAt = compactKeep + (compactKeep * compactThresholdPct / 100);
                if (taskMemCount >= triggerAt && triggerAt > 0)
                {
                    var deleted = await memoryRepo.CompactTaskMemoryAsync(sessionId, compactKeep, ct);
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation(
                        "Memory compact 觸發 PetraSessionId={SessionId} beforeCount={Before} afterCount={After} deleted={Deleted}",
                        sessionId, taskMemCount, taskMemCount - deleted, deleted);
                }
            }

            logger.LogInformation(
                "PetraOrchestrator v5.5 自管 chain dispatch 完成 {DispatchIndex}/{Total} subtaskId={SubtaskId} talent={Talent} outputLen={Len} toolCallId={ToolCallId}",
                dispatchIndex + 1, orderedIds.Count, subtaskId, talentName, outputText.Length, toolCallId);

            summaries.Add(new WorkerDispatchSummary(talentName, skill, outputText, toolCallId));
        }
        return summaries;
    }

    /// <summary>
    /// Stage 69：拼 memory context 注入 system prompt — 0 entries 兩層都空時 return string.Empty（caller skip inject）。
    /// </summary>
    private static string BuildMemoryContext(IReadOnlyList<TaskMemory> taskMems, IReadOnlyList<TalentMemory> talentMems)
    {
        if (taskMems.Count == 0 && talentMems.Count == 0) return string.Empty;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## 跨 session 長期記憶（v5.5 Phase 2）");
        if (taskMems.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Task 共用 context（同 TaskGroup 其他 Talent 累積）");
            foreach (var m in taskMems)
            {
                sb.AppendLine($"- **{m.Key}**（by {m.CreatedByTalent}）: {m.Content}");
            }
        }
        if (talentMems.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Talent 個人記憶（跨 task 累積）");
            foreach (var m in talentMems)
            {
                sb.AppendLine($"- **{m.Key}**: {m.Content}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Stage 64 子項 2：Workers 完成後 git commit/push/PR 接通。
    /// 範圍邊界：最小整合 — 不重做 v4 dev_plan / fix_loop / metadata 機制（留 Stage 65+）。
    /// 無 diff → 不誤建 PR；非 git repo → 捕例外 log warning 不擋流程。
    /// </summary>
    /// <summary>
    /// Stage 67：picks 抽 dispatchNames 後簽名 — v5 既有 worker name 與 v5.5 talent name 共用一致 typed string list。
    /// </summary>
    private async Task<string?> FinalizeGitAsync(
        PetraSessionContext ctx,
        string taskInput,
        IReadOnlyList<string> caps,
        IReadOnlyList<string> dispatchNames,
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

            var prBody = BuildPrBody(taskInput, caps, dispatchNames, workerSummaries);
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
        IReadOnlyList<string> dispatchNames,
        IReadOnlyList<string> workerSummaries)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## 任務");
        sb.AppendLine(taskInput ?? "");
        sb.AppendLine();
        sb.AppendLine("## Petra 動態決策");
        sb.AppendLine($"- Capability / Skill 序列：`{string.Join(" | ", caps)}`");
        sb.AppendLine($"- Workers / Talents dispatch 順序：{string.Join(" → ", dispatchNames)}");
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
    ///
    /// Stage 70：useSubtaskPlanning=true 時升級「需求拆解紀律」為 hierarchical decomposition + dependency graph 紀律段
    /// + few-shot 範例 + 輸出格式改為 JSON SubtaskPlan（取代 `|` 分隔字串）。default false 保 Stage 67/69 既有 path 0 regression。
    /// </summary>
    private static string BuildPetraSystemPrompt(string capabilityRoster, bool useSubtaskPlanning = false)
    {
        var decompositionSection = useSubtaskPlanning
            ? """

【Hierarchical Decomposition + Dependency Graph 紀律】（Stage 70：v5.5 Phase 2 Step 4）

接到任務時先用內部 reasoning 拆需求 + 識別 subtask 間依賴 — 不 dispatch worker 做 requirements_extraction。
判準：以模糊度 / 範圍 / 邊界三維度自我評估，命中 Design / Kickoff trigger 時主動拆 subtask。

紀律：
- simple task 仍回 1 subtask 0 dependency（**拆解是擴展不是取代** — Linear 形式對齊 Trial_v6-v14 既有 baseline）
- 複雜 task 拆 N 個 subtask（N ≤ 5 為佳），用 dependency edges 標示「先做 A 才能做 B」的 sequential / nested 關係
- subtask id 從 1 起算連號 / skill 必須在【可選 capability】內 / description 一句話描述 subtask 範圍
- dependency edge type：sequential（A 完成才能做 B）/ nested（B 是 A 的子工作）

★ Few-shot 範例 1：simple task（1 subtask）
  輸入：「修 README typo」
  輸出（單行 JSON）：{"subtasks":[{"id":1,"skill":"code_implementation","description":"修 README typo"}],"dependencies":[]}

★ Few-shot 範例 2：複雜 task（多 subtask + sequential chain）
  輸入：「Dashboard 加 Petra session 列表頁 + review + 補 Playwright test」
  輸出（單行 JSON）：{"subtasks":[{"id":1,"skill":"code_implementation","description":"實作 Dashboard Petra session 列表頁 + Razor + Service"},{"id":2,"skill":"code_review","description":"review 列表頁 production safety + coding style"},{"id":3,"skill":"qa_testing","description":"補 Playwright test 截圖驗收"}],"dependencies":[{"from":1,"to":2,"type":"sequential"},{"from":2,"to":3,"type":"sequential"}]}

"""
            : """

【需求拆解紀律】（Stage 67：合 requirements_extraction 進來）

接到任務時先用內部 reasoning 拆需求 — 不 dispatch worker 做 requirements_extraction。
判準：以模糊度 / 範圍 / 邊界三維度自我評估
  範例：「打磨 Dashboard 錯誤處理體驗」→ 內部拆「跨 5 範圍 + 中等改動 + UI 邊界」→ 命中 Design trigger
  範例：「Dashboard 加圖示」→ 內部拆「視覺 + 小改動」→ 命中 1-on-1 trigger
紀律：拆完直接決 capability 序列回（不要回「我先拆需求 → 再決定」這種兩步驟說法 / 不污染 capability 序列輸出格式）
""";

        var outputSection = useSubtaskPlanning
            ? """
【輸出紀律】（Stage 70：JSON SubtaskPlan）
- 只回單行 JSON 物件（含 subtasks + dependencies 兩 key）
- 不要 markdown 包裹 / 不要 backtick / 不要解釋 / 不要 prefix 「output:」
- 反例：```json{...}```（錯：markdown code fence 包裹）
- 反例：「我建議拆成 3 個 subtask」（錯：解釋）
- 反例：code_implementation|code_review（錯：舊 `|` 分隔字串格式 — Stage 70 已升級 JSON）
- 正例：{"subtasks":[{"id":1,"skill":"code_implementation","description":"..."}],"dependencies":[]}
"""
            : """
【輸出紀律】
- 只回 capability 序列（用 `|` 分隔）
- 不要 markdown 包裹 / 不要 backtick / 不要解釋 / 不要 prefix 「output:」
- 不要回 Worker 名稱（例如「Cody」），只回 capability tag
- 反例：```code_implementation|code_review```（錯：backtick 包裹）
- 反例：「我建議 code_implementation」（錯：解釋）
- 正例：`code_implementation|code_review`
""";

        return $$"""
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
{{decompositionSection}}
{{outputSection}}
""";
    }
}
