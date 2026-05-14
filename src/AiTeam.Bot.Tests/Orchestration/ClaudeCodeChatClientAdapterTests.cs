using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration.Petra;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AiTeam.Bot.Tests.Orchestration;

/// <summary>
/// Stage 65 子項 1：CLAUDE.md inject ritual 修根因 — 改用 CLI --append-system-prompt。
/// 改寫對應原 Stage 64 6 test：
/// - T1 驗 systemPrompt 透傳 template 內容（取代原 T1「workspace CLAUDE.md = template」）
/// - T2 驗 dispatch 拋例外時 workspace CLAUDE.md 0 動（取代原 T2「finally restore」）
/// - T3 驗 workspace 原本無 CLAUDE.md → dispatch 後仍 0 動（取代原 T3「inject 後刪除」）
/// - T4 transient 5xx retry 保留
/// - T5 null-safe Usage 保留
/// - T6 release_publishing 走 RunAsync + systemPrompt=null（取代原 T6「skip inject + workspace 0 動」）
/// - T7 新增：dispatch 拋例外 exception 仍 propagate（驗 Stage 65 子項 2 try-finally 結構正確）
///
/// 設計：
/// - 用 temp 資料夾模擬 workingDir + 寫假 Resources/CLAUDE_*.md（避免依賴實際 Bot dll 的 Resources）
/// - RecordingClaudeCodeService 紀錄被呼叫時 systemPrompt 內容（驗 inject 透過 CLI flag 而非 workspace 寫檔）
/// - tokenLogService 一律傳 null（test 不直接驗 token_logs 寫入；Trial_v11 SQL 驗證真實 production 寫入）
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

    // ─── T1：systemPrompt 透傳 template 內容 + workspace CLAUDE.md 0 動 ─────
    [Theory]
    [InlineData("code_implementation", "CLAUDE_Cody.md")]
    [InlineData("code_review",         "CLAUDE_Vera.md")]
    [InlineData("qa_testing",          "CLAUDE_Quinn.md")]
    [InlineData("documentation",       "CLAUDE_Sage.md")]
    [InlineData("requirements_extraction", "CLAUDE_Rosa.md")]
    [InlineData("ui_design",           "CLAUDE_Demi.md")]
    public async Task T1_SystemPromptForwardsTemplate_WorkspaceClaudeMdUntouched(string capability, string templateName)
    {
        // arrange: workspace 原已有 CLAUDE.md（不應該被 adapter 動到）
        var claudeMd = Path.Combine(_workingDir, "CLAUDE.md");
        var originalContent = $"[ORIGINAL]-{Guid.NewGuid()}";
        await File.WriteAllTextAsync(claudeMd, originalContent);

        var templatePath = Path.Combine(_resourcesDir, templateName);
        var templateContent = $"[TEMPLATE-{templateName}]-{Guid.NewGuid()}";
        await File.WriteAllTextAsync(templatePath, templateContent);

        var stub = new RecordingClaudeCodeService();
        var adapter = NewAdapter(stub, capability);

        // act
        await adapter.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

        // assert 1：dispatch 收到的 systemPrompt = template 內容
        Assert.Equal(templateContent, stub.CapturedSystemPrompt);
        // assert 2：workspace CLAUDE.md 0 動（前後內容一致 = 0 commit 污染）
        Assert.Equal(originalContent, await File.ReadAllTextAsync(claudeMd));
    }

    // ─── T2：dispatch 拋 exception 仍保 workspace CLAUDE.md 0 動 ──────────────
    [Fact]
    public async Task T2_DispatchThrows_WorkspaceClaudeMdUntouched()
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

        // dispatch 拋例外 — workspace CLAUDE.md 仍保原始（adapter 0 動）
        Assert.Equal(originalContent, await File.ReadAllTextAsync(claudeMd));
    }

    // ─── T3：原 workspace 無 CLAUDE.md → dispatch 後仍 0 存在（adapter 不寫不刪）────
    [Fact]
    public async Task T3_WorkspaceClaudeMdAbsent_RemainsAbsentAfterDispatch()
    {
        var claudeMd = Path.Combine(_workingDir, "CLAUDE.md");
        if (File.Exists(claudeMd)) File.Delete(claudeMd);
        var templatePath = Path.Combine(_resourcesDir, "CLAUDE_Cody.md");
        await File.WriteAllTextAsync(templatePath, "[T3-TEMPLATE]");

        var stub = new RecordingClaudeCodeService();
        var adapter = NewAdapter(stub, "code_implementation");

        await adapter.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

        // dispatch 收到 systemPrompt = template 內容
        Assert.Equal("[T3-TEMPLATE]", stub.CapturedSystemPrompt);
        // dispatch 後 workspace CLAUDE.md 仍不存在（adapter 0 寫 = 0 留下蛛絲）
        Assert.False(File.Exists(claudeMd));
    }

    // ─── T4：transient 5xx retry — 第一次 503 / 第二次 success ─────────────────────
    [Fact]
    public async Task T4_TransientRetry_OnFifthXxThenSuccess()
    {
        var stub = new SequentialResultStub(
            new ClaudeCodeResult(Success: false, Output: "Anthropic API: 503 Internal server error", ExitCode: 1, RawJson: "{}", Usage: null),
            new ClaudeCodeResult(Success: true,  Output: "[recovered]", ExitCode: 0, RawJson: "{}", Usage: null));

        var adapter = NewAdapter(stub, "code_implementation");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await adapter.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });
        sw.Stop();

        Assert.Equal(2, stub.CallCount);
        Assert.Contains("[recovered]", response.Text ?? "");
        Assert.True(sw.ElapsedMilliseconds >= 1000, $"exponential backoff first delay 1s — actual {sw.ElapsedMilliseconds}ms");
    }

    // ─── T5：null-safe token_logs — Usage=null adapter 不爆 + 仍正常回 ChatResponse ──
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

    // ─── T6：release_publishing 走 RunAsync + systemPrompt=null + workspace CLAUDE.md 0 動 ─
    [Fact]
    public async Task T6_ReleasePublishing_NoTemplate_DispatchWithNullSystemPrompt()
    {
        var claudeMd = Path.Combine(_workingDir, "CLAUDE.md");
        var originalContent = $"[ORIGINAL-RELEASE]-{Guid.NewGuid()}";
        await File.WriteAllTextAsync(claudeMd, originalContent);
        var releaseTpl = Path.Combine(_resourcesDir, "CLAUDE_Release.md");
        if (File.Exists(releaseTpl)) File.Delete(releaseTpl);

        var stub = new RecordingClaudeCodeService();
        var adapter = NewAdapter(stub, "release_publishing");

        var response = await adapter.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "release v3.55.0") });

        // release_publishing → 對應 template = null → systemPrompt = null
        Assert.Null(stub.CapturedSystemPrompt);
        // workspace CLAUDE.md 0 動
        Assert.Equal(originalContent, await File.ReadAllTextAsync(claudeMd));
        Assert.Equal("RunAsync", stub.LastInvokedMethod);
        Assert.NotNull(response);
    }

    // ─── T7：dispatch 拋 LlmApiFailureException 仍 propagate（Stage 65 子項 2 try-finally 結構正確）──
    [Fact]
    public async Task T7_DispatchThrows_ExceptionStillPropagates()
    {
        var stub = new ThrowingClaudeCodeService();
        var adapter = NewAdapter(stub, "code_review");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "review pls") }));

        Assert.Equal("boom", ex.Message);
        // 註：tokenLogService=null → 不直接驗 token_logs 真實寫入（Trial_v11 SQL 對 Vera 行存在性驗證 production path）。
        // 此 test 驗 adapter 改造後的 try { dispatch; capturedUsage } catch { throw } finally { token_logs } 結構不破壞 exception propagation。
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

    /// <summary>紀錄 dispatch 時 systemPrompt 參數內容（驗 inject 透過 CLI --append-system-prompt 而非 workspace 寫檔）。</summary>
    private sealed class RecordingClaudeCodeService : IClaudeCodeService
    {
        public string? CapturedSystemPrompt { get; private set; }
        public string? LastInvokedMethod { get; private set; }

        private Task<ClaudeCodeResult> Capture(string method, string input, string? systemPrompt)
        {
            CapturedSystemPrompt = systemPrompt;
            LastInvokedMethod = method;
            return Task.FromResult(new ClaudeCodeResult(Success: true, Output: $"[{method}] {input}", ExitCode: 0, RawJson: "{}", Usage: null));
        }

        public Task<ClaudeCodeResult> RunAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default, string? systemPrompt = null)
            => Capture(nameof(RunAsync), prompt, systemPrompt);

        public Task<ClaudeCodeResult> RunReadOnlyAsync(string workingDir, string prompt, string model, string apiKey, int? maxTurns = null, CancellationToken ct = default, string? systemPrompt = null)
            => Capture(nameof(RunReadOnlyAsync), prompt, systemPrompt);

        public Task<ClaudeCodeResult> RunVictoriaAsync(string workingDir, string prompt, string model, string apiKey, IReadOnlyList<ImageAttachment>? images = null, CancellationToken ct = default, string? systemPrompt = null)
            => Capture(nameof(RunVictoriaAsync), prompt, systemPrompt);

        public Task<ClaudeCodeResult> RunQaAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default, string? systemPrompt = null)
            => Capture(nameof(RunQaAsync), prompt, systemPrompt);

        public Task<ClaudeCodeResult> RunReviewAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default, string? systemPrompt = null)
            => Capture(nameof(RunReviewAsync), prompt, systemPrompt);

        public Task<ClaudeCodeResult> RunMeetingSessionAsync(string workingDir, string sessionId, string prompt, string model, string apiKey, bool isFirstMessage, int maxTurns, string[]? allowedTools = null, CancellationToken ct = default, string? systemPrompt = null)
            => Capture(nameof(RunMeetingSessionAsync), prompt, systemPrompt);
    }

    /// <summary>RunXxxAsync 一律拋例外（驗 adapter exception propagation）。</summary>
    private sealed class ThrowingClaudeCodeService : IClaudeCodeService
    {
        public Task<ClaudeCodeResult> RunAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default, string? systemPrompt = null)
            => throw new InvalidOperationException("boom");

        public Task<ClaudeCodeResult> RunReadOnlyAsync(string workingDir, string prompt, string model, string apiKey, int? maxTurns = null, CancellationToken ct = default, string? systemPrompt = null)
            => throw new InvalidOperationException("boom");

        public Task<ClaudeCodeResult> RunVictoriaAsync(string workingDir, string prompt, string model, string apiKey, IReadOnlyList<ImageAttachment>? images = null, CancellationToken ct = default, string? systemPrompt = null)
            => throw new InvalidOperationException("boom");

        public Task<ClaudeCodeResult> RunQaAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default, string? systemPrompt = null)
            => throw new InvalidOperationException("boom");

        public Task<ClaudeCodeResult> RunReviewAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default, string? systemPrompt = null)
            => throw new InvalidOperationException("boom");

        public Task<ClaudeCodeResult> RunMeetingSessionAsync(string workingDir, string sessionId, string prompt, string model, string apiKey, bool isFirstMessage, int maxTurns, string[]? allowedTools = null, CancellationToken ct = default, string? systemPrompt = null)
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

        public Task<ClaudeCodeResult> RunAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default, string? systemPrompt = null) => Next();
        public Task<ClaudeCodeResult> RunReadOnlyAsync(string workingDir, string prompt, string model, string apiKey, int? maxTurns = null, CancellationToken ct = default, string? systemPrompt = null) => Next();
        public Task<ClaudeCodeResult> RunVictoriaAsync(string workingDir, string prompt, string model, string apiKey, IReadOnlyList<ImageAttachment>? images = null, CancellationToken ct = default, string? systemPrompt = null) => Next();
        public Task<ClaudeCodeResult> RunQaAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default, string? systemPrompt = null) => Next();
        public Task<ClaudeCodeResult> RunReviewAsync(string workingDir, string prompt, string model, string apiKey, CancellationToken ct = default, string? systemPrompt = null) => Next();
        public Task<ClaudeCodeResult> RunMeetingSessionAsync(string workingDir, string sessionId, string prompt, string model, string apiKey, bool isFirstMessage, int maxTurns, string[]? allowedTools = null, CancellationToken ct = default, string? systemPrompt = null) => Next();
    }
}
