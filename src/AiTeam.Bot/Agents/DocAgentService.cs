using AiTeam.Bot.Orchestration.Petra;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Documentation Worker（Sage）：v5.5 production active 6 Talent 之一。
/// Stage 63B：加 IAgentTool 介面（v5 動態架構 PoC — Petra Orchestrator 動態 dispatch 用）。
/// Stage 78a：v4 IAgentExecutor.ExecuteTaskAsync + 所有 v4 helper 砍 — 純 v5.5 IAgentTool 實作（Petra orchestrator 透過 ClaudeCodeChatClientAdapter 走 Claude Code CLI subprocess）。
/// </summary>
[AgentCapability("documentation")]
public class DocAgentService(
    IClaudeCodeService claudeCodeService,
    TokenLogService tokenLogService,
    ILoggerFactory loggerFactory) : IAgentTool
{
    public string Name => "Sage";
    public IReadOnlyList<string> Capabilities { get; } = PetraWorkerHelper.GetCapabilities<DocAgentService>();
    public AIAgent CreateAgent(PetraSessionContext ctx)
        => PetraWorkerHelper.BuildAgent(claudeCodeService, "documentation", "Sage",
            "你是 Sage — Documentation Worker。負責產出與更新文件、歸檔紀錄。", ctx, tokenLogService, loggerFactory);
}
