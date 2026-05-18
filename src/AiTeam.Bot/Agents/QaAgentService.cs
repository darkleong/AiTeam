using AiTeam.Bot.Orchestration.Petra;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI;

namespace AiTeam.Bot.Agents;

/// <summary>
/// QA Worker（Quinn）：v5.5 production active 6 Talent 之一。
/// 走 Claude Code CLI subprocess：.cs 變更 → xUnit + NSubstitute + FluentAssertions；.razor / .css → Playwright 視覺截圖測試。
/// Stage 63B：加 IAgentTool 介面（v5 動態架構 PoC — Petra Orchestrator 動態 dispatch 用）。
/// Stage 78a：v4 IAgentExecutor.ExecuteTaskAsync + 所有 v4 helper 砍 — 純 v5.5 IAgentTool 實作（Petra orchestrator 透過 ClaudeCodeChatClientAdapter 走 Claude Code CLI subprocess）。
/// </summary>
[AgentCapability("qa_testing")]
public class QaAgentService(
    IClaudeCodeService claudeCodeService,
    TokenLogService tokenLogService,
    ILoggerFactory loggerFactory) : IAgentTool
{
    public string Name => "Quinn";
    public IReadOnlyList<string> Capabilities { get; } = PetraWorkerHelper.GetCapabilities<QaAgentService>();
    public AIAgent CreateAgent(PetraSessionContext ctx)
        => PetraWorkerHelper.BuildAgent(claudeCodeService, "qa_testing", "Quinn",
            "你是 Quinn — QA Testing Worker。負責執行測試、產出測試報告。", ctx, tokenLogService, loggerFactory);
}
