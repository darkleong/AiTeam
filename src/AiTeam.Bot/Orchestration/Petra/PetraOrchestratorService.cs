using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Data.SeedContent;
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
    PromptResolver promptResolver,
    TalentDispatchLockService talentLockService,   // Stage 75：per-Talent serialization lock
    InteractionService interactionService,          // Stage 80：HITL plan_confirm 卡開卡用
    ILoggerFactory loggerFactory,
    ILogger<PetraOrchestratorService> logger)
{
    private const string PetraAgentName = "PM";   // 對齊既有 appsettings.json BotAgentSettings.Agents.PM 鍵

    // Stage 67：v5.5 path round-robin counter（PetraOrchestratorService 是 Scoped — session 級無需 thread-safe）。
    private int _roundRobinCounter;

    /// <summary>啟動新 session — Petra 動態決策 + BuildSequential dispatch。taskGroupId 可為 null（spike forward path 無 TaskGroup）。
    /// Stage 77：標 virtual 供 xUnit T6 test-only subclass override stub（Stage 76 retry path 搬遷整合 regression test cover）。
    /// Stage 79：v5.5 image flow 補完 — 加 images param（PetraDispatchWorker 從 PetraInbox.Attachments 反序列化後傳入）。
    /// images null / 0 count → 0 image propagation 純文字 dispatch 對齊 Trial_v22 baseline 0 行為改變。</summary>
    public virtual async Task<PetraOrchestratorResult> StartAsync(
        Guid? taskGroupId,
        string taskInput,
        CancellationToken ct = default,
        IReadOnlyList<ImageAttachment>? images = null)
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
                // Stage 79：images 傳 Petra LLM call sites（GeminiProvider multimodal 真實看圖 / Petra 拍板 NeedsImageContext per subtask）
                SubtaskPlan plan;
                List<ITalent> talentPicks;
                if (useV5SubtaskPlanning)
                {
                    (plan, talentPicks) = await DecideTalentsWithPlanAsync(taskInput, talentsList, sessionWithCtx, ct, images);
                }
                else
                {
                    var (skills, picks) = await DecideTalentsAsync(taskInput, talentsList, sessionWithCtx, ct, images);
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

                // Stage 80：HITL plan_confirm 閘門 — flag=true 走 HITL path（開 BossInteraction plan_confirm 卡 + pause session + return Paused）
                // flag=false 維持 v5.5 baseline auto dispatch 0 行為改變（守 production 0 regression / Trial_v24 開時切 true）
                var useHITLPlanConfirmation = await workflowResolver.GetUseHITLPlanConfirmationAsync(ct);
                if (useHITLPlanConfirmation)
                {
                    await WaitForPlanConfirmationAsync(
                        session.Id, taskInput, plan, talentPicks, decidedCapabilities, ct);
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation(
                        "Stage 80：HITL plan_confirm 閘門 fire — sessionId={SessionId} subtasks={SubtaskCount} talents=[{Talents}] 等 Christ 4 decision 拍板",
                        session.Id, plan.Subtasks.Count, string.Join(",", talentPicks.Select(t => t.Name)));
                    return PetraOrchestratorResult.Paused(session.Id, decidedCapabilities,
                        $"Petra 已拆 {plan.Subtasks.Count} subtask 等 Christ HITL plan_confirm 拍板（approve/edit/reject/respond）。");
                }

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
                // Stage 75：talentNameToIdMap 提前 build — per-Talent serialization lock 永遠需要 Talent Id（不分 memory flag）
                //          既有 Stage 69 conditional build 修根因為「永遠 build」+ memory 段繼續用同個 map（Forge spike 揭架構盲點）
                // talentName → Talent.Id lookup map（v5.5 baseline 全 ProjectId=null 全域 Talent / Phase 3 per-Project Talent 加入時需擴展此 query）
                var names = talentPicks.Select(t => t.Name).Distinct().ToList();
                var talentNameToIdMap = await db.Talents
                    .Where(t => names.Contains(t.Name) && t.ProjectId == null)
                    .ToDictionaryAsync(t => t.Name, t => t.Id, ct);

                var outcome = await DispatchTalentsAsync(
                    session.Id, taskInput, plan, talentPicks, talentAgents,
                    useV5Memory, talentNameToIdMap, images, sessionWithCtx, ct);
                await db.SaveChangesAsync(ct);

                // Stage 81：replan signal handling（cap_reached → intervention + cancelled / replan_confirm → 開卡 + paused）
                if (outcome.Replan is { } signal)
                {
                    return await HandleReplanSignalAsync(session.Id, taskInput, signal, ct);
                }

                dispatchNames = talentPicks.Select(t => t.Name).ToList();
            }
            else
            {
                var toolsList = tools.ToList();
                logger.LogInformation(
                    "PetraOrchestrator 啟動 (v5 既有 IAgentTool path) — sessionId={SessionId} taskGroupId={TaskGroupId} toolsCount={Count} workingDir={Dir}",
                    session.Id, taskGroupId, toolsList.Count, sessionWithCtx.WorkingDir);

                // 1. DecideAsync — Petra LLM 動態決策 capability 序列
                // Stage 79：images 傳 Petra LLM call site（v5 path 對齊 v5.5 既有紀律）
                var (caps, picks) = await DecideAsync(taskInput, toolsList, sessionWithCtx, ct, images);
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
                await DispatchWorkersAsync(session.Id, taskInput, caps, picks, workerAgents, images, ct);
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
    /// Stage 79：v5.5 image flow 補完 — images 傳給 ILlmProvider.CompleteAsync（GeminiProvider multimodal 真實看圖）。
    /// </summary>
    private async Task<(List<string> Capabilities, List<IAgentTool> Picks)> DecideAsync(
        string taskInput,
        IReadOnlyList<IAgentTool> tools,
        PetraSessionContext ctx,
        CancellationToken ct,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        var capabilityRoster = string.Join(", ", tools.SelectMany(t => t.Capabilities).Distinct());
        var systemPrompt = await BuildPetraSystemPromptForRuntimeAsync(capabilityRoster, useSubtaskPlanning: false, ct);

        var provider = providerFactory.Create(PetraAgentName);
        // Stage 82 子項 2：AsyncLocal scope — token_logs.PetraSessionId 透傳（vs Stage 81 議題 #5 worker dispatch path 紀律對齊）
        using var _scope = TokenTrackingProvider.BeginPetraSessionScope(ctx.SessionId);
        var response = await provider.CompleteAsync(systemPrompt, $"任務：{taskInput}", ct, images);

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
        IReadOnlyList<ImageAttachment>? images,
        CancellationToken ct)
    {
        var summaries = new List<WorkerDispatchSummary>(workerAgents.Length);
        for (var i = 0; i < workerAgents.Length; i++)
        {
            var workerAgent = workerAgents[i];
            var workerName = picks[i].Name;
            // picks 與 decidedCapabilities 同 index 對齊 — DecideAsync 已 filter unknown cap 後保持順序
            var capability = i < decidedCapabilities.Count ? decidedCapabilities[i] : picks[i].Capabilities.FirstOrDefault() ?? "";

            // Stage 79：v5 path 簡化 — 第一個 worker 一律附 image（v5 path 0 SubtaskPlan / 0 NeedsImageContext flag）/ 後續 worker 純 text dispatch
            var inputMessages = i == 0
                ? BuildFirstWorkerInput(taskInput, images)
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
    /// Stage 79：v5.5 image flow 補完 — 第一個 worker input 構造（含 image AIContent 真實 propagate）。
    /// v5 path（DispatchWorkersAsync caller）簡化紀律：images != null AND 第一個 worker → 附 image / 0 條件性 NeedsImageContext flag（v5 path 0 SubtaskPlan）。
    /// v5.5 path 走 BuildInputMessagesForSubtaskAsync 條件性 dispatch（per-subtask NeedsImageContext 紀律）。
    /// </summary>
    private static List<ChatMessage> BuildFirstWorkerInput(
        string taskInput,
        IReadOnlyList<ImageAttachment>? images)
    {
        if (images is null || images.Count == 0)
        {
            return new List<ChatMessage> { new(ChatRole.User, taskInput) };
        }

        var contents = new List<AIContent> { new TextContent(taskInput) };
        foreach (var img in images)
        {
            var bytes = Convert.FromBase64String(img.Base64Data);
            contents.Add(new DataContent(bytes, img.MediaType));
        }
        return new List<ChatMessage> { new(ChatRole.User, contents) };
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
        CancellationToken ct,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        // skill roster 從 talent.Skills 取（vs 既有 DecideAsync 取 tools.SelectMany(Capabilities)）— 對 LLM 等價
        var skillRoster = string.Join(", ", talents.SelectMany(t => t.Skills).Distinct(StringComparer.OrdinalIgnoreCase));
        var systemPrompt = await BuildPetraSystemPromptForRuntimeAsync(skillRoster, useSubtaskPlanning: false, ct);

        var provider = providerFactory.Create(PetraAgentName);
        // Stage 79：v5.5 image flow 補完 — images 傳給 ILlmProvider.CompleteAsync（GeminiProvider multimodal 真實看圖）
        // Stage 82 子項 2：AsyncLocal scope — token_logs.PetraSessionId 透傳
        using var _scope = TokenTrackingProvider.BeginPetraSessionScope(ctx.SessionId);
        var response = await provider.CompleteAsync(systemPrompt, $"任務：{taskInput}", ct, images);

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
        CancellationToken ct,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        var skillRoster = string.Join(", ", talents.SelectMany(t => t.Skills).Distinct(StringComparer.OrdinalIgnoreCase));
        var systemPrompt = await BuildPetraSystemPromptForRuntimeAsync(skillRoster, useSubtaskPlanning: true, ct);

        var provider = providerFactory.Create(PetraAgentName);
        // Stage 79：v5.5 image flow 補完 — images 傳給 ILlmProvider.CompleteAsync（GeminiProvider multimodal 真實看圖 + Petra 拍板 NeedsImageContext per subtask）
        // Stage 82 子項 2：AsyncLocal scope — token_logs.PetraSessionId 透傳
        using var _scope = TokenTrackingProvider.BeginPetraSessionScope(ctx.SessionId);
        var response = await provider.CompleteAsync(systemPrompt, $"任務：{taskInput}", ct, images);

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
    private async Task<DispatchOutcome> DispatchTalentsAsync(
        Guid sessionId,
        string taskInput,
        SubtaskPlan plan,
        IReadOnlyList<ITalent> talentPicks,
        AIAgent[] talentAgents,
        bool useV5Memory,
        IReadOnlyDictionary<string, Guid> talentNameToIdMap,   // Stage 75：non-nullable（per-Talent lock 永遠用到 / Forge spike 揭架構盲點修根因）
        IReadOnlyList<ImageAttachment>? images,                 // Stage 79：v5.5 image flow 補完 — 條件性 per-subtask NeedsImageContext flag 才 propagate
        PetraSessionContext ctx,                                // Stage 81：InvokePetraReplanAsync 需要 ctx 取 sessionId / workingDir / model
        CancellationToken ct)
    {
        // Stage 75：talentNameToIdMap 永遠非 null（caller StartAsync 提前 build）— memoryEnabled 直接看 useV5Memory flag
        var memoryEnabled = useV5Memory;
        var compactKeep = memoryEnabled ? await workflowResolver.GetV5MemoryCompactKeepCountAsync(ct) : 0;
        var compactThresholdPct = memoryEnabled ? await workflowResolver.GetV5MemoryCompactThresholdPercentAsync(ct) : 0;

        // Stage 70：subtask Id → plan.Subtasks index（caller talentPicks / talentAgents 與 plan.Subtasks 同 index 對齊）
        var idToIndex = new Dictionary<int, int>(plan.Subtasks.Count);
        for (var i = 0; i < plan.Subtasks.Count; i++) idToIndex[plan.Subtasks[i].Id] = i;
        // Stage 70：dependsOn 觀察用 lookup（per subtask 直接 depends 的 from Id 集合）
        var dependsOnLookup = plan.Subtasks.ToDictionary(s => s.Id, _ => new List<int>());
        foreach (var edge in plan.Dependencies)
        {
            if (dependsOnLookup.ContainsKey(edge.ToId)) dependsOnLookup[edge.ToId].Add(edge.FromId);
        }

        // Stage 74：v5.5 Phase 3 Step 8 — DAG fan-out level grouping 取代既有 TopologicalSort flat list。
        // Linear plan (0 deps) → 每 level 1 subtask = sequential = 對齊 Trial baseline 3 subtask 線性場景 0 regression。
        // 真並行場景（同 level 多 subtask）→ Task.WhenAll LLM dispatch 並行（業界 1.4-2.4× speedup）+ DB write 並行段結束後 sequential（保 AppDbContext thread-safe）。
        var levels = SubtaskPlanLevelGrouping.Group(plan);

        var summaries = new List<WorkerDispatchSummary>(talentAgents.Length);
        for (var levelIdx = 0; levelIdx < levels.Count; levelIdx++)
        {
            var level = levels[levelIdx];
            // snapshot summaries before level start — level 內並行各 subtask 共用同一份 baseInput（前 level 累積 / WorkerDispatchSummary 是 immutable record shallow copy 安全）
            var summariesSnapshot = summaries.ToList();

            if (level.Count > 1)
            {
                // 並行段 — 路線 A 紀律：LLM dispatch 並行 / DB write 並行段結束後 sequential（AppDbContext 非 thread-safe）
                logger.LogInformation(
                    "PetraOrchestrator v5.5 自管 chain dispatch Level={Level}/{TotalLevels} 並行 subtaskIds=[{Ids}] talents=[{Talents}] sessionId={SessionId}",
                    levelIdx + 1, levels.Count, string.Join(",", level),
                    string.Join(",", level.Select(id => talentPicks[idToIndex[id]].Name)), sessionId);

                // LLM dispatch 並行段（只跑 talentAgent.RunAsync — IClaudeCodeService subprocess 各自獨立 / TokenLogService 自開 scope 寫 token_logs / Resolver 自管 lock）
                // Stage 75：per-Talent serialization lock — 同 talent_id 序列化（鎖只包 LLM dispatch / 不包 DB write 對齊 Stage 74 路線 A 紀律）
                var levelTasks = level.Select(async subtaskId =>
                {
                    var i = idToIndex[subtaskId];
                    var talentName = talentPicks[i].Name;
                    var talentId = talentNameToIdMap[talentName];   // Stage 75：map 永遠 build / 必有

                    var inputMessages = await BuildInputMessagesForSubtaskAsync(
                        taskInput, summariesSnapshot, levelIdx,
                        talentName, talentNameToIdMap, memoryEnabled, sessionId,
                        plan.Subtasks[i], images, ct);

                    using var lockHandle = await talentLockService.AcquireAsync(talentId, ct);
                    logger.LogInformation(
                        "PetraOrchestrator v5.5 dispatch acquire per-Talent lock talent={Talent} talentId={TalentId} subtaskId={SubtaskId} sessionId={SessionId}",
                        talentName, talentId, subtaskId, sessionId);

                    var response = await talentAgents[i].RunAsync(inputMessages, session: null, options: null, ct);
                    return (SubtaskId: subtaskId, OutputText: response.Text ?? "", Index: i);
                }).ToList();
                var levelResults = await Task.WhenAll(levelTasks);

                // sequential：DB write + summaries append + memory upsert + compact threshold（subtask Id 升序 deterministic）
                // Stage 81：每個 subtask 處理後檢查 cost cap + trigger evaluate — 並行 level 中第一個觸發的 subtask 決定 signal（保留所有 parallel outputs 不丟失）
                ReplanSignal? firstReplanSignal = null;
                foreach (var r in levelResults.OrderBy(x => x.SubtaskId))
                {
                    await ProcessSubtaskResultAsync(
                        r.SubtaskId, r.OutputText, r.Index, sessionId, summaries,
                        plan, talentPicks, dependsOnLookup, talentNameToIdMap, memoryEnabled,
                        compactKeep, compactThresholdPct, levels.Count, levelIdx, isParallel: true, ct);

                    if (firstReplanSignal is null)
                    {
                        firstReplanSignal = await CheckReplanTriggerAfterDispatchAsync(
                            sessionId, taskInput, plan.Subtasks[r.Index], talentPicks[r.Index].Name,
                            r.OutputText, summaries, ctx, ct);
                    }
                }
                if (firstReplanSignal is not null) return new DispatchOutcome(summaries, firstReplanSignal);
            }
            else
            {
                // 單 subtask level — sequential（對齊 Trial baseline 3 subtask 線性場景 0 regression）
                var subtaskId = level[0];
                var i = idToIndex[subtaskId];
                var talentName = talentPicks[i].Name;
                var skill = plan.Subtasks[i].SkillName;
                var talentId = talentNameToIdMap[talentName];   // Stage 75：map 永遠 build / 必有

                var inputMessages = await BuildInputMessagesForSubtaskAsync(
                    taskInput, summariesSnapshot, levelIdx,
                    talentName, talentNameToIdMap, memoryEnabled, sessionId,
                    plan.Subtasks[i], images, ct);

                logger.LogInformation(
                    "PetraOrchestrator v5.5 自管 chain dispatch Level={Level}/{TotalLevels} sequential subtaskId={SubtaskId} talent={Talent} skill={Skill} dependsOn=[{DependsOn}] inputMsgs={N} sessionId={SessionId}",
                    levelIdx + 1, levels.Count, subtaskId, talentName, skill,
                    string.Join(",", dependsOnLookup[subtaskId]), inputMessages.Count, sessionId);

                // Stage 75：per-Talent serialization lock（鎖只包 LLM dispatch / 不包 DB write 對齊 Stage 74 路線 A 紀律）
                string outputText;
                using (var lockHandle = await talentLockService.AcquireAsync(talentId, ct))
                {
                    logger.LogInformation(
                        "PetraOrchestrator v5.5 dispatch acquire per-Talent lock talent={Talent} talentId={TalentId} subtaskId={SubtaskId} sessionId={SessionId}",
                        talentName, talentId, subtaskId, sessionId);
                    var response = await talentAgents[i].RunAsync(inputMessages, session: null, options: null, ct);
                    outputText = response.Text ?? "";
                }

                await ProcessSubtaskResultAsync(
                    subtaskId, outputText, i, sessionId, summaries,
                    plan, talentPicks, dependsOnLookup, talentNameToIdMap, memoryEnabled,
                    compactKeep, compactThresholdPct, levels.Count, levelIdx, isParallel: false, ct);

                // Stage 81：每個 subtask 處理後檢查 cost cap + trigger evaluate
                var sequentialSignal = await CheckReplanTriggerAfterDispatchAsync(
                    sessionId, taskInput, plan.Subtasks[i], talentName, outputText, summaries, ctx, ct);
                if (sequentialSignal is not null) return new DispatchOutcome(summaries, sequentialSignal);
            }
        }
        return new DispatchOutcome(summaries, null);
    }

    /// <summary>
    /// Stage 74：v5.5 Phase 3 Step 8 — 抽出既有 dispatch input messages 構造（base chain + 可選 memory inject）。
    /// 純讀 / 0 DB write — 並行段（多 subtask 同 level）+ sequential 段（單 subtask）共用 helper。
    /// memory 走 IServiceScopeFactory query / talentNameToIdMap caller 傳（lookup 0 db.Talents query）。
    ///
    /// Stage 79：v5.5 image flow 補完 — 條件性 image AIContent dispatch。
    /// currentSubtask.NeedsImageContext=true AND images != null → first user message 加 image AIContent（DataContent）
    /// currentSubtask.NeedsImageContext=false 或 images=null → 純 text dispatch（既有 baseline 0 行為改變）
    /// 對齊業界紀律「pass images only to worker agents that need them」。
    /// </summary>
    private async Task<List<ChatMessage>> BuildInputMessagesForSubtaskAsync(
        string taskInput,
        List<WorkerDispatchSummary> summariesSnapshot,
        int levelIdx,
        string talentName,
        IReadOnlyDictionary<string, Guid> talentNameToIdMap,   // Stage 75：non-nullable（caller 永遠 build）
        bool memoryEnabled,
        Guid sessionId,
        Subtask currentSubtask,                                 // Stage 79：caller 傳 plan.Subtasks[i]
        IReadOnlyList<ImageAttachment>? images,                 // Stage 79：v5.5 image flow 補完 — 條件性 propagate
        CancellationToken ct)
    {
        var baseInput = levelIdx == 0
            ? new List<ChatMessage> { new(ChatRole.User, taskInput) }
            : BuildNextWorkerInput(taskInput, summariesSnapshot);

        // Stage 79：條件性 image AIContent dispatch — NeedsImageContext=true AND images != null → 替換 first user message 為 multi-modal Contents
        if (currentSubtask.NeedsImageContext && images is { Count: > 0 }
            && baseInput.Count > 0 && baseInput[0].Role == ChatRole.User)
        {
            var firstText = baseInput[0].Text ?? "";
            var contents = new List<AIContent> { new TextContent(firstText) };
            foreach (var img in images)
            {
                var bytes = Convert.FromBase64String(img.Base64Data);
                contents.Add(new DataContent(bytes, img.MediaType));
            }
            baseInput[0] = new ChatMessage(ChatRole.User, contents);
            logger.LogInformation(
                "Stage 79 Petra dispatch image AIContent → Worker talent={Talent} subtaskId={Id} imageCount={Count}",
                talentName, currentSubtask.Id, images.Count);
        }

        if (memoryEnabled && talentNameToIdMap.TryGetValue(talentName, out var talentId))
        {
            var taskMems = await memoryRepo.GetTaskMemoriesAsync(sessionId, ct);
            var talentMems = await memoryRepo.GetTalentMemoriesAsync(talentId, projectId: null, tagFilter: null, ct);
            var memoryContext = BuildMemoryContext(taskMems, talentMems);
            if (!string.IsNullOrEmpty(memoryContext))
            {
                var inputMessages = new List<ChatMessage>(baseInput.Count + 1)
                {
                    new(ChatRole.System, memoryContext),
                };
                inputMessages.AddRange(baseInput);
                logger.LogInformation(
                    "Petra v5.5 dispatch 注入 memory talent={Talent} taskMemoryCount={TaskN} talentMemoryCount={TalentN}",
                    talentName, taskMems.Count, talentMems.Count);
                return inputMessages;
            }
        }
        return baseInput;
    }

    /// <summary>
    /// Stage 74：v5.5 Phase 3 Step 8 — 抽出既有 dispatch 副作用（sessionRepo.AppendMessageAsync / memory upsert / compact threshold / db.SaveChangesAsync / summaries.Add）。
    /// 路線 A 紀律：並行 LLM dispatch 結束後此 helper 在 caller 內 sequential 跑（subtask Id 升序）— 保 AppDbContext thread-safe。
    /// 內部副作用順序對齊既有 Stage 69-73 baseline（不改 message 寫入 / memory upsert / compact threshold 邏輯，純結構抽出）。
    /// </summary>
    private async Task ProcessSubtaskResultAsync(
        int subtaskId,
        string outputText,
        int i,
        Guid sessionId,
        List<WorkerDispatchSummary> summaries,
        SubtaskPlan plan,
        IReadOnlyList<ITalent> talentPicks,
        Dictionary<int, List<int>> dependsOnLookup,
        IReadOnlyDictionary<string, Guid> talentNameToIdMap,   // Stage 75：non-nullable（caller 永遠 build）
        bool memoryEnabled,
        int compactKeep,
        int compactThresholdPct,
        int totalLevels,
        int levelIdx,
        bool isParallel,
        CancellationToken ct)
    {
        var talentName = talentPicks[i].Name;
        var skill = plan.Subtasks[i].SkillName;

        var toolCallId = Guid.NewGuid().ToString("N");
        var toolMessage = BuildToolMessage(talentName, skill, outputText);
        await sessionRepo.AppendMessageAsync(sessionId, "tool", toolMessage, toolCallId, ct);

        // memory 寫回（useV5Memory=true 時 dispatch 後 upsert TaskMemory + TalentMemory）
        if (memoryEnabled && talentNameToIdMap.TryGetValue(talentName, out var talentIdForWrite))
        {
            if (outputText.Length == 0)
            {
                logger.LogWarning(
                    "Petra v5.5 dispatch worker output empty skip memory write talent={Talent} skill={Skill} sessionId={SessionId}",
                    talentName, skill, sessionId);
            }
            else
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
        }

        await db.SaveChangesAsync(ct);

        // compact threshold 檢查（buffer-above-keep 模型：count >= keep * (100 + thresholdPct) / 100）
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
            "PetraOrchestrator v5.5 自管 chain dispatch 完成 Level={Level}/{TotalLevels} subtaskId={SubtaskId} talent={Talent} parallel={Parallel} outputLen={Len} toolCallId={ToolCallId}",
            levelIdx + 1, totalLevels, subtaskId, talentName, isParallel, outputText.Length, toolCallId);

        summaries.Add(new WorkerDispatchSummary(talentName, skill, outputText, toolCallId));
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

    // ========== Stage 80：HITL plan_confirm 閘門 ==========

    /// <summary>Stage 80：BossInteraction.ContextJson 用 SubtaskPlan + talent picks 序列化結構（plan_confirm 卡 / Resume 還原）。
    /// 純內部結構 — InteractionCard.razor render 端讀同 JSON / 4 decision pattern routing 用。</summary>
    internal sealed record PlanConfirmContext(
        Guid SessionId,
        string TaskInput,
        List<PlanConfirmSubtask> Subtasks,
        List<PlanConfirmDependency> Dependencies,
        List<string> TalentNames);

    internal sealed record PlanConfirmSubtask(int Id, string Skill, string Description, string TalentName, bool NeedsImageContext);

    internal sealed record PlanConfirmDependency(int From, int To, string Type);

    private static readonly JsonSerializerOptions PlanConfirmJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Stage 80：開 BossInteraction plan_confirm 卡 + pause session（chain dispatch 0 啟動 / 等 Christ 4 decision 拍板）。
    /// ContextJson 含 SubtaskPlan + talent picks + sessionId + taskInput（resume edit/respond path 重 decide 用）。
    /// 對齊 InteractionService.CreateInteractionAsync 既有 pattern — fire-and-forget 失敗只 log（plan_confirm 漏卡也不擋 PetraDispatchWorker row 完成）。</summary>
    private async Task WaitForPlanConfirmationAsync(
        Guid sessionId,
        string taskInput,
        SubtaskPlan plan,
        IReadOnlyList<ITalent> talentPicks,
        IReadOnlyList<string> decidedCapabilities,
        CancellationToken ct)
    {
        var context = new PlanConfirmContext(
            SessionId: sessionId,
            TaskInput: taskInput,
            Subtasks: plan.Subtasks
                .Select((s, i) => new PlanConfirmSubtask(s.Id, s.SkillName, s.Description, talentPicks[i].Name, s.NeedsImageContext))
                .ToList(),
            Dependencies: plan.Dependencies
                .Select(d => new PlanConfirmDependency(d.FromId, d.ToId, d.Type.ToString().ToLowerInvariant()))
                .ToList(),
            TalentNames: talentPicks.Select(t => t.Name).ToList());

        var contextJson = JsonSerializer.Serialize(context, PlanConfirmJsonOptions);

        var taskFirstLine = (taskInput ?? "").Split('\n').FirstOrDefault() ?? "";
        var title = taskFirstLine.Length > 80 ? taskFirstLine[..80] + "…" : taskFirstLine;
        var description = $"Petra 拆 {plan.Subtasks.Count} subtask（{string.Join(" → ", talentPicks.Select(t => t.Name))}）— 等 Christ 4 decision 拍板。";
        var systemNotes = $"[Stage 80 HITL] sessionId={sessionId.ToString("N")[..8]} — 4 decision pattern：approve（核准）/ edit（修改）/ reject（拒絕）/ respond（補充）。";

        _ = interactionService.CreateInteractionAsync(
            interactionType:      "plan_confirm",
            title:                title,
            description:          description,
            project:              null,
            agentName:            "Petra",
            availableActionsJson: InteractionService.PlanConfirmActionsJson,
            contextJson:          contextJson,
            systemNotes:          systemNotes);

        await sessionRepo.PauseAsync(sessionId, ct);
    }

    /// <summary>Stage 80：HITL plan_confirm 4 decision pattern resume — PlanConfirmationProcessor 看到 responded 拉起 fire。
    /// approve → 沿用既有 SubtaskPlan + 重建 talentPicks → DispatchAndFinalize（chain dispatch + FinalizeGit + CompleteAsync）
    /// edit / respond → 重 DecideTalentsWithPlanAsync（taskInput + override content prefix） → 新 plan_confirm 卡（loop until approve / reject）
    /// reject → 寫 task_memory `decision/plan-rejected` + sessionRepo.CancelAsync / chain dispatch 0 啟動。</summary>
    public virtual async Task<PetraOrchestratorResult> ResumeFromPlanConfirmationAsync(
        Guid sessionId,
        string decision,
        string? contextOverride,
        CancellationToken ct = default)
    {
        var session = await sessionRepo.GetWithMessagesAsync(sessionId, ct);
        if (session is null)
        {
            logger.LogWarning("ResumeFromPlanConfirmationAsync 找不到 sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "session 不存在");
        }

        // 找最新 plan_confirm BossInteraction 拿 ContextJson（SubtaskPlan + talent names 還原 dispatch 用）
        var planInteraction = await db.BossInteractions
            .Where(x => x.InteractionType == "plan_confirm" && x.ContextJson != null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.ContextJson!.Contains(sessionId.ToString()), ct);

        if (planInteraction?.ContextJson is null)
        {
            logger.LogWarning("ResumeFromPlanConfirmationAsync 找不到 plan_confirm ContextJson sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "plan_confirm ContextJson 不存在");
        }

        PlanConfirmContext? planContext;
        try
        {
            planContext = JsonSerializer.Deserialize<PlanConfirmContext>(planInteraction.ContextJson, PlanConfirmJsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "ResumeFromPlanConfirmationAsync ContextJson 解析失敗 sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), $"ContextJson parse failed: {ex.Message}");
        }
        if (planContext is null)
        {
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "ContextJson deserialize null");
        }

        logger.LogInformation(
            "Stage 80：HITL plan_confirm resume decision={Decision} sessionId={SessionId} subtasks={Count} contentLen={Len}",
            decision, sessionId, planContext.Subtasks.Count, contextOverride?.Length ?? 0);

        switch (decision)
        {
            case "approve":
                return await ResumeApproveAsync(session, planContext, ct);

            case "edit":
            case "respond":
                return await ResumeEditOrRespondAsync(session, planContext, decision, contextOverride, ct);

            case "reject":
                return await ResumeRejectAsync(session, planContext, contextOverride, ct);

            default:
                logger.LogWarning("ResumeFromPlanConfirmationAsync 未知 decision={Decision} sessionId={SessionId}", decision, sessionId);
                return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), $"未知 decision={decision}");
        }
    }

    /// <summary>Stage 80：approve path — 沿用 plan_confirm 既存 SubtaskPlan + 重建 talentPicks + DispatchAndFinalize。
    /// 對齊 StartAsync v5.5 path L131-169 真實 dispatch 路徑：DispatchTalentsAsync + FinalizeGitAsync + sessionRepo.CompleteAsync。</summary>
    private async Task<PetraOrchestratorResult> ResumeApproveAsync(
        PetraSession session,
        PlanConfirmContext planContext,
        CancellationToken ct)
    {
        // 還原 SubtaskPlan + Talents（從 ContextJson 重建 / 不重 call Petra LLM）
        var subtasks = planContext.Subtasks
            .Select(s => new Subtask(s.Id, s.Skill, s.Description, s.NeedsImageContext))
            .ToList();
        var deps = planContext.Dependencies
            .Select(d => new DependencyEdge(
                d.From, d.To,
                string.Equals(d.Type, "nested", StringComparison.OrdinalIgnoreCase) ? DependencyType.Nested : DependencyType.Sequential))
            .ToList();
        var plan = new SubtaskPlan(subtasks, deps);

        var talentsList = await talentFactory.GetAllAsync(ct);
        var talentPicks = new List<ITalent>(planContext.Subtasks.Count);
        foreach (var s in planContext.Subtasks)
        {
            var pick = talentsList.FirstOrDefault(t => string.Equals(t.Name, s.TalentName, StringComparison.OrdinalIgnoreCase))
                       ?? FindTalentForSkill(s.Skill, talentsList);
            if (pick is null)
            {
                logger.LogError(
                    "Stage 80 approve：找不到 Talent={TalentName} 也找不到 Skill={Skill} 的 fallback Talent — abort sessionId={SessionId}",
                    s.TalentName, s.Skill, session.Id);
                await sessionRepo.EscalateAsync(session.Id, CancellationToken.None);
                await db.SaveChangesAsync(CancellationToken.None);
                return PetraOrchestratorResult.Failure(session.Id, planContext.Subtasks.Select(x => x.Skill).ToList(),
                    $"approve 路徑找不到 Talent={s.TalentName}/Skill={s.Skill}");
            }
            talentPicks.Add(pick);
        }

        // session 從 paused 改回 running（PetraSessionRecoveryService 對齊 / 結束時自然 CompleteAsync）
        var sessionRow = await db.PetraSessions.FirstOrDefaultAsync(x => x.Id == session.Id, ct);
        if (sessionRow is not null)
        {
            sessionRow.Status = "running";
            sessionRow.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var ctx = BuildSessionContext(session.TaskGroupId) with { SessionId = session.Id };
        return await DispatchAndFinalizeAsync(
            ctx, planContext.TaskInput, plan, talentPicks, planContext.TalentNames, images: null, ct);
    }

    /// <summary>Stage 80：edit / respond path — 用 override content 重 DecideTalentsWithPlanAsync + 開新 plan_confirm 卡（loop until approve / reject）。
    /// 同 session 內 redecide（不開新 PetraSession）— append `[Christ EDIT]: content` 進 session messages 維持 audit trail。</summary>
    private async Task<PetraOrchestratorResult> ResumeEditOrRespondAsync(
        PetraSession session,
        PlanConfirmContext planContext,
        string decision,
        string? contextOverride,
        CancellationToken ct)
    {
        var contentPart = string.IsNullOrWhiteSpace(contextOverride) ? "" : $"\n\n[Christ {decision.ToUpperInvariant()}]: {contextOverride}";
        var mergedInput = planContext.TaskInput + contentPart;

        await sessionRepo.AppendMessageAsync(session.Id, "user", $"[plan_confirm {decision}] {contextOverride ?? ""}", ct: ct);
        await db.SaveChangesAsync(ct);

        // session 從 paused 改回 running（redecide 進行中）
        var sessionRow = await db.PetraSessions.FirstOrDefaultAsync(x => x.Id == session.Id, ct);
        if (sessionRow is not null)
        {
            sessionRow.Status = "running";
            sessionRow.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var talentsList = await talentFactory.GetAllAsync(ct);
        var ctx = BuildSessionContext(session.TaskGroupId) with { SessionId = session.Id };

        var (plan, talentPicks) = await DecideTalentsWithPlanAsync(mergedInput, talentsList, ctx, ct, images: null);
        if (talentPicks.Count == 0)
        {
            logger.LogWarning("Stage 80 redecide 回空序列 sessionId={SessionId} decision={Decision}", session.Id, decision);
            await sessionRepo.CompleteAsync(session.Id, ct);
            await db.SaveChangesAsync(ct);
            return PetraOrchestratorResult.Empty(session.Id);
        }
        var decidedCapabilities = plan.Subtasks.Select(s => s.SkillName).ToList();

        // 重開 plan_confirm 卡（loop until approve / reject）
        await WaitForPlanConfirmationAsync(session.Id, mergedInput, plan, talentPicks, decidedCapabilities, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Stage 80：HITL plan_confirm redecide 完成 sessionId={SessionId} decision={Decision} subtasks={Count}",
            session.Id, decision, plan.Subtasks.Count);

        return PetraOrchestratorResult.Paused(session.Id, decidedCapabilities,
            $"Petra 已依 Christ {decision} 重拆 {plan.Subtasks.Count} subtask 等再次拍板。");
    }

    /// <summary>Stage 80：reject path — 寫 task_memory `decision/plan-rejected` + sessionRepo.CancelAsync / chain dispatch 0 啟動。</summary>
    private async Task<PetraOrchestratorResult> ResumeRejectAsync(
        PetraSession session,
        PlanConfirmContext planContext,
        string? contextOverride,
        CancellationToken ct)
    {
        var note = string.IsNullOrWhiteSpace(contextOverride)
            ? "Christ rejected plan via HITL plan_confirm 閘門"
            : $"Christ rejected plan: {contextOverride}";
        try
        {
            await memoryRepo.UpsertTaskMemoryAsync(
                session.Id, projectId: null,
                key: "decision/plan-rejected",
                content: note,
                createdByTalent: "Petra",
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Stage 80 reject：UpsertTaskMemoryAsync 失敗（容錯不擋 reject 完成）sessionId={SessionId}", session.Id);
        }

        await sessionRepo.CancelAsync(session.Id, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Stage 80：HITL plan_confirm reject sessionId={SessionId} subtasks={Count}",
            session.Id, planContext.Subtasks.Count);

        // Stage 81 子項 10（議題 #3 + #8 命名語意收口）：Cancelled 工廠取代 Done — DispatchedWorkerCount=0 真實對齊 reject path 0 chain dispatch。
        return PetraOrchestratorResult.Cancelled(session.Id, planContext.Subtasks.Select(s => s.Skill).ToList(),
            $"Petra plan 被 Christ HITL reject — task_memory `decision/plan-rejected` 已寫入 / session cancelled。");
    }

    /// <summary>Stage 80：抽出 StartAsync v5.5 path 既有 dispatch + finalize 收尾段 — 供 approve resume + 既有 StartAsync 共用。
    /// 對齊既有 L131-169 邏輯：建 talentAgents + talentNameToIdMap + DispatchTalentsAsync + FinalizeGitAsync + sessionRepo.CompleteAsync。</summary>
    private async Task<PetraOrchestratorResult> DispatchAndFinalizeAsync(
        PetraSessionContext ctx,
        string taskInput,
        SubtaskPlan plan,
        IReadOnlyList<ITalent> talentPicks,
        IReadOnlyList<string> dispatchNames,
        IReadOnlyList<ImageAttachment>? images,
        CancellationToken ct)
    {
        try
        {
            var talentAgents = new AIAgent[talentPicks.Count];
            for (var i = 0; i < talentPicks.Count; i++)
            {
                talentAgents[i] = talentPicks[i].CreateAgent(ctx, plan.Subtasks[i].SkillName);
            }

            var useV5Memory = await workflowResolver.GetUseV5MemoryAsync(ct);
            var names = talentPicks.Select(t => t.Name).Distinct().ToList();
            var talentNameToIdMap = await db.Talents
                .Where(t => names.Contains(t.Name) && t.ProjectId == null)
                .ToDictionaryAsync(t => t.Name, t => t.Id, ct);

            var outcome = await DispatchTalentsAsync(
                ctx.SessionId, taskInput, plan, talentPicks, talentAgents,
                useV5Memory, talentNameToIdMap, images, ctx, ct);
            await db.SaveChangesAsync(ct);

            // Stage 81：replan signal handling（DispatchAndFinalize 也走同 flow / approve resume 期間可能再觸發 replan loop）
            if (outcome.Replan is { } signal)
            {
                return await HandleReplanSignalAsync(ctx.SessionId, taskInput, signal, ct);
            }

            var decidedCapabilities = plan.Subtasks.Select(s => s.SkillName).ToList();
            var prUrl = await FinalizeGitAsync(ctx, taskInput, decidedCapabilities, dispatchNames, ct);

            await sessionRepo.CompleteAsync(ctx.SessionId, ct);
            await db.SaveChangesAsync(ct);

            var summary = $"Petra 完成 {dispatchNames.Count} dispatch（{string.Join(" → ", dispatchNames)}）"
                + (prUrl is null ? "。" : $" + PR {prUrl}。");
            return PetraOrchestratorResult.Done(ctx.SessionId, decidedCapabilities, summary);
        }
        catch (OperationCanceledException)
        {
            await sessionRepo.EscalateAsync(ctx.SessionId, CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DispatchAndFinalizeAsync 失敗 sessionId={SessionId}", ctx.SessionId);
            await sessionRepo.EscalateAsync(ctx.SessionId, CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
            return PetraOrchestratorResult.Failure(ctx.SessionId, Array.Empty<string>(), ex.Message);
        }
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
    ///
    /// Stage 72：v5.5 Phase 2 Step 5 — 加第 3 optional param `baseTemplateOverride` 支援 DB-driven prompt。
    /// override != null（feature flag=true 從 DB load）→ 用 override 當 base template + string.Replace placeholder 注入動態值。
    /// override == null（feature flag=false / Test 9/27/28 baseline）→ 走 PetraPromptTemplate.Template 既有 hardcoded constant。
    /// 動態 skill roster + Stage 70/71 decomposition/output 段 100% 不動（議題 4 內容不動 / 只搬家）。
    /// </summary>
    private static string BuildPetraSystemPrompt(
        string capabilityRoster,
        bool useSubtaskPlanning = false,
        string? baseTemplateOverride = null)
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

★ Few-shot 反例（不拆 — 線性整包）：
  輸入：「打磨多 form 錯誤處理 toast 通知（跨 5 form 同類改動）」
  ❌ 過拆（錯誤）：{"subtasks":[{"id":1,"skill":"code_implementation","description":"修 Form A toast"},{"id":2,"skill":"code_implementation","description":"修 Form B toast"},{"id":3,"skill":"code_implementation","description":"修 Form C toast"}],"dependencies":[{"from":1,"to":2,"type":"sequential"},{"from":2,"to":3,"type":"sequential"}]}
  ✅ 線性整包（正確）：{"subtasks":[{"id":1,"skill":"code_implementation","description":"打磨多 form 錯誤處理 toast 通知"}],"dependencies":[]}

【判斷邊界】
- 線性整包（1 subtask）：同類改動 + 同 Skill + 同 scope — 不管幾個 form / 幾個檔 / 幾處改動
- 真不同 scope（拆 N subtask）：任務含真正不同性質（實作 + review + 測試 或 跨 module 獨立功能）+ 跨 Skill 串接
- 直覺判準：「一句話描述的 code 任務」= 線性整包 / 「A 完成後才能做 B 且性質真的不同」= 拆解

【判斷每個 subtask 是否需要附圖 context】（Stage 79：v5.5 image flow 補完）

對齊業界紀律「only give each agent the tools it actually needs / pass images only to worker agents that need them」。

判準：
- UI 修改 / 視覺 bug / mockup 對齊 → needsImageContext: true（Cody UI 修 / Vera UI review / Quinn UI E2E test）
- 純後端 logic / 文件 / 測試 logic → needsImageContext: false（Cody backend / Sage docs / Quinn logic test）
- 預設 false（保守紀律：未明確要求視覺 context 時不傳）

★ Few-shot 範例（UI bug case / 含 image context）：
  輸入：「修 Dashboard 操作中心 BossInteraction 卡片排版（附截圖）」
  輸出：{"subtasks":[{"id":1,"skill":"code_implementation","description":"修 BossInteraction 卡片排版","needsImageContext":true},{"id":2,"skill":"code_review","description":"review UI 變動","needsImageContext":true}],"dependencies":[{"from":1,"to":2,"type":"sequential"}]}

★ Few-shot 範例（後端 case / 0 image context）：
  輸入：「補 PetraInboxRepository.GetRecentAsync xUnit test」
  輸出：{"subtasks":[{"id":1,"skill":"qa_testing","description":"補 PetraInboxRepository xUnit test","needsImageContext":false}],"dependencies":[]}

★ Few-shot 範例（docs case / 0 image context）：
  輸入：「Stage 79 結案紀錄章節寫 Roadmap.md」
  輸出：{"subtasks":[{"id":1,"skill":"documentation","description":"寫 Stage 79 Roadmap 實作紀錄","needsImageContext":false}],"dependencies":[]}

★ Few-shot 反例 1（Stage 81 議題 #2 修法 — 純文字 prompt 無 attachment）：
  輸入：「補 PetraInbox FIFO ordering xUnit test」
  輸出：{"subtasks":[{"id":1,"skill":"qa_testing","description":"補 FIFO ordering test","needsImageContext":false}],"dependencies":[]}
  ⚠️ 紀律：prompt 0 image attachment → 所有 subtask needsImageContext 必 false（避免 Trial_v24 揭純文字誤判 true）

★ Few-shot 反例 2（Stage 81 議題 #2 修法 — 含 image 但純後端 / docs 改動）：
  輸入：「[附截圖] 修 PetraInboxRepository.GetRecentAsync 排序 bug」
  輸出：{"subtasks":[{"id":1,"skill":"code_implementation","description":"修 GetRecentAsync 排序","needsImageContext":false}],"dependencies":[]}
  ⚠️ 紀律：即使含 image，subtask 性質純後端 / docs → needsImageContext=false

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
- 正例（Stage 79 含 needsImageContext）：{"subtasks":[{"id":1,"skill":"code_implementation","description":"...","needsImageContext":true}],"dependencies":[]}
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

        // Stage 72：base template 來源
        // - override != null → DB-loaded（含 {{capabilityRoster}}/{{decompositionSection}}/{{outputSection}} placeholder）
        // - override == null → PetraPromptTemplate.Template 既有 hardcoded constant（Test 9/27/28 baseline / Stage 64+67+70+71 累積內容 byte-for-byte 對齊）
        var baseTemplate = baseTemplateOverride ?? PetraPromptTemplate.Template;
        return baseTemplate
            .Replace("{{capabilityRoster}}",     capabilityRoster)
            .Replace("{{decompositionSection}}", decompositionSection)
            .Replace("{{outputSection}}",        outputSection);
    }

    /// <summary>
    /// Stage 72 + Stage 73：v5.5 Phase 2/3 — runtime async wrapper for BuildPetraSystemPrompt（含 feature flag check + DB load）。
    ///
    /// Stage 72：flag=`Workflow:UseV5PromptDb`=true → 透過 PromptResolver 取 DB SkillPrompt `petra_orchestration` PromptBody 當 base template override。
    /// Stage 73：flag=true + Petra TalentPrompt 存在 → prepend persona body 上 base template；
    ///          不存在或 flag=false → 純 base template（backwards-compatible 守護 0 regression）。
    /// </summary>
    private async Task<string> BuildPetraSystemPromptForRuntimeAsync(
        string capabilityRoster,
        bool useSubtaskPlanning,
        CancellationToken ct)
    {
        var dbBase = await promptResolver.ResolvePetraBaseTemplateAsync(ct);
        var baseTemplate = BuildPetraSystemPrompt(capabilityRoster, useSubtaskPlanning, dbBase);

        // Stage 73：Petra persona prepend（flag-gated + TalentPrompt 存在才注入 / 不存在 fallback 純 base template）
        var persona = await ResolvePetraPersonaAsync(ct);
        if (string.IsNullOrWhiteSpace(persona)) return baseTemplate;

        return $"""
{persona}

────────────────────────────

{baseTemplate}
""";
    }

    /// <summary>
    /// Stage 73：取 Petra TalentPrompt persona（透過 db 查 Petra Talent.Id + PromptResolver cache）。
    /// flag=false / Petra Talent 不存在 / Petra TalentPrompt 不存在 → null（caller fallback 純 base template）。
    /// </summary>
    private async Task<string?> ResolvePetraPersonaAsync(CancellationToken ct)
    {
        var petra = await db.Talents
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ProjectId == null && t.Name == "Petra", ct);
        if (petra is null) return null;

        return await promptResolver.ResolveTalentPersonaAsync(petra.Id, ct);
    }

    // ========== Stage 81：動態 replan + HITL retry gate（LangGraph cycles 對齊 v1.1 議題 1+2+#3-#8 收口） ==========

    /// <summary>Stage 81：DispatchTalentsAsync 回傳結構 — Summaries（已完成 subtask 摘要）+ Replan（觸發信號 / null = 正常完成）。</summary>
    internal sealed record DispatchOutcome(
        List<WorkerDispatchSummary> Summaries,
        ReplanSignal? Replan);

    /// <summary>Stage 81：replan / cap-reached 觸發信號 — caller 路由分支 HandleReplanSignalAsync 用。
    /// Kind="replan_confirm" → 開卡 + Replanning result / Kind="cap_reached_iter|cost" → intervention card + Cancelled result。</summary>
    internal sealed record ReplanSignal(
        string Kind,
        int CurrentSubtaskId,
        string TriggerReason,
        string RetryInstruction,
        string LastOutputPreview,
        int CompletedCount,
        int ReplanIteration,
        int? CapValueInt,
        decimal? CapValueDecimal);

    /// <summary>Stage 81：Petra LLM JSON 回應結構 — LangGraph cycles 紀律：不回 plan 結構 / 只回 retry instruction（W8 對齊）。</summary>
    internal sealed record ReplanDecisionJson(
        [property: JsonPropertyName("shouldReplan")]     bool   ShouldReplan,
        [property: JsonPropertyName("reason")]           string Reason,
        [property: JsonPropertyName("retryInstruction")] string RetryInstruction,
        [property: JsonPropertyName("targetSubtaskId")]  int    TargetSubtaskId);

    /// <summary>Stage 81：replan_confirm BossInteraction.ContextJson 結構 — render UI + Resume 用。</summary>
    internal sealed record ReplanConfirmContext(
        Guid   SessionId,
        string TaskInput,
        int    CurrentSubtaskId,
        string CurrentSkillName,
        string CurrentTalentName,
        string TriggerReason,
        string RetryInstruction,
        string LastOutputPreview,
        int    CompletedCount,
        int    ReplanIteration);

    // Stage 81 議題 #7：trigger 偵測 regex — schema 對齊 CLAUDE_Vera.md L113 `"critical":[{...}]` 非空 + CLAUDE_Quinn.md L75 `"status":"failed"`
    private static readonly Regex VeraCriticalPattern =
        new("\"critical\"\\s*:\\s*\\[\\s*\\{", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex QuinnFailedPattern =
        new("\"status\"\\s*:\\s*\"failed\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Stage 81：純規則式偵測 — Vera code_review subtask 含 critical 非空 / Quinn qa_testing subtask 含 status=failed。
    /// 其他 skill 永遠不觸發 replan（避免 scope creep — W1 紀律）。</summary>
    internal static (bool ShouldTrigger, string TriggerReason) DetectReplanTrigger(string skill, string outputText)
    {
        if (string.IsNullOrEmpty(outputText)) return (false, "");
        if (string.Equals(skill, "code_review", StringComparison.OrdinalIgnoreCase)
            && VeraCriticalPattern.IsMatch(outputText))
            return (true, "vera_critical");
        if (string.Equals(skill, "qa_testing", StringComparison.OrdinalIgnoreCase)
            && QuinnFailedPattern.IsMatch(outputText))
            return (true, "quinn_failed");
        return (false, "");
    }

    /// <summary>Stage 81：每個 subtask dispatch 完成後檢查 — cost cap → trigger 偵測 → iter cap → Petra LLM call → return signal。
    /// null = 正常 / 非 null = caller 應該 return DispatchOutcome with signal 並 stop 後續 dispatch。</summary>
    private async Task<ReplanSignal?> CheckReplanTriggerAfterDispatchAsync(
        Guid sessionId,
        string taskInput,
        Subtask subtask,
        string talentName,
        string outputText,
        List<WorkerDispatchSummary> summaries,
        PetraSessionContext ctx,
        CancellationToken ct)
    {
        // 1. 更新 session cost USD（每個 dispatch 都 update / 不論 flag）
        await sessionRepo.UpdateSessionCostUsdAsync(sessionId, ct);
        await db.SaveChangesAsync(ct);

        // 2. 讀 flag — UseDynamicReplanning 內部已守 UseHITLPlanConfirmation=true 為前置（補強 #A）
        var useDynamicReplanning = await workflowResolver.GetUseDynamicReplanningAsync(ct);
        if (!useDynamicReplanning) return null;

        var (iter, cost) = await sessionRepo.GetReplanStateAsync(sessionId, ct);
        var maxIter = await workflowResolver.GetMaxReplanIterationsAsync(ct);
        var capUsd  = await workflowResolver.GetReplanCostCapUsdAsync(ct);

        // 3. cost cap 永遠檢查（場景 H — 超過上限直接 abort / 不論是否 trigger）
        if (cost > capUsd)
        {
            logger.LogWarning(
                "[Stage 81] session cost cap reached sessionId={SessionId} cost={Cost} cap={Cap} — abort + intervention",
                sessionId, cost, capUsd);
            return new ReplanSignal("cap_reached_cost", subtask.Id,
                $"cost cap ${capUsd:F2} reached (current=${cost:F4})",
                "", "", summaries.Count, iter, null, capUsd);
        }

        // 4. trigger 偵測（Vera critical / Quinn failed）
        var (shouldTrigger, triggerReason) = DetectReplanTrigger(subtask.SkillName, outputText);
        if (!shouldTrigger) return null;

        // 5. iter cap（場景 G — 已到上限不再 fire replan / 改 intervention）
        if (iter >= maxIter)
        {
            logger.LogWarning(
                "[Stage 81] max replan iterations reached sessionId={SessionId} iter={Iter} max={Max} — abort + intervention",
                sessionId, iter, maxIter);
            return new ReplanSignal("cap_reached_iter", subtask.Id,
                $"max iterations N={maxIter} reached",
                "", "", summaries.Count, iter, maxIter, null);
        }

        // 6. Petra LLM call 給 retry instruction（W8 紀律 — 不回 plan 結構）
        var decision = await InvokePetraReplanAsync(taskInput, summaries, subtask, triggerReason, outputText, ctx, ct);
        if (decision is null || !decision.ShouldReplan)
        {
            logger.LogInformation(
                "[Stage 81] Petra 判斷不需 replan sessionId={SessionId} subtaskId={SubtaskId} — 容錯往下走",
                sessionId, subtask.Id);
            return null;
        }

        var preview = outputText.Length > 500 ? outputText[..500] + "..." : outputText;
        logger.LogInformation(
            "[Stage 81] replan trigger fire sessionId={SessionId} subtaskId={SubtaskId} trigger={Trigger} iter={Iter}",
            sessionId, subtask.Id, triggerReason, iter);
        return new ReplanSignal("replan_confirm", subtask.Id, triggerReason,
            decision.RetryInstruction, preview, summaries.Count, iter, null, null);
    }

    /// <summary>Stage 81：caller 從 DispatchTalentsAsync 收到 ReplanSignal 後分支 — replan_confirm 開卡 / cap_reached 開 intervention。</summary>
    private async Task<PetraOrchestratorResult> HandleReplanSignalAsync(
        Guid sessionId, string taskInput, ReplanSignal signal, CancellationToken ct)
    {
        if (signal.Kind == "cap_reached_iter" || signal.Kind == "cap_reached_cost")
            return await HandleCapReachedAsync(sessionId, signal, ct);

        // 取 plan_confirm ContextJson 還原 currentSkill / currentTalent（render UI + Resume 用 / single source of truth）
        var planInteraction = await db.BossInteractions
            .Where(x => x.InteractionType == "plan_confirm" && x.ContextJson != null
                        && x.ContextJson.Contains(sessionId.ToString()))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        string currentSkill = "", currentTalentName = "";
        if (planInteraction?.ContextJson is not null)
        {
            try
            {
                var pctx = JsonSerializer.Deserialize<PlanConfirmContext>(planInteraction.ContextJson, PlanConfirmJsonOptions);
                var found = pctx?.Subtasks.FirstOrDefault(s => s.Id == signal.CurrentSubtaskId);
                if (found is not null)
                {
                    currentSkill = found.Skill;
                    currentTalentName = found.TalentName;
                }
            }
            catch (JsonException) { /* render fallback 空字串 */ }
        }

        await WaitForReplanConfirmationAsync(
            sessionId, taskInput, signal.CurrentSubtaskId, currentSkill, currentTalentName,
            signal.TriggerReason, signal.RetryInstruction, signal.LastOutputPreview,
            signal.CompletedCount, signal.ReplanIteration, ct);
        await db.SaveChangesAsync(ct);

        return PetraOrchestratorResult.Replanning(
            sessionId, signal.CurrentSubtaskId, signal.RetryInstruction, signal.TriggerReason);
    }

    /// <summary>Stage 81 場景 G + H：max iter / cost cap 達上限 → 開 intervention 卡 + 寫 task_memory + CancelAsync + Cancelled。</summary>
    private async Task<PetraOrchestratorResult> HandleCapReachedAsync(
        Guid sessionId, ReplanSignal signal, CancellationToken ct)
    {
        var memoryContent = signal.Kind == "cap_reached_iter"
            ? $"max iterations N={signal.CapValueInt} reached"
            : $"cost cap ${signal.CapValueDecimal:F2} reached";

        try
        {
            await memoryRepo.UpsertTaskMemoryAsync(
                sessionId, projectId: null,
                key: "decision/replan-cap-reached",
                content: memoryContent,
                createdByTalent: "Petra",
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Stage 81 HandleCapReached：UpsertTaskMemoryAsync 失敗（容錯不擋）sessionId={SessionId}", sessionId);
        }

        // 開既有 intervention 卡（Christ 拍板介入 / NotifyActionsJson 「我知道了」ack 按鈕）
        _ = interactionService.CreateInteractionAsync(
            interactionType:      "intervention",
            title:                "動態 replan cap 達上限",
            description:          $"Stage 81：{memoryContent} — session cancelled / chain dispatch 0 啟動。",
            project:              null,
            agentName:            "Petra",
            availableActionsJson: InteractionService.NotifyActionsJson,
            contextJson:          $$"""{"sessionId":"{{sessionId}}","kind":"{{signal.Kind}}","subtaskId":{{signal.CurrentSubtaskId}}}""",
            systemNotes:          $"[Stage 81 replan-cap] sessionId={sessionId.ToString("N")[..8]} — kind={signal.Kind}");

        await sessionRepo.CancelAsync(sessionId, ct);
        await db.SaveChangesAsync(ct);

        return PetraOrchestratorResult.Cancelled(sessionId, Array.Empty<string>(),
            $"Stage 81 replan cap reached：{memoryContent} — session cancelled / intervention card opened.");
    }

    /// <summary>Stage 81：開 BossInteraction replan_confirm 卡 + PauseAsync（對齊 Stage 80 WaitForPlanConfirmationAsync pattern）。</summary>
    private async Task WaitForReplanConfirmationAsync(
        Guid sessionId, string taskInput, int currentSubtaskId,
        string currentSkillName, string currentTalentName,
        string triggerReason, string retryInstruction, string lastOutputPreview,
        int completedCount, int replanIteration, CancellationToken ct)
    {
        var context = new ReplanConfirmContext(
            SessionId:         sessionId,
            TaskInput:         taskInput,
            CurrentSubtaskId:  currentSubtaskId,
            CurrentSkillName:  currentSkillName,
            CurrentTalentName: currentTalentName,
            TriggerReason:     triggerReason,
            RetryInstruction:  retryInstruction,
            LastOutputPreview: lastOutputPreview,
            CompletedCount:    completedCount,
            ReplanIteration:   replanIteration);

        var contextJson = JsonSerializer.Serialize(context, PlanConfirmJsonOptions);
        var taskFirstLine = (taskInput ?? "").Split('\n').FirstOrDefault() ?? "";
        var title = taskFirstLine.Length > 200 ? taskFirstLine[..200] + "…" : taskFirstLine;
        var description = $"Petra 建議 retry subtask #{currentSubtaskId}（{currentTalentName} / {currentSkillName}）— 觸發：{triggerReason}";
        var systemNotes = $"[Stage 81 動態 replan #{replanIteration + 1}] sessionId={sessionId.ToString("N")[..8]} — 4 decision：approve（同 subtask 重 dispatch）/ edit（修 retry instruction）/ reject（不採納 / 接受原結果繼續）/ respond（補充）";

        _ = interactionService.CreateInteractionAsync(
            interactionType:      "replan_confirm",
            title:                title,
            description:          description,
            project:              null,
            agentName:            "Petra",
            availableActionsJson: InteractionService.ReplanConfirmActionsJson,
            contextJson:          contextJson,
            systemNotes:          systemNotes);

        await sessionRepo.PauseAsync(sessionId, ct);
    }

    /// <summary>Stage 81：Petra LLM call 給 retry instruction（W8 紀律 — 不回新 plan 結構）。
    /// 容錯：JSON parse 失敗 / null shouldReplan → return null caller fallback 不 fire replan（保 chain 不中斷）。</summary>
    private async Task<ReplanDecisionJson?> InvokePetraReplanAsync(
        string taskInput, List<WorkerDispatchSummary> done, Subtask currentSubtask,
        string triggerReason, string lastOutput, PetraSessionContext ctx, CancellationToken ct)
    {
        var systemPrompt = BuildReplanRetrySystemPrompt();
        var doneText = done.Count == 0
            ? "（無）"
            : string.Join("\n", done.Select(d => $"- {d.WorkerName}({d.Capability}): {Truncate(d.Output, 300)}"));
        var lastTrunc = Truncate(lastOutput, 1500);
        // 用 $$"""..."""（雙 $）— literal brace 寫 `{` `}` / 插值寫 `{{var}}` `}}`
        var userPrompt = $$"""
            【ORIGINAL TASK】{{taskInput}}

            【ALREADY COMPLETED】
            {{doneText}}

            【CURRENT SUBTASK】#{{currentSubtask.Id}} {{currentSubtask.SkillName}} — {{currentSubtask.Description}}

            【LAST OUTPUT (TRIGGER REASON: {{triggerReason}})】
            {{lastTrunc}}

            請判斷是否需要 retry — 只回 JSON: {"shouldReplan":true/false,"reason":"...","retryInstruction":"...","targetSubtaskId":{{currentSubtask.Id}}}
            """;

        try
        {
            var provider = providerFactory.Create(PetraAgentName);
            // Stage 82 子項 2：AsyncLocal scope — token_logs.PetraSessionId 透傳（replan decide LLM call）
            using var _scope = TokenTrackingProvider.BeginPetraSessionScope(ctx.SessionId);
            var resp = await provider.CompleteAsync(systemPrompt, userPrompt, ct, images: null);
            await sessionRepo.AppendMessageAsync(ctx.SessionId, "assistant",
                $"[Stage 81 replan decide / trigger={triggerReason}]\n{resp.Content}", ct: ct);
            await db.SaveChangesAsync(ct);
            return TryParseReplanDecision(resp.Content);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "[Stage 81] InvokePetraReplanAsync 失敗 sessionId={SessionId} subtaskId={SubtaskId} — 容錯回 null（fallback 不 fire replan）",
                ctx.SessionId, currentSubtask.Id);
            return null;
        }
    }

    private static string BuildReplanRetrySystemPrompt() => """
你是 Petra — 動態 replan + HITL retry gate 評估者（Stage 81 LangGraph cycles 業界紀律）。

收到 Worker dispatch 結果觸發 replan condition（Vera critical 非空 / Quinn status=failed），請判斷：
1. 是否真的需要 retry？（有時 Vera critical 是低風險可接受 / Quinn failed 是 unverifiable_targets 不可修）
2. 如需 retry，給 currentSubtask 的「retry instruction」(對應 currentTalent 該怎麼重做的具體指示)

【重要紀律】
- **不回新 SubtaskPlan 結構** — 只回 retry instruction string 給 currentSubtask
- retry instruction 要具體 actionable（含修哪個 file / 哪行 / 改什麼）
- 對齊 LangGraph cycles 真實語意：同 subtask 重 dispatch with new instruction

【輸出格式】只回單行 JSON：
{"shouldReplan":true/false,"reason":"...","retryInstruction":"...","targetSubtaskId":N}

【Few-shot 範例 1：Vera critical → retry】
trigger=vera_critical, last output 含 "critical":[{"file":"PipelineView.razor","line":42,"message":"Circuit 斷線無 catch"}]
→ {"shouldReplan":true,"reason":"Vera 揭 Circuit 斷線真實 production 風險","retryInstruction":"Cody 重做 PipelineView.razor 第 42 行附近 5 個 handler 加 try-catch Exception 防 Circuit 斷線","targetSubtaskId":1}

【Few-shot 範例 2：Quinn failed → retry】
trigger=quinn_failed, last output 含 "status":"failed","failed_tests":["FooTests.cs 第 23 行型別不相符"]
→ {"shouldReplan":true,"reason":"Quinn test 編譯失敗可修","retryInstruction":"Cody 修 FooTests.cs 第 23 行型別不相符 + 重跑 dotnet test","targetSubtaskId":1}

【Few-shot 範例 3：Quinn unverifiable_targets → retry】
trigger=quinn_failed, last output 含 "unverifiable_targets":["路徑1: 找不到類別 X"]
→ {"shouldReplan":true,"reason":"target class 不存在 / 需 Cody 補建","retryInstruction":"Cody 補建 src/ 內 X 類別實作 + 重跑 QA","targetSubtaskId":1}

【Few-shot 反例（不需 retry / shouldReplan=false）】
trigger=vera_critical, Vera critical 是 documentation 微調建議 / 非 production safety
→ {"shouldReplan":false,"reason":"Vera critical 是 doc 微調建議 / production safety 無風險 / 接受原 output","retryInstruction":"","targetSubtaskId":1}
""";

    private static ReplanDecisionJson? TryParseReplanDecision(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // 去 markdown code fence 包裹（對齊 SubtaskPlanParser 既有紀律）
        var stripped = raw.Trim();
        if (stripped.StartsWith("```"))
        {
            var firstNewline = stripped.IndexOf('\n');
            if (firstNewline > 0) stripped = stripped[(firstNewline + 1)..];
            var fenceIdx = stripped.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceIdx >= 0) stripped = stripped[..fenceIdx];
            stripped = stripped.Trim();
        }
        try
        {
            return JsonSerializer.Deserialize<ReplanDecisionJson>(stripped, PlanConfirmJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string s, int max)
        => s.Length > max ? s[..max] + "...(truncated)" : s;

    /// <summary>Stage 81：HITL replan_confirm 4 decision pattern resume — PlanConfirmationProcessor 看到 responded 拉起 fire。
    /// approve → ContinueChainFromSubtaskAsync 從 currentSubtaskId 重 dispatch with retry instruction（LangGraph cycles）
    /// edit / respond → 重 InvokePetraReplanAsync 含 override context → 新 retry instruction → 開新 replan_confirm 卡（loop）
    /// reject → ContinueChainFromSubtaskAsync 從 currentSubtaskId+1 繼續（接受原 output / 不 cancel session / 議題 1 修法 v1.1）。</summary>
    public virtual async Task<PetraOrchestratorResult> ResumeFromReplanConfirmationAsync(
        Guid sessionId, string decision, string? contextOverride, CancellationToken ct = default)
    {
        var session = await sessionRepo.GetWithMessagesAsync(sessionId, ct);
        if (session is null)
        {
            logger.LogWarning("ResumeFromReplanConfirmationAsync 找不到 sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "session 不存在");
        }

        var replanInteraction = await db.BossInteractions
            .Where(x => x.InteractionType == "replan_confirm" && x.ContextJson != null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.ContextJson!.Contains(sessionId.ToString()), ct);
        if (replanInteraction?.ContextJson is null)
        {
            logger.LogWarning("ResumeFromReplanConfirmationAsync 找不到 replan_confirm ContextJson sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "replan_confirm ContextJson 不存在");
        }

        ReplanConfirmContext? ctxRecord;
        try
        {
            ctxRecord = JsonSerializer.Deserialize<ReplanConfirmContext>(replanInteraction.ContextJson, PlanConfirmJsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "ResumeFromReplanConfirmationAsync ContextJson 解析失敗 sessionId={SessionId}", sessionId);
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), $"ContextJson parse failed: {ex.Message}");
        }
        if (ctxRecord is null)
            return PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), "replan_confirm ContextJson deserialize null");

        logger.LogInformation(
            "Stage 81：HITL replan_confirm resume decision={Decision} sessionId={SessionId} currentSubtaskId={SubtaskId} iter={Iter}",
            decision, sessionId, ctxRecord.CurrentSubtaskId, ctxRecord.ReplanIteration);

        return decision switch
        {
            "approve" => await ResumeReplanApproveAsync(session, ctxRecord, ct),
            "edit" or "respond"
                      => await ResumeReplanEditOrRespondAsync(session, ctxRecord, decision, contextOverride, ct),
            "reject"  => await ResumeReplanRejectAsync(session, ctxRecord, ct),
            _         => PetraOrchestratorResult.Failure(sessionId, Array.Empty<string>(), $"未知 decision={decision}"),
        };
    }

    /// <summary>Stage 81 場景 C：approve = LangGraph cycles 同 subtask 重 dispatch with retry instruction（不從頭跑）。</summary>
    private async Task<PetraOrchestratorResult> ResumeReplanApproveAsync(
        PetraSession session, ReplanConfirmContext ctxRecord, CancellationToken ct)
    {
        await sessionRepo.IncrementReplanIterationAsync(session.Id, ct);
        await db.SaveChangesAsync(ct);

        // 從 currentSubtaskId 起重 dispatch（含 retry instruction prepend）— 對齊 LangGraph cycles
        return await ContinueChainFromSubtaskAsync(
            session, ctxRecord.CurrentSubtaskId, ctxRecord.RetryInstruction, ct);
    }

    /// <summary>Stage 81 場景 D + F：edit / respond = 重 InvokePetraReplanAsync 含 override → 新 retry instruction → 開新 replan_confirm 卡。</summary>
    private async Task<PetraOrchestratorResult> ResumeReplanEditOrRespondAsync(
        PetraSession session, ReplanConfirmContext ctxRecord, string decision,
        string? contextOverride, CancellationToken ct)
    {
        await sessionRepo.IncrementReplanIterationAsync(session.Id, ct);
        await db.SaveChangesAsync(ct);

        var overrideText = string.IsNullOrWhiteSpace(contextOverride) ? "" : contextOverride;
        await sessionRepo.AppendMessageAsync(session.Id, "user",
            $"[Stage 81 replan_{decision}] {overrideText}", ct: ct);
        await db.SaveChangesAsync(ct);

        // 取 plan_confirm ContextJson 還原 currentSubtask 細節（single source of truth）
        var planInteraction = await db.BossInteractions
            .Where(x => x.InteractionType == "plan_confirm" && x.ContextJson != null
                        && x.ContextJson.Contains(session.Id.ToString()))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (planInteraction?.ContextJson is null)
            return PetraOrchestratorResult.Failure(session.Id, Array.Empty<string>(),
                "edit/respond 路徑找不到 plan_confirm ContextJson（補強 #A 紀律 — single source of truth）");

        var planContext = JsonSerializer.Deserialize<PlanConfirmContext>(planInteraction.ContextJson, PlanConfirmJsonOptions);
        var currentSub = planContext?.Subtasks.FirstOrDefault(s => s.Id == ctxRecord.CurrentSubtaskId);
        if (planContext is null || currentSub is null)
            return PetraOrchestratorResult.Failure(session.Id, Array.Empty<string>(),
                $"plan_confirm ContextJson subtask #{ctxRecord.CurrentSubtaskId} 不存在");

        var subtask = new Subtask(currentSub.Id, currentSub.Skill, currentSub.Description, currentSub.NeedsImageContext);
        var summaries = await BuildSummariesFromSessionMessagesAsync(session.Id, ct);

        // 合 retry instruction + Christ override → 新 trigger reason
        var mergedTriggerReason = $"{ctxRecord.TriggerReason} + christ_{decision}";
        var augmentedLastOutput = $"{ctxRecord.LastOutputPreview}\n\n[Stage 81 retry instruction 既有]: {ctxRecord.RetryInstruction}\n[Stage 81 Christ {decision.ToUpperInvariant()}]: {overrideText}";

        var ctx = BuildSessionContext(session.TaskGroupId) with { SessionId = session.Id };
        var newDecision = await InvokePetraReplanAsync(
            ctxRecord.TaskInput, summaries, subtask,
            mergedTriggerReason, augmentedLastOutput, ctx, ct);

        if (newDecision is null || !newDecision.ShouldReplan)
        {
            logger.LogInformation(
                "[Stage 81] edit/respond Petra 判斷新 plan 不需 replan sessionId={SessionId} — fallback 接受原 output 繼續",
                session.Id);
            return await ContinueChainFromSubtaskAsync(session, ctxRecord.CurrentSubtaskId + 1, null, ct);
        }

        var newPreview = augmentedLastOutput.Length > 500 ? augmentedLastOutput[..500] + "..." : augmentedLastOutput;
        var (newIter, _) = await sessionRepo.GetReplanStateAsync(session.Id, ct);

        await WaitForReplanConfirmationAsync(
            session.Id, ctxRecord.TaskInput, ctxRecord.CurrentSubtaskId,
            ctxRecord.CurrentSkillName, ctxRecord.CurrentTalentName,
            mergedTriggerReason, newDecision.RetryInstruction, newPreview,
            ctxRecord.CompletedCount, newIter, ct);
        await db.SaveChangesAsync(ct);

        return PetraOrchestratorResult.Replanning(
            session.Id, ctxRecord.CurrentSubtaskId, newDecision.RetryInstruction, mergedTriggerReason);
    }

    /// <summary>Stage 81 場景 E：reject = 接受原 worker output / 繼續 chain dispatch 下個 subtask（不 cancel session / iter 不變 / v1.1 議題 1）。</summary>
    private async Task<PetraOrchestratorResult> ResumeReplanRejectAsync(
        PetraSession session, ReplanConfirmContext ctxRecord, CancellationToken ct)
    {
        // 不 increment iter — reject 不算 replan 輪數（議題 1 v1.1 紀律）
        await sessionRepo.AppendMessageAsync(session.Id, "user",
            $"[Stage 81 replan_reject] 不採納 Petra retry 建議 / 接受原 output 繼續 subtaskId={ctxRecord.CurrentSubtaskId}", ct: ct);
        await db.SaveChangesAsync(ct);

        return await ContinueChainFromSubtaskAsync(session, ctxRecord.CurrentSubtaskId + 1, null, ct);
    }

    /// <summary>Stage 81：從 plan_confirm ContextJson 還原 plan + talent picks → 從 startFromSubtaskId 起繼續 chain dispatch。
    /// 補強 #A 紀律：plan_confirm ContextJson 是 single source of truth — 不另存 plan 在 replan_confirm ContextJson 避重複。</summary>
    private async Task<PetraOrchestratorResult> ContinueChainFromSubtaskAsync(
        PetraSession session, int startFromSubtaskId, string? retryInstructionForFirst, CancellationToken ct)
    {
        var planInteraction = await db.BossInteractions
            .Where(x => x.InteractionType == "plan_confirm" && x.ContextJson != null
                        && x.ContextJson.Contains(session.Id.ToString()))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (planInteraction?.ContextJson is null)
            return PetraOrchestratorResult.Failure(session.Id, Array.Empty<string>(),
                "ContinueChainFromSubtaskAsync：找不到 plan_confirm ContextJson（補強 #A 紀律違反 — UseDynamicReplanning 應依賴 UseHITLPlanConfirmation）");

        PlanConfirmContext? planContext;
        try { planContext = JsonSerializer.Deserialize<PlanConfirmContext>(planInteraction.ContextJson, PlanConfirmJsonOptions); }
        catch (JsonException ex) { return PetraOrchestratorResult.Failure(session.Id, Array.Empty<string>(), $"ContextJson parse failed: {ex.Message}"); }
        if (planContext is null)
            return PetraOrchestratorResult.Failure(session.Id, Array.Empty<string>(), "plan_confirm ContextJson deserialize null");

        // session 從 paused 改 running
        var sessionRow = await db.PetraSessions.FirstOrDefaultAsync(x => x.Id == session.Id, ct);
        if (sessionRow is not null)
        {
            sessionRow.Status = "running";
            sessionRow.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        // 還原 SubtaskPlan + Talents
        var allSubtasks = planContext.Subtasks
            .Select(s => new Subtask(s.Id, s.Skill, s.Description, s.NeedsImageContext))
            .ToList();
        var allDeps = planContext.Dependencies
            .Select(d => new DependencyEdge(d.From, d.To,
                string.Equals(d.Type, "nested", StringComparison.OrdinalIgnoreCase) ? DependencyType.Nested : DependencyType.Sequential))
            .ToList();
        var fullPlan = new SubtaskPlan(allSubtasks, allDeps);

        var talentsList = await talentFactory.GetAllAsync(ct);
        var talentPicks = new List<ITalent>(allSubtasks.Count);
        foreach (var s in planContext.Subtasks)
        {
            var pick = talentsList.FirstOrDefault(t => string.Equals(t.Name, s.TalentName, StringComparison.OrdinalIgnoreCase))
                       ?? FindTalentForSkill(s.Skill, talentsList);
            if (pick is null)
                return PetraOrchestratorResult.Failure(session.Id, planContext.Subtasks.Select(x => x.Skill).ToList(),
                    $"ContinueChain：找不到 Talent={s.TalentName}/Skill={s.Skill}");
            talentPicks.Add(pick);
        }

        // 過濾剩餘 subtasks
        var remainingSubtasks = allSubtasks.Where(s => s.Id >= startFromSubtaskId).ToList();
        var ctx = BuildSessionContext(session.TaskGroupId) with { SessionId = session.Id };

        if (remainingSubtasks.Count == 0)
        {
            // 全部已完成 → 直接 finalize
            logger.LogInformation(
                "[Stage 81] ContinueChain 0 剩餘 subtask sessionId={SessionId} → finalize",
                session.Id);
            var prUrl = await FinalizeGitAsync(ctx, planContext.TaskInput,
                allSubtasks.Select(s => s.SkillName).ToList(),
                planContext.TalentNames, ct);
            await sessionRepo.CompleteAsync(session.Id, ct);
            await db.SaveChangesAsync(ct);
            return PetraOrchestratorResult.Done(session.Id,
                allSubtasks.Select(s => s.SkillName).ToList(),
                $"Stage 81 ContinueChain：0 剩餘 subtask / 完成 chain" + (prUrl is null ? "。" : $" + PR {prUrl}。"));
        }

        // 還原 summaries from PetraSessionMessages tool rows
        var existingSummaries = await BuildSummariesFromSessionMessagesAsync(session.Id, ct);

        // 簡化 sequential dispatch — 重 dispatch remaining subtasks（不走 level grouping / 對齊 unit test 簡化 + Trial_v25 production 真實驗）
        return await DispatchRemainingSubtasksAsync(
            ctx, planContext.TaskInput, fullPlan, talentPicks, existingSummaries,
            remainingSubtasks, retryInstructionForFirst, planContext.TalentNames, ct);
    }

    /// <summary>Stage 81：simplified sequential dispatch for remaining subtasks（ContinueChain 用 / 不走 level grouping）。
    /// 每個 subtask 後做 UpdateSessionCostUsdAsync + cap check + trigger detect（與 DispatchTalentsAsync 對齊紀律）。</summary>
    private async Task<PetraOrchestratorResult> DispatchRemainingSubtasksAsync(
        PetraSessionContext ctx,
        string taskInput,
        SubtaskPlan fullPlan,
        IReadOnlyList<ITalent> talentPicks,
        List<WorkerDispatchSummary> summaries,
        IReadOnlyList<Subtask> remainingSubtasks,
        string? retryInstructionForFirst,
        IReadOnlyList<string> dispatchNames,
        CancellationToken ct)
    {
        var idToIndex = new Dictionary<int, int>(fullPlan.Subtasks.Count);
        for (var i = 0; i < fullPlan.Subtasks.Count; i++) idToIndex[fullPlan.Subtasks[i].Id] = i;

        try
        {
            for (var k = 0; k < remainingSubtasks.Count; k++)
            {
                var subtask = remainingSubtasks[k];
                var i = idToIndex[subtask.Id];
                var talent = talentPicks[i];
                var talentName = talent.Name;

                var inputMessages = new List<ChatMessage>();
                if (k == 0 && !string.IsNullOrWhiteSpace(retryInstructionForFirst))
                {
                    inputMessages.Add(new ChatMessage(ChatRole.System,
                        $"[Stage 81 RETRY INSTRUCTION] {retryInstructionForFirst}"));
                }
                inputMessages.Add(new ChatMessage(ChatRole.User, taskInput));
                foreach (var s in summaries)
                {
                    inputMessages.Add(new ChatMessage(ChatRole.Assistant,
                        $"[前一個 worker：{s.WorkerName}（capability={s.Capability}）已完成]\n\n{s.Output}"));
                }

                logger.LogInformation(
                    "[Stage 81] ContinueChain dispatch subtaskId={SubtaskId} talent={Talent} skill={Skill} retryInstruction={HasRetry} sessionId={SessionId}",
                    subtask.Id, talentName, subtask.SkillName, !string.IsNullOrEmpty(retryInstructionForFirst) && k == 0, ctx.SessionId);

                var agent = talent.CreateAgent(ctx, subtask.SkillName);
                var response = await agent.RunAsync(inputMessages, session: null, options: null, ct);
                var outputText = response.Text ?? "";

                var toolCallId = Guid.NewGuid().ToString("N");
                var toolMessage = BuildToolMessage(talentName, subtask.SkillName, outputText);
                await sessionRepo.AppendMessageAsync(ctx.SessionId, "tool", toolMessage, toolCallId, ct);
                await db.SaveChangesAsync(ct);
                summaries.Add(new WorkerDispatchSummary(talentName, subtask.SkillName, outputText, toolCallId));

                // cap check + trigger evaluate（與 DispatchTalentsAsync 對齊紀律）
                var signal = await CheckReplanTriggerAfterDispatchAsync(
                    ctx.SessionId, taskInput, subtask, talentName, outputText, summaries, ctx, ct);
                if (signal is not null)
                {
                    return await HandleReplanSignalAsync(ctx.SessionId, taskInput, signal, ct);
                }
            }

            // 全部完成 → finalize
            var decidedCapabilities = fullPlan.Subtasks.Select(s => s.SkillName).ToList();
            var prUrl = await FinalizeGitAsync(ctx, taskInput, decidedCapabilities, dispatchNames, ct);
            await sessionRepo.CompleteAsync(ctx.SessionId, ct);
            await db.SaveChangesAsync(ct);

            var summary = $"Petra 完成 {dispatchNames.Count} dispatch（{string.Join(" → ", dispatchNames)}）"
                + (prUrl is null ? "。" : $" + PR {prUrl}。");
            return PetraOrchestratorResult.Done(ctx.SessionId, decidedCapabilities, summary);
        }
        catch (OperationCanceledException)
        {
            await sessionRepo.EscalateAsync(ctx.SessionId, CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Stage 81] DispatchRemainingSubtasksAsync 失敗 sessionId={SessionId}", ctx.SessionId);
            await sessionRepo.EscalateAsync(ctx.SessionId, CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
            return PetraOrchestratorResult.Failure(ctx.SessionId, Array.Empty<string>(), ex.Message);
        }
    }

    /// <summary>Stage 81：從 PetraSessionMessages tool role rows 還原 WorkerDispatchSummary list（Resume path 鋪 chain context）。
    /// 解析 BuildToolMessage 既有 format `[{worker}|{capability}|outputLen={N}]\n{text}`。</summary>
    private async Task<List<WorkerDispatchSummary>> BuildSummariesFromSessionMessagesAsync(
        Guid sessionId, CancellationToken ct)
    {
        var toolMessages = await db.PetraSessionMessages
            .Where(m => m.SessionId == sessionId && m.Role == "tool")
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var summaries = new List<WorkerDispatchSummary>(toolMessages.Count);
        foreach (var m in toolMessages)
        {
            // parse `[worker|capability|outputLen=N]\ntext`
            var content = m.Content ?? "";
            var firstNl = content.IndexOf('\n');
            if (firstNl <= 0) continue;
            var header = content[..firstNl];
            var body = content[(firstNl + 1)..];
            if (!header.StartsWith('[') || !header.EndsWith(']')) continue;
            var inner = header[1..^1];
            var parts = inner.Split('|');
            if (parts.Length < 2) continue;
            var workerName = parts[0];
            var capability = parts[1];
            summaries.Add(new WorkerDispatchSummary(workerName, capability, body, m.ToolCallId ?? ""));
        }
        return summaries;
    }
}
