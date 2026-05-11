using System.Runtime.CompilerServices;
using System.Text;
using AiTeam.Bot.Agents;
using Microsoft.Extensions.AI;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：IChatClient adapter 包既有 IClaudeCodeService（v5 動態架構 PoC 限制 (b) workaround）。
///
/// Stage 63A spike notes 揭限制 (b)：base AIAgent subclass 的 RunCoreAsync / RunCoreStreamingAsync
/// 在 BuildSequential workflow 中 0 invoke → Stage 63B 必走 ChatClientAgent(IChatClient, ...) ctor，
/// 而 IChatClient 必須是 framework 認得的型別 → 本 adapter 包既有 IClaudeCodeService（CLI subprocess pattern）。
///
/// Capability → IClaudeCodeService method dispatch（對齊 [IClaudeCodeService.cs](src/AiTeam.Bot/Agents/IClaudeCodeService.cs) 7 method）：
/// - code_implementation        → RunAsync（完整開發模式）
/// - code_review                → RunReviewAsync
/// - qa_testing                 → RunQaAsync
/// - documentation              → RunReadOnlyAsync（文件產出走 read-only context + edit doc 既有 pattern）
/// - requirements_extraction    → RunReadOnlyAsync
/// - ui_design                  → RunReadOnlyAsync
/// - release_publishing         → RunAsync
///
/// Mock 階段：IClaudeCodeService DI proxy 自動切 MockClaudeCodeService（既有 545 行 fixture）→ adapter 0 改動接管 Mock。
/// </summary>
internal sealed class ClaudeCodeChatClientAdapter(
    IClaudeCodeService claudeCode,
    string capability,
    string model,
    string apiKey,
    string workingDir,
    ILogger<ClaudeCodeChatClientAdapter> logger) : IChatClient
{
    private readonly ChatClientMetadata _metadata = new("ClaudeCode-via-IChatClient-adapter", defaultModelId: model);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = FlattenMessages(messages);
        logger.LogDebug("ClaudeCodeChatClientAdapter dispatch capability={Capability} promptLen={Len}", capability, prompt.Length);

        var result = await DispatchAsync(prompt, cancellationToken);
        var responseMessage = new ChatMessage(ChatRole.Assistant, result.Output ?? "");
        return new ChatResponse(responseMessage);
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
