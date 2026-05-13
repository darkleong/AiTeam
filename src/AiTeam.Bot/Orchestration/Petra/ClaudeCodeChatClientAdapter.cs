using System.Runtime.CompilerServices;
using System.Text;
using AiTeam.Bot.Agents;
using Microsoft.Extensions.AI;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：IChatClient adapter 包既有 IClaudeCodeService（v5 動態架構 PoC）。
///
/// 包 adapter 的真實理由（Stage 64 errata 修正）：`IClaudeCodeService` 是 CLI subprocess pattern 非 IChatClient 型別，
/// `ChatClientAgent(IChatClient, ...)` ctor 要 IChatClient 才能掛進 framework — adapter 是必要 wrap 層。
/// （原 Stage 63A spike 誤判「base AIAgent subclass 不被 dispatch」根因 = 漏 TurnToken trigger / Stage 63B commit `ac048ef` 已修。）
///
/// Capability → IClaudeCodeService method dispatch（對齊 [IClaudeCodeService.cs](src/AiTeam.Bot/Agents/IClaudeCodeService.cs) 7 method）：
/// - code_implementation        → RunAsync（完整開發模式）         + CLAUDE_Cody.md
/// - code_review                → RunReviewAsync                  + CLAUDE_Vera.md
/// - qa_testing                 → RunQaAsync                      + CLAUDE_Quinn.md
/// - documentation              → RunReadOnlyAsync                + CLAUDE_Sage.md
/// - requirements_extraction    → RunReadOnlyAsync                + CLAUDE_Rosa.md
/// - ui_design                  → RunReadOnlyAsync                + CLAUDE_Demi.md
/// - release_publishing         → RunAsync                        + (無 CLAUDE_Release.md — skip inject + warning log)
///
/// Stage 64 補強：
/// 1. CLAUDE.md 注入儀式（對齊 v4 DevAgentService.cs:239-285 既有 pattern — backup → write template → run → try-finally restore）
///    避免 CLAUDE.md 內容洩漏被誤 commit 進 PR。release_publishing 無對齊 template → skip inject + warning log（路線 A）。
/// 2. token_logs null-safe — Usage=null 仍寫 cost=0 紀錄保留觀測完整性（TokenLogService 本身 early return null usage）。
/// 3. Transient 5xx retry — DispatchAsync 結果 Output 含 5xx pattern → 3 次 exponential backoff（1s/2s/4s）。
///    不 catch LlmApiFailureException（auth/quota retry 無意義 — 直接 propagate）。
///
/// Mock 階段：IClaudeCodeService DI proxy 自動切 MockClaudeCodeService（既有 545 行 fixture）→ adapter 0 改動接管 Mock。
/// </summary>
internal sealed class ClaudeCodeChatClientAdapter(
    IClaudeCodeService claudeCode,
    string capability,
    string workerName,
    string model,
    string apiKey,
    string workingDir,
    AiTeam.Bot.Services.TokenLogService? tokenLogService,   // nullable: production DI 必注入 / xUnit test 可傳 null（adapter dispatch 驗 不驗 token_logs 寫入）
    ILogger<ClaudeCodeChatClientAdapter> logger) : IChatClient
{
    private readonly ChatClientMetadata _metadata = new("ClaudeCode-via-IChatClient-adapter", defaultModelId: model);

    // Stage 64 6b：5xx transient error pattern（Claude Code CLI subprocess 內部 HTTP 5xx 文字訊號 — string match 是唯一可行 detection）。
    // 對齊 ClaudeCodeService.DetectApiFailureSignal 不 cover 5xx 的事實：5xx 走 result.Success=false path（非 LlmApiFailureException）。
    private static readonly string[] TransientPatterns =
    {
        "503", "502", "500",
        "internal server error",
        "overloaded",
        "upstream",
    };

    // Stage 64 6b：exponential backoff delay（attempt 1 後 1s / attempt 2 後 2s / attempt 3 後 4s — 第 4 次不重試直接 return）。
    private static readonly int[] RetryDelaysMs = { 1000, 2000, 4000 };

    // Stage 64 1：capability → CLAUDE_<X>.md 對應表（release_publishing 無對齊 template → fallback warning + skip，路線 A）。
    private static readonly Dictionary<string, string?> CapabilityToTemplate = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code_implementation"]     = "CLAUDE_Cody.md",
        ["code_review"]             = "CLAUDE_Vera.md",
        ["qa_testing"]              = "CLAUDE_Quinn.md",
        ["documentation"]           = "CLAUDE_Sage.md",
        ["requirements_extraction"] = "CLAUDE_Rosa.md",
        ["ui_design"]               = "CLAUDE_Demi.md",
        ["release_publishing"]      = null,   // 對齊 v4 ReleaseAgentService 本身不用 inject ritual + Stage 65+ 評估是否新增 CLAUDE_Release.md
    };

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = FlattenMessages(messages);
        logger.LogInformation("ClaudeCodeChatClientAdapter dispatch worker={Worker} capability={Capability} promptLen={Len}", workerName, capability, prompt.Length);

        // Stage 64 1：CLAUDE.md inject ritual（對齊 DevAgentService.cs:239-285）。
        // workingDir 空 → skip inject（spike forward path 或測試環境 fallback — DispatchAsync 仍照走，Mock 階段不依賴 workingDir）。
        var claudeMdPath = string.IsNullOrEmpty(workingDir) ? null : Path.Combine(workingDir, "CLAUDE.md");
        var templateName = CapabilityToTemplate.TryGetValue(capability, out var t) ? t : null;
        var templatePath = templateName is null
            ? null
            : Path.Combine(AppContext.BaseDirectory, "Resources", templateName);

        string? originalClaudeMd = null;
        if (claudeMdPath is not null && File.Exists(claudeMdPath))
        {
            originalClaudeMd = await File.ReadAllTextAsync(claudeMdPath, cancellationToken);
        }

        if (claudeMdPath is not null && templatePath is not null && File.Exists(templatePath))
        {
            var content = await File.ReadAllTextAsync(templatePath, cancellationToken);
            await File.WriteAllTextAsync(claudeMdPath, content, cancellationToken);
            logger.LogInformation("CLAUDE.md inject 完成 worker={Worker} template={Template}", workerName, templateName);
        }
        else if (claudeMdPath is not null && templateName is null)
        {
            logger.LogWarning("Capability {Cap} 無對應 CLAUDE template（路線 A — fallback skip inject）worker={Worker}", capability, workerName);
        }
        else if (claudeMdPath is not null && templatePath is not null)
        {
            logger.LogWarning("CLAUDE template 不存在於 {Path}，略過寫入 worker={Worker}", templatePath, workerName);
        }

        try
        {
            var result = await DispatchWithRetryAsync(prompt, cancellationToken);

            // Stage 64 6a：token_logs null-safe — Usage=null 仍寫 cost=0 紀錄保留觀測完整性。
            // TokenLogService.LogCliUsageAsync 本身 usage is null → early return（line 35）— adapter 層自製 zero TokenUsage 對齊。
            if (tokenLogService is not null)
            {
                try
                {
                    var usageForLog = result.Usage ?? new TokenUsage(0, 0, 0, 0, 0m, false);
                    await tokenLogService.LogCliUsageAsync(workerName, model, "PetraOrchestratorV5", null, null, usageForLog, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "ClaudeCodeChatClientAdapter token_logs 寫入失敗（不影響 worker dispatch）worker={Worker}", workerName);
                }
            }

            var responseMessage = new ChatMessage(ChatRole.Assistant, result.Output ?? "");
            return new ChatResponse(responseMessage);
        }
        finally
        {
            // Stage 64 1：finally restore CLAUDE.md（對齊 DevAgentService.cs:295-300 紀律）。
            // CancellationToken.None — 避免 mid-workflow cancel 跳過 restore（plan reviewer 議題 2 補強）。
            if (claudeMdPath is not null)
            {
                try
                {
                    if (originalClaudeMd is not null)
                        await File.WriteAllTextAsync(claudeMdPath, originalClaudeMd, CancellationToken.None);
                    else if (File.Exists(claudeMdPath))
                        File.Delete(claudeMdPath);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "ClaudeCodeChatClientAdapter CLAUDE.md restore 失敗 worker={Worker} path={Path}", workerName, claudeMdPath);
                }
            }
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Stage 63B PoC：streaming 走同步 wrap（IClaudeCodeService 本身是 one-shot subprocess）— yield 一次足以對齊 framework dispatch 期望
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType == typeof(ChatClientMetadata) ? _metadata : null;

    public void Dispose()
    {
        // IClaudeCodeService 由 DI 管理 — adapter no-op
    }

    /// <summary>
    /// Stage 64 6b：transient 5xx retry — exponential backoff 1s/2s/4s 最多 3 次重試。
    /// 非 transient（auth/quota / Mock fail / 真實 logic error）→ 直接 return 不重試。
    /// token_logs 寫入由 caller 負責一次（retry 內部 attempt 不寫 — 對齊「最終 result.Usage 一次寫」契約）。
    /// </summary>
    private async Task<ClaudeCodeResult> DispatchWithRetryAsync(string prompt, CancellationToken ct)
    {
        ClaudeCodeResult? last = null;
        for (var attempt = 1; attempt <= RetryDelaysMs.Length + 1; attempt++)
        {
            last = await DispatchAsync(prompt, ct);
            if (last.Success) return last;

            if (!IsTransient5xx(last.Output))
            {
                // 非 transient — propagate failure 不重試
                return last;
            }

            if (attempt >= RetryDelaysMs.Length + 1)
            {
                logger.LogWarning("ClaudeCodeChatClientAdapter transient 5xx 重試耗盡（{Max} attempt）worker={Worker}", attempt, workerName);
                break;
            }

            var delayMs = RetryDelaysMs[attempt - 1];
            logger.LogWarning("ClaudeCodeChatClientAdapter transient 5xx retry {Attempt}/{Max} after {Delay}ms worker={Worker}",
                attempt, RetryDelaysMs.Length, delayMs, workerName);
            await Task.Delay(delayMs, ct);
        }
        return last!;
    }

    private static bool IsTransient5xx(string? output)
    {
        if (string.IsNullOrEmpty(output)) return false;
        var lower = output.ToLowerInvariant();
        return TransientPatterns.Any(p => lower.Contains(p));
    }

    private Task<ClaudeCodeResult> DispatchAsync(string prompt, CancellationToken ct) => capability switch
    {
        "code_implementation"     => claudeCode.RunAsync(workingDir, prompt, model, apiKey, ct),
        "code_review"             => claudeCode.RunReviewAsync(workingDir, prompt, model, apiKey, ct),
        "qa_testing"              => claudeCode.RunQaAsync(workingDir, prompt, model, apiKey, ct),
        "documentation"           => claudeCode.RunReadOnlyAsync(workingDir, prompt, model, apiKey, null, ct),
        "requirements_extraction" => claudeCode.RunReadOnlyAsync(workingDir, prompt, model, apiKey, null, ct),
        "ui_design"               => claudeCode.RunReadOnlyAsync(workingDir, prompt, model, apiKey, null, ct),
        "release_publishing"      => claudeCode.RunAsync(workingDir, prompt, model, apiKey, ct),
        _ => throw new InvalidOperationException($"未知 capability: {capability}（對齊 ClaudeCodeChatClientAdapter dispatch 表 — Stage 63B PoC 7 capability）"),
    };

    private static string FlattenMessages(IEnumerable<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var m in messages)
        {
            var roleTag = m.Role == ChatRole.System    ? "[system]"
                       : m.Role == ChatRole.User       ? "[user]"
                       : m.Role == ChatRole.Assistant  ? "[assistant]"
                       : m.Role == ChatRole.Tool       ? "[tool]"
                       : $"[{m.Role}]";
            sb.AppendLine(roleTag);
            sb.AppendLine(m.Text ?? "");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }
}
