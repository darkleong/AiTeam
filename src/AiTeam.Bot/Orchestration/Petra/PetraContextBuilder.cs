using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Data.SeedContent;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 84：PetraOrchestratorService 跨子 service 共用的 context / prompt / memory / resume 構造邏輯（對齊 refactor-sop.md SOP 3 Commons）。
///
/// method 範圍：
/// - <see cref="BuildSessionContext"/>：session-startup helper（CloneOrPull wire）
/// - <see cref="BuildResumeInputAsync"/>：Resume path 重建 task input（含 responded BossInteraction prefix）
/// - <see cref="BuildSummariesFromSessionMessagesAsync"/>：從 tool role messages rebuild WorkerDispatchSummary list
/// - <see cref="BuildPetraSystemPromptForRuntimeAsync"/>：DB SkillPrompt 載入 + persona prepend
/// - <see cref="ResolvePetraPersonaAsync"/>：取 Petra TalentPrompt persona body
/// - <see cref="BuildPetraSystemPrompt"/>（static）：Petra prompt 動態組裝（capabilityRoster / decomposition / output section 注入）
/// - <see cref="BuildMemoryContext"/>（static）：拼 memory context 注入 system prompt
///
/// 跨 service 共用：
/// - TalentDispatch.DispatchTalentsAsync 內 ProcessSubtaskResult 用 BuildMemoryContext + DecideTalentsAsync 用 BuildPetraSystemPromptForRuntime
/// - DynamicReplan.ContinueChainFromSubtaskAsync 用 BuildSessionContext + BuildSummariesFromSessionMessagesAsync + InvokePetraReplanAsync 用 BuildPetraSystemPromptForRuntime
/// - PlanConfirmation.ResumeFromPlanConfirmationAsync 用 BuildResumeInputAsync
/// - 主入口 PetraOrchestratorService.StartAsync 用 BuildSessionContext + ResumeAsync 用 BuildResumeInputAsync
/// </summary>
public class PetraContextBuilder(
    PromptResolver promptResolver,
    AppDbContext db,
    GitHubService gitHubService,
    IConfiguration configuration,
    ILogger<PetraContextBuilder> logger)
{
    /// <summary>
    /// Stage 64 子項 4b（Aria 必修 2）：v5 PoC 漏接 CloneOrPull wire — 對齊 v4 DevAgentService.cs:138 既有主動 clone pattern。
    /// 既有 CloneOrPull 防護「dir 存在但無 .git → 清理後 clone」（GitHubService.cs:160-165）— wire 通了就 cover 空 / 缺 .git 兩維度。
    /// v5 PoC 採 single shared clone（uniqueSuffix=null → {WorkspacePath}/AiTeam）— 不走 v4 per-task subfolder。
    /// </summary>
    public PetraSessionContext BuildSessionContext(Guid? taskGroupId)
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

    public async Task<string> BuildResumeInputAsync(PetraSession session, CancellationToken ct)
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

    /// <summary>Stage 81：從 PetraSessionMessages tool role rows 還原 WorkerDispatchSummary list（Resume path 鋪 chain context）。
    /// 解析 BuildToolMessage 既有 format `[{worker}|{capability}|outputLen={N}]\n{text}`。</summary>
    internal async Task<List<WorkerDispatchSummary>> BuildSummariesFromSessionMessagesAsync(
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

    /// <summary>
    /// Stage 72 + Stage 73：v5.5 Phase 2/3 — runtime async wrapper for BuildPetraSystemPrompt（含 feature flag check + DB load）。
    ///
    /// Stage 72：flag=`Workflow:UseV5PromptDb`=true → 透過 PromptResolver 取 DB SkillPrompt `petra_orchestration` PromptBody 當 base template override。
    /// Stage 73：flag=true + Petra TalentPrompt 存在 → prepend persona body 上 base template；
    ///          不存在或 flag=false → 純 base template（backwards-compatible 守護 0 regression）。
    /// </summary>
    public async Task<string> BuildPetraSystemPromptForRuntimeAsync(
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
    public async Task<string?> ResolvePetraPersonaAsync(CancellationToken ct)
    {
        var petra = await db.Talents
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ProjectId == null && t.Name == "Petra", ct);
        if (petra is null) return null;

        return await promptResolver.ResolveTalentPersonaAsync(petra.Id, ct);
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
    internal static string BuildPetraSystemPrompt(
        string capabilityRoster,
        bool useSubtaskPlanning = false,
        string? baseTemplateOverride = null)
    {
        var decompositionSection = useSubtaskPlanning
            ? """

【Hierarchical Decomposition + Dependency Graph 紀律】(Stage 70：v5.5 Phase 2 Step 4)

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

【判斷每個 subtask 是否需要附圖 context】(Stage 79：v5.5 image flow 補完)

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

【需求拆解紀律】(Stage 67：合 requirements_extraction 進來)

接到任務時先用內部 reasoning 拆需求 — 不 dispatch worker 做 requirements_extraction。
判準：以模糊度 / 範圍 / 邊界三維度自我評估
  範例：「打磨 Dashboard 錯誤處理體驗」→ 內部拆「跨 5 範圍 + 中等改動 + UI 邊界」→ 命中 Design trigger
  範例：「Dashboard 加圖示」→ 內部拆「視覺 + 小改動」→ 命中 1-on-1 trigger
紀律：拆完直接決 capability 序列回（不要回「我先拆需求 → 再決定」這種兩步驟說法 / 不污染 capability 序列輸出格式）
""";

        var outputSection = useSubtaskPlanning
            ? """
【輸出紀律】(Stage 70：JSON SubtaskPlan)
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
    /// Stage 69：拼 memory context 注入 system prompt — 0 entries 兩層都空時 return string.Empty（caller skip inject）。
    /// </summary>
    internal static string BuildMemoryContext(IReadOnlyList<TaskMemory> taskMems, IReadOnlyList<TalentMemory> talentMems)
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
}
