namespace AiTeam.Data;

public class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Project> Projects { get; set; } = [];
    public ICollection<AgentConfig> Agents { get; set; } = [];
    public ICollection<TaskItem> Tasks { get; set; } = [];
}

public class Project
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string Name { get; set; } = "";
    public string? RepoUrl { get; set; }
    public string? TechStack { get; set; } // JSONB
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Team Team { get; set; } = null!;
    public ICollection<TaskItem> Tasks { get; set; } = [];
}

public class AgentConfig
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string Name { get; set; } = ""; // CEO / Dev / Ops / QA / Doc / Requirements
    public string Description { get; set; } = ""; // CEO 系統提示用描述
    public int TrustLevel { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public string? DiscordChannelId { get; set; } // Discord 頻道 ID（ulong 存為字串）
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Team Team { get; set; } = null!;
}

public class TaskItem
{
    public Guid Id { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; } // CEO 任務描述（供 Agent 使用）
    public string TriggeredBy { get; set; } = ""; // Discord / GitHub / Schedule
    public string AssignedAgent { get; set; } = "";
    public string Status { get; set; } = "pending"; // pending / running / done / failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Team? Team { get; set; }
    public Project? Project { get; set; }
    public ICollection<TaskLog> Logs { get; set; } = [];
}

public class TaskLog
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string Agent { get; set; } = "";
    public string Step { get; set; } = "";
    public string Status { get; set; } = "pending"; // pending / running / done / failed
    public string? Payload { get; set; } // JSONB
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TaskItem Task { get; set; } = null!;
}

/// <summary>動態系統設定（key/value），可從 Dashboard 即時修改，免重啟 Bot。</summary>
public class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Rule
{
    public Guid Id { get; set; }
    public Guid? TeamId { get; set; }
    public string Content { get; set; } = "";
    /// <summary>null = 全域規則；有值 = 僅套用到指定 Agent</summary>
    public string? AgentName { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Team? Team { get; set; }
}
