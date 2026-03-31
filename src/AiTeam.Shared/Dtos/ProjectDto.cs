namespace AiTeam.Shared.Dtos;

/// <summary>專案 DTO。</summary>
public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? RepoUrl { get; set; }
    public string? TechStack { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TeamName { get; set; } = "";
    public int TaskCount { get; set; }
}
