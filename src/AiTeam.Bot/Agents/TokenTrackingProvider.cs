using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using Microsoft.Extensions.Logging;
using BotAgentSettings = AiTeam.Bot.Configuration.AgentSettings;
using BotAgentConfig   = AiTeam.Bot.Configuration.AgentConfig;

namespace AiTeam.Bot.Agents;

/// <summary>
/// ILlmProvider Decorator：在呼叫 inner provider 之前先進行 Token 守門檢查，
/// 呼叫後透明地記錄每次 LLM 呼叫的 Token 用量到 token_logs 資料表。
/// 包裝在 LlmProviderFactory.Create() 中，AgentService 無需任何改動。
/// Stage 17：支援 MockMode，啟用時直接回傳 MockLlmProvider（有意跳過此類，
/// 避免假的 Token 統計資料污染 Dashboard 監控頁）。
/// </summary>
public class TokenTrackingProvider(
    ILlmProvider inner,
    TokenRepository tokenRepository,
    DashboardPushService dashboardPush,
    DiscordAlertService discordAlert,
    BotAgentSettings agentSettings,
    BotAgentConfig agentConfig,
    ILogger<TokenTrackingProvider> logger,
    string agentName,
    string model) : ILlmProvider
{
    public async Task<LlmResponse> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        // ── Token 守門：呼叫 LLM 之前執行 ────────────────────────────
        // Stage 44：守門用 long 比較，避免高用量月份 overflow（DailyTokenLimitK × 1000 仍是 int 範圍，
        // 但累加 dailyUsed/monthlyUsed 後可能跨 int 邊界）。
        long estimatedTokens = (systemPrompt.Length + userMessage.Length) / 4;

        // Check 1：單次請求估算超過全域上限
        long singleLimit = (long)agentSettings.SingleRequestTokenLimitK * 1000;
        if (estimatedTokens > singleLimit)
        {
            var msg = $"⚠️ **[Token 守門]** Agent `{agentName}` 單次請求估算 token 數 {estimatedTokens:N0} " +
                      $"超過上限 {singleLimit:N0}。已拒絕送出。\nPrompt 前 200 字：`{userMessage[..Math.Min(200, userMessage.Length)]}`";
            logger.LogWarning("Token 守門攔截（單次上限）：Agent={Agent}, 估算={Estimated}, 上限={Limit}",
                agentName, estimatedTokens, singleLimit);
            await discordAlert.SendAsync(msg);
            throw new InvalidOperationException($"Token 守門：單次請求估算 {estimatedTokens:N0} tokens 超過上限 {singleLimit:N0}。");
        }

        // Check 2：Agent 日限
        long dailyLimit = (long)agentConfig.DailyTokenLimitK * 1000;
        var dailyUsed = await tokenRepository.GetAgentDailyTotalAsync(agentName, cancellationToken);
        if (dailyUsed + estimatedTokens > dailyLimit)
        {
            var msg = $"⚠️ **[Token 守門]** Agent `{agentName}` 今日已用 {dailyUsed:N0} tokens，" +
                      $"加上本次估算 {estimatedTokens:N0} 將超過日限 {dailyLimit:N0}。已拒絕送出。";
            logger.LogWarning("Token 守門攔截（日限）：Agent={Agent}, 已用={Used}, 估算={Estimated}, 上限={Limit}",
                agentName, dailyUsed, estimatedTokens, dailyLimit);
            await discordAlert.SendAsync(msg);
            throw new InvalidOperationException($"Token 守門：Agent {agentName} 今日用量 {dailyUsed:N0} + 估算 {estimatedTokens:N0} 超過日限 {dailyLimit:N0}。");
        }

        // Check 3：Agent 月限
        long agentMonthlyLimit = (long)agentConfig.MonthlyTokenLimitK * 1000;
        var agentMonthlyUsed = await tokenRepository.GetAgentMonthlyTotalAsync(agentName, cancellationToken);
        if (agentMonthlyUsed + estimatedTokens > agentMonthlyLimit)
        {
            var msg = $"⚠️ **[Token 守門]** Agent `{agentName}` 本月已用 {agentMonthlyUsed:N0} tokens，" +
                      $"加上本次估算 {estimatedTokens:N0} 將超過月限 {agentMonthlyLimit:N0}。已拒絕送出。";
            logger.LogWarning("Token 守門攔截（Agent 月限）：Agent={Agent}, 已用={Used}, 估算={Estimated}, 上限={Limit}",
                agentName, agentMonthlyUsed, estimatedTokens, agentMonthlyLimit);
            await discordAlert.SendAsync(msg);
            throw new InvalidOperationException($"Token 守門：Agent {agentName} 本月用量 {agentMonthlyUsed:N0} + 估算 {estimatedTokens:N0} 超過月限 {agentMonthlyLimit:N0}。");
        }

        // Check 4：全域月限
        long globalMonthlyLimit = (long)agentSettings.MonthlyTokenLimitK * 1000;
        var globalMonthlyUsed = await tokenRepository.GetGlobalMonthlyTotalAsync(cancellationToken);
        if (globalMonthlyUsed + estimatedTokens > globalMonthlyLimit)
        {
            var msg = $"🚨 **[Token 守門 — 全域月限]** 本月所有 Agent 累計已用 {globalMonthlyUsed:N0} tokens，" +
                      $"加上本次估算 {estimatedTokens:N0} 將超過全域月限 {globalMonthlyLimit:N0}。\n" +
                      $"**所有 LLM 呼叫已暫停。** 請修改 `AgentSettings:MonthlyTokenLimitK` 並重啟 Bot 後恢復。";
            logger.LogError("Token 守門攔截（全域月限）：全域已用={Used}, 估算={Estimated}, 上限={Limit}",
                globalMonthlyUsed, estimatedTokens, globalMonthlyLimit);
            await discordAlert.SendAsync(msg);
            throw new InvalidOperationException($"Token 守門：全域本月用量 {globalMonthlyUsed:N0} + 估算 {estimatedTokens:N0} 超過全域月限 {globalMonthlyLimit:N0}。所有 LLM 呼叫已暫停。");
        }

        // ── 呼叫實際 LLM ─────────────────────────────────────────────
        var response = await inner.CompleteAsync(systemPrompt, userMessage, cancellationToken, images);

        // ── 記錄實際用量 ──────────────────────────────────────────────
        // Stage 44：API 層 cache 欄位由 LlmResponse 自身提供時可填（目前 AnthropicProvider/GeminiProvider 未回傳 cache 細節
        // → 留 null，與舊行為相容。後續 FF 一搭車時可從 Anthropic SDK response 取 cache_creation_input_tokens 等欄位）。
        tokenRepository.Add(new TokenLog
        {
            AgentName    = agentName,
            Model        = model,
            InputTokens  = response.InputTokens,
            OutputTokens = response.OutputTokens,
            CreatedAt    = DateTime.UtcNow
        });
        await tokenRepository.SaveAsync(cancellationToken);

        // 通知 Dashboard 即時重整 Token 頁面
        await dashboardPush.PushTokenUpdateAsync();

        return response;
    }
}
