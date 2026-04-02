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
}
