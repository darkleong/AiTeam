using Microsoft.Agents.AI;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：v5 動態架構 PoC — Worker 對 Petra Orchestrator 暴露的 Tool 介面（路線 A factory pattern）。
///
/// 路線 A 設計目的：CreateAgent 內部建 ClaudeCodeChatClientAdapter + 包 ChatClientAgent return AIAgent
/// — 解 Stage 63A 揭限制 (b)（base AIAgent subclass 不被 framework workflow dispatch → ChatClientAgent ctor path 解決）。
///
/// Petra Orchestrator 透過 IEnumerable&lt;IAgentTool&gt; DI scan 取得所有 Worker，
/// 動態決策後對 picks 呼叫 CreateAgent → BuildSequential workflow 跑。
/// </summary>
public interface IAgentTool
{
    /// <summary>Worker 名稱（"Cody" / "Vera" / "Quinn" / "Sage" / "Rosa" / "Demi" / "Release"）。</summary>
    string Name { get; }

    /// <summary>Worker 提供的 capability tags（從 [AgentCapability] attribute reflection 取）。</summary>
    IReadOnlyList<string> Capabilities { get; }

    /// <summary>
    /// Factory：建 AIAgent 給 Petra BuildSequential workflow dispatch 用。
    /// 內部建 ClaudeCodeChatClientAdapter(capability 對應 IClaudeCodeService method) + 包 ChatClientAgent。
    /// </summary>
    AIAgent CreateAgent(PetraSessionContext ctx);
}
