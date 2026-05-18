using AiTeam.Bot.Orchestration.Petra;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Reviewer Worker（Vera）：v5.5 production active 6 Talent 之一。
/// Stage 16 重構：從「LLM 逐檔呼叫 + 獨立 Claude Code 影響分析」改為單一 Claude Code session（RunReviewAsync）。
/// Stage 63B：加 IAgentTool 介面（v5 動態架構 PoC — Petra Orchestrator 動態 dispatch 用）。
/// Stage 78a：v4 IAgentExecutor.ExecuteTaskAsync + 所有 v4 helper 砍 — 純 v5.5 IAgentTool 實作（Petra orchestrator 透過 ClaudeCodeChatClientAdapter 走 Claude Code CLI subprocess）。
/// </summary>
[AgentCapability("code_review")]
public class ReviewerAgentService(
    IClaudeCodeService claudeCodeService,
    TokenLogService tokenLogService,
    ILoggerFactory loggerFactory) : IAgentTool
{
    public string Name => "Vera";
    public IReadOnlyList<string> Capabilities { get; } = PetraWorkerHelper.GetCapabilities<ReviewerAgentService>();
    public AIAgent CreateAgent(PetraSessionContext ctx)
        => PetraWorkerHelper.BuildAgent(claudeCodeService, "code_review", "Vera",
            "你是 Vera — Code Review Worker。負責審查程式碼變更，發現問題與改進機會。", ctx, tokenLogService, loggerFactory);
}
