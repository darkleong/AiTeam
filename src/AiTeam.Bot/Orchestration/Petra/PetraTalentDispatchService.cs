using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Data.SeedContent;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 84：v5.5 Talent dispatch core 拆出 PetraOrchestratorService（含 detection family healthy 偏離 Roadmap 子项 3 → 移到 TalentDispatch）。
///
/// dispatch core method（Roadmap子项 4）：
/// - DecideTalentsAsync / DecideTalentsWithPlanAsync
/// - DispatchTalentsAsync（含 per-subtask CheckReplanTrigger inline detection）
/// - BuildInputMessagesForSubtaskAsync / ProcessSubtaskResultAsync
/// - DispatchRemainingSubtasksAsync（含 CheckReplan 第 3 caller）
///
/// detection family（健康偏離 / 從 Roadmap 子项 3 搬入）：
/// - DetectReplanTrigger（internal static）
/// - CheckReplanTriggerAfterDispatchAsync
/// - InvokePetraReplanAsync
///
/// state：_roundRobinCounter（Scoped lifecycle / per-session）
/// 跨 service 呼叫：注入 PetraContextBuilder（BuildMemoryContext / BuildPetraSystemPromptForRuntime / BuildSummariesFromSessionMessages）
/// </summary>
public class PetraTalentDispatchService(
    ITalentFactory talentFactory,
    MemoryRepository memoryRepo,
    PetraSessionRepository sessionRepo,
    AppDbContext db,
    LlmProviderFactory providerFactory,
    TalentDispatchLockService talentLockService,
    WorkflowSettingsResolver workflowResolver,
    PetraContextBuilder contextBuilder,
    ILogger<PetraTalentDispatchService> logger)
{
    private const string PetraAgentName = "PM";

    // Stage 67：v5.5 path round-robin counter（Scoped — session 級無需 thread-safe）
    private int _roundRobinCounter;

    /// <summary>Stage 81：PlanConfirmContext 解析 JSON options（resume edit/respond 重 decide 用）。</summary>
    internal static readonly JsonSerializerOptions PlanConfirmJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Stage 66：拼後續 worker input — 原 task input + 前面 worker 已做的 capability + 結果摘要。</summary>
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

    /// <summary>Stage 66：產 tool role message content — worker dispatch 結果摘要寫入 PetraSessionMessages。</summary>
    private static string BuildToolMessage(string workerName, string capability, string output)
    {
        const int maxLen = 2000;
        var truncated = output.Length > maxLen ? output[..maxLen] + "...(truncated)" : output;
        return $"[{workerName}|{capability}|outputLen={output.Length}]\n{truncated}";
    }

    internal async Task<(List<string> Skills, List<ITalent> TalentPicks)> DecideTalentsAsync(
        string taskInput,
        IReadOnlyList<ITalent> talents,
        PetraSessionContext ctx,
        CancellationToken ct,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        // skill roster 從 talent.Skills 取（vs 既有 DecideAsync 取 tools.SelectMany(Capabilities)）— 對 LLM 等價
        var skillRoster = string.Join(", ", talents.SelectMany(t => t.Skills).Distinct(StringComparer.OrdinalIgnoreCase));
        var systemPrompt = await contextBuilder.BuildPetraSystemPromptForRuntimeAsync(skillRoster, useSubtaskPlanning: false, ct);

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
            var pick = PetraTalentLookupHelper.FindTalentForSkill(skill, talents, ref _roundRobinCounter);
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

    // Stage 84：FindTalentForSkill 抽 PetraTalentLookupHelper static helper（解 TalentDispatch ↔ DynamicReplan 循環依賴）

    /// <summary>
    /// Stage 70：v5.5 Phase 2 Step 4 — Petra LLM 動態拆任務 + dependency graph + lookup Talent pool。
    /// Prompt 含 hierarchical decomposition + few-shot 範例 — LLM 回 JSON SubtaskPlan。
    /// 容錯紀律：JSON 解析失敗 fallback 為 Linear[code_implementation] 單一 subtask（0-crash 保 dispatch 不中斷）。
    /// </summary>
    internal async Task<(SubtaskPlan Plan, List<ITalent> TalentPicks)> DecideTalentsWithPlanAsync(
        string taskInput,
        IReadOnlyList<ITalent> talents,
        PetraSessionContext ctx,
        CancellationToken ct,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        var skillRoster = string.Join(", ", talents.SelectMany(t => t.Skills).Distinct(StringComparer.OrdinalIgnoreCase));
        var systemPrompt = await contextBuilder.BuildPetraSystemPromptForRuntimeAsync(skillRoster, useSubtaskPlanning: true, ct);

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
            var pick = PetraTalentLookupHelper.FindTalentForSkill(sub.SkillName, talents, ref _roundRobinCounter);
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
    internal async Task<DispatchOutcome> DispatchTalentsAsync(
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
    internal async Task<List<ChatMessage>> BuildInputMessagesForSubtaskAsync(
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
            var memoryContext = PetraContextBuilder.BuildMemoryContext(taskMems, talentMems);
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
    internal async Task ProcessSubtaskResultAsync(
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

    // Stage 84：BuildMemoryContext 拆出 PetraContextBuilder.cs（跨 service 共用紀律 / SOP 3）

    /// <summary>
    /// Stage 64 子項 2：Workers 完成後 git commit/push/PR 接通。
    /// 範圍邊界：最小整合 — 不重做 v4 dev_plan / fix_loop / metadata 機制（留 Stage 65+）。
    /// 無 diff → 不誤建 PR；非 git repo → 捕例外 log warning 不擋流程。
    /// </summary>
    // Stage 84：FinalizeGitAsync + BuildPrBody 拆出 PetraGitFinalizationService.cs（最獨立 / 0 跨 service）

    // ========== Stage 80：HITL plan_confirm 閘門 ==========

    /// <summary>Stage 80：BossInteraction.ContextJson 用 SubtaskPlan + talent picks 序列化結構（plan_confirm 卡 / Resume 還原）。
    /// 純內部結構 — InteractionCard.razor render 端讀同 JSON / 4 decision pattern routing 用。</summary>
    // Stage 84：PlanConfirmContext / PlanConfirmSubtask / PlanConfirmDependency 拆出 PetraOrchestratorDtos.cs
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
    internal async Task<ReplanSignal?> CheckReplanTriggerAfterDispatchAsync(
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
    internal async Task<ReplanDecisionJson?> InvokePetraReplanAsync(
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

    /// <summary>Stage 81+84：dispatch remaining subtasks（ContinueChain 用 / 不走 level grouping）。
    /// Stage 84：return type 改 DispatchOutcome（pure refactor — 不再 inline call HandleReplanSignalAsync + FinalizeGit
    /// → 解 TalentDispatch ↔ DynamicReplan 循環 / caller 負責 signal handling + finalize / 對齊 DispatchTalentsAsync 紀律）。</summary>
    internal async Task<DispatchOutcome> DispatchRemainingSubtasksAsync(
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
                    // Stage 84：return signal 讓 caller（ContinueChainFromSubtaskAsync）handle / 解循環
                    return new DispatchOutcome(summaries, signal);
                }
            }

            // 全部完成 → return DispatchOutcome 無 signal / caller 負責 FinalizeGit + sessionRepo.Complete
            return new DispatchOutcome(summaries, null);
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
            // Stage 84：rethrow / caller（ContinueChainFromSubtaskAsync）負責 Result.Failure 包裝
            throw;
        }
    }
}
