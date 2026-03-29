using Anthropic.SDK;
using AiTeam.Bot.Configuration;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Agents;

/// <summary>
/// 根據 Agent 設定建立對應的 ILlmProvider 實作。
/// 新增供應商只需在此加一個 case，不需動 Agent 核心邏輯。
/// </summary>
public class LlmProviderFactory(
    AnthropicClient anthropicClient,
    IOptions<AgentSettings> settings)
{
    private readonly AgentSettings _settings = settings.Value;

    /// <summary>
    /// 依 Agent 名稱（CEO / Dev / Ops）建立對應的 Provider。
    /// </summary>
    public ILlmProvider Create(string agentName)
    {
        if (!_settings.Agents.TryGetValue(agentName, out var config))
            throw new InvalidOperationException($"找不到 Agent 設定：{agentName}");

        return config.Provider.ToUpperInvariant() switch
        {
            "ANTHROPIC" => new AnthropicProvider(anthropicClient, config.Model),
            _ => throw new NotSupportedException($"不支援的 LLM Provider：{config.Provider}")
        };
    }
}
