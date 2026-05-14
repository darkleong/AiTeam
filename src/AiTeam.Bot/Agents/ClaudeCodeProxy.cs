using AiTeam.Bot.Services;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Stage 17：ClaudeCode Runtime Proxy。
/// 每次呼叫時動態檢查 MockMode flag，決定路由到真實 ClaudeCodeService 或 MockClaudeCodeService。
/// 不需重啟容器，5 分鐘內（AppSettingsService TTL）自動生效。
/// Stage 65 子項 1：6 method 加 systemPrompt 透傳（systemPrompt 放 ct 之後 — v4 caller 0 動）。
/// </summary>
public class ClaudeCodeProxy(
    ClaudeCodeService real,
    MockClaudeCodeService mock,
    AppSettingsService settings,
    ILogger<ClaudeCodeProxy> logger) : IClaudeCodeService
{
    private async Task<bool> IsMockModeAsync(CancellationToken ct)
        => await settings.GetBoolAsync("MockMode", false, ct);

    public async Task<ClaudeCodeResult> RunAsync(
        string workingDir, string prompt, string model, string anthropicApiKey,
        CancellationToken ct = default, string? systemPrompt = null)
    {
        if (await IsMockModeAsync(ct))
        {
            logger.LogInformation("[MockMode] ClaudeCodeProxy → RunAsync");
            return await mock.RunAsync(workingDir, prompt, model, anthropicApiKey, ct, systemPrompt);
        }
        return await real.RunAsync(workingDir, prompt, model, anthropicApiKey, ct, systemPrompt);
    }

    public async Task<ClaudeCodeResult> RunReadOnlyAsync(
        string workingDir, string prompt, string model, string anthropicApiKey,
        int? maxTurns = null, CancellationToken ct = default, string? systemPrompt = null)
    {
        if (await IsMockModeAsync(ct))
        {
            logger.LogInformation("[MockMode] ClaudeCodeProxy → RunReadOnlyAsync");
            return await mock.RunReadOnlyAsync(workingDir, prompt, model, anthropicApiKey, maxTurns, ct, systemPrompt);
        }
        return await real.RunReadOnlyAsync(workingDir, prompt, model, anthropicApiKey, maxTurns, ct, systemPrompt);
    }

    public async Task<ClaudeCodeResult> RunVictoriaAsync(
        string workingDir, string prompt, string model, string anthropicApiKey,
        IReadOnlyList<ImageAttachment>? images = null, CancellationToken ct = default, string? systemPrompt = null)
    {
        if (await IsMockModeAsync(ct))
        {
            logger.LogInformation("[MockMode] ClaudeCodeProxy → RunVictoriaAsync");
            return await mock.RunVictoriaAsync(workingDir, prompt, model, anthropicApiKey, images, ct, systemPrompt);
        }
        return await real.RunVictoriaAsync(workingDir, prompt, model, anthropicApiKey, images, ct, systemPrompt);
    }

    public async Task<ClaudeCodeResult> RunQaAsync(
        string workingDir, string prompt, string model, string anthropicApiKey,
        CancellationToken ct = default, string? systemPrompt = null)
    {
        if (await IsMockModeAsync(ct))
        {
            logger.LogInformation("[MockMode] ClaudeCodeProxy → RunQaAsync");
            return await mock.RunQaAsync(workingDir, prompt, model, anthropicApiKey, ct, systemPrompt);
        }
        return await real.RunQaAsync(workingDir, prompt, model, anthropicApiKey, ct, systemPrompt);
    }

    public async Task<ClaudeCodeResult> RunReviewAsync(
        string workingDir, string prompt, string model, string anthropicApiKey,
        CancellationToken ct = default, string? systemPrompt = null)
    {
        if (await IsMockModeAsync(ct))
        {
            logger.LogInformation("[MockMode] ClaudeCodeProxy → RunReviewAsync");
            return await mock.RunReviewAsync(workingDir, prompt, model, anthropicApiKey, ct, systemPrompt);
        }
        return await real.RunReviewAsync(workingDir, prompt, model, anthropicApiKey, ct, systemPrompt);
    }

    public async Task<ClaudeCodeResult> RunMeetingSessionAsync(
        string workingDir, string sessionId, string prompt, string model, string anthropicApiKey,
        bool isFirstMessage, int maxTurns, string[]? allowedTools = null,
        CancellationToken ct = default, string? systemPrompt = null)
    {
        if (await IsMockModeAsync(ct))
        {
            logger.LogInformation("[MockMode] ClaudeCodeProxy → RunMeetingSessionAsync（sessionId={Id}）", sessionId);
            return await mock.RunMeetingSessionAsync(
                workingDir, sessionId, prompt, model, anthropicApiKey, isFirstMessage, maxTurns, allowedTools, ct, systemPrompt);
        }
        return await real.RunMeetingSessionAsync(
            workingDir, sessionId, prompt, model, anthropicApiKey, isFirstMessage, maxTurns, allowedTools, ct, systemPrompt);
    }
}
