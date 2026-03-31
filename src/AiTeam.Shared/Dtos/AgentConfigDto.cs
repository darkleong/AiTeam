namespace AiTeam.Shared.Dtos;

/// <summary>Agent 設定 DTO（含信任等級與 Notion 規則）。</summary>
public class AgentConfigDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int TrustLevel { get; set; }
    public bool IsActive { get; set; }
    public string? TeamName { get; set; }
    /// <summary>來自 Notion 的規則清單，不存 DB。</summary>
    public IReadOnlyList<string> Rules { get; set; } = [];
}
