using System.Text.Json;
using AiTeam.Bot.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AiTeam.Bot.Orchestration.Petra.Spike;

// Stage 63A 動態決策 API spike — throwaway prototype（0 DI 註冊 / 0 production wire / Stage 63B 全砍重寫）。
//
// Phase 1 nuget xml doc grep + Phase 2 真打 Gemini Flash 揭露 3 finding：
// 1. Charter 候選 `MagenticOrchestrator<TState>` 不存在 — 真實 pattern = GroupChatManager + AgentWorkflowBuilder.CreateGroupChatBuilderWith
// 2. 跨 assembly subclass GroupChatManager 寫 `protected override`（不是 `protected internal override` — 詳 spike notes 第 1 段）
// 3. ⚠️ Framework limitation 兩條（spike 揭露 — 詳 spike notes 第 2 段）：
//    (a) base GroupChatManager subclass 透過 CreateGroupChatBuilderWith 建構的 workflow，manager.SelectNextAgentAsync 0 invoke
//        — GroupChatHost executor 1 superstep 結束（nuget 1.3.0 stable 對 base subclass 未啟動 manager loop）
//    (b) base AIAgent subclass 不被 framework Workflow dispatch — BuildSequential 中 ExecutorInvoked 但 RunCoreAsync/RunCoreStreamingAsync 0 invoke
//        → Stage 63B 必走 ChatClientAgent(IChatClient, ...) ctor + 寫 IChatClient adapter wrap Worker 既有 ClaudeCodeService
//
// Spike 適應實作：Petra 一次性 LLM 決策 agent 序列（DecideAsync）+ BuildSequential — 動態決策 capability 透過 ILlmProvider 直接路徑實證。
internal static class PetraSpikePrototype
{
    public static async Task<SpikeRunLog> RunScenarioAsync(string scenarioInput, ILlmProvider petraProvider, CancellationToken ct = default)
    {
        var log = new SpikeRunLog(scenarioInput);
        var cody = new MockWorkerAgent("Cody", input => $"Cody: 已實作「{Trim(input, 40)}」（mock fixture）", log);
        var vera = new MockWorkerAgent("Vera", input => $"Vera: review pass for「{Trim(input, 40)}」（mock fixture）", log);
        var agents = new AIAgent[] { cody, vera };

        // Petra LLM 動態決策（核心命題）— 看任務規模 + trigger 條件 prompt 真實分流不同序列。
        var sequence = await DecideAsync(scenarioInput, agents, petraProvider, log, ct);

        // 建 workflow + 跑（揭 framework limitation (b) — workflow 結束但 agent.RunCoreAsync 不 invoke / Stage 63B IChatClient adapter 解決）
        var workflow = AgentWorkflowBuilder.BuildSequential(sequence);
        var initial = new ChatMessage(ChatRole.User, scenarioInput);
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, initial, cancellationToken: ct);
        await foreach (var ev in run.WatchStreamAsync().WithCancellation(ct))
        {
            log.Events.Add(ev.GetType().Name);
            if (ev is WorkflowOutputEvent) break;
        }
        return log;
    }

    private static async Task<List<AIAgent>> DecideAsync(
        string scenarioInput, IReadOnlyList<AIAgent> agents, ILlmProvider provider, SpikeRunLog log, CancellationToken ct)
    {
        var roster = string.Join(", ", agents.Select(a => a.Name));
        var sysPrompt = $$"""
你是 Petra — Multi-Agent Orchestrator。依以下 trigger 條件動態決定 agent 序列（用 | 分隔）：
- 1-on-1 trigger（純技術改動 < 50 行 / typo / 文件配置）→ 回「Cody」
- Design trigger（跨 3-5 元件 / Issue ≥ 5）→ 回「Cody|Vera」
- Kickoff trigger（架構決策 / 跨多領域）→ 回「Cody|Vera|Cody|Vera」

可選 agent：{{roster}}
**只回 agent name 序列**（不要解釋，例如：Cody|Vera）
""";
        var response = await provider.CompleteAsync(sysPrompt, $"任務：{scenarioInput}", ct);
        var raw = response.Content.Trim().Split('\n')[0].Trim();
        log.PetraDecisions.AddRange(raw.Split('|').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)));
        var picks = log.PetraDecisions
            .Select(n => agents.FirstOrDefault(a => a.Name?.Equals(n, StringComparison.OrdinalIgnoreCase) == true))
            .Where(a => a is not null).Cast<AIAgent>().ToList();
        return picks.Count > 0 ? picks : new() { agents[0] };
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max] + "...";
}

// Mock Worker AIAgent — 純 fixture 回應（0 LLM）。⚠️ framework limitation (b)：RunCoreAsync/RunCoreStreamingAsync 在
// BuildSequential workflow 中 0 invoke — Stage 63B IChatClient adapter 路徑解決。
internal sealed class MockWorkerAgent(string name, Func<string, string> fixture, SpikeRunLog log) : AIAgent
{
    public override string? Name { get; } = name;

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages, AgentSession? thread, AgentRunOptions? options, CancellationToken cancellationToken)
    {
        var lastUserMsg = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        var reply = fixture(lastUserMsg);
        log.WorkerCalls.Add($"{name} (Run): {reply}");
        return Task.FromResult(new AgentResponse(new ChatMessage(ChatRole.Assistant, reply)) { AgentId = Id });
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages, AgentSession? thread, AgentRunOptions? options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var reply = fixture(messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "");
        log.WorkerCalls.Add($"{name} (Stream): {reply}");
        yield return new AgentResponseUpdate(ChatRole.Assistant, reply) { AgentId = Id };
        await Task.CompletedTask;
    }

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken ct)
        => new(new SpikeStatelessSession());

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(JsonElement s, JsonSerializerOptions? o, CancellationToken ct)
        => new(new SpikeStatelessSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(AgentSession s, JsonSerializerOptions? o, CancellationToken ct)
        => new(JsonDocument.Parse("{}").RootElement);

    private sealed class SpikeStatelessSession : AgentSession { }
}

internal sealed class SpikeRunLog(string scenarioInput)
{
    public string Scenario { get; } = scenarioInput;
    public List<string> PetraDecisions { get; } = new();
    public List<string> WorkerCalls { get; } = new();
    public List<string> Events { get; } = new();
}
