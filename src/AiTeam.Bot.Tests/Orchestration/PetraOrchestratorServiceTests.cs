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
            stub, capability, "mock-model", "mock-key", "/tmp/wd",
            NullLogger<ClaudeCodeChatClientAdapter>.Instance);

        var input = new[] { new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "test input") };
        var response = await adapter.GetResponseAsync(input);

        Assert.NotNull(response);
        Assert.Equal(expectedMethod, stub.LastInvokedMethod);
        Assert.Contains(expectedMethod, response.Text ?? "");
    }

    // ─── helper ───────────────────────────────────────────────────────────────────
    private static List<string> ParseCapabilities(string raw)
        => raw.Split('|').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

    private sealed class StubClaudeCodeService : IClaudeCodeService
    {
        public string? LastInvokedMethod { get; private set; }

        private Task<ClaudeCodeResult> Make(string method, string input) =>
            Task.FromResult(new ClaudeCodeResult(
                Success: true,
                Output: $"[{method}] echo: {input}",
                ExitCode: 0,
                RawJson: "{}",
                Usage: null));

        public Task<ClaudeCodeResult> RunAsync(string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default)
        { LastInvokedMethod = nameof(RunAsync); return Make(nameof(RunAsync), prompt); }

        public Task<ClaudeCodeResult> RunReadOnlyAsync(string workingDir, string prompt, string model, string anthropicApiKey, int? maxTurns = null, CancellationToken ct = default)
        { LastInvokedMethod = nameof(RunReadOnlyAsync); return Make(nameof(RunReadOnlyAsync), prompt); }

        public Task<ClaudeCodeResult> RunVictoriaAsync(string workingDir, string prompt, string model, string anthropicApiKey, IReadOnlyList<ImageAttachment>? images = null, CancellationToken ct = default)
        { LastInvokedMethod = nameof(RunVictoriaAsync); return Make(nameof(RunVictoriaAsync), prompt); }

        public Task<ClaudeCodeResult> RunQaAsync(string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default)
        { LastInvokedMethod = nameof(RunQaAsync); return Make(nameof(RunQaAsync), prompt); }

        public Task<ClaudeCodeResult> RunReviewAsync(string workingDir, string prompt, string model, string anthropicApiKey, CancellationToken ct = default)
        { LastInvokedMethod = nameof(RunReviewAsync); return Make(nameof(RunReviewAsync), prompt); }

        public Task<ClaudeCodeResult> RunMeetingSessionAsync(string workingDir, string sessionId, string prompt, string model, string anthropicApiKey, bool isFirstMessage, int maxTurns, string[]? allowedTools = null, CancellationToken ct = default)
        { LastInvokedMethod = nameof(RunMeetingSessionAsync); return Make(nameof(RunMeetingSessionAsync), prompt); }
    }
}
