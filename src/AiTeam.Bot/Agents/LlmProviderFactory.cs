using Anthropic.SDK;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Services;
using AiTeam.Data.Repositories;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Agents;

/// <summary>
/// 根據 Agent 設定建立對應的 ILlmProvider 實作。
/// 新增供應商只需在此加一個 case，不需動 Agent 核心邏輯。
/// 自動包裝 TokenTrackingProvider，透明地記錄每次呼叫的 Token 用量並推送即時通知。
/// Stage 17：支援 MockMode，啟用時直接回傳 MockLlmProvider（有意跳過 TokenTrackingProvider，
/// 避免假的 Token 統計資料污染 Dashboard 監控頁）。
/// </summary>
public class LlmProviderFactory(
    AnthropicClient anthropicClient,
    IOptions<AgentSettings> settings,
    TokenRepository tokenRepository,
    DashboardPushService dashboardPush,
    AppSettingsService appSettings)
{
    private readonly AgentSettings _settings = settings.Value;

    /// <summary>
    /// 依 Agent 名稱（CEO / Dev / Ops）建立對應的 Provider，並自動包裝 Token 追蹤。
    /// MockMode 啟用時回傳 MockLlmProvider（不包裝 TokenTrackingProvider）。
    /// </summary>
    public ILlmProvider Create(string agentName)
    {
        // MockMode：有意不包裝 TokenTrackingProvider，避免假統計資料
        if (appSettings.GetBoolAsync("MockMode", false).GetAwaiter().GetResult())
            return new MockLlmProvider();

        if (!_settings.Agents.TryGetValue(agentName, out var config))
            throw new InvalidOperationException($"找不到 Agent 設定：{agentName}");

        var inner = config.Provider.ToUpperInvariant() switch
        {
            "ANTHROPIC" => (ILlmProvider)new AnthropicProvider(anthropicClient, config.Model),
            _ => throw new NotSupportedException($"不支援的 LLM Provider：{config.Provider}")
        };

        return new TokenTrackingProvider(inner, tokenRepository, dashboardPush, agentName, config.Model);
    }
}
