using AiTeam.Bot.Agents;
using AiTeam.Bot.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：Worker IAgentTool wiring helper（v5 動態架構 PoC）— DRY 集中 reflection + ChatClientAgent ctor 樣板。
/// 7 Worker 不重複寫 capability scan / adapter build / ChatClientAgent ctor。
/// </summary>
internal static class PetraWorkerHelper
{
    /// <summary>從 class 上 [AgentCapability] attribute reflection 取 capability list。</summary>
    public static IReadOnlyList<string> GetCapabilities<T>() where T : class
        => typeof(T).GetCustomAttributes(typeof(AgentCapabilityAttribute), inherit: false)
            .Cast<AgentCapabilityAttribute>()
            .Select(a => a.Capability)
            .ToList();

    /// <summary>
    /// 建 ChatClientAgent 包裝 ClaudeCodeChatClientAdapter（路線 A — Stage 63A 限制 (b) workaround 必走）。
    /// 對齊 ChatClientAgent ctor 重載 1：(IChatClient, instructions, name, description, tools, ILoggerFactory, IServiceProvider)。
    ///
    /// Stage 72：加 optional `promptResolver` param — 非 null 時 adapter 走 DB SkillPrompt path（v5.5 path）/ null 時退既有 file fallback。
    /// </summary>
    public static AIAgent BuildAgent(
        IClaudeCodeService claudeCode,
        string capability,
        string workerName,
        string instructions,
        PetraSessionContext ctx,
        AiTeam.Bot.Services.TokenLogService tokenLogService,
        ILoggerFactory loggerFactory,
        PromptResolver? promptResolver = null)
    {
        var adapter = new ClaudeCodeChatClientAdapter(
            claudeCode,
            capability,
            workerName,
            ctx.Model,
            ctx.ApiKey,
            ctx.WorkingDir,
            tokenLogService,
            loggerFactory.CreateLogger<ClaudeCodeChatClientAdapter>(),
            promptResolver);

        return new ChatClientAgent(
            chatClient: adapter,
            instructions: instructions,
            name: workerName,
            description: capability,
            tools: null,
            loggerFactory: loggerFactory,
            services: null);
    }
}
