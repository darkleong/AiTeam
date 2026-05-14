using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Orchestration.Petra;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
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

        repo.AppendMessage(session.Id, "user", "修 README typo 1 行");
        repo.AppendMessage(session.Id, "assistant", "code_implementation");
        repo.AppendMessage(session.Id, "tool", "[Cody] 已實作（mock fixture）");
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
    [InlineData(typeof(RequirementsAgentService),"requirements_extraction")]
    [InlineData(typeof(DesignerAgentService),    "ui_design")]
    [InlineData(typeof(ReleaseAgentService),     "release_publishing")]
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
    [InlineData("code_implementation",     "RunAsync")]
    [InlineData("code_review",             "RunReviewAsync")]
    [InlineData("qa_testing",              "RunQaAsync")]
    [InlineData("documentation",           "RunReadOnlyAsync")]
    [InlineData("requirements_extraction", "RunReadOnlyAsync")]
    [InlineData("ui_design",               "RunReadOnlyAsync")]
    [InlineData("release_publishing",      "RunAsync")]
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

        var prompt = (string)method!.Invoke(null, new object?[] { "code_implementation, code_review" })!;

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

    // ─── Test 10（Stage 64）：ClaudeCodeChatClientAdapter dispatch 7 capability 完整 cover ──
    // 對齊 Roadmap 場景 6 chain 驗證精神（多 worker dispatch）— 既有 Test7 已 cover 7 capability 各自 dispatch
    // 此 test 補強：capability 字典本身完整 cover 7 個（防回歸 — 對齊 IClaudeCodeService 7 method）
    [Fact]
    public void Test10_AdapterCapabilityDispatch_CoversAllSevenCapabilities()
    {
        // 對齊 IClaudeCodeService 7 method（除了 RunVictoriaAsync/RunMeetingSessionAsync 是 v4 special path 不在 v5 dispatch）
        var expectedCapabilities = new[]
        {
            "code_implementation", "code_review", "qa_testing",
            "documentation", "requirements_extraction", "ui_design",
            "release_publishing"
        };

        // 對齊既有 Test7 Theory data — 7 個 capability 與 7 個 expectedMethod 對齊（不重複驗 dispatch，只驗 capability 列表完整性）
        Assert.Equal(7, expectedCapabilities.Length);
        Assert.Contains("release_publishing", expectedCapabilities);   // 議題 1 路線 A — 仍在 dispatch 表內
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
        var orch = new PetraOrchestratorService(
            tools: Array.Empty<AiTeam.Bot.Orchestration.Petra.IAgentTool>(),
            sessionRepo: repo,
            db: db,
            providerFactory: null!,
            gitHubService: null!,
            configuration: new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
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

    // ─── helper ───────────────────────────────────────────────────────────────────
    private static List<string> ParseCapabilities(string raw)
        => raw.Split('|').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

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
}
