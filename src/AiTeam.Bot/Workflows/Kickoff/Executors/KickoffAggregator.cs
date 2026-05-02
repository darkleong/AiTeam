using System.Text;
using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Kickoff.Executors;

/// <summary>
/// Stage 50：Kickoff Meeting fan-in barrier Aggregator。
/// 收 4 個 KickoffAgentOutput（Rosa/Demi/Cody/Quinn），count==4 時 SendMessageAsync 顯式送 KickoffRoundCollected 給 PetraExecutor。
///
/// 設計（對齊 microsoft/agent-framework MapReduce sample Shuffler pattern）：
///   - Executor instance per Workflow build（factory 模式 new 一次） → _bucket 跨 superstep 持有 OK，不會跨 TaskGroup 汙染
///   - framework AddFanInBarrierEdge 是序列化 deliver（對齊 MapReduce sample 用 List 而非 ConcurrentList 證實）→ Dictionary 即可，不需 ConcurrentDictionary
///   - loop back 時 round 變了 → 用 _expectedRound 比對自動 reset bucket
///   - 累積完成後同步 append meeting log（state.MeetingLog 含 4 Agent 段）+ SaveAsync 寫框架 state
/// </summary>
internal sealed class KickoffAggregator : Executor<KickoffAgentOutput>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KickoffAggregator> _logger;

    private readonly Dictionary<string, string> _bucket = new();
    private int _expectedRound = 0;

    public KickoffAggregator(
        IServiceScopeFactory scopeFactory,
        ILogger<KickoffAggregator> logger)
        : base("Kickoff-Aggregator")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask HandleAsync(
        KickoffAgentOutput msg, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // round 變了（loop back）→ reset bucket
        if (msg.Round != _expectedRound)
        {
            _bucket.Clear();
            _expectedRound = msg.Round;
        }

        _bucket[msg.AgentKey] = msg.Output;

        if (_bucket.Count < 4) return;

        // 累積完成（含 4 Agent）
        var collected = new KickoffRoundCollected
        {
            Round = msg.Round,
            Rosa  = _bucket.GetValueOrDefault("Rosa", ""),
            Demi  = _bucket.GetValueOrDefault("Demi", ""),
            Cody  = _bucket.GetValueOrDefault("Cody", ""),
            Quinn = _bucket.GetValueOrDefault("Quinn", ""),
        };

        // append meeting log（讀 framework state，append 4 Agent 段）
        var state = await KickoffStateHelpers.ReadAsync(context);
        var sb = new StringBuilder(state.MeetingLog);
        sb.AppendLine($"## Round {msg.Round}").AppendLine();
        sb.AppendLine("### Rosa（需求分析）").AppendLine(collected.Rosa).AppendLine();
        sb.AppendLine("### Demi（UI/UX 設計）").AppendLine(collected.Demi).AppendLine();
        sb.AppendLine("### Cody（技術可行性）").AppendLine(collected.Cody).AppendLine();
        sb.AppendLine("### Quinn（測試規劃）").AppendLine(collected.Quinn).AppendLine();
        state.MeetingLog = sb.ToString();
        await KickoffStateHelpers.SaveAsync(context, state);

        _bucket.Clear();
        _logger.LogInformation("[Stage50] Aggregator round {Round} 收齊 4 Agent，broadcast → Petra", msg.Round);

        // 用 SendMessageAsync 顯式送下游（對齊 MapReduce sample Shuffler pattern）
        await context.SendMessageAsync(collected, cancellationToken: cancellationToken);
    }
}
