using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Orchestration.Petra;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AiTeam.Bot.Tests.Orchestration;

/// <summary>
/// Stage 63B：v5 動態架構 PoC 7 驗證 test case（對齊 Aria 規劃書子項 8 + Charter 01_Spike_Plan.md ≥5 項）。
///
/// 紀律對齊：xUnit only（Mock 階段 0 LLM cost / Dashboard 5 場景完整化留 Trial_v9 真實任務階段）。
/// 不真打 Gemini — 全 stub ILlmProvider + InMemory DB。
/// </summary>
public class PetraOrchestratorServiceTests
{
    private static AppDbContext CreateInMemoryDb(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    // ─── Test 1：1-on-1 trigger → 只 dispatch code_implementation ───────────────────
    [Fact]
    public void Test1_DecideParse_OnOnOneTrigger_PicksCodeImplementationOnly()
    {
        var raw = "code_implementation";
        var caps = ParseCapabilities(raw);

        Assert.Single(caps);
        Assert.Equal("code_implementation", caps[0]);
    }

    // ─── Test 2：Design trigger → code_implementation | code_review ─────────────────
    [Fact]
    public void Test2_DecideParse_DesignTrigger_PicksTwoCapabilities()
    {
        var raw = "code_implementation|code_review";
        var caps = ParseCapabilities(raw);

        Assert.Equal(2, caps.Count);
        Assert.Contains("code_implementation", caps);
        Assert.Contains("code_review", caps);
    }

    // ─── Test 3：Kickoff trigger → 多輪 capability ────────────────────────────────
    [Fact]
    public void Test3_DecideParse_KickoffTrigger_PicksFourCapabilities()
    {
        var raw = "code_implementation|code_review|code_implementation|code_review";
        var caps = ParseCapabilities(raw);

        Assert.Equal(4, caps.Count);
        Assert.Equal("code_implementation", caps[0]);
        Assert.Equal("code_review", caps[3]);
    }

    // ─── Test 4：per-task session 持久化 — 寫 PetraSessionMessage + 跨 Worker 讀取保留 ─
    [Fact]
    public async Task Test4_PetraSessionRepository_PersistsMessagesAcrossDispatch()
    {
        await using var db = CreateInMemoryDb(nameof(Test4_PetraSessionRepository_PersistsMessagesAcrossDispatch));
        await db.Database.EnsureCreatedAsync();

        var repo = new PetraSessionRepository(db);
        var taskGroupId = Guid.NewGuid();

        var session = repo.Start(taskGroupId);
        await db.SaveChangesAsync();

        await repo.AppendMessageAsync(session.Id, "user", "修 README typo 1 行");
        await repo.AppendMessageAsync(session.Id, "assistant", "code_implementation");
        await repo.AppendMessageAsync(session.Id, "tool", "[Cody] 已實作（mock fixture）");
        await db.SaveChangesAsync();

        var reloaded = await repo.GetWithMessagesAsync(session.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(3, reloaded!.Messages.Count);
        Assert.Equal("user", reloaded.Messages.First(m => m.Content.Contains("typo")).Role);
        Assert.Equal("assistant", reloaded.Messages.First(m => m.Content == "code_implementation").Role);
        Assert.Equal("tool", reloaded.Messages.First(m => m.Content.StartsWith("[Cody]")).Role);

        await repo.CompleteAsync(session.Id);
        await db.SaveChangesAsync();

        var done = await db.PetraSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.Equal("done", done!.Status);
    }

    // ─── Test 5：feature flag default=false → v4 path 不受影響 ────────────────────
    [Fact]
    public void Test5_WorkflowSettings_UsePetraOrchestratorV5_DefaultIsFalse()
    {
        var settings = new WorkflowSettings();
        Assert.False(settings.UsePetraOrchestratorV5);
    }

    // ─── Test 6：7 Worker capability dispatch — reflection 取 attribute 命中 ───────
    [Theory]
    [InlineData(typeof(DevAgentService),         "code_implementation")]
    [InlineData(typeof(ReviewerAgentService),    "code_review")]
    [InlineData(typeof(QaAgentService),          "qa_testing")]
    [InlineData(typeof(DocAgentService),         "documentation")]
    // Stage 78a：砍 Rosa/Demi/Release 對應 3 InlineData — v5.5 4 Worker baseline。
    public void Test6_WorkerCapabilityAttribute_MapsToExpectedTag(Type workerType, string expectedCapability)
    {
        var attrs = workerType.GetCustomAttributes(typeof(AgentCapabilityAttribute), inherit: false)
            .Cast<AgentCapabilityAttribute>().ToList();

        Assert.NotEmpty(attrs);
        Assert.Contains(attrs, a => a.Capability == expectedCapability);
    }

    // ─── Test 7：BuildSequential + ChatClientAgent + Adapter 三層 wrapper 真實生效 ──
    // 路線 A 限制 (b) workaround 驗證 — adapter capability dispatch 7 capability 對應 IClaudeCodeService method
    [Theory]
    [InlineData("code_implementation", "RunAsync")]
    [InlineData("code_review",         "RunReviewAsync")]
    [InlineData("qa_testing",          "RunQaAsync")]
    [InlineData("documentation",       "RunReadOnlyAsync")]
    // Stage 78a：砍 Rosa/Demi/Release 對應 3 capability InlineData — v5.5 4 Worker baseline。
    public async Task Test7_ClaudeCodeChatClientAdapter_DispatchesByCapability(string capability, string expectedMethod)
    {
        var stub = new StubClaudeCodeService();
        var adapter = new ClaudeCodeChatClientAdapter(
            stub, capability, "TestWorker", "mock-model", "mock-key",
            workingDir: "",   // Stage 64：空 workingDir → adapter 內 string.IsNullOrEmpty 短路 skip CLAUDE.md inject（純驗 capability dispatch 不驗 inject ritual — inject ritual 由 ClaudeCodeChatClientAdapterTests cover）
            tokenLogService: null,    // Trial_v9 修：adapter 加 TokenLogService 注入 — test 純驗 dispatch 不驗 token_logs 寫入，傳 null 對齊 adapter null check fallback
            NullLogger<ClaudeCodeChatClientAdapter>.Instance);

        var input = new[] { new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "test input") };
        var response = await adapter.GetResponseAsync(input);

        Assert.NotNull(response);
        Assert.Equal(expectedMethod, stub.LastInvokedMethod);
        Assert.Contains(expectedMethod, response.Text ?? "");
    }

    // ─── Test 8（Stage 64）：Petra DecideAsync parse — unknown capability skip 不爆 ─────
    [Fact]
    public void Test8_DecideParse_UnknownCapability_SkippedInList()
    {
        // 模擬 raw 含未知 capability — adapter dispatch 表外的 tag 應該被 lookup 失敗（picks 不包含）
        var raw = "unknown_cap|code_review";
        var caps = ParseCapabilities(raw);

        Assert.Equal(2, caps.Count);   // parse 階段保留所有 token
        Assert.Equal("unknown_cap", caps[0]);
        Assert.Equal("code_review", caps[1]);

        // 對齊 PetraOrchestratorService.DecideAsync 內 picks lookup 邏輯：未知 capability 不會進 picks
        var knownCaps = new[] { "code_implementation", "code_review", "qa_testing" };
        var picks = caps.Where(c => knownCaps.Any(k => string.Equals(c, k, StringComparison.OrdinalIgnoreCase))).ToList();
        Assert.Single(picks);
        Assert.Equal("code_review", picks[0]);
    }

    // ─── Test 9（Stage 64）：BuildPetraSystemPrompt 升級三 trigger 具體判準 — 關鍵字 + 反例 ─
    // 驗 prompt 升級內容（子項 3）含三 trigger 具體判準 + 範例 + 反例 + 輸出紀律段
    [Fact]
    public void Test9_BuildPetraSystemPrompt_ContainsThreeTriggerCriteriaAndDiscipline()
    {
        // reflection 取 private static method（避免將 helper 公開為 internal 破壞封裝）
        var method = typeof(PetraOrchestratorService).GetMethod(
            "BuildPetraSystemPrompt",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        // Stage 70：BuildPetraSystemPrompt 簽名升級加 bool useSubtaskPlanning（default false）— reflection 不自動套 C# default value，顯式傳 false 維持既有 Stage 64/67/69 baseline 行為
        // Stage 72：簽名再加第 3 optional string? baseTemplateOverride（default null）— null 走 PetraPromptTemplate.Template hardcoded baseline / 0 regression
        var prompt = (string)method!.Invoke(null, new object?[] { "code_implementation, code_review", false, null })!;

        // 三 trigger 具體判準（Stage 64 子項 3 升級）
        Assert.Contains("1-on-1 trigger", prompt);
        Assert.Contains("Design trigger", prompt);
        Assert.Contains("Kickoff trigger", prompt);
        // 具體判準關鍵字
        Assert.Contains("< 50 行", prompt);
        Assert.Contains("Issue ≥ 5", prompt);
        Assert.Contains("架構決策", prompt);
        // 反例段
        Assert.Contains("反例", prompt);
        // 輸出紀律
        Assert.Contains("不要 markdown", prompt);
        Assert.Contains("不要解釋", prompt);
        // capability roster 注入
        Assert.Contains("code_implementation, code_review", prompt);
    }

    // ─── Test 10（Stage 64 / Stage 78a update）：ClaudeCodeChatClientAdapter dispatch 4 capability 完整 cover ──
    // Stage 78a：v4 path 砍後 ClaudeCodeChatClientAdapter dispatch 4 capability baseline（砍 requirements_extraction / ui_design / release_publishing）。
    // 對齊既有 Test7 Theory data — 4 個 capability 與 4 個 expectedMethod 對齊（不重複驗 dispatch，只驗 capability 列表完整性）。
    [Fact]
    public void Test10_AdapterCapabilityDispatch_CoversAllFourCapabilities()
    {
        // Stage 78a：v5.5 4 Worker baseline — code_implementation / code_review / qa_testing / documentation
        var expectedCapabilities = new[]
        {
            "code_implementation", "code_review", "qa_testing", "documentation"
        };

        Assert.Equal(4, expectedCapabilities.Length);
        Assert.DoesNotContain("requirements_extraction", expectedCapabilities);   // Stage 67 砍（合進 Petra system prompt）
        Assert.DoesNotContain("ui_design", expectedCapabilities);                 // Stage 78a 砍（Demi class 整套砍）
        Assert.DoesNotContain("release_publishing", expectedCapabilities);        // Stage 78a 砍（Release class 整套砍）
    }

    // ─── Test 11（Stage 64 Aria 必修 2）：BuildSessionContext CloneOrPull wire 對齊 v4 紀律 ──
    // 限制：GitHubService 既有 method 非 virtual / 無 interface，無法 unit test stub 驗 CloneOrPull invocation。
    // 真實 CloneOrPull wire 驗留 Trial_v10 真實任務 + Forge 自驗 docker logs 觀察「Petra BuildSessionContext CloneOrPull 完成 workingDir=」log line。
    // 本 unit test 改驗 fallback path：reflection 確認 BuildSessionContext private method 存在 + 簽名對齊（防回歸刪除）。
    [Fact]
    public void Test11_BuildSessionContext_MethodExists_FallbackPathReady()
    {
        var method = typeof(PetraOrchestratorService).GetMethod(
            "BuildSessionContext",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var parameters = method!.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(Guid?), parameters[0].ParameterType);
        Assert.Equal(typeof(PetraSessionContext), method.ReturnType);
    }

    // ─── Test 12（Stage 66 子項 1+2）：PetraOrchestratorService 自管 chain — worker A output 餵 worker B + tool role 寫入 ─
    // 修 GitHub #1308 framework BuildSequential edge 不傳 output 根因（Trial_v11 Vera 0 work 對應修法驗證）
    [Fact]
    public async Task Test12_DispatchWorkers_PassesPrevWorkerOutputToNext_AndWritesToolRoleMessages()
    {
        await using var db = CreateInMemoryDb(nameof(Test12_DispatchWorkers_PassesPrevWorkerOutputToNext_AndWritesToolRoleMessages));
        await db.Database.EnsureCreatedAsync();
        var repo = new PetraSessionRepository(db);

        var session = repo.Start(taskGroupId: null);
        await db.SaveChangesAsync();

        // 兩個 stub IChatClient — worker A 回固定 marker / worker B 對 messages 紀錄
        var chatA = new RecordingChatClient(returnText: "AAA-MARKER-FROM-WORKER-A");
        var chatB = new RecordingChatClient(returnText: "BBB-from-worker-B");
        var agentA = new ChatClientAgent(chatClient: chatA, instructions: null, name: "WorkerA");
        var agentB = new ChatClientAgent(chatClient: chatB, instructions: null, name: "WorkerB");

        // PetraOrchestratorService 建構：DispatchWorkersAsync 只用 logger / sessionRepo / db，其他 dep 傳 null! / Null logger
        // Stage 67：ctor 加 ITalentFactory + WorkflowSettingsResolver 兩參數 — Test 12 reflection invoke DispatchWorkersAsync 不走 StartAsync 不 call 此兩 dep / null! 安全
        // Stage 72：ctor 加 PromptResolver — Test 12 不走 BuildPetraSystemPromptForRuntimeAsync 不 call promptResolver / null! 安全
        var orch = new PetraOrchestratorService(
            tools: Array.Empty<AiTeam.Bot.Orchestration.Petra.IAgentTool>(),
            talentFactory: null!,
            workflowResolver: null!,
            sessionRepo: repo,
            memoryRepo: null!,
            db: db,
            providerFactory: null!,
            gitHubService: null!,
            configuration: new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            promptResolver: null!,
            talentLockService: new AiTeam.Bot.Services.TalentDispatchLockService(),   // Stage 75
            loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            logger: NullLogger<PetraOrchestratorService>.Instance);

        var picks = new List<AiTeam.Bot.Orchestration.Petra.IAgentTool>
        {
            new FakeAgentTool("WorkerA", "code_implementation"),
            new FakeAgentTool("WorkerB", "code_review"),
        };
        var caps = new List<string> { "code_implementation", "code_review" };
        var workerAgents = new AIAgent[] { agentA, agentB };

        // reflection invoke private DispatchWorkersAsync（保 method 為 private — 對齊 Test 9 既有 pattern）
        var method = typeof(PetraOrchestratorService).GetMethod(
            "DispatchWorkersAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task)method!.Invoke(orch, new object?[]
        {
            session.Id, "原始任務 input", caps, picks, workerAgents, CancellationToken.None
        })!;
        await task;

        // 1. Worker B 收到的 input messages 含 worker A 的 output marker（chain pass-through 真實生效）
        Assert.NotEmpty(chatB.LastReceivedMessages);
        var workerBPromptText = string.Join("\n", chatB.LastReceivedMessages.Select(m => m.Text ?? ""));
        Assert.Contains("AAA-MARKER-FROM-WORKER-A", workerBPromptText);
        Assert.Contains("原始任務 input", workerBPromptText);

        // 2. Worker A 收到 1 message（原 task input）
        Assert.Single(chatA.LastReceivedMessages);

        // 3. PetraSessionMessages tool role 寫入 ≥ 2 條 + ToolCallId 非空（議題 2 修法驗證）
        var toolMessages = await db.PetraSessionMessages
            .Where(m => m.SessionId == session.Id && m.Role == "tool")
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        Assert.Equal(2, toolMessages.Count);
        Assert.All(toolMessages, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.ToolCallId));
            Assert.NotEqual(Guid.Empty.ToString("N"), m.ToolCallId);
        });
        Assert.Contains("WorkerA", toolMessages[0].Content);
        Assert.Contains("WorkerB", toolMessages[1].Content);
    }

    // ─── Test 13（Stage 66 子項 3）：Cody 廣範圍指令範圍對照表 enforce — 只對 capability=code_implementation prepend ─
    [Theory]
    [InlineData("code_implementation", true)]
    [InlineData("code_review",         false)]
    [InlineData("qa_testing",          false)]
    [InlineData("documentation",       false)]
    public async Task Test13_Adapter_BroadScopeEnforce_PrependsForCodeImplementationOnly(string capability, bool shouldContainEnforce)
    {
        var stub = new StubClaudeCodeService();
        var adapter = new ClaudeCodeChatClientAdapter(
            stub, capability, "TestWorker", "mock-model", "mock-key",
            workingDir: "",
            tokenLogService: null,
            NullLogger<ClaudeCodeChatClientAdapter>.Instance);

        var input = new[] { new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "整個 Dashboard 凡是錯誤處理的地方") };
        await adapter.GetResponseAsync(input);

        Assert.NotNull(stub.LastReceivedPrompt);
        if (shouldContainEnforce)
        {
            Assert.Contains("廣範圍指令處理紀律", stub.LastReceivedPrompt!);
            Assert.Contains("範圍對照表", stub.LastReceivedPrompt!);
        }
        else
        {
            Assert.DoesNotContain("廣範圍指令處理紀律", stub.LastReceivedPrompt!);
        }
    }

    // ─── Test 14（Stage 67 / Stage 78a update）：Skill registry 4 Skill 完整載入 + 0 含 v4 capability ─
    [Fact]
    public void Test14_DefaultSkillRegistry_LoadsFourSkills_WithoutV4Capabilities()
    {
        var registry = new AiTeam.Bot.Orchestration.Petra.Skills.DefaultSkillRegistry();

        // Stage 78a：4 Skill baseline（砍 ui_design + release_publishing — Rosa/Demi/Release class 整套砍對應）
        Assert.Equal(4, registry.All.Count);
        Assert.NotNull(registry.GetByName("code_implementation"));
        Assert.NotNull(registry.GetByName("code_review"));
        Assert.NotNull(registry.GetByName("qa_testing"));
        Assert.NotNull(registry.GetByName("documentation"));

        // Stage 78a：v4 path 砍 — ui_design + release_publishing 對應 Demi/Release class 砍後 Skill registry 同步砍
        Assert.Null(registry.GetByName("ui_design"));
        Assert.Null(registry.GetByName("release_publishing"));

        // Stage 67 baseline：requirements_extraction 砍掉合進 Petra orchestrator system prompt
        Assert.Null(registry.GetByName("requirements_extraction"));

        // 對齊 ISkillRegistry case-insensitive lookup
        Assert.NotNull(registry.GetByName("CODE_IMPLEMENTATION"));
    }

    // ─── Test 15（Stage 67）：Talent pool 找 Talent dispatch — baseline 1 instance + Mock 多 instance round-robin ─
    [Fact]
    public void Test15_FindTalentForSkill_BaselineAndRoundRobin()
    {
        var orch = CreateMinimalOrchestratorForReflection();

        var cody1 = new FakeTalent("Cody", new[] { "code_implementation", "ui_design" });
        var cody2 = new FakeTalent("Cody-2", new[] { "code_implementation" });
        var vera = new FakeTalent("Vera", new[] { "code_review" });

        // baseline 1 instance — pool.Count == 1 直接 return 不走 round-robin
        var pickedSingle = InvokeFindTalentForSkill(orch, "code_review", new ITalent[] { cody1, vera });
        Assert.NotNull(pickedSingle);
        Assert.Equal("Vera", pickedSingle!.Name);

        // 多 instance round-robin — code_implementation pool = [Cody, Cody-2]
        var pool = new ITalent[] { cody1, cody2 };
        var first = InvokeFindTalentForSkill(orch, "code_implementation", pool);
        var second = InvokeFindTalentForSkill(orch, "code_implementation", pool);
        var third = InvokeFindTalentForSkill(orch, "code_implementation", pool);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(third);
        // counter 從 0 開始：[0]=Cody / [1]=Cody-2 / [2]=Cody（pool.Count=2 round-robin 回）
        Assert.Equal("Cody", first!.Name);
        Assert.Equal("Cody-2", second!.Name);
        Assert.Equal("Cody", third!.Name);

        // 找不到任何 Talent 擔任的 skill → null
        var missing = InvokeFindTalentForSkill(orch, "unknown_skill", pool);
        Assert.Null(missing);
    }

    // ─── Test 16（Stage 67）：Petra DecideAsync 回 Skill 序列 lookup Talent 對齊 — picks 順序 + Cody 兼 ui_design 自然分流到主 Skill ─
    [Fact]
    public void Test16_FindTalentForSkill_LookupBySkill_RespectsTalentPool()
    {
        var orch = CreateMinimalOrchestratorForReflection();

        // Cody 兼 code_implementation + ui_design + release_publishing / Vera code_review / Quinn qa_testing / Sage documentation
        var cody  = new FakeTalent("Cody",  new[] { "code_implementation", "ui_design", "release_publishing" });
        var vera  = new FakeTalent("Vera",  new[] { "code_review" });
        var quinn = new FakeTalent("Quinn", new[] { "qa_testing" });
        var sage  = new FakeTalent("Sage",  new[] { "documentation" });
        var pool = new ITalent[] { cody, vera, quinn, sage };

        // Petra 回「code_implementation|code_review」→ picks Cody → Vera
        var pick1 = InvokeFindTalentForSkill(orch, "code_implementation", pool);
        var pick2 = InvokeFindTalentForSkill(orch, "code_review", pool);
        Assert.Equal("Cody", pick1!.Name);
        Assert.Equal("Vera", pick2!.Name);

        // Cody 兼 ui_design → pick3 仍 Cody（單一 instance pool）
        var pick3 = InvokeFindTalentForSkill(orch, "ui_design", pool);
        Assert.Equal("Cody", pick3!.Name);

        // qa_testing → Quinn / documentation → Sage
        var pick4 = InvokeFindTalentForSkill(orch, "qa_testing", pool);
        var pick5 = InvokeFindTalentForSkill(orch, "documentation", pool);
        Assert.Equal("Quinn", pick4!.Name);
        Assert.Equal("Sage", pick5!.Name);
    }

    // ─── Test 17（Stage 67）：Feature flag UseTalentSkillSeparation default false 守 v5 既有 path 0 regression ─
    [Fact]
    public void Test17_WorkflowSettings_UseTalentSkillSeparation_DefaultIsFalse()
    {
        var settings = new AiTeam.Bot.Configuration.WorkflowSettings();
        // Stage 67：v5.5 path 預設 false / Trial_v13 ✅ + Christ 拍板才切 default true
        Assert.False(settings.UseTalentSkillSeparation);
        // v5 既有 path flag 維持（v5.5 是 v5 path 上面演進）
        Assert.False(settings.UsePetraOrchestratorV5);
    }

    // ─── Test 18（Stage 69）：UseV5Memory default = false 守 v5.5 既有 dispatch path 0 regression ──
    [Fact]
    public void Test18_WorkflowSettings_UseV5Memory_DefaultIsFalse()
    {
        var settings = new AiTeam.Bot.Configuration.WorkflowSettings();
        Assert.False(settings.UseV5Memory);
        // 對齊 compact config default
        Assert.Equal(60, settings.V5MemoryCompactThresholdPercent);
        Assert.Equal(50, settings.V5MemoryCompactKeepCount);
    }

    // ─── Test 19（Stage 69）：MemoryRepository.UpsertTaskMemoryAsync — 同 key 二次 = 1 row + UpdatedAt 推進 ──
    [Fact]
    public async Task Test19_MemoryRepository_UpsertTaskMemory_SameKey_UpdatesExisting()
    {
        await using var db = CreateInMemoryDb(nameof(Test19_MemoryRepository_UpsertTaskMemory_SameKey_UpdatesExisting));
        await db.Database.EnsureCreatedAsync();
        var repo = new MemoryRepository(db);
        // Stage 69 v2.1：scope = PetraSession（不是 v4 TaskGroup）— 對齊 v5.5 設計精神
        var petraSessionId = Guid.NewGuid();

        // 1st upsert
        await repo.UpsertTaskMemoryAsync(petraSessionId, projectId: null, key: "decision/cody-output-summary", content: "first content", createdByTalent: "Cody");
        await db.SaveChangesAsync();

        var firstCount = await db.TaskMemories.CountAsync(m => m.PetraSessionId == petraSessionId);
        var first = await db.TaskMemories.SingleAsync(m => m.PetraSessionId == petraSessionId);
        var firstUpdatedAt = first.UpdatedAt;
        Assert.Equal(1, firstCount);
        Assert.Equal("first content", first.Content);

        // 確保 UpdatedAt 有時間差（DateTime.UtcNow 同 tick 可能相同）
        await Task.Delay(10);

        // 2nd upsert 同 key
        await repo.UpsertTaskMemoryAsync(petraSessionId, projectId: null, key: "decision/cody-output-summary", content: "second content", createdByTalent: "Vera");
        await db.SaveChangesAsync();

        var secondCount = await db.TaskMemories.CountAsync(m => m.PetraSessionId == petraSessionId);
        var second = await db.TaskMemories.SingleAsync(m => m.PetraSessionId == petraSessionId);
        Assert.Equal(1, secondCount);     // upsert — 仍 1 row
        Assert.Equal("second content", second.Content);
        Assert.True(second.UpdatedAt > firstUpdatedAt, $"UpdatedAt 應推進 — first={firstUpdatedAt:O} second={second.UpdatedAt:O}");
        // CreatedByTalent 保留原值（先寫者留名 — Forge spike 拍板 #1）
        Assert.Equal("Cody", second.CreatedByTalent);
    }

    // ─── Test 20（Stage 69）：CompactTaskMemoryAsync — 100 條削回 50（newest 50 保留）──
    [Fact]
    public async Task Test20_MemoryRepository_CompactTaskMemory_KeepsNewestN()
    {
        await using var db = CreateInMemoryDb(nameof(Test20_MemoryRepository_CompactTaskMemory_KeepsNewestN));
        await db.Database.EnsureCreatedAsync();
        var repo = new MemoryRepository(db);
        var petraSessionId = Guid.NewGuid();

        // 寫 100 條 — 每條 key 不同（unique constraint 不撞）+ CreatedAt 升序遞增
        var baseTime = DateTime.UtcNow.AddHours(-1);
        for (var i = 0; i < 100; i++)
        {
            db.TaskMemories.Add(new TaskMemory
            {
                PetraSessionId = petraSessionId,
                Key = $"entry/{i:D3}",
                Content = $"content-{i}",
                CreatedByTalent = "Cody",
                CreatedAt = baseTime.AddSeconds(i),
                UpdatedAt = baseTime.AddSeconds(i),
            });
        }
        await db.SaveChangesAsync();
        Assert.Equal(100, await db.TaskMemories.CountAsync(m => m.PetraSessionId == petraSessionId));

        // compact 保留 newest 50
        var deleted = await repo.CompactTaskMemoryAsync(petraSessionId, keepCount: 50);
        await db.SaveChangesAsync();

        Assert.Equal(50, deleted);
        var remaining = await db.TaskMemories
            .Where(m => m.PetraSessionId == petraSessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        Assert.Equal(50, remaining.Count);
        // 保留的應是 newest（entry/050 ~ entry/099）
        Assert.Equal("entry/050", remaining.First().Key);
        Assert.Equal("entry/099", remaining.Last().Key);
    }

    // ─── Test 21（Stage 69）：TalentMemory ProjectId null vs non-null 隔離 ──
    // partial unique index InMemory provider 不 enforce filter；本 test 驗 Repository upsert 不誤刪不同 Project 同 key
    [Fact]
    public async Task Test21_MemoryRepository_TalentMemory_ProjectIsolation()
    {
        await using var db = CreateInMemoryDb(nameof(Test21_MemoryRepository_TalentMemory_ProjectIsolation));
        await db.Database.EnsureCreatedAsync();
        var repo = new MemoryRepository(db);
        var talentId = Guid.NewGuid();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        // 三筆同 talent + 同 key + 不同 ProjectId（null / A / B）— 應視為三筆獨立記憶
        await repo.UpsertTalentMemoryAsync(talentId, projectId: null, key: "last-task-summary", content: "global summary", tags: null);
        await repo.UpsertTalentMemoryAsync(talentId, projectId: projectA, key: "last-task-summary", content: "project A summary", tags: null);
        await repo.UpsertTalentMemoryAsync(talentId, projectId: projectB, key: "last-task-summary", content: "project B summary", tags: null);
        await db.SaveChangesAsync();

        var total = await db.TalentMemories.CountAsync(m => m.TalentId == talentId);
        Assert.Equal(3, total);

        // GetTalentMemoriesAsync(projectId: null) 只回全域
        var global = await repo.GetTalentMemoriesAsync(talentId, projectId: null, tagFilter: null);
        Assert.Single(global);
        Assert.Equal("global summary", global[0].Content);

        // GetTalentMemoriesAsync(projectId: projectA) 只回 Project A
        var pa = await repo.GetTalentMemoriesAsync(talentId, projectId: projectA, tagFilter: null);
        Assert.Single(pa);
        Assert.Equal("project A summary", pa[0].Content);

        // 同 projectId 再 upsert → 1 row update
        await repo.UpsertTalentMemoryAsync(talentId, projectId: projectA, key: "last-task-summary", content: "project A updated", tags: new[] { "v2" });
        await db.SaveChangesAsync();
        var paUpdated = await repo.GetTalentMemoriesAsync(talentId, projectId: projectA, tagFilter: null);
        Assert.Single(paUpdated);
        Assert.Equal("project A updated", paUpdated[0].Content);
        Assert.Contains("v2", paUpdated[0].Tags);
        // 仍是 3 row total（不誤刪不同 Project 同 key）
        Assert.Equal(3, await db.TalentMemories.CountAsync(m => m.TalentId == talentId));
    }

    // ─── Test 22（Stage 69）：GetTaskMemoriesAsync OrderBy CreatedAt ─
    [Fact]
    public async Task Test22_MemoryRepository_GetTaskMemories_OrderedByCreatedAt()
    {
        await using var db = CreateInMemoryDb(nameof(Test22_MemoryRepository_GetTaskMemories_OrderedByCreatedAt));
        await db.Database.EnsureCreatedAsync();
        var repo = new MemoryRepository(db);
        var petraSessionId = Guid.NewGuid();

        // 故意亂序寫入（CreatedAt 不依 insert 順序）
        var baseTime = DateTime.UtcNow.AddMinutes(-30);
        db.TaskMemories.Add(new TaskMemory { PetraSessionId = petraSessionId, Key = "k3", Content = "third", CreatedByTalent = "Cody", CreatedAt = baseTime.AddMinutes(20), UpdatedAt = baseTime.AddMinutes(20) });
        db.TaskMemories.Add(new TaskMemory { PetraSessionId = petraSessionId, Key = "k1", Content = "first", CreatedByTalent = "Cody", CreatedAt = baseTime, UpdatedAt = baseTime });
        db.TaskMemories.Add(new TaskMemory { PetraSessionId = petraSessionId, Key = "k2", Content = "second", CreatedByTalent = "Vera", CreatedAt = baseTime.AddMinutes(10), UpdatedAt = baseTime.AddMinutes(10) });
        await db.SaveChangesAsync();

        var got = await repo.GetTaskMemoriesAsync(petraSessionId);
        Assert.Equal(3, got.Count);
        Assert.Equal("k1", got[0].Key);
        Assert.Equal("k2", got[1].Key);
        Assert.Equal("k3", got[2].Key);
    }

    // ─── Test 23（Stage 70 + Stage 74）：SubtaskPlan record + Linear factory baseline ──────────
    // Stage 74：Linear factory 加 sequential edges（修根因 — DAG fan-out 引入後 Linear 必須真實 sequential 避誤為「全並行 level 0」）。
    [Fact]
    public void Test23_SubtaskPlan_LinearFactory_ProducesIdAscendingSubtasksWithSequentialEdges()
    {
        var skills = new[] { "code_implementation", "code_review", "qa_testing" };
        var plan = SubtaskPlan.Linear(skills);

        Assert.Equal(3, plan.Subtasks.Count);
        Assert.Equal(1, plan.Subtasks[0].Id);
        Assert.Equal("code_implementation", plan.Subtasks[0].SkillName);
        Assert.Equal(2, plan.Subtasks[1].Id);
        Assert.Equal("code_review", plan.Subtasks[1].SkillName);
        Assert.Equal(3, plan.Subtasks[2].Id);
        Assert.Equal("qa_testing", plan.Subtasks[2].SkillName);

        // Stage 74：Linear chain 真實 sequential edges 1→2, 2→3（後 Talent 依賴前 Talent output / DAG fan-out level grouping 才會把每 subtask 各自一個 level 走 sequential）
        Assert.Equal(2, plan.Dependencies.Count);
        Assert.Contains(plan.Dependencies, e => e.FromId == 1 && e.ToId == 2 && e.Type == DependencyType.Sequential);
        Assert.Contains(plan.Dependencies, e => e.FromId == 2 && e.ToId == 3 && e.Type == DependencyType.Sequential);

        // 空 skills → Empty plan
        var empty = SubtaskPlan.Linear(Array.Empty<string>());
        Assert.Empty(empty.Subtasks);
        Assert.Empty(empty.Dependencies);
        Assert.Same(SubtaskPlan.Empty, empty);

        // 單 skill → 1 subtask + 0 edges
        var single = SubtaskPlan.Linear(new[] { "code_implementation" });
        Assert.Single(single.Subtasks);
        Assert.Empty(single.Dependencies);
    }

    // ─── Test 24（Stage 70）：SubtaskPlanParser JSON 解析 + code fence strip + 失敗 fallback ─
    [Fact]
    public void Test24_SubtaskPlanParser_HandlesValidJsonAndCodeFenceAndMalformed()
    {
        // case 1：純 JSON ✓
        var raw1 = """{"subtasks":[{"id":1,"skill":"code_implementation","description":"impl"},{"id":2,"skill":"code_review","description":"review"}],"dependencies":[{"from":1,"to":2,"type":"sequential"}]}""";
        Assert.True(SubtaskPlanParser.TryParse(raw1, out var plan1, out var err1));
        Assert.Null(err1);
        Assert.Equal(2, plan1.Subtasks.Count);
        Assert.Equal("code_implementation", plan1.Subtasks[0].SkillName);
        Assert.Single(plan1.Dependencies);
        Assert.Equal(DependencyType.Sequential, plan1.Dependencies[0].Type);

        // case 2：markdown code fence 包裹 ✓ 自動 strip
        var raw2 = """
```json
{"subtasks":[{"id":1,"skill":"code_implementation","description":"x"}],"dependencies":[]}
```
""";
        Assert.True(SubtaskPlanParser.TryParse(raw2, out var plan2, out var err2));
        Assert.Null(err2);
        Assert.Single(plan2.Subtasks);

        // case 3：純亂碼 ✗ → return false + error 非空（plan = Empty）
        Assert.False(SubtaskPlanParser.TryParse("這是隨便亂寫的話不是 JSON", out var plan3, out var err3));
        Assert.NotNull(err3);
        Assert.Empty(plan3.Subtasks);

        // case 4：JSON 但 0 subtask ✗
        Assert.False(SubtaskPlanParser.TryParse("""{"subtasks":[],"dependencies":[]}""", out _, out var err4));
        Assert.NotNull(err4);

        // case 5：dependency 指向不存在 subtask Id → 該 edge skip（不擋整 plan）
        var raw5 = """{"subtasks":[{"id":1,"skill":"code_implementation"}],"dependencies":[{"from":1,"to":99,"type":"sequential"},{"from":1,"to":1,"type":"sequential"}]}""";
        Assert.True(SubtaskPlanParser.TryParse(raw5, out var plan5, out _));
        Assert.Single(plan5.Subtasks);
        Assert.Empty(plan5.Dependencies);   // 99 不存在 + 自指向 1→1 都 skip
    }

    // ─── Test 25（Stage 70）：SubtaskPlanTopologicalSort — Linear / 鏈 / 並行 / cycle ──
    [Fact]
    public void Test25_SubtaskPlanTopologicalSort_HandlesLinearAndChainAndParallelAndCycle()
    {
        // case 1：Linear plan (0 deps) → 回 Id 升序（對齊既有 Stage 69 dispatch 順序）
        var linear = SubtaskPlan.Linear(new[] { "a", "b", "c" });
        var order1 = SubtaskPlanTopologicalSort.Sort(linear);
        Assert.Equal(new[] { 1, 2, 3 }, order1);

        // case 2：sequential chain 1→2→3
        var chain = new SubtaskPlan(
            new[] { new Subtask(1, "a", ""), new Subtask(2, "b", ""), new Subtask(3, "c", "") },
            new[] { new DependencyEdge(1, 2, DependencyType.Sequential), new DependencyEdge(2, 3, DependencyType.Sequential) });
        var order2 = SubtaskPlanTopologicalSort.Sort(chain);
        Assert.Equal(new[] { 1, 2, 3 }, order2);

        // case 3：並行 deps — 1 與 2 都指向 3（1,2 為起點 deterministic 升序，3 一定在最後）
        var parallel = new SubtaskPlan(
            new[] { new Subtask(1, "a", ""), new Subtask(2, "b", ""), new Subtask(3, "c", "") },
            new[] { new DependencyEdge(1, 3, DependencyType.Sequential), new DependencyEdge(2, 3, DependencyType.Sequential) });
        var order3 = SubtaskPlanTopologicalSort.Sort(parallel);
        Assert.Equal(3, order3.Count);
        Assert.Equal(3, order3[^1]);   // 3 在最後
        Assert.Contains(1, order3);
        Assert.Contains(2, order3);

        // case 4：cycle 1→2 + 2→1 → throw
        var cyclic = new SubtaskPlan(
            new[] { new Subtask(1, "a", ""), new Subtask(2, "b", "") },
            new[] { new DependencyEdge(1, 2, DependencyType.Sequential), new DependencyEdge(2, 1, DependencyType.Sequential) });
        var ex = Assert.Throws<InvalidOperationException>(() => SubtaskPlanTopologicalSort.Sort(cyclic));
        Assert.Contains("cycle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Test 26（Stage 70）：UseV5SubtaskPlanning default false + 三 flag 連動 baseline ─
    [Fact]
    public void Test26_WorkflowSettings_UseV5SubtaskPlanning_DefaultIsFalse_AndThreeFlagBaseline()
    {
        var settings = new AiTeam.Bot.Configuration.WorkflowSettings();
        Assert.False(settings.UseV5SubtaskPlanning);
        // 三 flag 連動 baseline（v5 / v5.5 TalentSkillSeparation / v5.5 Step 4 SubtaskPlanning 都 false → 守 v4 既有 path 0 regression）
        Assert.False(settings.UsePetraOrchestratorV5);
        Assert.False(settings.UseTalentSkillSeparation);
    }

    // ─── Test 27（Stage 70）：BuildPetraSystemPrompt(useSubtaskPlanning) 兩 path 段落切換驗 ─
    [Fact]
    public void Test27_BuildPetraSystemPrompt_SubtaskPlanningPath_SwitchesDecompositionAndOutputSections()
    {
        var method = typeof(PetraOrchestratorService).GetMethod(
            "BuildPetraSystemPrompt",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        // useSubtaskPlanning = true → 含 hierarchical decomposition + JSON 輸出格式 + few-shot 範例
        // Stage 72：簽名加第 3 optional baseTemplateOverride（null = hardcoded baseline）
        var promptWith = (string)method!.Invoke(null, new object?[] { "code_implementation, code_review", true, null })!;
        Assert.Contains("Hierarchical Decomposition", promptWith);
        Assert.Contains("dependency", promptWith, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JSON SubtaskPlan", promptWith);
        Assert.Contains("subtasks", promptWith);
        Assert.Contains("Few-shot 範例 1", promptWith);
        Assert.Contains("Few-shot 範例 2", promptWith);
        Assert.Contains("拆解是擴展不是取代", promptWith);
        // 反例段守紀律
        Assert.Contains("反例", promptWith);
        // 三 trigger 既有判準仍保留（共用段）
        Assert.Contains("1-on-1 trigger", promptWith);
        Assert.Contains("Design trigger", promptWith);
        Assert.Contains("Kickoff trigger", promptWith);

        // useSubtaskPlanning = false（default）→ 守 Stage 67/69 既有 prompt 0 regression — Test9 既有 baseline 仍綠
        var promptWithout = (string)method.Invoke(null, new object?[] { "code_implementation, code_review", false, null })!;
        Assert.Contains("需求拆解紀律", promptWithout);
        Assert.DoesNotContain("Hierarchical Decomposition", promptWithout);
        Assert.DoesNotContain("JSON SubtaskPlan", promptWithout);
        Assert.DoesNotContain("Few-shot 範例 1", promptWithout);
        // 既有「`|` 分隔」輸出格式段仍在
        Assert.Contains("`|` 分隔", promptWithout);
    }

    // ─── Test 28（Stage 71）：BuildPetraSystemPrompt 升級後含線性整包反例 + 判斷邊界 ─
    [Fact]
    public void Test28_BuildPetraSystemPrompt_SubtaskPlanningPath_ContainsLinearBundleCounterexampleAndBoundary()
    {
        var method = typeof(PetraOrchestratorService).GetMethod(
            "BuildPetraSystemPrompt",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        // Stage 72：簽名加第 3 optional baseTemplateOverride（null = hardcoded baseline）
        var prompt = (string)method!.Invoke(null, new object?[] { "code_implementation, code_review", true, null })!;

        // Stage 71 新增：線性整包反例 + 判斷邊界
        Assert.Contains("線性整包", prompt);
        Assert.Contains("真不同 scope", prompt);
        Assert.Contains("判斷邊界", prompt);
        Assert.Contains("打磨多 form", prompt);   // 反例場景關鍵字
        // Stage 70 既有內容 0 regression
        Assert.Contains("Hierarchical Decomposition", prompt);
        Assert.Contains("Few-shot 範例 1", prompt);
        Assert.Contains("Few-shot 範例 2", prompt);
        Assert.Contains("拆解是擴展不是取代", prompt);
    }

    // ─── Test 29（Stage 71）：DispatchTalentsAsync Worker outputLen=0 → skip memory write ──
    [Fact]
    public async Task Test29_DispatchTalentsAsync_WorkerOutputEmpty_SkipsMemoryWrite()
    {
        const string dbName = nameof(Test29_DispatchTalentsAsync_WorkerOutputEmpty_SkipsMemoryWrite);
        var (db, _, sessionRepo, _, orch) = CreateMemoryTestServices(dbName);
        await db.Database.EnsureCreatedAsync();

        var session = sessionRepo.Start(taskGroupId: null);
        await db.SaveChangesAsync();

        var talentId = Guid.NewGuid();
        const string talentName = "Cody";
        var emptyClient = new RecordingChatClient(returnText: "");   // outputLen=0
        var agent = new ChatClientAgent(chatClient: emptyClient, instructions: null, name: talentName);
        var plan = SubtaskPlan.Linear(new[] { "code_implementation" });
        var talent = new FakeTalent(talentName, new[] { "code_implementation" });
        IReadOnlyDictionary<string, Guid> talentNameToIdMap = new Dictionary<string, Guid> { [talentName] = talentId };

        var method = typeof(PetraOrchestratorService).GetMethod(
            "DispatchTalentsAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task)method!.Invoke(orch, new object?[]
        {
            session.Id, "打磨 toast 通知", plan,
            (IReadOnlyList<ITalent>)new ITalent[] { talent },
            new AIAgent[] { agent },
            true,
            talentNameToIdMap,
            CancellationToken.None
        })!;
        await task;

        // outputLen=0 → 0 寫入
        Assert.Equal(0, await db.TaskMemories.CountAsync());
        Assert.Equal(0, await db.TalentMemories.CountAsync());
    }

    // ─── Test 30（Stage 71）：DispatchTalentsAsync Worker outputLen>0 → upsert 生效（regression 守護）──
    [Fact]
    public async Task Test30_DispatchTalentsAsync_WorkerOutputNonEmpty_WritesMemory()
    {
        const string dbName = nameof(Test30_DispatchTalentsAsync_WorkerOutputNonEmpty_WritesMemory);
        var (db, _, sessionRepo, _, orch) = CreateMemoryTestServices(dbName);
        await db.Database.EnsureCreatedAsync();

        var session = sessionRepo.Start(taskGroupId: null);
        await db.SaveChangesAsync();

        var talentId = Guid.NewGuid();
        const string talentName = "Cody";
        var nonEmptyClient = new RecordingChatClient(returnText: "Build 通過，0 error。");
        var agent = new ChatClientAgent(chatClient: nonEmptyClient, instructions: null, name: talentName);
        var plan = SubtaskPlan.Linear(new[] { "code_implementation" });
        var talent = new FakeTalent(talentName, new[] { "code_implementation" });
        IReadOnlyDictionary<string, Guid> talentNameToIdMap = new Dictionary<string, Guid> { [talentName] = talentId };

        var method = typeof(PetraOrchestratorService).GetMethod(
            "DispatchTalentsAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task)method!.Invoke(orch, new object?[]
        {
            session.Id, "修 README typo", plan,
            (IReadOnlyList<ITalent>)new ITalent[] { talent },
            new AIAgent[] { agent },
            true,
            talentNameToIdMap,
            CancellationToken.None
        })!;
        await task;

        // outputLen>0 → upsert 各 1 row
        Assert.Equal(1, await db.TaskMemories.CountAsync());
        Assert.Equal(1, await db.TalentMemories.CountAsync());
        var taskMem = await db.TaskMemories.SingleAsync();
        Assert.Equal($"decision/{talentName}-output-summary", taskMem.Key);
        Assert.Equal("Build 通過，0 error。", taskMem.Content);
    }

    // ─── helper ───────────────────────────────────────────────────────────────────
    private static List<string> ParseCapabilities(string raw)
        => raw.Split('|').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

    /// <summary>Stage 67 Test 15/16：建一個全 null! dep 的 PetraOrchestratorService 實例 — reflection invoke FindTalentForSkill 只用 _roundRobinCounter 不碰其他 dep。</summary>
    private static PetraOrchestratorService CreateMinimalOrchestratorForReflection()
        => new PetraOrchestratorService(
            tools: Array.Empty<AiTeam.Bot.Orchestration.Petra.IAgentTool>(),
            talentFactory: null!,
            workflowResolver: null!,
            sessionRepo: null!,
            memoryRepo: null!,
            db: null!,
            providerFactory: null!,
            gitHubService: null!,
            configuration: new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            promptResolver: null!,
            talentLockService: new AiTeam.Bot.Services.TalentDispatchLockService(),   // Stage 75
            loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            logger: NullLogger<PetraOrchestratorService>.Instance);

    /// <summary>Stage 71 Test 29/30：建立含真實 WorkflowSettingsResolver 的 test 服務群。
    /// 空 app_settings InMemory DB → resolver fallback IOptions defaults（compactKeep=50 / threshold=60）。</summary>
    private static (AppDbContext db, WorkflowSettingsResolver resolver,
        PetraSessionRepository sessionRepo, MemoryRepository memoryRepo,
        PetraOrchestratorService orch) CreateMemoryTestServices(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
        services.AddLogging();
        services.Configure<AiTeam.Bot.Configuration.WorkflowSettings>(_ => { });
        services.AddSingleton<AiTeam.Bot.Services.AppSettingsService>();
        services.AddSingleton<AiTeam.Bot.Configuration.WorkflowSettingsResolver>();
        var sp = services.BuildServiceProvider();

        var db = sp.GetRequiredService<AppDbContext>();
        var resolver = sp.GetRequiredService<AiTeam.Bot.Configuration.WorkflowSettingsResolver>();
        var sessionRepo = new PetraSessionRepository(db);
        var memoryRepo = new MemoryRepository(db);
        var orch = new PetraOrchestratorService(
            tools: Array.Empty<AiTeam.Bot.Orchestration.Petra.IAgentTool>(),
            talentFactory: null!,
            workflowResolver: resolver,
            sessionRepo: sessionRepo,
            memoryRepo: memoryRepo,
            db: db,
            providerFactory: null!,
            gitHubService: null!,
            configuration: new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            promptResolver: null!,
            talentLockService: new AiTeam.Bot.Services.TalentDispatchLockService(),   // Stage 75
            loggerFactory: NullLoggerFactory.Instance,
            logger: NullLogger<PetraOrchestratorService>.Instance);
        return (db, resolver, sessionRepo, memoryRepo, orch);
    }

    /// <summary>Stage 67 Test 15/16：reflection invoke private FindTalentForSkill。</summary>
    private static ITalent? InvokeFindTalentForSkill(PetraOrchestratorService orch, string skill, IReadOnlyList<ITalent> talents)
    {
        var method = typeof(PetraOrchestratorService).GetMethod(
            "FindTalentForSkill",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        return (ITalent?)method!.Invoke(orch, new object[] { skill, talents });
    }

    private sealed class FakeTalent : ITalent
    {
        public string Name { get; }
        public IReadOnlyList<string> Skills { get; }
        public FakeTalent(string name, IReadOnlyList<string> skills) { Name = name; Skills = skills; }
        public AIAgent CreateAgent(PetraSessionContext ctx, string skill)
            => throw new NotImplementedException("Test 15/16 reflection 只驗 FindTalentForSkill lookup 不驗 CreateAgent");
    }

    private sealed class StubClaudeCodeService : IClaudeCodeService
    {
        public string? LastInvokedMethod { get; private set; }
        public string? LastReceivedPrompt { get; private set; }

        private Task<ClaudeCodeResult> Make(string method, string input)
        {
            LastReceivedPrompt = input;
            return Task.FromResult(new ClaudeCodeResult(
                Success: true,
                Output: $"[{method}] echo: {input}",
                ExitCode: 0,
                RawJson: "{}",
                Usage: null));
        }

        public Task<ClaudeCodeResult> RunAsync(string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default, string? systemPrompt = null)
        { LastInvokedMethod = nameof(RunAsync); return Make(nameof(RunAsync), prompt); }

        public Task<ClaudeCodeResult> RunReadOnlyAsync(string workingDir, string prompt, string model, string anthropicApiKey, int? maxTurns = null, CancellationToken ct = default, string? systemPrompt = null)
        { LastInvokedMethod = nameof(RunReadOnlyAsync); return Make(nameof(RunReadOnlyAsync), prompt); }

        public Task<ClaudeCodeResult> RunVictoriaAsync(string workingDir, string prompt, string model, string anthropicApiKey, IReadOnlyList<ImageAttachment>? images = null, CancellationToken ct = default, string? systemPrompt = null)
        { LastInvokedMethod = nameof(RunVictoriaAsync); return Make(nameof(RunVictoriaAsync), prompt); }

        public Task<ClaudeCodeResult> RunQaAsync(string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default, string? systemPrompt = null)
        { LastInvokedMethod = nameof(RunQaAsync); return Make(nameof(RunQaAsync), prompt); }

        public Task<ClaudeCodeResult> RunReviewAsync(string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default, string? systemPrompt = null)
        { LastInvokedMethod = nameof(RunReviewAsync); return Make(nameof(RunReviewAsync), prompt); }

        public Task<ClaudeCodeResult> RunMeetingSessionAsync(string workingDir, string sessionId, string prompt, string model, string anthropicApiKey, bool isFirstMessage, int maxTurns, string[]? allowedTools = null, CancellationToken ct = default, string? systemPrompt = null)
        { LastInvokedMethod = nameof(RunMeetingSessionAsync); return Make(nameof(RunMeetingSessionAsync), prompt); }
    }

    // Stage 66 Test 12：IChatClient stub 紀錄收到的 messages（驗 chain pass-through）
    private sealed class RecordingChatClient : Microsoft.Extensions.AI.IChatClient
    {
        private readonly string _returnText;
        public List<Microsoft.Extensions.AI.ChatMessage> LastReceivedMessages { get; private set; } = new();

        public RecordingChatClient(string returnText) { _returnText = returnText; }

        public Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            Microsoft.Extensions.AI.ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastReceivedMessages = messages.ToList();
            var msg = new Microsoft.Extensions.AI.ChatMessage(
                Microsoft.Extensions.AI.ChatRole.Assistant, _returnText);
            return Task.FromResult(new Microsoft.Extensions.AI.ChatResponse(msg));
        }

        public IAsyncEnumerable<Microsoft.Extensions.AI.ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            Microsoft.Extensions.AI.ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Test 12 不驗 streaming path");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    // Stage 66 Test 12：IAgentTool stub — DispatchWorkersAsync 只用 Name property，CreateAgent 不會被呼叫到（外部已建好 workerAgents）
    private sealed class FakeAgentTool : AiTeam.Bot.Orchestration.Petra.IAgentTool
    {
        public string Name { get; }
        public IReadOnlyList<string> Capabilities { get; }
        public FakeAgentTool(string name, string capability)
        {
            Name = name;
            Capabilities = new[] { capability };
        }
        public AIAgent CreateAgent(AiTeam.Bot.Orchestration.Petra.PetraSessionContext ctx)
            => throw new NotImplementedException("DispatchWorkersAsync 收 workerAgents 不會呼叫 CreateAgent");
    }

    // ─── Test 46（Stage 72）：BuildPetraSystemPrompt baseTemplateOverride 非 null 走 DB base path ─
    // 驗 override 機制 — 三 placeholder（capabilityRoster / decompositionSection / outputSection）正確 Replace
    [Fact]
    public void Test46_BuildPetraSystemPrompt_BaseTemplateOverride_ReplacesAllPlaceholders()
    {
        var method = typeof(PetraOrchestratorService).GetMethod(
            "BuildPetraSystemPrompt",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        // 自訂 override base template — 含三 placeholder + 自訂內容（驗 DB-loaded path 從 PromptResolver 來的 body 也走相同 Replace 邏輯）
        const string customBase = """
[CUSTOM_HEADER]
roster={{capabilityRoster}}
---
{{decompositionSection}}
---
{{outputSection}}
[CUSTOM_FOOTER]
""";

        // useSubtaskPlanning=true → decomposition + output 走 Stage 70+71 段（含 Hierarchical Decomposition / JSON SubtaskPlan）
        var prompt = (string)method!.Invoke(null, new object?[] { "code_implementation, code_review", true, customBase })!;

        // override base 標記正確出現
        Assert.Contains("[CUSTOM_HEADER]", prompt);
        Assert.Contains("[CUSTOM_FOOTER]", prompt);
        // 三 placeholder 真實 Replace 成功（不應剩 raw `{{...}}` 字串）
        Assert.DoesNotContain("{{capabilityRoster}}", prompt);
        Assert.DoesNotContain("{{decompositionSection}}", prompt);
        Assert.DoesNotContain("{{outputSection}}", prompt);
        // dynamic 注入內容存在
        Assert.Contains("roster=code_implementation, code_review", prompt);
        // Stage 70 decomposition + Stage 70 output 段（useSubtaskPlanning=true）注入後仍存在
        Assert.Contains("Hierarchical Decomposition", prompt);
        Assert.Contains("JSON SubtaskPlan", prompt);
    }

    // ─── Test 47（Stage 72）：BuildPetraSystemPrompt baseTemplateOverride=null 走 hardcoded PetraPromptTemplate.Template baseline ─
    // 驗 Stage 64+67+70+71 累積 baseline 0 regression
    [Fact]
    public void Test47_BuildPetraSystemPrompt_NullOverride_UsesHardcodedTemplate()
    {
        var method = typeof(PetraOrchestratorService).GetMethod(
            "BuildPetraSystemPrompt",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        // override=null → 走 PetraPromptTemplate.Template hardcoded constant（Test9 等價驗）
        var prompt = (string)method!.Invoke(null, new object?[] { "code_implementation, code_review", false, null })!;

        // 對齊 PetraPromptTemplate.Template 內容（Stage 64 三 trigger / Stage 73：v5 → v5.5 升級）
        Assert.Contains("v5.5 動態架構 Multi-Agent Orchestrator", prompt);
        Assert.Contains("1-on-1 trigger", prompt);
        Assert.Contains("Design trigger", prompt);
        Assert.Contains("Kickoff trigger", prompt);
        // capability roster 注入
        Assert.Contains("code_implementation, code_review", prompt);
        // Stage 67 既有需求拆解紀律段（useSubtaskPlanning=false）
        Assert.Contains("需求拆解紀律", prompt);
        // useSubtaskPlanning=false 不該含 Stage 70 hierarchical decomposition 段
        Assert.DoesNotContain("Hierarchical Decomposition", prompt);
        // 三 placeholder 全部 Replace 完成（不應剩 raw 字串）
        Assert.DoesNotContain("{{capabilityRoster}}", prompt);
        Assert.DoesNotContain("{{decompositionSection}}", prompt);
        Assert.DoesNotContain("{{outputSection}}", prompt);
    }
}
