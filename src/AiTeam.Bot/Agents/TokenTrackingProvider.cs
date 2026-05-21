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
/// Stage 47：4 個 Check 改讀 DB（AppSettings + AgentConfigCache），
/// appsettings.json 保留作 fallback 安全網（DB 無值時生效）。
/// </summary>
public class TokenTrackingProvider(
    ILlmProvider inner,
    TokenRepository tokenRepository,
    DashboardPushService dashboardPush,
    DiscordAlertService discordAlert,
    BotAgentSettings agentSettings,
    BotAgentConfig agentConfig,
    AppSettingsService appSettings,
    AgentConfigCache agentConfigCache,
    TokenCostEstimator costEstimator,
    ILogger<TokenTrackingProvider> logger,
    string agentName,
    string model) : ILlmProvider
{
    // Stage 82 子項 2：AsyncLocal scope — Petra LLM call 4 site 包 using scope，TokenTrackingProvider 寫 TokenLog 時透傳 PetraSessionId
    // （對齊 Stage 81 議題 #5 worker dispatch path PetraSessionId 透傳紀律）。
    // ILlmProvider 介面 0 動（W1 紀律 / 介面穩定）/ 既有所有 caller（CeoAgentService / DashboardAgentService / AgentQueueProcessor）透明 /
    // Petra 4 call site 加 using scope。
    internal static readonly AsyncLocal<Guid?> PetraSessionAmbient = new();

    public static IDisposable BeginPetraSessionScope(Guid sessionId)
    {
        var prev = PetraSessionAmbient.Value;
        PetraSessionAmbient.Value = sessionId;
        return new PopScope(prev);
    }

    private sealed class PopScope(Guid? previous) : IDisposable
    {
        public void Dispose() => PetraSessionAmbient.Value = previous;
    }

    public async Task<LlmResponse> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ImageAttachment>? images = null)
    {
        // Stage 47：讀 per-agent DB 設定（null = DB 未設定，runtime fallback appsettings）
        var (_, _, dbDailyK, dbMonthlyK) = agentConfigCache.Get(agentName);

        // ── Token 守門：呼叫 LLM 之前執行 ────────────────────────────
        // Stage 44：守門用 long 比較，避免高用量月份 overflow（DailyTokenLimitK × 1000 仍是 int 範圍，
        // 但累加 dailyUsed/monthlyUsed 後可能跨 int 邊界）。
        long estimatedTokens = (systemPrompt.Length + userMessage.Length) / 4;

        // Check 1：單次請求上限（Stage 47：DB AppSettings 優先，fallback appsettings）
        var singleLimitFromDb = await appSettings.GetIntAsync("Token:SingleRequestLimitK", 0, cancellationToken);
        long singleLimit = singleLimitFromDb > 0
            ? (long)singleLimitFromDb * 1000
            : (long)agentSettings.SingleRequestTokenLimitK * 1000;

        if (estimatedTokens > singleLimit)
        {
            var msg = $"⚠️ **[Token 守門]** Agent `{agentName}` 單次請求估算 token 數 {estimatedTokens:N0} " +
                      $"超過上限 {singleLimit:N0}。已拒絕送出。\nPrompt 前 200 字：`{userMessage[..Math.Min(200, userMessage.Length)]}`";
            logger.LogWarning("Token 守門攔截（單次上限）：Agent={Agent}, 估算={Estimated}, 上限={Limit}",
                agentName, estimatedTokens, singleLimit);
            await discordAlert.SendAsync(msg);
            throw new InvalidOperationException($"Token 守門：單次請求估算 {estimatedTokens:N0} tokens 超過上限 {singleLimit:N0}。");
        }

        // Check 2：Agent 日限（Stage 47：AgentConfigCache DB 優先，fallback appsettings）
        long dailyLimit = dbDailyK.HasValue
            ? (long)dbDailyK.Value * 1000
            : (long)agentConfig.DailyTokenLimitK * 1000;

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

        // Check 3：Agent 月限（Stage 47：AgentConfigCache DB 優先，fallback appsettings）
        long agentMonthlyLimit = dbMonthlyK.HasValue
            ? (long)dbMonthlyK.Value * 1000
            : (long)agentConfig.MonthlyTokenLimitK * 1000;

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

        // Check 4：全域月限（Stage 47：DB AppSettings 優先，fallback appsettings）
        var globalMonthlyFromDb = await appSettings.GetIntAsync("Token:GlobalMonthlyLimitK", 0, cancellationToken);
        long globalMonthlyLimit = globalMonthlyFromDb > 0
            ? (long)globalMonthlyFromDb * 1000
            : (long)agentSettings.MonthlyTokenLimitK * 1000;

        var globalMonthlyUsed = await tokenRepository.GetGlobalMonthlyTotalAsync(cancellationToken);
        if (globalMonthlyUsed + estimatedTokens > globalMonthlyLimit)
        {
            var msg = $"🚨 **[Token 守門 — 全域月限]** 本月所有 Agent 累計已用 {globalMonthlyUsed:N0} tokens，" +
                      $"加上本次估算 {estimatedTokens:N0} 將超過全域月限 {globalMonthlyLimit:N0}。\n" +
                      $"**所有 LLM 呼叫已暫停。** 請至 Dashboard【系統設定 → Token 守門設定】調整全域月限，5 分鐘內自動生效。";
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
        // Stage 56：FF 四十三 修 — Path B（API direct call）原本連 TotalCostUsd 都不寫，現走 TokenCostEstimator
        // 估算 + 標記 IsEstimated=true。cache 細節仍受 ILlmProvider 既有限制傳 0（未來補 cache 欄位後可改）。
        var (cost, isEstimated) = costEstimator.Estimate(
            model, response.InputTokens, response.OutputTokens, cacheCreate: 0, cacheRead: 0);
        tokenRepository.Add(new TokenLog
        {
            AgentName       = agentName,
            Model           = model,
            InputTokens     = response.InputTokens,
            OutputTokens    = response.OutputTokens,
            TotalCostUsd    = cost,
            IsEstimated     = isEstimated,
            CreatedAt       = DateTime.UtcNow,
            // Stage 82 子項 2：AsyncLocal 透傳 — Petra 4 LLM call site 包 BeginPetraSessionScope 時填入 / 其他 caller default null（對齊既有行為）
            PetraSessionId  = PetraSessionAmbient.Value,
        });
        await tokenRepository.SaveAsync(cancellationToken);

        // 通知 Dashboard 即時重整 Token 頁面
        await dashboardPush.PushTokenUpdateAsync();

        return response;
    }
}
