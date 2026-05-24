namespace AiTeam.Shared.Dtos;

/// <summary>
/// Stage 87 A3：Talent 設定 DTO（取代 Stage 38 AgentConfigDto / v4 collapse 最後殘留收口）。
///
/// Dashboard TALENTS 分頁 Provider/Model + Token Limit 編輯 UI 用。
/// 對齊 talents 表 schema（Stage 67 baseline + Stage 87 加 DailyTokenLimitK + MonthlyTokenLimitK）。
/// </summary>
public class TalentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Stage 67：Talent 預設 LLM Provider（null = runtime fallback Agents:Petra:Provider）。</summary>
    public string? Provider { get; set; }
    /// <summary>Stage 67：Talent 預設 LLM Model（null = runtime fallback Agents:Petra:Model）。</summary>
    public string? Model { get; set; }
    /// <summary>Stage 87：Talent 日 Token 上限（千 token）。null = DB 未設定，runtime fallback appsettings。</summary>
    public int? DailyTokenLimitK { get; set; }
    /// <summary>Stage 87：Talent 月 Token 上限（千 token）。null = DB 未設定，runtime fallback appsettings。</summary>
    public int? MonthlyTokenLimitK { get; set; }
    public bool IsActive { get; set; }
}
