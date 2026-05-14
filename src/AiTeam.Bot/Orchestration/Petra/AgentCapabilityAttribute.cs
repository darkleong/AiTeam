namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：標註 Worker AgentService 提供的 capability tag（v5 動態架構 PoC）。
/// Petra Orchestrator 透過 reflection 取得 Worker capabilities，動態決策序列。
/// AllowMultiple=true — 例如 Sage 可同時擁有 documentation + release_publishing 兩 capability（Stage 63B 暫採一對一，留彈性）。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class AgentCapabilityAttribute(string capability) : Attribute
{
    public string Capability { get; } = capability;
}
