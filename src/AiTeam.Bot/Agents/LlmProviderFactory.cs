using Anthropic.SDK;
using AiTeam.Bot.Services;
using AiTeam.Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BotAgentSettings = AiTeam.Bot.Configuration.AgentSettings;
using BotAgentConfig   = AiTeam.Bot.Configuration.AgentConfig;

namespace AiTeam.Bot.Agents;

/// <summary>
/// 根據 Agent 設定建立對應的 ILlmProvider 實作。
/// 新增供應商只需在此加一個 case，不需動 Agent 核心邏輯。
/// 自動包裝 TokenTrackingProvider，透明地記錄每次呼叫的 Token 用量並推送即時通知，
/// 並在呼叫前進行 Token 守門（單次/日限/月限/全域月限）。
/// Stage 17：支援 MockMode，啟用時直接回傳 MockLlmProvider（有意跳過 TokenTrackingProvider，
/// 避免假的 Token 統計資料污染 Dashboard 監控頁）。
/// Stage 37-1：新增 GEMINI 分支（FF 四第一階段：API 層 Agent 可選 Google Gemini Flash）。
/// Stage 38：Provider / Model 改為 DB 優先（AgentConfigCache），appsettings 僅作 seed + fallback；
/// Token 限額（DailyTokenLimitK / MonthlyTokenLimitK）仍讀 appsettings 的 BotAgentConfig。
/// </summary>
public class LlmProviderFactory(
    AnthropicClient anthropicClient,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IOptions<BotAgentSettings> settings,
    TokenRepository tokenRepository,
    DashboardPushService dashboardPush,
    DiscordAlertService discordAlert,
    ILogger<TokenTrackingProvider> tokenLogger,
    ILoggerFactory loggerFactory,
    AppSettingsService appSettings,
    AgentConfigCache agentConfigCache,
    TokenCostEstimator costEstimator)
{
    private readonly BotAgentSettings _settings = settings.Value;
    private readonly string _geminiApiKey = configuration["Gemini:ApiKey"] ?? "";

    /// <summary>
    /// 依 Agent 名稱（CEO / Dev / Ops）建立對應的 Provider，並自動包裝 Token 追蹤與守門。
    /// MockMode 啟用時回傳 MockLlmProvider（不包裝 TokenTrackingProvider）。
    /// Stage 38：Provider / Model 優先讀 DB（AgentConfigCache），null 時 fallback appsettings。
    /// </summary>
    public ILlmProvider Create(string agentName)
    {
        // MockMode：有意不包裝 TokenTrackingProvider，避免假統計資料
        if (appSettings.GetBoolAsync("MockMode", false).GetAwaiter().GetResult())
            return new MockLlmProvider(appSettings);

        if (!_settings.Agents.TryGetValue(agentName, out var config))
            throw new InvalidOperationException($"找不到 Agent 設定：{agentName}");

        // Stage 38：DB override 優先，null 欄位 fallback appsettings（per-field）
        var (dbProvider, dbModel, _, _) = agentConfigCache.Get(agentName);
        var finalProvider = dbProvider ?? config.Provider;
        var finalModel    = dbModel    ?? config.Model;

        var inner = finalProvider.ToUpperInvariant() switch
        {
            "ANTHROPIC" => (ILlmProvider)new AnthropicProvider(anthropicClient, finalModel),
            "GEMINI"    => (ILlmProvider)new GeminiProvider(
                               httpClientFactory.CreateClient("Gemini"),
                               _geminiApiKey,
                               finalModel,
                               loggerFactory.CreateLogger<GeminiProvider>()),
            _ => throw new NotSupportedException($"不支援的 LLM Provider：{finalProvider}（Agent={agentName}）")
        };

        // Token log 要記實際用的 model（finalModel），不可傳 config.Model — 否則 Dashboard 改完後 Token 監控頁顯示舊 model
        return new TokenTrackingProvider(
            inner,
            tokenRepository,
            dashboardPush,
            discordAlert,
            _settings,
            config,
            appSettings,
            agentConfigCache,
            costEstimator,
            tokenLogger,
            agentName,
            finalModel);
    }
}
