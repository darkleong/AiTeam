using AiTeam.Bot.Orchestration.Petra;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Dev Worker（Cody）：v5.5 production active 6 Talent 之一。
/// Stage 11：核心執行層從「Claude API 一次性產出」升級為「Claude Code CLI 自主開發」。
/// Stage 63B：加 IAgentTool 介面（v5 動態架構 PoC — Petra Orchestrator 動態 dispatch 用）。
/// Stage 78a：v4 IAgentExecutor.ExecuteTaskAsync + BuildPlanAsync + ExecuteAsync + RunClaudeCodeAsync + 所有 v4 helper 砍（~990 行刪 / BlockedOperationException 一併砍 — 0 v5.5 caller）。純 v5.5 IAgentTool 實作（Petra orchestrator 透過 ClaudeCodeChatClientAdapter 走 Claude Code CLI subprocess）。
/// </summary>
[AgentCapability("code_implementation")]
public class DevAgentService(
    IClaudeCodeService claudeCodeService,
    TokenLogService tokenLogService,
    ILoggerFactory loggerFactory) : IAgentTool
{
    public string Name => "Cody";
    public IReadOnlyList<string> Capabilities { get; } = PetraWorkerHelper.GetCapabilities<DevAgentService>();
    public AIAgent CreateAgent(PetraSessionContext ctx)
        => PetraWorkerHelper.BuildAgent(claudeCodeService, "code_implementation", "Cody",
            "你是 Cody — Code Implementation Worker。負責依任務 input 寫程式碼。", ctx, tokenLogService, loggerFactory);
}
