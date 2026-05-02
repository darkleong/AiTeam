using System.Text;
using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Design.Executors;

/// <summary>
/// Stage 52：Design Meeting fan-in barrier Aggregator（v4 漸進遷移第四步）。
///
/// 職責（對齊 Stage 50 KickoffAggregator pattern）：
///   - 收 4 個 DesignAgentOutput（Rosa/Demi/Cody/Quinn）
///   - count==4 時 SendMessageAsync 顯式送 DesignRoundCollected 給 DesignPetraExecutor
///   - loop back 時 round 變了 → 用 _expectedRound 比對自動 reset bucket
///   - 累積完成後 append round meeting log（建議補強 1：state.DemiSessionId is null 時跳過 Demi 段拼接，
///     對齊 legacy DesignMeetingService.cs:215-220 條件式 append）
///
/// Demi short-circuit 邊界：DesignAgentExecutor[Demi] short-circuit 時 Output="" 仍進 bucket，
/// barrier 仍滿足 4 個收齊；append meeting log 時依 state.DemiSessionId is null 判斷跳過 Demi 段。
/// </summary>
[SendsMessage(typeof(DesignRoundCollected))]
internal sealed partial class DesignAggregator : Executor<DesignAgentOutput>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DesignAggregator> _logger;

    private readonly Dictionary<string, string> _bucket = new();
    private int _expectedRound = 0;

    public DesignAggregator(
        IServiceScopeFactory scopeFactory,
        ILogger<DesignAggregator> logger)
        : base("Design-Aggregator")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask HandleAsync(
        DesignAgentOutput msg, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // round 變了（loop back）→ reset bucket
        if (msg.Round != _expectedRound)
        {
            _bucket.Clear();
            _expectedRound = msg.Round;
        }

        _bucket[msg.AgentKey] = msg.Output;

        if (_bucket.Count < 4) return;

        // 累積完成（含 4 Agent，Demi short-circuit 時 Output="" 仍進 bucket）
        var collected = new DesignRoundCollected
        {
            Round = msg.Round,
            Rosa  = _bucket.GetValueOrDefault("Rosa", ""),
            Demi  = _bucket.GetValueOrDefault("Demi", ""),
            Cody  = _bucket.GetValueOrDefault("Cody", ""),
            Quinn = _bucket.GetValueOrDefault("Quinn", ""),
        };

        // append round meeting log（state.DemiSessionId is null 時跳過 Demi 段，建議補強 1）
        var state = await DesignStateHelpers.ReadAsync(context);
        var sb = new StringBuilder(state.MeetingLog);
        sb.AppendLine("### Rosa（需求分析）").AppendLine(collected.Rosa).AppendLine();
        if (state.DemiSessionId is not null)
        {
            sb.AppendLine("### Demi（UI/UX 設計）").AppendLine(collected.Demi).AppendLine();
        }
        sb.AppendLine("### Cody（技術可行性）").AppendLine(collected.Cody).AppendLine();
        sb.AppendLine("### Quinn（測試規劃）").AppendLine(collected.Quinn).AppendLine();
        state.MeetingLog = sb.ToString();
        await DesignStateHelpers.SaveAsync(context, state);

        _bucket.Clear();
        _logger.LogInformation("[Stage52] Aggregator round {Round} 收齊 4 Agent（hasDemi={HasDemi}），broadcast → Petra",
            msg.Round, state.DemiSessionId is not null);

        await context.SendMessageAsync(collected, cancellationToken: cancellationToken);
    }
}
