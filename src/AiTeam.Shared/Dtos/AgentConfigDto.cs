namespace AiTeam.Shared.Dtos;

/// <summary>Agent 設定 DTO（含信任等級）。</summary>
public class AgentConfigDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int TrustLevel { get; set; }
    public bool IsActive { get; set; }
    public string? TeamName { get; set; }
    /// <summary>Stage 38：LLM Provider（"Anthropic" / "Gemini"）。null = 尚未設定（sentinel 狀態，啟動 seed 後應不會為 null）。</summary>
    public string? Provider { get; set; }
    /// <summary>Stage 38：Model 名稱。null = 尚未設定。</summary>
    public string? Model { get; set; }
    /// <summary>Stage 47：Agent 日 Token 上限（千 token）。null = DB 未設定，runtime fallback appsettings。</summary>
    public int? DailyTokenLimitK { get; set; }
    /// <summary>Stage 47：Agent 月 Token 上限（千 token）。null = DB 未設定，runtime fallback appsettings。</summary>
    public int? MonthlyTokenLimitK { get; set; }
}
