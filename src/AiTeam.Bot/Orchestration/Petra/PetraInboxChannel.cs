using System.Threading.Channels;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 77：v5.5 Phase 3 補強 — PetraInbox rowId Bounded Channel queue（Singleton wrapper）。
///
/// 對齊業界 BackgroundService + Channel 紀律（Microsoft Learn Channels + 7 議題 WebSearch 結論）：
/// - <c>FullMode = Wait</c>（default 推薦 / production safest / 0 task drop / producer 自然 backpressure）
/// - <c>Capacity = 20</c>（AiTeam 單 Bot 個人場景 / Tier 1-2 帳號 / 配 MaxConcurrentPetra=3 + 5-6 row buffer）
/// - <c>SingleWriter = true</c>（PetraInboxProcessor 唯一 producer / 對齊 polling 紀律）
/// - <c>SingleReader = false</c>（PetraDispatchWorker N consumer / multi-consumer 並行 pickup）
///
/// 紀律延續：Channel 是 process-wide queue / Singleton lifetime / Bot 重啟自然清空（petra_inbox table 才是 persistent SoT）。
/// </summary>
public class PetraInboxChannel
{
    private const int CapacityDefault = 20;

    private readonly Channel<Guid> _channel;

    public PetraInboxChannel(ILogger<PetraInboxChannel> logger)
    {
        var options = new BoundedChannelOptions(CapacityDefault)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false,
        };
        _channel = Channel.CreateBounded<Guid>(options);
        logger.LogInformation(
            "PetraInboxChannel 初始化 capacity={Capacity} fullMode=Wait singleWriter=true singleReader=false",
            CapacityDefault);
    }

    public ChannelWriter<Guid> Writer => _channel.Writer;
    public ChannelReader<Guid> Reader => _channel.Reader;
}
