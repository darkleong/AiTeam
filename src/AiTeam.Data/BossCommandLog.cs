namespace AiTeam.Data;

/// <summary>
/// Stage 29-5：老闆從 Dashboard 下達的指令記錄。
/// 語意與 BossInteraction 相反——BossInteraction 是「等待老闆回應」，
/// BossCommandLog 是「老闆主動發起」，未來亦可用於追溯 Discord 端的指令。
/// </summary>
public class BossCommandLog
{
    public Guid     Id             { get; set; }
    public string   Text           { get; set; } = "";
    /// <summary>圖片附件序列化 JSON（ImageLogDto[]），格式：[{base64Data, mediaType}]。</summary>
    public string?  Images         { get; set; }
    /// <summary>來源：dashboard（本功能）或 discord（未來擴充）。</summary>
    public string   Source         { get; set; } = "dashboard";
    /// <summary>Victoria 回應原文，供追溯診斷。</summary>
    public string?  CeoResponseRaw { get; set; }
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;
}
