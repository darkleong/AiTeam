using System.Text.Json;
using AiTeam.Bot.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AiTeam.Bot.Orchestration.Petra.Spike;

// Stage 63A 動態決策 API spike — throwaway prototype（0 DI 註冊 / 0 production wire / Stage 63B 全砍重寫）。
// Charter 候選 MagenticOrchestrator<TState> 不存在於 nuget 1.3.0 → 真實 pattern = GroupChatManager.SelectNextAgentAsync override。
internal static class PetraSpikePrototype
{
    public static async Task<SpikeRunLog> RunScenarioAsync(string scenarioInput, ILlmProvider petraProvider, CancellationToken ct = default)
    {
        var log = new SpikeRunLog(scenarioInput);
        var cody = new MockWorkerAgent("Cody", input => $"Cody: 已實作「{Trim(input, 40)}」（mock fixture）", log);
        var vera = new MockWorkerAgent("Vera", input => $"Vera: review pass for「{Trim(input, 40)}」（mock fixture）", log);
        var agents = new[] { (AIAgent)cody, vera };

        var workflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(_ => new PetraSpikeGroupChatManager(petraProvider, agents, log, ct))
            .AddParticipants(agents)
            .WithName("PetraSpike")
            .Build();

        var initialMessages = new List<ChatMessage> { new(ChatRole.User, scenarioInput) };
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, initialMessages, cancellationToken: ct);
        await foreach (var ev in run.WatchStreamAsync().WithCancellation(ct))
        {
            if (ev is WorkflowOutputEvent) break;
        }
        return log;
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max] + "...";
}

// Petra 動態決策 — Gemini LLM 看 history + agents 名單，回「下一個 agent name」或「DONE」。
internal sealed class PetraSpikeGroupChatManager(
    ILlmProvider provider,
    IReadOnlyList<AIAgent> agents,
    SpikeRunLog log,
    CancellationToken hostCt) : GroupChatManager
{
    private const int MaxTurns = 8;

    protected override async ValueTask<AIAgent> SelectNextAgentAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken)
    {
        var roster = string.Join(", ", agents.Select(a => a.Name));
        var transcript = string.Join("\n", history.Select(m => $"[{m.Role.Value}] {Truncate(m.Text, 200)}"));
        var sysPrompt = $$"""
你是 Petra — Multi-Agent Orchestrator。依以下 trigger 條件動態選下一個 agent，**只回 agent name 或 DONE**（不要解釋）：
- 1-on-1 trigger（純技術改動 < 50 行 / typo / 文件配置）→ 跳 Vera 直接派 Cody → DONE
- Design trigger（跨 3-5 元件 / Issue ≥ 5）→ Cody → Vera → DONE
- Kickoff trigger（架構決策 / 跨多領域）→ 多輪 Cody → Vera → Cody → Vera → DONE

可選 agent：{{roster}}, DONE
""";
        var userPrompt = $"當前 history：\n{transcript}\n\n下一個 agent name（或 DONE）？";
        var response = await provider.CompleteAsync(sysPrompt, userPrompt, cancellationToken);
        var pick = response.Content.Trim().Split('\n')[0].Trim().TrimEnd('.', ',', ';');
        log.PetraDecisions.Add(pick);

        if (pick.Equals("DONE", StringComparison.OrdinalIgnoreCase))
        {
            log.PetraTerminated = true;
            return agents[0];
        }
        var match = agents.FirstOrDefault(a => a.Name?.Equals(pick, StringComparison.OrdinalIgnoreCase) == true);
        return match ?? agents[0];
    }

    protected override ValueTask<bool> ShouldTerminateAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken)
        => new(log.PetraTerminated || log.PetraDecisions.Count >= MaxTurns);

    protected override ValueTask<IEnumerable<ChatMessage>> UpdateHistoryAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken)
        => new(history);

    private static string Truncate(string s, int max) => string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "...";
}

// Mock Worker AIAgent — 純 fixture 回應（不打 LLM）。Spike 不用 session，3 個 session core 方法 throw（spike 走 stateless）。
internal sealed class MockWorkerAgent(string name, Func<string, string> fixture, SpikeRunLog log) : AIAgent
{
    public override string? Name { get; } = name;

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages, AgentSession? thread, AgentRunOptions? options, CancellationToken cancellationToken)
    {
        var lastUserMsg = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        var reply = fixture(lastUserMsg);
        log.WorkerCalls.Add($"{name}: {reply}");
        return Task.FromResult(new AgentResponse(new ChatMessage(ChatRole.Assistant, reply)) { AgentId = Id });
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages, AgentSession? thread, AgentRunOptions? options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var resp = await RunCoreAsync(messages, thread, options, cancellationToken);
        yield return new AgentResponseUpdate(ChatRole.Assistant, resp.Text) { AgentId = Id };
    }

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken)
        => new(new SpikeStatelessSession());

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(JsonElement serializedSession, JsonSerializerOptions? jsonSerializerOptions, CancellationToken cancellationToken)
        => new(new SpikeStatelessSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(AgentSession session, JsonSerializerOptions? jsonSerializerOptions, CancellationToken cancellationToken)
        => new(JsonDocument.Parse("{}").RootElement);

    private sealed class SpikeStatelessSession : AgentSession { }
}

internal sealed class SpikeRunLog(string scenarioInput)
{
    public string Scenario { get; } = scenarioInput;
    public List<string> PetraDecisions { get; } = new();
    public List<string> WorkerCalls { get; } = new();
    public bool PetraTerminated { get; set; }
}
