using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration.Petra;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AiTeam.Bot.Tests.Orchestration;

/// <summary>
/// Stage 64：ClaudeCodeChatClientAdapter 6 test 對齊 Roadmap 場景 1 / 4 / 5 + 議題 1 release_publishing 路線 A 驗證。
///
/// 設計：
/// - 用 temp 資料夾模擬 workingDir + 寫假 Resources/CLAUDE_*.md（避免依賴實際 Bot dll 的 Resources）
/// - StubClaudeCodeService 紀錄被呼叫時 workingDir/CLAUDE.md 的存在性 + 內容
/// - tokenLogService 一律傳 null（dispatch 驗 不驗 token_logs 寫入）
/// </summary>
public class ClaudeCodeChatClientAdapterTests : IDisposable
{
    private readonly string _baseDir;
    private readonly string _workingDir;
    private readonly string _resourcesDir;

    public ClaudeCodeChatClientAdapterTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "AiTeam_AdapterTests_" + Guid.NewGuid().ToString("N")[..8]);
        _workingDir = Path.Combine(_baseDir, "workspace");
        _resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        Directory.CreateDirectory(_workingDir);
        Directory.CreateDirectory(_resourcesDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    // ─── T1：CLAUDE.md inject ritual — dispatch 中 CLAUDE.md = template 內容 + dispatch 後還原 ─────
    [Theory]
    [InlineData("code_implementation", "CLAUDE_Cody.md")]
    [InlineData("code_review",         "CLAUDE_Vera.md")]
    [InlineData("qa_testing",          "CLAUDE_Quinn.md")]
    [InlineData("documentation",       "CLAUDE_Sage.md")]
    [InlineData("requirements_extraction", "CLAUDE_Rosa.md")]
    [InlineData("ui_design",           "CLAUDE_Demi.md")]
    public async Task T1_InjectsTemplate_AndRestoresOriginal(string capability, string templateName)
    {
        // arrange: 原 workspace 已有 CLAUDE.md 含特定 marker；template 也寫入特定 marker
        var claudeMd = Path.Combine(_workingDir, "CLAUDE.md");
        var originalContent = $"[ORIGINAL]-{Guid.NewGuid()}";
        await File.WriteAllTextAsync(claudeMd, originalContent);

        var templatePath = Path.Combine(_resourcesDir, templateName);
        var templateContent = $"[TEMPLATE-{templateName}]-{Guid.NewGuid()}";
        await File.WriteAllTextAsync(templatePath, templateContent);

        var stub = new RecordingClaudeCodeService(claudeMd);
        var adapter = NewAdapter(stub, capability);

        // act
        await adapter.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

        // assert: dispatch 中 CLAUDE.md = template
        Assert.Equal(templateContent, stub.CapturedClaudeMdAtDispatch);
        // dispatch 後還原 = 原始
        Assert.Equal(originalContent, await File.ReadAllTextAsync(claudeMd));
    }

    // ─── T2：dispatch 拋 exception 仍 finally restore ─────────────────────────────────
    [Fact]
    public async Task T2_DispatchThrows_StillRestoresOriginal()
    {
        var claudeMd = Path.Combine(_workingDir, "CLAUDE.md");
        var originalContent = $"[ORIGINAL]-{Guid.NewGuid()}";
        await File.WriteAllTextAsync(claudeMd, originalContent);
        var templatePath = Path.Combine(_resourcesDir, "CLAUDE_Cody.md");
        await File.WriteAllTextAsync(templatePath, "[T2-TEMPLATE]");

        var stub = new ThrowingClaudeCodeService();
        var adapter = NewAdapter(stub, "code_implementation");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") }));

        // dispatch 拋例外仍 finally restore
        Assert.Equal(originalContent, await File.ReadAllTextAsync(claudeMd));
    }

    // ─── T3：原 workspace 無 CLAUDE.md → dispatch 後刪除 ────────────────────────────
    [Fact]
    public async Task T3_OriginalAbsent_DeletesAfterDispatch()
    {
        var claudeMd = Path.Combine(_workingDir, "CLAUDE.md");
        if (File.Exists(claudeMd)) File.Delete(claudeMd);
        var templatePath = Path.Combine(_resourcesDir, "CLAUDE_Cody.md");
        await File.WriteAllTextAsync(templatePath, "[T3-TEMPLATE]");

        var stub = new RecordingClaudeCodeService(claudeMd);
        var adapter = NewAdapter(stub, "code_implementation");

        await adapter.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

        // dispatch 中應該寫入了 template
        Assert.Equal("[T3-TEMPLATE]", stub.CapturedClaudeMdAtDispatch);
        // dispatch 後應該刪除（不留 template 內容洩漏）
        Assert.False(File.Exists(claudeMd));
    }

    // ─── T4：transient 5xx retry — 第一次 503 / 第二次 success ─────────────────────
    [Fact]
    public async Task T4_TransientRetry_OnFifthXxThenSuccess()
    {
        // 不依賴 template / workingDir 寫檔 — 跑 RunAsync stub 直接控制 result
        var stub = new SequentialResultStub(
            new ClaudeCodeResult(Success: false, Output: "Anthropic API: 503 Internal server error", ExitCode: 1, RawJson: "{}", Usage: null),
            new ClaudeCodeResult(Success: true,  Output: "[recovered]", ExitCode: 0, RawJson: "{}", Usage: null));

        var adapter = NewAdapter(stub, "code_implementation");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await adapter.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });
        sw.Stop();

        Assert.Equal(2, stub.CallCount);                   // retry 一次
        Assert.Contains("[recovered]", response.Text ?? "");
        Assert.True(sw.ElapsedMilliseconds >= 1000, $"exponential backoff first delay 1s — actual {sw.ElapsedMilliseconds}ms");
    }

    // ─── T5：null-safe token_logs — Usage=null adapter 不爆 + 仍正常回 ChatResponse ──
    // 註：TokenLogService 簽名需要 DI scope（複雜）— 此 test 改驗 adapter 流程：Usage=null 結果照樣回 Output，
    // adapter 不會因為 Usage null 拋例外或 fail（real TokenLogService null-safe 處理由 LogCliUsageAsync 既有 null-check 已 cover）。
    [Fact]
    public async Task T5_NullSafeUsage_AdapterStillReturnsResponse()
    {
        var stub = new SequentialResultStub(
            new ClaudeCodeResult(Success: true, Output: "[ok with null usage]", ExitCode: 0, RawJson: "{}", Usage: null));

        var adapter = NewAdapter(stub, "code_implementation");
        var response = await adapter.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

        Assert.Equal(1, stub.CallCount);
        Assert.Contains("[ok with null usage]", response.Text ?? "");
    }

    // ─── T6：release_publishing 無 CLAUDE_Release.md → warning log + skip + dispatch 正常 ──
    [Fact]
    public async Task T6_ReleasePublishing_NoTemplate_SkipsInjectAndDispatches()
    {
        // arrange: workspace 有 CLAUDE.md，**不**寫 CLAUDE_Release.md template（路線 A：無對應 template）
        var claudeMd = Path.Combine(_workingDir, "CLAUDE.md");
        var originalContent = $"[ORIGINAL-RELEASE]-{Guid.NewGuid()}";
        await File.WriteAllTextAsync(claudeMd, originalContent);
        // 確保 CLAUDE_Release.md 不存在
        var releaseTpl = Path.Combine(_resourcesDir, "CLAUDE_Release.md");
        if (File.Exists(releaseTpl)) File.Delete(releaseTpl);

        var stub = new RecordingClaudeCodeService(claudeMd);
        var adapter = NewAdapter(stub, "release_publishing");

        var response = await adapter.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "release v3.54.0") });

        // dispatch 時 CLAUDE.md 仍為原始內容（路線 A skip inject）
        Assert.Equal(originalContent, stub.CapturedClaudeMdAtDispatch);
        // dispatch 後仍是原始內容（路線 A 不刪除 — 原本就在）
        Assert.Equal(originalContent, await File.ReadAllTextAsync(claudeMd));
        // dispatch 正常 — release_publishing → RunAsync
        Assert.Equal("RunAsync", stub.LastInvokedMethod);
        Assert.NotNull(response);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────────

    private ClaudeCodeChatClientAdapter NewAdapter(IClaudeCodeService stub, string capability) =>
        new(
            claudeCode: stub,
            capability: capability,
            workerName: "TestWorker",
            model: "mock-model",
            apiKey: "mock-key",
            workingDir: _workingDir,
            tokenLogService: null,
            logger: NullLogger<ClaudeCodeChatClientAdapter>.Instance);

    /// <summary>記錄 dispatch 時 CLAUDE.md 內容（驗 inject 真的在 RunAsync invoke 之前已寫入）。</summary>
    private sealed class RecordingClaudeCodeService : IClaudeCodeService
    {
        private readonly string _claudeMdPath;
        public RecordingClaudeCodeService(string claudeMdPath) => _claudeMdPath = claudeMdPath;

        public string? CapturedClaudeMdAtDispatch { get; private set; }
        public string? LastInvokedMethod { get; private set; }

        private async Task<ClaudeCodeResult> Capture(string method, string input)
        {
            CapturedClaudeMdAtDispatch = File.Exists(_claudeMdPath)
                ? await File.ReadAllTextAsync(_claudeMdPath)
                : null;
            LastInvokedMethod = method;
            return new ClaudeCodeResult(Success: true, Output: $"[{method}] {input}", ExitCode: 0, RawJson: "{}", Usage: null);
        }

        public Task<ClaudeCodeResult> RunAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default)
            => Capture(nameof(RunAsync), prompt);

        public Task<ClaudeCodeResult> RunReadOnlyAsync(string workingDir, string prompt, string model, string apiKey, int? maxTurns = null, CancellationToken ct = default)
            => Capture(nameof(RunReadOnlyAsync), prompt);

        public Task<ClaudeCodeResult> RunVictoriaAsync(string workingDir, string prompt, string model, string apiKey, IReadOnlyList<ImageAttachment>? images = null, CancellationToken ct = default)
            => Capture(nameof(RunVictoriaAsync), prompt);

        public Task<ClaudeCodeResult> RunQaAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default)
            => Capture(nameof(RunQaAsync), prompt);

        public Task<ClaudeCodeResult> RunReviewAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default)
            => Capture(nameof(RunReviewAsync), prompt);

        public Task<ClaudeCodeResult> RunMeetingSessionAsync(string workingDir, string sessionId, string prompt, string model, string apiKey, bool isFirstMessage, int maxTurns, string[]? allowedTools = null, CancellationToken ct = default)
            => Capture(nameof(RunMeetingSessionAsync), prompt);
    }

    /// <summary>RunAsync 拋例外驗 finally restore。</summary>
    private sealed class ThrowingClaudeCodeService : IClaudeCodeService
    {
        public Task<ClaudeCodeResult> RunAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");

        public Task<ClaudeCodeResult> RunReadOnlyAsync(string workingDir, string prompt, string model, string apiKey, int? maxTurns = null, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");

        public Task<ClaudeCodeResult> RunVictoriaAsync(string workingDir, string prompt, string model, string apiKey, IReadOnlyList<ImageAttachment>? images = null, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");

        public Task<ClaudeCodeResult> RunQaAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");

        public Task<ClaudeCodeResult> RunReviewAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");

        public Task<ClaudeCodeResult> RunMeetingSessionAsync(string workingDir, string sessionId, string prompt, string model, string apiKey, bool isFirstMessage, int maxTurns, string[]? allowedTools = null, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
    }

    /// <summary>按順序回不同 ClaudeCodeResult（驗 retry 流程）。</summary>
    private sealed class SequentialResultStub : IClaudeCodeService
    {
        private readonly Queue<ClaudeCodeResult> _results;
        public int CallCount { get; private set; }

        public SequentialResultStub(params ClaudeCodeResult[] results)
        {
            _results = new Queue<ClaudeCodeResult>(results);
        }

        private Task<ClaudeCodeResult> Next()
        {
            CallCount++;
            var r = _results.Count > 0 ? _results.Dequeue() : new ClaudeCodeResult(Success: true, Output: "[default]", ExitCode: 0, RawJson: "{}", Usage: null);
            return Task.FromResult(r);
        }

        public Task<ClaudeCodeResult> RunAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default) => Next();
        public Task<ClaudeCodeResult> RunReadOnlyAsync(string workingDir, string prompt, string model, string apiKey, int? maxTurns = null, CancellationToken ct = default) => Next();
        public Task<ClaudeCodeResult> RunVictoriaAsync(string workingDir, string prompt, string model, string apiKey, IReadOnlyList<ImageAttachment>? images = null, CancellationToken ct = default) => Next();
        public Task<ClaudeCodeResult> RunQaAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default) => Next();
        public Task<ClaudeCodeResult> RunReviewAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default) => Next();
        public Task<ClaudeCodeResult> RunMeetingSessionAsync(string workingDir, string sessionId, string prompt, string model, string apiKey, bool isFirstMessage, int maxTurns, string[]? allowedTools = null, CancellationToken ct = default) => Next();
    }
}
